"""Real public web discovery for DeepSeek's university-emblem tools."""
import asyncio
import html
import os
import json
import time
from pathlib import Path
import re
from html.parser import HTMLParser
from urllib.parse import parse_qs, urlsplit, quote

import httpx

USER_AGENT = 'AboutMeGame/0.1 (university emblem research)'


class IndexBusyError(ValueError):
    def __init__(self, response):
        super().__init__('public index temporarily busy')
        try:
            self.retry_after = min(10, max(5, float(response.headers.get('retry-after', '5'))))
        except ValueError:
            self.retry_after = 5


def index_json(response):
    response.raise_for_status()
    data = response.json()
    if data.get('error', {}).get('code') in ('maxlag', 'ratelimited', 'readonly'):
        raise IndexBusyError(response)
    if data.get('error'):
        raise ValueError('public index returned an API error')
    return data


def result_url(value):
    value = html.unescape(value)
    if value.startswith('//'):
        value = 'https:' + value
    parts = urlsplit(value)
    if parts.hostname and parts.hostname.endswith('duckduckgo.com'):
        value = parse_qs(parts.query).get('uddg', [''])[0]
    elif parts.path == '/url':
        value = parse_qs(parts.query).get('q', parse_qs(parts.query).get('url', ['']))[0]
    parts = urlsplit(value)
    if parts.scheme not in ('http', 'https') or not parts.hostname or parts.username or parts.password:
        return ''
    if any(parts.hostname == h or parts.hostname.endswith('.' + h) for h in
           ('google.com', 'duckduckgo.com', 'gstatic.com', 'youtube.com')):
        return ''
    return value


class SearchResults(HTMLParser):
    def __init__(self, google=False):
        super().__init__(); self.items = []; self.current = None; self.google = google

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag != 'a':
            return
        href = attrs.get('href', '')
        if 'result__a' not in attrs.get('class', '') and not (self.google and (href.startswith('/url?') or href.startswith('https://'))):
            return
        url = result_url(href)
        if url:
            self.current = {'url': url, 'title': ''}; self.items.append(self.current)

    def handle_data(self, value):
        if self.current:
            self.current['title'] += value.strip()[:200]

    def handle_endtag(self, tag):
        if tag == 'a':
            self.current = None


async def _html_search(client, query, google=False):
    url = 'https://www.google.com/search' if google else 'https://html.duckduckgo.com/html/'
    response = await client.get(url, params={'q': query})
    response.raise_for_status()
    parser = SearchResults(google); parser.feed(response.text)
    return parser.items[:12]


async def search_web(query):
    """No guessed URLs: return links actually supplied by a search provider."""
    async with httpx.AsyncClient(timeout=12, follow_redirects=True,
                                 headers={'User-Agent': USER_AGENT}) as client:
        # A configured search API is useful for hosted deployments; public search works locally.
        if os.getenv('TAVILY_API_KEY'):
            try:
                response = await client.post('https://api.tavily.com/search', headers={
                    'Authorization': 'Bearer ' + os.environ['TAVILY_API_KEY']}, json={
                    'query': query, 'max_results': 8, 'include_images': False, 'search_depth': 'basic'})
                response.raise_for_status()
                rows = [{'url': x['url'], 'title': x.get('title', ''), 'snippet': x.get('content', '')[:600]}
                        for x in response.json().get('results', []) if result_url(x.get('url', ''))]
                if rows:
                    return rows
            except (httpx.HTTPError, ValueError, KeyError):
                pass
        try:
            results = await _html_search(client, query)
        except (httpx.HTTPError, ValueError):
            results = []
        if not results:
            results = await _html_search(client, query, google=True)
        return list({x['url']: x for x in results}.values())[:12]


async def _indexed_university_once(name):
    """Wikidata's multilingual search supplies actual official-site and logo statements."""
    language = 'zh' if re.search(r'[\u4e00-\u9fff]', name) else 'en'
    async with httpx.AsyncClient(timeout=12, headers={'User-Agent': USER_AGENT}) as client:
        response = await client.get('https://www.wikidata.org/w/api.php', params={
            'action': 'wbsearchentities', 'format': 'json', 'search': name, 'language': language,
            'limit': 4, 'maxlag': 5})
        matches = index_json(response).get('search', [])
        ids = [x['id'] for x in matches if re.fullmatch(r'Q\d+', x.get('id', ''))]
        if not ids:
            return []
        response = await client.get('https://www.wikidata.org/w/api.php', params={
            'action': 'wbgetentities', 'format': 'json', 'ids': '|'.join(ids),
            'props': 'claims|labels', 'languages': language + '|en', 'maxlag': 5})
        entities = index_json(response).get('entities', {})
        results = []
        for identity, entity in entities.items():
            title = (entity.get('labels', {}).get(language) or entity.get('labels', {}).get('en') or {}).get('value', name)
            claims = entity.get('claims', {})
            sites = [x.get('mainsnak', {}).get('datavalue', {}).get('value') for x in claims.get('P856', [])]
            logos = [x.get('mainsnak', {}).get('datavalue', {}).get('value') for x in claims.get('P154', [])]
            source = 'https://www.wikidata.org/wiki/' + identity
            for site in sites[:2]:
                if isinstance(site, str) and result_url(site):
                    row = {'url': site, 'title': title, 'sourcePage': source}
                    if logos and isinstance(logos[0], str):
                        row['logoUrl'] = 'https://commons.wikimedia.org/wiki/Special:FilePath/' + quote(logos[0], safe='')
                    results.append(row)
            if not sites and logos and isinstance(logos[0], str):
                results.append({'url': source, 'title': title, 'sourcePage': source,
                                'logoUrl': 'https://commons.wikimedia.org/wiki/Special:FilePath/' + quote(logos[0], safe='')})
        titles = list(dict.fromkeys(x['logoUrl'].rsplit('/', 1)[-1] for x in results if x.get('logoUrl')))
        if titles:
            from urllib.parse import unquote
            try:
                response = await client.get('https://commons.wikimedia.org/w/api.php', params={
                    'action': 'query', 'format': 'json', 'titles': '|'.join('File:' + unquote(x) for x in titles),
                    'prop': 'imageinfo', 'iiprop': 'url', 'iiurlwidth': 500, 'maxlag': 5})
                data = index_json(response)
                images = {page.get('title', ''): (page.get('imageinfo') or [{}])[0]
                          for page in data.get('query', {}).get('pages', {}).values()}
            except (httpx.HTTPError, ValueError, KeyError, TypeError):
                # Keep the real homepage/Commons link even if thumbnail resolution fails.
                images = {}
            for row in results:
                if row.get('logoUrl'):
                    info = images.get('File:' + unquote(row['logoUrl'].rsplit('/', 1)[-1]), {})
                    if info.get('thumburl') or info.get('url'):
                        row['logoUrl'] = info.get('thumburl') or info['url']
        return results


async def wikipedia_university(name):
    """Independent article search when the institution database is busy or lacks the school."""
    language = 'zh' if re.search(r'[\u4e00-\u9fff]', name) else 'en'
    async with httpx.AsyncClient(timeout=12, headers={'User-Agent': USER_AGENT}) as client:
        response = await client.get('https://' + language + '.wikipedia.org/w/api.php', params={
            'action': 'query', 'format': 'json', 'generator': 'search', 'gsrsearch': name,
            'gsrlimit': 4, 'prop': 'pageimages|info', 'piprop': 'original|thumbnail',
            'pithumbsize': 500, 'inprop': 'url', 'maxlag': 5})
        pages = list(index_json(response).get('query', {}).get('pages', {}).values())
        pages.sort(key=lambda p: (p.get('title', '').casefold() != name.casefold(), p.get('index', 99)))
        rows = []
        for i, page in enumerate(pages):
            url = page.get('fullurl', '')
            if not result_url(url):
                continue
            row = {'url': url, 'sourcePage': url, 'title': page.get('title', name)}
            # Use the API's standard thumbnail route; originals may be rate-limited.
            image = page.get('thumbnail') or page.get('original') or {}
            if i == 0 and result_url(image.get('source', '')):
                row['logoUrl'] = image['source']
            rows.append(row)
        return rows


async def indexed_university(name):
    for attempt in range(2):
        try:
            rows = await _indexed_university_once(name)
            if rows:
                return rows
            break
        except IndexBusyError as exc:
            if attempt:
                break
            await asyncio.sleep(exc.retry_after)
            continue
        except (httpx.TimeoutException, httpx.ConnectError, httpx.RemoteProtocolError):
            if attempt:
                break
        except httpx.HTTPStatusError as exc:
            if attempt or (exc.response.status_code != 429 and exc.response.status_code < 500):
                break
        await asyncio.sleep(.4)
    return await wikipedia_university(name)


async def search_university(name):
    cache = Path(os.getenv('UNIVERSITY_CREST_CACHE', 'artifacts/university-crests')) / 'search'
    import hashlib
    file = cache / (hashlib.sha256(name.encode()).hexdigest() + '.json')
    try:
        previous = json.loads(file.read_text())
        if previous['expires'] > time.time():
            return previous['results']
    except (OSError, ValueError, KeyError):
        pass
    suffix = '" 校徽' if re.search(r'[\u4e00-\u9fff]', name) else '" logo'
    tasks = [asyncio.create_task(search_web('"' + name + suffix)), asyncio.create_task(indexed_university(name))]
    # Await the multilingual institution index even if HTML search returns first.
    # Generic or incomplete HTML results must not cancel the better school/logo source.
    try:
        indexed = await tasks[1]
    except (httpx.HTTPError, ValueError, KeyError, TypeError):
        indexed = []
    results = [indexed]
    if indexed:
        tasks[0].cancel()
        await asyncio.gather(tasks[0], return_exceptions=True)
    else:
        results.extend(await asyncio.gather(tasks[0], return_exceptions=True))
    pages = []
    # Index metadata is resilient when a HTML search provider is temporarily rate-limited.
    for rows in reversed(results):
        if isinstance(rows, list):
            pages.extend(rows)
    unique = {}
    for row in pages:
        previous = unique.setdefault(row['url'], {})
        for key, value in row.items():
            previous.setdefault(key, value)
    pages = list(unique.values())[:16]
    cache.mkdir(parents=True, exist_ok=True)
    temporary = file.with_suffix('.tmp')
    temporary.write_text(json.dumps({'results': pages, 'expires': time.time() + (86400 if pages else 60)}, ensure_ascii=False))
    temporary.replace(file)
    return pages

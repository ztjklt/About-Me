import asyncio
import json
import threading
from concurrent.futures import ThreadPoolExecutor

import pytest
from fastapi.testclient import TestClient
from server import app as api
from server import university_crests as crests
from server.university_search import SearchResults
from server import university_search as search
import httpx


@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv('ABOUTME_DB', str(tmp_path / 'profiles.sqlite'))
    monkeypatch.setenv('GAME_SERVER_KEY', 'private-test-key')
    return TestClient(api.app)


def login(client):
    response = client.post('/sessions', json={'nickname': '玩家'}).json()
    return {'Authorization': 'Bearer ' + response['token']}, response


def test_public_school_sites_and_real_search_links():
    assert crests.public_url('https://psl.eu/formation')
    assert crests.public_url('https://college.org/logo.svg')
    for bad in ('file:///etc/passwd', 'http://127.0.0.1/x', 'http://[::1]/x',
                'https://user:pass@college.org/x', 'http://localhost/x', 'https://college.org:8080/x'):
        assert not crests.public_url(bad)
    parser = SearchResults()
    parser.feed('<a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fpsl.eu%2Flogo">School</a>')
    assert parser.items == [{'url': 'https://psl.eu/logo', 'title': 'School'}]


def test_svg_is_rasterized_without_active_or_external_resources():
    svg = b'<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80"><circle cx="40" cy="40" r="35" fill="green"/></svg>'
    assert crests.raster_image(svg).startswith(b'\x89PNG')
    for payload in (b'<svg><script>alert(1)</script></svg>',
                    b'<svg><image href="http://127.0.0.1/private"/></svg>',
                    b'<svg><style>@import "http://private";</style></svg>'):
        with pytest.raises(ValueError):
            crests.raster_image(payload)


def test_small_college_alias_and_prefix_english_university_are_kept():
    intro = '我在哈理工就读，也在 University of Otago 学习过'
    assets = api.validate_personas({'personas': [], 'universities': [
        {'name': '哈理工', 'source': '哈理工'},
        {'name': 'University of Otago', 'source': 'University of Otago'}]}, intro)
    assert [a['institutionName'] for a in assets] == ['哈理工', 'University of Otago']


def test_live_progress_then_green_red_results_and_private_persistence(client, monkeypatch):
    entered, release = threading.Event(), threading.Event()
    async def provider(intro):
        return api.validate_personas({'personas': [{'name': '篮球', 'source': '篮球',
            'templateId': 'Projectile', 'visualId': 'basketball'}],
            'universities': [{'name': '湖北文理学院', 'source': '湖北文理学院'}],
            'terms': [{'term': x, 'source': x} for x in ('湖北文理学院', '篮球', '小提琴')]}, intro)
    async def emblem(name):
        entered.set()
        while not release.is_set():
            await asyncio.sleep(.002)
        return {'emblemPath': '/assets/university-crests/' + 'a'*64 + '.png',
                'crestStatus': 'ready', 'color': '#3058BB'}
    monkeypatch.setattr(api, 'generate_with_provider', provider)
    monkeypatch.setattr(api, 'generate_school_emblem', emblem)
    headers, account = login(client)
    other, _ = login(client)
    body = {'nickname': '玩家', 'introduction': '我在湖北文理学院，喜欢篮球，也会小提琴', 'generationId': 'a'*32}
    with client, ThreadPoolExecutor(max_workers=1) as pool:
        future = pool.submit(client.post, '/personas/generate', json=body, headers=headers)
        try:
            assert entered.wait(2)
            path = '/personas/generation-status?generationId=' + 'a'*32
            state = client.get(path, headers=headers).json()
            assert state['phase'] == 'searching'
            assert {x['term']: x['status'] for x in state['generationTerms']} == {
                '湖北文理学院': 'pending', '篮球': 'ready', '小提琴': 'failed'}
            assert client.get(path, headers=other).status_code == 404
            assert client.get(path).status_code == 401
        finally:
            release.set()
        response = future.result(timeout=3)
    assert response.status_code == 200
    profile = response.json()
    assert {x['term']: x['status'] for x in profile['generationTerms']} == {
        '湖北文理学院': 'ready', '篮球': 'ready', '小提琴': 'failed'}
    assert len(profile['personas']) == 2 and profile['personas'][1]['color'] == '#3058BB'
    assert client.get('/profiles/me', headers=headers).json()['generationTerms'] == profile['generationTerms']
    again = client.post('/personas/generate', headers=headers, json=body).json()
    assert [x['assetId'] for x in again['personas']] == [x['assetId'] for x in profile['personas']]
    assert again['revision'] == profile['revision']


def test_all_failed_terms_are_saved_without_fake_playable_props(client, monkeypatch):
    async def provider(intro):
        return api.validate_personas({'personas': [], 'terms': [{'term': '小提琴', 'source': '小提琴'}]}, intro)
    monkeypatch.setattr(api, 'generate_with_provider', provider)
    headers, _ = login(client)
    response = client.post('/personas/generate', headers=headers, json={
        'nickname': '玩家', 'introduction': '我会小提琴', 'generationId': 'b'*32})
    assert response.status_code == 200
    profile = response.json()
    assert profile['personas'] == []
    assert profile['generationTerms'][0]['status'] == 'failed'
    assert client.get('/profiles/me', headers=headers).json()['generationTerms'] == profile['generationTerms']


def test_ungrounded_and_private_keywords_cannot_appear_as_results():
    result = api.validate_personas({'personas': [], 'terms': [
        {'term': '篮球', 'source': '篮球'}, {'term': '清华大学', 'source': '清华大学'},
        {'term': '电话13800138000', 'source': '电话13800138000'}]}, '我会篮球，电话13800138000')
    assert [x['term'] for x in result.terms] == ['篮球']


def test_index_search_survives_web_failure_and_is_cached(tmp_path, monkeypatch):
    monkeypatch.setenv('UNIVERSITY_CREST_CACHE', str(tmp_path))
    calls = []
    async def unavailable(query):
        raise httpx.ConnectError('search temporarily unavailable')
    async def indexed(name):
        calls.append(name)
        return [{'url': 'https://college.org/', 'title': name,
                 'sourcePage': 'https://www.wikidata.org/wiki/Q123',
                 'logoUrl': 'https://college.org/logo.svg'}]
    monkeypatch.setattr(search, 'search_web', unavailable)
    monkeypatch.setattr(search, 'indexed_university', indexed)
    first = asyncio.run(search.search_university('小众学院'))
    second = asyncio.run(search.search_university('小众学院'))
    assert first == second and calls == ['小众学院']
    assert first[0]['logoUrl'].endswith('logo.svg')


def test_prose_model_response_keeps_real_page_candidates(monkeypatch):
    responses = iter([
        {'role': 'assistant', 'tool_calls': [{'id': 'a', 'function': {
            'name': 'fetch_university_page', 'arguments': '{"url":"https://college.org/"}'}}]},
        {'role': 'assistant', 'content': '找到校徽，可以使用网页返回的 logo。'}])
    async def complete(*args, **kwargs): return next(responses)
    async def lookup(name): return []
    async def page(url): return {'url': url, 'images': [
        {'url': 'https://college.org/logo.svg', 'hint': 'logo'}], 'links': [], 'text': ''}
    monkeypatch.setattr(crests, 'completion', complete)
    monkeypatch.setattr(crests, 'search_university', lookup)
    monkeypatch.setattr(crests, 'fetch_page', page)
    candidates = asyncio.run(crests.discover_candidates('小众学院'))
    assert candidates == [{'url': 'https://college.org/logo.svg', 'hint': 'logo',
                           'sourcePage': 'https://college.org/'}]


def test_rejected_index_logo_falls_back_to_deepseek_crawl(tmp_path, monkeypatch):
    from server.tests.test_university_crests import png
    monkeypatch.setenv('UNIVERSITY_CREST_CACHE', str(tmp_path))
    stages = []
    async def discover(name, prefer_index=True):
        stages.append(prefer_index)
        return [{'url': 'https://college.org/' + ('wrong.png' if prefer_index else 'logo.png'),
                 'sourcePage': 'https://college.org/', 'indexed': prefer_index}]
    async def verify(name, candidate):
        if candidate['indexed']: raise ValueError('wrong college')
        return png(), candidate['url']
    monkeypatch.setattr(crests, 'discover_candidates', discover)
    monkeypatch.setattr(crests, 'verify_candidate', verify)
    result = asyncio.run(crests.resolve_crest('小众学院'))
    assert stages == [True, False]
    assert result['status'] == 'ready' and result['sourceImage'].endswith('logo.png')


def test_a_shared_sentence_does_not_make_unimplemented_interests_green():
    from server.persona_results import generation_terms
    intro = '我喜欢篮球和小提琴'
    assets = api.validate_personas({'personas': [{'name': '篮球', 'source': intro,
        'templateId': 'Projectile', 'visualId': 'basketball'}],
        'terms': [{'term': word, 'source': intro} for word in ('篮球', '小提琴')]}, intro)
    statuses = generation_terms(assets, assets.terms, api.CATALOG_BY_PAIR)
    assert {t['term']: t['status'] for t in statuses} == {'篮球': 'ready', '小提琴': 'failed'}


def test_extracted_school_is_searched_even_if_provider_omits_universities(client, monkeypatch):
    searched = []
    intro = '我毕业于湖北文理学院，在 University of Otago 学习过'
    async def provider(value):
        return api.validate_personas({'personas': [], 'terms': [
            {'term': name, 'source': name} for name in ('湖北文理学院', 'University of Otago')]}, value)
    async def emblem(name):
        searched.append(name)
        return {'crestStatus': 'unavailable', 'crestReason': '未找到可确认的校徽'}
    monkeypatch.setattr(api, 'generate_with_provider', provider)
    monkeypatch.setattr(api, 'generate_school_emblem', emblem)
    headers, _ = login(client)
    response = client.post('/personas/generate', headers=headers, json={
        'nickname': '玩家', 'introduction': intro, 'generationId': 'f'*32})
    assert response.status_code == 200
    assert set(searched) == {'湖北文理学院', 'University of Otago'}
    assert response.json()['personas'] == []
    assert all(t['status'] == 'failed' for t in response.json()['generationTerms'])


def test_red_school_retry_turns_green_and_preserves_existing_prop(client, monkeypatch):
    provider_calls, searches = [], []
    async def provider(intro):
        provider_calls.append(intro)
        return api.validate_personas({'personas': [{'name': '篮球', 'term': '篮球', 'source': '篮球',
            'templateId': 'Projectile', 'visualId': 'basketball'}], 'terms': [
            {'term': x, 'source': x} for x in ('湖北文理学院', '篮球')]}, intro)
    async def emblem(name):
        searches.append(name)
        return {'crestStatus': 'unavailable'} if len(searches) == 1 else {
            'crestStatus': 'ready', 'emblemPath': '/assets/university-crests/' + 'b'*64 + '.png'}
    monkeypatch.setattr(api, 'generate_with_provider', provider)
    monkeypatch.setattr(api, 'generate_school_emblem', emblem)
    headers, _ = login(client)
    body = {'nickname': '玩家', 'introduction': '我毕业于湖北文理学院，喜欢篮球', 'generationId': 'c'*32}
    before = client.post('/personas/generate', headers=headers, json=body).json()
    after = client.post('/personas/generate', headers=headers, json=body).json()
    assert before['generationTerms'][0]['status'] == 'failed'
    assert after['generationTerms'][0]['status'] == 'ready'
    assert before['personas'][0]['assetId'] == after['personas'][0]['assetId']
    assert before['revision'] == after['revision'] and len(provider_calls) == 1
    assert len(after['personas']) == 2


def test_fast_html_does_not_cancel_institution_index(tmp_path, monkeypatch):
    monkeypatch.setenv('UNIVERSITY_CREST_CACHE', str(tmp_path))
    async def html(query): return [{'url': 'https://college.org/unrelated', 'title': 'Generic result'}]
    async def indexed(name):
        await asyncio.sleep(.01)
        return [{'url': 'https://college.org/', 'title': name, 'logoUrl': 'https://college.org/logo.svg'}]
    monkeypatch.setattr(search, 'search_web', html)
    monkeypatch.setattr(search, 'indexed_university', indexed)
    rows = asyncio.run(search.search_university('小众学院'))
    assert rows[0]['logoUrl'].endswith('logo.svg')


def test_transient_index_failure_gets_one_retry(monkeypatch):
    calls = []
    async def indexed(name):
        calls.append(name)
        if len(calls) == 1: raise httpx.ConnectError('temporary')
        return [{'url': 'https://college.org/', 'title': name}]
    monkeypatch.setattr(search, '_indexed_university_once', indexed)
    assert asyncio.run(search.indexed_university('小众学院'))[0]['title'] == '小众学院'
    assert len(calls) == 2


def test_http_200_maxlag_is_busy_and_uses_an_independent_index(monkeypatch):
    response = httpx.Response(200, headers={'retry-after': '5'}, json={'error': {'code': 'maxlag'}},
                              request=httpx.Request('GET', 'https://www.wikidata.org/w/api.php'))
    with pytest.raises(search.IndexBusyError) as caught:
        search.index_json(response)
    assert caught.value.retry_after == 5
    calls = []
    async def busy(name): raise caught.value
    async def pause(seconds): calls.append(seconds)
    async def wiki(name): return [{'url': 'https://en.wikipedia.org/wiki/College', 'title': name}]
    monkeypatch.setattr(search, '_indexed_university_once', busy)
    monkeypatch.setattr(search.asyncio, 'sleep', pause)
    monkeypatch.setattr(search, 'wikipedia_university', wiki)
    assert asyncio.run(search.indexed_university('College'))[0]['title'] == 'College'
    assert calls == [5]


def test_wikipedia_prefers_provided_standard_thumbnail(monkeypatch):
    original = httpx.AsyncClient
    def handler(request):
        assert request.url.params['pithumbsize'] == '500'
        return httpx.Response(200, json={'query': {'pages': {'1': {
            'title': 'College', 'fullurl': 'https://en.wikipedia.org/wiki/College',
            'original': {'source': 'https://upload.wikimedia.org/original.svg'},
            'thumbnail': {'source': 'https://thumb.wikimedia.org/provided-500.png'}}}}})
    def client(*args, **kwargs): return original(*args, **kwargs, transport=httpx.MockTransport(handler))
    monkeypatch.setattr(search.httpx, 'AsyncClient', client)
    rows = asyncio.run(search.wikipedia_university('College'))
    assert rows[0]['logoUrl'] == 'https://thumb.wikimedia.org/provided-500.png'

def test_catalog_aliases_share_one_ready_asset_without_greening_unrelated_term():
    from server.persona_results import generation_terms
    intro='我喜欢网络工程和路由器，也会小提琴'
    assets=api.validate_personas({'personas':[{'name':'路由器（小提琴爱好者）','term':'网络工程','source':intro,
        'templateId':'SpecialAbility','visualId':'network-router'}],
        'terms':[{'term':word,'source':intro} for word in ('网络工程','路由器','小提琴')]},intro)
    terms=generation_terms(assets,assets.terms,api.CATALOG_BY_PAIR)
    assert {t['term']:t['status'] for t in terms}=={'网络工程':'ready','路由器':'ready','小提琴':'failed'}
    assert terms[0]['assetId']==terms[1]['assetId']

def test_saved_alias_red_is_corrected_on_read_and_cached_generate(client,monkeypatch):
    intro='我喜欢网络工程和路由器'
    async def provider(value):
        return api.validate_personas({'personas':[{'name':'网络路由器','term':'网络工程','source':value,
            'templateId':'SpecialAbility','visualId':'network-router'}],
            'terms':[{'term':word,'source':value} for word in ('网络工程','路由器')]},value)
    monkeypatch.setattr(api,'generate_with_provider',provider)
    headers,session=login(client)
    body={'nickname':'测试','introduction':intro,'generationId':'a'*32}
    before=client.post('/personas/generate',headers=headers,json=body).json()
    with api.db() as con:
        before['generationTerms'][1].update(status='failed',assetId='',progress=0,stage='未生成')
        con.execute('UPDATE profiles SET profile=? WHERE id=?',(json.dumps(before),session['profile']['playerId']))
    after=client.get('/profiles/me',headers=headers).json()
    assert all(t['status']=='ready' for t in after['generationTerms'])
    cached=client.post('/personas/generate',headers=headers,json=body).json()
    assert all(t['status']=='ready' for t in cached['generationTerms'])
    assert cached['revision']==before['revision']
    with api.db() as con:
        stored=json.loads(con.execute('SELECT profile FROM profiles WHERE id=?',(session['profile']['playerId'],)).fetchone()[0])
    assert all(t['status']=='ready' for t in stored['generationTerms'])

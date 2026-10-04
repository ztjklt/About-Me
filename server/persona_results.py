"""Private extraction results: a green word must correspond to a usable generated asset."""
import re


class PersonaBatch(list):
    def __init__(self, assets, terms):
        super().__init__(assets); self.terms = terms


def extracted_terms(raw, introduction, safe):
    terms, seen = [], set()
    values = raw.get('terms', [])
    if not isinstance(values, list):
        return []
    for value in values[:32]:
        if not isinstance(value, dict):
            continue
        term, source = str(value.get('term', '')).strip(), str(value.get('source', '')).strip()
        if not term or len(term) > 80 or not source or len(source) > 160:
            continue
        if term not in introduction or source not in introduction or term not in source:
            continue
        if not safe(term) or not safe(source) or term.casefold() in seen:
            continue
        seen.add(term.casefold()); terms.append({'term': term, 'source': source})
    return terms


def fallback_term(asset, catalog):
    if asset.get('visualId') == 'trait-word':
        return asset['wordText']
    if asset.get('institutionName'):
        return asset['institutionName']
    source = asset.get('source', '')
    matches = [term for term in catalog.get('evidenceTerms', []) if term in source]
    if matches:
        return max(matches, key=len)
    return re.sub(r'^(?:我|本人|喜欢|热爱|擅长|会|是|在|就读于|毕业于)+', '', source)[:80] or asset['name']


def specific_catalog_pairs(word, catalogs):
    """The most specific published name wins over a shared hobby like swimming."""
    normalize = lambda value: re.sub(r'[^0-9A-Za-z\u4e00-\u9fff]', '', value).casefold()
    word = normalize(word)
    found = []
    for pair, catalog in catalogs.items():
        for label in [catalog.get('label', ''), *catalog.get('evidenceTerms', [])]:
            label = normalize(label)
            if label and label in word:
                found.append((len(label), pair))
    if not found:
        return set()
    length = max(length for length, pair in found)
    return {pair for size, pair in found if size == length}


def fact_identity(source, keyword, pair, catalogs):
    # Separate explicitly named props in a shared sentence, without inventing many props for one hobby.
    pairs = specific_catalog_pairs(keyword, catalogs) if keyword and keyword in source else set()
    return keyword if pairs == {pair} else source


def generation_terms(assets, terms, catalogs, pending=False):
    terms = [dict(x) for x in terms]
    def matches(asset, term):
        if asset.get('visualId') == 'trait-word':
            if term.get('wordText'):
                return (asset.get('wordCategory'), asset.get('wordText')) == (term.get('wordCategory'), term['wordText'])
            from server.trait_words import canonical
            known = canonical(term['term'])
            return bool(known and asset.get('wordCategory') == known['category'] and asset.get('wordText') == known['text'] and term['term'] in asset.get('source', ''))
        if asset.get('institutionName'):
            return asset['institutionName'] == term['term']
        if term['term'] not in asset.get('source', ''):
            return False
        known = specific_catalog_pairs(term['term'], catalogs)
        if known and (asset['templateId'], asset['visualId']) not in known:
            return False
        if asset.get('generationTerm') == term['term']:
            return True
        catalog = catalogs.get((asset['templateId'], asset['visualId']), {})
        normalize = lambda value: re.sub(r'[^0-9A-Za-z\u4e00-\u9fff]', '', value).casefold()
        word = normalize(term['term'])
        labels = [catalog.get('label', ''), *catalog.get('evidenceTerms', [])]
        if word and any(word in normalize(label) or normalize(label) in word for label in labels if normalize(label)):
            return True
        if asset.get('generationTerm'):
            return False
        # Legacy responses may omit a keyword; a shared sentence cannot mark unrelated terms green.
        return sum(t['term'] in asset.get('source', '') for t in terms) == 1
    for asset in assets:
        if not any(matches(asset, t) for t in terms):
            terms.append({'term': fallback_term(asset, catalogs.get((asset['templateId'], asset['visualId']), {})),
                          'source': asset.get('source', '')})
    result = []
    for term in terms[:32]:
        asset = next((a for a in assets if matches(a, term)), None)
        row = {'term': term['term'], 'source': term.get('source', ''), 'assetId': '',
               'status': 'failed', 'reason': '暂无对应的已上线道具', 'kind': 'prop', 'progress': 0, 'stage': '未生成'}
        if term.get('kind') == 'trait-word':
            row.update(kind='trait-word', wordCategory=term.get('wordCategory', 'custom'),
                       wordText=term.get('wordText', term['term']), abilityId=term.get('abilityId', ''))
            if term.get('status') in ('needs-confirmation', 'failed'):
                row.update(status=term['status'], reason=term['reason'], stage=term.get('stage', term['reason']))
        if asset:
            if asset.get('visualId') == 'trait-word':
                row.update(kind='trait-word', wordCategory=asset['wordCategory'], wordText=asset['wordText'], abilityId=asset['abilityId'])
            row['assetId'] = asset['assetId']
            if asset['visualId'] != 'school-disc':
                row.update(status='ready', reason=asset['name'], progress=1, stage='道具已生成')
            elif asset.get('emblemPath') and asset.get('crestStatus') != 'unavailable':
                row.update(status='ready', reason='校徽飞盘已生成', kind='university', progress=1, stage='校徽飞盘已生成')
            elif pending and asset.get('crestStatus') != 'unavailable':
                stage = asset.get('crestStage', '等待生成，国内大学优先')
                row.update(status='pending', kind='university', reason=stage, stage=stage,
                           progress=min(.95, asset.get('crestProgress', 0)))
            else:
                row['reason'] = asset.get('crestReason') or '未找到可确认的校徽，可再次生成重试'
                row.update(kind='university', progress=min(.95, asset.get('crestProgress', 0)), stage='未完成')
        result.append(row)
    return result


def playable_assets(assets):
    return [a for a in assets if a.get('visualId') != 'school-disc' or
            (a.get('emblemPath') and a.get('crestStatus') != 'unavailable')]

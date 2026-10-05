"""Controlled trait words: real asset contracts and private/authenticated API boundaries."""
import json
from pathlib import Path
import pytest
from fastapi.testclient import TestClient
from server import app as api, trait_words as words
from server.persona_results import generation_terms

@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv('ABOUTME_DB', str(tmp_path/'words.sqlite3'))
    monkeypatch.setenv('GAME_SERVER_KEY', 'word-test-key')
    monkeypatch.setenv('ABOUTME_AI_PEER_PER_MINUTE', '100')
    return TestClient(api.app)

def login(client):
    data=client.post('/sessions',json={'nickname':'字玩具'}).json()
    return {'Authorization':'Bearer '+data['token']}, data

def raw(intro):
    return api.validate_personas({'personas':[]},intro)

def test_catalog_exact_client_copy_and_sixteen_distinct_mbti_mechanics():
    root=Path(__file__).resolve().parents[2]
    assert (root/'shared/trait-abilities.json').read_bytes()==(root/'client/Assets/AboutMe/Resources/trait-abilities.json').read_bytes()
    ids={a['id'] for a in json.loads((root/'shared/persona-mechanics.json').read_text())['items']}
    assert len(words.ABILITIES)==16 and words.ABILITIES<=ids
    assert len(words.CATALOG['mbti'])==16
    assert len({w['abilityId'] for w in words.CATALOG['mbti']})==16
    assert all(w['abilityId'] in words.ABILITIES for w in words.WORDS)


@pytest.mark.parametrize('entry', words.CATALOG['mbti'])
@pytest.mark.parametrize('explicit_override', [False, True])
def test_mbti_default_upgrade_keeps_ids_and_explicit_player_choices(client, monkeypatch, entry, explicit_override):
    old_mapping = {'ENFP':'flame-fan','ESFP':'flame-fan','INFP':'bubble-lift','ENFJ':'bubble-lift',
                   'ISFP':'bubble-lift','ESFJ':'bubble-lift','ENTJ':'front-shield','ISFJ':'front-shield',
                   'ESTJ':'front-shield','INTJ':'calm-dome','ISTP':'calm-dome','ISTJ':'calm-dome',
                   'INFJ':'tractor-loose','INTP':'tractor-loose','ENTP':'spring-pad','ESTP':'spring-pad'}
    headers, session = login(client)
    intro = '我喜欢篮球，也喜欢阅读和听音乐；我的MBTI是'+entry['text']
    asset = dict(next(a for a in raw(intro) if a.get('wordText')==entry['text']), abilityId=old_mapping[entry['text']])
    choices = [dict(text=entry['text'], category='mbti', abilityId=asset['abilityId'])] if explicit_override else []
    old = dict(session['profile'], revision=1, personas=[asset], generationCandidates=[asset],
               generationTerms=[], traitWordVersion=1, wordAbilities=choices)
    with api.db() as con:
        con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?', (json.dumps(old),intro,old['playerId']))
    async def forbidden(_):
        raise AssertionError('MBTI mapping upgrade must not reroll existing assets')
    monkeypatch.setattr(api, 'generate_with_provider', forbidden)
    body = dict(nickname='字玩具', introduction=intro, generationId='d'*32)
    response = client.post('/personas/generate', headers=headers, json=body)
    assert response.status_code == 200, response.text
    after = response.json()
    expected = asset['abilityId'] if explicit_override else entry['abilityId']
    assert after['personas'][0]['abilityId'] == expected
    assert after['personas'][0]['assetId'] == asset['assetId']
    assert after['traitWordVersion'] == 2
    again = client.post('/personas/generate', headers=headers, json=body).json()
    assert again['revision'] == after['revision'] and again['personas'] == after['personas']

@pytest.mark.parametrize('entry',words.WORDS)
def test_all_published_personality_mood_and_mbti_are_grounded_assets(entry):
    intro=('我的MBTI是' if entry['category']=='mbti' else '我觉得自己')+entry['text']
    batch=raw(intro)
    a=next(a for a in batch if a['wordText']==entry['text'])
    assert a['wordCategory']==entry['category'] and a['abilityId']==entry['abilityId']
    assert a['visualId']=='trait-word' and a['templateId']=='SpecialAbility'
    assert words.valid_asset(a,intro,api.sensitive_persona_text)
    rows=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)
    assert all(r['status']=='ready' and r['assetId'] for r in rows)

@pytest.mark.parametrize('intro',['我不是热情的人','假如我温柔','他很冷静','我的朋友好奇','如果我MBTI是ENFP',
    '我想变成热情的人','She is friendly','My friend is ENFP','I am not calm','我不喜欢友好微笑'])
def test_negative_hypothetical_third_person_not_personas(intro):
    assert words.extract(intro)==[]


def test_synonyms_friendly_smile_one_and_different_facts_separate():
    intro='我热情似火；我很热烈；我友好微笑；我也微笑；我温柔；我专注'
    batch=raw(intro)
    assert [a['wordText'] for a in batch]==['热情','友好微笑','温柔','专注']
    assert len({a['assetId'] for a in batch})==4
    assert api.profile_follows_generation_rules({'personas':batch},intro)


def test_unknown_word_needs_confirmation_and_explicit_override_retains_id():
    intro='我的性格是云朵'
    batch=raw(intro)
    assert batch==[]
    row=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)[0]
    assert row['status']=='needs-confirmation' and row['reason']=='请选择能力' and not row['assetId']
    selection=[{'text':'云朵','category':'custom','abilityId':'bubble-lift'}]
    assets,terms=words.augment(batch,batch.terms,intro,selection,api.sensitive_persona_text)
    assert assets[0]['wordText']=='云朵' and assets[0]['abilityId']=='bubble-lift'
    replaced,_=words.augment(assets,terms,intro,[dict(selection[0],abilityId='front-shield')],api.sensitive_persona_text)
    assert replaced[0]['assetId']==assets[0]['assetId'] and len(replaced)==1
    assert replaced[0]['abilityId']=='front-shield'

@pytest.mark.parametrize('text',['<b>你好</b>','#热情','去死','手机号','电话13800138000','abcdefghijklm','😃','联系人@example.com','热情\n冷静'])
def test_public_word_charset_length_and_safety(text):
    assert not words.safe_word(text,api.sensitive_persona_text)


def test_custom_request_cannot_invent_self_fact_or_ability():
    intro='我喜欢篮球；他很云朵'
    values=[dict(text='云朵',category='custom',abilityId='spring-pad'),dict(text='星云',category='custom',abilityId='flame-fan')]
    assert words.extract(intro,values,api.sensitive_persona_text)==[]
    assert words.extract('我的性格是云朵',[dict(values[0],abilityId='run-python')])[0]['abilityId']==''


def test_total_budget_twenty_and_extra_word_red_without_fake_ready():
    intro='我'+ '、'.join(w['text'] for w in words.CATALOG['words'])+'；'+'；'.join('我的MBTI是'+w['text'] for w in words.CATALOG['mbti'])
    batch=raw(intro)
    assert len(batch)==20
    rows=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)
    assert sum(r['status']=='ready' for r in rows)==20
    assert any(r['status']=='failed' and '20件' in r['reason'] and not r['assetId'] for r in rows)


def test_twelve_asset_cache_recovers_smile_and_enfp_without_reroll(client, monkeypatch):
    intro='；'.join(f'我喜欢篮球{i}' for i in range(11))+'；我的性格是热情；我的表情是友好微笑；我的MBTI是ENFP'
    headers, session=login(client)
    props=[dict(assetId=f'stable-prop-{i}',name='篮球',source=f'篮球{i}',templateId='Projectile',visualId='basketball',color='#EE7733') for i in range(11)]
    candidates, terms=words.augment(props, [], intro, maximum=12)
    old=dict(session['profile'],revision=1,personas=candidates,generationCandidates=candidates,
             generationTerms=generation_terms(candidates,terms,api.CATALOG_BY_PAIR),traitWordVersion=words.VERSION)
    assert len(candidates)==12
    assert {t['wordText'] for t in old['generationTerms'] if t['status']=='failed'}=={'友好微笑','ENFP'}
    with api.db() as con:
        con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',(json.dumps(old),intro,old['playerId']))
    async def forbidden(_):
        raise AssertionError('a capacity upgrade must not reroll existing assets')
    monkeypatch.setattr(api,'generate_with_provider',forbidden)
    body=dict(nickname='字玩具',introduction=intro,generationId='b'*32)
    response=client.post('/personas/generate',headers=headers,json=body)
    assert response.status_code==200,response.text
    data=response.json()
    assert len(data['personas'])==14
    assert {a['assetId'] for a in candidates}<={a['assetId'] for a in data['personas']}
    assert all(t['status']=='ready' for t in data['generationTerms'])
    assert {a.get('wordText') for a in data['personas'] if a.get('visualId')=='trait-word'}=={'热情','友好微笑','ENFP'}
    second=client.post('/personas/generate',headers=headers,json=body).json()
    assert [a['assetId'] for a in second['personas']]==[a['assetId'] for a in data['personas']]
    assert second['revision']==data['revision']
    assert client.get('/profiles/me',headers=headers).json()['personas']==data['personas']
    trusted=client.post('/internal/verify',headers={'X-Game-Key':'word-test-key'},json={'token':session['token']})
    assert trusted.status_code==200 and len(trusted.json()['personas'])==14


def test_old_request_cache_upgrade_confirmation_persistence_and_public_privacy(client,monkeypatch):
    intro='我喜欢篮球；我的性格是热情、云朵；我的MBTI是ENFP'
    headers,session=login(client)
    old=dict(session['profile'],revision=1,personas=[dict(assetId='basketball-stable',name='篮球',source='篮球',templateId='Projectile',visualId='basketball',color='#EE7733')])
    with api.db() as con:con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',(json.dumps(old),intro,old['playerId']))
    async def forbidden(_):raise AssertionError('legacy word cache must not require another provider call')
    monkeypatch.setattr(api,'generate_with_provider',forbidden)
    body=dict(nickname='字玩具',introduction=intro,generationId='a'*32)
    before=client.post('/personas/generate',headers=headers,json=body)
    assert before.status_code==200,before.text
    data=before.json()
    assert data['personas'][0]['assetId']=='basketball-stable'
    heat=next(a for a in data['personas'] if a.get('wordText')=='热情')
    assert next(r for r in data['generationTerms'] if r.get('wordText')=='云朵')['status']=='needs-confirmation'
    body['wordAbilities']=[dict(text='云朵',category='custom',abilityId='bubble-lift'),dict(text='热情',category='personality',abilityId='spring-pad')]
    after=client.post('/personas/generate',headers=headers,json=body)
    assert after.status_code==200,after.text
    data=after.json()
    assert next(a for a in data['personas'] if a.get('wordText')=='热情')['assetId']==heat['assetId']
    assert next(a for a in data['personas'] if a.get('wordText')=='热情')['abilityId']=='spring-pad'
    assert len(data['personas'])==4
    body.pop('wordAbilities')
    cached=client.post('/personas/generate',headers=headers,json=body).json()
    assert [(a['assetId'],a.get('abilityId')) for a in cached['personas']]==[(a['assetId'],a.get('abilityId')) for a in data['personas']]
    assert client.get('/profiles/me',headers=headers).json()['personas']==cached['personas']
    public=client.post('/internal/verify',headers={'X-Game-Key':'word-test-key'},json={'token':session['token']}).json()
    assert 'source' not in json.dumps(public) and 'generation' not in json.dumps(public) and 'introduction' not in public
    assert public['personas'][1]['wordCategory']=='personality'
    assert public['personas'][1]['abilityId']=='spring-pad'


def test_api_contract_invalid_selection_rejected_and_catalog_separate(client):
    headers,_=login(client)
    for choice in [dict(text='云朵',category='custom',abilityId='code'),dict(text='云朵',category='weapon',abilityId='flame-fan'),dict(text='x'*13,category='custom',abilityId='flame-fan')]:
        assert client.post('/personas/generate',headers=headers,json=dict(nickname='字玩具',introduction='我的性格是云朵',wordAbilities=[choice])).status_code==422
    assert client.get('/trait-abilities').json()==words.CATALOG
    assert client.get('/health').json()['traitWordPersonas'] is True

@pytest.mark.parametrize('word',['坚定','勇敢','专注','理性','好奇','活力','独立'])
def test_all_negative_traits_require_positive_self_statement(word):
    assert words.extract('我不'+word)==[]
    assert words.extract('她很'+word)==[]
    assert words.extract('假设我是'+word)==[]


def test_bad_custom_display_is_explicit_red_not_silently_missing():
    batch=raw('我的性格是超长超长超长超长超长超长超长')
    row=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)[0]
    assert batch==[] and row['status']=='failed' and '12' in row['reason']


def test_catalog_retry_keeps_old_success_ids_and_saved_selection(client,monkeypatch):
    intro='我喜欢篮球和望远镜；我的性格是热情'
    headers,session=login(client)
    previous=raw(intro)[0]
    previous['abilityId']='front-shield'
    old=dict(session['profile'],revision=1,personas=[dict(assetId='old-ball',name='篮球',source='篮球',templateId='Projectile',visualId='basketball',color='#DD7755'),previous],
             generationCandidates=[dict(assetId='old-ball',name='篮球',source='篮球',templateId='Projectile',visualId='basketball',color='#DD7755'),previous],
             generationTerms=[dict(term='望远镜',source='我喜欢篮球和望远镜',status='failed')],catalogGenerationVersion='before-telescope',
             wordAbilities=[dict(text='热情',category='personality',abilityId='front-shield')])
    with api.db() as con:con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',(json.dumps(old),intro,old['playerId']))
    async def provider(value):
        return api.validate_personas({'personas':[dict(name='望远镜',term='望远镜',source='望远镜',templateId='Utility',visualId='telescope')],
            'terms':[dict(term='望远镜',source='望远镜')]},value)
    monkeypatch.setattr(api,'generate_with_provider',provider)
    response=client.post('/personas/generate',headers=headers,json=dict(nickname='字玩具',introduction=intro,generationId='c'*32))
    assert response.status_code==200,response.text
    after=response.json()
    assert any(a['assetId']=='old-ball' for a in after['personas'])
    heat=next(a for a in after['personas'] if a.get('wordText')=='热情')
    assert heat['assetId']==previous['assetId'] and heat['abilityId']=='front-shield'
    assert any(a['visualId']=='telescope' for a in after['personas'])


def test_public_verify_rejects_invalid_historical_word(client):
    headers,session=login(client)
    intro='我热情'
    asset=raw(intro)[0]
    asset['abilityId']='arbitrary-code'
    profile=dict(session['profile'],personas=[asset])
    with api.db() as con:con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',(json.dumps(profile),intro,profile['playerId']))
    assert client.post('/internal/verify',headers={'X-Game-Key':'word-test-key'},json={'token':session['token']}).status_code==422

@pytest.mark.parametrize('intro',['我喜欢温柔的人','我喜欢热情的角色','我研究MBTI ENFP','我喜欢ENFP','I like ENFP','I prefer passionate people'])
def test_preferences_do_not_infer_personality_or_mbti(intro):
    assert words.extract(intro)==[]


def test_questionnaire_expression_preference_is_explicit_owned_style():
    batch=raw('我喜欢的人物表情是友好微笑')
    assert len(batch)==1 and batch[0]['wordText']=='友好微笑'


def test_model_synonym_terms_collapse_to_one_text_row():
    intro='我很热情，也热烈'
    batch=api.validate_personas({'personas':[], 'terms':[dict(term=w,source=intro) for w in ('热情','热烈')]},intro)
    rows=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)
    assert len(batch)==len(rows)==1 and rows[0]['status']=='ready'


def test_font_gate_matches_delivered_glyph_codepoint_table(monkeypatch):
    assert words.FONT_SUPPORT['schema']=='aboutme-thick-word-font-v2'
    assert words.FONT_SUPPORT['fontSha256']=='2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b'
    assert len(words.FONT_SUPPORT['codepoints'])==44802 and 32 in words.FONT_SUPPORT['codepoints']
    assert (Path(__file__).resolve().parents[2]/'client/Assets/AboutMe/Resources/ThickWords/supported-codepoints.json').read_bytes()==(Path(__file__).resolve().parents[2]/'shared/trait-word-supported-codepoints.json').read_bytes()
    assert words.safe_word('云朵 ENFP',api.sensitive_persona_text)
    monkeypatch.setattr(words,'SUPPORTED_CODEPOINTS',words.SUPPORTED_CODEPOINTS-{ord('云')})
    batch=raw('我的性格是云朵')
    row=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)[0]
    assert row['status']=='failed' and '字体未支持' in row['reason'] and not row['assetId']
    assert not words.safe_word('云朵')


def test_supplementary_han_limit_counts_unicode_codepoints_not_utf16():
    supplementary=next(chr(cp) for cp in words.SUPPORTED_CODEPOINTS if 0x20000<=cp<=0x323af)
    assert len(supplementary)==1
    assert words.safe_word(supplementary*12)
    assert not words.safe_word(supplementary*13)

@pytest.mark.parametrize('intro',['我的性格是热情和温柔','我的性格是很热情','My personality is very gentle'])
def test_modifiers_and_conjunctions_do_not_create_unknown_shadow(intro):
    batch=raw(intro)
    assert batch and all(t.get('status')!='needs-confirmation' for t in batch.terms)
    assert all(a['wordCategory']!='custom' for a in batch)


def test_questionnaire_custom_expression_gets_confirmation():
    batch=raw('我喜欢的人物表情是云朵')
    row=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)[0]
    assert batch==[] and row['wordCategory']=='custom' and row['status']=='needs-confirmation'


def test_same_sentence_distinct_traits_not_source_dedup():
    batch=raw('我的性格是热情、温柔、理性。')
    assert {a['wordText'] for a in batch} == {'热情','温柔','理性'}
    assert len({a['assetId'] for a in batch}) == 3
    rows=generation_terms(batch,batch.terms,api.CATALOG_BY_PAIR)
    assert {r['assetId'] for r in rows if r['status']=='ready'} == {a['assetId'] for a in batch}
    aliases=raw('我的性格是热情、热烈；我喜欢的人物表情是友好微笑。')
    assert [a['wordText'] for a in aliases] == ['热情','友好微笑']

def test_custom_english_space_has_font_advance_but_empty_word_rejected():
    assert words.safe_word('very kind',api.sensitive_persona_text)
    assert not words.safe_word('   ',api.sensitive_persona_text)
    batch,terms=words.augment([],[], 'My personality is very kind',[dict(text='very kind',category='custom',abilityId='bubble-lift')],api.sensitive_persona_text)
    assert len(batch)==1 and batch[0]['wordText']=='very kind'

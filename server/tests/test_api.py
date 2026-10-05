import json
import httpx
import pytest
from fastapi.testclient import TestClient
from server import app as module

@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv('ABOUTME_DB', str(tmp_path / 'test.sqlite3'))
    monkeypatch.setenv('GAME_SERVER_KEY', 'test-server-key')
    monkeypatch.setattr(module, 'SCHOOL_EMBLEM_DIR', tmp_path / 'school-emblems')
    async def generated_emblem(school):
        return {'emblemPath':'/school-emblems/test.png','emblemCrop':'0.0600,0.0600,0.8800,0.8800',
                'emblemSource':'https://example.test/'+school,'emblemLicense':'test-only',
                'generatedBy':'DeepSeek Flash Vision','generationConfidence':.98}
    monkeypatch.setattr(module, 'generate_school_emblem', generated_emblem)
    return TestClient(module.app)

def credentials(client):
    data = client.post('/sessions', json={'nickname': '玩家'}).json()
    return {'Authorization': 'Bearer ' + data['token']}, data

def candidates(n=2):
    return {'personas': [dict(name=f'篮球{i}', source=f'兴趣{i}', templateId='Projectile',
                              visualId='basketball', color='#FFAA55') for i in range(n)]}

def test_tokens_and_private_profiles(client):
    headers, data = credentials(client)
    assert client.get('/profiles/me').status_code == 401
    assert client.get('/profiles/me', headers=headers).json()['playerId'] == data['profile']['playerId']
    assert client.post('/internal/verify', json={'token': data['token']}).status_code == 403
    assert client.post('/internal/verify', json={'token': data['token']}, headers={'X-Game-Key':'test-server-key'}).status_code == 200

def test_invalid_templates_dedup_and_variable_count():
    raw = candidates(7)
    raw['personas'].append(raw['personas'][0].copy())
    intro = '；'.join(f'兴趣{i}' for i in range(7))
    assert len(module.validate_personas(raw, intro)) == 7
    raw['personas'][0]['templateId'] = 'RunArbitraryCode'
    with pytest.raises(ValueError): module.validate_personas(raw, intro)
    with pytest.raises(ValueError): module.validate_personas({'personas': []}, intro)

def test_real_generation_varies_with_grounded_facts_and_has_a_room_budget():
    facts = [f'爱好{i}篮球' for i in range(module.MAX_PERSONAS + 2)]
    raw = {'personas': [dict(name=f'玩具{i}', source=fact, templateId='Projectile',
                             visualId='basketball') for i, fact in enumerate(facts)]}
    assert len(module.validate_personas({'personas': raw['personas'][:1]}, facts[0])) == 1
    assert len(module.validate_personas({'personas': raw['personas'][:9]}, '；'.join(facts))) == 9
    assert len(module.validate_personas(raw, '；'.join(facts))) == module.MAX_PERSONAS


def test_source_must_be_a_nonempty_exact_intro_span_and_repeated_fact_collapses():
    intro = '我喜欢篮球，也热爱篮球；我还喜欢摄影。'
    pair = ('Projectile', 'basketball')
    raw = {'personas': [
        dict(name='篮球一号', source='喜欢篮球', templateId=pair[0], visualId=pair[1]),
        dict(name='篮球二号', source='热爱篮球', templateId=pair[0], visualId=pair[1]),
        dict(name='相机', source='喜欢摄影', templateId='Utility', visualId='camera'),
        dict(name='虚构道具', source='从没写过的事实', templateId=pair[0], visualId=pair[1]),
    ]}
    result = module.validate_personas(raw, intro)
    assert [item['source'] for item in result] == ['喜欢篮球', '喜欢摄影']
    assert module.profile_follows_generation_rules({'personas': result}, intro)
    with pytest.raises(ValueError):
        module.validate_personas({'personas': [dict(name='篮球三号', source='',
            templateId=pair[0], visualId=pair[1])]}, intro)
    assert not module.profile_follows_generation_rules({'personas': [dict(result[0], source='')]}, intro)


def test_unsupported_music_props_return_actionable_error_without_saving(client, monkeypatch):
    introduction = '我喜欢Claude，也喜欢音乐、吉他和做游戏。平时会弹吉他、写旋律，还常和朋友玩合作游戏。'
    observed = {'personas': [
        dict(name='吉他', source='平时会弹吉他', templateId='Melee', visualId='ukulele-cc0'),
        dict(name='合作游戏手柄', source='常和朋友玩合作游戏', templateId='Utility', visualId='gamepad-cc0'),
    ]}
    class Provider:
        def __init__(self, **kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kwargs):
            return httpx.Response(200, json={'choices': [{'message': {'content': json.dumps(observed)}}]},
                                  request=httpx.Request('POST', url))
    monkeypatch.setattr(module.httpx, 'AsyncClient', Provider)
    for key in ('LLM_API_KEY', 'LLM_MODEL', 'LLM_BASE_URL'):
        monkeypatch.setenv(key, 'test')
    headers, _ = credentials(client)
    response = client.post('/personas/generate', headers=headers,
        json={'nickname': '玩家', 'introduction': introduction})
    assert response.status_code == 422
    assert '暂未匹配到已上线道具' in response.json()['detail']
    assert client.get('/profiles/me', headers=headers).json()['personas'] == []


def test_private_and_abusive_facts_do_not_become_personas():
    intro = '我喜欢摄影，手机号是13800138000，住在幸福路12号。'
    raw = {'personas': [
        dict(name='相机', source='喜欢摄影', templateId='Utility', visualId='camera'),
        dict(name='号码球', source='手机号是13800138000', templateId='Projectile', visualId='basketball'),
        dict(name='住址球', source='住在幸福路12号', templateId='Projectile', visualId='basketball'),
        dict(name='去死球', source='喜欢摄影', templateId='Projectile', visualId='basketball'),
    ]}
    result = module.validate_personas(raw, intro)
    assert [item['name'] for item in result] == ['相机']
    assert all(not module.sensitive_persona_text(item['name']) for item in result)
    with pytest.raises(ValueError):
        module.validate_personas({'personas': raw['personas'][1:3]}, intro)

def test_cc0_visual_catalog_allows_only_published_template_pairs():
    assert len(module.CATALOG) == 277
    result = module.validate_personas({'personas': [dict(name='抱枕', source='抱枕', templateId='Cushion', visualId='pillow-a')]}, '抱枕')
    assert result[0]['color'] == '#F5C0CF'
    with pytest.raises(ValueError):
        module.validate_personas({'personas': [dict(name='抱枕', source='抱枕', templateId='Projectile', visualId='pillow-a')]}, '抱枕')


def test_new_persona_pairs_have_grounded_server_mapping():
    from pathlib import Path
    root = Path(__file__).parents[2]
    expected = {
        ('Projectile', 'football-cc0'), ('Utility', 'gamepad-cc0'),
        ('Melee', 'ukulele-cc0'), ('Disc', 'notebook-cc0'),
    }
    assert expected <= module.ALLOWED
    assert module.LEGACY_FOOTBALL_PAIR in module.ALLOWED
    assert module.CATALOG_BY_PAIR[('Disc', 'notebook-cc0')]['label'] == '纸质笔记本'
    visuals = json.loads((root / 'shared/persona-visuals.json').read_text())['items']
    assert len(visuals) == 266
    assert len({item['visualId'] for item in visuals}) == 266
    assert all((item['templateId'], item['visualId']) in module.ALLOWED for item in visuals)
    intro = '我每周踢足球，也喜欢用手柄玩游戏，会弹尤克里里，还用笔记本记手账。'
    raw = {'personas': [
        dict(name=name, source=source, templateId=template, visualId=visual)
        for (template, visual), name, source in [
            (('Projectile', 'football-cc0'), '足球', '踢足球'),
            (('Utility', 'gamepad-cc0'), '手柄', '用手柄玩游戏'),
            (('Melee', 'ukulele-cc0'), '尤克里里', '弹尤克里里'),
            (('Disc', 'notebook-cc0'), '笔记本', '用笔记本记手账'),
        ]
    ]}
    assert len(module.validate_personas(raw, intro)) == 4
    old_football = {'personas': [dict(name='足球', source='踢足球',
        templateId='Ricochet', visualId='football-cc0')]}
    assert module.validate_personas(old_football, '踢足球')[0]['templateId'] == 'Projectile'
    assert module.profile_follows_generation_rules({'personas': old_football['personas']}, '踢足球')
    mismatched = {'personas': [dict(name='手账本', source='我用笔记本电脑写代码',
        templateId='Disc', visualId='notebook-cc0')]}
    with pytest.raises(ValueError):
        module.validate_personas(mismatched, '我用笔记本电脑写代码')
    for visual, template, accepted, rejected in [
        ('football-cc0', 'Projectile', '我踢足球', '我踢美式足球'),
        ('notebook-cc0', 'Disc', '我用纸质笔记本记手账', '我用电脑笔记本写代码'),
        ('football-cc0', 'Projectile', 'I play soccer', 'I play gridiron football'),
        ('football-cc0', 'Projectile', '我每周踢足球', '我踢美式橄榄球'),
        ('notebook-cc0', 'Disc', 'I write in a notebook', 'I use a notebook computer'),
        ('notebook-cc0', 'Disc', '我用纸质笔记本记手账', '我用笔记本电脑写代码'),
    ]:
        accepted_raw = {'personas': [dict(name='道具', source=accepted,
            templateId=template, visualId=visual)]}
        rejected_raw = {'personas': [dict(name='道具', source=rejected,
            templateId=template, visualId=visual)]}
        assert len(module.validate_personas(accepted_raw, accepted)) == 1
        with pytest.raises(ValueError):
            module.validate_personas(rejected_raw, rejected)

def test_generation_is_grounded_and_school_is_one_badge():
    raw = {'personas': [
        dict(name='逸仙精神', source='中山大学', templateId='Projectile', visualId='basketball', color='#FFFFFF'),
        dict(name='校园猫', source='校园猫文化', templateId='Summon', visualId='cat', color='#FFFFFF'),
        dict(name='三校区', source='广州、珠海、深圳三校区', templateId='Disc', visualId='school-disc', color='#FFFFFF'),
    ]}
    result = module.validate_personas(raw, '中山大学')
    assert len(result) == 1
    assert result[0]['name'] == '中山大学校徽圆盘'
    assert (result[0]['templateId'], result[0]['visualId']) == ('Disc', 'school-disc')
    assert result[0]['color'] == '#77C8BD'

@pytest.mark.parametrize('school', ['清华大学', '北京大学', '中山大学'])
def test_three_school_discs_are_marked_as_deepseek_generated(client, school, monkeypatch):
    async def school_provider(introduction):
        return module.validate_personas({'personas': [dict(name='校徽', source=school,
            templateId='Disc', visualId='school-disc')]}, introduction)
    monkeypatch.setattr(module, 'generate_with_provider', school_provider)
    headers, _ = credentials(client)
    response = client.post('/personas/generate', headers=headers,
        json={'nickname':'玩家', 'introduction':'我在'+school+'读书'})
    assert response.status_code == 200
    result = response.json()['personas'][0]
    assert result['name'] == school + '校徽圆盘'
    assert result['generatedBy'] == 'DeepSeek Flash Vision'
    assert result['emblemPath'].startswith('/school-emblems/')
    assert result['emblemCrop'] == '0.0600,0.0600,0.8800,0.8800'

def test_deepseek_crop_must_keep_a_complete_emblem():
    assert module.safe_crop({'x':.05,'y':.04,'width':.9,'height':.91}) == '0.0500,0.0400,0.9000,0.9100'
    with pytest.raises(ValueError):
        module.safe_crop({'x':.4,'y':.4,'width':.1,'height':.1})

def test_deepseek_vision_selects_candidate_and_returns_complete_crop():
    import asyncio
    import httpx
    candidates = [{'title':'Current crest','url':'https://images.test/crest.png',
                   'pageUrl':'https://source.test/crest','license':'test','mime':'image/png'}]
    png = b'\x89PNG\r\n\x1a\n' + b'prototype-image'
    class Provider:
        async def get(self, url, **kwargs):
            return httpx.Response(200, content=png, request=httpx.Request('GET', url))
        async def post(self, url, **kwargs):
            body = kwargs['json']
            assert body['model'] == 'deepseek-v4-flash-vision-exp'
            assert body['thinking'] == {'type': 'disabled'}
            assert body['messages'][1]['content'][1]['type'] == 'image_url'
            return httpx.Response(200, json={'choices':[{'message':{'content':json.dumps({
                'selectedIndex':0,'crop':{'x':.06,'y':.06,'width':.88,'height':.88},
                'confidence':.97,'reason':'完整现行校徽'})}}]}, request=httpx.Request('POST', url))
    selected, (data, mime), crop, confidence = asyncio.run(module.select_school_emblem_with_deepseek(
        Provider(), 'key', 'https://api.deepseek.test/v1', '清华大学', candidates))
    assert selected['title'] == 'Current crest' and data == png and mime == 'image/png'
    assert crop == '0.0600,0.0600,0.8800,0.8800' and confidence == .97

def test_introduction_length_is_bounded_before_provider(client):
    headers, _ = credentials(client)
    response = client.post('/personas/generate', headers=headers,
        json={'nickname':'玩家', 'introduction':'长' * (module.MAX_INTRO_CHARS + 1)})
    assert response.status_code == 422

def test_generation_persistence_reroll_and_new_assets(client, monkeypatch):
    async def fake(introduction):
        raw = {'personas': [
            dict(name='篮球', source='篮球', templateId='Projectile', visualId='basketball'),
            dict(name='猫', source='猫', templateId='Summon', visualId='cat'),
        ]}
        return module.validate_personas(raw, introduction)
    monkeypatch.setattr(module, 'generate_with_provider', fake)
    headers, _ = credentials(client)
    body = {'nickname': '猫猫', 'introduction': '我喜欢篮球和猫'}
    first = client.post('/personas/generate', json=body, headers=headers)
    assert first.status_code == 200
    assert client.post('/personas/generate', json=body, headers=headers).status_code == 409
    body['introduction'] += '还有摄影'
    second = client.post('/personas/generate', json=body, headers=headers).json()
    assert second['revision'] == 2
    assert second['personas'][0]['assetId'] != first.json()['personas'][0]['assetId']
    assert client.get('/profiles/me', headers=headers).json() == second

def test_legacy_ungrounded_result_can_be_regenerated_with_same_intro(client, monkeypatch):
    headers, data = credentials(client)
    legacy = dict(data['profile'], personas=[dict(assetId='old', name='校园猫', source='校园猫文化',
        templateId='Summon', visualId='cat', color='#FFFFFF')], revision=1)
    with module.db() as con:
        con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',
            (json.dumps(legacy, ensure_ascii=False), '中山大学', data['profile']['playerId']))
    async def fixed(introduction):
        return module.validate_personas({'personas':[dict(name='学校', source='中山大学',
            templateId='Disc', visualId='school-disc')]}, introduction)
    monkeypatch.setattr(module, 'generate_with_provider', fixed)
    result = client.post('/personas/generate', headers=headers,
        json={'nickname':'玩家', 'introduction':'中山大学'})
    assert result.status_code == 200
    assert len(result.json()['personas']) == 1

def test_no_fake_ai_fallback(client, monkeypatch):
    monkeypatch.delenv('LLM_API_KEY', raising=False)
    headers, _ = credentials(client)
    assert client.post('/personas/generate', json={'nickname':'玩家', 'introduction':'喜欢篮球'}, headers=headers).status_code == 503
    assert client.get('/profiles/me', headers=headers).json()['revision'] == 0

def test_result_idempotency(client):
    body = {'matchId': 'match-1', 'winner': 'p1', 'eaten': []}
    headers = {'X-Game-Key': 'test-server-key'}
    assert client.post('/internal/results', json=body, headers=headers).status_code == 200
    assert client.post('/internal/results', json=body, headers=headers).status_code == 200
    with module.db() as con:
        assert con.execute('SELECT COUNT(*) FROM matches').fetchone()[0] == 1

def test_unconfigured_speech_and_wechat_are_explicit(client, monkeypatch):
    headers,_ = credentials(client)
    monkeypatch.delenv('ASR_API_KEY',raising=False)
    monkeypatch.delenv('WECHAT_APP_ID',raising=False)
    assert client.post('/speech/transcribe',headers=headers,files={'file':('a.wav',b'abc','audio/wav')}).status_code == 503
    assert client.post('/sessions/wechat',json={'nickname':'玩家','code':'code'}).status_code == 503
    assert client.get('/rooms/INVALID/qrcode',headers=headers).status_code == 422

def test_wechat_login_keeps_profile_and_rotates_token(client,monkeypatch):
    import httpx
    monkeypatch.setenv('WECHAT_APP_ID','test-app')
    monkeypatch.setenv('WECHAT_APP_SECRET','test-secret')
    class Provider:
        def __init__(self,**kw):pass
        async def __aenter__(self):return self
        async def __aexit__(self,*args):pass
        async def get(self,url,**kw):return httpx.Response(200,json={'openid':'stable-user'},request=httpx.Request('GET',url))
    monkeypatch.setattr(module.httpx,'AsyncClient',Provider)
    first=client.post('/sessions/wechat',json={'nickname':'玩家','code':'first'}).json()
    second=client.post('/sessions/wechat',json={'nickname':'玩家','code':'second'}).json()
    assert first['profile']['playerId']==second['profile']['playerId']
    assert first['token']!=second['token']
    assert client.get('/profiles/me',headers={'Authorization':'Bearer '+first['token']}).status_code==401

def test_voice_upload_format_is_forwarded(client,monkeypatch):
    import httpx
    for key in ['ASR_API_KEY','ASR_BASE_URL','ASR_MODEL']:monkeypatch.setenv(key,'test')
    class Provider:
        def __init__(self,**kw):pass
        async def __aenter__(self):return self
        async def __aexit__(self,*args):pass
        async def post(self,url,**kw):
            assert kw['files']['file'][0]=='recording.webm'
            return httpx.Response(200,json={'text':'我喜欢篮球'},request=httpx.Request('POST','https://provider.example'))
    monkeypatch.setattr(module.httpx,'AsyncClient',Provider)
    headers,_=credentials(client)
    response=client.post('/speech/transcribe',headers=headers,files={'file':('voice.webm',b'audio','audio/webm')})
    assert response.status_code==200 and response.json()['text']=='我喜欢篮球'


def test_removed_intro_image_endpoint_has_no_upload_flow(client):
    headers,_=credentials(client)
    assert client.post('/introduction/recognize-image',headers=headers).status_code==404
    assert module.health()['introductionImageRecognition'] is False


def test_vision_can_use_existing_deepseek_text_credentials(monkeypatch):
    monkeypatch.delenv('DEEPSEEK_API_KEY',raising=False)
    monkeypatch.setenv('LLM_API_KEY','test-text-key')
    monkeypatch.setenv('LLM_BASE_URL','https://api.deepseek.com/v1')
    assert module.deepseek_vision_credentials()==('test-text-key','https://api.deepseek.com/v1')
    assert module.health()['visionConfigured'] is True


def test_timeout_result_keeps_co_winners_and_rankings_once(client):
    headers = {'X-Game-Key': 'test-server-key'}
    body = {'matchId': 'timeout-tie', 'winner': '', 'winners': ['p1', 'p2'],
            'endReason': 'timeout', 'eaten': [], 'rankings': [
                {'playerId': p, 'rank': 1, 'eaten': 2, 'remaining': 3, 'eliminated': False}
                for p in ['p1', 'p2']]}
    assert client.post('/internal/results', json=body, headers=headers).status_code == 200
    changed = dict(body, winners=['p3'])
    assert client.post('/internal/results', json=changed, headers=headers).status_code == 200
    with module.db() as con:
        rows = con.execute('SELECT result FROM matches WHERE id=?', ('timeout-tie',)).fetchall()
    assert len(rows) == 1
    saved = json.loads(rows[0][0])
    assert saved['winners'] == ['p1', 'p2']
    assert saved['endReason'] == 'timeout' and saved['rankings'] == body['rankings']


def test_catalog_upgrade_retries_newly_supported_red_prop_once(client,monkeypatch):
    headers,data=credentials(client);intro='我喜欢望远镜'
    old=dict(data['profile'],personas=[],generationCandidates=[],generationTerms=[dict(term='望远镜',source=intro,status='failed')],revision=1,catalogGenerationVersion='older-catalog')
    with module.db() as con:
        con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',(json.dumps(old),intro,data['profile']['playerId']))
    calls=[]
    async def generated(text):
        calls.append(text)
        return module.validate_personas({'terms':[dict(term='望远镜',source=text)],'personas':[dict(name='望远镜',term='望远镜',source=text,templateId='Utility',visualId='telescope')]},text)
    monkeypatch.setattr(module,'generate_with_provider',generated)
    body=dict(nickname='玩家',introduction=intro,generationId='f'*32)
    result=client.post('/personas/generate',headers=headers,json=body)
    assert result.status_code==200 and result.json()['revision']==2
    assert result.json()['personas'][0]['visualId']=='telescope' and result.json()['generationTerms'][0]['status']=='ready'
    again=client.post('/personas/generate',headers=headers,json=body)
    assert again.status_code==200 and again.json()['revision']==2 and calls==[intro]


def test_catalog_upgrade_does_not_retry_negative_or_unsupported_failed_words():
    for word,source in [('望远镜','我不喜欢望远镜'),('瞬间移动','我会瞬间移动')]:
        old=dict(personas=[],generationCandidates=[],generationTerms=[dict(term=word,source=source)],catalogGenerationVersion='older')
        assert not module.catalog_upgrade_can_retry_props(old,source)

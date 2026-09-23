import json
import pytest
from fastapi.testclient import TestClient
from server import app as module

@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv('ABOUTME_DB', str(tmp_path / 'test.sqlite3'))
    monkeypatch.setenv('GAME_SERVER_KEY', 'test-server-key')
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
    assert len(module.validate_personas(raw)) == 7
    raw['personas'][0]['templateId'] = 'RunArbitraryCode'
    with pytest.raises(ValueError): module.validate_personas(raw)
    with pytest.raises(ValueError): module.validate_personas({'personas': []})

def test_cc0_visual_catalog_allows_only_published_template_pairs():
    assert len(module.CATALOG) == 56
    result = module.validate_personas({'personas': [dict(name='抱枕', source='抱枕', templateId='Cushion', visualId='pillow-a')]})
    assert result[0]['color'] == '#F5C0CF'
    with pytest.raises(ValueError):
        module.validate_personas({'personas': [dict(name='抱枕', source='抱枕', templateId='Projectile', visualId='pillow-a')]})

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
    preset = module.preset_personas('我在中山大学读书')
    assert len(preset) == 1 and preset[0]['name'] == '中山大学校徽圆盘'
    assert module.preset_personas('我在中山大学读书，也喜欢篮球') is None

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


def test_intro_image_uses_deepseek_v4_flash_vision_and_returns_editable_intro(client,monkeypatch):
    import httpx
    monkeypatch.setenv('DEEPSEEK_API_KEY','test-key')
    monkeypatch.setenv('DEEPSEEK_BASE_URL','https://deepseek.example/v1')
    class Provider:
        def __init__(self,**kw):pass
        async def __aenter__(self):return self
        async def __aexit__(self,*args):pass
        async def post(self,url,**kw):
            assert url=='https://deepseek.example/v1/chat/completions'
            assert kw['json']['model']=='deepseek-v4-flash-vision-exp'
            content=kw['json']['messages'][1]['content']
            assert content[1]['type']=='image_url' and content[1]['image_url']['url'].startswith('data:image/png;base64,')
            return httpx.Response(200,json={'choices':[{'message':{'content':'{"introduction":"我喜欢篮球和摄影"}'}}]},request=httpx.Request('POST',url))
    monkeypatch.setattr(module.httpx,'AsyncClient',Provider)
    headers,_=credentials(client)
    png=b'\x89PNG\r\n\x1a\n' + b'test image'
    response=client.post('/introduction/recognize-image',headers=headers,files={'file':('intro.png',png,'image/png')})
    assert response.status_code==200 and response.json()=={'introduction':'我喜欢篮球和摄影'}


def test_intro_image_rejects_non_images(client):
    headers,_=credentials(client)
    response=client.post('/introduction/recognize-image',headers=headers,files={'file':('intro.txt',b'not an image','text/plain')})
    assert response.status_code==415


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


def roast_provider(monkeypatch, content, url_expected='https://api.deepseek.com/v1/chat/completions'):
    import httpx
    for key, value in [('LLM_API_KEY', 'test-key'), ('LLM_MODEL', 'deepseek-flash'), ('LLM_BASE_URL', 'https://api.deepseek.com/v1')]:
        monkeypatch.setenv(key, value)
    class Provider:
        def __init__(self, **kw): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kw):
            assert url == url_expected
            assert kw['json']['model'] == 'deepseek-flash'
            assert kw['json']['messages'][0]['content'] == module.ROAST_SYSTEM_PROMPT
            return httpx.Response(200, json={'choices': [{'message': {'content': content}}]}, request=httpx.Request('POST', url))
    monkeypatch.setattr(module.httpx, 'AsyncClient', Provider)


def test_roast_agent_returns_one_line_and_needs_login(client, monkeypatch):
    line = '养猫的都自称铲屎官，这局你铲的不是猫砂，是自己掉的命。'
    roast_provider(monkeypatch, line)
    assert client.post('/roast', json={'weapon_name': '猫'}).status_code == 401
    headers, _ = credentials(client)
    body = {'weapon_name': '猫', 'picker_name': '张三', 'loser_name': '李四'}
    assert client.post('/roast', headers=headers, json=body).json() == {'roast': line}
    assert client.post('/roast', headers=headers, json={'weapon_name': ''}).status_code == 422


def test_roast_agent_falls_back_when_the_line_breaks_the_length_rule(client, monkeypatch):
    roast_provider(monkeypatch, '太短')
    headers, _ = credentials(client)
    response = client.post('/roast', headers=headers, json={'weapon_name': '猫'})
    assert response.status_code == 200
    assert response.json()['roast'] == module.ROAST_FALLBACK
    assert not module.roast_is_bounded('太短') and not module.roast_is_bounded('长' * 41)
    assert module.roast_is_bounded('养猫的都自称铲屎官，这局你铲的不是猫砂，是自己掉的命。')


def test_roast_agent_needs_real_ai(client, monkeypatch):
    monkeypatch.delenv('LLM_API_KEY', raising=False)
    headers, _ = credentials(client)
    assert client.post('/roast', headers=headers, json={'weapon_name': '猫'}).status_code == 503

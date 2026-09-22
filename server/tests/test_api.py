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

def test_introduction_length_is_bounded_before_provider(client):
    headers, _ = credentials(client)
    response = client.post('/personas/generate', headers=headers,
        json={'nickname':'玩家', 'introduction':'长' * (module.MAX_INTRO_CHARS + 1)})
    assert response.status_code == 422

def test_generation_persistence_reroll_and_new_assets(client, monkeypatch):
    async def fake(_): return module.validate_personas(candidates())
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

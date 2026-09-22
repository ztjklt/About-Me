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

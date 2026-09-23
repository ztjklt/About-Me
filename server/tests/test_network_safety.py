import json
from types import SimpleNamespace

import httpx
import pytest
from fastapi import HTTPException
from fastapi.testclient import TestClient

from server import app as module


@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv('ABOUTME_DB', str(tmp_path / 'safety.sqlite3'))
    monkeypatch.setenv('LLM_API_KEY', 'test-key')
    monkeypatch.setenv('LLM_MODEL', 'test-model')
    monkeypatch.setenv('LLM_BASE_URL', 'https://provider.example/v1')
    monkeypatch.setenv('GAME_SERVER_KEY', 'test-server-key')
    return TestClient(module.app)


def identity(client):
    token = client.post('/sessions', json={'nickname': '玩家'}).json()['token']
    return {'Authorization': 'Bearer ' + token}


BODY = {'item_name': '篮球', 'template_id': 'Projectile', 'visual_id': 'basketball'}


def test_roast_forbids_source_and_rejects_private_name(client, monkeypatch):
    headers = identity(client)
    assert client.post('/roast', json=dict(BODY, item_context='我的原始介绍'), headers=headers).status_code == 422
    assert client.post('/roast', json=dict(BODY, item_name='我的电话13800138000'), headers=headers).status_code == 422
    assert client.post('/roast', json=dict(BODY, item_name='1 3 8 0 0 1 3 8 0 0 0'), headers=headers).status_code == 422
    assert client.post('/roast', json=dict(BODY, visual_id='unknown'), headers=headers).status_code == 422
    sent = []

    class Provider:
        def __init__(self, **kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kwargs):
            sent.append(json.loads(kwargs['json']['messages'][1]['content']))
            return httpx.Response(200, json={'choices': [{'message': {'content': '篮球刚上场就想投三分，结果先投进了对手的胃里。'}}]},
                                  request=httpx.Request('POST', url))

    monkeypatch.setattr(module.httpx, 'AsyncClient', Provider)
    result = client.post('/roast', json=BODY, headers=headers)
    assert result.status_code == 200
    assert '篮球' in result.json()['roast']
    assert sent == [BODY]


def test_unsafe_provider_output_is_never_returned(client, monkeypatch):
    headers = identity(client)

    class Provider:
        def __init__(self, **kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kwargs):
            return httpx.Response(200, json={'choices': [{'message': {'content': '请联系13800138000拿到对方的电话号码。'}}]},
                                  request=httpx.Request('POST', url))

    monkeypatch.setattr(module.httpx, 'AsyncClient', Provider)
    result = client.post('/roast', json=BODY, headers=headers)
    assert result.status_code == 200
    assert result.json()['roast'] == module.ROAST_FALLBACK
    assert '13800138000' not in result.text


def test_new_identities_share_ai_peer_budget(client, monkeypatch):
    async def fake(_): return '篮球刚上场就想投三分，结果先投进了对手的胃里。'
    monkeypatch.setattr(module, 'roast_with_provider', fake)
    headers = [identity(client) for _ in range(7)]
    statuses = [client.post('/roast', json=BODY, headers=item).status_code for item in headers]
    assert statuses == [200] * 6 + [429]


def test_legacy_unsafe_persona_cannot_enter_multiplayer(client):
    headers = identity(client)
    token = headers['Authorization'][7:]
    player_id, stored, _ = module.authenticate(headers['Authorization'])
    profile = json.loads(stored)
    profile['personas'] = [{'assetId': 'old', 'name': '电话13800138000',
                            'source': '篮球', 'templateId': 'Projectile',
                            'visualId': 'basketball', 'color': '#FFAA55'}]
    with module.db() as con:
        con.execute('UPDATE profiles SET profile=? WHERE id=?', (json.dumps(profile), player_id))
    response = client.post('/internal/verify', json={'token': token},
                           headers={'X-Game-Key': 'test-server-key'})
    assert response.status_code == 422


def test_global_budgets_apply_across_distinct_peers(client):
    for index in range(30):
        module.enforce_quota(SimpleNamespace(client=SimpleNamespace(host=f'ai-{index}')), 'ai')
    with pytest.raises(HTTPException) as error:
        module.enforce_quota(SimpleNamespace(client=SimpleNamespace(host='new-identity')), 'ai')
    assert error.value.status_code == 429


def test_only_authenticated_proxy_header_splits_peer_quota(client, monkeypatch):
    monkeypatch.setenv('ABOUTME_PROXY_KEY', 'test-proxy-secret')

    def request(ip, secret='test-proxy-secret'):
        return SimpleNamespace(client=SimpleNamespace(host='caddy-container'),
                               headers={'x-aboutme-client-ip': ip, 'x-aboutme-proxy-key': secret})

    assert module.quota_peer(request('2001:0db8::1')) == '2001:db8::1'
    assert module.quota_peer(request('198.51.100.1', 'wrong')) == 'caddy-container'
    assert module.quota_peer(request('198.51.100.1, 198.51.100.2')) == 'caddy-container'
    for index in range(6):
        module.enforce_quota(request(f'198.51.100.{index + 1}'), 'ai')
    for index in range(6):
        module.enforce_quota(request(f'203.0.113.{index + 1}', 'wrong'), 'ai')
    with pytest.raises(HTTPException) as error:
        module.enforce_quota(request('198.51.100.99', 'wrong'), 'ai')
    # An untrusted header shares the direct peer bucket; it cannot mint new peers.
    assert error.value.status_code == 429

    for index in range(120):
        module.enforce_quota(SimpleNamespace(client=SimpleNamespace(host=f'session-{index}')), 'sessions')
    with pytest.raises(HTTPException) as error:
        module.enforce_quota(SimpleNamespace(client=SimpleNamespace(host='another-peer')), 'sessions')
    assert error.value.status_code == 429

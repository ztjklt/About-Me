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
ROAST_LINE = '篮球刚上场就想投三分，结果先投进了对手的胃里。'


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
    assert result.status_code == 502
    assert '13800138000' not in result.text


def test_internal_roast_is_authenticated_and_idempotent(client, monkeypatch):
    calls = []
    async def fake(item):
        calls.append(item)
        return ROAST_LINE
    monkeypatch.setattr(module, 'roast_with_provider', fake)
    body = dict(BODY, match_id='a' * 32, event_id='b' * 32)
    assert client.post('/internal/roast', json=body).status_code == 403
    auth = {'X-Game-Key': 'test-server-key'}
    first = client.post('/internal/roast', json=body, headers=auth)
    assert first.status_code == 200
    assert first.json() == {'event_id': 'b' * 32, 'roast': ROAST_LINE, 'aiSource': 'provider'}
    assert client.post('/internal/roast', json=body, headers=auth).json() == first.json()
    assert len(calls) == 1 and calls[0] == BODY
    with module.db() as con:
        billed = con.execute("SELECT count FROM request_limits WHERE bucket LIKE 'ai:daily:%'").fetchone()[0]
    assert billed == 1
    assert client.post('/internal/roast', json=dict(body, item_name='排球'), headers=auth).status_code == 409
    assert client.post('/internal/roast', json=dict(body, source='私密介绍'), headers=auth).status_code == 422


def test_failed_internal_roast_does_not_cache_fake_success(client, monkeypatch):
    body = dict(BODY, match_id='c' * 32, event_id='d' * 32)
    auth = {'X-Game-Key': 'test-server-key'}
    async def fail(_):
        raise HTTPException(502, '模型失败')
    monkeypatch.setattr(module, 'roast_with_provider', fail)
    assert client.post('/internal/roast', json=body, headers=auth).status_code == 502
    with module.db() as con:
        assert con.execute('SELECT COUNT(*) FROM roast_events').fetchone()[0] == 0
    async def succeed(_): return ROAST_LINE
    monkeypatch.setattr(module, 'roast_with_provider', succeed)
    assert client.post('/internal/roast', json=body, headers=auth).status_code == 200


def test_pending_internal_roast_rejects_duplicate_without_provider_call(client, monkeypatch):
    body = dict(BODY, match_id='1' * 32, event_id='2' * 32)
    item = module.validated_roast_item(module.InternalRoastRequest(**body))
    digest = module.hashlib.sha256(json.dumps(item, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    with module.db() as con:
        con.execute('INSERT INTO roast_events VALUES (?,?,?,?,?)',
                    (body['match_id'] + ':' + body['event_id'], digest, 'pending', None, module.time.time()))
    async def unexpected(_):
        pytest.fail('duplicate pending event called provider')
    monkeypatch.setattr(module, 'roast_with_provider', unexpected)
    response = client.post('/internal/roast', json=body, headers={'X-Game-Key': 'test-server-key'})
    assert response.status_code == 409
    with module.db() as con:
        assert con.execute("SELECT count(*) FROM request_limits WHERE bucket LIKE 'ai:daily:%'").fetchone()[0] == 0


def test_unconfigured_internal_roast_returns_503_without_cache(client, monkeypatch):
    monkeypatch.delenv('LLM_API_KEY')
    body = dict(BODY, match_id='e' * 32, event_id='f' * 32)
    result = client.post('/internal/roast', json=body, headers={'X-Game-Key': 'test-server-key'})
    assert result.status_code == 503
    with module.db() as con:
        assert con.execute('SELECT COUNT(*) FROM roast_events').fetchone()[0] == 0


def test_school_only_generation_requires_real_provider(client, monkeypatch):
    monkeypatch.delenv('LLM_API_KEY')
    headers = identity(client)
    response = client.post('/personas/generate', headers=headers,
                           json={'nickname': '玩家', 'introduction': '我在中山大学读书'})
    assert response.status_code == 503


def test_public_playtest_ai_budget_is_configurable(client, monkeypatch):
    monkeypatch.setenv('ABOUTME_AI_DAILY_LIMIT', '2')
    request = SimpleNamespace(client=SimpleNamespace(host='shared-api-peer'))
    module.enforce_quota(request, 'ai_internal')
    module.enforce_quota(request, 'ai_internal')
    with pytest.raises(HTTPException) as error:
        module.enforce_quota(request, 'ai_internal')
    assert error.value.status_code == 429
    monkeypatch.setenv('ABOUTME_AI_DAILY_LIMIT', 'invalid')
    with pytest.raises(HTTPException) as error:
        module.enforce_quota(request, 'ai_internal')
    assert error.value.status_code == 503


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

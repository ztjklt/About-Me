"""The DS doll is a cosmetic backed by the saved introduction, never model output."""
from fastapi import HTTPException

from server import app as module
from server.easter_egg import likes_deepseek
from server.tests.test_api import client, credentials


def test_only_explicit_positive_deepseek_likes_unlock():
    for introduction in (
        '我喜欢DeepSeek', '喜欢 deep seek', '我很喜欢 DEEP  SEEK',
        '我不喜欢DeepSeek，但现在喜欢 DeepSeek', '我和朋友都喜欢DeepSeek',
    ):
        assert likes_deepseek(introduction), introduction
    for introduction in (
        '我不喜欢DeepSeek', '我不太喜欢 Deep Seek', '朋友喜欢DeepSeek',
        '我的朋友喜欢DeepSeek', '我听朋友说喜欢DeepSeek',
        '如果我喜欢DeepSeek', '我假装喜欢DeepSeek',
        '我曾喜欢DeepSeek但现在不喜欢DeepSeek',
        'DeepSeek喜欢我', '我喜欢别的产品', '我喜欢DeepSeek吗？', '我喜欢DeepSeek？',
    ):
        assert not likes_deepseek(introduction), introduction


def test_trusted_profile_unlock_selection_and_revocation(client, monkeypatch):
    async def basketball_provider(introduction):
        return module.validate_personas({'personas': [dict(name='篮球', source='喜欢篮球',
            templateId='Projectile', visualId='basketball')]}, introduction)
    monkeypatch.setattr(module, 'generate_with_provider', basketball_provider)
    headers, session = credentials(client)
    assert session['profile']['dsGirlUnlocked'] is False
    assert session['profile']['characterSkin'] == 'normal'
    locked = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0, 'characterSkin': 'ds-girl'})
    assert locked.status_code == 422
    intro = '我喜欢篮球，也喜欢 dEeP  SeEk'
    generated = client.post('/personas/generate', headers=headers,
        json={'nickname': '玩家', 'introduction': intro})
    assert generated.status_code == 200
    assert generated.json()['dsGirlUnlocked'] is True
    assert generated.json()['characterSkin'] == 'normal'
    assert len(generated.json()['personas']) == 1
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0, 'characterSkin': 'other'}).status_code == 422
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0, 'dsGirlUnlocked': True}).status_code == 422
    selected = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {'hair': 'tied'}, 'expectedRevision': 0, 'characterSkin': 'ds-girl'})
    assert selected.status_code == 200
    assert selected.json()['characterSkin'] == 'ds-girl'
    assert selected.json()['appearanceRevision'] == 1
    own = client.get('/profiles/me', headers=headers).json()
    verified = client.post('/internal/verify', headers={'X-Game-Key': 'test-server-key'},
        json={'token': session['token']}).json()
    assert own['characterSkin'] == verified['characterSkin'] == 'ds-girl'
    assert own['dsGirlUnlocked'] is verified['dsGirlUnlocked'] is True
    assert own['personas'] == generated.json()['personas']
    assert [p['assetId'] for p in own['personas']] == [p['assetId'] for p in verified['personas']]
    assert verified['personas'][0]['visualId'] == 'basketball'
    assert all('source' not in p for p in verified['personas'])
    async def unavailable(_):
        raise HTTPException(502, '模型失败')
    monkeypatch.setattr(module, 'generate_with_provider', unavailable)
    failed = client.post('/personas/generate', headers=headers,
        json={'nickname': '玩家', 'introduction': '我喜欢篮球，但不喜欢DeepSeek'})
    assert failed.status_code == 502
    assert client.get('/profiles/me', headers=headers).json()['characterSkin'] == 'ds-girl'
    monkeypatch.setattr(module, 'generate_with_provider', basketball_provider)
    negative = client.post('/personas/generate', headers=headers,
        json={'nickname': '玩家', 'introduction': '我喜欢篮球，但不喜欢DeepSeek'})
    assert negative.status_code == 200
    assert negative.json()['dsGirlUnlocked'] is False
    assert negative.json()['characterSkin'] == 'normal'
    assert negative.json()['appearanceRevision'] == 1
    assert client.get('/profiles/me', headers=headers).json()['characterSkin'] == 'normal'
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 1, 'characterSkin': 'ds-girl'}).status_code == 422


def test_existing_intro_is_rechecked_without_model_call(client):
    headers, session = credentials(client)
    with module.db() as con:
        con.execute('UPDATE profiles SET intro=? WHERE id=?',
                    ('我喜欢DeepSeek', session['profile']['playerId']))
    profile = client.get('/profiles/me', headers=headers).json()
    assert profile['dsGirlUnlocked'] is True
    assert profile['characterSkin'] == 'normal'
    selected = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0, 'characterSkin': 'ds-girl'})
    assert selected.status_code == 200
    revoked = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 1, 'introduction': '我不喜欢DeepSeek'})
    assert revoked.status_code == 200
    current = client.get('/profiles/me', headers=headers).json()
    assert current['dsGirlUnlocked'] is False
    assert current['characterSkin'] == 'normal'
    assert client.post('/internal/verify', headers={'X-Game-Key': 'test-server-key'},
        json={'token': session['token']}).json()['characterSkin'] == 'normal'


def test_intro_only_deepseek_like_unlocks_without_persona_or_model(client, monkeypatch):
    async def unexpected(_):
        raise AssertionError('cosmetic save called model')
    monkeypatch.setattr(module, 'generate_with_provider', unexpected)
    headers, session = credentials(client)
    selected = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0,
              'introduction': '我喜欢 Deep Seek', 'characterSkin': 'ds-girl'})
    assert selected.status_code == 200
    assert selected.json()['dsGirlUnlocked'] is True
    assert selected.json()['characterSkin'] == 'ds-girl'
    assert selected.json()['personas'] == []
    verified = client.post('/internal/verify', headers={'X-Game-Key': 'test-server-key'},
        json={'token': session['token']})
    assert verified.status_code == 200
    assert verified.json()['characterSkin'] == 'ds-girl'
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 1,
              'introduction': '我不喜欢DeepSeek', 'characterSkin': 'ds-girl'}).status_code == 422
    assert client.get('/profiles/me', headers=headers).json()['characterSkin'] == 'ds-girl'

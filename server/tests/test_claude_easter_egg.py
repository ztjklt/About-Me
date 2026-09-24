"""Claude and DeepSeek skins use independent, persisted introduction entitlements."""
import json

from server import app as module
from server.appearance import normalized_profile
from server.easter_egg import liked_brands, likes_claude
from server.tests.test_api import client, credentials


def test_claude_positive_negative_and_parallel_introductions():
    for introduction in ('我喜欢Claude', '喜欢 cLaUdE', '我很喜欢 Claude',
                         '我不喜欢Claude，但现在喜欢Claude'):
        assert likes_claude(introduction), introduction
    for introduction in ('我不喜欢Claude', '我不太喜欢Claude', '朋友喜欢Claude',
                         '我的朋友喜欢Claude', '如果我喜欢Claude', '我假装喜欢Claude',
                         '我喜欢Claude吗？', 'Claude喜欢我', '我喜欢ClaudeSonnet'):
        assert not likes_claude(introduction), introduction
    cases = (
        ('我喜欢DeepSeek和Claude', True, True),
        ('我喜欢DeepSeek，也喜欢Claude', True, True),
        ('我喜欢DeepSeek，不喜欢Claude', True, False),
        ('我不喜欢DeepSeek，但喜欢Claude', False, True),
        ('我喜欢Claude和DeepSeek', True, True),
    )
    for introduction, ds, claude in cases:
        assert liked_brands(introduction) == {'deepseek': ds, 'claude': claude}


def test_free_save_selection_independent_revocation_and_trusted_profile(client, monkeypatch):
    async def unexpected(_):
        raise AssertionError('appearance save must not call the model')
    monkeypatch.setattr(module, 'generate_with_provider', unexpected)
    headers, session = credentials(client)
    assert session['profile']['claudeGirlUnlocked'] is False
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0,
              'characterSkin': 'claude-girl'}).status_code == 403
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0,
              'claudeGirlUnlocked': True}).status_code == 422
    both = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0,
              'introduction': '我喜欢DeepSeek和Claude', 'characterSkin': 'claude-girl'})
    assert both.status_code == 200
    assert both.json()['dsGirlUnlocked'] is both.json()['claudeGirlUnlocked'] is True
    assert both.json()['characterSkin'] == 'claude-girl'
    assert both.json()['personas'] == []
    own = client.get('/profiles/me', headers=headers).json()
    verified = client.post('/internal/verify', headers={'X-Game-Key': 'test-server-key'},
        json={'token': session['token']}).json()
    assert own == verified == both.json()
    ds_only = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 1,
              'introduction': '我喜欢DeepSeek，不喜欢Claude'})
    assert ds_only.status_code == 200
    assert ds_only.json()['dsGirlUnlocked'] is True
    assert ds_only.json()['claudeGirlUnlocked'] is False
    assert ds_only.json()['characterSkin'] == 'normal'
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 2,
              'characterSkin': 'claude-girl'}).status_code == 403
    selected_ds = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 2,
              'characterSkin': 'ds-girl'})
    assert selected_ds.status_code == 200
    claude_only = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 3,
              'introduction': '我不喜欢DeepSeek，但喜欢Claude'})
    assert claude_only.status_code == 200
    assert claude_only.json()['dsGirlUnlocked'] is False
    assert claude_only.json()['claudeGirlUnlocked'] is True
    assert claude_only.json()['characterSkin'] == 'normal'
    assert client.get('/profiles/me', headers=headers).json() == claude_only.json()


def test_legacy_profile_defaults_and_existing_introduction(client):
    headers, session = credentials(client)
    legacy = {'playerId': session['profile']['playerId'], 'nickname': '旧角色',
              'color': '#79CFC5', 'outfit': 0, 'personas': [], 'revision': 0,
              'dsGirlUnlocked': True, 'characterSkin': 'ds-girl'}
    with module.db() as con:
        con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',
            (json.dumps(legacy), '我喜欢Claude', legacy['playerId']))
    current = client.get('/profiles/me', headers=headers).json()
    assert current['dsGirlUnlocked'] is True
    assert current['claudeGirlUnlocked'] is True
    assert current['characterSkin'] == 'ds-girl'
    assert normalized_profile({'characterSkin': 'claude-girl'})['characterSkin'] == 'normal'
    assert client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {}, 'expectedRevision': 0,
              'characterSkin': 'claude-girl'}).status_code == 200
    assert client.get('/profiles/me', headers=headers).json()['characterSkin'] == 'claude-girl'


def test_generation_recomputes_both_qualifications_without_losing_appearance(client, monkeypatch):
    async def provider(introduction):
        return module.validate_personas({'personas': [
            {'name': '篮球', 'source': '喜欢篮球', 'templateId': 'Projectile', 'visualId': 'basketball'}
        ]}, introduction)
    monkeypatch.setattr(module, 'generate_with_provider', provider)
    headers, _ = credentials(client)
    saved = client.post('/profiles/me/appearance', headers=headers,
        json={'appearance': {'hair': 'tied'}, 'expectedRevision': 0,
              'introduction': '我喜欢Claude', 'characterSkin': 'claude-girl'}).json()
    generated = client.post('/personas/generate', headers=headers,
        json={'nickname': '玩家', 'introduction': '我喜欢篮球，也喜欢DeepSeek和Claude'})
    assert generated.status_code == 200
    assert generated.json()['characterSkin'] == 'claude-girl'
    assert generated.json()['dsGirlUnlocked'] is generated.json()['claudeGirlUnlocked'] is True
    assert generated.json()['appearance'] == saved['appearance']
    assert generated.json()['appearanceRevision'] == saved['appearanceRevision']
    changed = client.post('/personas/generate', headers=headers,
        json={'nickname': '玩家', 'introduction': '我喜欢篮球，也喜欢DeepSeek，但不喜欢Claude'})
    assert changed.status_code == 200
    assert changed.json()['dsGirlUnlocked'] is True
    assert changed.json()['claudeGirlUnlocked'] is False
    assert changed.json()['characterSkin'] == 'normal'

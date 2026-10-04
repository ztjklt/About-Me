from server.easter_egg import liked_brands
from server.tests.test_api import client, credentials


def test_questionnaire_english_positive_negative_and_mentions():
    for text in ('I like DeepSeek, Claude.', 'I prefer DeepSeek and Claude.', 'I use DeepSeek and Claude.'):
        assert liked_brands(text) == {'deepseek': True, 'claude': True}
    for text in ("I don't like DeepSeek or Claude.", 'I do not use Claude.', 'I dislike DeepSeek.',
                 'My friend likes DeepSeek.', 'I read about Claude.', 'If I like Claude.',
                 'I like DeepSeek?', 'She said "I like Claude".', 'I like UnknownAI.'):
        assert liked_brands(text) == {'deepseek': False, 'claude': False}, text
    assert liked_brands("我喜欢 DeepSeek。 I don't like DeepSeek.")['deepseek'] is False
    assert liked_brands("I dislike Claude. 我喜欢 Claude。")['claude'] is True


def test_english_saved_preference_keeps_whitelist_and_personas(client):
    headers, session = credentials(client)
    before = session['profile']['personas']
    saved = client.post('/profiles/me/appearance', headers=headers, json={
        'expectedRevision': 0, 'appearance': {'expression': 'confident'},
        'introduction': 'I like DeepSeek, Claude.', 'characterSkin': 'normal'})
    assert saved.status_code == 200
    profile = saved.json()
    assert profile['dsGirlUnlocked'] and profile['claudeGirlUnlocked']
    assert profile['personas'] == before
    locked = client.post('/profiles/me/appearance', headers=headers, json={
        'expectedRevision': profile['appearanceRevision'], 'appearance': {}, 'characterSkin': 'unknown-ai'})
    assert locked.status_code == 422
    own = client.get('/profiles/me', headers=headers).json()
    assert own['appearance']['expression'] == 'confident'
    assert own['personas'] == before

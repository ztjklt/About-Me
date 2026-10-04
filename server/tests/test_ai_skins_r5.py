"""Catalog entitlements and persisted selection, using actual API handlers and SQLite.
No model response is involved in cosmetic unlock/save.
"""
import json
import pytest
from server import app as module
from server.appearance import normalized_profile
from server.easter_egg import liked_ai_brands, SKINS
from server.tests.test_api import client, credentials

@pytest.mark.parametrize('skin',SKINS,ids=lambda s:s['brand'])
def test_positive_only_aliases_and_latest_stance(skin):
    brand=skin['brand'];display={'deepseek':'DeepSeek','gpt':'ChatGPT','qwen':'通义千问','doubao':'豆包','glm':'智谱清言'}.get(brand,brand)
    for text in [f'我喜欢{display}',f'I like {display}.',f'我不喜欢{display}，但我喜欢{display}']:
        assert liked_ai_brands(text)[brand],text
    for text in [f'我不喜欢{display}',f'朋友喜欢{display}',f'我的朋友喜欢{display}',f'如果我喜欢{display}',f'我假装喜欢{display}',f'我喜欢{display}吗？',f'I do not like {display}.',f'My friend likes {display}.',f'If I like {display}.',f'I like {display}?',f'我喜欢{display}Sonnet']:
        assert not liked_ai_brands(text)[brand],text
    assert not liked_ai_brands(f'我喜欢{display}，我不喜欢{display}')[brand]

@pytest.mark.parametrize('skin',SKINS,ids=lambda s:s['brand'])
def test_locked_then_select_persist_public_and_revoke(client,skin):
    headers,session=credentials(client);value=skin['skin']
    denied=client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':0,'characterSkin':value})
    assert denied.status_code in (403,422)
    assert client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':0,'unlockedCharacterSkins':[value]}).status_code==422
    unlocked=client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':0,'introduction':'我喜欢'+skin['brand']}).json()
    assert unlocked['characterSkin']=='normal' and value in unlocked['unlockedCharacterSkins']
    selected=client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':1,'characterSkin':value})
    assert selected.status_code==200;profile=selected.json();assert profile['characterSkin']==value
    assert client.get('/profiles/me',headers=headers).json()==profile
    # Reopen the application over the same database; selection comes from persistent storage.
    from fastapi.testclient import TestClient
    with TestClient(module.app) as reopened:assert reopened.get('/profiles/me',headers=headers).json()==profile
    verified=client.post('/internal/verify',headers={'X-Game-Key':'test-server-key'},json={'token':session['token']}).json()
    assert verified['characterSkin']==value and value in verified['unlockedCharacterSkins']
    assert all(k not in verified for k in ['introduction','source','generationTerms','wordAbilities'])
    revoked=client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':2,'introduction':'我不喜欢'+skin['brand']}).json()
    assert value not in revoked['unlockedCharacterSkins'] and revoked['characterSkin']=='normal'


def test_all_brands_parallel_independent_and_legacy(client):
    text='我喜欢DeepSeek、Claude、GPT、Gemini、Grok、Kimi、千问、MiniMax、豆包、GLM'
    assert all(liked_ai_brands(text).values())
    assert all(liked_ai_brands('I like DeepSeek, Claude, GPT, Gemini, Grok, Kimi, Qwen, MiniMax, Doubao and GLM.').values())
    headers,_=credentials(client)
    response=client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':0,'introduction':text,'characterSkin':'gpt-girl'})
    assert response.status_code==200;assert len(response.json()['unlockedCharacterSkins'])==10
    assert response.json()['dsGirlUnlocked'] and response.json()['claudeGirlUnlocked']
    changed=client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':1,'introduction':text+'。我不喜欢Kimi'})
    assert changed.status_code==200 and changed.json()['characterSkin']=='gpt-girl' and 'kimi-girl' not in changed.json()['unlockedCharacterSkins']
    assert normalized_profile({'dsGirlUnlocked':True,'characterSkin':'ds-girl'})['characterSkin']=='ds-girl'
    assert normalized_profile({'characterSkin':'gpt-girl'})['characterSkin']=='normal'
    assert client.post('/profiles/me/appearance',headers=headers,json={'appearance':{'character':'gpt'},'expectedRevision':2}).status_code==422
    assert client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':2,'characterSkin':'invented-girl'}).status_code==422


def test_generated_traits_keep_selected_skin_without_extra_personas(client,monkeypatch):
    # Only provider extraction is a fixture; trait augmentation and persistence are actual handlers.
    async def provider(intro):return module.validate_personas({"personas":[]},intro)
    monkeypatch.setattr(module,"generate_with_provider",provider)
    headers,_=credentials(client)
    saved=client.post('/profiles/me/appearance',headers=headers,json={'appearance':{},'expectedRevision':0,'introduction':'我喜欢GPT','characterSkin':'gpt-girl'}).json()
    generated=client.post('/personas/generate',headers=headers,json={'nickname':'字玩家','introduction':'我喜欢GPT。我的性格是热情。','generationId':'a'*32})
    assert generated.status_code==200,generated.text
    assert generated.json()['characterSkin']=='gpt-girl'
    assert len(generated.json()['personas'])==1 and generated.json()['personas'][0]['visualId']=='trait-word'
    assert generated.json()['personas'][0]['abilityId']=='flame-fan'

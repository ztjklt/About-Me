import pytest
import asyncio
from unittest.mock import AsyncMock
from fastapi import HTTPException
from server.display_language import DisplayLanguageRequest
import server.display_language as language

def test_display_text_is_bounded():
    with pytest.raises(ValueError):
        DisplayLanguageRequest(text='a'*601)
    with pytest.raises(ValueError):
        DisplayLanguageRequest(text='hello', api_key='never-client-side')

async def scenario_display_translation_requires_server_provider(monkeypatch):
    monkeypatch.delenv('LLM_API_KEY', raising=False)
    language._cache.clear()
    with pytest.raises(HTTPException) as error:
        await language.translate_display('临时调侃')
    assert error.value.status_code == 503
    assert language._pending == {}

async def scenario_display_translation_cache_and_validation(monkeypatch):
    for key in ['LLM_API_KEY', 'LLM_MODEL', 'LLM_BASE_URL']:
        monkeypatch.setenv(key, 'test-only')
    language._cache.clear()
    class Response:
        def raise_for_status(self): pass
        def json(self): return {'choices':[{'message':{'content':'{"text":"A playful prop!"}'}}]}
    post = AsyncMock(return_value=Response())
    monkeypatch.setattr(language.httpx.AsyncClient, 'post', post)
    assert await language.translate_display('有趣的玩具') == 'A playful prop!'
    assert await language.translate_display('有趣的玩具') == 'A playful prop!'
    assert post.await_count == 1
    args = post.call_args.kwargs
    assert args['json']['messages'][0]['role'] == 'system'
    assert 'test-only' not in language._cache['有趣的玩具']


def test_display_translation_requires_server_provider(monkeypatch):
    asyncio.run(scenario_display_translation_requires_server_provider(monkeypatch))

def test_display_translation_cache_and_validation(monkeypatch):
    asyncio.run(scenario_display_translation_cache_and_validation(monkeypatch))

def test_display_language_route_requires_login_and_preserves_source(tmp_path, monkeypatch):
    from fastapi.testclient import TestClient
    from server import app as module
    monkeypatch.setenv('ABOUTME_DB', str(tmp_path/'profiles.sqlite3'))
    async def translated(value):
        assert value == '玩具真好玩'
        return 'Toys are fun!'
    monkeypatch.setattr(module, 'translate_display', translated)
    client = TestClient(module.app)
    assert client.post('/display-language',json={'text':'玩具真好玩'}).status_code == 401
    session = client.post('/sessions',json={'nickname':'名字不应改变'}).json()
    headers = {'Authorization':'Bearer '+session['token']}
    result = client.post('/display-language',json={'text':'玩具真好玩'},headers=headers)
    assert result.json() == {'text':'Toys are fun!'}
    assert client.get('/profiles/me',headers=headers).json()['nickname'] == '名字不应改变'

def test_display_translation_rejects_untranslated_output(monkeypatch):
    for key in ['LLM_API_KEY','LLM_MODEL','LLM_BASE_URL']:
        monkeypatch.setenv(key,'test-only')
    language._cache.clear()
    class Response:
        def raise_for_status(self): pass
        def json(self): return {'choices':[{'message':{'content':'{"text":"仍然是中文"}'}}]}
    monkeypatch.setattr(language.httpx.AsyncClient,'post',AsyncMock(return_value=Response()))
    with pytest.raises(HTTPException) as error:
        asyncio.run(language.translate_display('不应缓存不合格翻译'))
    assert error.value.status_code == 502
    assert '不应缓存不合格翻译' not in language._cache

import json
import httpx
import pytest
from server import app as module
from server import appearance
from server.tests.test_api import client, credentials


def provider(monkeypatch, data):
    for key in ('LLM_API_KEY', 'LLM_MODEL', 'LLM_BASE_URL'):
        monkeypatch.setenv(key, 'test')
    class AI:
        def __init__(self, **kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kwargs):
            payload=kwargs['json']
            assert '本次生成性格皮肤' in payload['messages'][0]['content']
            assert json.loads(payload['messages'][1]['content'])['personality']=='热情又好奇 ENFP'
            return httpx.Response(200, json={'choices':[{'message':{'content':json.dumps(data)}}]}, request=httpx.Request('POST', 'https://provider.test'))
    monkeypatch.setattr(appearance.httpx,'AsyncClient',AI)


def test_ai_skin_preview_save_reload_and_trusted_verification(client,monkeypatch):
    headers,session=credentials(client)
    provider(monkeypatch,{'appearance':{'skinPattern':'flame','topColor':'#F49A6B','accessoryColor':'#FFD773','skinColor':'#80513D'},'traits':['热情','好奇','热情','untrusted'],'reason':'暖橙火焰体现热情，星黄色点缀体现好奇。'})
    before=client.get('/profiles/me',headers=headers).json()
    response=client.post('/appearance/personality-skin',headers=headers,json={'introduction':'我喜欢游泳','personality':'热情又好奇 ENFP','appearance':{'skinColor':'#F3CDA9'}})
    assert response.status_code==200
    result=response.json()
    assert result['aiSource']=='provider' and result['mode']=='personality'
    assert result['traits']==['热情','好奇'] and result['appearance']['skinColor']=='#F3CDA9'
    assert result['appearance']['skinPattern']=='flame' and 0<=result['appearance']['skinSeed']<=9999
    assert client.get('/profiles/me',headers=headers).json()==before
    saved=client.post('/profiles/me/appearance',headers=headers,json={'expectedRevision':0,'appearance':result['appearance']})
    assert saved.status_code==200 and saved.json()['personas']==before['personas']
    assert client.get('/profiles/me',headers=headers).json()['appearance']==result['appearance']
    verified=client.post('/internal/verify',headers={'X-Game-Key':'test-server-key'},json={'token':session['token']})
    assert verified.json()['appearance']==result['appearance']


@pytest.mark.parametrize('config',[{'skinPattern':'remote-model-url'},{'skinSeed':10000},{'skinSeed':-1},{'damage':50}])
def test_skin_catalog_and_seed_are_bounded(client,config):
    headers,_=credentials(client)
    assert client.post('/profiles/me/appearance',headers=headers,json={'expectedRevision':0,'appearance':config}).status_code==422


def test_failed_skin_never_overwrites_saved_style(client,monkeypatch):
    headers,_=credentials(client)
    provider(monkeypatch,{'appearance':{'skinPattern':'plain'},'reason':'missing pattern'})
    before=client.get('/profiles/me',headers=headers).json()
    assert client.post('/appearance/personality-skin',headers=headers,json={'introduction':'我喜欢游泳','personality':'热情又好奇 ENFP'}).status_code==502
    assert client.get('/profiles/me',headers=headers).json()==before
    assert client.post('/appearance/personality-skin',json={'introduction':'我喜欢游泳'}).status_code==401


def test_swimming_interest_has_real_catalog_asset_and_negative_does_not(client):
    for source in ('我喜欢游泳','I enjoy swimming'):
        raw={'personas':[dict(name='游泳圈',source=source,templateId='Cushion',visualId='swimming-ring')]}
        result=module.validate_personas(raw,source)
        assert result[0]['visualId']=='swimming-ring'
    with pytest.raises(ValueError):
        module.validate_personas({'personas':[dict(name='游泳圈',source='我不会游泳',templateId='Cushion',visualId='swimming-ring')]},'我不会游泳')

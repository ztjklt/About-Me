import json
import pytest
import httpx
from server import app as api
from server.tests.test_api import client, credentials
from server.easter_egg import SKINS
from server.persona_results import skin_generation_terms

@pytest.mark.parametrize('skin', SKINS, ids=lambda s:s['brand'])
def test_cosmetic_terms_green_without_prop_budget(client, monkeypatch, skin):
    async def provider(intro):
        return api.validate_personas({'personas':[], 'terms':[{'term':skin['brand'],'source':intro}]}, intro)
    monkeypatch.setattr(api, 'generate_with_provider', provider)
    headers,_=credentials(client)
    intro='我喜欢'+skin['brand']+'，我的性格是热情'
    response=client.post('/personas/generate', headers=headers, json={'nickname':'玩家','introduction':intro,'generationId':'a'*32})
    assert response.status_code==200,response.text
    result=response.json();term=next(t for t in result['generationTerms'] if t['term']==skin['brand'])
    assert term['status']=='ready' and term['kind']=='ai-skin' and term['skinId']==skin['skin'] and not term['assetId']
    assert len(result['personas'])==1 and result['personas'][0]['visualId']=='trait-word'
    assert client.get('/profiles/me',headers=headers).json()['generationTerms']==result['generationTerms']
    again=client.post('/personas/generate', headers=headers, json={'nickname':'玩家','introduction':intro,'generationId':'b'*32}).json()
    assert again['generationTerms']==result['generationTerms']


def test_skin_unknown_negated_and_real_prop_identity():
    assert skin_generation_terms([], '我不喜欢GPT', ['gpt-girl'])==[]
    assert skin_generation_terms([], '朋友喜欢GPT', ['gpt-girl'])==[]
    assert skin_generation_terms([], '我喜欢GPT', [])==[]
    rows=skin_generation_terms([dict(term='GPT',assetId='real-prop',status='ready')], '我喜欢GPT', ['gpt-girl'])
    assert rows[0]['assetId']=='real-prop'


def provider_fixture(monkeypatch, raw):
    for key,value in [('LLM_API_KEY','fixture'),('LLM_MODEL','fixture'),('LLM_BASE_URL','https://fixture.invalid')]:monkeypatch.setenv(key,value)
    captured=[]
    class Provider:
        def __init__(self,**kw):pass
        async def __aenter__(self):return self
        async def __aexit__(self,*args):pass
        async def post(self,url,**kw):
            captured.append(kw['json'])
            return httpx.Response(200,request=httpx.Request('POST',url),json={'choices':[{'message':{'content':json.dumps(raw)}}]})
    monkeypatch.setattr(api.httpx,'AsyncClient',Provider)
    return captured

ITEMS=[dict(item_name='篮球',template_id='Projectile',visual_id='basketball'),dict(item_name='相机',template_id='Shockwave',visual_id='camera')]

def test_preparation_auth_public_input_and_order(client,monkeypatch):
    items=[dict(item_name='篮球',template_id='Projectile',visual_id='basketball')]
    calls=provider_fixture(monkeypatch, {'lines':[{'index':0,'roast':'唱跳缺个篮球'}]})
    assert client.post('/roast/prepare',json={'items':items}).status_code==401
    assert client.post('/internal/roast/prepare',json={'items':items}).status_code==403
    headers,_=credentials(client)
    result=client.post('/roast/prepare',headers=headers,json={'items':items})
    assert result.status_code==200,result.text
    assert result.json()['aiSource']=='provider' and result.json()['lines'][0]['item_name']=='篮球'
    prompt=json.loads(calls[0]['messages'][1]['content'])
    assert set(prompt[0])=={'index','item_name','template_id','visual_id','catalog_label'}
    assert client.post('/internal/roast/prepare',headers={'X-Game-Key':'test-server-key'},json={'items':items}).status_code==200

@pytest.mark.parametrize('raw',[{'lines':[]},{'lines':[{'index':2,'roast':'唱跳缺个篮球'}]}, {'lines':[{'index':0,'roast':'你这个傻逼。'}]}])
def test_invalid_preparation_cannot_be_played(client,monkeypatch,raw):
    provider_fixture(monkeypatch,raw);headers,_=credentials(client)
    response=client.post('/roast/prepare',headers=headers,json={'items':[ITEMS[0]]})
    assert response.status_code==502
    assert client.post('/roast/prepare',headers=headers,json={'items':[dict(ITEMS[0],source='private introduction')]}).status_code==422

@pytest.mark.parametrize('line',['唱跳缺个篮球','球技还没嘴硬','你也唱跳rap？','滤镜救不了你'])
def test_new_roast_style_accepts_short_punchlines(line):
    from server.roast_safety import safe_roast
    assert safe_roast(line)

@pytest.mark.parametrize('line',['太菜','我把篮球吃下去，打个嗝都能算三分球。','唱跳缺个篮球。你呢？','球技123嘴硬','唱跳\n缺个篮球','你这个傻逼呀','还唱跳?菜不菜?'])
def test_new_roast_style_rejects_long_or_unsafe_lines(line):
    from server.roast_safety import safe_roast
    assert not safe_roast(line)

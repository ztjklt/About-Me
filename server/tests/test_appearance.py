import json
import httpx
import pytest
from server import app as module
from server import appearance as appearance_module
from server.tests.test_api import client,credentials


def test_catalog_save_and_reconnect_do_not_change_personas(client):
    headers,data=credentials(client)
    before=client.post('/personas/generate',headers=headers,json={'nickname':'玩家','introduction':'中山大学'}).json()
    saved=client.post('/profiles/me/appearance',headers=headers,json={'expectedRevision':0,'appearance':{'hair':'tied','bag':'sling','top':'shirt','expression':'confident'}})
    assert saved.status_code==200
    after=saved.json()
    assert after['personas']==before['personas'] and after['revision']==before['revision']
    assert after['appearance']['expression']=='confident'
    assert after['appearance']['bag']=='sling' and after['appearanceRevision']==1
    assert client.get('/profiles/me',headers=headers).json()==after
    verified=client.post('/internal/verify',json={'token':data['token']},headers={'X-Game-Key':'test-server-key'}).json()
    assert verified['appearance']==after['appearance']
    again=client.post('/personas/generate',headers=headers,json={'nickname':'玩家','introduction':'北京大学'}).json()
    assert again['appearance']==after['appearance'] and again['appearanceRevision']==1


def test_invalid_or_gameplay_fields_are_rejected(client):
    headers,_=credentials(client)
    for appearance in [{'bag':'remote-model-url'},{'headAccessory':['cap','headphones']},{'moveSpeed':10},{'skinColor':'#INVALID'},{'height':5},{'expression':'arbitrary-mesh'}]:
        assert client.post('/profiles/me/appearance',headers=headers,json={'expectedRevision':0,'appearance':appearance}).status_code==422
    assert client.post('/profiles/me/appearance',json={'expectedRevision':0,'appearance':{}}).status_code==401
    assert client.get('/profiles/me',headers=headers).json()['appearanceRevision']==0


def test_old_profile_gets_defaults_and_stale_save_is_rejected(client):
    headers,data=credentials(client)
    legacy={'playerId':data['profile']['playerId'],'nickname':'旧角色','color':'#F3AFC2','outfit':1,'personas':[],'revision':0}
    with module.db() as con:con.execute('UPDATE profiles SET profile=? WHERE id=?',(json.dumps(legacy),legacy['playerId']))
    actual=client.get('/profiles/me',headers=headers).json()
    assert actual['appearance']['expression']=='friendly'
    assert actual['appearance']['top']=='varsity' and actual['appearance']['topColor']=='#F3AFC2'
    assert client.post('/profiles/me/appearance',headers=headers,json={'expectedRevision':0,'appearance':{}}).status_code==200
    assert client.post('/profiles/me/appearance',headers=headers,json={'expectedRevision':0,'appearance':{'top':'shirt'}}).status_code==409


def test_recommendation_is_real_catalog_bound_and_does_not_save(client,monkeypatch):
    for key in ['LLM_API_KEY','LLM_BASE_URL','LLM_MODEL']:monkeypatch.setenv(key,'test')
    class Provider:
        def __init__(self,**kwargs):pass
        async def __aenter__(self):return self
        async def __aexit__(self,*args):pass
        async def post(self,url,**kwargs):
            assert '只从目录选择' in kwargs['json']['messages'][0]['content']
            result={'appearance':{'top':'shirt','bag':'sling','skinColor':'#80513D','hairColor':'#202A3D'},'reason':'摄影时可以带一个斜挎包。'}
            return httpx.Response(200,json={'choices':[{'message':{'content':json.dumps(result)}}]},request=httpx.Request('POST','https://provider.test'))
    monkeypatch.setattr(appearance_module.httpx,'AsyncClient',Provider)
    headers,_=credentials(client)
    result=client.post('/appearance/recommend',headers=headers,json={'introduction':'我喜欢摄影','appearance':{'skinColor':'#F3CDA9','expression':'sleepy'}})
    assert result.json()['appearance']['expression']=='sleepy'
    assert result.status_code==200 and result.json()['appearance']['bag']=='sling'
    assert result.json()['appearance']['skinColor']=='#F3CDA9' and result.json()['appearance']['hairColor']=='#493A36'
    assert client.get('/profiles/me',headers=headers).json()['appearance']['bag']=='none'
    assert client.get('/profiles/me',headers=headers).json()['appearanceRevision']==0


def test_missing_ai_does_not_fake_or_overwrite_outfit(client,monkeypatch):
    monkeypatch.delenv('LLM_API_KEY',raising=False);headers,_=credentials(client)
    before=client.get('/profiles/me',headers=headers).json()
    assert client.post('/appearance/recommend',headers=headers,json={'introduction':'喜欢音乐'}).status_code==503
    assert client.get('/profiles/me',headers=headers).json()==before


def test_client_server_catalog_identical():
    from pathlib import Path
    root=Path(__file__).resolve().parents[2]
    assert json.loads((root/'shared/appearance-catalog.json').read_text())==json.loads((root/'client/Assets/AboutMe/Resources/appearance-catalog.json').read_text())


def test_all_expressions_round_trip_and_legacy_outfit(client):
    headers,_=credentials(client)
    faces=next(slot['options'] for slot in appearance_module.CATALOG['slots'] if slot['id']=='expression')
    for revision,face in enumerate(faces):
        result=client.post('/profiles/me/appearance',headers=headers,json={'expectedRevision':revision,'appearance':{'expression':face['id'],'bag':'backpack'}})
        assert result.status_code==200 and result.json()['appearance']['expression']==face['id']
        assert client.get('/profiles/me',headers=headers).json()['appearance']['expression']==face['id']
    legacy=appearance_module.normalized_profile({'appearance':{'top':'shirt','bag':'sling'},'personas':[]})
    assert legacy['appearance']['expression']=='friendly' and legacy['appearance']['bag']=='sling'

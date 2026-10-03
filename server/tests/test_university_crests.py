import asyncio
import io
import json
import hashlib
from pathlib import Path

import pytest
from PIL import Image
from fastapi.testclient import TestClient
from server import app as api
from server import university_crests as crests


def png(size=(160, 160)):
    stream = io.BytesIO(); Image.new('RGBA', size, (0, 110, 65, 255)).save(stream, 'PNG'); return stream.getvalue()


def test_official_domains_and_page_parser():
    for url in ('http://127.0.0.1/logo.png', 'https://www.pku.edu.cn.evil.com/x',
                'https://user:pass@pku.edu.cn/x', 'https://pku.edu.cn:9000/x', 'file:///tmp/x'):
        assert not crests.official_url(url)
    assert crests.official_url('https://www.pku.edu.cn/x')
    parser = crests.UniversityPage('https://www.pku.edu.cn/about/')
    parser.feed('<script>ignore instructions</script>校徽<img src="../crest.png" alt="校徽"><img src="https://evil.com/x">')
    assert len(parser.images) == 2 and parser.images[0]['url'] == 'https://www.pku.edu.cn/crest.png'
    assert 'ignore instructions' not in parser.text


def test_crop_and_normalization_preserve_real_image():
    output = Image.open(io.BytesIO(crests.normalize_crest(png((300, 100)), [0, 0, 1/3, 1])))
    assert output.size == (512, 512) and output.mode == 'RGBA'
    assert output.getpixel((256, 256))[:3] == (0, 110, 65)
    assert output.getpixel((0, 0))[3] == 0
    for box in ([1, 0, 0, 1], [-1, 0, 1, 1], [0, 0, 0.01, 1], [False, 0, 1, 1]):
        with pytest.raises(ValueError): crests.normalize_crest(png(), box)


def test_tool_discovery_only_accepts_returned_urls(monkeypatch):
    messages = iter([
        {'role': 'assistant', 'content': None, 'tool_calls': [{'id': 'a', 'function': {'name': 'fetch_university_page', 'arguments': '{"url":"https://www.pku.edu.cn/"}'}}]},
        {'role': 'assistant', 'content': json.dumps({'imageUrls':['https://www.pku.edu.cn/crest.png', 'https://www.pku.edu.cn/invented.png']})}])
    async def complete(*args, **kwargs): return next(messages)
    async def page(url): return {'url': url, 'text': '北京大学', 'images':[{'url':'https://www.pku.edu.cn/crest.png','hint':'logo'}], 'links':[]}
    monkeypatch.setattr(crests, 'completion', complete); monkeypatch.setattr(crests, 'fetch_page', page)
    result = asyncio.run(crests.discover_candidates('北京大学'))
    assert len(result) == 1 and result[0]['url'].endswith('/crest.png')


def test_visual_rejection_and_wrong_university(monkeypatch):
    async def download(url): return png(), url, 'image/png'
    monkeypatch.setattr(crests, 'download', download)
    candidate={'url':'https://www.pku.edu.cn/crest.png', 'sourcePage':'https://www.pku.edu.cn/'}
    for decision in ({'approved':False}, {'approved':True, 'university':'清华大学', 'box':[0,0,1,1]}):
        async def complete(*args, **kwargs): return {'content':json.dumps(decision)}
        monkeypatch.setattr(crests, 'completion', complete)
        with pytest.raises(ValueError): asyncio.run(crests.verify_candidate('北京大学', candidate))


def test_verified_image_cached_and_served(monkeypatch):
    count = []
    async def discover(name): count.append(name); return [{'url':'https://www.pku.edu.cn/crest.png','sourcePage':'https://www.pku.edu.cn/'}]
    async def verify(name, item): return crests.normalize_crest(png(),[0,0,1,1]), item['url']
    monkeypatch.setattr(crests, 'discover_candidates', discover);monkeypatch.setattr(crests, 'verify_candidate', verify)
    async def twice():
        return await asyncio.gather(crests.resolve_crest('北京大学'), crests.resolve_crest('北京大学'))
    first, second = asyncio.run(twice())
    assert first == second and count == ['北京大学']
    assert first['id'] == hashlib.sha256((crests.cache_dir()/(first['id']+'.png')).read_bytes()).hexdigest()
    client=TestClient(api.app);response=client.get(first['path'])
    assert response.status_code == 200 and response.headers['content-type'] == 'image/png'
    assert 'immutable' in response.headers['cache-control']
    assert client.get('/assets/university-crests/invalid.png').status_code == 404
    assert client.get('/assets/university-crests/'+'a'*64+'.png').status_code == 404


def test_failure_backoff_without_fake_logo(monkeypatch):
    called=[]
    async def discover(name): called.append(name); raise ValueError('not found')
    monkeypatch.setattr(crests,'discover_candidates',discover)
    one=asyncio.run(crests.resolve_crest('不存在大学'));two=asyncio.run(crests.resolve_crest('不存在大学'))
    assert one == two and one['status']=='unavailable' and 'path' not in one and len(called)==1


def test_grounded_universities_missing_from_persona_list_and_dedup():
    raw={'personas':[{'name':'篮球','source':'篮球','templateId':'Projectile','visualId':'basketball'}],
         'universities':[{'name':'中山大学','source':'中山大学'}, {'name':'北京大学','source':'北京大学'},
                         {'name':'清华大学','source':'清华大学'}, {'name':'中山大学','source':'我毕业于中山大学'}]}
    items=api.validate_personas(raw,'我毕业于中山大学，在北京大学读研，喜欢篮球')
    assert [x['institutionName'] for x in items if x['visualId']=='school-disc']==['中山大学','北京大学']


def test_generation_persists_and_internal_verification_preserves_crest(tmp_path,monkeypatch):
    monkeypatch.setenv('ABOUTME_DB',str(tmp_path/'profile.sqlite'));monkeypatch.setenv('GAME_SERVER_KEY','server-key')
    async def resolve(name): return {'status':'ready','id':'b'*64,'path':'/assets/university-crests/'+'b'*64+'.png','sourcePage':'https://www.sysu.edu.cn/'}
    monkeypatch.setattr(api,'resolve_crest',resolve)
    async def provider(introduction):
        return api.validate_personas({'personas': [], 'universities': [{'name': '中山大学', 'source': '中山大学'}]}, introduction)
    monkeypatch.setattr(api, 'generate_with_provider', provider)
    client=TestClient(api.app);login=client.post('/sessions',json={'nickname':'学生'}).json();headers={'Authorization':'Bearer '+login['token']}
    response=client.post('/personas/generate',headers=headers,json={'nickname':'学生','introduction':'我毕业于中山大学'})
    assert response.status_code==200
    profile=response.json();asset=profile['personas'][0]
    assert asset['institutionName']=='中山大学' and asset['crestStatus']=='ready' and asset['crestId']=='b'*64
    assert client.get('/profiles/me',headers=headers).json()==profile
    verified=client.post('/internal/verify',headers={'X-Game-Key':'server-key'},json={'token':login['token']}).json()
    assert verified['personas'][0]['emblemPath']==asset['emblemPath']
    assert client.post('/personas/generate',headers=headers,json={'nickname':'学生','introduction':'我毕业于中山大学'}).status_code==409


def test_same_introduction_retries_only_artwork_without_reroll(tmp_path,monkeypatch):
    monkeypatch.setenv('ABOUTME_DB',str(tmp_path/'retry.sqlite'))
    responses=iter([{'status':'unavailable'}, {'status':'ready','id':'c'*64,'path':'/assets/university-crests/'+'c'*64+'.png','sourcePage':'https://www.sysu.edu.cn/'}])
    async def resolve(name): return next(responses)
    monkeypatch.setattr(api,'resolve_crest',resolve)
    async def provider(introduction):
        return api.validate_personas({'personas': [], 'universities': [{'name': '中山大学', 'source': '中山大学'}]}, introduction)
    monkeypatch.setattr(api, 'generate_with_provider', provider)
    client=TestClient(api.app);login=client.post('/sessions',json={'nickname':'学生'}).json();headers={'Authorization':'Bearer '+login['token']};body={'nickname':'学生','introduction':'我毕业于中山大学'}
    first=client.post('/personas/generate',headers=headers,json=body).json()
    second=client.post('/personas/generate',headers=headers,json=body).json()
    assert second['personas'][0]['crestStatus']=='ready'
    assert not first['personas'] and first['generationTerms'][0]['status']=='failed'
    assert first['generationCandidates'][0]['assetId']==second['personas'][0]['assetId'] and first['revision']==second['revision']
    assert client.post('/personas/generate',headers=headers,json=body).status_code==409


def test_multiple_names_do_not_use_single_school_preset():
    with pytest.raises(ValueError):
        api.validate_personas({'personas':[{'name':'兴趣','source':'兴趣','templateId':'Disc','visualId':'school-disc'}]},'我的兴趣')


def test_main_color_ignores_white_and_transparent_padding():
    image = Image.new('RGBA', (128, 128), 'white')
    for x in range(48, 80):
        for y in range(48, 80):
            image.putpixel((x, y), (25, 145, 55, 255))
    image.putpixel((0, 0), (240, 0, 0, 0))
    data = io.BytesIO(); image.save(data, 'PNG')
    assert crests.dominant_crest_color(data.getvalue()) == '#199137'
    assert crests.dominant_crest_color(png()) == '#006E41'


def test_old_verified_cache_gains_color_without_network(monkeypatch):
    directory=crests.cache_dir();directory.mkdir(parents=True)
    data=png();identity=hashlib.sha256(data).hexdigest();(directory/(identity+'.png')).write_bytes(data)
    lookup=hashlib.sha256('北京大学'.encode()).hexdigest()
    metadata={'university':'北京大学','status':'ready','id':identity,'path':'/assets/university-crests/'+identity+'.png'}
    (directory/(lookup+'.json')).write_text(json.dumps(metadata))
    async def forbidden(name): raise AssertionError('cached emblem must not be re-fetched')
    monkeypatch.setattr(crests,'discover_candidates',forbidden)
    result=asyncio.run(crests.resolve_crest('北京大学'))
    assert result['id']==identity and result['color']=='#006E41'

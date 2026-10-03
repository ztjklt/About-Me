import asyncio
import pytest
from server import app as api
from server.university_crests import report_crest_progress
from server.persona_results import generation_terms


def school(name,country=''):
    return {'assetId':name,'name':name+'校徽圆盘','source':name,'institutionName':name,
            'institutionCountry':country,'templateId':'Disc','visualId':'school-disc','color':'#77C8BD'}


def test_domestic_group_finishes_before_overseas_and_countries_are_not_language(monkeypatch):
    assets=[school('哈佛大学','US'),school('Peking University','CN'),school('湖北文理学院','CN')]
    events=[]; states=[]
    async def generate(name):
        events.append(('start',name));report_crest_progress(.45,'正在下载校徽候选图')
        await asyncio.sleep(.005)
        events.append(('end',name))
        return {'crestStatus':'ready','emblemPath':'/assets/university-crests/'+'a'*64+'.png'}
    monkeypatch.setattr(api,'generate_school_emblem',generate)
    def update(): states.append(generation_terms(assets,[],api.CATALOG_BY_PAIR,pending=True))
    asyncio.run(api.attach_school_emblems(assets,update))
    foreign=events.index(('start','哈佛大学'))
    assert events.index(('end','Peking University'))<foreign
    assert events.index(('end','湖北文理学院'))<foreign
    assert any(next(t for t in state if t['term']=='哈佛大学')['progress']==0 and
               next(t for t in state if t['term']=='湖北文理学院')['progress']==.45 for state in states)
    assert all(t['progress']==1 and t['status']=='ready' for t in states[-1])


def test_failed_school_keeps_stage_progress_and_explicit_reason(monkeypatch):
    assets=[school('测试学院','CN')]
    async def generate(name):
        report_crest_progress(.65,'DeepSeek 正在核对学校与校徽')
        return {'crestStatus':'unavailable','crestReason':'图片不属于这所学校，请重试'}
    monkeypatch.setattr(api,'generate_school_emblem',generate)
    asyncio.run(api.attach_school_emblems(assets))
    result=generation_terms(assets,[],api.CATALOG_BY_PAIR)[0]
    assert result['kind']=='university' and result['status']=='failed'
    assert result['progress']==.65 and '不属于' in result['reason']


def test_domestic_metadata_survives_duplicate_school_persona():
    intro='我毕业于湖北文理学院'
    result=api.validate_personas({'personas':[{'name':'学校','source':'湖北文理学院',
        'templateId':'Disc','visualId':'school-disc'}], 'universities':[{
        'name':'湖北文理学院','source':'湖北文理学院','countryCode':'CN'}]},intro)
    assert len(result)==1 and result[0]['institutionCountry']=='CN'


def test_queue_deadline_keeps_successful_domestic_and_marks_waiting_school(monkeypatch):
    monkeypatch.setenv('UNIVERSITY_GENERATION_TIMEOUT','.03')
    assets=[school('国内大学','CN'),school('海外大学','US')]
    async def generate(name):
        if name=='国内大学':
            return {'crestStatus':'ready','emblemPath':'/assets/university-crests/'+'a'*64+'.png'}
        report_crest_progress(.45,'下载中')
        await asyncio.sleep(1)
    monkeypatch.setattr(api,'generate_school_emblem',generate)
    asyncio.run(api.attach_school_emblems(assets))
    terms=generation_terms(assets,[],api.CATALOG_BY_PAIR)
    assert terms[0]['status']=='ready' and terms[0]['progress']==1
    assert terms[1]['status']=='failed' and terms[1]['progress']==.45
    assert '重试' in terms[1]['reason']

#!/usr/bin/env python3
"""Live text extraction, non-seeded school lookup, progressive statuses and persistence."""
import json,os,shlex,sys,time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def main():
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('--env',type=Path,default=ROOT/'server/.env')
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/persona-keywords-live')
    parser.add_argument('--service-root',type=Path,default=ROOT/'builds/university-crests-service')
    parser.add_argument('--introduction',default='我毕业于湖北文理学院，也在 University of Otago 学习过。我喜欢篮球，会小提琴。')
    args=parser.parse_args()
    service=args.service_root if (args.service_root/'server/university_search.py').is_file() else ROOT
    sys.path.insert(0,str(service.resolve()))
    if args.env.is_file():
        for line in args.env.read_text().splitlines():
            if '=' not in line or line.lstrip().startswith('#'):continue
            key,value=line.split('=',1)
            if key.strip() in ('LLM_API_KEY','LLM_BASE_URL','LLM_MODEL','DEEPSEEK_API_KEY','DEEPSEEK_BASE_URL','UNIVERSITY_CREST_MODEL','TAVILY_API_KEY'):
                parts=shlex.split(value,comments=True);os.environ[key.strip()]=parts[0] if parts else ''
    args.output.mkdir(parents=True,exist_ok=True)
    os.environ['ABOUTME_DB']=str(args.output/'profiles.sqlite3')
    os.environ['UNIVERSITY_CREST_CACHE']=str(args.output/'crests')
    os.environ['GAME_SERVER_KEY']='local-keywords-validation'
    cached_names=set()
    for path in (args.output/'crests').glob('*.json'):
        try:
            cached=json.loads(path.read_text())
            if cached.get('status')=='ready' and (path.parent/(cached['id']+'.png')).is_file():
                cached_names.add(cached['university'])
        except (ValueError,KeyError,OSError):pass
    from fastapi.testclient import TestClient
    from server.app import app
    from server.university_crests import SEEDS
    with TestClient(app) as client,ThreadPoolExecutor(max_workers=1) as pool:
        session=client.post('/sessions',json={'nickname':'词条验证'}).json()
        headers={'Authorization':'Bearer '+session['token']}
        body={'nickname':'词条验证','introduction':args.introduction,'generationId':'e'*32}
        started=time.monotonic();history=[];first_terms=None
        future=pool.submit(client.post,'/personas/generate',json=body,headers=headers)
        while not future.done():
            state=client.get('/personas/generation-status?generationId='+'e'*32,headers=headers)
            if state.status_code==200:
                progress=state.json()
                values=[{'term':x['term'],'status':x['status']} for x in progress['generationTerms']]
                if values and first_terms is None:first_terms=round(time.monotonic()-started,2)
                record={'phase':progress['phase'],'terms':values}
                if not history or history[-1]['value']!=record:history.append({'seconds':round(time.monotonic()-started,2),'value':record})
            time.sleep(.7)
        response=future.result()
        if response.status_code!=200:
            print('Generation failed',response.status_code,response.json().get('detail'));return 1
        profile=response.json();first_seconds=round(time.monotonic()-started,2)
        first_statuses=[{'school':x['institutionName'],'status':x.get('crestStatus')} for x in profile['generationCandidates'] if x['visualId']=='school-disc']
        old_ids=[x['assetId'] for x in profile['generationCandidates']]
        repeat_started=time.monotonic()
        repeat=client.post('/personas/generate',json={**body,'generationId':'d'*32},headers=headers)
        assert repeat.status_code==200
        repeated=repeat.json()
        assert [x['assetId'] for x in repeated['generationCandidates']]==old_ids
        assert repeated['revision']==profile['revision']
        profile=repeated
        (args.output/'profile.json').write_text(json.dumps(profile,ensure_ascii=False,indent=2))
        assert client.get('/profiles/me',headers=headers).json()==profile
        internal=client.post('/internal/verify',headers={'X-Game-Key':'local-keywords-validation'},json={'token':session['token']}).json()
        assert internal['personas']==profile['personas']
        schools=[]
        for asset in profile['generationCandidates']:
            if asset['visualId']!='school-disc':continue
            row={'school':asset['institutionName'],'status':asset['crestStatus'],'seeded':asset['institutionName'] in SEEDS,
                 'verifiedImageCachedBeforeRun':asset['institutionName'] in cached_names}
            if asset.get('emblemPath'):
                image=client.get(asset['emblemPath']);assert image.status_code==200
                (args.output/(asset['institutionName']+'.png')).write_bytes(image.content)
                row.update(source=asset['emblemSource'],color=asset['color'])
            schools.append(row)
        report={'realDeepSeek':True,'realPublicSearch':any(not x['verifiedImageCachedBeforeRun'] for x in schools),
                'firstKeywordsSeconds':first_terms,'firstGenerationSeconds':first_seconds,
                'repeatSeconds':round(time.monotonic()-repeat_started,2),'firstSchoolStatuses':first_statuses,'identitiesAndRevisionPreserved':True,
                'totalSeconds':round(time.monotonic()-started,2),'schools':schools,
                'terms':profile['generationTerms'],'progressHistory':history,'savedAndInternalVerified':True}
        (args.output/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print(json.dumps(report,ensure_ascii=False))
        return 0 if schools and all(x['status']=='ready' for x in schools) else 1
if __name__=='__main__':raise SystemExit(main())

#!/usr/bin/env python3
"""Private QA account against a real configured provider; no secrets in results."""
import json,time,urllib.request,uuid
from pathlib import Path
base='http://127.0.0.1:8092';out=Path('artifacts/trait-persona-r5/real-ai');out.mkdir(parents=True,exist_ok=True)
def call(path,body=None,token=None):
 headers={'Content-Type':'application/json'}
 if token:headers['Authorization']='Bearer '+token
 req=urllib.request.Request(base+path,data=json.dumps(body,ensure_ascii=False).encode() if body is not None else None,headers=headers)
 with urllib.request.urlopen(req,timeout=115) as response:return json.load(response)
for i in range(15):
 try:health=call('/health');break
 except Exception:time.sleep(1)
session=call('/sessions',{'nickname':'R5真实AI验收'});intro='我的性格是热情；我喜欢的人物表情是友好微笑；我的 MBTI 是 ENFP；我喜欢篮球。'
start=time.monotonic()
try:
 profile=call('/personas/generate',{'nickname':'R5真实AI验收','introduction':intro,'generationId':uuid.uuid4().hex},session['token'])
 assert {'热情','友好微笑','ENFP'}<={a.get('wordText') for a in profile['personas']}
 assert any(a['visualId']=='basketball' for a in profile['personas'])
 (out/'profile.json').write_text(json.dumps(profile,ensure_ascii=False,indent=2)+'\n')
 summary={'evidence':'real configured LLM provider via real HTTP API; independent QA SQLite account','elapsedSeconds':round(time.monotonic()-start,2),'input':intro,'assets':[dict(assetId=a['assetId'],visualId=a['visualId'],wordText=a.get('wordText'),abilityId=a.get('abilityId')) for a in profile['personas']]}
 (out/'result.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n');print('Real AI representative input:',len(profile['personas']),'assets; required three words and basketball verified.')
except Exception as error:
 summary={'evidence':'real configured provider attempt failed; not fixture success','type':type(error).__name__,'elapsedSeconds':round(time.monotonic()-start,2),'status':getattr(error,'code',None)}
 (out/'result.json').write_text(json.dumps(summary,indent=2)+'\n');raise SystemExit('Real AI unavailable: '+str(summary))

#!/usr/bin/env python3
"""Verify animal preferences through the real configured provider on an isolated API."""
import argparse,json,time,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--api',required=True);p.add_argument('--output',type=Path,required=True);args=p.parse_args();args.output.mkdir(parents=True,exist_ok=True)
def call(path,body=None,token=None):
 headers={'Content-Type':'application/json'}
 if token:headers['Authorization']='Bearer '+token
 req=urllib.request.Request(args.api+path,data=json.dumps(body,ensure_ascii=False).encode() if body is not None else None,headers=headers)
 with urllib.request.urlopen(req,timeout=160) as response:return json.load(response)
session=call('/sessions',{'nickname':'Animal real provider QA'})
intro='我喜欢猫、狗、兔子和仓鼠。'
t=time.monotonic()
profile=call('/personas/generate',{'nickname':'Animal real provider QA','introduction':intro,'generationId':uuid.uuid4().hex},session['token'])
found={a['visualId'] for a in profile['personas']};expected={'cat','animal-dog','animal-rabbit','animal-hamster'}
result=dict(evidence='real configured LLM via HTTP, isolated QA account; no fixture substitutions',input=intro,elapsedSeconds=round(time.monotonic()-t,2),passed=expected<=found,assets=[{k:a.get(k) for k in ['assetId','name','visualId','templateId']} for a in profile['personas']])
(args.output/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
assert result['passed'], 'Real provider did not return all four animals'
print('Real provider returned all four animal Persona assets')

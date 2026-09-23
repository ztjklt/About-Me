"""Run each reviewed character asset once; completed task results are never overwritten."""
import argparse,json,os,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'artifacts/characters-v1';env=os.environ.copy();env.update(NODE_USE_ENV_PROXY='1',HTTPS_PROXY='http://127.0.0.1:7897',HTTP_PROXY='http://127.0.0.1:7897')
a=argparse.ArgumentParser();a.add_argument('asset',choices=['head','headphones','sling']);args=a.parse_args();name=args.asset
result=out/(name+'-job.json')
if result.exists():raise SystemExit('Existing result preserved: '+str(result))
cmd=['tripo','make',str(out/(name+'-reference.png')),'-p','face_limit='+str(2400 if name=='head' else 1600),'-p','texture=false','-p','pbr=false','--name','campus-doll-'+name,'-o',str(out),'--json']
plan=subprocess.run(cmd+['--dry-run'],env=env,text=True,capture_output=True,check=True);(out/(name+'-plan.json')).write_text(plan.stdout);assert json.loads(plan.stdout)['valid']
proc=subprocess.run(cmd+['--yes'],env=env,text=True,stdout=subprocess.PIPE);result.write_text(proc.stdout);print(proc.stdout);raise SystemExit(proc.returncode)

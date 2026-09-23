"""Sequential, resumable Tripo jobs for the approved V0.4 plan. No secrets logged."""
import argparse,json,os,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/campus-v04';REF=OUT/'references';PROMPTS=ROOT/'docs/map-v04-prompts'
ENV=os.environ.copy();ENV.update(NODE_USE_ENV_PROXY='1',HTTPS_PROXY='http://127.0.0.1:7897',HTTP_PROXY='http://127.0.0.1:7897')
(OUT/'jobs').mkdir(parents=True,exist_ok=True)
def run(args,path=None):
    print('RUN',args[0:3],flush=True)
    proc=subprocess.Popen(args,cwd=ROOT,env=ENV,stdout=subprocess.PIPE,stderr=None,text=True)
    output=proc.communicate()[0]
    if path:path.write_text(output)
    if proc.returncode:print(output,flush=True);raise SystemExit(proc.returncode)
    rows=[x for x in output.splitlines() if x.strip().startswith('{')]
    if not rows:raise RuntimeError('No JSON result')
    return json.loads(rows[-1])
def upload(p):
    p=Path(p);assert p.is_file(),p
    cache=OUT/'jobs/uploads.json';data=json.loads(cache.read_text()) if cache.exists() else {}
    key=str(p.resolve())+':'+str(p.stat().st_mtime_ns)
    if key in data:return data[key]
    value=run(['tripo','files','upload',str(p),'--json']);token=value.get('file_token') or value.get('data',{}).get('file_token')
    if not token:raise RuntimeError('Upload response lacks file_token: '+str(value))
    data[key]=token;cache.write_text(json.dumps(data,indent=2));return token
def image_result(job):
    data=json.loads((OUT/'jobs'/f'{job}.json').read_text());p=Path(data['output_dir'])
    # Prefer full-resolution image over the generated 512 px preview.
    for name in ['generated_image.png','generated_image.jpeg','generated_image.jpg','image.png','image.jpeg']:
        if (p/name).exists():return p/name
    for f in data.get('files',[]):
        if str(f).endswith(('.png','.jpeg','.jpg')) and 'preview' not in str(f):return p/Path(f).name
    raise RuntimeError('Cannot locate generated image in '+str(data))

def main():
    a=argparse.ArgumentParser();a.add_argument('job',choices=['entrance','overview','bay-image','hand-image','bay-model','hand-model']);a.add_argument('--plan',action='store_true');args=a.parse_args()
    name=args.job;result=OUT/'jobs'/f'{name}.json'
    if result.exists() and not args.plan:print('Existing job preserved:',result);print(result.read_text());return
    if name.endswith('model'):
        source=image_result(name.replace('model','image'));limit=6000 if name=='bay-model' else 4000
        cmd=['tripo','make',str(source),'-p',f'face_limit={limit}','-p','texture=false','-p','pbr=false','--name','campus-v04-'+name,'-o',str(OUT),'--json']
        if args.plan:
            data=run(cmd+['--dry-run'],OUT/'jobs'/f'{name}-plan.json');print(json.dumps(data,ensure_ascii=False));return
        # Always execute a free dry run and verify it before paid generation.
        plan=run(cmd+['--dry-run'],OUT/'jobs'/f'{name}-plan.json');assert plan['valid'] and len(plan['steps'])==1
        data=run(cmd+['--yes'],result)
    else:
        spec={
          'entrance':('01_campus_entrance',[REF/'original-campus.jpg',REF/'original-layout.png',REF/'ui-home.png',REF/'blockout-entrance.png'],'3072x1728'),
          'overview':('02_campus_overview',[REF/'blockout-overview.png',REF/'original-campus.jpg',REF/'ui-home.png'],'3072x1728'),
          'bay-image':('03_campus_bay_single',[REF/'blockout-bay.png',REF/'ui-home.png'],'2048x2048'),
          'hand-image':('04_hand_open',[REF/'blockout-hand.png',REF/'ui-home.png'],'2048x2048')}
        prompt_id,paths,size=spec[name];paths=[image_result('entrance') if p is None else p for p in paths]
        prompt=(PROMPTS/(prompt_id+'.txt')).read_text().strip();assert len(prompt)<=1024
        for p in paths:assert p.is_file(),p
        plan={'endpoint':'/v3/generation/image-to-image','model':'seedream_v5','reference_files':[str(p) for p in paths],'prompt':prompt,'size':size,'estimated_credits':5}
        (OUT/'jobs'/f'{name}-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2)+'\n')
        if args.plan:print(json.dumps(plan,ensure_ascii=False,indent=2));return
        tokens=[upload(p) for p in paths]
        cmd=['tripo','generate','image-to-image',tokens[0],'--model','seedream_v5','--prompt',prompt,'-p','inputs='+json.dumps(tokens[1:]),'-p','size='+size,'--name','campus-v04-'+name,'-o',str(OUT),'--json','--yes']
        data=run(cmd,result)
    print(json.dumps(data,ensure_ascii=False),flush=True)
if __name__=='__main__':main()

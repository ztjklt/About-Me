#!/usr/bin/env python3
"""Package matched R13 Mac/Web/API sources without modifying other worktrees."""
import argparse,hashlib,json,shutil,subprocess,tarfile
from pathlib import Path
from datetime import datetime,timezone
r=Path(__file__).resolve().parents[1]
a=argparse.ArgumentParser();a.add_argument('--prior-manifest',type=Path,required=True);args=a.parse_args()
m=json.loads(args.prior_manifest.read_text());assert m['releaseId']=='playtest-persona-r12'
sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()
branch=subprocess.check_output(['git','branch','--show-current'],cwd=r,text=True).strip()
freeze=r/'artifacts/persona-r13/source-freeze';freeze.mkdir(parents=True,exist_ok=True)
proc=subprocess.Popen(['git','archive',sha],cwd=r,stdout=subprocess.PIPE)
with tarfile.open(fileobj=proc.stdout,mode='r|') as archive:archive.extractall(freeze,filter='data')
assert proc.wait()==0
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
source={name:h(freeze/name) for name in subprocess.check_output(['git','ls-files'],cwd=r,text=True).splitlines() if name.startswith(('client/Assets/','client/Packages/','client/ProjectSettings/','server/','shared/','scripts/','art-source/'))}
service=r/'builds/playtest-persona-r13-service';service.mkdir(parents=True,exist_ok=True)
for folder in ('server','shared'):
 shutil.copytree(freeze/folder,service/folder,dirs_exist_ok=True,ignore=shutil.ignore_patterns('.env','tests','__pycache__'))
shutil.copy2(freeze/'scripts/playtest-integrated-release-entry.py',service/'server/release_entry.py')
def files(base):return {p.relative_to(base).as_posix():h(p) for p in sorted(base.rglob('*')) if p.is_file() and p.name not in ('release-manifest.json','prior-release-manifest.json','.DS_Store') and '__pycache__' not in p.parts}
def tree(d):return hashlib.sha256(json.dumps(d,sort_keys=True,separators=(',',':')).encode()).hexdigest()
app=r/'builds/playtest-persona-r13-mac/About Me.app';web=r/'builds/playtest-persona-r13-web'
assert app.is_dir() and (web/'index.html').is_file()
# Confirm the compiled client contains the R13 preparation and hollow presentation.
assembly=app/'Contents/Resources/Data/Managed/Assembly-CSharp.dll'
for symbol in ('PrepareEatingRoasts','HollowSpeechBubble','RoastPreparationPending'):
 assert symbol.encode() in assembly.read_bytes(),symbol
m.update(releaseId='playtest-persona-r13',runtimeSourceCommit=sha,clientSourceCommit=sha,apiSourceCommit=sha,sourceBranch=branch,sourceWorktree=str(r),sourceFrozenRoot=str(freeze),sourceFiles=source,sourceSnapshotSha256=hashlib.sha256(json.dumps(source,sort_keys=True).encode()).hexdigest(),eaterRoastVersion=2,parentReleaseId='playtest-persona-r12',parentProductionSource=m['runtimeSourceCommit'],appPath=str(app.relative_to(r)),executablePath=str((app/'Contents/MacOS/About Me').relative_to(r)),webPath=str(web.relative_to(r)),servicePath=str(service.relative_to(r)),apiPort=8097,gamePort=7797,packagedAtUTC=datetime.now(timezone.utc).isoformat(),nativeBuiltAtUTC=datetime.fromtimestamp(assembly.stat().st_mtime,timezone.utc).isoformat(),webBuiltAtUTC=datetime.fromtimestamp(next((web/'Build').glob('*.wasm')).stat().st_mtime,timezone.utc).isoformat(),sourceDirtyRange=subprocess.check_output(['git','status','--porcelain'],cwd=r,text=True).splitlines())
m['apiSourceSnapshot']=m['sourceSnapshotSha256'];m['requiredFeatures']+=['ai-skin-green-results','opening-prepared-roasts','hollow-compact-character-bubbles'];m['requiredAssemblySymbols']=[s for s in m['requiredAssemblySymbols'] if s!='RequestEatingRoast']+['PrepareEatingRoasts','HollowSpeechBubble']
for label,base in [('client',app),('web',web),('service',service)]:m[label+'Files']=files(base);m[label+'TreeSha256']=tree(m[label+'Files'])
m['assemblySha256']=h(assembly)
manifest=app.parent/'release-manifest.json';manifest.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n')
shutil.copy2(args.prior_manifest.parent/'prior-release-manifest.json',manifest.parent/'prior-release-manifest.json')
print(json.dumps({'release':m['releaseId'],'source':sha,'snapshot':m['sourceSnapshotSha256'],'sourceFiles':len(source)}))

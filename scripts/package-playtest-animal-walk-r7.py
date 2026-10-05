#!/usr/bin/env python3
"""Package the animal walking release from this worktree, inheriting complete R6."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[1];prior_root=root
p=argparse.ArgumentParser();p.add_argument('--destination',type=Path);args=p.parse_args();output=root/'artifacts/animal-persona-animation';output.mkdir(parents=True,exist_ok=True)
release='playtest-animal-walk-r7';app=root/f'builds/{release}-mac/About Me.app';web=root/f'builds/{release}-web';service=root/f'builds/{release}-service'
if not (app/'Contents/MacOS/About Me').is_file() or not (web/'index.html').is_file():raise SystemExit('Complete Mac/Web Release missing')
if service.exists():shutil.rmtree(service)
service.mkdir(parents=True)
for folder in ('server','shared'):shutil.copytree(root/folder,service/folder,ignore=shutil.ignore_patterns('.env','__pycache__','*.pyc','tests','.DS_Store','*.sqlite3','*.db'))
def hashes(base,folders=None):
 return dict(sorted((str(f.relative_to(base)),hashlib.sha256(f.read_bytes()).hexdigest()) for folder in (folders or [base]) for f in folder.rglob('*') if f.is_file() and f.name not in ('.env','.DS_Store') and '__pycache__' not in f.parts and f.suffix!='.pyc'))
def digest(files):return hashlib.sha256(json.dumps(files,sort_keys=True,separators=(',',':')).encode()).hexdigest()
sources=hashes(root,[root/x for x in ('client/Assets','client/Packages','client/ProjectSettings','server','shared','scripts','art-source')]);snapshot=hashlib.sha256(json.dumps(sources,sort_keys=True).encode()).hexdigest()
freeze=output/'source-freeze'
if freeze.exists():shutil.rmtree(freeze)
for name in sources:
 target=freeze/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/name,target)
assert hashes(freeze)==sources
prior_path=prior_root/'builds/playtest-animals-r6-mac/release-manifest.json';prior=json.loads(prior_path.read_text());commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
shutil.copy2(prior_path,app.parent/'prior-release-manifest.json')
client_files,web_files,service_files=hashes(app),hashes(web),hashes(service)
manifest={**{k:prior[k] for k in ('settingsSourceSnapshot','introductionSourceSnapshot')},'releaseId':release,'runtimeSourceCommit':commit,'runtimeSourceSha256':digest({k:v for k,v in sources.items() if k.startswith('client/')}),'sourceBranch':'codex/animal-persona','clientSourceCommit':commit,'clientBaseCommit':'78208762df19afa2bea556858690ec6b141b4edd','apiSourceCommit':commit,'sourceWorktree':str(root),'sourceFrozenRoot':str(freeze),'sourceSnapshotSha256':snapshot,'apiSourceSnapshot':snapshot,'sourceFiles':sources,'priorReleaseManifestSha256':hashlib.sha256(prior_path.read_bytes()).hexdigest(),'priorReleaseSnapshot':prior['sourceSnapshotSha256'],'protocolVersion':5,'appPath':str(app.relative_to(root)),'executablePath':str((app/'Contents/MacOS/About Me').relative_to(root)),'webPath':str(web.relative_to(root)),'servicePath':str(service.relative_to(root)),'apiPort':8095,'gamePort':7795,'runtimeCatalogCount':277,'mechanicCount':96,'traitAbilityCount':6,'mbtiTypeCount':16,'aiSkinCount':10,'animalVisuals':['cat','animal-dog','animal-rabbit','animal-hamster'],'modelDeliveries':json.loads((root/'artifacts/animal-persona/model-deliveries.json').read_text()),'animationDeliveries':json.loads((output/'animation-deliveries.json').read_text()),'animationCreditsConsumed':95,'localizedStrings':len(json.loads((root/'client/Assets/AboutMe/Resources/game-language.json').read_text())['entries']),'requiredFeatures':prior['requiredFeatures']+['animal-skeletal-walk-4','animal-held-landed-rest-pose'],'requiredAssemblySymbols':list(dict.fromkeys(prior['requiredAssemblySymbols']+['AnimalPersonaCatalog','TickAnimalPersona','AnimalPersonaMotion','AnimalPersonaRuntimeAudit'])),'clientFiles':client_files,'clientTreeSha256':digest(client_files),'webFiles':web_files,'webTreeSha256':digest(web_files),'serviceFiles':service_files,'serviceTreeSha256':digest(service_files),'assemblySha256':client_files['Contents/Resources/Data/Managed/Assembly-CSharp.dll'],'nativeBuiltAtUTC':datetime.fromtimestamp((output/'BuildMac.log').stat().st_mtime,timezone.utc).isoformat(),'webBuiltAtUTC':datetime.fromtimestamp((output/'BuildWeb.log').stat().st_mtime,timezone.utc).isoformat(),'packagedAtUTC':datetime.now(timezone.utc).isoformat(),'buildType':'Release','sourceDirtyRange':subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).splitlines(),'dataPolicy':'On first launch SQLite consistent backup of latest available R6/R5 DB into isolated R7 data directory; no credentials/database in package.'}
for file in (app.parent/'release-manifest.json',service/'release.json',output/'release.json'):file.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(output/'source-snapshot.json').write_text(json.dumps({'sourceSnapshotSha256':snapshot,'sourceFiles':sources,'sourceFrozenRoot':str(freeze)},ensure_ascii=False,indent=2)+'\n')
if args.destination:
 dst=args.destination.resolve()
 for suffix in ('mac','web','service'):
  target=dst/f'builds/{release}-{suffix}'
  if target.exists():raise SystemExit('Refuse overwrite '+str(target))
  shutil.copytree(root/f'builds/{release}-{suffix}',target)
 for name in ('start-playtest-animal-walk-r7.command','verify-playtest-animal-walk-r7.py'):shutil.copy2(root/'scripts'/name,dst/'scripts'/name)
 target=dst/'artifacts/animal-persona-walk-release'
 if target.exists():raise SystemExit('Refuse overwrite of published artifacts')
 target.mkdir(parents=True)
 for name in ('animation-deliveries.json','release.json','source-snapshot.json','walking.mp4','unity-animation-checks.txt','paired-build-check.txt','service-check.txt'):
  if (output/name).is_file():shutil.copy2(output/name,target/name)
 if (output/'runtime-audit').is_dir():shutil.copytree(output/'runtime-audit',target/'runtime-audit',ignore=shutil.ignore_patterns('*.log','walk-frames'))
print('Complete R7 Mac/Web/API packaged; snapshot',snapshot)

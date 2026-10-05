#!/usr/bin/env python3
"""Package the animal release from this worktree, inheriting verified complete R5."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[1];prior_root=Path('/Users/jitian/Documents/ChatGPT/About Me')
p=argparse.ArgumentParser();p.add_argument('--destination',type=Path);args=p.parse_args();output=root/'artifacts/animal-persona';output.mkdir(parents=True,exist_ok=True)
release='playtest-animals-r6';app=root/f'builds/{release}-mac/About Me.app';web=root/f'builds/{release}-web';service=root/f'builds/{release}-service'
if not (app/'Contents/MacOS/About Me').is_file() or not (web/'index.html').is_file():raise SystemExit('Complete Mac/Web Release missing')
if service.exists():shutil.rmtree(service)
service.mkdir(parents=True)
for folder in ('server','shared'):shutil.copytree(root/folder,service/folder,ignore=shutil.ignore_patterns('.env','__pycache__','*.pyc','tests','.DS_Store','*.sqlite3','*.db'))
def hashes(base,folders=None):
 return dict(sorted((str(f.relative_to(base)),hashlib.sha256(f.read_bytes()).hexdigest()) for folder in (folders or [base]) for f in folder.rglob('*') if f.is_file() and f.name not in ('.env','.DS_Store') and '__pycache__' not in f.parts and f.suffix!='.pyc'))
def digest(files):return hashlib.sha256(json.dumps(files,sort_keys=True,separators=(',',':')).encode()).hexdigest()
sources=hashes(root,[root/x for x in ('client/Assets','client/Packages','client/ProjectSettings','server','shared','scripts')]);snapshot=hashlib.sha256(json.dumps(sources,sort_keys=True).encode()).hexdigest()
freeze=output/'source-freeze'
if freeze.exists():shutil.rmtree(freeze)
for name in sources:
 target=freeze/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/name,target)
assert hashes(freeze)==sources
prior_path=prior_root/'builds/playtest-trait-r5-mac/release-manifest.json';prior=json.loads(prior_path.read_text());commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
client_files,web_files,service_files=hashes(app),hashes(web),hashes(service)
manifest={**{k:prior[k] for k in ('settingsSourceSnapshot','introductionSourceSnapshot')},'releaseId':release,'runtimeSourceCommit':commit,'runtimeSourceSha256':digest({k:v for k,v in sources.items() if k.startswith('client/')}),'sourceBranch':'codex/animal-persona','clientSourceCommit':commit,'clientBaseCommit':'78208762df19afa2bea556858690ec6b141b4edd','apiSourceCommit':commit,'sourceWorktree':str(root),'sourceFrozenRoot':str(freeze),'sourceSnapshotSha256':snapshot,'apiSourceSnapshot':snapshot,'sourceFiles':sources,'priorReleaseManifestSha256':hashlib.sha256(prior_path.read_bytes()).hexdigest(),'priorReleaseSnapshot':prior['sourceSnapshotSha256'],'protocolVersion':5,'appPath':str(app.relative_to(root)),'executablePath':str((app/'Contents/MacOS/About Me').relative_to(root)),'webPath':str(web.relative_to(root)),'servicePath':str(service.relative_to(root)),'apiPort':8093,'gamePort':7793,'runtimeCatalogCount':277,'mechanicCount':96,'traitAbilityCount':6,'mbtiTypeCount':16,'aiSkinCount':10,'animalVisuals':['cat','animal-dog','animal-rabbit','animal-hamster'],'modelDeliveries':json.loads((output/'model-deliveries.json').read_text()),'localizedStrings':len(json.loads((root/'client/Assets/AboutMe/Resources/game-language.json').read_text())['entries']),'requiredFeatures':prior['requiredFeatures']+['tripo-animal-models-4','single-use-thrown-persona','animal-nearby-random-chase'],'requiredAssemblySymbols':list(dict.fromkeys(prior['requiredAssemblySymbols']+['AnimalPersonaCatalog','TickAnimalPersona','AnimalPersonaMotion','AnimalPersonaRuntimeAudit'])),'clientFiles':client_files,'clientTreeSha256':digest(client_files),'webFiles':web_files,'webTreeSha256':digest(web_files),'serviceFiles':service_files,'serviceTreeSha256':digest(service_files),'assemblySha256':client_files['Contents/Resources/Data/Managed/Assembly-CSharp.dll'],'nativeBuiltAtUTC':datetime.fromtimestamp((output/'BuildMac.log').stat().st_mtime,timezone.utc).isoformat(),'webBuiltAtUTC':datetime.fromtimestamp((output/'BuildWeb.log').stat().st_mtime,timezone.utc).isoformat(),'packagedAtUTC':datetime.now(timezone.utc).isoformat(),'buildType':'Release','sourceDirtyRange':subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).splitlines(),'dataPolicy':'On first launch SQLite consistent backup of R5 DB into isolated R6 data directory; no credentials/database in package.'}
for file in (app.parent/'release-manifest.json',service/'release.json',output/'release.json'):file.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(output/'source-snapshot.json').write_text(json.dumps({'sourceSnapshotSha256':snapshot,'sourceFiles':sources,'sourceFrozenRoot':str(freeze)},ensure_ascii=False,indent=2)+'\n')
if args.destination:
 dst=args.destination.resolve()
 for suffix in ('mac','web','service'):
  target=dst/f'builds/{release}-{suffix}'
  if target.exists():raise SystemExit('Refuse overwrite '+str(target))
  shutil.copytree(root/f'builds/{release}-{suffix}',target)
 for name in ('start-playtest-animals-r6.command','verify-playtest-animals-r6.py'):shutil.copy2(root/'scripts'/name,dst/'scripts'/name)
 target=dst/'artifacts/animal-persona-release'
 if target.exists():raise SystemExit('Refuse overwrite of published artifacts')
 shutil.copytree(output,target,ignore=shutil.ignore_patterns('source-freeze','*.sqlite3','*.sqlite3-*','*-source','tripo-out','*-source-result.json','*.log'))
print('Complete R6 Mac/Web/API packaged; snapshot',snapshot)

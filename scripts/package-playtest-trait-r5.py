#!/usr/bin/env python3
"""Freeze and package the complete current R5, retaining its delivered R4 parent."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[1];shared_root=Path('/Users/jitian/Documents/ChatGPT/About Me')
p=argparse.ArgumentParser();p.add_argument('--destination',type=Path);args=p.parse_args();output=root/'artifacts/trait-persona-r5';output.mkdir(parents=True,exist_ok=True)
app=root/'builds/playtest-trait-r5-mac/About Me.app';web=root/'builds/playtest-trait-r5-web';service=root/'builds/playtest-trait-r5-service'
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
prior_path=shared_root/'builds/playtest-full-r4-mac/release-manifest.json';prior=json.loads(prior_path.read_text());commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
client_files,web_files,service_files=hashes(app),hashes(web),hashes(service)
manifest={**{k:prior[k] for k in ('settingsSourceSnapshot','introductionSourceSnapshot')},'releaseId':'playtest-trait-r5','sourceBranch':'codex/trait-persona-r5','clientSourceCommit':commit,'clientBaseCommit':'78208762df19afa2bea556858690ec6b141b4edd','apiSourceCommit':commit,'sourceWorktree':str(root),'sourceFrozenRoot':str(freeze),'sourceSnapshotSha256':snapshot,'apiSourceSnapshot':snapshot,'sourceFiles':sources,'priorReleaseManifestSha256':hashlib.sha256(prior_path.read_bytes()).hexdigest(),'priorReleaseSnapshot':prior['sourceSnapshotSha256'],'protocolVersion':4,'appPath':str(app.relative_to(root)),'executablePath':str((app/'Contents/MacOS/About Me').relative_to(root)),'webPath':str(web.relative_to(root)),'servicePath':str(service.relative_to(root)),'apiPort':8091,'gamePort':7791,'runtimeCatalogCount':274,'mechanicCount':96,'traitAbilityCount':6,'mbtiTypeCount':16,'aiSkinCount':10,'localizedStrings':len(json.loads((root/'client/Assets/AboutMe/Resources/game-language.json').read_text())['entries']),'requiredFeatures':prior['requiredFeatures']+['trait-word-personas','extruded-font-outline','borrowed-word-life-owner-preserved','optional-mbti-16','controlled-six-abilities','explicit-ai-skins-10'],'requiredAssemblySymbols':list(dict.fromkeys(prior['requiredAssemblySymbols']+['ThickWordVisual','TraitAbilityCatalog','BorrowWord','AISkinRegistry'])),'clientFiles':client_files,'clientTreeSha256':digest(client_files),'webFiles':web_files,'webTreeSha256':digest(web_files),'serviceFiles':service_files,'serviceTreeSha256':digest(service_files),'assemblySha256':client_files['Contents/Resources/Data/Managed/Assembly-CSharp.dll'],'nativeBuiltAtUTC':datetime.fromtimestamp((output/'BuildMac.log').stat().st_mtime,timezone.utc).isoformat(),'webBuiltAtUTC':datetime.fromtimestamp((output/'BuildWeb.log').stat().st_mtime,timezone.utc).isoformat(),'packagedAtUTC':datetime.now(timezone.utc).isoformat(),'buildType':'Release','sourceDirtyRange':subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).splitlines(),'dataPolicy':'On first launch SQLite consistent backup of prior R4 DB to new R5 data directory; subsequent databases independent. Old processes and DB preserved; no credentials/database in package.'}
for file in (app.parent/'release-manifest.json',service/'release.json',output/'release.json'):file.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(output/'source-snapshot.json').write_text(json.dumps({'sourceSnapshotSha256':snapshot,'sourceFiles':sources,'sourceFrozenRoot':str(freeze)},ensure_ascii=False,indent=2)+'\n')
if args.destination:
 dst=args.destination.resolve()
 for name in ('playtest-trait-r5-mac','playtest-trait-r5-web','playtest-trait-r5-service'):
  target=dst/'builds'/name
  if target.exists():raise SystemExit('Refuse overwrite of existing published candidate '+str(target))
  shutil.copytree(root/'builds'/name,target)
 for name in ('start-playtest-trait-r5.command','verify-playtest-trait-r5.py'):shutil.copy2(root/'scripts'/name,dst/'scripts'/name)
 target=dst/'artifacts/trait-persona-r5'
 if target.exists():raise SystemExit('Refuse overwrite of published artifacts')
 shutil.copytree(output,target,ignore=shutil.ignore_patterns('source-freeze','*.sqlite3','*.sqlite3-shm','*.sqlite3-wal','qa-crest-cache','real-ai-api.log'))
print('R5 frozen source',snapshot,'Mac/Web/API packaged; latest is not switched by packaging.')

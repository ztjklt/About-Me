#!/usr/bin/env python3
"""Freeze and package the complete R7 successor with a 20 Persona budget."""
import argparse
import hashlib
import json
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--prior-manifest', type=Path, required=True)
args = parser.parse_args()
prior = json.loads(args.prior_manifest.read_text())
if prior['releaseId'] != 'playtest-combined-r9':
    raise SystemExit('Requires the complete R9 integrated release')
release = 'playtest-persona-r10'
output = root / 'artifacts/persona-r10'
app = root / f'builds/{release}-mac/About Me.app'
web = root / f'builds/{release}-web'
service = root / f'builds/{release}-service'
output.mkdir(parents=True, exist_ok=True)
if not (app/'Contents/MacOS/About Me').is_file() or not (web/'index.html').is_file():
    raise SystemExit('Complete Mac/Web Release missing')
# The Web launch page must use this release pair; explicit URL parameters win.
index = web / 'index.html'
index.write_text(index.read_text().replace(':8093', ':8093').replace(':7793', ':7793')
                 .replace('matching R5 pair', 'matching R10 pair'))
if service.exists():
    shutil.rmtree(service)
for folder in ('server', 'shared'):
    shutil.copytree(root/folder, service/folder, ignore=shutil.ignore_patterns(
        '.env', '__pycache__', '*.pyc', 'tests', '.DS_Store', '*.sqlite3', '*.db'))

def hashes(base, folders=None):
    return dict(sorted((str(f.relative_to(base)), hashlib.sha256(f.read_bytes()).hexdigest())
        for folder in (folders or [base]) for f in folder.rglob('*')
        if f.is_file() and f.name not in ('.env', '.DS_Store') and
        '__pycache__' not in f.parts and f.suffix != '.pyc'))

def digest(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()

sources = hashes(root, [root/x for x in ('client/Assets', 'client/Packages',
    'client/ProjectSettings', 'server', 'shared', 'scripts', 'art-source')])
build_sources = json.loads((output/'paired-runtime-sources.json').read_text())
runtime = {k:v for k,v in sources.items() if k.startswith(('client/Assets/', 'client/Packages/', 'client/ProjectSettings/'))}
if runtime != build_sources:
    raise SystemExit('Client sources changed between paired builds and packaging')
snapshot = hashlib.sha256(json.dumps(sources, sort_keys=True).encode()).hexdigest()
freeze = output / 'source-freeze'
if freeze.exists():
    shutil.rmtree(freeze)
for name in sources:
    target = freeze / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root/name, target)
assert hashes(freeze) == sources
commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
shutil.copy2(args.prior_manifest, app.parent/'prior-release-manifest.json')
client_files, web_files, service_files = hashes(app), hashes(web), hashes(service)
manifest = {**{k:prior[k] for k in ('settingsSourceSnapshot', 'introductionSourceSnapshot',
    'clientBaseCommit', 'protocolVersion', 'runtimeCatalogCount', 'mechanicCount',
    'traitAbilityCount', 'mbtiTypeCount', 'aiSkinCount', 'animalVisuals',
    'modelDeliveries', 'animationDeliveries', 'animationCreditsConsumed', 'requiredAssemblySymbols')},
    'requiredAssemblySymbols': list(dict.fromkeys(prior['requiredAssemblySymbols']+['AuditSoloBots','SetSoloNpcCount'])), 'releaseId': release, 'runtimeSourceCommit': commit, 'clientSourceCommit': commit,
    'apiSourceCommit': commit, 'sourceBranch': 'codex/persona-limit-20',
    'soloSourceCommit': 'b19744cfe8dd034e62e1714c261af64904ae693a', 'sourceWorktree': str(root), 'sourceFrozenRoot': str(freeze),
    'sourceSnapshotSha256': snapshot, 'apiSourceSnapshot': snapshot, 'sourceFiles': sources,
    'priorReleaseManifestSha256': hashlib.sha256(args.prior_manifest.read_bytes()).hexdigest(),
    'priorReleaseSnapshot': prior['sourceSnapshotSha256'], 'maxPersonas': 20, 'flameDamagePerPulse': 20, 'aiPreferenceCount': 10, 'questionnaireCount': 16,
    'appPath': str(app.relative_to(root)),
    'executablePath': str((app/'Contents/MacOS/About Me').relative_to(root)),
    'webPath': str(web.relative_to(root)), 'servicePath': str(service.relative_to(root)),
    'apiPort': 8093, 'gamePort': 7793, 'requiredFeatures': list(dict.fromkeys(prior['requiredFeatures']+['flame-stability-20-per-pulse','unified-ai-preference-question'])),
    'clientFiles': client_files, 'clientTreeSha256': digest(client_files),
    'webFiles': web_files, 'webTreeSha256': digest(web_files),
    'serviceFiles': service_files, 'serviceTreeSha256': digest(service_files),
    'assemblySha256': client_files['Contents/Resources/Data/Managed/Assembly-CSharp.dll'],
    'nativeBuiltAtUTC': datetime.fromtimestamp((root/'artifacts/BuildMac.log').stat().st_mtime, timezone.utc).isoformat(),
    'webBuiltAtUTC': datetime.fromtimestamp((root/'artifacts/BuildWeb.log').stat().st_mtime, timezone.utc).isoformat(),
    'packagedAtUTC': datetime.now(timezone.utc).isoformat(), 'buildType': 'Release',
    'sourceDirtyRange': subprocess.check_output(['git', 'status', '--porcelain'], cwd=root, text=True).splitlines(),
    'dataPolicy': 'SQLite consistent backup of current R9 (or solo R6 / R7) profile DB on first launch; existing DB and processes retained. No secrets or DB shipped.'}
for path in (app.parent/'release-manifest.json', service/'release.json', output/'release.json'):
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
print('Complete R10 Mac/Web/API packaged; snapshot', snapshot)

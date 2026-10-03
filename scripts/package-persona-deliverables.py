"""Publish a matching local Mac/Web/API bundle after the editor build finishes."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def package():
    assembly = ROOT / 'builds/mac/About Me.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll'
    web = ROOT / 'builds/web'
    if not assembly.is_file() or not (web / 'index.html').is_file():
        raise SystemExit('Build Mac and Web with OriginalPersonaModelTools.BuildDeliverables first.')
    runtime = list((ROOT / 'client/Assets/AboutMe/Runtime').glob('*.cs'))
    if any(p.stat().st_mtime > assembly.stat().st_mtime for p in runtime):
        raise SystemExit('Runtime source changed after the Mac build; rebuild before packaging.')
    subprocess.run(['codesign', '--verify', '--deep', '--strict', str(ROOT / 'builds/mac/About Me.app')], check=True)
    stamp = str(time.time_ns())
    backup = ROOT / 'artifacts/persona-bundle-backup'
    backup.mkdir(parents=True, exist_ok=True)
    staged = {}
    try:
        for kind in ('mac', 'web', 'service'):
            fresh = ROOT / 'builds' / ('persona-fresh-' + kind + '-' + stamp)
            staged[kind] = fresh
            if kind in ('mac', 'web'):
                shutil.copytree(ROOT / 'builds' / kind, fresh)
            else:
                fresh.mkdir()
                for folder in ('server', 'shared'):
                    dest = fresh / folder
                    dest.mkdir()
                    for p in (ROOT / folder).iterdir():
                        if p.is_file() and (p.suffix in ('.py', '.json') or p.name in ('requirements.txt', '.env.example')):
                            shutil.copy2(p, dest / p.name)
                for p in (ROOT / 'client/Assets/AboutMe/Resources').glob('*.json'):
                    dest = fresh / 'client/Assets/AboutMe/Resources' / p.name
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(p, dest)
            # BuildPipeline preserves template timestamps. Refresh the index so an
            # existing browser revalidates it instead of requesting removed old filenames.
            if kind == 'web':
                os.utime(fresh / 'index.html', None)
        manifest = {
            'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
            'runtimeCatalogCount': len(json.loads((ROOT / 'shared/catalog.json').read_text())['items']),
            'mechanicCount': len(json.loads((ROOT / 'shared/persona-mechanics.json').read_text())['items']),
            'assemblySha256': hashlib.sha256(assembly.read_bytes()).hexdigest(),
            'catalogSha256': hashlib.sha256((ROOT / 'shared/catalog.json').read_bytes()).hexdigest(),
        }
        for kind, fresh in staged.items():
            (fresh / 'persona-bundle-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
        for kind, fresh in staged.items():
            live = ROOT / 'builds' / ('university-crests-' + kind)
            if live.exists():
                os.replace(live, backup / (live.name + '-' + stamp))
            os.replace(fresh, live)
    finally:
        for fresh in staged.values():
            if fresh.exists():
                shutil.rmtree(fresh)
    print(json.dumps(manifest))


if __name__ == '__main__':
    package()

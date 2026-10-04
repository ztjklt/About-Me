#!/usr/bin/env python3
"""Exact Release UI regression with preferences restored even on failure."""
import argparse,subprocess,tempfile,sys,os
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--binary',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--intro',action='store_true');p.add_argument('--skin',action='store_true');p.add_argument('--token-file',type=Path);args=p.parse_args();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
preferences='unity.About Me Studio.About Me';backup=subprocess.run(['defaults','export',preferences,'-'],capture_output=True)
try:
    for file in ('passed.txt','failed.txt'):(out/file).unlink(missing_ok=True)
    mode='ai-skin-ui' if args.skin else ('introduction-ui' if args.intro else 'trait-persona')
    env=dict(os.environ)
    if args.skin:
        import json
        env['ABOUTME_SKIN_AUDIT_TOKEN']=json.loads(args.token_file.read_text())[0]
    result=subprocess.run([str(args.binary),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','--ui-preview','presets','--'+mode+'-audit','--'+mode+'-audit-output',str(out),'-logFile',str(out/'runtime.log')],env=env,timeout=140,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    if result.returncode or (out/'failed.txt').exists() or not (out/'passed.txt').is_file():raise SystemExit((out/'failed.txt').read_text() if (out/'failed.txt').exists() else 'No audit completion: '+str(result.returncode))
    print('Actual Release engine checks:',len((out/'passed.txt').read_text().splitlines()))
finally:
    if backup.returncode==0:
        with tempfile.NamedTemporaryFile(suffix='.plist') as f:
            f.write(backup.stdout);f.flush();subprocess.run(['defaults','import',preferences,f.name],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)

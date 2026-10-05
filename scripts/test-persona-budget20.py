#!/usr/bin/env python3
"""Two actual Unity clients with 20 fixture assets each, using an isolated API DB.

This checks the released transport/authority/physics, not live AI or human play.
No user credentials, profiles or current processes are changed.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import urllib.request

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--manifest', type=Path, required=True)
parser.add_argument('--api-port', type=int, default=8097)
parser.add_argument('--game-port', type=int, default=7797)
parser.add_argument('--output', type=Path, default=Path('artifacts/persona-limit-20/network'))
args = parser.parse_args()
manifest = json.loads(args.manifest.read_text())
service = root / manifest['servicePath']
binary = root / manifest['executablePath']
out = root / args.output
out.mkdir(parents=True, exist_ok=True)
marker = out/'room.txt'
marker.unlink(missing_ok=True)
processes, logs = [], []

def start(command, env, name):
    log = open(out/(name+'.log'), 'w')
    logs.append(log)
    p = subprocess.Popen(command, env=env, stdout=log, stderr=subprocess.STDOUT)
    processes.append(p)
    return p

def wait_for(predicate, timeout, message):
    end = time.monotonic()+timeout
    while time.monotonic()<end:
        if predicate():
            return
        time.sleep(.2)
    raise RuntimeError(message)

def health():
    try:
        with urllib.request.urlopen(f'http://127.0.0.1:{args.api_port}/health',timeout=1) as r:
            return json.load(r).get('maxPersonas')==20
    except OSError:
        return False

try:
    with tempfile.TemporaryDirectory(prefix='aboutme-persona20-qa-') as temporary:
        env = dict(os.environ, ABOUTME_DB=str(Path(temporary)/'profiles.sqlite3'),
            GAME_SERVER_KEY='isolated-persona20-qa-key',
            ABOUTME_RELEASE_MANIFEST=str(args.manifest.resolve()),
            ABOUTME_GAME_PORT=str(args.game_port),
            ABOUTME_GAME_ENDPOINT=f'ws://127.0.0.1:{args.game_port}',
            ABOUTME_API_URL=f'http://127.0.0.1:{args.api_port}')
        os.environ.update({k:env[k] for k in ('ABOUTME_DB','GAME_SERVER_KEY')})
        sys.path.insert(0,str(service))
        from server import app as api, trait_words
        from fastapi.testclient import TestClient
        sessions = []
        with TestClient(api.app) as client:
            for index in range(2):
                session = client.post('/sessions',json={'nickname':f'Capacity QA {index+1}'}).json()
                intro = '；'.join(f'我喜欢篮球{i}' for i in range(17))+'；我的性格是热情；我的表情是友好微笑；我的MBTI是ENFP'
                props = [dict(assetId=f'qa-{index}-{i}',name='篮球',source=f'篮球{i}',templateId='Projectile',visualId='basketball',color='#EE7733') for i in range(17)]
                assets, _ = trait_words.augment(props,[],intro,[dict(text='友好微笑',category='mood',abilityId='bubble-lift')])
                assert len(assets)==20
                profile = dict(session['profile'],personas=assets,revision=1)
                with api.db() as con:
                    con.execute('UPDATE profiles SET profile=?,intro=? WHERE id=?',(json.dumps(profile),intro,profile['playerId']))
                sessions.append((session,assets))
        api_process = start([str(root/'.venv/bin/uvicorn'),'server.release_entry:app','--app-dir',str(service),'--host','127.0.0.1','--port',str(args.api_port)],env,'api')
        wait_for(lambda: api_process.poll() is not None or health(),15,'QA API did not start')
        assert api_process.poll() is None and health()
        authority = start([str(binary),'-batchmode','-nographics','--aboutme-server','--accelerated','-logFile',str(out/'authority.log')],env,'authority-console')
        wait_for(lambda: (out/'authority.log').exists() and f'Server listening on port {args.game_port}' in (out/'authority.log').read_text(errors='replace'),30,'QA authority did not start')
        clients = []
        for index,(session,_) in enumerate(sessions):
            command=[str(binary),'-batchmode','-nographics','--bot','--word-probe','--word-output',str(out/f'client-{index}.json'),'--word-preferred-ability','flame-fan' if index==0 else 'bubble-lift','--players','2','--marker',str(marker),'--endpoint',env['ABOUTME_GAME_ENDPOINT'],'-logFile',str(out/f'client-{index}.log')]
            if index==0:
                command += ['--create-room','--reconnect']
            clients.append(start(command,dict(env,ABOUTME_PROBE_TOKEN=session['token']),f'client-{index}-console'))
            if index==0:
                wait_for(marker.is_file,20,'QA host did not create a room')
        wait_for(lambda: all(p.poll() is not None for p in clients),100,'QA clients did not complete')
        reports=[]
        for index,(session,assets) in enumerate(sessions):
            assert clients[index].returncode==0
            report=json.loads((out/f'client-{index}.json').read_text())
            assert report['passed'] and report['players']==2 and report['physicalFrames']>10
            assert report['privacyChecked'] and report['protocolVersion']==5
            own_remaining=[frame['remaining'] for frame in report['publicFrames'] if frame['playerId']==report['playerId']]
            assert own_remaining and max(own_remaining)==20
            trace=(out/f'client-{index}.log').read_text(errors='replace')
            assert 'PROBE_PASS' in trace
            reports.append({'players':report['players'],'physicalFrames':report['physicalFrames'],
                'privacyChecked':report['privacyChecked'],'reconnectValidated':report['reconnectValidated'],
                'fixturePersonaCount':len(assets), 'maximumObservedRemaining':max(own_remaining),
                'useCommands':report['useCommands']})
        result={'passed':True,'releaseId':manifest['releaseId'],'assemblySha256':manifest['assemblySha256'],
            'scope':'Two actual released Unity clients, 20 fixture Personas each, isolated API and localhost authority, physics and host reconnect. No live AI, human, web or phone acceptance.', 'clients':reports}
        (out/'result.json').write_text(json.dumps(result,indent=2)+'\n')
        print(json.dumps(result,indent=2))
finally:
    for p in processes:
        if p.poll() is None:
            p.terminate()
    for p in processes:
        try:
            p.wait(timeout=5)
        except subprocess.TimeoutExpired:
            p.kill()
    for log in logs:
        log.close()

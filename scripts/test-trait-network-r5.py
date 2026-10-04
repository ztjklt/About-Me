#!/usr/bin/env python3
"""Two genuine Unity clients + real API accounts over localhost WebSocket.

Runs against an already configured API. Never creates fake profiles, grants skins,
logs credentials, alters PlayerPrefs, or stops an existing authority/client.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess
import time
import urllib.error
import urllib.request

ABILITIES = {'flame-fan', 'bubble-lift', 'front-shield', 'calm-dome', 'tractor-loose', 'spring-pad'}
class PersistenceVerified(Exception):
    pass


FORBIDDEN = {'source', 'introduction', 'generationCandidates', 'generationTerms', 'wordAbilities', 'token'}


def request(api, path, data=None, token=None, key=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if key:
        headers['X-Game-Key'] = key
    req = urllib.request.Request(api + path, data=None if data is None else json.dumps(data).encode(), headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=180) as response:
            return json.load(response)
    except urllib.error.HTTPError as exc:
        # Do not emit provider bodies or request material into test logs.
        raise RuntimeError(f'API {path} returned HTTP {exc.code}; no fixture substituted') from None


def private_keys(value):
    if isinstance(value, dict):
        return set(value) & FORBIDDEN or any(private_keys(v) for v in value.values())
    if isinstance(value, list):
        return any(private_keys(v) for v in value)
    return False


def wait_for(predicate, seconds, failure):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        if predicate():
            return
        time.sleep(.2)
    raise RuntimeError(failure)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--binary', type=Path, required=True)
    parser.add_argument('--api', required=True)
    parser.add_argument('--port', type=int, default=7793)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--env-file', type=Path, help='Optional private environment configuration; never copied into evidence')
    parser.add_argument('--token-file', type=Path, help='Optional private JSON array of two existing QA account tokens')
    parser.add_argument('--qa-token-output', type=Path, help='Private output file for later restart check; must be outside evidence directory')
    parser.add_argument('--verify-after-restart', type=Path, help='Prior result.json; compare authenticated profiles after caller restarts its independent API, no game processes launched')
    parser.add_argument('--authority-running', action='store_true', help='Use caller-verified authority without stopping it')
    args = parser.parse_args()
    if args.verify_after_restart and not args.token_file:
        parser.error('--verify-after-restart requires the private --token-file from the original run')
    binary = args.binary.resolve()
    if not binary.is_file():
        parser.error('binary does not exist')
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    # Keep each run isolated; reports from older binaries cannot produce a false pass.
    run = out / time.strftime('%Y%m%d-%H%M%S')
    run.mkdir(exist_ok=False)
    api = args.api.rstrip('/')
    env = dict(os.environ)
    if args.env_file:
        for line in args.env_file.read_text().splitlines():
            if not line.strip() or line.lstrip().startswith('#') or '=' not in line:
                continue
            name, value = line.split('=', 1)
            name = name.strip()
            if name in {'GAME_SERVER_KEY', 'ABOUTME_API_URL'}:
                env[name] = value.strip().strip('"').strip("'")
    env.update(ABOUTME_API_URL=api, ABOUTME_GAME_PORT=str(args.port), ABOUTME_GAME_ENDPOINT=f'ws://127.0.0.1:{args.port}')
    env.pop('ABOUTME_PROBE_TOKEN', None)
    result = {'passed': False, 'scope': 'Two genuine desktop Unity processes and real API accounts over localhost; not internet/phone/human acceptance',
              'binary': str(binary), 'binarySha256': hashlib.sha256(binary.read_bytes()).hexdigest(), 'api': api,
              'protocolVersion': 4, 'generation': 'not-run', 'reports': [], 'limitations': ['Borrow/cooldown transfer only asserted when observed; formal lifecycle audit covers deterministic transfer cases']}
    processes = []
    logs = []
    tokens = []
    try:
        health = request(api, '/health')
        if not health.get('traitWordPersonas') or health.get('partyMechanicsCount') != 96:
            raise RuntimeError('API does not match R5 word/96-mechanic capability contract')
        if args.token_file:
            tokens = json.loads(args.token_file.read_text())
            if not isinstance(tokens, list) or len(tokens) != 2 or not all(isinstance(t, str) and t for t in tokens):
                raise RuntimeError('private token file must contain exactly two QA tokens')
            profiles = [request(api, '/profiles/me', token=t) for t in tokens]
            result['generation'] = 'existing-real-api-profiles'
        else:
            if not health.get('aiConfigured'):
                raise RuntimeError('Real AI unavailable; word profiles were not manufactured')
            profiles = []
            for index in range(2):
                nickname = f'R5 联机 QA {index + 1}'
                session = request(api, '/sessions', {'nickname': nickname})
                tokens.append(session['token'])
                # Explicit first-person confirmations exercise all six server-approved abilities.
                texts = ['热情', '温柔', '专注', '理性', '好奇', '活力'] if index == 0 else ['温柔', '热情', '专注', '理性', '好奇', '活力']
                intro = '。'.join('我的性格是' + text for text in texts) + '。'
                profiles.append(request(api, '/personas/generate', {'nickname': nickname, 'introduction': intro,
                    'wordAbilities': [{'text': text, 'category': 'personality', 'abilityId': {'热情':'flame-fan','温柔':'bubble-lift','专注':'front-shield','理性':'calm-dome','好奇':'tractor-loose','活力':'spring-pad'}[text]} for text in texts]}, token=tokens[-1]))
            result['generation'] = 'real-ai-api-generation'
            # A genuine cache update must preserve the generated asset identity while saving a new ability.
            original = next((w for w in profiles[0].get('personas', []) if w.get('wordText') == '理性'), None)
            if original is None:
                raise RuntimeError('Real generation did not produce required rationality word')
            old_revision = profiles[0]['revision']
            profiles[0] = request(api, '/personas/generate', {'nickname': 'R5 联机 QA 1',
                'introduction': '。'.join('我的性格是' + text for text in ['热情', '温柔', '专注', '理性', '好奇', '活力']) + '。',
                'wordAbilities': [{'text': '理性', 'category': 'personality', 'abilityId': 'front-shield'}]}, token=tokens[0])
            changed = next((w for w in profiles[0].get('personas', []) if w.get('wordText') == '理性'), None)
            if not changed or changed['assetId'] != original['assetId'] or changed['abilityId'] != 'front-shield' or profiles[0]['revision'] <= old_revision:
                raise RuntimeError('Ability cache update did not preserve identity/save revision')
            result['abilityChange'] = {'playerId': profiles[0]['playerId'], 'assetId': changed['assetId'], 'wordText': '理性',
                'before': original['abilityId'], 'after': changed['abilityId'], 'beforeRevision': old_revision, 'afterRevision': profiles[0]['revision']}
        if args.qa_token_output:
            private_path = args.qa_token_output.resolve()
            if private_path == out or out in private_path.parents:
                raise RuntimeError('Private QA credentials must be outside evidence output')
            private_path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
            fd = os.open(private_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            with os.fdopen(fd, 'w') as handle:
                json.dump(tokens, handle)
        if len({p['playerId'] for p in profiles}) != 2:
            raise RuntimeError('Two distinct authenticated QA identities required')
        profile_evidence = []
        for index, (profile, token) in enumerate(zip(profiles, tokens)):
            words = [p for p in profile.get('personas', []) if p.get('visualId') == 'trait-word']
            if not words or not all(w.get('abilityId') in ABILITIES and w.get('wordText') and w.get('assetId') for w in words):
                raise RuntimeError('QA profile has no valid generated word assets')
            required_ability = 'flame-fan' if index == 0 else 'bubble-lift'
            if not any(w['abilityId'] == required_ability for w in words):
                raise RuntimeError(f'QA identity {index + 1} requires a real generated {required_ability} asset')
            entry = {'playerId': profile['playerId'], 'revision': profile.get('revision'), 'words': [{k:w.get(k) for k in ('assetId', 'wordCategory', 'wordText', 'abilityId')} for w in words]}
            if env.get('GAME_SERVER_KEY'):
                public = request(api, '/internal/verify', {'token': token}, key=env['GAME_SERVER_KEY'])
                if private_keys(public):
                    raise RuntimeError('Authority verification response leaks private profile fields')
                entry['authorityPrivacyChecked'] = True
            profile_evidence.append(entry)
        result['profiles'] = profile_evidence
        if args.verify_after_restart:
            prior = json.loads(args.verify_after_restart.read_text())
            previous = {p['playerId']: p for p in prior['profiles']}
            for entry in profile_evidence:
                before = previous.get(entry['playerId'])
                if not before or before['revision'] != entry['revision'] or sorted(before['words'], key=lambda w: w['assetId']) != sorted(entry['words'], key=lambda w: w['assetId']):
                    raise RuntimeError('API restart did not retain exact word assets/abilities/revision')
            result.update(passed=True, persistenceVerified=True, baseline=str(args.verify_after_restart.resolve()),
                          scope='Authenticated GET after caller-owned API restart; identical assets, ability IDs and saved revisions; no client/authority run')
            raise PersistenceVerified()
        marker = run / 'room.txt'
        def start(name, extra, token=None):
            log = run / (name + '.log')
            penv = dict(env)
            if token:
                penv['ABOUTME_PROBE_TOKEN'] = token
            process = subprocess.Popen([str(binary), '-batchmode', '-nographics', '-logFile', str(log), *extra], env=penv, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            processes.append(process)
            logs.append(log)
            return process, log
        if not args.authority_running:
            if not env.get('GAME_SERVER_KEY'):
                raise RuntimeError('GAME_SERVER_KEY required for real API authority authentication')
            with socket.socket() as sock:
                sock.bind(('127.0.0.1', args.port))
            authority, _ = start('authority', ['--aboutme-server', '--accelerated'])
            def listening():
                if authority.poll() is not None:
                    raise RuntimeError('Test authority exited before listening')
                try:
                    with socket.create_connection(('127.0.0.1', args.port), timeout=.2):
                        return True
                except OSError:
                    return False
            wait_for(listening, 45, 'Test authority did not listen')
        clients = []
        for index, token in enumerate(tokens):
            name = 'host' if index == 0 else 'guest'
            report = run / (name + '.json')
            extra = ['--bot', '--word-probe', '--word-output', str(report), '--event-output', str(run / (name + '-events.jsonl')),
                     '--word-preferred-ability', 'flame-fan' if index == 0 else 'bubble-lift', '--endpoint', env['ABOUTME_GAME_ENDPOINT'], '--marker', str(marker), '--players', '2', '--probe-timeout', '180']
            if index == 0:
                extra += ['--create-room', '--reconnect']
            clients.append((*start(name, extra, token), report))
            if index == 0:
                wait_for(lambda: marker.exists() or clients[0][0].poll() is not None, 45, 'Host failed to create room')
                if not marker.exists():
                    raise RuntimeError('Host exited before creating authenticated room')
        for process, log, report in clients:
            code = process.wait(timeout=195)
            if code or not report.exists():
                raise RuntimeError(f'Client failed with exit {code}; inspect {log.name}')
            data = json.loads(report.read_text())
            result['reports'].append(data)
            if not data.get('passed') or data.get('protocolVersion') != 4:
                raise RuntimeError('Word client evidence did not pass')
        a, b = result['reports']
        if a['room'] != b['room'] or a['matchId'] != b['matchId'] or set(a['identities']) != set(b['identities']):
            raise RuntimeError('Clients did not observe the same room/match/identities')
        if not a.get('reconnectValidated') or not all(r.get('privacyChecked') and r.get('physicalFrames', 0) >= 10 for r in (a, b)):
            raise RuntimeError('Missing actual reconnect/privacy/body pose evidence')
        expected_assets = {(p['playerId'], w['assetId']): (w['wordCategory'], w['wordText'], w['abilityId']) for p in profile_evidence for w in p['words']}
        for report in (a, b):
            seen = {(w['ownerId'], w['assetId']): (w['wordCategory'], w['wordText'], w['abilityId']) for w in report['assets']}
            if any(seen.get(key) != value for key, value in expected_assets.items()):
                raise RuntimeError('Client did not observe all generated word IDs/fields in reveal')
        actual = {(w['ownerId'],w['assetId']): (w['wordCategory'],w['wordText'],w['abilityId']) for w in a['assets']}
        for w in b['assets']:
            key = (w['ownerId'], w['assetId'])
            if key in actual and actual[key] != (w['wordCategory'],w['wordText'],w['abilityId']):
                raise RuntimeError('Client word asset fields disagree')
        result['observedEffectAbilities'] = sorted(set(a['effectAbilityIds']) | set(b['effectAbilityIds']))
        if not {'flame-fan', 'bubble-lift'} <= set(result['observedEffectAbilities']):
            raise RuntimeError('Missing formal flame/bubble effects from authenticated word instances')
        result['passed'] = True
    except PersistenceVerified:
        pass
    except Exception as exc:
        # Our error text excludes API/provider response bodies and credentials.
        result['failure'] = str(exc)
    finally:
        # Only terminate processes created by this driver, never a user's running game.
        for process in processes:
            if process.poll() is None:
                process.terminate()
        for process in processes:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
        # Defense in depth: tokens are passed only in environment, then scrubbed if a lower-level library emitted one.
        for log in logs:
            if log.exists():
                content = log.read_text(errors='replace')
                for token in tokens:
                    content = content.replace(token, '[REDACTED]')
                if env.get('GAME_SERVER_KEY'):
                    content = content.replace(env['GAME_SERVER_KEY'], '[REDACTED]')
                log.write_text(content)
        (run / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2))
    print(json.dumps({'passed': result['passed'], 'result': str(run / 'result.json'), 'generation': result['generation']}, ensure_ascii=False))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())

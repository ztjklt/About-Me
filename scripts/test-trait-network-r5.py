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
    parser.add_argument('--ai-skins-catalog', type=Path, default=Path(__file__).resolve().parents[1] / 'shared/ai-skins.json')
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
        skin_catalog = json.loads(args.ai_skins_catalog.read_text())
        published_skins = {row['skin'] for row in skin_catalog['skins']}
        if len(published_skins) != 10 or not {'gpt-girl', 'gemini-girl'} <= published_skins:
            raise RuntimeError('Shared AI skin catalog does not match ten published choices')
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
        if not args.token_file:
            result['skinChoices'] = []
            preference = '我喜欢DeepSeek。我喜欢Claude。我喜欢GPT。我喜欢Gemini。我喜欢Grok。我喜欢Kimi。我喜欢千问。我喜欢MiniMax。我喜欢豆包。我喜欢GLM。'
            for index, token in enumerate(tokens):
                profile = profiles[index]
                if profile.get('characterSkin') != 'normal':
                    raise RuntimeError('AI preference must not automatically select a character skin')
                texts = ['热情', '温柔', '专注', '理性', '好奇', '活力'] if index == 0 else ['温柔', '热情', '专注', '理性', '好奇', '活力']
                source_intro = '。'.join('我的性格是' + text for text in texts) + '。' + preference
                baseline_words = [{k:w.get(k) for k in ('assetId', 'wordCategory', 'wordText', 'abilityId', 'color')} for w in profile.get('personas', []) if w.get('visualId') == 'trait-word']
                # First save only the explicitly self-stated preferences. Qualification must leave the normal face selected.
                qualified = request(api, '/profiles/me/appearance', {'appearance': profile.get('appearance') or {},
                    'expectedRevision': profile['appearanceRevision'], 'introduction': source_intro}, token=token)
                qualified = request(api, '/profiles/me', token=token)
                if qualified.get('characterSkin') != 'normal' or set(qualified.get('unlockedCharacterSkins', [])) != published_skins:
                    raise RuntimeError('Explicit preferences did not qualify all ten skins while keeping normal selected')
                chosen = 'gpt-girl' if index == 0 else 'gemini-girl'
                request(api, '/profiles/me/appearance', {'appearance': qualified.get('appearance') or {},
                    'expectedRevision': qualified['appearanceRevision'], 'introduction': source_intro, 'characterSkin': chosen}, token=token)
                saved = request(api, '/profiles/me', token=token)
                saved_words = [{k:w.get(k) for k in ('assetId', 'wordCategory', 'wordText', 'abilityId', 'color')} for w in saved.get('personas', []) if w.get('visualId') == 'trait-word']
                if saved.get('characterSkin') != chosen or set(saved.get('unlockedCharacterSkins', [])) != published_skins or saved['appearanceRevision'] <= qualified['appearanceRevision'] or saved_words != baseline_words:
                    raise RuntimeError('Active AI skin selection was not saved with qualifications and original word assets')
                profiles[index] = saved
                result['skinChoices'].append({'playerId': saved['playerId'], 'before': 'normal', 'after': chosen,
                    'qualificationKeptNormal': True, 'unlockedCharacterSkins': sorted(saved['unlockedCharacterSkins']), 'appearanceRevision': saved['appearanceRevision']})
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
            chosen = 'gpt-girl' if index == 0 else 'gemini-girl'
            if profile.get('characterSkin') != chosen or set(profile.get('unlockedCharacterSkins', [])) != published_skins:
                raise RuntimeError('QA profiles must retain active GPT/Gemini choices and all ten real qualifications')
            entry = {'playerId': profile['playerId'], 'revision': profile.get('revision'), 'appearanceRevision': profile.get('appearanceRevision'), 'characterSkin': profile.get('characterSkin'), 'unlockedCharacterSkins': sorted(profile.get('unlockedCharacterSkins', [])), 'words': [{k:w.get(k) for k in ('assetId', 'wordCategory', 'wordText', 'abilityId', 'color')} for w in words], 'revealedWords': [{k:w.get(k) for k in ('assetId', 'wordCategory', 'wordText', 'abilityId', 'color')} for w in profile.get('personas', [])[:2] if w.get('visualId') == 'trait-word']}
            if env.get('GAME_SERVER_KEY'):
                public = request(api, '/internal/verify', {'token': token}, key=env['GAME_SERVER_KEY'])
                if private_keys(public):
                    raise RuntimeError('Authority verification response leaks private profile fields')
                if public.get('characterSkin') != profile['characterSkin'] or set(public.get('unlockedCharacterSkins', [])) != published_skins:
                    raise RuntimeError('Authority verification did not retain verified AI skin and qualifications')
                entry['authorityPrivacyChecked'] = True
            profile_evidence.append(entry)
        result['profiles'] = profile_evidence
        if args.verify_after_restart:
            prior = json.loads(args.verify_after_restart.read_text())
            previous = {p['playerId']: p for p in prior['profiles']}
            for entry in profile_evidence:
                before = previous.get(entry['playerId'])
                if not before or any(before.get(key) != entry.get(key) for key in ('revision', 'appearanceRevision', 'characterSkin', 'unlockedCharacterSkins')) or sorted(before['words'], key=lambda w: w['assetId']) != sorted(entry['words'], key=lambda w: w['assetId']):
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
        expected_skins = {p['playerId']: p['characterSkin'] for p in profile_evidence}
        for report in (a, b):
            if {p['playerId']: p['characterSkin'] for p in report.get('playerSkins', [])} != expected_skins:
                raise RuntimeError('Public client AI skins do not match explicitly saved API choices')
        if {p['playerId']: p['characterSkin'] for p in a.get('reconnectPlayerSkins', [])} != expected_skins:
            raise RuntimeError('Reconnect did not retain both verified AI skin choices')
        expected_assets = {(p['playerId'], w['assetId']): (w['wordCategory'], w['wordText'], w['abilityId'], w['color']) for p in profile_evidence for w in p['words']}
        for report in (a, b):
            seen = {(w['ownerId'], w['assetId']): (w['wordCategory'], w['wordText'], w['abilityId'], w['color']) for w in report['assets']}
            # The production wire contract exposes all of one's own inventory, but only
            # Items.Take(2) per other owner during Reveal. Remaining opponents' assets
            # stay private until actually dropped/carried; never require a wider leak.
            required = {key:value for key,value in expected_assets.items() if key[0] == report['playerId']}
            for profile in profile_evidence:
                if profile['playerId'] != report['playerId']:
                    required.update({(profile['playerId'], w['assetId']): (w['wordCategory'],w['wordText'],w['abilityId'],w['color']) for w in profile['revealedWords']})
            if any(seen.get(key) != value for key, value in required.items()):
                raise RuntimeError('Client is missing own inventory or publicly revealed Take(2) word IDs/fields')
            if any(expected_assets.get(key) != value for key,value in seen.items()):
                raise RuntimeError('An actually visible word asset differs from authenticated API fields')
        actual = {(w['ownerId'],w['assetId']): (w['wordCategory'],w['wordText'],w['abilityId'],w['color']) for w in a['assets']}
        for w in b['assets']:
            key = (w['ownerId'], w['assetId'])
            if key in actual and actual[key] != (w['wordCategory'],w['wordText'],w['abilityId'],w['color']):
                raise RuntimeError('Client word asset fields disagree')
        expected_public_words = {(w['wordCategory'],w['wordText'],w['abilityId'],w['color']) for p in profile_evidence for w in p['words']}
        for report in (a, b):
            frames = report.get('publicFrames', [])
            models = report.get('modelInstances', [])
            if {m['playerId']:m['characterSkin'] for m in models if m.get('verified') and m.get('vertices', 0) > 0 and m.get('bones', 0) > 0} != expected_skins:
                raise RuntimeError('Missing actual peer ToyActor resource mesh/bone binding instances')
            if report['reconnectRequested'] and not all(m.get('reconnectFrames', 0) > 0 for m in models):
                raise RuntimeError('Real model instances not verified after reconnect')
            if not frames or not all(f.get('modelVerified') and f.get('heldWordMeshVerified') for f in frames):
                raise RuntimeError('Model/held physical word instance verification failed')
            if not any(f.get('actionId', 0) > 0 and f.get('actionKind') != 'idle' for f in frames):
                raise RuntimeError('No actual public action observed')
            if any(f.get('wordText') and (f['wordCategory'],f['wordText'],f['abilityId'],f['color']) not in expected_public_words for f in frames):
                raise RuntimeError('Public equipped text/ability/color differs from authenticated word assets')
        public_keys = ('actionId','actionKind','remaining','wordCategory','wordText','abilityId','color','cooldown','time')
        public_a = {(f['serverTick'], f['playerId']): f for f in a['publicFrames']}
        public_b = {(f['serverTick'], f['playerId']): f for f in b['publicFrames']}
        common = public_a.keys() & public_b.keys()
        if not common:
            raise RuntimeError('No shared public server ticks observed by both clients')
        if any(any(public_a[key].get(field) != public_b[key].get(field) for field in public_keys) for key in common):
            raise RuntimeError('Peer equipped words/colors/actions/remaining/cooldown disagree at identical server ticks')
        result['matchingPublicFramePairs'] = len(common)
        result['modelInstanceScope'] = 'Headless genuine ToyActor imported Resource meshes and bone bindings driven by public snapshots; not pixel rendering'
        result['publicActions'] = sorted({f['actionKind'] for report in (a, b) for f in report['publicFrames']})
        borrowed_checks = []
        all_frames = [f for report in (a, b) for f in report['publicFrames']]
        for report in (a, b):
            for observation in report['observations']:
                if not observation.get('holderId') or observation['holderId'] == observation['ownerId']:
                    continue
                prior = [f for f in all_frames if f['playerId'] == observation['ownerId'] and f['instanceId'] == observation['instanceId'] and f['time'] <= observation['time'] and f['cooldown'] > 0]
                if not prior:
                    continue
                before = max(prior, key=lambda f:f['time'])
                if before['remaining'] != observation['ownerRemaining']:
                    raise RuntimeError('Observed borrowing changed owner remaining count')
                # Compare expiry times rather than requiring identical samples at different wire ticks.
                expiry_difference = abs(before['time'] + before['cooldown'] - observation['time'] - observation['cooldown'])
                if observation['cooldown'] <= 0 or expiry_difference > .4:
                    raise RuntimeError('Observed positive instance cooldown was reset across holder transfer')
                borrowed_checks.append({'instanceId': observation['instanceId'], 'ownerId': observation['ownerId'],
                    'holderId': observation['holderId'], 'ownerRemaining': observation['ownerRemaining'], 'remainingCooldown': observation['cooldown'], 'expiryDifference': expiry_difference})
        result['borrowCooldownChecks'] = borrowed_checks
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

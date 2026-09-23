"""Opt-in DeepSeek API probe. Prints metadata only; never prints prompts, output or keys."""
import json
import os
import sys
import tempfile
import uuid
from pathlib import Path

from fastapi.testclient import TestClient

root = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(root))


def load_llm_config(path):
    for line in path.read_text().splitlines():
        key, separator, value = line.partition('=')
        key = key.strip().removeprefix('export ')
        if separator and key in {'LLM_API_KEY', 'LLM_MODEL', 'LLM_BASE_URL'}:
            os.environ[key] = value.strip().strip('"\'')


def require_ok(response, stage):
    if response.status_code != 200:
        print(json.dumps({'stage': stage, 'status': response.status_code, 'result': 'failed'}))
        raise SystemExit(1)
    return response.json()


def main():
    if len(sys.argv) != 2:
        raise SystemExit('Usage: python scripts/probe-real-ai.py /path/to/uncommitted/server/.env')
    load_llm_config(Path(sys.argv[1]))
    from server import app as module
    with tempfile.TemporaryDirectory(prefix='aboutme-ai-probe-') as temporary:
        os.environ['ABOUTME_DB'] = str(Path(temporary) / 'probe.sqlite3')
        os.environ['GAME_SERVER_KEY'] = 'local-probe-only'
        with TestClient(module.app) as client:
            health = require_ok(client.get('/health'), 'health')
            if health['aiProvider'] != 'DeepSeek' or not health['aiConfigured']:
                print(json.dumps({'stage': 'configuration', 'result': 'not-deepseek'}))
                raise SystemExit(1)
            session = require_ok(client.post('/sessions', json={'nickname': '测试玩家'}), 'session')
            token = session['token']
            authorization = {'Authorization': 'Bearer ' + token}
            introduction = '我喜欢打篮球。'
            profile = require_ok(client.post('/personas/generate', headers=authorization,
                json={'introduction': introduction, 'nickname': '测试玩家'}), 'generation')
            own = require_ok(client.get('/profiles/me', headers=authorization), 'persistence')
            verified = require_ok(client.post('/internal/verify',
                headers={'X-Game-Key': 'local-probe-only'}, json={'token': token}), 'game_verify')
            if not profile['personas'] or own['personas'] != profile['personas'] or verified['personas'] != profile['personas']:
                print(json.dumps({'stage': 'profile_consistency', 'result': 'failed'}))
                raise SystemExit(1)
            asset = profile['personas'][0]
            event = {'match_id': uuid.uuid4().hex, 'event_id': uuid.uuid4().hex,
                     'item_name': asset['name'], 'template_id': asset['templateId'],
                     'visual_id': asset['visualId']}
            game_auth = {'X-Game-Key': 'local-probe-only'}
            roast = require_ok(client.post('/internal/roast', headers=game_auth, json=event), 'roast')
            replay = require_ok(client.post('/internal/roast', headers=game_auth, json=event), 'roast_replay')
            if roast != replay or roast['aiSource'] != 'provider':
                print(json.dumps({'stage': 'roast_idempotency', 'result': 'failed'}))
                raise SystemExit(1)
            print(json.dumps({'provider': health['aiProvider'], 'model': health['aiModel'],
                              'persona_count': len(profile['personas']), 'persisted': True,
                              'game_verified': True, 'roast_length': len(roast['roast']),
                              'roast_replay_cached': True, 'result': 'passed'}, ensure_ascii=False))


if __name__ == '__main__':
    main()

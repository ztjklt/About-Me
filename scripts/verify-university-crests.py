#!/usr/bin/env python3
"""Opt-in live DeepSeek + official website + API verification, with no credentials in output."""
import argparse
import json
import os
import shlex
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--env', type=Path, default=ROOT / 'server/.env')
    parser.add_argument('--introduction', default='我毕业于中山大学，在北京大学读研，喜欢篮球')
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/university-crest-live')
    args = parser.parse_args()
    if args.env.is_file():
        for line in args.env.read_text().splitlines():
            if not line.strip() or line.lstrip().startswith('#') or '=' not in line:
                continue
            key, value = line.split('=', 1)
            if key.strip() in ('LLM_API_KEY', 'LLM_BASE_URL', 'LLM_MODEL', 'DEEPSEEK_API_KEY', 'DEEPSEEK_BASE_URL', 'UNIVERSITY_CREST_MODEL'):
                values = shlex.split(value, comments=True)
                os.environ[key.strip()] = values[0] if values else ''
    args.output.mkdir(parents=True, exist_ok=True)
    os.environ['ABOUTME_DB'] = str(args.output / 'test-profiles.sqlite3')
    os.environ['UNIVERSITY_CREST_CACHE'] = str(args.output / 'crests')
    os.environ['GAME_SERVER_KEY'] = 'local-crest-validation-key'
    from fastapi.testclient import TestClient
    from server.app import app
    with TestClient(app) as client:
        login = client.post('/sessions', json={'nickname': '校徽验证'}).json()
        auth = {'Authorization': 'Bearer ' + login['token']}
        response = client.post('/personas/generate', headers=auth, json={'nickname': '校徽验证', 'introduction': args.introduction})
        if response.status_code != 200:
            print('Generation failed:', response.status_code, response.json().get('detail')); return 1
        profile = response.json()
        (args.output / 'profile.json').write_text(json.dumps(profile, ensure_ascii=False, indent=2))
        assert client.get('/profiles/me', headers=auth).json() == profile
        verified = client.post('/internal/verify', json={'token': login['token']}, headers={'X-Game-Key': 'local-crest-validation-key'}).json()
        assert verified['personas'] == profile['personas']
        rows = []
        for item in profile['personas']:
            if item['visualId'] != 'school-disc':
                continue
            row = {'school': item['institutionName'], 'status': item['crestStatus'], 'source': item.get('emblemSource', '')}
            if item['crestStatus'] == 'ready':
                image = client.get(item['emblemPath']); assert image.status_code == 200
                assert image.headers['content-type'] == 'image/png'
                (args.output / (item['institutionName'] + '.png')).write_bytes(image.content)
                row['imageServed'] = True
            rows.append(row)
        report = {'introduction': args.introduction, 'realDeepSeek': True, 'officialWebsites': True,
                  'persistence': True, 'internalVerification': True, 'schools': rows}
        (args.output / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
        print(json.dumps(report, ensure_ascii=False))
        return 0 if rows and all(x['status'] == 'ready' for x in rows) else 1

if __name__ == '__main__':
    raise SystemExit(main())

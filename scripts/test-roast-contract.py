"""Check a Unity socket snapshot through the API to a mock roast provider."""
import argparse
import json
import os
import pathlib
import sys
import tempfile

import httpx
from fastapi.testclient import TestClient


ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
from server import app as api  # noqa: E402


def reject_private_fields(value):
    if isinstance(value, dict):
        if 'source' in value:
            raise AssertionError('Unity snapshot contained Persona source')
        for nested in value.values():
            reject_private_fields(nested)
    elif isinstance(value, list):
        for nested in value:
            reject_private_fields(nested)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--snapshot', default='artifacts/final-network-2/room.txt.state.json')
    args = parser.parse_args()
    snapshot = json.loads((ROOT / args.snapshot).read_text())
    reject_private_fields(snapshot)
    items = snapshot.get('ground', []) + snapshot.get('inventory', []) + snapshot.get('revealed', [])
    if not items:
        raise AssertionError('Unity snapshot has no Persona to test')
    asset = items[0]['asset']
    required = {'assetId', 'name', 'templateId', 'visualId', 'color'}
    allowed = required | {'emblemPath', 'emblemCrop', 'generatedBy'}
    if not required <= set(asset) or not set(asset) <= allowed:
        raise AssertionError(f'Unity public asset fields differ: {set(asset)}')
    body = {'item_name': asset['name'], 'template_id': asset['templateId'],
            'visual_id': asset['visualId']}
    forwarded = []

    class MockProvider:
        def __init__(self, **kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kwargs):
            forwarded.append(json.loads(kwargs['json']['messages'][1]['content']))
            return httpx.Response(200, json={'choices': [{'message': {
                'content': '这件小道具刚出场就很自信，转眼却进了别人的背包。'}}]},
                request=httpx.Request('POST', url))

    with tempfile.TemporaryDirectory() as temporary:
        old = {key: os.environ.get(key) for key in
               ('ABOUTME_DB', 'LLM_API_KEY', 'LLM_MODEL', 'LLM_BASE_URL')}
        original_client = api.httpx.AsyncClient
        try:
            os.environ.update(ABOUTME_DB=str(pathlib.Path(temporary) / 'test.sqlite3'),
                              LLM_API_KEY='test-key', LLM_MODEL='test-model',
                              LLM_BASE_URL='https://mock-provider.invalid/v1')
            api.httpx.AsyncClient = MockProvider
            with TestClient(api.app) as client:
                token = client.post('/sessions', json={'nickname': '契约探针'}).json()['token']
                response = client.post('/roast', json=body,
                                       headers={'Authorization': 'Bearer ' + token})
                if response.status_code != 200:
                    raise AssertionError(f'API rejected public Unity asset: {response.status_code}')
                if forwarded != [body]:
                    raise AssertionError(f'Provider received unexpected fields: {forwarded}')
                forbidden = client.post('/roast', json=dict(body, source='PRIVATE_INTRO_FRAGMENT'),
                                        headers={'Authorization': 'Bearer ' + token})
                if forbidden.status_code != 422:
                    raise AssertionError('API accepted a request carrying source')
        finally:
            api.httpx.AsyncClient = original_client
            for key, value in old.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value
    print('PASS: Unity WebSocket snapshot contains only public Persona fields; '
          'API forwarded only three public roast fields to a mock provider and rejected source.')


if __name__ == '__main__':
    main()

"""Run the API behind the deployment Caddy rule and exercise source quotas locally."""
import argparse
import os
import pathlib
import secrets
import shutil
import subprocess
import tempfile
import time

import httpx


ROOT = pathlib.Path(__file__).resolve().parent.parent
API_PORT = 18881
EDGE_PORT = 18880


def wait_ready(url, processes):
    deadline = time.monotonic() + 15
    with httpx.Client(trust_env=False) as client:
        while time.monotonic() < deadline:
            if any(process.poll() is not None for process in processes):
                raise RuntimeError('API or Caddy exited before becoming ready')
            try:
                if client.get(url, timeout=1).status_code == 200:
                    return
            except httpx.HTTPError:
                pass
            time.sleep(.2)
    raise RuntimeError('Local proxy did not become ready')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--alternate-host', help='This machine\'s LAN IP, as a second real TCP source')
    args = parser.parse_args()
    if not shutil.which('caddy'):
        raise RuntimeError('Caddy is required for this runtime check')
    if not args.alternate_host:
        args.alternate_host = subprocess.check_output(['ipconfig', 'getifaddr', 'en0'], text=True).strip()
    if not args.alternate_host or args.alternate_host.startswith('127.'):
        raise RuntimeError('A non-loopback local IP is required for the second source')

    source = (ROOT / 'deploy/Caddyfile').read_text().split('{$GAME_DOMAIN}', 1)[0]
    caddyfile = '{\n    admin off\n}\n' + source.replace('{$API_DOMAIN}', f':{EDGE_PORT}').replace(
        'api:8081', f'127.0.0.1:{API_PORT}')
    key = secrets.token_urlsafe(32)
    with tempfile.TemporaryDirectory() as temporary:
        path = pathlib.Path(temporary)
        config = path / 'Caddyfile'
        config.write_text(caddyfile)
        env = os.environ.copy()
        env.update(ABOUTME_DB=str(path / 'quota.sqlite3'), ABOUTME_PROXY_KEY=key)
        processes = []
        try:
            with (path / 'api.log').open('w') as api_log, (path / 'caddy.log').open('w') as caddy_log:
                api = subprocess.Popen([str(ROOT / '.venv/bin/python'), '-m', 'uvicorn',
                    'server.app:app', '--host', '127.0.0.1', '--port', str(API_PORT),
                    '--no-proxy-headers'], cwd=ROOT, env=env, stdout=api_log,
                    stderr=subprocess.STDOUT)
                processes.append(api)
                edge = subprocess.Popen(['caddy', 'run', '--config', str(config), '--adapter', 'caddyfile'],
                                        cwd=ROOT, env=env, stdout=caddy_log, stderr=subprocess.STDOUT)
                processes.append(edge)
                loopback = f'http://127.0.0.1:{EDGE_PORT}'
                lan = f'http://{args.alternate_host}:{EDGE_PORT}'
                wait_ready(loopback + '/catalog', processes)
                with httpx.Client(trust_env=False, timeout=5) as client:
                    def session(base, headers=None):
                        return client.post(base + '/sessions', json={'nickname': '配额探针'}, headers=headers)

                    local_status = [session(loopback).status_code for _ in range(10)]
                    lan_status = [session(lan).status_code for _ in range(10)]
                    if local_status != [200] * 10 or lan_status != [200] * 10:
                        raise AssertionError(f'Different TCP sources shared a quota: {local_status}, {lan_status}')
                    forged = session(loopback, {'X-AboutMe-Client-IP': '203.0.113.99',
                                                'X-AboutMe-Proxy-Key': 'forged'})
                    if forged.status_code != 429 or session(lan).status_code != 429:
                        raise AssertionError('Proxy did not overwrite forged headers or enforce peer quotas')
                    direct = session(f'http://127.0.0.1:{API_PORT}',
                                     {'X-AboutMe-Client-IP': '203.0.113.99',
                                      'X-AboutMe-Proxy-Key': 'forged'})
                    if direct.status_code != 429:
                        raise AssertionError('API trusted a direct request with a forged proxy key')
                    if client.get(loopback + '/internal/verify').status_code != 404:
                        raise AssertionError('Caddy exposed an internal API path')
        finally:
            for process in processes:
                if process.poll() is None:
                    process.terminate()
            for process in processes:
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
    print('PASS: running Caddy and API separated two real local TCP source IPs; '
          'spoofed proxy headers did not split quotas and /internal stayed private.')


if __name__ == '__main__':
    main()

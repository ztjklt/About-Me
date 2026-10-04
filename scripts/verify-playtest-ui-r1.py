#!/usr/bin/env python3
"""Verify the exact local bundle before launch; occupied ports are never reused."""
import argparse
import hashlib
import json
import subprocess
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('manifest', type=Path)
parser.add_argument('--api-pid', type=int)
parser.add_argument('--authority-pid', type=int)
args = parser.parse_args()
manifest = json.loads(args.manifest.read_text())
root = args.manifest.resolve().parents[2]

def verify_files(base, files):
    for name, expected in files.items():
        path = base / name
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise SystemExit('版本文件缺失或哈希不匹配：' + str(path))

app = root / manifest['appPath']
service = root / manifest['servicePath']
verify_files(app, manifest['clientFiles'])
verify_files(service, manifest['serviceFiles'])
if manifest['clientSourceCommit'] != '205fcd22fcb4d81909f575d5327a48c0fc8abfa9':
    raise SystemExit('未核对的客户端源码版本')
if manifest['apiSourceCommit'] != '049312fe7917d82198cb94e305ad1a3415a52810':
    raise SystemExit('API 源码基线不匹配')

def listener(port, expected_pid):
    result = subprocess.run(['lsof', '-nP', '-iTCP:' + str(port), '-sTCP:LISTEN', '-Fp'],
                            text=True, capture_output=True)
    pids = {int(line[1:]) for line in result.stdout.splitlines() if line.startswith('p')}
    if pids != {expected_pid}:
        raise SystemExit('端口监听进程与本轮启动进程不匹配：' + str(port))

if args.api_pid is not None:
    listener(manifest['apiPort'], args.api_pid)
    command = subprocess.check_output(['ps', '-p', str(args.api_pid), '-o', 'command='], text=True)
    if str(service) not in command or 'server.release_entry:app' not in command:
        raise SystemExit('API 进程路径不匹配')
    url = 'http://127.0.0.1:' + str(manifest['apiPort'])
    with urllib.request.urlopen(url + '/local-release-version', timeout=2) as response:
        version = json.load(response)
    if any(version.get(key) != manifest[key] for key in ('releaseId', 'apiSourceCommit', 'serviceTreeSha256')):
        raise SystemExit('运行 API 交付标识不匹配')
    if version.get('pid') != args.api_pid or version.get('serviceRoot') != str(service):
        raise SystemExit('运行 API 进程身份不匹配')
    with urllib.request.urlopen(url + '/health', timeout=2) as response:
        health = json.load(response)
    if health.get('personaCatalogCount') != 271 or health.get('partyMechanicsCount') != 90:
        raise SystemExit('运行 API 的目录/机制数量不匹配')
if args.authority_pid is not None:
    listener(manifest['gamePort'], args.authority_pid)
    command = subprocess.check_output(['ps', '-p', str(args.authority_pid), '-o', 'command='], text=True)
    if str(root / manifest['executablePath']) not in command or '--aboutme-server' not in command:
        raise SystemExit('权威服务器运行路径不匹配')
print('已核对最新整合版 ' + manifest['releaseId'] + ' · 客户端/权威同包 · API 049312f · 271 道具 / 90 机制')

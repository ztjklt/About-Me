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
assembly=(app / 'Contents/Resources/Data/Managed/Assembly-CSharp.dll').read_bytes()
for symbol in manifest['requiredAssemblySymbols']:
    if symbol.encode() not in assembly: raise SystemExit('此前交付功能缺失：'+symbol)
if manifest['settingsSourceSnapshot'] != 'd65bd4d44b678cbda5de357195af1daeb2d04832819611eec3f6a717eaf2b07a': raise SystemExit('语言设置基线不匹配')
if len(json.loads((service/'shared/catalog.json').read_text())['items']) != 274: raise SystemExit('274目录未保留')

if manifest['clientSourceCommit'] != 'ab6ac6bd4bc2a5fff1717098765f4eaf2dce8a57':
    raise SystemExit('未核对的客户端源码版本')
if manifest['apiSourceCommit'] != '15a7f0207cdfc55c62dce77ccf08f57dd96ba1fd':
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
    if any(version.get(key) != manifest[key] for key in ('releaseId', 'apiSourceCommit', 'apiSourceSnapshot', 'serviceTreeSha256')):
        raise SystemExit('运行 API 交付标识不匹配')
    if version.get('pid') != args.api_pid or version.get('serviceRoot') != str(service):
        raise SystemExit('运行 API 进程身份不匹配')
    with urllib.request.urlopen(url + '/health', timeout=2) as response:
        health = json.load(response)
    if health.get('personaCatalogCount') != 274 or health.get('partyMechanicsCount') != manifest['mechanicCount']:
        raise SystemExit('运行 API 的目录/机制数量不匹配')
if args.authority_pid is not None:
    listener(manifest['gamePort'], args.authority_pid)
    command = subprocess.check_output(['ps', '-p', str(args.authority_pid), '-o', 'command='], text=True)
    if str(root / manifest['executablePath']) not in command or '--aboutme-server' not in command:
        raise SystemExit('权威服务器运行路径不匹配')
required={'compact-introduction-studio','questionnaire-free-answer-13','saved-avatar-persona-preview','compact-wardrobe-grid','horizontal-persona-results','free-introduction-preserved','player-settings','zh-en-language','catalog-274'}
if not required.issubset(set(manifest['requiredFeatures'])): raise SystemExit('先前交付功能门槛不完整')
print('已核对最新整合版 ' + manifest['releaseId'] + ' · 客户端/权威同包 · API R2 snapshot + 英文两品牌兼容 · 274 道具 / '+str(manifest['mechanicCount'])+' 机制')

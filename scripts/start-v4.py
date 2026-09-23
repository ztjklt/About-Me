"""Start V4 on isolated ports. Existing V3 services and builds are never stopped."""
import argparse
import os
import pathlib
import socket
import subprocess
import time

root = pathlib.Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--no-open', action='store_true')
args = parser.parse_args()
binary = root/'builds/v4-mac/About Me.app/Contents/MacOS/About Me'
if not binary.exists(): raise SystemExit('请先构建 V4 macOS 安装包。')

def occupied(port):
    with socket.socket() as sock: return sock.connect_ex(('127.0.0.1', port)) == 0

for port in (7794, 8094):
    if occupied(port): raise SystemExit(f'V4 端口 {port} 已使用；不会终止已有程序。')
env = os.environ.copy()
if (root/'server/.env').exists():
    loaded = subprocess.run(['sh','-c','set -a; . ./server/.env; env -0'],cwd=root,check=True,capture_output=True).stdout
    for entry in loaded.split(b'\0'):
        if b'=' in entry:
            key,value=entry.split(b'=',1);env[key.decode()]=value.decode()
env['ABOUTME_GAME_PORT']='7794'
env['ABOUTME_GAME_ENDPOINT']='ws://127.0.0.1:7794'
out=root/'artifacts/characters-v4/local';out.mkdir(parents=True,exist_ok=True)
processes=[];logs=[]

def start(name, command):
    log=(out/(name+'.log')).open('w');logs.append(log)
    process=subprocess.Popen(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
    processes.append(process);return process

try:
    if not occupied(8081): start('api',['sh','scripts/start-api.sh'])
    start('authority',[str(binary),'-batchmode','-nographics','--aboutme-server','--allow-samples','-logFile',str(out/'authority-engine.log')])
    if (root/'builds/v4-web/index.html').exists(): start('web',[str(root/'.venv/bin/python'),'-m','http.server','8094','--bind','127.0.0.1','--directory',str(root/'builds/v4-web')])
    for _ in range(120):
        if any(p.poll() is not None for p in processes): raise RuntimeError('V4 服务启动失败，请查看 artifacts/characters-v4/local')
        if occupied(7794): break
        time.sleep(.25)
    else: raise RuntimeError('V4 权威服务器启动超时')
    if not args.no_open: start('client',[str(binary),'-logFile',str(out/'client-engine.log')])
    print('V4 已启动。单人游戏 → 使用练习道具开始。多人连接：ws://127.0.0.1:7794',flush=True)
    print('Web：http://127.0.0.1:8094；网页多人模式请在设置中填入上述 V4 连接地址。',flush=True)
    print('旧 V3 继续运行。保留此终端；Ctrl+C 只停止本次启动的 V4 进程。',flush=True)
    while True:
        time.sleep(1)
        if any(p.poll() is not None for p in processes): break
except KeyboardInterrupt:
    pass
finally:
    for p in processes:
        if p.poll() is None: p.terminate()
    for p in processes:
        try:p.wait(timeout=5)
        except subprocess.TimeoutExpired:p.kill();p.wait()
    for log in logs:log.close()

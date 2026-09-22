"""Start the delivered local sample environment; Ctrl+C stops only owned services."""
import argparse
import json
import pathlib
import socket
import subprocess
import time
import urllib.request

root = pathlib.Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--no-open', action='store_true', help='Start services without opening game windows')
args = parser.parse_args()
binary = root / 'builds/mac/About Me.app/Contents/MacOS/About Me'
python = root / '.venv/bin/python'
if not binary.exists() or not (root / 'builds/web/index.html').exists() or not python.exists():
    raise SystemExit('缺少本地构建或 .venv，请先按 README 的构建与环境步骤准备。')
for port in (7777, 8080, 8081):
    with socket.socket() as check:
        if check.connect_ex(('127.0.0.1', port)) == 0:
            raise SystemExit(f'端口 {port} 已使用；请关闭之前的本地服务后重试。不会停止其他程序。')
out = root / 'artifacts/local'
out.mkdir(parents=True, exist_ok=True)
processes, logs = [], []


def start(name, command):
    log = (out / f'{name}.log').open('w')
    logs.append(log)
    proc = subprocess.Popen(command, cwd=root, stdout=log, stderr=subprocess.STDOUT)
    processes.append(proc)
    return proc


def ready(url):
    for _ in range(60):
        if any(p.poll() is not None for p in processes):
            raise RuntimeError(f'服务启动失败，请查看 {out}')
        try:
            with urllib.request.urlopen(url, timeout=1) as response:
                if response.status == 200:
                    return
        except OSError:
            pass
        time.sleep(.25)
    raise RuntimeError(f'服务启动超时：{url}')


try:
    start('api', ['sh', 'scripts/start-api.sh'])
    start('web', [str(python), '-m', 'http.server', '8080', '--bind', '127.0.0.1', '--directory', 'builds/web'])
    start('game', [str(binary), '-batchmode', '-nographics', '--aboutme-server', '--allow-samples', '-logFile', str(out / 'game-engine.log')])
    ready('http://127.0.0.1:8081/health')
    ready('http://127.0.0.1:8080/')
    for _ in range(60):
        with socket.socket() as check:
            if check.connect_ex(('127.0.0.1', 7777)) == 0:
                break
        if processes[-1].poll() is not None:
            raise RuntimeError('游戏服务器退出，请查看 game-engine.log')
        time.sleep(.25)
    else:
        raise RuntimeError('游戏服务器端口未就绪')
    print('本地样例环境已就绪：http://127.0.0.1:8080', flush=True)
    print('桌面和浏览器各点「样例测试」；桌面房间码留空创建，浏览器填同一码加入。', flush=True)
    print('各自 Ready 后由房主开始。无需点击「本机测试服务器」，它已经启动。', flush=True)
    with urllib.request.urlopen('http://127.0.0.1:8081/health', timeout=2) as response:
        health = json.load(response)
    ai_state = '已连接' if health.get('aiConfigured') else '尚未配置'
    asr_state = '已连接' if health.get('asrConfigured') else '尚未配置'
    print(f'真实 AI：{ai_state}；语音识别：{asr_state}。普通对局保留 5 分钟大手与 15 分钟终局。', flush=True)
    print('保留此终端；按 Ctrl+C 停止本次服务。日志：artifacts/local', flush=True)
    if not args.no_open:
        subprocess.run(['open', '-n', str(root / 'builds/mac/About Me.app')], check=True)
        subprocess.run(['open', 'http://127.0.0.1:8080'], check=True)
    while True:
        time.sleep(1)
        if any(p.poll() is not None for p in processes):
            raise RuntimeError(f'本地服务意外退出，请查看 {out}')
except KeyboardInterrupt:
    print('\n正在停止本次启动的服务。', flush=True)
finally:
    for proc in processes:
        if proc.poll() is None:
            proc.terminate()
    for proc in processes:
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()
    for log in logs:
        log.close()

"""Run the local Persona API without copying or printing provider credentials."""
import argparse
import json
import os
import shutil
import signal
import socket
import sqlite3
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
STATE = ROOT / 'artifacts/local-debug'
DATABASE = STATE / 'aboutme.sqlite3'
EMBLEMS = STATE / 'school-emblems'
LOG = STATE / 'api.log'
PID = STATE / 'api.pid'
HEALTH = 'http://127.0.0.1:8081/health'


def worktrees():
    output = subprocess.check_output(['git', 'worktree', 'list', '--porcelain'], cwd=ROOT, text=True)
    return [Path(line[9:]) for line in output.splitlines() if line.startswith('worktree ')]


def env_file(explicit):
    if explicit:
        path = Path(explicit).expanduser().resolve()
        if not path.is_file():
            raise SystemExit('指定的本机环境配置不存在')
        return path
    candidates = [root / 'server/.env' for root in worktrees() if (root / 'server/.env').is_file()]
    if len(candidates) != 1:
        raise SystemExit('需要用 --env-file 指定唯一的本机 server/.env')
    return candidates[0]


def runtime(root):
    candidates = [ROOT / '.venv/bin/python', root / '.venv/bin/python']
    for path in candidates:
        if path.is_file():
            return path
    raise SystemExit('没有找到含 API 依赖的 .venv/bin/python')


def configuration(path):
    env = os.environ.copy()
    for line in path.read_text().splitlines():
        key, separator, value = line.partition('=')
        key = key.strip().removeprefix('export ')
        if separator and key and not key.startswith('#'):
            env[key] = value.strip().strip('"\'')
    if any(not env.get(key) for key in ('LLM_API_KEY', 'LLM_MODEL', 'LLM_BASE_URL')):
        raise SystemExit('本机真实 AI 配置不完整')
    env['ABOUTME_DB'] = str(DATABASE)
    return env


def health():
    try:
        with urllib.request.urlopen(HEALTH, timeout=2) as response:
            body = json.load(response)
        return body if body.get('ok') else None
    except (OSError, ValueError, urllib.error.HTTPError):
        return None


def port_open():
    with socket.socket() as sock:
        sock.settimeout(1)
        return sock.connect_ex(('127.0.0.1', 8081)) == 0


def owned_pid():
    if not PID.is_file():
        return None
    try:
        pid = int(PID.read_text().strip())
        command = subprocess.check_output(['ps', '-p', str(pid), '-o', 'command='], text=True).strip()
    except (ValueError, subprocess.CalledProcessError):
        return None
    return pid if 'server.app:app' in command and '--port 8081' in command else None


def start(explicit):
    if health():
        if owned_pid() is None:
            raise SystemExit('8081 已有其他 API 在运行；请先由维护者切换到本分支服务')
        print('本机 API 已在 127.0.0.1:8081 运行')
        return
    if port_open():
        raise SystemExit('8081 已由其他服务占用')
    source = env_file(explicit)
    python = runtime(source.parent.parent)
    env = configuration(source)
    env['SCHOOL_EMBLEM_DIR'] = str(EMBLEMS)
    STATE.mkdir(parents=True, exist_ok=True)
    if not DATABASE.exists():
        existing = source.parent.parent / 'artifacts/aboutme.sqlite3'
        if existing.is_file():
            with sqlite3.connect(existing) as old, sqlite3.connect(DATABASE) as copied:
                old.backup(copied)
    original_emblems = source.parent.parent / 'artifacts/school-emblems'
    if not EMBLEMS.exists() and original_emblems.is_dir():
        shutil.copytree(original_emblems, EMBLEMS)
    EMBLEMS.mkdir(parents=True, exist_ok=True)
    with LOG.open('ab') as log:
        process = subprocess.Popen([str(python), '-m', 'uvicorn', 'server.app:app',
            '--host', '127.0.0.1', '--port', '8081', '--no-proxy-headers'],
            cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    PID.write_text(str(process.pid))
    for _ in range(40):
        if process.poll() is not None:
            break
        if health():
            print('本机 API 已就绪：http://127.0.0.1:8081/health')
            return
        time.sleep(.25)
    raise SystemExit(f'API 未就绪，请查看 {LOG}')


def status():
    body = health()
    print('本机 API：' + ('本分支运行中' if body and owned_pid() else
                      '其他进程在运行' if body else '未运行'))
    if body:
        print('真实 AI：' + ('已配置' if body.get('aiConfigured') else '未配置'))
        print('模型：' + str(body.get('aiModel', '')))


def stop():
    pid = owned_pid()
    if pid is None:
        raise SystemExit('没有找到由此启动器管理的 API 进程')
    os.kill(pid, signal.SIGTERM)
    PID.unlink(missing_ok=True)
    print('本机 API 已停止')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=('start', 'status', 'stop'))
    parser.add_argument('--env-file', help='未提交的 server/.env；省略时从 Git worktree 中自动查找')
    args = parser.parse_args()
    if args.action == 'start':
        start(args.env_file)
    elif args.action == 'status':
        status()
    else:
        stop()


if __name__ == '__main__':
    main()

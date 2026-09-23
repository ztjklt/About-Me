"""Local real-socket check: an abandoned started room releases its capacity."""
import pathlib
import subprocess
import time
import os


root = pathlib.Path(__file__).resolve().parent.parent
out = root / 'artifacts' / 'room-cleanup'
out.mkdir(parents=True, exist_ok=True)
binary = root / 'builds/mac/About Me.app/Contents/MacOS/About Me'
port = '7799'
environment = os.environ.copy()
environment['ABOUTME_GAME_PORT'] = port
processes = []


def start(name, *args):
    log = out / (name + '.log')
    log.unlink(missing_ok=True)
    process = subprocess.Popen([str(binary), '-batchmode', '-nographics', '-logFile', str(log),
                                '--endpoint', 'ws://127.0.0.1:' + port, *args], env=environment,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    processes.append(process)
    return process, log


def wait_for(predicate, seconds, message):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        result = predicate()
        if result:
            return result
        time.sleep(.2)
    raise RuntimeError(message)


try:
    server, server_log = start('server', '--aboutme-server', '--allow-samples', '--accelerated')
    wait_for(lambda: server_log.exists() and 'ABOUTME_SERVER_READY' in server_log.read_text(errors='replace'),
             15, 'Server did not start')
    marker = out / 'room.txt'
    marker.unlink(missing_ok=True)
    host, host_log = start('host', '--bot', '--create-room', '--marker', str(marker),
                           '--identity', 'cleanup-host', '--players', '2')
    code = wait_for(lambda: marker.read_text().strip() if marker.exists() else '', 15, 'Host did not create a room')
    guest, _ = start('guest', '--bot', '--marker', str(marker), '--identity', 'cleanup-guest')
    wait_for(lambda: host_log.exists() and 'PROBE_STATE Battle' in host_log.read_text(errors='replace'),
             15, 'Room did not enter battle')
    for client in (host, guest):
        client.terminate()
        client.wait(timeout=5)
    expired = 'ABOUTME_ROOM_EXPIRED room=' + code + ' remaining=0'
    wait_for(lambda: server_log.exists() and expired in server_log.read_text(errors='replace'),
             10, 'Abandoned room did not release capacity')

    second = out / 'new-room.txt'
    second.unlink(missing_ok=True)
    new_host, _ = start('new-host', '--bot', '--create-room', '--marker', str(second),
                        '--identity', 'cleanup-next', '--players', '2')
    new_code = wait_for(lambda: second.read_text().strip() if second.exists() else '',
                        15, 'Could not create a new room after cleanup')
    print(f'PASS: started room {code} expired after all clients disconnected; '
          f'capacity returned and new room {new_code} was created over local WebSocket.')
finally:
    for process in processes:
        if process.poll() is None:
            process.terminate()
    for process in processes:
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()

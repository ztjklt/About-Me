"""Real Mirror socket smoke test with one server and N standalone test clients."""
import argparse
import pathlib
import subprocess
import time
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--players', type=int, default=2, choices=[2,4,6])
parser.add_argument('--binary', default='builds/mac/About Me.app/Contents/MacOS/About Me')
args = parser.parse_args()
root=pathlib.Path(__file__).resolve().parent.parent
out=root/'artifacts'/f'network-{args.players}'
out.mkdir(parents=True,exist_ok=True)
marker=out/'room.txt'
marker.unlink(missing_ok=True)
binary=str((root/args.binary).resolve())
processes=[]

def start(name, extra):
    log=out/(name+'.log')
    p=subprocess.Popen([binary,'-batchmode','-nographics','-logFile',str(log),*extra],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    processes.append(p)
    return p, log

try:
    server,_=start('server',['--aboutme-server','--allow-samples','--accelerated'])
    time.sleep(3)
    clients=[start('host',['--bot','--create-room','--marker',str(marker),'--identity','host','--players',str(args.players)])]
    deadline=time.monotonic()+20
    while not marker.exists() and time.monotonic()<deadline: time.sleep(.25)
    if not marker.exists(): raise RuntimeError('Host did not create room; inspect artifacts logs')
    for i in range(1,args.players):
        clients.append(start(f'client-{i}',['--bot','--marker',str(marker),'--identity',f'client{i}']))
    for proc,log in clients:
        code=proc.wait(timeout=105)
        output=log.read_text(errors='replace')
        if code or 'PROBE_PASS' not in output: raise RuntimeError(f'Network probe failed: {log}')
        print(next(line for line in output.splitlines() if 'PROBE_PASS' in line))
    print(f'PASS: {args.players} actual client processes over local WebSocket; NOT an internet or phone test.')
finally:
    for p in processes:
        if p.poll() is None:p.terminate()
    for p in processes:
        try:p.wait(timeout=5)
        except subprocess.TimeoutExpired:p.kill()

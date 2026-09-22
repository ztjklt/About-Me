#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$task_root"
if [ -f server/.env ]; then
  set -a
  . ./server/.env
  set +a
fi
exec .venv/bin/uvicorn server.app:app --host 127.0.0.1 --port 8081

#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
task_manifest="$task_root/builds/playtest-ui-r1-mac/release-manifest.json"
task_python="$task_root/.venv/bin/python"
task_service="$task_root/builds/playtest-ui-r1-service"
task_app="$task_root/builds/playtest-ui-r1-mac/About Me.app/Contents/MacOS/About Me Integration QA"
task_verify="$task_root/scripts/verify-playtest-ui-r1.py"
if [ ! -x "$task_python" ]; then
  echo '缺少现有本机 API Python 环境；请按 README 准备环境。'
  exit 1
fi
"$task_python" "$task_verify" "$task_manifest"
if [ "${1:-}" = '--verify-only' ]; then exit 0; fi
for task_port in 8086 7786; do
  if lsof -nP -iTCP:"$task_port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "新版独立端口 $task_port 已占用；拒绝复用未核对服务，请关闭对应新版窗口后重试。"
    exit 1
  fi
done
cd "$task_root"
mkdir -p artifacts/playtest-ui-r1
if [ -f server/.env ]; then
  set -a
  . ./server/.env
  set +a
fi
export ABOUTME_RELEASE_MANIFEST="$task_manifest"
export ABOUTME_API_URL=http://127.0.0.1:8086
export ABOUTME_GAME_ENDPOINT=ws://127.0.0.1:7786
export ABOUTME_GAME_PORT=7786
export ABOUTME_DB="$task_root/artifacts/playtest-ui-r1/profiles.sqlite3"
export UNIVERSITY_CREST_CACHE="$task_root/artifacts/playtest-ui-r1/university-crests"
export GAME_SERVER_KEY="${GAME_SERVER_KEY:-aboutme-local-playtest-ui-r1}"
task_api_pid=
task_authority_pid=
cleanup() {
  if [ -n "$task_authority_pid" ]; then kill "$task_authority_pid" 2>/dev/null || true; fi
  if [ -n "$task_api_pid" ]; then kill "$task_api_pid" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM
"$task_root/.venv/bin/uvicorn" server.release_entry:app --app-dir "$task_service" --host 127.0.0.1 --port 8086 > "$task_root/artifacts/playtest-ui-r1/api.log" 2>&1 &
task_api_pid=$!
task_ready=false
for task_try in 1 2 3 4 5 6 7 8 9 10; do
  if curl --silent --fail --max-time 1 "$ABOUTME_API_URL/local-release-version" >/dev/null; then task_ready=true; break; fi
  if ! kill -0 "$task_api_pid" 2>/dev/null; then break; fi
  sleep 1
done
if [ "$task_ready" != true ]; then echo '新版 API 启动失败：artifacts/playtest-ui-r1/api.log'; exit 1; fi
"$task_python" "$task_verify" "$task_manifest" --api-pid "$task_api_pid"
"$task_app" -batchmode -nographics --aboutme-server --allow-samples -logFile "$task_root/artifacts/playtest-ui-r1/authority.log" > "$task_root/artifacts/playtest-ui-r1/authority-console.log" 2>&1 &
task_authority_pid=$!
task_ready=false
for task_try in 1 2 3 4 5 6 7 8 9 10; do
  if lsof -nP -iTCP:7786 -sTCP:LISTEN >/dev/null 2>&1; then task_ready=true; break; fi
  if ! kill -0 "$task_authority_pid" 2>/dev/null; then break; fi
  sleep 1
done
if [ "$task_ready" != true ]; then echo '新版权威服务启动失败：artifacts/playtest-ui-r1/authority.log'; exit 1; fi
"$task_python" "$task_verify" "$task_manifest" --api-pid "$task_api_pid" --authority-pid "$task_authority_pid"
echo '启动 playtest-ui-r1：结果横向滚动 + 可选选择题模式；自由输入保留。'
"$task_app" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "$task_root/artifacts/playtest-ui-r1/client.log" "$@"

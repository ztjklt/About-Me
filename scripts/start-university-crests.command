#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$task_root"
task_service_root="$task_root/builds/university-crests-service"
if [ ! -f "$task_service_root/server/university_crests.py" ]; then
  echo '新版校徽服务包缺失，请重新构建交付包。'
  exit 1
fi
task_app="$task_root/builds/university-crests-mac/About Me.app/Contents/MacOS/About Me"
if [ ! -x "$task_app" ]; then
  echo '校徽版游戏尚未构建，请运行 docs/UNIVERSITY_CREST_DELIVERY.md 中的构建入口。'
  exit 1
fi
mkdir -p artifacts
if [ -f server/.env ]; then
  set -a
  . ./server/.env
  set +a
fi
export UNIVERSITY_CREST_CACHE="$task_root/artifacts/university-crests"
export ABOUTME_API_URL=http://127.0.0.1:8085
export ABOUTME_DB="$task_root/artifacts/university-crests-profiles.sqlite3"
export GAME_SERVER_KEY="${GAME_SERVER_KEY:-aboutme-local-crest-server}"
task_api_pid=
task_game_server_pid=
cleanup() {
  if [ -n "$task_game_server_pid" ]; then
    kill "$task_game_server_pid" 2>/dev/null || true
    wait "$task_game_server_pid" 2>/dev/null || true
  fi
  if [ -n "$task_api_pid" ]; then
    kill "$task_api_pid" 2>/dev/null || true
    wait "$task_api_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT INT TERM
if curl --silent --fail --max-time 2 "$ABOUTME_API_URL/health" | .venv/bin/python -c 'import json,sys; h=json.load(sys.stdin); sys.exit(0 if h.get("latestUI") == "four-bubbles-e5470e2" and h.get("personaKeywordResults") and h.get("webUniversitySearch") and h.get("universityProgress") and h.get("domesticPriority") and h.get("personaCatalogCount",0) >= 267 and h.get("partyMechanicsCount",0) >= 80 and h.get("personalitySkins") and h.get("introductionImageRecognition") is False else 1)' 2>/dev/null; then
  echo '使用已启动的校徽 API。'
else
  if lsof -nP -iTCP:8085 -sTCP:LISTEN >/dev/null 2>&1; then
    echo '8085 端口被其他服务占用，请关闭该服务后重试。'
    exit 1
  fi
  PYTHONPATH="$task_service_root" .venv/bin/uvicorn server.app:app --app-dir "$task_service_root" --host 127.0.0.1 --port 8085 > artifacts/university-crests-api.log 2>&1 &
  task_api_pid=$!
  task_ready=false
  for task_try in 1 2 3 4 5 6 7 8 9 10; do
    if curl --silent --fail --max-time 1 "$ABOUTME_API_URL/health" >/dev/null; then
      task_ready=true
      break
    fi
    sleep 1
  done
  if [ "$task_ready" != true ]; then
    echo '校徽 API 启动失败，详情见 artifacts/university-crests-api.log。'
    exit 1
  fi
fi
export ABOUTME_GAME_ENDPOINT=ws://127.0.0.1:7785
if ! lsof -nP -iTCP:7785 -sTCP:LISTEN >/dev/null 2>&1; then
  ABOUTME_GAME_PORT=7785 "$task_app" -batchmode -nographics --aboutme-server --allow-samples -logFile "$task_root/artifacts/university-crests-server.log" > "$task_root/artifacts/university-crests-server-console.log" 2>&1 &
  task_game_server_pid=$!
  task_server_ready=false
  for task_try in 1 2 3 4 5 6 7 8 9 10; do
    if lsof -nP -iTCP:7785 -sTCP:LISTEN >/dev/null 2>&1; then
      task_server_ready=true
      break
    fi
    sleep 1
  done
  if [ "$task_server_ready" != true ]; then
    echo '本地校徽版联机服务启动失败，详情见 artifacts/university-crests-server.log。'
    exit 1
  fi
fi
"$task_app" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "$task_root/artifacts/university-crests-game.log" "$@"

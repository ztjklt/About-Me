#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
task_app="$task_root/builds/ds-local-current-qa/About Me Integration QA.app"
task_binary="$task_app/Contents/MacOS/About Me Integration QA"

if [ ! -x "$task_binary" ]; then
    echo "未找到当前 DS 娘窗口版游戏：$task_app"
    echo "请先在本分支构建 macOS QA 包。"
    exit 1
fi

python3 "$task_root/scripts/local-ai-api.py" start
export ABOUTME_API_URL=http://127.0.0.1:8081
exec "$task_binary" -screen-fullscreen 0 -screen-width 1280 -screen-height 720

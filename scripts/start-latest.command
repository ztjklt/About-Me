#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
task_launcher="$task_root/scripts/start-playtest-ui-r1.command"
if [ ! -x "$task_launcher" ]; then
  echo '最新整合试玩入口缺失；禁止回退旧版。'
  exit 1
fi
exec "$task_launcher" "$@"

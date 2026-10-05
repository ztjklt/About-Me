#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
task_launcher="$task_root/scripts/start-playtest-persona-r11.command"
if [ ! -x "$task_launcher" ]; then echo '最新完整整合包入口缺失；禁止回退专项包。'; exit 1; fi
exec "$task_launcher" "$@"

#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
exec "$task_root/scripts/start-university-crests.command" --ui-preview trait-lab "$@"

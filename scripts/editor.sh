#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
task_editor=${TUANJIE_EDITOR:-/Applications/Tuanjie/Hub/Editor/2022.3.62t11/Tuanjie.app/Contents/MacOS/Tuanjie}
task_method=${1:-Generate}
mkdir -p "$task_root/artifacts"
"$task_editor" -batchmode -nographics -quit -projectPath "$task_root/client" -executeMethod "AboutMe.Editor.ProjectTools.$task_method" -logFile "$task_root/artifacts/$task_method.log"

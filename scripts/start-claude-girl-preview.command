#!/bin/zsh
set -eu
task_root="$(cd "$(dirname "$0")/.." && pwd)"
preview_out="$task_root/artifacts/claude-girl-modular/manual-preview"
mkdir -p "$preview_out"
open -n "$task_root/builds/claude-girl-modular-mac/About Me.app" --args --body-physics-probe --probe-claude-all --probe-capture --probe-output "$preview_out" -screen-fullscreen 0 -logFile "$preview_out/player.log"

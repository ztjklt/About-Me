#!/bin/sh
set -eu
export ABOUTME_GAME_PORT="${PORT:-7777}"
if [ "${ABOUTME_TEST_SAMPLES:-0}" = "1" ]; then
    exec /game/AboutMeServer -batchmode -nographics --aboutme-server --allow-samples -logFile /dev/stdout
fi
exec /game/AboutMeServer -batchmode -nographics --aboutme-server -logFile /dev/stdout

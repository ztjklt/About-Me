#!/bin/sh
set -eu
echo "ABOUTME_RELEASE ${ABOUTME_RELEASE_ID:-unversioned} source=${ABOUTME_RELEASE_SHA:-unknown} protocol=5"
export ABOUTME_GAME_PORT="${PORT:-7777}"
if [ "${ABOUTME_TEST_SAMPLES:-0}" = "1" ]; then
    exec /game/AboutMeServer -batchmode -nographics --aboutme-server --allow-samples -logFile /dev/stdout
fi
exec /game/AboutMeServer -batchmode -nographics --aboutme-server -logFile /dev/stdout

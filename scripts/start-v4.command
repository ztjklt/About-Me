#!/bin/zsh
cd "${0:A:h:h}"
exec ./.venv/bin/python scripts/start-v4.py

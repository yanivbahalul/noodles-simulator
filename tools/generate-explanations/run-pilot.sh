#!/usr/bin/env bash
# Pilot: first 10 explanation videos. Listen to all before full batch.
set -euo pipefail
cd "$(dirname "$0")/../.."
# shellcheck disable=SC1091
source tools/generate-explanations/env-defaults.sh
pip3 install -q -r tools/generate-explanations/requirements.txt
echo "Vision: $GEMINI_VISION_MODEL | TTS: $TTS_PROVIDER"
python3 tools/generate-explanations/generate.py --limit 10 "$@"

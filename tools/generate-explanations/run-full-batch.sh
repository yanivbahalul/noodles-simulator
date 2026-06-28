#!/usr/bin/env bash
# Full batch: all question groups missing a ready explanation (~327).
set -euo pipefail
cd "$(dirname "$0")/../.."
# shellcheck disable=SC1091
source tools/generate-explanations/env-defaults.sh
pip3 install -q -r tools/generate-explanations/requirements.txt
tools/generate-explanations/download-phonikud-model.sh
# ponytail: force Edge — .env TTS_PROVIDER=gemini hits rate limits in batch
export TTS_PROVIDER=edge
export TTS_VOICE="${TTS_VOICE:-he-IL-HilaNeural}"
export TTS_NIKUD="${TTS_NIKUD:-1}"
export GEMINI_POLISH="${GEMINI_POLISH:-0}"
echo "Vision: $GEMINI_VISION_MODEL | TTS: $TTS_PROVIDER | Nikud: $TTS_NIKUD | Polish: $GEMINI_POLISH"
python3 tools/generate-explanations/generate.py --only-missing "$@"

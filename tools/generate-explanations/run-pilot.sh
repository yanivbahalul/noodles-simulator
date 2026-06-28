#!/usr/bin/env bash
# Pilot: generate explanation videos for the first 10 questions.
# Requires GEMINI_API_KEY (or GOOGLE_API_KEY) in repo-root .env
set -euo pipefail
cd "$(dirname "$0")/../.."
pip3 install -q -r tools/generate-explanations/requirements.txt
python3 tools/generate-explanations/generate.py --limit 10 "$@"

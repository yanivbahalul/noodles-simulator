#!/usr/bin/env bash
# Full batch: generate missing explanation videos for all questions (~327 groups).
# Run overnight; requires GEMINI_API_KEY in repo-root .env
set -euo pipefail
cd "$(dirname "$0")/../.."
pip3 install -q -r tools/generate-explanations/requirements.txt
python3 tools/generate-explanations/generate.py --only-missing "$@"

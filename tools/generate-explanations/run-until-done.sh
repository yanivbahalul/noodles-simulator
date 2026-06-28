#!/usr/bin/env bash
# Full batch with auto-resume after Gemini daily quota reset (midnight PT + 5 min).
# Launch: nohup bash tools/generate-explanations/run-until-done.sh >> tools/generate-explanations/batch.log 2>&1 </dev/null &
set -euo pipefail
cd "$(dirname "$0")/../.."
export HF_HUB_DISABLE_WARNINGS=1

seconds_until_gemini_reset() {
  python3 - <<'PY'
import datetime
from zoneinfo import ZoneInfo

now = datetime.datetime.now(ZoneInfo("America/Los_Angeles"))
if now.hour == 0 and now.minute < 5:
    print(60)
else:
    nxt = (now + datetime.timedelta(days=1)).replace(hour=0, minute=5, second=0, microsecond=0)
    print(max(60, int((nxt - now).total_seconds())))
PY
}

sleep_until_gemini_reset() {
  echo "Gemini daily quota exhausted — waiting for reset (~00:05 PT)..."
  while true; do
    wait="$(seconds_until_gemini_reset)"
    if (( wait <= 60 )); then
      echo "Gemini quota — resuming in ${wait}s"
      sleep "$wait"
      break
    fi
    hrs=$((wait / 3600))
    mins=$(((wait % 3600) / 60))
    echo "Gemini quota — resuming in ${hrs}h ${mins}m"
    sleep 60
  done
  echo "Gemini quota reset — resuming batch..."
}

while true; do
  set +e
  tools/generate-explanations/run-full-batch.sh "$@"
  code=$?
  set -e

  if [[ "$code" -eq 0 ]]; then
    echo "Batch complete — nothing left to generate."
    exit 0
  fi
  if [[ "$code" -eq 3 ]]; then
    sleep_until_gemini_reset
    continue
  fi
  echo "Batch exited with code $code (not quota). Fix errors and re-run."
  exit "$code"
done

#!/usr/bin/env bash
# Load test script for Noodles Simulator (uses Apache Bench)
set -euo pipefail

BASE="${BASE_URL:-http://localhost:5001}"
AB="${AB:-/usr/sbin/ab}"
REQUESTS="${REQUESTS:-100}"
CONCURRENCY="${CONCURRENCY:-20}"

if [[ ! -x "$AB" ]]; then
  echo "Apache Bench (ab) not found at $AB"
  exit 1
fi

run_test() {
  local name="$1"
  local url="$2"
  local n="${3:-$REQUESTS}"
  local c="${4:-$CONCURRENCY}"

  echo ""
  echo "============================================================"
  echo "  $name"
  echo "  URL: $url"
  echo "  Requests: $n  |  Concurrency: $c"
  echo "============================================================"

  local tmp
  tmp=$(mktemp)
  if "$AB" -n "$n" -c "$c" -k -q "$url" > "$tmp" 2>&1; then
    grep -E "^(Server Software|Document Path|Concurrency Level|Complete requests|Failed requests|Non-2xx responses|Total transferred|HTML transferred|Requests per second|Time per request|Transfer rate|Connection Times|Percentage of the requests)" "$tmp" || true
    echo "---"
    grep -A6 "Connection Times" "$tmp" | tail -5 || true
    echo "---"
    grep -A6 "Percentage of the requests" "$tmp" | tail -5 || true
  else
    echo "AB FAILED:"
    cat "$tmp"
  fi
  rm -f "$tmp"
}

echo "Noodles Simulator Load Test"
echo "Base URL: $BASE"
echo "Date: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo ""
echo "Note: Global rate limit is 120 req/min per IP — large tests will show 429s."

# Warmup
curl -s -o /dev/null "$BASE/health"

run_test "Health (lightweight JSON)" "$BASE/health" 200 25
sleep 2
run_test "Login page (Razor, static-ish)" "$BASE/Login" 150 25
sleep 2
run_test "Static CSS" "$BASE/css/site.css" 300 50
sleep 2
run_test "Leaderboard API (Supabase)" "$BASE/api/leaderboard-data?tab=total" 80 10
sleep 2
run_test "Online count API (Supabase)" "$BASE/api/online-count" 80 10
sleep 2
run_test "Leaderboard page (Razor + API embed)" "$BASE/Leaderboard" 60 10

echo ""
echo "============================================================"
echo "  Rate limit stress (expect 429 after ~120 in 1 min window)"
echo "============================================================"
run_test "Health burst (rate limit probe)" "$BASE/health" 200 50

echo ""
echo "Done."

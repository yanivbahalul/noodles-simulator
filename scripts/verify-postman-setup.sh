#!/usr/bin/env bash
# Verify Postman API key and MCP reachability for Cursor integration.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="$ROOT/.env"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source <(grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$ENV_FILE" | sed 's/^/export /')
  set +a
fi

echo "=== Postman setup verification ==="

if [[ -z "${POSTMAN_API_KEY:-}" ]]; then
  echo "❌ POSTMAN_API_KEY is not set."
  echo ""
  echo "1. Open https://postman.postman.co/settings/me/api-keys"
  echo "2. Generate API Key (name: Cursor Plugin)"
  echo "3. Add to $ENV_FILE:"
  echo "   POSTMAN_API_KEY=PMAK-your-key-here"
  echo "4. Add the same line to ~/.zshrc (Cursor needs it at launch):"
  echo "   export POSTMAN_API_KEY=PMAK-your-key-here"
  echo "5. Restart Cursor completely (Cmd+Q)"
  exit 1
fi

if [[ "$POSTMAN_API_KEY" != PMAK-* ]]; then
  echo "⚠️  POSTMAN_API_KEY does not start with PMAK- (unexpected format)"
fi

HTTP_CODE="$(curl -s -o /tmp/postman-me.json -w "%{http_code}" \
  -H "X-Api-Key: $POSTMAN_API_KEY" \
  "https://api.getpostman.com/me")"

if [[ "$HTTP_CODE" != "200" ]]; then
  echo "❌ Postman API key rejected (HTTP $HTTP_CODE)"
  cat /tmp/postman-me.json 2>/dev/null || true
  exit 1
fi

USER_NAME="$(python3 -c "import json; print(json.load(open('/tmp/postman-me.json'))['user']['username'])" 2>/dev/null || echo "unknown")"
echo "✅ Postman API key valid (user: $USER_NAME)"

MCP_CODE="$(curl -s -o /dev/null -w "%{http_code}" "https://mcp.postman.com/mcp")"
if [[ "$MCP_CODE" == "405" || "$MCP_CODE" == "401" ]]; then
  echo "✅ Postman MCP server reachable (HTTP $MCP_CODE)"
else
  echo "⚠️  Postman MCP returned HTTP $MCP_CODE (expected 401 or 405)"
fi

if [[ -f "$ROOT/.mcp.json" ]]; then
  echo "✅ Project .mcp.json present"
else
  echo "❌ Missing $ROOT/.mcp.json"
  exit 1
fi

if [[ -f "$ROOT/postman/Noodles-Simulator.postman_collection.json" ]]; then
  echo "✅ Postman collection ready to import"
else
  echo "❌ Missing Postman collection file"
  exit 1
fi

echo ""
echo "Next steps:"
echo "  1. Import postman/Noodles-Simulator.postman_collection.json into Postman"
echo "  2. Import postman/Noodles-Simulator.postman_environment.json"
echo "  3. Fill supabase_service_key / supabase_anon_key in the environment"
echo "  4. Restart Cursor → Settings → MCP → enable Postman"

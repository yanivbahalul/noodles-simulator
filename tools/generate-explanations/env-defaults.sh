#!/usr/bin/env bash
# Source from run-*.sh — loads repo .env and sets recommended TTS/vision defaults.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [[ -f "$ROOT/.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$ROOT/.env"
  set +a
fi

export GEMINI_VISION_MODEL="${GEMINI_VISION_MODEL:-gemini-2.0-flash-lite}"
export GEMINI_POLISH="${GEMINI_POLISH:-0}"
export TTS_RATE="${TTS_RATE:--4%}"
export TTS_SPEAKING_RATE="${TTS_SPEAKING_RATE:-0.90}"

if [[ -z "${TTS_PROVIDER:-}" ]]; then
  if [[ -n "${GOOGLE_CLOUD_TTS_API_KEY:-}" ]]; then
    export TTS_PROVIDER=google
    export GOOGLE_TTS_VOICE="${GOOGLE_TTS_VOICE:-he-IL-Wavenet-B}"
  else
    export TTS_PROVIDER=edge
    export TTS_VOICE="${TTS_VOICE:-he-IL-HilaNeural}"
  fi
fi

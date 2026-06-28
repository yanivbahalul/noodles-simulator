#!/usr/bin/env bash
# One-time: download Phonikud ONNX model for Hebrew nikud before TTS.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
MODEL="$DIR/models/phonikud-1.0.int8.onnx"
URL="https://huggingface.co/Phonikud/phonikud-onnx/resolve/main/phonikud-1.0.int8.onnx"
if [[ -f "$MODEL" ]]; then
  echo "phonikud model already present: $MODEL"
  exit 0
fi
mkdir -p "$DIR/models"
echo "Downloading phonikud model (~294MB)..."
curl -L -o "$MODEL" "$URL"
echo "Saved: $MODEL"

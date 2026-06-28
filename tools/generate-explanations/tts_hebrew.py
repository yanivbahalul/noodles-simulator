"""Hebrew TTS helpers — edge (free), Google Cloud, or ElevenLabs via TTS_PROVIDER."""

from __future__ import annotations

import asyncio
import base64
import os
import re
import subprocess
import tempfile
import wave
from pathlib import Path

import edge_tts
import requests

# ponytail: regex replacements, not a full Hebrew NLP pipeline
_PHONETIC = (
    (r"int\s*&", "אינט-רפרנס"),
    (r"\bint\b", "אינט"),
    (r"\bvoid\b", "וויד"),
    (r"\bmain\b", "מיין"),
    (r"\bstatic\b", "סטטיק"),
    (r"\bconst\b", "קונסט"),
    (r"\bclass\b", "קלאס"),
    (r"\breturn\b", "ריטורן"),
    (r"\breference\b", "רפרנס"),
    (r"אמפרסand", "אנד"),
    (r"&", " וגם "),
    (r"f\(\)", "אף"),
    (r"\bf\b", "אף"),
    (r"\bx\b", "איקס"),
    (r"שווה(?!\s*ל)", "שווה ל"),
    (r"להשמיך", "להשים"),
)


def prepare_hebrew_narration(text: str) -> str:
    """Make mixed Hebrew/English code text easier for TTS to read aloud."""
    out = (text or "").strip()
    for pattern, repl in _PHONETIC:
        out = re.sub(pattern, repl, out, flags=re.IGNORECASE)
    out = re.sub(r"\s+", " ", out)
    return out.strip()


def tts_config() -> dict[str, str]:
    return {
        "provider": os.environ.get("TTS_PROVIDER", "edge").strip().lower(),
        "voice": os.environ.get("TTS_VOICE", "he-IL-HilaNeural").strip(),
        "rate": os.environ.get("TTS_RATE", "-8%").strip(),
        "gemini_key": os.environ.get("GEMINI_API_KEY", os.environ.get("GOOGLE_API_KEY", "")).strip(),
        "gemini_model": os.environ.get("GEMINI_TTS_MODEL", "gemini-2.5-flash-preview-tts").strip(),
        "gemini_voice": os.environ.get("GEMINI_TTS_VOICE", "Kore").strip(),
        "google_key": os.environ.get("GOOGLE_CLOUD_TTS_API_KEY", "").strip(),
        "google_voice": os.environ.get("GOOGLE_TTS_VOICE", "he-IL-Wavenet-B").strip(),
        "elevenlabs_key": os.environ.get("ELEVENLABS_API_KEY", "").strip(),
        "elevenlabs_voice": os.environ.get("ELEVENLABS_VOICE_ID", "pNInz6obpgDQGcFmaJgB").strip(),
    }


def _elevenlabs_tts(text: str, out_path: Path, cfg: dict[str, str]) -> None:
    key = cfg["elevenlabs_key"]
    if not key:
        raise RuntimeError("ELEVENLABS_API_KEY required for TTS_PROVIDER=elevenlabs")
    voice = cfg["elevenlabs_voice"]
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice}"
    resp = requests.post(
        url,
        headers={"xi-api-key": key, "Content-Type": "application/json", "Accept": "audio/mpeg"},
        json={"text": text, "model_id": "eleven_multilingual_v2"},
        timeout=120,
    )
    resp.raise_for_status()
    out_path.write_bytes(resp.content)


async def _edge_tts(text: str, out_path: Path, cfg: dict[str, str]) -> None:
    rate = cfg["rate"] or "-8%"
    communicate = edge_tts.Communicate(text, cfg["voice"], rate=rate)
    await communicate.save(str(out_path))


async def synthesize_mp3(text: str, out_path: Path) -> None:
    prepared = prepare_hebrew_narration(text)
    cfg = tts_config()
    provider = cfg["provider"]

    if provider == "google":
        _google_tts(prepared, out_path, cfg)
    elif provider == "elevenlabs":
        _elevenlabs_tts(prepared, out_path, cfg)
    else:
        await _edge_tts(prepared, out_path, cfg)


if __name__ == "__main__":
    assert prepare_hebrew_narration("int& reference") == "אינט-רפרנס רפרנס"
    assert "אינט" in prepare_hebrew_narration("int רגיל")
    print("[ponytail] tts_hebrew self-check passed")

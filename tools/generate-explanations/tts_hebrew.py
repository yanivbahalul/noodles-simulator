"""Hebrew TTS helpers — edge (free), Google Cloud, Gemini, or ElevenLabs via TTS_PROVIDER."""

from __future__ import annotations

import asyncio
import base64
import json
import os
import re
import shutil
import subprocess
import tempfile
import time
import wave
from pathlib import Path
from typing import Any

import edge_tts
import requests

SPOKEN_MAP_PATH = Path(__file__).with_name("hebrew-spoken-map.json")


def _load_spoken_map() -> dict[str, Any]:
    if SPOKEN_MAP_PATH.is_file():
        return json.loads(SPOKEN_MAP_PATH.read_text(encoding="utf-8"))
    return {}


def _spoken_rules() -> tuple[
    list[tuple[str, str]],
    list[tuple[str, str]],
    list[str],
    dict[str, str],
    list[tuple[str, str]],
    list[tuple[str, str]],
]:
    m = _load_spoken_map()
    formal = sorted(m.get("formal_to_spoken") or [], key=lambda p: len(p[0]), reverse=True)
    formal_re = [(p, r) for p, r in (m.get("formal_regex") or [])]
    strip = list(m.get("strip_phrases") or [])
    codes = dict(m.get("code_labels") or {})
    english = sorted(m.get("english_loanwords") or [], key=lambda p: len(p[0]), reverse=True)
    regex = [(p, r) for p, r in (m.get("regex_rules") or [])]
    regex.append((r"שווה(?!\s*ל)", "שווה ל"))
    return formal, formal_re, strip, codes, english, regex


_FORMAL_TO_SPOKEN, _FORMAL_REGEX, _STRIP_PHRASES, _CODE_TTS, _ENGLISH_LOANWORDS, _PHONETIC = _spoken_rules()

# Fallback if JSON missing
if not _CODE_TTS:
    _CODE_TTS = {
        "int&": "int reference",
        "int*": "int pointer",
        "int": "int",
        "void": "void",
        "T": "T",
    }

# Latin/code tokens — nikud skips non-Hebrew runs automatically

_ANSWER_NUM_HE = ("", "אחת", "שתיים", "שלוש", "ארבע")

GLOSSARY_PATH = Path(__file__).with_name("tts-glossary.json")
PHONIKUD_MODEL_PATH = Path(__file__).parent / "models" / "phonikud-1.0.int8.onnx"
PHONIKUD_MODEL_URL = "https://huggingface.co/Phonikud/phonikud-onnx/resolve/main/phonikud-1.0.int8.onnx"
_HEBREW_RUN = re.compile(r"[\u0590-\u05FF]+")
# Cantillation / meteg — keep standard vowel marks (U+05B0–U+05BB) for TTS
_PHONETIC_EXTRAS = re.compile(r"[\u0591-\u05AF\u05BD\u05BF\u05C0\u05C3-\u05C7]")
_phonikud: Any | None = None
_glossary: dict[str, Any] | None = None


def _speak_code_english(code: str) -> str:
    """OCR token → spoken English label (e.g. int& → int reference)."""
    raw = (code or "").strip().replace(" ", "")
    if not raw:
        return ""
    for key in sorted(_CODE_TTS, key=len, reverse=True):
        if raw.lower() == key.lower():
            return _CODE_TTS[key]
    base = raw
    suffix = ""
    if base.endswith("&"):
        base, suffix = base[:-1], " reference"
    elif base.endswith("*"):
        base, suffix = base[:-1], " pointer"
    if base.lower() in _CODE_TTS:
        return _CODE_TTS[base.lower()] + suffix
    return prepare_hebrew_narration(raw)


def _base_tts_label(code: str) -> str:
    return _speak_code_english(code)


def load_glossary(path: Path | None = None) -> dict[str, Any]:
    global _glossary
    p = path or GLOSSARY_PATH
    if _glossary is not None and path is None:
        return _glossary
    if p.is_file():
        _glossary = json.loads(p.read_text(encoding="utf-8"))
    else:
        _glossary = {"labels": dict(_CODE_TTS), "by_question": {}}
    _glossary.setdefault("labels", {}).update(_CODE_TTS)
    return _glossary


def save_glossary(data: dict[str, Any], path: Path | None = None) -> Path:
    global _glossary
    p = path or GLOSSARY_PATH
    p.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    _glossary = data
    return p


def glossary_answer_codes(question_file: str) -> list[str] | None:
    g = load_glossary()
    codes = g.get("by_question", {}).get(question_file)
    return list(codes) if codes else None


def code_to_tts_label(code: str) -> str:
    raw = (code or "").strip().replace(" ", "")
    if not raw:
        return ""
    label = load_glossary().get("labels", {}).get(raw)
    if label:
        return label
    return _base_tts_label(raw)


def extract_answer_codes(image_paths: list[Path]) -> list[str]:
    """OCR answer_1..answer_4 PNGs so TTS names the option that is actually shown."""
    if len(image_paths) < 5 or not shutil.which("tesseract"):
        return []
    codes: list[str] = []
    for path in image_paths[1:5]:
        try:
            out = subprocess.check_output(
                ["tesseract", str(path), "stdout", "-l", "eng", "--psm", "7"],
                stderr=subprocess.DEVNULL,
                text=True,
            )
            codes.append(out.strip().replace(" ", ""))
        except (subprocess.CalledProcessError, FileNotFoundError):
            codes.append("")
    return codes


def build_glossary_labels(codes: set[str]) -> dict[str, str]:
    """Map every OCR'd answer token to its TTS label (uses phonetic rules, not prior glossary)."""
    labels = dict(_CODE_TTS)
    for code in sorted(codes):
        if code:
            labels[code] = _base_tts_label(code)
    return labels


def _apply_english_loanwords(text: str) -> str:
    out = text
    for he, en in _ENGLISH_LOANWORDS:
        # ponytail: whole-token only — don't break טיפוס when mapping טי→T
        out = re.sub(
            rf"(?<![\u0590-\u05FF]){re.escape(he)}(?![\u0590-\u05FF])",
            en,
            out,
        )
    return out


def prepare_hebrew_narration(text: str) -> str:
    """Spoken Israeli Hebrew + English loanwords in Latin for TTS."""
    out = (text or "").strip()
    for phrase in _STRIP_PHRASES:
        out = re.sub(re.escape(phrase) + r"\s*", "", out, flags=re.IGNORECASE)
    for pattern, repl in _FORMAL_REGEX:
        out = re.sub(pattern, repl, out, flags=re.IGNORECASE)
    for formal, spoken in _FORMAL_TO_SPOKEN:
        out = out.replace(formal, spoken)
    for pattern, repl in _PHONETIC:
        out = re.sub(pattern, repl, out, flags=re.IGNORECASE)
    out = _apply_english_loanwords(out)
    # ponytail: em dash buries the tail — TTS de-emphasizes what follows
    out = re.sub(r"\s*[—–]\s+", ". ", out)
    out = re.sub(r"ל([A-Za-z])", r"ל \1", out)
    out = re.sub(r"\s+", " ", out)
    return out.strip()


def _nikud_enabled() -> bool:
    return os.environ.get("TTS_NIKUD", "1").strip().lower() not in ("0", "false", "no", "off")


def _ensure_phonikud_model() -> Path | None:
    if PHONIKUD_MODEL_PATH.is_file():
        return PHONIKUD_MODEL_PATH
    try:
        PHONIKUD_MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
        print("  [nikud] downloading phonikud model (~294MB)...", flush=True)
        resp = requests.get(PHONIKUD_MODEL_URL, timeout=600)
        resp.raise_for_status()
        PHONIKUD_MODEL_PATH.write_bytes(resp.content)
        return PHONIKUD_MODEL_PATH
    except (OSError, requests.RequestException) as e:
        print(f"  [nikud] model unavailable ({e}) — TTS without nikud", flush=True)
        return None


def _get_phonikud() -> Any | None:
    global _phonikud
    if _phonikud is not None:
        return _phonikud
    if not _nikud_enabled():
        return None
    path = _ensure_phonikud_model()
    if not path:
        return None
    try:
        from phonikud_onnx import Phonikud

        _phonikud = Phonikud(str(path))
    except ImportError:
        print("  [nikud] pip install phonikud-onnx — TTS without nikud", flush=True)
        return None
    return _phonikud


def _clean_nikud(text: str) -> str:
    """Nikud male for TTS — drop stress/meteg markers Phonikud adds for G2P."""
    return _PHONETIC_EXTRAS.sub("", text)


def add_nikud_for_tts(text: str) -> str:
    """Add vowel marks to Hebrew runs; leave Latin/code tokens untouched."""
    if not text or not _nikud_enabled():
        return text
    model = _get_phonikud()
    if model is None:
        return text
    out: list[str] = []
    for m in re.finditer(r"[\u0590-\u05FF]+|[^\u0590-\u05FF]+", text):
        chunk = m.group()
        if _HEBREW_RUN.fullmatch(chunk):
            marked = model.add_diacritics(chunk, mark_matres_lectionis="|")
            chunk = _clean_nikud(model.get_nikud_male(marked, "|"))
        out.append(chunk)
    return "".join(out)


def prepare_for_tts(
    text: str,
    highlight_answer: int | None = None,
    answer_codes: list[str] | None = None,
) -> str:
    """Spoken Hebrew normalization + nikud — final string sent to TTS."""
    return add_nikud_for_tts(prepare_slide_narration(text, highlight_answer, answer_codes))


def prepare_slide_narration(
    text: str,
    highlight_answer: int | None = None,
    answer_codes: list[str] | None = None,
) -> str:
    """Prefix answer slides; label comes from OCR of the answer PNG, not the script."""
    out = prepare_hebrew_narration(text)
    if highlight_answer is None or not (1 <= highlight_answer <= 4):
        return out

    num = _ANSWER_NUM_HE[highlight_answer]
    body = re.sub(r"^תשובה\s+\S+\s*[,.]?\s*", "", out)
    body = re.sub(
        r"^(?:(?:const\s+)?int(?:\s+(?:reference|pointer))?|void|T|float|double|bool|string|char)\s*[—\-–]\s*",
        "",
        body.strip(),
        flags=re.IGNORECASE,
    )

    if answer_codes and len(answer_codes) >= highlight_answer:
        code = answer_codes[highlight_answer - 1]
        if code:
            label = code_to_tts_label(code)
            return f"תשובה {num}. {label}. {body}".strip()

    if not re.match(rf"^תשובה\s*{num}", out):
        return f"תשובה {num}. {body}".strip()
    return out


def tts_config() -> dict[str, str]:
    return {
        "provider": os.environ.get("TTS_PROVIDER", "edge").strip().lower(),
        "voice": os.environ.get("TTS_VOICE", "he-IL-HilaNeural").strip(),
        "rate": os.environ.get("TTS_RATE", "-4%").strip(),
        "speaking_rate": os.environ.get("TTS_SPEAKING_RATE", "0.90").strip(),
        "gemini_key": os.environ.get("GEMINI_API_KEY", os.environ.get("GOOGLE_API_KEY", "")).strip(),
        "gemini_model": os.environ.get("GEMINI_TTS_MODEL", "gemini-2.5-flash-preview-tts").strip(),
        "gemini_voice": os.environ.get("GEMINI_TTS_VOICE", "Kore").strip(),
        "google_key": os.environ.get("GOOGLE_CLOUD_TTS_API_KEY", "").strip(),
        "google_voice": os.environ.get("GOOGLE_TTS_VOICE", "he-IL-Wavenet-B").strip(),
        "elevenlabs_key": os.environ.get("ELEVENLABS_API_KEY", "").strip(),
        "elevenlabs_voice": os.environ.get("ELEVENLABS_VOICE_ID", "pNInz6obpgDQGcFmaJgB").strip(),
    }


def _google_tts(text: str, out_path: Path, cfg: dict[str, str]) -> None:
    key = cfg["google_key"]
    if not key:
        raise RuntimeError("GOOGLE_CLOUD_TTS_API_KEY required for TTS_PROVIDER=google")
    try:
        rate = float(cfg["speaking_rate"])
    except ValueError:
        rate = 0.90
    url = f"https://texttospeech.googleapis.com/v1/text:synthesize?key={key}"
    body = {
        "input": {"text": text},
        "voice": {"languageCode": "he-IL", "name": cfg["google_voice"]},
        "audioConfig": {"audioEncoding": "MP3", "speakingRate": rate, "pitch": 0},
    }
    resp = requests.post(url, json=body, timeout=60)
    resp.raise_for_status()
    audio = resp.json().get("audioContent")
    if not audio:
        raise RuntimeError("Google TTS returned no audio")
    out_path.write_bytes(base64.b64decode(audio))


def _pcm_to_mp3(pcm: bytes, out_path: Path, rate: int = 24000) -> None:
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        wav_path = Path(tmp.name)
    try:
        with wave.open(str(wav_path), "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(rate)
            wf.writeframes(pcm)
        subprocess.run(
            ["ffmpeg", "-y", "-i", str(wav_path), "-codec:a", "libmp3lame", "-q:a", "2", str(out_path)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
    finally:
        wav_path.unlink(missing_ok=True)


def _gemini_tts(text: str, out_path: Path, cfg: dict[str, str]) -> None:
    key = cfg["gemini_key"]
    if not key:
        raise RuntimeError("GEMINI_API_KEY required for TTS_PROVIDER=gemini")
    model = cfg["gemini_model"]
    url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}"
    body = {
        "contents": [{"parts": [{"text": text}]}],
        "generationConfig": {
            "responseModalities": ["AUDIO"],
            "speechConfig": {
                "voiceConfig": {
                    "prebuiltVoiceConfig": {"voiceName": cfg["gemini_voice"]}
                }
            },
        },
    }
    resp = requests.post(url, json=body, timeout=120)
    if resp.status_code in (429, 503):
        resp.raise_for_status()
    data = resp.json()
    parts = data.get("candidates", [{}])[0].get("content", {}).get("parts", [])
    inline = next((p.get("inlineData") for p in parts if p.get("inlineData")), None)
    if not inline or not inline.get("data"):
        raise RuntimeError("Gemini TTS returned no audio")
    pcm = base64.b64decode(inline["data"])
    rate = 24000
    mime = inline.get("mimeType") or ""
    m = re.search(r"rate=(\d+)", mime)
    if m:
        rate = int(m.group(1))
    _pcm_to_mp3(pcm, out_path, rate)


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
    rate = cfg["rate"] or "-4%"
    communicate = edge_tts.Communicate(text, cfg["voice"], rate=rate)
    await communicate.save(str(out_path))


async def synthesize_mp3(
    text: str,
    out_path: Path,
    *,
    highlight_answer: int | None = None,
    answer_codes: list[str] | None = None,
) -> None:
    prepared = prepare_for_tts(text, highlight_answer, answer_codes)
    cfg = tts_config()
    provider = cfg["provider"]

    if provider == "gemini":
        try:
            _gemini_tts(prepared, out_path, cfg)
        except (requests.HTTPError, RuntimeError) as e:
            code = getattr(getattr(e, "response", None), "status_code", None)
            if code in (429, 503) or "429" in str(e) or "503" in str(e):
                print("  [tts] Gemini rate-limited — falling back to edge", flush=True)
                await _edge_tts(prepared, out_path, cfg)
            else:
                raise
    elif provider == "google":
        _google_tts(prepared, out_path, cfg)
    elif provider == "elevenlabs":
        _elevenlabs_tts(prepared, out_path, cfg)
    else:
        await _edge_tts(prepared, out_path, cfg)


if __name__ == "__main__":
    assert prepare_hebrew_narration("int&") == "int reference"
    assert prepare_hebrew_narration("int*") == "int pointer"
    assert prepare_hebrew_narration("סי ועוד ועוד") == "C plus plus"
    assert prepare_hebrew_narration("C++") == "C plus plus"
    assert prepare_hebrew_narration("מחזיר רפרנס לאיקס") == "מחזיר reference ל x"
    assert prepare_hebrew_narration("לא ניתן להשים") == "אי אפשר להשים"
    assert prepare_hebrew_narration("לא X — זו לא תשובה חוקית") == "לא X. זה פשוט לא נכון"
    assert "שימו לב" not in prepare_hebrew_narration("שימו לב, זה לא חוקי")
    assert code_to_tts_label("int*") == "int pointer"
    assert code_to_tts_label("int") == "int"
    assert prepare_slide_narration("מחזיר copy.", 2, ["int&", "int*", "int", "T"]).startswith(
        "תשובה שתיים. int pointer."
    )
    assert prepare_slide_narration("מחזיר copy.", 3, ["int&", "int*", "int", "T"]).startswith(
        "תשובה שלוש. int."
    )
    if PHONIKUD_MODEL_PATH.is_file():
        n = add_nikud_for_tts("תשובה אחת")
        assert any(0x05B0 <= ord(c) <= 0x05BC for c in n), n
    print("[ponytail] tts_hebrew self-check passed")

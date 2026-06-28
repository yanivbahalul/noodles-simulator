#!/usr/bin/env python3
"""Offline pipeline: Gemini Vision → Hebrew TTS → ffmpeg → Supabase.

Usage (from repo root):
  pip install -r tools/generate-explanations/requirements.txt
  python tools/generate-explanations/generate.py --limit 5
  python tools/generate-explanations/generate.py --only-missing
  python tools/generate-explanations/generate.py --question-id "123.png"

Requires: GEMINI_API_KEY, SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY (or SERVICE_ROLE_SECRET),
          SUPABASE_BUCKET, ffmpeg on PATH.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.parse
from pathlib import Path
from typing import Any

import requests
from PIL import Image, ImageDraw

from tts_hebrew import (
    build_glossary_labels,
    extract_answer_codes,
    glossary_answer_codes,
    load_glossary,
    save_glossary,
    synthesize_mp3,
    GLOSSARY_PATH,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp"}
VIDEO_W, VIDEO_H = 1280, 720

SCRIPT_PROMPT = """You explain OOP / programming quiz questions in Hebrew for Israeli students.

You receive ONE screenshot showing the full quiz screen:
- Top: the question
- Bottom row (left to right): answer_1, answer_2, answer_3, answer_4
- answer_1 (leftmost) is ALWAYS the correct answer

Return ONLY valid JSON (no markdown fences) with this shape:
{
  "slides": [
    {
      "narration": "Hebrew text to speak aloud",
      "imageRef": "full",
      "highlightAnswer": null | 1 | 2 | 3 | 4
    }
  ],
  "needsReview": false
}

Hebrew / TTS rules (CRITICAL — read before writing each narration):
- Write like a native Israeli developer speaking to a friend, NOT like a textbook.
- Before finalizing EACH narration, silently ask yourself:
  "Would an Israeli student actually say it this way out loud? Is it clear for TTS?"
  If not — rewrite until it sounds natural.
- NEVER spell English letter-by-letter in Hebrew (bad: "סי ועוד ועוד", "איי פי איי", "אל מ"מ").
- Language names and code terms stay in Latin English: C++, int, reference, pointer, value, copy.
- Use spoken Israeli Hebrew: "אי אפשר" not "לא ניתן", "כי" not "מכיוון ש", "פה" not "בהקשר זה".
- No formal filler: "שימו לב", "בשאלה זו", "כביכול", "למעשה", "הינה".
- No markdown, backticks, em dashes (—), or symbols TTS reads badly (+, &, * in narration).
- ONE short sentence per slide when possible. Period, not dash, between ideas.

Slide structure:
- Exactly 5 slides. Up to 60 seconds total.
- Slide 1 (highlightAnswer null): what the question tests — one short sentence.
- Slides 2–5 (highlightAnswer 1–4): ONE sentence about THAT answer only.
- Do NOT name the answer type at the start — OCR adds "תשובה X. {type}." automatically.
- The pipeline prefix is long — put the main point in the FIRST words of the body, not the end.
- Example body: "מחזיר copy, אי אפשר לשים בו value."
- Wrong / invalid answers: lead with the verdict ("פשוט לא נכון", "זה לא return type") — never "תשובה חוקית", never double negation ("לא X — זו לא Y").
- Each slide highlights ONLY its answer number.
- Use imageRef "full" for every slide.
- Set needsReview true if unreadable or unsure.
"""

POLISH_PROMPT = """You are a native Israeli Hebrew editor for programming tutorial voiceovers.

You receive JSON with a "slides" array. Each slide has "narration" (Hebrew text for TTS) and "highlightAnswer".

For EVERY narration, rewrite if needed so it sounds natural when spoken aloud by Israeli TTS.

Fix these common problems:
- Letter-by-letter English ("סי ועוד ועוד" → keep concept as C++ in Latin, or rephrase without spelling symbols)
- Written/formal Hebrew → spoken Israeli Hebrew
- Redundant words, mumbling, double explanations
- Em dashes (—) → split into one sentence, or use "כי"/"אז"
- Exam language: "תשובה חוקית", "לא חוקי" → "פשוט לא נכון", "זה לא תקין"
- Double negation at end ("לא X — זו לא Y") → one clear verdict in the FIRST words
- Remember OCR adds "תשובה X. {label}." — body must not bury the point at the end
- Symbols (+, &, *, #) → Latin English words (reference, pointer, star) or rephrase
- Wrong terms: "להשמיך" → "להשים", "לא ניתן" → "אי אפשר"

Keep the same meaning and slide count. Do NOT change highlightAnswer or imageRef.
Return ONLY valid JSON: {"slides": [...]} — same structure, improved narrations only.
"""

MAX_SLIDES = 5


class GeminiQuotaExhausted(RuntimeError):
    """Gemini free-tier daily quota exhausted — stop batch, resume tomorrow."""


def _err_text(ex: BaseException) -> str:
    return str(ex)


def _is_rate_limited(ex: BaseException) -> bool:
    t = _err_text(ex)
    return "429" in t or "RESOURCE_EXHAUSTED" in t


def _is_daily_quota(ex: BaseException) -> bool:
    if not _is_rate_limited(ex):
        return False
    t = _err_text(ex)
    return "FreeTier" in t or "PerDay" in t or "quota exceeded" in t.lower()


def _retry_wait_sec(ex: BaseException, attempt: int) -> float:
    m = re.search(r"retry in (\d+(?:\.\d+)?)s", _err_text(ex), re.I)
    if m:
        return float(m.group(1)) + 1
    return float(min(6 * (attempt + 1), 60))


def _vision_models() -> list[str]:
    primary = os.environ.get("GEMINI_VISION_MODEL", "gemini-2.0-flash-lite").strip()
    pool = [primary, "gemini-2.0-flash-lite", "gemini-2.0-flash", "gemini-2.5-flash-lite", "gemini-2.5-flash"]
    out: list[str] = []
    for m in pool:
        if m and m not in out:
            out.append(m)
    return out


def _first_sentence(text: str) -> str:
    text = (text or "").strip()
    if not text:
        return ""
    m = re.match(r"^[^.!?]+[.!?]?", text)
    return (m.group(0) if m else text[:90]).strip()


def _trim_sentences(text: str, max_sentences: int) -> str:
    text = (text or "").strip()
    if max_sentences <= 0 or not text:
        return text
    parts = [p for p in re.split(r"(?<=[.!?])\s+", text) if p.strip()]
    return " ".join(parts[:max_sentences]).strip()


def _slide_highlight_answer(slide: dict[str, Any]) -> int | None:
    n = slide.get("highlightAnswer")
    if isinstance(n, int) and 1 <= n <= 4:
        return n
    h = slide.get("highlight")
    if h == "correct":
        return 1
    return None


def _split_bundled_wrong(narration: str) -> list[tuple[int, str]]:
    """Legacy scripts that lump wrong answers into one slide."""
    found = re.findall(r"תשובה\s*([234])\s*:\s*([^.!?]+[.!?]?)", narration)
    if found:
        return [(int(n), t.strip()) for n, t in found]
    return []


def compact_script(script: dict[str, Any]) -> dict[str, Any]:
    """Normalize to 5 slides — one highlighted answer per slide."""
    raw = [s for s in (script.get("slides") or []) if (s.get("narration") or "").strip()]
    if not raw:
        return script

    compact: list[dict[str, Any]] = []
    for s in raw:
        ha = _slide_highlight_answer(s)
        max_sent = 2 if ha else 1
        narration = _trim_sentences(s.get("narration", ""), max_sent)
        if s.get("highlight") == "wrong" and ha is None:
            for num, bit in _split_bundled_wrong(s.get("narration", "")):
                compact.append({
                    "narration": _trim_sentences(bit, 1),
                    "imageRef": "full",
                    "highlightAnswer": num,
                })
            continue
        compact.append({
            "narration": narration,
            "imageRef": "full",
            "highlightAnswer": ha,
        })

    script["slides"] = compact[:MAX_SLIDES]
    return script


def load_dotenv(path: Path) -> None:
    if not path.is_file():
        return
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            continue
        key, val = line.split("=", 1)
        key, val = key.strip(), val.strip().strip('"').strip("'")
        os.environ.setdefault(key, val)


def env(name: str, *fallbacks: str) -> str:
    for key in (name, *fallbacks):
        val = os.environ.get(key, "").strip()
        if val:
            return val
    return ""


def supabase_headers(api_key: str) -> dict[str, str]:
    return {
        "apikey": api_key,
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
    }


def list_storage_images(base_url: str, api_key: str, bucket: str) -> list[str]:
    """List image objects via Storage REST API (paginated)."""
    headers = {"apikey": api_key, "Authorization": f"Bearer {api_key}"}
    files: list[str] = []
    offset = 0
    limit = 1000
    while True:
        url = f"{base_url}/storage/v1/object/list/{bucket}"
        resp = requests.post(
            url,
            headers=headers,
            json={"prefix": "", "limit": limit, "offset": offset},
            timeout=120,
        )
        resp.raise_for_status()
        page = resp.json()
        if not page:
            break
        for item in page:
            name = item.get("name") or ""
            if not name or name.endswith("/"):
                continue
            ext = Path(name).suffix.lower()
            if ext in IMAGE_EXTS and not name.startswith("explanations/"):
                files.append(name)
        if len(page) < limit:
            break
        offset += len(page)
    return sorted(files, key=str.lower)


def group_questions(files: list[str]) -> list[list[str]]:
    groups: list[list[str]] = []
    for i in range(0, len(files) - 4, 5):
        groups.append(files[i : i + 5])
    return groups


def sanitize_video_name(question_file: str) -> str:
    name = question_file.strip()
    for ch in '\\/:*?"<>|':
        name = name.replace(ch, "_")
    return name


def video_object_path(question_file: str) -> str:
    return f"explanations/{sanitize_video_name(question_file)}.mp4"


def fetch_explanation_row(base_url: str, api_key: str, question_file: str) -> dict | None:
    q = urllib.parse.quote(f'"{question_file}"')
    url = f"{base_url}/rest/v1/question_explanations?QuestionFile=eq.{urllib.parse.quote(question_file)}&select=*"
    resp = requests.get(url, headers=supabase_headers(api_key), timeout=30)
    if not resp.ok:
        return None
    rows = resp.json()
    return rows[0] if rows else None


def upsert_explanation(base_url: str, api_key: str, row: dict[str, Any]) -> None:
    url = f"{base_url}/rest/v1/question_explanations"
    headers = supabase_headers(api_key)
    headers["Prefer"] = "resolution=merge-duplicates,return=minimal"
    resp = requests.post(url, headers=headers, json=[row], timeout=30)
    resp.raise_for_status()


def _storage_url(base_url: str, bucket: str, path: str) -> str:
    encoded = "/".join(urllib.parse.quote(p) for p in path.split("/"))
    return f"{base_url}/storage/v1/object/{bucket}/{encoded}"


def download_storage_object(base_url: str, api_key: str, bucket: str, path: str, dest: Path) -> None:
    url = _storage_url(base_url, bucket, path)
    resp = requests.get(url, headers={"apikey": api_key, "Authorization": f"Bearer {api_key}"}, timeout=120)
    resp.raise_for_status()
    dest.write_bytes(resp.content)


def storage_object_exists(base_url: str, api_key: str, bucket: str, path: str) -> bool:
    url = _storage_url(base_url, bucket, path)
    resp = requests.head(
        url, headers={"apikey": api_key, "Authorization": f"Bearer {api_key}"}, timeout=30
    )
    return resp.ok


def explanations_table_exists(base_url: str, api_key: str) -> bool:
    url = f"{base_url}/rest/v1/question_explanations?select=QuestionFile&limit=1"
    resp = requests.get(url, headers=supabase_headers(api_key), timeout=30)
    if resp.status_code == 404:
        return False
    resp.raise_for_status()
    return True


def backfill_explanations_db(base_url: str, api_key: str, bucket: str, groups: list[list[str]]) -> dict[str, int]:
    stats = {"ready": 0, "missing": 0}
    now = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    for group in groups:
        question_file = group[0]
        video_path = video_object_path(question_file)
        if not storage_object_exists(base_url, api_key, bucket, video_path):
            stats["missing"] += 1
            continue
        upsert_explanation(base_url, api_key, {
            "QuestionFile": question_file,
            "VideoPath": video_path,
            "ScriptJson": "",
            "Status": "ready",
            "ErrorMessage": "",
            "GeneratedAt": now,
        })
        stats["ready"] += 1
    return stats


def upload_storage_object(base_url: str, api_key: str, bucket: str, path: str, data: bytes, content_type: str) -> None:
    encoded = "/".join(urllib.parse.quote(p) for p in path.split("/"))
    url = f"{base_url}/storage/v1/object/{bucket}/{encoded}"
    headers = {
        "apikey": api_key,
        "Authorization": f"Bearer {api_key}",
        "Content-Type": content_type,
        "x-upsert": "true",
    }
    resp = requests.post(url, headers=headers, data=data, timeout=300)
    resp.raise_for_status()


def compose_quiz_screenshot(image_paths: list[Path], out: Path) -> Path:
    """Stitch question + 4 answers into one screen-like image for Gemini Vision."""
    w, pad = 1280, 20
    bg, label_c = (28, 28, 36), (180, 180, 190)

    q = Image.open(image_paths[0]).convert("RGB")
    answers = [Image.open(p).convert("RGB") for p in image_paths[1:5]]

    q.thumbnail((w - 2 * pad, 460), Image.Resampling.LANCZOS)

    cols, gap = 4, 10
    cell_w = (w - 2 * pad - gap * (cols - 1)) // cols
    cell_h = 110
    scaled = []
    for img in answers:
        copy = img.copy()
        copy.thumbnail((cell_w - 16, cell_h - 32), Image.Resampling.LANCZOS)
        scaled.append(copy)

    top = pad + 22
    h = top + q.height + 16 + 22 + cell_h + pad
    canvas = Image.new("RGB", (w, h), bg)
    draw = ImageDraw.Draw(canvas)

    draw.text((pad, pad), "שאלה", fill=label_c)
    canvas.paste(q, ((w - q.width) // 2, top))

    y_row = top + q.height + 16
    for i, img in enumerate(scaled):
        x0 = pad + i * (cell_w + gap)
        draw.text((x0 + 6, y_row), f"תשובה {i + 1}", fill=label_c)
        canvas.paste(img, (x0 + (cell_w - img.width) // 2, y_row + 20))

    out.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(out, "PNG")
    return out


def generate_script(api_key: str, image_paths: list[Path], work: Path) -> dict[str, Any]:
    from google import genai
    from google.genai import errors as genai_errors

    models = _vision_models()
    composite = compose_quiz_screenshot(image_paths, work / "quiz-composite.png")
    contents = [
        SCRIPT_PROMPT,
        "Full quiz screenshot (question on top, answers 1–4 left to right; answer 1 is correct):",
        Image.open(composite),
    ]
    client = genai.Client(api_key=api_key)
    last_err: Exception | None = None
    daily_hit = False

    for model_name in models:
        for attempt in range(4):
            try:
                response = client.models.generate_content(model=model_name, contents=contents)
                text = (response.text or "").strip()
                text = re.sub(r"^```(?:json)?\s*", "", text)
                text = re.sub(r"\s*```$", "", text)
                if model_name != models[0]:
                    print(f"  [vision] used fallback model {model_name}", flush=True)
                return json.loads(text)
            except genai_errors.ClientError as ex:
                last_err = ex
                if _is_daily_quota(ex):
                    daily_hit = True
                    print(f"  [vision] {model_name} daily quota — next model", flush=True)
                    break
                if _is_rate_limited(ex):
                    wait = _retry_wait_sec(ex, attempt)
                    print(f"  [vision] {model_name} rate limit — retry in {wait:.0f}s", flush=True)
                    time.sleep(wait)
                    continue
                raise
            except genai_errors.ServerError as ex:
                last_err = ex
                if _is_daily_quota(ex) or _is_rate_limited(ex):
                    if _is_daily_quota(ex):
                        daily_hit = True
                        print(f"  [vision] {model_name} daily quota — next model", flush=True)
                        break
                    wait = _retry_wait_sec(ex, attempt)
                    print(f"  [vision] {model_name} busy — retry in {wait:.0f}s", flush=True)
                    time.sleep(wait)
                    continue
                raise
            except json.JSONDecodeError as ex:
                last_err = ex
                if attempt < 2:
                    time.sleep(2)
                    continue
                raise

    if daily_hit:
        raise GeminiQuotaExhausted(
            "Gemini daily free-tier quota exhausted on all vision models. "
            "Enable billing, wait until tomorrow, or set GEMINI_VISION_MODEL to a model with quota left. "
            "Resume with: tools/generate-explanations/run-until-done.sh "
            "(auto) or python3 tools/generate-explanations/generate.py --only-missing"
        )
    raise RuntimeError(f"Gemini vision failed after retries: {last_err}")


def polish_script(script: dict[str, Any], api_key: str) -> dict[str, Any]:
    """Second pass: fix spoken Hebrew before TTS (gemini-2.0-flash text-only)."""
    if os.environ.get("GEMINI_POLISH", "1").strip().lower() in ("0", "false", "no"):
        return script
    slides = script.get("slides") or []
    if not slides:
        return script

    from google import genai
    from google.genai import errors as genai_errors

    model = os.environ.get("GEMINI_POLISH_MODEL", "gemini-2.0-flash-lite").strip()
    payload = json.dumps({"slides": slides}, ensure_ascii=False)
    client = genai.Client(api_key=api_key)
    try:
        response = client.models.generate_content(
            model=model,
            contents=[POLISH_PROMPT, payload],
        )
    except (genai_errors.ClientError, genai_errors.ServerError) as ex:
        if _is_daily_quota(ex) or _is_rate_limited(ex):
            print("  [polish] skipped — Gemini quota/rate limit", flush=True)
            return script
        raise
    text = (response.text or "").strip()
    text = re.sub(r"^```(?:json)?\s*", "", text)
    text = re.sub(r"\s*```$", "", text)
    out = json.loads(text)
    merged = []
    for i, slide in enumerate(slides):
        raw = (out.get("slides") or [])
        narr = raw[i].get("narration", slide.get("narration")) if i < len(raw) else slide.get("narration")
        merged.append({**slide, "narration": (narr or "").strip()})
    script["slides"] = merged
    return script


def _autocrop_content(img: Image.Image, tol: int = 36) -> Image.Image:
    """Trim uniform margins so short code snippets fill the answer card."""
    img = img.convert("RGB")
    px = img.load()
    w, h = img.size
    min_x, min_y, max_x, max_y = w, h, 0, 0
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if not (r > 230 and g > 230 and b > 230) and not (r > 185 and g > 165 and b < 175):
                min_x, min_y = min(min_x, x), min(min_y, y)
                max_x, max_y = max(max_x, x), max(max_y, y)
    if max_x <= min_x:
        return img
    return img.crop((max(0, min_x - 2), max(0, min_y - 2), min(w, max_x + 3), min(h, max_y + 3)))


def _prepare_answer_image(img: Image.Image) -> Image.Image:
    """Remove quiz UI tint (yellow/green/red selection) so code symbols stay readable."""
    img = img.convert("RGB")
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            # yellow/beige selected, green correct, red incorrect button backgrounds
            if (r > 185 and g > 165 and b < 175) or (g > 185 and r < 175 and b < 175) or (r > 185 and g < 150 and b < 150):
                px[x, y] = (248, 248, 252)
    return _autocrop_content(img)


def build_quiz_frame(image_paths: list[Path], highlight_answer: int | None = None) -> Image.Image:
    """Render 1280x720 slide; highlight_answer 1–4 highlights exactly one card."""
    w, h = VIDEO_W, VIDEO_H
    pad_v, pad_h, gap = 10, 22, 14
    gap_q_answers = 12
    bg = (24, 24, 32)
    canvas = Image.new("RGB", (w, h), bg)
    draw = ImageDraw.Draw(canvas)

    cell_w, cell_h = 190, 64
    inner_pad = 6
    content_max = (cell_w - 2 * inner_pad, cell_h - 2 * inner_pad)

    q = Image.open(image_paths[0]).convert("RGB")
    q.thumbnail((w - 2 * pad_h, h - 2 * pad_v - cell_h - gap_q_answers - 8), Image.Resampling.LANCZOS)

    block_h = q.height + gap_q_answers + cell_h
    block_y = max(pad_v, (h - block_h) // 2)
    q_y = block_y
    canvas.paste(q, ((w - q.width) // 2, q_y))

    row_w = 4 * cell_w + 3 * gap
    start_x = (w - row_w) // 2
    answers_y = q_y + q.height + gap_q_answers
    answer_rects: list[tuple[int, int, int, int]] = []

    for i, path in enumerate(image_paths[1:5]):
        x0 = start_x + i * (cell_w + gap)
        y0 = answers_y
        rect = (x0, y0, x0 + cell_w, y0 + cell_h)
        answer_rects.append(rect)

        draw.rounded_rectangle(rect, radius=8, fill=(38, 38, 46), outline=(68, 68, 78), width=2)
        inner = (x0 + inner_pad, y0 + inner_pad, x0 + cell_w - inner_pad, y0 + cell_h - inner_pad)
        draw.rounded_rectangle(inner, radius=6, fill=(248, 248, 252))

        ans = _prepare_answer_image(Image.open(path))
        ans.thumbnail(content_max, Image.Resampling.LANCZOS)
        ax = inner[0] + (inner[2] - inner[0] - ans.width) // 2
        ay = inner[1] + (inner[3] - inner[1] - ans.height) // 2
        canvas.paste(ans, (ax, ay))
        draw.text((x0 + 10, y0 + 6), str(i + 1), fill=(120, 120, 130))

    if highlight_answer is not None and 1 <= highlight_answer <= 4:
        idx = highlight_answer - 1
        x0, y0, x1, y1 = answer_rects[idx]
        color = (72, 210, 110) if highlight_answer == 1 else (235, 85, 85)
        width = 4 if highlight_answer == 1 else 3
        draw.rounded_rectangle([x0 - 3, y0 - 3, x1 + 3, y1 + 3], radius=10, outline=color, width=width)

    return canvas


def save_quiz_frame(image_paths: list[Path], slide: dict[str, Any], out: Path) -> Path:
    out.parent.mkdir(parents=True, exist_ok=True)
    build_quiz_frame(image_paths, _slide_highlight_answer(slide)).save(out, "PNG")
    return out


async def tts_to_mp3(
    text: str,
    out_path: Path,
    highlight_answer: int | None = None,
    answer_codes: list[str] | None = None,
) -> float:
    await synthesize_mp3(text, out_path, highlight_answer=highlight_answer, answer_codes=answer_codes)
    dur = ffprobe_duration(out_path)
    return max(dur, 1.0)


def ffprobe_duration(path: Path) -> float:
    if not shutil.which("ffprobe"):
        return 4.0
    cmd = [
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", str(path),
    ]
    try:
        out = subprocess.check_output(cmd, stderr=subprocess.DEVNULL, text=True).strip()
        return float(out)
    except (subprocess.CalledProcessError, ValueError):
        return 4.0


def make_slide_clip(frame: Path, audio: Path, duration: float, out: Path) -> None:
    cmd = [
        "ffmpeg", "-y",
        "-loop", "1", "-i", str(frame),
        "-i", str(audio),
        "-c:v", "libx264", "-tune", "stillimage", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "128k",
        "-t", f"{duration:.2f}",
        "-shortest",
        str(out),
    ]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def concat_clips(clips: list[Path], out: Path) -> None:
    list_file = out.with_suffix(".txt")
    list_file.write_text("\n".join(f"file '{c.resolve()}'" for c in clips), encoding="utf-8")
    cmd = [
        "ffmpeg", "-y", "-f", "concat", "-safe", "0", "-i", str(list_file),
        "-c", "copy", str(out),
    ]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def build_video(slides: list[dict], image_paths: list[Path], work: Path, answer_codes: list[str] | None = None) -> Path:
    clips: list[Path] = []
    for i, slide in enumerate(slides):
        narration = (slide.get("narration") or "").strip()
        if not narration:
            continue
        frame = work / f"slide_{i:02d}.frame.png"
        save_quiz_frame(image_paths, slide, frame)
        audio = work / f"slide_{i:02d}.mp3"
        ha = _slide_highlight_answer(slide)
        print(f"    slide {i + 1}/{len(slides)}...", flush=True)
        asyncio.run(tts_to_mp3(narration, audio, highlight_answer=ha, answer_codes=answer_codes))
        if os.environ.get("TTS_PROVIDER", "edge").strip().lower() == "gemini":
            time.sleep(int(os.environ.get("TTS_PAUSE_SECONDS", "2")))
        duration = ffprobe_duration(audio) + 0.1
        clip = work / f"clip_{i:02d}.mp4"
        make_slide_clip(frame, audio, duration, clip)
        clips.append(clip)
    if not clips:
        raise RuntimeError("no slides produced")
    final = work / "final.mp4"
    if len(clips) == 1:
        shutil.copy(clips[0], final)
    else:
        concat_clips(clips, final)
    return final


def build_tts_glossary(
    groups: list[list[str]],
    *,
    sb_url: str,
    sb_key: str,
    bucket: str,
) -> Path:
    """OCR all answer PNGs once — per-question codes + global TTS labels."""
    by_question: dict[str, list[str]] = {}
    all_codes: set[str] = set()
    total = len(groups)
    print(f"Building TTS glossary from {total} question groups...", flush=True)
    for i, group in enumerate(groups, 1):
        question_file = group[0]
        with tempfile.TemporaryDirectory(prefix="noodles-gloss-") as tmp:
            paths: list[Path] = []
            for fname in group:
                dest = Path(tmp) / fname.replace("/", "_")
                download_storage_object(sb_url, sb_key, bucket, fname, dest)
                paths.append(dest)
            codes = extract_answer_codes(paths)
            by_question[question_file] = codes
            all_codes.update(c for c in codes if c)
        if i % 25 == 0 or i == total:
            print(f"  glossary scan {i}/{total}...", flush=True)
    labels = build_glossary_labels(all_codes)
    data = {
        "built_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "questions": total,
        "unique_codes": len(all_codes),
        "labels": labels,
        "by_question": by_question,
    }
    path = save_glossary(data)
    print(
        f"Glossary ready: {len(all_codes)} unique codes, {total} questions → {path.name}",
        flush=True,
    )
    uniq = sorted(all_codes)
    if uniq:
        sample = ", ".join(f"{c}→{labels[c]}" for c in uniq[:8])
        print(f"  sample: {sample}{'…' if len(uniq) > 8 else ''}", flush=True)
    return path


def glossary_covers(groups: list[list[str]]) -> bool:
    if not GLOSSARY_PATH.is_file():
        return False
    g = load_glossary()
    by_q = g.get("by_question") or {}
    return all(g[0] in by_q for g in groups)


def process_question(
    group: list[str],
    *,
    sb_url: str,
    sb_key: str,
    bucket: str,
    gemini_key: str | None,
    only_missing: bool,
    script_override: dict[str, Any] | None = None,
    local_copy: Path | None = None,
    skip_db: bool = False,
    script_out: Path | None = None,
    script_only: bool = False,
) -> str:
    question_file = group[0]
    video_path = video_object_path(question_file)
    existing = None if skip_db else fetch_explanation_row(sb_url, sb_key, question_file)
    if only_missing:
        if existing and existing.get("Status") == "ready":
            return "skip"
        if skip_db and storage_object_exists(sb_url, sb_key, bucket, video_path):
            return "skip"

    if not skip_db:
        upsert_explanation(sb_url, sb_key, {
            "QuestionFile": question_file,
            "VideoPath": "",
            "ScriptJson": "",
            "Status": "pending",
            "ErrorMessage": "",
            "GeneratedAt": None,
        })

    if not shutil.which("ffmpeg"):
        raise RuntimeError("ffmpeg not found on PATH")

    with tempfile.TemporaryDirectory(prefix="noodles-explain-") as tmp:
        work = Path(tmp)
        paths: list[Path] = []
        refs = ["question", "answer_1", "answer_2", "answer_3", "answer_4"]
        for fname in group:
            dest = work / fname.replace("/", "_")
            download_storage_object(sb_url, sb_key, bucket, fname, dest)
            paths.append(dest)
        ref_map = dict(zip(refs, paths))
        composite = compose_quiz_screenshot(paths, work / "quiz-composite.png")
        ref_map["full"] = composite
        for ref in refs:
            ref_map[ref] = composite

        if script_override is not None:
            script = script_override
        else:
            if not gemini_key:
                raise RuntimeError("GEMINI_API_KEY required without --script-file")
            print("  vision...", flush=True)
            script = generate_script(gemini_key, paths, work)
        script = compact_script(script)
        if gemini_key and not script_override:
            print("  polish hebrew...", flush=True)
            script = polish_script(script, gemini_key)
            script = compact_script(script)
        answer_codes = glossary_answer_codes(question_file)
        if not answer_codes or not any(answer_codes):
            answer_codes = extract_answer_codes(paths)
        if not any(answer_codes) and script.get("answerCodes"):
            answer_codes = list(script["answerCodes"])
        if answer_codes:
            script["answerCodes"] = answer_codes
        slides = script.get("slides") or []
        if not slides:
            raise RuntimeError("empty slides")

        if script_out:
            script_out.parent.mkdir(parents=True, exist_ok=True)
            script_out.write_text(json.dumps(script, ensure_ascii=False, indent=2), encoding="utf-8")
        if script_only:
            return "needs_review" if script.get("needsReview") else "ready"

        print(f"  video ({len(slides)} slides)...", flush=True)
        video = build_video(slides, paths, work, answer_codes or None)
        if local_copy:
            local_copy.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy(video, local_copy)
        video_path = video_object_path(question_file)
        print("  upload...", flush=True)
        upload_storage_object(
            sb_url, sb_key, bucket, video_path,
            video.read_bytes(), "video/mp4",
        )

        status = "needs_review" if script.get("needsReview") else "ready"
        if not skip_db:
            upsert_explanation(sb_url, sb_key, {
                "QuestionFile": question_file,
                "VideoPath": video_path,
                "ScriptJson": json.dumps(script, ensure_ascii=False),
                "Status": status,
                "ErrorMessage": "",
                "GeneratedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            })
    return status


def main() -> int:
    load_dotenv(REPO_ROOT / ".env")
    parser = argparse.ArgumentParser(description="Generate question explanation videos")
    parser.add_argument("--limit", type=int, default=0, help="Max questions to process (0 = all)")
    parser.add_argument("--only-missing", action="store_true", help="Skip questions with status=ready")
    parser.add_argument("--question-id", type=str, default="", help="Process a single question file")
    parser.add_argument("--validate", action="store_true", help="List question groups and exit (no API calls)")
    parser.add_argument("--script-file", type=str, default="", help="Use pre-written script JSON (skip Gemini)")
    parser.add_argument("--local-copy", type=str, default="", help="Also save MP4 to this local path")
    parser.add_argument("--skip-db", action="store_true", help="Skip question_explanations DB writes (video only)")
    parser.add_argument("--script-out", type=str, default="", help="Save generated script JSON to this path")
    parser.add_argument("--script-only", action="store_true", help="Generate script only (no TTS/video)")
    parser.add_argument("--backfill-db", action="store_true", help="Register existing storage videos in question_explanations")
    parser.add_argument("--glossary-only", action="store_true", help="OCR all answers and write tts-glossary.json, then exit")
    parser.add_argument("--rebuild-glossary", action="store_true", help="Force re-scan before batch")
    args = parser.parse_args()

    sb_url = env("SUPABASE_URL").rstrip("/")
    sb_key = env("SUPABASE_SERVICE_ROLE_KEY", "SERVICE_ROLE_SECRET", "SUPABASE_ANON_KEY")
    bucket = env("SUPABASE_BUCKET", "BUCKET") or "images"
    gemini_key = env("GEMINI_API_KEY", "GOOGLE_API_KEY")

    if args.validate:
        if not sb_url or not sb_key:
            print("Missing SUPABASE_URL or SERVICE_ROLE_SECRET", file=sys.stderr)
            return 1
        files = list_storage_images(sb_url, sb_key, bucket)
        groups = group_questions(files)
        print(json.dumps({"images": len(files), "groups": len(groups), "sample": [g[0] for g in groups[:5]]}, indent=2))
        return 0

    missing = [n for n, v in [
        ("SUPABASE_URL", sb_url),
        ("SUPABASE_SERVICE_ROLE_KEY", sb_key),
    ] if not v]
    if not args.script_file:
        if not gemini_key:
            missing.append("GEMINI_API_KEY")
    if missing:
        print(f"Missing env: {', '.join(missing)}", file=sys.stderr)
        return 1

    script_override = None
    if args.script_file:
        script_override = json.loads(Path(args.script_file).read_text(encoding="utf-8"))
    local_copy = Path(args.local_copy) if args.local_copy else None
    script_out = Path(args.script_out) if args.script_out else None

    print(f"Listing images from bucket '{bucket}'...")
    files = list_storage_images(sb_url, sb_key, bucket)
    groups = group_questions(files)
    print(f"Found {len(groups)} question groups from {len(files)} images")

    if args.question_id:
        groups = [g for g in groups if g[0] == args.question_id or g[0].endswith(args.question_id)]
        if not groups:
            print(f"No group found for question-id={args.question_id!r}", file=sys.stderr)
            return 1

    if args.limit > 0:
        groups = groups[: args.limit]

    if args.backfill_db:
        if not explanations_table_exists(sb_url, sb_key):
            print("Run supabase/question_explanations.sql in Supabase SQL editor first.", file=sys.stderr)
            return 1
        stats = backfill_explanations_db(sb_url, sb_key, bucket, groups)
        print("Backfill:", json.dumps(stats, indent=2))
        return 0

    if args.glossary_only or args.rebuild_glossary or not glossary_covers(groups):
        build_tts_glossary(groups, sb_url=sb_url, sb_key=sb_key, bucket=bucket)
        if args.glossary_only:
            return 0
    else:
        load_glossary()
        print(f"Using existing glossary ({GLOSSARY_PATH.name})", flush=True)

    skip_db = args.skip_db
    if not skip_db and not explanations_table_exists(sb_url, sb_key):
        print("question_explanations table missing — using --skip-db (videos only).", flush=True)
        print("After batch: run supabase/question_explanations.sql then: python3 tools/generate-explanations/generate.py --backfill-db", flush=True)
        skip_db = True

    stats = {"ready": 0, "needs_review": 0, "failed": 0, "skip": 0}
    quota_stop = False
    for i, group in enumerate(groups, 1):
        q = group[0]
        print(f"[{i}/{len(groups)}] {q} ...", flush=True)
        try:
            result = process_question(
                group,
                sb_url=sb_url,
                sb_key=sb_key,
                bucket=bucket,
                gemini_key=gemini_key or None,
                only_missing=args.only_missing,
                script_override=script_override,
                local_copy=local_copy,
                skip_db=skip_db,
                script_out=script_out,
                script_only=args.script_only,
            )
            stats[result] = stats.get(result, 0) + 1
            print(f"  -> {result}")
            if script_out and script_out.exists():
                print(f"  script: {script_out}")
            if local_copy and local_copy.exists():
                print(f"  local: {local_copy}")
        except GeminiQuotaExhausted as ex:
            stats["failed"] += 1
            quota_stop = True
            print(f"\n  -> STOP: {ex}", file=sys.stderr)
            break
        except Exception as ex:
            stats["failed"] += 1
            print(f"  -> failed: {ex}", file=sys.stderr)
            if not skip_db:
                upsert_explanation(sb_url, sb_key, {
                "QuestionFile": q,
                "VideoPath": "",
                "ScriptJson": "",
                "Status": "failed",
                "ErrorMessage": str(ex)[:500],
                    "GeneratedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                })

    print("\nDone:", json.dumps(stats, indent=2))
    if quota_stop:
        return 3  # run-until-done.sh sleeps until daily quota reset
    return 0 if stats["failed"] == 0 else 2


if __name__ == "__main__":
    # ponytail: runnable checks
    bundled = {
        "slides": [
            {"narration": "intro", "highlight": None},
            {"narration": "correct", "highlight": "correct"},
            {"narration": "תשובה 2: א. תשובה 3: ב. תשובה 4: ג.", "highlight": "wrong"},
        ]
    }
    out = compact_script(bundled)
    assert len(out["slides"]) == 5, out
    assert out["slides"][2]["highlightAnswer"] == 2
    assert out["slides"][0]["narration"] == "intro"
    tinted = Image.new("RGB", (4, 4), (255, 220, 120))
    cleaned = _prepare_answer_image(tinted)
    assert cleaned.getpixel((0, 0)) == (248, 248, 252)
    raise SystemExit(main())

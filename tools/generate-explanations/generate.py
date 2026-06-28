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
import base64
import json
import mimetypes
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

from tts_hebrew import prepare_hebrew_narration, synthesize_mp3

REPO_ROOT = Path(__file__).resolve().parents[2]
IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp"}
VIDEO_W, VIDEO_H = 1280, 720

SCRIPT_PROMPT = """You explain Israeli driving theory (תיאוריה) quiz questions in Hebrew.

You receive 5 images in order:
1. question — the question image
2. answer_1 — the CORRECT answer (always image #2)
3. answer_2 — wrong distractor
4. answer_3 — wrong distractor
5. answer_4 — wrong distractor

Return ONLY valid JSON (no markdown fences) with this shape:
{
  "slides": [
    {
      "narration": "Hebrew text to speak (2-4 sentences, clear and educational)",
      "imageRef": "question|answer_1|answer_2|answer_3|answer_4",
      "highlight": null | "correct" | "wrong"
    }
  ],
  "needsReview": false
}

Rules:
- 4-7 slides total: intro question, explain correct answer, briefly why each wrong answer is wrong.
- narration must be Hebrew, conversational, for someone who just got the question wrong.
- Write for text-to-speech: short sentences, natural spoken Hebrew, no English words if avoidable.
- Spell code terms phonetically in Hebrew (e.g. "אינט-רפרנס" not "int&", "וויד" not "void").
- Use imageRef exactly as listed above.
- Set highlight "correct" when showing answer_1, "wrong" when explaining a distractor.
- Set needsReview true if images are unreadable or you're unsure.
"""


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


def download_storage_object(base_url: str, api_key: str, bucket: str, path: str, dest: Path) -> None:
    encoded = "/".join(urllib.parse.quote(p) for p in path.split("/"))
    url = f"{base_url}/storage/v1/object/{bucket}/{encoded}"
    resp = requests.get(url, headers={"apikey": api_key, "Authorization": f"Bearer {api_key}"}, timeout=120)
    resp.raise_for_status()
    dest.write_bytes(resp.content)


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


def image_part(path: Path) -> dict[str, Any]:
    mime, _ = mimetypes.guess_type(str(path))
    mime = mime or "image/png"
    data = base64.b64encode(path.read_bytes()).decode("ascii")
    return {"inline_data": {"mime_type": mime, "data": data}}


def generate_script(api_key: str, image_paths: list[Path]) -> dict[str, Any]:
    import google.generativeai as genai

    genai.configure(api_key=api_key)
    model = genai.GenerativeModel("gemini-2.0-flash")
    parts: list[Any] = [SCRIPT_PROMPT]
    labels = ["question", "answer_1 (CORRECT)", "answer_2", "answer_3", "answer_4"]
    for label, p in zip(labels, image_paths):
        parts.append(f"Image: {label}")
        parts.append(image_part(p))
    response = model.generate_content(parts)
    text = (response.text or "").strip()
    text = re.sub(r"^```(?:json)?\s*", "", text)
    text = re.sub(r"\s*```$", "", text)
    return json.loads(text)


def fit_image_canvas(src: Path, highlight: str | None) -> Path:
    """Letterbox image to VIDEO_W x VIDEO_H with optional green/red border."""
    img = Image.open(src).convert("RGB")
    img.thumbnail((VIDEO_W - 80, VIDEO_H - 120), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (VIDEO_W, VIDEO_H), (24, 24, 32))
    x = (VIDEO_W - img.width) // 2
    y = (VIDEO_H - img.height) // 2
    canvas.paste(img, (x, y))
    if highlight in ("correct", "wrong"):
        color = (50, 205, 50) if highlight == "correct" else (255, 76, 76)
        draw = ImageDraw.Draw(canvas)
        pad = 6
        draw.rectangle(
            [x - pad, y - pad, x + img.width + pad, y + img.height + pad],
            outline=color,
            width=8,
        )
    out = src.with_suffix(".frame.png")
    canvas.save(out, "PNG")
    return out


async def tts_to_mp3(text: str, out_path: Path) -> float:
    await synthesize_mp3(text, out_path)
    dur = ffprobe_duration(out_path)
    return max(dur, 2.5)


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


def build_video(slides: list[dict], ref_to_path: dict[str, Path], work: Path) -> Path:
    clips: list[Path] = []
    for i, slide in enumerate(slides):
        ref = slide.get("imageRef", "question")
        narration = (slide.get("narration") or "").strip()
        if not narration:
            continue
        src = ref_to_path.get(ref)
        if not src or not src.is_file():
            continue
        highlight = slide.get("highlight")
        frame = fit_image_canvas(src, highlight if isinstance(highlight, str) else None)
        audio = work / f"slide_{i:02d}.mp3"
        asyncio.run(tts_to_mp3(narration, audio))
        duration = ffprobe_duration(audio) + 0.3
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
) -> str:
    question_file = group[0]
    existing = None if skip_db else fetch_explanation_row(sb_url, sb_key, question_file)
    if only_missing and existing and existing.get("Status") == "ready":
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

        if script_override is not None:
            script = script_override
        else:
            if not gemini_key:
                raise RuntimeError("GEMINI_API_KEY required without --script-file")
            script = generate_script(gemini_key, paths)
        slides = script.get("slides") or []
        if not slides:
            raise RuntimeError("empty slides")

        video = build_video(slides, ref_map, work)
        if local_copy:
            local_copy.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy(video, local_copy)
        video_path = video_object_path(question_file)
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

    stats = {"ready": 0, "needs_review": 0, "failed": 0, "skip": 0}
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
                skip_db=args.skip_db,
            )
            stats[result] = stats.get(result, 0) + 1
            print(f"  -> {result}")
            if local_copy and local_copy.exists():
                print(f"  local: {local_copy}")
        except Exception as ex:
            stats["failed"] += 1
            print(f"  -> failed: {ex}", file=sys.stderr)
            if not args.skip_db:
                upsert_explanation(sb_url, sb_key, {
                "QuestionFile": q,
                "VideoPath": "",
                "ScriptJson": "",
                "Status": "failed",
                "ErrorMessage": str(ex)[:500],
                    "GeneratedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                })

    print("\nDone:", json.dumps(stats, indent=2))
    return 0 if stats["failed"] == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())

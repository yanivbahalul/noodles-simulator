Question explanation video pipeline
===============================

Generates Hebrew slide+narration videos for each quiz question (offline batch).
User clicks "למה טעיתי?" after a wrong answer and gets a pre-rendered MP4.

Recommended production setup
----------------------------
  GEMINI_VISION_MODEL=gemini-2.5-flash     # script quality
  TTS_PROVIDER=edge           # recommended for batch (Gemini TTS rate-limits)
  TTS_PROVIDER=google         # premium: GOOGLE_CLOUD_TTS_API_KEY + he-IL-Wavenet-B
  TTS_PROVIDER=gemini         # optional; often rate-limited in batch

Quality workflow (do this once before full batch):
  1. Run SQL: supabase/question_explanations.sql in Supabase SQL editor
  2. ./tools/generate-explanations/run-pilot.sh
  3. Listen to all 10 videos; fix any with needsReview in dashboard
  4. ./tools/generate-explanations/run-full-batch.sh

Prerequisites
-------------
- Python 3.10+
- ffmpeg + ffprobe on PATH
- tesseract on PATH (OCR for answer labels in TTS — brew install tesseract)
- `.env` in repo root with:
  - GEMINI_API_KEY
  - SUPABASE_URL
  - SUPABASE_SERVICE_ROLE_KEY (or SERVICE_ROLE_SECRET)
  - SUPABASE_BUCKET (default: images)

Setup
-----
  pip install -r tools/generate-explanations/requirements.txt

Usage
-----
  ./tools/generate-explanations/run-pilot.sh          # first 10 questions
  ./tools/generate-explanations/run-full-batch.sh     # all missing (one shot)
  ./tools/generate-explanations/run-until-done.sh     # same, auto-resumes daily after quota

  # Long batch in background (survives quota limits):
  # nohup bash tools/generate-explanations/run-until-done.sh >> tools/generate-explanations/batch.log 2>&1 &

  python tools/generate-explanations/generate.py --question-id "foo.png"
  python tools/generate-explanations/generate.py --script-file script.json --local-copy out.mp4

Text-to-speech providers
------------------------
  TTS_PROVIDER=edge          # free, fast — good for pilot (default if no Google key)
  TTS_VOICE=he-IL-HilaNeural # or he-IL-AvriNeural (male)

  TTS_PROVIDER=google        # recommended production (~$4/1M chars)
  GOOGLE_CLOUD_TTS_API_KEY=...
  GOOGLE_TTS_VOICE=he-IL-Wavenet-B

  TTS_PROVIDER=elevenlabs    # premium, expensive at scale
  ELEVENLABS_API_KEY=...

Avoid TTS_PROVIDER=gemini for batch — rate-limited and slow (12s pause per slide).

Demo (local, no DB):
  source tools/generate-explanations/env-defaults.sh
  python tools/generate-explanations/generate.py --limit 1 --skip-db \
    --local-copy wwwroot/demo/explanation-demo-edge.mp4

Manual script edit (hard questions):
  python tools/generate-explanations/generate.py --script-only --script-out script.json --question-id "..."
  # edit script.json, then:
  python tools/generate-explanations/generate.py --script-file script.json --question-id "..."

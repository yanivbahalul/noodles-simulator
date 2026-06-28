Question explanation video pipeline
===============================

Generates Hebrew slide+narration videos for each quiz question (offline batch).

Prerequisites
-------------
- Python 3.10+
- ffmpeg + ffprobe on PATH
- `.env` in repo root with:
  - GEMINI_API_KEY
  - SUPABASE_URL
  - SUPABASE_SERVICE_ROLE_KEY (or SERVICE_ROLE_SECRET)
  - SUPABASE_BUCKET (default: images)

Setup
-----
1. Run SQL migration: supabase/question_explanations.sql in Supabase SQL editor
2. pip install -r tools/generate-explanations/requirements.txt

Usage
-----
  python tools/generate-explanations/generate.py --limit 10
  python tools/generate-explanations/generate.py --only-missing
  python tools/generate-explanations/generate.py --question-id "your-question.png"

Pilot then full batch:
  python tools/generate-explanations/generate.py --limit 10
  python tools/generate-explanations/generate.py --only-missing

Text-to-speech (Hebrew quality)
-------------------------------
Default: Microsoft Edge TTS (free, no API key). Quality is OK but not premium.

Env vars:
  TTS_PROVIDER=edge          # default; free
  TTS_VOICE=he-IL-HilaNeural # or he-IL-AvriNeural (male)
  TTS_RATE=-8%               # slightly slower = clearer Hebrew

Better quality with same Gemini API key (Gemini Pro / AI Studio):
  TTS_PROVIDER=gemini
  GEMINI_API_KEY=...              # from aistudio.google.com/apikey
  GEMINI_TTS_MODEL=gemini-2.5-flash-preview-tts
  GEMINI_TTS_VOICE=Kore           # try other voices in AI Studio

  TTS_PROVIDER=google
  GOOGLE_CLOUD_TTS_API_KEY=...   # enable Cloud Text-to-Speech API
  GOOGLE_TTS_VOICE=he-IL-Wavenet-B

  TTS_PROVIDER=elevenlabs
  ELEVENLABS_API_KEY=...
  ELEVENLABS_VOICE_ID=...        # multilingual voice

Gemini (GEMINI_API_KEY) is only for reading images and writing the script — not for voice.

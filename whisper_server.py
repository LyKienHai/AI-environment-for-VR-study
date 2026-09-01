"""
whisper_server.py — Local Whisper speech-to-text REST server
=============================================================
Runs on http://localhost:7864

Endpoint:
    POST /transcribe
        multipart form: audio=<WAV file>
        returns: { "text": "...", "language": "de", "duration": 2.3 }

Start with:
    python whisper_server.py

Install dependencies (run once):
    pip install openai-whisper fastapi uvicorn python-multipart --break-system-packages

Models (set WHISPER_MODEL env var or edit MODEL below):
    tiny   — fastest, lowest accuracy (~39 MB)
    base   — good balance, recommended (~74 MB) ← default
    small  — better accuracy (~244 MB)
    medium — high accuracy, needs 8 GB RAM (~769 MB)
    large  — best quality, needs 10+ GB RAM
"""

import os
import io
import time
import tempfile
import traceback
import asyncio

# ── dependencies ──────────────────────────────────────────────────────────────
try:
    import whisper
except ImportError:
    print("\n❌ openai-whisper not installed.")
    print("   Run: pip install openai-whisper --break-system-packages\n")
    raise

try:
    import uvicorn
    from fastapi import FastAPI, File, UploadFile, HTTPException
    from fastapi.middleware.cors import CORSMiddleware
    from fastapi.responses import JSONResponse
except ImportError:
    print("\n❌ FastAPI/uvicorn not installed.")
    print("   Run: pip install fastapi uvicorn python-multipart --break-system-packages\n")
    raise

# ── configuration ─────────────────────────────────────────────────────────────
HOST        = os.getenv("WHISPER_HOST",     "0.0.0.0")
PORT        = int(os.getenv("WHISPER_PORT", "7864"))
MODEL_NAME  = os.getenv("WHISPER_MODEL",    "base")
LANGUAGE    = os.getenv("WHISPER_LANGUAGE", None)   # None = auto-detect; "de" = German only

# ── load model once at startup ────────────────────────────────────────────────
print(f"\n🎙  Loading Whisper model '{MODEL_NAME}'… (first run downloads the model)")
_t0 = time.time()
model = whisper.load_model(MODEL_NAME)
print(f"✅  Model ready in {time.time()-_t0:.1f}s\n")

# ── FastAPI app ───────────────────────────────────────────────────────────────
app = FastAPI(
    title="Whisper Transcription Server",
    description="Local speech-to-text for the GenAI VR Medical Room project.",
    version="1.0.0",
)

# Allow Unity (any origin) to POST to this server
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["POST", "GET"],
    allow_headers=["*"],
)

@app.get("/")
def root():
    return {"status": "ok", "model": MODEL_NAME, "language": LANGUAGE or "auto"}

@app.get("/health")
def health():
    return {"status": "ok"}

@app.post("/transcribe")
async def transcribe(audio: UploadFile = File(...)):
    """
    Accept a WAV audio file, transcribe it with Whisper, return JSON.

    Returns:
        { "text": "Guten Tag!", "language": "de", "duration": 1.87 }
    """
    t_start = time.time()

    # Read uploaded bytes
    raw_bytes = await audio.read()
    if len(raw_bytes) < 100:
        raise HTTPException(status_code=400, detail="Audio file too small.")

    # Write to temp file (Whisper needs a file path)
    suffix = ".wav"
    if audio.filename:
        ext = os.path.splitext(audio.filename)[-1].lower()
        if ext in (".wav", ".mp3", ".ogg", ".flac", ".m4a"):
            suffix = ext

    try:
        with tempfile.NamedTemporaryFile(suffix=suffix, delete=False) as tmp:
            tmp.write(raw_bytes)
            tmp_path = tmp.name

        # Transcribe
        opts = {"fp16": False}   # fp16=False for CPU safety; enable if CUDA available
        if LANGUAGE:
            opts["language"] = LANGUAGE

        result = model.transcribe(tmp_path, **opts)

        text     = result.get("text", "").strip()
        language = result.get("language", "")
        duration = time.time() - t_start

        print(f"[Whisper] '{text[:80]}' | lang={language} | {duration:.2f}s")

        return JSONResponse({
            "text":     text,
            "language": language,
            "duration": round(duration, 2),
        })

    except Exception as e:
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        try: os.unlink(tmp_path)
        except: pass


# ── run ───────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    print(f"🎤  Whisper server starting on http://{HOST}:{PORT}")
    print(f"    Model: {MODEL_NAME}  |  Language: {LANGUAGE or 'auto-detect'}")
    print(f"    Unity endpoint: POST http://localhost:{PORT}/transcribe\n")
    uvicorn.run(app, host=HOST, port=PORT, log_level="warning")

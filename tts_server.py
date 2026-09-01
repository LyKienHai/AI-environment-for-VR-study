"""
tts_server.py — Doctor voice text-to-speech server
====================================================
POST /speak  {"text": "...", "lang": "de", "rate": "+20%"}
  → returns audio/mpeg (MP3)

Primary engine : edge-tts  (Microsoft neural voices, free, requires internet)
Fallback engine: pyttsx3   (Windows SAPI, fully offline)

Best German voices (edge-tts):
  de-DE-FlorianMultilingualNeural  ← most natural male (default)
  de-DE-SeraphinaMultilingualNeural ← most natural female
  de-DE-ConradNeural               ← classic male
  de-DE-KillianNeural              ← expressive male
  de-DE-KatjaNeural                ← classic female

Speed: rate="+0%" normal · "+15%" slightly faster · "+30%" fast

Run: python tts_server.py  →  http://localhost:7866
"""

import asyncio
import io
import os
import sys
import tempfile
import logging
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel
import uvicorn

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("tts_server")

app = FastAPI(title="Doctor TTS Server")

# ── voices ────────────────────────────────────────────────────────────────────
VOICE_DE  = os.environ.get("TTS_VOICE",    "de-DE-FlorianMultilingualNeural")
VOICE_EN  = os.environ.get("TTS_VOICE_EN", "en-US-GuyNeural")
RATE_DE   = os.environ.get("TTS_RATE",    "+15%")   # slightly faster than default

# ── edge-tts (primary) ────────────────────────────────────────────────────────
try:
    import edge_tts
    EDGE_AVAILABLE = True
    log.info("edge-tts available ✓")
except ImportError:
    EDGE_AVAILABLE = False
    log.warning("edge-tts not installed — will use pyttsx3 fallback")

# ── pyttsx3 (offline fallback) ────────────────────────────────────────────────
try:
    import pyttsx3
    _pyttsx3_engine = pyttsx3.init()
    # Try to set a German voice
    for v in _pyttsx3_engine.getProperty("voices"):
        if "de" in v.id.lower() or "german" in v.name.lower():
            _pyttsx3_engine.setProperty("voice", v.id)
            log.info(f"pyttsx3 German voice: {v.name}")
            break
    _pyttsx3_engine.setProperty("rate", 160)
    PYTTSX3_AVAILABLE = True
    log.info("pyttsx3 available ✓")
except Exception:
    PYTTSX3_AVAILABLE = False
    log.warning("pyttsx3 not available")

# ── request model ─────────────────────────────────────────────────────────────
class SpeakRequest(BaseModel):
    text: str
    lang: str = "de"   # "de" or "en"
    voice: str = ""    # override voice name if provided

# ── helpers ───────────────────────────────────────────────────────────────────

async def _edge_tts_bytes(text: str, voice: str, rate: str = "+15%") -> bytes:
    """Synthesise text with edge-tts and return raw MP3 bytes."""
    out = io.BytesIO()
    communicate = edge_tts.Communicate(text, voice, rate=rate)
    async for chunk in communicate.stream():
        if chunk["type"] == "audio":
            out.write(chunk["data"])
    data = out.getvalue()
    if not data:
        raise RuntimeError("edge-tts returned no audio")
    return data


def _pyttsx3_wav_bytes(text: str) -> bytes:
    """Synthesise text with pyttsx3 and return WAV bytes."""
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
        tmp = f.name
    try:
        _pyttsx3_engine.save_to_file(text, tmp)
        _pyttsx3_engine.runAndWait()
        with open(tmp, "rb") as f:
            return f.read()
    finally:
        try:
            os.unlink(tmp)
        except OSError:
            pass

# ── routes ────────────────────────────────────────────────────────────────────

@app.get("/health")
def health():
    return {
        "status": "ok",
        "edge_tts": EDGE_AVAILABLE,
        "pyttsx3": PYTTSX3_AVAILABLE,
        "default_voice_de": VOICE_DE,
    }


@app.post("/speak")
async def speak(req: SpeakRequest):
    if not req.text.strip():
        raise HTTPException(400, "text is empty")

    voice = req.voice or (VOICE_DE if req.lang == "de" else VOICE_EN)
    rate  = RATE_DE if req.lang == "de" else "+10%"

    # ── try edge-tts first ────────────────────────────────────────────────────
    if EDGE_AVAILABLE:
        try:
            audio = await _edge_tts_bytes(req.text, voice, rate=rate)
            log.info(f"[edge-tts] synthesised {len(req.text)} chars ({voice} {rate})")
            return Response(content=audio, media_type="audio/mpeg")
        except Exception as e:
            log.warning(f"[edge-tts] failed: {e} — falling back to pyttsx3")

    # ── pyttsx3 fallback ──────────────────────────────────────────────────────
    if PYTTSX3_AVAILABLE:
        try:
            audio = _pyttsx3_wav_bytes(req.text)
            log.info(f"[pyttsx3] synthesised {len(req.text)} chars")
            return Response(content=audio, media_type="audio/wav")
        except Exception as e:
            log.error(f"[pyttsx3] failed: {e}")

    raise HTTPException(503, "No TTS engine available. "
                              "Install edge-tts: pip install edge-tts")


# ── entry point ───────────────────────────────────────────────────────────────
if __name__ == "__main__":
    log.info("Doctor TTS server starting on http://localhost:7866")
    log.info(f"  Primary voice (DE): {VOICE_DE}")
    log.info(f"  Primary voice (EN): {VOICE_EN}")
    uvicorn.run(app, host="0.0.0.0", port=7866)

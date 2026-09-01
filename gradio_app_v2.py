"""
gradio_app_v2.py — German Medical VR Language Learning
=======================================================
Tab 1  LEARNER MODE : 2 fixed rooms, NPC bilingual chat
Tab 2  BUILDER MODE : AI generates a variation of Doctor or Reception room

Run:  "E:\Python\python.exe" gradio_app_v2.py
"""

import base64
import json
import os
import re
import tempfile
import time
import requests
import gradio as gr


def _atomic_json_write(path: str, data: dict) -> None:
    """Write JSON atomically so Unity never reads a half-written (empty) file.

    Python truncates the file to 0 bytes as soon as open("w") is called,
    which can cause Unity to read an empty file if its 1-second poll fires
    between the truncate and the completed json.dump.  Writing to a temp
    file first and then renaming with os.replace() is an atomic swap on
    both Windows (NTFS, same drive) and Linux, so Unity always sees either
    the old complete file or the new complete file — never an empty one.
    """
    dir_path = os.path.dirname(os.path.abspath(path))
    os.makedirs(dir_path, exist_ok=True)
    fd, tmp_path = tempfile.mkstemp(dir=dir_path, suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        os.replace(tmp_path, path)
    except Exception:
        try:
            os.unlink(tmp_path)
        except OSError:
            pass
        raise

# ── Color fetcher (optional — graceful fallback if module not found) ────────────
try:
    from color_fetcher import randomize_room_colors, apply_colors_to_config
    _COLOR_FETCHER_OK = True
except ImportError:
    _COLOR_FETCHER_OK = False

# ── Room AI Generator (optional — graceful fallback if module not found) ─────────
try:
    from room_ai_generator import RoomAIGenerator
    _ROOM_AI_OK = True
except ImportError:
    _ROOM_AI_OK = False

_room_ai_gen = None

def _get_room_ai():
    global _room_ai_gen
    if _room_ai_gen is None and _ROOM_AI_OK:
        _room_ai_gen = RoomAIGenerator()
    return _room_ai_gen

def _parse_prim_calls(cs_code):
    """Extract Prim() objects from generated C# code.
    Handles both new Color(r,g,b) and Unity named colors (Color.black, etc.)."""
    import re

    # Unity named color → RGB mapping
    _NAMED = {
        'black':(0,0,0), 'white':(1,1,1), 'red':(1,0,0), 'green':(0,0.5,0),
        'blue':(0,0,1), 'yellow':(1,0.92,0.016), 'cyan':(0,1,1),
        'magenta':(1,0,1), 'gray':(0.5,0.5,0.5), 'grey':(0.5,0.5,0.5),
        'clear':(0,0,0), 'brown':(0.55,0.27,0.07), 'orange':(1,0.65,0),
    }

    def _f(s): return float(s.rstrip('f'))

    num  = r'[-\d.]+f?'
    vec3 = rf'new Vector3\(\s*({num})\s*,\s*({num})\s*,\s*({num})\s*\)'
    # Color: either new Color(r,g,b[,a]) or Color.name
    col  = (rf'(?:new Color\(\s*({num})\s*,\s*({num})\s*,\s*({num})'
            rf'(?:\s*,\s*{num})?\s*\)|Color\.(\w+))')

    pat = (r'Prim\(\s*PrimitiveType\.(\w+),\s*"([^"]+)",\s*'
           + vec3 + r',\s*' + vec3 + r',\s*' + col + r'\s*\)')

    objs = []
    for m in re.finditer(pat, cs_code):
        try:
            # groups: 1=type 2=name 3-5=pos 6-8=scale 9-11=new Color 12=named color
            if m.group(9) is not None:
                color = [_f(m.group(9)), _f(m.group(10)), _f(m.group(11))]
            else:
                color = list(_NAMED.get(m.group(12).lower(), (0.5, 0.5, 0.5)))
            objs.append({
                'type':  m.group(1),
                'name':  m.group(2),
                'pos':   [_f(m.group(3)), _f(m.group(4)), _f(m.group(5))],
                'scale': [_f(m.group(6)), _f(m.group(7)), _f(m.group(8))],
                'color': color,
            })
        except Exception:
            pass
    return objs


def _make_3d_previews_html(new_objs, request_text):
    """Two-panel 3D preview rendered server-side with matplotlib → base64 PNG."""
    import io as _io, base64 as _b64
    label = request_text[:40]

    try:
        import matplotlib
        matplotlib.use('Agg')          # must be before pyplot import; safe to call repeatedly
        import matplotlib.pyplot as _plt
        import matplotlib as _mpl
        from mpl_toolkits.mplot3d.art3d import Poly3DCollection as _P3C
        _plt.close('all')              # clear any leaked figures from previous calls
    except ImportError:
        return "<p style='color:#aaa;padding:12px'>Install matplotlib: <code>pip install matplotlib</code></p>"

    def _box_faces(ux, uy, uz, w, h, d):
        mx, my, mz = ux, uz, uy
        x0,x1 = mx-w/2, mx+w/2
        y0,y1 = my-d/2, my+d/2
        z0,z1 = mz-h/2, mz+h/2
        return [
            [[x0,y0,z0],[x1,y0,z0],[x1,y1,z0],[x0,y1,z0]],
            [[x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1]],
            [[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1]],
            [[x0,y1,z0],[x1,y1,z0],[x1,y1,z1],[x0,y1,z1]],
            [[x0,y0,z0],[x0,y1,z0],[x0,y1,z1],[x0,y0,z1]],
            [[x1,y0,z0],[x1,y1,z0],[x1,y1,z1],[x1,y0,z1]],
        ]

    def _add_box(ax, ux, uy, uz, w, h, d, color, alpha=1.0, ec='none'):
        fc = tuple(min(1.0, max(0.0, c)) for c in color[:3])
        try:
            ax.add_collection3d(_P3C(_box_faces(ux,uy,uz,w,h,d),
                                      alpha=alpha, facecolor=fc,
                                      edgecolor=ec, linewidth=0.4))
        except Exception:
            pass

    def _style(ax):
        try:
            ax.set_xlabel(''); ax.set_ylabel(''); ax.set_zlabel('')
            ax.set_xticklabels([]); ax.set_yticklabels([]); ax.set_zticklabels([])
            for pane in (ax.xaxis.pane, ax.yaxis.pane, ax.zaxis.pane):
                pane.set_facecolor('#1e2438'); pane.set_alpha(0.5)
            ax.grid(True, color='#2a3050', linewidth=0.5, alpha=0.3)
        except Exception:
            pass

    EXISTING = [
        {'pos':[0,-1.74,0.5], 'scale':[5,0.1,1.8],  'color':[0.45,0.28,0.12]},
        {'pos':[0,-1.9,2.2],  'scale':[0.6,0.5,0.6], 'color':[0.12,0.12,0.12]},
        {'pos':[-0.8,-1.9,-1.3],'scale':[0.6,0.5,0.6],'color':[0.15,0.2,0.55]},
        {'pos':[0.8,-1.9,-1.3], 'scale':[0.6,0.5,0.6],'color':[0.15,0.2,0.55]},
        {'pos':[0,-0.5,4.6],  'scale':[2.5,3.5,0.4], 'color':[0.5,0.3,0.1]},
        {'pos':[-4.8,0.5,-1], 'scale':[0.1,2,1.5],   'color':[0.8,0.85,0.9]},
        {'pos':[-4,-1.8,-4.2],'scale':[0.8,1.2,0.6], 'color':[0.5,0.5,0.55]},
    ]

    BG = '#12161f'
    fig = None
    try:
        fig = _plt.figure(figsize=(9, 3.5), facecolor=BG)   # slightly smaller = less memory

        # ── Panel 1: object-only view ─────────────────────────────────────────
        ROOM_SCALE = 4.0
        obj_only = [o for o in new_objs if max(o['scale']) < ROOM_SCALE] or new_objs

        ax1 = fig.add_subplot(121, projection='3d')
        ax1.set_facecolor('#1a1f2e')
        if obj_only:
            n = len(obj_only)
            cx = sum(o['pos'][0] for o in obj_only)/n
            cy = sum(o['pos'][1] for o in obj_only)/n
            cz = sum(o['pos'][2] for o in obj_only)/n
            for o in obj_only:
                _add_box(ax1, o['pos'][0]-cx, o['pos'][1]-cy, o['pos'][2]-cz,
                         o['scale'][0], o['scale'][1], o['scale'][2], o['color'], 0.92, '#555')
            max_ext = max(
                max(abs(o['pos'][dim] - [cx,cy,cz][dim]) + o['scale'][dim]/2
                    for dim in range(3))
                for o in obj_only
            )
            lim = max(0.6, max_ext * 1.5)
            min_y = min((o['pos'][1]-cy) - o['scale'][1]/2 for o in obj_only)
            _add_box(ax1, 0, min_y-0.08, 0, lim*1.5, 0.04, lim*1.5, [0.18,0.22,0.32], 0.3)
            ax1.set_xlim(-lim, lim); ax1.set_ylim(-lim, lim); ax1.set_zlim(-lim, lim)
        ax1.set_title(label, color='#9999cc', fontsize=8, pad=4)
        ax1.view_init(elev=22, azim=35)
        _style(ax1)

        # ── Panel 2: full room view ───────────────────────────────────────────
        ax2 = fig.add_subplot(122, projection='3d')
        ax2.set_facecolor('#1a1f2e')
        _add_box(ax2, 0,-2.53,0, 10,0.05,10, [0.2,0.4,0.5], 0.3)
        for o in EXISTING:
            _add_box(ax2, o['pos'][0],o['pos'][1],o['pos'][2],
                     o['scale'][0],o['scale'][1],o['scale'][2], o['color'], 0.45,'#222')
        for o in new_objs:
            _add_box(ax2, o['pos'][0],o['pos'][1],o['pos'][2],
                     o['scale'][0],o['scale'][1],o['scale'][2], o['color'], 0.95,'#ffee44')
        ax2.set_xlim(-5.5,5.5); ax2.set_ylim(-5.5,5.5); ax2.set_zlim(-3,3)
        ax2.set_title('Room placement  (new = yellow outline)', color='#9999cc', fontsize=8, pad=4)
        ax2.view_init(elev=28, azim=-40)
        _style(ax2)

        _plt.subplots_adjust(left=0.01, right=0.99, top=0.92, bottom=0.02, wspace=0.05)
        buf = _io.BytesIO()
        _plt.savefig(buf, format='png', facecolor=BG, dpi=90, bbox_inches='tight')
        buf.seek(0)
        b64 = _b64.b64encode(buf.read()).decode()
        return f'<div style="background:#0f1117;border-radius:12px;padding:8px"><img src="data:image/png;base64,{b64}" style="width:100%;border-radius:8px;display:block" alt="3D preview"/></div>'

    except Exception as e:
        return f"<p style='color:#e88;padding:10px'>Preview error: {e}</p>"
    finally:
        if fig is not None:
            _plt.close(fig)
        _plt.close('all')   # safety net


def room_ai_generate(request: str):
    _dd = lambda: gr.Dropdown(choices=_obj_choices(), value=None)
    if not request.strip():
        return ("⚠️ Please describe what to add.",
                "<p style='color:#666'>No output yet.</p>", "",
                _obj_list_html(), _dd())
    gen = _get_room_ai()
    if gen is None:
        return ("❌ room_ai_generator.py not found.", "", "",
                _obj_list_html(), _dd())
    ok, status, cs, _fname = gen.generate(request.strip())
    if not ok or not cs:
        return status, "<p style='color:#e55'>Generation failed.</p>", cs, _obj_list_html(), _dd()
    objs = _parse_prim_calls(cs)
    preview_html = _make_3d_previews_html(objs, request.strip()) if objs else \
        "<p style='color:#888;padding:10px'>No Prim() objects parsed from output.</p>"
    return status, preview_html, cs, _obj_list_html(), _dd()

# ── Generated-object file management ─────────────────────────────────────────

# Base room scripts that contain the marker but are NOT user-generated objects
_OBJ_BASE_SCRIPTS = {'RoomAIAdditions', 'GameBootstrap', 'DoctorEmptyRoom', 'DoctorRoomDecorator'}

def _obj_files():
    """Return all AI-generated .cs files by scanning for the marker comment."""
    from pathlib import Path
    workspace = Path(__file__).parent
    found = []
    for p in sorted(workspace.glob("*.cs")):
        if p.stem in _OBJ_BASE_SCRIPTS:
            continue
        try:
            if "auto-generated by Room AI" in p.read_text(encoding="utf-8", errors="ignore"):
                found.append(p)
        except Exception:
            pass
    return found

def _obj_list_html():
    from pathlib import Path
    try:
        from room_ai_generator import UNITY_ASSETS
        ua = UNITY_ASSETS
    except Exception:
        ua = Path("__nonexistent__")
    files = _obj_files()
    if not files:
        return "<p style='color:#7ab;padding:10px'>✅ No AI-generated objects — room is clean.</p>"
    rows = ""
    for p in files:
        icon = "🟢" if (ua / p.name).exists() else "🔴"
        rows += (
            f"<tr>"
            f"<td style='padding:5px 10px;color:#ccd'>{p.name}</td>"
            f"<td style='text-align:center;padding:5px 4px'>{icon}</td>"
            f"</tr>"
        )
    return (
        "<table style='width:100%;border-collapse:collapse;font-size:13px'>"
        "<thead><tr>"
        "<th style='text-align:left;padding:5px 10px;color:#99aacc;border-bottom:1px solid #334'>Object file</th>"
        "<th style='padding:5px 4px;color:#99aacc;border-bottom:1px solid #334'>Unity</th>"
        "</tr></thead>"
        f"<tbody>{rows}</tbody></table>"
        "<p style='color:#556;font-size:11px;margin:4px 10px'>🟢 in Unity Assets &nbsp;🔴 workspace only</p>"
    )

def _obj_delete(fname: str):
    from pathlib import Path
    if not fname:
        return "⚠️ No object specified.", _obj_list_html()
    try:
        from room_ai_generator import UNITY_ASSETS
        ua = UNITY_ASSETS
    except Exception:
        ua = Path("__nonexistent__")
    removed = []
    ws = Path(__file__).parent / fname
    if ws.exists():
        ws.unlink(); removed.append("workspace")
    if (ua / fname).exists():
        (ua / fname).unlink(); removed.append("Unity Assets")
    meta = ua / (fname + ".meta")
    if meta.exists():
        meta.unlink()
    msg = (f"🗑️ Deleted `{fname}` from {' + '.join(removed)}. Unity will recompile."
           if removed else f"⚠️ `{fname}` not found.")
    return msg, _obj_list_html()

def _obj_choices():
    """Sorted list of AI-generated file names for the delete dropdown."""
    return [p.name for p in _obj_files()]

def _obj_delete_selected(fname: str):
    """Delete selected object and return updated status + table + dropdown."""
    if not fname:
        return "⚠️ Select an object first.", _obj_list_html(), gr.Dropdown(choices=_obj_choices())
    msg, html = _obj_delete(fname)
    return msg, html, gr.Dropdown(choices=_obj_choices(), value=None)

# ── Whisper (lazy-loaded on first recording — avoids slowing Gradio startup) ────
_whisper_model = None

def _get_whisper():
    global _whisper_model
    if _whisper_model is None:
        try:
            import whisper as _w
            print("[Whisper] Loading model 'base'…")
            _whisper_model = _w.load_model("base")
            print("[Whisper] Model ready.")
        except ImportError:
            print("[Whisper] openai-whisper not installed — run install_whisper.bat")
    return _whisper_model

def transcribe_and_append(audio_path, existing_text):
    """Transcribe mic recording with Whisper and append to the compose box."""
    if audio_path is None:
        return existing_text or ""
    model = _get_whisper()
    if model is None:
        return (existing_text or "") + "\n[Whisper not installed — run install_whisper.bat]"
    try:
        result = model.transcribe(audio_path, fp16=False)
        new_text = result.get("text", "").strip()
        if new_text:
            ex = (existing_text or "").strip()
            return (ex + " " + new_text).strip() if ex else new_text
    except Exception as e:
        print(f"[Whisper] Error: {e}")
    return existing_text or ""

def drop_last_word(text):
    """Remove the last word from the compose text."""
    text = (text or "").rstrip()
    i = text.rfind(" ")
    return "" if i < 0 else text[:i]

# ── Config ─────────────────────────────────────────────────────────────────────
OUTPUT_DIR   = r"E:\AI_HDRI_Unity"
CONFIG_FILE  = os.path.join(OUTPUT_DIR, "room_config.json")
OLLAMA_URL   = "http://localhost:11434/api/generate"
OLLAMA_MODEL = "llama3.2:3b"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# ── Fixed room objects (same every room) ────────────────────────────────────────
FIXED_OBJECTS = {
    "corner1": {"type": "flower_pot",   "german": "der Blumentopf",    "english": "flower pot"},
    "corner2": {"type": "plant",        "german": "die Zimmerpflanze", "english": "plant"},
    "corner3": {"type": "water_cooler", "german": "der Wasserspender", "english": "water cooler"},
    "corner4": {"type": "trash_bin",    "german": "der Mülleimer",     "english": "bin"},
    "table":         {"german": "der Tisch",          "english": "table"},
    "chair_doctor":  {"german": "der Arztstuhl",      "english": "doctor's chair"},
    "chair_patient": {"german": "der Patientenstuhl", "english": "patient's chair"},
    "door":          {"german": "die Tür",             "english": "door"},
}

# ── Learner preset rooms ────────────────────────────────────────────────────────
LEARNER_PRESETS = {

    "🏥 Arztpraxis (Doctor's Office)": {
        "room_type": "Arztpraxis",
        "palette":   {"wall": "#C8E6F5", "floor": "#5A7A8A", "ceiling": "#D8EEF8"},
        "npc": {
            "name":    "Dr. Müller",
            "role":    "Hausarzt",
            "context": ("Du bist Dr. Müller, Hausarzt in einer deutschen Arztpraxis. "
                        "Du hilfst Einwanderern, medizinisches Deutsch zu lernen."),
        },
        "lesson": {
            "topic": "Beim Hausarzt",
            "phrases": [
                {"de": "Ich habe einen Termin.",             "en": "I have an appointment."},
                {"de": "Wo tut es weh?",                    "en": "Where does it hurt?"},
                {"de": "Ich bin krank.",                    "en": "I am sick."},
                {"de": "Ich habe Fieber.",                  "en": "I have a fever."},
                {"de": "Ich brauche ein Rezept.",           "en": "I need a prescription."},
                {"de": "Können Sie das bitte wiederholen?", "en": "Can you please repeat that?"},
            ],
        },
        "objects": dict(FIXED_OBJECTS),
    },

    "🗂️ Rezeption (Reception / Front Desk)": {
        "room_type": "Rezeption",
        "palette":   {"wall": "#B8E8D0", "floor": "#3D6B55", "ceiling": "#C8EED8"},
        "npc": {
            "name":    "Frau Schmidt",
            "role":    "Empfangsdame",
            "context": ("Du bist Frau Schmidt, Empfangsdame in einer deutschen Arztpraxis. "
                        "Du empfängst Patienten, buchst Termine und hilfst Einwanderern, "
                        "die nötigen deutschen Phrasen zu lernen."),
        },
        "lesson": {
            "topic": "An der Rezeption",
            "phrases": [
                {"de": "Guten Morgen, ich habe einen Termin.",  "en": "Good morning, I have an appointment."},
                {"de": "Wie lange muss ich warten?",            "en": "How long do I have to wait?"},
                {"de": "Bitte nehmen Sie Platz.",               "en": "Please take a seat."},
                {"de": "Haben Sie Ihre Versicherungskarte?",    "en": "Do you have your insurance card?"},
                {"de": "Bitte füllen Sie dieses Formular aus.", "en": "Please fill in this form."},
                {"de": "Der Arzt ist gleich für Sie da.",       "en": "The doctor will see you shortly."},
            ],
        },
        "objects": dict(FIXED_OBJECTS),
    },
}

# Builder mode: AI only generates variations of these 2 room types
BUILDER_ROOM_TYPES = {
    "🏥 Arztpraxis (Doctor's Office)": {
        "base_npc_role": "Hausarzt",
        "wall_query_default": "doctor office medical room",
    },
    "🗂️ Rezeption (Reception / Front Desk)": {
        "base_npc_role": "Empfangsdame",
        "wall_query_default": "clinic reception desk modern",
    },
}

# ── Colour palettes ─────────────────────────────────────────────────────────────
import random as _random
import colorsys as _colorsys

# Each scheme: (wall_hex, ceiling_hex, floor_hex)
# Rule: wall=bright pastel, ceiling=slightly lighter wall, floor=clearly darker
ROOM_SCHEMES = [
    # Sky blue walls
    ("#C8E6F5", "#D8EEF8", "#5A7A8A"),
    # Mint green walls
    ("#B8E8D0", "#C8EED8", "#3D6B55"),
    # Warm cream walls
    ("#F5E6CC", "#F8EDD8", "#8B6340"),
    # Lavender walls
    ("#DDD0F0", "#E6D8F5", "#6A5580"),
    # Dusty rose walls
    ("#F5C8CC", "#F8D0D5", "#8B4550"),
    # Teal walls
    ("#B0DCE0", "#C0E4E8", "#2E6870"),
    # Sage green walls
    ("#C4D8B8", "#CDE0C3", "#4A6640"),
    # Peach walls
    ("#F5D4B8", "#F8DCC5", "#8B5A35"),
    # Soft yellow walls
    ("#F0E8B0", "#F5EEC0", "#7A6820"),
    # Powder blue walls
    ("#C0D0F0", "#CCD8F5", "#3A4A80"),
    # Coral walls
    ("#F5C4B0", "#F8CEBC", "#8B4030"),
    # Steel blue walls
    ("#B8C8E8", "#C5D2EE", "#384870"),
    # Warm grey walls
    ("#DDD8D0", "#E5E0D8", "#605850"),
    # Seafoam walls
    ("#B5E0D8", "#C0E8E0", "#2A6860"),
    # Blush pink walls
    ("#F0D0D8", "#F5D8DE", "#804050"),
]

def _random_room_palette():
    """Pick a random scheme — ensures floor is always darker than wall/ceiling."""
    scheme = _random.choice(ROOM_SCHEMES)
    return {"wall": scheme[0], "ceiling": scheme[1], "floor": scheme[2]}

WALL_SUGGESTIONS = [s[0] for s in ROOM_SCHEMES]

# ── TTS helper ───────────────────────────────────────────────────────────────────

TTS_SERVER_URL = "http://localhost:7866/speak"

def _tts_speak(text: str, lang: str = "de") -> str | None:
    """Call tts_server.py, save audio to a temp file, return the file path.
    Returns None silently if the server is not running or is too slow."""
    if not text.strip():
        return None
    try:
        resp = requests.post(TTS_SERVER_URL,
                             json={"text": text, "lang": lang},
                             timeout=12)
        if resp.status_code == 200:
            ct  = resp.headers.get("Content-Type", "")
            ext = "wav" if "wav" in ct else "mp3"
            fd, tmp_path = tempfile.mkstemp(suffix=f".{ext}")
            with os.fdopen(fd, "wb") as f:
                f.write(resp.content)
            return tmp_path
    except Exception:
        pass
    return None

def _tts_audio_html(text: str, lang: str = "de") -> str:
    """Use the browser's built-in Web Speech API to speak text.
    - speechSynthesis.cancel() stops any currently-playing speech → no echo.
    - No audio element, no base64, no server round-trip needed in the browser.
    - Chrome/Edge on Windows use Microsoft neural voices (same engine as edge-tts).
    Falls back to edge-tts audio element if Web Speech is unavailable.
    """
    if not text.strip():
        return ""

    import json as _json
    text_js  = _json.dumps(text)          # safely escape quotes / newlines
    lang_code = "de-DE" if lang == "de" else "en-US"
    uid       = abs(hash(text)) % 9999999

    # Web Speech API approach — cancel previous, then speak new text
    return f"""<span id="npc_tts_{uid}" style="display:none"></span>
<script>
(function(){{
  if(!window.speechSynthesis) return;
  window.speechSynthesis.cancel();          // stop anything currently playing
  var txt  = {text_js};
  var lang = "{lang_code}";
  function doSpeak(){{
    var u = new SpeechSynthesisUtterance(txt);
    u.lang = lang;
    u.rate = 1.1;
    var voices = window.speechSynthesis.getVoices();
    // Prefer a natural male German voice (Microsoft David / Hedda / Stefan / Conrad)
    var pick = voices.filter(function(v){{
      return v.lang.startsWith('de') &&
             /male|david|stefan|hedda|michael|hans|franz/i.test(v.name);
    }});
    if(!pick.length) pick = voices.filter(function(v){{ return v.lang.startsWith('de'); }});
    if(pick.length) u.voice = pick[0];
    window.speechSynthesis.speak(u);
  }}
  // getVoices() is async on first call — wait for list if needed
  var v = window.speechSynthesis.getVoices();
  if(v.length) doSpeak();
  else window.speechSynthesis.addEventListener('voiceschanged', doSpeak, {{once:true}});
}})();
</script>"""

def _extract_for_tts(reply: str) -> str:
    """Collect spoken German content from the NPC reply.

    Rules:
    • Skip lines that contain 🇬🇧 (English translation lines)
    • Skip parenthetical vocab notes like (Übersetzung: …)
    • Remove 🇩🇪 flag prefix and surrounding quotes
    • Keep all other German lines (intro sentences, questions, etc.)
    """
    import re
    parts = []
    for line in reply.split("\n"):
        s = line.strip()
        if not s:
            continue
        if "🇬🇧" in s:
            continue                          # skip English lines
        if re.match(r"^\(.*\)$", s):
            continue                          # skip parenthetical notes
        s = s.replace("🇩🇪", "").strip()     # strip German flag
        s = re.sub(r'^["“„]|["”„]$', '', s).strip()  # strip surrounding quotes
        if s:
            parts.append(s)
    return " ".join(parts)[:700]


# ── Ollama helpers ──────────────────────────────────────────────────────────────

def ollama_available():
    try:
        return requests.get("http://localhost:11434/api/tags", timeout=2).status_code == 200
    except:
        return False

def ollama_model_ready(model: str) -> bool:
    try:
        r = requests.get("http://localhost:11434/api/tags", timeout=2)
        if r.status_code == 200:
            return any(m.get("name","").startswith(model.split(":")[0])
                       for m in r.json().get("models", []))
    except:
        pass
    return False

def _content_str(content) -> str:
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        parts = []
        for b in content:
            parts.append(b.get("text", b.get("content", str(b))) if isinstance(b, dict) else str(b))
        return " ".join(parts)
    return str(content)

def ollama_chat_stream(system_prompt, user_message, history):
    messages = [{"role": "system", "content": _content_str(system_prompt)}]
    for u, a in history:
        messages.append({"role": "user",      "content": _content_str(u)})
        messages.append({"role": "assistant", "content": _content_str(a)})
    messages.append({"role": "user", "content": _content_str(user_message)})

    try:
        with requests.post("http://localhost:11434/api/chat",
                           json={"model": OLLAMA_MODEL, "messages": messages,
                                 "stream": True, "options": {"num_ctx": 4096}},
                           stream=True, timeout=120) as resp:
            if resp.status_code != 200:
                yield f"⚠️ Ollama HTTP {resp.status_code}: {resp.text[:200]}"
                return
            for line in resp.iter_lines():
                if line:
                    try:
                        data = json.loads(line)
                        if data.get("error"):
                            yield f"⚠️ {data['error']}"; return
                        token = data.get("message", {}).get("content", "")
                        if token:
                            yield token
                        if data.get("done"):
                            break
                    except json.JSONDecodeError:
                        continue
    except requests.exceptions.Timeout:
        yield "⚠️ Ollama timed out. Try again."
    except Exception as e:
        yield f"⚠️ Ollama error: {e}"

# ── 3D Room Preview JS (injected via gr.Blocks js= so it runs in the real page) ─
# Svelte {#html} strips <script> tags and <iframe> — so we put all JS here and
# pass color data through data-* attributes on the canvas element.

ROOM_PREVIEW_JS = """
() => {
  function hexRgb(h){
    h=h.replace('#','');
    if(h.length===3)h=h[0]+h[0]+h[1]+h[1]+h[2]+h[2];
    return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];
  }
  function shadeCol(hex,f){
    var c=hexRgb(hex);
    return 'rgb('+Math.round(Math.min(255,c[0]*f))+','+Math.round(Math.min(255,c[1]*f))+','+Math.round(Math.min(255,c[2]*f))+')';
  }
  function nrm(v){var l=Math.sqrt(v[0]*v[0]+v[1]*v[1]+v[2]*v[2]);return l<1e-9?[0,0,1]:[v[0]/l,v[1]/l,v[2]/l];}
  function crs(a,b){return[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];}
  function dot(a,b){return a[0]*b[0]+a[1]*b[1]+a[2]*b[2];}
  function sub(a,b){return[a[0]-b[0],a[1]-b[1],a[2]-b[2]];}

  function initRoomCanvas(canvas){
    if(canvas.__roomInit) return;
    canvas.__roomInit=true;
    var ctx=canvas.getContext('2d');
    var CW=canvas.width, CH=canvas.height;
    var WALL=canvas.dataset.wall, FLOOR=canvas.dataset.floor, CEIL=canvas.dataset.ceil;
    var W=2.5,H=2.0,D=2.5;
    var angle=0.5,tilt=0.28,radius=5.5;
    var drag=false,lx=0,ly=0,spin=true;

    function prj(pt,cam,R,U,F){
      var d=sub(pt,cam);
      var cx=dot(d,R),cy=dot(d,U),cz=dot(d,F);
      if(cz<0.05)return null;
      return{sx:CW/2+cx/cz*380,sy:CH/2-cy/cz*380,d:cz};
    }

    var faces=[
      {c:[[-W,-H,-D],[W,-H,-D],[W,-H,D],[-W,-H,D]],n:[0,1,0],ctr:[0,-H,0],col:FLOOR,f:1.0,a:1.0,al:true},
      {c:[[-W,-H,-D],[-W,H,-D],[W,H,-D],[W,-H,-D]],n:[0,0,1],ctr:[0,0,-D],col:WALL,f:0.78,a:1.0,al:false},
      {c:[[-W,-H,-D],[-W,-H,D],[-W,H,D],[-W,H,-D]],n:[1,0,0],ctr:[-W,0,0],col:WALL,f:0.68,a:1.0,al:false},
      {c:[[W,-H,-D],[W,H,-D],[W,H,D],[W,-H,D]],n:[-1,0,0],ctr:[W,0,0],col:WALL,f:0.90,a:1.0,al:false},
      {c:[[W,-H,D],[W,H,D],[-W,H,D],[-W,-H,D]],n:[0,0,-1],ctr:[0,0,D],col:WALL,f:0.75,a:0.10,al:true},
      {c:[[-W,H,-D],[W,H,-D],[W,H,D],[-W,H,D]],n:[0,-1,0],ctr:[0,H,0],col:CEIL,f:0.92,a:0.80,al:true}
    ];

    function draw(){
      ctx.clearRect(0,0,CW,CH);
      ctx.fillStyle='#0f1117';ctx.fillRect(0,0,CW,CH);
      var cam=[Math.sin(angle)*Math.cos(tilt)*radius,Math.sin(tilt)*radius,Math.cos(angle)*Math.cos(tilt)*radius];
      var F=nrm(sub([0,0,0],cam));
      var wu=Math.abs(F[1])>0.98?[0,0,1]:[0,1,0];
      var R=nrm(crs(F,wu)),U=nrm(crs(R,F));
      var todo=[];
      faces.forEach(function(face){
        var vis=dot(face.n,sub(cam,face.ctr))>0;
        if(!vis&&!face.al)return;
        var al=(!vis&&face.al)?Math.min(face.a,0.12):face.a;
        var sd=0,cnt=0;
        var pts=face.c.map(function(pt){var p=prj(pt,cam,R,U,F);if(p){sd+=p.d;cnt++;}return p;});
        if(!cnt)return;
        todo.push({face:face,pts:pts,avgD:sd/cnt,al:al});
      });
      todo.sort(function(a,b){return b.avgD-a.avgD;});
      todo.forEach(function(item){
        var pts=item.pts;
        if(pts.some(function(p){return!p;}))return;
        ctx.save();ctx.globalAlpha=item.al;
        ctx.beginPath();ctx.moveTo(pts[0].sx,pts[0].sy);
        for(var i=1;i<pts.length;i++)ctx.lineTo(pts[i].sx,pts[i].sy);
        ctx.closePath();
        ctx.fillStyle=shadeCol(item.face.col,item.face.f);ctx.fill();
        ctx.strokeStyle='rgba(0,0,0,0.10)';ctx.lineWidth=0.7;ctx.stroke();
        ctx.restore();
      });
    }

    canvas.addEventListener('mousedown',function(e){drag=true;spin=false;lx=e.clientX;ly=e.clientY;canvas.style.cursor='grabbing';e.preventDefault();});
    document.addEventListener('mouseup',function(){drag=false;canvas.style.cursor='grab';});
    document.addEventListener('mousemove',function(e){
      if(!drag)return;
      angle-=(e.clientX-lx)*0.012;
      tilt=Math.max(-0.5,Math.min(1.3,tilt+(e.clientY-ly)*0.008));
      lx=e.clientX;ly=e.clientY;
    });
    canvas.addEventListener('wheel',function(e){radius=Math.max(2.5,Math.min(11,radius+e.deltaY*0.012));e.preventDefault();},{passive:false});
    canvas.addEventListener('touchstart',function(e){spin=false;drag=true;lx=e.touches[0].clientX;ly=e.touches[0].clientY;},{passive:true});
    canvas.addEventListener('touchend',function(){drag=false;});
    canvas.addEventListener('touchmove',function(e){
      if(!drag)return;
      angle-=(e.touches[0].clientX-lx)*0.012;
      tilt=Math.max(-0.5,Math.min(1.3,tilt+(e.touches[0].clientY-ly)*0.008));
      lx=e.touches[0].clientX;ly=e.touches[0].clientY;e.preventDefault();
    },{passive:false});

    (function loop(){if(spin&&!drag)angle+=0.006;draw();requestAnimationFrame(loop);})();
  }

  function scanForCanvases(root){
    var canvases=(root&&root.querySelectorAll)?root.querySelectorAll('canvas[data-room]'):[];
    canvases.forEach(initRoomCanvas);
    if(root&&root.tagName==='CANVAS'&&root.dataset&&root.dataset.room) initRoomCanvas(root);
  }

  // Watch for dynamically added room canvases (Gradio updates gr.HTML via DOM mutation)
  var obs=new MutationObserver(function(mutations){
    mutations.forEach(function(m){
      m.addedNodes.forEach(function(node){
        if(node.nodeType===1) scanForCanvases(node);
      });
    });
  });
  obs.observe(document.body,{childList:true,subtree:true});

  // Scan existing elements (e.g. if page already has a canvas on load)
  scanForCanvases(document);
}
"""

# ── 3D Room Preview HTML (canvas only — JS lives in ROOM_PREVIEW_JS above) ──────

def make_room_preview_html(palette: dict, room_type: str) -> str:
    """
    Build the 3D room preview.

    img onerror trick: Svelte's {#html} does NOT execute <script> tags,
    but it DOES register DOM event handlers (onerror, onload, etc.).
    We base64-encode the full JS so there are zero quoting conflicts,
    then decode + execute it via createElement('script') inside onerror.
    """
    wall    = palette.get("wall",    "#D4E9F7")
    floor_c = palette.get("floor",   "#C8A96E")
    ceiling = palette.get("ceiling", "#F8F8F0")
    uid     = str(abs(hash(room_type + wall + floor_c)))[:8]

    # ── Canvas 2D perspective renderer ────────────────────────────────
    # All JS braces must be doubled for Python f-string.
    js = f"""(function(){{
  var c=document.getElementById('c{uid}');
  if(!c||c.__ri)return; c.__ri=true;
  var ctx=c.getContext('2d');
  var CW=c.width,CH=c.height;
  var WALL='{wall}',FLOOR='{floor_c}',CEIL='{ceiling}';
  var W=2.5,H=2.0,D=2.5,angle=0.5,tilt=0.28,radius=5.5;
  var drag=false,lx=0,ly=0,spin=true;
  function hr(h){{h=h.replace('#','');if(h.length===3)h=h[0]+h[0]+h[1]+h[1]+h[2]+h[2];return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];}}
  function sc(hex,f){{var c=hr(hex);return 'rgb('+Math.round(Math.min(255,c[0]*f))+','+Math.round(Math.min(255,c[1]*f))+','+Math.round(Math.min(255,c[2]*f))+')';}}
  function nm(v){{var l=Math.sqrt(v[0]*v[0]+v[1]*v[1]+v[2]*v[2]);return l<1e-9?[0,0,1]:[v[0]/l,v[1]/l,v[2]/l];}}
  function cr(a,b){{return[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];}}
  function dt(a,b){{return a[0]*b[0]+a[1]*b[1]+a[2]*b[2];}}
  function sb(a,b){{return[a[0]-b[0],a[1]-b[1],a[2]-b[2]];}}
  function pj(pt,cm,R,U,F){{var d=sb(pt,cm),cx=dt(d,R),cy=dt(d,U),cz=dt(d,F);if(cz<0.05)return null;return{{sx:CW/2+cx/cz*380,sy:CH/2-cy/cz*380,d:cz}};}}
  var faces=[
    {{c:[[-W,-H,-D],[W,-H,-D],[W,-H,D],[-W,-H,D]],n:[0,1,0],ctr:[0,-H,0],col:FLOOR,f:1.0,a:1.0,al:true}},
    {{c:[[-W,-H,-D],[-W,H,-D],[W,H,-D],[W,-H,-D]],n:[0,0,1],ctr:[0,0,-D],col:WALL,f:0.78,a:1.0,al:false}},
    {{c:[[-W,-H,-D],[-W,-H,D],[-W,H,D],[-W,H,-D]],n:[1,0,0],ctr:[-W,0,0],col:WALL,f:0.68,a:1.0,al:false}},
    {{c:[[W,-H,-D],[W,H,-D],[W,H,D],[W,-H,D]],n:[-1,0,0],ctr:[W,0,0],col:WALL,f:0.90,a:1.0,al:false}},
    {{c:[[W,-H,D],[W,H,D],[-W,H,D],[-W,-H,D]],n:[0,0,-1],ctr:[0,0,D],col:WALL,f:0.75,a:0.10,al:true}},
    {{c:[[-W,H,-D],[W,H,-D],[W,H,D],[-W,H,D]],n:[0,-1,0],ctr:[0,H,0],col:CEIL,f:0.92,a:0.80,al:true}}
  ];
  function draw(){{
    ctx.clearRect(0,0,CW,CH);ctx.fillStyle='#0f1117';ctx.fillRect(0,0,CW,CH);
    var cam=[Math.sin(angle)*Math.cos(tilt)*radius,Math.sin(tilt)*radius,Math.cos(angle)*Math.cos(tilt)*radius];
    var F=nm(sb([0,0,0],cam)),wu=Math.abs(F[1])>0.98?[0,0,1]:[0,1,0];
    var R=nm(cr(F,wu)),U=nm(cr(R,F)),todo=[];
    faces.forEach(function(face){{
      var vis=dt(face.n,sb(cam,face.ctr))>0;
      if(!vis&&!face.al)return;
      var al=(!vis&&face.al)?Math.min(face.a,0.12):face.a,sd=0,cnt=0;
      var pts=face.c.map(function(pt){{var p=pj(pt,cam,R,U,F);if(p){{sd+=p.d;cnt++;}}return p;}});
      if(!cnt)return;
      todo.push({{face:face,pts:pts,avgD:sd/cnt,al:al}});
    }});
    todo.sort(function(a,b){{return b.avgD-a.avgD;}});
    todo.forEach(function(item){{
      var pts=item.pts;
      if(pts.some(function(p){{return!p;}}))return;
      ctx.save();ctx.globalAlpha=item.al;
      ctx.beginPath();ctx.moveTo(pts[0].sx,pts[0].sy);
      for(var i=1;i<pts.length;i++)ctx.lineTo(pts[i].sx,pts[i].sy);
      ctx.closePath();ctx.fillStyle=sc(item.face.col,item.face.f);ctx.fill();
      ctx.strokeStyle='rgba(0,0,0,0.10)';ctx.lineWidth=0.7;ctx.stroke();ctx.restore();
    }});
  }}
  c.addEventListener('mousedown',function(e){{drag=true;spin=false;lx=e.clientX;ly=e.clientY;c.style.cursor='grabbing';e.preventDefault();}});
  document.addEventListener('mouseup',function(){{drag=false;c.style.cursor='grab';}});
  document.addEventListener('mousemove',function(e){{if(!drag)return;angle-=(e.clientX-lx)*0.012;tilt=Math.max(-0.5,Math.min(1.3,tilt+(e.clientY-ly)*0.008));lx=e.clientX;ly=e.clientY;}});
  c.addEventListener('wheel',function(e){{radius=Math.max(2.5,Math.min(11,radius+e.deltaY*0.012));e.preventDefault();}},{{passive:false}});
  c.addEventListener('touchstart',function(e){{spin=false;drag=true;lx=e.touches[0].clientX;ly=e.touches[0].clientY;}},{{passive:true}});
  c.addEventListener('touchend',function(){{drag=false;}});
  c.addEventListener('touchmove',function(e){{if(!drag)return;angle-=(e.touches[0].clientX-lx)*0.012;tilt=Math.max(-0.5,Math.min(1.3,tilt+(e.touches[0].clientY-ly)*0.008));lx=e.touches[0].clientX;ly=e.touches[0].clientY;e.preventDefault();}},{{passive:false}});
  (function loop(){{if(spin&&!drag)angle+=0.006;draw();requestAnimationFrame(loop);}})();
}})();"""

    b64 = base64.b64encode(js.encode("utf-8")).decode("ascii")

    # onerror fires when the invalid img src fails — 100% reliable in {#html}
    # atob() decodes the base64 JS; createElement('script') + append executes it
    onerror = (
        f"var s=document.createElement('script');"
        f"s.textContent=atob('{b64}');"
        f"document.head.appendChild(s);"
        f"this.onerror=null;this.remove();"
    )

    chips = (
        f'<span style="background:{wall};padding:3px 10px;border-radius:20px;font-size:10px;'
        f'font-weight:700;color:#333;border:1px solid rgba(0,0,0,0.15)">Wall {wall}</span> '
        f'<span style="background:{floor_c};padding:3px 10px;border-radius:20px;font-size:10px;'
        f'font-weight:700;color:#fff;border:1px solid rgba(0,0,0,0.3)">Floor {floor_c}</span> '
        f'<span style="background:{ceiling};padding:3px 10px;border-radius:20px;font-size:10px;'
        f'font-weight:700;color:#333;border:1px solid rgba(0,0,0,0.15)">Ceiling {ceiling}</span>'
    )

    return (
        f'<div style="background:#0f1117;border-radius:12px;overflow:hidden;'
        f'font-family:sans-serif;user-select:none">'
        f'<p style="color:#6e7681;font-size:11px;margin:0;padding:6px 14px">'
        f'🏗️ {room_type} &nbsp;·&nbsp;'
        f'<span style="color:#aaa">Drag to rotate · Scroll to zoom</span></p>'
        f'<canvas id="c{uid}" width="560" height="280"'
        f' style="width:100%;height:280px;display:block;cursor:grab;background:#0f1117"></canvas>'
        f'<img src="data:image/png;base64,!" onerror="{onerror}" '
        f'style="display:none" alt="">'
        f'<div style="display:flex;gap:6px;justify-content:center;padding:6px 10px 8px;'
        f'flex-wrap:wrap">{chips}</div>'
        f'</div>'
    )

# (old srcdoc/script approaches removed — see ROOM_PREVIEW_JS above)

# ── JSON extractor ──────────────────────────────────────────────────────────────

def _extract_json(raw: str):
    for pat in [r'```json\s*([\s\S]*?)\s*```', r'```\s*([\s\S]*?)\s*```']:
        m = re.search(pat, raw)
        if m:
            try: return json.loads(m.group(1).strip())
            except: pass
    depth = 0; start = -1; in_str = False; esc = False
    for i, c in enumerate(raw):
        if esc: esc = False; continue
        if c == '\\' and in_str: esc = True; continue
        if c == '"': in_str = not in_str; continue
        if in_str: continue
        if c == '{':
            if depth == 0: start = i
            depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0 and start != -1:
                try: return json.loads(raw[start:i+1])
                except: start = -1
    print(f"[JSON] parse fail — raw: {raw[:300]}")
    return None


# ── Builder Mode: AI config generation ─────────────────────────────────────────

def build_room(room_type_key: str, description: str):
    if room_type_key not in BUILDER_ROOM_TYPES:
        yield "⚠️ Select a room type first.", "", ""
        return

    yield "⏳ Generating room with AI…", "", ""

    if not ollama_available():
        yield "⚠️ Ollama is not running. Start it: `ollama serve`", "", ""
        return

    role       = BUILDER_ROOM_TYPES[room_type_key]["base_npc_role"]
    room_label = room_type_key.split("(")[0].strip().replace("🏥","").replace("🗂️","").strip()
    style_note = description.strip() if description.strip() else "modern standard German clinic"

    # ── AI only generates NPC + lesson content; colors come from palette sites ──
    prompt = (
        f"You are configuring a German medical VR room. Output only JSON.\n"
        f"Room: {room_label}. Style: {style_note}.\n\n"
        f"Return JSON with EXACTLY these keys (strings only, no arrays):\n"
        f"npc_name, lesson_topic,\n"
        f"de1, en1, de2, en2, de3, en3, de4, en4, de5, en5, de6, en6\n\n"
        f"de1..de6 are German medical phrases, en1..en6 are their English translations.\n"
        f"Example: {{\"npc_name\":\"Dr. Bauer\",\"lesson_topic\":\"Beim Arzt\","
        f"\"de1\":\"Ich habe Kopfschmerzen.\",\"en1\":\"I have a headache.\","
        f"\"de2\":\"...\",\"en2\":\"...\","
        f"\"de3\":\"...\",\"en3\":\"...\","
        f"\"de4\":\"...\",\"en4\":\"...\","
        f"\"de5\":\"...\",\"en5\":\"...\","
        f"\"de6\":\"...\",\"en6\":\"...\"}}"
    )

    try:
        resp = requests.post(OLLAMA_URL, json={
            "model":   OLLAMA_MODEL,
            "prompt":  prompt,
            "stream":  False,
            "format":  "json",
            "options": {"num_ctx": 4096, "temperature": 0.7},
        }, timeout=120)
        raw = resp.json().get("response", "")
        print(f"[Builder] raw ({len(raw)} chars): {raw[:500]}")
        ai = _extract_json(raw)
        if ai is None:
            try:
                ai = json.loads(raw)
            except Exception:
                ai = None
    except Exception as e:
        yield f"❌ Ollama error: {e}", "", ""
        return

    if not ai:
        yield "❌ AI returned invalid JSON. Try again or simplify the description.", "", ""
        return

    # ── Colors: pick random scheme (floor always darker than wall/ceiling) ─────────
    palette = _random_room_palette()

    # ── Reconstruct phrases from flat keys ────────────────────────────────────────
    phrases = []
    for i in range(1, 7):
        de = ai.get(f"de{i}", "").strip()
        en = ai.get(f"en{i}", "").strip()
        if de and en:
            phrases.append({"de": de, "en": en})
    if not phrases:
        phrases = [
            {"de": "Ich habe einen Termin.", "en": "I have an appointment."},
            {"de": "Wo tut es weh?",         "en": "Where does it hurt?"},
        ]

    # ── Build full config ─────────────────────────────────────────────────────────
    config = {
        "room_type": room_label,
        "mode":      "builder",
        "palette":   palette,
        "objects":   dict(FIXED_OBJECTS),
        "npc": {
            "name":    ai.get("npc_name", "Dr. Müller"),
            "role":    role,
            "context": f"Du bist {ai.get('npc_name', role)} in Deutschland. Du hilfst Einwanderern, medizinisches Deutsch zu lernen.",
        },
        "lesson": {
            "topic":   ai.get("lesson_topic", room_label),
            "phrases": phrases,
        },
    }

    _atomic_json_write(CONFIG_FILE, config)

    lesson_text = f"**📚 {config['lesson']['topic']}**\n\n"
    for p in phrases:
        lesson_text += f"🇩🇪 **{p.get('de','')}**  →  🇬🇧 _{p.get('en','')}_\n\n"

    pal = config["palette"]
    status = (
        f"✅ **{config['room_type']}** generated!\n\n"
        f"**Wall + Ceiling:** `{pal['wall']}`  **Floor:** `{pal['floor']}`\n\n"
        f"NPC: **{config['npc']['name']}** ({role})\n\n"
        f"Config saved → `{CONFIG_FILE}`\n"
        f"Unity hot-reloads automatically."
    )

    yield status, make_room_preview_html(palette, config["room_type"]), lesson_text

# ── Learner Mode logic ──────────────────────────────────────────────────────────

def save_config(config: dict):
    _atomic_json_write(CONFIG_FILE, config)

def load_learner_room(preset_name: str):
    if preset_name not in LEARNER_PRESETS:
        return "⚠️ Select a room first.", "", [], ""
    config = dict(LEARNER_PRESETS[preset_name])

    # Randomize colors each load — bright wall/ceiling + wood floor
    config["palette"] = randomize_room_colors() if _COLOR_FETCHER_OK else config.get("palette", {
        "wall": "#E8F4F0", "floor": "#C8A96E", "ceiling": "#E8F4F0"
    })
    save_config(config)

    phrases     = config["lesson"].get("phrases", [])
    topic       = config["lesson"].get("topic", "")
    phrase_text = f"**📚 Lektion: {topic}**\n\n"
    for p in phrases:
        phrase_text += f"🇩🇪 **{p['de']}**\n🇬🇧 _{p['en']}_\n\n"

    npc = config.get("npc", {})
    pal = config["palette"]
    status = (f"✅ Room loaded: **{config['room_type']}**\n"
              f"NPC: **{npc.get('name','?')}** ({npc.get('role','')})\n\n"
              f"**Wall + Ceiling:** `{pal['wall']}`  **Floor:** `{pal['floor']}`\n"
              f"Config saved to: `{CONFIG_FILE}`")

    preview = make_room_preview_html(config["palette"], config["room_type"])
    return status, phrase_text, [], preview

def learner_chat(user_msg: str, history: list, preset_name: str):
    if not user_msg.strip():
        yield history, "", None; return

    if preset_name not in LEARNER_PRESETS:
        yield history + [{"role":"user","content":user_msg},
                         {"role":"assistant","content":"⚠️ Please load a room first."}], "", None
        return

    config     = LEARNER_PRESETS[preset_name]
    npc        = config.get("npc", {})
    system_ctx = (npc.get("context", "Du bist ein Arzt.") +
                  "\n\nAlways reply in this format:\n"
                  "🇩🇪 [German]\n🇬🇧 [English]\n💡 [vocab tip — optional]\nBe brief.")

    if not ollama_available():
        yield history + [{"role":"user","content":user_msg},
                         {"role":"assistant","content":"⚠️ Ollama is not running."}], "", None
        return
    if not ollama_model_ready(OLLAMA_MODEL):
        yield history + [{"role":"user","content":user_msg},
                         {"role":"assistant","content":f"⚠️ Model `{OLLAMA_MODEL}` not found."}], "", None
        return

    history = history + [{"role":"user","content":user_msg},
                         {"role":"assistant","content":"⏳ ..."}]
    yield history, "", None

    msgs = history[:-2]
    ollama_hist = []
    for i in range(0, len(msgs)-1, 2):
        if msgs[i]["role"]=="user" and msgs[i+1]["role"]=="assistant":
            ollama_hist.append((_content_str(msgs[i]["content"]),
                                _content_str(msgs[i+1]["content"])))
    ollama_hist = ollama_hist[-3:]

    full_reply = ""
    for token in ollama_chat_stream(system_ctx, user_msg, ollama_hist):
        full_reply += token
        history[-1] = {"role":"assistant","content":full_reply}
        yield history, "", None

    if not full_reply:
        history[-1] = {"role":"assistant","content":"⚠️ No response. Is `ollama serve` running?"}
        yield history, "", None
        return

    # ── Speak the German line via TTS server ──────────────────────────────────
    german_text = _extract_for_tts(full_reply)
    audio_html  = _tts_audio_html(german_text, "de")
    yield history, "", audio_html

# ── Gradio UI ───────────────────────────────────────────────────────────────────

_EMPTY_PREVIEW = ("<div style='background:#0f1117;border-radius:12px;padding:30px;"
                  "text-align:center;color:#444;font-size:13px'>"
                  "Load a room to see colour preview</div>")

with gr.Blocks(title="🏥 German Medical VR", theme=gr.themes.Soft(),
               js=ROOM_PREVIEW_JS,
               css=".hidden-del-helper{position:absolute;width:1px;height:1px;overflow:hidden;opacity:0;pointer-events:none}") as demo:

    gr.Markdown("# 🏥 German Medical VR — Language Learning\n"
                "**Immersive clinic environments for immigrants learning German in Germany**")

    with gr.Tabs():

        # ── Tab 1: Learner Mode ────────────────────────────────────────────────
        with gr.Tab("📖 Learner Mode"):

            with gr.Row():
                # Left: room selector + preview
                with gr.Column(scale=1):
                    gr.Markdown("### 🏗️ Room")
                    preset_dd = gr.Dropdown(
                        choices=list(LEARNER_PRESETS.keys()),
                        label="Select Room",
                        value=list(LEARNER_PRESETS.keys())[0],
                    )
                    load_btn     = gr.Button("▶ Load Room in Unity", variant="primary")
                    room_status  = gr.Markdown("_No room loaded yet_")
                    room_preview = gr.HTML(_EMPTY_PREVIEW)

                # Right: lesson + NPC chat
                with gr.Column(scale=1):
                    gr.Markdown("### 📚 Lesson Phrases")
                    phrases_box = gr.Markdown("_Load a room to see today's phrases_")
                    gr.Markdown("### 💬 Talk to the NPC")
                    chatbot = gr.Chatbot(label="NPC Conversation", height=260)

                    chat_inp = gr.Textbox(
                        placeholder="Type in German or English…",
                        label="Your message", lines=2,
                    )
                    chat_btn = gr.Button("📤 Send to Doctor", variant="primary")
                    # Hidden audio element — plays automatically, no visible player
                    doctor_audio = gr.HTML(value="", visible=True, elem_id="doctor_audio_slot")

            load_btn.click(load_learner_room,
                           inputs=[preset_dd],
                           outputs=[room_status, phrases_box, chatbot, room_preview])
            chat_btn.click(learner_chat,
                           inputs=[chat_inp, chatbot, preset_dd],
                           outputs=[chatbot, chat_inp, doctor_audio])
            chat_inp.submit(learner_chat,
                            inputs=[chat_inp, chatbot, preset_dd],
                            outputs=[chatbot, chat_inp, doctor_audio])

        # ── Tab 2: Builder Mode ────────────────────────────────────────────────
        with gr.Tab("🏗️ Builder Mode"):

            gr.Markdown("### Generate an AI variation of Doctor or Receptionist room\n"
                        "Pick a room type, describe your style, and AI creates a new colour scheme + NPC + lesson.")

            with gr.Row():
                builder_room_dd = gr.Dropdown(
                    choices=list(BUILDER_ROOM_TYPES.keys()),
                    label="Room Type",
                    value=list(BUILDER_ROOM_TYPES.keys())[0],
                )
                build_btn = gr.Button("🤖 Generate with AI", variant="primary")

            builder_desc = gr.Textbox(
                label="Style description (optional)",
                placeholder='e.g. "modern with mint green walls and wood floor" or leave blank for random style',
                lines=2,
            )

            with gr.Row():
                with gr.Column(scale=1):
                    build_status  = gr.Markdown("_No room generated yet_")
                    build_preview = gr.HTML(_EMPTY_PREVIEW)
                with gr.Column(scale=1):
                    build_lesson  = gr.Markdown("")

            build_btn.click(build_room,
                            inputs=[builder_room_dd, builder_desc],
                            outputs=[build_status, build_preview, build_lesson])


            gr.Markdown("---\n### 🤖 Room AI — Add Objects to the Scene")
            gr.Markdown(
                "Type **object + location** (e.g. `chair corner`, `piano left wall`) — "
                "the AI writes Unity C# code and saves it as its own named file.\n\n"
                "> **No API keys needed** — uses Ollama locally."
            )
            with gr.Row():
                room_ai_req = gr.Textbox(
                    label="Object + location",
                    placeholder="chair corner",
                    lines=2,
                    scale=4,
                )
                room_ai_btn = gr.Button("✨ Generate & Apply", variant="primary", scale=1)

            room_ai_status  = gr.Markdown("*Ready — describe something to add.*")
            room_ai_preview = gr.HTML("<p style='color:#555;padding:8px'>3D preview appears here after generation.</p>")
            with gr.Accordion("📄 Generated C# code", open=False):
                room_ai_code = gr.Textbox(lines=15, max_lines=40, show_label=False)

            gr.Markdown("#### 🗂️ Generated Objects")
            room_ai_del_status  = gr.Markdown()
            room_ai_obj_html    = gr.HTML(_obj_list_html())
            with gr.Row():
                _ai_del_dd  = gr.Dropdown(
                    choices=_obj_choices(),
                    label="Select object to delete",
                    scale=4,
                    interactive=True,
                )
                _ai_del_btn = gr.Button("🗑 Delete Selected", variant="stop", scale=1)
            room_ai_refresh_btn = gr.Button("🔄 Refresh", size="sm")

            _ai_outs = [room_ai_status, room_ai_preview, room_ai_code, room_ai_obj_html, _ai_del_dd]

            room_ai_btn.click(fn=room_ai_generate,
                              inputs=[room_ai_req],
                              outputs=_ai_outs)
            room_ai_req.submit(fn=room_ai_generate,
                               inputs=[room_ai_req],
                               outputs=_ai_outs)
            room_ai_refresh_btn.click(
                fn=lambda: (_obj_list_html(), gr.Dropdown(choices=_obj_choices())),
                outputs=[room_ai_obj_html, _ai_del_dd],
            )
            _ai_del_btn.click(
                fn=_obj_delete_selected,
                inputs=[_ai_del_dd],
                outputs=[room_ai_del_status, room_ai_obj_html, _ai_del_dd],
            )

    gr.Markdown("---\n**Quick start:** `ollama serve` · Select a room · Load in Unity · Chat with NPC")

if __name__ == "__main__":
    print("=" * 60)
    print("  German Medical VR — Language Learning")
    print(f"  Ollama : {'✅ running' if ollama_available() else '❌ run: ollama serve'}")
    print(f"  Model  : {'✅ ready'   if ollama_model_ready(OLLAMA_MODEL) else f'❌ run: ollama pull {OLLAMA_MODEL}'}")
    print("=" * 60)
    demo.queue().launch(server_name="0.0.0.0", server_port=7863, share=False, inbrowser=True)

"""
room_manager.py
===============
Manages multiple named rooms for the German Medical VR project.

Each room has:
  - A mode:  "empty" (bare room + doctor NPC) or "decorated" (full furniture)
  - A folder: rooms/<room_id>/*.cs  — saved copies of its AI-generated objects
  - An action log: created, add_object, delete_object, undo, redo

Only ONE room is "active" at a time — its objects are live in Unity Assets.
Switching rooms swaps the .cs files in Unity Assets automatically.
"""

import json
import shutil
import uuid
from datetime import datetime
from pathlib import Path

# ── Paths ─────────────────────────────────────────────────────────────────────
WORKSPACE_DIR = Path(__file__).parent
ROOMS_DIR     = WORKSPACE_DIR / "rooms"
REGISTRY_FILE = WORKSPACE_DIR / "rooms_registry.json"

try:
    from room_ai_generator import UNITY_ASSETS
except Exception:
    UNITY_ASSETS = Path("E:/AI_HDRI_Unity/Assets")

_BASE_SCRIPTS = {
    "RoomAIAdditions", "GameBootstrap",
    "DoctorEmptyRoom", "DoctorRoomDecorator",
}

# ── Registry I/O ──────────────────────────────────────────────────────────────

def load_registry() -> dict:
    try:
        if REGISTRY_FILE.exists():
            return json.loads(REGISTRY_FILE.read_text(encoding="utf-8"))
    except Exception:
        pass
    return {"active_room_id": None, "rooms": {}}

def save_registry(reg: dict):
    REGISTRY_FILE.write_text(
        json.dumps(reg, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

# ── Room CRUD ─────────────────────────────────────────────────────────────────

def create_room(name: str, mode: str) -> tuple[dict, str]:
    """Create a new room. mode = 'empty' | 'decorated'."""
    if not name.strip():
        return {}, "⚠️ Enter a room name first."
    reg = load_registry()
    room_id = f"room_{uuid.uuid4().hex[:8]}"
    now = datetime.now().isoformat(timespec="seconds")
    room = {
        "id": room_id,
        "name": name.strip(),
        "mode": mode,
        "created_at": now,
        "log": [{"action": "created_room", "timestamp": now, "mode": mode}],
    }
    reg["rooms"][room_id] = room
    (ROOMS_DIR / room_id).mkdir(parents=True, exist_ok=True)
    save_registry(reg)
    return room, f"✅ Created **{name.strip()}** ({mode} mode)."

def delete_room(room_id: str) -> str:
    reg = load_registry()
    if room_id not in reg["rooms"]:
        return "⚠️ Room not found."
    name = reg["rooms"][room_id]["name"]
    if reg.get("active_room_id") == room_id:
        _clear_unity_ai_files()
        reg["active_room_id"] = None
    room_dir = ROOMS_DIR / room_id
    if room_dir.exists():
        shutil.rmtree(room_dir)
    del reg["rooms"][room_id]
    save_registry(reg)
    return f"🗑️ Deleted room **{name}**."

# ── Activation ────────────────────────────────────────────────────────────────

def activate_room(room_id: str) -> str:
    reg = load_registry()
    if room_id not in reg["rooms"]:
        return "⚠️ Room not found."
    room = reg["rooms"][room_id]

    # Back up current active room's files to its folder before switching
    current_id = reg.get("active_room_id")
    if current_id and current_id != room_id and current_id in reg["rooms"]:
        _backup_room_files(current_id)

    # Clear AI-generated files from Unity Assets
    _clear_unity_ai_files()

    # Deploy new room's saved .cs files
    room_dir = ROOMS_DIR / room_id
    if room_dir.exists():
        for cs in room_dir.glob("*.cs"):
            (UNITY_ASSETS / cs.name).write_text(
                cs.read_text(encoding="utf-8"), encoding="utf-8")

    # Switch room decorator (empty vs decorated)
    _switch_decorator(room["mode"])

    reg["active_room_id"] = room_id
    save_registry(reg)
    return (f"✅ **{room['name']}** is now active.\n\n"
            "Re-enter Play mode in Unity to see the change.")

def get_active_room_id() -> str | None:
    return load_registry().get("active_room_id")

# ── Object logging ────────────────────────────────────────────────────────────

def log_add_object(room_id: str, fname: str, request: str, cs_content: str):
    if not room_id:
        return
    reg = load_registry()
    if room_id not in reg["rooms"]:
        return
    reg["rooms"][room_id]["log"].append({
        "action": "add_object",
        "timestamp": datetime.now().isoformat(timespec="seconds"),
        "file": fname,
        "request": request,
        "cs_content": cs_content,
        "undone": False,
    })
    # Also persist to room folder
    room_dir = ROOMS_DIR / room_id
    room_dir.mkdir(parents=True, exist_ok=True)
    (room_dir / fname).write_text(cs_content, encoding="utf-8")
    save_registry(reg)

def log_delete_object(room_id: str, fname: str):
    if not room_id:
        return
    reg = load_registry()
    if room_id not in reg["rooms"]:
        return
    reg["rooms"][room_id]["log"].append({
        "action": "delete_object",
        "timestamp": datetime.now().isoformat(timespec="seconds"),
        "file": fname,
    })
    room_file = ROOMS_DIR / room_id / fname
    if room_file.exists():
        room_file.unlink()
    save_registry(reg)

# ── Undo / Redo ───────────────────────────────────────────────────────────────

def undo_last(room_id: str) -> str:
    """Delete the most recent non-undone add_object."""
    reg = load_registry()
    if room_id not in reg["rooms"]:
        return "⚠️ Room not found."
    log = reg["rooms"][room_id]["log"]
    for i in range(len(log) - 1, -1, -1):
        entry = log[i]
        if entry.get("action") == "add_object" and not entry.get("undone"):
            fname = entry["file"]
            for p in [WORKSPACE_DIR / fname, ROOMS_DIR / room_id / fname]:
                if p.exists():
                    p.unlink()
            if UNITY_ASSETS.exists():
                ua = UNITY_ASSETS / fname
                if ua.exists():
                    ua.unlink()
                meta = UNITY_ASSETS / (fname + ".meta")
                if meta.exists():
                    meta.unlink()
            log[i]["undone"] = True
            log.append({
                "action": "undo",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "file": fname,
            })
            save_registry(reg)
            return f"↩️ Undone: removed **{fname}**. Unity will recompile."
    return "Nothing left to undo."

def redo_last(room_id: str) -> str:
    """Re-apply the most recent undone add_object."""
    reg = load_registry()
    if room_id not in reg["rooms"]:
        return "⚠️ Room not found."
    log = reg["rooms"][room_id]["log"]
    # Walk backwards to find the last 'undo' entry
    for i in range(len(log) - 1, -1, -1):
        entry = log[i]
        if entry.get("action") == "undo":
            fname = entry["file"]
            # Find its original add_object entry and restore it
            cs_content = None
            for j, e in enumerate(log):
                if e.get("action") == "add_object" and e.get("file") == fname and e.get("undone"):
                    cs_content = e.get("cs_content", "")
                    log[j]["undone"] = False
                    break
            if not cs_content:
                return "⚠️ Original file content not found — cannot redo."
            (WORKSPACE_DIR / fname).write_text(cs_content, encoding="utf-8")
            room_dir = ROOMS_DIR / room_id
            room_dir.mkdir(parents=True, exist_ok=True)
            (room_dir / fname).write_text(cs_content, encoding="utf-8")
            if UNITY_ASSETS.exists():
                (UNITY_ASSETS / fname).write_text(cs_content, encoding="utf-8")
            log.pop(i)  # remove the undo entry
            log.append({
                "action": "redo",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "file": fname,
            })
            save_registry(reg)
            return f"↪️ Redone: restored **{fname}**. Unity will recompile."
    return "Nothing left to redo."

# ── HTML rendering ────────────────────────────────────────────────────────────

def rooms_list_html() -> str:
    """Scrollable card list of all rooms."""
    reg = load_registry()
    rooms = reg.get("rooms", {})
    active_id = reg.get("active_room_id")
    if not rooms:
        return "<p style='color:#7ab;padding:12px 4px'>No rooms yet — create one above.</p>"

    cards = ""
    for room in sorted(rooms.values(), key=lambda r: r["created_at"], reverse=True):
        rid       = room["id"]
        name      = room["name"]
        mode      = room["mode"]
        is_active = rid == active_id
        obj_count = sum(
            1 for e in room["log"]
            if e.get("action") == "add_object" and not e.get("undone")
        )
        mode_icon  = "🏗️" if mode == "empty" else "🏥"
        mode_label = "Empty Canvas" if mode == "empty" else "Decorated"
        created    = room["created_at"][:10]

        active_badge = (
            "<span style='background:#1a5c35;color:#7fe;padding:1px 7px;"
            "border-radius:10px;font-size:11px;margin-right:6px'>● ACTIVE</span>"
            if is_active else ""
        )

        def _js(action):
            return (
                f"var t=document.querySelector('#_rm_act_id textarea');"
                f"t.value='{rid}|{action}';"
                f"t.dispatchEvent(new Event('input',{{bubbles:true}}));"
                f"setTimeout(function(){{document.querySelector('#_rm_act_btn button').click()}},120);"
            )

        activate_btn = ("" if is_active else
            f"<button onclick=\"{_js('activate')}\" "
            f"style='background:#14396b;color:#7cf;border:none;border-radius:4px;"
            f"padding:2px 9px;cursor:pointer;font-size:12px;margin-right:4px'>▶ Activate</button>")

        cards += (
            f"<div style='border:1px solid {'#2a5040' if is_active else '#252d40'};"
            f"border-radius:8px;padding:10px 12px;margin-bottom:8px;"
            f"background:{'#0d1f17' if is_active else '#111827'}'>"
            f"<div style='display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:6px'>"
            f"<div>{active_badge}"
            f"<strong style='color:#dde'>{name}</strong>&nbsp;"
            f"<span style='color:#667;font-size:12px'>{mode_icon} {mode_label} · "
            f"{obj_count} object{'s' if obj_count!=1 else ''} · {created}</span>"
            f"</div>"
            f"<div style='white-space:nowrap'>"
            f"{activate_btn}"
            f"<button onclick=\"{_js('showlog')}\" "
            f"style='background:#1e2a3a;color:#9ac;border:none;border-radius:4px;"
            f"padding:2px 9px;cursor:pointer;font-size:12px;margin-right:4px'>📋 Log</button>"
            f"<button onclick=\"{_js('delete')}\" "
            f"style='background:#4a1010;color:#f99;border:none;border-radius:4px;"
            f"padding:2px 9px;cursor:pointer;font-size:12px'>🗑 Delete</button>"
            f"</div></div></div>"
        )
    return f"<div style='max-height:320px;overflow-y:auto;padding-right:2px'>{cards}</div>"

def room_log_html(room_id: str) -> str:
    """Action log for a single room."""
    if not room_id:
        return "<p style='color:#667;padding:8px'>Click 📋 Log on a room card above.</p>"
    reg = load_registry()
    if room_id not in reg["rooms"]:
        return "<p style='color:#888'>Room not found.</p>"
    room = reg["rooms"][room_id]
    log  = room["log"]
    name = room["name"]

    _icons = {
        "created_room":  ("🏠", "#5a8", lambda e: f"Room created ({e.get('mode','?')} mode)"),
        "add_object":    ("✅", "#8bf", lambda e: (
            f'<code>{e.get("file","")}</code> ← "{e.get("request","")}"'
            + (' <em style="color:#f88;font-size:11px">[undone]</em>' if e.get("undone") else "")
        )),
        "delete_object": ("🗑️", "#f88", lambda e: f'<code>{e.get("file","")}</code> deleted'),
        "undo":          ("↩️", "#fa8", lambda e: f'undid <code>{e.get("file","")}</code>'),
        "redo":          ("↪️", "#af8", lambda e: f'redid <code>{e.get("file","")}</code>'),
    }

    rows = ""
    for entry in reversed(log):
        action = entry.get("action", "?")
        icon, color, detail_fn = _icons.get(action, ("•", "#aaa", lambda e: action))
        ts     = entry.get("timestamp", "")[:16].replace("T", " ")
        detail = detail_fn(entry)
        rows += (
            f"<tr>"
            f"<td style='padding:4px 8px;color:#556;font-size:11px;white-space:nowrap'>{ts}</td>"
            f"<td style='padding:4px 6px;text-align:center;font-size:14px'>{icon}</td>"
            f"<td style='padding:4px 8px;color:{color};font-size:12px'>{detail}</td>"
            f"</tr>"
        )

    return (
        f"<div style='background:#0d1117;border-radius:8px;padding:10px'>"
        f"<strong style='color:#99aacc;font-size:13px'>📋 {name} — Action Log</strong>"
        f"<div style='max-height:220px;overflow-y:auto;margin-top:8px'>"
        f"<table style='width:100%;border-collapse:collapse'>{rows}</table>"
        f"</div></div>"
    )

# ── Internal helpers ──────────────────────────────────────────────────────────

def _backup_room_files(room_id: str):
    room_dir = ROOMS_DIR / room_id
    room_dir.mkdir(parents=True, exist_ok=True)
    if not UNITY_ASSETS.exists():
        return
    for cs in UNITY_ASSETS.glob("*.cs"):
        if cs.stem in _BASE_SCRIPTS:
            continue
        try:
            text = cs.read_text(encoding="utf-8", errors="ignore")
            if "auto-generated by Room AI" in text:
                (room_dir / cs.name).write_text(text, encoding="utf-8")
        except Exception:
            pass

def _clear_unity_ai_files():
    if not UNITY_ASSETS.exists():
        return
    for cs in list(UNITY_ASSETS.glob("*.cs")):
        if cs.stem in _BASE_SCRIPTS:
            continue
        try:
            if "auto-generated by Room AI" in cs.read_text(encoding="utf-8", errors="ignore"):
                cs.unlink()
                meta = UNITY_ASSETS / (cs.name + ".meta")
                if meta.exists():
                    meta.unlink()
        except Exception:
            pass

def _switch_decorator(mode: str):
    workspace = Path(__file__).parent
    if mode == "empty":
        src = workspace / "DoctorEmptyRoom.cs"
        if src.exists():
            (UNITY_ASSETS / "DoctorEmptyRoom.cs").write_text(
                src.read_text(encoding="utf-8"), encoding="utf-8")
        dec = UNITY_ASSETS / "DoctorRoomDecorator.cs"
        if dec.exists():
            dec.unlink()
    else:
        src = workspace / "DoctorRoomDecorator.cs"
        if src.exists():
            (UNITY_ASSETS / "DoctorRoomDecorator.cs").write_text(
                src.read_text(encoding="utf-8"), encoding="utf-8")
        emp = UNITY_ASSETS / "DoctorEmptyRoom.cs"
        if emp.exists():
            emp.unlink()

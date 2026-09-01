"""
room_ai_gradio.py
=================
Gradio web UI for the Room AI generator.
Runs on port 7865.

Usage: python room_ai_gradio.py
Then open: http://localhost:7865
"""

import os
import re
import io
import base64
from pathlib import Path
import gradio as gr
from room_ai_generator import RoomAIGenerator, UNITY_ASSETS, WORKSPACE_DIR

# ── 3D preview helpers ────────────────────────────────────────────────────────

def _parse_prim_calls(cs_code):
    _NAMED = {
        'black':(0,0,0),'white':(1,1,1),'red':(1,0,0),'green':(0,0.5,0),
        'blue':(0,0,1),'yellow':(1,0.92,0.016),'cyan':(0,1,1),
        'magenta':(1,0,1),'gray':(0.5,0.5,0.5),'grey':(0.5,0.5,0.5),
        'brown':(0.55,0.27,0.07),'orange':(1,0.65,0),
    }
    def _f(s): return float(s.rstrip('f'))
    num  = r'[-\d.]+f?'
    vec3 = rf'new Vector3\(\s*({num})\s*,\s*({num})\s*,\s*({num})\s*\)'
    col  = (rf'(?:new Color\(\s*({num})\s*,\s*({num})\s*,\s*({num})'
            rf'(?:\s*,\s*{num})?\s*\)|Color\.(\w+))')
    pat  = (r'Prim\(\s*PrimitiveType\.(\w+),\s*"([^"]+)",\s*'
            + vec3 + r',\s*' + vec3 + r',\s*' + col + r'\s*\)')
    objs = []
    for m in re.finditer(pat, cs_code):
        try:
            color = ([_f(m.group(9)), _f(m.group(10)), _f(m.group(11))]
                     if m.group(9) is not None
                     else list(_NAMED.get(m.group(12).lower(), (0.5,0.5,0.5))))
            objs.append({
                'type': m.group(1), 'name': m.group(2),
                'pos':   [_f(m.group(3)), _f(m.group(4)), _f(m.group(5))],
                'scale': [_f(m.group(6)), _f(m.group(7)), _f(m.group(8))],
                'color': color,
            })
        except Exception:
            pass
    return objs

def _make_preview_html(new_objs, request_text):
    label = request_text[:40]
    try:
        import matplotlib
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt
        from mpl_toolkits.mplot3d.art3d import Poly3DCollection
        plt.close('all')
    except ImportError:
        return "<p style='color:#aaa'>Install matplotlib for preview.</p>"

    def _faces(ux,uy,uz,w,h,d):
        mx,my,mz = ux,uz,uy
        x0,x1=mx-w/2,mx+w/2; y0,y1=my-d/2,my+d/2; z0,z1=mz-h/2,mz+h/2
        return [[[x0,y0,z0],[x1,y0,z0],[x1,y1,z0],[x0,y1,z0]],
                [[x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1]],
                [[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1]],
                [[x0,y1,z0],[x1,y1,z0],[x1,y1,z1],[x0,y1,z1]],
                [[x0,y0,z0],[x0,y1,z0],[x0,y1,z1],[x0,y0,z1]],
                [[x1,y0,z0],[x1,y1,z0],[x1,y1,z1],[x1,y0,z1]]]

    def _box(ax,ux,uy,uz,w,h,d,color,alpha=1.0,ec='none'):
        fc=tuple(min(1.0,max(0.0,c)) for c in color[:3])
        try: ax.add_collection3d(Poly3DCollection(_faces(ux,uy,uz,w,h,d),
                                  alpha=alpha,facecolor=fc,edgecolor=ec,linewidth=0.4))
        except Exception: pass

    def _style(ax):
        try:
            ax.set_xlabel('');ax.set_ylabel('');ax.set_zlabel('')
            ax.set_xticklabels([]);ax.set_yticklabels([]);ax.set_zticklabels([])
            for p in (ax.xaxis.pane,ax.yaxis.pane,ax.zaxis.pane):
                p.set_facecolor('#1e2438');p.set_alpha(0.5)
            ax.grid(True,color='#2a3050',linewidth=0.5,alpha=0.3)
        except Exception: pass

    EXISTING=[
        {'pos':[0,-1.74,0.5],'scale':[5,0.1,1.8],'color':[0.45,0.28,0.12]},
        {'pos':[0,-1.9,2.2],'scale':[0.6,0.5,0.6],'color':[0.12,0.12,0.12]},
        {'pos':[-0.8,-1.9,-1.3],'scale':[0.6,0.5,0.6],'color':[0.15,0.2,0.55]},
        {'pos':[0.8,-1.9,-1.3],'scale':[0.6,0.5,0.6],'color':[0.15,0.2,0.55]},
        {'pos':[0,-0.5,4.6],'scale':[2.5,3.5,0.4],'color':[0.5,0.3,0.1]},
        {'pos':[-4.8,0.5,-1],'scale':[0.1,2,1.5],'color':[0.8,0.85,0.9]},
        {'pos':[-4,-1.8,-4.2],'scale':[0.8,1.2,0.6],'color':[0.5,0.5,0.55]},
    ]
    BG='#12161f'
    fig=None
    try:
        fig=plt.figure(figsize=(9,3.5),facecolor=BG)
        ROOM_SCALE=4.0
        obj_only=[o for o in new_objs if max(o['scale'])<ROOM_SCALE] or new_objs
        ax1=fig.add_subplot(121,projection='3d'); ax1.set_facecolor('#1a1f2e')
        if obj_only:
            n=len(obj_only)
            cx=sum(o['pos'][0] for o in obj_only)/n
            cy=sum(o['pos'][1] for o in obj_only)/n
            cz=sum(o['pos'][2] for o in obj_only)/n
            for o in obj_only:
                _box(ax1,o['pos'][0]-cx,o['pos'][1]-cy,o['pos'][2]-cz,
                     o['scale'][0],o['scale'][1],o['scale'][2],o['color'],0.92,'#555')
            me=max(max(abs(o['pos'][d]-[cx,cy,cz][d])+o['scale'][d]/2
                       for d in range(3)) for o in obj_only)
            lim=max(0.6,me*1.5)
            ax1.set_xlim(-lim,lim);ax1.set_ylim(-lim,lim);ax1.set_zlim(-lim,lim)
        ax1.set_title(label,color='#9999cc',fontsize=8,pad=4)
        ax1.view_init(elev=22,azim=35); _style(ax1)

        ax2=fig.add_subplot(122,projection='3d'); ax2.set_facecolor('#1a1f2e')
        _box(ax2,0,-2.53,0,10,0.05,10,[0.2,0.4,0.5],0.3)
        for o in EXISTING:
            _box(ax2,o['pos'][0],o['pos'][1],o['pos'][2],
                 o['scale'][0],o['scale'][1],o['scale'][2],o['color'],0.45,'#222')
        for o in new_objs:
            _box(ax2,o['pos'][0],o['pos'][1],o['pos'][2],
                 o['scale'][0],o['scale'][1],o['scale'][2],o['color'],0.95,'#ffee44')
        ax2.set_xlim(-5.5,5.5);ax2.set_ylim(-5.5,5.5);ax2.set_zlim(-3,3)
        ax2.set_title('Room placement  (new=yellow)',color='#9999cc',fontsize=8,pad=4)
        ax2.view_init(elev=28,azim=-40); _style(ax2)

        plt.subplots_adjust(left=0.01,right=0.99,top=0.92,bottom=0.02,wspace=0.05)
        buf=io.BytesIO()
        plt.savefig(buf,format='png',facecolor=BG,dpi=90,bbox_inches='tight')
        buf.seek(0)
        b64=base64.b64encode(buf.read()).decode()
        return (f'<div style="background:#0f1117;border-radius:12px;padding:8px">'
                f'<img src="data:image/png;base64,{b64}" style="width:100%;border-radius:8px;display:block"/>'
                f'</div>')
    except Exception as e:
        return f"<p style='color:#e88;padding:8px'>Preview error: {e}</p>"
    finally:
        if fig is not None: plt.close(fig)
        plt.close('all')

# ── Persistent history ────────────────────────────────────────────────────────
import json

_HISTORY_FILE = WORKSPACE_DIR / "room_ai_history.json"

def _load_history() -> list:
    try:
        if _HISTORY_FILE.exists():
            return json.loads(_HISTORY_FILE.read_text(encoding="utf-8"))
    except Exception:
        pass
    return []

def _save_history(h: list):
    try:
        _HISTORY_FILE.write_text(json.dumps(h[-50:], ensure_ascii=False), encoding="utf-8")
    except Exception:
        pass

_history: list = _load_history()

# ── Generator singleton (re-created when keys change) ─────────────────────────
_generator = None

def get_generator(groq_key: str, gemini_key: str) -> RoomAIGenerator:
    global _generator
    if _generator is None:
        _generator = RoomAIGenerator(groq_key=groq_key.strip(), gemini_key=gemini_key.strip())
    return _generator

def reload_generator(groq_key: str, gemini_key: str):
    global _generator
    _generator = RoomAIGenerator(groq_key=groq_key.strip(), gemini_key=gemini_key.strip())
    return "✅ Provider chain refreshed."

def run_generation(request: str, groq_key: str, gemini_key: str):
    if not request.strip():
        return ("⚠️ Please enter a room request.",
                "<p style='color:#666'>No output yet.</p>", "",
                format_history(), list_objects_html())

    gen = get_generator(groq_key, gemini_key)
    ok, status, cs, fname = gen.generate(request.strip())

    objs = _parse_prim_calls(cs) if cs else []
    if objs:
        preview_html = _make_preview_html(objs, request.strip())
    elif cs:
        preview_html = "<p style='color:#e88;padding:10px'>⚠️ No Prim() objects parsed — check Generated C# below.</p>"
    else:
        preview_html = "<p style='color:#666;padding:10px'>Generation failed.</p>"

    entry = f"✅ `{fname}` — {len(objs)} obj" if ok and objs else f"❌ {request[:30]}"
    _history.insert(0, [request, entry])
    _save_history(_history)
    return (status, preview_html, cs,
            format_history(), list_objects_html())

def format_history() -> str:
    if not _history:
        return "*No requests yet.*"
    return "\n".join(f"- **{r}** — {s}" for r, s in _history[:10])



# ── Generated-object file management ─────────────────────────────────────────

# Base room scripts that happen to contain the marker — exclude from the list
_BASE_SCRIPTS = {'RoomAIAdditions', 'GameBootstrap', 'DoctorEmptyRoom', 'DoctorRoomDecorator', 'ChairCorner'}

def _ai_files() -> list[str]:
    """List all AI-generated object .cs files (written by the generator —
    detected by the 'auto-generated by Room AI' marker inside the file)."""
    found = []
    for p in sorted(WORKSPACE_DIR.glob("*.cs")):
        if p.stem in _BASE_SCRIPTS:
            continue
        try:
            if "auto-generated by Room AI" in p.read_text(encoding="utf-8", errors="ignore"):
                found.append(p.name)
        except Exception:
            pass
    return found

def list_objects_html() -> str:
    """HTML table with an inline 🗑 Delete button per row."""
    files = _ai_files()
    if not files:
        return "<p style='color:#7ab;padding:12px'>✅ No AI-generated objects — room is clean.</p>"
    rows = ""
    for f in files:
        unity_icon = "🟢" if UNITY_ASSETS.exists() and (UNITY_ASSETS / f).exists() else "🔴"
        js = (
            f"(function(){{"
            f"var t=document.querySelector('#_delbox textarea');"
            f"if(!t){{console.error('_delbox not found');return;}}"
            f"var s=Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set;"
            f"s.call(t,'{f}');"
            f"t.dispatchEvent(new Event('input',{{bubbles:true}}));"
            f"t.dispatchEvent(new Event('change',{{bubbles:true}}));"
            f"setTimeout(function(){{"
            f"var b=document.querySelector('#_delbtn button');"
            f"if(b){{b.click();}}else{{console.error('_delbtn not found');}}"
            f"}},250);"
            f"}})()"
        )
        rows += (
            f"<tr>"
            f"<td style='padding:6px 12px;color:#ccd'>{f}</td>"
            f"<td style='padding:6px 4px;text-align:center'>{unity_icon}</td>"
            f"<td style='padding:4px 10px;text-align:right'>"
            f"<button onclick=\"{js}\" "
            f"style='background:#7b2020;color:#fcc;border:none;border-radius:4px;"
            f"padding:3px 12px;cursor:pointer;font-size:12px'>🗑 Delete</button>"
            f"</td>"
            f"</tr>"
        )
    return (
        "<table style='width:100%;border-collapse:collapse;font-size:13px'>"
        "<thead><tr>"
        "<th style='text-align:left;padding:6px 12px;color:#99aacc;border-bottom:1px solid #334'>File</th>"
        "<th style='padding:6px 4px;color:#99aacc;border-bottom:1px solid #334'>Unity</th>"
        "<th></th>"
        "</tr></thead>"
        f"<tbody>{rows}</tbody></table>"
        "<p style='color:#556;font-size:11px;margin:6px 12px 0'>🟢 in Unity Assets &nbsp;🔴 workspace only</p>"
    )

def delete_object(fname: str) -> tuple[str, str]:
    """Delete the named .cs (+ .meta) from workspace AND Unity Assets."""
    if not fname:
        return "⚠️ No object specified.", list_objects_html()
    removed = []
    ws = WORKSPACE_DIR / fname
    if ws.exists():
        ws.unlink(); removed.append("workspace")
    if UNITY_ASSETS.exists():
        ua = UNITY_ASSETS / fname
        if ua.exists():
            ua.unlink(); removed.append("Unity Assets")
        meta = UNITY_ASSETS / (fname + ".meta")
        if meta.exists():
            meta.unlink()
    msg = (f"🗑️ Deleted `{fname}` from {' + '.join(removed)}. Unity will recompile."
           if removed else f"⚠️ `{fname}` not found on disk.")
    return msg, list_objects_html()

# ── Example prompts (short "object location" style) ───────────────────────────
EXAMPLES = [
    "piano left wall",
    "coat rack back wall",
    "side table lamp corner",
    "fish tank filing cabinet",
    "wall clock back wall",
    "plants front corners",
    "reception desk back wall",
    "children play area right corner",
    "coffee table middle",
    "bookshelf right wall",
]

# ── UI ────────────────────────────────────────────────────────────────────────
with gr.Blocks(title="🏥 Room AI Generator") as demo:
    gr.Markdown("""
# 🏥 Room AI Generator
Describe what you want to add — each object is saved as its own file.
Delete any object instantly from the **Generated Objects** panel below.

**Provider chain:** Groq (800 tok/s) → Gemini Flash → Ollama (local fallback)
""")

    with gr.Row():
        # ── Left column: controls ─────────────────────────────────────────────
        with gr.Column(scale=1):
            gr.Markdown("### 🔑 API Keys *(optional — skip to use Ollama only)*")
            groq_key   = gr.Textbox(label="Groq API Key",   placeholder="gsk_...",  type="password",
                                    value=os.getenv("GROQ_API_KEY", ""))
            gemini_key = gr.Textbox(label="Gemini API Key", placeholder="AIza...", type="password",
                                    value=os.getenv("GEMINI_API_KEY", ""))
            refresh_btn = gr.Button("🔄 Apply Keys", size="sm")
            refresh_out = gr.Markdown()

            gr.Markdown("---")
            gr.Markdown("### 🪑 Room Request")
            request_box = gr.Textbox(
                label="Object + location  (e.g. \"chair corner\", \"piano left wall\")",
                placeholder="chair corner",
                lines=2,
            )
            generate_btn = gr.Button("✨ Generate & Apply to Unity", variant="primary")

            gr.Markdown("### 💡 Examples")
            for ex in EXAMPLES:
                gr.Button(ex, size="sm").click(
                    fn=lambda e=ex: e, outputs=request_box
                )

            gr.Markdown("---")
            gr.Markdown("### 📜 History")
            history_md = gr.Markdown("*No requests yet.*")

        # ── Right column: output ──────────────────────────────────────────────
        with gr.Column(scale=2):
            status_out  = gr.Markdown("*Ready — enter a request and click Generate.*")
            preview_out = gr.HTML("<div style='background:#0f1117;border-radius:12px;padding:20px;color:#555;text-align:center'>3D preview will appear here after generation</div>")
            code_out    = gr.Textbox(
                label="Generated C# (auto-saved as its own named file)",
                lines=12,
                max_lines=28,
            )

    # ── Generated Objects panel ───────────────────────────────────────────────
    gr.Markdown("---")
    gr.Markdown("### 🗂️ Generated Objects")
    del_status_out   = gr.Markdown()
    objects_html     = gr.HTML(list_objects_html())
    refresh_list_btn = gr.Button("🔄 Refresh", size="sm")
    # Hidden elements — in DOM but invisible; JS sets value + clicks to trigger delete
    _delbox = gr.Textbox(elem_id="_delbox", label="", visible=False)
    _delbtn = gr.Button("del", elem_id="_delbtn", visible=False)

    # ── Wire events ───────────────────────────────────────────────────────────
    gen_outputs = [status_out, preview_out, code_out, history_md, objects_html]

    generate_btn.click(
        fn=run_generation,
        inputs=[request_box, groq_key, gemini_key],
        outputs=gen_outputs,
    )
    request_box.submit(
        fn=run_generation,
        inputs=[request_box, groq_key, gemini_key],
        outputs=gen_outputs,
    )
    refresh_btn.click(
        fn=reload_generator,
        inputs=[groq_key, gemini_key],
        outputs=[refresh_out],
    )
    refresh_list_btn.click(
        fn=list_objects_html,
        outputs=[objects_html],
    )
    _delbtn.click(
        fn=delete_object,
        inputs=[_delbox],
        outputs=[del_status_out, objects_html],
    )

if __name__ == "__main__":
    demo.launch(server_port=7865, share=False, theme=gr.themes.Soft())

"""
gradio_app_depth.py  —  AI Room Generator (cubemap version)
Copy to E:/ai_generator/ai_generator/ and run:
    python gradio_app_depth.py

Run generate_presets.py FIRST to create the 4 fixed scenario assets.
"""

import os, shutil, logging
import requests, gradio as gr, torch
from pathlib import Path
from diffusers import StableDiffusionPipeline, DPMSolverMultistepScheduler
from PIL import Image as PILImage
import equirect_to_cubemap

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# ── Paths ─────────────────────────────────────────────────────────────────────
OUTPUT_DIR = Path("E:/ai_generator/output")
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

ASSETS_DIR = Path(__file__).parent / "assets"
FACE_KEYS  = equirect_to_cubemap.FACE_KEYS
FACE_LABELS = ["Front Wall", "Back Wall", "Left Wall", "Right Wall", "Floor", "Ceiling"]
FACE_SIZE  = 512

SCENARIOS = [
    {"name": "🏥 Doctor's Office",  "key": "doctor_office"},
    {"name": "🔪 Operating Room",   "key": "operating_room"},
    {"name": "💊 Medicine Room",    "key": "medicine_room"},
    {"name": "🪑 Waiting Room",     "key": "waiting_room"},
]

# ── Scenario load ─────────────────────────────────────────────────────────────
def load_scenario(key: str, name: str):
    asset_dir = ASSETS_DIR / key
    missing = [f for f in FACE_KEYS if not (asset_dir / f"{f}.png").exists()]
    if missing:
        return (
            f"❌ {name} missing faces: {', '.join(missing)}\n"
            f"Run: python generate_presets.py",
            *[None] * 6,
        )

    previews = []
    for face in FACE_KEYS:
        shutil.copy(str(asset_dir / f"{face}.png"), str(OUTPUT_DIR / f"{face}.png"))
        previews.append(PILImage.open(str(asset_dir / f"{face}.png")))

    # touch wall_front last so Unity's watcher fires after all 6 are copied
    (OUTPUT_DIR / "wall_front.png").touch()

    return f"✅ {name} loaded → Unity room is updating!", *previews

# ── Ollama ────────────────────────────────────────────────────────────────────
OLLAMA_HOST  = os.environ.get("OLLAMA_HOST",  "http://localhost:11434")
OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "llama3")

def enrich_prompt(user_prompt: str) -> str:
    try:
        r = requests.post(
            f"{OLLAMA_HOST}/api/generate",
            json={
                "model": OLLAMA_MODEL,
                "prompt": (
                    "Rewrite as a detailed Stable Diffusion prompt for a 360 degree equirectangular "
                    "panorama interior room. Reply with prompt only.\n\nRoom: " + user_prompt
                ),
                "stream": False,
            },
            timeout=5,
        )
        r.raise_for_status()
        return r.json().get("response", user_prompt).strip()
    except Exception:
        return (user_prompt +
                ", 360 degree equirectangular panorama, photorealistic interior, highly detailed, 8k")

# ── Stable Diffusion ───────────────────────────────────────────────────────────
_sd_pipe = None

def get_sd_pipe():
    global _sd_pipe
    if _sd_pipe is None:
        device = "cuda" if torch.cuda.is_available() else "cpu"
        _sd_pipe = StableDiffusionPipeline.from_pretrained(
            "runwayml/stable-diffusion-v1-5",
            torch_dtype=torch.float16 if device == "cuda" else torch.float32,
            safety_checker=None,
        ).to(device)
        _sd_pipe.scheduler = DPMSolverMultistepScheduler.from_config(
            _sd_pipe.scheduler.config, algorithm_type="dpmsolver++", use_karras_sigmas=True,
        )
        logger.info("[SD] Ready on %s.", device)
    return _sd_pipe

def _preload():
    import threading
    threading.Thread(target=get_sd_pipe, daemon=True).start()

def generate_custom(user_prompt, steps, guidance):
    yield "⏳ Enriching prompt...", *[None] * 6

    sd_prompt = enrich_prompt(user_prompt)
    pipe = get_sd_pipe()

    # ── panorama → 4 walls ────────────────────────────────────────────────────
    yield f"🎨 Generating panorama ({int(steps)} steps)...", *[None] * 6
    pano = pipe(sd_prompt, width=1024, height=512,
                num_inference_steps=int(steps),
                guidance_scale=float(guidance)).images[0]
    tmp_pano = OUTPUT_DIR / "_panorama_tmp.png"
    pano.save(str(tmp_pano))

    yield "✂️ Slicing panorama → 4 walls...", *[None] * 6
    wall_imgs = equirect_to_cubemap.convert(str(tmp_pano), OUTPUT_DIR,
                                            face_size=FACE_SIZE,
                                            faces=equirect_to_cubemap.WALL_KEYS)

    # ── floor (separate generation) ───────────────────────────────────────────
    yield "🎨 Generating floor...", *[None] * 6
    floor_prompt = sd_prompt.replace(
        "360 degree equirectangular panorama", ""
    ) + ", top-down bird's-eye view, flat floor texture, no distortion"
    floor_img = pipe(floor_prompt, width=FACE_SIZE, height=FACE_SIZE,
                     num_inference_steps=int(steps),
                     guidance_scale=float(guidance)).images[0]
    floor_img.save(str(OUTPUT_DIR / "floor.png"))

    # ── ceiling (separate generation) ─────────────────────────────────────────
    yield "🎨 Generating ceiling...", *[None] * 6
    ceiling_prompt = sd_prompt.replace(
        "360 degree equirectangular panorama", ""
    ) + ", bottom-up view looking up at ceiling, flat undistorted view"
    ceiling_img = pipe(ceiling_prompt, width=FACE_SIZE, height=FACE_SIZE,
                       num_inference_steps=int(steps),
                       guidance_scale=float(guidance)).images[0]
    ceiling_img.save(str(OUTPUT_DIR / "ceiling.png"))

    # ── trigger Unity ─────────────────────────────────────────────────────────
    (OUTPUT_DIR / "wall_front.png").touch()

    ordered = [wall_imgs.get(k) for k in equirect_to_cubemap.WALL_KEYS] + [floor_img, ceiling_img]
    yield "✅ Room complete — Unity is updating!", *ordered

# ── Gradio UI ─────────────────────────────────────────────────────────────────
with gr.Blocks(title="AI Room Generator") as demo:
    gr.Markdown("# 🌍 AI Room Generator\n360° panorama → 6 cube faces → Unity box room")

    with gr.Tabs():

        # ── Tab 1: Saved Scenes ───────────────────────────────────────────────
        with gr.Tab("📦 Scenes"):
            gr.Markdown("### Click a scene to instantly load the full room in Unity.")
            sc_status = gr.Textbox(label="Status", lines=2, interactive=False)
            with gr.Row():
                sc_previews = [gr.Image(label=lbl, type="pil", height=160)
                               for lbl in FACE_LABELS]
            with gr.Row():
                for sc in SCENARIOS:
                    ok = all((ASSETS_DIR / sc["key"] / f"{f}.png").exists() for f in FACE_KEYS)
                    btn = gr.Button(sc["name"], variant="primary" if ok else "secondary", scale=1)
                    btn.click(
                        fn=lambda k=sc["key"], n=sc["name"]: load_scenario(k, n),
                        outputs=[sc_status, *sc_previews],
                    )

            no_presets = not any(
                all((ASSETS_DIR / sc["key"] / f"{f}.png").exists() for f in FACE_KEYS)
                for sc in SCENARIOS
            )
            if no_presets:
                gr.Markdown("No preset rooms found. Run: python generate_presets.py")

        # ── Tab 2: Custom Room ────────────────────────────────────────────────
        with gr.Tab("✏️ Custom Room"):
            gr.Markdown(
                "Generates one 360° panorama then slices it into 6 cube faces automatically."
            )
            with gr.Row():
                with gr.Column(scale=2):
                    prompt_box  = gr.Textbox(label="Room description",
                                             placeholder="a cozy library with warm fireplace")
                    steps_sl    = gr.Slider(5, 40, value=15, step=1,
                                            label="Steps  (15 = fast · 25 = better quality)")
                    guidance_sl = gr.Slider(1.0, 15.0, value=7.5, step=0.5,
                                            label="Guidance scale")
                    gen_btn     = gr.Button("Generate Room", variant="primary")

                with gr.Column(scale=3):
                    status_box = gr.Textbox(label="Status", lines=2, interactive=False)
                    with gr.Row():
                        custom_previews = [gr.Image(label=lbl, type="pil", height=160)
                                           for lbl in FACE_LABELS]

            gen_btn.click(
                fn=generate_custom,
                inputs=[prompt_box, steps_sl, guidance_sl],
                outputs=[status_box, *custom_previews],
            )

if __name__ == "__main__":
    _preload()
    demo.queue().launch(server_name="0.0.0.0", server_port=7860, share=False, inbrowser=True)

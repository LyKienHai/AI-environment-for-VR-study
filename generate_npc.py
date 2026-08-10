# -*- coding: utf-8 -*-
"""
generate_npc.py  -  Generate a 2D NPC sprite, remove background, auto-pixelate.

Pipeline:
  1. Stable Diffusion generates full-body character on white background
  2. PIL removes white background  ->  transparent PNG
  3. pixelize_npc.py uploads to pixel-me.tokyo  ->  pixel art PNG (Unity-ready)

Run from E:/ai_generator/ai_generator/:
    python generate_npc.py                     # doctor (default)
    python generate_npc.py receptionist        # receptionist

Output (pixel art, transparent):
    E:/ai_generator/output/npc_<name>.png
    E:/ai_generator/ai_generator/assets/npc_<name>.png
"""

import sys, logging, torch, numpy as np
from pathlib import Path
from PIL import Image
from diffusers import StableDiffusionPipeline, DPMSolverMultistepScheduler

logging.basicConfig(level=logging.INFO, format="%(message)s")
logger = logging.getLogger(__name__)

OUTPUT_DIR = Path("E:/ai_generator/output")
ASSETS_DIR = Path(__file__).parent / "assets"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
ASSETS_DIR.mkdir(parents=True, exist_ok=True)

STEPS = 25

# ── Character definitions ──────────────────────────────────────────────────
CHARACTERS = {
    "doctor": {
        "prompt": (
            "full body portrait of a friendly male doctor, white lab coat, stethoscope around neck, "
            "dark trousers, black shoes, standing upright facing forward, arms relaxed at sides, "
            "neutral expression, head to toe full body shot, isolated on pure white background, "
            "no shadows, clean white backdrop, professional medical portrait, photorealistic, 8k"
        ),
        "negative": (
            "cropped, cut off, half body, background pattern, shadow, gradient, "
            "gray background, multiple people, sitting"
        ),
    },
    "receptionist": {
        "prompt": (
            "full body portrait of a friendly female receptionist, white blouse, dark skirt, "
            "hair neatly tied back, standing upright facing forward, arms relaxed at sides, "
            "warm smile, head to toe full body shot, isolated on pure white background, "
            "no shadows, clean white backdrop, professional portrait, photorealistic, 8k"
        ),
        "negative": (
            "cropped, cut off, half body, background pattern, shadow, gradient, "
            "gray background, multiple people, sitting, lab coat, stethoscope"
        ),
    },
}


def remove_white_background(img: Image.Image, tolerance: int = 35) -> Image.Image:
    """Convert near-white pixels to transparent."""
    img  = img.convert("RGBA")
    data = np.array(img, dtype=np.float32)
    r, g, b = data[..., 0], data[..., 1], data[..., 2]
    is_bg  = (r > 255 - tolerance) & (g > 255 - tolerance) & (b > 255 - tolerance)
    whiteness = np.minimum(r, np.minimum(g, b))
    alpha  = 1.0 - np.clip((whiteness - (255 - tolerance)) / tolerance, 0, 1)
    data[..., 3] = np.where(is_bg, alpha * 255, 255)
    return Image.fromarray(data.astype(np.uint8), "RGBA")


def generate(character: str = "doctor"):
    if character not in CHARACTERS:
        raise ValueError(f"Unknown character '{character}'. Choose from: {list(CHARACTERS)}")

    cfg  = CHARACTERS[character]
    name = f"npc_{character}"

    # ── Step 1: Generate with SD ──────────────────────────────────────────
    device = "cuda" if torch.cuda.is_available() else "cpu"
    logger.info(f"[1/3] Loading Stable Diffusion on {device}...")
    pipe = StableDiffusionPipeline.from_pretrained(
        "runwayml/stable-diffusion-v1-5",
        torch_dtype=torch.float16 if device == "cuda" else torch.float32,
        safety_checker=None,
    ).to(device)
    pipe.scheduler = DPMSolverMultistepScheduler.from_config(
        pipe.scheduler.config, algorithm_type="dpmsolver++", use_karras_sigmas=True,
    )

    logger.info(f"[1/3] Generating {character} sprite...")
    image = pipe(
        cfg["prompt"],
        negative_prompt=cfg["negative"],
        width=512, height=768,
        num_inference_steps=STEPS,
        guidance_scale=8.0,
    ).images[0]

    # ── Step 2: Remove background ─────────────────────────────────────────
    logger.info("[2/3] Removing white background...")
    sprite = remove_white_background(image)

    # Save the raw transparent PNG (before pixelation)
    raw_out = OUTPUT_DIR / f"{name}_raw.png"
    sprite.save(str(raw_out))
    logger.info(f"      Saved raw -> {raw_out}")

    # ── Step 3: Pixelate via pixel-me.tokyo ───────────────────────────────
    logger.info("[3/3] Pixelating via pixel-me.tokyo (browser will open)...")
    try:
        from pixelize_npc import pixelize
        pixel_out = OUTPUT_DIR / f"{name}.png"
        asset_out = ASSETS_DIR / f"{name}.png"
        pixelize(str(raw_out), str(pixel_out))
        # also copy to assets
        import shutil
        shutil.copy(str(pixel_out), str(asset_out))
        logger.info(f"      Pixel art -> {pixel_out}")
        logger.info(f"      Asset copy -> {asset_out}")
    except Exception as e:
        logger.warning(f"      Pixelation failed ({e})")
        logger.warning("      Saving raw (no pixel art) as final output instead.")
        import shutil
        final = OUTPUT_DIR / f"{name}.png"
        shutil.copy(str(raw_out), str(final))
        shutil.copy(str(raw_out), str(ASSETS_DIR / f"{name}.png"))

    logger.info(f"Done! Unity will pick up {name}.png automatically.")


if __name__ == "__main__":
    char = sys.argv[1] if len(sys.argv) > 1 else "doctor"
    generate(char)

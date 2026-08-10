"""
generate_presets.py  —  One-time setup: generate all 4 room presets.

Each of the 6 faces is generated independently:
  - 4 walls: flat frontal elevation view, each with unique furniture
  - floor:   flat top-down texture
  - ceiling: flat plain white texture

Run ONCE from E:/ai_generator/ai_generator/:
    python generate_presets.py
"""

import logging, torch
from pathlib import Path
from diffusers import StableDiffusionPipeline, DPMSolverMultistepScheduler

logging.basicConfig(level=logging.INFO, format="%(message)s")
logger = logging.getLogger(__name__)

ASSETS_DIR = Path(__file__).parent / "assets"
ASSETS_DIR.mkdir(exist_ok=True)

FACE_KEYS = ["wall_front", "wall_back", "wall_left", "wall_right", "floor", "ceiling"]
WALL_SIZE  = 512   # square per face
STEPS      = 20

FLAT_WALL  = "flat frontal view, architectural interior elevation, no perspective distortion, straight-on, photorealistic, 8k"
FLAT_TEX   = "flat 2D material swatch, no perspective, no shadows, no furniture, seamless texture, 8k"

SCENARIOS = [
    {
        "name": "Doctor's Office",
        "key":  "doctor_office",
        "faces": {
            "wall_front":  f"wooden desk with stethoscope and medical chart on top, plain white wall behind, {FLAT_WALL}",
            "wall_back":   f"wooden interior door with door frame touching the floor, door knob visible, coat rack on left side, white wall, door bottom at floor level, {FLAT_WALL}",
            "wall_left":   f"medical patient examination bed with white paper roll, metal frame, centered on white wall, clinical interior, {FLAT_WALL}",
            "wall_right":  f"tall bookshelf fully filled with medical textbooks, plain white wall, {FLAT_WALL}",
            "floor":       f"seamless light oak hardwood floor planks texture, {FLAT_TEX}",
            "ceiling":     f"pure solid white blank surface, 100 percent white, no pattern, no lines, no texture, flat white paint, {FLAT_TEX}",
        },
    },
    {
        "name": "Operating Room",
        "key":  "operating_room",
        "faces": {
            "wall_front":  f"stainless steel operating table in center, sterile blue-green tiled wall behind, {FLAT_WALL}",
            "wall_back":   f"double swing hospital doors with small windows, sterile blue-green tiled wall, {FLAT_WALL}",
            "wall_left":   f"vital signs monitor on stand and anesthesia machine, sterile blue-green tiled wall, {FLAT_WALL}",
            "wall_right":  f"stainless steel scrub sink with faucet, sterile blue-green tiled wall, {FLAT_WALL}",
            "floor":       f"seamless white hospital epoxy floor texture, {FLAT_TEX}",
            "ceiling":     f"pure solid white blank surface, 100 percent white, no pattern, no lines, no texture, flat white paint, {FLAT_TEX}",
        },
    },
    {
        "name": "Medicine Room",
        "key":  "medicine_room",
        "faces": {
            "wall_front":  f"floor-to-ceiling metal shelf with labeled medicine bottles and boxes, white wall, {FLAT_WALL}",
            "wall_back":   f"pharmacy dispensing counter with computer monitor, small door on side, white wall, {FLAT_WALL}",
            "wall_left":   f"glass-door refrigerated medicine cabinet with cold storage units, white wall, {FLAT_WALL}",
            "wall_right":  f"metal shelf with blister packs and pill bottles organized by row, white wall, {FLAT_WALL}",
            "floor":       f"seamless white ceramic tile floor texture, {FLAT_TEX}",
            "ceiling":     f"pure solid white blank surface, 100 percent white, no pattern, no lines, no texture, flat white paint, {FLAT_TEX}",
        },
    },
    {
        "name": "Waiting Room",
        "key":  "waiting_room",
        "faces": {
            "wall_front":  f"row of three blue fabric waiting chairs against plain white wall, {FLAT_WALL}",
            "wall_back":   f"reception desk with sliding glass window and nurse, plain white wall, {FLAT_WALL}",
            "wall_left":   f"large window with natural daylight and one tall indoor potted plant, white wall, {FLAT_WALL}",
            "wall_right":  f"two health awareness posters framed on wall, small coffee table with magazines below, {FLAT_WALL}",
            "floor":       f"seamless light oak wood laminate floor texture, {FLAT_TEX}",
            "ceiling":     f"pure solid white blank surface, 100 percent white, no pattern, no lines, no texture, flat white paint, {FLAT_TEX}",
        },
    },
]


def load_pipeline():
    device = "cuda" if torch.cuda.is_available() else "cpu"
    logger.info(f"Loading Stable Diffusion on {device}...")
    pipe = StableDiffusionPipeline.from_pretrained(
        "runwayml/stable-diffusion-v1-5",
        torch_dtype=torch.float16 if device == "cuda" else torch.float32,
        safety_checker=None,
    ).to(device)
    pipe.scheduler = DPMSolverMultistepScheduler.from_config(
        pipe.scheduler.config, algorithm_type="dpmsolver++", use_karras_sigmas=True,
    )
    logger.info("Pipeline ready.")
    return pipe


def main():
    pipe = load_pipeline()

    for i, sc in enumerate(SCENARIOS, 1):
        asset_dir = ASSETS_DIR / sc["key"]
        logger.info(f"\n[{i}/{len(SCENARIOS)}] {sc['name']}")

        asset_dir.mkdir(exist_ok=True)

        for j, (face, prompt) in enumerate(sc["faces"].items(), 1):
            out = asset_dir / f"{face}.png"
            if out.exists():
                logger.info(f"  [{j}/6] {face} exists — skipping.")
                continue
            logger.info(f"  [{j}/6] Generating {face}...")
            img = pipe(prompt, width=WALL_SIZE, height=WALL_SIZE,
                       num_inference_steps=STEPS, guidance_scale=7.5).images[0]
            img.save(str(out))
            logger.info(f"        Saved → {out}")

    logger.info("\n✅ All presets ready! Start gradio_app_depth.py to load them into Unity.")


if __name__ == "__main__":
    main()

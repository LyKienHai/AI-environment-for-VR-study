"""
depth_estimator.py
Uses Intel DPT (via HuggingFace transformers) to generate a depth map
from the 360° panoramic image. Saves a grayscale PNG where:
  - WHITE (255) = FAR away
  - BLACK (0)   = CLOSE to camera
"""

import logging
import numpy as np
from pathlib import Path
from PIL import Image

logger = logging.getLogger(__name__)

_pipe = None


def _get_pipeline():
    global _pipe
    if _pipe is None:
        from transformers import pipeline
        logger.info("[Depth] Loading Intel DPT depth model (first run downloads ~400MB)...")
        _pipe = pipeline(
            "depth-estimation",
            model="Intel/dpt-hybrid-midas",
        )
        logger.info("[Depth] Depth model ready.")
    return _pipe


def estimate_depth(color_image_path: str, depth_image_path: str) -> str:
    """
    Generate a depth map from color_image_path and save to depth_image_path.
    Returns depth_image_path.
    """
    pipe = _get_pipeline()

    image = Image.open(color_image_path).convert("RGB")
    logger.info(f"[Depth] Running depth estimation on {color_image_path} ...")

    result = pipe(image)
    # result["depth"] is a PIL Image in float/grayscale form
    depth_pil = result["depth"]

    # Normalize to 0-255 uint8
    depth_arr = np.array(depth_pil, dtype=np.float32)
    d_min, d_max = depth_arr.min(), depth_arr.max()
    if d_max - d_min > 0:
        depth_norm = (depth_arr - d_min) / (d_max - d_min)
    else:
        depth_norm = depth_arr * 0.0

    # Resize to match color image resolution
    orig_w, orig_h = image.size
    depth_uint8 = (depth_norm * 255).astype(np.uint8)
    depth_resized = Image.fromarray(depth_uint8, mode="L").resize(
        (orig_w, orig_h), Image.BILINEAR
    )

    Path(depth_image_path).parent.mkdir(parents=True, exist_ok=True)
    depth_resized.save(depth_image_path)
    logger.info(f"[Depth] Saved depth map to {depth_image_path}")

    return depth_image_path


if __name__ == "__main__":
    import sys
    logging.basicConfig(level=logging.INFO)
    src = sys.argv[1] if len(sys.argv) > 1 else "output/scene_360.png"
    dst = src.replace("scene_360.png", "scene_360_depth.png")
    estimate_depth(src, dst)
    print(f"Depth map saved: {dst}")

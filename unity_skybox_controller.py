"""
unity_skybox_controller.py
Sends the generated 360 image path to Unity via HTTP.
Unity's AISkyboxReceiver.cs script applies it as a panoramic skybox.
"""

import os
import json
import logging
import requests
from pathlib import Path

logger = logging.getLogger(__name__)

UNITY_HOST    = os.environ.get("UNITY_HOST", "127.0.0.1")
UNITY_PORT    = int(os.environ.get("UNITY_PORT", "6000"))
UNITY_ENABLED = os.environ.get("UNITY_ENABLED", "false").lower() == "true"

_BASE = f"http://{UNITY_HOST}:{UNITY_PORT}"


def _check_connection():
    try:
        requests.get(_BASE, timeout=2)
    except requests.exceptions.ConnectionError:
        raise ConnectionError(
            f"Cannot reach Unity HTTP server at {_BASE}.\n"
            "Make sure Unity is running with AISkyboxReceiver attached to a GameObject."
        )


def apply_skybox_to_unity(image_path: str) -> None:
    if not UNITY_ENABLED:
        logger.info("Unity integration disabled (set UNITY_ENABLED=true to enable).")
        return

    abs_path = str(Path(image_path).resolve()).replace("\\", "/")
    logger.info(f"Sending skybox to Unity: {abs_path}")

    _check_connection()

    r = requests.post(
        f"{_BASE}/set_skybox",
        json={"image_path": abs_path},
        timeout=10,
    )
    r.raise_for_status()
    logger.info(f"Unity response: {r.text}")


if __name__ == "__main__":
    import sys
    logging.basicConfig(level=logging.INFO)
    img = sys.argv[1] if len(sys.argv) > 1 else "output/scene_360.png"
    apply_skybox_to_unity(img)
    print("Done.")

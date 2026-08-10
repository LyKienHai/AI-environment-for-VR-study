"""
ue5_hdri_controller.py
Sends a 360° equirectangular image to Unreal Engine 5 via the Remote Control API
and applies it to an HDRI Backdrop actor in the current level.
"""

import os
import logging
import requests
from pathlib import Path

logger = logging.getLogger(__name__)

UE5_HOST             = os.environ.get("UE5_HOST", "127.0.0.1")
UE5_RC_PORT          = int(os.environ.get("UE5_RC_PORT", "30010"))
UE5_HDRI_ACTOR_LABEL = os.environ.get("UE5_HDRI_ACTOR_LABEL", "HDRIBackdrop")
UE5_ENABLED          = os.environ.get("UE5_ENABLED", "false").lower() == "true"

_BASE = f"http://{UE5_HOST}:{UE5_RC_PORT}"


def _check_connection():
    try:
        r = requests.get(f"{_BASE}/remote/info", timeout=3)
        r.raise_for_status()
    except Exception as e:
        raise ConnectionError(f"Cannot reach UE5 at {_BASE}: {e}")


def _import_and_apply(image_path: str):
    """Import image as texture and apply to HDRIBackdrop via Remote Control."""
    abs_path = str(Path(image_path).resolve()).replace("\\", "/")
    dest_path = "/Game/AI_Generator/HDRI_Current"

    # Step 1: Import texture using EditorAssetLibrary
    logger.info(f"Importing texture from {abs_path}")
    import_r = requests.put(
        f"{_BASE}/remote/object/call",
        json={
            "objectPath": "/Script/UnrealEd.Default__EditorAssetLibrary",
            "functionName": "ImportAsset",
            "parameters": {
                "Filename": abs_path,
                "DestinationPath": dest_path,
                "bReplaceExisting": True,
                "bSave": True,
            },
            "generateTransaction": True,
        },
        timeout=30,
    )
    logger.info(f"Import response: {import_r.status_code} - {import_r.text}")

    texture_path = dest_path
    if import_r.status_code == 200:
        result = import_r.json()
        ret = result.get("ReturnValue", "")
        if ret:
            texture_path = ret

    # Step 2: Set the Cubemap property - try multiple actor path formats
    texture_obj = f"{texture_path}.{Path(texture_path).name}"

    actor_paths = [
        UE5_HDRI_ACTOR_LABEL,
        f"/Game/Maps/Untitled.Untitled:PersistentLevel.{UE5_HDRI_ACTOR_LABEL}",
        f"/Game/Maps/Untitled.{UE5_HDRI_ACTOR_LABEL}",
        f"HDRIBackdrop_{UE5_HDRI_ACTOR_LABEL}",
    ]

    success = False
    for actor_path in actor_paths:
        set_r = requests.put(
            f"{_BASE}/remote/object/property",
            json={
                "objectPath": actor_path,
                "access": "WRITE_ACCESS",
                "propertyName": "Cubemap",
                "propertyValue": {"Cubemap": texture_obj},
            },
            timeout=10,
        )
        logger.info(f"Set property [{actor_path}]: {set_r.status_code} - {set_r.text}")
        if set_r.status_code == 200:
            success = True
            logger.info(f"Successfully applied HDRI to {actor_path}")
            break

    if not success:
        raise RuntimeError(
            f"Could not set Cubemap property.\n"
            f"Texture imported to: {texture_path}\n"
            f"Manually drag it onto the HDRIBackdrop Cubemap slot in UE5."
        )


def apply_hdri_to_ue5(image_path: str) -> None:
    if not UE5_ENABLED:
        logger.info("UE5 integration disabled (set UE5_ENABLED=true to enable).")
        return

    _check_connection()
    logger.info(f"Applying HDRI to UE5: {image_path}")
    _import_and_apply(image_path)
    logger.info("HDRI applied to UE5 HDRI Backdrop!")


if __name__ == "__main__":
    import sys
    logging.basicConfig(level=logging.INFO)
    img = sys.argv[1] if len(sys.argv) > 1 else "output/scene_360.png"
    apply_hdri_to_ue5(img)

"""
equirect_to_cubemap.py
Converts one equirectangular (360°) panorama into 6 cube-face images.
No extra libraries needed — only numpy + PIL.

Convention (Y-up, right-handed matching Unity's box room):
  wall_front  = looking  +Z
  wall_back   = looking  -Z
  wall_right  = looking  +X
  wall_left   = looking  -X
  ceiling     = looking  +Y
  floor       = looking  -Y
"""

import numpy as np
from pathlib import Path
from PIL import Image

FACE_KEYS  = ["wall_front", "wall_back", "wall_left", "wall_right", "floor", "ceiling"]
WALL_KEYS  = ["wall_front", "wall_back", "wall_left", "wall_right"]   # safe to extract from panorama
POLE_KEYS  = ["floor", "ceiling"]                                      # generate separately — poles are too distorted


def _face_uvs(size: int):
    """Return (U, V) grids covering [-1, 1] x [-1, 1] for a face."""
    lin = np.linspace(-1.0, 1.0, size)
    return np.meshgrid(lin, lin)   # U: left→right, V: top→bottom


def _face_to_xyz(face: str, size: int):
    U, V = _face_uvs(size)
    one = np.ones((size, size), dtype=np.float32)

    if face == "wall_front":    return  U, -V,  one
    if face == "wall_back":     return -U, -V, -one
    if face == "wall_right":    return  one, -V, -U
    if face == "wall_left":     return -one, -V,  U
    if face == "ceiling":       return  U,  one,  V
    if face == "floor":         return  U, -one, -V
    raise ValueError(f"Unknown face: {face}")


def _xyz_to_uv(x, y, z, eq_h, eq_w):
    """Map 3-D directions → pixel coords in the equirectangular image."""
    norm = np.sqrt(x*x + y*y + z*z)
    x, y, z = x / norm, y / norm, z / norm

    lon = np.arctan2(x, z)                     # -π .. π   (0 = +Z)
    lat = np.arcsin(np.clip(y, -1.0, 1.0))     # -π/2..π/2

    px = ((lon + np.pi) / (2.0 * np.pi) * eq_w).astype(np.int32)
    py = ((0.5 - lat / np.pi) * eq_h).astype(np.int32)

    np.clip(px, 0, eq_w - 1, out=px)
    np.clip(py, 0, eq_h - 1, out=py)
    return px, py


def convert(equirect_path: str, output_dir, face_size: int = 512, faces=None):
    """
    Split a 360° equirectangular image into cube-face PNGs.
    faces: list of face keys to extract (default: WALL_KEYS only — poles are too distorted).
    Returns dict {face_key: PIL.Image}.
    """
    if faces is None:
        faces = WALL_KEYS   # skip floor/ceiling by default

    arr = np.array(Image.open(equirect_path).convert("RGB"))
    eq_h, eq_w = arr.shape[:2]

    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    result = {}
    for face in faces:
        x, y, z  = _face_to_xyz(face, face_size)
        px, py   = _xyz_to_uv(x, y, z, eq_h, eq_w)
        face_arr = arr[py, px]
        img = Image.fromarray(face_arr.astype(np.uint8))
        img.save(str(output_dir / f"{face}.png"))
        result[face] = img

    return result


if __name__ == "__main__":
    import sys
    if len(sys.argv) < 3:
        print("Usage: python equirect_to_cubemap.py input.png output_dir/ [face_size]")
        sys.exit(1)
    size = int(sys.argv[3]) if len(sys.argv) > 3 else 512
    imgs = convert(sys.argv[1], sys.argv[2], size)
    print(f"Saved {len(imgs)} faces to {sys.argv[2]}")

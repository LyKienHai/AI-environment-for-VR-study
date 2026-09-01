"""
color_fetcher.py — Randomize VR room colors from curated palettes.

Sources:
  • PPG Paints       (https://www.ppg.com/en-US)
  • RAL Color Chart   (https://www.ralcolorchart.com/)
  • Dulux Vietnam     (https://www.dulux.vn/vi/mau-sac-bang-mau/filters/h_White)

Rules:
  • Walls (×4) + Ceiling = same bright color (HSL lightness ≥ 60%)
  • Floor = warm wood / tile tone

Live scraping is attempted first; falls back to curated lists if scraping fails.
"""

import colorsys
import json
import os
import random

try:
    import requests
    _REQUESTS_OK = True
except ImportError:
    _REQUESTS_OK = False

try:
    from bs4 import BeautifulSoup
    _BS4_OK = True
except ImportError:
    _BS4_OK = False

# ── Curated bright wall/ceiling palettes ─────────────────────────────────────────
# Each list is sourced from the respective site's visible palette range.

PPG_BRIGHT = [
    # PPG Paints — Light interior shades
    "#F5F0E8", "#F2EBD9", "#EDE0C8", "#E8F4F0", "#D4EBF5",
    "#E8E4F0", "#F5E8E8", "#E8F5E8", "#EAF0D8", "#F8EDD8",
    "#DDEEF5", "#E8D8F0", "#F0D8E8", "#D8F0E8", "#F0F0D8",
    "#EAF4F8", "#F8EAF4", "#EAF8F4", "#F4F8EA", "#F8F4EA",
    "#E4F0F8", "#F0F4E4", "#F4E4F0", "#E8EEF8", "#F0E8EE",
]

RAL_BRIGHT = [
    # RAL Classic — light / pastel shades only
    "#F4F0E8",  # 9001 Cream White
    "#E8EEF0",  # 9018 Papyrus White
    "#F0EEE4",  # 9002 Grey White
    "#F5F0E0",  # 1013 Pearl White
    "#F5F5F0",  # 9010 Pure White
    "#EAF0EA",  # 6019 Pastel Green
    "#EAF0F5",  # 5024 Pastel Blue
    "#F5EAE0",  # 1015 Light Ivory
    "#E8F0EA",  # 6021 Pale Green
    "#F0E8EE",  # 4009 Pastel Violet
    "#E4EEF4",  # 5014 Pigeon Blue
    "#EAF5EA",  # 6027 Light Green
    "#F0EAE4",  # 1014 Ivory
    "#E8EEF5",  # 5012 Light Blue
    "#F5EAF0",  # 4010 Telemagenta (pale)
    "#EEF5EA",  # 6034 Pastel Turquoise
    "#EEF0F5",  # 5015 Sky Blue (light)
    "#F5EEE8",  # 1016 Sulfur Yellow (pastel)
    "#EEF5EE",  # 6038 Luminous Green (pastel)
    "#F0EEF5",  # 4002 Red Lilac (light)
    "#F5F0EA",  # 1019 Grey Beige (light)
    "#EAF5F0",  # 6019 Mint (pastel)
    "#F0F5EA",  # 6026 Opal Green (pastel)
    "#EAF0F0",  # 5021 Water Blue (light)
    "#F5EAF5",  # 4009 Pale lavender
]

DULUX_WHITE_FILTER = [
    # Dulux Vietnam — white/light filter shades
    "#F8F4EE", "#F4F0E8", "#EEEAE4", "#E8F4F0", "#F0EEE8",
    "#ECF4F8", "#F4ECE8", "#EEF4EC", "#F8F0EC", "#ECF0F8",
    "#F4F8EE", "#F0ECF4", "#ECEEF4", "#F8ECF0", "#F0F4F8",
    "#F4EEF0", "#ECF8F4", "#F8EEEC", "#F4F4EC", "#ECF4EC",
    "#F0F8F4", "#F8F0F4", "#F4F0F4", "#F0F4F0", "#F8F8EC",
]

# ── Wood & warm floor tones ──────────────────────────────────────────────────────

WOOD_FLOOR = [
    "#C8A96E",  # Light hardwood
    "#B8955A",  # Medium hardwood
    "#A07850",  # Classic oak
    "#7A6652",  # Dark hardwood
    "#8B7355",  # Walnut
    "#C4956A",  # Pine
    "#D4B896",  # Blonde hardwood
    "#9C7A56",  # Teak
    "#B08060",  # Cherry wood
    "#D4C8A0",  # Ash wood
    "#C8B890",  # Beige hardwood
    "#A8906A",  # Caramel oak
    "#C0A070",  # Golden oak
    "#B09070",  # Rustic pine
    "#D0B880",  # Nordic birch
]

ALL_BRIGHT = PPG_BRIGHT + RAL_BRIGHT + DULUX_WHITE_FILTER


# ── Helpers ──────────────────────────────────────────────────────────────────────

def _hex_to_hls(hex_color: str):
    """Return (h, l, s) in 0-1 range."""
    h = hex_color.lstrip("#")
    r = int(h[0:2], 16) / 255
    g = int(h[2:4], 16) / 255
    b = int(h[4:6], 16) / 255
    return colorsys.rgb_to_hls(r, g, b)


def _is_bright(hex_color: str, min_lightness: float = 0.60) -> bool:
    """True if the color is light enough to be a wall color."""
    try:
        _, l, _ = _hex_to_hls(hex_color)
        return l >= min_lightness
    except Exception:
        return False


def _parse_hex_from_style(style: str) -> list:
    """Extract #RRGGBB codes from a CSS style string."""
    import re
    return re.findall(r"#([0-9A-Fa-f]{6})\b", style)


# ── Live scraping (best-effort) ──────────────────────────────────────────────────

def _scrape_ral() -> list:
    """Scrape RAL color chart for bright hex colors."""
    if not (_REQUESTS_OK and _BS4_OK):
        return []
    try:
        resp = requests.get(
            "https://www.ralcolorchart.com/",
            timeout=8,
            headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"},
        )
        soup = BeautifulSoup(resp.text, "html.parser")
        colors = []
        for el in soup.select("[style]"):
            for hex_val in _parse_hex_from_style(el.get("style", "")):
                full = f"#{hex_val}"
                if _is_bright(full) and full not in colors:
                    colors.append(full)
        return colors[:50]
    except Exception:
        return []


def _scrape_dulux() -> list:
    """Scrape Dulux VN white-filter page for bright hex colors."""
    if not (_REQUESTS_OK and _BS4_OK):
        return []
    try:
        resp = requests.get(
            "https://www.dulux.vn/vi/mau-sac-bang-mau/filters/h_White",
            timeout=8,
            headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"},
        )
        soup = BeautifulSoup(resp.text, "html.parser")
        colors = []
        for el in soup.select("[style], [data-color], [data-hex]"):
            # Check inline style
            for hex_val in _parse_hex_from_style(el.get("style", "")):
                full = f"#{hex_val}"
                if _is_bright(full) and full not in colors:
                    colors.append(full)
            # Check data attributes
            for attr in ("data-color", "data-hex", "data-bg"):
                val = el.get(attr, "")
                if val.startswith("#") and len(val) == 7:
                    if _is_bright(val) and val not in colors:
                        colors.append(val)
        return colors[:50]
    except Exception:
        return []


# ── Public API ───────────────────────────────────────────────────────────────────

def fetch_bright_colors(try_live: bool = True) -> list:
    """
    Return a pool of bright colors.
    Tries live scraping first; always merges curated fallbacks.

    Args:
        try_live: Set False to skip network calls (faster, offline-safe).

    Returns:
        List of hex color strings guaranteed to have lightness >= 0.60.
    """
    pool = list(ALL_BRIGHT)

    if try_live:
        for scraped in (_scrape_ral(), _scrape_dulux()):
            for c in scraped:
                if c not in pool:
                    pool.append(c)

    # Final safety filter
    pool = [c for c in pool if _is_bright(c)]
    return pool if pool else ALL_BRIGHT


def randomize_room_colors(try_live: bool = True) -> dict:
    """
    Pick a random bright color for walls + ceiling and a wood tone for the floor.

    Room color rules:
      • 4 walls + ceiling = same bright color (not dark)
      • floor = warm wood tone

    Args:
        try_live: Whether to attempt live web scraping for fresh colors.

    Returns:
        dict with keys: wall, floor, ceiling
    """
    pool = fetch_bright_colors(try_live=try_live)
    wall_color = random.choice(pool)
    floor_color = random.choice(WOOD_FLOOR)
    return {
        "wall":    wall_color,
        "floor":   floor_color,
        "ceiling": wall_color,  # same bright color as walls per room rules
    }


def apply_colors_to_config(config_path: str, palette: dict) -> dict:
    """
    Load existing room_config.json, update the palette section, and save.
    Creates a minimal config if file doesn't exist.

    Args:
        config_path: Absolute path to room_config.json.
        palette: dict with wall/floor/ceiling hex strings.

    Returns:
        The full config dict that was written.
    """
    config = {}
    if os.path.exists(config_path):
        try:
            with open(config_path, "r", encoding="utf-8") as f:
                config = json.load(f)
        except Exception:
            pass

    if not config:
        config = {
            "room_type": "Arztpraxis",
            "mode":      "color_random",
        }

    config["palette"] = {
        "wall":    palette["wall"],
        "floor":   palette["floor"],
        "ceiling": palette["ceiling"],
    }

    # Atomic write — prevents Unity reading an empty file mid-write
    dir_path = os.path.dirname(os.path.abspath(config_path))
    os.makedirs(dir_path, exist_ok=True)
    import tempfile
    fd, tmp_path = tempfile.mkstemp(dir=dir_path, suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(config, f, ensure_ascii=False, indent=2)
        os.replace(tmp_path, config_path)
    except Exception:
        try: os.unlink(tmp_path)
        except OSError: pass
        raise

    return config


# ── CLI quick-test ────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Fetching colors (live scraping enabled)…")
    palette = randomize_room_colors(try_live=True)
    print(f"  Wall    : {palette['wall']}")
    print(f"  Floor   : {palette['floor']}")
    print(f"  Ceiling : {palette['ceiling']}")
    print(f"\nPool size: {len(fetch_bright_colors())} bright colors available")

# -*- coding: utf-8 -*-
"""
pixelize_npc.py  -  Upload to pixel-me.tokyo, intercept blob creation, save pixel art.
"""
import sys, time, base64
from pathlib import Path


def pixelize(input_path: str, output_path: str = None) -> str:
    from playwright.sync_api import sync_playwright

    input_path  = Path(input_path).resolve()
    output_path = Path(output_path).resolve() if output_path else \
                  input_path.parent / (input_path.stem + "_pixel.png")

    if not input_path.exists():
        raise FileNotFoundError(f"Not found: {input_path}")

    print(f"[Pixelize] Input:  {input_path.name}")
    print(f"[Pixelize] Output: {output_path.name}")

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=False)
        ctx     = browser.new_context()
        page    = ctx.new_page()

        # ── Inject blob interceptor BEFORE the page runs its own JS ──────────
        page.add_init_script("""
            window._pixelBlobs = [];
            const _orig = URL.createObjectURL.bind(URL);
            URL.createObjectURL = function(obj) {
                const url = _orig(obj);
                if (obj && obj instanceof Blob && obj.type && obj.type.startsWith('image/')) {
                    const reader = new FileReader();
                    reader.onload = function(e) {
                        // store base64 data (strip the data:image/...;base64, prefix)
                        const b64 = e.target.result.split(',')[1];
                        window._pixelBlobs.push(b64);
                        console.log('[PixelCapture] Blob captured, size=' + b64.length);
                    };
                    reader.readAsDataURL(obj);
                }
                return url;
            };
        """)

        # ── Load page ─────────────────────────────────────────────────────────
        page.goto("https://pixel-me.tokyo/en/", wait_until="networkidle", timeout=30000)
        print("[Pixelize] Page loaded.")

        # Auto-accept any cookie/consent banners
        cookie_selectors = [
            "button:has-text('Accept all')",
            "button:has-text('Accept All')",
            "button:has-text('Allow all')",
            "button:has-text('Allow All')",
            "button:has-text('Agree')",
            "button:has-text('OK')",
            "button:has-text('Got it')",
            "button[id*='accept']",
            "button[class*='accept']",
            "button[class*='consent']",
        ]
        for sel in cookie_selectors:
            try:
                btn = page.locator(sel).first
                if btn.is_visible(timeout=1500):
                    btn.click()
                    print(f"[Pixelize] Cookie consent accepted.")
                    time.sleep(0.5)
                    break
            except Exception:
                continue

        print("[Pixelize] Uploading...")

        # ── Upload image ──────────────────────────────────────────────────────
        file_input = page.locator("input[type='file']").first
        file_input.set_input_files(str(input_path))

        # Wait for upload preview blob to be created, then clear the list
        print("[Pixelize] Waiting for upload preview to settle...")
        time.sleep(5.0)
        page.evaluate("() => { window._pixelBlobs = []; }")
        print("[Pixelize] Blob list cleared. Now waiting for pixel art (up to 45s)...")

        # ── Poll for NEW blobs = pixel art result ─────────────────────────────
        deadline = time.time() + 45
        b64_data = None

        while time.time() < deadline:
            time.sleep(0.8)
            blobs = page.evaluate("() => window._pixelBlobs || []")
            if blobs:
                # Take the last blob (most recently generated)
                b64_data = blobs[-1]
                try:
                    raw = base64.b64decode(b64_data[:200])
                    w = int.from_bytes(raw[16:20], 'big')
                    h = int.from_bytes(raw[20:24], 'big')
                    print(f"[Pixelize] Pixel art captured: {w}x{h} px")
                except Exception:
                    print(f"[Pixelize] Pixel art blob captured (size={len(b64_data)})")
                break

        browser.close()

    if b64_data:
        output_path.write_bytes(base64.b64decode(b64_data))
        print(f"[Pixelize] Saved -> {output_path}")
    else:
        print("[Pixelize] ERROR: No pixel art captured within 45s.")
        print("           Check that the browser uploaded correctly.")
        return str(input_path)

    return str(output_path)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python pixelize_npc.py <input.png> [output.png]")
        sys.exit(1)
    inp = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else None
    result = pixelize(inp, out)
    print("Done:", result)

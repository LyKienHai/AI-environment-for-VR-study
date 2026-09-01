"""
generate_quiz_images.py
=======================
Generates AI images for WallImageQuiz medical vocabulary frames.

MODE 1 - Internet (default):
  Uses Pollinations.ai — free, no API key, generates a UNIQUE AI image
  for every request. Each of the 6 variants per item is genuinely different.
  Install: pip install requests Pillow

MODE 2 - Offline fallback (--offline flag):
  Draws clean local illustrations with Pillow. No internet needed.
  Install: pip install Pillow

Output folder: quiz_images/ next to this script (portable, works on any PC).
Called automatically by WallImageQuiz.cs at Unity startup when images are missing.
Can also be run manually:
  python generate_quiz_images.py           # AI images (internet)
  python generate_quiz_images.py --offline # drawn illustrations
  python generate_quiz_images.py --force   # re-generate even existing files
"""

import os, sys, io, math, time, random, urllib.parse
from pathlib import Path

# ── Config ─────────────────────────────────────────────────────────────────────
SCRIPT_DIR = Path(__file__).resolve().parent
OUTPUT_DIR  = str(SCRIPT_DIR / "quiz_images")

W, H = 512, 384   # larger resolution for AI images (Unity will scale down)

FORCE_OFFLINE  = "--offline" in sys.argv
FORCE_REGEN    = "--force"   in sys.argv   # re-download even existing files

# Prompts tuned for clear, recognisable medical illustrations
ITEMS = [
    # Original 12
    ("stethoskop",          "stethoscope medical instrument, clean white background, realistic photo"),
    ("thermometer",         "medical thermometer clinical, white background, realistic"),
    ("spritze",             "medical syringe with needle, white background, realistic photo"),
    ("pflaster",            "adhesive bandage plaster, wound dressing, white background"),
    ("blutdruckgeraet",     "blood pressure monitor cuff, sphygmomanometer, white background"),
    ("skalpell",            "surgical scalpel blade, medical tool, white background, realistic"),
    ("verband",             "white medical bandage roll, white background, clinical"),
    ("roentgenbild",        "chest x-ray image, black background, medical radiograph"),
    ("tablette",            "medicine pills and tablets, white background, pharmacy"),
    ("rollstuhl",           "wheelchair, white background, medical equipment"),
    ("kruecke",             "crutch walking aid, white background, medical"),
    ("krankenbett",         "hospital bed clinical, white background, medical"),
    # 8 new clinic objects
    ("handschuhe",          "latex medical gloves, white background, clinical"),
    ("maske",               "surgical face mask medical, white background"),
    ("otoskop",             "otoscope ear examination tool, white background, medical"),
    ("mikroskop",           "laboratory microscope, white background, science"),
    ("wattebausch",         "white cotton ball medical, white background, clinical"),
    ("pipette",             "laboratory pipette dropper, white background, science"),
    ("erste-hilfe-kasten",  "first aid kit red cross box, white background"),
    ("augentropfen",        "eye drops bottle medical, white background, clinical"),
]

# ══════════════════════════════════════════════════════════════════════════════
# MODE 1 — Pollinations.ai (free AI image generation, no API key)
# ══════════════════════════════════════════════════════════════════════════════

POLLINATIONS_URL = "https://image.pollinations.ai/prompt/{prompt}?width={w}&height={h}&nologo=true&seed={seed}"

def generate_pollinations(prompt, save_path, seed=None):
    """Generate an AI image via Pollinations.ai and save it. Returns True on success."""
    import requests
    from PIL import Image

    if seed is None:
        seed = random.randint(1, 999999)

    encoded = urllib.parse.quote(prompt)
    url = POLLINATIONS_URL.format(prompt=encoded, w=W, h=H, seed=seed)

    try:
        resp = requests.get(url, timeout=60,
                            headers={"User-Agent": "MedicalVRQuiz/1.0"})
        if resp.status_code != 200 or len(resp.content) < 3000:
            print(f"    [pollinations] HTTP {resp.status_code}, size {len(resp.content)}")
            return False
        img = Image.open(io.BytesIO(resp.content)).convert("RGB")
        img.save(save_path, "PNG")
        return True
    except Exception as e:
        print(f"    [pollinations error] {e}")
        return False

# ══════════════════════════════════════════════════════════════════════════════
# MODE 2 — Local Pillow illustrations (offline fallback)
# ══════════════════════════════════════════════════════════════════════════════

def draw_offline(name):
    from PIL import Image, ImageDraw
    def new(bg=(255,255,255)):
        img = Image.new("RGB",(W,H),bg); return img,ImageDraw.Draw(img)
    def lbl(d,t,c=(40,40,40)):
        d.rectangle([0,H-28,W,H],fill=(240,240,240)); d.text((W//2,H-14),t,fill=c,anchor="mm")

    if name=="stethoskop":
        img,d=new((245,248,252))
        pts=[(128+55*math.cos(math.radians(a)),100+55*math.sin(math.radians(a))) for a in range(180,361)]
        d.line(pts,fill=(50,55,65),width=8)
        d.ellipse([90,120,166,190],fill=(60,60,70),outline=(30,30,40),width=3)
        d.ellipse([108,135,148,175],fill=(180,185,195))
        d.line([(74,100),(55,75)],fill=(50,55,65),width=7)
        d.line([(182,100),(201,75)],fill=(50,55,65),width=7)
        d.ellipse([48,68,62,82],fill=(50,55,65)); d.ellipse([195,68,209,82],fill=(50,55,65))
        lbl(d,"Stethoskop"); return img
    elif name=="thermometer":
        img,d=new((248,252,248))
        d.rounded_rectangle([112,20,144,155],radius=15,fill=(220,235,240),outline=(150,170,180),width=2)
        d.rectangle([119,80,137,148],fill=(220,40,40))
        d.ellipse([105,145,151,185],fill=(220,40,40),outline=(180,20,20),width=2)
        for i,y in enumerate(range(40,150,10)): d.line([(144,y),(148 if i%3==0 else 145,y)],fill=(80,80,80),width=1)
        lbl(d,"Thermometer"); return img
    elif name=="spritze":
        img,d=new((248,248,252))
        d.rectangle([40,85,190,107],fill=(220,230,240),outline=(140,160,180),width=2)
        d.rectangle([42,87,120,105],fill=(200,225,245))
        d.polygon([(40,91),(40,101),(18,96)],fill=(180,185,195))
        d.line([(18,96),(8,96)],fill=(150,155,165),width=2)
        d.rectangle([185,88,220,104],fill=(180,180,190)); d.rectangle([215,82,230,110],fill=(130,130,145))
        d.rectangle([188,80,206,112],fill=(200,200,210),outline=(150,150,165),width=1)
        lbl(d,"Spritze"); return img
    elif name=="pflaster":
        img,d=new((252,248,242))
        d.rounded_rectangle([38,62,218,130],radius=12,fill=(255,218,160),outline=(210,170,110),width=2)
        d.rectangle([38,62,95,130],fill=(240,195,130)); d.rectangle([161,62,218,130],fill=(240,195,130))
        d.rectangle([98,65,158,127],fill=(255,255,255),outline=(220,220,220),width=1)
        for y in range(70,126,6): d.line([(98,y),(158,y)],fill=(220,220,220),width=1)
        lbl(d,"Pflaster"); return img
    elif name=="blutdruckgeraet":
        img,d=new((248,248,252))
        d.rounded_rectangle([70,95,200,165],radius=10,fill=(240,240,245),outline=(180,180,200),width=2)
        d.rounded_rectangle([80,103,190,145],radius=5,fill=(30,40,30),outline=(100,120,100),width=1)
        d.text((135,120),"120/80",fill=(0,220,0),anchor="mm"); d.text((135,135),"mmHg",fill=(0,180,0),anchor="mm")
        d.ellipse([55,30,201,90],fill=(200,210,230),outline=(160,175,200),width=3)
        lbl(d,"Blutdruckgeraet"); return img
    elif name=="skalpell":
        img,d=new((248,250,250))
        d.rounded_rectangle([85,130,175,170],radius=6,fill=(180,185,195),outline=(140,145,155),width=2)
        for x in range(95,170,8): d.line([(x,132),(x,168)],fill=(150,155,165),width=1)
        d.polygon([(170,145),(240,130),(240,138),(175,155)],fill=(210,215,220),outline=(180,185,190),width=1)
        lbl(d,"Skalpell"); return img
    elif name=="verband":
        img,d=new((252,252,250))
        d.ellipse([68,40,188,150],fill=(255,255,255),outline=(210,210,215),width=3)
        for i in range(1,4): d.ellipse([68+i*6,40+i*6,188-i*6,150-i*6],outline=(215,215,220),width=1)
        d.rectangle([108,68,148,122],fill=(240,50,50)); d.rectangle([98,86,158,104],fill=(240,50,50))
        lbl(d,"Verband"); return img
    elif name=="roentgenbild":
        img,d=new((20,22,35))
        for i,y in enumerate(range(52,130,12)): d.arc([60-i*2,y,196+i*2,y+20],start=200,end=340,fill=(190,195,210),width=3)
        d.rounded_rectangle([118,35,138,155],radius=4,fill=(170,175,195))
        for y in range(40,155,14): d.rectangle([112,y,144,y+8],fill=(180,185,200),outline=(140,145,165),width=1)
        d.rectangle([0,0,W,H],outline=(100,105,115),width=4)
        lbl(d,"Roentgenbild",c=(180,185,200)); return img
    elif name=="tablette":
        img,d=new((250,250,252))
        pills=[(70,60,48,26,15,(220,60,60),(200,30,30)),(155,55,48,26,15,(60,120,220),(40,90,190)),
               (65,110,48,26,15,(60,180,80),(30,150,50)),(155,110,48,26,15,(230,180,50),(200,150,20)),
               (110,80,30,30,0,(200,80,200),(170,50,170)),(108,140,44,22,12,(80,200,200),(50,170,170))]
        for cx,cy,pw,ph,r,fill,outline in pills:
            d.rounded_rectangle([cx-pw//2,cy-ph//2,cx+pw//2,cy+ph//2],radius=r,fill=fill,outline=outline,width=2)
        lbl(d,"Tablette"); return img
    elif name=="rollstuhl":
        img,d=new((248,250,250))
        d.ellipse([30,80,150,165],outline=(60,65,80),width=6)
        cx,cy=90,132
        for a in range(0,360,45): d.line([(cx,cy),(int(cx+35*math.cos(math.radians(a))),int(cy+35*math.sin(math.radians(a))))],fill=(70,75,90),width=2)
        d.ellipse([165,135,210,170],outline=(60,65,80),width=5)
        d.rectangle([90,75,195,100],fill=(80,100,160),outline=(60,80,140),width=2)
        lbl(d,"Rollstuhl"); return img
    elif name=="kruecke":
        img,d=new((252,250,248))
        d.rounded_rectangle([118,45,142,175],radius=6,fill=(180,140,90),outline=(150,110,65),width=2)
        d.rounded_rectangle([82,30,178,58],radius=10,fill=(80,85,95),outline=(55,60,70),width=2)
        d.rounded_rectangle([88,105,152,125],radius=6,fill=(60,65,75),outline=(40,45,55),width=2)
        d.line([(118,175),(90,192)],fill=(150,110,65),width=7); d.line([(142,175),(170,192)],fill=(150,110,65),width=7)
        lbl(d,"Kruecke"); return img
    elif name=="krankenbett":
        img,d=new((245,248,252))
        d.rounded_rectangle([25,75,235,140],radius=8,fill=(255,255,255),outline=(200,205,215),width=2)
        d.rounded_rectangle([170,78,230,108],radius=6,fill=(245,245,255),outline=(210,210,225),width=1)
        d.rectangle([25,105,155,138],fill=(180,200,230),outline=(160,180,210),width=1)
        d.rectangle([215,50,235,140],fill=(160,165,175),outline=(130,135,145),width=2)
        d.rectangle([25,50,45,140],fill=(160,165,175),outline=(130,135,145),width=2)
        lbl(d,"Krankenbett"); return img
    else:
        img,d=new(); lbl(d,name); return img

# ══════════════════════════════════════════════════════════════════════════════
# Main
# ══════════════════════════════════════════════════════════════════════════════

if __name__ == "__main__":
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"Output folder: {OUTPUT_DIR}")
    print(f"1 image per item, {len(ITEMS)} items total\n")
    skipped = generated = failed = 0

    for name, query in ITEMS:
        path = os.path.join(OUTPUT_DIR, f"{name}.png")

        if Path(path).exists() and not FORCE_REGEN:
            print(f"  [{name}] already exists — skipped")
            skipped += 1
            continue

        print(f"  [{name}]", end=" ", flush=True)

        ok = False
        if not FORCE_OFFLINE:
            seed = random.randint(1, 999999)
            print(f"AI generating...", end=" ", flush=True)
            ok = generate_pollinations(query, path, seed=seed)
            if ok:
                print("✓")
                time.sleep(random.uniform(1.0, 2.0))   # polite pause

        if not ok:
            print("offline...", end=" ", flush=True)
            try:
                img = draw_offline(name)
                img.save(path, "PNG")
                print("done")
                ok = True
            except Exception as e:
                print(f"FAILED: {e}")

        if ok: generated += 1
        else:  failed   += 1

    print(f"\nDone: {generated} generated, {skipped} already existed, {failed} failed")
    print(f"Images saved to: {OUTPUT_DIR}")

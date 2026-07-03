#!/usr/bin/env python3
"""
Generador determinista de iconos para IDPersonalSecure.

Produce:
  - Android: mipmaps (mdpi..xxxhdpi) ic_launcher / ic_launcher_round / ic_launcher_foreground,
             icono adaptativo (anydpi-v26) y color de fondo.
  - Windows: Assets/app.ico multi-tamaño (16..256).

Diseño: escudo blanco con check sobre degradado índigo→violeta (identidad "ID segura").
Requiere Pillow.  Uso:  python tools/gen_icons.py
"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ANDROID_RES = os.path.join(ROOT, "android", "app", "src", "main", "res")
WIN_ASSETS = os.path.join(ROOT, "windows", "IDPersonalSecure", "Assets")

BG1 = (79, 70, 229)    # índigo #4F46E5 (arriba)
BG2 = (124, 58, 237)   # violeta #7C3AED (abajo)
SHIELD = (255, 255, 255)
CHECK = (79, 70, 229)
BG_COLOR_HEX = "#4F46E5"

SS = 4  # supersampling para bordes suaves

# Escudo normalizado en caja [0,1] (y hacia abajo)
SHIELD_PTS = [
    (0.50, 0.05), (0.88, 0.20), (0.88, 0.54),
    (0.68, 0.80), (0.50, 0.95), (0.32, 0.80),
    (0.12, 0.54), (0.12, 0.20),
]
CHECK_PTS = [(0.34, 0.53), (0.45, 0.66), (0.69, 0.34)]


def make_gradient(s: int) -> Image.Image:
    col = Image.new("L", (1, s))
    col.putdata([int(255 * y / (s - 1)) for y in range(s)])
    mask = col.resize((s, s))
    top = Image.new("RGB", (s, s), BG2)
    base = Image.new("RGB", (s, s), BG1)
    return Image.composite(top, base, mask).convert("RGBA")


def draw_emblem(img: Image.Image, cx: float, cy: float, size: float):
    d = ImageDraw.Draw(img)
    def m(pts):
        return [(cx + (nx - 0.5) * size, cy + (ny - 0.5) * size) for nx, ny in pts]
    d.polygon(m(SHIELD_PTS), fill=SHIELD)
    pts = m(CHECK_PTS)
    w = max(2, int(size * 0.075))
    d.line(pts, fill=CHECK, width=w, joint="curve")
    r = w / 2
    for (x, y) in pts:
        d.ellipse([x - r, y - r, x + r, y + r], fill=CHECK)


def render(size: int, mode: str) -> Image.Image:
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    if mode in ("square", "round"):
        grad = make_gradient(s)
        mask = Image.new("L", (s, s), 0)
        md = ImageDraw.Draw(mask)
        if mode == "round":
            md.ellipse([0, 0, s - 1, s - 1], fill=255)
        else:
            md.rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 0.22), fill=255)
        img.paste(grad, (0, 0), mask)
        draw_emblem(img, s / 2, s / 2, s * 0.60)
    else:  # foreground para icono adaptativo (zona segura ~66%)
        draw_emblem(img, s / 2, s / 2, s * 0.52)
    return img.resize((size, size), Image.LANCZOS)


def write_android():
    densities = {
        "mdpi": (48, 108), "hdpi": (72, 162), "xhdpi": (96, 216),
        "xxhdpi": (144, 324), "xxxhdpi": (192, 432),
    }
    for name, (legacy, fg) in densities.items():
        d = os.path.join(ANDROID_RES, f"mipmap-{name}")
        os.makedirs(d, exist_ok=True)
        render(legacy, "square").save(os.path.join(d, "ic_launcher.png"))
        render(legacy, "round").save(os.path.join(d, "ic_launcher_round.png"))
        render(fg, "fg").save(os.path.join(d, "ic_launcher_foreground.png"))

    anydpi = os.path.join(ANDROID_RES, "mipmap-anydpi-v26")
    os.makedirs(anydpi, exist_ok=True)
    adaptive = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n'
        '    <background android:drawable="@color/ic_launcher_background" />\n'
        '    <foreground android:drawable="@mipmap/ic_launcher_foreground" />\n'
        '</adaptive-icon>\n'
    )
    with open(os.path.join(anydpi, "ic_launcher.xml"), "w", encoding="utf-8") as f:
        f.write(adaptive)
    with open(os.path.join(anydpi, "ic_launcher_round.xml"), "w", encoding="utf-8") as f:
        f.write(adaptive)

    values = os.path.join(ANDROID_RES, "values")
    os.makedirs(values, exist_ok=True)
    with open(os.path.join(values, "ic_launcher_background.xml"), "w", encoding="utf-8") as f:
        f.write(
            '<?xml version="1.0" encoding="utf-8"?>\n<resources>\n'
            f'    <color name="ic_launcher_background">{BG_COLOR_HEX}</color>\n</resources>\n'
        )


def write_windows():
    os.makedirs(WIN_ASSETS, exist_ok=True)
    master = render(256, "square")
    master.save(os.path.join(WIN_ASSETS, "app.ico"),
                sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    master.save(os.path.join(WIN_ASSETS, "app.png"))


if __name__ == "__main__":
    write_android()
    write_windows()
    print("Iconos generados:")
    print(f"  Android -> {ANDROID_RES}")
    print(f"  Windows -> {WIN_ASSETS}")

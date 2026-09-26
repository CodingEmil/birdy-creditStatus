"""Render the kingfisher design study without touching the active app icon.

Authoring dependencies: pillow==12.2.0, resvg-py==0.5.0
Run from any directory: python scripts/preview-icon-variants.py
"""

from io import BytesIO
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import resvg_py

ROOT = Path(__file__).resolve().parents[1]
DESIGNS = ROOT / "docs" / "design" / "eisvogel-varianten"
VARIANTS = (
    ("01-natur-flat", "01  Natur-Flat", "Natürliche Form, klare Farbflächen"),
    ("02-geometrisch", "02  Geometrisch", "Kantig, reduziert, plakativ"),
    ("03-outline", "03  Outline", "Leichte Kontur mit orangefarbenem Akzent"),
    ("04-im-flug", "04  Papier-Look / Flug", "Geschichtete Flächen, dynamische Silhouette"),
    ("05-sanft-schattiert", "05  Sanft schattiert", "Mehr Tiefe, dezentes Licht und Gefieder"),
    ("06-emblem", "06  Emblem", "Nahaufnahme mit dunklem, rundem Hintergrund"),
)


def font(size, bold=False):
    name = "seguisb.ttf" if bold else "segoeui.ttf"
    path = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts" / name
    return ImageFont.truetype(str(path), size) if path.exists() else ImageFont.load_default(size=size)


def render(source, size):
    png = resvg_py.svg_to_bytes(
        svg_path=str(source), width=size * 4, height=size * 4, skip_system_fonts=True
    )
    with Image.open(BytesIO(png)) as image:
        return image.convert("RGBA").resize((size, size), Image.Resampling.LANCZOS)


def main():
    sheet = Image.new("RGB", (1392, 936), "#E5EAF0")
    draw = ImageDraw.Draw(sheet)
    draw.text((24, 18), "EISVOGEL — SECHS RICHTUNGEN", font=font(29, True), fill="#18364A")
    draw.text(
        (25, 61), "Großansicht + echte Tray-Größen (16 / 24 / 32 px), jeweils auf hellem und dunklem Hintergrund.",
        font=font(15), fill="#5B6D7F",
    )
    for index, (stem, title, subtitle) in enumerate(VARIANTS):
        source = DESIGNS / f"{stem}.svg"
        frames = {size: render(source, size) for size in (16, 24, 32, 176, 512)}
        frames[512].save(DESIGNS / f"{stem}.png")
        x, y = 24 + (index % 3) * 456, 106 + (index // 3) * 404
        draw.rounded_rectangle((x, y, x + 432, y + 380), radius=16, fill="#FFFFFF")
        draw.text((x + 16, y + 13), title, font=font(21, True), fill="#18364A")
        draw.text((x + 16, y + 45), subtitle, font=font(13), fill="#657587")
        for offset, background, text, label in (
            (16, "#EEF2F5", "#64778A", "HELL"),
            (224, "#202B36", "#A4B6C5", "DUNKEL"),
        ):
            px, py = x + offset, y + 76
            draw.rounded_rectangle((px, py, px + 192, py + 288), radius=10, fill=background)
            sheet.paste(frames[176], (px + 8, py + 10), frames[176])
            draw.text((px + 12, py + 191), label, font=font(10, True), fill=text)
            for size, center in ((16, 35), (24, 94), (32, 154)):
                sheet.paste(frames[size], (px + center - size // 2, py + 239 - size // 2), frames[size])
                draw.text((px + center - 13, py + 262), f"{size} px", font=font(10), fill=text)
    output = DESIGNS / "vergleich.png"
    sheet.save(output)
    print(f"Rendered {len(VARIANTS)} variants and {output.relative_to(ROOT)}")


if __name__ == "__main__":
    main()

"""Export the Birdy SVG to a Windows ICO and a light/dark preview.

Optional authoring tools (not needed to build the app):
    python -m pip install pillow==12.2.0 resvg-py==0.5.0
    python scripts/generate-icon.py
    python scripts/generate-icon.py --check
"""

import argparse
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw
import resvg_py

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "BirdyCreditStatus" / "Assets"
SIZES = (16, 20, 24, 28, 32, 40, 48, 64, 96, 128, 256)


def render(size):
    # Render each size independently with supersampling, never from a smaller ICO frame.
    png = resvg_py.svg_to_bytes(
        svg_path=str(ASSETS / "BirdyIcon.svg"),
        width=size * 8,
        height=size * 8,
        skip_system_fonts=True,
    )
    with Image.open(BytesIO(png)) as image:
        return image.convert("RGBA").resize((size, size), Image.Resampling.LANCZOS)


def preview(frames):
    sheet = Image.new("RGB", (880, 420), "#EEF1F5")
    draw = ImageDraw.Draw(sheet)
    draw.rectangle((440, 0, 879, 419), fill="#20242C")
    for left, label, text in ((0, "LIGHT", "#535D6D"), (440, "DARK", "#ABB4C2")):
        draw.text((left + 24, 20), label, fill=text)
        sheet.paste(frames[256], (left + 90, 30), frames[256])
        for size, x in ((16, 38), (20, 95), (24, 155), (32, 220), (48, 300)):
            sheet.paste(frames[size], (left + x, 328 - size // 2), frames[size])
            draw.text((left + x, 365), f"{size} px", fill=text)
    return sheet


def check_icon(path, frames):
    with Image.open(path) as icon:
        if icon.ico.sizes() != {(size, size) for size in SIZES}:
            raise SystemExit(f"Unexpected ICO sizes: {path}")
        for size, expected in frames.items():
            actual = icon.ico.getimage((size, size)).convert("RGBA")
            if actual.tobytes() != expected.tobytes():
                raise SystemExit(f"Stale ICO frame ({size} px): {path}")
            alpha = actual.getchannel("A")
            if alpha.getextrema() != (0, 255) or actual.getpixel((0, 0))[3] != 0:
                raise SystemExit(f"Missing transparency ({size} px): {path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Validate without writing files")
    args = parser.parse_args()
    frames = {size: render(size) for size in SIZES}
    sheet = preview(frames)
    icon_path = ASSETS / "TrayIcon.ico"
    preview_path = ASSETS / "BirdyIcon-preview.png"

    if not args.check:
        frames[256].save(
            icon_path,
            format="ICO",
            sizes=[(size, size) for size in SIZES],
            append_images=[frames[size] for size in SIZES if size != 256],
        )
        sheet.save(preview_path)

    check_icon(icon_path, frames)
    with Image.open(preview_path) as actual:
        if actual.size != sheet.size or actual.convert("RGB").tobytes() != sheet.tobytes():
            raise SystemExit(f"Stale preview: {preview_path}")
    print(f"OK: {len(SIZES)} transparent ICO sizes and light/dark preview match BirdyIcon.svg")


if __name__ == "__main__":
    main()

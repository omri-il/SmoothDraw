"""Draws the app icon into assets/smoothdraw.ico (run once; the .ico is committed).

SmoothDraw = a white pencil on a red tile. (SmoothZoom's ring icon is drawn by the same
kind of script in the smoothzoom repo; the two were one script until 2026-09-28.)

The .ico holds 16-256 px sizes, every size drawn at 4x and downsampled, so the 16 px
tray / Start-menu icon stays crisp. Needs Pillow:  py deploy/make_icons.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SS = 4  # supersampling factor


def tile(size, colour):
    img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = size * SS
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=s * 7 // 32, fill=colour)
    return img, d, s


def pen(size):
    """A pencil at 45°: point lower-left, body, then an eraser band upper-right."""
    img, d, s = tile(size, (229, 72, 77, 255))
    k = 0.70710678
    ux, uy = k, -k          # along the pencil, point → eraser
    nx, ny = k, k           # across it
    tx, ty = s * 7 / 32, s * 25 / 32   # the point
    h = s * 3.2 / 32        # half width

    def at(along, across):
        return (tx + ux * along + nx * across, ty + uy * along + ny * across)

    cone, body_end, eraser_end = s * 6 / 32, s * 20 / 32, s * 24 / 32
    white, pink = (255, 255, 255, 255), (255, 205, 210, 255)
    d.polygon([at(0, 0), at(cone, h), at(cone, -h)], fill=white)                       # point
    d.polygon([at(cone + s / 64, h), at(body_end, h), at(body_end, -h), at(cone + s / 64, -h)],
              fill=white)                                                                # body
    d.polygon([at(body_end + s / 64, h), at(eraser_end, h), at(eraser_end, -h),
               at(body_end + s / 64, -h)], fill=pink)                                    # eraser
    return img.resize((size, size), Image.LANCZOS)


def save(draw, name):
    out = ROOT / "assets" / name
    out.parent.mkdir(exist_ok=True)
    frames = [draw(n) for n in SIZES]
    frames[-1].save(out, format="ICO", sizes=[(n, n) for n in SIZES],
                    append_images=frames[:-1])
    print("wrote", out.relative_to(ROOT))


if __name__ == "__main__":
    save(pen, "smoothdraw.ico")

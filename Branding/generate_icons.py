"""Generates every Capkit icon and logo from one definition.

The mark is monochrome: a near-black rounded-square plate, four white capture brackets
(the region selection frame) and a white record dot. Sizes of 32 px and below use a heavier variant so the
brackets stay visible.

Usage (from the repository root, needs Pillow):  python Branding/generate_icons.py
"""

import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

PLATE_START = (52, 53, 58)    # #34353A, top left
PLATE_END = (14, 15, 17)      # #0E0F11, bottom right
RIM = (78, 80, 87)            # #4E5057, keeps the plate visible on dark backgrounds
WHITE = (255, 255, 255)
PAGE = (250, 250, 250)
PAGE_EDGE = (204, 204, 207)

SUPERSAMPLE = 4

# Geometry as fractions of the canvas: (bracket inset, arm length, stroke width, dot radius).
REGULAR = (0.255, 0.175, 0.075, 0.095)
HEAVY = (0.205, 0.215, 0.120, 0.125)


def gradient(size, start, end):
    """Diagonal gradient from the top-left to the bottom-right corner."""
    ramp = Image.linear_gradient("L").resize((size * 2, size * 2)).rotate(45, resample=Image.BICUBIC)
    offset = size // 2
    mask = ramp.crop((offset, offset, offset + size, offset + size))
    return Image.composite(Image.new("RGB", (size, size), end), Image.new("RGB", (size, size), start), mask)


def draw_brackets(draw, size, geometry, color, origin=(0, 0)):
    inset, arm, stroke, _ = (v * size for v in geometry)
    ox, oy = origin
    w = max(1, round(stroke))
    lo, hi = inset, size - inset
    corners = [
        ((lo, lo), (1, 1)),
        ((hi, lo), (-1, 1)),
        ((lo, hi), (1, -1)),
        ((hi, hi), (-1, -1)),
    ]

    for (cx, cy), (dx, dy) in corners:
        x, y = ox + cx, oy + cy
        # Each bracket is two strokes meeting at the corner, with round ends and a round joint.
        draw.line([(x, y), (x + dx * arm, y)], fill=color, width=w)
        draw.line([(x, y), (x, y + dy * arm)], fill=color, width=w)
        r = w / 2
        for px, py in ((x, y), (x + dx * arm, y), (x, y + dy * arm)):
            draw.ellipse([px - r, py - r, px + r, py + r], fill=color)


def draw_dot(draw, size, geometry, color, origin=(0, 0)):
    radius = geometry[3] * size
    c = size / 2
    ox, oy = origin
    draw.ellipse([ox + c - radius, oy + c - radius, ox + c + radius, oy + c + radius], fill=color)


def mark(size, plated=True, white=False):
    """The app mark at `size` pixels, rendered large and scaled down for clean edges."""
    geometry = HEAVY if size <= 32 else REGULAR
    big = size * SUPERSAMPLE
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))

    if plated:
        margin = round(big * (0.03 if size <= 32 else 0.06))
        radius = round(big * 0.23)
        plate = gradient(big, PLATE_START, PLATE_END).convert("RGBA")
        mask = Image.new("L", (big, big), 0)
        ImageDraw.Draw(mask).rounded_rectangle([margin, margin, big - margin, big - margin], radius=radius, fill=255)
        image.paste(plate, (0, 0), mask)
        rim = max(1, round(big * 0.008)) if size > 32 else SUPERSAMPLE
        ImageDraw.Draw(image).rounded_rectangle([margin, margin, big - margin, big - margin], radius=radius,
                                                outline=RIM, width=rim)

    draw = ImageDraw.Draw(image)
    draw_brackets(draw, big, geometry, WHITE)
    draw_dot(draw, big, geometry, WHITE)
    return image.resize((size, size), Image.LANCZOS)


def file_icon(size):
    """Document page with a folded corner and the mark on it (for Capkit file types)."""
    big = size * SUPERSAMPLE
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    left, right, top, bottom = big * 0.16, big * 0.84, big * 0.06, big * 0.94
    fold = big * 0.2
    outline = max(1, round(big * 0.018))
    draw.polygon([(left, top), (right - fold, top), (right, top + fold), (right, bottom), (left, bottom)],
                 fill=PAGE, outline=PAGE_EDGE, width=outline)
    draw.polygon([(right - fold, top), (right - fold, top + fold), (right, top + fold)], fill=PAGE_EDGE)
    inner = round(big * 0.54)
    image.alpha_composite(mark(inner // SUPERSAMPLE, plated=True).resize((inner, inner), Image.LANCZOS),
                          (round((big - inner) / 2), round(big * 0.34)))
    return image.resize((size, size), Image.LANCZOS)


def save_ico(path, render, sizes=(16, 24, 32, 48, 64, 128, 256)):
    images = [render(s) for s in sizes]
    images[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=images[:-1])
    print("wrote", os.path.relpath(path, ROOT))


def save_png(path, image):
    image.save(path, format="PNG", optimize=True)
    print("wrote", os.path.relpath(path, ROOT))


def tile(width, height, mark_fraction):
    """A Store tile: transparent, with the plated mark centred at `mark_fraction` of the shorter side."""
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    size = max(16, round(min(width, height) * mark_fraction))
    canvas.alpha_composite(mark(size), ((width - size) // 2, (height - size) // 2))
    return canvas


def main():
    app_icon = lambda s: mark(s)
    for path in ("Capkit/Capkit_Icon.ico", "Capkit.HelpersLib/Resources/Capkit_Icon.ico", "Capkit.Steam/Capkit_Icon.ico"):
        save_ico(os.path.join(ROOT, path), app_icon)

    save_ico(os.path.join(ROOT, "Capkit.HelpersLib/Resources/Capkit_Icon_White.ico"), lambda s: mark(s, plated=False, white=True))
    save_ico(os.path.join(ROOT, "Capkit/Capkit_File_Icon.ico"), file_icon)
    save_png(os.path.join(ROOT, "Capkit.HelpersLib/Resources/Capkit_Logo.png"), mark(256))

    # macOS app bundle icon and source images for documentation.
    mark(1024).save(os.path.join(ROOT, "Branding/Capkit.icns"), format="ICNS")
    print("wrote Branding/Capkit.icns")
    save_png(os.path.join(ROOT, "Branding/Capkit_Icon_1024.png"), mark(1024))

    # Microsoft Store tiles keep their existing names and pixel sizes.
    assets = os.path.join(ROOT, "Capkit.Setup/MicrosoftStore/Assets")
    for name in sorted(os.listdir(assets)):
        if not name.endswith(".png"):
            continue
        path = os.path.join(assets, name)
        width, height = Image.open(path).size

        if name.startswith("Square44x44Logo") or name.startswith("StoreLogo"):
            image = mark(width)
        elif name.startswith("Wide310x150Logo"):
            image = tile(width, height, 0.72)
        elif name.startswith("SmallTile"):
            image = tile(width, height, 0.8)
        else:  # Square150x150Logo, LargeTile
            image = tile(width, height, 0.72)

        save_png(path, image)


if __name__ == "__main__":
    main()

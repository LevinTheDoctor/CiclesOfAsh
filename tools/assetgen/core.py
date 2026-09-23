"""Gemeinsame Helfer für alle Asset-Generatoren: Palette, Zeichnen, Outline/Schattierung, Sheets."""
import random
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "src" / "CirclesOfAsh" / "Content"
TEXTURES = CONTENT / "Textures"
FONTS = CONTENT / "Fonts"
AUDIO = CONTENT / "Audio"
FONT_SOURCES = ROOT / "tools" / "fonts"
DOCS = ROOT / "docs"

rng = random.Random(1321)  # fester Seed -> reproduzierbare Ausgabe

# ---------------------------------------------------------------- Palette (RGBA)
CLEAR = (0, 0, 0, 0)
OUTLINE = (12, 8, 16, 255)
BLACK = (18, 14, 24, 255)
BONE = (222, 214, 192, 255)
WHITE = (245, 240, 230, 255)
BLOOD = (150, 24, 36, 255)
DARK_BLOOD = (90, 12, 24, 255)
EMBER = (232, 120, 40, 255)
FLAME = (255, 200, 90, 255)
GOLD = (212, 170, 72, 255)
DARK_GOLD = (140, 100, 40, 255)
MANA = (96, 110, 230, 255)
SOUL = (110, 230, 190, 255)
ASH = (120, 112, 128, 255)
STEEL = (165, 165, 180, 255)
DARK_STEEL = (85, 85, 100, 255)
PURPLE = (85, 50, 120, 255)
DEEP_PURPLE = (48, 28, 72, 255)
SHADOW = (34, 26, 46, 255)
PALE = (165, 185, 165, 255)
VIOLET = (170, 100, 230, 255)
WOOD = (95, 65, 45, 255)
DARK_WOOD = (60, 40, 28, 255)
LEATHER = (110, 70, 45, 255)
STONE = (120, 116, 130, 255)
DARK_STONE = (72, 68, 82, 255)
# Graustufen für einfärbbare Ebenen (Haut, Haare, Akzent): Der Code multipliziert mit der Wunschfarbe
TINT_LIGHT = (240, 240, 240, 255)
TINT_MID = (195, 195, 195, 255)
TINT_DARK = (140, 140, 140, 255)
TINT_EYE = (40, 40, 48, 255)


def new_image(width, height):
    return Image.new("RGBA", (width, height), CLEAR)


def rect(draw, x, y, width, height, color):
    """Rechteck mit Breite/Höhe statt Eckpunkten (lesbarer für Pixel-Art)."""
    if width > 0 and height > 0:
        draw.rectangle([x, y, x + width - 1, y + height - 1], fill=color)


def pixel(draw, x, y, color):
    draw.point((x, y), fill=color)


def shift(color, amount):
    """Hellt (amount > 0) oder dunkelt (amount < 0) eine Farbe ab, Alpha bleibt."""
    return tuple(max(0, min(255, c + amount)) for c in color[:3]) + (color[3],)


def polish(image, outline=OUTLINE, light=26, dark=-30, gradient=18):
    """
    Macht flache Formen zu "echter" Pixel-Art:
      1. Lichtkante oben/links (Licht kommt von oben links)
      2. Schattenkante unten
      3. sanfter vertikaler Verlauf (unten dunkler)
      4. 1-Pixel-Outline um die Silhouette
    Wird PRO FRAME angewendet, damit nichts in Nachbarframes blutet.
    """
    width, height = image.size
    source = image.load()
    result = image.copy()
    target = result.load()

    def alpha(x, y):
        return source[x, y][3] if 0 <= x < width and 0 <= y < height else 0

    for y in range(height):
        for x in range(width):
            color = source[x, y]
            if color[3] == 0:
                continue
            shade = int(-gradient * (y / max(1, height - 1)))
            if alpha(x, y - 1) == 0 or alpha(x - 1, y) == 0:
                shade += light
            elif alpha(x, y + 1) == 0 or alpha(x + 1, y) == 0:
                shade += dark
            target[x, y] = shift(color, shade)

    if outline is not None:
        for y in range(height):
            for x in range(width):
                if source[x, y][3] != 0:
                    continue
                if any(alpha(x + dx, y + dy) > 0 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    target[x, y] = outline
    return result


def tint_frame(frame, tint, strength):
    data = frame.load()
    for y in range(frame.height):
        for x in range(frame.width):
            r, g, b, a = data[x, y]
            if a:
                data[x, y] = (int(r + (tint[0] - r) * strength), int(g + (tint[1] - g) * strength),
                              int(b + (tint[2] - b) * strength), a)
    return frame


def build_sheet(frame_width, frame_height, rows):
    """rows = Liste von Frame-Listen. Jede Zeile = eine Animation (siehe manifest.json)."""
    columns = max(len(row) for row in rows)
    sheet = new_image(frame_width * columns, frame_height * len(rows))
    for row_index, frames in enumerate(rows):
        for column_index, frame in enumerate(frames):
            sheet.paste(frame, (column_index * frame_width, row_index * frame_height), frame)
    return sheet


def dither_rect(draw, x, y, width, height, color_a, color_b):
    """Schachbrett-Dithering zwischen zwei Farben (klassische Pixel-Art-Textur)."""
    for yy in range(y, y + height):
        for xx in range(x, x + width):
            pixel(draw, xx, yy, color_a if (xx + yy) % 2 == 0 else color_b)

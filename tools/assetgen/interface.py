"""Oberflächen-Grafiken: Sprachflaggen für das Optionsmenü.

Flaggen sind Vektormotive (Diagonalen, Kreuze), die sich auf 24 x 16 Pixel nicht sauber von Hand
setzen lassen. Deshalb: groß zeichnen, klein rechnen und danach jedes Pixel auf die nächste echte
Flaggenfarbe setzen. So entstehen scharfe Pixel ohne Zwischentöne – derselbe Look wie der Rest.
"""
from PIL import Image, ImageDraw

# Kantenlängen: groß für die Sprachauswahl, klein für das Symbol in der Reiterleiste.
FLAG_SIZE = (24, 16)
FLAG_SMALL_SIZE = (12, 8)
SUPERSAMPLE = 16

# Offizielle Farben, leicht aufgehellt, wo das Original auf dunklem Grund verschwände.
DE_BLACK = (24, 22, 28, 255)
DE_RED = (221, 0, 0, 255)
DE_GOLD = (255, 206, 0, 255)
UK_BLUE = (1, 33, 105, 255)
UK_WHITE = (240, 240, 240, 255)
UK_RED = (200, 16, 46, 255)


def _nearest(palette, color):
    """Die Palettenfarbe mit dem kleinsten Abstand (Summe der Quadrate je Kanal)."""
    return min(palette, key=lambda candidate: sum((a - b) ** 2 for a, b in zip(candidate[:3], color[:3])))


def _render(draw_motif, palette, size):
    """Zeichnet das Motiv SUPERSAMPLE-fach groß und rechnet es scharf auf `size` herunter."""
    width, height = size[0] * SUPERSAMPLE, size[1] * SUPERSAMPLE
    large = Image.new("RGBA", (width, height), palette[0])
    draw_motif(ImageDraw.Draw(large), width, height)
    small = large.resize(size, Image.BOX)   # BOX = Mittelwert je Zielpixel
    pixels = small.load()
    for y in range(size[1]):
        for x in range(size[0]):
            pixels[x, y] = _nearest(palette, pixels[x, y])
    return small


def _germany(draw, width, height):
    """Schwarz-Rot-Gold, drei gleich hohe Streifen."""
    third = height / 3
    draw.rectangle([0, 0, width, third], fill=DE_BLACK)
    draw.rectangle([0, third, width, 2 * third], fill=DE_RED)
    draw.rectangle([0, 2 * third, width, height], fill=DE_GOLD)


def _band(draw, start, end, offset, half_width, color):
    """Gerades Band von start nach end, seitlich um `offset` verschoben (Normale gegen den Uhrzeigersinn)."""
    direction = (end[0] - start[0], end[1] - start[1])
    length = (direction[0] ** 2 + direction[1] ** 2) ** 0.5
    unit = (direction[0] / length, direction[1] / length)
    normal = (unit[1], -unit[0])
    # Über die Enden hinaus verlängern, damit an den Ecken keine Lücke bleibt.
    extended_start = (start[0] - unit[0] * half_width * 2, start[1] - unit[1] * half_width * 2)
    extended_end = (end[0] + unit[0] * half_width * 2, end[1] + unit[1] * half_width * 2)
    corners = []
    for point in (extended_start, extended_end):
        for side in (1, -1):
            shift = offset + side * half_width
            corners.append((point[0] + normal[0] * shift, point[1] + normal[1] * shift))
    draw.polygon([corners[0], corners[2], corners[3], corners[1]], fill=color)


def _united_kingdom(draw, width, height):
    """
    Union Jack für "English". Maße nach der amtlichen Vorlage (bezogen auf die Flaggenhöhe):
    weißes Andreaskreuz 1/5, rotes 1/15 und gegenläufig versetzt ("Windrad"), weißes Kreuz 1/3,
    rotes Kreuz 1/5.
    """
    center = (width / 2, height / 2)
    corners = [(0, 0), (width, 0), (width, height), (0, height)]
    for corner in corners:                                   # weißes Andreaskreuz
        _band(draw, center, corner, 0, height / 10, UK_WHITE)
    for corner in corners:                                   # rotes Andreaskreuz, je Viertel versetzt
        _band(draw, center, corner, height / 30, height / 30, UK_RED)
    draw.rectangle([center[0] - height / 6, 0, center[0] + height / 6, height], fill=UK_WHITE)
    draw.rectangle([0, center[1] - height / 6, width, center[1] + height / 6], fill=UK_WHITE)
    draw.rectangle([center[0] - height / 10, 0, center[0] + height / 10, height], fill=UK_RED)
    draw.rectangle([0, center[1] - height / 10, width, center[1] + height / 10], fill=UK_RED)


# Sprach-Id -> (Motiv, Palette). Die erste Palettenfarbe ist der Grund. Neue Sprache = neuer Eintrag
# hier + Content/Lang/<id>.json + zwei Zeilen in manifest.json.
FLAGS = {
    "de": (_germany, [DE_BLACK, DE_RED, DE_GOLD]),
    "en": (_united_kingdom, [UK_BLUE, UK_WHITE, UK_RED]),
}


def flags(textures):
    """flag_<id>.png (24 x 16) und flag_<id>_small.png (12 x 8) je Sprache."""
    for language, (motif, palette) in FLAGS.items():
        _render(motif, palette, FLAG_SIZE).save(textures / f"flag_{language}.png")
        _render(motif, palette, FLAG_SMALL_SIZE).save(textures / f"flag_{language}_small.png")


def generate(textures):
    flags(textures)

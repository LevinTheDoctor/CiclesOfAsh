"""Oberflächen-Grafiken: Sprachflaggen für das Optionsmenü und Controller-Tasten.

Flaggen sind Vektormotive (Diagonalen, Kreuze), die sich auf 24 x 16 Pixel nicht sauber von Hand
setzen lassen. Deshalb: groß zeichnen, klein rechnen und danach jedes Pixel auf die nächste echte
Flaggenfarbe setzen. So entstehen scharfe Pixel ohne Zwischentöne – derselbe Look wie der Rest.

Controller-Tasten (Glyphen) sind dagegen echte Pixel-Art, Pixel für Pixel gesetzt: je Familie
(Xbox, PlayStation, Switch, Tastatur) ein Blatt mit einer Zeile, jedes Einzelbild 16 x 12 Pixel.
Die Einzelbilder heißen im Manifest GENAU wie die Beschriftungen in Content/Data/controllers.json
("A", "RB", "○", "Menü" …) – so findet jede vorhandene Belegung ihr Bild ohne Zuordnungstabelle.
"""
from PIL import Image, ImageDraw, ImageFont

from .core import FONT_SOURCES

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


# ================================================================== Controller-Tasten
GLYPH_SIZE = (16, 12)

# Gehäusefarben: dunkler Knopf mit Licht oben links und Schatten unten rechts, wie die Props.
BODY = (46, 42, 54, 255)
BODY_LIGHT = (84, 78, 96, 255)
BODY_SHADOW = (30, 26, 36, 255)
OUTLINE = (10, 8, 14, 255)
LABEL = (226, 220, 206, 255)

XBOX = {"A": (104, 196, 92, 255), "B": (226, 76, 72, 255), "X": (74, 138, 232, 255), "Y": (242, 198, 64, 255)}
PS_CROSS = (120, 168, 236, 255)
PS_CIRCLE = (232, 96, 96, 255)
PS_SQUARE = (224, 138, 206, 255)
PS_TRIANGLE = (72, 204, 172, 255)
SWITCH_LETTER = (238, 238, 242, 255)


def _label_font():
    return ImageFont.truetype(str(FONT_SOURCES / "Tiny5-Regular.ttf"), 8)


def _text(draw, text, center_x, center_y, color):
    """Zentriert einen kurzen Text pixelgenau (Tiny5, ohne Kantenglättung wie die Spielschrift)."""
    font = _label_font()
    draw.fontmode = "1"
    left, top, right, bottom = font.getbbox(text)
    x = round(center_x - (right - left) / 2) - left
    y = round(center_y - (bottom - top) / 2) - top
    draw.text((x, y), text, font=font, fill=color)


def _shade(image, left, top, right, bottom):
    """Licht oben links, Schatten unten rechts auf allen Gehäusepixeln (nicht auf Beschriftungen)."""
    pixels = image.load()
    for y in range(top, bottom + 1):
        for x in range(left, right + 1):
            if pixels[x, y] != BODY:
                continue
            above = pixels[x, y - 1] if y > 0 else OUTLINE
            leftside = pixels[x - 1, y] if x > 0 else OUTLINE
            below = pixels[x, y + 1] if y < image.height - 1 else OUTLINE
            rightside = pixels[x + 1, y] if x < image.width - 1 else OUTLINE
            if above == OUTLINE or leftside == OUTLINE:
                pixels[x, y] = BODY_LIGHT
            elif below == OUTLINE or rightside == OUTLINE:
                pixels[x, y] = BODY_SHADOW


def _new_glyph():
    return Image.new("RGBA", GLYPH_SIZE, (0, 0, 0, 0))


def _round_button(draw_symbol):
    """Runder Frontknopf, 11 x 11, mittig. draw_symbol(draw, cx, cy) setzt Buchstabe oder Symbol."""
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.ellipse([2, 0, 12, 10], fill=OUTLINE)
    draw.ellipse([3, 1, 11, 9], fill=BODY)
    _shade(image, 3, 1, 11, 9)
    draw_symbol(draw, 7, 5)
    return image


def _letter(text, color):
    return _round_button(lambda draw, cx, cy: _text(draw, text, cx + 0.5, cy + 0.5, color))


def _ps_cross(draw, cx, cy):
    for offset in range(-2, 3):
        draw.point((cx + offset, cy + offset), fill=PS_CROSS)
        draw.point((cx + offset, cy - offset), fill=PS_CROSS)


def _ps_circle(draw, cx, cy):
    draw.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], outline=PS_CIRCLE)


def _ps_square(draw, cx, cy):
    draw.rectangle([cx - 2, cy - 2, cx + 2, cy + 2], outline=PS_SQUARE)


def _ps_triangle(draw, cx, cy):
    draw.line([(cx, cy - 2), (cx - 3, cy + 2)], fill=PS_TRIANGLE)
    draw.line([(cx, cy - 2), (cx + 3, cy + 2)], fill=PS_TRIANGLE)
    draw.line([(cx - 3, cy + 2), (cx + 3, cy + 2)], fill=PS_TRIANGLE)


def _bumper(text):
    """Schultertaste: flache, breite Taste, oben stärker gerundet."""
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([0, 2, 15, 10], radius=3, fill=OUTLINE)
    draw.rounded_rectangle([1, 3, 14, 9], radius=2, fill=BODY)
    _shade(image, 1, 3, 14, 9)
    _text(draw, text, 8, 6.5, LABEL)
    return image


def _trigger(text):
    """Trigger: hohe Taste mit gewölbter Oberkante, Beschriftung unten."""
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([1, 0, 14, 11], radius=4, fill=OUTLINE)
    draw.rectangle([1, 6, 14, 11], fill=OUTLINE)
    draw.rounded_rectangle([2, 1, 13, 10], radius=3, fill=BODY)
    draw.rectangle([2, 6, 13, 10], fill=BODY)
    _shade(image, 2, 1, 13, 10)
    _text(draw, text, 8, 6.5, LABEL)
    return image


def _pill(draw_icon):
    """Kleine ovale Systemtaste (Menü, Ansicht, Options, +, −) mit Symbol statt Text."""
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([2, 2, 13, 9], radius=3, fill=OUTLINE)
    draw.rounded_rectangle([3, 3, 12, 8], radius=2, fill=BODY)
    _shade(image, 3, 3, 12, 8)
    draw_icon(draw, 7, 5)
    return image


def _icon_menu(draw, cx, cy):
    for row in (cy - 1, cy + 1, cy + 3):
        draw.line([(cx - 2, row - 1), (cx + 3, row - 1)], fill=LABEL)


def _icon_view(draw, cx, cy):
    draw.rectangle([cx - 2, cy - 2, cx + 1, cy + 1], outline=LABEL)
    draw.rectangle([cx, cy, cx + 3, cy + 3], outline=LABEL)


def _icon_create(draw, cx, cy):
    """Create/Share: Pfeil schräg nach oben rechts – "etwas hinausschicken"."""
    draw.line([(cx - 2, cy + 2), (cx + 2, cy - 2)], fill=LABEL)
    draw.line([(cx, cy - 2), (cx + 2, cy - 2)], fill=LABEL)
    draw.line([(cx + 2, cy - 2), (cx + 2, cy)], fill=LABEL)


def _icon_plus(draw, cx, cy):
    draw.line([(cx - 2, cy + 0.5), (cx + 2, cy + 0.5)], fill=LABEL)
    draw.line([(cx, cy - 2), (cx, cy + 2)], fill=LABEL)


def _icon_minus(draw, cx, cy):
    draw.line([(cx - 2, cy), (cx + 2, cy)], fill=LABEL)


def _icon_start(draw, cx, cy):
    """Start: kleines Dreieck nach rechts."""
    for column in range(3):
        draw.line([(cx - 1 + column, cy - 2 + column), (cx - 1 + column, cy + 2 - column)], fill=LABEL)


def _icon_select(draw, cx, cy):
    """Select: zwei kurze Balken."""
    draw.line([(cx - 2, cy - 1), (cx + 2, cy - 1)], fill=LABEL)
    draw.line([(cx - 2, cy + 1), (cx + 2, cy + 1)], fill=LABEL)


def _stick(text):
    """Analogstick (als Taste gedrückt): runde Kappe mit Mulde, darin die Beschriftung."""
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.ellipse([2, 0, 13, 11], fill=OUTLINE)
    draw.ellipse([3, 1, 12, 10], fill=BODY)
    _shade(image, 3, 1, 12, 10)
    draw.ellipse([4, 2, 11, 9], outline=BODY_SHADOW)
    _text(draw, text, 8, 6, LABEL)
    return image


def _stick_move():
    """Stick zum Laufen: kleinere Kappe, außen herum je ein Pfeil in alle vier Richtungen."""
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.ellipse([4, 2, 11, 9], fill=OUTLINE)
    draw.ellipse([5, 3, 10, 8], fill=BODY)
    _shade(image, 5, 3, 10, 8)
    draw.rectangle([7, 5, 8, 6], fill=BODY_SHADOW)
    # Pfeilspitzen: je drei Pixel als Keil, außerhalb der Kappe
    draw.line([(7, 0), (8, 0)], fill=LABEL)
    draw.line([(7, 11), (8, 11)], fill=LABEL)
    draw.line([(2, 5), (2, 6)], fill=LABEL)
    draw.line([(13, 5), (13, 6)], fill=LABEL)
    for x, y in ((6, 1), (9, 1), (6, 10), (9, 10), (3, 4), (3, 7), (12, 4), (12, 7)):
        draw.point((x, y), fill=LABEL)
    return image


def _dpad(highlight):
    """Steuerkreuz; highlight = "up"/"down"/"left"/"right" oder None (alle Richtungen)."""
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.rectangle([6, 0, 9, 11], fill=OUTLINE)
    draw.rectangle([2, 4, 13, 7], fill=OUTLINE)
    draw.rectangle([7, 1, 8, 10], fill=BODY)
    draw.rectangle([3, 5, 12, 6], fill=BODY)
    _shade(image, 3, 1, 12, 10)
    arms = {"up": [7, 1, 8, 3], "down": [7, 8, 8, 10], "left": [3, 5, 5, 6], "right": [10, 5, 12, 6]}
    for direction, box in arms.items():
        if highlight is None or highlight == direction:
            draw.rectangle(box, fill=LABEL)
    return image


def _keycap():
    """
    Leere Tastenkappe. Die Beschriftung setzt das Spiel zur Laufzeit darauf, und für lange Namen
    ("Enter", "Umschalt") zieht es die Kappe in die Breite: Die Spalten 4 bis 11 sind alle gleich,
    also kann die Mitte gestreckt werden, ohne dass Ecken oder Kanten verzerren (3-Slice).
    """
    image = _new_glyph()
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([0, 0, 15, 11], radius=2, fill=OUTLINE)
    draw.rounded_rectangle([1, 0, 14, 9], radius=2, fill=BODY)
    _shade(image, 1, 0, 14, 9)
    return image


def _movement_and_system(start_icon=_icon_start, select_icon=_icon_select):
    """
    Einzelbilder, die jede Controller-Familie gleich hat: Steuerkreuz, Laufen, Start/Select – und
    "round", ein leerer Frontknopf. Auf ihn schreibt das Spiel Beschriftungen, für die es kein
    eigenes Bild gibt (etwa die Nummern der Logitech-Pads im DirectInput-Modus).
    """
    return [
        ("round", _round_button(lambda draw, cx, cy: None)),
        ("dpad", _dpad(None)),
        ("dpad_up", _dpad("up")),
        ("dpad_down", _dpad("down")),
        ("dpad_left", _dpad("left")),
        ("dpad_right", _dpad("right")),
        ("stick", _stick_move()),
        ("Start", _pill(start_icon)),
        ("Select", _pill(select_icon)),
    ]


def glyph_families():
    """Familie -> Liste (Clip-Name, Einzelbild). Die Clip-Namen sind die Beschriftungen der Profile."""
    xbox = [(letter, _letter(letter, color)) for letter, color in XBOX.items()]
    xbox += [("LB", _bumper("LB")), ("RB", _bumper("RB")), ("LT", _trigger("LT")), ("RT", _trigger("RT")),
             ("Menü", _pill(_icon_menu)), ("Ansicht", _pill(_icon_view)), ("LS", _stick("LS")), ("RS", _stick("RS")),
             # Steam Deck und 8BitDo benutzen Xbox-Buchstaben mit L1/R1-Schultern
             ("L1", _bumper("L1")), ("R1", _bumper("R1")), ("L2", _trigger("L2")), ("R2", _trigger("R2"))]
    xbox += _movement_and_system()

    playstation = [("X", _round_button(_ps_cross)), ("○", _round_button(_ps_circle)),
                   ("□", _round_button(_ps_square)), ("△", _round_button(_ps_triangle)),
                   ("L1", _bumper("L1")), ("R1", _bumper("R1")), ("L2", _trigger("L2")), ("R2", _trigger("R2")),
                   ("Options", _pill(_icon_menu)), ("Create", _pill(_icon_create)), ("L3", _stick("L3")), ("R3", _stick("R3"))]
    playstation += _movement_and_system()

    switch = [(letter, _letter(letter, SWITCH_LETTER)) for letter in ("A", "B", "X", "Y")]
    switch += [("L", _bumper("L")), ("R", _bumper("R")), ("ZL", _trigger("ZL")), ("ZR", _trigger("ZR")),
               ("+", _pill(_icon_plus)), ("-", _pill(_icon_minus)), ("LS", _stick("LS")), ("RS", _stick("RS"))]
    switch += _movement_and_system(_icon_plus, _icon_minus)

    keyboard = [("key", _keycap())]
    return {"xbox": xbox, "playstation": playstation, "switch": switch, "keyboard": keyboard}


def glyphs(textures):
    """glyphs_<familie>.png: eine Zeile, 16 x 12 je Taste. Reihenfolge = Spalte im Manifest."""
    for family, frames in glyph_families().items():
        sheet = Image.new("RGBA", (GLYPH_SIZE[0] * len(frames), GLYPH_SIZE[1]), (0, 0, 0, 0))
        for column, (_, frame) in enumerate(frames):
            sheet.paste(frame, (column * GLYPH_SIZE[0], 0), frame)
        sheet.save(textures / f"glyphs_{family}.png")


def generate(textures):
    flags(textures)
    glyphs(textures)

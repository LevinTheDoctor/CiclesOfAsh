"""
Spielerfigur als EBENEN (16x24 pro Frame), damit der Charakter-Editor sie frei kombinieren kann:
  body   - Haut (Graustufen -> im Spiel mit Hautton eingefärbt)
  hair_* - Frisuren (Graustufen -> Haarfarbe)
  outfit_<klasse> - feste Farben der Klasse
  accent_<klasse> - Wappenrock/Stola/Schal (Graustufen -> Akzentfarbe)
Zeichenreihenfolge im Spiel: body, hair, outfit, accent.
Zeilen: 0 idle, 1 run, 2 jump, 3 hurt (je bis zu 4 Frames).
"""
from PIL import ImageDraw

from .core import (BLACK, DARK_STEEL, DEEP_PURPLE, GOLD, LEATHER, MANA, OUTLINE, PURPLE, SHADOW, SOUL, STEEL, TINT_DARK,
                   TINT_EYE, TINT_LIGHT, TINT_MID, WOOD, build_sheet, new_image, pixel, polish, rect)

ANIMATIONS = (("idle", 4), ("run", 4), ("jump", 4), ("hurt", 2))
TINT_OUTLINE = (60, 60, 66, 255)


def pose(anim, frame):
    """Gemeinsame Pose aller Ebenen -> alle Layer bleiben synchron."""
    run = anim == "run"
    return {
        "bob": 1 if anim in ("idle", "hurt") and frame in (1, 2) else 0,
        "legs": [(-1, 1, 0, 1), (0, 0, 1, 0), (1, -1, 1, 0), (0, 0, 0, 1)][frame] if run else (0, 0, 0, 0),
        "arm": [1, 0, -1, 0][frame] if run else 0,
        "jump": anim == "jump",
        "run": run,
        "frame": frame,
    }


def draw_legs(draw, p, color, boot):
    if p["jump"]:
        rect(draw, 5, 18, 2, 4, color)
        rect(draw, 9, 17, 2, 4, color)
        rect(draw, 5, 21, 2, 1, boot)
        rect(draw, 9, 20, 2, 1, boot)
        return
    left_shift, right_shift, left_lift, right_lift = p["legs"]
    rect(draw, 5 + left_shift, 18, 2, 6 - left_lift, color)
    rect(draw, 9 + right_shift, 18, 2, 6 - right_lift, color)
    rect(draw, 5 + left_shift, 23 - left_lift, 2, 1, boot)
    rect(draw, 9 + right_shift, 23 - right_lift, 2, 1, boot)


# ------------------------------------------------------------------ Körper
def body_frame(p):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    draw_legs(draw, p, TINT_MID, TINT_DARK)
    rect(draw, 5, 9 + b, 6, 9, TINT_MID)               # Rumpf (vom Outfit verdeckt)
    rect(draw, 4, 10 + b + p["arm"], 1, 5, TINT_MID)    # hinterer Arm
    rect(draw, 11, 10 + b - p["arm"], 1, 5, TINT_LIGHT)  # vorderer Arm
    pixel(draw, 11, 15 + b - p["arm"], TINT_LIGHT)      # Hand
    rect(draw, 7, 8 + b, 2, 1, TINT_MID)                # Hals
    rect(draw, 5, 3 + b, 6, 6, TINT_LIGHT)              # Kopf
    rect(draw, 5, 7 + b, 1, 2, TINT_MID)                # Wangen-/Kieferschatten
    pixel(draw, 10, 7 + b, TINT_MID)
    pixel(draw, 8, 5 + b, TINT_MID)                     # Brauenlinie
    pixel(draw, 10, 5 + b, TINT_MID)
    pixel(draw, 8, 6 + b, TINT_EYE)                     # Augen (Blick nach rechts)
    pixel(draw, 10, 6 + b, TINT_EYE)
    pixel(draw, 9, 8 + b, TINT_DARK)                    # Mund
    return polish(image, outline=TINT_OUTLINE, light=12, dark=-22, gradient=10)


# ------------------------------------------------------------------ Haare
def hair_frame(style, p):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    light, mid, dark = TINT_LIGHT, TINT_MID, TINT_DARK
    if style == "short":
        rect(draw, 5, 2 + b, 6, 2, mid)
        rect(draw, 4, 3 + b, 2, 3, mid)
        rect(draw, 9, 3 + b, 2, 1, light)
        pixel(draw, 6, 2 + b, light)
    elif style == "long":
        rect(draw, 5, 2 + b, 6, 2, mid)
        rect(draw, 3, 3 + b, 3, 9, mid)                # langes Haar fällt über den Rücken
        rect(draw, 3, 11 + b, 2, 2, dark)
        rect(draw, 9, 3 + b, 2, 1, light)
        pixel(draw, 7, 2 + b, light)
    elif style == "braid":
        rect(draw, 5, 2 + b, 6, 2, mid)
        rect(draw, 4, 3 + b, 2, 3, mid)
        for index, y in enumerate(range(6, 15)):       # geflochtener Zopf: abwechselnd hell/dunkel
            pixel(draw, 3 + (index % 2), y + b, light if index % 2 else dark)
        pixel(draw, 3, 15 + b, GOLD)                   # Zopfspange (feste Farbe)
    elif style == "mohawk":
        rect(draw, 7, 0 + b, 2, 3, mid)
        pixel(draw, 7, 0 + b, light)
        rect(draw, 5, 3 + b, 1, 2, dark)               # rasierte Seiten
    elif style == "hooded_curls":
        for x, y in ((5, 2), (7, 1), (9, 2), (4, 4), (10, 3), (6, 2), (8, 2)):
            rect(draw, x, y + b, 2, 2, mid)
            pixel(draw, x, y + b, light)
    return polish(image, outline=TINT_OUTLINE, light=10, dark=-20, gradient=0)


# ------------------------------------------------------------------ Outfits (feste Farben)
def outfit_frame(cls, p):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b, arm, frame = p["bob"], p["arm"], p["frame"]
    if cls == "warrior":
        draw_legs(draw, p, DARK_STEEL, BLACK)
        rect(draw, 5, 9 + b, 6, 7, STEEL)                  # Brustpanzer
        rect(draw, 6, 10 + b, 1, 4, (200, 200, 215, 255))  # Glanzlicht
        rect(draw, 5, 15 + b, 6, 1, LEATHER)               # Gürtel
        pixel(draw, 8, 15 + b, GOLD)                       # Schnalle
        rect(draw, 4, 9 + b, 2, 2, DARK_STEEL)             # Schulterplatten
        rect(draw, 10, 9 + b, 2, 2, DARK_STEEL)
        rect(draw, 11, 13 + b - arm, 1, 2, DARK_STEEL)     # Panzerhandschuh
        rect(draw, 12, 5 + b, 1, 10, STEEL)                # Klinge
        pixel(draw, 12, 5 + b, (230, 230, 240, 255))
        rect(draw, 11, 15 + b, 3, 1, GOLD)                 # Parierstange
        pixel(draw, 12, 16 + b, LEATHER)
    elif cls == "mage":
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        draw_legs(draw, p, DEEP_PURPLE, BLACK)
        rect(draw, 4, 9 + b, 8, 12, DEEP_PURPLE)           # Robe
        rect(draw, 4 + sway, 20 + b, 8, 2, DEEP_PURPLE)
        rect(draw, 4, 8 + b, 8, 2, PURPLE)                 # Kapuze (zurückgeschlagen)
        rect(draw, 10, 11 + b - arm, 2, 4, PURPLE)         # Ärmel
        rect(draw, 5, 16 + b, 6, 1, LEATHER)               # Kordel
        rect(draw, 13, 4 + b, 1, 19, WOOD)                 # Stab
        rect(draw, 12, 1 + b, 3, 3, SOUL if frame % 2 == 0 else MANA)
        pixel(draw, 13, 0 + b, (220, 255, 240, 255))
    else:  # shadow
        draw_legs(draw, p, SHADOW, BLACK)
        rect(draw, 5, 9 + b, 6, 10, SHADOW)                # Umhang
        rect(draw, 7, 11 + b, 1, 7, BLACK)                 # Faltenwurf
        rect(draw, 4, 12 + b, 1, 6, BLACK)
        rect(draw, 4, 2 + b, 8, 3, BLACK)                  # Kapuze
        rect(draw, 4, 2 + b, 2, 8, BLACK)
        rect(draw, 11, 3 + b, 1, 5, BLACK)
        rect(draw, 7, 7 + b, 4, 2, (40, 32, 52, 255))      # Maske
        rect(draw, 12, 13 + b - arm, 1, 4, STEEL)          # Dolch
        rect(draw, 11, 16 + b - arm, 3, 1, DARK_STEEL)
    return polish(image)


def accent_frame(cls, p):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b, frame = p["bob"], p["frame"]
    if cls == "warrior":
        rect(draw, 2, 10 + b, 2, 10, TINT_DARK)            # Umhang hinten
        if p["run"]:
            rect(draw, 1, 12 + b + frame % 2, 1, 6, TINT_DARK)
        rect(draw, 6, 11 + b, 4, 7, TINT_MID)              # Wappenrock
        rect(draw, 7, 12 + b, 2, 3, TINT_LIGHT)            # Wappen
    elif cls == "mage":
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        rect(draw, 7, 10 + b, 2, 11, TINT_MID)             # Stola
        rect(draw, 4 + sway, 21 + b, 8, 1, TINT_LIGHT)     # Saum
        pixel(draw, 7, 12 + b, TINT_LIGHT)
    else:
        rect(draw, 5, 9 + b, 6, 2, TINT_MID)               # Schal
        if p["run"]:
            rect(draw, 1 + frame % 2, 10 + b, 4, 1, TINT_DARK)
        else:
            rect(draw, 4, 10 + b, 1, 3, TINT_DARK)
    return polish(image, outline=(50, 50, 56, 255), light=10, dark=-20, gradient=0)


def layer_sheet(frame_function):
    rows = [[frame_function(pose(anim, i)) for i in range(count)] for anim, count in ANIMATIONS]
    return build_sheet(16, 24, rows)


def generate(textures):
    layer_sheet(body_frame).save(textures / "char_body.png")
    for style in ("short", "long", "braid", "mohawk", "hooded_curls"):
        layer_sheet(lambda p, s=style: hair_frame(s, p)).save(textures / f"char_hair_{style}.png")
    for cls in ("warrior", "mage", "shadow"):
        layer_sheet(lambda p, c=cls: outfit_frame(c, p)).save(textures / f"char_outfit_{cls}.png")
        layer_sheet(lambda p, c=cls: accent_frame(c, p)).save(textures / f"char_accent_{cls}.png")


__all__ = ["generate", "OUTLINE"]

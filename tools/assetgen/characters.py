"""
Spielerfigur als EBENEN (16x24 pro Frame), damit der Charakter-Editor sie frei kombinieren kann:
  body   - Haut (Graustufen -> im Spiel mit Hautton eingefärbt)
  hair_* - Frisuren (Graustufen -> Haarfarbe)
  outfit_<klasse> - feste Farben der Klasse
  accent_<klasse> - Wappenrock/Stola/Schal (Graustufen -> Akzentfarbe)
Zeichenreihenfolge im Spiel: body, hair, outfit, accent.
Zeilen: 0 idle, 1 run, 2 jump, 3 hurt (je bis zu 4 Frames).
"""
from PIL import Image, ImageDraw

from .core import (ASH, BLACK, DARK_GOLD, DARK_STEEL, DARK_STONE, DARK_WOOD, DEEP_PURPLE, EMBER, FLAME, GOLD,
                   LEATHER, MANA, OUTLINE, PURPLE, SHADOW, SOUL, STEEL,
                   TINT_DARK, TINT_EYE, TINT_LIGHT, TINT_MID, WOOD, build_sheet, new_image, pixel, polish, rect, shift)

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
    return variant_body_frame(p, "m_average")


def variant_body_frame(p, variant):
    """Körpervarianten: Silhouette UND Binnenzeichnung variieren (Taille, Muskeln, Rundung),
    Kopf/Hals/Fußlinie bleiben identisch, damit Haare und Kleidung weiterhin passen."""
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    heavy = variant.endswith("heavy")
    athletic = variant.endswith("athletic")
    female = variant.startswith("f")
    if heavy:
        # breiter Rumpf, weiche Rundung: Schatten nur am Rand, Bauch leicht vorgewölbt
        draw_legs(draw, p, TINT_MID, TINT_DARK)
        rect(draw, 4, 9 + b, 8, 9, TINT_MID)
        rect(draw, 3, 9 + b, 10, 3, TINT_LIGHT)     # breite, weiche Schultern/Kasten oben
        rect(draw, 4, 16 + b, 8, 2, TINT_DARK)      # vorgewölbte Bauch-Unterkante
        pixel(draw, 5, 12 + b, TINT_DARK)           # Rand-Deutung statt Muskeln
        pixel(draw, 10, 12 + b, TINT_DARK)
        pixel(draw, 5, 14 + b, TINT_DARK)
        pixel(draw, 10, 14 + b, TINT_DARK)
        if female:
            rect(draw, 4, 10 + b, 8, 2, TINT_LIGHT)
            pixel(draw, 6, 13 + b, TINT_DARK)       # sanfter Busen-Schatten
            pixel(draw, 9, 13 + b, TINT_DARK)
            rect(draw, 5, 14 + b, 1, 2, TINT_DARK)  # angedeutete Taille unter dem Busen
            rect(draw, 10, 14 + b, 1, 2, TINT_DARK)
        else:
            rect(draw, 3, 10 + b, 1, 4, TINT_DARK)   # seitliche Wamst-Rundung
            rect(draw, 12, 10 + b, 1, 4, TINT_DARK)
    elif athletic:
        # schmale Taille, breite Schultern — plus Bauchmuskeln als waagerechte Schattenlinien
        draw_legs(draw, p, TINT_MID, TINT_DARK)
        if female:
            # f_athletic: breitere Schultern (x 4-11) UND schmalerer Rumpf als f_average —
            # die Silhouette MUSS sich von f_average unterscheiden (Auftrag G9)
            rect(draw, 4, 9 + b, 8, 2, TINT_LIGHT)    # breite trainierte Schultern
            rect(draw, 6, 11 + b, 4, 3, TINT_MID)     # Rumpf auf 4 px Taille verjüngt
            rect(draw, 5, 14 + b, 6, 4, TINT_MID)     # Hüfte ausgestellt, bis y 17 durchgehend
            pixel(draw, 5, 12 + b, TINT_DARK)        # eingezogene Taille, beidseitig
            pixel(draw, 5, 13 + b, TINT_DARK)
            pixel(draw, 10, 12 + b, TINT_DARK)
            pixel(draw, 10, 13 + b, TINT_DARK)
            for y in (11, 13, 15):                    # Bauchmuskeln: waagerechte Linien
                pixel(draw, 7, y + b, TINT_DARK)
                pixel(draw, 8, y + b, TINT_DARK)
        else:
            rect(draw, 4, 9 + b, 8, 9, TINT_MID)
            rect(draw, 4, 9 + b, 8, 2, TINT_LIGHT)    # breite Schultern
            rect(draw, 6, 11 + b, 4, 4, TINT_MID)     # trainierte Taille (schmaler als Schultern)
            rect(draw, 5, 15 + b, 6, 2, TINT_MID)
            for y in (11, 13, 15):                    # Sixpack: drei Schattenlinien
                rect(draw, 7, y + b, 2, 1, TINT_DARK)
            pixel(draw, 6, 12 + b, TINT_DARK)         # vertikale Mittelrinne
            pixel(draw, 6, 14 + b, TINT_DARK)
            pixel(draw, 6, 16 + b, TINT_DARK)
    else:
        # average: Standardkörper mit dezenter Zeichnung
        draw_legs(draw, p, TINT_MID, TINT_DARK)
        rect(draw, 5, 9 + b, 6, 9, TINT_MID)
        if female:
            rect(draw, 5, 10 + b, 6, 2, TINT_LIGHT)   # Brust
            pixel(draw, 6, 12 + b, TINT_DARK)         # eingezogene Taille
            pixel(draw, 6, 13 + b, TINT_DARK)
            pixel(draw, 10, 12 + b, TINT_DARK)
            pixel(draw, 10, 13 + b, TINT_DARK)
            rect(draw, 5, 15 + b, 6, 2, TINT_LIGHT)   # Hüfte
        else:
            pixel(draw, 6, 13 + b, TINT_DARK)          # leichte Taille andeuten
            pixel(draw, 10, 13 + b, TINT_DARK)
    rect(draw, 4, 10 + b + p["arm"], 1, 5, TINT_MID)     # hinterer Arm
    if heavy:
        rect(draw, 3, 10 + b + p["arm"], 1, 6, TINT_MID)
        rect(draw, 12, 10 + b - p["arm"], 1, 6, TINT_LIGHT)
    elif athletic and not female:
        rect(draw, 3, 10 + b + p["arm"], 1, 5, TINT_MID)
        rect(draw, 12, 10 + b - p["arm"], 1, 5, TINT_LIGHT)
    rect(draw, 11, 10 + b - p["arm"], 1, 5, TINT_LIGHT)  # vorderer Arm
    pixel(draw, 11, 15 + b - p["arm"], TINT_LIGHT)       # Hand
    if heavy:
        pixel(draw, 12, 15 + b + p["arm"], TINT_MID)
        pixel(draw, 12, 16 + b - p["arm"], TINT_LIGHT)
    rect(draw, 7, 8 + b, 2, 1, TINT_MID)                 # Hals
    rect(draw, 5, 3 + b, 6, 6, TINT_LIGHT)               # Kopf
    rect(draw, 5, 7 + b, 1, 2, TINT_MID)                 # Wangen-/Kieferschatten
    pixel(draw, 10, 7 + b, TINT_MID)
    pixel(draw, 8, 5 + b, TINT_MID)                      # Brauenlinie
    pixel(draw, 10, 5 + b, TINT_MID)
    pixel(draw, 8, 6 + b, TINT_EYE)                      # Augen (Blick nach rechts)
    pixel(draw, 10, 6 + b, TINT_EYE)
    pixel(draw, 9, 8 + b, TINT_DARK)                     # Mund
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
# G10: leichte Kleidung statt Vollpanzer — der Rumpf (y 9-16) bleibt frei, damit der
# Körpertyp sichtbar bleibt; die Klasse erkennen Klinge/Stab/Kapuze/Heiligenschein.
def outfit_frame(cls, p):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b, arm, frame = p["bob"], p["arm"], p["frame"]
    if cls == "warrior":
        draw_legs(draw, p, DARK_STEEL, BLACK)              # Hose
        rect(draw, 5, 15 + b, 6, 1, LEATHER)               # Gürtel
        pixel(draw, 8, 15 + b, GOLD)                       # Schnalle
        rect(draw, 5, 9 + b, 2, 1, LEATHER)                # Riemen über die Schultern (nur y 9)
        rect(draw, 9, 9 + b, 2, 1, LEATHER)
        rect(draw, 4, 9 + b, 2, 2, DARK_STEEL)             # Schulterplatten bleiben
        rect(draw, 10, 9 + b, 2, 2, DARK_STEEL)
        rect(draw, 11, 13 + b - arm, 1, 2, DARK_STEEL)     # Panzerhandschuh
        rect(draw, 12, 5 + b, 1, 10, STEEL)                # Klinge
        pixel(draw, 12, 5 + b, (230, 230, 240, 255))
        rect(draw, 11, 15 + b, 3, 1, GOLD)                 # Parierstange
        pixel(draw, 12, 16 + b, LEATHER)
    elif cls == "mage":
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        draw_legs(draw, p, DEEP_PURPLE, BLACK)            # Hose
        rect(draw, 4 + sway, 17 + b, 8, 5, DEEP_PURPLE)   # knielanger Rock ab der Hüfte (Rumpf frei)
        rect(draw, 4, 8 + b, 8, 2, PURPLE)                 # Kapuze (zurückgeschlagen)
        rect(draw, 5, 15 + b, 6, 1, LEATHER)               # Kordel als Gürtel
        rect(draw, 11, 11 + b - arm, 1, 4, PURPLE)         # Ärmel am vorderen Arm
        rect(draw, 13, 4 + b, 1, 19, WOOD)                 # Stab
        rect(draw, 12, 1 + b, 3, 3, SOUL if frame % 2 == 0 else MANA)
        pixel(draw, 13, 0 + b, (220, 255, 240, 255))
    elif cls == "shadow":
        draw_legs(draw, p, SHADOW, BLACK)                  # enge Hose
        rect(draw, 5, 15 + b, 6, 1, SHADOW)                 # Hüfttuch
        rect(draw, 4, 2 + b, 8, 3, BLACK)                  # Kapuze bleibt
        rect(draw, 4, 2 + b, 2, 8, BLACK)
        rect(draw, 11, 3 + b, 1, 5, BLACK)
        rect(draw, 7, 7 + b, 4, 2, (40, 32, 52, 255))      # Maske
        rect(draw, 3, 10 + b, 1, 5, BLACK)                 # Umhangstreifen hinter dem Rücken
        rect(draw, 12, 13 + b - arm, 1, 4, STEEL)          # Dolch
        rect(draw, 11, 16 + b - arm, 3, 1, DARK_STEEL)
    else:  # angel
        # Gefallener Engel: helle, schlichte Kleidung mit Gürtel (Rumpf frei)
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        robe = (218, 212, 198, 255)
        robe_dark = (170, 164, 152, 255)
        draw_legs(draw, p, robe_dark, BLACK)
        rect(draw, 5 + sway, 17 + b, 6, 5, robe)           # knielanger Rock ab der Hüfte
        rect(draw, 6, 9 + b, 4, 1, shift(robe, 15))        # heller Kragen (nur y 9)
        rect(draw, 5, 15 + b, 6, 1, LEATHER)               # schlichter Gürtel
        pixel(draw, 8, 15 + b, GOLD)                       # kleine Schnalle
        rect(draw, 11, 11 + b - arm, 1, 4, robe_dark)      # Ärmel am vorderen Arm
        rect(draw, 12, 10 + b - arm, 1, 2, (235, 232, 224, 255))  # Hand
    return polish(image)


def accent_frame(cls, p):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b, frame = p["bob"], p["frame"]
    if cls == "warrior":
        rect(draw, 2, 10 + b, 2, 10, TINT_DARK)            # Umhang hinten
        if p["run"]:
            rect(draw, 1, 12 + b + frame % 2, 1, 6, TINT_DARK)
        rect(draw, 6, 9 + b, 4, 2, TINT_MID)               # kleines Wappen nur auf der Brust (y 9-10)
        rect(draw, 7, 9 + b, 2, 1, TINT_LIGHT)
    elif cls == "mage":
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        rect(draw, 7, 9 + b, 2, 2, TINT_MID)               # kurze Stola über dem Brustbein (y 9-10)
        pixel(draw, 7, 11 + b, TINT_LIGHT)
        pixel(draw, 8, 11 + b, TINT_LIGHT)
        rect(draw, 4 + sway, 21 + b, 8, 1, TINT_LIGHT)     # Saum unten bleibt
    elif cls == "shadow":
        rect(draw, 5, 9 + b, 6, 2, TINT_MID)               # Schal um den Hals
        if p["run"]:
            rect(draw, 1 + frame % 2, 10 + b, 4, 1, TINT_DARK)
        else:
            rect(draw, 4, 10 + b, 1, 3, TINT_DARK)
    else:  # angel
        # Gefallener Engel: Schärpe über die Schulter + Heiligenschein, einfärbbar (Graustufen)
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        rect(draw, 6, 9 + b, 2, 1, TINT_MID)               # Schärpe an der Schulter (nur y 9)
        for y in range(10 + b, 16 + b):                   # diagonal über die Brust (dünn, 1 px)
            pixel(draw, 6 + (y - 10 - b) // 3, y, TINT_MID)
        pixel(draw, 8, 16 + b, TINT_DARK)                  # Quaste
        pixel(draw, 9, 17 + b, TINT_DARK)
        halo = (240, 240, 240, 255)                        # Heiligenschein als Ellipse über dem Kopf
        draw.arc([4 + sway, 0 + b, 11 + sway, 4 + b], 180, 360, fill=TINT_LIGHT, width=1)
        pixel(draw, 4 + sway, 1 + b, halo)
        pixel(draw, 11 + sway, 1 + b, halo)
    return polish(image, outline=(50, 50, 56, 255), light=10, dark=-20, gradient=0)


# ------------------------------------------------------------------ Make-up (Graustufen, wird eingefärbt)
def makeup_frame(style, p):
    """Wenige Pixel im Gesicht (Kopf y 3-9, Augen y 6). Wandert mit p['bob'] mit."""
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    if style == "liner":                       # Lidstrich: dunkle Linie unter den Brauen
        pixel(draw, 7, 5 + b, TINT_DARK)
        pixel(draw, 8, 5 + b, TINT_DARK)
        pixel(draw, 9, 5 + b, TINT_DARK)
        pixel(draw, 10, 5 + b, TINT_DARK)
        pixel(draw, 7, 4 + b, TINT_MID)        # kleiner Flügel am äußeren Lid
    elif style == "shadow":                   # Lidschatten: Fläche über den Augen
        rect(draw, 7, 4 + b, 4, 2, TINT_MID)
        pixel(draw, 7, 4 + b, TINT_LIGHT)
        pixel(draw, 10, 4 + b, TINT_DARK)
    elif style == "lips":                     # betonter Mund
        rect(draw, 8, 8 + b, 3, 1, TINT_DARK)
        pixel(draw, 8, 7 + b, TINT_MID)
        pixel(draw, 10, 7 + b, TINT_MID)
    elif style == "war":                      # Kriegsbemalung: Streifen über die Wangen
        rect(draw, 5, 7 + b, 1, 3, TINT_MID)
        rect(draw, 10, 7 + b, 1, 3, TINT_MID)
        pixel(draw, 5, 10 + b, TINT_DARK)
        pixel(draw, 10, 10 + b, TINT_DARK)
    return polish(image, outline=None, light=8, dark=-12, gradient=0)


# ------------------------------------------------------------------ Flügel (Graustufen, hinter dem Körper)
def wings_frame(kind, p):
    """Ragt links und rechts über die Figur hinaus, Fußlinie bleibt gleich. In jump weiter geöffnet.
    Wir nur die linke Flügelhälfte und spiegelt sie an x=8 auf die rechte Seite."""
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    spread = 2 if p["jump"] else 0            # Sprung = erkennbar weiter geöffnet
    flap = p["frame"] % 2
    top = 6 + b - flap + spread
    half = new_image(8, 24)
    hd = ImageDraw.Draw(half)
    if kind == "feathered":                   # gefiedert, hell (Engel)
        for i in range(5):                    # federige Treppenstufen nach außen
            hd.rectangle([7 - min(4, i + 1), top + i * 2, 7, top + i * 2 + 1], fill=TINT_LIGHT if i < 2 else TINT_MID)
        pixel(hd, 7, top + 10, TINT_MID)      # unterste Feder
    elif kind == "tattered":                  # zerfetzt, dunkel (gefallen)
        for i in range(4):
            if (i + p["frame"]) % 3 != 2:     # Lücken = zerfetzter Look
                hd.rectangle([7 - (2 if i % 2 else 1), top + i * 2, 7, top + i * 2 + 1], fill=TINT_DARK)
        pixel(hd, 6, top + 8, TINT_DARK)
        pixel(hd, 4, top + 9, TINT_DARK)
    else:                                     # ember: glühend, aus Asche
        for i in range(4):
            hd.rectangle([7 - (3 if i % 2 else 1), top + i * 2, 7, top + i * 2 + 1],
                        fill=TINT_LIGHT if i < 2 else TINT_DARK)
        pixel(hd, 7, top, TINT_LIGHT)
    mirror = half.transpose(Image.FLIP_LEFT_RIGHT)   # rechte Flügelhälfte = Spiegel
    image.paste(half, (0, 0), half)
    image.paste(mirror, (8, 0), mirror)
    return polish(image, outline=None, light=14, dark=-18, gradient=8)


# ------------------------------------------------------------------ Rüstung (G11: Graustufen, Ebene über der Kleidung)
def armor_frame(kind, p):
    """Deckt den Rumpf (y 9-17) ab — hier darf eine geschlossene Fläche entstehen.

    Bewusst FARBIG statt in Graustufen: Der Code zeichnet die Rüstung ungetönt (Color.White),
    Graustufen blieben also grau und alle vier sahen aus wie derselbe helle Klotz. Mit eigenem
    Material erkennt man auf einen Blick, was man trägt.
    """
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    if kind == "leather":
        light, mid, dark = (150, 104, 68, 255), LEATHER, DARK_WOOD
        rect(draw, 5, 9 + b, 6, 7, mid)
        rect(draw, 5, 9 + b, 6, 1, light)                  # Kragen
        for y in range(10 + b, 15 + b):                    # Kreuzschnürung vorn
            pixel(draw, 7 if (y - b) % 2 else 8, y, dark)
            pixel(draw, 8 if (y - b) % 2 else 7, y, light)
        rect(draw, 5, 15 + b, 6, 1, dark)                  # Saum
        pixel(draw, 5, 11 + b, dark)                       # Seitennaht
        pixel(draw, 10, 13 + b, dark)
    elif kind == "chain":
        rect(draw, 5, 9 + b, 6, 7, STEEL)
        for y in range(9 + b, 16 + b):                     # versetztes Maschenmuster
            for x in range(5, 11):
                pixel(draw, x, y, DARK_STEEL if (x + y) % 2 == 0 else STEEL)
        rect(draw, 4, 9 + b, 2, 2, DARK_STEEL)             # kurze Ärmel
        rect(draw, 10, 9 + b, 2, 2, DARK_STEEL)
        rect(draw, 5, 9 + b, 6, 1, (210, 210, 225, 255))   # Lichtkante oben
        rect(draw, 5, 15 + b, 6, 1, DARK_STEEL)
    elif kind == "scale":
        rect(draw, 5, 9 + b, 6, 7, DARK_GOLD)              # Bronzegrund
        for y in range(9 + b, 16 + b):                     # überlappende Schuppenreihen
            for x in range(5 + ((y - b) % 2), 11, 2):
                pixel(draw, x, y, GOLD)
        rect(draw, 4, 9 + b, 2, 2, GOLD)                   # Schulterstücke
        rect(draw, 10, 9 + b, 2, 2, GOLD)
        rect(draw, 5, 15 + b, 6, 1, DARK_WOOD)             # Lederkante unten
    else:  # ash: schwerer Harnisch mit Glutadern
        rect(draw, 4, 9 + b, 8, 7, DARK_STONE)             # breiter, schwerer Harnisch
        rect(draw, 4, 9 + b, 8, 1, ASH)                    # Lichtkante
        rect(draw, 4, 9 + b, 1, 7, ASH)
        for x, y in ((6, 10), (7, 11), (6, 12), (9, 11), (9, 13), (8, 14)):
            pixel(draw, x, y + b, EMBER)                   # Glutadern ziehen sich durch die Platte
        for x, y in ((7, 12), (9, 12), (8, 13)):
            pixel(draw, x, y + b, FLAME)                   # hellere Kerne
        rect(draw, 3, 9 + b, 1, 3, ASH)                    # breite Schulterklappen
        rect(draw, 12, 9 + b, 1, 3, ASH)
        rect(draw, 4, 15 + b, 8, 1, BLACK)                 # schwerer Saum
    return polish(image, outline=OUTLINE, light=10, dark=-20, gradient=0)


def layer_sheet(frame_function):
    rows = [[frame_function(pose(anim, i)) for i in range(count)] for anim, count in ANIMATIONS]
    return build_sheet(16, 24, rows)


def generate(textures):
    layer_sheet(body_frame).save(textures / "char_body.png")
    for variant in ("m_heavy", "m_average", "m_athletic", "f_heavy", "f_average", "f_athletic"):
        layer_sheet(lambda p, v=variant: variant_body_frame(p, v)).save(textures / f"char_body_{variant}.png")
    for style in ("short", "long", "braid", "mohawk", "hooded_curls"):
        layer_sheet(lambda p, s=style: hair_frame(s, p)).save(textures / f"char_hair_{style}.png")
    for style in ("liner", "shadow", "lips", "war"):
        layer_sheet(lambda p, s=style: makeup_frame(s, p)).save(textures / f"char_makeup_{style}.png")
    for kind in ("feathered", "tattered", "ember"):
        layer_sheet(lambda p, k=kind: wings_frame(k, p)).save(textures / f"char_wings_{kind}.png")
    for cls in ("warrior", "mage", "shadow", "angel"):
        layer_sheet(lambda p, c=cls: outfit_frame(c, p)).save(textures / f"char_outfit_{cls}.png")
        layer_sheet(lambda p, c=cls: accent_frame(c, p)).save(textures / f"char_accent_{cls}.png")
    for kind, name in (("leather", "leather"), ("chain", "chain"), ("scale", "scale"), ("ash", "ash")):
        layer_sheet(lambda p, k=kind: armor_frame(k, p)).save(textures / f"char_armor_{name}.png")


__all__ = ["generate", "OUTLINE"]

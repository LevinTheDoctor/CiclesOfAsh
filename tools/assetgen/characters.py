"""
Spielerfigur als EBENEN (24x32 pro Frame), damit der Charakter-Editor sie frei kombinieren kann:
  body   - Haut (Graustufen -> im Spiel mit Hautton eingefärbt)
  hair_* - Frisuren (Graustufen -> Haarfarbe)
  outfit_<klasse> - feste Farben der Klasse
  accent_<klasse> - Wappenrock/Stola/Schal (Graustufen -> Akzentfarbe)
  armor_<art>     - feste Rüstungsmaterialien, ungetönt gezeichnet
Zeichenreihenfolge im Spiel: body, hair, outfit, accent.
Zeilen: 0 idle, 1 run, 2 jump, 3 hurt (je bis zu 4 Frames).

16-Bit-Sprache (Auftrag G12/G13): vier bis sechs Tonwerte je Material statt drei,
Lichtquelle oben links, Materialkontrast (Leder matt, Kette glänzend, Bronze warm, Stein stumpf).
Graustufen-Ebenen bekommen zusaetzliche Zwischentoene (TINT_TOP), damit die Tonwertabstufung
der Farbtönung mehr Spielraum gibt.
"""
from PIL import Image, ImageDraw

from .core import (ASH, BLACK, DARK_GOLD, DARK_STEEL, DARK_STONE, DARK_WOOD, DEEP_PURPLE, EMBER, FLAME, GOLD,
                   LEATHER, MANA, OUTLINE, PURPLE, SHADOW, SOUL, STEEL,
                   TINT_DARK, TINT_EYE, TINT_LIGHT, TINT_MID, WOOD, build_sheet, new_image, pixel, polish, rect, shift)

ANIMATIONS = (("idle", 4), ("run", 4), ("jump", 4), ("hurt", 2))
TINT_OUTLINE = (60, 60, 66, 255)
TINT_TOP = (225, 225, 225, 255)   # 4. Graustufe: Zwischenwert ueber TINT_MID (16-Bit)
TINT_DEEP = (110, 110, 110, 255)  # 5. Graustufe: unter TINT_DARK

W, H = 24, 32                     # Frame-Groesse der Figuren-Ebenen


def pose(anim, frame):
    """Gemeinsame Pose aller Ebenen -> alle Layer bleiben synchron."""
    run = anim == "run"
    return {
        "bob": 1 if anim in ("idle", "hurt") and frame in (1, 2) else 0,
        "legs": [(-2, 2, 0, 1), (0, 0, 2, 0), (2, -2, 2, 0), (0, 0, 0, 2)][frame] if run else (0, 0, 0, 0),
        "arm": [1, 0, -1, 0][frame] if run else 0,
        "jump": anim == "jump",
        "run": run,
        "frame": frame,
    }


def draw_legs(draw, p, color, boot, shade=None):
    """Beine auf 24x32: 2 px breit, 8 px hoch, vo = Verschleiß-/Schattenfarbe."""
    shade = shade or shift(color, -35)
    if p["jump"]:
        rect(draw, 7, 24, 3, 6, color)
        rect(draw, 13, 23, 3, 6, color)
        rect(draw, 7, 24, 1, 6, shift(color, 20))            # Lichtkante links
        rect(draw, 13, 26, 1, 3, shade)                     # Schattenkante
        rect(draw, 7, 29, 3, 1, boot)
        rect(draw, 13, 28, 3, 1, boot)
        return
    ls, rs, ll, rl = p["legs"]
    rect(draw, 7 + ls, 24, 3, 8 - ll, color)
    rect(draw, 13 + rs, 24, 3, 8 - rl, color)
    rect(draw, 7 + ls, 24, 1, 8 - ll, shift(color, 20))     # Licht oben links
    rect(draw, 9 + ls, 24 + 3, 1, 5 - ll, shade)            # Schattenkante
    rect(draw, 13 + rs, 26, 1, 6 - rl, shade)
    rect(draw, 7 + ls, 31 - ll, 3, 1, boot)
    rect(draw, 13 + rs, 31 - rl, 3, 1, boot)


# ------------------------------------------------------------------ Körper (G9 + G13)
def body_frame(p):
    return variant_body_frame(p, "m_average")


def head_and_neck(draw, p):
    """Kopf/Hals aller Körpertypen IDENTISCH (damit Haare und Make-up passen).
    16-Bit: 5 Hauttonwerte, Licht oben links, Wangen-, Brauen- und Mundschatten."""
    b = p["bob"]
    rect(draw, 9, 11 + b, 4, 2, TINT_MID)                   # Hals
    rect(draw, 9, 11 + b, 1, 2, TINT_TOP)                   # Hals-Lichtkante
    rect(draw, 7, 3 + b, 8, 9, TINT_LIGHT)                  # Kopf
    rect(draw, 7, 3 + b, 3, 2, TINT_TOP + (0,) if False else (250, 250, 250, 255))  # Stirnlicht
    rect(draw, 7, 8 + b, 2, 3, TINT_MID)                    # Wangen-/Kieferschatten
    rect(draw, 12, 9 + b, 2, 2, TINT_MID)
    pixel(draw, 8, 9 + b, TINT_DEEP)                        # Kinn-Schatten
    rect(draw, 8, 6 + b, 2, 1, TINT_MID)                    # Brauen
    rect(draw, 11, 6 + b, 2, 1, TINT_MID)
    pixel(draw, 8, 7 + b, TINT_EYE)                         # Augen (Blick nach rechts)
    pixel(draw, 11, 7 + b, TINT_EYE)
    pixel(draw, 8, 11 + b, TINT_DARK)                       # Mund


def variant_body_frame(p, variant):
    """Körpervarianten auf 24x32 (G15): Die Statur kommt aus dem UMRISS, nicht nur der Schattierung.
    athletic: V-Form — Schultern x 3-20, Taille x 6-17 deutlich eingezogen, Arme ABSTEHEND.
    average:  gleichmäßig x 4-19 mit leichter Taille.
    heavy:    durchgehend breit x 2-21, Bauch vorgewölbt (unten breiter), Arme dicht am Leib.
    Rumpf y 13-23, Beine ab 24. Kopf (y 3-11) und Fußlinie (y 31) bleiben identisch."""
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    heavy = variant.endswith("heavy")
    athletic = variant.endswith("athletic")
    female = variant.startswith("f")
    draw_legs(draw, p, TINT_MID, TINT_DARK, TINT_DEEP)
    if heavy:
        # durchgehend breit (x 2-21), Bauch vorgewölbt: Rumpf wird nach unten BREITER.
        # Arme eng am Leib (x 1-4 / 19-22), Silhouette durchgehend geschlossen.
        widths = [18, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20]
        for i, wdt in enumerate(widths):
            x0 = (W - wdt) // 2
            rect(draw, x0, 13 + b + i, wdt, 1, TINT_MID)
        rect(draw, 3, 13 + b, 18, 4, TINT_LIGHT)             # massiver Kasten oben
        rect(draw, 3, 13 + b, 6, 2, (250, 250, 250, 255))   # Licht oben links
        rect(draw, 3, 21 + b, 18, 2, TINT_DEEP)             # vorgewölbte Bauch-Unterkante
        for y in (15, 17, 19):                               # Rand-Deutung statt Muskeln
            pixel(draw, 2, y + b, TINT_DEEP)
            pixel(draw, 21, y + b, TINT_DEEP)
        if female:
            rect(draw, 6, 15 + b, 3, 2, TINT_TOP)            # sanfter Brust-Schatten
            rect(draw, 15, 15 + b, 3, 2, TINT_TOP)
            pixel(draw, 8, 17 + b, TINT_DEEP)
            pixel(draw, 15, 17 + b, TINT_DEEP)
        else:
            rect(draw, 2, 16 + b, 1, 5, TINT_DEEP)           # seitliche Wamst-Rundung
            rect(draw, 21, 16 + b, 1, 5, TINT_DEEP)
            rect(draw, 5, 20 + b, 14, 1, TINT_DARK)         # Bauchfalte
            pixel(draw, 4, 19 + b, TINT_DEEP)                # Bauch-Bogen unten außen
            pixel(draw, 19, 19 + b, TINT_DEEP)
    elif athletic:
        # V-Form: breite Schultern (x 3-20), KRIFTIG eingezogene Taille (x 7-16, 4 Zeilen tief),
        # Hüfte/Becken wieder breiter — die ARME stehen ab (Lücke zwischen Arm und Taille!)
        if female:
            # Schultern x 4-19, Taille x 7-16, Hüfte x 4-19 (Sanduhr)
            torso = [16, 16, 15, 12, 9, 8, 8, 10, 13, 16, 16]
            for i, wdt in enumerate(torso):
                x0 = (W - wdt) // 2
                rect(draw, x0, 13 + b + i, wdt, 1, TINT_MID)
            rect(draw, 4, 13 + b, 16, 3, TINT_LIGHT)        # trainierte Schultern
            rect(draw, 4, 13 + b, 6, 2, (250, 250, 250, 255))
            for y in (17, 18):                               # Taille innen nachschattieren
                pixel(draw, 8, y + b, TINT_DEEP)
                pixel(draw, 15, y + b, TINT_DEEP)
            for y in (17, 19, 21):                           # Bauchmuskeln
                rect(draw, 10, y + b, 3, 1, TINT_DEEP)
                rect(draw, 13, y + b, 2, 1, TINT_DEEP)
        else:
            # Schultern x 3-20, Taille x 7-16 (4 Zeilen), unten wieder ausgestellt
            torso = [18, 18, 16, 12, 9, 8, 8, 11, 15, 16, 16]
            for i, wdt in enumerate(torso):
                x0 = (W - wdt) // 2
                rect(draw, x0, 13 + b + i, wdt, 1, TINT_MID)
            rect(draw, 3, 13 + b, 18, 3, TINT_LIGHT)        # breite Schultern (x 3-20)
            rect(draw, 3, 13 + b, 7, 2, (250, 250, 250, 255))
            for y in (17, 19, 21):                           # Sixpack: drei Schattenlinien
                rect(draw, 10, y + b, 4, 1, TINT_DEEP)
            rect(draw, 11, 16 + b, 1, 6, TINT_DEEP)          # vertikale Mittelrinne
            rect(draw, 10, 22 + b, 4, 1, TINT_DARK)         # Leistenbeuge
            pixel(draw, 8, 17 + b, TINT_DEEP)                # Taille-V-Schatten
            pixel(draw, 8, 18 + b, TINT_DEEP)
            pixel(draw, 15, 17 + b, TINT_DEEP)
            pixel(draw, 15, 18 + b, TINT_DEEP)
    else:
        # average: gleichmäßig x 4-19 mit leichter Taille
        torso = [16, 16, 16, 16, 16, 15, 15, 16, 16, 16, 16]
        for i, wdt in enumerate(torso):
            x0 = (W - wdt) // 2
            rect(draw, x0, 13 + b + i, wdt, 1, TINT_MID)
        rect(draw, 4, 13 + b, 16, 3, TINT_LIGHT)
        rect(draw, 4, 13 + b, 6, 2, (250, 250, 250, 255))
        if female:
            rect(draw, 8, 15 + b, 3, 2, TINT_TOP)           # Brust
            rect(draw, 13, 15 + b, 3, 2, TINT_TOP)
            pixel(draw, 8, 18 + b, TINT_DEEP)                # eingezogene Taille
            pixel(draw, 8, 19 + b, TINT_DEEP)
            pixel(draw, 15, 18 + b, TINT_DEEP)
            pixel(draw, 15, 19 + b, TINT_DEEP)
        else:
            pixel(draw, 8, 18 + b, TINT_DEEP)                # leichte Taille
            pixel(draw, 15, 18 + b, TINT_DEEP)
    # Arme: beim ATHLETEN abstehend (Lücke zum Rumpf = V-Form lesbar), heavy eng am Leib,
    # average normal. Der hintere Arm x 4-5/19-20, vorderer x 17-18 — beim Athletic weiter außen.
    arm = p["arm"]
    if athletic:
        # Arme ganz aussen (x 2-3 / 20-21). Zusammen mit der auf 8 px verschmaelerten Taille
        # bleiben je 4 px Luft; polish() legt beidseitig 1 px Kontur an, sichtbar bleiben 2 px.
        # Vorher waren es 2 px Luft - die Kontur schloss die Luecke komplett und die V-Form war weg.
        rect(draw, 2, 14 + b + arm, 2, 7, TINT_MID)          # hinterer Arm abstehend
        rect(draw, 2, 14 + b + arm, 1, 7, TINT_TOP)
        rect(draw, 20, 14 + b - arm, 2, 7, TINT_LIGHT)      # vorderer Arm abstehend
        rect(draw, 21, 14 + b - arm, 1, 7, TINT_TOP)
        pixel(draw, 2, 21 + b + arm, TINT_MID)               # Hände
        pixel(draw, 21, 21 + b - arm, TINT_LIGHT)
    elif heavy:
        rect(draw, 2, 14 + b + arm, 3, 9, TINT_MID)          # Arme eng am wuchtigen Leib
        rect(draw, 2, 14 + b + arm, 1, 9, TINT_TOP)
        rect(draw, 19, 14 + b - arm, 3, 9, TINT_LIGHT)
        rect(draw, 21, 14 + b - arm, 1, 9, TINT_TOP)
        pixel(draw, 2, 23 + b + arm, TINT_MID)
        pixel(draw, 21, 23 + b - arm, TINT_LIGHT)
    else:
        rect(draw, 5, 14 + b + arm, 2, 8, TINT_MID)
        rect(draw, 5, 14 + b + arm, 1, 8, TINT_TOP)
        rect(draw, 17, 14 + b - arm, 2, 8, TINT_LIGHT)
        rect(draw, 18, 14 + b - arm, 1, 8, TINT_TOP)
        pixel(draw, 17, 22 + b - arm, TINT_LIGHT)            # Hand vorn
        pixel(draw, 18, 22 + b - arm, TINT_TOP)
        pixel(draw, 5, 22 + b + arm, TINT_MID)               # Hand hinten
    head_and_neck(draw, p)
    return polish(image, outline=TINT_OUTLINE, light=14, dark=-24, gradient=12)


# ------------------------------------------------------------------ Haare (Graustufen)
def hair_frame(style, p):
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    light, mid, dark, top = TINT_LIGHT, TINT_MID, TINT_DARK, (250, 250, 250, 255)
    if style == "short":
        rect(draw, 7, 2 + b, 8, 3, mid)
        rect(draw, 6, 4 + b, 3, 3, mid)
        rect(draw, 6, 2 + b, 4, 1, top)
        rect(draw, 13, 4 + b, 2, 1, light)
        pixel(draw, 9, 2 + b, light)
    elif style == "long":
        rect(draw, 7, 2 + b, 8, 3, mid)
        rect(draw, 4, 4 + b, 4, 12, mid)                    # langes Haar über den Rücken
        rect(draw, 4, 4 + b, 1, 10, TINT_TOP)              # Lichtkante links
        rect(draw, 4, 14 + b, 3, 3, dark)
        rect(draw, 13, 4 + b, 2, 1, light)
        pixel(draw, 9, 2 + b, light)
        rect(draw, 7, 2 + b, 3, 1, top)
    elif style == "braid":
        rect(draw, 7, 2 + b, 8, 3, mid)
        rect(draw, 6, 4 + b, 3, 4, mid)
        rect(draw, 7, 2 + b, 4, 1, top)
        for index, y in enumerate(range(8, 21)):           # Zopf: abwechselnd hell/dunkel
            pixel(draw, 4 + (index % 2), y + b, light if index % 2 else dark)
        rect(draw, 4, 21 + b, 2, 2, dark)                  # Zopfspitze
        pixel(draw, 4, 22 + b, GOLD)                        # Zopfspange (feste Farbe)
    elif style == "mohawk":
        rect(draw, 10, 0 + b, 4, 4, mid)
        pixel(draw, 10, 0 + b, top)
        rect(draw, 8, 0 + b, 1, 3, light)
        rect(draw, 7, 4 + b, 2, 3, dark)                    # rasierte Seiten
        rect(draw, 15, 4 + b, 2, 3, dark)
    elif style == "hooded_curls":
        for x, y in ((7, 2), (10, 1), (13, 2), (6, 4), (14, 3), (8, 2), (12, 2)):
            rect(draw, x, y + b, 2, 2, mid)
            pixel(draw, x, y + b, light)
        rect(draw, 7, 2 + b, 2, 1, top)
    return polish(image, outline=TINT_OUTLINE, light=10, dark=-20, gradient=0)


# ------------------------------------------------------------------ Outfits (feste Farben, G10 + G13)
# Leichte Kleidung — Rumpf (y 16-20) bleibt frei; Klasse erkennen Klinge/Stab/Kapuze/Schein.
def outfit_frame(cls, p):
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b, arm, frame = p["bob"], p["arm"], p["frame"]
    if cls == "warrior":
        steel_l = (200, 200, 218, 255)                     # Stahl-Licht
        steel_m = DARK_STEEL                                # Stahl-Mittelton (Hose)
        steel_d = (55, 55, 68, 255)                         # Stahl-Schatten
        draw_legs(draw, p, steel_m, BLACK, steel_d)
        rect(draw, 7, 21 + b, 10, 2, LEATHER)               # Gürtel
        rect(draw, 11, 21 + b, 2, 2, GOLD)                  # Schnalle
        rect(draw, 7, 13 + b, 3, 1, LEATHER)                # Riemen über die Schultern (nur y 13)
        rect(draw, 14, 13 + b, 3, 1, LEATHER)
        rect(draw, 6, 13 + b, 3, 3, steel_m)                # Schulterplatten
        rect(draw, 6, 13 + b, 3, 1, steel_l)
        rect(draw, 15, 13 + b, 3, 3, steel_m)
        rect(draw, 15, 13 + b, 3, 1, steel_l)
        rect(draw, 18, 18 + b - arm, 2, 3, steel_m)          # Panzerhandschuh am vorderen Arm
        rect(draw, 19, 18 + b - arm, 1, 3, steel_l)
        rect(draw, 21, 7 + b, 2, 13, STEEL)                  # Klinge
        rect(draw, 21, 7 + b, 1, 13, steel_l)
        pixel(draw, 21, 7 + b, (235, 235, 246, 255))
        rect(draw, 20, 20 + b, 4, 1, GOLD)                   # Parierstange
        pixel(draw, 22, 21 + b, LEATHER)
    elif cls == "mage":
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        robe_l = (78, 48, 110, 255)                         # Robe-Licht
        robe_m = DEEP_PURPLE
        robe_d = (36, 20, 56, 255)
        draw_legs(draw, p, robe_m, BLACK, robe_d)
        rect(draw, 6 + sway, 23 + b, 12, 6, robe_m)         # knielanger Rock ab der Hüfte
        rect(draw, 6 + sway, 23 + b, 3, 6, robe_l)          # Lichtkante
        rect(draw, 14 + sway, 27 + b, 4, 2, robe_d)         # Faltenwurf
        rect(draw, 6, 12 + b, 12, 3, PURPLE)                # Kapuze (zurückgeschlagen)
        rect(draw, 6, 12 + b, 5, 1, robe_l)
        rect(draw, 7, 21 + b, 10, 1, LEATHER)               # Kordel als Gürtel
        rect(draw, 18, 15 + b - arm, 2, 6, PURPLE)           # Ärmel am vorderen Arm
        rect(draw, 18, 15 + b - arm, 1, 6, robe_l)
        rect(draw, 21, 5 + b, 2, 25, WOOD)                  # Stab
        rect(draw, 21, 5 + b, 1, 25, (120, 82, 58, 255))
        rect(draw, 20, 1 + b, 4, 4, SOUL if frame % 2 == 0 else MANA)
        pixel(draw, 21, 0 + b, (220, 255, 240, 255))
        pixel(draw, 22, 2 + b, (240, 255, 250, 255))
    elif cls == "shadow":
        cloak = (40, 32, 52, 255)
        draw_legs(draw, p, SHADOW, BLACK, (22, 16, 32, 255))
        rect(draw, 7, 21 + b, 10, 2, SHADOW)                 # Hüfttuch
        rect(draw, 7, 21 + b, 4, 1, (52, 42, 66, 255))
        rect(draw, 6, 3 + b, 12, 4, BLACK)                   # Kapuze
        rect(draw, 6, 3 + b, 3, 10, BLACK)
        rect(draw, 17, 4 + b, 2, 6, BLACK)
        rect(draw, 10, 10 + b, 6, 3, cloak)                  # Maske
        rect(draw, 11, 11 + b, 1, 1, (90, 110, 130, 255))    # Masken-Augen-Glanz
        rect(draw, 14, 11 + b, 1, 1, (90, 110, 130, 255))
        rect(draw, 4, 13 + b, 2, 7, BLACK)                   # Umhangstreifen hinter dem Rücken
        rect(draw, 21, 17 + b - arm, 2, 5, STEEL)            # Dolch
        rect(draw, 21, 17 + b - arm, 1, 5, (210, 210, 225, 255))
        rect(draw, 20, 21 + b - arm, 4, 1, DARK_STEEL)
    else:  # angel
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        robe = (218, 212, 198, 255)
        robe_l = (238, 233, 220, 255)
        robe_d = (170, 164, 152, 255)
        draw_legs(draw, p, robe_d, BLACK, (140, 134, 122, 255))
        rect(draw, 7 + sway, 23 + b, 10, 6, robe)           # knielanger Rock ab der Hüfte
        rect(draw, 7 + sway, 23 + b, 3, 6, robe_l)          # Licht oben links
        rect(draw, 13 + sway, 27 + b, 3, 2, robe_d)         # Falte
        rect(draw, 8, 13 + b, 8, 1, robe_l)                 # heller Kragen (nur y 13)
        rect(draw, 7, 21 + b, 10, 1, LEATHER)               # schlichter Gürtel
        pixel(draw, 12, 21 + b, GOLD)                       # kleine Schnalle
        rect(draw, 18, 15 + b - arm, 2, 6, robe_d)           # Ärmel am vorderen Arm
        rect(draw, 18, 15 + b - arm, 1, 6, robe_l)
    return polish(image)


def accent_frame(cls, p):
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b, frame = p["bob"], p["frame"]
    if cls == "warrior":
        rect(draw, 3, 14 + b, 3, 13, TINT_DARK)             # Umhang hinten
        rect(draw, 3, 14 + b, 1, 12, TINT_MID)              # Lichtkante
        if p["run"]:
            rect(draw, 1, 16 + b + frame % 2, 2, 8, TINT_DARK)
        rect(draw, 8, 13 + b, 8, 2, TINT_MID)                # kleines Wappen auf der Brust (y 13-14)
        rect(draw, 9, 13 + b, 6, 1, TINT_LIGHT)
    elif cls == "mage":
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        rect(draw, 9, 13 + b, 3, 3, TINT_MID)                # kurze Stola über dem Brustbein
        rect(draw, 9, 13 + b, 2, 1, TINT_TOP)
        pixel(draw, 10, 16 + b, TINT_LIGHT)
        rect(draw, 6 + sway, 28 + b, 12, 1, TINT_LIGHT)      # Saum unten
    elif cls == "shadow":
        rect(draw, 7, 13 + b, 10, 2, TINT_MID)               # Schal um den Hals
        rect(draw, 7, 13 + b, 5, 1, TINT_TOP)
        if p["run"]:
            rect(draw, 2 + frame % 2, 14 + b, 5, 1, TINT_DARK)
        else:
            rect(draw, 5, 14 + b, 2, 4, TINT_DARK)
    else:  # angel
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        rect(draw, 8, 13 + b, 3, 1, TINT_MID)                # Schärpe an der Schulter (nur y 13)
        for y in range(14 + b, 21 + b):                      # diagonal über die Brust (dünn, 1-2 px)
            pixel(draw, 8 + (y - 14 - b) // 3, y, TINT_MID)
            if (y - b) % 3 == 2:
                pixel(draw, 9 + (y - 14 - b) // 3, y, TINT_DARK)
        pixel(draw, 11, 22 + b, TINT_DARK)                   # Quaste
        pixel(draw, 12, 23 + b, TINT_DARK)
        halo = (240, 240, 240, 255)                          # Heiligenschein über dem Kopf
        draw.arc([6 + sway, 0 + b, 17 + sway, 6 + b], 180, 360, fill=TINT_LIGHT, width=1)
        pixel(draw, 6 + sway, 1 + b, halo)
        pixel(draw, 17 + sway, 1 + b, halo)
    return polish(image, outline=(50, 50, 56, 255), light=10, dark=-20, gradient=0)


# ------------------------------------------------------------------ Make-up (Graustufen, wird eingefärbt)
def makeup_frame(style, p):
    """Wenige Pixel im Gesicht (Kopf y 3-11, Augen y 7). Wandert mit p['bob'] mit."""
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    if style == "liner":                       # Lidstrich: dunkle Linie unter den Brauen
        rect(draw, 8, 6 + b, 3, 1, TINT_DARK)
        rect(draw, 11, 6 + b, 2, 1, TINT_DARK)
        pixel(draw, 7, 5 + b, TINT_MID)         # kleiner Flügel am äußeren Lid
    elif style == "shadow":                    # Lidschatten: Fläche über den Augen
        rect(draw, 8, 5 + b, 5, 2, TINT_MID)
        pixel(draw, 8, 5 + b, TINT_TOP)
        pixel(draw, 12, 5 + b, TINT_DARK)
    elif style == "lips":                      # betonter Mund
        rect(draw, 9, 11 + b, 4, 1, TINT_DARK)
        pixel(draw, 9, 10 + b, TINT_MID)
        pixel(draw, 12, 10 + b, TINT_MID)
    elif style == "war":                       # Kriegsbemalung: Streifen über die Wangen
        rect(draw, 7, 9 + b, 1, 4, TINT_MID)
        rect(draw, 15, 9 + b, 1, 4, TINT_MID)
        pixel(draw, 7, 13 + b, TINT_DARK)
        pixel(draw, 15, 13 + b, TINT_DARK)
    return polish(image, outline=None, light=8, dark=-12, gradient=0)


# ------------------------------------------------------------------ Flügel (Graustufen, hinter dem Körper)
def wings_frame(kind, p):
    """Ragt links und rechts über die Figur hinaus, Fußlinie bleibt gleich. In jump weiter geöffnet.
    Zeichnet nur die linke Flügelhälfte und spiegelt sie an x=12 auf die rechte Seite."""
    image = new_image(W, H)
    b = p["bob"]
    spread = 3 if p["jump"] else 0             # Sprung = erkennbar weiter geöffnet
    flap = p["frame"] % 2
    top = 9 + b - flap + spread
    half = new_image(12, H)
    hd = ImageDraw.Draw(half)
    if kind == "feathered":                    # gefiedert, hell (Engel)
        for i in range(7):                     # federige Treppenstufen nach außen
            x0 = 11 - min(9, i + 2)
            hd.rectangle([x0, top + i * 2, 11, top + i * 2 + 1],
                         fill=TINT_LIGHT if i < 3 else TINT_MID)
            if i % 2 == 0:
                hd.rectangle([x0, top + i * 2, x0 + 2, top + i * 2], fill=(250, 250, 250, 255))
        hd.rectangle([10, top + 14, 11, top + 15], fill=TINT_MID)   # unterste Feder
    elif kind == "tattered":                   # zerfetzt, dunkel (gefallen)
        for i in range(6):
            if (i + p["frame"]) % 3 != 2:      # Lücken = zerfetzter Look
                x0 = 11 - (4 if i % 2 else 2)
                hd.rectangle([x0, top + i * 2, 11, top + i * 2 + 1], fill=TINT_DARK)
        pixel(hd, 9, top + 12, TINT_DEEP)
        pixel(hd, 5, top + 14, TINT_DEEP)
    else:                                      # ember: glühend, aus Asche
        for i in range(6):
            x0 = 11 - (5 if i % 2 else 2)
            hd.rectangle([x0, top + i * 2, 11, top + i * 2 + 1],
                         fill=TINT_LIGHT if i < 3 else TINT_DARK)
        pixel(hd, 11, top, (250, 250, 250, 255))
    mirror = half.transpose(Image.FLIP_LEFT_RIGHT)   # rechte Flügelhälfte = Spiegel
    image.paste(half, (0, 0), half)
    image.paste(mirror, (12, 0), mirror)
    return polish(image, outline=None, light=14, dark=-18, gradient=8)


# ------------------------------------------------------------------ Rüstung (feste Materialien, Ebene über der Kleidung)
def punch(image, x, y, width=1, height=1):
    """Schlägt ein LOCH in die Rüstung: Die Pixel werden durchsichtig, darunter kommt der Körper
    zum Vorschein. Nicht über ImageDraw, denn das würde malen statt wegnehmen."""
    for oy in range(height):
        for ox in range(width):
            px, py = x + ox, y + oy
            if 0 <= px < image.width and 0 <= py < image.height:
                image.putpixel((px, py), (0, 0, 0, 0))


def armor_frame(kind, p, worn=False):
    """Deckt den Rumpf (y 13-21) ab — hier darf eine geschlossene Fläche entstehen.
    16-Bit: je Material vier bis sechs Tonwerte mit Licht oben links.
    Bewusst FARBIG statt in Graustufen: Der Code zeichnet die Rüstung ungetönt (Color.White).

    worn=True zeichnet dasselbe Stück ramponiert: Risse und echte Löcher (siehe punch), durch die
    der Körper durchscheint. Gezeichnet wird immer erst das heile Stück und dann der Schaden
    hineingeschlagen — so bleiben beide Fassungen zwangsläufig deckungsgleich."""
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    if kind == "leather":
        # Leder: matt, warm, kaum Glanz — vier stumpfe Töne
        light, mid, dark, deep = (150, 104, 68, 255), LEATHER, DARK_WOOD, (46, 28, 18, 255)
        rect(draw, 7, 13 + b, 10, 9, mid)
        rect(draw, 7, 13 + b, 10, 1, light)                  # Kragen
        rect(draw, 7, 13 + b, 3, 9, (128, 86, 56, 255))     # linke Seite im Licht
        for y in range(14 + b, 21 + b):                      # Kreuzschnürung vorn
            pixel(draw, 11 if (y - b) % 2 else 12, y, dark)
            pixel(draw, 12 if (y - b) % 2 else 11, y, light)
        rect(draw, 7, 21 + b, 10, 1, dark)                   # Saum
        pixel(draw, 7, 17 + b, deep)                          # Seitennähte
        pixel(draw, 16, 19 + b, deep)
        rect(draw, 8, 22 + b, 8, 1, dark)                     # untere Kante leicht ausgestellt
    elif kind == "chain":
        # Kette: hart glänzend — Silber mit hellem Licht und tiefem Schatten
        lite, mid, dark, deep = (210, 210, 225, 255), STEEL, DARK_STEEL, (52, 52, 64, 255)
        rect(draw, 7, 13 + b, 10, 9, mid)
        for y in range(13 + b, 22 + b):                      # versetztes Maschenmuster
            for x in range(7, 17):
                pixel(draw, x, y, dark if (x + y) % 2 == 0 else mid)
        rect(draw, 7, 13 + b, 10, 1, lite)                   # Lichtkante oben
        rect(draw, 7, 13 + b, 1, 9, (185, 185, 202, 255))   # Lichtkante links
        rect(draw, 6, 13 + b, 3, 3, dark)                     # kurze Ärmel
        rect(draw, 15, 13 + b, 3, 3, dark)
        rect(draw, 7, 21 + b, 10, 1, deep)                   # Saum
        pixel(draw, 9, 15 + b, (235, 235, 246, 255))         # Glanzpunkt (Glanzlicht oben links)
    elif kind == "scale":
        # Bronze: warm glänzend — Goldtöne mit hartem Licht
        lite, mid, dark, deep = (240, 210, 130, 255), GOLD, DARK_GOLD, (100, 70, 26, 255)
        rect(draw, 7, 13 + b, 10, 9, dark)                   # Bronzegrund
        for y in range(13 + b, 21 + b):                      # überlappende Schuppenreihen
            for x in range(7 + ((y - b) % 3), 17, 3):
                pixel(draw, x, y, mid)
                if y > 13 + b:
                    pixel(draw, x, y - 1, lite)
        rect(draw, 6, 13 + b, 3, 3, mid)                     # Schulterstücke
        rect(draw, 15, 13 + b, 3, 3, mid)
        rect(draw, 6, 13 + b, 3, 1, lite)
        rect(draw, 7, 21 + b, 10, 1, DARK_WOOD)              # Lederkante unten
        pixel(draw, 16, 17 + b, deep)                         # Schattenkante rechts
    else:  # ash: schwerer Harnisch mit Glutadern — Stein: stumpf, rau
        lite, mid, dark = (140, 134, 152, 255), (96, 92, 108, 255), DARK_STONE
        rect(draw, 6, 13 + b, 12, 9, mid)                    # breite, schwere Platte
        rect(draw, 6, 13 + b, 12, 1, lite)                   # Lichtkante
        rect(draw, 6, 13 + b, 2, 9, (118, 112, 130, 255))   # linke Lichtseite
        rect(draw, 6, 21 + b, 12, 1, BLACK)                  # schwerer Saum
        for x, y in ((9, 15), (10, 16), (9, 17), (14, 16), (14, 18), (13, 19)):
            pixel(draw, x, y + b, EMBER)                     # Glutadern durch die Platte
        for x, y in ((10, 16), (14, 16), (13, 18)):
            pixel(draw, x, y + b, FLAME)                     # hellere Kerne
        rect(draw, 5, 13 + b, 2, 4, mid)                     # breite Schulterklappen
        rect(draw, 17, 13 + b, 2, 4, mid)
        rect(draw, 5, 13 + b, 2, 1, lite)

    if worn:
        armor_damage(image, draw, kind, b)
    return polish(image, outline=OUTLINE, light=10, dark=-20, gradient=0)


def armor_damage(image, draw, kind, b):
    """Der Schaden auf der halb verbrauchten Rüstung. Je Material das, was dort zuerst nachgibt."""
    if kind == "leather":
        # Schnürung geplatzt, Riss quer über die Brust, Saum ausgefranst.
        for y in range(15, 20):
            punch(image, 11, y + b, 2, 1)                     # die Naht ist auf
        for step, y in enumerate(range(16, 21)):
            punch(image, 8 + step, y + b)                     # schräger Riss
        punch(image, 7, 22 + b, 2, 1)                         # Saum eingerissen
        punch(image, 14, 22 + b, 2, 1)
        rect(draw, 7, 20 + b, 3, 1, (46, 28, 18, 255))       # aufgescheuertes Leder
    elif kind == "chain":
        # An der linken Schulter sind die Maschen aufgerissen, dort blitzt die Haut durch.
        punch(image, 6, 13 + b, 3, 3)
        punch(image, 7, 16 + b, 2, 2)
        punch(image, 15, 19 + b, 2, 2)                        # zweites Loch tiefer rechts
        punch(image, 11, 17 + b, 1, 3)                        # aufgetrennte Reihe in der Mitte
        rect(draw, 9, 16 + b, 1, 2, (52, 52, 64, 255))       # dunkle Bruchkante
    elif kind == "scale":
        # Fleckweise fehlen Schuppen, die Lederkante unten ist gerissen.
        for x, y, w, h in ((8, 15, 2, 2), (13, 14, 2, 2), (10, 19, 3, 1), (15, 17, 1, 2)):
            punch(image, x, y + b, w, h)
        punch(image, 9, 22 + b, 3, 1)
        rect(draw, 7, 16 + b, 1, 4, (100, 70, 26, 255))      # freigelegter Bronzegrund
    else:  # ash
        # Platten abgesprengt, die Glut darunter liegt offen.
        punch(image, 6, 13 + b, 2, 3)
        punch(image, 16, 18 + b, 2, 3)
        punch(image, 11, 14 + b, 2, 1)
        for x, y in ((9, 16), (10, 17), (13, 17), (14, 19), (12, 20)):
            pixel(draw, x, y + b, EMBER)                      # Adern brechen auf
        for x, y in ((10, 17), (13, 17)):
            pixel(draw, x, y + b, FLAME)


def layer_sheet(frame_function):
    rows = [[frame_function(pose(anim, i)) for i in range(count)] for anim, count in ANIMATIONS]
    return build_sheet(W, H, rows)


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
    for kind in ("leather", "chain", "scale", "ash"):
        layer_sheet(lambda p, k=kind: armor_frame(k, p)).save(textures / f"char_armor_{kind}.png")
        # Halb verbrauchte Fassung. characters.py benutzt bewusst KEINEN Zufall – deshalb
        # verschieben diese zusätzlichen Blätter den gemeinsamen Zufallsstrom nicht, und alle
        # später erzeugten Texturen bleiben Byte für Byte gleich.
        layer_sheet(lambda p, k=kind: armor_frame(k, p, worn=True)).save(textures / f"char_armor_{kind}_worn.png")


__all__ = ["generate", "OUTLINE"]
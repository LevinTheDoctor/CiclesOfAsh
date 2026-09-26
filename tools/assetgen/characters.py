"""
Spielerfigur als EBENEN (24x32 pro Frame), damit der Charakter-Editor sie frei kombinieren kann:
  body     - Haut (Graustufen -> im Spiel mit Hautton eingefärbt)
  under_*  - Unterwäsche (feste Farben). Die EINZIGE unzerstörbare Kleidungsebene
  makeup_* - Gesichtsbemalung (Graustufen -> Make-up-Farbe)
  hair_*   - Frisuren (Graustufen -> Haarfarbe)
  garment_<art>[_worn|_broken] - Kleidung und Rüstung in DREI Verfallsstufen, feste Farben
  gear_<klasse>   - Waffe und die Faust, die sie hält (feste Farben). Unzerstörbar
  accent_<klasse> - Klassenzeichen (Graustufen -> Wappenfarbe). Nichts Textiles
Zeichenreihenfolge im Spiel: wings, body, under, makeup, hair, garment, gear, accent.
Zeilen: 0 idle, 1 run, 2 jump, 3 hurt (je bis zu 4 Frames).

Der Verfall (Vorbild Ghosts 'n Goblins): Zerstörbar ist genau EINE Ebene, die Kleidung. Sie deckt
Rumpf UND Beine, sonst blieben unzerstörbare Hosen übrig. Darunter liegt die Unterwäsche, darüber
Waffe und Klassenzeichen - deshalb steht die Figur am Ende in Unterhose da, hält aber ihre Klinge.

Waffenhaltung: Alles, was mit der Waffe zu tun hat, hängt an EINEM gemeinsamen Anker
(`hand_anchor`). Auch die sechs Körper laufen mit dem Unterarm auf diese Zelle zu - nur deshalb
passt eine einzige Ausrüstungs-Ebene auf jeden Körpertyp. Vorher endete der Arm je Typ woanders und
die Klinge stand daneben in der Luft.

16-Bit-Sprache (Auftrag G12/G13): vier bis sechs Tonwerte je Material statt drei,
Lichtquelle oben links, Materialkontrast (Leder matt, Kette glänzend, Bronze warm, Stein stumpf).
Graustufen-Ebenen bekommen zusaetzliche Zwischentoene (TINT_TOP), damit die Tonwertabstufung
der Farbtönung mehr Spielraum gibt.

FARBREGEL: Nur Ebenen, die der Code einfärbt (Haut, Haare, Make-up, Flügel, Akzent), sind
Graustufen. Unterwäsche, Kleidung und Ausrüstung werden ungetönt gezeichnet und brauchen eigene
Farben - Graustufen blieben dort grau.
"""
from PIL import Image, ImageDraw

from .core import (ASH, BLACK, BONE, DARK_GOLD, DARK_STEEL, DARK_STONE, DARK_WOOD, DEEP_PURPLE, EMBER, FLAME, GOLD,
                   LEATHER, MANA, OUTLINE, PURPLE, SHADOW, SOUL, STEEL, WHITE,
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


def hand_anchor(p):
    """
    Zelle (x, y) der VORDEREN Faust, 2x2 gross: x..x+1, y..y+1.

    Die einzige Wahrheit fuer Unterarm, Faust, Aermel und Waffe. Bewusst OHNE Sonderfall fuer den
    Sprung: Der Koerper hebt den Arm dort nicht, ein angehobener Anker wuerde die Waffe von der
    Hand loesen. Der Schwung kommt stattdessen aus der Neigung der Waffe (siehe weapon_shaft).
    """
    return 19, 21 + p["bob"] - p["arm"]


def front_arm(draw, p, upper_x, upper_width, mid, top):
    """
    Vorderer Arm. Der OBERARM behaelt die Breite des Koerpertyps - dort liest sich die Statur, und
    die V-Form des Athleten lebt in y 14-19. Der UNTERARM laeuft bei allen sechs Typen auf dieselbe
    Faustzelle zu, damit eine einzige Ausruestungs-Ebene bei jedem Koerper in der Hand sitzt.
    """
    hx, hy = hand_anchor(p)
    shift = p["bob"] - p["arm"]
    rect(draw, upper_x, 14 + shift, upper_width, 6, mid)          # Oberarm y 14-19
    rect(draw, upper_x + upper_width - 1, 14 + shift, 1, 6, top)  # Lichtkante aussen
    rect(draw, hx, hy - 2, 2, 4, mid)                             # Unterarm bis in die Hand
    rect(draw, hx + 1, hy - 2, 1, 4, top)
    if upper_x + upper_width - 1 < hx:                            # Ellenbogen-Bruecke schmaler Typen
        rect(draw, upper_x, 19 + shift, hx - upper_x, 1, mid)


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
    # Mund mittig zwischen den Augen. Vorher lag er als EIN Pixel bei (8, 11), also in der linken
    # unteren Ecke des Kopfes und auf der ersten Halszeile - praktisch unsichtbar, und der
    # Lippenstift hatte gar keine Stelle, auf die er passen konnte.
    rect(draw, 9, 10 + b, 2, 1, TINT_DARK)


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
    # average normal. Nur der OBERARM trägt diesen Unterschied — der Unterarm läuft bei allen
    # sechs auf die gemeinsame Faustzelle zu (front_arm / hand_anchor), sonst haelt jeder
    # Koerpertyp die Waffe an einer anderen Stelle und keine Ausruestungs-Ebene passt.
    arm = p["arm"]
    if athletic:
        # Hinterer Arm ganz aussen (x 2-3). Zusammen mit der auf 8 px verschmaelerten Taille
        # bleiben je 4 px Luft; polish() legt beidseitig 1 px Kontur an, sichtbar bleiben 2 px.
        # Vorher waren es 2 px Luft - die Kontur schloss die Luecke komplett und die V-Form war weg.
        rect(draw, 2, 14 + b + arm, 2, 7, TINT_MID)          # hinterer Arm abstehend
        rect(draw, 2, 14 + b + arm, 1, 7, TINT_TOP)
        pixel(draw, 2, 21 + b + arm, TINT_MID)               # hintere Hand
        front_arm(draw, p, 20, 2, TINT_LIGHT, TINT_TOP)      # Oberarm abstehend (x 20-21)
    elif heavy:
        rect(draw, 2, 14 + b + arm, 3, 9, TINT_MID)          # Arme eng am wuchtigen Leib
        rect(draw, 2, 14 + b + arm, 1, 9, TINT_TOP)
        pixel(draw, 2, 23 + b + arm, TINT_MID)
        front_arm(draw, p, 19, 3, TINT_LIGHT, TINT_TOP)      # wuchtiger Oberarm (x 19-21)
    else:
        rect(draw, 5, 14 + b + arm, 2, 8, TINT_MID)
        rect(draw, 5, 14 + b + arm, 1, 8, TINT_TOP)
        pixel(draw, 5, 22 + b + arm, TINT_MID)               # Hand hinten
        front_arm(draw, p, 17, 2, TINT_LIGHT, TINT_TOP)      # schmaler Oberarm (x 17-18)
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


# ------------------------------------------------------------------ Ausruestung: Waffe + Faust
# Feste Farben (der Code zeichnet die Ebene ungetoent). UNZERSTOERBAR und darum getrennt von der
# Kleidung: Wenn das letzte Kleidungsstueck zerfaellt, soll die Figur nackt sein, nicht wehrlos.
def weapon_shaft(draw, x, y_start, height, width, mid, light, lean=0, up=True):
    """
    Klinge oder Schaft, Zeile fuer Zeile von der Faust weg gezeichnet (up=False zeichnet nach
    unten, fuer den Rueckhandgriff). `lean` neigt das freie Ende um bis zu `lean` Pixel: Dadurch
    schwingt die Waffe mit Lauf und Sprung mit, statt starr neben dem Arm zu stehen.
    """
    for row in range(height):
        tilt = round(lean * (row + 1) / height)
        y = y_start - row if up else y_start + row
        rect(draw, x + tilt, y, width, 1, mid)
        pixel(draw, x + tilt, y, light)


def fist(draw, p, mid, light, dark):
    """
    Geschlossene Faust auf dem gemeinsamen Anker (x 18-20, y 21-22). Wird ZULETZT gezeichnet, also
    ueber den Waffengriff - erst dadurch umfasst die Hand die Waffe sichtbar. Vorher lag die Klinge
    zwei Pixel neben der Hand und schwang nicht einmal mit dem Arm mit.
    """
    hx, hy = hand_anchor(p)
    rect(draw, hx - 1, hy, 3, 2, mid)
    rect(draw, hx - 1, hy, 3, 1, light)
    pixel(draw, hx + 1, hy + 1, dark)                        # Fingerkante unten


def weapon_lean(p):
    """Neigung der Waffe: im Sprung nach hinten gerissen, im Lauf gegen den Armschwung."""
    return 1 if p["jump"] else -p["arm"]


def gear_frame(cls, p):
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    hx, hy = hand_anchor(p)
    lean, frame = weapon_lean(p), p["frame"]
    if cls == "warrior":
        steel_l, steel_m, steel_d = (200, 200, 218, 255), DARK_STEEL, (55, 55, 68, 255)
        rect(draw, hx, hy - 2, 2, 4, LEATHER)                # Griff, steckt IN der Faust
        weapon_shaft(draw, hx, hy - 3, 13, 2, STEEL, (235, 235, 246, 255), lean)
        rect(draw, hx - 1, hy - 2, 4, 1, GOLD)               # Parierstange direkt ueber der Faust
        pixel(draw, hx, hy + 2, GOLD)                        # Knauf direkt unter der Faust
        fist(draw, p, steel_m, steel_l, steel_d)             # Panzerhandschuh
        pixel(draw, hx - 1, hy + 1, LEATHER)                 # Daumen ueber dem Griff
    elif cls == "mage":
        wrap, wrap_l = (86, 56, 118, 255), (126, 96, 158, 255)
        # Der Schaft laeuft DURCH die Faust: unten bis kurz ueber den Boden, oben zum Kristall.
        weapon_shaft(draw, hx, hy + 7, 8, 2, WOOD, (120, 82, 58, 255), 0, up=False)
        weapon_shaft(draw, hx, hy - 1, 17, 2, WOOD, (120, 82, 58, 255), lean)
        fist(draw, p, wrap, wrap_l, (52, 32, 74, 255))       # umwickelte Hand
        pixel(draw, hx - 1, hy + 1, wrap_l)                  # Daumen ueber dem Schaft
        tip_x, tip_y = hx - 1 + lean, hy - 21
        rect(draw, tip_x, tip_y, 4, 4, SOUL if frame % 2 == 0 else MANA)
        pixel(draw, tip_x + 1, tip_y - 1, (220, 255, 240, 255))
        pixel(draw, tip_x + 2, tip_y + 1, (240, 255, 250, 255))
    elif cls == "shadow":
        wrap = (40, 32, 52, 255)
        rect(draw, hx, hy - 1, 2, 3, DARK_STEEL)             # Griff in der Faust
        fist(draw, p, wrap, (62, 52, 78, 255), BLACK)
        rect(draw, hx - 1, hy + 2, 4, 1, DARK_STEEL)         # Parierstange UNTER der Faust
        # Rueckhandgriff: die Klinge zeigt nach unten aus der Faust heraus.
        weapon_shaft(draw, hx, hy + 3, 6, 2, STEEL, (210, 210, 225, 255), -lean, up=False)
        pixel(draw, hx - 1, hy + 1, (62, 52, 78, 255))       # Daumen
    else:  # angel: waffenlos, aber die Hand bleibt gewickelt und ein Band flattert mit
        band, band_l = (206, 198, 180, 255), (238, 233, 220, 255)
        fist(draw, p, band, band_l, (150, 144, 132, 255))
        for row in range(5):                                 # Band, das der Bewegung nachlaeuft
            pixel(draw, hx + 1 + round(lean * (row + 1) / 5), hy + 2 + row, band if row % 2 else band_l)
    return polish(image)


# ------------------------------------------------------------------ Klassenzeichen (Graustufen -> Wappenfarbe)
# Bewusst NICHTS Textiles: Umhang, Stola und Schal sind Stoff und gehoeren in die zerstoerbare
# Kleidung. Hier steht nur, was auch dann noch da ist, wenn alles andere zerfallen ist - sonst
# waere die im Editor gewaehlte Wappenfarbe nach dem letzten Treffer unsichtbar.
def accent_frame(cls, p):
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b, frame = p["bob"], p["frame"]
    hx, hy = hand_anchor(p)
    if cls == "warrior":
        # Schulterplatte, nicht Wappenrock: Stoff gehoert in die zerstoerbare Kleidung. Flach und
        # breit auf der Schulter (y 13-15), damit sie nicht als Latz auf der Brust liest.
        rect(draw, 4, 13 + b, 5, 3, TINT_MID)
        rect(draw, 4, 13 + b, 5, 1, TINT_TOP)                # Lichtkante oben
        rect(draw, 4, 13 + b, 1, 3, TINT_LIGHT)
        rect(draw, 4, 15 + b, 5, 1, TINT_DARK)               # Nietenkante unten
        pixel(draw, 6, 16 + b, TINT_DARK)                    # Riemen zur Achsel
    elif cls == "mage":
        # Leuchtring um den Stabkristall. Wandert mit derselben Neigung wie der Stab.
        tip_x, tip_y = hx - 2 + weapon_lean(p), hy - 22
        draw.ellipse([tip_x, tip_y, tip_x + 5, tip_y + 5], outline=TINT_MID)
        pixel(draw, tip_x + 2, tip_y, TINT_LIGHT)
        pixel(draw, tip_x + 3, tip_y + 5, TINT_DARK)
        pixel(draw, tip_x, tip_y + 2 + frame % 2, TINT_TOP)  # Funke kreist
    elif cls == "shadow":
        rect(draw, 9, 9 + b, 6, 4, TINT_DARK)                # Maske ueber dem Gesicht
        rect(draw, 9, 9 + b, 6, 1, TINT_MID)
        pixel(draw, 10, 11 + b, TINT_TOP)                    # Sehschlitze
        pixel(draw, 13, 11 + b, TINT_TOP)
        rect(draw, 9, 12 + b, 6, 1, TINT_DARK)
    else:  # angel: Heiligenschein ueber dem Kopf
        sway = [0, 1, 0, -1][frame] if p["run"] else 0
        draw.arc([6 + sway, 0 + b, 17 + sway, 6 + b], 180, 360, fill=TINT_LIGHT, width=1)
        pixel(draw, 6 + sway, 1 + b, TINT_TOP)
        pixel(draw, 17 + sway, 1 + b, TINT_TOP)
        pixel(draw, 11 + sway, 0 + b, TINT_TOP)
    return polish(image, outline=(50, 50, 56, 255), light=10, dark=-20, gradient=0)


# ------------------------------------------------------------------ Make-up (Graustufen, wird eingefärbt)
def makeup_frame(style, p):
    """
    Wenige Pixel im Gesicht, ausgerichtet an den ECHTEN Zuegen aus head_and_neck:
    Kopf x 7-14 / y 3-11, Brauen y 6, Augen (8, 7) und (11, 7), Wangen y 8-10, Mund x 9-10 / y 10.

    Die alten Koordinaten stammten noch von 16x24: Bei der Vergroesserung (Auftrag G12) sind Kopf
    und Zuege mitgewandert, diese Ebene nicht. Ergebnis war ein Lidschatten als Stirnband, ein
    Lidstrich als Monobraue, Lippenstift auf dem Kinn und Kriegsbemalung am Kopfrand.
    Alles bleibt INNERHALB des Kopfes - sonst haengt die Farbe in der Luft.
    """
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    if style == "liner":                       # Lidstrich: duenne Linie NEBEN den Augen
        # Bewusst auf der Aussenseite: Ein Strich ueber dem Augenpixel selbst (8, 7) / (11, 7)
        # uebermalt die Pupille, und die Figur schaut dann aus zwei roten Flecken.
        pixel(draw, 9, 7 + b, TINT_DARK)
        pixel(draw, 12, 7 + b, TINT_DARK)
        pixel(draw, 13, 6 + b, TINT_MID)        # kleiner Fluegel am aeusseren Lid
        pixel(draw, 7, 6 + b, TINT_MID)
    elif style == "shadow":                    # Lidschatten: zwei getrennte Lidflaechen
        rect(draw, 8, 6 + b, 2, 1, TINT_MID)
        rect(draw, 11, 6 + b, 2, 1, TINT_MID)
        pixel(draw, 8, 6 + b, TINT_TOP)
        pixel(draw, 12, 6 + b, TINT_DARK)
    elif style == "lips":                      # betonter Mund - genau auf dem Mundpixel
        rect(draw, 9, 10 + b, 2, 1, TINT_DARK)
        pixel(draw, 9, 9 + b, TINT_MID)         # angedeutete Oberlippe
    elif style == "war":                       # Kriegsbemalung: schraeg ueber beide Wangen
        for step in range(3):
            pixel(draw, 7 + step // 2, 8 + step + b, TINT_MID)
            pixel(draw, 13 - step // 2, 8 + step + b, TINT_MID)
        pixel(draw, 8, 10 + b, TINT_DARK)
        pixel(draw, 12, 10 + b, TINT_DARK)
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


# ------------------------------------------------------------------ Unterwaesche (feste Farben)
# Die EINZIGE unzerstoerbare Kleidungsebene und reiner Gag: Das Muster wird pro Lauf gewuerfelt und
# ist erst zu sehen, wenn alles andere zerfallen ist. Nur der Huefte (y 21-24, x 7-16) - diese
# Flaeche ist bei ALLEN sechs Koerpertypen gedeckt, auch beim schmalsten Athleten-Rumpf.
UNDERWEAR_PALETTES = {
    "plain":   ((238, 233, 220, 255), (206, 198, 180, 255), (166, 158, 142, 255), None),
    "hearts":  ((246, 208, 216, 255), (226, 168, 184, 255), (176, 118, 136, 255), (198, 40, 66, 255)),
    "stripes": ((240, 240, 246, 255), (208, 210, 226, 255), (160, 162, 182, 255), (70, 96, 186, 255)),
    "polka":   ((244, 232, 200, 255), (214, 198, 158, 255), (168, 152, 114, 255), (126, 84, 48, 255)),
    "flames":  ((72, 58, 62, 255), (48, 38, 44, 255), (30, 22, 28, 255), EMBER),
    "bones":   ((126, 122, 134, 255), (94, 90, 104, 255), (62, 58, 72, 255), BONE),
}


def underwear_frame(pattern, p):
    """Slip auf der Huefte. Folgt bewusst NICHT den Beinen: Die Huefte schwingt beim Laufen nicht
    mit, nur die Beine darunter."""
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b = p["bob"]
    light, mid, dark, motif = UNDERWEAR_PALETTES[pattern]
    rect(draw, 7, 21 + b, 10, 3, mid)                        # Bund und Sitz
    rect(draw, 7, 21 + b, 10, 1, light)                      # Bundlicht oben
    rect(draw, 7, 21 + b, 2, 3, light)                       # Lichtseite links
    rect(draw, 7, 24 + b, 9, 1, mid)                         # Schritt zwischen den Beinen
    rect(draw, 15, 22 + b, 2, 2, dark)                       # Schattenkante rechts
    if pattern == "hearts":
        for x in (9, 13):                                    # zwei Herzchen, je 3x2
            rect(draw, x, 22 + b, 3, 1, motif)
            pixel(draw, x + 1, 23 + b, motif)
    elif pattern == "stripes":
        for x in (8, 11, 14):
            rect(draw, x, 21 + b, 1, 4, motif)
    elif pattern == "polka":
        for x, y in ((9, 22), (12, 23), (15, 22), (10, 24)):
            pixel(draw, x, y + b, motif)
    elif pattern == "flames":
        for x in (9, 12, 15):                                # Fluemmchen schlagen nach oben
            pixel(draw, x, 23 + b, motif)
            pixel(draw, x, 22 + b, FLAME)
    elif pattern == "bones":
        rect(draw, 9, 22 + b, 6, 1, motif)                   # gekreuzte Knoechlein
        pixel(draw, 9, 23 + b, motif)
        pixel(draw, 14, 21 + b, motif)
        pixel(draw, 14, 23 + b, motif)
        pixel(draw, 9, 21 + b, motif)
    return polish(image, outline=None, light=8, dark=-14, gradient=0)


# ------------------------------------------------------------------ Kleidung und Ruestung, drei Stufen
def punch(image, x, y, width=1, height=1):
    """Schlägt ein LOCH in die Kleidung: Die Pixel werden durchsichtig, darunter kommen Körper und
    Unterwäsche zum Vorschein. Nicht über ImageDraw, denn das würde malen statt wegnehmen."""
    for oy in range(height):
        for ox in range(width):
            px, py = x + ox, y + oy
            if 0 <= px < image.width and 0 <= py < image.height:
                image.putpixel((px, py), (0, 0, 0, 0))


# Materialpaletten (licht, mitte, dunkel, tief). Bewusst FARBIG: Der Code zeichnet die
# Kleidungsebene ungetoent, Graustufen blieben grau — genau das war der G11-Fehler.
GARMENT_MATERIALS = {
    "crusader_garb":  ((150, 104, 68, 255), LEATHER, DARK_WOOD, (46, 28, 18, 255)),
    "soot_robe":      ((78, 48, 110, 255), DEEP_PURPLE, (36, 20, 56, 255), (24, 12, 38, 255)),
    "confessor_rags": ((62, 52, 78, 255), SHADOW, (22, 16, 32, 255), BLACK),
    "feather_shift":  ((238, 233, 220, 255), (206, 198, 180, 255), (170, 164, 152, 255), (140, 134, 122, 255)),
    "leather_jerkin": ((160, 112, 74, 255), LEATHER, DARK_WOOD, (46, 28, 18, 255)),
    "chainmail":      ((210, 210, 225, 255), STEEL, DARK_STEEL, (52, 52, 64, 255)),
    "scale_mail":     ((240, 210, 130, 255), GOLD, DARK_GOLD, (100, 70, 26, 255)),
    "ash_harness":    ((140, 134, 152, 255), (96, 92, 108, 255), DARK_STONE, BLACK),
}


def garment_torso(draw, p, x, width, light, mid, deep=None):
    """Geschlossene Fläche über dem Rumpf (y 13-22) mit Licht oben links. Hier DARF eine
    geschlossene Fläche entstehen — das ist der Sinn der Ebene."""
    b = p["bob"]
    rect(draw, x, 13 + b, width, 10, mid)
    rect(draw, x, 13 + b, width, 1, light)                   # Kragen / Lichtkante oben
    rect(draw, x, 13 + b, 2, 10, light)                      # Lichtseite links
    if deep:
        rect(draw, x + width - 1, 16 + b, 1, 6, deep)        # Schattenkante rechts


def garment_sleeve(draw, p, mid, light):
    """
    Ärmel über dem OBERARM. Er endet an der Ellenbeuge, nicht an der Hand: Ein Ärmel bis zur Faust
    würde die Lücke zwischen Rumpf und Arm schließen (polish() legt je Kante 1 px Kontur an) und die
    Figur zu einer Tonne machen. So bleibt der Unterarm frei, und man sieht die Waffe in der Hand
    liegen statt in einem Stoffschlauch.
    """
    hx, hy = hand_anchor(p)
    rect(draw, 17, hy - 7, 3, 4, mid)
    rect(draw, 19, hy - 7, 1, 4, light)


def garment_skirt(draw, p, light, mid, dark, length=5):
    """
    Rock ab der Hüfte, schwingt beim Laufen mit. Liegt IMMER über einem Beinzeug, nie statt eines:
    Sonst kämen beim Zerfallen nackte Beine unter einem halben Rock hervor, und ein Stück, das die
    Beine gar nicht deckt, ließe unzerstörbare Hosen übrig.
    """
    b = p["bob"]
    sway = [0, 1, 0, -1][p["frame"]] if p["run"] else 0
    rect(draw, 7 + sway, 23 + b, 10, length, mid)
    rect(draw, 7 + sway, 23 + b, 3, length, light)
    rect(draw, 13 + sway, 23 + length - 2 + b, 4, 2, dark)   # Faltenwurf
    rect(draw, 7 + sway, 23 + length - 1 + b, 10, 1, dark)   # Saum


def garment_weave(draw, p, x, width, light, dark, style):
    """
    Binnenzeichnung auf dem Rumpf. Ohne sie ist die Kleidung ein flacher Farbklotz — bei 24x32 ist
    genug Platz für Steppnähte, Falten oder Fransen, und erst dadurch unterscheiden sich die vier
    Klassenstücke auf den ersten Blick.
    """
    b = p["bob"]
    if style == "quilt":                                     # Steppnaht: waagerechte Polsterlinien
        for y in (15, 18, 20):
            rect(draw, x + 1, y + b, width - 2, 1, dark)
            rect(draw, x + 1, y + 1 + b, width - 2, 1, light)
    elif style == "folds":                                   # Faltenwurf: senkrechte Schattenrinnen
        for offset in (2, width // 2, width - 3):
            rect(draw, x + offset, 14 + b, 1, 8, dark)
            rect(draw, x + offset + 1, 14 + b, 1, 8, light)
    elif style == "patch":                                   # Flicken: unruhige Stofffetzen
        for fx, fy, fw, fh in ((1, 15, 3, 2), (width - 5, 17, 4, 2), (2, 19, 4, 2)):
            rect(draw, x + fx, fy + b, fw, fh, dark)
            rect(draw, x + fx, fy + b, fw, 1, light)
    else:                                                    # "weave": feines Leinengitter
        for y in range(15 + b, 22 + b, 2):
            for fx in range(x + 1, x + width - 1, 2):
                pixel(draw, fx + (y % 4 == 0), y, light if (fx + y) % 4 else dark)


def garment_frame(kind, p, stage=0):
    """
    Ein Kleidungsstück in der Verfallsstufe `stage` (0 heil, 1 angeschlagen, 2 zerfetzt).

    Deckt Rumpf UND Beine: Erst dadurch ist das Ausziehen bis auf die Unterwäsche vollständig — ein
    Stück, das nur den Rumpf deckt, ließe unzerstörbare Hosen übrig.

    Gezeichnet wird immer erst das heile Stück und dann der Schaden hineingeschlagen. So sind alle
    drei Stufen zwangsläufig deckungsgleich, und die Deckung nimmt von Stufe zu Stufe streng ab.
    """
    image = new_image(W, H)
    draw = ImageDraw.Draw(image)
    b, frame = p["bob"], p["frame"]
    light, mid, dark, deep = GARMENT_MATERIALS[kind]

    if kind == "crusader_garb":
        draw_legs(draw, p, DARK_STEEL, BLACK, (55, 55, 68, 255))   # Beinzeug aus Stahl
        garment_torso(draw, p, 7, 10, light, mid, deep)
        garment_weave(draw, p, 7, 10, light, dark, "quilt")  # gestepptes Wams
        rect(draw, 7, 21 + b, 10, 2, DARK_WOOD)              # breiter Guertel
        rect(draw, 11, 21 + b, 2, 2, GOLD)                   # Schnalle
        rect(draw, 7, 13 + b, 3, 1, deep)                    # Riemen ueber die Schultern
        rect(draw, 14, 13 + b, 3, 1, deep)
        # Kreuz des Ritterordens auf der Brust: gequiltete Erhebung mit Kern aus Gold.
        # (Neues zweites Muster neben der Steppnaht — liegt voll im Rumpf x 8-15, y 15-20.)
        rect(draw, 11, 15 + b, 2, 5, shift(LEATHER, 18))     # Kreuzbalken senkrecht
        rect(draw, 9, 17 + b, 6, 2, shift(LEATHER, 18))      # Kreuzbalken waagerecht
        rect(draw, 11, 15 + b, 1, 5, shift(LEATHER, 32))    # Kreuz-Licht oben links
        rect(draw, 9, 17 + b, 2, 1, shift(LEATHER, 32))
        pixel(draw, 11, 16 + b, GOLD)                          # Niete im Kreuzkern
        pixel(draw, 11, 15 + b, shift(GOLD, 30))              # Nieten-Glanz
        for x in (8, 13):                                     # Ziernieten am Guertel
            pixel(draw, x, 22 + b, GOLD)
            pixel(draw, x, 22 + b, shift(GOLD, 30))
        garment_sleeve(draw, p, mid, light)
    elif kind == "soot_robe":
        draw_legs(draw, p, dark, BLACK, deep)                # dunkle Hose unter der Robe
        garment_skirt(draw, p, light, mid, dark, 5)          # knielange Robe
        garment_torso(draw, p, 7, 10, light, mid, deep)
        garment_weave(draw, p, 7, 10, light, deep, "folds")  # schwerer Faltenwurf
        rect(draw, 6, 12 + b, 12, 2, PURPLE)                 # zurueckgeschlagene Kapuze
        rect(draw, 6, 12 + b, 5, 1, light)
        rect(draw, 7, 21 + b, 10, 1, LEATHER)                # Kordel als Guertel
        # Runensaum am Kragen: drei Zeichen, jeder Stich mit hellem Kern (Aschemagier-Stickerei).
        # (Zweites Muster; Kragenzone y 14, x 8-14 liegt voll im Rumpf.)
        for i, rx in enumerate((8, 11, 14)):
            pixel(draw, rx, 14 + b, (196, 154, 240, 255))     # Stich
            pixel(draw, rx, 14 + b, (232, 205, 255, 255) if i == 1 else (196, 154, 240, 255))
            pixel(draw, rx + 1, 15 + b, (196, 154, 240, 255))  # Stich-Schatten darunter
        pixel(draw, 9, 14 + b, (232, 205, 255, 255))          # Glanz im mittleren Zeichen
        # Aschefleck auf dem Stoff: der Magier arbeitet am Herd des Tempels.
        pixel(draw, 14, 19 + b, (150, 140, 158, 255))
        pixel(draw, 15, 19 + b, (120, 112, 128, 255))
        pixel(draw, 14, 20 + b, (120, 112, 128, 255))
        garment_sleeve(draw, p, PURPLE, light)
    elif kind == "confessor_rags":
        draw_legs(draw, p, mid, BLACK, dark)
        garment_torso(draw, p, 7, 10, light, mid, deep)
        garment_weave(draw, p, 7, 10, light, deep, "patch")  # zusammengestueckelter Stoff
        rect(draw, 6, 3 + b, 12, 4, BLACK)                   # Kapuze ueber dem Kopf
        rect(draw, 6, 3 + b, 3, 10, BLACK)
        rect(draw, 17, 4 + b, 2, 6, BLACK)
        rect(draw, 7, 21 + b, 10, 2, dark)                   # Huefttuch
        rect(draw, 7, 21 + b, 4, 1, light)
        for x in (8, 12, 15):                                # ausgefranster Saum, von Anfang an
            pixel(draw, x, 23 + b, dark)
        # Grosse Flicken-Naht diagonal ueber die Brust: der Stoff ist geflickt und haelt nicht mehr.
        # (Zweites Muster; Diagonale x 9-14, y 15-19 voll im Rumpf, Stichloecher statt Kontur.)
        for i in range(5):
            sx, sy = 9 + i, 15 + i
            pixel(draw, sx, sy + b, shift(SHADOW, 28))        # Stich oben links
            pixel(draw, sx, sy + 1 + b, shift(SHADOW, -14))  # Stichschatten
        for x in (9, 13):                                     # Stopfnadel-Kreuze
            pixel(draw, x, 17 + b, shift(SHADOW, 28))
        pixel(draw, 11, 19 + b, shift(SHADOW, -14))           # Knopfloch, dunkel
        pixel(draw, 11, 18 + b, light)                        # Knopf, mit Licht
        garment_sleeve(draw, p, mid, light)
    elif kind == "feather_shift":
        draw_legs(draw, p, dark, BLACK, deep)                # Leinenhose unter dem Hemd
        garment_skirt(draw, p, light, mid, dark, 4)
        garment_torso(draw, p, 7, 10, light, mid, deep)
        garment_weave(draw, p, 7, 10, light, dark, "weave")  # feines Leinen
        rect(draw, 8, 13 + b, 8, 1, light)                   # heller Kragen
        rect(draw, 7, 21 + b, 10, 1, LEATHER)                # schlichter Guertel
        pixel(draw, 12, 21 + b, GOLD)
        for x in (8, 11, 14):                                # Daunenkante am Saum
            pixel(draw, x, 26 + b, light)
        # Verlorene Daune auf der Schulter: das Engelskleid verliert im Spiel Federn.
        # (Zweites Motiv; ein- bis zwei Pixel im Rumpf, je Frame an anderer Stelle.)
        for fx, fy in ((8, 14), (15, 16), (9, 18)):
            pixel(draw, fx, fy + b, WHITE)
            pixel(draw, fx + 1, fy + b, (245, 240, 230, 255))
        pixel(draw, 13, 19 + b, (222, 214, 196, 255))         # feine Stickerei unter der Brust
        pixel(draw, 14, 19 + b, (238, 233, 220, 255))
        garment_sleeve(draw, p, mid, light)
    elif kind == "leather_jerkin":
        draw_legs(draw, p, dark, BLACK, deep)                # Lederhose
        garment_torso(draw, p, 7, 10, light, mid, deep)
        for y in range(14 + b, 21 + b):                      # Kreuzschnuerung vorn
            pixel(draw, 11 if (y - b) % 2 else 12, y, dark)
            pixel(draw, 12 if (y - b) % 2 else 11, y, light)
        rect(draw, 7, 22 + b, 10, 1, dark)                   # Saum
        pixel(draw, 7, 17 + b, deep)                         # Seitennaehte
        pixel(draw, 16, 19 + b, deep)
        garment_sleeve(draw, p, mid, light)
    elif kind == "chainmail":
        draw_legs(draw, p, dark, BLACK, deep)                # Beinlinge aus Kette
        garment_torso(draw, p, 7, 10, light, mid, deep)
        for y in range(13 + b, 23 + b):                      # versetztes Maschenmuster
            for x in range(7, 17):
                pixel(draw, x, y, dark if (x + y) % 2 == 0 else mid)
        rect(draw, 7, 13 + b, 10, 1, light)                  # Lichtkante oben
        rect(draw, 7, 13 + b, 1, 10, (185, 185, 202, 255))
        rect(draw, 6, 13 + b, 3, 3, dark)                    # kurze Aermel
        rect(draw, 15, 13 + b, 3, 3, dark)
        rect(draw, 7, 23 + b, 10, 2, dark)                   # Kettenschurz ueber der Huefte
        rect(draw, 7, 23 + b, 10, 1, mid)
        pixel(draw, 9, 15 + b, (235, 235, 246, 255))         # Glanzpunkt oben links
        garment_sleeve(draw, p, mid, light)
    elif kind == "scale_mail":
        draw_legs(draw, p, DARK_WOOD, BLACK, (46, 28, 18, 255))    # Lederbeinzeug
        garment_torso(draw, p, 7, 10, mid, dark, deep)
        for y in range(13 + b, 22 + b):                      # ueberlappende Schuppenreihen
            for x in range(7 + ((y - b) % 3), 17, 3):
                pixel(draw, x, y, mid)
                if y > 13 + b:
                    pixel(draw, x, y - 1, light)
        rect(draw, 6, 13 + b, 3, 3, mid)                     # Schulterstuecke
        rect(draw, 15, 13 + b, 3, 3, mid)
        rect(draw, 6, 13 + b, 3, 1, light)
        rect(draw, 7, 23 + b, 10, 3, dark)                   # Schuppenschurz
        for x in range(7, 17, 3):
            pixel(draw, x, 24 + b, mid)
        rect(draw, 7, 22 + b, 10, 1, DARK_WOOD)              # Lederkante
        garment_sleeve(draw, p, dark, mid)
    else:  # ash_harness: schwerer Harnisch mit Glutadern — Stein, stumpf und rau
        draw_legs(draw, p, dark, BLACK, deep)                # Plattenbeine
        garment_torso(draw, p, 6, 12, light, mid, deep)
        rect(draw, 6, 22 + b, 12, 1, BLACK)                  # schwerer Saum
        for x, y in ((9, 15), (10, 16), (9, 17), (14, 16), (14, 18), (13, 19)):
            pixel(draw, x, y + b, EMBER)                     # Glutadern durch die Platte
        for x, y in ((10, 16), (14, 16), (13, 18)):
            pixel(draw, x, y + b, FLAME)                     # hellere Kerne
        rect(draw, 5, 13 + b, 2, 4, mid)                     # breite Schulterklappen
        rect(draw, 17, 13 + b, 2, 4, mid)
        rect(draw, 5, 13 + b, 2, 1, light)
        rect(draw, 6, 23 + b, 12, 2, mid)                    # Plattenschurz
        rect(draw, 6, 23 + b, 12, 1, light)
        garment_sleeve(draw, p, mid, light)

    polished = polish(image, outline=OUTLINE, light=10, dark=-20, gradient=0)
    # Der Schaden kommt NACH polish(): Die Kontur-Schleife dort faerbt jedes durchsichtige Pixel
    # ein, das an ein gedecktes grenzt — sie wuerde also jedes Loch bis 2 px Breite komplett wieder
    # zumalen. Genau daran ist die alte .worn-Fassung gescheitert: Sie unterschied sich kaum vom
    # heilen Bild. (Dieselbe 4-px-Regel wie bei den Koerper-Silhouetten, siehe GLM_TASKS.md.)
    for step in range(1, min(stage, 2) + 1):                 # Stufen bauen aufeinander auf
        garment_damage(polished, ImageDraw.Draw(polished), kind, p, step)
    return polished


# Wunden je Material und Stufe: Löcher als (x, y, breite, hoehe), y noch ohne p["bob"].
# Stufe 1 reißt auf, Stufe 2 nimmt ganze Partien und das Beinzeug mit. Die Stufen sind kumulativ —
# die Deckung nimmt dadurch zwangsläufig streng ab, und genau das ist das Abnahmekriterium.
GARMENT_WOUNDS = {
    "crusader_garb": {
        1: [(11, 15, 2, 4), (8, 19, 2, 1), (14, 16, 1, 3)],
        2: [(6, 13, 4, 3), (13, 17, 4, 5), (7, 20, 3, 2), (7, 26, 3, 5), (13, 25, 3, 6)],
    },
    "soot_robe": {
        1: [(12, 14, 3, 3), (7, 18, 2, 2), (16, 20, 1, 3)],
        2: [(6, 12, 5, 3), (13, 16, 4, 6), (7, 24, 4, 5), (14, 26, 4, 4), (10, 27, 3, 3)],
    },
    "confessor_rags": {
        1: [(10, 16, 3, 2), (15, 13, 2, 4), (7, 22, 3, 2)],
        2: [(6, 3, 4, 4), (12, 17, 5, 5), (7, 13, 3, 3), (7, 25, 3, 6), (13, 24, 3, 7)],
    },
    "feather_shift": {
        1: [(12, 15, 3, 3), (7, 19, 2, 3), (8, 13, 3, 1)],
        2: [(6, 13, 5, 4), (13, 17, 4, 6), (6, 25, 5, 4), (13, 27, 5, 2), (9, 21, 4, 2)],
    },
    "leather_jerkin": {
        1: [(11, 15, 2, 5), (8, 17, 1, 3), (15, 20, 2, 2)],
        2: [(6, 13, 4, 4), (13, 16, 4, 6), (7, 21, 4, 2), (7, 26, 3, 5), (13, 27, 3, 4)],
    },
    "chainmail": {
        1: [(6, 13, 3, 3), (11, 17, 1, 4), (15, 20, 2, 2)],
        2: [(7, 14, 3, 5), (13, 15, 5, 6), (6, 23, 5, 2), (7, 27, 3, 4), (13, 26, 3, 5)],
    },
    "scale_mail": {
        1: [(8, 15, 2, 2), (13, 14, 2, 2), (10, 19, 3, 1), (15, 17, 1, 2)],
        2: [(6, 13, 4, 4), (12, 16, 5, 6), (6, 23, 6, 3), (7, 26, 3, 5), (14, 27, 2, 4)],
    },
    "ash_harness": {
        1: [(6, 13, 2, 3), (16, 18, 2, 3), (11, 14, 2, 1)],
        2: [(5, 13, 5, 5), (15, 15, 4, 7), (5, 23, 6, 2), (7, 26, 3, 5), (13, 25, 3, 6)],
    },
}


def garment_damage(image, draw, kind, p, stage):
    """Der Schaden EINER Stufe: Löcher heraus, dann die Bruchkante hinein. Je Material das, was dort
    zuerst nachgibt — geplatzte Nähte beim Leder, aufgerissene Maschen bei der Kette, fehlende
    Schuppen bei der Bronze, abgesprengte Platten beim Harnisch."""
    b = p["bob"]
    light, mid, dark, deep = GARMENT_MATERIALS[kind]
    for x, y, width, height in GARMENT_WOUNDS[kind][stage]:
        punch(image, x, y + b, width, height)

    if kind in ("crusader_garb", "leather_jerkin"):
        rect(draw, 7, 20 + b, 3, 1, deep)                    # aufgescheuertes Leder
        if stage == 2:
            for x in (8, 11, 15):                            # Saum ausgefranst
                punch(image, x, 22 + b, 1, 1)
    elif kind in ("soot_robe", "feather_shift", "confessor_rags"):
        for x in range(7, 17, 3):                            # Stoff franst an der Bruchkante aus
            pixel(draw, x, (19 if stage == 1 else 21) + b, dark)
        if stage == 2:
            for x in (7, 10, 14, 17):
                punch(image, x, 27 + b, 1, 2)
    elif kind == "chainmail":
        rect(draw, 9, 16 + b, 1, 2, deep)                    # dunkle Bruchkante
        if stage == 2:
            for x in range(7, 17, 2):                        # aufgetrennte Reihe
                punch(image, x, 22 + b, 1, 1)
    elif kind == "scale_mail":
        rect(draw, 7, 16 + b, 1, 4, deep)                    # freigelegter Bronzegrund
        if stage == 2:
            for x in range(8, 17, 3):
                punch(image, x, 21 + b, 1, 2)
    else:  # ash_harness: die Glut darunter liegt offen
        veins = ((9, 16), (10, 17), (13, 17)) if stage == 1 else ((9, 16), (12, 20), (14, 19), (10, 24))
        for x, y in veins:
            pixel(draw, x, y + b, EMBER)
        for x, y in veins[:2]:
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
    for pattern in UNDERWEAR_PALETTES:
        layer_sheet(lambda p, m=pattern: underwear_frame(m, p)).save(textures / f"char_under_{pattern}.png")
    for cls in ("warrior", "mage", "shadow", "angel"):
        layer_sheet(lambda p, c=cls: gear_frame(c, p)).save(textures / f"char_gear_{cls}.png")
        layer_sheet(lambda p, c=cls: accent_frame(c, p)).save(textures / f"char_accent_{cls}.png")
    # Drei Verfallsstufen je Kleidungsstueck. characters.py benutzt bewusst KEINEN Zufall – deshalb
    # verschieben diese zusätzlichen Blätter den gemeinsamen Zufallsstrom nicht, und alle
    # später erzeugten Texturen bleiben Byte für Byte gleich.
    for kind in GARMENT_MATERIALS:
        for stage, suffix in enumerate(("", "_worn", "_broken")):
            layer_sheet(lambda p, k=kind, t=stage: garment_frame(k, p, t)).save(
                textures / f"char_garment_{kind}{suffix}.png")


__all__ = ["generate", "OUTLINE"]
"""Gegner, Bosse, Mini-Boss, Fledermäuse und Begleitseelen – alle mit Outline und Schattierung."""
import math

from PIL import ImageDraw

from .core import (ASH, BLACK, BLOOD, BONE, DARK_BLOOD, DARK_GOLD, DARK_STEEL, EMBER, FLAME, GOLD, LEATHER, MANA, PALE,
                   SHADOW, SOUL, STEEL, VIOLET, WHITE, WOOD, build_sheet, new_image, pixel, polish, rect, shift)


# ------------------------------------------------------------------ Standardgegner (16x16 / 16x24)
def ghoul(anim, frame):
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    step = frame % 2
    rect(draw, 3, 6, 8, 6, PALE)                          # gekrümmter Rücken
    for x in (4, 6, 8):
        rect(draw, x, 7, 1, 3, shift(PALE, -40))          # Rippen
    rect(draw, 9, 3, 5, 5, PALE)                          # vorgestreckter Kopf
    rect(draw, 12, 6, 2, 1, BLACK)                        # Maul
    pixel(draw, 12, 4, BLOOD)
    pixel(draw, 13, 4, EMBER)
    rect(draw, 11, 8, 2, 4, PALE)                         # Arme
    pixel(draw, 13, 11, BONE)                             # Krallen
    pixel(draw, 13, 12, BONE)
    rect(draw, 3, 12, 8, 1, DARK_BLOOD)                   # Lendentuch
    if anim == "run":
        rect(draw, 4 + step, 13, 2, 3, PALE)
        rect(draw, 8 - step, 13, 2, 3, PALE)
    else:
        rect(draw, 4, 13, 2, 3, PALE)
        rect(draw, 8, 13 - (frame == 2), 2, 3, PALE)
    return polish(image)


def hound(anim, frame):
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    rect(draw, 2, 6, 10, 5, DARK_BLOOD)                   # Leib
    for x in (3, 5, 7, 9):
        pixel(draw, x, 5, BLACK)                          # Rückenstacheln
    rect(draw, 11, 4, 4, 5, DARK_BLOOD)                   # Kopf
    rect(draw, 14, 7, 2, 1, BONE)                         # Fänge
    pixel(draw, 13, 5, FLAME)
    rect(draw, 0, 5, 2, 2, EMBER)                         # glühender Schwanz
    pixel(draw, 0, 4, FLAME)
    offsets = [(0, 2), (1, 1), (2, 0), (1, 1)][frame] if anim == "run" else (1, 1)
    rect(draw, 3 + offsets[0], 11, 1, 4, BLACK)
    rect(draw, 5 + offsets[1], 11, 1, 4, BLACK)
    rect(draw, 9 + offsets[1], 11, 1, 4, BLACK)
    rect(draw, 12 - offsets[0], 11, 1, 4, BLACK)
    return polish(image)


def wraith(frame):
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    body = (190, 200, 215, 210)
    rect(draw, 5, 1, 6, 7, body)
    rect(draw, 6, 3, 4, 3, BLACK)
    pixel(draw, 7, 4, SOUL)
    pixel(draw, 9, 4, SOUL)
    rect(draw, 3, 7, 10, 5, body)
    rect(draw, 2 + frame % 2, 8, 2, 1, body)              # ausgestreckte Geisterhände
    rect(draw, 12 - frame % 2, 8, 2, 1, body)
    for x in range(3, 13):                                # zerfetzter Saum
        rect(draw, x, 12, 1, 1 + (x + frame) % 3, body)
    return polish(image, outline=(30, 34, 48, 200))


def cultist(anim, frame):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    rect(draw, 4, 9, 8, 14, BLOOD)
    rect(draw, 7, 10, 2, 12, DARK_BLOOD)                  # Mittelnaht
    rect(draw, 4, 22, 8, 1, DARK_BLOOD)
    rect(draw, 5, 2, 6, 8, DARK_BLOOD)
    rect(draw, 7, 0, 2, 2, DARK_BLOOD)
    rect(draw, 6, 5, 4, 3, BLACK)
    pixel(draw, 7, 6, EMBER)
    pixel(draw, 9, 6, EMBER)
    rect(draw, 6, 12, 4, 1, GOLD)                         # Kultamulett
    glow = FLAME if frame % 2 else EMBER
    if anim == "cast":
        rect(draw, 2, 6 - frame % 2, 2, 5, BLOOD)
        rect(draw, 12, 6 - frame % 2, 2, 5, BLOOD)
        rect(draw, 1, 3 - frame % 2, 3, 3, glow)
        rect(draw, 12, 3 - frame % 2, 3, 3, glow)
    else:
        rect(draw, 3, 12, 2, 5, BLOOD)
        rect(draw, 11, 12, 2, 5, BLOOD)
        pixel(draw, 3, 17, glow)
        pixel(draw, 12, 17, glow)
    return polish(image)


def warden(anim, frame):
    """Kerkermeister (Mini-Boss, 32x32): Kapuze, Schlüsselring, Kettenflegel."""
    image = new_image(32, 32)
    draw = ImageDraw.Draw(image)
    b = 1 if frame in (1, 2) else 0
    step = frame % 2 if anim == "run" else 0
    rect(draw, 9 + step, 25, 5, 7, BLACK)                 # Beine
    rect(draw, 18 - step, 25, 5, 7, BLACK)
    rect(draw, 7, 11 + b, 18, 15, LEATHER)                # massiger Körper
    rect(draw, 9, 13 + b, 14, 11, shift(LEATHER, -25))
    rect(draw, 7, 20 + b, 18, 2, BLACK)                   # Gürtel
    for x in (9, 12, 15):
        rect(draw, x, 22 + b, 2, 2, GOLD)                 # Schlüsselbund
    rect(draw, 10, 2 + b, 12, 11, (60, 40, 40, 255))      # Henkerskapuze
    rect(draw, 13, 6 + b, 7, 3, BLACK)
    pixel(draw, 15, 7 + b, EMBER)
    pixel(draw, 18, 7 + b, EMBER)
    rect(draw, 3, 12 + b, 4, 10, LEATHER)                 # Arme
    rect(draw, 25, 12 + b, 4, 10, LEATHER)
    swing = 0 if anim != "cast" else [-6, -3, 0, 3][frame]
    for i in range(5):                                    # Kette mit Flegel
        rect(draw, 28, 18 + b + i * 2 + swing // 2, 1, 1, DARK_STEEL)
    rect(draw, 26, 27 + b + swing // 2, 5, 5, DARK_STEEL)
    pixel(draw, 26, 27 + b + swing // 2, STEEL)
    return polish(image)


def boss(kind, anim, frame):
    image = new_image(48, 48)
    draw = ImageDraw.Draw(image)
    breathe = 1 if frame in (1, 2) else 0
    casting = anim == "cast"
    if kind == "shepherd":
        draw.polygon([(24, 6 + breathe), (9, 47), (39, 47)], fill=ASH)
        draw.polygon([(24, 10 + breathe), (15, 47), (33, 47)], fill=(95, 90, 105, 255))
        for x in range(12, 38, 5):
            draw.line([(x, 40), (x + 2, 47)], fill=(80, 76, 90, 255))       # Faltenwurf
        draw.ellipse([17, 4 + breathe, 31, 18 + breathe], fill=ASH)
        draw.ellipse([19, 7 + breathe, 29, 16 + breathe], fill=BONE)
        rect(draw, 21, 10 + breathe, 2, 2, BLACK)
        rect(draw, 26, 10 + breathe, 2, 2, BLACK)
        draw.line([(22, 14 + breathe), (27, 14 + breathe)], fill=(150, 140, 120, 255))
        rect(draw, 40, 8, 2, 40, WOOD)
        draw.arc([34, 2, 46, 14], 180, 360, fill=WOOD, width=2)
        draw.ellipse([20, 20, 28, 24], outline=GOLD)                      # Heiligenschein-Rest
        if casting:
            draw.ellipse([2, 14 - frame, 12, 24 - frame], outline=SOUL, width=2)
    elif kind == "mammon":
        draw.ellipse([6, 14 + breathe, 42, 47], fill=DARK_GOLD)
        draw.ellipse([10, 18 + breathe, 38, 44], fill=GOLD)
        for x, y in ((14, 30), (30, 26), (22, 38), (34, 36)):
            rect(draw, x, y, 3, 3, (250, 220, 120, 255))                    # eingewachsene Münzen
        draw.ellipse([15, 4 + breathe, 33, 22 + breathe], fill=GOLD)
        rect(draw, 18, 12 + breathe, 3, 3, BLOOD)
        rect(draw, 27, 12 + breathe, 3, 3, BLOOD)
        draw.polygon([(15, 6), (18, 0), (21, 5), (24, 0), (27, 5), (30, 0), (33, 6)], fill=GOLD)
        for x in (18, 24, 30):
            pixel(draw, x, 1, BLOOD)                                        # Rubine
        rect(draw, 18, 18 + breathe, 12, 2, DARK_BLOOD)
        for x in range(19, 30, 2):
            pixel(draw, x, 18 + breathe, BONE)                              # Zähne
        if casting:
            for i in range(5):
                rect(draw, 3 + i * 9, (frame * 6 + i * 9) % 30, 3, 3, GOLD)
    else:
        rect(draw, 10, 16 + breathe, 28, 26, DARK_BLOOD)
        rect(draw, 14, 18 + breathe, 20, 20, BLOOD)
        for y in range(20, 36, 5):
            draw.line([(16, y + breathe), (32, y + 2 + breathe)], fill=(255, 120, 60, 255))  # glühende Risse
        rect(draw, 16, 4 + breathe, 16, 14, DARK_BLOOD)
        draw.polygon([(16, 6), (6, 0), (14, 11)], fill=BONE)
        draw.polygon([(32, 6), (42, 0), (34, 11)], fill=BONE)
        rect(draw, 19, 9 + breathe, 3, 2, FLAME)
        rect(draw, 27, 9 + breathe, 3, 2, FLAME)
        rect(draw, 20, 14 + breathe, 8, 2, BLACK)
        rect(draw, 12, 42, 8, 6, BLACK)
        rect(draw, 28, 42, 8, 6, BLACK)
        raise_arms = -6 if casting else 0
        rect(draw, 4, 18 + breathe + raise_arms, 6, 16, DARK_BLOOD)
        rect(draw, 38, 18 + breathe + raise_arms, 6, 16, DARK_BLOOD)
        if casting:
            draw.ellipse([2, 8 + raise_arms, 12, 18 + raise_arms], fill=EMBER)
            draw.ellipse([36, 8 + raise_arms, 46, 18 + raise_arms], fill=EMBER)
    return polish(image)


# ------------------------------------------------------------------ Neue Kreaturen (v2)
def skeleton(anim, frame):
    """Knochenwächter 16x18: zäher Walker mit Schild-Rest."""
    image = new_image(16, 18)
    draw = ImageDraw.Draw(image)
    step = frame % 2
    rect(draw, 4, 3, 8, 6, BONE)                           # Schädel
    rect(draw, 5, 5, 2, 2, BLACK)
    rect(draw, 9, 5, 2, 2, BLACK)
    pixel(draw, 6, 5, EMBER)
    pixel(draw, 10, 5, EMBER)
    rect(draw, 6, 8, 4, 1, BLACK)                           # Kiefer
    for x in (5, 7, 9):
        pixel(draw, x, 9, shift(BONE, -40))
    rect(draw, 4, 10, 8, 3, BONE)                           # Brustkorb
    rect(draw, 5, 11, 6, 1, shift(BONE, -50))
    rect(draw, 6, 13, 1, 5, BONE)                           # Beine
    rect(draw, 9, 13, 1, 5, BONE)
    if anim == "run":
        rect(draw, 4 + step * 2, 10, 2, 4, BONE)            # schwingende Arme
        rect(draw, 10 - step * 2, 10, 2, 4, BONE)
    else:
        rect(draw, 2, 10, 2, 6, DARK_STEEL)                 # Schild
        rect(draw, 3, 11, 1, 4, STEEL)
    return polish(image)


def spider(anim, frame):
    """Grabspinne 16x12: flacher Körper, zappelnde Beine."""
    image = new_image(16, 12)
    draw = ImageDraw.Draw(image)
    body = (52, 34, 40, 255)
    rect(draw, 5, 4, 6, 5, body)                            # Hinterleib
    rect(draw, 9, 5, 3, 3, shift(body, 25))                 # Kopf
    pixel(draw, 11, 6, BLOOD)
    pixel(draw, 12, 5, BLOOD)
    lift = frame % 2
    for i, dx in enumerate((-4, -2, 2, 4)):                 # 4 Beinpaare
        left_y = 7 - lift if i % 2 == 0 else 8 + lift
        rect(draw, 5 + dx, left_y, 2, 1, body)
        rect(draw, 4 + dx, left_y + 1, 1, 2, body)
        rect(draw, 9 + dx, left_y, 2, 1, body)
        rect(draw, 10 + dx, left_y + 1, 1, 2, body)
    pixel(draw, 7, 5, shift(body, 40))                      # Muster
    pixel(draw, 8, 6, shift(body, 40)
          )
    return polish(image)


def imp(anim, frame):
    """Feuerimp 16x16: kleiner Flieger mit Glühkrone."""
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    skin = (140, 52, 44, 255)
    bob = frame % 2
    rect(draw, 6, 5 + bob, 5, 6, skin)                      # Kopf + Leib
    rect(draw, 7, 7 + bob, 1, 1, BLACK)
    rect(draw, 9, 7 + bob, 1, 1, BLACK)
    pixel(draw, 7, 6 + bob, FLAME)
    pixel(draw, 9, 6 + bob, FLAME)
    for x, spike in ((6, -2), (8, -3), (10, -2)):           # Hörner
        rect(draw, x, 3 + bob + spike, 1, 2, DARK_GOLD)
    rect(draw, 4, 6 + bob, 2, 2, skin)                      # Flügel
    rect(draw, 11, 6 + bob, 2, 2, skin)
    rect(draw, 3, 5 + bob - (frame % 2), 1, 3, shift(skin, -30))
    rect(draw, 13, 5 + bob - (frame % 2), 1, 3, shift(skin, -30))
    rect(draw, 7, 11 + bob, 2, 2, EMBER)                    # Glutschweif
    pixel(draw, 7, 13 + bob, FLAME)
    return polish(image)


def swarm(anim, frame):
    """Seelensplitter 10x10: winziger hüpfender Splitter."""
    image = new_image(10, 10)
    draw = ImageDraw.Draw(image)
    core = (170, 190, 200, 230)
    dark = (100, 110, 130, 230)
    spread = frame % 3
    rect(draw, 4 - spread // 2, 2, 3 + spread, 5, core)     # unruhiger Umriss
    rect(draw, 3, 4 - spread // 2, 5, 2 + spread, dark)
    pixel(draw, 5, 4, WHITE)
    pixel(draw, 4 + frame % 3, 1, SOUL)
    return polish(image, outline=(20, 24, 34, 200))


def knight(anim, frame):
    """Verfluchter Ritter 16x24: rostige Rüstung, Schild, chargt."""
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    step = frame % 2
    rust = (110, 92, 70, 255)
    dark_rust = (70, 56, 44, 255)
    rect(draw, 4, 2, 8, 6, rust)                            # Helm
    rect(draw, 5, 4, 2, 2, BLACK)                           # Sehschlitz
    rect(draw, 9, 4, 2, 2, BLACK)
    pixel(draw, 6, 4, BLOOD)
    pixel(draw, 10, 4, BLOOD)
    rect(draw, 3, 8, 10, 8, rust)                           # Brustpanzer
    rect(draw, 4, 9, 8, 2, dark_rust)
    rect(draw, 7, 9, 2, 6, BLOOD)                           # Riss + Blut
    rect(draw, 2, 10, 2, 5, dark_rust)                      # Schultern
    rect(draw, 12, 10, 2, 5, dark_rust)
    rect(draw, 5, 16, 2, 6, dark_rust)                      # Beine
    rect(draw, 9, 16, 2, 6, dark_rust)
    if anim == "cast":
        # Telegraph: Schwert hochgehoben, glühende Runen
        rect(draw, 13, 2, 1, 9, STEEL)
        rect(draw, 12, 1, 3, 2, DARK_STEEL)
        pixel(draw, 13, 0, FLAME)
        rect(draw, 3, 9, 1, 6, FLAME)
    else:
        rect(draw, 13, 6 + step, 1, 8, STEEL)               # Schwert seitlich
        rect(draw, 0, 8, 3, 8, dark_rust)                   # Schild vor sich
        rect(draw, 1, 10, 1, 4, BLOOD)
    return polish(image)


def leech(anim, frame):
    """Sumpfblutegel 16x8: flach, getarnt, springt hoch."""
    image = new_image(16, 8)
    draw = ImageDraw.Draw(image)
    body = (70, 50, 66, 255)
    squish = frame % 2
    rect(draw, 2, 3 + squish, 12, 4 - squish, body)         # gedehnter Leib
    rect(draw, 13, 3, 2, 2, shift(body, 30))                # Saugkopf
    pixel(draw, 15, 4, BLOOD)
    rect(draw, 5, 2 + squish, 2, 1, shift(body, -30))      # Rückenstreifen
    rect(draw, 9, 2 + squish, 2, 1, shift(body, -30))
    rect(draw, 3, 6 + (squish == 0), 1, 1, DARK_BLOOD)
    return polish(image, outline=(18, 14, 22, 220))


def golem(anim, frame):
    """Aschgolem 24x24: kompakt, viel Risse, sehr zäh."""
    image = new_image(24, 24)
    draw = ImageDraw.Draw(image)
    stone = (96, 92, 108, 255)
    dark = (60, 56, 70, 255)
    step = frame % 2
    rect(draw, 5, 3, 14, 10, stone)                         # Kopf in den Schultern
    rect(draw, 7, 6, 3, 2, EMBER)
    rect(draw, 14, 6, 3, 2, EMBER)
    rect(draw, 9, 9, 6, 1, BLACK)
    rect(draw, 3, 12, 18, 9, stone)                          # Rumpf
    rect(draw, 4, 13, 16, 7, dark)
    rect(draw, 8, 13, 2, 7, BLACK)                          # Kernriss
    rect(draw, 8, 15, 2, 2, EMBER)
    rect(draw, 12, 17, 2, 2, EMBER)
    rect(draw, 15, 14, 1, 4, BLACK)
    rect(draw, 0, 13, 3, 7, dark)                           # Arme
    rect(draw, 21, 13, 3, 7, dark)
    rect(draw, 1, 20, 2, 2, stone)
    rect(draw, 21, 20, 2, 2, stone)
    rect(draw, 6, 21 + step, 4, 3, dark)                    # Beine
    rect(draw, 14, 21 + (1 - step), 4, 3, dark)
    return polish(image)


# ------------------------------------------------------------------ Drachen (G4)
def drake(anim, frame):
    """Drache 24x20: fliegender Angreifer, Flügel schlagen (idle = Flug)."""
    image = new_image(24, 20)
    draw = ImageDraw.Draw(image)
    flap = frame % 2
    scale = (96, 44, 48, 255)
    dark = (56, 22, 28, 255)
    belly = (150, 130, 100, 255)
    wing = (72, 30, 40, 255)
    # Flügel links/rechts, schlagen mit dem Frame
    for wy, wx in ((2, 2), (2, 14)):
        draw.polygon([(wx + 3, 8 + flap * 2), (wx - 2, 1 + flap * 3), (wx + 7, 5 + flap * 2)], fill=wing)
    rect(draw, 7, 4 + flap, 10, 7, scale)                      # Leib
    rect(draw, 8, 8 + flap, 8, 2, belly)                       # heller Bauch
    rect(draw, 15, 3 + flap, 6, 5, scale)                      # Kopf mit Schnauze
    rect(draw, 20, 5 + flap, 2, 1, dark)                       # Maul
    pixel(draw, 17, 4 + flap, FLAME)                           # Auge
    for x in (9, 12, 15):                                      # Rückenstacheln
        pixel(draw, x, 3 + flap, dark)
    draw.polygon([(5, 8 + flap), (1, 11 + flap), (5, 12 + flap)], fill=dark)   # Schwanz mit Spitze
    rect(draw, 8, 12, 2, 2, scale)                             # Beine angezogen
    rect(draw, 13, 12, 2, 2, scale)
    pixel(draw, 8, 14, DARK_GOLD)                              # Krallen
    pixel(draw, 14, 14, DARK_GOLD)
    if anim == "cast":
        rect(draw, 21, 7 + flap, 2, 2, FLAME)                  # Feuer speiend
        pixel(draw, 23, 8 + flap, EMBER)
    return polish(image)


def dragon_whelp(anim, frame):
    """Drachenjunges 16x16: kleiner Schwarm-Drache, hüpfend-flatternd."""
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    bob = frame % 2
    scale = (108, 62, 52, 255)
    dark = (64, 34, 30, 255)
    wing = (80, 46, 40, 255)
    draw.polygon([(5, 5 + bob), (0, 1 + bob * 2), (3, 6 + bob)], fill=wing)    # Flügel
    draw.polygon([(8, 5 + bob), (13, 1 + bob * 2), (10, 6 + bob)], fill=wing)
    rect(draw, 4, 4 + bob, 7, 6, scale)                        # Leib
    rect(draw, 9, 5 + bob, 5, 4, scale)                        # Kopf
    rect(draw, 13, 7 + bob, 2, 1, dark)                        # Maul
    pixel(draw, 11, 6 + bob, EMBER)                            # Auge
    pixel(draw, 6, 3 + bob, dark)                              # Stachel
    pixel(draw, 9, 3 + bob, dark)
    rect(draw, 5, 10 + bob, 2, 3, scale)                       # Beine
    rect(draw, 8, 10 + bob, 2, 3, scale)
    pixel(draw, 5, 13 + bob, DARK_GOLD)
    pixel(draw, 9, 13 + bob, DARK_GOLD)
    if anim == "run":
        rect(draw, 2, 9 + bob, 2, 1, dark)                      # wedelnder Schwanz
    return polish(image)


def companion_dragonling(frame):
    """Begleiter-Drache 12x12: kleines, treues Drachenjunges."""
    image = new_image(12, 12)
    draw = ImageDraw.Draw(image)
    bob = frame % 2
    scale = (128, 78, 60, 255)
    dark = (70, 40, 34, 255)
    wing = (90, 54, 46, 255)
    draw.polygon([(3, 4 + bob), (0, 1 + bob * 2), (2, 5 + bob)], fill=wing)
    draw.polygon([(6, 4 + bob), (9, 1 + bob * 2), (7, 5 + bob)], fill=wing)
    rect(draw, 2, 3 + bob, 6, 5, scale)                         # Leib
    rect(draw, 6, 4 + bob, 4, 3, scale)                         # Kopf
    pixel(draw, 9, 5 + bob, dark)                               # Maul
    pixel(draw, 8, 4 + bob, GOLD)                               # freundliches Auge
    pixel(draw, 3, 2 + bob, dark)                               # Stachelchen
    rect(draw, 3, 8 + bob, 2, 2, scale)                        # Beinchen
    rect(draw, 6, 8 + bob, 2, 2, scale)
    return polish(image, light=18, dark=-14, gradient=0)


# ------------------------------------------------------------------ NPCs (v2)
def npc_pilgrim(frame):
    """Betender Pilger 16x20: kniet, gefaltete Hände, warmes Licht."""
    image = new_image(16, 20)
    draw = ImageDraw.Draw(image)
    robe = (96, 70, 62, 255)
    robe_dark = (70, 50, 46, 255)
    sway = frame % 2
    rect(draw, 4, 12, 8, 8, robe)                          # kniende Robe
    rect(draw, 4, 18, 8, 1, robe_dark)
    rect(draw, 5, 4 + sway, 6, 8, robe)                    # Oberkörper leicht wiegend
    rect(draw, 6, 5 + sway, 4, 3, BONE)                    # Gesicht
    rect(draw, 7, 6 + sway, 1, 1, BLACK)
    rect(draw, 9, 6 + sway, 1, 1, BLACK)
    rect(draw, 7, 8 + sway, 3, 1, (60, 44, 40, 255))       # gefaltete Hände
    rect(draw, 10, 3 + sway, 4, 5, (60, 44, 40, 255))      # Kapuze
    return polish(image)


def npc_hermit(frame):
    """Eremit 16x20: hockt, dunkle Kapuze, bläuliches Licht."""
    image = new_image(16, 20)
    draw = ImageDraw.Draw(image)
    cloak = (54, 52, 70, 255)
    cloak_dark = (38, 36, 52, 255)
    sway = frame % 2
    rect(draw, 3, 10, 10, 10, cloak)                       # hockende Gestalt
    rect(draw, 3, 18, 10, 1, cloak_dark)
    rect(draw, 5, 4 + sway, 6, 7, cloak)                   # Kopf mit Kapuze
    rect(draw, 6, 6 + sway, 4, 3, BLACK)
    pixel(draw, 7, 7 + sway, (150, 220, 255, 255))         # leuchtende Augen
    pixel(draw, 9, 7 + sway, (150, 220, 255, 255))
    rect(draw, 1, 12 + sway, 2, 4, cloak_dark)             # Arm mit Stab
    rect(draw, 0, 4 + sway, 1, 12, WOOD)
    return polish(image)


def npc_keeper(frame):
    """Tempelwärtin 16x22: aufrecht, wallendes Gewand, kerzenwarm."""
    image = new_image(16, 22)
    draw = ImageDraw.Draw(image)
    gown = (120, 96, 76, 255)
    gown_dark = (88, 68, 54, 255)
    sway = frame % 2
    rect(draw, 4, 8, 8, 14, gown)                          # wallendes Gewand
    rect(draw, 4, 20, 8, 1, gown_dark)
    rect(draw, 5, 2 + sway, 6, 6, BONE)                    # Gesicht
    rect(draw, 6, 4 + sway, 1, 1, BLACK)
    rect(draw, 9, 4 + sway, 1, 1, BLACK)
    rect(draw, 4, 1 + sway, 8, 3, (60, 44, 40, 255))       # Haar/Schleier
    rect(draw, 2, 9 + sway, 2, 6, gown)                    # Arme
    rect(draw, 12, 9 + sway, 2, 6, gown)
    pixel(draw, 2, 15 + sway, FLAME)                       # Kerze
    pixel(draw, 13, 15 + sway, FLAME)
    return polish(image)


def bat(anim, frame):
    image = new_image(10, 8)
    draw = ImageDraw.Draw(image)
    body = (50, 40, 60, 255)
    if anim == "hang":
        rect(draw, 4, 1, 2, 5, body)                     # kopfüber, Flügel angelegt
        rect(draw, 3, 2, 1, 3, body)
        rect(draw, 6, 2, 1, 3, body)
        pixel(draw, 4, 5, BLOOD)
        pixel(draw, 5, 5, BLOOD)
    else:
        rect(draw, 4, 3, 2, 3, body)
        pixel(draw, 4, 3, BLOOD)
        pixel(draw, 5, 3, BLOOD)
        if frame == 0:
            draw.polygon([(4, 4), (0, 1), (1, 5)], fill=body)
            draw.polygon([(5, 4), (9, 1), (8, 5)], fill=body)
        else:
            draw.polygon([(4, 4), (0, 6), (2, 7)], fill=body)
            draw.polygon([(5, 4), (9, 6), (7, 7)], fill=body)
    return polish(image, light=20, dark=-10, gradient=0)


def wisp(core, glow, shape="flame"):
    frames = []
    for frame in range(4):
        image = new_image(12, 12)
        draw = ImageDraw.Draw(image)
        flicker = frame % 2
        draw.ellipse([1, 2 - flicker, 10, 11], fill=glow[:3] + (110,))
        draw.ellipse([3, 4 - flicker, 8, 9], fill=core)
        if shape == "chain":
            for i in range(3):
                pixel(draw, 2 + i * 3, 10, (120, 120, 130, 255))
        elif shape == "bell":
            rect(draw, 4, 1 - flicker + 1, 4, 1, GOLD)
        pixel(draw, 5, 1 - flicker + 1, core)
        pixel(draw, 4, 6, BLACK)
        pixel(draw, 7, 6, BLACK)
        frames.append(image)
    return build_sheet(12, 12, [frames])


def generate(textures):
    build_sheet(16, 16, [[ghoul("idle", i) for i in range(4)], [ghoul("run", i) for i in range(4)]]).save(textures / "enemy_ghoul.png")
    build_sheet(16, 16, [[hound("idle", i) for i in range(4)], [hound("run", i) for i in range(4)]]).save(textures / "enemy_hound.png")
    build_sheet(16, 16, [[wraith(i) for i in range(4)]]).save(textures / "enemy_wraith.png")
    build_sheet(16, 24, [[cultist("idle", i) for i in range(4)], [cultist("cast", i) for i in range(4)]]).save(textures / "enemy_cultist.png")
    # --- Neue Kreaturen (v2): mehr Gegnervielfalt je Kreis
    build_sheet(16, 18, [[skeleton("idle", i) for i in range(4)], [skeleton("run", i) for i in range(4)]]).save(textures / "enemy_skeleton.png")
    build_sheet(16, 12, [[spider("idle", i) for i in range(4)], [spider("run", i) for i in range(4)]]).save(textures / "enemy_spider.png")
    build_sheet(16, 16, [[imp("idle", i) for i in range(4)]]).save(textures / "enemy_imp.png")
    build_sheet(10, 10, [[swarm("idle", i) for i in range(4)]]).save(textures / "enemy_swarm.png")
    build_sheet(16, 24, [[knight("idle", i) for i in range(4)], [knight("run", i) for i in range(4)],
                         [knight("cast", i) for i in range(4)]]).save(textures / "enemy_knight.png")
    build_sheet(16, 8, [[leech("idle", i) for i in range(4)], [leech("run", i) for i in range(4)]]).save(textures / "enemy_leech.png")
    build_sheet(24, 24, [[golem("idle", i) for i in range(4)], [golem("run", i) for i in range(4)]]).save(textures / "enemy_golem.png")
    # --- Drachen (G4): fliegender Angreifer + Schwarm-Drache
    build_sheet(24, 20, [[drake("idle", i) for i in range(4)], [drake("cast", i) for i in range(4)]]).save(textures / "enemy_drake.png")
    build_sheet(16, 16, [[dragon_whelp("idle", i) for i in range(4)], [dragon_whelp("run", i) for i in range(4)]]).save(textures / "enemy_dragon_whelp.png")
    # --- NPCs (v2): Pilger, Eremit, Tempelwärtin
    build_sheet(16, 20, [[npc_pilgrim(i) for i in range(4)]]).save(textures / "npc_pilgrim.png")
    build_sheet(16, 20, [[npc_hermit(i) for i in range(4)]]).save(textures / "npc_hermit.png")
    build_sheet(16, 22, [[npc_keeper(i) for i in range(4)]]).save(textures / "npc_keeper.png")
    build_sheet(32, 32, [[warden(a, i) for i in range(4)] for a in ("idle", "run", "cast")]).save(textures / "miniboss_warden.png")
    for kind in ("shepherd", "mammon", "titan"):
        build_sheet(48, 48, [[boss(kind, "idle", i) for i in range(4)], [boss(kind, "cast", i) for i in range(4)]]).save(textures / f"boss_{kind}.png")
    build_sheet(10, 8, [[bat("hang", 0)], [bat("fly", i) for i in range(2)]]).save(textures / "prop_bat.png")
    wisp(EMBER, GOLD).save(textures / "companion_ember.png")
    wisp(WHITE, (140, 200, 255, 255)).save(textures / "companion_tear.png")
    wisp(VIOLET, MANA).save(textures / "companion_moon.png")
    wisp((200, 200, 210, 255), (120, 120, 140, 255), "chain").save(textures / "companion_chain.png")
    wisp(GOLD, (255, 230, 150, 255), "bell").save(textures / "companion_bell.png")
    _ = (math, SHADOW)  # (Importe für spätere Erweiterungen)

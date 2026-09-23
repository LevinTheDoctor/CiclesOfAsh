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

"""Tilesets (je Kreis mit zunehmendem Verfall), Hintergründe, Props, Items, Runen, Effekte, Logo."""
import math

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from .core import (ASH, BLACK, BLOOD, BONE, CLEAR, DARK_BLOOD, DARK_GOLD, DARK_STEEL, DARK_STONE, DARK_WOOD, EMBER,
                   FLAME, FONT_SOURCES, GOLD, LEATHER, MANA, OUTLINE, PALE, SHADOW, SOUL, STEEL, STONE, VIOLET, WHITE,
                   WOOD, build_sheet, dither_rect, new_image, pixel, polish, rect, rng, shift)

# ------------------------------------------------------------------ Tilesets
# Reihenfolge der Kacheln (muss zu TileMap.cs passen):
# 0 Oberkante, 1 Mauer, 2 Plattform, 3 rissige Wand, 4 Hintergrund, 5 Hintergrund zerbrochen,
# 6 Gittertor, 7 bröckelnde Plattform, 8 Wasser/Flüssigkeit, 9 Hintergrund-Nische
# Tiefe-Progression: limbo = intakte Burg (ordentliche Ziegel, kaum Schäden),
# greed = verfallende Ruine (Risse, Goldader, Brocken), wrath = rohe Höhle (organische Ränder, Glut).
MATERIALS = {
    "limbo": dict(base=(118, 116, 134), mortar=(70, 68, 86), light=(168, 164, 186), accent=(100, 138, 92),
                  back=(56, 54, 72), back_mortar=(40, 38, 52), hole=(14, 12, 22), liquid=(60, 95, 125, 170),
                  crack=(200, 190, 230), decay=0.1, wear=2),
    "greed": dict(base=(138, 112, 76), mortar=(84, 66, 42), light=(186, 160, 104), accent=(214, 178, 82),
                  back=(66, 52, 38), back_mortar=(46, 36, 26), hole=(20, 14, 8), liquid=(170, 140, 60, 180),
                  crack=(255, 220, 120), decay=0.5, wear=7),
    "wrath": dict(base=(84, 42, 44), mortar=(40, 18, 22), light=(122, 66, 60), accent=(235, 104, 48),
                  back=(44, 22, 26), back_mortar=(28, 14, 18), hole=(140, 38, 16), liquid=(120, 18, 30, 200),
                  crack=(255, 120, 48), decay=0.9, wear=13),
}


def bricks(draw, ox, base, mortar, light=None, jitter=0):
    rect(draw, ox, 0, 16, 16, base)
    for row in range(4):
        y = row * 4
        rect(draw, ox, y, 16, 1, mortar)
        offset = 0 if row % 2 == 0 else 4
        for x in range(offset, 16, 8):
            rect(draw, ox + x, y, 1, 4, mortar)
        for x in range(0, 16):                        # Steinstruktur: vereinzelte hellere/dunklere Pixel
            if rng.random() < 0.12:
                pixel(draw, ox + x, y + 1 + rng.randrange(3), shift(base + (255,), rng.choice((-14, 12)))[:3])
    if light:
        rect(draw, ox, 0, 16, 2, light)
    if jitter:
        for _ in range(jitter):                       # abgeplatzte Ecken
            x, y = rng.randrange(16), rng.randrange(2, 16)
            pixel(draw, ox + x, y, mortar)


def cracks(draw, ox, color, amount):
    for _ in range(amount):
        x, y = rng.randrange(2, 14), rng.randrange(2, 8)
        for _step in range(rng.randrange(3, 7)):
            pixel(draw, ox + x, y, color)
            x = max(0, min(15, x + rng.choice((-1, 0, 1))))
            y = min(15, y + 1)


def tileset(name):
    m = MATERIALS[name]
    image = new_image(160, 16)
    draw = ImageDraw.Draw(image)
    damage = int(m["decay"] * 6)
    wear = m["wear"]
    bricks(draw, 0, m["base"], m["mortar"], m["light"], jitter=wear)                     # 0 Oberkante
    for _ in range(2 + damage):
        pixel(draw, rng.randrange(16), rng.randrange(0, 3), m["accent"])                 # Moos / Goldader / Glut
    bricks(draw, 16, shift(m["base"] + (255,), -20)[:3], m["mortar"], jitter=wear)      # 1 Mauer
    cracks(draw, 16, m["mortar"], damage)
    rect(draw, 32, 0, 16, 4, m["light"])                                                 # 2 Plattform
    rect(draw, 32, 4, 16, 1, m["mortar"])
    for x in (34, 45):
        rect(draw, x, 5, 2, 4, m["base"])
    bricks(draw, 48, m["base"], m["mortar"])                                             # 3 rissige Wand
    cracks(draw, 48, m["crack"], 3)
    bricks(draw, 64, m["back"], m["back_mortar"])                                        # 4 Hintergrund
    bricks(draw, 80, m["back"], m["back_mortar"])                                        # 5 zerbrochen
    draw.polygon([(84, 3), (92, 2), (94, 9), (90, 14), (83, 12), (82, 7)], fill=m["hole"])
    if name == "wrath":
        draw.polygon([(86, 6), (90, 5), (91, 10), (87, 11)], fill=(255, 120, 40))
    for x in range(96, 112, 4):                                                          # 6 Gittertor
        rect(draw, x + 1, 0, 2, 16, DARK_STEEL[:3])
        pixel(draw, x + 1, 0, STEEL[:3])
    rect(draw, 96, 3, 16, 2, DARK_STEEL[:3])
    rect(draw, 96, 11, 16, 2, DARK_STEEL[:3])
    for x in range(97, 112, 4):
        pixel(draw, x + 1, 15, STEEL[:3])                                                # Spitzen unten
    rect(draw, 112, 0, 16, 4, shift(m["light"] + (255,), -30)[:3])                       # 7 bröckelnd
    rect(draw, 112, 4, 16, 1, m["mortar"])
    for x in (114, 119, 125):
        rect(draw, x, 0, 1, 4, m["mortar"])
    for x, y in ((116, 6), (121, 8), (124, 6)):
        rect(draw, x, y, 2, 2, m["base"])                                                 # herabfallende Brocken
    liquid = m["liquid"]                                                                 # 8 Flüssigkeit
    rect(draw, 128, 0, 16, 16, liquid)
    rect(draw, 128, 0, 16, 2, tuple(min(255, c + 60) for c in liquid[:3]) + (220,))
    for x in range(130, 144, 5):
        pixel(draw, x, 6, tuple(min(255, c + 40) for c in liquid[:3]) + (200,))
    bricks(draw, 144, m["back"], m["back_mortar"])                                       # 9 Nische
    draw.rectangle([149, 4, 154, 15], fill=m["hole"])
    draw.pieslice([149, 1, 154, 7], 180, 360, fill=m["hole"])
    if name == "wrath":
        # Höhlen-Feeling: unregelmäßige Ränder auf der Mauer + mehr Glut
        for _ in range(10):
            draw.polygon([(rng.randrange(16), rng.randrange(16)) for _ in range(3)], fill=shift(m["base"] + (255,), -18))
        for _ in range(6):
            pixel(draw, rng.randrange(16), rng.randrange(16), (255, 90, 40, 180))
    elif name == "greed":
        # Ruinen-Feeling: abgeplatzte Putzstellen (dunkle Flecken) im Hintergrund
        for _ in range(8):
            x, y = rng.randrange(64, 112), rng.randrange(16)
            rect(draw, x, y, rng.randrange(2, 4), rng.randrange(1, 3), m["hole"])
    return image


# ------------------------------------------------------------------ Hintergründe
def background(name):
    width, height = 480, 270
    image = new_image(width, height)
    draw = ImageDraw.Draw(image)
    top, bottom, silhouette = {
        "limbo": ((42, 30, 66), (10, 8, 16), (22, 16, 34)),
        "greed": ((58, 40, 24), (14, 9, 6), (30, 20, 12)),
        "wrath": ((80, 18, 14), (12, 4, 6), (30, 8, 10)),
    }[name]
    for y in range(height):
        t = y / height
        draw.line([(0, y), (width, y)], fill=tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(3)) + (255,))
    if name == "limbo":
        for _ in range(90):
            pixel(draw, rng.randrange(width), rng.randrange(170), (200, 190, 220, rng.randrange(90, 255)))
        draw.ellipse([352, 30, 408, 86], fill=(225, 215, 200, 255))
        draw.ellipse([360, 40, 372, 52], fill=(200, 190, 175, 255))
        # Burg: Türme mit Zinnen und Fenstern (intakte Festung des Limbus)
        rect(draw, 0, 215, width, 55, silhouette)
        for x, spire_height in [(40, 110), (120, 80), (180, 130), (300, 95), (430, 120)]:
            tower_w = 34
            top = 215 - spire_height
            rect(draw, x - tower_w // 2, top, tower_w, spire_height, silhouette)
            for z in range(x - tower_w // 2, x + tower_w // 2, 8):            # Zinnenkranz
                rect(draw, z, top - 6, 5, 6, silhouette)
            rect(draw, x - 4, top + 18, 8, 14, (28, 20, 40, 255))             # Fenster
            rect(draw, x - 3, top + 48, 6, 10, (28, 20, 40, 255))
            if spire_height > 100:                                            # Turmspitze
                draw.polygon([(x - tower_w // 2 - 4, top), (x, top - 26), (x + tower_w // 2 + 4, top)], fill=silhouette)
    elif name == "greed":
        for x in range(0, width, 40):                                        # Höhlendecke mit Stalaktiten
            draw.polygon([(x, 0), (x + 40, 0), (x + 20 + rng.randrange(-6, 6), 30 + rng.randrange(40))], fill=silhouette)
        for _ in range(40):
            pixel(draw, rng.randrange(width), rng.randrange(60, 200), (240, 200, 90, rng.randrange(60, 200)))  # Goldglitzern
        # Ruinen: halb eingestürzte Mauern mit Lücken
        rect(draw, 0, 215, width, 55, silhouette)
        for x, spire_height in [(60, 60), (200, 100), (260, 70), (390, 110)]:
            top = 215 - spire_height
            rect(draw, x - 10, top, 20, spire_height, silhouette)
            draw.polygon([(x - 12, top + 6), (x, top), (x + 12, top + 8)], fill=silhouette)
            for gap in range(top + 14, 215, 22):                              # herausgebrochene Lücken
                draw.polygon([(x - 10, gap), (x + 10, gap + 8), (x - 10, gap + 14)], fill=top_color(name, gap))
    else:
        for _ in range(70):
            pixel(draw, rng.randrange(width), rng.randrange(height), (255, 120, 60, rng.randrange(60, 220)))  # Glut
        draw.ellipse([190, 150, 290, 250], fill=(160, 40, 20, 90))           # glühender Schlund
        # Höhle: Stalaktiten oben, unregelmäßige Stalagmiten unten
        for x in range(0, width, 30):
            draw.polygon([(x, 0), (x + 30, 0), (x + 15 + rng.randrange(-8, 8), 40 + rng.randrange(50))], fill=silhouette)
        rect(draw, 0, 215, width, 55, silhouette)
        for x in range(-20, width, 44):
            spike_h = 30 + rng.randrange(70)
            draw.polygon([(x, 270), (x + 22, 270), (x + 11 + rng.randrange(-6, 6), 270 - spike_h)], fill=silhouette)
    return image


def top_color(name, y):
    """Hintergrundfarbe an Höhe y (für "Löcher" in Ruinen-Silhouetten, damit sie durchsichtig wirken)."""
    top, bottom = {
        "limbo": ((42, 30, 66), (10, 8, 16)),
        "greed": ((58, 40, 24), (14, 9, 6)),
        "wrath": ((80, 18, 14), (12, 4, 6)),
    }[name]
    t = min(1.0, y / 270)
    return tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(3)) + (255,)


def background_mid():
    width, height = 480, 270
    image = new_image(width, height)
    draw = ImageDraw.Draw(image)
    color = (16, 12, 24, 230)
    for x in range(0, width, 120):
        draw.polygon([(x, 0), (x + 120, 0), (x + 120, 50), (x + 60, 14), (x, 50)], fill=color)
        rect(draw, x - 6, 0, 12, height, color)
        rect(draw, x - 9, height - 20, 18, 20, color)
    return image


# ------------------------------------------------------------------ Props
def prop_frames(width, height, count, painter, do_polish=True):
    frames = []
    for frame in range(count):
        image = new_image(width, height)
        painter(ImageDraw.Draw(image), frame)
        frames.append(polish(image) if do_polish else image)
    return frames


def props(textures):
    def lamp(d, f):                                   # hängende Laterne 12x20
        rect(d, 5, 0, 1, 7, DARK_STEEL)
        rect(d, 3, 7, 6, 2, DARK_STEEL)
        rect(d, 3, 9, 6, 7, (60, 50, 40, 255))
        rect(d, 4, 10, 4, 5, FLAME if f else GOLD)
        rect(d, 3, 16, 6, 1, DARK_STEEL)
    build_sheet(12, 20, [prop_frames(12, 20, 2, lamp)]).save(textures / "prop_lamp.png")

    def torch(d, f):                                  # Wandfackel 8x16
        rect(d, 3, 8, 2, 8, WOOD)
        rect(d, 2, 10, 4, 1, DARK_STEEL)
        flames = [[(3, 2), (2, 4), (5, 4)], [(4, 1), (2, 5), (5, 3)], [(3, 3), (2, 5), (5, 5)]][f]
        d.polygon([(2, 8), (6, 8)] + [flames[0]], fill=EMBER)
        rect(d, 3, 5, 2, 3, FLAME)
    build_sheet(8, 16, [prop_frames(8, 16, 3, torch)]).save(textures / "prop_torch.png")

    def candles(d, f):                                # Kerzengruppe 16x10
        for x, h in ((2, 5), (6, 7), (10, 4), (13, 6)):
            rect(d, x, 10 - h, 2, h, BONE)
            pixel(d, x + (f + x) % 2, 10 - h - 1, FLAME)
            pixel(d, x, 10 - h - 2, EMBER)
    build_sheet(16, 10, [prop_frames(16, 10, 2, candles)]).save(textures / "prop_candles.png")

    def brazier(d, f, lit):                           # Kohlenbecken 16x16
        rect(d, 2, 7, 12, 3, DARK_STEEL)
        rect(d, 4, 10, 8, 2, DARK_STEEL)
        rect(d, 7, 12, 2, 3, DARK_STEEL)
        rect(d, 4, 15, 8, 1, DARK_STEEL)
        rect(d, 3, 6, 10, 1, (40, 30, 30, 255))
        if lit:
            d.polygon([(3, 7), (13, 7), (8 + (f - 1) * 2, 0)], fill=EMBER)
            d.polygon([(5, 7), (11, 7), (8 - (f - 1), 2)], fill=FLAME)
    build_sheet(16, 16, [prop_frames(16, 16, 1, lambda d, f: brazier(d, f, False)),
                         prop_frames(16, 16, 3, lambda d, f: brazier(d, f, True))]).save(textures / "prop_brazier.png")

    def lever(d, f, on):                              # Hebel 12x14
        rect(d, 2, 10, 8, 4, DARK_STONE)
        rect(d, 3, 9, 6, 1, STONE)
        if on:
            d.line([(6, 10), (10, 3)], fill=DARK_STEEL, width=2)
            rect(d, 9, 1, 3, 3, SOUL)
        else:
            d.line([(6, 10), (2, 3)], fill=DARK_STEEL, width=2)
            rect(d, 0, 1, 3, 3, BLOOD)
    build_sheet(12, 14, [prop_frames(12, 14, 1, lambda d, f: lever(d, f, False)),
                         prop_frames(12, 14, 1, lambda d, f: lever(d, f, True))]).save(textures / "prop_lever.png")

    def pillar(d, f, state):                          # Runensäule 16x32
        rect(d, 2, 28, 12, 4, DARK_STONE)
        rect(d, 4, 4, 8, 24, STONE)
        rect(d, 2, 0, 12, 4, DARK_STONE)
        rect(d, 5, 5, 1, 22, shift(STONE, 25))
        glow = {0: (60, 55, 70, 255), 1: SOUL, 2: BLOOD}[state]
        rect(d, 6, 9, 4, 8, glow)                     # Feld für das Runensymbol (im Code überlagert)
    build_sheet(16, 32, [prop_frames(16, 32, 1, lambda d, f: pillar(d, f, 0)),
                         prop_frames(16, 32, 1, lambda d, f: pillar(d, f, 1)),
                         prop_frames(16, 32, 1, lambda d, f: pillar(d, f, 2))]).save(textures / "prop_rune_pillar.png")

    def mural(d, f):                                  # Steintafel 40x24
        rect(d, 0, 0, 40, 24, DARK_STONE)
        rect(d, 2, 2, 36, 20, (54, 50, 62, 255))
        for x in (1, 38):
            rect(d, x, 1, 1, 22, STONE)
    build_sheet(40, 24, [prop_frames(40, 24, 1, mural)]).save(textures / "prop_mural.png")

    def chest(d, f, is_open):                         # Truhe 16x12
        rect(d, 1, 5, 14, 7, WOOD)
        rect(d, 1, 8, 14, 1, DARK_STEEL)
        rect(d, 1, 5, 1, 7, DARK_STEEL)
        rect(d, 14, 5, 1, 7, DARK_STEEL)
        if is_open:
            rect(d, 1, 0, 14, 3, DARK_WOOD)
            rect(d, 2, 4, 12, 2, (255, 210, 120, 255))
        else:
            rect(d, 1, 2, 14, 3, DARK_WOOD)
            rect(d, 7, 6, 2, 2, GOLD)
    build_sheet(16, 12, [prop_frames(16, 12, 1, lambda d, f: chest(d, f, False)),
                         prop_frames(16, 12, 1, lambda d, f: chest(d, f, True))]).save(textures / "prop_chest.png")

    def cage(d, f, is_open):                          # Käfig mit Gefangenem 16x24
        if not is_open:
            rect(d, 6, 11, 4, 9, (150, 150, 140, 200))           # Gefangene Seele
            rect(d, 6, 7, 4, 4, (200, 195, 180, 220))
            pixel(d, 7, 8, BLACK)
            pixel(d, 9, 8, BLACK)
        rect(d, 1, 3, 14, 2, DARK_STEEL)
        rect(d, 1, 21, 14, 3, DARK_STEEL)
        rect(d, 7, 0, 2, 3, DARK_STEEL)
        bars = (1, 5, 9, 13) if not is_open else (1, 13)
        for x in bars:
            rect(d, x, 5, 2, 16, DARK_STEEL)
        if is_open:
            rect(d, 14, 6, 2, 14, DARK_STEEL)                     # aufgeschwungene Tür
    build_sheet(16, 24, [prop_frames(16, 24, 1, lambda d, f: cage(d, f, False)),
                         prop_frames(16, 24, 1, lambda d, f: cage(d, f, True))]).save(textures / "prop_cage.png")

    def bones(d, f):
        d.ellipse([1, 1, 6, 6], fill=BONE)
        pixel(d, 3, 3, BLACK)
        pixel(d, 5, 3, BLACK)
        rect(d, 6, 4, 6, 1, BONE)
        rect(d, 8, 5, 5, 1, shift(BONE, -30))
    build_sheet(14, 7, [prop_frames(14, 7, 1, bones)]).save(textures / "prop_bones.png")

    def coffin(d, f):                                 # stehender Sarg 12x24
        d.polygon([(3, 0), (9, 0), (11, 6), (9, 23), (3, 23), (1, 6)], fill=DARK_WOOD)
        rect(d, 5, 5, 2, 10, GOLD)
        rect(d, 3, 8, 6, 2, GOLD)
    build_sheet(12, 24, [prop_frames(12, 24, 1, coffin)]).save(textures / "prop_coffin.png")

    def chains(d, f):                                 # hängende Kette 6x28
        for y in range(0, 26, 3):
            rect(d, 2 + (y // 3 + f) % 2, y, 2, 2, DARK_STEEL)
        d.polygon([(1, 25), (5, 25), (3, 28)], fill=DARK_STEEL)
    build_sheet(6, 28, [prop_frames(6, 28, 1, chains)]).save(textures / "prop_chains.png")

    def stalactite(d, f):
        d.polygon([(0, 0), (8, 0), (4, 15)], fill=DARK_STONE)
        d.line([(3, 1), (4, 10)], fill=STONE)
    build_sheet(8, 16, [prop_frames(8, 16, 1, stalactite)]).save(textures / "prop_stalactite.png")

    def window(d, f):                                 # Bleiglasfenster 24x40
        d.rectangle([2, 10, 21, 39], fill=(30, 20, 40, 255))
        d.pieslice([2, 0, 21, 20], 180, 360, fill=(30, 20, 40, 255))
        colors = [BLOOD, (70, 70, 180, 255), GOLD, (60, 140, 90, 255)]
        for y in range(4, 38, 5):
            for x in range(4, 20, 4):
                d.rectangle([x, y, x + 2, y + 3], fill=colors[(x + y) % 4])
        rect(d, 11, 2, 2, 37, BLACK)
        rect(d, 3, 22, 18, 2, BLACK)
    build_sheet(24, 40, [prop_frames(24, 40, 1, window)]).save(textures / "prop_window.png")

    def statue(d, f):                                 # trauernder Engel 16x28
        c, s = (150, 146, 156, 255), (110, 106, 118, 255)
        d.polygon([(8, 6), (0, 2), (2, 16)], fill=s)                   # Flügel
        d.polygon([(8, 6), (16, 2), (14, 16)], fill=s)
        rect(d, 5, 3, 6, 5, c)
        rect(d, 4, 8, 8, 16, c)
        rect(d, 6, 9, 4, 4, s)                                          # Hände vor dem Gesicht
        rect(d, 2, 24, 12, 4, DARK_STONE)
    build_sheet(16, 28, [prop_frames(16, 28, 1, statue)]).save(textures / "prop_statue.png")

    def gold_pile(d, f):
        d.polygon([(0, 8), (8, 1), (16, 8)], fill=DARK_GOLD)
        for x, y in ((4, 5), (8, 3), (11, 6), (6, 7), (9, 5)):
            rect(d, x, y, 2, 1, GOLD)
        pixel(d, 8 + f, 2, WHITE)
    build_sheet(16, 8, [prop_frames(16, 8, 2, gold_pile)]).save(textures / "prop_gold_pile.png")

    def lava_vent(d, f):
        rect(d, 0, 4, 16, 4, (40, 20, 20, 255))
        d.ellipse([3, 1 + f, 13, 8], fill=EMBER)
        rect(d, 6, 3 + f, 4, 2, FLAME)
    build_sheet(16, 8, [prop_frames(16, 8, 2, lava_vent)]).save(textures / "prop_lava_vent.png")

    def banner(d, f):                                 # Banner 10x24 (Akzent Blutrot)
        rect(d, 0, 0, 10, 1, WOOD)
        d.polygon([(1, 1), (9, 1), (9, 22), (5, 18 + f), (1, 22)], fill=BLOOD)
        rect(d, 4, 6, 2, 8, GOLD)
        rect(d, 2, 8, 6, 2, GOLD)
    build_sheet(10, 24, [prop_frames(10, 24, 2, banner)]).save(textures / "prop_banner.png")

    def cobweb(d, f):
        web = (200, 200, 210, 150)
        for i in range(0, 16, 4):
            d.line([(0, 0), (16 - i, i)], fill=web)
        d.arc([-8, -8, 8, 8], 0, 90, fill=web)
        d.arc([-14, -14, 14, 14], 0, 90, fill=web)
    build_sheet(16, 16, [prop_frames(16, 16, 1, cobweb, do_polish=False)]).save(textures / "prop_cobweb.png")

    # --- Zerstörbare Deko (v2)
    def urn(d, f):                                      # Graburne 12x16, leichtes Geistern-Licht
        d.polygon([(3, 3), (8, 1), (9, 3), (9, 13), (3, 13), (2, 5)], fill=(110, 118, 132, 255))
        rect(d, 4, 0, 4, 3, (70, 74, 86, 255))
        rect(d, 3, 12, 6, 1, DARK_STONE)
        pixel(d, 5, 6, (160, 200, 208, 220))
        pixel(d, 7, 8, (160, 200, 208, 180))
        rect(d, 2, 5, 7, 1, (84, 90, 104, 255))
    build_sheet(12, 16, [prop_frames(12, 16, 1, urn)]).save(textures / "prop_urn.png")

    def barrel(d, f):                                   # Altes Fass 14x18
        rect(d, 2, 1, 10, 16, WOOD)
        rect(d, 2, 4, 10, 1, DARK_WOOD)
        rect(d, 2, 12, 10, 1, DARK_WOOD)
        for x in range(3, 12, 3):                       # vertikale Dauben
            rect(d, x, 1, 1, 16, (74, 50, 34, 255))
        rect(d, 1, 1, 1, 16, DARK_WOOD)
        rect(d, 12, 1, 1, 16, DARK_WOOD)
        pixel(d, 4, 8, (50, 60, 40, 255))               # Moos
        pixel(d, 9, 9, (50, 60, 40, 255))
    build_sheet(14, 18, [prop_frames(14, 18, 1, barrel)]).save(textures / "prop_barrel.png")

    def bone_pile(d, f):                               # Knochenhaufen 16x10
        d.polygon([(0, 9), (16, 9), (13, 4), (8, 2), (3, 5)], fill=(90, 86, 74, 255))
        rect(d, 3, 5, 3, 1, BONE)
        rect(d, 8, 3, 4, 1, BONE)
        rect(d, 5, 7, 2, 1, BONE)
        rect(d, 11, 6, 3, 1, shift(BONE, -30))
        d.ellipse([6, 5, 9, 8], fill=BONE)              # Schädelrest
        pixel(d, 7, 6, BLACK)
    build_sheet(16, 10, [prop_frames(16, 10, 1, bone_pile)]).save(textures / "prop_bone_pile.png")

    def bookshelf(d, f):                               # Morsches Regal 16x22
        rect(d, 0, 0, 16, 22, DARK_WOOD)
        for row in range(3):
            y = 1 + row * 7
            rect(d, 1, y, 14, 6, (30, 22, 16, 255))
            for x, col in ((2, BLOOD), (5, DARK_GOLD), (8, (60, 80, 60, 255)), (11, DARK_BLOOD)):
                rect(d, x, y + 1, 2, 5, col)
        pixel(d, 3, 20, (50, 60, 40, 255))             # Schimmel
        pixel(d, 12, 18, (50, 60, 40, 255))
    build_sheet(16, 22, [prop_frames(16, 22, 1, bookshelf)]).save(textures / "prop_bookshelf.png")

    # --- Fluchtkreaturen (v2)
    def rat(d, f):                                      # Höhlenratte 10x6
        fur = (74, 62, 58, 255)
        step = f % 2
        rect(d, 1, 2, 7, 3, fur)
        rect(d, 0, 3, 2, 2, shift(fur, 25))            # Kopf
        pixel(d, 0, 3, BLOOD)
        rect(d, 8, 1 + step, 2, 1, (150, 130, 110, 255))   # Schwanz
        rect(d, 2, 5 + (step == 0), 1, 1, BLACK)
        rect(d, 5, 5 + (step == 1), 1, 1, BLACK)
    build_sheet(10, 6, [prop_frames(10, 6, 2, rat)]).save(textures / "prop_rat.png")

    def moth(d, f):                                     # Grabmotte 8x8
        wing = (170, 160, 150, 235)
        body = (60, 50, 46, 255)
        up = f % 2
        d.polygon([(3, 3 + up), (0, 1 + up), (1, 5 + up)], fill=wing)
        d.polygon([(4, 3 + up), (7, 1 + up), (6, 5 + up)], fill=wing)
        rect(d, 3, 2, 2, 4, body)
        pixel(d, 3, 2, BONE)
        pixel(d, 4, 2, BONE)
    build_sheet(8, 8, [prop_frames(8, 8, 2, moth)]).save(textures / "prop_moth.png")


# ------------------------------------------------------------------ Items (Icons 12x12, eine Spalte pro Item)
ITEM_ORDER = ["grave_lantern", "saint_lamp", "hellfire_lantern", "rosary", "bleeding_heart", "eye_of_vigil",
              "ring_of_ash", "ring_of_silence", "ring_of_mammon", "prayer_beads", "soul_shard", "lost_letter",
              "leather_jerkin", "chainmail", "scale_mail", "ash_harness"]


def item_icons(textures):
    def icon(item, frame):
        image = new_image(12, 12)
        d = ImageDraw.Draw(image)
        glint = frame == 1
        if item.endswith(("lantern", "lamp")):
            glow = {"grave_lantern": GOLD, "saint_lamp": WHITE, "hellfire_lantern": EMBER}[item]
            rect(d, 5, 0, 2, 2, DARK_STEEL)
            rect(d, 3, 2, 6, 1, DARK_STEEL)
            rect(d, 3, 3, 6, 7, (60, 50, 40, 255))
            rect(d, 4, 4, 4, 5, glow)
            rect(d, 3, 10, 6, 1, DARK_STEEL)
        elif item == "rosary":
            d.ellipse([1, 1, 9, 9], outline=BONE)
            rect(d, 8, 8, 1, 3, GOLD)
            rect(d, 7, 9, 3, 1, GOLD)
        elif item == "bleeding_heart":
            rect(d, 2, 2, 3, 3, BLOOD)
            rect(d, 6, 2, 3, 3, BLOOD)
            d.polygon([(1, 4), (10, 4), (5, 10)], fill=BLOOD)
            pixel(d, 5, 11, BLOOD)
        elif item == "eye_of_vigil":
            d.ellipse([1, 3, 10, 8], fill=BONE)
            d.ellipse([4, 3, 7, 8], fill=MANA)
            pixel(d, 5, 5, BLACK)
        elif item.startswith("ring"):
            band = {"ring_of_ash": ASH, "ring_of_silence": (40, 30, 55, 255), "ring_of_mammon": GOLD}[item]
            stone = {"ring_of_ash": EMBER, "ring_of_silence": VIOLET, "ring_of_mammon": BLOOD}[item]
            d.ellipse([2, 3, 9, 10], outline=band, width=2)
            rect(d, 4, 1, 4, 3, stone)
        elif item == "prayer_beads":
            for i in range(6):
                angle = i / 6 * math.tau
                rect(d, 5 + int(math.cos(angle) * 3), 5 + int(math.sin(angle) * 3), 2, 2, (180, 130, 90, 255))
            rect(d, 5, 9, 1, 3, GOLD)
        elif item == "soul_shard":
            d.polygon([(6, 0), (10, 6), (6, 11), (2, 6)], fill=SOUL)
            d.line([(6, 1), (6, 10)], fill=WHITE)
        elif item == "lost_letter":
            rect(d, 1, 3, 10, 7, BONE)
            d.line([(1, 3), (6, 7), (11, 3)], fill=(150, 140, 120, 255))
            rect(d, 5, 6, 2, 2, BLOOD)                                     # Siegel
        elif item == "leather_jerkin":                                     # Lederwams
            d.polygon([(2, 1), (9, 1), (10, 10), (1, 10)], fill=LEATHER)
            rect(d, 2, 1, 1, 9, shift(LEATHER, -25))
            rect(d, 5, 1, 1, 9, shift(LEATHER, -35))                       # Naht
            rect(d, 4, 3, 3, 1, DARK_WOOD)                                 # Kragen
        elif item == "chainmail":                                           # Kettenhemd
            d.polygon([(2, 1), (9, 1), (10, 10), (1, 10)], fill=STEEL)
            for y in range(2, 10, 2):                                      # Maschenmuster
                for x in range(2, 10, 2):
                    pixel(d, x + (y // 2) % 2, y, DARK_STEEL)
            rect(d, 4, 0, 3, 1, DARK_STEEL)                                # Halsbund
        elif item == "scale_mail":                                          # Schuppenpanzer
            d.polygon([(2, 1), (9, 1), (10, 10), (1, 10)], fill=(110, 104, 122, 255))
            for y in range(2, 10, 2):                                      # überlappende Schuppen
                for x in range(1 + y % 4, 11, 3):
                    d.arc([x, y, x + 2, y + 2], 180, 360, fill=(140, 134, 150, 255))
            rect(d, 4, 0, 3, 1, DARK_STEEL)
        elif item == "ash_harness":                                         # Aschenharnisch (heilig)
            d.polygon([(2, 1), (9, 1), (10, 10), (1, 10)], fill=(70, 64, 78, 255))
            rect(d, 3, 2, 5, 8, (100, 92, 112, 255))
            rect(d, 5, 2, 1, 8, EMBER)                                      # glühende Mittelnaht
            pixel(d, 5, 4, FLAME)
            pixel(d, 5, 7, FLAME)
            rect(d, 4, 0, 3, 1, GOLD)                                       # goldener Kragen
        result = polish(image, light=18, dark=-18, gradient=0)
        if glint:
            ImageDraw.Draw(result).point((9, 1), fill=WHITE)
        return result

    rows = [[icon(item, frame) for item in ITEM_ORDER] for frame in range(2)]
    build_sheet(12, 12, rows).save(textures / "items.png")
    return ITEM_ORDER


# ------------------------------------------------------------------ Runen (4 Symbole, 8x8)
def runes(textures):
    frames = []
    shapes = [
        [(1, 1), (6, 6), (1, 6), (6, 1)],                 # Kreuzung
        [(3, 0), (3, 7), (0, 3), (6, 3)],                 # Kreuz
        [(0, 7), (3, 0), (6, 7), (1, 4)],                 # Dreieck
        [(0, 0), (6, 0), (6, 7), (0, 7)],                 # Rahmen
    ]
    for points in shapes:
        image = new_image(8, 8)
        d = ImageDraw.Draw(image)
        d.line(points[:2], fill=WHITE)
        d.line(points[2:], fill=WHITE)
        if points is shapes[2]:
            d.line([points[1], points[2]], fill=WHITE)
        if points is shapes[3]:
            d.rectangle([1, 1, 5, 6], outline=WHITE)
        frames.append(image)
    build_sheet(8, 8, [frames]).save(textures / "runes.png")


# ------------------------------------------------------------------ Projektile, Effekte, Pickups, Siegel
def small_sprites(textures):
    def projectile(kind):
        frames = []
        for frame in range(2):
            image = new_image(8, 8)
            d = ImageDraw.Draw(image)
            if kind == "holy_bolt":
                d.polygon([(4, 0), (7, 4), (4, 7), (1, 4)], fill=GOLD)
                rect(d, 3, 3, 2, 2, WHITE if frame else BONE)
            elif kind == "shadow_dagger":
                d.polygon([(0, 4), (7, 3), (7, 4), (0, 5)], fill=VIOLET)
                rect(d, 0, 3 + frame, 2, 2, SHADOW)
            elif kind == "enemy_orb":
                d.ellipse([1, 1, 6, 6], fill=BLOOD)
                d.ellipse([2, 2, 4, 4], fill=EMBER if frame else GOLD)
            elif kind == "ember":
                d.ellipse([1, 1, 6, 6], fill=EMBER)
                rect(d, 3, 3, 2, 2, GOLD)
            elif kind == "candle":
                rect(d, 3, 4, 2, 4, BONE)
                d.polygon([(4, 0 + frame), (6, 3), (4, 4), (2, 3)], fill=EMBER if frame else GOLD)
            elif kind == "soul_spark":
                d.ellipse([2, 2, 5, 5], fill=SOUL)
                pixel(d, 3 + frame, 1, WHITE)
            frames.append(image)
        build_sheet(8, 8, [frames]).save(textures / f"projectile_{kind}.png")

    for kind in ("holy_bolt", "shadow_dagger", "enemy_orb", "ember", "candle", "soul_spark"):
        projectile(kind)

    slashes = []
    for frame in range(3):
        image = new_image(32, 24)
        d = ImageDraw.Draw(image)
        width = 4 - frame
        d.arc([2, 2, 30, 30], 200 + frame * 20, 340, fill=WHITE, width=width)
        d.arc([4, 4, 28, 28], 210 + frame * 20, 330, fill=GOLD, width=max(1, width - 1))
        slashes.append(image)
    build_sheet(32, 24, [slashes]).save(textures / "effect_slash.png")

    def pickup(kind):
        size = 12 if kind == "relic" else 8
        frames = []
        for frame in range(2):
            image = new_image(size, size)
            d = ImageDraw.Draw(image)
            if kind == "soul":
                d.ellipse([1, 1, 6, 6], fill=SOUL[:3] + (190,))
                d.ellipse([2, 2 + frame, 4, 4 + frame], fill=WHITE)
            elif kind == "heart":
                rect(d, 1, 2, 2, 2, BLOOD)
                rect(d, 5, 2, 2, 2, BLOOD)
                d.polygon([(0, 3), (7, 3), (4, 7 - frame)], fill=BLOOD)
                pixel(d, 2, 2, WHITE)
            elif kind == "mana":
                d.polygon([(4, 0), (7, 4), (4, 7), (1, 4)], fill=MANA)
                pixel(d, 3 + frame, 3, WHITE)
            elif kind == "relic":
                rect(d, 3, 1, 6, 4, GOLD)
                rect(d, 5, 5, 2, 3, DARK_GOLD)
                rect(d, 3, 8, 6, 2, GOLD)
                rect(d, 4, 2, 4, 1, BLOOD)
                if frame:
                    pixel(d, 8, 0, WHITE)
            frames.append(image)
        build_sheet(size, size, [frames]).save(textures / f"pickup_{kind}.png")

    for kind in ("soul", "heart", "mana", "relic"):
        pickup(kind)

    rows = []
    for active in (False, True):
        frames = []
        for frame in range(4):
            image = new_image(32, 32)
            d = ImageDraw.Draw(image)
            color = GOLD if active else ASH
            d.ellipse([1, 1, 30, 30], fill=(240, 220, 140, 90) if active else (120, 112, 128, 40))
            d.ellipse([3, 3, 28, 28], outline=color)
            d.ellipse([8, 8, 23, 23], outline=color)
            for i in range(6):
                angle = math.radians(i * 60 + frame * 15)
                rect(d, int(15.5 + math.cos(angle) * 11), int(15.5 + math.sin(angle) * 11), 2, 2, color)
            rect(d, 15, 9, 2, 14, color)
            rect(d, 11, 13, 10, 2, color)
            frames.append(image)
        rows.append(frames)
    build_sheet(32, 32, rows).save(textures / "exit_sigil.png")


# ------------------------------------------------------------------ Logo
def logo(textures, docs):
    """Logo: Schriftzug mit Goldverlauf, Blutrand, Höllentrichter dahinter, Asche-Partikel."""
    scale = 1
    width, height = 320, 110
    image = new_image(width, height)
    d = ImageDraw.Draw(image)
    center_x, center_y = width // 2, 58
    for index in range(6):                                          # Höllentrichter (Dantes Kreise)
        rx, ry = 150 - index * 22, 42 - index * 6
        color = [(90, 20, 26, 150), (110, 26, 30, 150), (130, 34, 30, 160), (160, 60, 30, 170), (200, 110, 40, 180), (240, 190, 90, 200)][index]
        d.ellipse([center_x - rx, center_y - ry + index * 4, center_x + rx, center_y + ry + index * 4], outline=color, width=1)
    font = ImageFont.truetype(str(FONT_SOURCES / "JacquardaBastarda9-Regular.ttf"), 36)
    text = "Circles of Ash"
    mask = Image.new("L", (width, height), 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.fontmode = "1"
    box = mask_draw.textbbox((0, 0), text, font=font)
    tx = (width - (box[2] - box[0])) // 2 - box[0]
    ty = 30 - box[1]
    mask_draw.text((tx, ty), text, font=font, fill=255)
    gradient = new_image(width, height)                             # Goldverlauf von oben hell nach unten dunkel
    gd = ImageDraw.Draw(gradient)
    for y in range(height):
        t = max(0.0, min(1.0, (y - 28) / 44))
        color = (int(255 * (1 - t) + 150 * t), int(230 * (1 - t) + 90 * t), int(150 * (1 - t) + 30 * t), 255)
        gd.line([(0, y), (width, y)], fill=color)
    outline_mask = mask.filter(ImageFilter.MaxFilter(3))
    shadow = Image.new("RGBA", (width, height), (70, 8, 16, 255))
    image.paste(Image.new("RGBA", (width, height), (0, 0, 0, 200)), (2, 3), outline_mask)   # Schlagschatten
    image.paste(shadow, (0, 0), outline_mask)                                                # Blutrand
    image.paste(gradient, (0, 0), mask)
    sub_font = ImageFont.truetype(str(FONT_SOURCES / "Tiny5-Regular.ttf"), 8)
    d = ImageDraw.Draw(image)
    d.fontmode = "1"
    subtitle = "·  EIN ABSTIEG DURCH DIE KREISE  ·"
    sw = d.textlength(subtitle, font=sub_font)
    d.text(((width - sw) / 2, 88), subtitle, font=sub_font, fill=(200, 190, 170, 255))
    for _ in range(60):                                                                    # Asche
        x, y = rng.randrange(width), rng.randrange(height)
        if image.getpixel((x, y))[3] == 0:
            d.point((x, y), fill=(150, 140, 150, rng.randrange(60, 200)))
    image.save(textures / "logo.png")
    docs.mkdir(parents=True, exist_ok=True)
    image.resize((width * 4, height * 4), Image.NEAREST).save(docs / "logo.png")
    _ = (scale, CLEAR, OUTLINE, PALE, LEATHER, DARK_BLOOD, BLOOD)


def generate(textures, docs):
    for name in MATERIALS:
        tileset(name).save(textures / f"tiles_{name}.png")
        background(name).save(textures / f"bg_{name}.png")
    background_mid().save(textures / "bg_mid.png")
    props(textures)
    item_icons(textures)
    runes(textures)
    small_sprites(textures)
    logo(textures, docs)
    _ = dither_rect

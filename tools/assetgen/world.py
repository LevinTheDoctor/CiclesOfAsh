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
    """16-Bit: vier Tonwerte — Ziegel mit Lichtkante oben links neben der Fuge,
    Fugenschatten unten, individuelles Steinkorn. Reihenversatz wie im klassischen Mauerwerk."""
    base_l = shift(base + (255,), 26)[:3]                     # Ziegel-Licht
    base_d = shift(base + (255,), -26)[:3]                    # Ziegel-Schatten
    rect(draw, ox, 0, 16, 16, base)
    for row in range(4):
        y = row * 4
        offset = 0 if row % 2 == 0 else 4
        rect(draw, ox, y, 16, 1, mortar)                       # Fugenlinie
        for x in range(offset - 4, 16, 8):
            rect(draw, ox + x, y, 1, 4, mortar)                 # Stoßfuge (versetzt)
            if 0 <= x + 1 < 16:
                pixel(draw, ox + x + 1, y, base_l)               # Lichtkante rechts der Stoßfuge
        for x in range(0, 16):                                  # Steinstruktur: individuelles Korn
            if rng.random() < 0.12:
                pixel(draw, ox + x, y + 1 + rng.randrange(3), shift(base + (255,), rng.choice((-14, 12)))[:3])
            if rng.random() < 0.06:
                pixel(draw, ox + x, y + 2, base_d)
        if y + 3 < 16:                                         # Fugenschatten unten
            for x in range(0, 16):
                if rng.random() < 0.5:
                    pixel(draw, ox + x, y + 3, shift(mortar + (255,), -12)[:3])
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
    """16-Bit-Anhebung (G14): Silhouetten bekommen eine Lichtkante oben links und
    Binnenstruktur (Zinnen-Schatten, Fensterrahmen, Ziegel-Reihen), Himmel bekommt
    eine zweite Farbzone am Horizont. Grundgerüst bleibt identisch."""
    width, height = 480, 270
    image = new_image(width, height)
    draw = ImageDraw.Draw(image)
    top, bottom, silhouette = {
        "limbo": ((42, 30, 66), (10, 8, 16), (22, 16, 34)),
        "greed": ((58, 40, 24), (14, 9, 6), (30, 20, 12)),
        "wrath": ((80, 18, 14), (12, 4, 6), (30, 8, 10)),
    }[name]
    horizon = (48, 36, 74) if name == "limbo" else ((64, 45, 28) if name == "greed" else (86, 22, 16))
    for y in range(height):
        t = y / height
        if y > 200:                                  # dunklere Zone direkt über dem Horizont
            t2 = (y - 200) / 70
            base = tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(3))
            col = tuple(int(base[i] * (1 - t2 * 0.35) + horizon[i] * t2 * 0.35) for i in range(3))
        else:
            col = tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(3))
        draw.line([(0, y), (width, y)], fill=col + (255,))
    sil_light = tuple(min(255, c + 18) for c in silhouette[:3]) + (255,)   # Silhouetten-Licht oben links
    sil_dark = tuple(max(0, c - 10) for c in silhouette[:3]) + (255,)
    if name == "limbo":
        for _ in range(90):
            pixel(draw, rng.randrange(width), rng.randrange(170), (200, 190, 220, rng.randrange(90, 255)))
        draw.ellipse([352, 30, 408, 86], fill=(225, 215, 200, 255))
        draw.ellipse([360, 40, 372, 52], fill=(200, 190, 175, 255))
        # Burg: Türme mit Zinnen und Fenstern (intakte Festung des Limbus)
        rect(draw, 0, 215, width, 55, silhouette)
        rect(draw, 0, 215, width, 2, sil_light)                       # Mauer-Lichtkante
        for x, spire_height in [(40, 110), (120, 80), (180, 130), (300, 95), (430, 120)]:
            tower_w = 34
            top = 215 - spire_height
            rect(draw, x - tower_w // 2, top, tower_w, spire_height, silhouette)
            rect(draw, x - tower_w // 2, top, tower_w, 1, sil_light)   # Turm-Lichtkante
            rect(draw, x - tower_w // 2, top, 2, spire_height, sil_light)   # linke Lichtseite
            for z in range(x - tower_w // 2, x + tower_w // 2, 8):            # Zinnenkranz
                rect(draw, z, top - 6, 5, 6, silhouette)
                rect(draw, z, top - 6, 5, 1, sil_light)
            rect(draw, x - 4, top + 18, 8, 14, (28, 20, 40, 255))             # Fenster
            rect(draw, x - 5, top + 17, 10, 1, sil_dark)                       # Fensterrahmen oben
            rect(draw, x - 3, top + 48, 6, 10, (28, 20, 40, 255))
            rect(draw, x - 4, top + 47, 8, 1, sil_dark)
            if spire_height > 100:                                            # Turmspitze
                draw.polygon([(x - tower_w // 2 - 4, top), (x, top - 26), (x + tower_w // 2 + 4, top)], fill=silhouette)
                draw.line([(x - tower_w // 2 - 4, top), (x, top - 26)], fill=sil_light)   # Dach-Licht
            for by in range(top + 8, 215, 12):                                # Ziegel-Reihen andeuten
                if rng.random() < 0.5:
                    rect(draw, x - tower_w // 2 + rng.randrange(3, 28), by, 4, 1, sil_dark)
    elif name == "greed":
        for x in range(0, width, 40):                                        # Höhlendecke mit Stalaktiten
            draw.polygon([(x, 0), (x + 40, 0), (x + 20 + rng.randrange(-6, 6), 30 + rng.randrange(40))], fill=silhouette)
            draw.line([(x + 2, 0), (x + 16, 28)], fill=sil_light)           # Decken-Lichtkante
        for _ in range(40):
            pixel(draw, rng.randrange(width), rng.randrange(60, 200), (240, 200, 90, rng.randrange(60, 200)))  # Goldglitzern
            if rng.random() < 0.4:
                px2 = rng.randrange(width), rng.randrange(60, 200)
                pixel(draw, px2[0], px2[1], (255, 230, 140, rng.randrange(120, 255)))   # heller Glanzkern
        # Ruinen: halb eingestürzte Mauern mit Lücken
        rect(draw, 0, 215, width, 55, silhouette)
        rect(draw, 0, 215, width, 2, sil_light)
        for x, spire_height in [(60, 60), (200, 100), (260, 70), (390, 110)]:
            top = 215 - spire_height
            rect(draw, x - 10, top, 20, spire_height, silhouette)
            rect(draw, x - 10, top, 20, 1, sil_light)
            rect(draw, x - 10, top, 2, spire_height, sil_light)
            draw.polygon([(x - 12, top + 6), (x, top), (x + 12, top + 8)], fill=silhouette)
            draw.line([(x - 12, top + 6), (x, top)], fill=sil_light)
            for gap in range(top + 14, 215, 22):                              # herausgebrochene Lücken
                draw.polygon([(x - 10, gap), (x + 10, gap + 8), (x - 10, gap + 14)], fill=top_color(name, gap))
    else:
        for _ in range(70):
            pixel(draw, rng.randrange(width), rng.randrange(height), (255, 120, 60, rng.randrange(60, 220)))  # Glut
            if rng.random() < 0.3:
                pixel(draw, rng.randrange(width), rng.randrange(height), (255, 200, 90, rng.randrange(80, 255)))  # Glut-Kerne
        draw.ellipse([190, 150, 290, 250], fill=(160, 40, 20, 90))           # glühender Schlund
        draw.ellipse([210, 170, 270, 230], fill=(200, 70, 30, 70))          # Schlund-Kern
        # Höhle: Stalaktiten oben, unregelmäßige Stalagmiten unten
        for x in range(0, width, 30):
            draw.polygon([(x, 0), (x + 30, 0), (x + 15 + rng.randrange(-8, 8), 40 + rng.randrange(50))], fill=silhouette)
            draw.line([(x + 2, 0), (x + 12, 34)], fill=sil_light)
        rect(draw, 0, 215, width, 55, silhouette)
        rect(draw, 0, 215, width, 2, sil_light)
        for x in range(-20, width, 44):
            spike_h = 30 + rng.randrange(70)
            draw.polygon([(x, 270), (x + 22, 270), (x + 11 + rng.randrange(-6, 6), 270 - spike_h)], fill=silhouette)
            draw.line([(x + 2, 270), (x + 9 + rng.randrange(-4, 4), 270 - spike_h + 4)], fill=sil_light)   # Stalagmiten-Licht
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
        rect(d, 3, 7, 3, 1, STEEL)                     # Bügel-Licht
        rect(d, 3, 9, 6, 7, (60, 50, 40, 255))
        rect(d, 4, 10, 4, 5, FLAME if f else GOLD)
        rect(d, 4, 10, 1, 2, (255, 240, 180, 255))     # Flammenkern oben
        rect(d, 3, 16, 6, 1, DARK_STEEL)
    build_sheet(12, 20, [prop_frames(12, 20, 2, lamp)]).save(textures / "prop_lamp.png")

    def torch(d, f):                                  # Wandfackel 8x16
        rect(d, 3, 8, 2, 8, WOOD)
        rect(d, 3, 8, 1, 8, (120, 82, 58, 255))         # Holz-Lichtkante
        rect(d, 2, 10, 4, 1, DARK_STEEL)
        flames = [[(3, 2), (2, 4), (5, 4)], [(4, 1), (2, 5), (5, 3)], [(3, 3), (2, 5), (5, 5)]][f]
        d.polygon([(2, 8), (6, 8)] + [flames[0]], fill=EMBER)
        rect(d, 3, 5, 2, 3, FLAME)
        pixel(d, 3, 6, (255, 240, 180, 255))           # Flammenkern
    build_sheet(8, 16, [prop_frames(8, 16, 3, torch)]).save(textures / "prop_torch.png")

    def candles(d, f):                                # Kerzengruppe 16x10
        for x, h in ((2, 5), (6, 7), (10, 4), (13, 6)):
            rect(d, x, 10 - h, 2, h, BONE)
            pixel(d, x, 10 - h, (245, 238, 215, 255))  # Wachs-Licht oben
            pixel(d, x + (f + x) % 2, 10 - h - 1, FLAME)
            pixel(d, x, 10 - h - 2, EMBER)
    build_sheet(16, 10, [prop_frames(16, 10, 2, candles)]).save(textures / "prop_candles.png")

    def brazier(d, f, lit):                           # Kohlenbecken 16x16
        rect(d, 2, 7, 12, 3, DARK_STEEL)
        rect(d, 2, 7, 5, 1, (120, 120, 135, 255))       # Rand-Licht
        rect(d, 4, 10, 8, 2, DARK_STEEL)
        rect(d, 7, 12, 2, 3, DARK_STEEL)
        rect(d, 4, 15, 8, 1, DARK_STEEL)
        rect(d, 3, 6, 10, 1, (40, 30, 30, 255))
        if lit:
            d.polygon([(3, 7), (13, 7), (8 + (f - 1) * 2, 0)], fill=EMBER)
            d.polygon([(5, 7), (11, 7), (8 - (f - 1), 2)], fill=FLAME)
            pixel(d, 8, 4, (255, 240, 180, 255))        # Flammenkern
    build_sheet(16, 16, [prop_frames(16, 16, 1, lambda d, f: brazier(d, f, False)),
                         prop_frames(16, 16, 3, lambda d, f: brazier(d, f, True))]).save(textures / "prop_brazier.png")

    def lever(d, f, on):                              # Hebel 12x14
        rect(d, 2, 10, 8, 4, DARK_STONE)
        rect(d, 3, 9, 6, 1, STONE)
        rect(d, 3, 9, 3, 1, (146, 142, 158, 255))      # Sockel-Licht
        if on:
            d.line([(6, 10), (10, 3)], fill=DARK_STEEL, width=2)
            pixel(d, 10, 3, (210, 210, 225, 255))      # Hebel-Glanz
            rect(d, 9, 1, 3, 3, SOUL)
            pixel(d, 9, 1, (200, 245, 225, 255))
        else:
            d.line([(6, 10), (2, 3)], fill=DARK_STEEL, width=2)
            pixel(d, 2, 3, (210, 210, 225, 255))
            rect(d, 0, 1, 3, 3, BLOOD)
            pixel(d, 0, 1, (200, 50, 70, 255))
    build_sheet(12, 14, [prop_frames(12, 14, 1, lambda d, f: lever(d, f, False)),
                         prop_frames(12, 14, 1, lambda d, f: lever(d, f, True))]).save(textures / "prop_lever.png")

    def pillar(d, f, state):                          # Runensäule 16x32
        rect(d, 2, 28, 12, 4, DARK_STONE)
        rect(d, 4, 4, 8, 24, STONE)
        rect(d, 5, 5, 1, 22, shift(STONE, 25))
        rect(d, 4, 4, 3, 2, (146, 142, 158, 255))      # Kapitell-Licht
        rect(d, 2, 0, 12, 4, DARK_STONE)
        glow = {0: (60, 55, 70, 255), 1: SOUL, 2: BLOOD}[state]
        rect(d, 6, 9, 4, 8, glow)                     # Feld für das Runensymbol (im Code überlagert)
        if state == 1:
            pixel(d, 6, 9, (200, 245, 225, 255))      # Runen-Glanz oben links
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
        rect(d, 1, 5, 3, 2, (120, 82, 58, 255))        # Deckel-Licht
        rect(d, 1, 8, 14, 1, DARK_STEEL)
        rect(d, 1, 5, 1, 7, DARK_STEEL)
        rect(d, 14, 5, 1, 7, DARK_STEEL)
        if is_open:
            rect(d, 1, 0, 14, 3, DARK_WOOD)
            rect(d, 2, 4, 12, 2, (255, 210, 120, 255))
            pixel(d, 3, 4, (255, 245, 200, 255))       # Gold-Glanz
        else:
            rect(d, 1, 2, 14, 3, DARK_WOOD)
            rect(d, 1, 2, 3, 1, (84, 58, 42, 255))    # Deckel-Licht
            rect(d, 7, 6, 2, 2, GOLD)
            pixel(d, 7, 6, (250, 220, 130, 255))
    build_sheet(16, 12, [prop_frames(16, 12, 1, lambda d, f: chest(d, f, False)),
                         prop_frames(16, 12, 1, lambda d, f: chest(d, f, True))]).save(textures / "prop_chest.png")

    def cage(d, f, is_open):                          # Käfig mit Gefangenem 16x24
        if not is_open:
            rect(d, 6, 11, 4, 9, (150, 150, 140, 200))           # Gefangene Seele
            rect(d, 6, 7, 4, 4, (200, 195, 180, 220))
            rect(d, 6, 7, 2, 2, (225, 220, 205, 220))            # Kopf-Licht
            pixel(d, 7, 8, BLACK)
            pixel(d, 9, 8, BLACK)
        rect(d, 1, 3, 14, 2, DARK_STEEL)
        rect(d, 1, 3, 5, 1, (120, 120, 135, 255))     # Deck-Licht
        rect(d, 1, 21, 14, 3, DARK_STEEL)
        rect(d, 7, 0, 2, 3, DARK_STEEL)                # Aufhängung oben
        bars = (1, 5, 9, 13) if not is_open else (1, 13)
        for x in bars:
            rect(d, x, 5, 2, 16, DARK_STEEL)
            pixel(d, x, 5, (120, 120, 135, 255))      # Gitter-Licht oben
        if is_open:
            rect(d, 14, 6, 2, 14, DARK_STEEL)                     # aufgeschwungene Tür
    build_sheet(16, 24, [prop_frames(16, 24, 1, lambda d, f: cage(d, f, False)),
                         prop_frames(16, 24, 1, lambda d, f: cage(d, f, True))]).save(textures / "prop_cage.png")

    def bones(d, f):
        d.ellipse([1, 1, 6, 6], fill=BONE)
        pixel(d, 3, 3, BLACK)
        pixel(d, 5, 3, BLACK)
        rect(d, 6, 4, 6, 1, BONE)
        pixel(d, 6, 4, (245, 238, 215, 255))          # Knochen-Licht
        rect(d, 8, 5, 5, 1, shift(BONE, -30))
    build_sheet(14, 7, [prop_frames(14, 7, 1, bones)]).save(textures / "prop_bones.png")

    def coffin(d, f):                                 # stehender Sarg 12x24
        d.polygon([(3, 0), (9, 0), (11, 6), (9, 23), (3, 23), (1, 6)], fill=DARK_WOOD)
        rect(d, 3, 1, 2, 4, (84, 58, 42, 255))        # Holz-Lichtkante
        rect(d, 5, 5, 2, 10, GOLD)
        pixel(d, 5, 5, (250, 220, 130, 255))          # Gold-Glanz
        rect(d, 3, 8, 6, 2, GOLD)
    build_sheet(12, 24, [prop_frames(12, 24, 1, coffin)]).save(textures / "prop_coffin.png")

    def chains(d, f):                                 # hängende Kette 6x28
        for y in range(0, 26, 3):
            rect(d, 2 + (y // 3 + f) % 2, y, 2, 2, DARK_STEEL)
            pixel(d, 2 + (y // 3 + f) % 2, y, (120, 120, 135, 255))   # Glied-Glanz
        d.polygon([(1, 25), (5, 25), (3, 28)], fill=DARK_STEEL)
    build_sheet(6, 28, [prop_frames(6, 28, 1, chains)]).save(textures / "prop_chains.png")

    def stalactite(d, f):
        d.polygon([(0, 0), (8, 0), (4, 15)], fill=DARK_STONE)
        d.line([(3, 1), (4, 10)], fill=STONE)
        d.line([(2, 1), (3, 8)], fill=(146, 142, 158, 255))   # Licht links
    build_sheet(8, 16, [prop_frames(8, 16, 1, stalactite)]).save(textures / "prop_stalactite.png")

    def window(d, f):                                 # Bleiglasfenster 24x40
        d.rectangle([2, 10, 21, 39], fill=(30, 20, 40, 255))
        d.pieslice([2, 0, 21, 20], 180, 360, fill=(30, 20, 40, 255))
        colors = [BLOOD, (70, 70, 180, 255), GOLD, (60, 140, 90, 255)]
        for y in range(4, 38, 5):
            for x in range(4, 20, 4):
                d.rectangle([x, y, x + 2, y + 3], fill=colors[(x + y) % 4])
                if (x + y) % 8 < 4:
                    pixel(d, x, y, WHITE)               # Glas-Licht oben links
        rect(d, 11, 2, 2, 37, BLACK)
        rect(d, 3, 22, 18, 2, BLACK)
    build_sheet(24, 40, [prop_frames(24, 40, 1, window)]).save(textures / "prop_window.png")

    def statue(d, f):                                 # trauernder Engel 16x28
        c, s = (150, 146, 156, 255), (110, 106, 118, 255)
        c_l = (176, 172, 184, 255)                     # Stein-Licht
        d.polygon([(8, 6), (0, 2), (2, 16)], fill=s)                   # Flügel
        d.polygon([(8, 6), (16, 2), (14, 16)], fill=s)
        rect(d, 5, 3, 6, 5, c)
        rect(d, 5, 3, 3, 2, c_l)                        # Stirn-Licht
        rect(d, 4, 8, 8, 16, c)
        rect(d, 4, 8, 3, 3, c_l)                        # Brust-Licht
        rect(d, 6, 9, 4, 4, s)                                          # Hände vor dem Gesicht
        rect(d, 2, 24, 12, 4, DARK_STONE)
        rect(d, 2, 24, 5, 1, (146, 142, 158, 255))     # Sockel-Licht
    build_sheet(16, 28, [prop_frames(16, 28, 1, statue)]).save(textures / "prop_statue.png")

    def gold_pile(d, f):
        d.polygon([(0, 8), (8, 1), (16, 8)], fill=DARK_GOLD)
        for x, y in ((4, 5), (8, 3), (11, 6), (6, 7), (9, 5)):
            rect(d, x, y, 2, 1, GOLD)
            pixel(d, x, y, (250, 220, 130, 255))       # Münz-Glanz
        pixel(d, 8 + f, 2, WHITE)
    build_sheet(16, 8, [prop_frames(16, 8, 2, gold_pile)]).save(textures / "prop_gold_pile.png")

    def lava_vent(d, f):
        rect(d, 0, 4, 16, 4, (40, 20, 20, 255))
        d.ellipse([3, 1 + f, 13, 8], fill=EMBER)
        rect(d, 6, 3 + f, 4, 2, FLAME)
        pixel(d, 7, 4 + f, (255, 240, 180, 255))        # Glut-Kern
    build_sheet(16, 8, [prop_frames(16, 8, 2, lava_vent)]).save(textures / "prop_lava_vent.png")

    def banner(d, f):                                 # Banner 10x24 (Akzent Blutrot)
        rect(d, 0, 0, 10, 1, WOOD)
        d.polygon([(1, 1), (9, 1), (9, 22), (5, 18 + f), (1, 22)], fill=BLOOD)
        rect(d, 1, 1, 2, 4, (190, 44, 60, 255))        # Stoff-Licht oben links
        rect(d, 4, 6, 2, 8, GOLD)
        pixel(d, 4, 6, (250, 220, 130, 255))
        rect(d, 2, 8, 6, 2, GOLD)
    build_sheet(10, 24, [prop_frames(10, 24, 2, banner)]).save(textures / "prop_banner.png")

    def cobweb(d, f):
        web = (200, 200, 210, 150)
        web_l = (230, 230, 240, 150)
        for i in range(0, 16, 4):
            d.line([(0, 0), (16 - i, i)], fill=web)
        d.line([(0, 0), (16, 0)], fill=web_l)          # Licht oben
        d.arc([-8, -8, 8, 8], 0, 90, fill=web)
        d.arc([-14, -14, 14, 14], 0, 90, fill=web_l)
    build_sheet(16, 16, [prop_frames(16, 16, 1, cobweb, do_polish=False)]).save(textures / "prop_cobweb.png")

    # --- Zerstörbare Deko (v2)
    def urn(d, f):                                      # Graburne 12x16, leichtes Geistern-Licht
        d.polygon([(3, 3), (8, 1), (9, 3), (9, 13), (3, 13), (2, 5)], fill=(110, 118, 132, 255))
        rect(d, 3, 4, 2, 4, (134, 142, 156, 255))        # Urne-Licht links
        rect(d, 4, 0, 4, 3, (70, 74, 86, 255))
        rect(d, 3, 12, 6, 1, DARK_STONE)
        pixel(d, 5, 6, (160, 200, 208, 220))
        pixel(d, 7, 8, (160, 200, 208, 180))
        pixel(d, 5, 5, (200, 235, 240, 220))             # Geistern-Licht Kern
        rect(d, 2, 5, 7, 1, (84, 90, 104, 255))
    build_sheet(12, 16, [prop_frames(12, 16, 1, urn)]).save(textures / "prop_urn.png")

    def barrel(d, f):                                   # Altes Fass 14x18
        rect(d, 2, 1, 10, 16, WOOD)
        rect(d, 2, 1, 2, 16, (120, 82, 58, 255))        # Daube-Licht links
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
        pixel(d, 3, 5, (245, 238, 215, 255))            # Knochen-Licht
        rect(d, 5, 7, 2, 1, BONE)
        rect(d, 11, 6, 3, 1, shift(BONE, -30))
        d.ellipse([6, 5, 9, 8], fill=BONE)              # Schädelrest
        pixel(d, 7, 6, BLACK)
        pixel(d, 6, 5, (245, 238, 215, 255))
    build_sheet(16, 10, [prop_frames(16, 10, 1, bone_pile)]).save(textures / "prop_bone_pile.png")

    def bookshelf(d, f):                               # Morsches Regal 16x22
        rect(d, 0, 0, 16, 22, DARK_WOOD)
        for row in range(3):
            y = 1 + row * 7
            rect(d, 1, y, 14, 6, (30, 22, 16, 255))
            for x, col in ((2, BLOOD), (5, DARK_GOLD), (8, (60, 80, 60, 255)), (11, DARK_BLOOD)):
                rect(d, x, y + 1, 2, 5, col)
                pixel(d, x, y + 1, shift(col, 40))      # Buchrücken-Licht
        pixel(d, 3, 20, (50, 60, 40, 255))             # Schimmel
        pixel(d, 12, 18, (50, 60, 40, 255))
    build_sheet(16, 22, [prop_frames(16, 22, 1, bookshelf)]).save(textures / "prop_bookshelf.png")

    # --- Fluchtkreaturen (v2)
    def rat(d, f):                                      # Höhlenratte 10x6
        fur = (74, 62, 58, 255)
        fur_l = (100, 86, 80, 255)                       # Fell-Licht
        step = f % 2
        rect(d, 1, 2, 7, 3, fur)
        rect(d, 1, 2, 3, 1, fur_l)
        rect(d, 0, 3, 2, 2, shift(fur, 25))             # Kopf
        pixel(d, 0, 3, BLOOD)
        rect(d, 8, 1 + step, 2, 1, (150, 130, 110, 255))   # Schwanz
        rect(d, 2, 5 + (step == 0), 1, 1, BLACK)
        rect(d, 5, 5 + (step == 1), 1, 1, BLACK)
    build_sheet(10, 6, [prop_frames(10, 6, 2, rat)]).save(textures / "prop_rat.png")

    def moth(d, f):                                     # Grabmotte 8x8
        wing = (170, 160, 150, 235)
        wing_l = (200, 192, 182, 235)                    # Flügel-Licht
        body = (60, 50, 46, 255)
        up = f % 2
        d.polygon([(3, 3 + up), (0, 1 + up), (1, 5 + up)], fill=wing)
        d.polygon([(4, 3 + up), (7, 1 + up), (6, 5 + up)], fill=wing)
        pixel(d, 2, 3 + up, wing_l)                      # Flügel-Licht innen
        pixel(d, 5, 3 + up, wing_l)
        rect(d, 3, 2, 2, 4, body)
        pixel(d, 3, 2, BONE)
        pixel(d, 4, 2, BONE)
    build_sheet(8, 8, [prop_frames(8, 8, 2, moth)]).save(textures / "prop_moth.png")

    # --- Rätsel-Props (G8): Druckplatte, Spiegel, Schiebeblock
    def pressure_plate(d, f, pressed):                  # Bodenplatte 16x6: erhaben / eingedrückt
        if pressed:
            rect(d, 1, 3, 14, 3, DARK_STONE)            # versenkte Platte
            rect(d, 2, 4, 12, 2, STONE)
            rect(d, 3, 5, 10, 1, shift(STONE, -30))
            pixel(d, 4, 4, GOLD)                        # Aufdruck
            pixel(d, 11, 4, GOLD)
        else:
            rect(d, 1, 1, 14, 4, STONE)                 # erhabene Platte mit Rand
            rect(d, 2, 2, 12, 2, shift(STONE, 20))
            rect(d, 1, 1, 14, 1, shift(STONE, 35))
            pixel(d, 4, 3, GOLD)
            pixel(d, 11, 3, GOLD)
            rect(d, 0, 5, 16, 1, DARK_STONE)            # Sockellinie
    build_sheet(16, 6, [prop_frames(16, 6, 1, lambda d, f: pressure_plate(d, f, False)),
                        prop_frames(16, 6, 1, lambda d, f: pressure_plate(d, f, True))]).save(textures / "prop_pressure_plate.png")

    def mirror(d, f):                                   # drehbarer Spiegel 16x16: 4 Winkel
        angle = f * 45                                  # 0, 45, 90, 135 Grad
        frame_col = DARK_GOLD
        glass = (170, 210, 225, 200)
        glass_dark = (120, 150, 175, 200)
        if angle == 0:                                  # senkrecht: Strich vertikal, Sockel unten
            rect(d, 7, 1, 2, 11, frame_col)
            rect(d, 7, 2, 1, 9, glass)
            rect(d, 8, 2, 1, 9, glass_dark)
            rect(d, 5, 12, 6, 2, DARK_STONE)
            rect(d, 4, 14, 8, 2, STONE)
        elif angle == 90:                               # waagerecht: Strich horizontal
            rect(d, 2, 5, 12, 2, frame_col)
            rect(d, 3, 5, 10, 1, glass)
            rect(d, 3, 6, 10, 1, glass_dark)
            rect(d, 6, 8, 4, 2, DARK_STONE)
            rect(d, 7, 10, 2, 4, STONE)
        else:                                           # 45/135 Grad: diagonale Spiegelfläche
            for i in range(11):
                x = 2 + i if angle == 45 else 13 - i
                rect(d, x, 12 - i, 2, 1, frame_col)
            for i in range(9):
                x = 3 + i if angle == 45 else 12 - i
                rect(d, x, 11 - i, 1, 1, glass)
            if angle == 45:
                rect(d, 12, 12, 4, 2, DARK_STONE)       # Fuß am unteren Ende der Diagonale
            else:
                rect(d, 0, 12, 4, 2, DARK_STONE)
            rect(d, 7, 14, 2, 2, STONE)
    build_sheet(16, 16, [prop_frames(16, 16, 4, mirror)]).save(textures / "prop_mirror.png")

    def push_block(d, f):                               # schiebbarer Steinblock 16x16
        rect(d, 1, 1, 14, 14, STONE)
        rect(d, 2, 2, 12, 12, DARK_STONE)
        rect(d, 3, 3, 10, 10, STONE)
        d.line([(3, 3), (12, 12)], fill=shift(STONE, -25))     # Laufspur-Kerben
        d.line([(12, 3), (12, 8)], fill=shift(STONE, -25))
        pixel(d, 4, 4, shift(STONE, 30))
        pixel(d, 11, 4, shift(STONE, 30))
        pixel(d, 4, 11, shift(STONE, 30))
        pixel(d, 11, 11, shift(STONE, 30))
        rect(d, 7, 6, 2, 4, shift(STONE, -35))          # Griffmulde mittig
    build_sheet(16, 16, [prop_frames(16, 16, 1, push_block)]).save(textures / "prop_push_block.png")


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
    """G16: 16-Bit-Anhebung der Kleinsprites. Alle vier Gruppen zeichnet der Code ungetönt
    (Color.White — Pickup.cs:82, Projectile.cs:85, EffectSystem.cs:138), also echte Farben
    statt Graustufen. Silhouetten bleiben pixelgenau: Binnenzeichnung nur auf Pixeln, die
    die Grundform schon füllt. Licht von oben links, vier bis sechs Tonwerte je Material."""

    def only_on(draw, image, points, color):
        """Setzt Pixel nur dort, wo die Grundform schon opak ist — Umriss wächst nie."""
        data = image.load()
        for x, y in points:
            if 0 <= x < image.width and 0 <= y < image.height and data[x, y][3] > 0:
                pixel(draw, x, y, color)

    def projectile(kind):
        frames = []
        for frame in range(2):
            image = new_image(8, 8)
            d = ImageDraw.Draw(image)
            if kind == "holy_bolt":
                d.polygon([(4, 0), (7, 4), (4, 7), (1, 4)], fill=GOLD)
                only_on(d, image, [(2, 3), (3, 2), (4, 2), (2, 4)], shift(GOLD, 55))    # Licht oben links
                only_on(d, image, [(6, 5), (5, 6), (6, 4)], shift(GOLD, -50))          # Schatten unten rechts
                rect(d, 3, 3, 2, 2, WHITE if frame else BONE)
            elif kind == "shadow_dagger":
                d.polygon([(0, 4), (7, 3), (7, 4), (0, 5)], fill=VIOLET)
                only_on(d, image, [(1, 4), (2, 4), (3, 4), (4, 4)], shift(VIOLET, 60))  # Klinge-Licht oben
                only_on(d, image, [(5, 5), (6, 5)], shift(VIOLET, -55))                 # Klingenschatten
                rect(d, 0, 3 + frame, 2, 2, SHADOW)
                pixel(d, 7, 3 + frame, shift(VIOLET, -80))                              # Fluchtlicht
            elif kind == "enemy_orb":
                d.ellipse([1, 1, 6, 6], fill=BLOOD)
                only_on(d, image, [(2, 2), (3, 2), (2, 3)], DARK_BLOOD)                 # Binnenschatten
                only_on(d, image, [(5, 5), (6, 5), (5, 6)], shift(BLOOD, -40))
                d.ellipse([2, 2, 4, 4], fill=EMBER if frame else GOLD)
                pixel(d, 2, 2, FLAME if frame else WHITE)                               # Glutkern
            elif kind == "ember":
                d.ellipse([1, 1, 6, 6], fill=EMBER)
                only_on(d, image, [(2, 2), (3, 2), (2, 3)], FLAME)                      # Heisskern oben links
                only_on(d, image, [(5, 5), (6, 4), (4, 6)], DARK_BLOOD)                 # veraschte Kante unten
                rect(d, 3, 3, 2, 2, GOLD)
                pixel(d, 3, 3, FLAME)                                                   # Glanzpunkt
            elif kind == "candle":
                rect(d, 3, 4, 2, 4, BONE)
                only_on(d, image, [(3, 4), (3, 5), (3, 6)], WHITE)                     # Wachs-Licht links
                only_on(d, image, [(4, 7), (5, 7)], DARK_WOOD)                         # Docht-Ruß
                d.polygon([(4, 0 + frame), (6, 3), (4, 4), (2, 3)], fill=EMBER if frame else GOLD)
                pixel(d, 4, 1 + frame, FLAME)                                           # Flammenkern
            elif kind == "soul_spark":
                d.ellipse([2, 2, 5, 5], fill=SOUL)
                only_on(d, image, [(2, 2), (3, 2), (2, 3)], shift(SOUL, 50))            # Licht oben links
                only_on(d, image, [(5, 4), (4, 5), (5, 5)], shift(SOUL, -45))           # Randabdunklung
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
        # G16: Binnenlicht entlang des inneren Bogens — maskiert, nur auf vorhandenen Bogenpixeln
        # (ein freier Zusatzbogen würde die Silhouette verbreitern, siehe Messung).
        for bbox, start, end, color in (
            ([6, 6, 26, 26], 215 + frame * 20, 325, shift(GOLD, -40)),   # Ablösungs-Schimmer innen
            ([3, 3, 29, 29], 205 + frame * 20, 335, shift(WHITE, 15)),    # Kernlicht oben
        ):
            scratch = Image.new("L", image.size, 0)                        # Bogensegment als Maske
            ImageDraw.Draw(scratch).arc(bbox, start, end, fill=255, width=1)
            data = image.load()
            mask = scratch.load()
            for y in range(image.height):
                for x in range(image.width):
                    if mask[x, y] and data[x, y][3] > 0:
                        d.point((x, y), fill=color)
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
                only_on(d, image, [(2, 2), (3, 2), (2, 3)], shift(SOUL, 55))            # Licht oben links
                only_on(d, image, [(6, 5), (5, 6), (6, 6)], shift(SOUL, -45))           # Randabdunklung
                d.ellipse([2, 2 + frame, 4, 4 + frame], fill=WHITE)
                pixel(d, 2, 2 + frame, shift(WHITE, 20))
            elif kind == "heart":
                rect(d, 1, 2, 2, 2, BLOOD)
                rect(d, 5, 2, 2, 2, BLOOD)
                d.polygon([(0, 3), (7, 3), (4, 7 - frame)], fill=BLOOD)
                only_on(d, image, [(1, 2), (2, 2), (1, 3), (0, 3)], shift(BLOOD, 55))   # Licht oben links
                only_on(d, image, [(6, 3), (7, 3), (6, 4)], DARK_BLOOD)                 # Schattenkante
                only_on(d, image, [(4, 5), (3, 5), (5, 5)], shift(BLOOD, -35))          # Herzgrube
                pixel(d, 2, 2, WHITE)
            elif kind == "mana":
                d.polygon([(4, 0), (7, 4), (4, 7), (1, 4)], fill=MANA)
                only_on(d, image, [(2, 3), (3, 2), (4, 1), (3, 3)], shift(MANA, 60))    # Licht oben links
                only_on(d, image, [(6, 5), (5, 6), (6, 6)], shift(MANA, -55))          # Schatten unten rechts
                pixel(d, 3 + frame, 3, WHITE)
            elif kind == "relic":
                rect(d, 3, 1, 6, 4, GOLD)
                rect(d, 5, 5, 2, 3, DARK_GOLD)
                rect(d, 3, 8, 6, 2, GOLD)
                only_on(d, image, [(3, 1), (4, 1), (5, 1), (3, 2), (3, 3)], shift(GOLD, 50))   # Licht oben links
                only_on(d, image, [(8, 3), (8, 4), (7, 4)], DARK_GOLD)                          # Schattenkante
                only_on(d, image, [(4, 8), (5, 8), (6, 8), (4, 9)], shift(GOLD, -35))          # unterer Schatten
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

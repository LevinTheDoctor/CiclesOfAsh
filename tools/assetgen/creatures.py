"""Gegner, Bosse, Mini-Boss, Fledermäuse und Begleitseelen – alle mit Outline und Schattierung.
G14: 16-Bit-Anhebung — vier bis sechs Tonwerte je Material, Licht von oben links,
klarere Silhouetten mit Binnenzeichnung. Maße unverändert (Kampfreichweiten!)."""
import math

from PIL import ImageDraw

from .core import (ASH, BLACK, BLOOD, BONE, DARK_BLOOD, DARK_GOLD, DARK_STEEL, EMBER, FLAME, GOLD, LEATHER, MANA, PALE,
                   SHADOW, SOUL, STEEL, VIOLET, WHITE, WOOD, build_sheet, new_image, pixel, polish, rect, shift)


# ------------------------------------------------------------------ Standardgegner (16x16 / 16x24)
def ghoul(anim, frame):
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    step = frame % 2
    pale_l = shift(PALE, 30)                                # Licht oben links
    rect(draw, 3, 6, 8, 6, PALE)
    rect(draw, 3, 6, 3, 2, pale_l)                          # Rücken-Lichtkante
    for x in (4, 6, 8):
        rect(draw, x, 7, 1, 3, shift(PALE, -40))            # Rippen
    rect(draw, 9, 3, 5, 5, PALE)                            # vorgestreckter Kopf
    rect(draw, 9, 3, 2, 2, pale_l)                          # Stirn-Licht
    rect(draw, 12, 6, 2, 1, BLACK)                          # Maul
    pixel(draw, 12, 4, BLOOD)
    pixel(draw, 13, 4, EMBER)
    rect(draw, 11, 8, 2, 4, PALE)                           # Arme
    rect(draw, 11, 8, 1, 4, shift(PALE, -20))               # Arm-Schattenkante
    pixel(draw, 13, 11, BONE)                               # Krallen
    pixel(draw, 13, 12, BONE)
    rect(draw, 3, 12, 8, 1, DARK_BLOOD)                     # Lendentuch
    pixel(draw, 4, 12, (170, 40, 60, 255))                  # Licht auf dem Lendentuch
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
    hide_l = (150, 40, 52, 255)                             # dunkles Fleisch im Licht
    rect(draw, 2, 6, 10, 5, DARK_BLOOD)
    rect(draw, 2, 6, 4, 2, hide_l)                          # Rücken-Lichtkante
    for x in (3, 5, 7, 9):
        pixel(draw, x, 5, BLACK)                            # Rückenstacheln
    rect(draw, 11, 4, 4, 5, DARK_BLOOD)                     # Kopf
    rect(draw, 11, 4, 2, 2, hide_l)
    rect(draw, 14, 7, 2, 1, BONE)                           # Fänge
    pixel(draw, 13, 5, FLAME)
    rect(draw, 0, 5, 2, 2, EMBER)                           # glühender Schwanz
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
    light = (215, 224, 238, 210)                             # Geisterschleier im Licht
    rect(draw, 5, 1, 6, 7, body)
    rect(draw, 5, 1, 3, 2, light)                           # Kapuze-Lichtkante
    rect(draw, 6, 3, 4, 3, BLACK)
    pixel(draw, 7, 4, SOUL)
    pixel(draw, 9, 4, SOUL)
    rect(draw, 3, 7, 10, 5, body)
    rect(draw, 3, 7, 4, 1, light)
    rect(draw, 2 + frame % 2, 8, 2, 1, body)                 # ausgestreckte Geisterhände
    rect(draw, 12 - frame % 2, 8, 2, 1, body)
    for x in range(3, 13):                                   # zerfetzter Saum
        rect(draw, x, 12, 1, 1 + (x + frame) % 3, body)
        if (x + frame) % 4 == 1 and 1 + (x + frame) % 3 >= 2:
            pixel(draw, x, 12 + (x + frame) % 3, (150, 158, 172, 200))   # dunklere Fransen innen
    return polish(image, outline=(30, 34, 48, 200))


def cultist(anim, frame):
    image = new_image(16, 24)
    draw = ImageDraw.Draw(image)
    robe_l = (190, 44, 60, 255)                             # Robe-Licht oben links
    rect(draw, 4, 9, 8, 14, BLOOD)
    rect(draw, 4, 9, 3, 14, robe_l)                          # linke Seite im Licht
    rect(draw, 7, 10, 2, 12, DARK_BLOOD)                     # Mittelnaht
    rect(draw, 4, 22, 8, 1, DARK_BLOOD)
    rect(draw, 5, 2, 6, 8, DARK_BLOOD)
    rect(draw, 5, 2, 3, 2, (110, 26, 44, 255))              # Kapuzen-Licht
    rect(draw, 7, 0, 2, 2, DARK_BLOOD)
    rect(draw, 6, 5, 4, 3, BLACK)
    pixel(draw, 7, 6, EMBER)
    pixel(draw, 9, 6, EMBER)
    rect(draw, 6, 12, 4, 1, GOLD)                            # Kultamulett
    pixel(draw, 7, 12, (250, 220, 130, 255))                 # Amulett-Glanz
    glow = FLAME if frame % 2 else EMBER
    if anim == "cast":
        rect(draw, 2, 6 - frame % 2, 2, 5, BLOOD)
        rect(draw, 12, 6 - frame % 2, 2, 5, BLOOD)
        rect(draw, 1, 3 - frame % 2, 3, 3, glow)
        rect(draw, 12, 3 - frame % 2, 3, 3, glow)
    else:
        rect(draw, 3, 12, 2, 5, BLOOD)
        rect(draw, 11, 12, 2, 5, BLOOD)
        rect(draw, 3, 12, 1, 5, robe_l)
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
    rect(draw, 7, 11 + b, 7, 3, shift(LEATHER, 25))       # Licht oben links (Leder matt)
    rect(draw, 9, 13 + b, 14, 11, shift(LEATHER, -25))
    rect(draw, 9, 14 + b, 5, 3, shift(LEATHER, -12))      # Zwischenton Bauchfalte
    rect(draw, 7, 20 + b, 18, 2, BLACK)                   # Gürtel
    for x in (9, 12, 15):
        rect(draw, x, 22 + b, 2, 2, GOLD)                 # Schlüsselbund
        pixel(draw, x, 22 + b, (250, 220, 130, 255))      # Goldglanz
    rect(draw, 10, 2 + b, 12, 11, (60, 40, 40, 255))      # Henkerskapuze
    rect(draw, 10, 2 + b, 5, 3, (86, 60, 58, 255))        # Kapuze-Licht
    rect(draw, 13, 6 + b, 7, 3, BLACK)
    pixel(draw, 15, 7 + b, EMBER)
    pixel(draw, 18, 7 + b, EMBER)
    rect(draw, 3, 12 + b, 4, 10, LEATHER)                 # Arme
    rect(draw, 25, 12 + b, 4, 10, LEATHER)
    rect(draw, 3, 12 + b, 2, 10, shift(LEATHER, 20))
    swing = 0 if anim != "cast" else [-6, -3, 0, 3][frame]
    for i in range(5):                                    # Kette mit Flegel
        rect(draw, 28, 18 + b + i * 2 + swing // 2, 1, 1, DARK_STEEL)
        pixel(draw, 28, 18 + b + i * 2 + swing // 2, STEEL)   # Kettenglieder-Glanz
    rect(draw, 26, 27 + b + swing // 2, 5, 5, DARK_STEEL)
    pixel(draw, 26, 27 + b + swing // 2, (210, 210, 225, 255))   # Flegel-Glanzpunkt
    return polish(image)


def _over(image, draw, x, y, color):
    """Zeichnet nur auf bereits gedeckte Pixel. Die Silhouette bleibt damit garantiert
    unangetastet (G16-Regel: Zusätze laufen über eine Maske, die nur bestehende Pixel
    überzeichnet — alles, was darüber hinausragt, würde nach polish() die Form verbreitern)."""
    if 0 <= x < image.width and 0 <= y < image.height and image.getpixel((x, y))[3] > 0:
        draw.point((x, y), fill=color)


def boss(kind, anim, frame):
    image = new_image(48, 48)
    draw = ImageDraw.Draw(image)
    breathe = 1 if frame in (1, 2) else 0
    casting = anim == "cast"
    if kind == "shepherd":
        ash_l = shift(ASH, 28)                             # Asche-Licht oben links
        draw.polygon([(24, 6 + breathe), (9, 47), (39, 47)], fill=ASH)
        draw.polygon([(24, 10 + breathe), (15, 47), (33, 47)], fill=(95, 90, 105, 255))
        for x, y in ((22, 16), (21, 19), (20, 22), (19, 25), (18, 28), (18, 31)):
            pixel(draw, x, y + breathe, ash_l)             # Falten-Licht als innere Pixelkette
        for x in range(12, 38, 5):
            draw.line([(x, 40), (x + 2, 47)], fill=(80, 76, 90, 255))       # Faltenwurf
        draw.ellipse([17, 4 + breathe, 31, 18 + breathe], fill=ASH)
        draw.ellipse([19, 7 + breathe, 29, 16 + breathe], fill=BONE)
        pixel(draw, 20, 8 + breathe, (245, 238, 215, 255))   # Stirn-Licht innen
        pixel(draw, 21, 8 + breathe, (245, 238, 215, 255))
        rect(draw, 21, 10 + breathe, 2, 2, BLACK)
        rect(draw, 26, 10 + breathe, 2, 2, BLACK)
        draw.line([(22, 14 + breathe), (27, 14 + breathe)], fill=(150, 140, 120, 255))
        rect(draw, 40, 8, 2, 40, WOOD)
        pixel(draw, 40, 8, (120, 82, 58, 255))              # Stab-Licht oben
        draw.arc([34, 2, 46, 14], 180, 360, fill=WOOD, width=2)
        draw.ellipse([20, 20, 28, 24], outline=GOLD)                      # Heiligenschein-Rest
        if casting:
            draw.ellipse([2, 14 - frame, 12, 24 - frame], outline=SOUL, width=2)
    elif kind == "mammon":
        gold_l = (250, 220, 120, 255)                      # Gold-Licht
        draw.ellipse([6, 14 + breathe, 42, 47], fill=DARK_GOLD)
        draw.ellipse([10, 18 + breathe, 38, 44], fill=GOLD)
        for x, y in ((11, 20), (12, 19), (13, 19), (14, 19), (11, 21), (12, 21)):   # Wanst-Licht innen
            pixel(draw, x, y + breathe, gold_l)
        for x, y in ((14, 30), (30, 26), (22, 38), (34, 36)):
            rect(draw, x, y, 3, 3, gold_l)                                 # eingewachsene Münzen
        draw.ellipse([15, 4 + breathe, 33, 22 + breathe], fill=GOLD)
        for x, y in ((17, 6), (18, 5), (19, 5), (20, 5), (17, 7), (18, 7)):   # Schädel-Licht innen
            pixel(draw, x, y + breathe, gold_l)
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
    elif kind == "tempest":
        # Wollust: ein Wirbelsturm aus Leibern, der nie zur Ruhe kommt. Kein fester Koerper -
        # die Silhouette dreht sich mit dem Frame, damit er nie stillzustehen scheint.
        spin = frame * 3
        rose_d, rose_m, rose_l = (92, 36, 62, 255), (156, 72, 104, 255), (226, 140, 172, 255)
        rose_h = (255, 190, 210, 255)                        # hoeller Glanz (fuenfter Ton)
        for i in range(7):                                 # Trichter von unten nach oben enger
            wide = 22 - i * 2
            y = 44 - i * 6
            off = ((spin + i * 5) % 12) - 6
            draw.ellipse([24 - wide + off, y - 4, 24 + wide + off, y + 4], fill=rose_d if i % 2 else rose_m)
            draw.arc([24 - wide + off, y - 4, 24 + wide + off, y + 4], 180, 360, fill=rose_l)
            _over(image, draw, 24 - wide + 6 + off, y - 3, rose_h)   # Windband-Glanz je Ebene
            _over(image, draw, 24 + wide - 8 + off, y - 3, rose_l if i % 2 else rose_h)
        for x, y in ((18, 20), (30, 26), (14, 32), (33, 36), (22, 40)):   # mitgerissene Leiber
            rect(draw, x + (spin % 3) - 1, y + breathe, 3, 5, BONE)
            pixel(draw, x + (spin % 3) - 1, y + breathe, WHITE)
            pixel(draw, x + (spin % 3), y + breathe, shift(BONE, -30))    # Schattenkante am Leib
        rect(draw, 20, 8 + breathe, 3, 3, BLACK)           # zwei Augen im Auge des Sturms
        rect(draw, 26, 8 + breathe, 3, 3, BLACK)
        pixel(draw, 20, 8 + breathe, rose_h)                # Glanz in den Augenkernen
        pixel(draw, 26, 8 + breathe, rose_h)
        for i in range(3):                                 # rosa Schleier zwischen den Ebenen
            _over(image, draw, 12 + spin % 5 + i * 6, 14 + i * 8, (255, 170, 195, 160))
        if casting:
            draw.arc([2, 10, 46, 46], 0, 360, fill=rose_l, width=2)
            draw.arc([4, 12, 44, 44], 0, 360, fill=rose_h, width=1)   # innerer Glanzring
    elif kind == "cerberus":
        # Voellerei: drei Koepfe an einem gedrungenen Leib, alle drei kauen.
        fur_d, fur_m, fur_l = (44, 52, 30, 255), (76, 92, 48, 255), (118, 136, 76, 255)
        fur_h = (150, 168, 102, 255)                        # Fell-Glanz (vierter Ton)
        slime = (110, 140, 62, 200)                        # Schleim zwischen den Kiefern
        rect(draw, 6, 24 + breathe, 36, 18, fur_m)         # massiger Rumpf
        rect(draw, 6, 24 + breathe, 36, 2, fur_l)          # Ruecken-Lichtkante
        for x in range(10, 38, 5):                          # Fellsträhnen als innere Kette
            pixel(draw, x, 26 + breathe, fur_h if x % 2 else fur_l)
        for x in range(8, 40, 4):                           # Speckfalten am Bauch
            draw.line([(x, 34 + breathe), (x + 2, 37 + breathe)], fill=fur_d)
            pixel(draw, x + 1, 35 + breathe, slime)         # Geifer-Tropfen
        rect(draw, 6, 40, 6, 8, fur_d)                     # vier Laeufe
        rect(draw, 16, 40, 6, 8, fur_d)
        rect(draw, 26, 40, 6, 8, fur_d)
        rect(draw, 36, 40, 6, 8, fur_d)
        for lx in (6, 16, 26, 36):                          # Lauf-Stirnlicht
            rect(draw, lx, 40, 6, 1, fur_m)
        # Je 4 px Luft zwischen den Schaedeln: polish() legt an jede Kante 1 px Kontur, bei
        # weniger Abstand waechst das Ganze zu einem einzigen gruenen Block zusammen.
        for i, hx in enumerate((4, 19, 34)):                # drei Schaedel, versetzt kauend
            chew = breathe if i != 1 else 1 - breathe
            rect(draw, hx, 8 + chew, 10, 14, fur_m)
            rect(draw, hx, 8 + chew, 10, 2, fur_l)
            rect(draw, hx + 1, 8 + chew, 4, 1, fur_h)       # Schaedel-Glanz oben links
            rect(draw, hx + 1, 13 + chew, 3, 3, FLAME)     # gluehende Augen
            rect(draw, hx + 6, 13 + chew, 3, 3, FLAME)
            pixel(draw, hx + 1, 13 + chew, WHITE)           # Augen-Kern
            pixel(draw, hx + 6, 13 + chew, WHITE)
            rect(draw, hx + 1, 19 + chew, 8, 2, BLACK)     # Maul
            for tx in range(hx + 2, hx + 9, 2):
                pixel(draw, tx, 19 + chew, BONE)           # Zaehne
            pixel(draw, hx + 4, 20 + chew, slime)          # Geifer im Maulwinkel
            rect(draw, hx + 3, 22 + chew, 4, 3, fur_d)     # Hals zum Rumpf
            rect(draw, hx + 3, 22 + chew, 1, 3, fur_m)      # Hals-Schattierung links
        if casting:
            for i, hx in enumerate((10, 22, 34)):
                rect(draw, hx, 24 + frame * 4, 3, 6, EMBER)
                pixel(draw, hx, 24 + frame * 4, FLAME)      # Brocken-Glanz
    elif kind == "heresiarch":
        # Ketzerei: ein Brennender, der aus seinem aufgebrochenen Sarg steigt.
        tomb_d, tomb_m, tomb_l = (46, 40, 44, 255), (84, 76, 82, 255), (128, 120, 128, 255)
        tomb_h = (168, 160, 168, 255)                        # Stein-Glanz (vierter Ton)
        rect(draw, 4, 30, 40, 18, tomb_m)                  # Sarkophag
        rect(draw, 4, 30, 40, 2, tomb_l)
        rect(draw, 4, 30, 2, 18, tomb_l)
        for x in range(8, 42, 7):                          # gesprengter Deckel
            draw.polygon([(x, 30), (x + 4, 24), (x + 7, 30)], fill=tomb_d)
            pixel(draw, x + 1, 29, tomb_l)                  # Deckelbruch-Licht
        rect(draw, 4, 30, 40, 1, tomb_h)                    # Sargkanten-Glanz oben links
        for x in range(6, 42, 9):                           # Meissel-Spuren im Stein
            pixel(draw, x, 38, tomb_d)
            pixel(draw, x + 1, 43, tomb_d)
        rect(draw, 16, 10 + breathe, 16, 22, DARK_BLOOD)   # Leib in Flammen
        for y in range(12, 30, 4):
            draw.line([(17, y + breathe), (31, y + 2 + breathe)], fill=EMBER)
            pixel(draw, 18 + (y % 8), y + breathe, shift(DARK_BLOOD, 20))  # Glut-Adern im Leib
        for x, y in ((19, 14), (26, 18), (22, 24)):
            pixel(draw, x, y + breathe, FLAME)             # Flammenkerne
            pixel(draw, x, y - 1 + breathe, WHITE)          # weisser Kern im Flammenherd
        rect(draw, 18, 4 + breathe, 12, 10, ASH)           # Schaedel
        rect(draw, 18, 4 + breathe, 12, 2, BONE)
        rect(draw, 18, 4 + breathe, 5, 1, shift(BONE, 22))  # Schaedel-Glanz oben links
        rect(draw, 20, 8 + breathe, 3, 3, FLAME)
        rect(draw, 25, 8 + breathe, 3, 3, FLAME)
        rect(draw, 21, 12 + breathe, 6, 1, BLACK)
        pixel(draw, 21, 12 + breathe, EMBER)                # Glut im Mund
        if casting:
            for i in range(4):
                rect(draw, 6 + i * 11, 2 + (frame * 5) % 12, 3, 5, EMBER)
                pixel(draw, 7 + i * 11, 3 + (frame * 5) % 12, FLAME)   # Funken-Glanz
    elif kind == "minotaur":
        # Gewalt: schwer, gehoernt, immer im Ansturm.
        hide_d, hide_m, hide_l = (58, 34, 26, 255), (104, 62, 44, 255), (150, 96, 68, 255)
        hide_h = (188, 130, 96, 255)                        # Fell-Glanz (vierter Ton)
        rect(draw, 10, 18 + breathe, 28, 22, hide_m)       # Brustkorb
        rect(draw, 10, 18 + breathe, 28, 2, hide_l)
        rect(draw, 10, 18 + breathe, 8, 1, hide_h)          # Brustring-Glanz oben links
        for y in range(22, 36, 4):                         # Rippenschatten
            draw.line([(13, y + breathe), (35, y + 1 + breathe)], fill=hide_d)
            pixel(draw, 13, y + breathe, hide_l)            # Lichtpixel an jeder Rippe
        rect(draw, 12, 20 + breathe, 3, 3, BLOOD)          # alte Kampfwunde
        rect(draw, 12, 20 + breathe, 2, 1, (200, 60, 56, 255))   # Wunden-Rand im Licht
        rect(draw, 8, 40, 10, 8, hide_d)                   # Hufe
        rect(draw, 30, 40, 10, 8, hide_d)
        rect(draw, 8, 40, 10, 1, hide_m)                    # Huf-Stirnlicht
        rect(draw, 30, 40, 10, 1, hide_m)
        for hx in range(8, 16):                             # Huf-Behaarung (nur innen)
            _over(image, draw, hx, 39, hide_d)
            _over(image, draw, hx + 22, 39, hide_d)
        rect(draw, 2, 20 + breathe, 8, 18, hide_m)         # Arme
        rect(draw, 38, 20 + breathe, 8, 18, hide_m)
        rect(draw, 2, 20 + breathe, 2, 18, hide_l)
        rect(draw, 38, 20 + breathe, 2, 18, hide_l)
        rect(draw, 2, 20 + breathe, 2, 1, hide_h)           # Schulter-Glanz
        rect(draw, 38, 20 + breathe, 2, 1, hide_h)
        rect(draw, 16, 4 + breathe, 16, 14, hide_m)        # Stierschaedel
        rect(draw, 16, 4 + breathe, 16, 2, hide_l)
        rect(draw, 16, 4 + breathe, 6, 1, hide_h)           # Stirn-Glanz oben links
        rect(draw, 19, 10 + breathe, 3, 3, BLOOD)          # blutunterlaufene Augen
        rect(draw, 26, 10 + breathe, 3, 3, BLOOD)
        pixel(draw, 19, 10 + breathe, (200, 60, 56, 255))   # Augen-Rand im Licht
        pixel(draw, 26, 10 + breathe, (200, 60, 56, 255))
        for ex in (21, 28):                                 # Augen-Glanz innen
            _over(image, draw, ex, 12 + breathe, (200, 60, 56, 255))
        rect(draw, 20, 15 + breathe, 8, 3, (40, 26, 20, 255))   # Nuestern
        pixel(draw, 22, 16 + breathe, BONE)
        pixel(draw, 25, 16 + breathe, BONE)
        draw.polygon([(16, 6), (4, 2), (14, 11)], fill=BONE)     # Hoerner
        draw.polygon([(32, 6), (44, 2), (34, 11)], fill=BONE)
        pixel(draw, 6, 4, WHITE)
        pixel(draw, 42, 4, WHITE)
        for y in range(4, 10, 2):                           # Hornring-Zeichnung (nur innen)
            _over(image, draw, 12 - y // 2, y, shift(BONE, -40))
            _over(image, draw, 33 + y // 2, y, shift(BONE, -40))
        if casting:
            for i in range(5):                             # Staub beim Scharren
                pixel(draw, 6 + i * 9, 46 - (frame + i) % 3, ASH)
                _over(image, draw, 7 + i * 9, 45 - (frame + i) % 3, shift(ASH, 25))   # Staub-Glanz
    elif kind == "geryon":
        # Betrug: freundliches Gesicht, Leib einer Schlange, Schwanz mit Stachel.
        scale_d, scale_m, scale_l = (46, 30, 62, 255), (86, 56, 112, 255), (132, 92, 166, 255)
        scale_h = (176, 138, 208, 255)                      # Schuppen-Glanz (vierter Ton)
        for i in range(5):                                 # geringelter Leib
            y = 20 + i * 6
            off = 6 if i % 2 else -6
            draw.ellipse([14 + off, y, 40 + off, y + 9], fill=scale_m if i % 2 else scale_d)
            draw.arc([14 + off, y, 40 + off, y + 9], 180, 360, fill=scale_l)
            for sx in range(16 + off, 38 + off, 6):          # einzelne Schuppen als Bögen
                draw.arc([sx, y + 2, sx + 4, y + 6], 200, 340, fill=scale_l if sx % 12 else scale_h)
            _over(image, draw, 16 + off, y + 1, scale_h)     # Ring-Anfangs-Glanz
        draw.polygon([(6, 44), (2, 34), (12, 40)], fill=scale_d)      # Stachelschwanz
        for sy in range(35, 44):                              # Schwanz-Schattierung (nur innen)
            sx = 2 + (44 - sy) // 3
            _over(image, draw, sx, sy, scale_m)
        pixel(draw, 3, 35, (150, 255, 150, 255))                      # Gift
        _over(image, draw, 4, 35, (210, 255, 210, 255))               # Gift-Glanz (im Stachel)
        for side, sign in ((10, -1), (38, 1)):                        # Fluegel
            draw.polygon([(24, 18 + breathe), (side + sign * 10, 6 + breathe), (side, 22 + breathe)],
                         fill=scale_d)
            draw.line([(24, 18 + breathe), (side + sign * 10, 6 + breathe)], fill=scale_l)
            for wy in range(9, 19, 4):                       # Fluegel-Rippen (nur innen)
                fx = 24 + sign * (wy - 12)
                if min(24, fx) <= fx <= max(24, fx):
                    _over(image, draw, fx, wy + breathe, scale_m)
        face_l = (244, 224, 200, 255)
        rect(draw, 18, 4 + breathe, 12, 12, (216, 188, 156, 255))     # ehrliches Menschengesicht
        rect(draw, 18, 4 + breathe, 12, 2, face_l)
        rect(draw, 18, 4 + breathe, 5, 1, (255, 238, 220, 255))       # Wangen-Glanz oben links
        rect(draw, 20, 8 + breathe, 2, 2, BLACK)
        rect(draw, 26, 8 + breathe, 2, 2, BLACK)
        pixel(draw, 21, 9 + breathe, (90, 130, 180, 255))   # Iris — fast freundlich
        pixel(draw, 27, 9 + breathe, (90, 130, 180, 255))
        draw.line([(21, 13 + breathe), (27, 13 + breathe)], fill=(150, 110, 90, 255))   # Laecheln
        pixel(draw, 20, 12 + breathe, (150, 110, 90, 255))
        pixel(draw, 28, 12 + breathe, (150, 110, 90, 255))
        for tx in (21, 25):                                  # weisse Zaehne im Laecheln
            pixel(draw, tx, 13 + breathe, WHITE)
        if casting:
            draw.ellipse([16, 2, 32, 18], outline=(150, 255, 150, 255))
            draw.ellipse([18, 4, 30, 16], outline=(210, 255, 210, 255))   # innerer Glanzring
    elif kind == "lucifer":
        # Verrat: bis zur Brust im Eis, drei Gesichter, sechs Fluegel, die den Frost erzeugen.
        ice_d, ice_m, ice_l = (36, 58, 82, 255), (86, 124, 156, 255), (168, 208, 232, 255)
        ice_h = (220, 240, 252, 255)                        # Eis-Glanz (vierter Ton)
        body_d, body_m = (28, 22, 34, 255), (58, 48, 66, 255)
        body_l = (86, 74, 94, 255)                           # Feder-Licht (fuenfter Ton)
        for side, sign in ((8, -1), (40, 1)):              # drei Fluegelpaare
            for i, wy in enumerate((10, 20, 30)):
                tip = side + sign * (10 - i * 2)
                draw.polygon([(24, wy + breathe), (tip, wy - 6 + breathe), (tip, wy + 6 + breathe)],
                             fill=body_d if i % 2 else body_m)
                draw.line([(24, wy + breathe), (tip, wy - 6 + breathe)], fill=ice_m)
                for fx in range(24, tip, -sign * 3):          # Federknochen im Fluegel
                    pixel(draw, fx, wy - 2 + breathe + (24 - fx) // 6, body_l)
        rect(draw, 14, 14 + breathe, 20, 20, body_m)       # Rumpf
        rect(draw, 14, 14 + breathe, 20, 2, body_d)
        rect(draw, 14, 14 + breathe, 6, 1, body_l)           # Brust-Glanz oben links
        for x in range(16, 32, 4):                           # Frostzauber-Zeichen auf der Brust
            pixel(draw, x, 20 + breathe, ice_m)
            pixel(draw, x, 24 + breathe, ice_d)
        # Wieder 4 px Luft (siehe Kerberos) - vorher ueberlappten sich die Gesichter sogar.
        for i, hx in enumerate((3, 17, 31)):                # drei Gesichter
            rect(draw, hx, 2 + breathe, 10, 12, body_m)
            rect(draw, hx, 2 + breathe, 10, 2, ice_m)
            rect(draw, hx, 2 + breathe, 4, 1, body_l)       # Stirn-Glanz oben links
            eye = (BLOOD, FLAME, (180, 180, 200, 255))[i]  # rot, gelb, fahl
            rect(draw, hx + 1, 6 + breathe, 3, 2, eye)
            rect(draw, hx + 6, 6 + breathe, 3, 2, eye)
            pixel(draw, hx + 1, 6 + breathe, WHITE)         # Augen-Glanz
            rect(draw, hx + 2, 11 + breathe, 6, 2, BLACK)  # kauendes Maul
            for tx in range(hx + 3, hx + 8, 2):
                pixel(draw, tx, 11 + breathe, BONE)
        rect(draw, 2, 34, 44, 14, ice_m)                   # zugefrorener Kokytos
        rect(draw, 2, 34, 44, 2, ice_l)
        rect(draw, 2, 34, 20, 1, ice_h)                     # Eisfläche-Glanz oben links
        for x in range(4, 46, 9):                          # Eisrisse
            draw.line([(x, 36), (x + 4, 47)], fill=ice_d)
            pixel(draw, x + 1, 38, ice_l)
            pixel(draw, x + 2, 42, ice_h)                   # Riss-Glanzpunkt
        for x in range(6, 44, 12):                           # eingeschlossene Luftblasen
            pixel(draw, x, 40 + (x // 12) % 3, ice_l)
        if casting:
            for i in range(6):                             # Frosthauch
                pixel(draw, 4 + i * 8, 20 + (frame * 3 + i) % 10, ice_l)
                if i % 2 == 0:
                    pixel(draw, 5 + i * 8, 21 + (frame * 3 + i) % 10, ice_h)   # Hauch-Glanz
    else:
        blood_l = (200, 44, 56, 255)                       # Fleisch-Licht oben links
        rect(draw, 10, 16 + breathe, 28, 26, DARK_BLOOD)
        rect(draw, 14, 18 + breathe, 20, 20, BLOOD)
        for x in range(15, 21):                            # Arm-/Brust-Licht innen (flach)
            pixel(draw, x, 19 + breathe, blood_l)
        for y in range(20, 36, 5):
            draw.line([(16, y + breathe), (32, y + 2 + breathe)], fill=(255, 120, 60, 255))  # glühende Risse
        rect(draw, 16, 4 + breathe, 16, 14, DARK_BLOOD)
        for x in range(17, 22):                            # Kopf-Licht innen
            pixel(draw, x, 5 + breathe, blood_l)
        draw.polygon([(16, 6), (6, 0), (14, 11)], fill=BONE)
        draw.polygon([(32, 6), (42, 0), (34, 11)], fill=BONE)
        pixel(draw, 14, 9, (245, 238, 215, 255))                          # Hörner-Glanz innen
        pixel(draw, 33, 9, (245, 238, 215, 255))
        rect(draw, 19, 9 + breathe, 3, 2, FLAME)
        rect(draw, 27, 9 + breathe, 3, 2, FLAME)
        rect(draw, 20, 14 + breathe, 8, 2, BLACK)
        rect(draw, 12, 42, 8, 6, BLACK)
        rect(draw, 28, 42, 8, 6, BLACK)
        raise_arms = -6 if casting else 0
        rect(draw, 4, 18 + breathe + raise_arms, 6, 16, DARK_BLOOD)
        rect(draw, 38, 18 + breathe + raise_arms, 6, 16, DARK_BLOOD)
        for yy in range(19, 23):                           # Arm-Licht innen
            pixel(draw, 5, yy + breathe + raise_arms, blood_l)
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
    bone_l = shift(BONE, 25)                              # Knochen-Licht
    rect(draw, 4, 3, 8, 6, BONE)                           # Schädel
    rect(draw, 4, 3, 3, 2, bone_l)                         # Stirn-Licht
    rect(draw, 5, 5, 2, 2, BLACK)
    rect(draw, 9, 5, 2, 2, BLACK)
    pixel(draw, 6, 5, EMBER)
    pixel(draw, 10, 5, EMBER)
    rect(draw, 6, 8, 4, 1, BLACK)                          # Kiefer
    for x in (5, 7, 9):
        pixel(draw, x, 9, shift(BONE, -40))
    rect(draw, 4, 10, 8, 3, BONE)                          # Brustkorb
    rect(draw, 4, 10, 3, 1, bone_l)
    for x in (6, 8):                                       # Zwischenrippen-Schatten
        pixel(draw, x, 11, shift(BONE, -50))
    rect(draw, 5, 11, 6, 1, shift(BONE, -50))
    rect(draw, 6, 13, 1, 5, BONE)                           # Beine
    rect(draw, 9, 13, 1, 5, BONE)
    if anim == "run":
        rect(draw, 4 + step * 2, 10, 2, 4, BONE)            # schwingende Arme
        rect(draw, 10 - step * 2, 10, 2, 4, BONE)
    else:
        rect(draw, 2, 10, 2, 6, DARK_STEEL)                 # Schild
        rect(draw, 3, 11, 1, 4, STEEL)
        pixel(draw, 2, 10, (210, 210, 225, 255))            # Schild-Glanz oben
    return polish(image)


def spider(anim, frame):
    """Grabspinne 16x12: flacher Körper, zappelnde Beine."""
    image = new_image(16, 12)
    draw = ImageDraw.Draw(image)
    body = (52, 34, 40, 255)
    body_l = (88, 62, 66, 255)                             # Chitin-Licht oben links
    rect(draw, 5, 4, 6, 5, body)                            # Hinterleib
    rect(draw, 5, 4, 3, 2, body_l)
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
    pixel(draw, 8, 6, shift(body, 40))
    pixel(draw, 6, 7, shift(body, -25))                     # Bauch-Schatten
    return polish(image)


def imp(anim, frame):
    """Feuerimp 16x16: kleiner Flieger mit Glühkrone."""
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    skin = (140, 52, 44, 255)
    skin_l = (178, 78, 60, 255)                             # Haut-Licht
    bob = frame % 2
    rect(draw, 6, 5 + bob, 5, 6, skin)                      # Kopf + Leib
    rect(draw, 6, 5 + bob, 2, 2, skin_l)                     # Stirn-Licht
    rect(draw, 7, 7 + bob, 1, 1, BLACK)
    rect(draw, 9, 7 + bob, 1, 1, BLACK)
    pixel(draw, 7, 6 + bob, FLAME)
    pixel(draw, 9, 6 + bob, FLAME)
    for x, spike in ((6, -2), (8, -3), (10, -2)):           # Hörner
        rect(draw, x, 3 + bob + spike, 1, 2, DARK_GOLD)
        pixel(draw, x, 3 + bob + spike, GOLD)                # Horn-Glanz
    rect(draw, 4, 6 + bob, 2, 2, skin)                       # Flügel
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
    rect(draw, 4 - spread // 2, 2, 2, 2, (215, 230, 240, 230))   # Licht oben links
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
    rust_l = (138, 118, 88, 255)                            # Rost-Licht
    dark_rust = (70, 56, 44, 255)
    rect(draw, 4, 2, 8, 6, rust)                            # Helm
    rect(draw, 4, 2, 3, 2, rust_l)                          # Helm-Licht
    rect(draw, 5, 4, 2, 2, BLACK)                           # Sehschlitz
    rect(draw, 9, 4, 2, 2, BLACK)
    pixel(draw, 6, 4, BLOOD)
    pixel(draw, 10, 4, BLOOD)
    rect(draw, 3, 8, 10, 8, rust)                           # Brustpanzer
    rect(draw, 3, 8, 4, 2, rust_l)                           # Panzer-Licht oben
    rect(draw, 4, 9, 8, 2, dark_rust)
    rect(draw, 7, 9, 2, 6, BLOOD)                           # Riss + Blut
    pixel(draw, 7, 9, (200, 50, 70, 255))                   # Blut-Glanz
    rect(draw, 2, 10, 2, 5, dark_rust)                      # Schultern
    rect(draw, 12, 10, 2, 5, dark_rust)
    rect(draw, 2, 10, 2, 1, rust_l)
    rect(draw, 12, 10, 2, 1, rust_l)
    rect(draw, 5, 16, 2, 6, dark_rust)                      # Beine
    rect(draw, 9, 16, 2, 6, dark_rust)
    if anim == "cast":
        # Telegraph: Schwert hochgehoben, glühende Runen
        rect(draw, 13, 2, 1, 9, STEEL)
        rect(draw, 13, 2, 1, 3, (210, 210, 225, 255))       # Klingen-Glanz
        rect(draw, 12, 1, 3, 2, DARK_STEEL)
        pixel(draw, 13, 0, FLAME)
        rect(draw, 3, 9, 1, 6, FLAME)
    else:
        rect(draw, 13, 6 + step, 1, 8, STEEL)               # Schwert seitlich
        rect(draw, 13, 6 + step, 1, 3, (210, 210, 225, 255))
        rect(draw, 0, 8, 3, 8, dark_rust)                   # Schild vor sich
        rect(draw, 1, 8, 2, 1, rust_l)
        rect(draw, 1, 10, 1, 4, BLOOD)
    return polish(image)


def leech(anim, frame):
    """Sumpfblutegel 16x8: flach, getarnt, springt hoch."""
    image = new_image(16, 8)
    draw = ImageDraw.Draw(image)
    body = (70, 50, 66, 255)
    body_l = (98, 74, 90, 255)                              # Leib-Licht oben
    squish = frame % 2
    rect(draw, 2, 3 + squish, 12, 4 - squish, body)         # gedehnter Leib
    rect(draw, 2, 3 + squish, 5, 1, body_l)
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
    stone_l = (128, 124, 142, 255)                          # Stein-Licht (stumpf)
    dark = (60, 56, 70, 255)
    step = frame % 2
    rect(draw, 5, 3, 14, 10, stone)                         # Kopf in den Schultern
    rect(draw, 5, 3, 6, 3, stone_l)                         # Stirn-Licht
    rect(draw, 7, 6, 3, 2, EMBER)
    rect(draw, 14, 6, 3, 2, EMBER)
    pixel(draw, 7, 6, FLAME)                                 # Glut-Augen mit hellem Kern
    pixel(draw, 14, 6, FLAME)
    rect(draw, 9, 9, 6, 1, BLACK)
    rect(draw, 3, 12, 18, 9, stone)                          # Rumpf
    rect(draw, 3, 12, 8, 2, stone_l)                        # Rumpf-Licht oben
    rect(draw, 4, 13, 16, 7, dark)
    rect(draw, 8, 13, 2, 7, BLACK)                          # Kernriss
    rect(draw, 8, 15, 2, 2, EMBER)
    rect(draw, 12, 17, 2, 2, EMBER)
    pixel(draw, 12, 17, FLAME)
    rect(draw, 15, 14, 1, 4, BLACK)
    rect(draw, 0, 13, 3, 7, dark)                           # Arme
    rect(draw, 21, 13, 3, 7, dark)
    rect(draw, 0, 13, 1, 2, stone)
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
    scale_l = (128, 66, 66, 255)                            # Schuppen-Licht
    dark = (56, 22, 28, 255)
    belly = (150, 130, 100, 255)
    belly_l = (176, 156, 122, 255)
    wing = (72, 30, 40, 255)
    wing_l = (96, 44, 56, 255)                              # Flügelmembran-Licht
    # Flügel links/rechts, schlagen mit dem Frame
    for wy, wx in ((2, 2), (2, 14)):
        draw.polygon([(wx + 3, 8 + flap * 2), (wx - 2, 1 + flap * 3), (wx + 7, 5 + flap * 2)], fill=wing)
        draw.line([(wx + 3, 8 + flap * 2), (wx - 1, 2 + flap * 3)], fill=wing_l, width=1)   # Flügelknochen-Licht
    rect(draw, 7, 4 + flap, 10, 7, scale)                      # Leib
    rect(draw, 7, 4 + flap, 4, 2, scale_l)                     # Rücken-Lichtkante
    rect(draw, 8, 8 + flap, 8, 2, belly)                       # heller Bauch
    rect(draw, 8, 8 + flap, 4, 1, belly_l)                      # Bauch-Licht
    rect(draw, 15, 3 + flap, 6, 5, scale)                      # Kopf mit Schnauze
    rect(draw, 15, 3 + flap, 3, 2, scale_l)                    # Kopf-Licht
    rect(draw, 20, 5 + flap, 2, 1, dark)                       # Maul
    pixel(draw, 17, 4 + flap, FLAME)                           # Auge mit hellem Kern
    for x in (9, 12, 15):                                      # Rückenstacheln
        pixel(draw, x, 3 + flap, dark)
        pixel(draw, x, 3 + flap, (120, 50, 60, 255))            # Stachel-Glanz
    draw.polygon([(5, 8 + flap), (1, 11 + flap), (5, 12 + flap)], fill=dark)   # Schwanz mit Spitze
    rect(draw, 8, 12, 2, 2, scale)                             # Beine angezogen
    rect(draw, 13, 12, 2, 2, scale)
    pixel(draw, 8, 14, DARK_GOLD)                              # Krallen
    pixel(draw, 14, 14, DARK_GOLD)
    if anim == "cast":
        rect(draw, 21, 7 + flap, 2, 2, FLAME)                  # Feuer speiend
        pixel(draw, 23, 8 + flap, EMBER)
        pixel(draw, 21, 7 + flap, (255, 240, 180, 255))         # Flammenkern
    return polish(image)


def dragon_whelp(anim, frame):
    """Drachenjunges 16x16: kleiner Schwarm-Drache, hüpfend-flatternd."""
    image = new_image(16, 16)
    draw = ImageDraw.Draw(image)
    bob = frame % 2
    scale = (108, 62, 52, 255)
    scale_l = (136, 86, 70, 255)                             # Schuppen-Licht
    dark = (64, 34, 30, 255)
    wing = (80, 46, 40, 255)
    wing_l = (104, 62, 52, 255)
    draw.polygon([(5, 5 + bob), (0, 1 + bob * 2), (3, 6 + bob)], fill=wing)    # Flügel
    draw.polygon([(8, 5 + bob), (13, 1 + bob * 2), (10, 6 + bob)], fill=wing)
    pixel(draw, 3, 4 + bob, wing_l)                          # Flügel-Licht innen (Knochenansatz)
    pixel(draw, 10, 4 + bob, wing_l)
    rect(draw, 4, 4 + bob, 7, 6, scale)                        # Leib
    rect(draw, 4, 4 + bob, 3, 2, scale_l)                      # Rücken-Licht
    rect(draw, 9, 5 + bob, 5, 4, scale)                        # Kopf
    rect(draw, 9, 5 + bob, 2, 2, scale_l)                      # Kopf-Licht
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


def companion_dragonling(frame, variant="normal"):
    """Begleiter-Drache 12x12: kleines, treues Drachenjunges. variant pale/deep nur als Farbstimmung."""
    image = new_image(12, 12)
    draw = ImageDraw.Draw(image)
    bob = frame % 2
    scale = (128, 78, 60, 255)
    scale_l = (156, 104, 80, 255)
    dark = (70, 40, 34, 255)
    wing = (90, 54, 46, 255)
    wing_l = (116, 72, 60, 255)
    eye = GOLD
    if variant == "pale":       # hell/kühl: helle Schuppen, Eisblick
        scale = (150, 156, 168, 255)
        scale_l = (182, 188, 200, 255)
        dark = (96, 104, 120, 255)
        wing = (120, 128, 146, 255)
        wing_l = (148, 156, 176, 255)
        eye = (140, 200, 255, 255)
    elif variant == "deep":     # dunkel/warm: veraschte Schuppen, Glutaugen
        scale = (78, 44, 40, 255)
        scale_l = (104, 62, 54, 255)
        dark = (48, 26, 24, 255)
        wing = (58, 32, 30, 255)
        wing_l = (78, 44, 40, 255)
        eye = EMBER
    draw.polygon([(3, 4 + bob), (0, 1 + bob * 2), (2, 5 + bob)], fill=wing)
    draw.polygon([(6, 4 + bob), (9, 1 + bob * 2), (7, 5 + bob)], fill=wing)
    draw.line([(3, 4 + bob), (1, 2 + bob * 2)], fill=wing_l, width=1)   # Flügelknochen-Licht
    draw.line([(6, 4 + bob), (8, 2 + bob * 2)], fill=wing_l, width=1)
    rect(draw, 2, 3 + bob, 6, 5, scale)                         # Leib
    rect(draw, 2, 3 + bob, 3, 2, scale_l)                        # Rücken-Licht
    rect(draw, 6, 4 + bob, 4, 3, scale)                         # Kopf
    rect(draw, 6, 4 + bob, 2, 1, scale_l)                       # Kopf-Licht
    pixel(draw, 9, 5 + bob, dark)                               # Maul
    pixel(draw, 8, 4 + bob, eye)                                # freundliches Auge
    pixel(draw, 3, 2 + bob, dark)                              # Stachelchen
    rect(draw, 3, 8 + bob, 2, 2, scale)                        # Beinchen
    rect(draw, 6, 8 + bob, 2, 2, scale)
    return polish(image, light=18, dark=-14, gradient=0)


def dragonling_variant(variant):
    return build_sheet(12, 12, [[companion_dragonling(i, variant) for i in range(4)]])


# ------------------------------------------------------------------ NPCs (v2)
def npc_pilgrim(frame):
    """Betender Pilger 16x20: kniet, gefaltete Hände, warmes Licht."""
    image = new_image(16, 20)
    draw = ImageDraw.Draw(image)
    robe = (96, 70, 62, 255)
    robe_l = (122, 92, 80, 255)                             # Robe-Licht
    robe_dark = (70, 50, 46, 255)
    sway = frame % 2
    rect(draw, 4, 12, 8, 8, robe)                          # kniende Robe
    rect(draw, 4, 12, 3, 2, robe_l)
    rect(draw, 4, 18, 8, 1, robe_dark)
    rect(draw, 5, 4 + sway, 6, 8, robe)                    # Oberkörper leicht wiegend
    rect(draw, 5, 4 + sway, 3, 2, robe_l)
    rect(draw, 6, 5 + sway, 4, 3, BONE)                    # Gesicht
    pixel(draw, 6, 5 + sway, (245, 238, 215, 255))         # Wangen-Licht
    rect(draw, 7, 6 + sway, 1, 1, BLACK)
    rect(draw, 9, 6 + sway, 1, 1, BLACK)
    rect(draw, 7, 8 + sway, 3, 1, (60, 44, 40, 255))       # gefaltete Hände
    rect(draw, 10, 3 + sway, 4, 5, (60, 44, 40, 255))      # Kapuze
    rect(draw, 10, 3 + sway, 2, 2, (84, 62, 56, 255))      # Kapuze-Licht
    return polish(image)


def npc_hermit(frame):
    """Eremit 16x20: hockt, dunkle Kapuze, bläuliches Licht."""
    image = new_image(16, 20)
    draw = ImageDraw.Draw(image)
    cloak = (54, 52, 70, 255)
    cloak_l = (76, 74, 96, 255)                             # Umhang-Licht
    cloak_dark = (38, 36, 52, 255)
    sway = frame % 2
    rect(draw, 3, 10, 10, 10, cloak)                       # hockende Gestalt
    rect(draw, 3, 10, 4, 2, cloak_l)
    rect(draw, 3, 18, 10, 1, cloak_dark)
    rect(draw, 5, 4 + sway, 6, 7, cloak)                   # Kopf mit Kapuze
    rect(draw, 5, 4 + sway, 3, 2, cloak_l)
    rect(draw, 6, 6 + sway, 4, 3, BLACK)
    pixel(draw, 7, 7 + sway, (150, 220, 255, 255))         # leuchtende Augen
    pixel(draw, 9, 7 + sway, (150, 220, 255, 255))
    pixel(draw, 7, 7 + sway, (220, 245, 255, 255))         # Augen-Kern
    pixel(draw, 9, 7 + sway, (220, 245, 255, 255))
    rect(draw, 1, 12 + sway, 2, 4, cloak_dark)             # Arm mit Stab
    rect(draw, 0, 4 + sway, 1, 12, WOOD)
    rect(draw, 0, 4 + sway, 1, 3, (120, 82, 58, 255))      # Stab-Licht
    return polish(image)


def npc_keeper(frame):
    """Tempelwärtin 16x22: aufrecht, wallendes Gewand, kerzenwarm."""
    image = new_image(16, 22)
    draw = ImageDraw.Draw(image)
    gown = (120, 96, 76, 255)
    gown_l = (146, 120, 96, 255)                            # Gewand-Licht
    gown_dark = (88, 68, 54, 255)
    sway = frame % 2
    rect(draw, 4, 8, 8, 14, gown)                          # wallendes Gewand
    rect(draw, 4, 8, 3, 14, gown_l)
    rect(draw, 4, 20, 8, 1, gown_dark)
    rect(draw, 5, 2 + sway, 6, 6, BONE)                    # Gesicht
    pixel(draw, 5, 2 + sway, (245, 238, 215, 255))         # Stirn-Licht
    rect(draw, 6, 4 + sway, 1, 1, BLACK)
    rect(draw, 9, 4 + sway, 1, 1, BLACK)
    rect(draw, 4, 1 + sway, 8, 3, (60, 44, 40, 255))       # Haar/Schleier
    rect(draw, 4, 1 + sway, 4, 1, (84, 62, 52, 255))       # Schleier-Licht
    rect(draw, 2, 9 + sway, 2, 6, gown)                    # Arme
    rect(draw, 12, 9 + sway, 2, 6, gown)
    rect(draw, 2, 9 + sway, 1, 6, gown_l)
    pixel(draw, 2, 15 + sway, FLAME)                       # Kerze
    pixel(draw, 13, 15 + sway, FLAME)
    pixel(draw, 2, 15 + sway, (255, 240, 180, 255))        # Flammenkern
    pixel(draw, 13, 15 + sway, (255, 240, 180, 255))
    return polish(image)


def bat(anim, frame):
    image = new_image(10, 8)
    draw = ImageDraw.Draw(image)
    body = (50, 40, 60, 255)
    body_l = (76, 62, 84, 255)                              # Fell-Licht
    if anim == "hang":
        rect(draw, 4, 1, 2, 5, body)                     # kopfüber, Flügel angelegt
        rect(draw, 3, 2, 1, 3, body)
        rect(draw, 6, 2, 1, 3, body)
        pixel(draw, 4, 1, body_l)                        # Fell-Licht oben
        pixel(draw, 4, 5, BLOOD)
        pixel(draw, 5, 5, BLOOD)
    else:
        rect(draw, 4, 3, 2, 3, body)
        pixel(draw, 4, 3, body_l)
        pixel(draw, 4, 3, BLOOD)
        pixel(draw, 5, 3, BLOOD)
        if frame == 0:
            draw.polygon([(4, 4), (0, 1), (1, 5)], fill=body)
            draw.polygon([(5, 4), (9, 1), (8, 5)], fill=body)
        else:
            draw.polygon([(4, 4), (0, 6), (2, 7)], fill=body)
            draw.polygon([(5, 4), (9, 6), (7, 7)], fill=body)
    return polish(image, light=20, dark=-10, gradient=0)


def wisp(core, glow, shape="flame", variant="normal"):
    """Begleitseele. variant: 'normal' | 'pale' (hell/kühl) | 'deep' (dunkel/warm) — nur Farbstimmung,
    die Silhouette bleibt identisch, damit der Begleiter wiedererkennbar bleibt.

    G16: 16-Bit-Anhebung — der Code zeichnet Begleiter ungetönt (Color.White, Companion.cs:64),
    also eigene Farben statt Graustufen. Vier bis fünf Tonwerte je Fassung: heller Kern, Licht oben
    links, Flacker-Reflex, Randschatten unten rechts. Nur Binnenzeichnung: Jedes Pixel wird nur
    gesetzt, wenn die Kern-Ellipse es schon füllt — Umriss bleibt pixelgenau."""
    if variant == "pale":
        core = shift(core, 60) if core[0] < 200 else core     # Richtung hell/kühl ziehen
        glow = tuple(min(255, c + 70) for c in glow[:3]) + (glow[3],)
    elif variant == "deep":
        core = shift(core, -70)
        glow = (max(30, glow[0] - 60), max(20, glow[1] - 70), max(10, glow[2] - 90), glow[3])
    light = shift(core, 55)                                    # Licht oben links
    deep = shift(core, -55)                                    # Schatten unten rechts
    spark = tuple(min(255, c + 70) for c in glow[:3]) + (255,)  # Flacker-Reflex
    frames = []
    for frame in range(4):
        image = new_image(12, 12)
        draw = ImageDraw.Draw(image)
        flicker = frame % 2
        top = 4 - flicker                                      # obere Kernzeile
        draw.ellipse([1, 2 - flicker, 10, 11], fill=glow[:3] + (110,))   # Aura (unverändert)
        draw.ellipse([3, top, 8, 9], fill=core)                           # Kern (unverändert)

        def core_pixel(x, y, color):
            """Setzt nur, wo der Kern schon gefüllt ist — die Silhouette wächst nie."""
            if image.getpixel((x, y))[3] > 0:
                pixel(draw, x, y, color)

        for x in range(3, 9):                                  # Licht oben links im Kern
            core_pixel(x, top, light)
            core_pixel(3, top + (x - 3), light)
        for x in range(5, 9):                                  # Schatten unten rechts
            core_pixel(x, 9 - (8 - x) - flicker, deep)
        core_pixel(5 - flicker, top + 2, spark)                # wandernder Glanzpunkt
        core_pixel(4 + flicker, 8 - flicker, shift(core, 30))
        if shape == "chain":
            for i in range(3):
                link = (120, 120, 130, 255)
                pixel(draw, 2 + i * 3, 10, link)
                pixel(draw, 2 + i * 3, 10, shift(link, 60) if i == 1 else link)  # Mittelglied-Licht
        elif shape == "bell":
            rect(draw, 4, 1 - flicker + 1, 4, 1, GOLD)
            pixel(draw, 6, 1 - flicker + 1, shift(GOLD, 50))   # Klöppel-Glanz
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
    for kind in ("shepherd", "mammon", "titan",
                 "tempest", "cerberus", "heresiarch", "minotaur", "geryon", "lucifer"):
        build_sheet(48, 48, [[boss(kind, "idle", i) for i in range(4)], [boss(kind, "cast", i) for i in range(4)]]).save(textures / f"boss_{kind}.png")
    build_sheet(10, 8, [[bat("hang", 0)], [bat("fly", i) for i in range(2)]]).save(textures / "prop_bat.png")
    # --- Begleitseelen (G7): je zwei Farbfassungen pale/deep neben der Normalfassung
    companions = {
        "ember": (EMBER, GOLD, "flame"),
        "tear": (WHITE, (140, 200, 255, 255), "flame"),
        "moon": (VIOLET, MANA, "flame"),
        "chain": ((200, 200, 210, 255), (120, 120, 140, 255), "chain"),
        "bell": (GOLD, (255, 230, 150, 255), "bell"),
    }
    for name, (core, glow, shape) in companions.items():
        wisp(core, glow, shape).save(textures / f"companion_{name}.png")
        for variant in ("pale", "deep"):
            wisp(core, glow, shape, variant).save(textures / f"companion_{name}_{variant}.png")
    build_sheet(12, 12, [[companion_dragonling(i) for i in range(4)]]).save(textures / "companion_dragonling.png")
    for variant in ("pale", "deep"):
        dragonling_variant(variant).save(textures / f"companion_dragonling_{variant}.png")
    _ = (math, SHADOW)  # (Importe für spätere Erweiterungen)

"""App-Icons: CirclesOfAsh.icns (macOS), CirclesOfAsh.ico (Windows) und Icon.bmp (Fenster und Dock).

Das Banner aus docs/logo.png taugt nicht als Icon – bei 16x16 wäre von einem 1280x440 breiten
Schriftzug nichts mehr zu erkennen. Stattdessen wird das Bildmotiv des Spiels gezeichnet:
der Höllentrichter: Ringe, die nach innen enger, tiefer und heißer werden. Weil jeder Ring
etwas höher sitzt als der vorige, entsteht der Eindruck eines Schlundes statt einer Zielscheibe.
Das liest sich in jeder Größe, weil es nur aus wenigen kräftigen Formen besteht.
"""
import shutil
import struct
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw

from .core import ASH, BLACK, BLOOD, CLEAR, DEEP_PURPLE, EMBER, FLAME, GOLD, ROOT

ICONS = ROOT / "build" / "icons"

# Fenster- und Dock-Symbol der LAUFENDEN App. MonoGame (SdlGameWindow, dekompiliert geprüft) sucht
# in der Programmdatei die eingebettete Ressource "Icon.bmp" und reicht sie an SDL_SetWindowIcon
# weiter. Unter macOS setzt SDL damit das Dock-Bild ([NSApp setApplicationIconImage:]) – fehlt die
# Datei, nimmt MonoGame sein eigenes Logo, und genau das stand bisher im Dock, sobald das Spiel lief.
# Die Datei liegt im Repository (anders als build/icons/), weil jeder Build sie braucht, auch
# "dotnet run" aus einem frischen Klon.
WINDOW_ICON = ROOT / "src" / "CirclesOfAsh" / "Icon.bmp"
# 256 px reichen für die größte Dock-Kachel auf Retina (128 pt) und für Cmd+Tab.
WINDOW_ICON_SIZE = 256

# macOS-Raster (Apple Human Interface Guidelines): Auf 1024 px ist die Kachel 824 px groß und
# hat rund 185 px Eckenradius, der Rest bleibt durchsichtig. Ohne diesen Rand zieht macOS das Bild
# bis an die Kante der Dock-Kachel, und das Symbol wirkt größer als alle anderen daneben.
TILE_FRACTION = 824 / 1024
CORNER_FRACTION = 185 / 824

# Die Ringe von außen nach innen: (Anteil des Radius, Farbe). Außen kalt, innen glühend.
RINGS = [
    (1.00, DEEP_PURPLE),
    (0.84, (52, 44, 66, 255)),
    (0.68, ASH),
    (0.52, (110, 60, 70, 255)),
    (0.38, BLOOD),
    (0.24, EMBER),
    (0.12, FLAME),
]

# Perspektive: Jeder innere Ring rutscht nach oben und wird flacher – so schaut man in einen
# Trichter hinein, statt auf eine flache Scheibe. 0 = keine Verkürzung, 1 = maximal.
TILT = 0.30

# macOS erwartet genau diese Kantenlängen im Iconset; @2x sind die Retina-Varianten.
ICNS_SIZES = [16, 32, 64, 128, 256, 512, 1024]
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]


def draw_icon(size):
    """Zeichnet das Icon in der gewünschten Kantenlänge. Supersampling gegen harte Treppen."""
    scale = 4 if size <= 256 else 1
    canvas = size * scale
    image = Image.new("RGBA", (canvas, canvas), BLACK)
    draw = ImageDraw.Draw(image)

    center = canvas / 2
    outer = canvas * 0.46
    for fraction, color in RINGS:
        radius = outer * fraction
        # Der Ring wird gestaucht (height) und wandert nach oben (lift) – beides umso stärker,
        # je weiter innen er liegt. Das ergibt die Tiefenwirkung.
        height = radius * (1.0 - TILT * (1.0 - fraction))
        lift = outer * TILT * (1.0 - fraction) * 0.55
        draw.ellipse([center - radius, center - height - lift, center + radius, center + height - lift], fill=color)

    # Goldener Rand außen: hebt das Icon vom dunklen Dock-Hintergrund ab.
    width = max(1, int(canvas * 0.02))
    draw.ellipse([center - outer, center - outer, center + outer, center + outer], outline=GOLD, width=width)

    if scale > 1:
        image = image.resize((size, size), Image.LANCZOS)
    return image


def draw_app_icon(size):
    """
    Das Motiv als abgerundete Kachel mit durchsichtigem Rand (macOS-Raster). Gilt für das .icns
    UND für Icon.bmp – so zeigt das Dock vor und nach dem Start dasselbe Bild, ohne Sprung.
    """
    tile = max(1, round(size * TILE_FRACTION))
    motif = draw_icon(tile)
    # Maske vierfach groß zeichnen und verkleinern: weiche statt treppiger Ecken.
    mask = Image.new("L", (tile * 4, tile * 4), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, tile * 4 - 1, tile * 4 - 1],
                                           radius=tile * 4 * CORNER_FRACTION, fill=255)
    mask = mask.resize((tile, tile), Image.LANCZOS)
    icon = Image.new("RGBA", (size, size), CLEAR)
    offset = (size - tile) // 2
    icon.paste(motif, (offset, offset), mask)
    return icon


def write_bmp(image, path):
    """
    32-Bit-BMP mit Alphakanal für SDL_LoadBMP (so lädt MonoGame das Fenstersymbol).
    Kopf: BITMAPV4HEADER mit BI_BITFIELDS und ausdrücklichen Farbmasken. Pillow schreibt RGBA nur
    mit dem alten 40-Byte-Kopf ohne Masken – dann muss SDL raten, ob das vierte Byte Alpha ist.
    """
    width, height = image.size
    # BMP speichert die Zeilen von unten nach oben; "BGRA" ist die Bytefolge der Masken unten.
    pixels = image.convert("RGBA").transpose(Image.FLIP_TOP_BOTTOM).tobytes("raw", "BGRA")
    header_size = 108                  # BITMAPV4HEADER
    data_offset = 14 + header_size     # Dateikopf (14 Byte) + Infokopf
    bi_bitfields = 3
    file_header = struct.pack("<2sIHHI", b"BM", data_offset + len(pixels), 0, 0, data_offset)
    info_header = struct.pack("<IiiHHIIiiII", header_size, width, height, 1, 32, bi_bitfields,
                              len(pixels), 2835, 2835, 0, 0)   # 2835 px/m = 72 dpi
    masks = struct.pack("<IIII", 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000)   # R, G, B, A
    color_space = struct.pack("<I", 0x73524742)   # "sRGB" (LCS_sRGB): Endpunkte und Gamma unbenutzt
    unused_endpoints_and_gamma = bytes(36 + 12)
    path.write_bytes(file_header + info_header + masks + color_space + unused_endpoints_and_gamma + pixels)


def write_ico(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    largest = draw_icon(max(ICO_SIZES))
    largest.save(path, format="ICO", sizes=[(s, s) for s in ICO_SIZES])


def write_icns(path):
    """
    Baut ein .icns. Wo `iconutil` vorhanden ist (macOS), wird es benutzt; sonst schreiben wir
    das Format selbst – ein .icns ist nur eine Liste aus (Typ, Länge, PNG-Daten).
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    # Typkürzel des icns-Formats je Kantenlänge.
    types = {16: b"icp4", 32: b"icp5", 64: b"icp6", 128: b"ic07",
             256: b"ic08", 512: b"ic09", 1024: b"ic10"}

    if shutil.which("iconutil"):
        with tempfile.TemporaryDirectory() as temporary:
            iconset = Path(temporary) / "CirclesOfAsh.iconset"
            iconset.mkdir()
            for size in (16, 32, 128, 256, 512):
                draw_app_icon(size).save(iconset / f"icon_{size}x{size}.png")
                draw_app_icon(size * 2).save(iconset / f"icon_{size}x{size}@2x.png")
            subprocess.run(["iconutil", "-c", "icns", str(iconset), "-o", str(path)], check=True)
        return

    chunks = []
    for size in ICNS_SIZES:
        buffer = tempfile.SpooledTemporaryFile()
        draw_app_icon(size).save(buffer, format="PNG")
        buffer.seek(0)
        data = buffer.read()
        chunks.append(types[size] + struct.pack(">I", len(data) + 8) + data)
    body = b"".join(chunks)
    path.write_bytes(b"icns" + struct.pack(">I", len(body) + 8) + body)


def generate(icons=ICONS):
    """Schreibt .ico und .icns nach build/icons/ und das Fenstersymbol Icon.bmp ins Projekt."""
    icons.mkdir(parents=True, exist_ok=True)
    write_ico(icons / "CirclesOfAsh.ico")
    write_icns(icons / "CirclesOfAsh.icns")
    write_bmp(draw_app_icon(WINDOW_ICON_SIZE), WINDOW_ICON)
    # Vorschau für die Dokumentation: so, wie es im Dock steht
    draw_app_icon(512).save(ROOT / "docs" / "icon-preview.png")

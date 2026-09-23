"""App-Icons für die Auslieferung: CirclesOfAsh.icns (macOS) und CirclesOfAsh.ico (Windows).

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

from .core import ASH, BLACK, BLOOD, DEEP_PURPLE, EMBER, FLAME, GOLD, ROOT

ICONS = ROOT / "build" / "icons"

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
                draw_icon(size).save(iconset / f"icon_{size}x{size}.png")
                draw_icon(size * 2).save(iconset / f"icon_{size}x{size}@2x.png")
            subprocess.run(["iconutil", "-c", "icns", str(iconset), "-o", str(path)], check=True)
        return

    chunks = []
    for size in ICNS_SIZES:
        buffer = tempfile.SpooledTemporaryFile()
        draw_icon(size).save(buffer, format="PNG")
        buffer.seek(0)
        data = buffer.read()
        chunks.append(types[size] + struct.pack(">I", len(data) + 8) + data)
    body = b"".join(chunks)
    path.write_bytes(b"icns" + struct.pack(">I", len(body) + 8) + body)


def generate(icons=ICONS):
    """Schreibt beide Icon-Dateien nach build/icons/."""
    icons.mkdir(parents=True, exist_ok=True)
    write_ico(icons / "CirclesOfAsh.ico")
    write_icns(icons / "CirclesOfAsh.icns")
    # Vorschau für die Dokumentation
    draw_icon(512).save(ROOT / "docs" / "icon-preview.png")

#!/usr/bin/env python3
"""
Erzeugt ALLE Platzhalter-Assets von "Circles of Ash" prozedural (CC0).
Die eigentlichen Generatoren liegen im Paket tools/assetgen/:
  core.py        Palette, Outline/Schattierung (polish), Sheet-Bau
  characters.py  Spieler-Ebenen für den Charakter-Editor
  creatures.py   Gegner, Bosse, Mini-Boss, Fledermäuse, Begleiter
  world.py       Tilesets je Kreis, Hintergründe, Props, Items, Runen, Effekte, Logo
  media.py       Bitmap-Fonts und Sounds
  music.py       Loopender Soundtrack je Ort
  icons.py       App-Icons (.icns/.ico) für die Auslieferung

Aufruf aus dem Repo-Root:  python tools/generate_placeholder_assets.py   (benötigt: pip install pillow)
Jede Datei darf durch echte Art ersetzt werden, solange Größe/Raster zu Content/manifest.json passen.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from assetgen import characters, creatures, icons, media, music, world  # noqa: E402
from assetgen.core import AUDIO, CONTENT, DOCS, FONTS, TEXTURES  # noqa: E402


def main():
    for folder in (TEXTURES, FONTS, AUDIO):
        folder.mkdir(parents=True, exist_ok=True)
    for stale in TEXTURES.glob("*.png"):          # alte Dateien entfernen, damit nichts Verwaistes liegen bleibt
        stale.unlink()
    characters.generate(TEXTURES)
    creatures.generate(TEXTURES)
    world.generate(TEXTURES, DOCS)
    media.generate(FONTS, AUDIO)
    music.generate(AUDIO)
    icons.generate()
    print(f"Assets erzeugt in {CONTENT}")


if __name__ == "__main__":
    main()

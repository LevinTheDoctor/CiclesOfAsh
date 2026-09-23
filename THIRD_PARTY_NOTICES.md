# Drittanbieter-Hinweise

| Komponente | Lizenz | Verwendung |
|---|---|---|
| [MonoGame](https://github.com/MonoGame/MonoGame) | Microsoft Public License (Ms-PL) | Game-Framework (NuGet) |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) | MIT | Spielstand-Datenbank (NuGet) |
| [SDL_GameControllerDB](https://github.com/gabomdq/SDL_GameControllerDB) | MIT (z.T. zlib) | `Content/gamecontrollerdb.txt` – Controller-Mapping-Datenbank; MonoGame liest sie beim Start |
| [Tiny5](https://fonts.google.com/specimen/Tiny5) | SIL Open Font License 1.1 | Fließtext-Font, gerendert nach `Content/Fonts/body.png` – Lizenz: `tools/fonts/Tiny5-OFL.txt` |
| [Jacquarda Bastarda 9](https://fonts.google.com/specimen/Jacquarda+Bastarda+9) | SIL Open Font License 1.1 | Titel-Font (gotisch), gerendert nach `Content/Fonts/title.png` – Lizenz: `tools/fonts/JacquardaBastarda9-OFL.txt` |

Alle übrigen Grafiken und Klänge in `src/CirclesOfAsh/Content` wurden von
`tools/generate_placeholder_assets.py` prozedural erzeugt und stehen unter **CC0** (Public Domain).
Das schließt ausdrücklich ein:

* Tilesets, Hintergründe, Figuren, Gegner, NPCs, Props und Effekte (`tools/assetgen/`)
* Soundeffekte und den kompletten Soundtrack `Content/Audio/music_*.wav` (`tools/assetgen/music.py`)
* die App-Icons in `build/icons/` (`tools/assetgen/icons.py`)

Die beiden Schriften oben sind die einzige Ausnahme: Sie stammen von Google Fonts und stehen unter
der OFL. Gerendert werden daraus nur Pixel-Atlanten – die TTF-Dateien selbst liegen unter
`tools/fonts/` samt Lizenztext.

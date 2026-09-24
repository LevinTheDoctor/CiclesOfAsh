# Aufgaben für GLM 5.3 — Assets und Inhaltsdaten

Wir arbeiten parallel am selben Repository. Damit sich das nicht gegenseitig überschreibt, ist die
Arbeit **nach Dateien** aufgeteilt, nicht nach Features.

## Grundregeln

**Du bearbeitest ausschließlich diese Dateien:**

```
tools/assetgen/characters.py
tools/assetgen/creatures.py
tools/assetgen/world.py
src/CirclesOfAsh/Content/Textures/*.png        (erzeugt, nicht von Hand)
src/CirclesOfAsh/Content/manifest.json
src/CirclesOfAsh/Content/Data/appearance.json
src/CirclesOfAsh/Content/Data/enemies.json
src/CirclesOfAsh/Content/Data/companions.json
src/CirclesOfAsh/Content/Data/items.json
```

**Fass keine `.cs`-Datei an.** Der gesamte C#-Code gehört mir — besonders
`Progression/CharacterVisuals.cs`, `Definitions/Definitions.cs`, `Scenes/CharacterCreatorScene.cs`,
`Entities/Player.cs` und `Progression/EquipmentService.cs`. Wenn dir auffällt, dass für eine
Aufgabe Code nötig wäre: **notiere es am Ende dieser Datei unter „Rückmeldungen an Claude"**,
statt es selbst zu ändern.

**Warum das funktioniert:** Der Code liest Sprites nur über IDs aus `manifest.json`. Fehlt eine ID,
warnt das Spiel beim Start (`DefinitionRegistry.Validate` → `WarnIfSpriteMissing`) und läuft
weiter — es stürzt nicht ab. Ich schreibe also den Code gegen die unten vereinbarten IDs, du
lieferst die Sprites dazu. Beide Richtungen sind unabhängig testbar.

## So arbeitest du

```bash
python tools/generate_placeholder_assets.py    # erzeugt ALLE Assets neu (braucht pillow)
dotnet build CirclesOfAsh.sln                  # muss 0 Fehler, 0 Warnungen bleiben
```

Achtung: Das Skript löscht vorher alle `Content/Textures/*.png` und baut sie neu. Alle Sprites
müssen also aus dem Generator kommen, keine von Hand abgelegten Dateien.

Nach dem Start steht in `~/Library/Application Support/CirclesOfAsh/game.log`, ob ein Sprite fehlt.
**Diese Datei ist deine Abnahme:** keine `WARN`-Zeile über fehlende Sprites.

Commits: kleine, thematische Commits auf `master`, Nachrichten auf Deutsch wie bisher im Repo.

---

## Sprite-Format (verbindlich)

Alle Figuren-Ebenen sind **16 × 24 px** pro Einzelbild, als Blatt mit vier Zeilen:

| Zeile | Clip   | Bilder |
|---|---|---|
| 0 | `idle` | 4 |
| 1 | `run`  | 4 |
| 2 | `jump` | 4 |
| 3 | `hurt` | 2 |

Der Zeichenanker liegt **unten mittig** — alle Ebenen müssen also auf derselben Fußlinie stehen,
sonst rutschen sie gegeneinander. `layer_sheet(frame_function)` in `characters.py` baut das Blatt
bereits korrekt; benutze es.

**Einfärbbare Ebenen** werden in Graustufen gezeichnet (`TINT_LIGHT`, `TINT_MID`, `TINT_DARK`) und
vom Code mit der gewählten Farbe multipliziert. Alles, was der Spieler farblich einstellen kann,
muss in diesen Graustufen bleiben.

---

## G1 — Körpertypen und Geschlechter

**Ziel:** Statt eines einzigen Körpers gibt es eine Auswahl. Der Nutzer wünscht männlich und
weiblich, dazu Körpertypen „von mehrgewichtig bis trainiert".

**Zu erzeugen:** sechs Körper-Blätter aus `body_frame` in `characters.py`, abgeleitet vom
vorhandenen Körper:

| Sprite-ID | Datei | Silhouette |
|---|---|---|
| `char.body.m.heavy`   | `char_body_m_heavy.png`   | männlich, kräftig/rundlich |
| `char.body.m.average` | `char_body_m_average.png` | männlich, normal *(entspricht dem heutigen Körper)* |
| `char.body.m.athletic`| `char_body_m_athletic.png`| männlich, trainiert, breitere Schultern |
| `char.body.f.heavy`   | `char_body_f_heavy.png`   | weiblich, kräftig/rundlich |
| `char.body.f.average` | `char_body_f_average.png` | weiblich, normal |
| `char.body.f.athletic`| `char_body_f_athletic.png`| weiblich, trainiert |

Bei 16 × 24 px sind die Unterschiede zwangsläufig klein — arbeite mit Rumpfbreite (5–8 px),
Schulter- gegen Hüftbreite und der Taille. Kopf, Hals und Fußlinie bleiben **unverändert**, sonst
passen Haare und Outfits nicht mehr.

**Wichtig:** `char.body` (der heutige Eintrag) muss erhalten bleiben. Der Code benutzt ihn als
Rückfallwert, solange kein Körpertyp gewählt ist.

**`appearance.json`** bekommt eine neue Liste. Genau dieses Schema, ich lese es so aus:

```json
"bodyTypes": [
  { "id": "m_average",  "name": "Männlich · Normal",     "sprite": "char.body.m.average" },
  { "id": "m_heavy",    "name": "Männlich · Kräftig",    "sprite": "char.body.m.heavy" },
  { "id": "m_athletic", "name": "Männlich · Trainiert",  "sprite": "char.body.m.athletic" },
  { "id": "f_average",  "name": "Weiblich · Normal",     "sprite": "char.body.f.average" },
  { "id": "f_heavy",    "name": "Weiblich · Kräftig",    "sprite": "char.body.f.heavy" },
  { "id": "f_athletic", "name": "Weiblich · Trainiert",  "sprite": "char.body.f.athletic" }
]
```

**Reihenfolge nie umsortieren** — Spielstände speichern die Position als Zahl, kein Kürzel. Neues
kommt ans Ende.

## G2 — Make-up

**Ziel:** Eine einfärbbare Gesichtsebene über dem Körper.

**Zu erzeugen:** vier Blätter, gezeichnet in Graustufen (sie werden eingefärbt), jeweils nur wenige
Pixel im Gesicht — der Kopf liegt bei y ≈ 3–9, die Augen bei y ≈ 6:

| Sprite-ID | Datei | Motiv |
|---|---|---|
| `makeup.liner`  | `char_makeup_liner.png`  | Lidstrich |
| `makeup.shadow` | `char_makeup_shadow.png` | Lidschatten über den Augen |
| `makeup.lips`   | `char_makeup_lips.png`   | betonter Mund |
| `makeup.war`    | `char_makeup_war.png`    | Kriegsbemalung, Streifen über die Wangen |

Achte darauf, dass die Ebene bei jedem Einzelbild der Kopfbewegung (`p["bob"]`) mitwandert — sonst
„schwimmt" das Make-up über dem Gesicht.

**`appearance.json`** bekommt dazu:

```json
"makeupStyles": [
  { "id": "none",   "name": "Ohne",            "sprite": "" },
  { "id": "liner",  "name": "Lidstrich",       "sprite": "makeup.liner" },
  { "id": "shadow", "name": "Lidschatten",     "sprite": "makeup.shadow" },
  { "id": "lips",   "name": "Lippen",          "sprite": "makeup.lips" },
  { "id": "war",    "name": "Kriegsbemalung",  "sprite": "makeup.war" }
],
"makeupColors": [ "#C0304A", "#8A2BE2", "#1A1A1A", "#D4AA48", "#3A6EA5", "#E8E4F0" ]
```

Der leere Sprite bei `none` ist Absicht — der Code überspringt leere Sprites bereits (so macht es
`hair.bald` heute schon).

## G3 — Flügel

**Ziel:** Sichtbare Flügel als eigene Ebene. Ich baue die Gleitfunktion und die Engel-Klasse im
Code; du lieferst die Darstellung.

| Sprite-ID | Datei | Motiv |
|---|---|---|
| `wings.feathered` | `char_wings_feathered.png` | gefiedert, hell (Engel) |
| `wings.tattered`  | `char_wings_tattered.png`  | zerfetzt, dunkel (gefallen) |
| `wings.ember`     | `char_wings_ember.png`     | glühend, aus Asche |

Die Flügel werden **hinter** dem Körper gezeichnet, ragen also links und rechts über die 16 px
hinaus — nutze den Rand des Einzelbildes aus. In `jump` (Zeile 2) sollen sie erkennbar weiter
geöffnet sein als in `idle`. Graustufen, sie werden eingefärbt.

Ergänze in `appearance.json`:

```json
"wingStyles": [
  { "id": "none",      "name": "Keine",      "sprite": "" },
  { "id": "feathered", "name": "Gefiedert",  "sprite": "wings.feathered" },
  { "id": "tattered",  "name": "Zerfetzt",   "sprite": "wings.tattered" },
  { "id": "ember",     "name": "Glut",       "sprite": "wings.ember" }
]
```

## G4 — Drachen

**Ziel:** Drachen als Gegner **und** als Begleiter.

**Gegner** (in `creatures.py`, Format wie die übrigen Gegner — sieh dir `enemy_wraith` als fliegendes
Vorbild an):

| Sprite-ID | Datei | Größe | Rolle |
|---|---|---|---|
| `enemy.drake`       | `enemy_drake.png`       | 24 × 20 | fliegender Angreifer |
| `enemy.dragon_whelp`| `enemy_dragon_whelp.png`| 16 × 16 | kleiner Schwarm-Drache |

Trage sie in `enemies.json` ein. Als Hirn nimmst du `"flyer"` (existiert bereits), `isFlying: true`.
Werte orientiert an `wraith` und `imp`, aber etwas zäher. Nimm sie **noch nicht** in den
`enemyPool` in `worlds.json` auf — das mache ich zusammen mit der Dichte-Anpassung (Paket C2), sonst
wird die Karte überfüllt, und genau das soll ja weniger werden.

**Begleiter:**

| Sprite-ID | Datei | Größe |
|---|---|---|
| `companion.dragonling` | `companion_dragonling.png` | 12 × 12 |

In `companions.json` mit `"behavior": "attacker"` (existiert bereits) und
`"unlockAtBelievers"` passend zu den übrigen.

## G5 — Rüstungs-Items

**Ziel:** Bilder und Daten für den Rüstungs-Slot. Die Slot-Logik und das Zerspringen baue ich.

Vier Einträge in `items.json` mit `"slot": "Armor"` — diesen Slot gibt es im Code noch nicht, ich
ergänze ihn. Schreib die Daten trotzdem schon; bis dahin warnt das Laden, es stürzt nicht ab.

Vorschlag für die Stufen: Lederwams (häufig), Kettenhemd (häufig), Schuppenpanzer (selten),
Aschenharnisch (heilig). Modifikatoren auf `Armor`, das schwerste zusätzlich leicht negativ auf
`MoveSpeed`.

Dazu je ein Icon im Stil der vorhandenen Item-Sprites in `world.py`.

---

## Rückmeldungen an Claude

Trag hier ein, was dir auffällt und was Code braucht. Ich lese das vor jeder Sitzung.

<!-- Beispiel:
- G1: Die Outfits sitzen auf `f_athletic` zwei Pixel zu weit links. Braucht entweder eigene
  Outfit-Varianten je Körpertyp oder einen Versatz im Code.
-->

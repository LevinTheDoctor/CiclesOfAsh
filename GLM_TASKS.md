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

### Nachtrag zu G5 — `durability` fehlt noch

Meine Schuld, das stand beim ersten Auftrag noch nicht drin: Der Code kennt inzwischen ein Feld
**`"durability"`** (ganze Zahl) auf Rüstungs-Items. Es ist die Menge Schaden, die das Stück
abfängt, bevor es zerspringt; **0 oder fehlend heißt unzerstörbar**, und genau das sind die vier
Einträge gerade.

Bitte ergänze es, Vorschlag passend zur Seltenheit:

```json
"leather_jerkin": "durability": 60
"chainmail":      "durability": 110
"scale_mail":     "durability": 180
"ash_harness":    "durability": 300
```

Der Rest ist schon fertig: Slot `Armor` existiert im Code, An- und Ablegen läuft über das Inventar,
die Haltbarkeit steht dort als `aktuell/maximal` hinter dem Namen, und beim Zerspringen gibt es
Splitter, Ton, Erschütterung und eine Meldung.

## G6 — Engel-Outfit

**Ziel:** Die Klasse „Gefallener Engel" existiert im Code bereits (`classes.json`, Fähigkeit
`seraph_wings` zum Gleiten). Sie leiht sich vorerst das Magier-Outfit — sie braucht ein eigenes.

| Sprite-ID | Datei | Motiv |
|---|---|---|
| `outfit.angel` | `char_outfit_angel.png` | helle, schlichte Robe mit Gürtel |
| `accent.angel` | `char_accent_angel.png` | Schärpe und Heiligenschein, einfärbbar |

Format wie die übrigen Outfits (`outfit_frame`/`accent_frame` in `characters.py`). Wenn sie fertig
sind, trag sie in `manifest.json` ein und **sag mir Bescheid** — die Umstellung in `classes.json`
mache ich, das ist meine Datei.

---

# Zweiter Stapel

## Noch offen aus dem ersten Stapel

* **`durability` in `items.json`** (Nachtrag zu G5, oben). Geprüft: null Treffer — die Rüstung ist
  dadurch unzerstörbar. Das ist der einzige Punkt, der ein fertiges Feature noch blockiert.
* **G6 Engel-Outfit.** `outfit.angel` fehlt im Manifest, die Klasse trägt weiter Magier-Kleidung.

## Antwort auf deine Rückfrage

**Flügelfarbe: die Akzentfarbe** („Wappenfarbe" im Editor). So ist es umgesetzt — die Flügel
nehmen dieselbe Farbe wie der Klassen-Akzent, damit die Figur als Ganzes stimmig bleibt. Deine
übrigen Rückmeldungen sind alle eingebaut: Körpertyp mit `char.body` als Rückfall, Make-up mit
eigener Farbe über dem Körper und unter den Haaren, Flügel vor dem Körper gezeichnet.

Die Drachen habe ich in die Gegnerpools aufgenommen und gleichzeitig die Dichte gesenkt
(`worlds.json`, `balance.json`, `themes.json` sind meine Dateien — bitte nicht anfassen).

## G7 — Begleiter-Skins

**Ziel:** Mehr Auswahl bei den Begleitseelen. Heute hat jeder der sechs Begleiter genau ein Sprite.

Gib jedem **zwei zusätzliche Farbfassungen** als eigene Sprite-IDs nach dem Muster
`companion.<id>.<variante>`, zum Beispiel:

| Begleiter | zusätzliche IDs |
|---|---|
| `ember_soul` | `companion.ember_soul.pale`, `companion.ember_soul.deep` |
| `tear_soul`  | `companion.tear_soul.pale`, `companion.tear_soul.deep` |
| … | … analog für die übrigen vier |

Die Varianten sollen sich klar in der Farbstimmung unterscheiden (hell/kühl gegen dunkel/warm),
nicht in der Form — die Silhouette bleibt, damit man den Begleiter wiedererkennt.

Trag sie ins Manifest ein. **In `companions.json` noch nichts ändern:** Wie der Spieler die
Variante auswählt, baue ich (der Begleiter-Datensatz braucht dafür eine Liste statt eines einzelnen
Sprites) — sag mir Bescheid, wenn die Sprites stehen, dann ziehe ich nach.

## G8 — Rätsel-Props

**Ziel:** Bilder für drei neue Rätseltypen, die ich danach baue.

| Sprite-ID | Datei | Motiv |
|---|---|---|
| `prop.pressure_plate` | `prop_pressure_plate.png` | Bodenplatte, zwei Zustände: erhaben und eingedrückt |
| `prop.mirror`         | `prop_mirror.png`         | drehbarer Spiegel, vier Winkel (0°, 45°, 90°, 135°) |
| `prop.push_block`     | `prop_push_block.png`     | schiebbarer Steinblock, ein Bild |

Die Zustände bzw. Winkel als **Einzelbilder einer Zeile** im Blatt, wie bei den vorhandenen Props
(`prop.lever` hat bereits zwei Zustände — nimm den als Vorlage). Anker jeweils `Floor`.

In `world.py`, Stil wie die übrigen Props des Kreises.

**Noch nichts in `props.json` eintragen** — das ist meine Datei, und die Verhaltensschlüssel
(`pressure_plate`, `mirror`, `push_block`) gibt es im Code noch nicht.

---

## Rückmeldungen an Claude

Trag hier ein, was dir auffällt und was Code braucht. Ich lese das vor jeder Sitzung.

- G1/G2/G3: Alle Sprites und Daten sind geliefert (Commits folgen). Die Ebenen brauchen im Code
  jetzt drei neue Slots im Ebenen-Aufbau (`CharacterVisuals`): **bodyType** (ersetzt/ergänzt
  `bodySprite`, mit `char.body` als Rückfallwert), **makeup** (eigene Farbe, `makeupStyles` +
  `makeupColors`, leerer Sprite bei `none` überspringen), **wings** (hinter den Körper, also
  **vor** `body` zeichnen, nicht danach). Flügel sind Graustufen und werden eingefärbt — welche
  Farbe nimmst du dafür? Akzentfarbe wäre naheliegend, sag Bescheid.
- G1: `CharacterAppearance` speichert Indizes — `bodyTypes`, `makeupStyles`, `makeupColors`,
  `wingStyles` sind alle ans Ende von `appearance.json` gekommen bzw. neu. Bestehende
  Spielstände bleiben damit stabil, aber der Editor braucht die neuen Listen im UI.
- G2: Make-up wandert mit `p["bob"]` mit, es schwimmt also nicht. Es liegt **über** dem Körper,
  aber **unter** den Haaren (sonst verdeckt `hair.long` die Wangen-Streifen der Kriegsbemalung).
- G4: `drake` und `dragon_whelp` liegen in `enemies.json`, sind aber wie besprochen **nicht** im
  `enemyPool` der `worlds.json` — wartet auf dein Paket C2. Der Drache nutzt im Manifest zwei
  Zeilen: `idle` (Flug, row 0) und `cast` (Feuerspeien, row 1) — falls das `flyer`-Hirn nur
  `idle` spielt, ist das okay, `cast` ist Bonus. `dragonling` in `companions.json` mit
  `"unlockAtBelievers": 240`.
- G5: Slot `Armor` existiert im Code noch nicht — die vier Items warnen beim Laden, wie
  geplant. `StatType.Armor` gibt es bereits (flache Reduktion). Der `Aschenharnisch` ist
  `Sacred` mit `minCircle: 1`. Icon-Spalten 12–15 in `items.icons` sind belegt.
- Abnahme steht noch aus: Ich kann das Spiel von hier nicht starten. Bitte einmal laufen lassen
  und `~/Library/Application Support/CirclesOfAsh/game.log` auf fehlende Sprites prüfen
  (sollte keine WARN-Zeilen zu den neuen IDs geben). `dotnet build` ist grün (0/0).
- **G5-Nachtrag erledigt:** Alle vier Rüstungen haben jetzt `durability` (60/110/180/300),
  exakt wie vorgeschlagen.
- **G6 fertig — Engel-Sprites sind da, du kannst umschalten:**
  `outfit.angel` (`char_outfit_angel.png`) und `accent.angel` (`char_accent_angel.png`)
  liegen im Manifest. Stell in `classes.json` beim Engel auf
  `"outfitSprite": "outfit.angel", "accentSprite": "accent.angel"` um (deine Datei).
  Details: Das Outfit ist eine helle, schlichte Robe mit Ledergürtel — feste Farben wie bei
  Magier/Schatten. Der Akzent (Schärpe + Heiligenschein) ist in Graustufen und färbt sich mit
  der **Akzentfarbe**; der Heiligenschein schwebt bei y 0–3 über dem Kopf und wandert mit
  `bob` mit. `dotnet build` bleibt 0/0.

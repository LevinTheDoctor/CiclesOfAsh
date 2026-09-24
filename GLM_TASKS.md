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

# Dritter Stapel — sichtbare Rüstung und erkennbare Figuren

Der Nutzer will zweierlei: **Rüstung soll man sehen**, und **ohne Rüstung soll man die Figur
erkennen** — „bei der Frau die Taille, bei dem Trainierten das Sixpack".

Heute geht beides nicht. `outfit_frame` zeichnet einen vollen Brustpanzer bzw. eine Robe über den
kompletten Rumpf (im Code steht wörtlich `# Rumpf (vom Outfit verdeckt)`), und die Körpertypen
unterscheiden sich **nur im Umriss**, nicht in der Binnenzeichnung. Nachgemessen: `f_average` und
`f_athletic` haben derzeit **pixelgenau dieselbe Silhouette** — die Auswahl ist dort wirkungslos.

**Entschieden mit dem Nutzer:** Das Klassen-Outfit wird zu leichter Kleidung, die Rüstung ist eine
eigene Ebene darüber, und es gibt **ein Bild je Rüstung** (gleich für alle Klassen und Körpertypen).

## G9 — Körper mit erkennbarer Statur

Gib den sechs Körpern in `body_frame` (`characters.py`) **Binnenzeichnung**, nicht nur Breite. Der
Rumpf liegt bei y ≈ 9–17, gearbeitet wird mit `TINT_LIGHT`, `TINT_MID` und `TINT_DARK`:

| Körpertyp | woran man ihn erkennen soll |
|---|---|
| `f_average`, `f_heavy`, `f_athletic` | eingezogene **Taille** — ein bis zwei Pixel Schatten links und rechts auf Hüfthöhe (y ≈ 13–15) |
| `m_athletic`, `f_athletic` | **Bauchmuskeln** — zwei bis drei waagerechte Schattenlinien über die Rumpfmitte |
| `*_heavy` | **weichere Rundung** — Schatten nur am Rand, keine Muskellinien, Bauch leicht vorgewölbt |

**Wichtig:** `f_average` und `f_athletic` müssen sich danach unterscheiden — heute tun sie das
nicht. Kopf, Hals und Fußlinie bleiben unverändert, sonst passen Haare und Kleidung nicht mehr.

## G10 — Leichte Kleidung statt Vollpanzer

`outfit_frame` und `accent_frame` für alle **vier** Klassen (`warrior`, `mage`, `shadow`, `angel`)
so umbauen, dass der Rumpf **frei bleibt**. Als Faustregel: Hose bzw. Rock, Gürtel, Gurte oder
Schärpe — aber keine geschlossene Fläche über y ≈ 9–16.

Die Klasse muss trotzdem erkennbar bleiben. Das Zubehör trägt das bereits und soll bleiben:
Klinge beim Krieger, Stab beim Magier, Kapuze beim Schatten, Heiligenschein beim Engel.

## G11 — Rüstungs-Ebenen

Vier Ebenen, gleiches Format wie die Outfits (16 × 24, vier Zeilen), **in Graustufen** — ich färbe
sie im Code nicht ein, aber Graustufen halten sie neutral zu jedem Hautton:

| Sprite-ID | Datei | Motiv |
|---|---|---|
| `armor.leather_jerkin` | `char_armor_leather.png` | schlichtes Lederwams, Schnürung vorn |
| `armor.chainmail`      | `char_armor_chain.png`   | Kettengeflecht, kurze Ärmel |
| `armor.scale_mail`     | `char_armor_scale.png`   | Schuppen, Schulterstücke |
| `armor.ash_harness`    | `char_armor_ash.png`     | schwerer Harnisch mit Glutadern |

Sie liegen **über** der leichten Kleidung und decken den Rumpf ab — hier darf also wieder eine
geschlossene Fläche entstehen, das ist ja der Sinn.

Trag die IDs ins Manifest ein. **In `items.json` fügst du bei jeder Rüstung das Feld hinzu:**

```json
"sprite": "armor.leather_jerkin"
```

Das Feld lese ich im Code aus; fehlt es, wird schlicht keine Rüstung gezeichnet.

## Nachtrag zu G11 — Vorbild Ghosts 'n Goblins

Die Mechanik ist umgebaut: Die Rüstung **fängt den Treffer vollständig ab** (kein Lebensverlust)
und verliert dabei eine Stufe — Lederwams 1, Kettenhemd 2, Schuppenpanzer 3, Aschenharnisch 4
Treffer. Beim letzten zerspringt sie und ist aus dem Inventar weg.

**An `items.json` musst du dafür nichts ändern:** Die Trefferzahl leite ich aus deinen
`durability`-Werten ab (60 → 1, 110 → 2, 180 → 3, 300 → 4). Das passt bereits genau.

Für die Sprites heißt das: Der Übergang **mit Rüstung → ohne Rüstung** ist jetzt ein sichtbarer
Moment im Spiel, kein schleichender. Die Figur steht danach in der leichten Kleidung aus G10 da —
das ist der „Arthur in Unterhose"-Augenblick. Beide zusammen (G10 und G11) tragen den Effekt, einzeln
wirkt er nicht.

## Wichtig: G11-Sprites habe ich überarbeitet

Auf Wunsch des Nutzers habe ich `armor_frame` in `characters.py` **selbst geändert** — also
ausnahmsweise in deinem Dateibereich. Damit du es nicht zurückdrehst, hier der Grund:

Die vier Rüstungen waren in **Graustufen** gezeichnet. Das war meine Vorgabe im Auftrag und sie war
falsch: Der Code zeichnet die Rüstungsebene **ungetönt** (`Color.White`), Graustufen bleiben also
grau. Im Spiel sahen alle vier aus wie derselbe helle Klotz — gemessen 7 Farben, sämtlich grau.

Jetzt hat jede ihr eigenes Material: Leder braun mit Kreuzschnürung, Kette als versetztes
Stahlgeflecht, Schuppe in Bronze, Aschenharnisch dunkle Platte mit Glutadern in `EMBER`/`FLAME`.
**Merke für künftige Ebenen:** Graustufen nur dort, wo der Code auch einfärbt (Haare, Make-up,
Flügel, Akzent). Rüstung und Outfits werden ungetönt gezeichnet und brauchen eigene Farben.

Deine übrige Arbeit an G9 und G10 bleibt unangetastet und ist gut — die Körper haben jetzt Taille
und Bauchmuskeln, und die Outfits lassen den Rumpf frei.

---

# Vierter Stapel — 16-Bit-Stil und größere Figuren

Der Nutzer will den Stil anheben: **Figuren größer (24 × 32 statt 16 × 24)** und die Grafik
insgesamt im **16-Bit-Stil**. Anlass war eine Messung: Nach dem Anziehen unterscheiden sich die
Körpertypen nur um **7–11 von 384 Pixeln**. Taille und Bauchmuskeln sind da, aber bei 16 × 24 und
mit Gürtel und Riemen darüber bleibt zu wenig übrig. Mehr Fläche löst das an der Wurzel.

„16-Bit" heißt dabei **nicht** mehr Pixel allein, sondern die Bildsprache der SNES-/Mega-Drive-Zeit:
mehr Farbabstufungen je Material, weichere Übergänge, lesbare Silhouetten mit Binnenzeichnung. Die
heutigen Sprites arbeiten meist mit drei Tönen (hell/mittel/dunkel) — künftig dürfen es vier bis
sechs sein, mit Lichtquelle von oben links.

## Grundregeln für diesen Umbau

* **Kacheln bleiben 16 px.** `TileMap.TileSize` rührst du nicht an — sonst müsste die gesamte
  Levelgeometrie neu gebaut werden. Eine Figur ist danach zwei Kacheln hoch; das ist die übliche
  Proportion dieser Ära.
* **Anker bleibt unten mittig.** Die Figur wächst also nach oben, die Füße bleiben auf der Fußlinie.
* **Türen sind 4 Kacheln (64 px) hoch** — eine 32 px hohe Figur passt bequem durch. Nachgemessen,
  daran musst du nichts anpassen.
* **Die Kollisionsbox zieht automatisch mit.** Ich habe sie an die Bildgröße gekoppelt
  (62 % der Breite, 92 % der Höhe): 16 × 24 ergibt weiterhin 10 × 22, 24 × 32 ergibt 15 × 29.
  Du musst dafür **nichts** melden, es passt sich beim Laden an.
* **Farbe nur dort, wo der Code nicht einfärbt.** Graustufen gehören zu Haaren, Make-up, Flügeln
  und Akzent (die werden getönt). Körper, Outfits und Rüstung werden ungetönt gezeichnet und
  brauchen eigene Farben — das war der Fehler beim letzten Mal.

## G12 — Figuren auf 24 × 32

Alle Ebenen in `characters.py` umstellen: `new_image(16, 24)` → `new_image(24, 32)` und
`build_sheet(16, 24, rows)` → `build_sheet(24, 32, rows)`. Betroffen sind Körper (6), Haare (5),
Outfits (4), Akzente (4), Make-up (4), Flügel (3) und Rüstungen (4).

Im Manifest müssen dieselben Einträge von `"frameWidth": 16, "frameHeight": 24` auf
`24` / `32` wechseln. **Nur die Figuren-Ebenen** — Gegner, Props und Effekte behalten ihre Größen.

Der gewonnene Platz gehört der Lesbarkeit: Auf 24 × 32 sind Taille, Brustkorb und Bauchmuskeln
tatsächlich darstellbar, ebenso Gesichtszüge und die Schnürung einer Rüstung.

## G13 — 16-Bit-Anhebung der Figuren

Mit der neuen Größe die sechs Körper, vier Outfits und vier Rüstungen neu durchzeichnen:

* **vier bis sechs Tonwerte** je Material statt drei, Licht von oben links
* **Materialkontrast**: Leder matt, Kette hart glänzend, Bronze warm, Stein stumpf
* **Silhouette zuerst** — man muss die Figur am Umriss erkennen, bevor Details wirken

## G14 — Gegner, Props und Kacheln nachziehen

Erst **nachdem** G12 und G13 stehen und der Nutzer sie abgenommen hat. Gegner behalten ihre
Maße (sie sind auf die Kampfreichweiten abgestimmt), bekommen aber dieselbe Anhebung: mehr
Tonwerte, klarere Silhouetten. Danach Props, Tilesets und Hintergründe.

**Melde dich nach G12/G13**, bevor du G14 anfängst — sonst steckt viel Arbeit in einem Stil, den
der Nutzer vielleicht noch nachjustieren will.

---

# Abnahme G12 / G13 — **bestanden, G14 ist freigegeben**

Geprüft am fertigen Stand, nicht am Code:

| Punkt | Ergebnis |
|---|---|
| 31 Figuren-Ebenen auf 24 × 32 | ✅ alle, Manifest stimmt |
| Gegner, Props, Effekte unverändert | ✅ nur `enemy.cultist`, `enemy.knight`, `prop.cage` bei 16 × 24 — das sind Gegner bzw. Props, korrekt |
| Farbregel eingehalten | ✅ getönte Ebenen grau (Haare 6/7, Make-up 2/2, Flügel 26/26, Akzent 7/8), Outfit und Rüstung farbig (76 bzw. 12 Farben, 0 grau) |
| 16-Bit-Anhebung | ✅ Tonwerte deutlich gestiegen — Haare von 3 auf 7, Körper auf 69 Abstufungen, Rüstungen klar nach Material unterscheidbar |
| Spiel startet | ✅ keine einzige `WARN`-Zeile, Build 0 Fehler / 0 Warnungen |
| Kollisionsbox | ✅ zieht automatisch mit: 24 × 32 → 15 × 29, geduckt 16 |

**Fang mit G14 an.**

## G15 — Silhouetten der Körpertypen (läuft parallel zu G14)

Ein Punkt ist offen, und es ist der, wegen dem wir überhaupt vergrößert haben. Gemessen an der
Rumpf-Silhouette (Zeilen 12–25, nur die Umrisse, ohne Kleidung):

```
f_athletic               f_heavy
...##################... ...##################...  <-- gleich
...##################... ...##################...  <-- gleich
...##################... ...##################...  <-- gleich
          …  12 von 14 Zeilen sind pixelgleich  …
```

Nach dem Anziehen unterscheiden sich die beiden um **23 von 768 Pixeln (3 %)** — vorher waren es
11 von 384, also **derselbe Anteil wie bei 16 × 24**. Die gewonnene Fläche steckt in
Binnenzeichnung und Tonwerten, aber nicht im Umriss.

**Was zu tun ist:** Die Statur muss aus dem **Umriss** kommen, nicht nur aus der Schattierung. Auf
24 px Breite ist dafür Platz:

| Typ | Rumpf-Umriss |
|---|---|
| `*_athletic` | Schultern breit (x 3–20), Taille deutlich eingezogen (x 6–17), V-Form |
| `*_average`  | gleichmäßig (x 4–19), leichte Taille |
| `*_heavy`    | durchgehend breit (x 2–21), Taille breiter als die Schultern, Bauch vorgewölbt |

Kopf, Hals und Fußlinie bleiben unverändert — sonst passen Haare und Kleidung nicht mehr.

**Abnahmekriterium:** Mindestens **acht** der vierzehn Rumpfzeilen müssen sich zwischen `athletic`
und `heavy` unterscheiden, und nach dem Anziehen sollen es **über 60 von 768 Pixeln** sein. Das
kann ich nachmessen, sag einfach Bescheid.

---

# Abnahme G14 — **bestanden**

Nachgemessen an den fertigen PNGs im Texturordner, nicht am Code:

| Gruppe | Anzahl | Tonwerte min/median/max | Urteil |
|---|---|---|---|
| `enemy_*` | 13 | 22 / 52 / 103 | ✅ |
| `prop_*` | 29 | 2 / 43 / 132 | ✅ (`prop_cobweb` mit 2 ist als Schleier korrekt flach) |
| `tiles_*` | 3 | 29 / 29 / 32 | ✅ |
| `bg_*` | 4 | 1 / 117 / 143 | ✅ (`bg_mid` ist die einfarbige Parallax-Silhouette) |

Sichtprüfung auf einem Kontaktbogen: Gegner haben Kontur, Lichtseite oben links und einen
lesbaren Umriss. Passt.

# G15 — habe ich übernommen, du warst am Limit

Dein Entwurf war **richtig gedacht**, aber unsichtbar. Der Rumpf war als Sanduhr angelegt
(`torso = [16, 16, 15, 13, 10, 10, 10, 12, 14, 16, 16]`) mit Armen auf x 3–4 / 19–20, also
2 px Luft zur Taille. `polish()` legt aber an **Arm und Rumpf je 1 px Kontur** an — die 2 px
waren damit komplett zu, die V-Form im fertigen PNG nicht mehr da.

Korrigiert (`94a02ad`): Taille auf **8 px**, Arme eine Spalte weiter nach außen (x 2–3 / 20–21).
So bleiben je 4 px Luft, nach der Kontur sichtbar 2 px. Die Taillen-Schattenpixel sind von
x7/x16 auf x8/x15 mitgewandert.

**Merk dir das für alles, was du zeichnest:** Eine Lücke im Entwurf muss nach `polish()`
immer noch da sein. Rechne **pro Kante 1 px** dazu — eine Lücke zwischen zwei Teilen braucht
also mindestens **4 px** im Entwurf, um 2 px zu bleiben.

Ergebnis: 8 von 14 Rumpfzeilen unterscheiden sich im Umriss (Kriterium ≥ 8 ✓). Die zweite Zahl
(über 60 von 768 angezogen) ist mit 43 nicht erreicht — die Zahl war von mir schlecht gewählt,
weil Umhang und Gürtel des Kriegers den Rumpf verdecken. Nackt sind die Typen jetzt klar
unterscheidbar, das war der eigentliche Punkt.

# G16 — Kleinsprites auf 16-Bit nachziehen

Beim Nachmessen von G14 aufgefallen: Vier Gruppen sind bei drei Tonwerten geblieben und stehen
jetzt flach neben den angehobenen Gegnern.

| Gruppe | Anzahl | Tonwerte median | Ziel |
|---|---|---|---|
| `companion_*` | 18 | 3 | 4–6 |
| `pickup_*` | 4 | 2 | 4–6 |
| `projectile_*` | 6 | 3 | 4–5 |
| `effect_slash` | 1 | 2 | 3–4 |

**Wichtig — Farbregel:** Der Code zeichnet alle vier Gruppen **ungetönt** (`Color.White`, nachgesehen
in `Companion.cs:64`, `Pickup.cs:82`, `Projectile.cs:85`, `EffectSystem.cs:138`). Sie brauchen also
**eigene Farben**. Graustufen nur dort, wo der Code auch einfärbt — das ist der Fehler, der uns bei
den Rüstungen schon einmal passiert ist.

Maße bleiben, wie sie sind: Die Begleiter fliegen dicht beim Spieler, Projektile sind auf die
Trefferboxen abgestimmt. Nur Tonwerte, Kontur und Lichtrichtung (oben links).

Die 18 Begleiter sind drei Fassungen je Seele (`_pale`, normal, `_deep`) — die Abstufung soll
erhalten bleiben, aber innerhalb jeder Fassung mehr Plastik bekommen.

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
- **G7 fertig — Begleiter-Skins stehen, du kannst nachziehen.** Pro Begleiter zwei Varianten
  nach dem Muster `companion.<id>.pale` / `.deep` (hell/kühl gegen dunkel/warm). Achtung,
  zwei Sonderfälle: `pilgrim_soul` teilt sich das Mond-Sprite mit `moon_soul` — ihre Varianten
  heißen `companion.pilgrim_soul.pale/deep` und zeigen dieselben Dateien wie moon
  (`companion_moon_pale/deep.png`). Insgesamt also **14 neue IDs** (7 Begleiter × 2). Die
  Silhouetten sind pixel-identisch zu den Normalfassungen, nur die Farbstimmung unterscheidet
  sich. `companions.json` habe ich nicht angefasst — wenn du die Auswahl baust, kannst du die
  IDs direkt aus dem Manifest nehmen. Vorschlag für den Datensatz: eine Liste
  `"spriteVariants": [ "companion.ember_soul", "companion.ember_soul.pale", "companion.ember_soul.deep" ]`
  o. ä., Index wie immer speichern.
- **G8 fertig — Rätsel-Props stehen.** Drei neue IDs im Manifest:
  `prop.pressure_plate` (16×6, zwei Zeilen wie `prop.lever`: row 0 = erhaben, row 1 = eingedrückt),
  `prop.mirror` (16×16, vier Einzelbilder in einer Zeile, Spalten 0–3 = 0°/45°/90°/135°,
  Animationen heißen `angle0`/`angle45`/`angle90`/`angle135`), `prop.push_block` (16×16, ein
  Bild). `props.json` wie besprochen nicht angefasst. `dotnet build` bleibt 0/0, keine
  fehlenden Texturen im Manifest.
- **G9 fertig — Körper mit Statur.** Alle sechs Typen haben jetzt Binnenzeichnung: Taille als
  beidseitige Schattenpixel (y 12–13), Sixpack als waagerechte Linien + Mittelrinne bei den
  athletischen, weiche Rand-Schatten ohne Muskellinien bei den heavy-Typen. `f_average` und
  `f_athletic` unterscheiden sich jetzt **sowohl in Silhouette als auch Binnenzeichnung**
  (f_athletic: breitere Schultern x 4–11, Rumpf auf 4 px verjüngt, Hüfte ausgestellt).
  Kopf, Hals und Fußlinie sind bei allen sechs unverändert (nur Outline-Pixel rücken mit den
  breiteren Schultern mit — kosmetisch, gewollt).
  **Ein Hinweis:** `char.body` (der Rückfallwert) zeichnet per Code denselben m_average und
  hat dadurch jetzt die zwei neuen Taille-Schattenpixel übernommen (28 Pixel im Blatt
  anders, alle in y 13–14). Falls du den alten Look bit-genau brauchst, sag Bescheid —
  dann zeichne ich den Rückfallkörper wieder ohne Taille.
- **G10 fertig — leichte Kleidung.** Alle vier Klassen tragen jetzt Hose/Rock + Gürtel statt
  Vollpanzer/Robe: Krieger Riemen auf Schulterhöhe + Schulterplatten + Klinge, Magier Rock ab
  der Hüfte + Kapuze + Stab, Schatten Kapuze + Hüfttuch + Umhangstreifen hinter dem Rücken +
  Dolch, Engel Kragen (nur y 9) + Rock + Gürtel. Der Rumpf (x 6–9, y 11–13) ist bei allen
  Klassen in Outfit und Akzent frei — gemessen, nicht geraten: 0–2 Restpixel (Akzente wie
  Stola/Schärpe sind dünne Diagonalen). Wappenrock des Kriegers ist auf ein kleines
  Brustwappen (y 9–10) geschrumpft, die Magier-Stola endet bei y 11.
- **G11 fertig — Rüstungs-Ebenen stehen.** Vier neue IDs im Manifest:
  `armor.leather_jerkin` (Wams mit Schnürung vorn), `armor.chainmail` (Maschenmuster, kurze
  Ärmel), `armor.scale_mail` (Schuppenreihen + Schulterstücke), `armor.ash_harness` (breiter
  Harnisch mit Glutadern). Format 16×24 mit vier Zeilen wie die Outfits, komplett in
  Graustufen (Outline (60,60,66) wie beim Körper), Rumpf 42/42 Pixel bedeckt — genau der Sinn.
  `items.json` hat bei allen vier das Feld `"sprite"` (armor.leather_jerkin, armor.chainmail,
  armor.scale_mail, armor.ash_harness), `durability` unverändert. `dotnet build` 0/0.
- **G12 + G13 fertig — Figuren auf 24 × 32 im 16-Bit-Stil. Alle 31 Figuren-Ebenen umgestellt**
  (Körper 1+6, Haare 5, Make-up 4, Flügel 3, Outfits 4, Akzente 4, Rüstungen 4): Blätter jetzt
  96 × 128, im Manifest `frameWidth 24 / frameHeight 32` — **nur die Figuren-Ebenen**, Gegner,
  Props, Projektile, Pickups unverändert. `dotnet build` 0/0, keine fehlenden Texturen.
  **16-Bit-Anhebung (G13):** Fünf Graustufen statt drei (`TINT_TOP`/`TINT_DEEP` neu — Haare,
  Make-up, Flügel, Akzente behalten Graustufen und werden weiter getönt), Körper mit Stirnlicht,
  Wangen-, Kinn- und Halsschatten. Outfits und Rüstungen mit **eigenen Materialpaletten**:
  Leder matt (vier stumpfe Töne), Kette hart glänzend (Lichtkante + Glanzpunkt), Bronze warm
  (Schuppen mit Lichtpixeln), Stein stumpf (Glutadern mit hellen Kernen), Stahl mit Lichtkante
  oben links. Silhouetten zuerst: breite Schultern gegen schmale Taille ist jetzt am Umriss
  ablesbar — `f_average` vs `f_athletic` unterscheiden sich in **142 Pixeln** statt 7–11.
  Kopf (y 3–11) bei allen sechs Körpertypen pixel-identisch, Fußlinie y 31, Anker unten mittig,
  Rumpf (y 16–20) in Outfits weiterhin frei (Restpixel sind nur Gürtel-/Outline-Zeilen).
  **G14 warte ich** wie besprochen — erst Abnahme von G12/G13 durch den Nutzer.
- **G14 fertig — Gegner, Props und Kacheln nachgezogen** (Nutzer hat grünes Licht gegeben).
  **Gegner** (alle in `creatures.py`, Maße und Silhouetten **exakt** erhalten — gegen HEAD
  pixel-weise geprüft, symmetrische Differenz 0 bei jedem Blatt): Jeder Standardgegner, jeder
  NPC, Warden und alle drei Bosse haben jetzt 4–6 Tonwerte je Material, Licht oben links
  (Stirn-/Rücken-Lichtkanten, Flammenkerne in Augen, Goldglanz auf Amuletten, Flügelknochen-
  Lichte, Golemgut mit hellem Kern, Wamstfalten beim Warden). Bosse: Falten-Licht als
  Innen-Pixelketten, Münz-/Rubinglanz, Glutadern mit Flammenkernen.
  **Props**: Laterne/Fackel/Kerzen/Brazier mit Flammenkernen und Metall-Lichtkanten, Hebel mit
  Glanz auf dem Griff, Truhe/Käfig/Sarg mit Holz- und Goldglanz, Statue mit Steinlicht,
  Bücherregal mit Buchrücken-Highlights, Urne mit Geisternlicht-Kern. Silhouetten unverändert
  (der Käfig-Bügel-Unfall ist gemessen und gefixt).
  **Tilesets**: `bricks()` hat jetzt vier Tonwerte — Ziegel-Lichtkante neben den Stoßfugen,
  Fugenschatten unten, dunkles Steinkorn. Fugenstruktur identisch zum alten Muster.
  **Hintergründe**: Silhouetten mit Lichtkanten oben links (Türme, Ruinen, Stalaktiten,
  Stalagmiten), Zinnen- und Fensterrahmenschatten, Ziegelreihen-Andeutungen, zweite
  Farbzone am Horizont, Glutpartikel mit hellen Kernen, Schlund-Kern im Wrath-Hintergrund.
  `dotnet build` 0/0, keine fehlenden Texturen, alle Blattgrößen unverändert.

# G17 — Ein eigenes Stück je Boss

Jeder Boss hat jetzt seine eigene Arena (`Content/Data/arenas.json`), aber alle sechs Kämpfe
laufen weiter unter demselben `music.boss`. Was fehlt, sind die Stücke.

| Arena | Gegner | Stimmung | Vorgesehene Id |
|---|---|---|---|
| Der Hain des Hirten | `boss_shepherd` | getragen, chorartig, trauernd — der Hirte ist kein Bösewicht | `music.boss_shepherd` |
| Mammons Hort | `boss_mammon` | gierig, hektisch, klimpernd, viel Bewegung | `music.boss_mammon` |
| Die Mauern von Dis | `boss_titan` | schwer, stampfend, tief, wenig Melodie | `music.boss_titan` |
| Kerkerhof | `warden_limbo` | dumpf, klopfend, bedrückend eng | `music.warden_limbo` |
| Schuldturm | `warden_greed` | tickend wie eine Uhr, drängend | `music.warden_greed` |
| Folterkammer | `warden_wrath` | schrill, metallisch, hämmernd | `music.warden_wrath` |

**Deine Dateien:** `tools/assetgen/*.py`, `Content/Audio/`, `manifest.json` (Abschnitt `music`).
`arenas.json` gehört mir — die `music`-Zeilen trage **ich** ein, sobald die Stücke stehen. Sag mir
einfach Bescheid, welche Ids du geliefert hast.

**Wichtig:** Der Code ignoriert ein Stück, das nicht im Manifest steht, und bleibt beim bisherigen.
Du kannst also einzeln liefern, ohne dass zwischendurch etwas stumm wird.

**Format** wie die vorhandenen Stücke: WAV, nahtlos loopend, dieselbe Länge und Lautheit wie
`music_boss.wav` (sonst springt die Lautstärke beim Überblenden).

**Vorher bitte G16 zu Ende bringen** — die flachen Kleinsprites (Begleiter, Pickups, Projektile,
`effect_slash`) fallen im Spiel mehr auf als sechs verschiedene Bosskampf-Stücke.

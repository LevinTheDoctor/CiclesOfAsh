# Arbeitsstand

Diese Datei führt die offenen Arbeitspakete. **Sie wird gelöscht, sobald alles erledigt ist** —
der Zustand gehört dann in die Git-Historie, nicht ins Repo.

Zustände: `[ ]` offen · `[~]` in Arbeit · `[x]` erledigt

Reihenfolge mit dem Nutzer abgestimmt: **Paket A zuerst** (Kampf & Bewegung), danach B, C, D.

---

## Paket A — Kampf & Bewegung

Heute kämpft das Spiel fast vollständig **automatisch**: Alle Schadensfähigkeiten stehen in
`abilities.json` auf `"activation": "Auto"` und feuern selbstständig, sobald Abklingzeit und Mana
passen (`Entities/Player.cs`, `UpdateAbilities`). Nur Dash, Tarnung und die Ultimate sind manuell.
Genau das soll sich für Bosskämpfe ändern.

### [ ] A1 Ducken

**Problem:** Es gibt kein Ducken. `GameAction.Down` dient nur zum Durchfallen durch Plattformen und
als Dash-Richtung; eine verkleinerte Trefferbox gibt es nirgends.

**Lösung:** Neuer Zustand in `Player.UpdateMovement` — isoliert wie Dash und Schwimmen. Geduckt:
halbe Trefferbox-Höhe, langsameres Laufen, kein Sprung. Aufstehen wird verhindert, solange über dem
Kopf eine massive Kachel liegt (sonst steckt man in der Decke). Sprite bekommt einen „crouch"-Clip.

**Prüfen:** Unter eine zwei Kacheln hohe Öffnung ducken und hindurchlaufen; unter einer niedrigen
Decke darf man nicht aufstehen können.

### [ ] A2 Manuelles Moveset im Bosskampf

**Problem:** Im Bosskampf schaut man dem Automatismus zu.

**Lösung:** Ein manueller Nahkampf als eigene `IAbilityBehavior`-Variante (das Muster trägt das
bereits — `Abilities/BuiltInAbilities.cs`, `MeleeArcAbility` ist die Vorlage):

* **Angriff** — Kombo aus drei Schlägen mit Zeitfenster, jeder mit eigener Reichweite.
* **Blocken** — hält Schaden ab, kostet Ausdauer; im richtigen Moment gedrückt = Parade, die den
  Boss kurz öffnet.
* **Drehsprung auf `W`** — Sprungangriff, der im Fallen Schaden im Umkreis macht.

Der Automatik-Modus bleibt erhalten; im Optionsmenü (Reiter Gameplay) wählbar, ob Bosskämpfe
manuell oder automatisch laufen. So bleibt das bisherige Spielgefühl für alle, die es mögen.

**Prüfen:** Bosskampf mit Tastatur und Controller; Ausdauerleiste sichtbar; Parade erkennbar.

### [ ] A3 Rüstung: anlegen, ablegen, zerspringen

**Problem:** Es gibt nur passive Statboni in den Slots Lampe, Amulett, Ring
(`Definitions.ItemSlot`). Keine Rüstung, keine Haltbarkeit.

**Lösung:** Neuer Slot `Armor`. `EquipmentService` ist bereits generisch über `ItemSlot`, das
An-/Ablegen funktioniert also ohne Änderung. Neu ist die **Haltbarkeit**: Rüstung nimmt bei jedem
Treffer Schaden, zerspringt bei 0 mit Splitter-Effekt und Ton und ist für den Rest des Laufs weg.
`RunState.Equipped` ist heute `Dictionary<ItemSlot, string>` und braucht dafür einen Zusatzwert.

**Prüfen:** Rüstung anlegen, Treffer kassieren, Haltbarkeit sinkt sichtbar, bei 0 zerspringt sie.

### [ ] A4 Flügel als Funktion und Engel-Klasse

**Problem:** Es gibt drei Klassen und keine Flugfähigkeit.

**Lösung:** Flügel als sichtbare Ebene (`CharacterVisuals` nimmt beliebig viele Ebenen) **und** als
Fähigkeit: Gleiten (Fallgeschwindigkeit gedeckelt, solange Sprung gehalten wird) plus begrenzter
Auftrieb. Neue Klasse „Engel" in `classes.json` mit den Flügeln als Startfähigkeit.

**Prüfen:** Als Engel von einem Podest gleiten; Flügel sind im Charakter-Editor sichtbar.

---

## Paket B — Charakter & Begleiter

### [ ] B1 Geschlecht und Körpertypen

Heute gibt es genau **einen** Körper-Sprite (`appearance.json`, `bodySprite`). Geplant: Auswahl
männlich/weiblich und Körpertyp von mehrgewichtig bis trainiert, also mehrere Körper-Sprite-Sätze.
Achtung: `CharacterAppearance` speichert **Indizes**, keine Ids — neue Einträge müssen ans Ende der
Listen, sonst verschieben sich bestehende Spielstände.

### [ ] B2 Make-up

Weitere Farbebene über dem Gesicht, technisch wie Haare und Akzent. Der Ebenen-Aufbau in
`Progression/CharacterVisuals.cs` trägt das ohne Umbau.

### [ ] B3 Mehr Begleiter-Skins und sprechende Begleiter

Sechs Begleiter existieren, der Dialog `pet_talk` ebenfalls. Geplant: mehr Skins und echte
Gespräche, die von selbst beginnen — auch im Tutorial (siehe D).

### [ ] B4 Drachen

Als Begleiter und als Gegner. Gegner-Seite über `enemies.json` plus ein Flug-Hirn; Begleiter-Seite
über ein neues `ICompanionBehavior`.

---

## Paket C — Welt & Inhalte

### [ ] C1 Mehr Rätseltypen

Heute drei (`levers`, `rune_order`, `braziers`, alle in `Puzzles/Puzzles.cs`). Geplant:
Druckplatten, Spiegel für Lichtstrahlen, Gewichts-/Schieberätsel. Neuer Typ = neue `IPuzzle`-Klasse,
eine Registry-Zeile und ein Zweig in `DungeonGenerator.PlacePuzzle`.

### [ ] C2 Mehr Gegner, aber weniger überfüllte Karte

Zwei gegenläufige Wünsche, deshalb getrennt: **mehr Gegner-Arten** in den Pool (`worlds.json`),
gleichzeitig **weniger Deko und weniger gleichzeitige Gegner** pro Raum. Stellschrauben stehen alle
in `balance.json` (`baseWaveSize`, `maxAliveEnemies`, `waveGrowth`) und im Generator
(`PlaceChests`, Deko-Dichte je Thema).

### [ ] C3 Eigenes Thema je Boss und Mini-Boss

Jeder Kreis hat bereits Tileset, Hintergrund, Musik und Lichtstimmung. Was fehlt, ist eine eigene
**Arena** je Boss: bisher ist der Thronsaal ein generischer `RoomType.Boss`-Raum. Geplant: Arena-
Vorlage je Boss (Geometrie, Props, Beleuchtung) und ein eigenes Stück je Mini-Boss.

---

## Paket D — Tutorial (optional spielbar)

Es gibt **kein** Tutorial; nur `tips.json` auf den Ladebildschirmen. Geplant: ein optionaler
Einstieg, den man im Titel oder beim ersten Start wählen kann — geführt von einem sprechenden
Begleiter (siehe B3), der Bewegung, Ducken, Kampf, Block und Interaktion erklärt. Überspringbar und
jederzeit wiederholbar.

---

## Erledigt in dieser Sitzung

* [x] Optionen aus **jedem** Pausenmenü erreichbar (`73531d8`) — im Verlies fehlte der Eintrag.
* [x] Stille Fehlerpfade in `AudioService` und `MusicSystem` sichtbar gemacht (`73531d8`).
  Die „kein Ton"-Meldung war damit in einer Minute geklärt: Das Spiel spielt ab
  (Zustand `Playing`, Lautstärke 0,74), die Ursache lag am Ausgabegerät des Rechners.

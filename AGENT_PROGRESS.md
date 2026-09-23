# Arbeitsstand

Diese Datei führt die offenen Arbeitspakete. **Sie wird gelöscht, sobald alles erledigt ist** —
der Zustand gehört dann in die Git-Historie, nicht ins Repo.

Zustände: `[ ]` offen · `[~]` in Arbeit · `[x]` erledigt (mit Commit-Kürzel)

---

## Paket 1 — Spielfehler

Alle acht Punkte laufen auf dasselbe Grundproblem zu: Eine Arena gilt nur dann als geschafft, wenn
**kein Gegner mehr lebt** (`WaveDirector.cs:69`), und nur `CompleteArena` öffnet die versiegelten
Türen wieder. Es gibt keinen Notausgang. Jeder Gegner, der lebt aber nicht erreichbar oder nicht
auffindbar ist, sperrt den Spieler dauerhaft ein.

### [x] 1.1 Gegner ihrem Ereignis zuordnen

**Problem:** `DungeonWorld.AliveEnemyCount` zählt Gegner global über den ganzen Dungeon. Arena
(`WaveDirector.Update`) und Rescue-Ereignis (`UpdateRescueEvent`) warten beide auf denselben
Zähler. Lebt irgendwo noch eine Rescue-Wache, wird keine Arena je fertig — und die Türen bleiben zu.

**Lösung:** Jeder Gegner bekommt beim Spawnen ein Besitzer-Kürzel (`Enemy.Owner`, Muster wie
`Npc.Tag`). `WaveDirector` übergibt die Id des Arenaraums, `StartRescueFight` übergibt `"rescue"`.
Neu: `AliveEnemyCount(string owner)`; beide Systeme fragen nur noch ihren eigenen Besitzer ab.

**Prüfen:** Rescue-Kampf auslösen, Wachen am Leben lassen, Raum verlassen, eine Arena leerräumen —
die Türen müssen sich öffnen.

**Erledigt.** `Enemy.Owner` + `DungeonWorld.AliveEnemyCountOf(owner)`; `RoomNode.OwnerKey` liefert
das Kürzel. Alle sechs Spawn-Stellen (Welle, Boss, Boss-Diener, Kerker-Wächter, Kerker-Wachen,
Rescue-Wachen) setzen ihn; Arena und Rettung zählen nur noch sich selbst.

### [x] 1.2 Flieger in der Arena halten

**Problem:** `Enemy.Update` überspringt für fliegende Gegner jede Kachelkollision („Geister schweben
durch Wände"). Das gilt auch für die versiegelten Türkacheln. `wraith` und `imp` können aus der
Arena fliegen — auch durch einen kräftigen Rückstoß — und sind draußen unerreichbar, leben aber.

**Lösung:** Solange eine Arena versiegelt ist, wird die Position aller ihrer Gegner auf die
Raumgrenzen geklemmt. Das Durchschweben durch Wände innerhalb des Raums bleibt erhalten.

**Prüfen:** Flieger am Rand der Arena mit einem Dash-Angriff wegstoßen — er darf den Raum nicht
verlassen.

**Erledigt.** `Enemy.ConfineToArena` klemmt fliegende Gegner auf `ActiveArena.PixelBounds`, solange
der eigene Besitzer dort kämpft; `WaveDirector.ActiveArena` legt den versiegelten Raum offen.
Innen bleibt das Durchschweben erhalten, an den Grenzen wird die Achsengeschwindigkeit genullt.

### [x] 1.3 Den Egel auffindbar machen

**Problem:** `leech` benutzt das `ambusher`-Hirn und bewegt sich getarnt **überhaupt nicht**, bis
der Spieler auf 60 px herankommt. Er steht im normalen Gegnerpool. Liegt er in einer Ecke, an der
man nicht vorbeiläuft, sinkt der Zähler nie auf 0. Das ist die wahrscheinlichste Ursache des
gemeldeten Fehlers „Welle besiegt und nichts passiert".

**Lösung:** Ist er der letzte lebende Gegner seines Besitzers, weckt er sich selbst und geht auf den
Spieler zu. Zusätzlich zeigt die HUD die Zahl der verbliebenen Gegner, damit sichtbar ist, dass
überhaupt noch etwas lebt.

**Prüfen:** Arena mit Egel leerräumen, ohne in seine Ecke zu laufen — er muss von selbst kommen.

**Erledigt.** `AmbusherBrain`: Auslöseabstand 3000 px statt 60 px, sobald
`AliveEnemyCountOf(owner) <= 1`. HUD: „Verdammte: N" unter dem Wellenstatus, solange ein Kampf
läuft und Gegner übrig sind.

### [x] 1.4 Notausgang gegen Einmauern

**Problem:** Auch mit 1.1 bis 1.3 kann ein unvorhergesehener Fall den Spieler einsperren. Es gibt
keine Rückfallebene.

**Lösung:** Ein Wächter im `WaveDirector`: Passiert in einer Arena 45 Sekunden lang nichts (kein
Spawn, kein Tod), werden die verbliebenen Gegner entfernt, die Arena abgeschlossen und eine Zeile
ins Log geschrieben. Bewusst *auffällig* im Log — der Wächter soll eine verbleibende Ursache
sichtbar machen, nicht still überdecken.

**Prüfen:** Log nach längeren Testläufen auf die Meldung durchsehen.

**Erledigt.** `WaveDirector.CheckStalemate` misst die Gesundheitssumme des Kampfes
(`DungeonWorld.ThreatOf(owner)`) statt nur den Zähler: Jeder Spawn erhöht sie, jeder Treffer senkt
sie — ein laufender, nur langsamer Boss-Kampf löst den Wächter also nicht aus. Stillstand über
45 s → Gegner entfernen, Arena abschließen, `Log.Warn("NOTAUSGANG: …")`.

### [x] 1.5 Spawnpunkte gegen die Geometrie prüfen

**Problem:** `SpawnWaveEnemy` würfelt eine Position und prüft nur den Abstand zum Spieler, nie ob
dort eine Wand ist. Dieselbe Lücke haben Prison-Wachen, Boss-Spawn und Rescue-Wachen. Ein Gegner
kann in Geometrie stecken bleiben.

**Lösung:** Gemeinsame Hilfsfunktion in `DungeonWorld`, die eine Kandidatenposition gegen die
`TileMap` prüft (dieselbe Idee wie `WarnIfSpawnBlocked` in `HubScene`, aber wiederverwendbar). Alle
vier Spawn-Stellen benutzen sie; schlägt sie fehl, wird auf die Bodenmitte des Raums zurückgefallen.

**Prüfen:** Log auf Meldungen über verworfene Spawnpunkte ansehen.

**Erledigt.** `DungeonWorld.SafeSpawnBottomCenter(definition, candidate, room)` + private
`IsBodyBlocked`: prüft den vollständigen Körper-AABB gegen blockierende Kacheln, Rückfall auf die
Bodenmitte des Raums. Verdrahtet an Wellen-Spawn, Boss-Spawn, Kerker-Wächter/Wachen und
Rescue-Wachen. Jeder Verwurf wird ins Log geschrieben.

### [x] 1.6 NPC-Bewegung mit Physik

**Problem:** `Npc.Update` ist die einzige Bewegungslogik im Spiel ohne Physik — keine Schwerkraft,
keine Kollision, keine Kartengrenze. Die befreite Seele läuft auf konstanter Höhe stur in
X-Richtung und damit durch Wände aus der Karte heraus.

**Lösung:** Dasselbe Muster wie `Enemy.Update`: Schwerkraft, dann
`TilePhysics.MoveAndCollide(this, world.Map, …)`. Bleibt sie an einer Wand stehen, darf sie
springen, damit sie nicht dauerhaft hängt.

**Prüfen:** Eine Seele befreien und zum Ausgang begleiten — sie muss auf dem Boden laufen und die
Karte nicht verlassen.

**Erledigt.** `Npc.Update` setzt `Velocity.X` (nur bei aktiver Flucht) und läuft danach durch
`TilePhysics.MoveAndCollide` — Schwerkraft, Kachelkollision, One-Way-Plattformen. Hängt sie an
einer Wand, hüpft sie (240 px). `UpdateHub` (Tempel) bleibt bewusst ohne Physik.

### [x] 1.7 Tempel: Brett und Schrein freistellen

**Problem:** Der Pilger steht exakt auf dem Missionsbrett (beide bei x = 72) und wird **nach** dem
Brett gezeichnet, verdeckt es also. Die NPC-Schleife überschreibt außerdem den Hotspot
bedingungslos — am Brett stehend öffnet Interagieren den NPC-Dialog statt das Brett. Nebenbefund:
`OpenNpcDialog` benutzt für jeden NPC die feste Dialog-Id der Tempelwärtin.

**Lösung:** Pilger und Eremit auf den Tempelboden versetzen, die Podeste bleiben Brett und Schrein
vorbehalten. Hotspot-Erkennung wählt den **nächstgelegenen** Kandidaten statt des letzten Treffers.
`npc.Definition.DialogId` statt der festen Id. Beschriftungen an den Stationen, damit man sie
findet.

**Prüfen:** Im Tempel Brett und Schrein sehen und öffnen können; Pilger und Eremit führen ihren
eigenen Dialog.

**Erledigt.** Pilger/Eremit stehen jetzt auf dem Tempelboden (Zeile `HubHeightTiles - 2`) unter
ihren Podesten. `UpdateHotspots` sammelt alle Kandidaten und nimmt den nächstgelegenen innerhalb
28 px (Reichweite bleibt, Überschneidung aufgelöst). `OpenNpcDialog` nimmt
`npc.Definition.DialogId`; der Prompt nennt den NPC-Namen. Brett und Schrein tragen kleine
Beschriftungen.

### [x] 1.8 Erzeugung prüfbar machen

**Problem:** Der Nutzer beschreibt die Dungeon-Erzeugung als „komisch"; die Beispiel-Layouts sind
auffällig linear. Ob tatsächlich Räume unerreichbar erzeugt werden, ist bisher nicht messbar.

**Lösung:** Erst messen, nicht raten. Nach `DungeonGenerator.Generate` eine Erreichbarkeitsprüfung
(Flutfüllung vom Spielerstart über begehbare Kacheln, Sprunghöhe ≈ 96 px). Sie meldet ins Log, wenn
Siegeltor, Arena, Kerker oder Schatzraum nicht erreichbar sind.

**Prüfen:** Mehrere Seeds starten und das Log auswerten. Schlägt die Prüfung regelmäßig an, wird
der Generator selbst überarbeitet — das ist dann ein eigenes Paket.

**Erledigt.** `DungeonReachability.Check` (Flutfüllung: horizontal, Sprung ≤ 6 Kacheln hoch,
beliebig tief Fall, Schachtspalten) läuft in `DungeonScene` direkt nach `Generate` und meldet
unerreichbare Pflichträume per `Log.Warn`. Schatzräume gelten als erreichbar, sobald ihre
rissige Dash-Sperre betreten werden kann (die Wand ist zwei Kacheln dick — die Prüfung schreitet
durch die Cracked-Kacheln). Messergebnis Seed-Sweep (`dotnet run --project tools/SeedSweep`):
**1600 Dungeons über 400 Seeds, 0 Befunde.** Die Linearität der Beispiel-Layouts ist also kein
Reichweitenfehler; der Generator selbst braucht (noch) kein eigenes Paket.

---

## Paket 2 — Ein Build-Skript für alle Systeme

**Problem:** Es gibt kein Skript, das für das gerade laufende System das passende Paket baut.
`build/publish.sh` hat `win-x64` als Vorgabe, egal worauf es läuft; `publish-windows.ps1` ist fest
auf Windows; `macos-app.sh` baut nur macOS-Bündel. Für Linux existiert überhaupt kein Bündel.

**Lösung:** Ein `build/build.sh`, das über `uname -s`/`uname -m` den Runtime Identifier bestimmt,
für macOS das vorhandene `macos-app.sh` aufruft (keine Logik doppeln), für Linux zusätzlich
Startskript und `.desktop`-Datei erzeugt und für Windows den Publish fährt. `publish.sh` bleibt für
ausdrückliche Cross-Builds.

**Prüfen:** `./build/build.sh` ohne Argumente auf diesem Mac ausführen — es muss ohne Nachfrage ein
lauffähiges `CirclesOfAsh.app` erzeugen.

**Erledigt.** `build/build.sh` erkennt Darwin/Linux/MSYS × amd64/arm64 und leitet den RID ab.
macOS delegiert an `macos-app.sh`; Linux bekommt `CirclesOfAsh.sh` (relativer Wrapper) und
`circlesofash.desktop` (Menü-Eintrag); Windows fährt den Publish. Verifiziert auf diesem Mac:
Bündel entsteht ohne Nachfrage, das Spiel startet (Log: „Daten geladen …"). `publish.sh` für
Cross-Builds unangetastet.

---

## Paket 3 — Controller-Unterstützung über die SDL-Datenbank

**Problem:** Erkannt werden Controller nur über Textbausteine im Gerätenamen, und die Tastenbelegung
selbst ist fest im Code verdrahtet (`InputState._bindings`). Unbekannte Pads fallen auf das
Xbox-Profil zurück oder werden von SDL gar nicht als Controller erkannt.

**Lösung:** Geprüft: MonoGame 3.8.2 sucht beim Start selbst eine `gamecontrollerdb.txt` im
Programmverzeichnis (`InitDatabase` in `MonoGame.Framework.dll`). Es genügt also, die Datei aus dem
SDL_GameControllerDB-Projekt mitzuliefern und über die `.csproj` ins Ausgabeverzeichnis kopieren
zu lassen — damit werden nahezu alle handelsüblichen Controller korrekt belegt. Dazu weitere
Beschriftungsprofile in `controllers.json` (8BitDo, Logitech, generische DirectInput-Pads) und ein
Eintrag in `THIRD_PARTY_NOTICES.md`.

**Prüfen:** Mit verschiedenen Controllern starten und im Log den erkannten Namen und das gewählte
Profil ablesen.

**Erledigt.** `Content/gamecontrollerdb.txt` (SDL_GameControllerDB-Stand, 2.288 Zeilen: 322 macOS-,
869 Windows-, 744 Linux-Mappings, u. a. 317× 8BitDo) wird über die bestehende
`Content\**`-Kopierregel automatisch neben die Binary gelegt — MonoGame 3.8.2 lädt sie beim Start
selbst (in der DLL verifiziert). `controllers.json` um 8BitDo- und Logitech-Profile ergänzt;
`THIRD_PARTY_NOTICES.md` nennt Quelle und Lizenz. Die physische Belegung (`InputState._bindings`)
bleibt davon unberührt — sie ist Paket 4/Roadmap (frei belegbare Tasten).

---

## Paket 4 — Optionsmenü mit Reitern

**Problem:** `SettingsScene` ist eine flache Liste aus elf Zeilen. Es gibt keine Kategorie und
keinen Platz für Steuerungs-Einstellungen.

**Lösung:** Vier Reiter — Bildschirm · Audio · Steuerung · Gameplay. Links/Rechts wechselt den
Reiter, Hoch/Runter die Zeile, Werte über die Schultertasten. Der Reiter „Steuerung" zeigt den
erkannten Controller und ist später der Ort für frei belegbare Tasten
(`Content/Data/input.json`, bisher nur Roadmap).

**Prüfen:** Jeden Reiter mit Tastatur **und** Controller durchsteuern; alle Werte müssen einen
Neustart überleben.

**Erledigt (Code).** `SettingsScene` in vier Reiter gegliedert: Bildschirm (3 Zeilen), Audio (3),
Steuerung (3: Controller-Name, Profil, Belegungsstatus), Gameplay (4). Links/Rechts (Tastatur
Pfeile, Pad-Stick/D-Pad) wechselt den Reiter, Hoch/Runter die Zeile, Q/E bzw. X/Y ändern Werte,
Esc verlässt und speichert. `InputState.CurrentPadName` und public
`GameContext.ResolveControllerLabels` speisen den Steuerungs-Reiter. Spielstart verifiziert;
**manuell zu prüfen:** Reiter-Durchsteuerung mit Pad + Persistenz über Neustart (SQLite liegt in
`~/Library/Application Support/CirclesOfAsh/save.db`).

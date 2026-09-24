# Arbeitsstand

Diese Datei führt die offenen Arbeitspakete. **Sie wird gelöscht, sobald alles erledigt ist** —
der Zustand gehört dann in die Git-Historie, nicht ins Repo.

Zustände: `[ ]` offen · `[~]` in Arbeit · `[x]` erledigt

Reihenfolge mit dem Nutzer abgestimmt: **Paket A zuerst** (Kampf & Bewegung), danach B, C, D.

**Arbeitsteilung mit GLM 5.3:** Aufgeteilt nach *Dateien*, nicht nach Features — siehe
[GLM_TASKS.md](GLM_TASKS.md). GLM liefert Sprites und Inhaltsdaten (`tools/assetgen/*.py`,
`Content/Textures/`, `manifest.json`, `appearance.json`, `enemies.json`, `companions.json`,
`items.json`), Claude schreibt den gesamten C#-Code. Das geht parallel, weil der Code Sprites nur
über IDs anspricht und ein fehlendes Sprite beim Start nur eine Warnung erzeugt statt eines
Absturzes.

---

## Paket A — Kampf & Bewegung

Heute kämpft das Spiel fast vollständig **automatisch**: Alle Schadensfähigkeiten stehen in
`abilities.json` auf `"activation": "Auto"` und feuern selbstständig, sobald Abklingzeit und Mana
passen (`Entities/Player.cs`, `UpdateAbilities`). Nur Dash, Tarnung und die Ultimate sind manuell.
Genau das soll sich für Bosskämpfe ändern.

### [x] A1 Ducken

**Problem:** Es gibt kein Ducken. `GameAction.Down` dient nur zum Durchfallen durch Plattformen und
als Dash-Richtung; eine verkleinerte Trefferbox gibt es nirgends.

**Lösung:** Neuer Zustand in `Player.UpdateMovement` — isoliert wie Dash und Schwimmen. Geduckt:
halbe Trefferbox-Höhe, langsameres Laufen, kein Sprung. Aufstehen wird verhindert, solange über dem
Kopf eine massive Kachel liegt (sonst steckt man in der Decke). Sprite bekommt einen „crouch"-Clip.

**Prüfen:** Unter eine zwei Kacheln hohe Öffnung ducken und hindurchlaufen; unter einer niedrigen
Decke darf man nicht aufstehen können.

**Erledigt.** Trefferbox 22 → 12 px (Füße bleiben stehen), Tempo 45 %, kein Sprung, Aufstehen nur
bei freier Höhe über `TilePhysics.IsBlocked`. Durchfallen durch Plattformen bleibt möglich. Solange
es keine eigenen Hock-Sprites gibt, wird die Figur gestaucht gezeichnet — dafür nehmen
`AnimationPlayer` und `LayeredSprite` jetzt eine getrennte X/Y-Skalierung.

### [x] A2 Manueller Nahkampf

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

**Erledigt.** Angriff `J`/RT, Block `K`/LT, Drehsprung `W`. Kombo aus drei Schlägen (×1,0 / ×1,15 /
×1,6) im 0,55-s-Fenster; Block kostet Ausdauer und lässt ein Viertel durch, in den ersten 0,22 s
ist es eine Parade (kein Schaden, Angreifer wird zurückgestoßen). Drehsprung schlägt beim Aufkommen
im Umkreis zu. Ausdauerleiste im HUD unter Leben und Mana.

**Nachgetragen** (`28dd8ec`): Der Umschalter steht jetzt im Reiter Gameplay — „Bosskämpfe: Selbst
kämpfen / Automatik hilft". Auf „Selbst kämpfen" schweigen die automatischen Fähigkeiten, solange
ein Boss oder Mini-Boss lebt; außerhalb des Bosskampfs bleibt die Automatik an, sonst würde das
Grundspiel zum Dauergeklicke. Standard bleibt „Automatik hilft".

### [x] A3 Rüstung: anlegen, ablegen, zerspringen

**Problem:** Es gibt nur passive Statboni in den Slots Lampe, Amulett, Ring
(`Definitions.ItemSlot`). Keine Rüstung, keine Haltbarkeit.

**Lösung:** Neuer Slot `Armor`. `EquipmentService` ist bereits generisch über `ItemSlot`, das
An-/Ablegen funktioniert also ohne Änderung. Neu ist die **Haltbarkeit**: Rüstung nimmt bei jedem
Treffer Schaden, zerspringt bei 0 mit Splitter-Effekt und Ton und ist für den Rest des Laufs weg.
`RunState.Equipped` ist heute `Dictionary<ItemSlot, string>` und braucht dafür einen Zusatzwert.

**Prüfen:** Rüstung anlegen, Treffer kassieren, Haltbarkeit sinkt sichtbar, bei 0 zerspringt sie.

**Erledigt.** Slot `Armor`, Feld `Durability` auf `ItemDefinition`, `RunState.ArmorDurability`.
`EquipmentService.DamageArmor` zieht bei jedem Treffer ab und meldet das Zerspringen; die Rüstung
ist dann abgelegt **und** aus dem Inventar verschwunden. Ab- und wieder Anlegen setzt die
Haltbarkeit zurück — sonst könnte man Schaden durch Aus- und Einpacken heilen. Gespeichert in
`run_profile`, also ohne Migration. Im Inventar steht `aktuell/maximal` hinter dem Namen.

**Umgebaut nach Ghosts 'n Goblins.** Die Rüstung hat anfangs gar nicht geschützt: `TakeDamage`
lief zuerst, die Rüstung litt nur zusätzlich mit — eine zweite Lebensleiste statt eines Schildes.
Jetzt **fängt sie den Treffer vollständig ab** und verliert eine Stufe (Leder 1, Kette 2, Schuppe 3,
Asche 4 Treffer, abgeleitet aus `durability`). Beim letzten Treffer zerspringt sie in drei
gestaffelten Partikelschüben und ist aus dem Inventar weg. Wichtig dabei: ein
Unverwundbarkeitsfenster nach jedem abgefangenen Treffer — sonst nähme **ein** Gegnerkontakt der
Reihe nach alle Stufen mit.

**Nachgezogen:** `durability`-Werte von GLM geliefert. Rüstung ist jetzt außerdem **sichtbar** —
`ItemDefinition.Sprite` trägt die Ebene, `CharacterVisuals` zeichnet sie über der Kleidung, und
`Player.RefreshAppearance` baut die Ebenen neu, wenn sich die Rüstung ändert (An-/Ablegen im
Inventar, Zerspringen im Kampf).

**Offen bei GLM (Aufträge G9–G11):** Körper mit erkennbarer Statur (Taille, Bauchmuskeln —
`f_average` und `f_athletic` sind derzeit pixelgleich), leichte Kleidung statt Vollpanzer, und die
vier Rüstungs-Sprites. Bis dahin gibt es kein Bild zu zeichnen, die Figur sieht aus wie bisher.

### [x] A4 Flügel als Funktion und Engel-Klasse

**Problem:** Es gibt drei Klassen und keine Flugfähigkeit.

**Lösung:** Flügel als sichtbare Ebene (`CharacterVisuals` nimmt beliebig viele Ebenen) **und** als
Fähigkeit: Gleiten (Fallgeschwindigkeit gedeckelt, solange Sprung gehalten wird) plus begrenzter
Auftrieb. Neue Klasse „Engel" in `classes.json` mit den Flügeln als Startfähigkeit.

**Prüfen:** Als Engel von einem Podest gleiten; Flügel sind im Charakter-Editor sichtbar.

**Erledigt.** Neue Fähigkeit `seraph_wings` (Verhalten `glide`, passiv): Im Fallen die Sprungtaste
halten deckelt die Sinkgeschwindigkeit auf 55 px/s und senkt die Schwerkraft. Vierte Klasse
„Gefallener Engel" mit `holy_bolt` und `seraph_wings` als Startfähigkeiten. Die sichtbaren Flügel
sind davon getrennt — reine Aussehens-Ebene, jede Klasse kann sie tragen.

**Offen:** Eigene Engel-Sprites (Auftrag G6). Bis dahin leiht sich die Klasse das Magier-Outfit,
damit es keine Warnung über fehlende Sprites gibt.

---

## Paket B — Charakter & Begleiter

### [x] B1 Geschlecht und Körpertypen

Heute gibt es genau **einen** Körper-Sprite (`appearance.json`, `bodySprite`). Geplant: Auswahl
männlich/weiblich und Körpertyp von mehrgewichtig bis trainiert, also mehrere Körper-Sprite-Sätze.
Achtung: `CharacterAppearance` speichert **Indizes**, keine Ids — neue Einträge müssen ans Ende der
Listen, sonst verschieben sich bestehende Spielstände.

**Code erledigt.** `AppearanceDefinition` kennt `BodyTypes`, `MakeupStyles`, `MakeupColors` und
`WingStyles`; `CharacterAppearance` hat vier neue Felder mit Standardwerten (alte Spielstände
bleiben gültig, `run_profile` ist Schlüssel/Wert und braucht keine Migration). `CharacterVisuals`
zeichnet Flügel hinter der Figur, dann Körpertyp, Make-up unter den Haaren. Der Charakter-Editor
blendet die neuen Zeilen nur ein, wenn `appearance.json` auch Auswahlmöglichkeiten liefert.

**Sprites geliefert** (GLM, G1–G3 und G9): sechs Körper mit Binnenzeichnung — Taille bei den
weiblichen, Bauchmuskeln bei den trainierten, weiche Rundung bei den kräftigen.

**Bekannte Einschränkung:** Nachgemessen unterscheiden sich die Körpertypen **nach dem Anziehen**
nur um 7–11 von 384 Pixeln. Bei 16 × 24 und mit Gürtel und Riemen darüber bleibt zu wenig übrig.
Genau das ist der Anlass für Paket E.

### [x] B2 Make-up

Weitere Farbebene über dem Gesicht, technisch wie Haare und Akzent. Der Ebenen-Aufbau in
`Progression/CharacterVisuals.cs` trägt das ohne Umbau.

**Erledigt.** Vier Stile (Lidstrich, Lidschatten, Lippen, Kriegsbemalung) plus eigene Farbliste,
gezeichnet über dem Körper und **unter** den Haaren.

### [x] B3 Mehr Begleiter-Skins und sprechende Begleiter — `dfd7b3f`

**Skins.** GLM hatte 18 Sprites geliefert, erreichbar waren davon **sechs** — die Varianten lagen
ungenutzt im Manifest. `Companions/CompanionSkins.cs` löst sie über die Namensregel
`companion.<id>.pale` / `.deep` auf: eine neue Fassung braucht nur einen Manifest-Eintrag, keinen
Code. Alle sieben Begleiter haben jetzt drei Fassungen (`pilgrim_soul` teilt sich die Sprites mit
`moon_soul`), macht **21 wählbare Erscheinungen**.

Gewechselt wird im Tempel über die Dialogwahl **„Gestalt wechseln"**; die Seele nimmt die neue
Gestalt sofort an. Die Wahl hält über Läufe hinweg — Migration **V4** (Spalte `skin` in `pets`,
Default 0, ältere Spielstände sehen unverändert aus, am echten Spielstand durchgelaufen).

**Sprechende Begleiter.** Ein Zwischenruf hält das Spiel bewusst **nicht** an — ein Dialogfenster
mitten im Kampf wäre unerträglich. Stattdessen eine Sprechblase über der Seele, die von selbst
verschwindet (7 s Sperre zwischen zwei Rufen, 4,5 s Standzeit).

Zehn Auslöser, jeder mit genau einer Aufrufstelle im Code:

| Auslöser | wo |
|---|---|
| `run_start` | `DungeonScene.OnEnter` |
| `room_cleared` / `boss_defeated` | `DungeonWorld.OnArenaCleared` |
| `low_health` | `DungeonWorld.WatchPlayerHealth` (nur beim Überschreiten der 30 %) |
| `boss_start` | `DungeonWorld.SpawnEnemy` |
| `armor_shattered` | `Player.OnArmorHit` |
| `collectible_found` | `DungeonWorld.CollectItem` |
| `first_crouch` / `first_block` | `Player.UpdateCrouch` / `UpdateMeleeCombat` |
| `hub_idle` | `HubScene.UpdateIdleChatter` (nach 8 s Ruhe) |

Die Zeilen stehen in `Content/Data/chatter.json` (35 Stück) und lassen sich nach Begleiter oder
Verhalten filtern — jede Seele hat eine eigene Stimme, ohne dass die Datei in Varianten zerfällt.
Ein Tippfehler im Filter fällt beim Start auf (`DefinitionRegistry.Validate`), statt die Zeile
lautlos nie zu erreichen. Die Auslöser `first_crouch` / `first_block` sind schon auf das Tutorial
(Paket D) hin angelegt.

**Nicht beurteilt:** Ob die Seelen zu viel oder zu wenig reden, zeigt erst ein Durchgang. Die
Sperren stehen als Konstanten in `CompanionChatter.cs`, die Zeilen in der JSON.

### [x] B4 Drachen

Als Begleiter und als Gegner. Gegner-Seite über `enemies.json` plus ein Flug-Hirn; Begleiter-Seite
über ein neues `ICompanionBehavior`.

**Erledigt.** GLM lieferte Sprites und Daten, ich habe `drake` und `dragon_whelp` gestaffelt in die
Gegnerpools aufgenommen (Limbus nur das Junge, Zorn den Aschdrachen mit Gewicht 3). Der Begleiter
`dragonling` war über `companions.json` schon freischaltbar.

---

## Paket C — Welt & Inhalte

### [x] C1 Mehr Rätseltypen — `bd64ddc`

Aus drei sind **fünf** geworden. GLM hatte die Props in G8 geliefert — Druckplatte, Spiegel in vier
Stellungen, Schiebeblock lagen fertig im Manifest, waren aber an nichts angeschlossen.

**`weights`** — Drei Druckplatten müssen **gleichzeitig** beschwert sein, es gibt aber nur zwei
Schiebeblöcke: auf der dritten muss der Spieler selbst stehen bleiben. Die Platte meldet jeden
Zustandswechsel, auch das Hochkommen — sonst könnte man sie nacheinander abklappern.

Der Block rutscht auf Tastendruck eine Kachel weiter, statt sich mit dem Körper schieben zu lassen.
Mit echter Kollision würde der Spieler bei jedem Sprung daran hängenbleiben. Nach dem Rutschen
fällt er so weit, wie der Boden es zulässt — sonst schwebt er über einer Grube und das Rätsel wäre
unlösbar, ohne dass man es sieht.

**`mirrors`** — Lichtstrahl vom Leuchter zum Standbild lenken. Diagonal gestellt lenkt der Spiegel
um 90°, flach lässt er einen Strahl seiner eigenen Richtung durch und blockt den querlaufenden.
Einer der drei drehbaren steht deshalb bewusst im Weg und muss flach gedreht werden. Drei feste
Spiegel hängen hoch an der Wand und zeigen den gedachten Weg.

Beim Nachrechnen fiel eine **triviale Lösung** auf: alle drei flach gestellt, und der Strahl lief
einfach die Bodenreihe entlang ins Ziel. Der feste Sperrspiegel in Spalte 16 liegt genau in dem
Stück Bodenreihe, das der gedachte Weg überspringt — damit ist die Lösung eindeutig (1 von 64
Kombinationen, nachgerechnet).

`AssignRoomTypes` legte einen Rätselraum bisher nur für zwei fest verdrahtete Schlüssel an; das ist
jetzt eine Menge, in die ein neues Raumrätsel nur noch eingetragen wird.

**Geprüft:** 300 Seeds kopflos durch den Generator — in jedem entsteht der Rätselraum, alle Teile
werden platziert, und der Strahl trifft mit der Soll-Lösung jedes Mal das Standbild.

**Nicht beurteilt:** Ob die Rätsel sich gut *anfühlen* — besonders ob das Schieben per Taste
eingängig ist — zeigt erst ein Durchgang.

### [x] C2 Mehr Gegner, aber weniger überfüllte Karte

Zwei gegenläufige Wünsche, deshalb getrennt: **mehr Gegner-Arten** in den Pool (`worlds.json`),
gleichzeitig **weniger Deko und weniger gleichzeitige Gegner** pro Raum. Stellschrauben stehen alle
in `balance.json` (`baseWaveSize`, `maxAliveEnemies`, `waveGrowth`) und im Generator
(`PlaceChests`, Deko-Dichte je Thema).

**Erledigt.** `baseWaveSize` 12 → 8, `maxAliveEnemies` 45 → 22, `waveGrowth` 0,25 → 0,35 (Wellen
eher länger als breiter), Verfalls-Zuschlag 0,45 → 0,25. Neuer Regler `decorDensity` (0,6) in
`balance.json`, angewandt in `DungeonGenerator.Decorate` — ein Wert statt zehn Themen. Wellen sind
damit 35–40 % kleiner. **Das Urteil steht aus:** Ob es jetzt zu leer ist, zeigt erst ein Durchgang.

### [x] C3 Eigenes Thema je Boss und Mini-Boss — `dbd71b5`

Bisher war der Thronsaal ein generischer Raum mit zwei Standardplattformen — jeder Boss kämpfte in
derselben Halle. Jetzt hat jeder der sechs Gegner eine eigene Arena in `Content/Data/arenas.json`:

| Arena | Gegner | Kennzeichen |
|---|---|---|
| Der Hain des Hirten | `boss_shepherd` | hohe Mittelplattform, Kerzen, Standbild |
| Mammons Hort | `boss_mammon` | vier Absätze, zwei Säulen, Goldhaufen |
| Die Mauern von Dis | `boss_titan` | Lavaschlote und Ketten, enge Deckung |
| Kerkerhof | `warden_limbo` | schlicht, zwei Absätze, Fackeln |
| Schuldturm | `warden_greed` | ein langer Absatz quer durch die Mitte |
| Folterkammer | `warden_wrath` | Kohlenbecken und Ketten |

Alle Angaben sind **Raum**-Kacheln — eine Arena ist damit unabhängig davon, wo im Verlies der Raum
liegt. Ohne Eintrag bleibt es beim Standardraum. Grundhelligkeit und Musik wirken nur im Thronsaal,
wo die Arena das ganze Verlies ist; ein Stück, das noch nicht im Manifest steht, wird ignoriert
statt Stille zu erzeugen.

Die Deko steht an festen Spalten; ist eine von einem Spiel-Prop belegt (im Kerker die Käfige),
rückt sie bis zu drei Kacheln zur Seite statt zu verschwinden.

**Geprüft** über 60 Seeds je Arena: Geometrie in 60 von 60 Räumen exakt, keine Türkachel zugebaut,
kein Prop schwebt, Deko zu 99–100 % am vorgesehenen Platz.

**Offen:** Ein eigenes Musikstück je Boss — reine Asset-Arbeit, als **G17** an GLM gegeben. Bis
dahin spielt überall `music.boss`.

---

## Paket D — Tutorial (optional spielbar)

### [x] D1 Tutorial, geführt von der Begleitseele — `a41f61b`

Zehn Schritte, jeder wartet auf **eine** Handlung: gehen, springen, ducken, angreifen, blocken,
Drehsprung, einen Gegner töten, etwas benutzen. Keine Zeitbegrenzung und kein Zwang — wer
weiterläuft statt zu üben, wird nicht aufgehalten, der Schritt bleibt stehen. `F5` bricht ab.

Die Schritte stehen in `Content/Data/tutorial.json`: Reihenfolge, Texte und Auslöser sind reine
Daten, der Code kennt nur die Auslösernamen.

Statt eigener Abfragen im Spielerzustand nutzt das Tutorial **dieselben Ereignisse**, mit denen
schon die Begleitseelen zum Sprechen gebracht werden. `DungeonWorld.Say` ist jetzt die eine
Meldestelle für beide Zuhörer — keine Spielregel muss wissen, ob gerade ein Tutorial läuft. Dafür
sind `first_crouch`/`first_block` zu `crouch`/`block` geworden und fünf Ereignisse neu dazugekommen
(`jump`, `attack`, `spin`, `interact`, `kill`).

Solange das Tutorial führt, schweigen die Zwischenrufe — zwei Sprechblasen um dieselbe Figur
würden einander überschreiben. Die Blase erscheint notfalls über dem Spieler selbst; ohne diesen
Rückfall bliebe das Tutorial stumm, wenn jemand „Allein hinabsteigen" gewählt hat.

Läuft nur im ersten Verlies eines Laufs und schaltet sich danach selbst ab. Im Optionsmenü
(Gameplay) jederzeit wieder einschaltbar.

`Validate` prüft die Auslöser gegen die Liste, die der Code meldet. Ein Tippfehler wäre hier
besonders teuer: Der Schritt würde nie bestanden, und der Spieler hinge für immer fest.

**Geprüft:** Alle zehn Auslöser werden vom Code wirklich gemeldet — gegen sämtliche `Say`-Aufrufe
im Projekt abgeglichen.

**Nicht beurteilt:** Ob die Reihenfolge sich gut anfühlt und ob zehn Schritte zu viel sind.

---

## Paket E — 16-Bit-Stil und größere Figuren

**Anlass:** Messung an den fertigen Sprites. Nach dem Anziehen unterscheiden sich die Körpertypen
nur um **7–11 von 384 Pixeln**, und nur **14 von 72 Rumpf-Pixeln** zeigen überhaupt Haut. Taille
und Bauchmuskeln sind gezeichnet, gehen bei 16 × 24 unter Gürtel und Riemen aber unter. Mehr Fläche
löst das an der Wurzel.

Mit dem Nutzer entschieden: Figuren auf **24 × 32**, Grafik insgesamt im **16-Bit-Stil** — gemeint
ist die Bildsprache der SNES-/Mega-Drive-Zeit (mehr Farbabstufungen je Material, weichere
Übergänge, lesbare Silhouetten), nicht bloß mehr Pixel.

### [x] E1 Kollisionsboxen von der Sprite-Größe lösen

**Problem:** Die Box stand als feste `10 × 22` im Code, dazu Steh- und Hockhöhe und eine
`- 22`-Annahme im Generator. Beim Umstieg hätte man jede dieser Zahlen einzeln nachziehen müssen;
wer eine vergisst, bekommt eine Figur, die in Wänden steckt oder schwebt.

**Erledigt.** Die Box leitet sich aus der Bildgröße ab (62 % Breite, 92 % Höhe, geduckt 55 %
davon). Bei 16 × 24 ergibt das **exakt** die bisherigen 10 × 22 und 12 — heute ändert sich also
nichts; bei 24 × 32 werden daraus 15 × 29 und 16, ohne Codeänderung. `LayeredSprite.FrameSize`
liefert die Größe, NPCs rechnen genauso. Der Generator gibt den Spawn jetzt als **Mitte der Füße**,
die Ecke leitet `PlayerFactory` ab.

**Nachgemessen als Randbedingung:** Türen sind 4 Kacheln (64 px) hoch — eine 32-px-Figur passt
bequem durch, die Levelgeometrie bleibt unangetastet. Kacheln bleiben bei 16 px.

### [x] E2 Figuren auf 24 × 32 (GLM, G12)

Alle 30 Figuren-Ebenen plus die Manifest-Einträge. Nur die Figuren — Gegner, Props und Effekte
behalten ihre Maße.

**Abgenommen.** 31 Ebenen auf 24 × 32, Manifest stimmt, Gegner und Props unangetastet. Spiel
startet ohne Warnung, die Kollisionsbox zieht wie vorgesehen automatisch auf 15 × 29 mit.

### [x] E3 16-Bit-Anhebung der Figuren (GLM, G13)

Vier bis sechs Tonwerte je Material statt drei, Licht von oben links, Materialkontrast zwischen
Leder, Kette, Bronze und Stein.

**Abgenommen.** Tonwerte deutlich gestiegen (Haare 3 → 7, Körper 69 Abstufungen), Farbregel
eingehalten, Rüstungen nach Material klar unterscheidbar.

### [x] E4 Gegner, Props und Kacheln nachziehen (GLM, G14)

**Abgenommen.** Nachgemessen an den fertigen PNGs, nicht am Code:

| Gruppe | Anzahl | Tonwerte min/median/max |
|---|---|---|
| `enemy_*` | 13 | 22 / 52 / 103 |
| `prop_*` | 29 | 2 / 43 / 132 |
| `tiles_*` | 3 | 29 / 29 / 32 |
| `bg_*` | 4 | 1 / 117 / 143 |

Die beiden Einer- und Zweierwerte sind in Ordnung: `prop_cobweb` ist ein Schleier-Overlay,
`bg_mid` eine Parallax-Silhouette in einer Farbe — beide sollen flach sein.

### [x] E5 Silhouetten der Körpertypen (GLM G15, von mir fertiggestellt) — `94a02ad`

Der Punkt, wegen dem überhaupt vergrößert wurde: 12 von 14 Rumpfzeilen waren zwischen
`f_athletic` und `f_heavy` **pixelgleich**, die gewonnene Fläche steckte in Binnenzeichnung statt
im Umriss.

GLM hat den Entwurf angelegt und ist dann ans Nutzungslimit gelaufen. Sein Rumpf war als Sanduhr
korrekt gezeichnet (Taille 10 px, Arme auf x 3–4 / 19–20, also 2 px Luft) — aber **unsichtbar**:
`polish()` legt an Arm *und* Rumpf je 1 px Kontur an und schloss die Lücke vollständig.
Korrigiert auf Taille 8 px und Arme eine Spalte weiter außen (x 2–3 / 20–21), damit 2 px Luft
übrig bleiben. Die Taillen-Schattenpixel sind von x7/x16 auf x8/x15 mitgewandert.

Der Rumpf entsteht jetzt zeilenweise aus einer Breitenliste:

| Typ | Rumpf-Umriss |
|---|---|
| `*_athletic` | Sanduhr — Schultern 16–18 px, Taille 8 px, Hüfte wieder ausgestellt |
| `*_average` | gleichmäßig 16 px, 1 px Andeutung |
| `*_heavy` | durchgehend 20 px, nach unten breiter (vorgewölbter Bauch) |

**Nachgemessen:** 8 von 14 Rumpfzeilen unterscheiden sich jetzt im Umriss (Kriterium ≥ 8 ✓,
vorher 2). Das zweite Kriterium — über 60 von 768 Pixeln nach dem Anziehen — ist mit **43**
**nicht erreicht**; das war eine Zahl, die ich mir selbst ausgedacht hatte, und sie greift zu
kurz: Umhang und Gürtel des Kriegers verdecken den Rumpf weitgehend, der Umriss kommt nur an
Taille und Armen durch. Nackt sind die vier Typen klar unterscheidbar (43 statt 23 Pixel bei
gleichzeitig deutlich anderem Umriss). **Ob das angezogen reicht, ist eine Sichtsache — schau
dir die vier Körpertypen im Editor an.**

### [ ] E6 Kleinsprites auf 16-Bit nachziehen (GLM, G16)

Beim Nachmessen von E4 aufgefallen: Begleiter, Pickups, Projektile und `effect_slash` sind bei
drei Tonwerten geblieben und stehen jetzt flach neben den angehobenen Gegnern.

| Gruppe | Anzahl | Tonwerte median |
|---|---|---|
| `companion_*` | 18 | 3 |
| `pickup_*` | 4 | 2 |
| `projectile_*` | 6 | 3 |

Alle vier Gruppen zeichnet der Code **ungetönt** (`Color.White`) — sie brauchen also eigene
Farben, die Graustufen-Regel gilt hier nicht. Als G16 beauftragt.

### Merkregel aus einem Fehler

Die Rüstungen waren zunächst in Graustufen gezeichnet — so stand es in meinem Auftrag, und die
Vorgabe war falsch: Der Code zeichnet die Rüstungsebene **ungetönt**, Graustufen bleiben also grau
und alle vier sahen aus wie derselbe helle Klotz. **Graustufen nur dort, wo der Code auch einfärbt**
(Haare, Make-up, Flügel, Akzent). Körper, Outfits und Rüstung brauchen eigene Farben.

---

## Nach dem Spieltest — drei Korrekturen

Rückmeldung: „das mit den beschweren ist nice aber funktioniert nicht ganz", „auch die Rüstung ist
noch nicht da", plus der Wunsch, den Editor umzubauen.

### [x] F1 Gewichts- und Spiegelrätsel gegen Schächte absichern — `cc2f8a9`

Aus dem Screenshot ausgemessen (Maßstab 4×, die gelben Punkte der Platte sind 4 px breit): zwei
Druckplatten im Abstand von genau **acht Kacheln** — mein Raster —, und dazwischen, wo die
Schiebeblöcke stehen müssten, ein **vier Kacheln breites Loch** im Boden. Das ist der Schacht, den
`CarveExit` für einen Ausgang nach unten in die Bodenreihe schneidet.

Zwei Fehler, beide von mir:

* Die mittlere Platte landete über dem Loch und schwebte — `PlacePropAt` prüfte den Boden nie.
* `IsTargetBlocked` fragte nur, ob die Zielkachel *fest* ist. Ein Loch ist nicht fest, also war der
  Schub erlaubt — und die Fall-Schleife ließ den Block durch den Schacht **in den Raum darunter**
  sinken. Weg, Rätsel für immer unlösbar. Genau deshalb waren keine Blöcke mehr zu sehen.

Korrigiert an drei Stellen: Der Rätselraum wird erst gewählt, **nachdem** Umwege, Schatzabzweige
und Kerker stehen (die hängen weitere Ausgänge an — daran ist mein erster Anlauf gescheitert);
ausgeschlossen sind nur Ausgänge nach **unten**. `PlacePropAt` meldet Fehlschlag ohne festen Boden.
Ein Schub ins Leere wird abgelehnt, und die Fall-Schleife endet am Rand des eigenen Raums.

**Meine 300-Seed-Prüfung war zu schwach** — in den geprüften Läufen hatte zufällig kein Rätselraum
einen senkrechten Ausgang. Jetzt 500 Seeds mit drei zusätzlichen Bedingungen (kein Schacht nach
unten, feste Kachel unter jedem Teil, durchgehender Boden auf **jedem** Schiebeweg von jedem Block
zu jeder Platte). Vorher 256 Fehler, jetzt null. 394 von 500 Verliesen bekommen das Rätsel.

### [x] F2 Rüstung von der ersten Sekunde an — `54ca4a2`

Ich habe den Spielstand gelesen: der Lauf hatte **kein einziges Item**. Die Rüstung war also weder
kaputt noch unsichtbar — sie entstand nur als Zufallsbeute aus Truhen, und bei 13 Items im Topf
vergeht damit leicht ein halber Lauf ohne.

Jede Klasse startet jetzt mit einem Lederwams (`startingArmor` in `classes.json`). Dabei zwei echte
Fehler gefunden:

* `CollectItem` rief `Apply`, aber nie `RefreshAppearance`. `Apply` rührt nur die Werte an, nicht
  die Sprite-Ebenen — eine im Verlies aufgesammelte Rüstung wäre bis zum nächsten Verlies
  unsichtbar geblieben. `InventoryScene` macht es seit jeher richtig.
* `ArmorHitsOf` rechnete 60 Robustheit auf genau **einen** Treffer um; das Lederwams zersprang also
  beim ersten Schlag. Man sah die Splitter, aber nie den Moment, in dem der Panzer einen Schlag
  schluckt. Jetzt fängt jede Rüstung mindestens einen Treffer ganz ab (60→2, 110→3, 180→4, 300→5).

`Validate` prüft jetzt auch Item-Sprites und die Startrüstung — ein falsch benanntes Rüstungssprite
wäre bisher lautlos verschwunden.

**Nicht beurteilt:** Ob zwei Treffer für den Start zu großzügig sind, zeigt erst ein Durchgang.

### [x] F3 Charakter-Editor: Geschlecht → Statur → Klasse — `6cbd47c`

Geschlecht war gar kein eigenes Feld — es steckte nur im ID-Präfix der sechs Körpertypen.
`Progression/BodyTypeCatalog.cs` zerlegt die eine Liste in zwei Achsen und rechnet sie wieder
zusammen. Datenformat, Persistenz (`body_type`) und `CharacterVisuals` bleiben unverändert: keine
Migration, und `appearance.json` (GLMs Datei) unangetastet.

Statur läuft auf der Skala **Kräftig → Normal → Trainiert**; ein Geschlechtswechsel behält sie.
Fehlen die Präfixe (Mod), fällt die Geschlechtszeile weg und „Statur" listet wie bisher alles.

Nachgerechnet: Alle sechs Kombinationen treffen genau einen Körpertyp, keinen doppelt, Hin- und
Zurückrechnen ist stabil. Bei zwölf statt elf Zeilen sinkt der Abstand von 13,1 auf 12,0 px — die
Schrift ist 10 px hoch, die letzte Zeile endet zwei Pixel über dem Infoblock.

---

## Erledigt in dieser Sitzung

* [x] Optionen aus **jedem** Pausenmenü erreichbar (`73531d8`) — im Verlies fehlte der Eintrag.
* [x] Stille Fehlerpfade in `AudioService` und `MusicSystem` sichtbar gemacht (`73531d8`).
  Die „kein Ton"-Meldung war damit in einer Minute geklärt: Das Spiel spielt ab
  (Zustand `Playing`, Lautstärke 0,74), die Ursache lag am Ausgabegerät des Rechners.

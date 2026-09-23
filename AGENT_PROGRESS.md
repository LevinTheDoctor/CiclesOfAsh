# Arbeitsstand

Diese Datei führt die offenen Arbeitspakete. **Sie wird gelöscht, sobald alles erledigt ist** —
der Zustand gehört dann in die Git-Historie, nicht ins Repo.

Zustände: `[ ]` offen · `[~]` in Arbeit · `[x]` erledigt (mit Commit-Kürzel)

---

## Paket 5 — Spawn-Ordnung überarbeiten

**Problem (Nutzermeldung mit Screenshot, 23.09.):** „Einige Spawn-Sachen sind gerade nicht in
Ordnung." Die Fallback-Logik aus dem alten Paket 1.5 hatte drei erkennbare Schwächen:

1. Der alte Fallback fiel bei blockiertem Kandidaten stur auf die **Bodenmitte** des Raums
   zurück. Steht der Spieler dort (typisch beim Wellen-Spawn), erscheint der Gegner auf ihm.
2. Kerker-Wachen mit blockierter Wunschkposition landeten **alle** in derselben Raummitte —
   sichtbar gestapelt.
3. Flieger-Spawnhöhen in Höhlenräumen lagen regelmäßig im Fels; danach war der Ort reiner Zufall.

**Lösung:** `DungeonWorld.FindSpawnSpot(definition, preferred, room, minPlayerDistance)`:
erst der Wunschkandidat, dann Versatz-Versuche in wachsenden Ringen um ihn (Wachen bleiben an
ihren Posten), dann raumweite Zufallsversuche, erst ganz zum Schluss die Bodenmitte. Optionaler
Mindestabstand zum Spieler. Umgestellt: Wellen-Spawn (80 px Abstand), Boss, Kerker-Wächter und
-Wachen (24 px), Rescue-Wachen (24 px), Boss-Diener (Thronsaal-Podeste).

**Prüfen:** Höhlen-Arena mit Fliegern, Kerker mit Wachen, Rescue-Kampf — nichts mehr im Fels,
auf dem Spieler oder gestapelt. **Manuell durch den Nutzer.**

---

## Paket 6 — Controller-DB und Optionsmenü auffindbar machen

**Problem (Nutzermeldung, 23.09.):** Paket 3 (SDL-DB) und Paket 4 (Optionsreiter) waren „nicht
aufzufinden" — das geprüfte `publish/`-Bündel stammte aus der Paket-2-Phase. Außerdem war die
Annahme aus Paket 3 falsch.

**Aufgeklärt (dekompiliert, MonoGame 3.8.2.1105):** `GamePad.InitDatabase` lädt NUR eine
**eingebaute** Kopie der DB als Manifest-Resource aus der Framework-DLL — eine externe Datei
wird nirgends gesucht. Die eingebaute Kopie ist älter (1.971 Mappings, macOS: 271) als die
beigelegte (2.276 Mappings).

**Lösung (umgesetzt):** Eigener Loader `SdlControllerMappings` in `CirclesGame` **vor** allen
SDL-Initialisierungen: lädt `Content/gamecontrollerdb.txt` in den Speicher und übergibt sie per
`SDL_RWFromMem` + `SDL_GameControllerAddMappingsFromRW(freesrc=1)` — exakt der Weg, den MonoGame
selbst für die eingebaute DB nimmt (`SDL_GameControllerAddMappingsFromFile` fehlt in MonoGames
SDL-Build, verifiziert über `nm`). SDL kombiniert beide DBs; Log bestätigt: **„Controller-
Datenbank geladen: 50 Mappings"** (50 neue, nicht enthaltene). Native Bibliothek wird mit
MonoGames Pfad-Schema aufgelöst (direkt, ../Frameworks, runtimes/&lt;rid&gt;/native mit OS-Fallbacks).
Außerdem: **Optionen jetzt auch im Hauptmenü** (TitleScene-Eintrag), nicht nur über Pause.

**Prüfen:** Frisches `.app` starten: Log-Zeile „Controller-Datenbank geladen: …"; Optionen im
Hauptmenü; Reiter-Durchsteuerung mit Pad. **Bündel neu gebaut und Start verifiziert.**
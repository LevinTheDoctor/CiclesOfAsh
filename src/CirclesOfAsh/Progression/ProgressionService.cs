using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Persistence;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Progression;

/// <summary>
/// Alle Regeln rund um Fortschritt: Gestalten, Lauf starten, Dungeons planen, Belohnungen, Tod.
/// Kennt weder Szenen noch Grafik -> reine Spiellogik, gut testbar.
///
/// Es gibt mehrere Gestalten (<see cref="SavedCharacter"/>) mit je höchstens einem Lauf. Genau eine
/// ist aktiv; <see cref="CurrentRun"/> ist immer der Lauf DIESER Gestalt. Gläubige, Ewige Gaben,
/// Begleitseelen und Bitten (<see cref="Meta"/>) gehören dem Gott und damit allen Gestalten.
/// </summary>
public sealed class ProgressionService
{
    /// <summary>Höchstzahl gespeicherter Gestalten – so passt die Auswahl auf einen Bildschirm.</summary>
    public const int MaxCharacters = 8;

    private readonly DefinitionRegistry _definitions;
    private readonly ISaveRepository _saves;
    /// <summary>Zuletzt gespielte Gestalt vorne (siehe <see cref="Touch"/>).</summary>
    private readonly List<SavedCharacter> _characters;

    public ProgressionService(DefinitionRegistry definitions, ISaveRepository saves)
    {
        _definitions = definitions;
        _saves = saves;
        Meta = saves.LoadMeta();
        _characters = saves.LoadCharacters();
        DiscardIncompatibleRuns();
        // Die zuletzt aktive Gestalt wieder aufnehmen; wurde sie gelöscht, die zuletzt gespielte.
        // "??" = Null-Coalescing: nimmt den rechten Wert, wenn der linke null ist.
        SavedCharacter? active = _characters.FirstOrDefault(character => character.Id == Meta.ActiveCharacterId)
                                 ?? _characters.FirstOrDefault();
        if (active is not null) Activate(active);
        Missions = new MissionService(definitions, Meta);
        UnlockCompanionsByBelievers();
    }

    public MetaState Meta { get; }
    public MissionService Missions { get; }
    /// <summary>Alle Gestalten, zuletzt gespielte zuerst. IReadOnlyList: Ändern nur über diesen Dienst.</summary>
    public IReadOnlyList<SavedCharacter> Characters => _characters;
    /// <summary>Die Gestalt, mit der gerade gespielt wird – oder null, wenn es noch keine gibt.</summary>
    public SavedCharacter? ActiveCharacter { get; private set; }
    /// <summary>Lauf der aktiven Gestalt, oder null.</summary>
    public RunState? CurrentRun { get; private set; }
    public bool CanCreateCharacter => _characters.Count < MaxCharacters;
    private BalanceDefinition Balance => _definitions.Balance;

    /// <summary>Die aktive Gestalt – wer einen Lauf verändert, MUSS eine haben.</summary>
    private SavedCharacter RequireActiveCharacter() =>
        ActiveCharacter ?? throw new InvalidOperationException("Ohne aktive Gestalt gibt es keinen Lauf.");

    /// <summary>Aktuell gewählte Schwierigkeit (aus difficulties.json; Fallback = "devout").</summary>
    public DifficultyDefinition Difficulty =>
        _definitions.Difficulties.TryGet(_difficultyId, out DifficultyDefinition? difficulty) ? difficulty
            : _definitions.Difficulties.Get("devout");

    private string _difficultyId = "devout";

    public void SetDifficulty(string difficultyId)
    {
        if (!_definitions.Difficulties.Contains(difficultyId)) return;
        _difficultyId = difficultyId;
    }

    public WorldDefinition WorldOf(RunState run) => _definitions.Worlds.Get(run.WorldId);
    public CircleDefinition CircleOf(RunState run) => WorldOf(run).Circles[run.CircleIndex];

    public IEnumerable<CompanionDefinition> AvailableCompanions =>
        _definitions.Companions.All.Where(companion => Meta.UnlockedCompanions.Contains(companion.Id));

    // ------------------------------------------------------------------ Gestalten
    /// <summary>Macht eine Gestalt zur aktiven, lädt ihren Lauf und merkt sie sich für den nächsten Start.</summary>
    public void SelectCharacter(SavedCharacter character)
    {
        Activate(character);
        Touch(character);
        _saves.SaveMeta(Meta);
    }

    /// <summary>
    /// Legt eine neue Gestalt an und macht sie aktiv. Ihr Lauf beginnt erst mit
    /// <see cref="StartNewRun"/> – bricht jemand den Editor ab, entsteht also gar nichts.
    /// </summary>
    public SavedCharacter CreateCharacter(CharacterAppearance appearance)
    {
        DateTime now = DateTime.UtcNow;
        var character = new SavedCharacter { Appearance = appearance, CreatedAt = now, LastPlayedAt = now };
        _saves.SaveCharacter(character);   // vergibt die Id
        _characters.Insert(0, character);
        Activate(character);
        _saves.SaveMeta(Meta);
        return character;
    }

    /// <summary>Neues Aussehen samt Name – auch mitten im Lauf. Der laufende Abstieg sieht es sofort.</summary>
    public void ChangeAppearance(SavedCharacter character, CharacterAppearance appearance)
    {
        character.Appearance = appearance;
        _saves.SaveCharacter(character);
        if (character == ActiveCharacter && CurrentRun is not null) CurrentRun.Appearance = appearance;
    }

    /// <summary>Löscht eine Gestalt samt Lauf. Was dem Gott gehört (Gläubige, Gaben, Seelen), bleibt.</summary>
    public void DeleteCharacter(SavedCharacter character)
    {
        _saves.DeleteCharacter(character.Id);
        _characters.Remove(character);
        if (character != ActiveCharacter) return;
        ActiveCharacter = null;
        CurrentRun = null;
        Meta.ActiveCharacterId = 0;
        _saves.SaveMeta(Meta);
    }

    /// <summary>
    /// Der Lauf einer beliebigen Gestalt, etwa für die Vorschau in der Auswahl (Kleidung, Unterwäsche).
    /// Bei der aktiven ist es <see cref="CurrentRun"/> selbst, sonst eine frisch geladene Kopie.
    /// </summary>
    public RunState? PeekRun(SavedCharacter character)
    {
        if (character == ActiveCharacter) return CurrentRun;
        if (character.CurrentRun is null) return null;
        RunState? run = _saves.LoadRun(character.Id);
        if (run is not null) run.Appearance = character.Appearance;
        return run;
    }

    /// <summary>
    /// Klasse, in der eine Gestalt gezeigt wird: die ihres Laufs, sonst die zuletzt gespielte, sonst
    /// die erste aus classes.json (neue Gestalt, oder die Klasse stammt aus einer entfernten Mod).
    /// </summary>
    public ClassDefinition DisplayClassOf(SavedCharacter? character)
    {
        string classId = character?.CurrentRun?.ClassId ?? character?.LastClassId ?? "";
        return _definitions.Classes.TryGet(classId, out ClassDefinition? known) ? known : _definitions.Classes.All.First();
    }

    private void Activate(SavedCharacter character)
    {
        ActiveCharacter = character;
        Meta.ActiveCharacterId = character.Id;
        CurrentRun = character.CurrentRun is null ? null : _saves.LoadRun(character.Id);
        // Das Aussehen gehört der Gestalt; der Lauf trägt nur eine Kopie für Spielerfigur und Anzeigen.
        if (CurrentRun is not null) CurrentRun.Appearance = character.Appearance;
    }

    /// <summary>"Zuletzt gespielt = jetzt" – und die Gestalt rückt in der Liste nach vorne.</summary>
    private void Touch(SavedCharacter character)
    {
        character.LastPlayedAt = DateTime.UtcNow;
        _saves.SaveCharacter(character);
        _characters.Remove(character);
        _characters.Insert(0, character);
    }

    /// <summary>Kurzfassung für die Auswahlliste; wird nach jeder Änderung am Lauf erneuert.</summary>
    private static RunSummary Summarize(RunState run) =>
        new(run.ClassId, run.WorldId, run.CircleIndex, run.DungeonIndex, run.Level);

    /// <summary>
    /// Verwirft Läufe, die nicht mehr zu den Spieldaten passen (etwa weil eine Mod fehlt). Einmal beim
    /// Start für ALLE Gestalten – danach zeigt die Auswahl nur Läufe, die sich auch fortsetzen lassen.
    /// </summary>
    private void DiscardIncompatibleRuns()
    {
        foreach (SavedCharacter character in _characters.Where(character => character.CurrentRun is not null))
        {
            RunState? run = _saves.LoadRun(character.Id);
            if (run is not null && IsRunCompatible(run)) continue;
            Log.Warn($"Gespeicherter Lauf von '{character.Name}' passt nicht mehr zu den Spieldaten und wird verworfen.");
            _saves.DeleteRun(character.Id);
            character.CurrentRun = null;
        }
    }

    // ------------------------------------------------------------------ Lauf
    /// <summary>Beginnt einen neuen Lauf der aktiven Gestalt (ein alter wird dabei ersetzt).</summary>
    /// <param name="underwear">
    /// Muster der Unterwäsche, oder -1 zum Würfeln. Der Editor gibt das durch, was er in der
    /// Vorschau gezeigt hat – sonst würde der Lauf mit einem anderen Muster beginnen als versprochen.
    /// </param>
    public RunState StartNewRun(string classId, IEnumerable<string> companionIds, int underwear = -1)
    {
        SavedCharacter character = RequireActiveCharacter();
        ClassDefinition playerClass = _definitions.Classes.Get(classId);
        var run = new RunState
        {
            ClassId = playerClass.Id,
            Appearance = character.Appearance,
            WorldId = _definitions.Worlds.All.First().Id,
            Seed = Random.Shared.Next(),
            CompanionIds = companionIds.Take(Balance.CompanionSlots).ToList(),
            Underwear = underwear >= 0 ? underwear : RollUnderwear(),
        };
        foreach (string abilityId in playerClass.StartingAbilities) run.AbilityLevels[abilityId] = 1;
        // Startkleidung über den normalen Weg anlegen: AddItem setzt die Trefferzahl gleich mit.
        if (_definitions.Items.TryGet(playerClass.StartingArmor, out ItemDefinition? armor))
            EquipmentService.AddItem(run, armor);

        Meta.RunsStarted++;
        character.Runs++;
        character.LastClassId = playerClass.Id;
        character.DeepestCircle = Math.Max(character.DeepestCircle, run.CircleIndex + 1);
        character.CurrentRun = Summarize(run);
        CurrentRun = run;
        _saves.SaveRun(character.Id, run);
        Touch(character);
        _saves.SaveMeta(Meta);
        return run;
    }

    /// <summary>Würfelt ein Unterwäsche-Muster. Ohne hinterlegte Muster bleibt es bei 0.</summary>
    public int RollUnderwear()
    {
        int count = _definitions.Appearance.UnderwearStyles.Count;
        return count > 0 ? Random.Shared.Next(count) : 0;
    }

    public DungeonPlan CreateDungeonPlan(RunState run)
    {
        CircleDefinition circle = CircleOf(run);
        int index = run.DungeonIndex;
        bool isBoss = index >= Balance.DungeonsPerCircle - 1;
        float difficulty = circle.Difficulty * (1f + Balance.DifficultyStepPerDungeon * index);

        // Deterministischer Seed pro Dungeon. NICHT HashCode.Combine verwenden: der ist pro Prozess zufällig!
        int seed = unchecked(run.Seed ^ (run.CircleIndex + 1) * 7919 ^ (index + 1) * 104729);

        // Unsigned-Modulo: (uint) macht negative Seeds positiv -> gültiger Listenindex
        string puzzle = isBoss || circle.Puzzles.Count == 0 ? "" : circle.Puzzles[(int)((uint)seed % (uint)circle.Puzzles.Count)];
        bool hasPrison = !isBoss && circle.Prison is not null && circle.Prison.DungeonIndex == index;
        // Rescue-Zuverlässigkeit: Läuft eine aktive Rescue-Bitte, ALLE Kerkerdungeons bekommen einen Kerker
        // (statt nur dem festen DungeonIndex). "free_the_captives" ist so immer fortsetzbar.
        if (!isBoss && circle.Prison is not null && HasActiveRescueMission())
        {
            int prisonIndex = Math.Min(circle.Prison.DungeonIndex, Balance.DungeonsPerCircle - 2);
            hasPrison = index == prisonIndex || index == Balance.DungeonsPerCircle - 2;
        }

        return new DungeonPlan(
            Circle: circle,
            CircleIndex: run.CircleIndex,
            DungeonIndex: index,
            IsBossDungeon: isBoss,
            DifficultyMultiplier: difficulty,
            PathLength: isBoss ? 3 : Balance.BaseRoomCount + index * Balance.RoomsPerDungeonIndex,
            ArenaCount: isBoss ? 0 : 1 + index / 2,
            WavesPerArena: Balance.BaseWavesPerArena + index / 2,
            TreasureBranches: isBoss ? 0 : Balance.TreasureBranches,
            Seed: seed,
            HasPrison: hasPrison,
            PuzzleKey: puzzle,
            LeverCount: Balance.LeverCount,
            DetourCount: isBoss ? 0 : Balance.MaxDetours,
            ChestCount: isBoss ? 0 : Balance.ChestsPerDungeon,
            CollectibleCount: isBoss ? 0 : circle.CollectiblesPerDungeon);
    }

    /// <summary>Übernimmt den Dungeon-Stand und vergibt Belohnungen. Gibt zurück, was passiert ist.</summary>
    public DungeonOutcome CompleteDungeon(RunState finishedRun)
    {
        SavedCharacter character = RequireActiveCharacter();
        var outcome = new DungeonOutcome();
        CircleDefinition circle = CircleOf(finishedRun);
        WorldDefinition world = WorldOf(finishedRun);
        bool wasBoss = finishedRun.DungeonIndex >= Balance.DungeonsPerCircle - 1;

        outcome.BelieversGained = circle.BelieversPerDungeon;
        if (wasBoss)
        {
            outcome.CircleCompleted = true;
            outcome.CompletedMissions.AddRange(Missions.Report(MissionType.CompleteCircle, circle.Id));
            // HashSet.Add liefert true nur beim ersten Mal -> Belohnung wird nur einmal "neu" gemeldet
            if (!string.IsNullOrEmpty(circle.BossReward) && Meta.UnlockedAbilities.Add(circle.BossReward))
                outcome.UnlockedAbilityId = circle.BossReward;

            finishedRun.CircleIndex++;
            finishedRun.DungeonIndex = 0;
            if (finishedRun.CircleIndex >= world.Circles.Count)
            {
                outcome.BelieversGained += world.BelieversOnLiberation;
                outcome.LiberatedWorldName = world.Name;
                Meta.LiberatedWorlds.Add(world.Id);
                WorldDefinition? nextWorld = _definitions.Worlds.All.SkipWhile(candidate => candidate.Id != world.Id).Skip(1).FirstOrDefault();
                if (nextWorld is null) outcome.GameCompleted = true;
                else
                {
                    finishedRun.WorldId = nextWorld.Id;
                    finishedRun.CircleIndex = 0;
                }
            }
        }
        else
        {
            finishedRun.DungeonIndex++;
        }

        // Härtere Stufen zahlen mehr Gläubige – das Risiko soll sich lohnen (rewardMultiplier).
        outcome.BelieversGained = (int)MathF.Round(outcome.BelieversGained * Difficulty.RewardMultiplier);
        Meta.Believers += outcome.BelieversGained;
        outcome.UnlockedCompanionIds.AddRange(UnlockCompanionsByBelievers());

        if (outcome.GameCompleted)
        {
            CurrentRun = null;
            character.CurrentRun = null;
            _saves.DeleteRun(character.Id);
        }
        else
        {
            CurrentRun = finishedRun;
            character.CurrentRun = Summarize(finishedRun);
            character.DeepestCircle = Math.Max(character.DeepestCircle, finishedRun.CircleIndex + 1);
            _saves.SaveRun(character.Id, finishedRun);
        }
        Touch(character);
        _saves.SaveMeta(Meta);
        return outcome;
    }

    /// <summary>
    /// Permadeath: Lauf + Klasse der aktiven Gestalt sind weg, nur ein Teil der Gläubigen bleibt treu.
    /// Die Gestalt selbst bleibt und kann neu aufbrechen; permanente Boss-Fähigkeiten und Begleiter
    /// bleiben ebenfalls erhalten (Meta-Fortschritt).
    /// </summary>
    public DeathReport HandleDeath()
    {
        SavedCharacter character = RequireActiveCharacter();
        RunState? run = CurrentRun;
        string className = run is not null && _definitions.Classes.TryGet(run.ClassId, out ClassDefinition? playerClass)
            ? playerClass.Name.Translated
            : "?";
        int reachedCircle = (run?.CircleIndex ?? 0) + 1;
        long before = Meta.Believers;
        // Die Schwierigkeitsstufe bestimmt, wie viele Gläubige den Tod überdauern (Balance = Rückfall für alte Stände).
        Meta.Believers = (long)MathF.Floor(before * Difficulty.BelieverRetention);
        Meta.Deaths++;
        character.Deaths++;
        character.DeepestCircle = Math.Max(character.DeepestCircle, reachedCircle);
        character.CurrentRun = null;
        CurrentRun = null;
        _saves.DeleteRun(character.Id);
        Touch(character);
        _saves.SaveMeta(Meta);
        return new DeathReport(before, Meta.Believers, reachedCircle, className, character.Name);
    }

    /// <summary>
    /// Gefangene befreit: Gläubige, dauerhafter Begleiter und Missionsfortschritt.
    /// Pro Lauf und Kerker nur einmal (Schlüssel aus Seed, Kreis und Dungeon).
    /// </summary>
    public RescueResult RescueCaptives(DungeonPlan plan, RunState run)
    {
        PrisonDefinition? prison = plan.Circle.Prison;
        string key = $"{run.Seed}:{plan.Circle.Id}:{plan.DungeonIndex}";
        if (prison is null || !Meta.RescuedPrisons.Add(key))
            return new RescueResult(true, 0, null, Array.Empty<MissionDefinition>());

        Meta.Believers += prison.Believers;
        string? companionName = null;
        if (!string.IsNullOrEmpty(prison.CompanionReward) && Meta.UnlockedCompanions.Add(prison.CompanionReward))
            companionName = _definitions.Companions.Get(prison.CompanionReward).Name;

        List<MissionDefinition> missions = Missions.Report(MissionType.Rescue, plan.Circle.Id, prison.Captives);
        _saves.SaveMeta(Meta);   // sofort sichern: der neue Begleiter soll auch nach einem Tod bleiben
        return new RescueResult(false, prison.Believers, companionName, missions);
    }

    public void SaveMeta() => _saves.SaveMeta(Meta);

    /// <summary>Gibt es eine aktive Bitte vom Typ Rescue (z. B. "Öffnet die Käfige")?</summary>
    public bool HasActiveRescueMission() =>
        Missions.Active.Any(mission => mission.Type == MissionType.Rescue);

    /// <summary>Speichert Änderungen am festgeschriebenen Lauf (z. B. Ausrüstung im Kreis-Menü).</summary>
    public void SaveRun(RunState run)
    {
        // Nur der festgeschriebene Lauf der aktiven Gestalt – nie eine Arbeitskopie aus dem Verlies.
        if (run != CurrentRun || ActiveCharacter is not { } character) return;
        character.CurrentRun = Summarize(run);
        _saves.SaveRun(character.Id, run);
    }

    // ------------------------------------------------------------------ Formeln
    /// <summary>Benötigte Seelen von Stufe 'level' auf 'level + 1' (exponentielle Kurve).</summary>
    public float ExperienceForNextLevel(int level) => MathF.Round(Balance.XpBase * MathF.Pow(Balance.XpGrowth, level - 1));

    /// <summary>Göttliche Macht: Prozentbonus aus Gläubigen, z. B. 250 Gläubige * 0.05 / 100 = +12,5 %.</summary>
    public float BelieverBonus(float perHundred) => Meta.Believers / 100f * perHundred;

    private IEnumerable<string> UnlockCompanionsByBelievers()
    {
        var newlyUnlocked = new List<string>();
        foreach (CompanionDefinition companion in _definitions.Companions.All)
        {
            if (companion.UnlockAtBelievers < 0) continue;   // nur durch Befreiung erhältlich
            if (Meta.Believers >= companion.UnlockAtBelievers && Meta.UnlockedCompanions.Add(companion.Id))
                newlyUnlocked.Add(companion.Id);
        }
        return newlyUnlocked;
    }

    private bool IsRunCompatible(RunState run) =>
        _definitions.Classes.Contains(run.ClassId) &&
        _definitions.Worlds.Contains(run.WorldId) &&
        run.CircleIndex < _definitions.Worlds.Get(run.WorldId).Circles.Count &&
        run.AbilityLevels.Keys.All(_definitions.Abilities.Contains) &&   // Methodengruppe statt Lambda
        run.Items.All(_definitions.Items.Contains);
}

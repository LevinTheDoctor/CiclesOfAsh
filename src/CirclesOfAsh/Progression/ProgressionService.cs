using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Persistence;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Progression;

/// <summary>
/// Alle Regeln rund um Fortschritt: Lauf starten, Dungeons planen, Belohnungen, Tod.
/// Kennt weder Szenen noch Grafik -> reine Spiellogik, gut testbar.
/// </summary>
public sealed class ProgressionService
{
    private readonly DefinitionRegistry _definitions;
    private readonly ISaveRepository _saves;

    public ProgressionService(DefinitionRegistry definitions, ISaveRepository saves)
    {
        _definitions = definitions;
        _saves = saves;
        Meta = saves.LoadMeta();
        CurrentRun = saves.LoadRun();
        if (CurrentRun is not null && !IsRunCompatible(CurrentRun))
        {
            Log.Warn("Gespeicherter Lauf passt nicht mehr zu den Spieldaten und wird verworfen.");
            CurrentRun = null;
            saves.DeleteRun();
        }
        Missions = new MissionService(definitions, Meta);
        UnlockCompanionsByBelievers();
    }

    public MetaState Meta { get; }
    public MissionService Missions { get; }
    public RunState? CurrentRun { get; private set; }
    private BalanceDefinition Balance => _definitions.Balance;

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

    // ------------------------------------------------------------------ Lauf
    /// <param name="underwear">
    /// Muster der Unterwäsche, oder -1 zum Würfeln. Der Editor gibt das durch, was er in der
    /// Vorschau gezeigt hat – sonst würde der Lauf mit einem anderen Muster beginnen als versprochen.
    /// </param>
    public RunState StartNewRun(string classId, CharacterAppearance appearance, IEnumerable<string> companionIds,
                               int underwear = -1)
    {
        ClassDefinition playerClass = _definitions.Classes.Get(classId);
        var run = new RunState
        {
            ClassId = playerClass.Id,
            Appearance = appearance,
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
        CurrentRun = run;
        _saves.SaveRun(run);
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
            _saves.DeleteRun();
        }
        else
        {
            CurrentRun = finishedRun;
            _saves.SaveRun(finishedRun);
        }
        _saves.SaveMeta(Meta);
        return outcome;
    }

    /// <summary>
    /// Permadeath: Lauf + Klasse weg, nur ein Teil der Gläubigen bleibt treu.
    /// Permanente Boss-Fähigkeiten und Begleiter bleiben erhalten (Meta-Fortschritt).
    /// </summary>
    public DeathReport HandleDeath()
    {
        RunState? run = CurrentRun;
        string className = run is null ? "?" : _definitions.Classes.Get(run.ClassId).Name;
        long before = Meta.Believers;
        // Die Schwierigkeitsstufe bestimmt, wie viele Gläubige den Tod überdauern (Balance = Rückfall für alte Stände).
        Meta.Believers = (long)MathF.Floor(before * Difficulty.BelieverRetention);
        Meta.Deaths++;
        CurrentRun = null;
        _saves.DeleteRun();
        _saves.SaveMeta(Meta);
        return new DeathReport(before, Meta.Believers, (run?.CircleIndex ?? 0) + 1, className);
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
        if (run == CurrentRun) _saves.SaveRun(run);
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

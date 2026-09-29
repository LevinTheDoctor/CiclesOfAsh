using CirclesOfAsh.Definitions;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Progression;

/// <summary>
/// Ein möglicher Gegner in der Arena: der Boss eines Kreises oder sein Kerkermeister.
/// <paramref name="DungeonIndex"/> ist das Verlies, in dem er sonst wartet – daraus folgt seine Stärke.
/// </summary>
public sealed record ArenaOpponent(EnemyDefinition Enemy, WorldDefinition World, CircleDefinition Circle,
                                   int CircleIndex, int DungeonIndex, bool IsMiniBoss)
{
    public string Id => Enemy.Id;
}

/// <summary>
/// Wer in die Arena steigt. <see cref="Run"/> ist IMMER eine Kopie: Was in der Arena geschieht –
/// zerfetzte Kleidung, verbrauchtes Leben –, berührt den echten Lauf der Gestalt nicht.
/// <see cref="Believers"/> und <see cref="EternalGifts"/> reisen mit, damit der Rechner des
/// Mitspielers (Online) den Kämpfer genauso stark baut wie der eigene.
/// </summary>
public sealed record ArenaFighter(int CharacterId, string Name, CharacterAppearance Appearance, RunState Run,
                                  long Believers, IReadOnlyList<string> EternalGifts);

/// <summary>
/// Regeln der Arena: welche Gegner es gibt, welche schon freigeschaltet sind, wie ein Kämpfer
/// entsteht und wie der Kampfplatz aussieht. Kennt weder Szenen noch Netzwerk – reine Spiellogik.
///
/// Freigeschaltet ist ein Gegner, sobald irgendeine Gestalt seinen Kreis erreicht hat (in einer
/// befreiten Welt: alle). Die Arena ist ein Wiedersehen, keine Vorschau auf das, was unten wartet.
/// </summary>
public sealed class ArenaService
{
    private readonly DefinitionRegistry _definitions;
    private readonly ProgressionService _progression;

    public ArenaService(DefinitionRegistry definitions, ProgressionService progression)
    {
        _definitions = definitions;
        _progression = progression;
        Opponents = BuildOpponents().ToList();
    }

    /// <summary>Alle Gegner in Spielreihenfolge: je Kreis erst der Kerkermeister, dann der Boss.</summary>
    public IReadOnlyList<ArenaOpponent> Opponents { get; }

    public IEnumerable<ArenaOpponent> UnlockedOpponents => Opponents.Where(IsUnlocked);

    public ArenaOpponent? Find(string enemyId) =>
        Opponents.FirstOrDefault(opponent => opponent.Id.Equals(enemyId, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<ArenaOpponent> BuildOpponents()
    {
        BalanceDefinition balance = _definitions.Balance;
        foreach (WorldDefinition world in _definitions.Worlds.All)
        {
            for (int index = 0; index < world.Circles.Count; index++)
            {
                CircleDefinition circle = world.Circles[index];
                // "is { } prison" bindet den Kerker nur, wenn es einen gibt (Eigenschaftsmuster).
                if (circle.Prison is { } prison && _definitions.Enemies.TryGet(prison.MiniBoss, out EnemyDefinition? warden))
                    yield return new ArenaOpponent(warden, world, circle, index, prison.DungeonIndex, IsMiniBoss: true);
                if (_definitions.Enemies.TryGet(circle.Boss, out EnemyDefinition? boss))
                    yield return new ArenaOpponent(boss, world, circle, index, balance.DungeonsPerCircle - 1, IsMiniBoss: false);
            }
        }
    }

    public bool IsUnlocked(ArenaOpponent opponent)
    {
        HashSet<string> liberated = _progression.Meta.LiberatedWorlds;
        if (liberated.Contains(opponent.World.Id)) return true;
        // Nur die erste noch nicht befreite Welt ist teilweise offen – bis zum tiefsten Kreis, den
        // eine Gestalt je erreicht hat. Mindestens der erste: Den sieht jede Gestalt als Erstes.
        WorldDefinition? current = _definitions.Worlds.All.FirstOrDefault(world => !liberated.Contains(world.Id));
        if (current?.Id != opponent.World.Id) return false;
        int reach = _progression.Characters.Select(character => character.DeepestCircle).DefaultIfEmpty(0).Max();
        return opponent.CircleIndex < Math.Max(1, reach);
    }

    /// <summary>
    /// Ein Kämpfer aus einer Gestalt: mit Lauf so, wie der Lauf gerade steht (Stufe, Gaben,
    /// Ausrüstung), ohne Lauf mit der Startausstattung der zuletzt gespielten Klasse.
    /// </summary>
    public ArenaFighter CreateFighter(SavedCharacter character)
    {
        MetaState meta = _progression.Meta;
        // "?." + "??": Kopie des Laufs, falls es einen gibt – sonst ein frischer.
        RunState run = _progression.PeekRun(character)?.Clone()
                       ?? _progression.CreateStartingRun(_progression.DisplayClassOf(character), character.Appearance,
                                                         Array.Empty<string>());
        run.Appearance = character.Appearance;
        return new ArenaFighter(character.Id, character.Name, character.Appearance, run, meta.Believers,
                                meta.UnlockedAbilities.ToList());
    }

    // Obergrenzen für Kämpfer aus dem Netz. Großzügig für echte Spielstände, aber endlich.
    private const int MaxNameLength = 20;
    private const long MaxBelievers = 1_000_000;
    private const int MaxLevel = 999;
    private const int MaxItems = 100;
    private const int MaxArmorHits = 99;

    /// <summary>
    /// Macht einen Kämpfer unbedenklich, bevor daraus eine Figur entsteht. Nötig für Kämpfer aus dem
    /// Netz: Die Daten kommen ungeprüft an, und etwa eine riesige Zahl Upgrade-Stapel ließe
    /// <see cref="PlayerFactory"/> endlos schleifen. Unbekannte Ids (fremde Mods) fallen weg,
    /// Zahlen werden auf gültige Bereiche geklemmt. Für eigene Gestalten ändert sich nichts.
    /// </summary>
    public ArenaFighter Sanitize(ArenaFighter fighter)
    {
        RunState source = fighter.Run;
        var run = new RunState
        {
            ClassId = _definitions.Classes.Contains(source.ClassId) ? source.ClassId : _definitions.Classes.All.First().Id,
            WorldId = source.WorldId,
            Level = Math.Clamp(source.Level, 1, MaxLevel),
            Appearance = fighter.Appearance,
            ArmorDurability = Math.Clamp(source.ArmorDurability, 0, MaxArmorHits),
            Underwear = source.Underwear,
        };
        // Where + ToDictionary: nur bekannte Einträge, Werte geklemmt (Stufe bzw. Stapel der Definition)
        foreach (var (abilityId, level) in source.AbilityLevels.Where(pair => _definitions.Abilities.Contains(pair.Key)))
            run.AbilityLevels[abilityId] = Math.Clamp(level, 1, _definitions.Abilities.Get(abilityId).MaxLevel);
        foreach (var (upgradeId, stacks) in source.UpgradeStacks.Where(pair => _definitions.Upgrades.Contains(pair.Key)))
            run.UpgradeStacks[upgradeId] = Math.Clamp(stacks, 0, _definitions.Upgrades.Get(upgradeId).MaxStacks);
        run.Items.AddRange(source.Items.Where(_definitions.Items.Contains).Take(MaxItems));
        foreach (var (slot, itemId) in source.Equipped)
            if (Enum.IsDefined(slot) && _definitions.Items.Contains(itemId)) run.Equipped[slot] = itemId;

        string name = new string(fighter.Name.Where(character => !char.IsControl(character)).Take(MaxNameLength).ToArray());
        List<string> gifts = fighter.EternalGifts.Where(_definitions.Abilities.Contains)
                                                 .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        CharacterAppearance look = fighter.Appearance with { Name = name };   // "with": Kopie mit geändertem Namen
        run.Appearance = look;
        return fighter with   // Record-Kopie: nur die geprüften Teile ersetzen
        {
            Name = name,
            Appearance = look,
            Run = run,
            Believers = Math.Clamp(fighter.Believers, 0, MaxBelievers),
            EternalGifts = gifts,
        };
    }

    /// <summary>
    /// Der Kampfplatz: EIN Raum, der Thronsaal des Gegners mit seiner Arena-Geometrie. Stärke wie
    /// in dem Verlies, in dem er sonst wartet; zu zweit hält er mehr aus (<see cref="DungeonPlan.PartySize"/>).
    /// </summary>
    public DungeonPlan CreatePlan(ArenaOpponent opponent, int partySize, int seed)
    {
        BalanceDefinition balance = _definitions.Balance;
        float difficulty = opponent.Circle.Difficulty * (1f + balance.DifficultyStepPerDungeon * opponent.DungeonIndex);
        return new DungeonPlan(
            Circle: opponent.Circle,
            CircleIndex: opponent.CircleIndex,
            DungeonIndex: opponent.DungeonIndex,
            IsBossDungeon: true,
            DifficultyMultiplier: difficulty,
            PathLength: 1,   // ein einziger Raum: Man steht sofort im Thronsaal
            ArenaCount: 0,
            WavesPerArena: 0,
            TreasureBranches: 0,
            Seed: seed,
            HasPrison: false,
            PuzzleKey: "",
            LeverCount: 0,
            DetourCount: 0,
            ChestCount: 0,
            CollectibleCount: 0,
            ArenaBoss: opponent.Id,
            PartySize: Math.Max(1, partySize));
    }

    /// <summary>
    /// Musik des Kampfes: das eigene Stück der Arena, sonst das Boss-Stück des Kreises.
    /// Dieselbe Reihenfolge wie im Thronsaal eines Laufs (DungeonScene).
    /// </summary>
    public string MusicOf(ArenaOpponent opponent, Func<string, bool> hasMusic)
    {
        if (_definitions.Arenas.TryGet(opponent.Id, out ArenaDefinition? arena) && arena.Music.Length > 0 && hasMusic(arena.Music))
            return arena.Music;
        return opponent.Circle.BossMusic.Length > 0 ? opponent.Circle.BossMusic : opponent.Circle.Music;
    }
}

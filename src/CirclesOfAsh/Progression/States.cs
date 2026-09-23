using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.Progression;

/// <summary>
/// Aussehen aus dem Charakter-Editor. Die Zahlen sind Indizes in die Listen aus appearance.json
/// -> Mods können Farben austauschen, ohne alte Spielstände zu brechen.
/// </summary>
public sealed record CharacterAppearance(string Name, int SkinTone, int HairStyle, int HairColor, int AccentColor)
{
    public static CharacterAppearance Default { get; } = new("Namenloser", 0, 0, 0, 0);
}

/// <summary>
/// Zustand des AKTUELLEN Laufs. Geht beim Tod komplett verloren (Roguelike) – inklusive Klasse.
/// </summary>
public sealed class RunState
{
    public string ClassId { get; set; } = "";
    public string WorldId { get; set; } = "";
    public int CircleIndex { get; set; }
    public int DungeonIndex { get; set; }
    public int Level { get; set; } = 1;
    public float Experience { get; set; }
    public int Seed { get; set; }
    public Dictionary<string, int> AbilityLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> UpgradeStacks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> CompanionIds { get; set; } = new();
    public CharacterAppearance Appearance { get; set; } = CharacterAppearance.Default;
    /// <summary>Gefundene Items (Duplikate möglich). Gehen beim Tod verloren.</summary>
    public List<string> Items { get; set; } = new();
    /// <summary>Pro Slot höchstens ein ausgerüstetes Item.</summary>
    public Dictionary<ItemSlot, string> Equipped { get; set; } = new();

    /// <summary>
    /// Tiefe Kopie. Der Dungeon arbeitet auf einer Kopie; nur bei Erfolg wird sie übernommen.
    /// Wer mitten im Dungeon aufgibt, startet diesen Dungeon also mit dem alten Stand neu.
    /// </summary>
    public RunState Clone() => new()
    {
        ClassId = ClassId,
        WorldId = WorldId,
        CircleIndex = CircleIndex,
        DungeonIndex = DungeonIndex,
        Level = Level,
        Experience = Experience,
        Seed = Seed,
        AbilityLevels = new Dictionary<string, int>(AbilityLevels, StringComparer.OrdinalIgnoreCase),
        UpgradeStacks = new Dictionary<string, int>(UpgradeStacks, StringComparer.OrdinalIgnoreCase),
        CompanionIds = new List<string>(CompanionIds),
        Appearance = Appearance,   // Record ist unveränderlich -> Referenz teilen ist sicher
        Items = new List<string>(Items),
        Equipped = new Dictionary<ItemSlot, string>(Equipped),
    };
}

/// <summary>
/// Meta-Progression: bleibt über alle Läufe erhalten. Gläubige (teilweise), permanente Boss-Fähigkeiten,
/// freigeschaltete Begleiter, befreite Welten.
/// </summary>
public sealed class MetaState
{
    public long Believers { get; set; }
    public int Deaths { get; set; }
    public int RunsStarted { get; set; }
    public HashSet<string> UnlockedAbilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UnlockedCompanions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LiberatedWorlds { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Bitten der Gläubigen: Id -> Fortschritt. Nicht enthalten = noch nicht angenommen.</summary>
    public Dictionary<string, MissionProgress> Missions { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Vorgemerkter Fortschritt für verfügbare Bitten (wird bei Annahme übernommen).</summary>
    public Dictionary<string, int> PendingMissionProgress { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Schon befreite Kerker ("seed:kreis:dungeon") -> Belohnung nur einmal pro Lauf.</summary>
    public HashSet<string> RescuedPrisons { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum MissionStatus { Active, Completed }

public sealed class MissionProgress
{
    public MissionStatus Status { get; set; }
    public int Progress { get; set; }
}

/// <summary>Ergebnis einer Gefangenenbefreiung.</summary>
public sealed record RescueResult(bool AlreadyRescued, int Believers, string? CompanionName, IReadOnlyList<MissionDefinition> CompletedMissions);

/// <summary>Was nach einem geschafften Dungeon passiert ist (für die Anzeige).</summary>
public sealed class DungeonOutcome
{
    public int BelieversGained { get; set; }
    public string? UnlockedAbilityId { get; set; }
    public List<string> UnlockedCompanionIds { get; } = new();
    public string? LiberatedWorldName { get; set; }
    public bool CircleCompleted { get; set; }
    public bool GameCompleted { get; set; }
    public List<MissionDefinition> CompletedMissions { get; } = new();
}

/// <summary>"record struct" = Werttyp-Record: klein, unveränderlich, ohne Heap-Allokation.</summary>
public readonly record struct DeathReport(long BelieversBefore, long BelieversAfter, int ReachedCircle, string ClassName);

namespace CirclesOfAsh.Combat;

/// <summary>
/// Alle Werte, die Upgrades, Klassen, Items und Gläubige beeinflussen können.
/// Neuer Wert? Hier ergänzen + ggf. in <see cref="StatSheet.Defaults"/> einen Startwert setzen.
/// </summary>
public enum StatType
{
    MaxHealth,
    MaxMana,
    ManaRegen,
    MoveSpeed,
    JumpPower,
    Might,              // Schadensmultiplikator (1.0 = 100 %)
    Armor,              // flache Schadensreduktion pro Treffer
    CooldownReduction,  // 0.2 = 20 % kürzere Abklingzeiten (max. 75 %)
    AreaSize,           // Multiplikator für Radien
    PickupRadius,
    StealthDamage,      // Multiplikator für den ersten Treffer aus der Tarnung
    LightRadius,        // Radius des eigenen Lichts in Pixeln (Laternen erhöhen ihn)
}

/// <summary>Ein einzelner Bonus. "readonly record struct" = kleiner, unveränderlicher Werttyp.</summary>
public readonly record struct StatModifier(StatType Stat, float Amount, bool IsPercent);

/// <summary>
/// Wertetabelle nach dem Muster: Endwert = (Basis + Summe Flach) * (1 + Summe Prozent).
/// Boni sind nach QUELLEN gruppiert ("permanent", "equipment" ...). Eine Quelle kann komplett ersetzt
/// werden (Ausrüstung wechseln), ohne andere Boni zu berühren. Ergebnisse werden gecacht.
/// </summary>
public sealed class StatSheet
{
    public const string PermanentSource = "permanent";

    public static readonly IReadOnlyDictionary<StatType, float> Defaults = new Dictionary<StatType, float>
    {
        [StatType.Might] = 1f,
        [StatType.AreaSize] = 1f,
        [StatType.StealthDamage] = 1f,
        [StatType.PickupRadius] = 32f,
        [StatType.JumpPower] = 460f,
        [StatType.MoveSpeed] = 110f,
        [StatType.LightRadius] = 72f,
    };

    private readonly Dictionary<StatType, float> _base = new();
    private readonly Dictionary<string, List<StatModifier>> _sources = new();
    private readonly Dictionary<StatType, float> _cache = new();

    public StatSheet(IReadOnlyDictionary<StatType, float> baseValues)
    {
        foreach (var (stat, value) in Defaults) _base[stat] = value;
        foreach (var (stat, value) in baseValues) _base[stat] = value;
    }

    // Indexer: erlaubt die Schreibweise stats[StatType.Might]
    public float this[StatType stat]
    {
        get
        {
            if (_cache.TryGetValue(stat, out float cached)) return cached;
            float flat = 0f, percent = 0f;
            foreach (List<StatModifier> modifiers in _sources.Values)
            {
                foreach (StatModifier modifier in modifiers)
                {
                    if (modifier.Stat != stat) continue;
                    if (modifier.IsPercent) percent += modifier.Amount;
                    else flat += modifier.Amount;
                }
            }
            float value = ((_base.TryGetValue(stat, out float baseValue) ? baseValue : 0f) + flat) * (1f + percent);
            _cache[stat] = value;
            return value;
        }
    }

    public void AddFlat(StatType stat, float amount) => AddModifier(PermanentSource, new StatModifier(stat, amount, false));
    public void AddPercent(StatType stat, float amount) => AddModifier(PermanentSource, new StatModifier(stat, amount, true));

    /// <summary>Ersetzt alle Boni einer Quelle (z. B. "equipment" nach dem Ausrüsten).</summary>
    public void SetSource(string sourceId, IEnumerable<StatModifier> modifiers)
    {
        _sources[sourceId] = modifiers.ToList();
        _cache.Clear();   // Cache ungültig -> Werte werden beim nächsten Zugriff neu berechnet
    }

    private void AddModifier(string sourceId, StatModifier modifier)
    {
        if (!_sources.TryGetValue(sourceId, out List<StatModifier>? modifiers))
        {
            modifiers = new List<StatModifier>();
            _sources[sourceId] = modifiers;
        }
        modifiers.Add(modifier);
        _cache.Clear();
    }

    /// <summary>Parst Stat-Namen aus JSON ("maxHealth" -> StatType.MaxHealth), ohne Groß-/Kleinschreibung.</summary>
    public static bool TryParse(string name, out StatType stat) => Enum.TryParse(name, ignoreCase: true, out stat);

    public static Dictionary<StatType, float> ParseAll(IReadOnlyDictionary<string, float> raw)
    {
        var parsed = new Dictionary<StatType, float>();
        foreach (var (name, value) in raw)
        {
            if (TryParse(name, out StatType stat)) parsed[stat] = value;
        }
        return parsed;
    }
}

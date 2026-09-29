using CirclesOfAsh.Localization;

namespace CirclesOfAsh.Combat;

/// <summary>
/// Anzeigenamen der Werte für Inventar und Beschreibungen. Früher stand dort der Enum-Name
/// ("LightRadius", "Might") – in keiner Sprache ein Wort, das ein Spieler kennt.
/// </summary>
public static class StatNames
{
    /// <summary>
    /// switch-Ausdruck mit "=>" je Fall: kompakter als eine switch-Anweisung, und der Compiler
    /// warnt, wenn ein neuer StatType dazukommt, der hier fehlt (Fall "_" fängt ihn trotzdem ab).
    /// </summary>
    public static string Of(StatType stat) => stat switch
    {
        StatType.MaxHealth => Loc.T("Leben"),
        StatType.MaxMana => Loc.T("Mana"),
        StatType.ManaRegen => Loc.T("Mana pro Sekunde"),
        StatType.MoveSpeed => Loc.T("Tempo"),
        StatType.JumpPower => Loc.T("Sprungkraft"),
        StatType.Might => Loc.T("Macht"),
        StatType.Armor => Loc.T("Rüstung"),
        StatType.CooldownReduction => Loc.T("Abklingzeit"),
        StatType.AreaSize => Loc.T("Wirkungsbereich"),
        StatType.PickupRadius => Loc.T("Sammelradius"),
        StatType.StealthDamage => Loc.T("Tarnungsschaden"),
        StatType.LightRadius => Loc.T("Lichtradius"),
        _ => stat.ToString(),
    };
}

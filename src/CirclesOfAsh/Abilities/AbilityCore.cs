using CirclesOfAsh.Combat;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Abilities;

/// <summary>
/// Strategy-Pattern: Jede Fähigkeitsart (Projektil, Nova, Orbit ...) ist eine eigene Klasse mit
/// dieser Schnittstelle. Welche Klasse zu einer Fähigkeit gehört, steht im JSON ("behavior").
/// Methoden mit Rumpf in einem Interface = "Default Interface Methods" (C# 8): Implementierungen
/// müssen nur überschreiben, was sie brauchen.
/// </summary>
public interface IAbilityBehavior
{
    /// <summary>Einmalig beim Erlernen (z. B. Doppelsprung aktiviert sich hier).</summary>
    void OnEquip(Player owner, AbilityInstance ability) { }

    /// <summary>Auslösen. false = nichts passiert (kein Ziel) -> keine Abklingzeit, kein Manaverbrauch.</summary>
    bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability);

    /// <summary>Jeden Frame, z. B. für dauerhaft kreisende Orbs.</summary>
    void Update(DungeonWorld world, Player owner, AbilityInstance ability, float deltaSeconds) { }

    void Draw(SpriteBatch spriteBatch, DungeonWorld world, Player owner, AbilityInstance ability) { }
}

/// <summary>
/// Eine erlernte Fähigkeit: Definition (Bauplan) + Behavior (Logik) + Laufzeitzustand (Stufe, Abklingzeit).
/// Stufenabhängige Werte werden berechnet statt gespeichert -> keine Inkonsistenzen beim Aufstufen.
/// </summary>
public sealed class AbilityInstance
{
    public AbilityInstance(AbilityDefinition definition, IAbilityBehavior behavior, int level)
    {
        Definition = definition;
        Behavior = behavior;
        Level = Math.Clamp(level, 1, definition.MaxLevel);
    }

    public AbilityDefinition Definition { get; }
    public IAbilityBehavior Behavior { get; }
    public int Level { get; set; }
    public float CooldownRemaining { get; set; }
    public bool IsMaxLevel => Level >= Definition.MaxLevel;

    public float BaseDamage => Definition.Damage + Definition.DamagePerLevel * (Level - 1);

    public int Count => Definition.Count +
        (Definition.CountEveryLevels > 0 ? (Level - 1) / Definition.CountEveryLevels : 0);   // Ganzzahldivision rundet ab

    public float EffectiveCooldown(StatSheet stats) =>
        Definition.Cooldown * (1f - Math.Clamp(stats[StatType.CooldownReduction], 0f, 0.75f));

    public float Area(StatSheet stats) => Definition.Radius * stats[StatType.AreaSize];

    /// <summary>Schaden inkl. Macht (Might) und optionalem Tarnungsbonus.</summary>
    public float ComputeDamage(Player owner, float stealthMultiplier) => BaseDamage * owner.Stats[StatType.Might] * stealthMultiplier;
}

using CirclesOfAsh.Abilities;
using CirclesOfAsh.Combat;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;

namespace CirclesOfAsh.Progression;

/// <summary>
/// Factory-Pattern: baut aus dem gespeicherten Lauf (RunState) + Meta-Fortschritt einen spielbereiten Spieler.
/// Reihenfolge der Boni: Klassen-Basiswerte -> Gläubigen-Bonus -> Upgrades aus diesem Lauf -> Ausrüstung.
/// </summary>
public static class PlayerFactory
{
    public static Player Create(GameContext context, RunState run, Vector2 spawn)
    {
        DefinitionRegistry definitions = context.Definitions;
        ClassDefinition playerClass = definitions.Classes.Get(run.ClassId);

        var stats = new StatSheet(StatSheet.ParseAll(playerClass.BaseStats));
        BalanceDefinition balance = definitions.Balance;
        stats.AddPercent(StatType.Might, context.Progression.BelieverBonus(balance.MightPerHundredBelievers));
        stats.AddPercent(StatType.MaxHealth, context.Progression.BelieverBonus(balance.HealthPerHundredBelievers));

        foreach (var (upgradeId, stacks) in run.UpgradeStacks)
        {
            if (!definitions.Upgrades.Contains(upgradeId)) continue;
            for (int stack = 0; stack < stacks; stack++) LevelUpService.ApplyStatUpgrade(stats, definitions.Upgrades.Get(upgradeId));
        }

        var player = new Player(playerClass, CharacterVisuals.Create(context, playerClass, run.Appearance), stats, spawn);

        // Fähigkeiten aus dem Lauf + permanent freigeschaltete (Boss-Belohnungen), ohne Duplikate
        var abilityLevels = new Dictionary<string, int>(run.AbilityLevels, StringComparer.OrdinalIgnoreCase);
        foreach (string permanentId in context.Progression.Meta.UnlockedAbilities)
            abilityLevels.TryAdd(permanentId, 1);   // TryAdd überschreibt nicht, falls schon vorhanden

        foreach (var (abilityId, level) in abilityLevels)
        {
            if (!definitions.Abilities.Contains(abilityId)) continue;
            AbilityDefinition definition = definitions.Abilities.Get(abilityId);
            player.AddAbility(new AbilityInstance(definition, context.Behaviors.CreateAbility(definition.Behavior), level));
        }

        EquipmentService.Apply(definitions, run, player);   // Ausrüstung zuletzt: eigene Stat-Quelle "equipment"
        return player;
    }

    public static IEnumerable<Companion> CreateCompanions(GameContext context, RunState run, Vector2 spawn)
    {
        int slot = 0;
        foreach (string companionId in run.CompanionIds)
        {
            if (!context.Definitions.Companions.Contains(companionId)) continue;
            CompanionDefinition definition = context.Definitions.Companions.Get(companionId);
            yield return new Companion(definition, context.Assets.GetSpriteSheet(definition.SpriteSheet),
                context.Behaviors.CreateCompanion(definition.Behavior), slot++, spawn);
        }
    }
}

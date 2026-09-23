using CirclesOfAsh.Abilities;
using CirclesOfAsh.Combat;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;

namespace CirclesOfAsh.Progression;

public enum UpgradeOfferKind { NewAbility, AbilityLevel, Stat }

/// <summary>Eine Wahlmöglichkeit beim Stufenaufstieg.</summary>
public sealed record UpgradeOffer(UpgradeOfferKind Kind, string TargetId, string Title, string Description);

/// <summary>Erstellt Level-Up-Angebote (Vampire-Survivors-Stil: 1 aus 3) und wendet die Wahl an.</summary>
public static class LevelUpService
{
    public static List<UpgradeOffer> CreateOffers(GameContext context, RunState run, Player player, Random random)
    {
        DefinitionRegistry definitions = context.Definitions;
        ClassDefinition playerClass = definitions.Classes.Get(run.ClassId);
        var candidates = new List<UpgradeOffer>();

        foreach (string abilityId in playerClass.AbilityPool)
        {
            AbilityDefinition ability = definitions.Abilities.Get(abilityId);
            AbilityInstance? owned = player.FindAbility(abilityId);
            if (owned is null)
                candidates.Add(new UpgradeOffer(UpgradeOfferKind.NewAbility, abilityId, $"Neu: {ability.Name}", ability.Description));
            else if (!owned.IsMaxLevel)
                candidates.Add(new UpgradeOffer(UpgradeOfferKind.AbilityLevel, abilityId, $"{ability.Name} Stufe {owned.Level + 1}", ability.Description));
        }
        candidates.AddRange(StatOffers(definitions, run));

        // Mischen und die ersten N nehmen
        return candidates.OrderBy(_ => random.Next()).Take(definitions.Balance.UpgradeChoices).ToList();
    }

    public static UpgradeOffer? CreateRandomStatOffer(GameContext context, RunState run, Random random) =>
        StatOffers(context.Definitions, run).OrderBy(_ => random.Next()).FirstOrDefault();

    private static IEnumerable<UpgradeOffer> StatOffers(DefinitionRegistry definitions, RunState run) =>
        definitions.Upgrades.All
            .Where(upgrade => run.UpgradeStacks.GetValueOrDefault(upgrade.Id) < upgrade.MaxStacks)
            .Select(upgrade => new UpgradeOffer(UpgradeOfferKind.Stat, upgrade.Id, upgrade.Name, upgrade.Description));

    public static void Apply(UpgradeOffer offer, GameContext context, RunState run, Player player)
    {
        DefinitionRegistry definitions = context.Definitions;
        switch (offer.Kind)
        {
            case UpgradeOfferKind.NewAbility:
                AbilityDefinition definition = definitions.Abilities.Get(offer.TargetId);
                run.AbilityLevels[offer.TargetId] = 1;
                player.AddAbility(new AbilityInstance(definition, context.Behaviors.CreateAbility(definition.Behavior), 1));
                break;
            case UpgradeOfferKind.AbilityLevel:
                AbilityInstance? ability = player.FindAbility(offer.TargetId);
                if (ability is null) return;
                ability.Level++;
                run.AbilityLevels[offer.TargetId] = ability.Level;
                break;
            case UpgradeOfferKind.Stat:
                UpgradeDefinition upgrade = definitions.Upgrades.Get(offer.TargetId);
                run.UpgradeStacks[offer.TargetId] = run.UpgradeStacks.GetValueOrDefault(offer.TargetId) + 1;
                ApplyStatUpgrade(player.Stats, upgrade);
                player.RefreshDerivedStats();
                break;
        }
    }

    public static void ApplyStatUpgrade(StatSheet stats, UpgradeDefinition upgrade)
    {
        if (!StatSheet.TryParse(upgrade.Stat, out StatType stat)) return;
        if (upgrade.IsPercent) stats.AddPercent(stat, upgrade.Amount);
        else stats.AddFlat(stat, upgrade.Amount);
    }
}

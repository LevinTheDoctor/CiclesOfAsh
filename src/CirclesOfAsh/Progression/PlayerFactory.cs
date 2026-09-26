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

        var player = new Player(playerClass,
            CharacterVisuals.Create(context, playerClass, run.Appearance,
                EquipmentService.ArmorSprite(context.Definitions, run, context.Assets), run.Underwear),
            stats, Vector2.Zero);
        // "spawn" ist die Mitte der Fuesse. Die Ecke erst JETZT ableiten, wenn die Kollisionsbox
        // feststeht - sie haengt an der Sprite-Groesse und ist keine feste Zahl mehr.
        player.Position = spawn - new Vector2(player.Size.X / 2f, player.Size.Y);

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
            string sheetId = Companions.CompanionSkins.SheetOf(context, definition);
            yield return new Companion(definition, context.Assets.GetSpriteSheet(sheetId), sheetId,
                context.Behaviors.CreateCompanion(definition.Behavior), slot++, spawn);
        }
    }

    /// <summary>
    /// Spieler für den Heimwelt-Hub: nutzt den gespeicherten Lauf (falls vorhanden) oder einen
    /// schlichten "Wandler"-Look. Ohne Kampf-Stats-Wirrwarr: Basiswerte + Gläubigen-Bonus reicht.
    /// </summary>
    /// <param name="bottomCenter">
    /// Standpunkt in PIXELN: Mitte der Füße, also der Punkt, auf dem die Figur steht.
    /// Bewusst nicht die linke obere Ecke – die hängt von der Figurgröße ab und lud zum
    /// Verwechseln mit Kachelkoordinaten ein. Gleiche Konvention wie bei <see cref="Npc"/>.
    /// </param>
    public static Player CreateHubPlayer(GameContext context, Vector2 bottomCenter)
    {
        ClassDefinition playerClass = context.Progression.CurrentRun is { } run
            ? context.Definitions.Classes.Get(run.ClassId)
            : context.Definitions.Classes.All.First();
        CharacterAppearance appearance = context.Progression.CurrentRun?.Appearance ?? CharacterAppearance.Default;
        var stats = new StatSheet(StatSheet.ParseAll(playerClass.BaseStats));
        stats.AddPercent(StatType.Might, context.Progression.BelieverBonus(context.Definitions.Balance.MightPerHundredBelievers));
        // Im Tempel traegt der Spieler seine Kleidung ebenfalls sichtbar - samt Verfallsstufe.
        RunState? hubRun = context.Progression.CurrentRun;
        string? hubArmor = hubRun is null ? null : EquipmentService.ArmorSprite(context.Definitions, hubRun, context.Assets);
        var player = new Player(playerClass,
            CharacterVisuals.Create(context, playerClass, appearance, hubArmor, hubRun?.Underwear ?? 0),
            stats, Vector2.Zero);
        // Erst jetzt ist die Größe der Kollisionsbox bekannt -> Ecke daraus ableiten.
        player.Position = bottomCenter - new Vector2(player.Size.X / 2f, player.Size.Y);
        return player;
    }
}

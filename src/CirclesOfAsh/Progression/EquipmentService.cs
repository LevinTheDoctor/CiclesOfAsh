using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;

namespace CirclesOfAsh.Progression;

/// <summary>Regeln für Items: aufnehmen, (ab)legen, Boni anwenden, Beute auswürfeln.</summary>
public static class EquipmentService
{
    public const string StatSource = "equipment";
    public static readonly Color DefaultLightColor = new(255, 214, 170);

    /// <summary>Fügt ein Item hinzu. Ist der Slot frei, wird es sofort angelegt (true).</summary>
    public static bool AddItem(RunState run, ItemDefinition item)
    {
        run.Items.Add(item.Id);
        if (item.Slot == ItemSlot.Collectible || run.Equipped.ContainsKey(item.Slot)) return false;
        run.Equipped[item.Slot] = item.Id;
        return true;
    }

    /// <summary>Anlegen oder – wenn schon angelegt – ablegen.</summary>
    public static void Toggle(RunState run, ItemDefinition item)
    {
        if (item.Slot == ItemSlot.Collectible) return;
        if (run.Equipped.TryGetValue(item.Slot, out string? current) && current == item.Id) run.Equipped.Remove(item.Slot);
        else run.Equipped[item.Slot] = item.Id;
    }

    public static bool IsEquipped(RunState run, ItemDefinition item) =>
        run.Equipped.TryGetValue(item.Slot, out string? current) && current == item.Id;

    public static IEnumerable<StatModifier> CollectModifiers(DefinitionRegistry definitions, RunState run)
    {
        foreach (string itemId in run.Equipped.Values)
        {
            if (!definitions.Items.Contains(itemId)) continue;
            foreach (StatModifierDefinition modifier in definitions.Items.Get(itemId).Modifiers)
            {
                if (StatSheet.TryParse(modifier.Stat, out StatType stat))
                    yield return new StatModifier(stat, modifier.Amount, modifier.IsPercent);
            }
        }
    }

    /// <summary>Ersetzt die Stat-Quelle "equipment" und setzt die Lichtfarbe der Laterne.</summary>
    public static void Apply(DefinitionRegistry definitions, RunState run, Player player)
    {
        player.Stats.SetSource(StatSource, CollectModifiers(definitions, run));
        player.LightColor = run.Equipped.TryGetValue(ItemSlot.Lamp, out string? lampId) && definitions.Items.Contains(lampId)
            ? ColorUtil.FromHex(definitions.Items.Get(lampId).LightColor, DefaultLightColor)
            : DefaultLightColor;
        player.RefreshDerivedStats();
    }

    /// <summary>
    /// Gewichtete Beute. Noch nicht besessene Items werden bevorzugt; gibt es keine mehr, null (-> Reliquie).
    /// Schatzkammern verschieben die Gewichte Richtung selten/heilig.
    /// </summary>
    public static ItemDefinition? RollLoot(DefinitionRegistry definitions, RunState run, int circleIndex, Random random, bool isTreasure)
    {
        List<ItemDefinition> pool = definitions.Items.All
            .Where(item => item.Slot != ItemSlot.Collectible && item.MinCircle <= circleIndex && !run.Items.Contains(item.Id))
            .ToList();
        if (pool.Count == 0) return null;

        // Lokale Funktion mit switch-Ausdruck: Seltenheit -> Gewicht
        int Weight(ItemDefinition item) => item.Rarity switch
        {
            ItemRarity.Common => isTreasure ? 3 : 10,
            ItemRarity.Rare => isTreasure ? 6 : 4,
            _ => isTreasure ? 6 : 1,
        };

        int roll = random.Next(pool.Sum(Weight));
        foreach (ItemDefinition item in pool)
        {
            roll -= Weight(item);
            if (roll < 0) return item;
        }
        return pool[^1];
    }
}

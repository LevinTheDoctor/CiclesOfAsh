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
        if (item.Slot == ItemSlot.Armor) run.ArmorDurability = ArmorHitsOf(item);
        return true;
    }

    /// <summary>Anlegen oder – wenn schon angelegt – ablegen.</summary>
    public static void Toggle(RunState run, ItemDefinition item)
    {
        if (item.Slot == ItemSlot.Collectible) return;
        if (run.Equipped.TryGetValue(item.Slot, out string? current) && current == item.Id) run.Equipped.Remove(item.Slot);
        else
        {
            run.Equipped[item.Slot] = item.Id;
            // Frisch angelegte Rüstung ist wieder ganz. Abgelegte behält ihren Zustand nicht –
            // das ist Absicht: Sonst könnte man Schaden durch Ab- und Anlegen zurücksetzen.
            if (item.Slot == ItemSlot.Armor) run.ArmorDurability = ArmorHitsOf(item);
        }
    }

    /// <summary>
    /// Wie viele Treffer diese Rüstung abfängt. Ohne ausdrückliche Angabe aus der Robustheit
    /// abgeleitet, damit vorhandene Daten ohne Änderung sinnvolle Werte ergeben
    /// (60 → 2, 110 → 3, 180 → 4, 300 → 5).
    ///
    /// Das "+ 1" ist Absicht: JEDE Rüstung fängt mindestens einen Treffer ganz ab, bevor sie
    /// zerspringt. Ohne das war das Lederwams bei 60 Robustheit sofort hin – man sah die
    /// Splitter, aber nie den Moment, in dem der Panzer einen Schlag schluckt.
    /// </summary>
    public static int ArmorHitsOf(ItemDefinition item) =>
        item.ArmorHits > 0 ? item.ArmorHits : Math.Clamp((int)MathF.Round(item.Durability / 60f) + 1, 1, 5);

    /// <summary>Ergebnis eines Treffers auf die Rüstung.</summary>
    public enum ArmorResult { None, Absorbed, Shattered }

    /// <summary>
    /// Die Rüstung fängt einen Treffer VOLLSTÄNDIG ab (Vorbild Ghosts 'n Goblins) und verliert
    /// dabei eine Stufe. Beim letzten Treffer zerspringt sie: abgelegt und aus dem Inventar weg.
    /// </summary>
    public static ArmorResult AbsorbHit(DefinitionRegistry definitions, RunState run)
    {
        if (!run.Equipped.TryGetValue(ItemSlot.Armor, out string? armorId) || !definitions.Items.Contains(armorId))
            return ArmorResult.None;

        // Alte Spielstände führten hier einen Schadenspool (z. B. 60) – auf die Trefferzahl klemmen.
        int maxHits = ArmorHitsOf(definitions.Items.Get(armorId));
        int remaining = Math.Clamp(run.ArmorDurability, 0, maxHits);
        if (remaining <= 0) return ArmorResult.None;

        remaining--;
        run.ArmorDurability = remaining;
        if (remaining > 0) return ArmorResult.Absorbed;

        run.Equipped.Remove(ItemSlot.Armor);
        run.Items.Remove(armorId);
        return ArmorResult.Shattered;
    }

    /// <summary>Sprite-Ebene der getragenen Rüstung, oder null (keine getragen / kein Bild hinterlegt).</summary>
    public static string? ArmorSprite(DefinitionRegistry definitions, RunState run)
    {
        if (!run.Equipped.TryGetValue(ItemSlot.Armor, out string? armorId) || !definitions.Items.Contains(armorId))
            return null;
        string sprite = definitions.Items.Get(armorId).Sprite;
        return string.IsNullOrEmpty(sprite) ? null : sprite;
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

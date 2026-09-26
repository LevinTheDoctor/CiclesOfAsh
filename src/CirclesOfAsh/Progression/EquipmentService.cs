using CirclesOfAsh.Assets;
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
    /// Kleinste Trefferzahl einer Rüstung. Bei weniger als <see cref="Stages"/> Treffern wären
    /// nicht alle Verfallsstufen zu sehen – der Verfall ist aber der halbe Reiz.
    /// </summary>
    public const int MinArmorHits = Stages;

    /// <summary>
    /// Wie viele Treffer dieses Stück abfängt. Ohne ausdrückliche Angabe aus der Robustheit
    /// abgeleitet, damit vorhandene Daten ohne Änderung sinnvolle Werte ergeben.
    ///
    /// UNZERSTÖRBARE Rüstung gibt es nicht: Auch ein Stück ohne jede Angabe kommt über die
    /// Untergrenze auf <see cref="MinArmorHits"/> Treffer. Das ist der Sinn der Mechanik – alles,
    /// was man trägt, geht irgendwann kaputt, bis auf die Unterwäsche.
    /// </summary>
    public static int ArmorHitsOf(ItemDefinition item) =>
        item.ArmorHits > 0
            ? item.ArmorHits
            : Math.Clamp((int)MathF.Round(item.Durability / 60f) + 1, MinArmorHits, 6);

    /// <summary>Ergebnis eines Treffers auf die Rüstung.</summary>
    public enum ArmorResult { None, Absorbed, Shattered }

    /// <summary>
    /// Was der Treffer angerichtet hat. Mehr als das <see cref="ArmorResult"/>, weil die Meldung
    /// im Spiel den Namen des Stücks braucht: "Deine Rüstung zerspringt" ist für eine Rußrobe
    /// schlicht falsch.
    /// </summary>
    /// <param name="Stage">Verfallsstufe NACH dem Treffer (0 heil … <see cref="Stages"/>-1 zerfetzt).</param>
    /// <param name="StageChanged">Ob der Treffer eine neue Stufe erreicht hat – nur dann lohnt eine Meldung.</param>
    public readonly record struct ArmorHit(ArmorResult Result, string ItemName, int Stage, bool StageChanged)
    {
        public static ArmorHit None { get; } = new(ArmorResult.None, "", 0, false);
    }

    /// <summary>
    /// Das Stück fängt einen Treffer VOLLSTÄNDIG ab (Vorbild Ghosts 'n Goblins) und verliert dabei
    /// einen Treffer seines Vorrats. Beim letzten zerfällt es: abgelegt und aus dem Inventar weg.
    /// </summary>
    public static ArmorHit AbsorbHit(DefinitionRegistry definitions, RunState run)
    {
        if (!run.Equipped.TryGetValue(ItemSlot.Armor, out string? armorId) || !definitions.Items.Contains(armorId))
            return ArmorHit.None;

        ItemDefinition item = definitions.Items.Get(armorId);
        // Alte Spielstände führten hier einen Schadenspool (z. B. 60) – auf die Trefferzahl klemmen.
        int maxHits = ArmorHitsOf(item);
        int remaining = Math.Clamp(run.ArmorDurability, 0, maxHits);
        if (remaining <= 0) return ArmorHit.None;

        int stageBefore = StageOf(remaining, maxHits);
        remaining--;
        run.ArmorDurability = remaining;
        if (remaining > 0)
        {
            int stage = StageOf(remaining, maxHits);
            return new ArmorHit(ArmorResult.Absorbed, item.Name, stage, stage != stageBefore);
        }

        run.Equipped.Remove(ItemSlot.Armor);
        run.Items.Remove(armorId);
        return new ArmorHit(ArmorResult.Shattered, item.Name, Stages - 1, true);
    }

    /// <summary>
    /// Sichtbare Verfallsstufen eines Kleidungsstücks: heil, angeschlagen, zerfetzt. Danach ist es
    /// weg. Drei Stufen, weil ein einzelner Sprung von "ganz" auf "hin" den Verfall verschluckt.
    /// </summary>
    public const int Stages = 3;

    /// <summary>
    /// Anhängsel je Stufe. Der Index ist die Stufe, die heile Fassung trägt gar keins – so bleibt
    /// die Sprite-Id eines Items auch ohne Verfallsbilder gültig.
    /// </summary>
    public static readonly string[] StageSuffixes = { "", ".worn", ".broken" };

    /// <summary>Menschenlesbare Stufennamen für Inventar und Meldungen.</summary>
    public static readonly string[] StageNames = { "heil", "angeschlagen", "zerfetzt" };

    /// <summary>
    /// Verfallsstufe aus verbleibenden und höchstmöglichen Treffern. Gleichmäßig in Drittel
    /// geteilt, damit auch das kleinste Stück (drei Treffer) jede Stufe genau einmal zeigt.
    /// </summary>
    public static int StageOf(int remaining, int maxHits)
    {
        if (maxHits <= 0 || remaining <= 0) return Stages - 1;
        if (remaining * Stages > maxHits * 2) return 0;
        return remaining * Stages > maxHits ? 1 : 2;
    }

    /// <summary>Verfallsstufe des getragenen Stücks.</summary>
    public static int ArmorStage(ItemDefinition item, RunState run)
    {
        int maxHits = ArmorHitsOf(item);
        return StageOf(Math.Clamp(run.ArmorDurability, 0, maxHits), maxHits);
    }

    /// <summary>
    /// Sprite-Ebene der getragenen Kleidung, oder null (nichts getragen / kein Bild hinterlegt).
    ///
    /// Mit jedem Drittel des Vorrats kommt die nächste Verfallsfassung – man soll den Panzer
    /// aufgehen sehen, statt erst beim Zerfallen etwas zu merken. Fehlt ein Verfallsbild, wird auf
    /// die nächstniedrigere Stufe zurückgefallen: Ein Stück ohne eigene Schadensfassung sieht dann
    /// eben unverändert aus, statt als magenta Platzhalter zu erscheinen.
    /// </summary>
    public static string? ArmorSprite(DefinitionRegistry definitions, RunState run, AssetManager? assets = null)
    {
        if (!run.Equipped.TryGetValue(ItemSlot.Armor, out string? armorId) || !definitions.Items.Contains(armorId))
            return null;
        ItemDefinition item = definitions.Items.Get(armorId);
        if (string.IsNullOrEmpty(item.Sprite)) return null;
        return StageSprite(item.Sprite, ArmorStage(item, run), assets);
    }

    /// <summary>Sprite-Id einer Stufe, mit Rückfall auf die nächstniedrigere vorhandene Fassung.</summary>
    public static string StageSprite(string sprite, int stage, AssetManager? assets = null)
    {
        for (int step = Math.Clamp(stage, 0, Stages - 1); step > 0; step--)
        {
            string candidate = sprite + StageSuffixes[step];
            if (assets is null || assets.HasSpriteSheet(candidate)) return candidate;
        }
        return sprite;
    }

    /// <summary>Verbleibende Treffer und Höchstzahl der getragenen Rüstung – für die Anzeige im HUD.</summary>
    public static (int Remaining, int Max) ArmorHits(DefinitionRegistry definitions, RunState run)
    {
        if (!run.Equipped.TryGetValue(ItemSlot.Armor, out string? armorId) || !definitions.Items.Contains(armorId))
            return (0, 0);
        int maxHits = ArmorHitsOf(definitions.Items.Get(armorId));
        return (Math.Clamp(run.ArmorDurability, 0, maxHits), maxHits);
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
            .Where(item => item.Lootable && item.Slot != ItemSlot.Collectible
                           && item.MinCircle <= circleIndex && !run.Items.Contains(item.Id))
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

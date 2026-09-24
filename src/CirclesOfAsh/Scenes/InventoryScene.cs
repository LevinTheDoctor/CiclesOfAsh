using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Inventar als Overlay. Enter legt ein Item an oder ab (ein Item pro Slot).
/// Im Dungeon (player != null) wirkt die Änderung sofort, im Kreis-Menü wird der Lauf gespeichert.
/// </summary>
public sealed class InventoryScene : SceneBase
{
    private readonly RunState _run;
    private readonly Player? _player;
    private MenuList _menu = new();
    private List<ItemDefinition> _items = new();

    public InventoryScene(GameContext context, RunState run, Player? player) : base(context)
    {
        _run = run;
        _player = player;
        Rebuild(0);
    }

    public override bool IsOverlay => true;

    private void Rebuild(int selectedIndex)
    {
        DefinitionSet<ItemDefinition> definitions = Context.Definitions.Items;
        // Distinct: Duplikate nur einmal anzeigen; OrderBy/ThenBy sortiert nach Slot, dann Name
        _items = _run.Items.Distinct()
            .Where(definitions.Contains)
            .Select(definitions.Get)
            .Where(item => item.Slot != ItemSlot.Collectible)
            .OrderBy(item => item.Slot).ThenBy(item => item.Name)
            .ToList();

        _menu = new MenuList();
        foreach (ItemDefinition item in _items)
        {
            ItemDefinition captured = item;
            string marker = EquipmentService.IsEquipped(_run, item) ? " (angelegt)" : "";
            // Bei getragener Rüstung zeigt die Zeile, wie viel sie noch aushält.
            string wear = item.Slot == ItemSlot.Armor && EquipmentService.IsEquipped(_run, item) && item.Durability > 0
                ? $"  {_run.ArmorDurability}/{item.Durability}"
                : "";
            _menu.Add($"[{SlotName(item.Slot)}] {item.Name}{marker}{wear}", () => Toggle(captured), hint: Describe(item));
        }
        _menu.Select(selectedIndex);
    }

    private void Toggle(ItemDefinition item)
    {
        EquipmentService.Toggle(_run, item);
        if (_player is not null) EquipmentService.Apply(Context.Definitions, _run, _player);
        else Context.Progression.SaveRun(_run);
        Rebuild(_menu.SelectedIndex);
    }

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Cancel)) Context.Scenes.Pop();
        else _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D pixel = Context.Assets.Pixel;
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.7f);
        var panel = new Rectangle(50, 24, CirclesGame.VirtualWidth - 100, 222);
        UiDraw.Panel(spriteBatch, pixel, panel);
        Context.TitleFont.DrawCentered(spriteBatch, "Inventar", centerX, panel.Top + 4, Palette.Gold);

        if (_items.Count == 0)
        {
            Context.Font.DrawCentered(spriteBatch, "Noch nichts gefunden. Öffne Truhen in den Verliesen!", centerX, panel.Top + 80, Palette.Ash);
        }
        else
        {
            _menu.Draw(spriteBatch, Context.Font, centerX, panel.Top + 32, maxVisible: 9);
            string hint = _menu.Selected?.Hint ?? "";
            Context.Font.DrawCenteredLines(spriteBatch, Context.Font.Wrap(hint, panel.Width - 24), centerX, panel.Bottom - 44, Palette.Bone * 0.85f);
        }
        Context.Font.DrawCentered(spriteBatch,
            $"{Context.Input.Glyph(GameAction.Confirm)} anlegen/ablegen · {Context.Input.Glyph(GameAction.Cancel)} zurück",
            centerX, panel.Bottom - 12, Palette.Ash);
        spriteBatch.End();
    }

    private static string SlotName(ItemSlot slot) => slot switch
    {
        ItemSlot.Lamp => "Laterne",
        ItemSlot.Amulet => "Amulett",
        ItemSlot.Ring => "Ring",
        ItemSlot.Armor => "Rüstung",
        _ => "Fund",
    };

    /// <summary>Beschreibung + Boni lesbar, z. B. "+30 LightRadius · +5 % Might".</summary>
    private static string Describe(ItemDefinition item)
    {
        IEnumerable<string> bonuses = item.Modifiers.Select(modifier =>
            modifier.IsPercent ? $"{modifier.Amount * 100:+0;-0} % {modifier.Stat}" : $"{modifier.Amount:+0.##;-0.##} {modifier.Stat}");
        string rarity = item.Rarity switch { ItemRarity.Sacred => "Heilig", ItemRarity.Rare => "Selten", _ => "Gewöhnlich" };
        return $"{rarity}: {item.Description}\n{string.Join(" · ", bonuses)}";
    }
}

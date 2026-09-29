using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Glutschmiede im Tempel: der bezahlte Weg zurück zu einer Rüstung.
///
/// Kleidung IST Rüstung und zerfällt in drei Stufen bis auf die Unterwäsche – bisher ohne jeden
/// Weg zurück. Hier gibt es zwei, beide gegen Gläubige:
///   FLICKEN     hebt die getragene Kleidung um eine Stufe, höchstens bis zu dem, was das Stück kann.
///   NEU WEBEN   nur, wenn gar nichts mehr getragen wird: die Startkleidung der Klasse, zerfetzt.
///
/// Der Flickpreis steigt mit jeder Nutzung im selben Lauf (<see cref="RunState.ForgeUses"/>).
/// Das ist Absicht: Der Verfall soll spürbar bleiben, sonst wäre der Ghosts-'n-Goblins-Reiz weg.
/// Die eigentliche Regel steht in <see cref="EquipmentService.Mend"/> – dieselbe, die auch der
/// Trauernde Engel im Verlies benutzt.
/// </summary>
public sealed class ForgeScene : SceneBase
{
    private readonly RunState _run;
    private MenuList _menu = new();
    private string _notice = "";

    public ForgeScene(GameContext context, RunState run) : base(context)
    {
        _run = run;
        BuildMenu();
    }

    public override bool IsOverlay => true;

    private BalanceDefinition Balance => Context.Definitions.Balance;

    /// <summary>Was das Flicken beim nächsten Mal kostet – steigt mit jeder Nutzung im Lauf.</summary>
    private int MendCost => Balance.ForgeMendCost + Balance.ForgeCostPerUse * _run.ForgeUses;

    private ClassDefinition? PlayerClass =>
        Context.Definitions.Classes.Contains(_run.ClassId) ? Context.Definitions.Classes.Get(_run.ClassId) : null;

    private void BuildMenu()
    {
        int selected = _menu.SelectedIndex;
        _menu = new MenuList();
        long believers = Context.Progression.Meta.Believers;
        (int remaining, int max) = EquipmentService.ArmorHits(Context.Definitions, _run);

        if (max > 0)
        {
            bool whole = remaining >= max;
            _menu.Add(
                whole ? "Flicken – nicht nötig, sie ist heil" : $"Flicken ({MendCost} Gläubige)",
                () => Mend(MendCost),
                isEnabled: !whole && believers >= MendCost,
                hint: whole ? null : $"Hebt die Kleidung um eine Stufe. Du hast {believers} Gläubige.");
        }
        else
        {
            bool canWeave = !string.IsNullOrEmpty(PlayerClass?.StartingArmor);
            _menu.Add(
                canWeave ? $"Neu weben ({Balance.ForgeWeaveCost} Gläubige)" : "Neu weben – deine Klasse trägt nichts",
                () => Mend(Balance.ForgeWeaveCost),
                isEnabled: canWeave && believers >= Balance.ForgeWeaveCost,
                hint: canWeave
                    ? $"Webt deine Startkleidung aus Asche – zerfetzt, nicht heil. Du hast {believers} Gläubige."
                    : null);
        }

        _menu.Add("Zurück", () => Context.Scenes.Pop());
        _menu.Select(Math.Min(selected, _menu.Count - 1));
    }

    private void Mend(int cost)
    {
        if (Context.Progression.Meta.Believers < cost)
        {
            _notice = $"Nicht genug Gläubige ({cost} nötig).";
            Context.Audio.Play("error", 0.5f);
            return;
        }

        EquipmentService.MendOutcome outcome =
            EquipmentService.Mend(Context.Definitions, _run, PlayerClass, Balance.ForgeMendHits);
        if (!outcome.Changed)
        {
            // Nichts geändert heißt: nichts bezahlen. Der Knopf ist dann ohnehin gesperrt, aber
            // die Regel steht hier, nicht im Menü – sonst kostet ein künftiger zweiter Aufrufer.
            _notice = outcome.Result == EquipmentService.MendResult.AlreadyWhole
                ? "Sie ist heil. Die Esse bleibt kalt."
                : "Es ist nichts da, woraus sich etwas weben ließe.";
            Context.Audio.Play("error", 0.5f);
            return;
        }

        Context.Progression.Meta.Believers -= cost;
        _run.ForgeUses++;
        Context.Progression.SaveMeta();
        Context.Progression.SaveRun(_run);
        // Kein EquipmentService.Apply hier: Im Tempel gibt es keine Kampffigur, deren Werte zu
        // setzen waeren - die entsteht erst beim naechsten Abstieg aus dem gespeicherten Lauf
        // (PlayerFactory). Die Tempelfigur zieht sich um, sobald HubScene die Aenderung bemerkt.

        _notice = outcome.Result == EquipmentService.MendResult.Reweaved
            ? $"{outcome.ItemName} neu gewoben – {EquipmentService.StageNames[outcome.Stage]} (-{cost} Gläubige)."
            : $"{outcome.ItemName} geflickt – {EquipmentService.StageNames[outcome.Stage]} (-{cost} Gläubige).";
        Context.Audio.Play("unseal", 0.7f);
        BuildMenu();
    }

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Cancel))
        {
            Context.Scenes.Pop();
            return;
        }
        _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D pixel = Context.Assets.Pixel;
        float centerX = CirclesGame.VirtualWidth / 2f;

        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.65f);
        var panel = new Rectangle(70, 48, CirclesGame.VirtualWidth - 140, 170);
        UiDraw.Panel(spriteBatch, pixel, panel);
        Context.TitleFont.DrawCentered(spriteBatch, "Glutschmiede", centerX, panel.Top + 6, Palette.Gold);

        float y = panel.Top + 32;
        (int remaining, int max) = EquipmentService.ArmorHits(Context.Definitions, _run);
        if (max > 0)
        {
            int stage = EquipmentService.StageOf(remaining, max);
            string name = Context.Definitions.Items.Contains(_run.Equipped[ItemSlot.Armor])
                ? Context.Definitions.Items.Get(_run.Equipped[ItemSlot.Armor]).Name
                : "Kleidung";
            Context.Font.DrawCentered(spriteBatch, $"{name}: {EquipmentService.StageNames[stage]} ({remaining}/{max} Treffer)",
                centerX, y, Palette.Bone);
        }
        else
        {
            Context.Font.DrawCentered(spriteBatch, "Du trägst nur noch Unterwäsche.", centerX, y, Palette.Ash);
        }
        y += 16;
        Context.Font.DrawCentered(spriteBatch, $"Gläubige: {Context.Progression.Meta.Believers}", centerX, y, Palette.Faith);

        _menu.Draw(spriteBatch, Context.Font, centerX, y + 22);

        if (_notice.Length > 0)
            Context.Font.DrawCentered(spriteBatch, _notice, centerX, panel.Bottom - 30, Palette.Gold);
        Context.Font.DrawCentered(spriteBatch, "Esc: zurück", centerX, panel.Bottom - 16, Palette.Ash);
        spriteBatch.End();
    }
}

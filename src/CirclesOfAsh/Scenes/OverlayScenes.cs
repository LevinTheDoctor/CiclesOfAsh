using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>Level-Up: 1 aus N wählen. Overlay -> der Dungeon bleibt sichtbar, ist aber pausiert.</summary>
public sealed class LevelUpScene : SceneBase
{
    private readonly MenuList _menu = new();
    private readonly List<UpgradeOffer> _offers;

    public LevelUpScene(GameContext context, List<UpgradeOffer> offers, RunState run, Player player) : base(context)
    {
        _offers = offers;
        foreach (UpgradeOffer offer in offers)
        {
            _menu.Add(offer.Title, () =>
            {
                LevelUpService.Apply(offer, Context, run, player);
                Context.Scenes.Pop();
            }, hint: offer.Description);
        }
    }

    public override bool IsOverlay => true;

    public override void Update(float deltaSeconds) => _menu.Update(Context.Input, Context.Audio);

    public override void Draw(SpriteBatch spriteBatch)
    {
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, Context.Assets.Pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.5f);
        var panel = new Rectangle(70, 50, CirclesGame.VirtualWidth - 140, 60 + _offers.Count * 14 + 30);
        UiDraw.Panel(spriteBatch, Context.Assets.Pixel, panel);
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Göttliche Eingebung"), centerX, panel.Top + 6, Palette.Gold);
        _menu.Draw(spriteBatch, Context.Font, centerX, panel.Top + 40);
        string hint = Context.Font.Wrap(_menu.Selected?.Hint ?? "", panel.Width - 20);
        Context.Font.DrawCentered(spriteBatch, hint, centerX, panel.Bottom - 24, Palette.Bone * 0.8f);
        spriteBatch.End();
    }
}

public sealed class PauseScene : SceneBase
{
    private readonly RunState _run;
    private readonly Player _player;
    private MenuList _menu = new();

    public PauseScene(GameContext context, RunState run, Player player) : base(context)
    {
        _run = run;
        _player = player;
        BuildMenu();
    }

    /// <summary>Das Optionsmenü liegt über der Pause – nach einem Sprachwechsel neu beschriften.</summary>
    public override void OnLanguageChanged() => BuildMenu();

    private void BuildMenu()
    {
        int selected = _menu.SelectedIndex;
        _menu = new MenuList();
        _menu.Add(Loc.T("Fortsetzen"), () => Context.Scenes.Pop());
        _menu.Add(Loc.T("Inventar"), () => Context.Scenes.Push(new InventoryScene(Context, _run, _player)));
        // Optionen gehoeren in JEDES Pausenmenue. Bisher kam man nur vom Titel und aus dem Tempel
        // heran - mitten im Verlies liess sich die Lautstaerke nicht mehr aendern.
        _menu.Add(Loc.T("Optionen"), () => Context.Scenes.Push(new SettingsScene(Context)));
        _menu.Add(Loc.T("Zum Titel (Verlies startet neu)"), () =>
        {
            Context.Progression.SaveMeta();   // Missionsfortschritt & Befreiungen nicht verlieren
            Context.Scenes.Replace(new TitleScene(Context));
        });
        _menu.Select(selected);
    }

    public override bool IsOverlay => true;

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Pause)) Context.Scenes.Pop();
        else _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, Context.Assets.Pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.6f);
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Innehalten"), CirclesGame.VirtualWidth / 2f, 80, Palette.Gold);
        _menu.Draw(spriteBatch, Context.Font, CirclesGame.VirtualWidth / 2f, 124);
        spriteBatch.End();
    }
}

/// <summary>
/// Sicherheitsabfrage vor unumkehrbaren Aktionen (Gestalt löschen, Lauf aufgeben). Overlay: Die
/// Szene darunter bleibt sichtbar. "Nein" steht bewusst oben und ist vorausgewählt – wer aus
/// Versehen zweimal bestätigt, verliert so nichts.
/// </summary>
public sealed class ConfirmScene : SceneBase
{
    private readonly string _title;
    private readonly string _question;
    private readonly MenuList _menu = new();

    public ConfirmScene(GameContext context, string title, string question, string confirmLabel, Action onConfirm) : base(context)
    {
        _title = title;
        _question = question;
        _menu.Add(Loc.T("Nein"), () => Context.Scenes.Pop());
        _menu.Add(confirmLabel, () =>
        {
            // Erst schließen, dann handeln: onConfirm darf selbst Szenen wechseln. Beides landet
            // in der Warteschlange des SceneManagers und wird in genau dieser Reihenfolge ausgeführt.
            Context.Scenes.Pop();
            onConfirm();
        });
    }

    public override bool IsOverlay => true;

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Cancel)) Context.Scenes.Pop();
        else _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float centerX = CirclesGame.VirtualWidth / 2f;
        BitmapFont font = Context.Font;
        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, Context.Assets.Pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.6f);
        var panel = new Rectangle(90, 70, CirclesGame.VirtualWidth - 180, 124);
        UiDraw.Panel(spriteBatch, Context.Assets.Pixel, panel);
        Context.TitleFont.DrawCentered(spriteBatch, _title, centerX, panel.Top + 6, Palette.Ember);
        font.DrawCenteredLines(spriteBatch, font.Wrap(_question, panel.Width - 24), centerX, panel.Top + 42, Palette.Bone);
        _menu.Draw(spriteBatch, font, centerX, panel.Bottom - 36);
        spriteBatch.End();
    }
}

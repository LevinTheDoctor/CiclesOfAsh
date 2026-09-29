using CirclesOfAsh.Core;
using CirclesOfAsh.Localization;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>Pause im Heimwelt-Hub: Fortsetzen, Optionen, Zum Titel.</summary>
public sealed class HubPauseScene : SceneBase
{
    private MenuList _menu = new();

    public HubPauseScene(GameContext context) : base(context) => BuildMenu();

    /// <summary>Das Optionsmenü liegt über der Pause – nach einem Sprachwechsel neu beschriften.</summary>
    public override void OnLanguageChanged() => BuildMenu();

    private void BuildMenu()
    {
        int selected = _menu.SelectedIndex;
        _menu = new MenuList();
        _menu.Add(Loc.T("Fortsetzen"), () => Context.Scenes.Pop());
        _menu.Add(Loc.T("Optionen"), () => Context.Scenes.Push(new SettingsScene(Context)));
        _menu.Add(Loc.T("Zum Titel"), () => Context.Scenes.Replace(new TitleScene(Context)));
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
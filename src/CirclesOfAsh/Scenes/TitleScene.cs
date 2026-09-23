using CirclesOfAsh.Core;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

public sealed class TitleScene : SceneBase
{
    private readonly MenuList _menu = new();
    private float _time;

    public TitleScene(GameContext context) : base(context) { }

    public override void OnEnter()
    {
        ProgressionService progression = Context.Progression;
        if (progression.CurrentRun is not null)
        {
            _menu.Add("Abstieg fortsetzen", () => Context.Scenes.Replace(new CircleIntroScene(Context)));
            _menu.Add("Lauf aufgeben (zählt als Tod)", () =>
                Context.Scenes.Replace(new GameOverScene(Context, progression.HandleDeath())));
        }
        else
        {
            _menu.Add("Neuer Lauf", () => Context.Scenes.Replace(new CharacterCreatorScene(Context)));
        }
        _menu.Add("Tempel der Gläubigen", () => Context.Scenes.Push(new MissionBoardScene(Context)));
        _menu.Add("Beenden", Context.RequestExit);
    }

    public override void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.35f);
        float centerX = CirclesGame.VirtualWidth / 2f;
        InfernoFunnel.Draw(spriteBatch, Context.Assets.Pixel, new Vector2(centerX, 150), 460f, 90f, 7, -1, _time * 0.4f, animate: true);

        Texture2D logo = Context.Assets.GetTexture("ui.logo");
        spriteBatch.Draw(logo, new Vector2(centerX - logo.Width / 2f, 4 + MathF.Sin(_time) * 2f), Color.White);
        _menu.Draw(spriteBatch, Context.Font, centerX, 126);

        MetaState meta = Context.Progression.Meta;
        Context.Font.DrawCentered(spriteBatch, $"Gläubige: {meta.Believers}   ·   Tode: {meta.Deaths}   ·   Ewige Gaben: {meta.UnlockedAbilities.Count}",
            centerX, CirclesGame.VirtualHeight - 30, Palette.Faith);
        Context.Font.DrawCentered(spriteBatch, "A/D laufen · Leertaste springen · Shift Dash · Q/E Gaben · F benutzen · Esc Pause",
            centerX, CirclesGame.VirtualHeight - 16, Palette.Ash);
        spriteBatch.End();
    }
}

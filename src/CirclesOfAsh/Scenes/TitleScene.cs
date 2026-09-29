using CirclesOfAsh.Core;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

public sealed class TitleScene : SceneBase
{
    private MenuList _menu = new();
    private float _time;

    public TitleScene(GameContext context) : base(context) { }

    public override void OnEnter()
    {
        Context.Music.Play("music.title");
        BuildMenu();
    }

    /// <summary>Nach einem Sprachwechsel im Optionsmenü (das über dem Titel liegt) neu beschriften.</summary>
    public override void OnLanguageChanged() => BuildMenu();

    private void BuildMenu()
    {
        int selected = _menu.SelectedIndex;
        _menu = new MenuList();
        ProgressionService progression = Context.Progression;
        if (progression.CurrentRun is not null)
        {
            _menu.Add(Loc.T("Abstieg fortsetzen"), () => Context.Scenes.Replace(new HubScene(Context)));
            _menu.Add(Loc.T("Lauf aufgeben (zählt als Tod)"), () =>
                Context.Scenes.Replace(new GameOverScene(Context, progression.HandleDeath())));
        }
        else
        {
            _menu.Add(Loc.T("Neuer Lauf"), () => Context.Scenes.Replace(new CharacterCreatorScene(Context)));
        }
        // Ohne laufenden Abstieg gibt es keinen begehbaren Tempel (kein Spielerfigur-Zustand) –
        // dann bleibt das Missionsbrett als Menü erreichbar.
        if (progression.CurrentRun is null)
            _menu.Add(Loc.T("Bitten der Gläubigen"), () => Context.Scenes.Push(new MissionBoardScene(Context)));
        // Optionen gehören auch ins Hauptmenü: Schwierigkeit, Audio und Controller-Profile sollen
        // vor dem ersten Abstieg einstellbar sein – nicht erst über die Pause im Tempel.
        _menu.Add(Loc.T("Optionen"), () => Context.Scenes.Push(new SettingsScene(Context)));
        _menu.Add(Loc.T("Beenden"), Context.RequestExit);
        _menu.Select(selected);
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
        // Ringzahl aus der Welt lesen statt fest verdrahten: Der Titel zeigt sonst sieben Ringe,
        // waehrend das Inferno neun Kreise tief ist.
        int rings = Context.Definitions.Worlds.All.FirstOrDefault()?.Circles.Count ?? 9;
        InfernoFunnel.Draw(spriteBatch, Context.Assets.Pixel, new Vector2(centerX, 150), 460f, 90f, rings, -1, _time * 0.4f, animate: true);

        UiDraw.Logo(spriteBatch, Context, centerX, 4 + MathF.Sin(_time) * 2f);
        _menu.Draw(spriteBatch, Context.Font, centerX, 126);

        MetaState meta = Context.Progression.Meta;
        Context.Font.DrawCentered(spriteBatch,
            Loc.T("Gläubige: {0}   ·   Tode: {1}   ·   Ewige Gaben: {2}", meta.Believers, meta.Deaths, meta.UnlockedAbilities.Count),
            centerX, CirclesGame.VirtualHeight - 30, Palette.Faith);
        InputState input = Context.Input;
        string controls = input.HasGamePad
            ? Loc.T("Stick laufen · {0} springen · {1} Dash · {2}/{3} Gaben · {4} benutzen · {5} Pause",
                input.Glyph(GameAction.Jump), input.Glyph(GameAction.Dash), input.Glyph(GameAction.AbilityOne),
                input.Glyph(GameAction.AbilityTwo), input.Glyph(GameAction.Interact), input.Glyph(GameAction.Pause))
            : Loc.T("A/D laufen · Leertaste springen · Shift Dash · Q/E Gaben · F benutzen · Esc Pause");
        Context.Font.DrawCentered(spriteBatch, controls,
            centerX, CirclesGame.VirtualHeight - 16, Palette.Ash);
        spriteBatch.End();
    }
}

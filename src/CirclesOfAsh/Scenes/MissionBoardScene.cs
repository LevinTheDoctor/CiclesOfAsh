using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Tempel der Gläubigen: Bitten annehmen (max. N gleichzeitig) oder aufgeben.
/// Gesperrte Bitten zeigen, ab wie vielen Gläubigen sie erscheinen -> Anreiz für Meta-Fortschritt.
/// </summary>
public sealed class MissionBoardScene : SceneBase
{
    private MenuList _menu = new();

    public MissionBoardScene(GameContext context) : base(context) => Rebuild(0);

    public override bool IsOverlay => true;
    private MissionService Missions => Context.Progression.Missions;

    private void Rebuild(int selectedIndex)
    {
        _menu = new MenuList();
        string confirm = Context.Input.Glyph(GameAction.Confirm);
        foreach (MissionDefinition mission in Missions.Active)
        {
            MissionDefinition captured = mission;
            _menu.Add($"{mission.Title}  {Missions.ProgressOf(mission)}/{mission.Count}", () => Change(() => Missions.Abandon(captured)),
                hint: Loc.T("{0}: \"{1}\"\nBelohnung: {2} Gläubige · {3}: Bitte aufgeben", mission.Giver, mission.Text, mission.RewardBelievers, confirm));
        }
        foreach (MissionDefinition mission in Missions.Available)
        {
            MissionDefinition captured = mission;
            string hintSuffix = Missions.CanAccept ? Loc.T("{0}: annehmen", confirm) : Loc.T("Du kannst höchstens {0} Bitten tragen.", Missions.MaxActive);
            _menu.Add(Loc.T("Neu: {0}", mission.Title), () => Change(() => Missions.Accept(captured)), Missions.CanAccept,
                hint: Loc.T("{0}: \"{1}\"\nBelohnung: {2} Gläubige · {3}", mission.Giver, mission.Text, mission.RewardBelievers, hintSuffix));
        }
        foreach (MissionDefinition mission in Missions.Locked)
            _menu.Add(Loc.T("??? (ab {0} Gläubigen)", mission.RequiredBelievers), () => { }, isEnabled: false);   // "() => { }" = leere Aktion
        _menu.Select(selectedIndex);
    }

    /// <summary>Änderung ausführen, sofort speichern, Menü neu aufbauen.</summary>
    private void Change(Action change)
    {
        change();
        Context.Progression.SaveMeta();
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
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.75f);
        var panel = new Rectangle(34, 16, CirclesGame.VirtualWidth - 68, 238);
        UiDraw.Panel(spriteBatch, pixel, panel);
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Bitten der Gläubigen"), centerX, panel.Top + 4, Palette.Gold);
        Context.Font.DrawCentered(spriteBatch, Loc.T("Aktiv {0}/{1} · Erfüllt {2}", Missions.Active.Count(), Missions.MaxActive, Missions.Completed.Count()),
            centerX, panel.Top + 26, Palette.Ash);

        if (_menu.Count == 0) Context.Font.DrawCentered(spriteBatch, Loc.T("Alle Bitten sind erfüllt. Die Gläubigen danken dir."), centerX, 110, Palette.Faith);
        else _menu.Draw(spriteBatch, Context.Font, centerX, panel.Top + 42, maxVisible: 8);

        string hint = _menu.Selected?.Hint ?? "";
        Context.Font.DrawCenteredLines(spriteBatch, Context.Font.Wrap(hint, panel.Width - 24), centerX, panel.Bottom - 48, Palette.Bone * 0.85f);
        Context.Font.DrawCentered(spriteBatch, Loc.T("{0} zurück", Context.Input.Glyph(GameAction.Cancel)), centerX, panel.Bottom - 12, Palette.Ash);
        spriteBatch.End();
    }
}

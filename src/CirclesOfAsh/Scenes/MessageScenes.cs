using CirclesOfAsh.Core;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>Allgemeiner Textbildschirm mit Titel, Text und "Weiter"-Aktion. Wiederverwendbar (DRY).</summary>
public sealed class MessageScene : SceneBase
{
    private readonly string _title;
    private readonly string _body;
    private readonly Color _titleColor;
    private readonly Action _onContinue;

    public MessageScene(GameContext context, string title, string body, Color titleColor, Action onContinue) : base(context)
    {
        _title = title;
        _body = body;
        _titleColor = titleColor;
        _onContinue = onContinue;
    }

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Confirm)) _onContinue();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.65f);
        Context.TitleFont.DrawCentered(spriteBatch, _title, centerX, 40, _titleColor);
        var panel = new Rectangle(60, 84, CirclesGame.VirtualWidth - 120, 130);
        UiDraw.Panel(spriteBatch, Context.Assets.Pixel, panel);
        Context.Font.DrawShadowed(spriteBatch, Context.Font.Wrap(_body, panel.Width - 20), new Vector2(panel.Left + 10, panel.Top + 10), Palette.Bone);
        Context.Font.DrawCentered(spriteBatch, Loc.T("{0}: weiter", Context.Input.Glyph(GameAction.Confirm)), centerX, CirclesGame.VirtualHeight - 20, Palette.Ash);
        spriteBatch.End();
    }
}

/// <summary>Permadeath-Bildschirm: Was verloren ging, was bleibt.</summary>
public sealed class GameOverScene : SceneBase
{
    private readonly DeathReport _report;

    public GameOverScene(GameContext context, DeathReport report) : base(context) => _report = report;

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Confirm)) Context.Scenes.Replace(new TitleScene(Context));
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.75f);
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Du bist gefallen"), centerX, 50, Palette.Blood);
        Context.Font.DrawCentered(spriteBatch, Loc.T("Die Gestalt des {0} ist vergessen. Dein Abstieg endete im {1}. Kreis.", _report.ClassName, _report.ReachedCircle), centerX, 100, Palette.Bone);
        Context.Font.DrawCentered(spriteBatch, Loc.T("Gläubige: {0}  ›  {1} bleiben dir treu.", _report.BelieversBefore, _report.BelieversAfter), centerX, 120, Palette.Faith);
        Context.Font.DrawCentered(spriteBatch, Loc.T("Deine ewigen Gaben und Begleitseelen bleiben erhalten."), centerX, 140, Palette.Soul);
        Context.Font.DrawCentered(spriteBatch, Loc.T("{0}: zurück zum Titel", Context.Input.Glyph(GameAction.Confirm)), centerX, CirclesGame.VirtualHeight - 20, Palette.Ash);
        spriteBatch.End();
    }
}

/// <summary>Entscheidet anhand des Dungeon-Ergebnisses, welcher Bildschirm als Nächstes kommt.</summary>
public static class ResultSceneFactory
{
    public static IScene Create(GameContext context, DungeonOutcome outcome)
    {
        var lines = new List<string> { Loc.T("+{0} Gläubige schließen sich dir an.", outcome.BelieversGained) };
        if (outcome.UnlockedAbilityId is not null)
        {
            var ability = context.Definitions.Abilities.Get(outcome.UnlockedAbilityId);
            lines.Add(Loc.T("EWIGE GABE: {0} – {1} (bleibt auch nach dem Tod)", ability.Name, ability.Description));
        }
        foreach (string companionId in outcome.UnlockedCompanionIds)
            lines.Add(Loc.T("Neue Begleitseele erwacht: {0}", context.Definitions.Companions.Get(companionId).Name));
        if (outcome.LiberatedWorldName is not null) lines.Add(Loc.T("{0} ist befreit!", outcome.LiberatedWorldName));
        foreach (var mission in outcome.CompletedMissions)
            lines.Add(Loc.T("Bitte erfüllt: {0} (+{1} Gläubige)", mission.Title, mission.RewardBelievers));

        string title = outcome.GameCompleted ? Loc.T("Das Inferno ist gebrochen")
            : outcome.CircleCompleted ? Loc.T("Der Kreis ist befreit")
            : Loc.T("Das Verlies ist geläutert");

        // Ein Bildschirm mit ZWEI Wegen statt einer Taste: tiefer oder heim. Das Spiel schob den
        // Spieler vorher nach jedem Verlies ungefragt in den Tempel zurück.
        // Beim Spielende gibt es nichts zu wählen - dann bleibt es beim schlichten Textbildschirm.
        if (outcome.GameCompleted)
            return new MessageScene(context, title, string.Join("\n\n", lines), Palette.Gold,
                                    () => context.Scenes.Replace(new TitleScene(context)));
        return new DescentChoiceScene(context, outcome, title, lines);
    }
}

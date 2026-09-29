using CirclesOfAsh.Core;
using CirclesOfAsh.Localization;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Nach dem Arena-Kampf: Sieg, Niederlage, verlassen oder Verbindung verloren – mit Gegner und
/// Kampfzeit. Nichts davon kostet etwas; ein Sieg steht im Werdegang der Gestalt.
/// </summary>
public sealed class ArenaResultScene : SceneBase
{
    private readonly ArenaOutcome _outcome;
    private readonly ArenaLobbyChoice _choice;

    public ArenaResultScene(GameContext context, ArenaOutcome outcome, ArenaLobbyChoice choice) : base(context)
    {
        _outcome = outcome;
        _choice = choice;
    }

    public override void OnEnter()
    {
        Context.Music.Play("music.title");
        if (_outcome.Result == ArenaResult.Victory) Context.Audio.Play("levelup", 0.7f);
    }

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Confirm) || Context.Input.WasPressed(GameAction.Cancel))
            Context.Scenes.Replace(new ArenaLobbyScene(Context, _choice));
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.7f);

        // Tupel-Dekonstruktion aus einem switch-Ausdruck: Überschrift und Farbe je Ergebnis
        (string title, Color color) = _outcome.Result switch
        {
            ArenaResult.Victory => (Loc.T("Sieg!"), Palette.Gold),
            ArenaResult.Defeat => (Loc.T("Niederlage"), Palette.Blood),
            ArenaResult.ConnectionLost => (Loc.T("Verbindung verloren"), Palette.Ember),
            _ => (Loc.T("Kampf verlassen"), Palette.Ash),
        };
        Context.TitleFont.DrawCentered(spriteBatch, title, centerX, 50, color);

        string clock = TimeSpan.FromSeconds(_outcome.Seconds).ToString(@"m\:ss");
        // Ohne Verb, das zu einer oder zwei Gestalten passen müsste: Die Namen stehen für sich.
        string summary = _outcome.Result switch
        {
            ArenaResult.Victory => Loc.T("{0} ist bezwungen – nach {1}.", _outcome.OpponentName, clock),
            ArenaResult.Defeat => Loc.T("{0} war zu stark – nach {1}.", _outcome.OpponentName, clock),
            ArenaResult.ConnectionLost => Loc.T("Der Kampf gegen {0} brach nach {1} ab.", _outcome.OpponentName, clock),
            _ => Loc.T("Der Kampf gegen {0} wurde nach {1} verlassen.", _outcome.OpponentName, clock),
        };
        Context.Font.DrawCentered(spriteBatch, summary, centerX, 96, Palette.Bone);
        Context.Font.DrawCentered(spriteBatch, string.Join(" & ", _outcome.FighterNames), centerX, 110, Palette.Faith);
        string note = _outcome.Result == ArenaResult.Victory
            ? Loc.T("Der Sieg steht im Werdegang der Gestalt.")
            : Loc.T("In der Arena kostet eine Niederlage nichts.");
        Context.Font.DrawCentered(spriteBatch, note, centerX, 128, Palette.Soul);
        Context.Font.DrawCentered(spriteBatch, Loc.T("{0}: zurück zur Arena", Context.Input.Glyph(GameAction.Confirm)),
                                  centerX, CirclesGame.VirtualHeight - 20, Palette.Ash);
        spriteBatch.End();
    }
}

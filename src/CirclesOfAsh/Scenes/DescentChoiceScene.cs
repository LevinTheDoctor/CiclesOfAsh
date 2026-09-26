using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Der Bildschirm nach einem geschafften Verlies: Beute, Gläubige – und die Entscheidung,
/// ob es SOFORT weitergeht oder zurück in den Tempel.
///
/// Vorher landete man nach jedem Verlies ohne Wahl wieder im Tempel und musste sich erneut durch
/// das Höllentor klicken. Der Abstieg hatte damit keinen Zug nach unten. Jetzt ist der Tempel die
/// sichere Entscheidung (der Lauf ist gespeichert, beim nächsten Aufbruch steigt man an derselben
/// Stelle wieder ein), und Weitergehen ist die riskante: Wer unten stirbt, verliert Klasse, Items
/// und drei Viertel der Gläubigen.
///
/// Wichtig für die Anzeige: <see cref="ProgressionService.CompleteDungeon"/> hat die Indizes zu
/// diesem Zeitpunkt SCHON weitergestellt. Nach einem Bosskampf zeigt der Bildschirm deshalb bereits
/// den nächsten Kreis – genau das ist gewollt, man sieht, wohin es ginge.
/// </summary>
public sealed class DescentChoiceScene : SceneBase
{
    private readonly DungeonOutcome _outcome;
    private readonly string _title;
    private readonly IReadOnlyList<string> _lines;
    private readonly MenuList _menu = new();
    private float _time;

    public DescentChoiceScene(GameContext context, DungeonOutcome outcome, string title, IReadOnlyList<string> lines)
        : base(context)
    {
        _outcome = outcome;
        _title = title;
        _lines = lines;
    }

    private RunState? Run => Context.Progression.CurrentRun;

    public override void OnEnter()
    {
        // Ohne Lauf (Welt befreit, Spiel durch) gibt es nichts mehr zu entscheiden.
        if (Run is not { } run)
        {
            _menu.Add("Weiter", () => Context.Scenes.Replace(new TitleScene(Context)));
            return;
        }

        string deeper = _outcome.CircleCompleted
            ? $"Hinab in den {Context.Progression.CircleOf(run).Name}"
            : $"Weiter ins Verlies {run.DungeonIndex + 1}";
        _menu.Add(deeper, () => Context.Scenes.Replace(LoadingScene.ForDungeon(Context, run)),
                  hint: "Kein Halt, keine Ausrüstung, kein Missionsbrett. Stirbst du unten, ist der Lauf verloren.");
        _menu.Add("Inventar", () => Context.Scenes.Push(new InventoryScene(Context, run, player: null)),
                  hint: "Kleidung und Fundstücke anlegen, bevor es weitergeht.");
        _menu.Add("Zurück in den Tempel", () => Context.Scenes.Replace(new HubScene(Context)),
                  hint: "Sicher: Der Lauf ist gespeichert, du steigst später an dieser Stelle wieder ein.");
    }

    public override void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D pixel = Context.Assets.Pixel;
        RunState? run = Run;
        CircleDefinition? circle = run is null ? null : Context.Progression.CircleOf(run);
        int circles = run is null ? 1 : Context.Progression.WorldOf(run).Circles.Count;
        // Tiefe als ANTEIL, nicht als feste Schrittweite: Sonst liefe die Dunkelheit schon beim
        // vierten von neun Kreisen an den Anschlag und die untere Hälfte sähe überall gleich aus.
        float depth = run is null || circles <= 1 ? 1f : run.CircleIndex / (float)(circles - 1);
        UiDraw.Begin(spriteBatch);
        UiDraw.Backdrop(spriteBatch, Context, 0.5f + 0.35f * depth, circle?.Background ?? "");

        float centerX = CirclesGame.VirtualWidth / 2f;
        Context.TitleFont.DrawCentered(spriteBatch, _title, centerX, 16, Palette.Gold);

        var panel = new Rectangle(60, 40, CirclesGame.VirtualWidth - 120, 74);
        UiDraw.Panel(spriteBatch, pixel, panel);
        Context.Font.DrawShadowed(spriteBatch, Context.Font.Wrap(string.Join("\n", _lines), panel.Width - 16),
                                  new Vector2(panel.Left + 8, panel.Top + 6), Palette.Bone);

        if (run is not null && circle is not null)
        {
            InfernoFunnel.Draw(spriteBatch, pixel, new Vector2(centerX, 124), 150f, 46f,
                               circles, run.CircleIndex, _time, animate: false);
            Context.Font.DrawCentered(spriteBatch, $"Tiefe {run.CircleIndex + 1} / {circles} · {circle.Name}",
                                      centerX, 176, Palette.Ash);
        }
        _menu.Draw(spriteBatch, Context.Font, centerX, 192);
        spriteBatch.End();
    }
}

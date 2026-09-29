using System.Diagnostics;
using CirclesOfAsh.Core;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Ladebildschirm mit ECHTER Arbeit: Eine Warteschlange von Ladeschritten wird pro Frame mit Zeitbudget
/// abgearbeitet (das Bild bleibt flüssig). Dazu Logo, sinkender Höllentrichter, Asche und Tipps.
/// Eine Mindestdauer sorgt dafür, dass man Tipp und Animation auch sieht.
/// </summary>
public sealed class LoadingScene : SceneBase
{
    private const double FrameBudgetMilliseconds = 10.0;
    private const float FadeSeconds = 0.35f;

    private readonly Queue<(string Label, Action Work)> _steps;
    private readonly int _totalSteps;
    private readonly Func<IScene> _next;
    private readonly float _minimumSeconds;
    private readonly string _subtitle;
    private readonly string _tip;
    private readonly List<Vector2> _ash = new();
    private readonly Random _random = new();
    private string _currentLabel = "";
    private float _elapsed;
    private float _shownProgress;
    private float _fadeOut;

    public LoadingScene(GameContext context, IEnumerable<(string Label, Action Work)> steps, Func<IScene> next,
                        float minimumSeconds, string subtitle) : base(context)
    {
        _steps = new Queue<(string, Action)>(steps);
        _totalSteps = Math.Max(1, _steps.Count);
        _next = next;
        _minimumSeconds = minimumSeconds;
        _subtitle = subtitle;
        IReadOnlyList<LocalizedText> tips = context.Definitions.Tips;
        _tip = tips.Count > 0 ? tips[_random.Next(tips.Count)] : "";
        for (int index = 0; index < 70; index++)
            _ash.Add(new Vector2(_random.Next(CirclesGame.VirtualWidth), _random.Next(CirclesGame.VirtualHeight)));
    }

    /// <summary>Spielstart: alle Texturen, Spritesheets und Sounds vorladen.</summary>
    public static LoadingScene ForStartup(GameContext context)
    {
        var steps = new List<(string, Action)>();
        // "id" wird pro Schleifendurchlauf neu angelegt -> jedes Lambda fängt seinen eigenen Wert ein
        foreach (string id in context.Assets.TextureIds.ToList())
            steps.Add((Loc.T("Die Mauern werden errichtet …"), () => context.Assets.GetTexture(id)));
        foreach (string id in context.Assets.SpriteSheetIds.ToList())
            steps.Add((Loc.T("Die Verdammten erwachen …"), () => context.Assets.GetSpriteSheet(id)));
        foreach (string id in context.Audio.SoundIds.ToList())
            steps.Add((Loc.T("Die Glocken werden gestimmt …"), () => context.Audio.Preload(id)));
        return new LoadingScene(context, steps, () => new TitleScene(context), 2.6f, "");
    }

    /// <summary>Vor jedem Verlies: Dungeon generieren, während der Trichter tiefer sinkt.</summary>
    public static LoadingScene ForDungeon(GameContext context, RunState run)
    {
        DungeonScene? scene = null;
        string circle = context.Progression.CircleOf(run).Name;
        var steps = new List<(string, Action)> { (Loc.T("Das Verlies formt sich …"), () => scene = new DungeonScene(context, run)) };
        // "scene!" -> nach dem Schritt garantiert gesetzt
        return new LoadingScene(context, steps, () => scene!, 1.4f, Loc.T("Abstieg in {0}", circle));
    }

    public override void Update(float deltaSeconds)
    {
        _elapsed += deltaSeconds;
        var stopwatch = Stopwatch.StartNew();
        while (_steps.Count > 0 && stopwatch.Elapsed.TotalMilliseconds < FrameBudgetMilliseconds)
        {
            (string label, Action work) = _steps.Dequeue();
            _currentLabel = label;
            work();
        }

        float target = 1f - _steps.Count / (float)_totalSteps;
        // Anzeige zusätzlich an die Mindestdauer koppeln -> der Balken füllt sich gleichmäßig
        target = MathF.Min(target, _elapsed / _minimumSeconds);
        _shownProgress = MathUtil.Damp(_shownProgress, target, 8f, deltaSeconds);

        for (int index = 0; index < _ash.Count; index++)
        {
            Vector2 flake = _ash[index] + new Vector2(MathF.Sin(_elapsed + index) * 6f, 14f + index % 5 * 3f) * deltaSeconds;
            if (flake.Y > CirclesGame.VirtualHeight) flake = new Vector2(_random.Next(CirclesGame.VirtualWidth), -2f);
            _ash[index] = flake;
        }

        bool isDone = _steps.Count == 0 && _elapsed >= _minimumSeconds;
        if (!isDone) return;
        _fadeOut += deltaSeconds;
        if (_fadeOut >= FadeSeconds) Context.Scenes.Replace(_next());
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D pixel = Context.Assets.Pixel;
        float centerX = CirclesGame.VirtualWidth / 2f;
        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Palette.Void);

        InfernoFunnel.Draw(spriteBatch, pixel, new Vector2(centerX, 118), 400f, 80f, rings: 9, highlighted: -1, _elapsed, animate: true);
        // Glühender Schlund im Zentrum
        for (int glow = 0; glow < 4; glow++)
            InfernoFunnel.DrawEllipse(spriteBatch, pixel, new Vector2(centerX, 198), 20f - glow * 4f, 5f - glow, Palette.Ember * (0.3f + glow * 0.15f));

        foreach (Vector2 flake in _ash) UiDraw.Rect(spriteBatch, pixel, new Rectangle((int)flake.X, (int)flake.Y, 1, 1), Palette.Ash * 0.8f);

        float breathe = MathF.Sin(_elapsed * 1.5f) * 2f;
        UiDraw.Logo(spriteBatch, Context, centerX, 6 + breathe);
        if (_subtitle.Length > 0) Context.TitleFont.DrawCentered(spriteBatch, _subtitle, centerX, 120, Palette.Faith);

        var bar = new Rectangle((int)centerX - 110, 214, 220, 6);
        UiDraw.Bar(spriteBatch, pixel, bar, _shownProgress, Palette.Ember);
        UiDraw.Border(spriteBatch, pixel, new Rectangle(bar.X - 2, bar.Y - 2, bar.Width + 4, bar.Height + 4), Palette.Gold * 0.6f);
        Context.Font.DrawCentered(spriteBatch, _currentLabel, centerX, 224, Palette.Ash);
        Context.Font.DrawCenteredLines(spriteBatch, Context.Font.Wrap(_tip, CirclesGame.VirtualWidth - 60), centerX, 240, Palette.Bone * 0.8f);

        float fade = MathF.Max(1f - _elapsed / FadeSeconds, _fadeOut / FadeSeconds);   // Ein- und Ausblenden
        if (fade > 0f) UiDraw.Rect(spriteBatch, pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * MathF.Min(1f, fade));
        spriteBatch.End();
    }
}

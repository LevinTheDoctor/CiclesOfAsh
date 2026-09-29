using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Localization;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Scenes;

/// <summary>Wie in der Arena gespielt wird.</summary>
public enum ArenaMode { Solo, LocalDuo, OnlineHost, OnlineJoin }

/// <summary>Wie ein Arena-Kampf endete.</summary>
public enum ArenaResult { Victory, Defeat, Abandoned, ConnectionLost }

/// <summary>
/// Was in der Lobby zuletzt eingestellt war. Reist durch Kampf und Ergebnis mit, damit "zurück zur
/// Arena" nicht alles neu verlangt. "sealed record" = unveränderlicher Datenträger mit Wertgleichheit.
/// </summary>
public sealed record ArenaLobbyChoice(ArenaMode Mode, string OpponentId, int FirstCharacterId, int SecondCharacterId,
                                      bool SwapDevices, string Address)
{
    public static ArenaLobbyChoice Default { get; } = new(ArenaMode.Solo, "", 0, 0, false, "");
}

/// <summary>Was der Ergebnisbildschirm zeigt.</summary>
public sealed record ArenaOutcome(ArenaResult Result, string OpponentName, float Seconds, IReadOnlyList<string> FighterNames);

/// <summary>
/// Ein Kampf, der online auch unter der Pause weiterlaufen muss. Gastgeber und Gast erfüllen es;
/// die Pause treibt ihn darüber an, ohne zu wissen, welche Seite sie vor sich hat.
/// </summary>
public interface IRunningMatch
{
    void UpdateWhilePaused(float deltaSeconds);
    bool IsFinished { get; }
}

/// <summary>Wie ein Kampf besetzt ist: Gegner, Kämpfer und woher ihre Eingaben kommen.</summary>
public sealed class ArenaMatchSetup
{
    public required ArenaOpponent Opponent { get; init; }
    public required IReadOnlyList<ArenaFighter> Fighters { get; init; }
    /// <summary>Eingabe je Kämpfer. null = die gemeinsame Eingabe des Spiels (Tastatur + erster Controller).</summary>
    public required IReadOnlyList<IPlayerInput?> Inputs { get; init; }
    /// <summary>Welche Kämpfer an DIESEM Rechner sitzen – nur deren Gestalten zählen einen Sieg.</summary>
    public required IReadOnlyList<bool> IsLocal { get; init; }
    public required ArenaLobbyChoice Choice { get; init; }
    public int Seed { get; init; } = Random.Shared.Next();
    /// <summary>Online als Gastgeber: die Verbindung zum Gast. null = alle Kämpfer sitzen hier.</summary>
    public Networking.ArenaHostSession? Host { get; init; }
}

/// <summary>
/// Ein Kampf in der Arena – allein, zu zweit an einem Rechner oder als Gastgeber online.
/// Nutzt dieselbe Welt wie ein Verlies (<see cref="DungeonWorld"/>), nur ohne Lauf: Kein Tod kostet
/// etwas, kein Sieg bringt Seelen oder Beute. Gezählt wird allein der Sieg im Werdegang der Gestalt.
/// </summary>
public sealed class ArenaScene : SceneBase, IRunningMatch
{
    /// <summary>So lange bleibt der Kampfplatz nach dem letzten Schlag sichtbar, bevor das Ergebnis kommt.</summary>
    private const float OutcomeDelaySeconds = 1.8f;
    /// <summary>Abstand der Figuren beim Start: nebeneinander statt übereinander.</summary>
    private const float FighterSpacing = 24f;

    private readonly ArenaMatchSetup _setup;
    private readonly DungeonWorld _world;
    private readonly Hud _hud;
    private readonly string _musicId;
    private ArenaResult? _result;
    private float _outcomeTimer;
    /// <summary>Kampfzeit im Moment der Entscheidung – der Nachlauf danach zählt nicht mit.</summary>
    private float _decisiveSeconds;

    public ArenaScene(GameContext context, ArenaMatchSetup setup) : base(context)
    {
        _setup = setup;
        ArenaService arena = context.Arena;
        DungeonPlan plan = arena.CreatePlan(setup.Opponent, setup.Fighters.Count, setup.Seed);
        DungeonLayout layout = new DungeonGenerator(context.Definitions).Generate(plan);
        List<Player> players = CreatePlayers(context, setup, layout);

        // Begleitseelen nur allein: Zu zweit habt ihr einander – und ihre KI kennt nur eine Figur.
        IEnumerable<Companion> companions = players.Count == 1
            ? PlayerFactory.CreateCompanions(context, setup.Fighters[0].Run, players[0].Center)
            : Enumerable.Empty<Companion>();
        // Lauf der Welt = der (geprüfte) Lauf von Spieler 1 – dieselbe Kopie, die seine Figur trägt.
        _world = new DungeonWorld(context, plan, layout, players[0].Run ?? setup.Fighters[0].Run, players, companions);
        _world.PlayerDied += OnAllFallen;
        _world.BossDefeated += OnBossDefeated;
        setup.Host?.AttachWorld(_world, context.Audio);
        _hud = new Hud(context);
        _musicId = arena.MusicOf(setup.Opponent, context.Music.Has);
        Log.Info($"Arena: {setup.Opponent.Id} gegen {string.Join(", ", setup.Fighters.Select(fighter => fighter.Name))}, Seed {setup.Seed}");
    }

    /// <summary>Die Welt des Kampfes – der Online-Gastgeber liest daraus seine Schnappschüsse.</summary>
    public DungeonWorld World => _world;

    /// <summary>Das Ergebnis ist schon unterwegs (Szenenwechsel eingereiht). Danach passiert nichts mehr.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Baut die Figuren aus den Kämpfern (DRY: dieselbe Methode nutzt auch der Online-Gast).</summary>
    public static List<Player> CreatePlayers(GameContext context, ArenaMatchSetup setup, DungeonLayout layout) =>
        CreatePlayers(context, setup.Fighters, setup.Inputs, layout);

    public static List<Player> CreatePlayers(GameContext context, IReadOnlyList<ArenaFighter> fighters,
                                             IReadOnlyList<IPlayerInput?> inputs, DungeonLayout layout)
    {
        var players = new List<Player>();
        for (int index = 0; index < fighters.Count; index++)
        {
            // Jeder Kämpfer wird geprüft – auch der eigene (harmlos), vor allem der aus dem Netz.
            ArenaFighter fighter = context.Arena.Sanitize(fighters[index]);
            Vector2 spawn = layout.PlayerSpawn + new Vector2(index * FighterSpacing, 0f);
            Player player = PlayerFactory.Create(context, fighter.Run, spawn, fighter.Believers, fighter.EternalGifts);
            player.Name = fighter.Name;
            player.Input = inputs[index];
            players.Add(player);
        }
        return players;
    }

    public override void OnEnter() => Context.Music.Play(_musicId);

    public override void OnExit()
    {
        _world.PlayerDied -= OnAllFallen;
        _world.BossDefeated -= OnBossDefeated;
        _world.Dispose();
        _setup.Host?.Dispose();
    }

    public override void Update(float deltaSeconds)
    {
        UpdateInputs();
        if (_result is null && WantsPause())
        {
            // Online hält niemand die Welt an: Der Gast würde sonst in einem eingefrorenen Kampf stehen.
            Context.Scenes.Push(new ArenaPauseScene(Context, onLeave: () => Finish(ArenaResult.Abandoned),
                                                    runningMatch: _setup.Host is not null ? this : null));
            return;
        }
        Tick(deltaSeconds);
    }

    /// <summary>
    /// Online-Pause: Die Welt läuft im Hintergrund weiter (siehe <see cref="ArenaPauseScene"/>).
    /// Die Figuren an diesem Rechner stehen solange still – das Blättern im Menü soll sie nicht lenken.
    /// </summary>
    public void UpdateWhilePaused(float deltaSeconds)
    {
        UpdateInputs();
        IReadOnlyList<Player> players = _world.Players;
        for (int index = 0; index < players.Count; index++)
            if (_setup.IsLocal[index]) players[index].Input = IdleInput.Instance;
        Tick(deltaSeconds);
        for (int index = 0; index < players.Count; index++)
            if (_setup.IsLocal[index]) players[index].Input = _setup.Inputs[index];
    }

    /// <summary>Eigene Geräte der Spieler einlesen. "as" = Umwandlung ohne Ausnahme: null, wenn es kein DeviceInput ist.</summary>
    private void UpdateInputs()
    {
        foreach (IPlayerInput? input in _setup.Inputs) (input as DeviceInput)?.Update();
    }

    /// <summary>Ein Schritt des Kampfes: Netz abholen, Welt rechnen, Schnappschuss senden, Ende abwarten.</summary>
    private void Tick(float deltaSeconds)
    {
        if (_setup.Host is { } host && !host.PumpBeforeUpdate())
        {
            Finish(ArenaResult.ConnectionLost);
            return;
        }
        // Auch nach dem letzten Schlag läuft die Welt weiter (Bosstod, Staub), damit der Moment sichtbar bleibt.
        _world.Update(deltaSeconds);
        _world.PendingLevelUps = 0;   // keine Stufenaufstiege: In der Arena gibt es keinen Lauf, der wächst
        _setup.Host?.SendSnapshot(_world);

        if (_result is not { } result) return;
        _outcomeTimer -= deltaSeconds;
        if (_outcomeTimer <= 0f) Finish(result);
    }

    /// <summary>Pause von JEDER Eingabe an diesem Rechner – auch Spieler 2 am eigenen Controller.</summary>
    private bool WantsPause() =>
        Context.Input.WasPressed(GameAction.Pause)
        || _setup.Inputs.Any(input => input is DeviceInput device && device.WasPressed(GameAction.Pause));

    private void OnAllFallen() => BeginOutcome(ArenaResult.Defeat);

    private void OnBossDefeated()
    {
        // Gezählt wird nur für Gestalten an diesem Rechner – der Online-Gast zählt bei sich.
        for (int index = 0; index < _setup.Fighters.Count; index++)
            if (_setup.IsLocal[index]) Context.Progression.RecordArenaWin(_setup.Fighters[index].CharacterId);
        BeginOutcome(ArenaResult.Victory);
    }

    private void BeginOutcome(ArenaResult result)
    {
        if (_result is not null) return;   // Sieg und Niederlage im selben Frame: das Erste zählt
        _result = result;
        _outcomeTimer = OutcomeDelaySeconds;
        _decisiveSeconds = _world.ElapsedSeconds;
    }

    private void Finish(ArenaResult result)
    {
        if (IsFinished) return;   // nur einmal: Verbindungsabbruch und Ende können im selben Frame kommen
        IsFinished = true;
        float seconds = _result is not null ? _decisiveSeconds : _world.ElapsedSeconds;
        _setup.Host?.SendEnd(result, seconds);
        var outcome = new ArenaOutcome(result, _setup.Opponent.Enemy.Name, seconds,
                                       _setup.Fighters.Select(fighter => fighter.Name).ToList());
        Context.Scenes.Replace(new ArenaResultScene(Context, outcome, _setup.Choice));
    }

    /// <summary>Lichtkarte rendern, bevor die Leinwand gebunden wird.</summary>
    public override void PrepareDraw(SpriteBatch spriteBatch) => _world.PrepareDraw(spriteBatch);

    public override void Draw(SpriteBatch spriteBatch)
    {
        _world.Draw(spriteBatch);
        _hud.Draw(spriteBatch, _world);
    }
}

/// <summary>
/// Pause in der Arena: Fortsetzen, Optionen, Arena verlassen. Online (<c>runningMatch</c> gesetzt)
/// läuft der Kampf darunter weiter – diese Szene treibt ihn selbst an, weil der SceneManager nur
/// die oberste Szene aktualisiert.
/// </summary>
public sealed class ArenaPauseScene : SceneBase
{
    private readonly Action _onLeave;
    /// <summary>Online: der Kampf, der weiterläuft. null = die Welt steht still, solange pausiert ist.</summary>
    private readonly IRunningMatch? _runningMatch;
    private MenuList _menu = new();

    public ArenaPauseScene(GameContext context, Action onLeave, IRunningMatch? runningMatch) : base(context)
    {
        _onLeave = onLeave;
        _runningMatch = runningMatch;
        BuildMenu();
    }

    public override bool IsOverlay => true;

    public override void OnLanguageChanged() => BuildMenu();

    private void BuildMenu()
    {
        int selected = _menu.SelectedIndex;
        _menu = new MenuList();
        _menu.Add(Loc.T("Fortsetzen"), () => Context.Scenes.Pop());
        _menu.Add(Loc.T("Optionen"), () => Context.Scenes.Push(new SettingsScene(Context)));
        _menu.Add(Loc.T("Arena verlassen"), () =>
        {
            Context.Scenes.Pop();
            _onLeave();
        });
        _menu.Select(selected);
    }

    public override void Update(float deltaSeconds)
    {
        _runningMatch?.UpdateWhilePaused(deltaSeconds);
        // Kam der Kampf dabei zum Ende, ist der Wechsel zum Ergebnis schon eingereiht – ein Pop
        // danach würde genau diesen Ergebnisbildschirm wieder vom Stapel nehmen.
        if (_runningMatch is { IsFinished: true }) return;
        if (Context.Input.WasPressed(GameAction.Pause)) Context.Scenes.Pop();
        else _menu.Update(Context.Input, Context.Audio);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        UiDraw.Begin(spriteBatch);
        UiDraw.Rect(spriteBatch, Context.Assets.Pixel, new Rectangle(0, 0, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight), Color.Black * 0.6f);
        Context.TitleFont.DrawCentered(spriteBatch, Loc.T("Innehalten"), CirclesGame.VirtualWidth / 2f, 80, Palette.Gold);
        if (_runningMatch is not null)
            Context.Font.DrawCentered(spriteBatch, Loc.T("Online läuft der Kampf weiter."), CirclesGame.VirtualWidth / 2f, 110, Palette.Ember);
        _menu.Draw(spriteBatch, Context.Font, CirclesGame.VirtualWidth / 2f, 124);
        spriteBatch.End();
    }
}

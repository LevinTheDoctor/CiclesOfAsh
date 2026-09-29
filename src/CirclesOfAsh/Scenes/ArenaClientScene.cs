using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Networking;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Online-Arena beim Gast. Diese Szene rechnet keine Spielregeln: Sie schickt die eigenen Tasten an
/// den Gastgeber und zeigt, was von dort zurückkommt (<see cref="DungeonWorld.ApplyMirror"/>).
/// Spieler 1 ist der Gastgeber, Spieler 2 man selbst – auf beiden Rechnern gleich angeordnet.
/// </summary>
public sealed class ArenaClientScene : SceneBase, IRunningMatch
{
    private readonly ArenaClientSession _session;
    private readonly ArenaOpponent _opponent;
    private readonly IReadOnlyList<ArenaFighter> _fighters;
    private readonly int _ownCharacterId;
    private readonly ArenaLobbyChoice _choice;
    private readonly DungeonWorld _world;
    private readonly Hud _hud;
    private readonly string _musicId;
    /// <summary>Wiederverwendete Liste für die pro Frame angekommenen Schnappschüsse (spart Müll für den GC).</summary>
    private readonly List<WorldMirrorFrame> _frames = new();

    public ArenaClientScene(GameContext context, ArenaClientSession session, ArenaOpponent opponent, DungeonPlan plan,
                            DungeonLayout layout, ArenaFighter hostFighter, ArenaFighter ownFighter, ArenaLobbyChoice choice)
        : base(context)
    {
        _session = session;
        _opponent = opponent;
        _fighters = new[] { hostFighter, ownFighter };
        _ownCharacterId = ownFighter.CharacterId;
        _choice = choice;
        // Die Figuren bekommen keine Eingabe: Sie bewegen sich nur über die Schnappschüsse.
        var noInput = new IPlayerInput?[] { IdleInput.Instance, IdleInput.Instance };
        List<Player> players = ArenaScene.CreatePlayers(context, _fighters, noInput, layout);
        _world = new DungeonWorld(context, plan, layout, players[0].Run ?? hostFighter.Run, players, Enumerable.Empty<Companion>());
        _hud = new Hud(context);
        _musicId = context.Arena.MusicOf(opponent, context.Music.Has);
    }

    public bool IsFinished { get; private set; }

    public override void OnEnter() => Context.Music.Play(_musicId);

    public override void OnExit()
    {
        _world.Dispose();
        _session.Dispose();   // schickt "Bye" – der Gastgeber merkt sofort, dass niemand mehr da ist
    }

    public override void Update(float deltaSeconds)
    {
        if (Context.Input.WasPressed(GameAction.Pause))
        {
            Context.Scenes.Push(new ArenaPauseScene(Context, onLeave: () => Finish(ArenaResult.Abandoned, _world.ElapsedSeconds),
                                                    runningMatch: this));
            return;
        }
        Step(deltaSeconds, ArenaProtocol.CaptureInput(Context.Input));
    }

    /// <summary>In der Pause läuft der Kampf weiter – die eigene Figur bekommt solange keine Tasten.</summary>
    public void UpdateWhilePaused(float deltaSeconds) => Step(deltaSeconds, InputFrame.Idle);

    private void Step(float deltaSeconds, InputFrame input)
    {
        _session.SendInput(input);
        _frames.Clear();
        bool isConnected = _session.Receive(_frames, out ArenaEnd? end);
        // Ereignisse ALLER angekommenen Frames nachspielen, den Zustand aber nur vom neuesten nehmen.
        foreach (WorldMirrorFrame frame in _frames) _world.ReplayEvents(frame.Events);
        if (_frames.Count > 0) _world.ApplyMirror(_frames[^1]);
        _world.UpdateMirror(deltaSeconds);

        if (end is { } ended) Finish((ArenaResult)ended.Result, ended.Seconds);
        else if (!isConnected) Finish(ArenaResult.ConnectionLost, _world.ElapsedSeconds);
    }

    private void Finish(ArenaResult result, float seconds)
    {
        if (IsFinished) return;
        IsFinished = true;
        if (result == ArenaResult.Victory) Context.Progression.RecordArenaWin(_ownCharacterId);
        var outcome = new ArenaOutcome(result, _opponent.Enemy.Name, seconds, _fighters.Select(fighter => fighter.Name).ToList());
        Context.Scenes.Replace(new ArenaResultScene(Context, outcome, _choice));
    }

    public override void PrepareDraw(SpriteBatch spriteBatch) => _world.PrepareDraw(spriteBatch);

    public override void Draw(SpriteBatch spriteBatch)
    {
        _world.Draw(spriteBatch);
        _hud.Draw(spriteBatch, _world);
    }
}

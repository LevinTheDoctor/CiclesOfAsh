using CirclesOfAsh.Core;
using CirclesOfAsh.Progression;
using CirclesOfAsh.UI;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Scenes;

/// <summary>
/// Das eigentliche Spiel. Baut Dungeon + Spieler auf, leitet Updates weiter und reagiert auf
/// Ereignisse der Welt (Tod, Ziel erreicht, Level-Up) mit Szenenwechseln.
/// </summary>
public sealed class DungeonScene : SceneBase
{
    private readonly RunState _run;   // Arbeitskopie – wird nur bei Erfolg übernommen
    private readonly DungeonWorld _world;
    private readonly Hud _hud;
    private bool _isFinished;

    public DungeonScene(GameContext context, RunState committedRun) : base(context)
    {
        _run = committedRun.Clone();
        DungeonPlan plan = context.Progression.CreateDungeonPlan(_run);
        DungeonLayout layout = new DungeonGenerator(context.Definitions).Generate(plan);
        var player = PlayerFactory.Create(context, _run, layout.PlayerSpawn);
        var companions = PlayerFactory.CreateCompanions(context, _run, player.Center);

        _world = new DungeonWorld(context, plan, layout, _run, player, companions);
        _hud = new Hud(context);

        // Events abonnieren: "+=" hängt eine Methode an das Event an
        _world.PlayerDied += OnPlayerDied;
        _world.GoalReached += OnGoalReached;
        Log.Info($"Dungeon erzeugt: Kreis {plan.CircleIndex + 1}, Verlies {plan.DungeonIndex + 1}, Seed {plan.Seed}, Boss {plan.IsBossDungeon}");
    }

    public override void OnExit()
    {
        // "-=" meldet ab -> keine Referenzen bleiben hängen (Speicherleck-Vorsorge)
        _world.PlayerDied -= OnPlayerDied;
        _world.GoalReached -= OnGoalReached;
        _world.Dispose();   // Render-Target der Lichtkarte freigeben (GPU-Speicher)
    }

    public override void Update(float deltaSeconds)
    {
        if (_isFinished) return;
        if (Context.Input.WasPressed(GameAction.Pause))
        {
            Context.Scenes.Push(new PauseScene(Context, _run, _world.Player));
            return;
        }

        _world.Update(deltaSeconds);

        if (!_isFinished && _world.PendingLevelUps > 0)
        {
            _world.PendingLevelUps--;
            var offers = LevelUpService.CreateOffers(Context, _run, _world.Player, _world.Random);
            if (offers.Count > 0) Context.Scenes.Push(new LevelUpScene(Context, offers, _run, _world.Player));
        }
    }

    private void OnPlayerDied()
    {
        _isFinished = true;
        DeathReport report = Context.Progression.HandleDeath();
        Context.Scenes.Replace(new GameOverScene(Context, report));
    }

    private void OnGoalReached()
    {
        _isFinished = true;
        DungeonOutcome outcome = Context.Progression.CompleteDungeon(_run);
        Context.Scenes.Replace(ResultSceneFactory.Create(Context, outcome));
    }

    /// <summary>Lichtkarte rendern, bevor die Leinwand gebunden wird.</summary>
    public override void PrepareDraw(SpriteBatch spriteBatch) => _world.PrepareDraw(spriteBatch);

    public override void Draw(SpriteBatch spriteBatch)
    {
        _world.Draw(spriteBatch);
        _hud.Draw(spriteBatch, _world);
    }
}

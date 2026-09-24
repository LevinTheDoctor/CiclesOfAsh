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
    private readonly string _musicId;
    private bool _isFinished;

    public DungeonScene(GameContext context, RunState committedRun) : base(context)
    {
        _run = committedRun.Clone();
        DungeonPlan plan = context.Progression.CreateDungeonPlan(_run);
        DungeonLayout layout = new DungeonGenerator(context.Definitions).Generate(plan);
        DungeonReachability.Check(layout);   // meldet unerreichbare Pflichträume ins Log (nur Messung)
        var player = PlayerFactory.Create(context, _run, layout.PlayerSpawn);
        var companions = PlayerFactory.CreateCompanions(context, _run, player.Center);

        _world = new DungeonWorld(context, plan, layout, _run, player, companions);
        _hud = new Hud(context);

        // Events abonnieren: "+=" hängt eine Methode an das Event an
        _world.PlayerDied += OnPlayerDied;
        _world.GoalReached += OnGoalReached;
        _world.DialogRequested += OnDialogRequested;
        // Musik nach Kreis (worlds.json); der Thronsaal bekommt sein eigenes Stück.
        _musicId = plan.IsBossDungeon && plan.Circle.BossMusic.Length > 0 ? plan.Circle.BossMusic : plan.Circle.Music;
        Log.Info($"Dungeon erzeugt: Kreis {plan.CircleIndex + 1}, Verlies {plan.DungeonIndex + 1}, Seed {plan.Seed}, Boss {plan.IsBossDungeon}");
    }

    public override void OnEnter()
    {
        Context.Music.Play(_musicId);
        // Begleiter melden sich beim Abstieg zu Wort (einmal pro Lauf, siehe chatter.json).
        _world.Say(Companions.CompanionChatter.RunStart);
    }

    private void OnDialogRequested(Entities.Npc npc)
    {
        var pending = _world.ConsumePendingDialog();
        if (pending is not { } value) return;
        Context.Scenes.Push(new DialogScene(Context, value.Dialog, value.Entry, value.Npc));
    }

    public override void OnExit()
    {
        // "-=" meldet ab -> keine Referenzen bleiben hängen (Speicherleck-Vorsorge)
        _world.PlayerDied -= OnPlayerDied;
        _world.GoalReached -= OnGoalReached;
        _world.DialogRequested -= OnDialogRequested;
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

using CirclesOfAsh.Assets;
using CirclesOfAsh.Companions;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

/// <summary>Begleitseele: schwebt hinter dem Spieler und führt in festem Takt ihr Behavior aus.</summary>
public sealed class Companion : Entity
{
    private readonly AnimationPlayer _animation;
    private readonly int _slotIndex;
    private float _actionTimer;
    private float _bobTime;

    public Companion(CompanionDefinition definition, SpriteSheet sheet, ICompanionBehavior behavior, int slotIndex, Vector2 startCenter)
    {
        Definition = definition;
        Behavior = behavior;
        _slotIndex = slotIndex;
        _animation = new AnimationPlayer(sheet);
        Size = new Point(8, 8);
        Position = startCenter;
    }

    public CompanionDefinition Definition { get; }
    public ICompanionBehavior Behavior { get; }

    public override void Update(DungeonWorld world, float deltaSeconds)
    {
        _animation.Update(deltaSeconds);
        _bobTime += deltaSeconds;

        Player player = world.Player;
        float side = player.FacingRight ? -1f : 1f;   // hinter dem Spieler schweben
        var target = new Vector2(
            player.Center.X + side * (14f + _slotIndex * 10f),
            player.Position.Y - 6f + MathF.Sin(_bobTime * 3f + _slotIndex) * 3f);
        Position = MathUtil.Damp(Position, target - Size.ToVector2() / 2f, 6f, deltaSeconds);

        _actionTimer += deltaSeconds;
        if (_actionTimer < Definition.Interval) return;
        // Nur wenn das Behavior wirklich etwas getan hat, beginnt der Takt von vorne
        if (Behavior.Act(this, world)) _actionTimer = 0f;
    }

    /// <summary>
    /// Hub-Variante: schwebt dem Spieler nach, OHNE Kampf-Verhalten (kein world nötig).
    /// Im Tempel laufen die Seelen sichtbar herum = "Haustier"-Gefühl.
    /// </summary>
    public void UpdateHub(Player player, float deltaSeconds)
    {
        _animation.Update(deltaSeconds);
        _bobTime += deltaSeconds;
        float side = player.FacingRight ? -1f : 1f;
        var target = new Vector2(
            player.Center.X + side * (16f + _slotIndex * 11f),
            player.Position.Y - 8f + MathF.Sin(_bobTime * 2.4f + _slotIndex) * 4f);
        Position = MathUtil.Damp(Position, target - Size.ToVector2() / 2f, 4.5f, deltaSeconds);
    }

    public override void Draw(SpriteBatch spriteBatch) =>
        _animation.Draw(spriteBatch, BottomCenter, flipHorizontally: false, Color.White);
}

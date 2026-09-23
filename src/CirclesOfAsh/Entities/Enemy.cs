using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Enemies;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

/// <summary>
/// Ein Gegner = Definition (Werte) + Brain (Verhalten, Strategy-Pattern) + Physik.
/// Das Brain setzt nur Wünsche (Velocity, Animation); Physik und Kollision macht der Gegner selbst.
/// </summary>
public sealed class Enemy : Actor
{
    private const float SummonDuration = 0.7f;
    private readonly AnimationPlayer _animation;
    private readonly Color _tint;
    private float _spawnTimer = SummonDuration;

    public Enemy(EnemyDefinition definition, SpriteSheet sheet, IEnemyBrain brain, Vector2 bottomCenter,
                 float healthMultiplier, float damageMultiplier)
        : base(definition.MaxHealth * healthMultiplier)
    {
        Definition = definition;
        Brain = brain;
        ContactDamage = definition.ContactDamage * damageMultiplier;
        DamageMultiplier = damageMultiplier;
        Size = new Point(definition.Width, definition.Height);
        Position = bottomCenter - new Vector2(definition.Width / 2f, definition.Height);
        _animation = new AnimationPlayer(sheet);
        _tint = ColorUtil.FromHex(definition.Tint, Color.White);   // einmal parsen, nicht jeden Frame
    }

    public EnemyDefinition Definition { get; }
    public IEnemyBrain Brain { get; }
    public float ContactDamage { get; }
    public float DamageMultiplier { get; }
    /// <summary>Effektives Lauftempo (Basis * Schwierigkeits-Modifikator).</summary>
    public float EffectiveMoveSpeed => Definition.MoveSpeed * _speedMultiplier;
    private float _speedMultiplier = 1f;

    public void ApplySpeedMultiplier(float multiplier) => _speedMultiplier = multiplier;

    public bool IsSpawning => _spawnTimer > 0f;
    public bool IsBoss => Definition.IsBoss;
    public bool IsMiniBoss => Definition.IsMiniBoss;

    /// <summary>Brains können hier eine Animation erzwingen (z. B. "cast"). null = automatisch idle/run.</summary>
    public string? ForcedAnimation { get; set; }

    /// <summary>
    /// Wer diesen Gegner beschworen hat: die Id des Arenaraums oder "rescue" für die Wachen des
    /// Rettungsereignisses. Arena und Rescue zählen nur ihre EIGENEN Gegner – sonst hält eine noch
    /// lebende Rescue-Wache am anderen Ende des Verlieses die Arenatüren für immer verschlossen.
    /// </summary>
    public string Owner { get; set; } = "";

    public override void Update(DungeonWorld world, float deltaSeconds)
    {
        _animation.Update(deltaSeconds);
        if (IsSpawning)
        {
            _spawnTimer -= deltaSeconds;
            return;
        }

        Health.Update(deltaSeconds);
        HitFlashSeconds -= deltaSeconds;
        if (KnockbackSeconds > 0f) KnockbackSeconds -= deltaSeconds;
        else Brain.Update(this, world, deltaSeconds);

        if (Definition.IsFlying)
        {
            Position += Velocity * deltaSeconds;   // Geister schweben durch Wände
            ConfineToArena(world);
        }
        else
        {
            Velocity.Y = MathF.Min(Velocity.Y + TilePhysics.Gravity * deltaSeconds, TilePhysics.MaxFallSpeed);
            LastCollision = TilePhysics.MoveAndCollide(this, world.Map, deltaSeconds, ignorePlatforms: false);
            OnGround = LastCollision.HasFlag(CollisionResult.Landed);
        }

        if (MathF.Abs(Velocity.X) > 1f) FacingRight = Velocity.X > 0f;
        _animation.Play(ForcedAnimation ?? (MathF.Abs(Velocity.X) > 5f ? "run" : "idle"));
    }

    /// <summary>
    /// Hält fliegende Gegner in ihrer versiegelten Arena fest. Sie ignorieren bewusst jede
    /// Kachelkollision – das gilt aber auch für die zugemauerten Türen, und ein Gegner, der
    /// hinausfliegt, ist unerreichbar und hält die Arena für immer offen.
    /// Innerhalb des Raums bleibt das Durchschweben durch Wände erhalten.
    /// </summary>
    private void ConfineToArena(DungeonWorld world)
    {
        if (world.Waves.ActiveArena is not { } arena || arena.OwnerKey != Owner) return;

        Rectangle bounds = arena.PixelBounds;
        float left = bounds.Left + 2, right = bounds.Right - Size.X - 2;
        float top = bounds.Top + 2, bottom = bounds.Bottom - Size.Y - 2;
        // Bei sehr schmalen Räumen darf Clamp nicht mit vertauschten Grenzen aufgerufen werden.
        if (right < left || bottom < top) return;

        float clampedX = Math.Clamp(Position.X, left, right);
        float clampedY = Math.Clamp(Position.Y, top, bottom);
        // Gegen die Wand gedrückt: Geschwindigkeit in dieser Achse abbauen, sonst klebt er dort fest.
        if (clampedX != Position.X) Velocity.X = 0f;
        if (clampedY != Position.Y) Velocity.Y = 0f;
        Position = new Vector2(clampedX, clampedY);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (IsSpawning)
        {
            // Beschwörungs-Vorwarnung: pulsierende Silhouette -> Spieler kann reagieren (faire Spawns)
            float pulse = 0.3f + 0.3f * MathF.Sin(_spawnTimer * 30f);
            _animation.Draw(spriteBatch, BottomCenter, !FacingRight, Palette.Ember * pulse);
            return;
        }
        _animation.Draw(spriteBatch, BottomCenter, !FacingRight, FlashTint(_tint));
    }
}

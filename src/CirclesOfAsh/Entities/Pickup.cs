using CirclesOfAsh.Assets;
using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

public enum PickupKind { Soul, Heart, ManaShard, Relic, Item }

/// <summary>Einsammelbares Objekt. Seelen/Herzen/Mana werden magnetisch angezogen (Vampire-Survivors-Gefühl).</summary>
public sealed class Pickup : Entity
{
    private const float MagnetAcceleration = 700f;
    private readonly AnimationPlayer _animation;
    private readonly Vector2 _floatAnchor;
    private bool _isMagnetized;
    private float _magnetSpeed;
    private float _time;

    /// <param name="itemId">Nur bei <see cref="PickupKind.Item"/>: welche ItemDefinition.</param>
    /// <param name="clip">Animation im Sheet (Item-Icons liegen als eigene Animationen in einem Sheet).</param>
    /// <param name="floating">Schwebt auf der Stelle statt herauszuspringen (Sammelobjekte im Raum).</param>
    public Pickup(PickupKind kind, SpriteSheet sheet, Vector2 center, float value, Random random,
                  string itemId = "", string? clip = null, bool floating = false)
    {
        Kind = kind;
        Value = value;
        ItemId = itemId;
        IsFloating = floating;
        _animation = new AnimationPlayer(sheet);
        if (clip is not null) _animation.Play(clip);
        Size = kind is PickupKind.Relic or PickupKind.Item ? new Point(12, 12) : new Point(6, 6);
        Position = center - Size.ToVector2() / 2f;
        _floatAnchor = Position;
        _time = random.NextSingle() * 6f;
        if (!floating) Velocity = new Vector2((random.NextSingle() - 0.5f) * 80f, -120f - random.NextSingle() * 60f);
    }

    public PickupKind Kind { get; }
    public float Value { get; }
    public string ItemId { get; }
    public bool IsFloating { get; }
    /// <summary>Items und Reliquien leuchten schwach -> auch im Dunkeln auffindbar.</summary>
    public float GlowRadius => Kind is PickupKind.Item or PickupKind.Relic ? 22f : Kind == PickupKind.Soul ? 8f : 0f;

    public override void Update(DungeonWorld world, float deltaSeconds)
    {
        _animation.Update(deltaSeconds);
        _time += deltaSeconds;
        Player player = world.Player;
        bool canMagnetize = Kind is not (PickupKind.Relic or PickupKind.Item);
        if (!_isMagnetized && canMagnetize &&
            Vector2.Distance(Center, player.Center) < player.Stats[StatType.PickupRadius])
        {
            _isMagnetized = true;
        }

        if (_isMagnetized)
        {
            _magnetSpeed += MagnetAcceleration * deltaSeconds;
            Position += MathUtil.SafeNormalize(player.Center - Center, Vector2.UnitY) * _magnetSpeed * deltaSeconds;
        }
        else if (IsFloating)
        {
            Position = _floatAnchor + new Vector2(0f, MathF.Sin(_time * 2.5f) * 2f);   // sanftes Auf und Ab
        }
        else
        {
            Velocity.X *= 0.92f;   // Luftreibung
            Velocity.Y = MathF.Min(Velocity.Y + TilePhysics.Gravity * 0.6f * deltaSeconds, TilePhysics.MaxFallSpeed);
            TilePhysics.MoveAndCollide(this, world.Map, deltaSeconds, ignorePlatforms: false);
        }

        if (Bounds.Intersects(player.Bounds))
        {
            world.CollectPickup(this);
            Remove();
        }
    }

    public override void Draw(SpriteBatch spriteBatch) =>
        _animation.Draw(spriteBatch, BottomCenter, flipHorizontally: false, Color.White);
}

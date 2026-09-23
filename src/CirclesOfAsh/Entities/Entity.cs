using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

/// <summary>
/// Basis aller Spielobjekte. Position/Velocity sind bewusst öffentliche FELDER statt Properties:
/// Vector2 ist ein struct (Werttyp). Über eine Property bekäme man eine Kopie, und
/// "entity.Position.X += 5" würde nicht kompilieren. Felder erlauben direktes Ändern der Komponenten.
/// </summary>
public abstract class Entity
{
    public Vector2 Position;   // linke obere Ecke der Kollisionsbox
    public Vector2 Velocity;

    public Point Size { get; protected set; }
    public bool IsRemoved { get; private set; }

    public Rectangle Bounds => new((int)MathF.Floor(Position.X), (int)MathF.Floor(Position.Y), Size.X, Size.Y);
    public Vector2 Center => Position + Size.ToVector2() / 2f;
    public Vector2 BottomCenter => new(Position.X + Size.X / 2f, Position.Y + Size.Y);

    /// <summary>Markiert zum Entfernen. Gelöscht wird erst nach dem Update-Durchlauf (sicheres Iterieren).</summary>
    public void Remove() => IsRemoved = true;

    public abstract void Update(DungeonWorld world, float deltaSeconds);
    public abstract void Draw(SpriteBatch spriteBatch);
}

/// <summary>Lebewesen mit Leben, Blickrichtung, Rückstoß und Bodenkontakt (Spieler + Gegner).</summary>
public abstract class Actor : Entity
{
    protected float KnockbackSeconds;
    protected float HitFlashSeconds;

    protected Actor(float maxHealth) => Health = new Health(maxHealth);

    public Health Health { get; }
    public bool OnGround { get; protected set; }
    public bool FacingRight { get; set; } = true;
    public CollisionResult LastCollision { get; protected set; }

    public void ApplyKnockback(Vector2 source, float strength, float resistance = 0f)
    {
        float effective = strength * (1f - Math.Clamp(resistance, 0f, 1f));
        if (effective <= 0f) return;
        float direction = MathF.Sign(Center.X - source.X);   // -1, 0 oder +1
        if (direction == 0f) direction = 1f;
        Velocity = new Vector2(direction * effective * 2f, -effective * 0.9f);
        KnockbackSeconds = 0.15f;
    }

    public void Flash() => HitFlashSeconds = 0.12f;

    protected Color FlashTint(Color baseTint) =>
        HitFlashSeconds > 0f ? new Color(255, 110, 110, (int)baseTint.A) : baseTint;
}

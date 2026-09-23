using CirclesOfAsh.Assets;
using CirclesOfAsh.Combat;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

/// <summary>Geschoss für Spieler, Begleiter und Gegner. Die Fraktion entscheidet, wen es treffen kann.</summary>
public sealed class Projectile : Entity
{
    private readonly AnimationPlayer _animation;
    private readonly HashSet<Enemy> _alreadyHit = new();   // HashSet: O(1)-Prüfung "schon getroffen?"
    private readonly float _damage;
    private readonly float _knockback;
    private readonly bool _collidesWithTiles;
    private float _lifetime;
    private int _pierceLeft;

    public Projectile(Faction faction, SpriteSheet sheet, Vector2 center, Vector2 velocity, float damage,
                      int pierce, float lifetime, float knockback, bool collidesWithTiles = true, float gravity = 0f)
    {
        Faction = faction;
        _animation = new AnimationPlayer(sheet);
        Size = new Point(6, 6);
        Position = center - Size.ToVector2() / 2f;
        Velocity = velocity;
        _damage = damage;
        _pierceLeft = pierce;
        _lifetime = lifetime;
        _knockback = knockback;
        _collidesWithTiles = collidesWithTiles;
        Gravity = gravity;
    }

    public Faction Faction { get; }
    public float Gravity { get; }

    public override void Update(DungeonWorld world, float deltaSeconds)
    {
        _animation.Update(deltaSeconds);
        _lifetime -= deltaSeconds;
        if (_lifetime <= 0f)
        {
            Remove();
            return;
        }

        Velocity.Y += Gravity * deltaSeconds;
        Position += Velocity * deltaSeconds;
        if (_collidesWithTiles && TileMap.IsBlocking(world.Map[TileMap.ToTile(Center.X), TileMap.ToTile(Center.Y)]))
        {
            world.Effects.Burst(Center, Color.Gray, 4, 40f);
            Remove();
            return;
        }

        if (Faction == Faction.Player) HitEnemies(world);
        else if (Bounds.Intersects(world.Player.Bounds))
        {
            world.Player.TakeHit(world, _damage, Center, _knockback);
            Remove();
        }
    }

    private void HitEnemies(DungeonWorld world)
    {
        foreach (Enemy enemy in world.EnemiesIntersecting(Bounds))
        {
            // HashSet.Add liefert false, wenn das Element schon enthalten war -> jeder Gegner nur einmal
            if (!_alreadyHit.Add(enemy)) continue;
            world.DamageEnemy(enemy, _damage, Center, _knockback);
            if (_pierceLeft-- <= 0)   // Post-Dekrement: erst vergleichen, dann um 1 verringern
            {
                Remove();
                return;
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        SpriteSheet sheet = _animation.Sheet;
        Rectangle source = sheet.GetFrameRectangle(_animation.CurrentClip, _animation.FrameIndex);
        float rotation = MathF.Atan2(Velocity.Y, Velocity.X);   // Flugrichtung als Winkel
        var origin = new Vector2(sheet.FrameWidth / 2f, sheet.FrameHeight / 2f);
        spriteBatch.Draw(sheet.Texture, Center, source, Color.White, rotation, origin, 1f, SpriteEffects.None, 0f);
    }
}

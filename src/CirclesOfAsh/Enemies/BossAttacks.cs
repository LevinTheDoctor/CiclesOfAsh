using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Enemies;

/// <summary>Ein Boss-Angriff als kleine Zustandsmaschine: Begin -> Update bis fertig (true).</summary>
public interface IBossAttack
{
    void Begin(Enemy boss, DungeonWorld world, float intensity);
    /// <summary>true = Angriff beendet.</summary>
    bool Update(Enemy boss, DungeonWorld world, float deltaSeconds);
}

/// <summary>"charge": kurze Vorwarnung, dann Sturmangriff bis zur Wand.</summary>
public sealed class ChargeAttack : IBossAttack
{
    private float _telegraph = 0.7f;
    private float _chargeTime = 1.4f;
    private float _direction;
    private float _intensity;

    public void Begin(Enemy boss, DungeonWorld world, float intensity)
    {
        _intensity = intensity;
        _direction = MathF.Sign(world.Player.Center.X - boss.Center.X);
        if (_direction == 0f) _direction = 1f;
        boss.ForcedAnimation = "cast";
    }

    public bool Update(Enemy boss, DungeonWorld world, float deltaSeconds)
    {
        if (_telegraph > 0f)
        {
            _telegraph -= deltaSeconds;
            boss.Velocity.X = 0f;
            world.Effects.Burst(boss.BottomCenter, Palette.Ember, 1, 40f, 0.3f);
            return false;
        }
        _chargeTime -= deltaSeconds;
        boss.Velocity.X = _direction * 260f * _intensity;
        bool hitWall = (boss.LastCollision & CollisionResult.HitWall) != 0;   // "&" = bitweises UND prüft das Flag
        if (hitWall) world.ShakeCamera(6f);
        return hitWall || _chargeTime <= 0f;
    }
}

/// <summary>"projectile_ring": mehrere Geschossringe mit versetztem Winkel.</summary>
public sealed class ProjectileRingAttack : IBossAttack
{
    private const int ProjectilesPerRing = 12;
    private int _ringsLeft = 3;
    private float _timer;
    private float _angleOffset;

    public void Begin(Enemy boss, DungeonWorld world, float intensity)
    {
        _ringsLeft = intensity > 1.2f ? 4 : 3;
        boss.ForcedAnimation = "cast";
    }

    public bool Update(Enemy boss, DungeonWorld world, float deltaSeconds)
    {
        boss.Velocity.X = 0f;
        _timer -= deltaSeconds;
        if (_timer > 0f) return false;
        _timer = 0.45f;
        _angleOffset += MathHelper.Pi / ProjectilesPerRing;   // jeder Ring um eine halbe Lücke gedreht
        var sheet = world.Context.Assets.GetSpriteSheet(boss.Definition.ProjectileSprite);
        for (int index = 0; index < ProjectilesPerRing; index++)
        {
            float angle = _angleOffset + index * MathHelper.TwoPi / ProjectilesPerRing;
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * boss.Definition.ProjectileSpeed;
            world.Spawn(new Projectile(Faction.Enemy, sheet, boss.Center, velocity,
                boss.Definition.ProjectileDamage * boss.DamageMultiplier, 0, 4f, 60f, collidesWithTiles: false));
        }
        world.Context.Audio.Play("shoot", 0.4f, -0.3f);
        return --_ringsLeft <= 0;   // Prä-Dekrement: erst verringern, dann vergleichen
    }
}

/// <summary>"summon": beschwört Diener (EnemyDefinition.SummonEnemy).</summary>
public sealed class SummonAttack : IBossAttack
{
    private float _duration = 1f;

    public void Begin(Enemy boss, DungeonWorld world, float intensity)
    {
        boss.ForcedAnimation = "cast";
        if (string.IsNullOrEmpty(boss.Definition.SummonEnemy)) return;
        var minion = world.Context.Definitions.Enemies.Get(boss.Definition.SummonEnemy);
        int count = intensity > 1.2f ? 4 : 3;
        for (int index = 0; index < count; index++)
        {
            float offsetX = (index - (count - 1) / 2f) * 40f;
            // Diener erben den Besitzer des Bosses – sonst zaehlt der Thronsaal sie nicht mit
            // und die Tuer oeffnet sich, waehrend noch Diener leben.
            world.SpawnEnemy(minion, new Vector2(boss.BottomCenter.X + offsetX, boss.BottomCenter.Y - (minion.IsFlying ? 40f : 0f)),
                boss.Owner);
        }
        world.Announce("Diener werden beschworen!");
    }

    public bool Update(Enemy boss, DungeonWorld world, float deltaSeconds)
    {
        boss.Velocity.X = 0f;
        _duration -= deltaSeconds;
        return _duration <= 0f;
    }
}

/// <summary>"slam": hoher Sprung, beim Aufprall laufen Schockwellen über den Boden.</summary>
public sealed class SlamAttack : IBossAttack
{
    private bool _hasLeftGround;
    private float _safety = 3f;

    public void Begin(Enemy boss, DungeonWorld world, float intensity)
    {
        boss.Velocity.Y = -520f;
        boss.Velocity.X = MathF.Sign(world.Player.Center.X - boss.Center.X) * 90f * intensity;
    }

    public bool Update(Enemy boss, DungeonWorld world, float deltaSeconds)
    {
        _safety -= deltaSeconds;
        if (!boss.OnGround) _hasLeftGround = true;
        if (!_hasLeftGround || !boss.OnGround) return _safety <= 0f;

        boss.Velocity.X = 0f;
        world.ShakeCamera(7f);
        var sheet = world.Context.Assets.GetSpriteSheet(boss.Definition.ProjectileSprite);
        var origin = new Vector2(boss.Center.X, boss.Bounds.Bottom - 5);
        foreach (float direction in new[] { -1f, 1f })
        {
            world.Spawn(new Projectile(Faction.Enemy, sheet, origin, new Vector2(direction * 170f, 0f),
                boss.Definition.ProjectileDamage * boss.DamageMultiplier * 1.5f, 0, 2.5f, 120f));
        }
        world.Effects.Burst(origin, Palette.Ash, 30, 120f);
        return true;
    }
}

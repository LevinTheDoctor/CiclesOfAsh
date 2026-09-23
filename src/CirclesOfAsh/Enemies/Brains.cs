using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Enemies;

/// <summary>KI eines Gegners (Strategy-Pattern). Pro Gegner eine eigene Instanz -> darf Zustand haben.</summary>
public interface IEnemyBrain
{
    void Update(Enemy enemy, DungeonWorld world, float deltaSeconds);
}

/// <summary>Gemeinsame Helfer für Brains (DRY).</summary>
internal static class BrainHelpers
{
    /// <summary>Getarnte Spieler sind unsichtbar -> Gegner wandern ziellos.</summary>
    public static bool CanSeePlayer(DungeonWorld world) => !world.Player.IsStealthed && !world.Player.Health.IsDead;

    public static bool IsWallAhead(Enemy enemy, TileMap map, float direction)
    {
        float probeX = direction > 0 ? enemy.Bounds.Right + 2 : enemy.Bounds.Left - 2;
        return TileMap.IsBlocking(map[TileMap.ToTile(probeX), TileMap.ToTile(enemy.Center.Y)]);
    }

    public static void FireAtPlayer(Enemy enemy, DungeonWorld world, float speed, float spreadRadians = 0f)
    {
        Vector2 direction = MathUtil.SafeNormalize(world.Player.Center - enemy.Center, Vector2.UnitX);
        direction = MathUtil.Rotate(direction, spreadRadians);
        world.Spawn(new Projectile(Faction.Enemy, world.Context.Assets.GetSpriteSheet(enemy.Definition.ProjectileSprite),
            enemy.Center, direction * speed, enemy.Definition.ProjectileDamage * enemy.DamageMultiplier, 0, 4f, 60f));
    }
}

/// <summary>"walker": läuft auf den Spieler zu und springt über Hindernisse.</summary>
public sealed class WalkerBrain : IEnemyBrain
{
    private float _wanderDirection = 1f;
    private float _jumpCooldown;

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds)
    {
        _jumpCooldown -= deltaSeconds;
        float direction;
        if (BrainHelpers.CanSeePlayer(world))
        {
            float deltaX = world.Player.Center.X - enemy.Center.X;
            direction = MathF.Abs(deltaX) < 4f ? 0f : MathF.Sign(deltaX);
        }
        else
        {
            if (enemy.LastCollision.HasFlag(CollisionResult.HitWallLeft) || enemy.LastCollision.HasFlag(CollisionResult.HitWallRight))
                _wanderDirection = -_wanderDirection;
            direction = _wanderDirection * 0.5f;
        }
        enemy.Velocity.X = direction * enemy.EffectiveMoveSpeed;

        bool playerAbove = world.Player.Bounds.Bottom < enemy.Bounds.Top - 24;
        bool wantsJump = BrainHelpers.IsWallAhead(enemy, world.Map, direction) || (playerAbove && world.Random.NextSingle() < 0.02f);
        if (enemy.OnGround && wantsJump && _jumpCooldown <= 0f && direction != 0f)
        {
            enemy.Velocity.Y = -380f;
            _jumpCooldown = 0.8f;
        }
    }
}

/// <summary>"flyer": schwebt mit Wellenbewegung auf den Spieler zu, durch Wände hindurch.</summary>
public sealed class FlyerBrain : IEnemyBrain
{
    private float _time;

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds)
    {
        _time += deltaSeconds;
        Vector2 desired;
        if (BrainHelpers.CanSeePlayer(world))
        {
            Vector2 toPlayer = MathUtil.SafeNormalize(world.Player.Center - enemy.Center, Vector2.Zero);
            var perpendicular = new Vector2(-toPlayer.Y, toPlayer.X);   // 90°-gedrehter Vektor für die Welle
            desired = (toPlayer + perpendicular * MathF.Sin(_time * 4f) * 0.6f) * enemy.EffectiveMoveSpeed;
        }
        else
        {
            desired = new Vector2(MathF.Cos(_time), MathF.Sin(_time * 1.3f)) * enemy.EffectiveMoveSpeed * 0.3f;
        }
        enemy.Velocity = MathUtil.Damp(enemy.Velocity, desired, 3f, deltaSeconds);
    }
}

/// <summary>"caster": hält Abstand und wirft Geschosse.</summary>
public sealed class CasterBrain : IEnemyBrain
{
    private const float PreferredMin = 90f, PreferredMax = 160f;
    private float _attackTimer = 1.5f;
    private float _castAnimationTimer;

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds)
    {
        _castAnimationTimer -= deltaSeconds;
        enemy.ForcedAnimation = _castAnimationTimer > 0f ? "cast" : null;
        if (!BrainHelpers.CanSeePlayer(world))
        {
            enemy.Velocity.X = 0f;
            return;
        }

        float deltaX = world.Player.Center.X - enemy.Center.X;
        float distance = MathF.Abs(deltaX);
        float direction = distance < PreferredMin ? -MathF.Sign(deltaX) : distance > PreferredMax ? MathF.Sign(deltaX) : 0f;
        enemy.Velocity.X = direction * enemy.EffectiveMoveSpeed;
        enemy.FacingRight = deltaX > 0f;

        _attackTimer -= deltaSeconds;
        if (_attackTimer > 0f || distance > 280f) return;
        _attackTimer = enemy.Definition.AttackInterval;
        _castAnimationTimer = 0.4f;
        BrainHelpers.FireAtPlayer(enemy, world, enemy.Definition.ProjectileSpeed);
    }
}

/// <summary>
/// "charger": Telegraph-Angriff wie ein Boss: kurz ausholen (stehen bleiben, Glühen), dann
/// Sturmangriff in Richtung Spieler. Danach kurze Erholung. Für Ritter & Keiler-artige Gegner.
/// </summary>
public sealed class ChargerBrain : IEnemyBrain
{
    private const float TelegraphSeconds = 0.65f;
    private const float RecoverSeconds = 0.9f;

    private enum Phase { Stalk, Telegraph, Charge, Recover }
    private Phase _phase = Phase.Stalk;
    private float _timer;
    private float _chargeDirection;

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds)
    {
        _timer -= deltaSeconds;
        switch (_phase)
        {
            case Phase.Stalk:
            {
                if (!BrainHelpers.CanSeePlayer(world))
                {
                    enemy.Velocity.X = 0f;
                    return;
                }
                float deltaX = world.Player.Center.X - enemy.Center.X;
                enemy.FacingRight = deltaX > 0f;
                enemy.Velocity.X = MathF.Sign(deltaX) * enemy.EffectiveMoveSpeed;
                // In Angriffsweite und auf gleicher Höhe? Dann ausholen.
                if (MathF.Abs(deltaX) < 150f && MathF.Abs(world.Player.Center.Y - enemy.Center.Y) < 30f)
                {
                    _phase = Phase.Telegraph;
                    _timer = TelegraphSeconds;
                    _chargeDirection = MathF.Sign(deltaX);
                    if (_chargeDirection == 0f) _chargeDirection = 1f;
                    enemy.ForcedAnimation = "cast";
                }
                break;
            }
            case Phase.Telegraph:
                enemy.Velocity.X = 0f;
                if (_timer <= 0f)
                {
                    _phase = Phase.Charge;
                    _timer = 0.55f;
                    enemy.ForcedAnimation = null;
                    world.Context.Audio.Play("roar", 0.25f, 0.5f);
                }
                break;
            case Phase.Charge:
                enemy.Velocity.X = _chargeDirection * enemy.EffectiveMoveSpeed * 3.2f;
                if (_timer <= 0f)
                {
                    _phase = Phase.Recover;
                    _timer = RecoverSeconds;
                    enemy.Velocity.X *= 0.2f;
                }
                break;
            case Phase.Recover:
                enemy.Velocity.X *= 1f - MathF.Min(1f, 6f * deltaSeconds);   // sanft ausrollen
                if (_timer <= 0f) _phase = Phase.Stalk;
                break;
        }
    }
}

/// <summary>
/// "swarmer": winzig, schnell, sehr zerbrechlich. Bewegt sich in Sprüngen auf den Spieler zu.
/// Einzeln harmlos, in Gruppen (z. B. von einem Splitterer beschworen) gefährlich.
/// </summary>
public sealed class SwarmerBrain : IEnemyBrain
{
    private float _hopCooldown;

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds)
    {
        _hopCooldown -= deltaSeconds;
        if (!BrainHelpers.CanSeePlayer(world))
        {
            enemy.Velocity.X = 0f;
            return;
        }
        if (enemy.OnGround && _hopCooldown <= 0f)
        {
            float deltaX = world.Player.Center.X - enemy.Center.X;
            enemy.Velocity.X = MathF.Sign(deltaX) * enemy.EffectiveMoveSpeed;
            enemy.Velocity.Y = -260f;
            bool playerAbove = world.Player.Center.Y < enemy.Center.Y - 40f;
            if (playerAbove) enemy.Velocity.Y = -420f;
            _hopCooldown = 0.35f;
        }
    }
}

/// <summary>
/// "ambusher": liegt als Hügel getarnt am Boden und greift an, wenn der Spieler nah ist.
/// Ideal für Blutegel in gefluteten Räumen. IsFlying=false, aber ohne Verfolgung bis zur Auslösung.
/// </summary>
public sealed class AmbusherBrain : IEnemyBrain
{
    private const float TriggerDistance = 60f;
    private bool _wasTriggered;

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds)
    {
        if (!_wasTriggered)
        {
            enemy.Velocity.X = 0f;
            float distance = Vector2.Distance(enemy.Center, world.Player.Center);
            if (distance < TriggerDistance)
            {
                _wasTriggered = true;
                enemy.ForcedAnimation = null;
                float direction = MathF.Sign(world.Player.Center.X - enemy.Center.X);
                enemy.Velocity.X = direction * enemy.EffectiveMoveSpeed * 2f;
                enemy.Velocity.Y = -180f;   // Blutegel springt hoch
            }
            else
            {
                enemy.ForcedAnimation = "idle";   // getarnt: regungslos
            }
            return;
        }
        // Ausgelöst: kriecht zäh auf den Spieler zu und springt nach
        if (BrainHelpers.CanSeePlayer(world))
        {
            float deltaX = world.Player.Center.X - enemy.Center.X;
            enemy.Velocity.X = MathF.Abs(deltaX) < 3f ? 0f : MathF.Sign(deltaX) * enemy.EffectiveMoveSpeed;
            bool playerAbove = world.Player.Bounds.Bottom < enemy.Bounds.Top - 20;
            if (playerAbove && enemy.OnGround) enemy.Velocity.Y = -300f;
        }
    }
}

/// <summary>
/// "boss": wählt anhand der Lebens-Phase (aus JSON) Angriffe aus einer Liste und führt sie nacheinander aus.
/// Die Angriffe selbst sind wieder austauschbare Strategien (<see cref="IBossAttack"/>).
/// </summary>
public sealed class BossBrain : IEnemyBrain
{
    private IBossAttack? _currentAttack;
    private BossPhaseDefinition? _currentPhase;
    private float _pauseTimer = 1.5f;
    private int _attackIndex;

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds)
    {
        BossPhaseDefinition? phase = SelectPhase(enemy);
        if (phase is null) return;
        if (phase != _currentPhase)
        {
            if (_currentPhase is not null)   // Phasenwechsel (nicht beim ersten Mal) inszenieren
            {
                world.Announce($"{enemy.Definition.Name} rast vor Zorn!");
                world.Effects.Ring(enemy.Center, 50f, Palette.Blood, 40);
                world.Context.Audio.Play("roar", 0.8f);
            }
            _currentPhase = phase;
            _attackIndex = 0;
        }

        if (_currentAttack is not null)
        {
            if (!_currentAttack.Update(enemy, world, deltaSeconds)) return;
            _currentAttack = null;
            enemy.ForcedAnimation = null;
            _pauseTimer = phase.PauseBetweenAttacks;
            return;
        }

        // Zwischen Angriffen langsam auf den Spieler zugehen
        float deltaX = world.Player.Center.X - enemy.Center.X;
        enemy.Velocity.X = MathF.Sign(deltaX) * enemy.EffectiveMoveSpeed * 0.4f * phase.SpeedMultiplier;

        _pauseTimer -= deltaSeconds;
        if (_pauseTimer > 0f || phase.Attacks.Count == 0) return;
        string attackKey = phase.Attacks[_attackIndex++ % phase.Attacks.Count];
        _currentAttack = world.Context.Behaviors.CreateBossAttack(attackKey);
        _currentAttack.Begin(enemy, world, phase.SpeedMultiplier);
    }

    private static BossPhaseDefinition? SelectPhase(Enemy enemy)
    {
        float ratio = enemy.Health.Ratio;
        // Die Phase mit dem kleinsten Schwellwert, der noch >= ratio ist
        return enemy.Definition.Phases
            .Where(phase => ratio <= phase.HealthBelow)
            .OrderBy(phase => phase.HealthBelow)
            .FirstOrDefault();
    }
}

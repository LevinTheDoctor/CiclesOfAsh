using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Abilities;

// Alle mitgelieferten Fähigkeits-Behaviors. Registriert in Modding/BehaviorRegistry.cs.
// Eigene Behaviors: Klasse anlegen, IAbilityBehavior implementieren, in der Registry eintragen,
// in abilities.json per "behavior": "mein_key" verwenden. Fertig.

/// <summary>"projectile": feuert Geschosse auf den nächsten Gegner (Fächer bei mehreren).</summary>
public sealed class ProjectileAbility : IAbilityBehavior
{
    private const float SpreadRadians = 0.18f;

    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability)
    {
        AbilityDefinition definition = ability.Definition;
        Enemy? target = world.FindNearestEnemy(owner.Center, definition.Range);
        if (target is null) return false;

        float damage = ability.ComputeDamage(owner, owner.ConsumeStealthBonus());
        Vector2 baseDirection = MathUtil.SafeNormalize(target.Center - owner.Center, Vector2.UnitX);
        int count = ability.Count;
        for (int index = 0; index < count; index++)
        {
            // (index - (count-1)/2) verteilt symmetrisch um die Mitte: z. B. -1, 0, +1
            float angle = (index - (count - 1) / 2f) * SpreadRadians;
            Vector2 direction = MathUtil.Rotate(baseDirection, angle);
            world.Spawn(new Projectile(Faction.Player, world.Context.Assets.GetSpriteSheet(definition.Sprite), owner.Center,
                direction * definition.Speed, damage, definition.Pierce, definition.Range / definition.Speed * 1.4f, definition.Knockback));
        }
        return true;
    }
}

/// <summary>"melee_arc": Hieb in Richtung des nächsten Gegners, trifft alle in einem Rechteck.</summary>
public sealed class MeleeArcAbility : IAbilityBehavior
{
    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability)
    {
        Enemy? target = world.FindNearestEnemy(owner.Center, ability.Definition.Range);
        if (target is null) return false;

        bool toRight = target.Center.X >= owner.Center.X;
        int reach = (int)ability.Area(owner.Stats);
        var area = new Rectangle(toRight ? owner.Bounds.Right : owner.Bounds.Left - reach, owner.Bounds.Top - 8, reach, owner.Size.Y + 12);

        float damage = ability.ComputeDamage(owner, owner.ConsumeStealthBonus());
        // ToList(): Momentaufnahme, weil DamageEnemy Gegner töten (und markieren) kann
        foreach (Enemy enemy in world.EnemiesIntersecting(area).ToList())
            world.DamageEnemy(enemy, damage, owner.Center, ability.Definition.Knockback);
        world.HitBreakables(new Vector2(area.Center.X, owner.Center.Y), reach);   // Hieb zerspringt auch Urnen & Fässer

        var effectBottom = new Vector2(area.Center.X, owner.Bounds.Bottom);
        world.Effects.PlaySprite(world.Context.Assets.GetSpriteSheet(ability.Definition.Sprite), effectBottom, flip: !toRight);
        return true;
    }
}

/// <summary>"nova": Schadensring um den Spieler. Auto-Novas zünden nur, wenn Gegner in Reichweite sind.</summary>
public sealed class NovaAbility : IAbilityBehavior
{
    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability)
    {
        float radius = ability.Area(owner.Stats);
        List<Enemy> targets = world.EnemiesInRadius(owner.Center, radius).ToList();
        if (targets.Count == 0 && ability.Definition.Activation == AbilityActivation.Auto) return false;

        float damage = ability.ComputeDamage(owner, owner.ConsumeStealthBonus());
        foreach (Enemy enemy in targets) world.DamageEnemy(enemy, damage, owner.Center, ability.Definition.Knockback);
        world.HitBreakables(owner.Center, radius);   // Nova sprengt auch zerbrechliche Deko
        world.Effects.Ring(owner.Center, radius, Palette.Faith);
        return true;
    }
}

/// <summary>"orbit": dauerhaft kreisende Objekte (Kerzen, Klingen), die bei Berührung Schaden machen.</summary>
public sealed class OrbitAbility : IAbilityBehavior
{
    private const float HitIntervalSeconds = 0.5f;
    private readonly Dictionary<Enemy, float> _hitCooldowns = new();
    private float _angle;

    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability) => false;

    public void Update(DungeonWorld world, Player owner, AbilityInstance ability, float deltaSeconds)
    {
        float radius = ability.Area(owner.Stats);
        _angle += ability.Definition.Speed / MathF.Max(radius, 1f) * deltaSeconds;   // Bahngeschwindigkeit -> Winkelgeschwindigkeit

        // Abklingzeiten pro Gegner herunterzählen; Schlüssel vorher kopieren, weil wir das Dictionary ändern
        foreach (Enemy enemy in _hitCooldowns.Keys.ToList())
        {
            _hitCooldowns[enemy] -= deltaSeconds;
            if (_hitCooldowns[enemy] <= 0f || enemy.IsRemoved) _hitCooldowns.Remove(enemy);
        }

        float damage = ability.ComputeDamage(owner, 1f);
        foreach (Vector2 orb in OrbPositions(owner, ability))
        {
            var hitBox = new Rectangle((int)orb.X - 4, (int)orb.Y - 4, 8, 8);
            foreach (Enemy enemy in world.EnemiesIntersecting(hitBox).ToList())
            {
                if (_hitCooldowns.ContainsKey(enemy)) continue;
                _hitCooldowns[enemy] = HitIntervalSeconds;
                world.DamageEnemy(enemy, damage, orb, ability.Definition.Knockback);
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, DungeonWorld world, Player owner, AbilityInstance ability)
    {
        var sheet = world.Context.Assets.GetSpriteSheet(ability.Definition.Sprite);
        var source = sheet.GetFrameRectangle(sheet.GetClip("idle"), (int)(_angle * 2f) % 2);
        var origin = new Vector2(sheet.FrameWidth / 2f, sheet.FrameHeight / 2f);
        foreach (Vector2 orb in OrbPositions(owner, ability))
            spriteBatch.Draw(sheet.Texture, new Vector2(MathF.Round(orb.X), MathF.Round(orb.Y)), source, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
    }

    private IEnumerable<Vector2> OrbPositions(Player owner, AbilityInstance ability)
    {
        int count = ability.Count;
        float radius = ability.Area(owner.Stats);
        for (int index = 0; index < count; index++)
        {
            float angle = _angle + index * MathHelper.TwoPi / count;
            yield return owner.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
    }
}

/// <summary>"stealth": Tarnung. Gegner verlieren das Ziel, der nächste Angriff verursacht Bonusschaden.</summary>
public sealed class StealthAbility : IAbilityBehavior
{
    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability)
    {
        owner.EnterStealth(ability.Definition.Duration + 0.5f * (ability.Level - 1));
        world.Effects.Burst(owner.Center, Palette.Violet, 20, 60f, gravity: -40f);
        return true;
    }
}

/// <summary>"dash": schneller Stoß (Richtung per Hoch/Runter/Blickrichtung). Zerbricht rissige Wände.</summary>
public sealed class DashAbility : IAbilityBehavior
{
    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability)
    {
        InputState input = world.Context.Input;
        Vector2 direction = input.IsDown(GameAction.Up) ? -Vector2.UnitY
            : input.IsDown(GameAction.Down) ? Vector2.UnitY
            : new Vector2(owner.FacingRight ? 1f : -1f, 0f);
        return owner.StartDash(direction, ability.Definition.Speed, ability.Definition.Duration, breaksGates: true);
    }
}

/// <summary>"air_jump": passive Mehrfachsprünge (Count = zusätzliche Sprünge in der Luft).</summary>
public sealed class AirJumpAbility : IAbilityBehavior
{
    public void OnEquip(Player owner, AbilityInstance ability) => owner.MaxAirJumps += ability.Count;

    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability) => false;
}

/// <summary>
/// "glide": Flügel als Funktion. Passiv – der Spieler gleitet, solange er im Fallen die Sprungtaste
/// hält. Die Fallgeschwindigkeit wird dabei gedeckelt, gesteuert wird weiter normal.
/// Die sichtbaren Flügel sind davon unabhängig (reine Aussehens-Ebene im Charakter-Editor).
/// </summary>
public sealed class GlideAbility : IAbilityBehavior
{
    public void OnEquip(Player owner, AbilityInstance ability) =>
        // Speed steht in abilities.json und ist hier die maximale Sinkgeschwindigkeit.
        owner.GlideFallSpeed = ability.Definition.Speed > 0f ? ability.Definition.Speed : 60f;

    public bool TryActivate(DungeonWorld world, Player owner, AbilityInstance ability) => false;
}

using CirclesOfAsh.Abilities;
using CirclesOfAsh.Assets;
using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Entities;

/// <summary>
/// Der spielbare Gott in Menschengestalt. Enthält Bewegung (Plattformer-Feeling mit Coyote Time
/// und Jump Buffer), Mana, Tarnung, Dash und die Verwaltung der Fähigkeiten.
/// Die Fähigkeiten selbst sind eigene Klassen (Strategy-Pattern), der Spieler ruft sie nur auf.
/// </summary>
public sealed class Player : Actor
{
    private const float Acceleration = 1500f;
    private const float Deceleration = 1800f;
    private const float AirControl = 0.7f;
    private const float CoyoteTime = 0.1f;        // kurz nach dem Verlassen einer Kante darf man noch springen
    private const float JumpBufferTime = 0.12f;   // zu früh gedrückter Sprung wird bei Landung ausgeführt
    private const float JumpCutMultiplier = 0.45f; // Taste loslassen = niedrigerer Sprung
    private const float HurtInvulnerability = 0.8f;

    private readonly List<AbilityInstance> _abilities = new();
    private const float WaterSpeedFactor = 0.6f;
    private const float WaterGravityFactor = 0.35f;
    private const float WaterMaxFall = 70f;
    private const float WaterJumpFactor = 0.55f;

    private readonly LayeredSprite _visual;
    private bool _wasInWater;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _dropThroughTimer;
    private int _airJumpsUsed;
    private float _dashTimer;
    private Vector2 _dashDirection;
    private float _dashSpeed;
    private bool _dashBreaksGates;
    private float _stealthTimer;

    public Player(ClassDefinition playerClass, LayeredSprite visual, StatSheet stats, Vector2 spawnPosition)
        : base(stats[StatType.MaxHealth])   // ": base(...)" ruft den Konstruktor der Basisklasse Actor auf
    {
        Class = playerClass;
        Stats = stats;
        Size = new Point(10, 22);
        Position = spawnPosition;
        Mana = stats[StatType.MaxMana];
        _visual = visual;
    }

    public ClassDefinition Class { get; }
    public StatSheet Stats { get; }
    public float Mana { get; private set; }
    public float MaxMana => Stats[StatType.MaxMana];
    public int MaxAirJumps { get; set; }
    public bool IsDashing => _dashTimer > 0f;
    public bool IsStealthed => _stealthTimer > 0f;
    public IReadOnlyList<AbilityInstance> Abilities => _abilities;
    /// <summary>Farbe des eigenen Lichts – hängt von der getragenen Laterne ab.</summary>
    public Color LightColor { get; set; } = Color.White;
    public bool IsInWater => _wasInWater;

    public override void Update(DungeonWorld world, float deltaSeconds)
    {
        InputState input = world.Context.Input;
        Health.Update(deltaSeconds);
        HitFlashSeconds -= deltaSeconds;
        _stealthTimer = MathF.Max(0f, _stealthTimer - deltaSeconds);
        Mana = MathF.Min(MaxMana, Mana + Stats[StatType.ManaRegen] * deltaSeconds);

        if (IsDashing) UpdateDash(world, deltaSeconds);
        else UpdateMovement(world, input, deltaSeconds);

        UpdateAbilities(world, input, deltaSeconds);
        UpdateAnimation(deltaSeconds);
    }

    // ------------------------------------------------------------------ Bewegung
    private void UpdateMovement(DungeonWorld world, InputState input, float deltaSeconds)
    {
        float horizontal = input.Horizontal;
        if (KnockbackSeconds > 0f)
        {
            KnockbackSeconds -= deltaSeconds;
        }
        else
        {
            float targetSpeed = horizontal * Stats[StatType.MoveSpeed];
            float rate = (MathF.Abs(horizontal) > 0.01f ? Acceleration : Deceleration) * (OnGround ? 1f : AirControl);
            Velocity.X = MoveTowards(Velocity.X, targetSpeed, rate * deltaSeconds);
        }
        if (MathF.Abs(horizontal) > 0.1f) FacingRight = horizontal > 0f;

        bool inWater = TilePhysics.IsInWater(this, world.Map);
        if (inWater != _wasInWater)
        {
            // Beim Ein- und Auftauchen spritzt es
            world.Effects.Burst(new Vector2(Center.X, Center.Y), new Color(140, 180, 220), 12, 70f, 0.5f);
            if (inWater) world.Context.Audio.Play("splash", 0.4f);
        }
        _wasInWater = inWater;
        if (inWater) Velocity.X = MathHelper.Clamp(Velocity.X, -Stats[StatType.MoveSpeed] * WaterSpeedFactor, Stats[StatType.MoveSpeed] * WaterSpeedFactor);

        _coyoteTimer = OnGround ? CoyoteTime : _coyoteTimer - deltaSeconds;
        _jumpBufferTimer = input.WasPressed(GameAction.Jump) ? JumpBufferTime : _jumpBufferTimer - deltaSeconds;
        _dropThroughTimer -= deltaSeconds;

        if (_jumpBufferTimer > 0f) TryJump(world, input);
        if (input.WasReleased(GameAction.Jump) && Velocity.Y < 0f) Velocity.Y *= JumpCutMultiplier;

        float gravity = TilePhysics.Gravity * (inWater ? WaterGravityFactor : 1f);
        float maxFall = inWater ? WaterMaxFall : TilePhysics.MaxFallSpeed;
        Velocity.Y = MathF.Min(Velocity.Y + gravity * deltaSeconds, maxFall);
        LastCollision = TilePhysics.MoveAndCollide(this, world.Map, deltaSeconds, ignorePlatforms: _dropThroughTimer > 0f);
        OnGround = LastCollision.HasFlag(CollisionResult.Landed);
        if (OnGround) _airJumpsUsed = 0;
    }

    private void TryJump(DungeonWorld world, InputState input)
    {
        if (input.IsDown(GameAction.Down) && OnGround && TilePhysics.IsStandingOnPlatformOnly(this, world.Map))
        {
            _dropThroughTimer = 0.25f;   // Runter + Springen auf Plattform = durchfallen
            _jumpBufferTimer = 0f;
            return;
        }
        if (_wasInWater)
        {
            Jump(world, WaterJumpFactor);   // im Wasser: beliebig oft "schwimmen"
        }
        else if (_coyoteTimer > 0f)
        {
            Jump(world);
        }
        else if (_airJumpsUsed < MaxAirJumps)
        {
            _airJumpsUsed++;
            Jump(world);
            world.Effects.Burst(BottomCenter, Palette.Soul, 10, 60f);
        }
    }

    private void Jump(DungeonWorld world, float powerFactor = 1f)
    {
        Velocity.Y = -Stats[StatType.JumpPower] * powerFactor;
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;
        world.Context.Audio.Play("pickup", 0.25f, -0.6f);
    }

    private static float MoveTowards(float current, float target, float maxDelta) =>
        MathF.Abs(target - current) <= maxDelta ? target : current + MathF.Sign(target - current) * maxDelta;

    // ------------------------------------------------------------------ Dash & Tarnung (von Fähigkeiten ausgelöst)
    public bool StartDash(Vector2 direction, float speed, float duration, bool breaksGates)
    {
        if (IsDashing) return false;
        _dashDirection = MathUtil.SafeNormalize(direction, Vector2.UnitX);
        _dashSpeed = speed;
        _dashTimer = duration;
        _dashBreaksGates = breaksGates;
        Health.GrantInvulnerability(duration + 0.05f);
        return true;
    }

    private void UpdateDash(DungeonWorld world, float deltaSeconds)
    {
        _dashTimer -= deltaSeconds;
        Velocity = _dashDirection * _dashSpeed;
        if (_dashBreaksGates) BreakCrackedTilesAhead(world);
        LastCollision = TilePhysics.MoveAndCollide(this, world.Map, deltaSeconds, ignorePlatforms: _dashDirection.Y > 0f);
        OnGround = LastCollision.HasFlag(CollisionResult.Landed);
        world.Effects.Burst(Center, Palette.Violet * 0.7f, 2, 10f, 0.25f, gravity: 0f);
        if (_dashTimer <= 0f) Velocity *= 0.3f;
    }

    private void BreakCrackedTilesAhead(DungeonWorld world)
    {
        Rectangle probe = Bounds;
        probe.Offset((int)(_dashDirection.X * 8f), (int)(_dashDirection.Y * 8f));
        for (int y = TileMap.ToTile(probe.Top); y <= TileMap.ToTile(probe.Bottom - 1); y++)
        {
            for (int x = TileMap.ToTile(probe.Left); x <= TileMap.ToTile(probe.Right - 1); x++)
            {
                if (world.Map[x, y] == TileType.Cracked) world.BreakTile(x, y);
            }
        }
    }

    public void EnterStealth(float seconds) => _stealthTimer = MathF.Max(_stealthTimer, seconds);

    /// <summary>
    /// Liefert den Tarnungs-Schadensbonus und beendet dabei die Tarnung (der erste Schlag enttarnt).
    /// Fähigkeiten rufen dies einmal pro Auslösung auf.
    /// </summary>
    public float ConsumeStealthBonus()
    {
        if (!IsStealthed) return 1f;
        _stealthTimer = 0f;
        return Stats[StatType.StealthDamage];
    }

    // ------------------------------------------------------------------ Kampf & Ressourcen
    public void TakeHit(DungeonWorld world, float amount, Vector2 source, float knockback)
    {
        float reduced = MathF.Max(1f, amount - Stats[StatType.Armor]);
        if (Health.TakeDamage(reduced, HurtInvulnerability) <= 0f) return;

        _stealthTimer = 0f;
        Flash();
        ApplyKnockback(source, knockback);
        world.Effects.Burst(Center, Palette.Blood, 12, 90f);
        if (world.Context.Settings.ShowDamageNumbers)
            world.Effects.Text(new Vector2(Center.X, Position.Y - 4), $"-{(int)MathF.Ceiling(reduced)}", Palette.Blood);
        world.Context.Audio.Play("hurt", 0.7f);
        world.ShakeCamera(3f);
        if (Health.IsDead) world.NotifyPlayerDied();
    }

    public void RestoreMana(float amount) => Mana = MathF.Min(MaxMana, Mana + amount);

    /// <summary>Nach Stat-Upgrades aufrufen, damit abgeleitete Werte (Max-Leben) stimmen.</summary>
    public void RefreshDerivedStats()
    {
        Health.SetMax(Stats[StatType.MaxHealth]);
        Mana = MathF.Min(Mana, MaxMana);
    }

    // ------------------------------------------------------------------ Fähigkeiten
    public void AddAbility(AbilityInstance ability)
    {
        _abilities.Add(ability);
        ability.Behavior.OnEquip(this, ability);
    }

    // FirstOrDefault liefert null, wenn nichts passt; StringComparison.OrdinalIgnoreCase ignoriert Groß/Klein
    public AbilityInstance? FindAbility(string abilityId) =>
        _abilities.FirstOrDefault(ability => string.Equals(ability.Definition.Id, abilityId, StringComparison.OrdinalIgnoreCase));

    private void UpdateAbilities(DungeonWorld world, InputState input, float deltaSeconds)
    {
        foreach (AbilityInstance ability in _abilities)
        {
            ability.CooldownRemaining = MathF.Max(0f, ability.CooldownRemaining - deltaSeconds);
            ability.Behavior.Update(world, this, ability, deltaSeconds);

            AbilityDefinition definition = ability.Definition;
            bool wantsToFire = definition.Activation switch
            {
                AbilityActivation.Auto => true,
                AbilityActivation.Manual => input.WasPressed(definition.InputAction),
                _ => false,
            };
            if (!wantsToFire || ability.CooldownRemaining > 0f || Mana < definition.ManaCost) continue;
            if (!ability.Behavior.TryActivate(world, this, ability)) continue;

            Mana -= definition.ManaCost;
            ability.CooldownRemaining = ability.EffectiveCooldown(Stats);
            world.Context.Audio.Play(definition.Sound, 0.35f);
        }
    }

    // ------------------------------------------------------------------ Heimwelt-Hub
    /// <summary>Bewegung im Hub: gleiche Plattformer-Physik, aber ohne Kampf/Fähigkeiten/Mana.</summary>
    public void UpdateHub(World.TileMap map, InputState input, float deltaSeconds)
    {
        float horizontal = input.Horizontal;
        float targetSpeed = horizontal * Stats[StatType.MoveSpeed];
        float rate = (MathF.Abs(horizontal) > 0.01f ? Acceleration : Deceleration) * (OnGround ? 1f : AirControl);
        Velocity.X = MoveTowards(Velocity.X, targetSpeed, rate * deltaSeconds);
        if (MathF.Abs(horizontal) > 0.1f) FacingRight = horizontal > 0f;

        if (input.WasPressed(GameAction.Jump) && OnGround) Velocity.Y = -Stats[StatType.JumpPower];
        Velocity.Y = MathF.Min(Velocity.Y + TilePhysics.Gravity * deltaSeconds, TilePhysics.MaxFallSpeed);
        LastCollision = TilePhysics.MoveAndCollide(this, map, deltaSeconds, ignorePlatforms: false);
        OnGround = LastCollision.HasFlag(CollisionResult.Landed);
        UpdateAnimation(deltaSeconds);
    }

    // ------------------------------------------------------------------ Darstellung
    private void UpdateAnimation(float deltaSeconds)
    {
        string clip = HitFlashSeconds > 0f ? "hurt"
            : !OnGround ? "jump"
            : MathF.Abs(Velocity.X) > 10f ? "run"
            : "idle";
        _visual.Play(clip);
        _visual.Update(deltaSeconds);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Color tint = IsStealthed ? new Color(150, 110, 220) * 0.45f : Color.White;
        // Blinken während der Unverwundbarkeit: jeden zweiten "Takt" halbtransparent
        if (Health.IsInvulnerable && !IsDashing && (int)(Health.InvulnerableSeconds * 20f) % 2 == 0) tint *= 0.35f;
        if (HitFlashSeconds > 0f) tint = ColorUtil.Multiply(tint, new Color(255, 140, 140));   // Treffer: rötlich
        _visual.Draw(spriteBatch, BottomCenter, flipHorizontally: !FacingRight, tint);
    }
}

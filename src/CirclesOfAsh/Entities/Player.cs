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
    /// <summary>
    /// Anteile der BILDgröße, aus denen die Kollisionsbox entsteht. Fest verdrahtete Pixelwerte
    /// (früher 10 x 22) müssten bei jeder Änderung der Sprite-Größe nachgezogen werden – über
    /// Anteile trägt ein größeres Sprite die Physik von selbst mit.
    /// </summary>
    private const float BodyWidthFactor = 0.62f;
    private const float BodyHeightFactor = 0.92f;
    private const float CrouchFactor = 0.55f;

    private readonly int _standHeight;
    private readonly int _crouchHeight;
    /// <summary>Geduckt kommt man nur noch halb so schnell voran – das ist der Preis fuer die Deckung.</summary>
    private const float CrouchSpeedFactor = 0.45f;

    // ---------------------------------------------------------------- Manueller Nahkampf
    /// <summary>Ausdauer regeneriert pro Sekunde, solange nicht geblockt wird.</summary>
    private const float StaminaRegenPerSecond = 28f;
    private const float MaxStamina = 100f;
    private const float BlockDrainPerSecond = 18f;
    private const float AttackStaminaCost = 12f;
    private const float SpinJumpStaminaCost = 25f;
    /// <summary>So lange nach dem Blocken zaehlt ein Treffer als Parade.</summary>
    private const float ParryWindow = 0.22f;
    /// <summary>Zeitfenster, in dem der naechste Schlag die Kombo fortsetzt statt neu zu beginnen.</summary>
    private const float ComboWindow = 0.55f;
    private const float AttackDuration = 0.18f;
    /// <summary>Schadensfaktor je Kombostufe – der dritte Schlag sitzt deutlich haerter.</summary>
    private static readonly float[] ComboDamage = { 1.0f, 1.15f, 1.6f };
    private const float BlockedDamageFactor = 0.25f;

    private readonly List<AbilityInstance> _abilities = new();
    private const float WaterSpeedFactor = 0.6f;
    private const float WaterGravityFactor = 0.35f;
    private const float WaterMaxFall = 70f;
    private const float WaterJumpFactor = 0.55f;

    private LayeredSprite _visual;
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

    /// <summary>Geduckt: halbe Trefferbox, langsamer, kein Sprung. Unter niedrigen Decken erzwungen.</summary>
    public bool IsCrouching { get; private set; }

    /// <summary>Ausdauer fuer den manuellen Nahkampf (0..100).</summary>
    public float Stamina { get; private set; } = MaxStamina;
    public float MaxStaminaValue => MaxStamina;
    /// <summary>true, solange die Blocktaste gehalten wird UND Ausdauer da ist.</summary>
    public bool IsBlocking { get; private set; }
    /// <summary>Kombostufe 0..2, nur zur Anzeige.</summary>
    public int ComboStep { get; private set; }

    private float _attackTimer;
    private float _comboTimer;
    private float _blockHeldTimer;
    private bool _isSpinning;

    public Player(ClassDefinition playerClass, LayeredSprite visual, StatSheet stats, Vector2 spawnPosition)
        : base(stats[StatType.MaxHealth])   // ": base(...)" ruft den Konstruktor der Basisklasse Actor auf
    {
        Class = playerClass;
        Stats = stats;
        // Kollisionsbox aus der Bildgröße ableiten (16x24 -> 10x22, 24x32 -> 15x29).
        Point frame = visual.FrameSize;
        _standHeight = Math.Max(4, (int)MathF.Round(frame.Y * BodyHeightFactor));
        _crouchHeight = Math.Max(3, (int)MathF.Round(_standHeight * CrouchFactor));
        Size = new Point(Math.Max(4, (int)MathF.Round(frame.X * BodyWidthFactor)), _standHeight);
        Position = spawnPosition;
        Mana = stats[StatType.MaxMana];
        _visual = visual;
    }

    public ClassDefinition Class { get; }
    public StatSheet Stats { get; }
    public float Mana { get; private set; }
    public float MaxMana => Stats[StatType.MaxMana];
    public int MaxAirJumps { get; set; }

    /// <summary>
    /// Maximale Sinkgeschwindigkeit beim Gleiten (0 = kein Gleiten). Wird von der Fähigkeit
    /// "glide" gesetzt; gegleitet wird, solange im Fallen die Sprungtaste gehalten wird.
    /// </summary>
    public float GlideFallSpeed { get; set; }

    /// <summary>Nur zur Anzeige und fuer Effekte: gleitet der Spieler gerade?</summary>
    public bool IsGliding { get; private set; }
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
        UpdateCrouch(world, input);
        UpdateMeleeCombat(world, input, deltaSeconds);
        if (KnockbackSeconds > 0f)
        {
            KnockbackSeconds -= deltaSeconds;
        }
        else
        {
            float targetSpeed = horizontal * Stats[StatType.MoveSpeed] * (IsCrouching ? CrouchSpeedFactor : 1f);
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

        // Gleiten: nur im Fallen, nur an der Luft, nur mit gehaltener Sprungtaste. Die Schwerkraft
        // wirkt weiter, wird aber sofort auf die Gleitgeschwindigkeit gedeckelt.
        IsGliding = GlideFallSpeed > 0f && !OnGround && !inWater && Velocity.Y > 0f
                    && input.IsDown(GameAction.Jump);
        if (IsGliding)
        {
            gravity *= 0.35f;
            maxFall = GlideFallSpeed;
        }
        Velocity.Y = MathF.Min(Velocity.Y + gravity * deltaSeconds, maxFall);
        LastCollision = TilePhysics.MoveAndCollide(this, world.Map, deltaSeconds, ignorePlatforms: _dropThroughTimer > 0f);
        OnGround = LastCollision.HasFlag(CollisionResult.Landed);
        if (OnGround) _airJumpsUsed = 0;
    }

    /// <summary>
    /// Manueller Nahkampf: Schlagkombo, Block mit Parade und Drehsprung. Bewusst hier im Spieler
    /// statt als Faehigkeit – anders als die Faehigkeiten haengt das direkt an Bewegung und
    /// Trefferbox, und es soll auch ohne ausgeruestete Faehigkeit funktionieren.
    /// </summary>
    private void UpdateMeleeCombat(DungeonWorld world, InputState input, float deltaSeconds)
    {
        _attackTimer -= deltaSeconds;
        _comboTimer -= deltaSeconds;
        if (_comboTimer <= 0f) ComboStep = 0;

        // ---- Blocken: haelt Schaden ab, zehrt aber an der Ausdauer
        bool wantsBlock = input.IsDown(GameAction.Block) && OnGround && Stamina > 0f;
        if (wantsBlock)
        {
            if (!IsBlocking) _blockHeldTimer = 0f;   // frisch aufgesetzt -> Paradefenster laeuft
            _blockHeldTimer += deltaSeconds;
            Stamina = MathF.Max(0f, Stamina - BlockDrainPerSecond * deltaSeconds);
            Velocity.X *= 0.4f;   // im Block kommt man kaum vom Fleck
        }
        else
        {
            Stamina = MathF.Min(MaxStamina, Stamina + StaminaRegenPerSecond * deltaSeconds);
        }
        IsBlocking = wantsBlock;

        // ---- Drehsprung: Sprungangriff, der beim Aufkommen im Umkreis trifft
        if (input.WasPressed(GameAction.Up) && !_isSpinning && Stamina >= SpinJumpStaminaCost
            && !IsCrouching && !IsBlocking && (OnGround || _coyoteTimer > 0f))
        {
            Stamina -= SpinJumpStaminaCost;
            _isSpinning = true;
            Velocity.Y = -Stats[StatType.JumpPower] * 0.9f;
            world.Context.Audio.Play("slash", 0.5f, 0.3f);
        }
        if (_isSpinning && OnGround && Velocity.Y >= 0f)
        {
            _isSpinning = false;
            SpinImpact(world);
        }

        // ---- Schlagkombo
        if (!input.WasPressed(GameAction.Attack) || _attackTimer > 0f || IsBlocking) return;
        if (Stamina < AttackStaminaCost) return;
        Stamina -= AttackStaminaCost;
        _attackTimer = AttackDuration;
        _comboTimer = ComboWindow;
        Swing(world, ComboDamage[ComboStep]);
        ComboStep = (ComboStep + 1) % ComboDamage.Length;
    }

    /// <summary>Ein Hieb nach vorn. Reichweite und Flaeche wie bei MeleeArcAbility, nur spielergesteuert.</summary>
    private void Swing(DungeonWorld world, float damageFactor)
    {
        const int reach = 26;
        var area = new Rectangle(FacingRight ? Bounds.Right : Bounds.Left - reach, Bounds.Top - 6, reach, Size.Y + 10);
        float damage = Stats[StatType.Might] * damageFactor * ConsumeStealthBonus();
        foreach (Enemy enemy in world.EnemiesIntersecting(area).ToList())
            world.DamageEnemy(enemy, damage, Center, 120f);
        world.HitBreakables(new Vector2(area.Center.X, Center.Y), reach);
        world.Effects.Burst(new Vector2(area.Center.X, Center.Y), Palette.Bone, 4, 60f, 0.25f);
        world.Context.Audio.Play("slash", 0.45f, damageFactor > 1.3f ? -0.2f : 0.15f);
    }

    /// <summary>Aufschlag des Drehsprungs: Schaden im Umkreis, dazu Erschuetterung.</summary>
    private void SpinImpact(DungeonWorld world)
    {
        const int radius = 34;
        var area = new Rectangle((int)Center.X - radius, Bounds.Top, radius * 2, Size.Y + 8);
        float damage = Stats[StatType.Might] * 1.4f;
        foreach (Enemy enemy in world.EnemiesIntersecting(area).ToList())
            world.DamageEnemy(enemy, damage, Center, 200f);
        world.HitBreakables(Center, radius);
        world.Effects.Ring(Center, radius, Palette.Gold);
        world.ShakeCamera(3f);
        world.Context.Audio.Play("hit", 0.6f, -0.3f);
    }

    /// <summary>
    /// Ducken: Runter halten, solange man am Boden und nicht im Wasser ist. Das Aufstehen ist
    /// gesperrt, solange oben eine massive Kachel liegt – sonst steckte die Figur in der Decke.
    /// </summary>
    private void UpdateCrouch(DungeonWorld world, InputState input)
    {
        bool wantsCrouch = input.IsDown(GameAction.Down) && OnGround && !_wasInWater && !IsDashing;
        if (wantsCrouch)
        {
            SetHeight(_crouchHeight);
            IsCrouching = true;
            return;
        }
        if (!IsCrouching) return;

        // Aufstehen nur, wenn die volle Hoehe frei ist.
        var standBox = new Rectangle((int)MathF.Floor(Position.X), (int)MathF.Floor(Position.Y + Size.Y - _standHeight),
                                     Size.X, _standHeight);
        if (TilePhysics.IsBlocked(world.Map, standBox)) return;
        SetHeight(_standHeight);
        IsCrouching = false;
    }

    /// <summary>Aendert die Hoehe der Trefferbox und haelt dabei die Fuesse an Ort und Stelle.</summary>
    private void SetHeight(int height)
    {
        if (Size.Y == height) return;
        float bottom = Position.Y + Size.Y;
        Size = new Point(Size.X, height);
        Position.Y = bottom - height;
    }

    private void TryJump(DungeonWorld world, InputState input)
    {
        if (input.IsDown(GameAction.Down) && OnGround && TilePhysics.IsStandingOnPlatformOnly(this, world.Map))
        {
            _dropThroughTimer = 0.25f;   // Runter + Springen auf Plattform = durchfallen
            _jumpBufferTimer = 0f;
            return;
        }
        // Geduckt wird nicht gesprungen – sonst schnellt man unter jeder niedrigen Decke hoch.
        if (IsCrouching) { _jumpBufferTimer = 0f; return; }
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

    /// <summary>
    /// Die Ruestung hat den Treffer geschluckt. Beim letzten Mal zerspringt sie und fliegt in
    /// Einzelteilen davon - danach steht die Figur in ihrer leichten Kleidung da.
    /// </summary>
    private void OnArmorHit(DungeonWorld world, Vector2 source, float knockback,
                            Progression.EquipmentService.ArmorResult result)
    {
        _stealthTimer = 0f;
        Flash();
        ApplyKnockback(source, knockback);
        // Gleiches Zeitfenster wie bei echtem Schaden, sonst nimmt EIN Gegnerkontakt der Reihe
        // nach alle Ruestungsstufen mit.
        Health.GrantInvulnerability(HurtInvulnerability);

        if (result == Progression.EquipmentService.ArmorResult.Absorbed)
        {
            world.Effects.Burst(Center, Palette.Ash, 8, 90f, 0.45f);
            world.Context.Audio.Play("hit", 0.7f, -0.45f);   // dumpfer als ein Treffer auf Fleisch
            world.ShakeCamera(2.5f);
            return;
        }

        // Zerspringen: Teile in mehreren Schueben, damit sie gestaffelt wegfliegen und fallen.
        world.Effects.Burst(Center, Palette.Ash, 16, 150f, 0.9f);
        world.Effects.Burst(Center, Palette.Bone, 10, 110f, 0.8f);
        world.Effects.Burst(Center, Palette.Gold, 6, 190f, 1.0f);
        world.Effects.Ring(Center, 24f, Palette.Bone);
        world.Context.Audio.Play("crumble", 0.9f, -0.3f);
        world.Announce("Deine Ruestung zerspringt!");
        world.ShakeCamera(6f);

        RefreshDerivedStats();
        Stats.SetSource(Progression.EquipmentService.StatSource,
            Progression.EquipmentService.CollectModifiers(world.Context.Definitions, world.Run));
        RefreshAppearance(world.Context, world.Run);   // Panzer verschwindet auch sichtbar
    }

    /// <summary>
    /// Baut das Ebenen-Sprite neu – nötig, wenn sich die getragene Rüstung ändert. Bewusst kein
    /// readonly-Feld mehr: Die Rüstung ist die einzige Ebene, die sich mitten im Lauf ändert.
    /// </summary>
    public void RefreshAppearance(GameContext context, Progression.RunState run) =>
        _visual = Progression.CharacterVisuals.Create(context, Class, run.Appearance,
            Progression.EquipmentService.ArmorSprite(context.Definitions, run));

    // ------------------------------------------------------------------ Kampf & Ressourcen
    public void TakeHit(DungeonWorld world, float amount, Vector2 source, float knockback)
    {
        float reduced = MathF.Max(1f, amount - Stats[StatType.Armor]);

        if (IsBlocking)
        {
            // Parade: im ersten Moment des Blockens kostet der Treffer gar nichts und der Angreifer
            // prallt zurueck. Spaeter geblockt kommt ein Viertel durch.
            bool isParry = _blockHeldTimer <= ParryWindow;
            world.Effects.Ring(Center, isParry ? 26f : 18f, isParry ? Palette.Gold : Palette.Ash);
            world.Context.Audio.Play(isParry ? "unseal" : "hit", isParry ? 0.7f : 0.4f, isParry ? 0.4f : -0.1f);
            world.ShakeCamera(isParry ? 4f : 2f);
            if (isParry)
            {
                // Der Angreifer wird zurueckgestossen und ist kurz offen.
                foreach (Enemy enemy in world.EnemiesIntersecting(Bounds).ToList())
                    world.DamageEnemy(enemy, 0f, Center, 260f);
                return;
            }
            reduced *= BlockedDamageFactor;
            Stamina = MathF.Max(0f, Stamina - 20f);
        }

        // Die Ruestung faengt den Treffer VOLLSTAENDIG ab, bevor Leben verloren geht (Vorbild
        // Ghosts 'n Goblins). Vorher lief TakeDamage zuerst und die Ruestung litt nur zusaetzlich
        // mit - sie war also eine zweite Lebensleiste statt eines Schildes.
        if (!Health.IsInvulnerable)
        {
            var armorResult = Progression.EquipmentService.AbsorbHit(world.Context.Definitions, world.Run);
            if (armorResult != Progression.EquipmentService.ArmorResult.None)
            {
                OnArmorHit(world, source, knockback, armorResult);
                return;
            }
        }

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
        // Solange es keine eigenen Hock-Sprites gibt, wird die Figur gestaucht gezeichnet.
        // Der Zeichenursprung liegt auf den Fuessen, sie sinkt also korrekt zusammen.
        Vector2? squash = IsCrouching ? new Vector2(1f, _crouchHeight / (float)_standHeight) : null;
        _visual.Draw(spriteBatch, BottomCenter, flipHorizontally: !FacingRight, tint, squash);
    }
}

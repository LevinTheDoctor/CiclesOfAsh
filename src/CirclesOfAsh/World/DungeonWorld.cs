using CirclesOfAsh.Assets;
using CirclesOfAsh.Combat;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Entities;
using CirclesOfAsh.Progression;
using CirclesOfAsh.Puzzles;

namespace CirclesOfAsh.World;

/// <summary>
/// Laufzeitzustand EINES Dungeons: Karte, Entities, Props, Rätsel, Licht, Kamera, Wellen, Siegel.
/// Bietet Abfragen (nächster Gegner ...) und zentrale Aktionen (Schaden, Tod, Beute, Tor öffnen),
/// damit Fähigkeiten/Brains/Props nicht selbst Listen manipulieren müssen.
/// Nach außen (Szene) kommuniziert die Welt über Events -> sie kennt keine Szenen.
/// </summary>
public sealed class DungeonWorld : IDisposable
{
    private const float AnnouncementSeconds = 2.4f;
    private const float InteractionRange = 14f;

    private readonly List<Enemy> _enemies = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<Pickup> _pickups = new();
    private readonly List<Prop> _props = new();
    private readonly List<Companion> _companions;
    private readonly List<Entity> _spawnQueue = new();
    private readonly Queue<string> _announcements = new();
    private readonly Texture2D _tileset;
    private readonly Texture2D _backgroundFar;
    private readonly Texture2D _backgroundMid;
    private readonly Color _tileTint;
    private readonly Color _backgroundTint;
    private readonly AnimationPlayer _sigil;
    private readonly CrumbleSystem _crumble;
    private bool _goalTriggered;
    private bool _bossDefeated;
    private bool _playerDeathReported;
    private float _shakeStrength;
    private Vector2 _shakeOffset;
    private string? _announcement;
    private float _announcementTimer;
    private float _time;

    public DungeonWorld(GameContext context, DungeonPlan plan, DungeonLayout layout, RunState run,
                        Player player, IEnumerable<Companion> companions)
    {
        Context = context;
        Plan = plan;
        Layout = layout;
        Run = run;
        Player = player;
        _companions = companions.ToList();
        Random = new Random(plan.Seed ^ 0x5EED);   // "^" = XOR: leitet einen zweiten, unabhängigen Seed ab
        Effects = new EffectSystem(Random);
        Camera = new Camera2D(CirclesGame.VirtualWidth, CirclesGame.VirtualHeight);
        Waves = new WaveDirector(plan, context.Definitions.Balance, layout.Rooms);
        Lighting = new LightingSystem(context.GraphicsDevice, CirclesGame.VirtualWidth, CirclesGame.VirtualHeight)
        {
            Ambient = ColorUtil.FromHex(plan.Circle.AmbientLight, Color.Gray),   // Objekt-Initialisierer nach dem Konstruktor
        };
        _crumble = new CrumbleSystem(context.Definitions.Balance);

        _tileset = context.Assets.GetTexture(plan.Circle.Tileset);
        _backgroundFar = context.Assets.GetTexture(plan.Circle.Background);
        _backgroundMid = context.Assets.GetTexture("background.mid");
        _tileTint = ColorUtil.FromHex(plan.Circle.TileTint, Color.White);
        _backgroundTint = ColorUtil.FromHex(plan.Circle.BackgroundTint, Color.White);
        _sigil = new AnimationPlayer(context.Assets.GetSpriteSheet("exit.sigil"));

        CreateProps();
        foreach (CollectiblePlacement collectible in layout.Collectibles) SpawnItemPickup(collectible.ItemId, collectible.Center, floating: true);

        if (layout.Puzzle is { } puzzleSpec)
        {
            Puzzle = context.Behaviors.CreatePuzzle(puzzleSpec.Key);
            Puzzle.Initialize(this);
        }
        IsGateOpen = layout.GateTiles.Count == 0;

        UpdateCurrentRoom();
        Camera.SnapTo(Player.Center, CurrentRoom?.PixelBounds ?? Map.PixelBounds);
        TryActivateGoal();
    }

    // "event Action?" = Beobachter-Muster (Observer): Die Szene abonniert, die Welt meldet.
    public event Action? PlayerDied;
    public event Action? GoalReached;

    public GameContext Context { get; }
    public DungeonPlan Plan { get; }
    public DungeonLayout Layout { get; }
    public TileMap Map => Layout.Map;
    public RunState Run { get; }
    public Player Player { get; }
    public EffectSystem Effects { get; }
    public Camera2D Camera { get; }
    public WaveDirector Waves { get; }
    public LightingSystem Lighting { get; }
    public IPuzzle? Puzzle { get; }
    public Random Random { get; }
    public RoomNode? CurrentRoom { get; private set; }
    public bool IsGoalActive { get; private set; }
    public bool IsGateOpen { get; private set; }
    public Enemy? ActiveBoss { get; private set; }
    public Prop? InteractionTarget { get; private set; }
    public int PendingLevelUps { get; set; }
    public IReadOnlyList<Companion> Companions => _companions;

    public int AliveEnemyCount =>
        _enemies.Count(enemy => !enemy.IsRemoved) + _spawnQueue.Count(entity => entity is Enemy);

    private Matrix WorldTransform => Camera.Transform * Matrix.CreateTranslation(_shakeOffset.X, _shakeOffset.Y, 0f);

    private void CreateProps()
    {
        foreach (PropPlacement placement in Layout.Props)
        {
            var prop = new Prop(placement.Definition, Context.Assets.GetSpriteSheet(placement.Definition.SpriteSheet),
                Context.Behaviors.CreateProp(placement.Definition.Behavior), placement.BottomCenter,
                placement.Room, placement.Tag, placement.Index);
            _props.Add(prop);
        }
        // Erst initialisieren, wenn alle existieren (Behaviors dürfen andere Props abfragen)
        foreach (Prop prop in _props) prop.Behavior.Initialize(prop, this);
    }

    // ------------------------------------------------------------------ Game-Loop
    public void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        Player.Update(this, deltaSeconds);
        UpdateCurrentRoom();
        Waves.Update(this, deltaSeconds);
        _crumble.Update(this, deltaSeconds);
        Puzzle?.Update(this, deltaSeconds);

        foreach (Companion companion in _companions) companion.Update(this, deltaSeconds);
        foreach (Enemy enemy in _enemies) if (!enemy.IsRemoved) enemy.Update(this, deltaSeconds);
        foreach (Projectile projectile in _projectiles) if (!projectile.IsRemoved) projectile.Update(this, deltaSeconds);
        foreach (Pickup pickup in _pickups) if (!pickup.IsRemoved) pickup.Update(this, deltaSeconds);
        foreach (Prop prop in _props) if (!prop.IsRemoved) prop.Update(this, deltaSeconds);

        UpdateInteraction();
        ApplyContactDamage();
        Effects.Ambient(Plan.Circle.AmbientParticles, Camera.VisibleArea, deltaSeconds);
        Effects.Update(deltaSeconds);
        _sigil.Update(deltaSeconds);
        CheckGoal();
        RemoveDeadAndFlushSpawns();
        UpdateAnnouncements(deltaSeconds);

        _shakeStrength = MathUtil.Damp(_shakeStrength, 0f, 10f, deltaSeconds);
        Vector2 shake = _shakeStrength > 0.2f ? MathUtil.RandomDirection(Random) * _shakeStrength : Vector2.Zero;
        _shakeOffset = new Vector2(MathF.Round(shake.X), MathF.Round(shake.Y));   // einmal pro Frame -> Licht und Welt wackeln gleich
        Camera.Follow(Player.Center, CurrentRoom?.PixelBounds ?? Map.PixelBounds, deltaSeconds);
    }

    private void UpdateCurrentRoom()
    {
        RoomNode? room = Layout.RoomAtPixel(Player.Center);
        if (room is null || room == CurrentRoom) return;
        CurrentRoom = room;
        if (!room.IsVisited && room.Theme is { } theme && room.Type is RoomType.Corridor or RoomType.Start)
            Announce(theme.Name);   // Raumname beim ersten Betreten -> Räume bekommen Identität
        room.IsVisited = true;
    }

    /// <summary>Nächstes interagierbares Prop in Reichweite suchen und bei Tastendruck auslösen.</summary>
    private void UpdateInteraction()
    {
        InteractionTarget = null;
        float bestDistance = float.MaxValue;
        foreach (Prop prop in _props)
        {
            if (!prop.CanInteract || prop.IsRemoved) continue;
            Rectangle area = prop.Bounds;
            area.Inflate(InteractionRange, 4);
            if (!area.Intersects(Player.Bounds)) continue;
            float distance = MathF.Abs(prop.Center.X - Player.Center.X);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            InteractionTarget = prop;
        }
        if (InteractionTarget is not null && Context.Input.WasPressed(GameAction.Interact))
            InteractionTarget.Behavior.Interact(InteractionTarget, this);
    }

    private void ApplyContactDamage()
    {
        foreach (Enemy enemy in _enemies)
        {
            if (enemy.IsRemoved || enemy.IsSpawning || !enemy.Bounds.Intersects(Player.Bounds)) continue;
            Player.TakeHit(this, enemy.ContactDamage, enemy.Center, 110f);
        }
    }

    private void CheckGoal()
    {
        if (!IsGoalActive || _goalTriggered) return;
        var trigger = new Rectangle((int)Layout.GoalBottomCenter.X - 12, (int)Layout.GoalBottomCenter.Y - 32, 24, 32);
        if (!trigger.Intersects(Player.Bounds)) return;
        _goalTriggered = true;
        GoalReached?.Invoke();   // "?.Invoke" = nur aufrufen, wenn jemand abonniert hat
    }

    private void RemoveDeadAndFlushSpawns()
    {
        _enemies.RemoveAll(entity => entity.IsRemoved);
        _projectiles.RemoveAll(entity => entity.IsRemoved);
        _pickups.RemoveAll(entity => entity.IsRemoved);
        _props.RemoveAll(entity => entity.IsRemoved);

        foreach (Entity entity in _spawnQueue)
        {
            // switch-Anweisung mit Typ-Mustern: prüft den Laufzeittyp und castet gleichzeitig
            switch (entity)
            {
                case Enemy enemy: _enemies.Add(enemy); break;
                case Projectile projectile: _projectiles.Add(projectile); break;
                case Pickup pickup: _pickups.Add(pickup); break;
            }
        }
        _spawnQueue.Clear();
    }

    private void UpdateAnnouncements(float deltaSeconds)
    {
        _announcementTimer -= deltaSeconds;
        if (_announcementTimer > 0f || _announcements.Count == 0) return;
        _announcement = _announcements.Dequeue();   // Warteschlange: nichts wird überschrieben
        _announcementTimer = AnnouncementSeconds;
    }

    // ------------------------------------------------------------------ Abfragen
    public Enemy? FindNearestEnemy(Vector2 from, float maxRange)
    {
        Enemy? nearest = null;
        float bestDistanceSquared = maxRange * maxRange;   // Quadrat-Abstände sparen die teure Wurzel
        foreach (Enemy enemy in _enemies)
        {
            if (enemy.IsRemoved || enemy.IsSpawning) continue;
            float distanceSquared = Vector2.DistanceSquared(from, enemy.Center);
            if (distanceSquared >= bestDistanceSquared) continue;
            bestDistanceSquared = distanceSquared;
            nearest = enemy;
        }
        return nearest;
    }

    // "yield return" = Iterator: liefert Treffer einzeln, ohne eine Zwischenliste anzulegen
    public IEnumerable<Enemy> EnemiesInRadius(Vector2 center, float radius)
    {
        float radiusSquared = radius * radius;
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsRemoved && !enemy.IsSpawning && Vector2.DistanceSquared(center, enemy.Center) <= radiusSquared)
                yield return enemy;
        }
    }

    public IEnumerable<Enemy> EnemiesIntersecting(Rectangle area)
    {
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsRemoved && !enemy.IsSpawning && enemy.Bounds.Intersects(area)) yield return enemy;
        }
    }

    public IEnumerable<Prop> PropsWithTag(string tag) => _props.Where(prop => prop.Tag == tag);

    // ------------------------------------------------------------------ Aktionen
    public void Spawn(Entity entity) => _spawnQueue.Add(entity);

    public Enemy SpawnEnemy(EnemyDefinition definition, Vector2 bottomCenter)
    {
        float healthMultiplier = Plan.DifficultyMultiplier;
        float damageMultiplier = 1f + (Plan.DifficultyMultiplier - 1f) * 0.5f;
        var enemy = new Enemy(definition, Context.Assets.GetSpriteSheet(definition.SpriteSheet),
            Context.Behaviors.CreateEnemyBrain(definition.Brain), bottomCenter, healthMultiplier, damageMultiplier);
        if (definition.IsBoss || definition.IsMiniBoss) ActiveBoss = enemy;
        Spawn(enemy);
        return enemy;
    }

    public void DamageEnemy(Enemy enemy, float amount, Vector2 source, float knockback)
    {
        if (enemy.IsRemoved || enemy.IsSpawning) return;
        float applied = enemy.Health.TakeDamage(amount, 0f);
        if (applied <= 0f) return;

        enemy.Flash();
        bool isHeavy = enemy.IsBoss || enemy.IsMiniBoss;
        enemy.ApplyKnockback(source, knockback, isHeavy ? 1f : enemy.Definition.KnockbackResistance);
        Effects.Burst(enemy.Center, Palette.Blood, 5, 70f);
        Effects.Text(new Vector2(enemy.Center.X, enemy.Position.Y - 2), ((int)MathF.Ceiling(applied)).ToString(), Palette.Bone);
        Context.Audio.Play("hit", 0.3f, Random.NextSingle() * 0.4f - 0.2f);
        if (enemy.Health.IsDead) KillEnemy(enemy);
    }

    private void KillEnemy(Enemy enemy)
    {
        enemy.Remove();
        Effects.Burst(enemy.Center, Palette.Ash, 14, 90f);
        for (int soul = 0; soul < enemy.Definition.SoulValue; soul++) SpawnPickup(PickupKind.Soul, enemy.Center, 1f);

        BalanceDefinition balance = Context.Definitions.Balance;
        double roll = Random.NextDouble();
        if (roll < balance.HeartDropChance) SpawnPickup(PickupKind.Heart, enemy.Center, 15f);
        else if (roll < balance.HeartDropChance + balance.ManaDropChance) SpawnPickup(PickupKind.ManaShard, enemy.Center, 20f);

        AnnounceMissions(Context.Progression.Missions.Report(MissionType.Slay, enemy.Definition.Id));

        if (enemy != ActiveBoss) return;
        ActiveBoss = null;
        Effects.Ring(enemy.Center, 60f, Palette.Faith, 48);
        ShakeCamera(8f);
        Context.Audio.Play("roar", 0.8f, -0.4f);
        foreach (Enemy minion in _enemies.Where(other => !other.IsRemoved)) KillEnemy(minion);   // Diener sterben mit dem Boss
    }

    private void SpawnPickup(PickupKind kind, Vector2 center, float value)
    {
        string sheetId = kind switch
        {
            PickupKind.Soul => "pickup.soul",
            PickupKind.Heart => "pickup.heart",
            PickupKind.ManaShard => "pickup.mana",
            _ => "pickup.relic",
        };
        Spawn(new Pickup(kind, Context.Assets.GetSpriteSheet(sheetId), center, value, Random));
    }

    private void SpawnItemPickup(string itemId, Vector2 center, bool floating)
    {
        var pickup = new Pickup(PickupKind.Item, Context.Assets.GetSpriteSheet("items.icons"), center, 0f, Random,
                                itemId, clip: itemId, floating: floating);
        // Im Konstruktor direkt einfügen (Warteschlange läuft erst im ersten Update)
        if (floating) _pickups.Add(pickup);
        else Spawn(pickup);
    }

    public void CollectPickup(Pickup pickup)
    {
        switch (pickup.Kind)
        {
            case PickupKind.Soul:
                GainExperience(pickup.Value);
                Context.Audio.Play("pickup", 0.15f, Random.NextSingle() * 0.3f);
                break;
            case PickupKind.Heart:
                Player.Health.Heal(pickup.Value);
                Effects.Text(Player.Center - new Vector2(0, 16), $"+{pickup.Value:0}", Palette.Soul);
                break;
            case PickupKind.ManaShard:
                Player.RestoreMana(pickup.Value);
                Effects.Text(Player.Center - new Vector2(0, 16), $"+{pickup.Value:0}", Palette.Mana);
                break;
            case PickupKind.Relic:
                UpgradeOffer? relic = LevelUpService.CreateRandomStatOffer(Context, Run, Random);
                if (relic is not null)
                {
                    LevelUpService.Apply(relic, Context, Run, Player);
                    Announce($"Reliquie: {relic.Title}");
                }
                Player.Health.Heal(Player.Health.Max);
                Context.Audio.Play("levelup", 0.6f);
                break;
            case PickupKind.Item:
                CollectItem(pickup.ItemId);
                break;
        }
    }

    private void CollectItem(string itemId)
    {
        if (!Context.Definitions.Items.Contains(itemId)) return;
        ItemDefinition item = Context.Definitions.Items.Get(itemId);
        Context.Audio.Play("chest", 0.5f, 0.3f);
        Effects.Ring(Player.Center, 20f, Palette.Faith, 20);

        if (item.Slot == ItemSlot.Collectible)
        {
            Announce($"Gefunden: {item.Name}");
            AnnounceMissions(Context.Progression.Missions.Report(MissionType.Collect, item.Id));
            return;
        }
        bool equipped = EquipmentService.AddItem(Run, item);
        if (equipped) EquipmentService.Apply(Context.Definitions, Run, Player);
        Announce(equipped ? $"{item.Name} – ausgerüstet" : $"{item.Name} – im Inventar");
    }

    /// <summary>Wird von Truhen aufgerufen. Kein neues Item mehr übrig? Dann gibt es eine Reliquie.</summary>
    public void OpenChest(Prop chest, bool isTreasure)
    {
        Context.Audio.Play("chest", 0.7f);
        Effects.Burst(chest.Center, Palette.Faith, 16, 80f);
        Vector2 spawn = new(chest.Center.X, chest.Position.Y - 4);
        ItemDefinition? loot = EquipmentService.RollLoot(Context.Definitions, Run, Plan.CircleIndex, Random, isTreasure);
        if (loot is not null) SpawnItemPickup(loot.Id, spawn, floating: false);
        else SpawnPickup(PickupKind.Relic, spawn, 0f);
        for (int soul = 0; soul < (isTreasure ? 12 : 5); soul++) SpawnPickup(PickupKind.Soul, spawn, 1f);
    }

    public void NotifyPuzzle(Prop prop) => Puzzle?.OnPropActivated(prop, this);

    /// <summary>Öffnet das Siegeltor um den Ausgang (Rätsel gelöst).</summary>
    public void OpenGate()
    {
        if (IsGateOpen) return;
        IsGateOpen = true;
        foreach (Point tile in Layout.GateTiles)
        {
            Map[tile.X, tile.Y] = TileType.Empty;
            Effects.Burst(new Vector2((tile.X + 0.5f) * TileMap.TileSize, (tile.Y + 0.5f) * TileMap.TileSize), Palette.Ash, 3, 40f);
        }
        Context.Audio.Play("gate", 0.8f);
        ShakeCamera(5f);
        TryActivateGoal();
    }

    public void GainExperience(float amount)
    {
        Run.Experience += amount;
        // while statt if: große Seelenmengen können mehrere Level auf einmal bringen
        while (Run.Experience >= Context.Progression.ExperienceForNextLevel(Run.Level))
        {
            Run.Experience -= Context.Progression.ExperienceForNextLevel(Run.Level);
            Run.Level++;
            PendingLevelUps++;
            Effects.Ring(Player.Center, 30f, Palette.Faith);
            Context.Audio.Play("levelup", 0.6f);
        }
    }

    public void BreakTile(int tileX, int tileY)
    {
        Map[tileX, tileY] = TileType.Empty;
        var center = new Vector2((tileX + 0.5f) * TileMap.TileSize, (tileY + 0.5f) * TileMap.TileSize);
        Effects.Burst(center, Palette.Ember, 10, 100f);
        Context.Audio.Play("hit", 0.6f, -0.5f);
        ShakeCamera(2f);
    }

    public void OnArenaCleared(RoomNode room)
    {
        Context.Audio.Play("unseal", 0.6f);
        switch (room.Type)
        {
            case RoomType.Boss:
                _bossDefeated = true;
                break;
            case RoomType.Prison:
                FreeCaptives(room);
                break;
            default:
                Announce("Die Tore öffnen sich.");
                break;
        }
        TryActivateGoal();
    }

    private void FreeCaptives(RoomNode room)
    {
        foreach (Prop cage in _props.Where(prop => prop.Tag == "cage" && prop.Room == room))
            cage.Behavior.OnSignal(cage, this, "open");

        RescueResult result = Context.Progression.RescueCaptives(Plan, Run);
        if (result.AlreadyRescued)
        {
            Announce("Diese Seelen sind bereits frei.");
            return;
        }
        Announce($"Die Gefangenen sind frei! +{result.Believers} Gläubige");
        if (result.CompanionName is not null) Announce($"Neuer Begleiter: {result.CompanionName}");
        AnnounceMissions(result.CompletedMissions);
    }

    private void AnnounceMissions(IEnumerable<MissionDefinition> completed)
    {
        foreach (MissionDefinition mission in completed)
        {
            Announce($"Bitte erfüllt: {mission.Title} (+{mission.RewardBelievers})");
            Context.Audio.Play("levelup", 0.5f, 0.2f);
        }
    }

    /// <summary>Das Siegel erwacht erst, wenn alle Kämpfe geschafft UND das Tor offen ist.</summary>
    private void TryActivateGoal()
    {
        if (IsGoalActive) return;
        bool fightsDone = Plan.IsBossDungeon ? _bossDefeated : Waves.AllArenasCleared;
        if (!fightsDone || !IsGateOpen) return;
        IsGoalActive = true;
        _sigil.Play("active");
        Announce("Das Siegel ist erwacht – finde es!");
    }

    public void NotifyPlayerDied()
    {
        if (_playerDeathReported) return;
        _playerDeathReported = true;
        PlayerDied?.Invoke();
    }

    public void ShakeCamera(float strength) => _shakeStrength = MathF.Max(_shakeStrength, strength);

    public void Announce(string text)
    {
        if (_announcements.Count < 6) _announcements.Enqueue(text);
    }

    public string? CurrentAnnouncement => _announcementTimer > 0f ? _announcement : null;

    // ------------------------------------------------------------------ Zeichnen
    /// <summary>Lichtkarte befüllen. Läuft vor dem Binden der Leinwand (siehe IScene.PrepareDraw).</summary>
    public void PrepareDraw(SpriteBatch spriteBatch)
    {
        Lighting.Clear();
        Lighting.Add(Player.Center, Player.Stats[StatType.LightRadius], Player.LightColor);
        foreach (Prop prop in _props)
        {
            if (prop.LightRadius <= 0f) continue;
            float flicker = 1f + MathF.Sin(_time * 9f + prop.Center.X) * prop.Definition.LightFlicker;
            Lighting.Add(prop.Center, prop.LightRadius * flicker, prop.LightColor);
        }
        foreach (Projectile projectile in _projectiles) Lighting.Add(projectile.Center, 18f, new Color(255, 200, 150) * 0.7f);
        // Schwaches Glühen um Gegner: In tiefen Kreisen bleiben sie so erkennbar (Fairness trotz Dunkelheit)
        foreach (Enemy enemy in _enemies) Lighting.Add(enemy.Center, enemy.IsBoss || enemy.IsMiniBoss ? 44f : 20f, new Color(130, 50, 50));
        foreach (Pickup pickup in _pickups) Lighting.Add(pickup.Center, pickup.GlowRadius, new Color(200, 255, 230) * 0.6f);
        if (IsGoalActive) Lighting.Add(Layout.GoalBottomCenter - new Vector2(0, 16), 70f, Palette.Faith);
        Lighting.Render(Context.GraphicsDevice, spriteBatch, WorldTransform);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        // 1) Hintergrund im Bildschirmraum mit Parallax (weiter weg = langsamer)
        spriteBatch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);
        DrawParallaxLayer(spriteBatch, _backgroundFar, 0.08f);
        DrawParallaxLayer(spriteBatch, _backgroundMid, 0.3f);
        spriteBatch.End();

        // 2) Beleuchtete Welt
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, transformMatrix: WorldTransform);
        Map.Draw(spriteBatch, _tileset, Camera.VisibleArea, _tileTint, Plan.Circle.Decay, _time);
        foreach (Prop prop in _props) prop.Draw(spriteBatch);
        if (!IsGoalActive) _sigil.Draw(spriteBatch, Layout.GoalBottomCenter, false, Color.White * 0.5f);
        foreach (Pickup pickup in _pickups) pickup.Draw(spriteBatch);
        foreach (Enemy enemy in _enemies) enemy.Draw(spriteBatch);
        foreach (Companion companion in _companions) companion.Draw(spriteBatch);
        Player.Draw(spriteBatch);
        spriteBatch.End();

        // 3) Licht darübermultiplizieren -> Dunkelheit
        Lighting.Composite(spriteBatch);

        // 4) Selbstleuchtendes (unbeeinflusst vom Dunkel): Siegel, Magie, Partikel, Texte, Interaktionshinweis
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, transformMatrix: WorldTransform);
        if (IsGoalActive) _sigil.Draw(spriteBatch, Layout.GoalBottomCenter, false, Color.White);
        foreach (var ability in Player.Abilities) ability.Behavior.Draw(spriteBatch, this, Player, ability);
        foreach (Projectile projectile in _projectiles) projectile.Draw(spriteBatch);
        Effects.Draw(spriteBatch, Context.Assets.Pixel, Context.Font);
        DrawInteractionPrompt(spriteBatch);
        spriteBatch.End();
    }

    private void DrawInteractionPrompt(SpriteBatch spriteBatch)
    {
        if (InteractionTarget is not { } target || string.IsNullOrEmpty(target.Definition.Prompt)) return;
        string text = $"[F] {target.Definition.Prompt}";
        int width = Context.Font.MeasureWidth(text);
        float bob = MathF.Sin(_time * 4f) * 1.5f;
        Context.Font.DrawShadowed(spriteBatch, text, new Vector2(target.Center.X - width / 2f, target.Position.Y - 12 + bob), Palette.Faith);
    }

    private void DrawParallaxLayer(SpriteBatch spriteBatch, Texture2D texture, float factor)
    {
        // Modulo sorgt für nahtlose Wiederholung: zwei Kopien nebeneinander decken jede Verschiebung ab
        float offset = -(Camera.Position.X * factor % texture.Width);
        spriteBatch.Draw(texture, new Vector2(MathF.Round(offset), 0f), _backgroundTint);
        spriteBatch.Draw(texture, new Vector2(MathF.Round(offset) + texture.Width, 0f), _backgroundTint);
    }

    public void Dispose() => Lighting.Dispose();
}

using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.World;

/// <summary>
/// Steuert Arenen: Betritt der Spieler einen Kampfraum, werden dessen Türen versiegelt und Wellen
/// gespawnt (kontinuierlicher Strom wie bei Vampire Survivors, gedeckelt durch MaxAliveEnemies).
/// Nach der letzten Welle öffnen sich die Türen. Im Boss-Raum wird stattdessen der Boss beschworen.
/// </summary>
public sealed class WaveDirector
{
    private readonly DungeonPlan _plan;
    private readonly BalanceDefinition _balance;
    /// <summary>Wellengröße der gewählten Schwierigkeitsstufe (difficulties.json).</summary>
    private readonly float _waveSizeMultiplier;
    private readonly Dictionary<Point, TileType> _sealedTiles = new();
    private RoomNode? _arena;
    private int _waveIndex;
    private int _remainingToSpawn;
    private float _spawnTimer;
    private float _breakTimer;
    /// <summary>
    /// Notausgang-Wächter: Sekunden seit der letzten spürbaren Kampfhandlung (Spawn, Treffer,
    /// Tod). Passiert 45 Sekunden lang nichts, obwohl noch Gegner leben, wird der Kampf
    /// zwangsweise abgeschlossen – lieber ein sichtbarer Log-Eintrag als ein eingesperrter Spieler.
    /// </summary>
    private const float StalemateSeconds = 45f;
    private float _stalemateTimer;
    /// <summary>Gesundheitssumme der Kampfgegner beim letzten Wächter-Tick (Taktgeber des Wächters).</summary>
    private float _lastKnownThreat;

    public WaveDirector(DungeonPlan plan, BalanceDefinition balance, IEnumerable<RoomNode> rooms, float waveSizeMultiplier)
    {
        _plan = plan;
        _balance = balance;
        _waveSizeMultiplier = waveSizeMultiplier;
        TotalArenas = rooms.Count(room => room.Type == RoomType.Arena);
    }

    public int TotalArenas { get; }
    public int ClearedArenas { get; private set; }
    public bool AllArenasCleared => ClearedArenas >= TotalArenas;
    public bool IsFighting => _arena is not null;

    /// <summary>Der gerade versiegelte Kampfraum, sonst null. Gegner dieses Raums bleiben darin eingesperrt.</summary>
    public RoomNode? ActiveArena => _arena;

    public string? StatusText => _arena is { Type: RoomType.Arena }   // Property-Pattern: nicht null UND Type == Arena
        ? $"Welle {Math.Min(_waveIndex + 1, _plan.WavesPerArena)}/{_plan.WavesPerArena}"
        : null;

    public void Update(DungeonWorld world, float deltaSeconds)
    {
        if (_arena is null)
        {
            TryBeginArena(world);
            return;
        }

        // Boss- und Kerkerkämpfe haben keine Wellen: vorbei, wenn alle Gegner tot sind
        if (_arena.Type is RoomType.Boss or RoomType.Prison)
        {
            if (world.AliveEnemyCountOf(_arena.OwnerKey) == 0) CompleteArena(world);
            else CheckStalemate(world, deltaSeconds);
            return;
        }

        if (_breakTimer > 0f)
        {
            _breakTimer -= deltaSeconds;
            if (_breakTimer <= 0f) StartWave();
            return;
        }

        _spawnTimer -= deltaSeconds;
        if (_remainingToSpawn > 0 && _spawnTimer <= 0f && world.AliveEnemyCountOf(_arena.OwnerKey) < _balance.MaxAliveEnemies)
        {
            SpawnWaveEnemy(world);
            _remainingToSpawn--;
            _spawnTimer = _balance.SpawnInterval;
        }

        if (_remainingToSpawn > 0 || world.AliveEnemyCountOf(_arena.OwnerKey) > 0)
        {
            CheckStalemate(world, deltaSeconds);
            return;
        }

        _waveIndex++;
        if (_waveIndex >= _plan.WavesPerArena)
        {
            CompleteArena(world);
            return;
        }
        _breakTimer = _balance.WaveBreakSeconds;
        world.Announce($"Welle {_waveIndex + 1} naht …");
    }

    /// <summary>
    /// Notausgang: Steht der Kampf 45 Sekunden lang still — kein Gegner stirbt, nimmt Schaden
    /// oder wird beschworen —, werden die Verbliebenen entfernt und die Arena abgeschlossen.
    /// Gemessen wird die Gesundheitssumme aller Gegner des Kampfes: Jeder Spawn erhöht sie,
    /// jeder Treffer oder Tod senkt sie. So greift der Wächter nicht mitten in einem
    /// ordentlichen, nur langen Boss-Kampf. Der Log-Eintrag ist bewusst laut – er soll eine
    /// verbleibende Ursache sichtbar machen, nicht still überdecken.
    /// </summary>
    private void CheckStalemate(DungeonWorld world, float deltaSeconds)
    {
        float threat = world.ThreatOf(_arena!.OwnerKey);
        if (MathF.Abs(threat - _lastKnownThreat) > 0.5f)
        {
            _lastKnownThreat = threat;
            _stalemateTimer = 0f;
            return;
        }

        _stalemateTimer += deltaSeconds;
        if (_stalemateTimer < StalemateSeconds) return;

        int alive = world.AliveEnemyCountOf(_arena.OwnerKey);
        int removed = world.RemoveEnemiesOf(_arena.OwnerKey);
        Log.Warn($"NOTAUSGANG: Kampf in Raum {_arena.OwnerKey} ({_arena.Type}) ging {StalemateSeconds:0} s "
               + $"lang nicht vorwärts ({alive} Gegner lebten, niemand nahm Schaden). {removed} Gegner "
               + "wurden entfernt und die Arena abgeschlossen. Bitte melden, wenn das regulär vorkommt.");
        world.Announce("Ein Fluch löst sich – die Tore öffnen sich.");
        _stalemateTimer = 0f;
        _lastKnownThreat = 0f;
        CompleteArena(world);
    }

    private void TryBeginArena(DungeonWorld world)
    {
        RoomNode? room = world.CurrentRoom;
        if (room is null || !room.IsCombatRoom || room.IsCleared) return;

        // Erst starten, wenn der Spieler vollständig im Raum ist -> er wird nie in einer Tür eingemauert
        Rectangle inner = room.PixelBounds;
        inner.Inflate(-28, -16);
        if (!inner.Contains(world.Player.Bounds)) return;

        _arena = room;
        _stalemateTimer = 0f;
        _lastKnownThreat = 0f;
        SealDoors(world.Map, room);
        world.Context.Audio.Play("roar", 0.6f);
        world.ShakeCamera(4f);

        if (room.Type == RoomType.Boss)
        {
            var bossPosition = new Vector2(room.PixelBounds.Center.X + 96, DungeonGenerator.FloorPixelY(room));
            bossPosition = world.SafeSpawnBottomCenter(world.Context.Definitions.Enemies.Get(_plan.Circle.Boss), bossPosition, room);
            world.SpawnEnemy(world.Context.Definitions.Enemies.Get(_plan.Circle.Boss), bossPosition, room.OwnerKey);
            world.Announce(world.Context.Definitions.Enemies.Get(_plan.Circle.Boss).Name);
            return;
        }
        if (room.Type == RoomType.Prison && _plan.Circle.Prison is { } prison)
        {
            SpawnPrisonGuards(world, room, prison);
            return;
        }
        _waveIndex = 0;
        StartWave();
        world.Announce("Die Verdammten erheben sich!");
    }

    /// <summary>Mini-Boss in der Raummitte, Wachen verteilt. Danach öffnen sich die Käfige.</summary>
    private static void SpawnPrisonGuards(DungeonWorld world, RoomNode room, PrisonDefinition prison)
    {
        EnemyDefinition warden = world.Context.Definitions.Enemies.Get(prison.MiniBoss);
        float floorY = DungeonGenerator.FloorPixelY(room);
        Vector2 wardenSpot = world.SafeSpawnBottomCenter(warden, new Vector2(room.PixelBounds.Center.X + 64, floorY), room);
        world.SpawnEnemy(warden, wardenSpot, room.OwnerKey);
        world.Announce($"{warden.Name} bewacht die Gefangenen!");
        if (string.IsNullOrEmpty(prison.Guards)) return;
        EnemyDefinition guard = world.Context.Definitions.Enemies.Get(prison.Guards);
        for (int index = 0; index < prison.GuardCount; index++)
        {
            float x = room.PixelBounds.Left + 60 + index * (room.PixelBounds.Width - 120) / Math.Max(1, prison.GuardCount - 1);
            float y = guard.IsFlying ? room.PixelBounds.Top + 60 : floorY;
            Vector2 spot = world.SafeSpawnBottomCenter(guard, new Vector2(x, y), room);
            world.SpawnEnemy(guard, spot, room.OwnerKey);
        }
    }

    private void StartWave()
    {
        float size = _balance.BaseWaveSize * _plan.DifficultyMultiplier * _waveSizeMultiplier * (1f + _balance.WaveGrowth * _waveIndex);
        // Tiefe-Progression: Kreise mit viel Verfall (decay) spawnen dichter -> es "wimmelt" unten
        size *= 1f + _plan.Circle.Decay * 0.45f;
        _remainingToSpawn = Math.Max(1, (int)MathF.Round(size));
        _spawnTimer = 0.5f;
    }

    private void SpawnWaveEnemy(DungeonWorld world)
    {
        EnemyDefinition definition = PickWeightedEnemy(world);
        Rectangle room = _arena!.PixelBounds;   // "!" = Null-Forgiving: wir wissen, dass _arena hier gesetzt ist
        float floorY = DungeonGenerator.FloorPixelY(_arena);

        Vector2 position = default;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float x = room.Left + 40 + world.Random.NextSingle() * (room.Width - 80);
            float y = definition.IsFlying ? room.Top + 40 + world.Random.NextSingle() * 90f : floorY;
            position = new Vector2(x, y);
            if (MathF.Abs(x - world.Player.Center.X) > 80f) break;   // nicht direkt neben dem Spieler spawnen
        }
        // Fällt die Position in Geometrie, geht es in die Raummitte – ein Gegner im Fels ist unerreichbar.
        position = world.SafeSpawnBottomCenter(definition, position, _arena!);
        world.SpawnEnemy(definition, position, _arena.OwnerKey);
    }

    private EnemyDefinition PickWeightedEnemy(DungeonWorld world)
    {
        List<SpawnWeight> pool = _plan.Circle.EnemyPool;
        int roll = world.Random.Next(pool.Sum(entry => Math.Max(1, entry.Weight)));
        foreach (SpawnWeight entry in pool)
        {
            roll -= Math.Max(1, entry.Weight);
            if (roll < 0) return world.Context.Definitions.Enemies.Get(entry.Enemy);
        }
        return world.Context.Definitions.Enemies.Get(pool[^1].Enemy);
    }

    private void CompleteArena(DungeonWorld world)
    {
        RoomNode room = _arena!;
        UnsealDoors(world.Map);
        room.IsCleared = true;
        if (room.Type == RoomType.Arena) ClearedArenas++;
        _arena = null;
        world.OnArenaCleared(room);
    }

    private void SealDoors(TileMap map, RoomNode room)
    {
        foreach (Point tile in room.DoorTiles)
        {
            _sealedTiles[tile] = map[tile.X, tile.Y];
            map[tile.X, tile.Y] = TileType.Solid;
        }
    }

    private void UnsealDoors(TileMap map)
    {
        foreach (var (tile, original) in _sealedTiles) map[tile.X, tile.Y] = original;
        _sealedTiles.Clear();
    }
}

using CirclesOfAsh.Combat;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Enemies;
using CirclesOfAsh.Entities;

namespace CirclesOfAsh.World;

// =====================================================================================================
// Online-Arena, Modell "Gastgeber rechnet, Gast zeigt": Der Gastgeber simuliert den Kampf allein und
// schickt in jedem Frame einen Schnappschuss (WorldMirrorFrame). Der Gast rechnet keine Spielregeln –
// er spiegelt nur, was ankommt. Das ist robust gegen Rundungsunterschiede zwischen Rechnern: Es gibt
// genau EINE Wahrheit, die des Gastgebers.
//
// Zweierlei wird übertragen:
//  * ZUSTAND (Figuren, Gegner, Geschosse, geänderte Kacheln, entfernte Props): steht in jedem Frame
//    vollständig drin. Geht ein Schnappschuss verloren, korrigiert der nächste alles.
//  * EREIGNISSE (Töne, Effekte, Erschütterung, Ansagen): einmalig, rein für Auge und Ohr. Gehen sie
//    bei Überlast verloren, fehlt nur ein Funke – am Spielstand ändert das nichts.
// =====================================================================================================

/// <summary>
/// Ein einmaliges Ereignis, das der Gast nachspielt. Abstrakter Record + je Art ein eigener Record:
/// Jede Art weiß selbst, wie sie nachgespielt wird (Polymorphie statt switch).
/// </summary>
public abstract record WorldEvent
{
    public abstract void Replay(DungeonWorld world);
}

public sealed record SoundEvent(string SoundId, float Volume, float Pitch) : WorldEvent
{
    public override void Replay(DungeonWorld world) => world.Context.Audio.Play(SoundId, Volume, Pitch);
}

public sealed record BurstEvent(Vector2 Position, Color Color, int Count, float Speed, float Lifetime, float Gravity) : WorldEvent
{
    public override void Replay(DungeonWorld world) => world.Effects.Burst(Position, Color, Count, Speed, Lifetime, Gravity);
}

public sealed record RingEvent(Vector2 Center, float Radius, Color Color, int Count) : WorldEvent
{
    public override void Replay(DungeonWorld world) => world.Effects.Ring(Center, Radius, Color, Count);
}

public sealed record TextEvent(Vector2 Position, string Text, Color Color) : WorldEvent
{
    public override void Replay(DungeonWorld world) => world.Effects.Text(Position, Text, Color);
}

public sealed record SpriteEvent(string SheetId, Vector2 BottomCenter, bool Flip) : WorldEvent
{
    public override void Replay(DungeonWorld world) =>
        world.Effects.PlaySprite(world.Context.Assets.GetSpriteSheet(SheetId), BottomCenter, Flip);
}

public sealed record ShakeEvent(float Strength) : WorldEvent
{
    public override void Replay(DungeonWorld world) => world.ShakeCamera(Strength);
}

public sealed record AnnounceEvent(string Text) : WorldEvent
{
    public override void Replay(DungeonWorld world) => world.Announce(Text);
}

/// <summary>Ja/Nein-Zustände einer Figur als Bits in EINEM Byte ([Flags] erlaubt Kombinationen mit "|").</summary>
[Flags]
public enum PlayerMirrorFlags : byte
{
    None = 0,
    FacingRight = 1,
    Dead = 2,
    Crouching = 4,
    Stealthed = 8,
    Blocking = 16,
    Flashing = 32,
    Dashing = 64,
}

/// <summary>Alles, was der Gast braucht, um eine Figur zu zeigen – und was die Arena-Anzeige liest.</summary>
public sealed record PlayerMirrorState(
    Vector2 Position, PlayerMirrorFlags Flags, string Animation,
    float Health, float MaxHealth, float InvulnerableSeconds, float Mana, float Stamina,
    int ArmorDurability, string ArmorSprite, float ReviveProgress, IReadOnlyList<float> AbilityPhases);

public sealed record EnemyMirrorState(
    int NetworkId, string DefinitionId, Vector2 Position, bool FacingRight, string Animation,
    float SpawnSeconds, bool Flashing, float Health, float MaxHealth, bool IsActiveBoss);

/// <summary>"readonly record struct": kleiner Werttyp ohne Heap-Allokation – davon gibt es viele je Frame.</summary>
public readonly record struct ProjectileMirrorState(int NetworkId, string SheetId, Vector2 Position, Vector2 Velocity);

public readonly record struct PickupMirrorState(int NetworkId, PickupKind Kind, string SheetId, string Animation, Vector2 Position);

public readonly record struct TileMirrorState(int X, int Y, TileType Type);

/// <summary>Ein Frame des Gastgebers: der ganze sichtbare Zustand plus die Ereignisse seit dem letzten.</summary>
public sealed class WorldMirrorFrame
{
    public float ElapsedSeconds { get; init; }
    public List<PlayerMirrorState> Players { get; } = new();
    public List<EnemyMirrorState> Enemies { get; } = new();
    public List<ProjectileMirrorState> Projectiles { get; } = new();
    public List<PickupMirrorState> Pickups { get; } = new();
    /// <summary>Kacheln, die vom erzeugten Stand abweichen (etwa zerbröckelte Plattformen).</summary>
    public List<TileMirrorState> ChangedTiles { get; } = new();
    /// <summary>Netz-Ids aller bisher entfernten Props, die nicht reine Zierde sind (zerschlagene Urnen).</summary>
    public List<int> RemovedProps { get; } = new();
    public List<WorldEvent> Events { get; } = new();
}

/// <summary>Gegner beim Gast denken nicht: Sie stehen dort, wo der Gastgeber sie meldet.</summary>
internal sealed class MirrorBrain : IEnemyBrain
{
    public static MirrorBrain Instance { get; } = new();

    public void Update(Enemy enemy, DungeonWorld world, float deltaSeconds) { }
}

/// <summary>
/// Die Spiegel-Seite der Welt. "partial" verteilt eine Klasse auf mehrere Dateien: Hier steht nur,
/// was die Online-Arena braucht – das Verlies selbst bleibt in DungeonWorld.cs übersichtlich.
/// </summary>
public sealed partial class DungeonWorld
{
    /// <summary>Kachel -> Wert bei der Erzeugung, NUR für Kacheln, die gerade davon abweichen.</summary>
    private readonly Dictionary<Point, TileType> _originalTiles = new();
    /// <summary>Entfernte Props mit Folgen fürs Spiel (Zierde wie Fledermäuse rechnet jeder selbst).</summary>
    private readonly HashSet<int> _removedPropIds = new();
    private int _lastNetworkId;

    /// <summary>
    /// Einmalige Ereignisse dieser Welt (Effekte, Erschütterung, Ansagen). Der Online-Gastgeber
    /// hört mit; ohne Zuhörer kostet das nichts. Töne meldet der AudioService selbst.
    /// </summary>
    public event Action<WorldEvent>? EventRecorded;

    /// <summary>Nächste freie Netz-Id. Props bekommen ihre zuerst – in derselben Reihenfolge wie beim Gast.</summary>
    private int NextNetworkId() => ++_lastNetworkId;   // Prä-Inkrement: erst erhöhen, dann liefern

    private void RecordEvent(WorldEvent worldEvent) => EventRecorded?.Invoke(worldEvent);

    /// <summary>Merkt sich, welche Kacheln vom erzeugten Stand abweichen (siehe <see cref="TileMap.TileChanged"/>).</summary>
    private void OnTileChanged(int x, int y, TileType previous, TileType current)
    {
        var tile = new Point(x, y);
        // TryAdd: nur beim ERSTEN Abweichen den Ursprungswert merken
        if (_originalTiles.TryAdd(tile, previous)) return;
        if (_originalTiles[tile] == current) _originalTiles.Remove(tile);   // wieder wie am Anfang
    }

    // ------------------------------------------------------------------ Gastgeber
    /// <summary>Der ganze sichtbare Zustand dieses Frames. Läuft nach <see cref="Update"/>.</summary>
    public WorldMirrorFrame CaptureMirror(IEnumerable<WorldEvent> events)
    {
        var frame = new WorldMirrorFrame { ElapsedSeconds = _time };
        foreach (Player player in _players) frame.Players.Add(player.CaptureMirror(this));
        foreach (Enemy enemy in _enemies)
        {
            if (enemy.IsRemoved) continue;
            frame.Enemies.Add(new EnemyMirrorState(enemy.NetworkId, enemy.Definition.Id, enemy.Position, enemy.FacingRight,
                enemy.AnimationName, enemy.SpawnSecondsLeft, enemy.IsFlashing, enemy.Health.Current, enemy.Health.Max,
                enemy == ActiveBoss));
        }
        foreach (Projectile projectile in _projectiles)
            if (!projectile.IsRemoved)
                frame.Projectiles.Add(new ProjectileMirrorState(projectile.NetworkId, projectile.SheetId, projectile.Position, projectile.Velocity));
        foreach (Pickup pickup in _pickups)
            if (!pickup.IsRemoved)
                frame.Pickups.Add(new PickupMirrorState(pickup.NetworkId, pickup.Kind, pickup.SheetId, pickup.AnimationName, pickup.Position));
        foreach (Point tile in _originalTiles.Keys) frame.ChangedTiles.Add(new TileMirrorState(tile.X, tile.Y, Map[tile.X, tile.Y]));
        frame.RemovedProps.AddRange(_removedPropIds);
        frame.Events.AddRange(events);
        return frame;
    }

    // ------------------------------------------------------------------ Gast
    /// <summary>Übernimmt den Zustand eines Frames. Ereignisse spielt <see cref="ReplayEvents"/> nach.</summary>
    public void ApplyMirror(WorldMirrorFrame frame)
    {
        _time = frame.ElapsedSeconds;
        for (int index = 0; index < _players.Count && index < frame.Players.Count; index++)
        {
            PlayerMirrorState state = frame.Players[index];
            _players[index].ApplyMirror(state, this);
            if (state.ReviveProgress > 0f) _reviveProgress[_players[index]] = state.ReviveProgress * ReviveSeconds;
            else _reviveProgress.Remove(_players[index]);
        }
        MirrorEnemies(frame.Enemies);
        MirrorProjectiles(frame.Projectiles);
        MirrorPickups(frame.Pickups);
        MirrorTiles(frame.ChangedTiles);
        var removed = new HashSet<int>(frame.RemovedProps);   // HashSet: "enthalten?" in O(1) statt die Liste zu durchsuchen
        foreach (Prop prop in _props)
            if (removed.Contains(prop.NetworkId)) prop.Remove();
    }

    /// <summary>
    /// Netz-Id -> Objekt für schnellen Zugriff. TryAdd statt ToDictionary: Sollte eine Id doch
    /// doppelt vorkommen, gewinnt die erste, statt dass eine Ausnahme den Kampf beendet.
    /// </summary>
    private static Dictionary<int, T> IndexById<T>(IEnumerable<T> entities) where T : Entity
    {
        var index = new Dictionary<int, T>();
        foreach (T entity in entities) index.TryAdd(entity.NetworkId, entity);
        return index;
    }

    /// <summary>Spielt Ereignisse nach – auch die von Frames, deren Zustand schon überholt ist.</summary>
    public void ReplayEvents(IEnumerable<WorldEvent> events)
    {
        foreach (WorldEvent worldEvent in events) worldEvent.Replay(this);
    }

    private void MirrorEnemies(List<EnemyMirrorState> states)
    {
        Dictionary<int, Enemy> known = IndexById(_enemies);
        var seen = new HashSet<int>();
        ActiveBoss = null;
        foreach (EnemyMirrorState state in states)
        {
            if (!seen.Add(state.NetworkId)) continue;   // doppelte Id im selben Frame: nur die erste zählt
            if (!known.TryGetValue(state.NetworkId, out Enemy? enemy))
            {
                // Unbekannter Gegner (andere Spieldaten beim Gastgeber) -> auslassen statt abstürzen
                if (!Context.Definitions.Enemies.TryGet(state.DefinitionId, out EnemyDefinition? definition)) continue;
                enemy = new Enemy(definition, Context.Assets.GetSpriteSheet(definition.SpriteSheet), MirrorBrain.Instance,
                                  Vector2.Zero, 1f, 1f) { NetworkId = state.NetworkId };
                _enemies.Add(enemy);
            }
            enemy.ApplyMirror(state);
            if (state.IsActiveBoss) ActiveBoss = enemy;
        }
        _enemies.RemoveAll(enemy => !seen.Contains(enemy.NetworkId));
    }

    private void MirrorProjectiles(List<ProjectileMirrorState> states)
    {
        Dictionary<int, Projectile> known = IndexById(_projectiles);
        var seen = new HashSet<int>();
        foreach (ProjectileMirrorState state in states)
        {
            if (!seen.Add(state.NetworkId)) continue;   // doppelte Id im selben Frame: nur die erste zählt
            if (!known.TryGetValue(state.NetworkId, out Projectile? projectile))
            {
                // Schaden 0, Lebensdauer "ewig": Der Gastgeber entscheidet, wann es verschwindet.
                projectile = new Projectile(Faction.Enemy, Context.Assets.GetSpriteSheet(state.SheetId), state.Position,
                                            state.Velocity, 0f, 0, float.MaxValue, 0f, collidesWithTiles: false)
                    { NetworkId = state.NetworkId };
                _projectiles.Add(projectile);
            }
            projectile.ApplyMirror(state.Position, state.Velocity);
        }
        _projectiles.RemoveAll(projectile => !seen.Contains(projectile.NetworkId));
    }

    private void MirrorPickups(List<PickupMirrorState> states)
    {
        Dictionary<int, Pickup> known = IndexById(_pickups);
        var seen = new HashSet<int>();
        foreach (PickupMirrorState state in states)
        {
            if (!seen.Add(state.NetworkId)) continue;   // doppelte Id im selben Frame: nur die erste zählt
            if (!known.TryGetValue(state.NetworkId, out Pickup? pickup))
            {
                pickup = new Pickup(state.Kind, Context.Assets.GetSpriteSheet(state.SheetId), state.Position, 0f, Random,
                                    clip: state.Animation, floating: true) { NetworkId = state.NetworkId };
                _pickups.Add(pickup);
            }
            pickup.ApplyMirror(state.Position);
        }
        _pickups.RemoveAll(pickup => !seen.Contains(pickup.NetworkId));
    }

    /// <summary>Setzt geänderte Kacheln; was der Gastgeber nicht mehr meldet, kehrt zum Ursprung zurück.</summary>
    private void MirrorTiles(List<TileMirrorState> changed)
    {
        var reported = new HashSet<Point>(changed.Select(tile => new Point(tile.X, tile.Y)));
        // ToList: Beim Zurücksetzen ändert sich _originalTiles (OnTileChanged) – erst kopieren, dann ändern.
        foreach (var (tile, original) in _originalTiles.ToList())
            if (!reported.Contains(tile)) Map[tile.X, tile.Y] = original;
        foreach (TileMirrorState tile in changed) Map[tile.X, tile.Y] = tile.Type;
    }

    /// <summary>
    /// Ein Frame beim Gast: Animationen und Geschosse laufen bis zum nächsten Schnappschuss weiter,
    /// Effekte und Kamera wie gewohnt. Regeln (Treffer, Tod, KI) laufen NICHT – die kommen vom Gastgeber.
    /// </summary>
    public void UpdateMirror(float deltaSeconds)
    {
        _time += deltaSeconds;
        foreach (Player player in _players) player.AdvanceMirror(deltaSeconds);
        foreach (Enemy enemy in _enemies) enemy.AdvanceMirror(deltaSeconds);
        foreach (Projectile projectile in _projectiles) projectile.AdvanceMirror(deltaSeconds);
        foreach (Pickup pickup in _pickups) pickup.AdvanceMirror(deltaSeconds);
        foreach (Prop prop in _props)
        {
            if (prop.IsRemoved) continue;
            // Zierde (Fledermäuse) rechnet der Gast selbst, alles andere zeigt er nur an.
            if (prop.Behavior.IsCosmetic) prop.Update(this, deltaSeconds);
            else prop.Animation.Update(deltaSeconds);
        }
        _props.RemoveAll(prop => prop.IsRemoved);
        UpdateCurrentRoom();
        Effects.Ambient(Plan.Circle.AmbientParticles, Camera.VisibleArea, deltaSeconds);
        Effects.Update(deltaSeconds);
        UpdateAnnouncements(deltaSeconds);
        UpdateCameraAndShake(deltaSeconds);
    }
}

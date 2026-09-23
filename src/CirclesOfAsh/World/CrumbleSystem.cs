using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.World;

/// <summary>
/// Bröckelnde Plattformen: Betritt der Spieler eine, zerfällt sie nach kurzer Zeit und
/// erscheint später wieder (nur wenn niemand im Weg steht). Je tiefer der Kreis, desto mehr davon.
/// </summary>
public sealed class CrumbleSystem
{
    private readonly Dictionary<Point, float> _breaking = new();
    private readonly Dictionary<Point, float> _respawning = new();
    private readonly float _delay;
    private readonly float _respawnSeconds;

    public CrumbleSystem(BalanceDefinition balance)
    {
        _delay = balance.CrumbleDelay;
        _respawnSeconds = balance.CrumbleRespawnSeconds;
    }

    public void Update(DungeonWorld world, float deltaSeconds)
    {
        TileMap map = world.Map;
        DetectStepping(world, map);

        // ToList(): Kopie, weil wir während der Schleife aus dem Dictionary entfernen
        foreach (var (tile, time) in _breaking.ToList())
        {
            float left = time - deltaSeconds;
            Vector2 center = new((tile.X + 0.5f) * TileMap.TileSize, (tile.Y + 0.5f) * TileMap.TileSize);
            if (world.Random.NextSingle() < 0.3f) world.Effects.Burst(center, Palette.Ash, 1, 20f, 0.4f);
            if (left > 0f)
            {
                _breaking[tile] = left;
                continue;
            }
            _breaking.Remove(tile);
            map[tile.X, tile.Y] = TileType.Empty;
            _respawning[tile] = _respawnSeconds;
            world.Effects.Burst(center, Palette.Ash, 10, 60f, 0.7f);
            world.Context.Audio.Play("crumble", 0.25f, world.Random.NextSingle() * 0.3f);
        }

        foreach (var (tile, time) in _respawning.ToList())
        {
            float left = time - deltaSeconds;
            var area = new Rectangle(tile.X * TileMap.TileSize, tile.Y * TileMap.TileSize, TileMap.TileSize, TileMap.TileSize);
            if (left > 0f || area.Intersects(world.Player.Bounds))
            {
                _respawning[tile] = MathF.Max(left, 0f);
                continue;
            }
            _respawning.Remove(tile);
            map[tile.X, tile.Y] = TileType.Crumbling;
        }
    }

    private void DetectStepping(DungeonWorld world, TileMap map)
    {
        if (!world.Player.OnGround) return;
        Rectangle bounds = world.Player.Bounds;
        int row = TileMap.ToTile(bounds.Bottom + 1);
        for (int column = TileMap.ToTile(bounds.Left); column <= TileMap.ToTile(bounds.Right - 1); column++)
        {
            var tile = new Point(column, row);
            if (map[column, row] == TileType.Crumbling && !_breaking.ContainsKey(tile)) _breaking[tile] = _delay;
        }
    }
}

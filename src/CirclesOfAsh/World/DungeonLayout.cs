using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.World;

public enum RoomType { Start, Corridor, Arena, Treasure, Exit, Boss, Prison, Puzzle }

public enum Direction { Left, Right, Up, Down }

/// <summary>Ein Raum im Metroidvania-Raster. Ein Raum = eine Bildschirmgröße (30x17 Kacheln).</summary>
public sealed class RoomNode
{
    public RoomNode(Point gridPosition, RoomType type)
    {
        GridPosition = gridPosition;
        Type = type;
    }

    public Point GridPosition { get; }
    public RoomType Type { get; set; }
    /// <summary>Optional = nicht nötig zum Abschließen (Umwege, Kerker, Schatzräume).</summary>
    public bool IsOptional { get; set; }
    /// <summary>"Persönlichkeit" des Raums (Form + Deko). null = schlichter Raum.</summary>
    public RoomThemeDefinition? Theme { get; set; }

    /// <summary>Ausgänge dieses Raums. Wert = true, wenn der Durchgang gesperrt ist (rissige Wand, braucht Dash).</summary>
    public Dictionary<Direction, bool> Exits { get; } = new();

    /// <summary>Kacheln der eigenen Durchgänge. Kampfräume versiegeln genau diese.</summary>
    public List<Point> DoorTiles { get; } = new();

    public Rectangle TileBounds { get; set; }
    public Rectangle PixelBounds => new(TileBounds.X * TileMap.TileSize, TileBounds.Y * TileMap.TileSize,
                                         TileBounds.Width * TileMap.TileSize, TileBounds.Height * TileMap.TileSize);

    public bool IsVisited { get; set; }
    public bool IsCleared { get; set; }
    public bool IsCombatRoom => Type is RoomType.Arena or RoomType.Boss or RoomType.Prison;
}

/// <summary>Was der Generator erzeugen soll. Record = unveränderlicher Datencontainer mit Wertgleichheit.</summary>
public sealed record DungeonPlan(
    CircleDefinition Circle,
    int CircleIndex,
    int DungeonIndex,
    bool IsBossDungeon,
    float DifficultyMultiplier,
    int PathLength,
    int ArenaCount,
    int WavesPerArena,
    int TreasureBranches,
    int Seed,
    bool HasPrison,
    string PuzzleKey,
    int LeverCount,
    int DetourCount,
    int ChestCount,
    int CollectibleCount);

/// <summary>Ein platziertes Weltobjekt. Tag/Index verknüpfen es mit Rätseln ("lever", "rune" + Symbol ...).</summary>
public sealed record PropPlacement(PropDefinition Definition, Vector2 BottomCenter, RoomNode Room, string Tag, int Index);

public sealed record CollectiblePlacement(string ItemId, Vector2 Center);

/// <summary>Rätsel vor dem Siegeltor. Order = Lösung (nur bei Runen genutzt).</summary>
public sealed record PuzzleSpec(string Key, IReadOnlyList<int> Order);

/// <summary>
/// Ergebnis des Generators. "required" (C# 11) erzwingt, dass diese Properties beim Erzeugen
/// per Objekt-Initialisierer gesetzt werden -> kein halbfertiges Layout möglich.
/// </summary>
public sealed class DungeonLayout
{
    private Dictionary<Point, RoomNode>? _roomsByGrid;

    public required TileMap Map { get; init; }
    public required IReadOnlyList<RoomNode> Rooms { get; init; }
    public required Point GridSize { get; init; }
    public required Vector2 PlayerSpawn { get; init; }
    public required RoomNode GoalRoom { get; init; }
    public required Vector2 GoalBottomCenter { get; init; }
    public required IReadOnlyList<PropPlacement> Props { get; init; }
    public required IReadOnlyList<CollectiblePlacement> Collectibles { get; init; }
    public required IReadOnlyList<Point> GateTiles { get; init; }
    public PuzzleSpec? Puzzle { get; init; }

    public RoomNode? RoomAtPixel(Vector2 pixel)
    {
        // "??=" weist nur zu, wenn der Wert noch null ist (Lazy Initialization)
        _roomsByGrid ??= Rooms.ToDictionary(room => room.GridPosition);
        var grid = new Point(
            (int)MathF.Floor(pixel.X / (DungeonGenerator.RoomWidthTiles * TileMap.TileSize)),
            (int)MathF.Floor(pixel.Y / (DungeonGenerator.RoomHeightTiles * TileMap.TileSize)));
        return _roomsByGrid.GetValueOrDefault(grid);
    }
}

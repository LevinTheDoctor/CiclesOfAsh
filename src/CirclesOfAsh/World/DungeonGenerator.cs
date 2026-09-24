using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.World;

/// <summary>
/// Prozeduraler Metroidvania-Generator in vier Phasen:
///  1. GRAPH     Kritischer Pfad (Random Walk) + Umwege (alternative Routen) + Schatzräume (Dash-Sperre)
///               + optionaler Kerker mit Gefangenen.
///  2. THEMEN    Jeder Raum bekommt eine "Persönlichkeit" (Krypta, Höhle, Kathedrale, geflutet ...).
///  3. GEOMETRIE Räume in die Kachelkarte fräsen: Türen, Schächte, Höhlendecken, Teiche, bröckelnde Plattformen,
///               Siegeltor um den Ausgang.
///  4. INHALT    Rätsel-Props, Truhen, Käfige, Deko nach Thema, Sammelobjekte.
/// Gleicher Seed = gleicher Dungeon.
/// </summary>
public sealed class DungeonGenerator
{
    public const int RoomWidthTiles = 30;
    public const int RoomHeightTiles = 17;
    private const int GridHeight = 5;
    private const int DoorTopRow = 12;
    private const int DoorBottomRow = 15;
    private const int ShaftLeftColumn = 13;
    private const int ShaftRightColumn = 16;
    private const int FloorRow = RoomHeightTiles - 1;
    private const int TileSize = TileMap.TileSize;

    // Sprunghöhe h = v² / (2g) = 460² / (2 * 1100) ≈ 96 px = 6 Kacheln. Abstände 3-5 Kacheln -> immer Puffer.
    private static readonly (int Row, int Column, int Length)[] ClimbPlatforms =
    {
        (13, 15, 6), (10, 9, 6), (7, 15, 6), (3, 12, 6),
    };

    private static readonly Dictionary<Direction, Point> Offsets = new()
    {
        [Direction.Left] = new Point(-1, 0),
        [Direction.Right] = new Point(1, 0),
        [Direction.Up] = new Point(0, -1),
        [Direction.Down] = new Point(0, 1),
    };

    private readonly DefinitionRegistry _definitions;
    private readonly List<PropPlacement> _props = new();
    // Belegte Spalten pro (Raum, Anker) -> Props überlappen sich nicht. Tupel als Dictionary-Schlüssel.
    private readonly Dictionary<(RoomNode Room, PropAnchor Anchor), HashSet<int>> _usedColumns = new();
    private Random _random = new(0);
    private TileMap _map = null!;   // "null!" = "wird garantiert vor der Nutzung gesetzt" (unterdrückt die Null-Warnung)

    public DungeonGenerator(DefinitionRegistry definitions) => _definitions = definitions;

    public DungeonLayout Generate(DungeonPlan plan)
    {
        _random = new Random(plan.Seed);
        _props.Clear();
        _usedColumns.Clear();

        // ---------- 1. Graph
        int gridWidth = plan.PathLength + 1;
        var rooms = new Dictionary<Point, RoomNode>();
        List<RoomNode> path = BuildCriticalPath(plan, gridWidth, rooms);
        string puzzleKey = AssignRoomTypes(plan, path);
        if (!plan.IsBossDungeon)
        {
            AddDetours(plan, path, rooms, gridWidth);
            AddTreasureBranches(plan, path, rooms, gridWidth);
            if (plan.HasPrison) AddPrison(path, rooms, gridWidth);
        }

        // ---------- 2. Themen
        foreach (RoomNode room in rooms.Values) room.Theme = PickTheme(plan, room);

        // ---------- 3. Geometrie
        _map = new TileMap(gridWidth * RoomWidthTiles, GridHeight * RoomHeightTiles, TileType.Solid);
        foreach (RoomNode room in rooms.Values) CarveRoom(room, plan);
        RoomNode start = path[0];
        RoomNode goal = path[^1];
        List<Point> gateTiles = plan.IsBossDungeon || string.IsNullOrEmpty(puzzleKey) ? new List<Point>() : BuildSigilCage(goal);

        // ---------- 4. Inhalt (Reihenfolge wichtig: Pflicht-Props zuerst, Deko füllt den Rest)
        List<RoomNode> allRooms = rooms.Values.ToList();
        PuzzleSpec? puzzle = gateTiles.Count > 0 ? PlacePuzzle(puzzleKey, path, allRooms, plan.LeverCount) : null;
        if (gateTiles.Count > 0 && puzzle is null)
        {
            foreach (Point tile in gateTiles) _map[tile.X, tile.Y] = TileType.Empty;   // kein Rätsel platzierbar -> Tor offen
            gateTiles.Clear();
        }
        PlaceChests(plan, allRooms);
        PlaceCages(allRooms);
        foreach (RoomNode room in allRooms) Decorate(room);
        List<CollectiblePlacement> collectibles = PlaceCollectibles(plan, allRooms);

        return new DungeonLayout
        {
            Map = _map,
            Rooms = allRooms,
            GridSize = new Point(gridWidth, GridHeight),
            PlayerSpawn = new Vector2((start.TileBounds.X + 3) * TileSize, FloorPixelY(start) - 22),
            GoalRoom = goal,
            GoalBottomCenter = FloorCenter(goal),
            Props = _props.ToList(),
            Collectibles = collectibles,
            GateTiles = gateTiles,
            Puzzle = puzzle,
        };
    }

    public static float FloorPixelY(RoomNode room) => (room.TileBounds.Y + FloorRow) * TileSize;

    private static Vector2 FloorCenter(RoomNode room) =>
        new((room.TileBounds.X + RoomWidthTiles / 2f) * TileSize, FloorPixelY(room));

    // =================================================================== 1. Graph
    private List<RoomNode> BuildCriticalPath(DungeonPlan plan, int gridWidth, Dictionary<Point, RoomNode> rooms)
    {
        var current = new Point(0, _random.Next(1, GridHeight - 1));
        var path = new List<RoomNode> { AddRoom(rooms, current, RoomType.Corridor) };

        for (int step = 1; step < plan.PathLength; step++)
        {
            // Der letzte Schritt geht immer nach rechts: Der Ausgang hat so nur einen seitlichen Eingang
            // und das Siegeltor kann frei in der Raummitte stehen.
            bool isLastStep = step == plan.PathLength - 1;
            var options = new List<(Direction Direction, int Weight)> { (Direction.Right, 3) };
            if (!plan.IsBossDungeon && !isLastStep)
            {
                if (IsFree(rooms, current, Direction.Up, gridWidth)) options.Add((Direction.Up, 1));
                if (IsFree(rooms, current, Direction.Down, gridWidth)) options.Add((Direction.Down, 1));
            }
            Direction direction = PickWeighted(options);
            Point next = current + Offsets[direction];   // Point unterstützt den "+"-Operator
            RoomNode nextRoom = AddRoom(rooms, next, RoomType.Corridor);
            Connect(path[^1], nextRoom, direction, gated: false);
            path.Add(nextRoom);
            current = next;
        }
        return path;
    }

    /// <summary>Setzt Start, Ziel, Arenen und ggf. den Rätselraum. Gibt den tatsächlich nutzbaren Rätsel-Schlüssel zurück.</summary>
    private string AssignRoomTypes(DungeonPlan plan, List<RoomNode> path)
    {
        path[0].Type = RoomType.Start;
        path[^1].Type = plan.IsBossDungeon ? RoomType.Boss : RoomType.Exit;   // "^1" = Index vom Ende (letztes Element)

        int middleCount = path.Count - 2;
        int arenas = Math.Min(plan.ArenaCount, middleCount);
        for (int arena = 0; arena < arenas; arena++)
        {
            int index = 1 + (arena + 1) * middleCount / (arenas + 1);
            path[Math.Clamp(index, 1, path.Count - 2)].Type = RoomType.Arena;
        }

        string key = plan.PuzzleKey;
        if (key is "rune_order" or "braziers")
        {
            RoomNode? puzzleRoom = path.Skip(1).Take(middleCount)
                .Where(room => room.Type == RoomType.Corridor)
                .OrderBy(_ => _random.Next())
                .FirstOrDefault();
            if (puzzleRoom is null) key = "levers";   // kein freier Raum -> auf Hebel ausweichen
            else puzzleRoom.Type = RoomType.Puzzle;
        }
        return key;
    }

    /// <summary>
    /// Umwege: Wo der Pfad drei Räume geradeaus läuft, wird darüber oder darunter eine Parallelroute gebaut.
    /// So hat der Spieler echte Wahl, welchen Weg er nimmt.
    /// </summary>
    private void AddDetours(DungeonPlan plan, List<RoomNode> path, Dictionary<Point, RoomNode> rooms, int gridWidth)
    {
        int added = 0;
        for (int index = 1; index + 2 <= path.Count - 2 && added < plan.DetourCount; index++)
        {
            Point origin = path[index].GridPosition;
            bool isStraightRun = path[index + 1].GridPosition == origin + new Point(1, 0)
                              && path[index + 2].GridPosition == origin + new Point(2, 0);
            if (!isStraightRun || _random.NextSingle() < 0.35f) continue;

            // Array mit zufälliger Reihenfolge: mal zuerst oben, mal zuerst unten probieren
            int[] verticalOffsets = _random.Next(2) == 0 ? new[] { -1, 1 } : new[] { 1, -1 };
            foreach (int offsetY in verticalOffsets)
            {
                Point[] cells = { origin + new Point(0, offsetY), origin + new Point(1, offsetY), origin + new Point(2, offsetY) };
                if (!cells.All(cell => IsInside(cell, gridWidth) && !rooms.ContainsKey(cell))) continue;

                RoomNode[] detour = cells.Select(cell => AddRoom(rooms, cell, RoomType.Corridor)).ToArray();
                foreach (RoomNode room in detour) room.IsOptional = true;
                Direction vertical = offsetY < 0 ? Direction.Up : Direction.Down;
                Connect(path[index], detour[0], vertical, gated: false);
                Connect(detour[0], detour[1], Direction.Right, gated: false);
                Connect(detour[1], detour[2], Direction.Right, gated: false);
                Connect(detour[2], path[index + 2], Opposite(vertical), gated: false);
                added++;
                index += 2;
                break;
            }
        }
    }

    private void AddTreasureBranches(DungeonPlan plan, List<RoomNode> path, Dictionary<Point, RoomNode> rooms, int gridWidth)
    {
        for (int branch = 0; branch < plan.TreasureBranches; branch++)
        {
            RoomNode? added = TryAttachRoom(path.Skip(1).Take(path.Count - 2), rooms, gridWidth,
                new[] { Direction.Up, Direction.Down, Direction.Left }, RoomType.Treasure, gated: true);
            if (added is null) break;
        }
    }

    private void AddPrison(List<RoomNode> path, Dictionary<Point, RoomNode> rooms, int gridWidth)
    {
        IEnumerable<RoomNode> anchors = rooms.Values
            .Where(room => room.Type is RoomType.Corridor or RoomType.Arena && room != path[^1])
            .ToList();   // ToList: Momentaufnahme, weil TryAttachRoom das Dictionary verändert
        TryAttachRoom(anchors, rooms, gridWidth, new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right },
            RoomType.Prison, gated: false);
    }

    /// <summary>Hängt einen neuen Raum an einen zufälligen freien Nachbarplatz. Gibt den neuen Raum oder null zurück.</summary>
    private RoomNode? TryAttachRoom(IEnumerable<RoomNode> anchors, Dictionary<Point, RoomNode> rooms, int gridWidth,
                                    Direction[] directions, RoomType type, bool gated)
    {
        foreach (RoomNode anchor in anchors.OrderBy(_ => _random.Next()).ToList())
        {
            foreach (Direction direction in directions.OrderBy(_ => _random.Next()))
            {
                // Nur freie Seiten: ein Raum darf pro Richtung höchstens einen Ausgang haben
                if (anchor.Exits.ContainsKey(direction) || !IsFree(rooms, anchor.GridPosition, direction, gridWidth)) continue;
                RoomNode room = AddRoom(rooms, anchor.GridPosition + Offsets[direction], type);
                room.IsOptional = true;
                Connect(anchor, room, direction, gated);
                return room;
            }
        }
        return null;
    }

    private static RoomNode AddRoom(Dictionary<Point, RoomNode> rooms, Point position, RoomType type)
    {
        var room = new RoomNode(position, type);
        rooms[position] = room;
        return room;
    }

    private static bool IsInside(Point cell, int gridWidth) => cell.X >= 0 && cell.X < gridWidth && cell.Y >= 0 && cell.Y < GridHeight;

    private static bool IsFree(Dictionary<Point, RoomNode> rooms, Point from, Direction direction, int gridWidth)
    {
        Point target = from + Offsets[direction];
        return IsInside(target, gridWidth) && !rooms.ContainsKey(target);
    }

    private static void Connect(RoomNode from, RoomNode to, Direction direction, bool gated)
    {
        from.Exits[direction] = gated;
        to.Exits[Opposite(direction)] = gated;
    }

    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        Direction.Up => Direction.Down,
        _ => Direction.Up,
    };

    private Direction PickWeighted(List<(Direction Direction, int Weight)> options)
    {
        int roll = _random.Next(options.Sum(option => option.Weight));
        foreach (var (direction, weight) in options)
        {
            if (roll < weight) return direction;
            roll -= weight;
        }
        return options[^1].Direction;
    }

    // =================================================================== 2. Themen
    private RoomThemeDefinition? PickTheme(DungeonPlan plan, RoomNode room)
    {
        // Besondere Räume haben feste Themen – sofern in themes.json vorhanden
        string? fixedTheme = room.Type switch
        {
            RoomType.Boss => "throne",
            RoomType.Exit => "sanctum",
            RoomType.Prison => "prison",
            _ => null,
        };
        if (fixedTheme is not null) return _definitions.Themes.Contains(fixedTheme) ? _definitions.Themes.Get(fixedTheme) : null;

        List<ThemeWeight> weights = plan.Circle.Themes;
        if (weights.Count == 0) return null;
        int roll = _random.Next(weights.Sum(weight => Math.Max(1, weight.Weight)));
        foreach (ThemeWeight weight in weights)
        {
            roll -= Math.Max(1, weight.Weight);
            if (roll < 0) return _definitions.Themes.Get(weight.Theme);
        }
        return null;
    }

    // =================================================================== 3. Geometrie
    private void CarveRoom(RoomNode room, DungeonPlan plan)
    {
        int originX = room.GridPosition.X * RoomWidthTiles;
        int originY = room.GridPosition.Y * RoomHeightTiles;
        room.TileBounds = new Rectangle(originX, originY, RoomWidthTiles, RoomHeightTiles);
        _map.Fill(new Rectangle(originX + 1, originY + 1, RoomWidthTiles - 2, RoomHeightTiles - 2), TileType.Empty);

        foreach (var (direction, gated) in room.Exits) CarveExit(room, direction, gated);

        // Funktionsräume bleiben geometrisch schlicht, damit Rätsel und Kämpfe fair bleiben
        bool allowsShape = room.Type is RoomType.Corridor or RoomType.Treasure or RoomType.Start or RoomType.Arena;
        RoomShape shape = allowsShape ? room.Theme?.Shape ?? RoomShape.Normal : RoomShape.Normal;
        if (shape == RoomShape.Cave) CarveCave(room);
        else if (shape == RoomShape.Flooded) CarvePond(room);

        if (room.Type != RoomType.Exit) AddPlatforms(room, plan.Circle.Decay);
    }

    private void CarveExit(RoomNode room, Direction direction, bool gated)
    {
        int originX = room.TileBounds.X;
        int originY = room.TileBounds.Y;
        TileType opening = gated ? TileType.Cracked : TileType.Empty;

        // Lokale Funktion: hat Zugriff auf "room" und "opening" aus der umgebenden Methode
        void Open(int x, int y)
        {
            _map[x, y] = opening;
            room.DoorTiles.Add(new Point(x, y));
        }

        switch (direction)
        {
            case Direction.Left:
            case Direction.Right:
                int wallX = direction == Direction.Left ? originX : originX + RoomWidthTiles - 1;
                for (int row = DoorTopRow; row <= DoorBottomRow; row++) Open(wallX, originY + row);
                break;
            case Direction.Up:
                for (int column = ShaftLeftColumn; column <= ShaftRightColumn; column++) Open(originX + column, originY);
                foreach (var (row, column, length) in ClimbPlatforms)
                    _map.Fill(new Rectangle(originX + column, originY + row, length, 1), TileType.Platform);
                break;
            case Direction.Down:
                for (int column = ShaftLeftColumn; column <= ShaftRightColumn; column++) Open(originX + column, originY + FloorRow);
                _map.Fill(new Rectangle(originX + ShaftLeftColumn - 2, originY + FloorRow - 1, 8, 1), TileType.Platform);
                break;
        }
    }

    /// <summary>Höhle: zerklüftete Decke und kleine Bodenhügel. Schächte und Türen bleiben frei.</summary>
    private void CarveCave(RoomNode room)
    {
        int originX = room.TileBounds.X;
        int originY = room.TileBounds.Y;
        bool hasUpShaft = room.Exits.ContainsKey(Direction.Up);
        bool hasDownShaft = room.Exits.ContainsKey(Direction.Down);

        int depth = 1;
        for (int column = 1; column < RoomWidthTiles - 1; column++)
        {
            depth = Math.Clamp(depth + _random.Next(-1, 2), 0, 2);   // Random Walk -> natürlich wirkende Decke
            if (hasUpShaft && column is >= ShaftLeftColumn - 2 and <= ShaftRightColumn + 2) continue;
            for (int row = 1; row <= depth; row++) _map[originX + column, originY + row] = TileType.Solid;
        }

        for (int bump = 0; bump < 2; bump++)
        {
            int width = _random.Next(2, 4);
            int column = _random.Next(5, RoomWidthTiles - 5 - width);
            if (hasDownShaft && column + width >= ShaftLeftColumn - 2 && column <= ShaftRightColumn + 2) continue;
            _map.Fill(new Rectangle(originX + column, originY + FloorRow - 1, width, 1), TileType.Solid);
        }
    }

    /// <summary>Geflutet: Teich in der Raummitte mit Uferkanten. Nicht bei Bodenschacht.</summary>
    private void CarvePond(RoomNode room)
    {
        if (room.Exits.ContainsKey(Direction.Down)) return;
        int originX = room.TileBounds.X;
        int originY = room.TileBounds.Y;
        _map.Fill(new Rectangle(originX + 9, originY + FloorRow - 2, 12, 2), TileType.Water);
        _map[originX + 8, originY + FloorRow - 1] = TileType.Solid;
        _map[originX + 21, originY + FloorRow - 1] = TileType.Solid;
    }

    private void AddPlatforms(RoomNode room, float decay)
    {
        int originX = room.TileBounds.X;
        int originY = room.TileBounds.Y;
        bool hasShaft = room.Exits.ContainsKey(Direction.Up) || room.Exits.ContainsKey(Direction.Down);

        var platforms = new List<Rectangle>();
        if (room.Type == RoomType.Boss)
        {
            platforms.Add(new Rectangle(originX + 3, originY + 11, 6, 1));
            platforms.Add(new Rectangle(originX + 21, originY + 11, 6, 1));
        }
        else
        {
            int count = room.Type is RoomType.Arena or RoomType.Prison ? 3 : _random.Next(1, 3);
            for (int index = 0; index < count; index++)
            {
                bool leftSide = index % 2 == 0;
                int length = _random.Next(4, 7);
                int column = leftSide ? _random.Next(2, 5) : _random.Next(22, 28 - length);
                int row = _random.Next(10, 13);
                if (index == 2 && !hasShaft)
                {
                    column = 12;
                    row = 8;
                }
                platforms.Add(new Rectangle(originX + column, originY + row, length, 1));
            }
        }

        foreach (Rectangle platform in platforms)
        {
            // Je tiefer der Kreis (höherer Verfall), desto mehr Plattformen zerbröckeln unter den Füßen.
            // Nur leere Kacheln überschreiben: Höhlendecke und Kletterplattformen bleiben unangetastet.
            TileType type = _random.NextSingle() < decay * 0.8f ? TileType.Crumbling : TileType.Platform;
            for (int x = platform.Left; x < platform.Right; x++)
                if (_map[x, platform.Top] == TileType.Empty) _map[x, platform.Top] = type;
        }
    }

    /// <summary>Käfig aus Torgittern um das Siegel. Öffnet sich, wenn das Rätsel gelöst ist.</summary>
    private List<Point> BuildSigilCage(RoomNode goal)
    {
        int originX = goal.TileBounds.X;
        int originY = goal.TileBounds.Y;
        var gate = new List<Point>();
        for (int row = 11; row <= 15; row++)
        {
            gate.Add(new Point(originX + 12, originY + row));
            gate.Add(new Point(originX + 18, originY + row));
        }
        for (int column = 13; column <= 17; column++) gate.Add(new Point(originX + column, originY + 11));
        foreach (Point tile in gate) _map[tile.X, tile.Y] = TileType.Gate;

        HashSet<int> reserved = UsedColumns(goal, PropAnchor.Floor);
        for (int column = 10; column <= 20; column++) reserved.Add(column);
        return gate;
    }

    // =================================================================== 4. Inhalt
    private PuzzleSpec? PlacePuzzle(string key, List<RoomNode> path, List<RoomNode> rooms, int leverCount)
    {
        // Rätselteile nur in Räumen, die ohne Dash erreichbar und nicht der Ausgang sind
        List<RoomNode> candidates = rooms
            .Where(room => room.Type is not (RoomType.Treasure or RoomType.Exit or RoomType.Prison or RoomType.Boss))
            .ToList();

        switch (key)
        {
            case "levers":
            {
                // Optionale Räume (Umwege) bevorzugen -> Erkundung wird belohnt
                int placed = 0;
                foreach (RoomNode room in candidates.OrderBy(room => room.IsOptional ? 0 : 1).ThenBy(_ => _random.Next()))
                {
                    if (placed >= leverCount) break;
                    if (PlaceProp(room, "lever", PropAnchor.Floor, "lever", placed)) placed++;
                }
                return placed > 0 ? new PuzzleSpec(key, Array.Empty<int>()) : null;
            }
            case "rune_order":
            {
                RoomNode? puzzleRoom = path.FirstOrDefault(room => room.Type == RoomType.Puzzle);
                if (puzzleRoom is null) return PlacePuzzle("levers", path, rooms, leverCount);
                int[] symbols = Enumerable.Range(0, 4).OrderBy(_ => _random.Next()).ToArray();
                int[] columns = { 5, 11, 17, 23 };
                int pillars = 0;
                for (int index = 0; index < symbols.Length; index++)
                    if (PlaceProp(puzzleRoom, "rune_pillar", PropAnchor.Floor, "rune", symbols[index], columns[index])) pillars++;
                if (pillars < symbols.Length) return PlacePuzzle("levers", path, rooms, leverCount);

                // Die Inschrift hängt in einem ANDEREN Raum -> man muss sie erst finden.
                // Any() bricht beim ersten erfolgreichen Platzieren ab.
                bool muralPlaced = candidates.Where(room => room != puzzleRoom).OrderBy(_ => _random.Next())
                    .Any(room => PlaceProp(room, "mural", PropAnchor.Wall, "mural", 0));
                if (!muralPlaced) PlaceProp(puzzleRoom, "mural", PropAnchor.Wall, "mural", 0);
                int[] order = Enumerable.Range(0, 4).OrderBy(_ => _random.Next()).ToArray();
                return new PuzzleSpec(key, order);
            }
            case "braziers":
            {
                RoomNode? puzzleRoom = path.FirstOrDefault(room => room.Type == RoomType.Puzzle);
                if (puzzleRoom is null) return PlacePuzzle("levers", path, rooms, leverCount);
                int[] columns = { 4, 14, 24 };
                int placed = 0;
                for (int index = 0; index < columns.Length; index++)
                    if (PlaceProp(puzzleRoom, "brazier", PropAnchor.Floor, "brazier", index, columns[index])) placed++;
                return placed > 0 ? new PuzzleSpec(key, Array.Empty<int>()) : null;
            }
            default:
                return null;
        }
    }

    private void PlaceChests(DungeonPlan plan, List<RoomNode> rooms)
    {
        foreach (RoomNode treasure in rooms.Where(room => room.Type == RoomType.Treasure))
            PlaceProp(treasure, "chest", PropAnchor.Floor, "chest_treasure", 0, RoomWidthTiles / 2 - 1);

        IEnumerable<RoomNode> normal = rooms.Where(room => room.Type is RoomType.Corridor or RoomType.Start or RoomType.Puzzle)
                                            .OrderBy(_ => _random.Next())
                                            .Take(plan.ChestCount);
        foreach (RoomNode room in normal) PlaceProp(room, "chest", PropAnchor.Floor, "chest", 0);
    }

    private void PlaceCages(List<RoomNode> rooms)
    {
        foreach (RoomNode prison in rooms.Where(room => room.Type == RoomType.Prison))
        {
            int[] columns = { 6, 13, 21 };
            for (int index = 0; index < columns.Length; index++) PlaceProp(prison, "cage", PropAnchor.Floor, "cage", index, columns[index]);
        }
    }

    private void Decorate(RoomNode room)
    {
        if (room.Theme is null) return;
        float density = _definitions.Balance.DecorDensity;
        foreach (ThemePropRule rule in room.Theme.Props)
        {
            int count = _random.Next(rule.Min, Math.Max(rule.Min, rule.Max) + 1);
            // Deko-Regler aus balance.json. Bewusst abrunden: Eine Regel mit max 1 kann dadurch
            // ganz entfallen, was den Raum wirklich leerer macht statt nur die Spitzen zu kappen.
            count = (int)MathF.Floor(count * density);
            for (int index = 0; index < count; index++) PlaceProp(room, rule.Prop, rule.Anchor, "decor", index);
        }
    }

    private List<CollectiblePlacement> PlaceCollectibles(DungeonPlan plan, List<RoomNode> rooms)
    {
        var placements = new List<CollectiblePlacement>();
        if (plan.CollectibleCount <= 0 || plan.Circle.Collectibles.Count == 0) return placements;

        List<RoomNode> candidates = rooms.Where(room => room.Type is not (RoomType.Treasure or RoomType.Exit or RoomType.Boss))
                                         .OrderBy(_ => _random.Next()).ToList();
        for (int index = 0; index < plan.CollectibleCount && candidates.Count > 0; index++)
        {
            RoomNode room = candidates[index % candidates.Count];
            string itemId = plan.Circle.Collectibles[_random.Next(plan.Circle.Collectibles.Count)];
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int column = _random.Next(4, RoomWidthTiles - 4);
                int row = _random.Next(10, 13);   // mit einem Sprung vom Boden erreichbar
                int tileX = room.TileBounds.X + column;
                int tileY = room.TileBounds.Y + row;
                if (_map[tileX, tileY] != TileType.Empty || _map[tileX, tileY + 1] != TileType.Empty) continue;
                placements.Add(new CollectiblePlacement(itemId, new Vector2((tileX + 0.5f) * TileSize, (tileY + 0.5f) * TileSize)));
                break;
            }
        }
        return placements;
    }

    // ------------------------------------------------------------------- Platzierungshilfen
    private HashSet<int> UsedColumns(RoomNode room, PropAnchor anchor)
    {
        if (!_usedColumns.TryGetValue((room, anchor), out HashSet<int>? used))
        {
            used = new HashSet<int>();
            _usedColumns[(room, anchor)] = used;
        }
        return used;
    }

    private bool PlaceProp(RoomNode room, string propId, PropAnchor anchor, string tag, int index, int? preferredColumn = null)
    {
        if (!_definitions.Props.Contains(propId)) return false;
        PropDefinition prop = _definitions.Props.Get(propId);
        Vector2? spot = FindSpot(room, prop, anchor, preferredColumn);
        if (spot is not { } bottomCenter) return false;   // Pattern: "nicht null" + Wert in bottomCenter entpacken
        _props.Add(new PropPlacement(prop, bottomCenter, room, tag, index));
        return true;
    }

    /// <summary>
    /// Sucht einen freien Platz für ein Prop:
    ///   Floor   - auf dem Boden (auch auf Hügeln), nie im Wasser oder auf Schacht-Plattformen
    ///   Ceiling - hängend unter der (evtl. zerklüfteten) Decke
    ///   Wall    - frei schwebend vor der Hintergrundmauer
    /// </summary>
    private Vector2? FindSpot(RoomNode room, PropDefinition prop, PropAnchor anchor, int? preferredColumn)
    {
        int originX = room.TileBounds.X;
        int originY = room.TileBounds.Y;
        int widthTiles = Math.Max(1, (int)MathF.Ceiling(prop.Width / (float)TileSize));
        int heightTiles = Math.Max(1, (int)MathF.Ceiling(prop.Height / (float)TileSize));
        HashSet<int> used = UsedColumns(room, anchor);

        for (int attempt = 0; attempt < 24; attempt++)
        {
            int column = preferredColumn is int fixedColumn && attempt == 0
                ? fixedColumn
                : _random.Next(3, RoomWidthTiles - 3 - widthTiles + 1);
            if (Enumerable.Range(column, widthTiles).Any(used.Contains)) continue;

            int? bottomRow = anchor switch
            {
                PropAnchor.Floor => FloorRowAt(originX + column, originY, widthTiles),
                PropAnchor.Ceiling => CeilingRowAt(originX + column, originY, widthTiles) is int top ? top + heightTiles : null,
                _ => _random.Next(Math.Min(5 + heightTiles, 10), 11),   // Math.Min verhindert min > max
            };
            if (bottomRow is not int row) continue;
            if (!IsAreaEmpty(originX + column, originY + row - heightTiles, widthTiles, heightTiles)) continue;

            for (int blocked = column - 1; blocked <= column + widthTiles; blocked++) used.Add(blocked);
            float x = (originX + column + widthTiles / 2f) * TileSize;
            float y = anchor == PropAnchor.Ceiling
                ? (originY + row - heightTiles) * TileSize + prop.Height   // hängt direkt an der Decke
                : (originY + row) * TileSize;
            return new Vector2(x, y);
        }
        return null;
    }

    /// <summary>Relative Zeile des Bodens unter der Spalte (erste massive Zeile unter einer leeren) oder null.</summary>
    private int? FloorRowAt(int tileX, int originY, int widthTiles)
    {
        for (int row = FloorRow - 1; row >= FloorRow - 3; row--)
        {
            bool free = true, grounded = true;
            for (int x = tileX; x < tileX + widthTiles; x++)
            {
                free &= _map[x, originY + row] == TileType.Empty;           // "&=" = UND-Zuweisung
                grounded &= TileMap.IsBlocking(_map[x, originY + row + 1]);
            }
            if (free && grounded) return row + 1;
        }
        return null;
    }

    /// <summary>Relative Zeile der ersten freien Reihe unter der Decke oder null.</summary>
    private int? CeilingRowAt(int tileX, int originY, int widthTiles)
    {
        for (int row = 1; row <= 6; row++)
        {
            bool free = true, hanging = true;
            for (int x = tileX; x < tileX + widthTiles; x++)
            {
                free &= _map[x, originY + row] == TileType.Empty;
                hanging &= TileMap.IsBlocking(_map[x, originY + row - 1]);
            }
            if (free && hanging) return row;
        }
        return null;
    }

    private bool IsAreaEmpty(int tileX, int tileY, int width, int height)
    {
        for (int y = tileY; y < tileY + height; y++)
            for (int x = tileX; x < tileX + width; x++)
                if (_map[x, y] != TileType.Empty) return false;
        return true;
    }
}

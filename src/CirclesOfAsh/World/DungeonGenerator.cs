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
        AssignRoomTypes(plan, path);
        // Vor den Abzweigen: einen Raum mit heilem Boden für das Rätsel vormerken (siehe
        // RoomNode.KeepFloorIntact). Danach ist regelmässig keiner mehr übrig.
        ReservePuzzleRoom(plan, path);
        if (!plan.IsBossDungeon)
        {
            AddDetours(plan, path, rooms, gridWidth);
            AddTreasureBranches(plan, path, rooms, gridWidth);
            if (plan.HasPrison) AddPrison(path, rooms, gridWidth);
        }
        // Erst jetzt, wenn alle Ausgaenge feststehen (siehe AssignPuzzleRoom).
        string puzzleKey = AssignPuzzleRoom(plan, path);

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
        PuzzleSpec? puzzle = gateTiles.Count > 0 ? PlacePuzzle(puzzleKey, plan, path, allRooms) : null;
        if (gateTiles.Count > 0 && puzzle is null)
        {
            foreach (Point tile in gateTiles) _map[tile.X, tile.Y] = TileType.Empty;   // kein Rätsel platzierbar -> Tor offen
            gateTiles.Clear();
        }
        PlaceChests(plan, allRooms);
        PlaceMendShrine(plan, allRooms);
        PlaceCages(allRooms);
        PlaceArenaProps(plan, allRooms);
        foreach (RoomNode room in allRooms) Decorate(room);
        List<CollectiblePlacement> collectibles = PlaceCollectibles(plan, allRooms);

        return new DungeonLayout
        {
            Map = _map,
            Rooms = allRooms,
            GridSize = new Point(gridWidth, GridHeight),
            // Mitte der Fuesse, nicht die linke obere Ecke: Die Figurhoehe haengt an der
            // Sprite-Groesse, der Generator kennt sie nicht.
            PlayerSpawn = PlayerSpawnOf(start),
            GoalRoom = goal,
            GoalBottomCenter = FloorCenter(goal),
            Props = _props.ToList(),
            Collectibles = collectibles,
            GateTiles = gateTiles,
            Puzzle = puzzle,
        };
    }

    public static float FloorPixelY(RoomNode room) => (room.TileBounds.Y + FloorRow) * TileSize;

    /// <summary>Wo der Spieler startet. Eine Formel, zwei Nutzer: das Layout und die Engelsprobe.</summary>
    private static Vector2 PlayerSpawnOf(RoomNode start) =>
        new((start.TileBounds.X + 3) * TileSize + TileSize / 2f, FloorPixelY(start));

    private static Vector2 PlayerSpawnOf(List<RoomNode> rooms) =>
        PlayerSpawnOf(rooms.First(room => room.Type == RoomType.Start));

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

    /// <summary>
    /// Rätsel, die einen eigenen Raum brauchen, weil ihre Teile in fester Geometrie zueinander
    /// stehen müssen. "levers" steht bewusst nicht drin: seine Hebel verteilen sich über das
    /// ganze Verlies – deshalb ist es auch der Rückfall, wenn kein freier Raum übrig ist.
    /// </summary>
    /// <summary>Ausweichspalten fuer Arena-Deko: erst die Wunschspalte, dann abwechselnd daneben.</summary>
    private static readonly int[] NearbyOffsets = { 0, -1, 1, -2, 2, -3, 3 };

    private static readonly HashSet<string> NeedsPuzzleRoom =
        new(StringComparer.OrdinalIgnoreCase) { "rune_order", "rune_circle", "braziers", "weights", "mirrors" };

    /// <summary>Setzt Start, Ziel und Arenen. Der Rätselraum kommt später (siehe <see cref="AssignPuzzleRoom"/>).</summary>
    private void AssignRoomTypes(DungeonPlan plan, List<RoomNode> path)
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

    }

    /// <summary>
    /// Wählt den Rätselraum – und zwar ERST, nachdem Umwege, Schatzabzweige und Kerker stehen.
    /// Die hängen nämlich weitere Ausgänge an bestehende Räume: Ein Raum, der bei der Typvergabe
    /// noch schachtfrei war, kann danach einen bekommen haben.
    ///
    /// Ausgeschlossen sind nur Ausgänge nach UNTEN. Die öffnen die Bodenreihe auf vier Kacheln
    /// (<see cref="CarveExit"/>), und genau auf dieser Reihe stellen die Raumrätsel ihre Teile in
    /// fester Geometrie auf: Druckplatten würden über dem Loch schweben, Schiebeblöcke
    /// hindurchfallen. Ein Ausgang nach oben öffnet die Decke und stört nicht.
    ///
    /// Gibt den tatsächlich nutzbaren Rätsel-Schlüssel zurück.
    /// </summary>
    private string AssignPuzzleRoom(DungeonPlan plan, List<RoomNode> path)
    {
        string key = plan.PuzzleKey;
        if (!NeedsPuzzleRoom.Contains(key)) return key;

        RoomNode? puzzleRoom = PuzzleRoomCandidates(path)
            .Where(room => !room.Exits.ContainsKey(Direction.Down))
            .OrderByDescending(room => room.KeepFloorIntact)   // der vorgemerkte Raum zuerst
            .ThenBy(_ => _random.Next())
            .FirstOrDefault();
        if (puzzleRoom is null) return "levers";   // kein freier Raum -> auf Hebel ausweichen
        puzzleRoom.Type = RoomType.Puzzle;
        return key;
    }

    /// <summary>
    /// Merkt einen Mittelraum als künftigen Rätselraum vor, BEVOR Umwege, Schatz- und
    /// Kerkerabzweige Ausgänge anhängen. <see cref="AddDetours"/> und <see cref="TryAttachRoom"/>
    /// hängen sich danach nicht mehr nach unten an ihn, sein Boden bleibt also heil.
    /// </summary>
    private void ReservePuzzleRoom(DungeonPlan plan, List<RoomNode> path)
    {
        if (plan.IsBossDungeon || !NeedsPuzzleRoom.Contains(plan.PuzzleKey)) return;
        // Schon der kritische Pfad steigt ab: Wo er nach unten geht, hat der Raum bereits einen
        // Schacht in der Bodenreihe. Vorgemerkt wird deshalb nur ein Raum, dessen Boden noch heil
        // ist - sonst schuetzt die Vormerkung einen Boden, der ohnehin schon ein Loch hat.
        RoomNode? reserved = PuzzleRoomCandidates(path)
            .Where(room => !room.Exits.ContainsKey(Direction.Down))
            .OrderBy(_ => _random.Next())
            .FirstOrDefault();
        if (reserved is not null) reserved.KeepFloorIntact = true;
    }

    /// <summary>Mittelräume, in denen ein Raumrätsel stehen könnte: Korridore zwischen Start und Ziel.</summary>
    private static IEnumerable<RoomNode> PuzzleRoomCandidates(List<RoomNode> path) =>
        path.Skip(1).Take(Math.Max(0, path.Count - 2)).Where(room => room.Type == RoomType.Corridor);

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
                // Ein Umweg nach unten reisst bei BEIDEN Anschlussräumen ein Loch in die Bodenreihe.
                // Ist einer davon als Rätselraum vorgemerkt, wird diese Richtung übersprungen.
                if (offsetY > 0 && (path[index].KeepFloorIntact || path[index + 2].KeepFloorIntact)) continue;
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
                // Der vorgemerkte Rätselraum behält seinen Boden – nach oben oder zur Seite gern.
                if (direction == Direction.Down && anchor.KeepFloorIntact) continue;
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

        // Eine eigene Arena ersetzt die Standardplattformen komplett - sonst laegen fremde
        // Absaetze quer durch eine bewusst gesetzte Geometrie.
        if (ArenaFor(plan, room) is { } arena) BuildArena(room, arena);
        else if (room.Type != RoomType.Exit) AddPlatforms(room, plan.Circle.Decay);
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

    /// <summary>
    /// Die Arena, die zu diesem Raum gehoert - oder null. Der Thronsaal traegt den Boss des
    /// Kreises, der Kerker seinen Mini-Boss. Alle anderen Raeume bleiben Standardraeume.
    /// </summary>
    private ArenaDefinition? ArenaFor(DungeonPlan plan, RoomNode room)
    {
        string enemyId = room.Type switch
        {
            RoomType.Boss => plan.BossEnemyId,
            RoomType.Prison => plan.Circle.Prison?.MiniBoss ?? "",
            _ => "",
        };
        return enemyId.Length > 0 && _definitions.Arenas.TryGet(enemyId, out ArenaDefinition? arena) ? arena : null;
    }

    /// <summary>
    /// Baut Absaetze und Saeulen einer Arena. Die Props kommen spaeter (PlaceArenaProps), weil in
    /// dieser Phase die Prop-Liste noch gar nicht gefuellt wird.
    /// </summary>
    private void BuildArena(RoomNode room, ArenaDefinition arena)
    {
        int originX = room.TileBounds.X;
        int originY = room.TileBounds.Y;

        foreach (ArenaPlatform platform in arena.Platforms)
        {
            for (int offset = 0; offset < Math.Max(1, platform.Length); offset++)
            {
                int x = originX + platform.Column + offset;
                int y = originY + platform.Row;
                // Nur leere Kacheln ueberschreiben: Tueroeffnungen und Aussenwand bleiben heil.
                if (_map.IsInside(x, y) && _map[x, y] == TileType.Empty) _map[x, y] = TileType.Platform;
            }
        }
        foreach (ArenaPillar pillar in arena.Pillars)
        {
            for (int offset = 0; offset < Math.Max(1, pillar.Height); offset++)
            {
                int x = originX + pillar.Column;
                int y = originY + pillar.Row + offset;
                if (_map.IsInside(x, y) && _map[x, y] == TileType.Empty) _map[x, y] = TileType.Solid;
            }
        }
    }

    /// <summary>
    /// Deko der Arena an FESTEN Spalten. Bewusst ueber PlacePropAt statt PlaceProp: Letzteres
    /// weicht auf eine zufaellige Spalte aus, wenn die gewuenschte belegt ist - bei einer
    /// entworfenen Arena landet die Deko dann irgendwo und der Entwurf ist hin.
    ///
    /// Belegte Spalten werden uebersprungen statt verschoben: Im Kerker stehen die Kaefige schon,
    /// und die gehoeren zum Spiel, die Arena-Deko nicht.
    /// Laeuft vor der Zufallsdeko, die dann ihrerseits ausweicht.
    /// </summary>
    private void PlaceArenaProps(DungeonPlan plan, IEnumerable<RoomNode> rooms)
    {
        foreach (RoomNode room in rooms)
        {
            if (ArenaFor(plan, room) is not { } arena) continue;
            HashSet<int> used = UsedColumns(room, PropAnchor.Floor);
            int index = 0;
            foreach (ArenaProp prop in arena.Props)
            {
                // Ist die Wunschspalte vergeben (im Kerker stehen die Kaefige schon) oder fehlt
                // dort der Boden, ruecken wir bis zu zwei Kacheln zur Seite. Ganz wegzulassen
                // waere schlechter: Dann fehlte in manchen Laeufen ohne sichtbaren Grund eine
                // Fackel, und die Arena saehe jedes Mal anders leer aus.
                foreach (int offset in NearbyOffsets)
                {
                    int column = prop.Column + offset;
                    if (column < 2 || column > RoomWidthTiles - 3 || used.Contains(column)) continue;
                    if (!PlacePropAt(room, prop.Prop, "decor", index, column, FloorRow)) continue;
                    index++;
                    break;
                }
            }
        }
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
    /// <summary>
    /// Stellt die Teile eines Raetsels auf. Schlaegt das fehl, weicht der Generator auf "levers"
    /// aus - und dabei muss ALLES verschwinden, was das verworfene Raetsel schon gesetzt hat.
    /// Sonst bleiben Spiegel stehen, an denen man drehen kann, ohne dass etwas passiert, und
    /// Druckplatten, die nie zaehlen. Deshalb laeuft jeder Versuch ueber diesen Wrapper: Er merkt
    /// sich den Stand von _props und schneidet bei Misserfolg darauf zurueck.
    /// </summary>
    private PuzzleSpec? PlacePuzzle(string key, DungeonPlan plan, List<RoomNode> path, List<RoomNode> rooms)
    {
        int mark = _props.Count;
        PuzzleSpec? placed = PlacePuzzleCore(key, plan, path, rooms, mark);
        if (placed is null) RollbackProps(mark);
        return placed;
    }

    /// <summary>Nimmt alle Props zurueck, die seit <paramref name="mark"/> gesetzt wurden.</summary>
    private void RollbackProps(int mark)
    {
        for (int index = _props.Count - 1; index >= mark; index--)
        {
            PropPlacement placement = _props[index];
            // Die Spalte wieder freigeben, sonst meidet die Deko spaeter einen Platz ohne Grund.
            // Ueber alle Anker, weil PropPlacement den Anker nicht mitfuehrt - eine Inschrift haengt
            // an der Wand, Platten und Spiegel stehen auf dem Boden.
            int column = (int)(placement.BottomCenter.X / TileSize) - placement.Room.TileBounds.X;
            foreach (PropAnchor anchor in Enum.GetValues<PropAnchor>()) UsedColumns(placement.Room, anchor).Remove(column);
            _props.RemoveAt(index);
        }
    }

    private PuzzleSpec? PlacePuzzleCore(string key, DungeonPlan plan, List<RoomNode> path, List<RoomNode> rooms, int mark)
    {
        // Wie stark das Rätsel in diesem Kreis anzieht (balance.json, "puzzleScaling").
        PuzzleScalingDefinition scaling = _definitions.Balance.PuzzleScaling;
        int circle = plan.CircleIndex;

        // Rätselteile nur in Räumen, die ohne Dash erreichbar und nicht der Ausgang sind
        List<RoomNode> candidates = rooms
            .Where(room => room.Type is not (RoomType.Treasure or RoomType.Exit or RoomType.Prison or RoomType.Boss))
            .ToList();

        switch (key)
        {
            case "levers":
            {
                // Optionale Räume (Umwege) bevorzugen -> Erkundung wird belohnt
                int leverCount = scaling.LeverCountAt(plan.LeverCount, circle);
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
                if (puzzleRoom is null) return FallBackToLevers(plan, path, rooms, mark);
                int steps = Math.Clamp(scaling.RuneOrderLength, 2, 4);   // 4 = Zellen in runes.png
                int[] symbols = Enumerable.Range(0, steps).OrderBy(_ => _random.Next()).ToArray();
                int[] columns = SpreadColumns(steps, 5, 23);
                int pillars = 0;
                for (int index = 0; index < symbols.Length; index++)
                    if (PlaceProp(puzzleRoom, "rune_pillar", PropAnchor.Floor, "rune", symbols[index], columns[index])) pillars++;
                if (pillars < symbols.Length) return FallBackToLevers(plan, path, rooms, mark);

                // Die Inschrift hängt in einem ANDEREN Raum -> man muss sie erst finden.
                // Any() bricht beim ersten erfolgreichen Platzieren ab.
                bool muralPlaced = candidates.Where(room => room != puzzleRoom).OrderBy(_ => _random.Next())
                    .Any(room => PlaceProp(room, "mural", PropAnchor.Wall, "mural", 0));
                if (!muralPlaced) PlaceProp(puzzleRoom, "mural", PropAnchor.Wall, "mural", 0);
                int[] order = Enumerable.Range(0, steps).OrderBy(_ => _random.Next()).ToArray();
                return new PuzzleSpec(key, order);
            }
            case "rune_circle":
            {
                RoomNode? puzzleRoom = path.FirstOrDefault(room => room.Type == RoomType.Puzzle);
                if (puzzleRoom is null) return FallBackToLevers(plan, path, rooms, mark);

                // Vier Saeulen, jede mit eigenem Symbol - mehr Runen hat runes.png nicht.
                const int ringSize = 4;
                int[] ringColumns = SpreadColumns(ringSize, 5, 23);
                int pillars = 0;
                for (int index = 0; index < ringSize; index++)
                    if (PlaceProp(puzzleRoom, "rune_pillar", PropAnchor.Floor, "rune", index, ringColumns[index])) pillars++;
                if (pillars < ringSize) return FallBackToLevers(plan, path, rooms, mark);

                // Die Startstellung entsteht RUECKWAERTS aus der geloesten: Von "alle leuchten" aus
                // werden ein paar zufaellige Zuege angewandt. Damit ist sie garantiert loesbar, und
                // zwar in hoechstens so vielen Zuegen, wie hier gewuerfelt wurden.
                var lit = new bool[ringSize];
                Array.Fill(lit, true);
                int shuffles = 2 + _random.Next(ringSize);
                for (int move = 0; move < shuffles; move++)
                {
                    int touched = _random.Next(ringSize);
                    for (int offset = -1; offset <= 1; offset++)
                    {
                        int at = (touched + offset + ringSize) % ringSize;
                        lit[at] = !lit[at];
                    }
                }
                // Alles-leuchtet waere schon geloest - dann einmal von Hand verdrehen.
                if (lit.All(on => on))
                    for (int offset = -1; offset <= 1; offset++) lit[(offset + ringSize) % ringSize] = false;

                return new PuzzleSpec(key, lit.Select(on => on ? 1 : 0).ToArray());
            }
            case "braziers":
            {
                RoomNode? puzzleRoom = path.FirstOrDefault(room => room.Type == RoomType.Puzzle);
                if (puzzleRoom is null) return FallBackToLevers(plan, path, rooms, mark);
                int[] columns = { 4, 14, 24 };
                int placed = 0;
                for (int index = 0; index < columns.Length; index++)
                    if (PlaceProp(puzzleRoom, "brazier", PropAnchor.Floor, "brazier", index, columns[index])) placed++;
                return placed > 0 ? new PuzzleSpec(key, Array.Empty<int>()) : null;
            }
            case "weights":
            {
                RoomNode? puzzleRoom = path.FirstOrDefault(room => room.Type == RoomType.Puzzle);
                if (puzzleRoom is null) return FallBackToLevers(plan, path, rooms, mark);

                // Eine Platte mehr als Bloecke: Auf der letzten muss der Spieler selbst stehen
                // bleiben – sonst waere es nur Hin- und Herlaufen. Wie viele es sind, haengt am
                // Kreis (balance.json, "weightPlates").
                int plateCount = scaling.WeightPlatesAt(circle);
                int[] plateColumns = SpreadColumns(plateCount, 6, 22);
                int plates = 0;
                for (int index = 0; index < plateColumns.Length; index++)
                    if (PlacePropAt(puzzleRoom, "pressure_plate", "plate", index, plateColumns[index], FloorRow)) plates++;
                if (plates < plateColumns.Length) return FallBackToLevers(plan, path, rooms, mark);

                // Bloecke bewusst NICHT auf den Platten: sie stehen genau dazwischen und muessen
                // geschoben werden. Der Block rutscht je Druck eine Kachel weiter.
                int[] blockColumns = new int[plateCount - 1];
                for (int index = 0; index < blockColumns.Length; index++)
                    blockColumns[index] = (plateColumns[index] + plateColumns[index + 1]) / 2;
                int blocks = 0;
                for (int index = 0; index < blockColumns.Length; index++)
                    if (PlacePropAt(puzzleRoom, "push_block", "block", index, blockColumns[index], FloorRow)) blocks++;
                // Ohne beide Bloecke ist eine Platte zu viel und das Raetsel unloesbar.
                if (blocks < blockColumns.Length) return FallBackToLevers(plan, path, rooms, mark);
                return new PuzzleSpec(key, Array.Empty<int>());
            }
            case "mirrors":
            {
                RoomNode? puzzleRoom = path.FirstOrDefault(room => room.Type == RoomType.Puzzle);
                if (puzzleRoom is null) return FallBackToLevers(plan, path, rooms, mark);

                // Der gedachte Weg: Der Leuchter strahlt am Boden nach rechts, ZWEI benachbarte
                // drehbare Spiegel heben ihn an die Wand und wieder herunter, der dritte liegt auf
                // einem waagerechten Stueck und muss flach gestellt werden. Zwei feste Spiegel oben
                // zeigen den Weg (ausserhalb der Sprungweite, damit niemand sie verdreht), und ein
                // fester Sperrspiegel steht quer in genau dem Stueck Bodenreihe, das der Weg
                // ueberspringt - ohne ihn koennte man alles flach stellen und der Strahl liefe
                // einfach durch.
                //
                // NEU: Welches Paar hebt, und in welchen Spalten das steht, wird gewuerfelt. Vorher
                // war jedes Spiegelraetsel im Spiel dasselbe - gleiche Spalten, gleiche Loesung,
                // neunmal je Lauf. Ob die gewuerfelte Fassung GENAU EINE Loesung hat, rechnet der
                // Generator selbst nach (BeamTracer), statt es zu behaupten; gelingt das in keinem
                // Versuch, kommt die bewaehrte feste Fassung zum Zug.
                for (int attempt = 0; attempt <= MirrorAttempts; attempt++)
                {
                    int before = _props.Count;
                    PuzzleSpec? built = TryPlaceMirrors(puzzleRoom, useFixedFallback: attempt == MirrorAttempts);
                    if (built is not null) return built;
                    RollbackProps(before);
                }
                return FallBackToLevers(plan, path, rooms, mark);
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Rueckfall auf die Hebelsuche. Nimmt die Teile des verworfenen Raetsels zurueck und macht den
    /// vorgemerkten Raetselraum wieder zum gewoehnlichen Korridor: Sonst verlangt die
    /// Erreichbarkeitspruefung einen Pflichtraum, in dem nichts steht, und die Minikarte faerbt ihn
    /// als Raetselraum ein.
    /// </summary>
    private PuzzleSpec? FallBackToLevers(DungeonPlan plan, List<RoomNode> path, List<RoomNode> rooms, int mark)
    {
        RollbackProps(mark);
        foreach (RoomNode room in path.Where(room => room.Type == RoomType.Puzzle)) room.Type = RoomType.Corridor;
        return PlacePuzzleCore("levers", plan, path, rooms, _props.Count);
    }

    /// <summary>
    /// Verteilt <paramref name="count"/> Spalten gleichmaessig zwischen zwei Raendern. Ersetzt die
    /// fest eingetragenen Spaltenlisten, damit die Teilezahl mit der Tiefe wachsen kann, ohne dass
    /// jemand eine zweite Liste nachpflegen muss.
    /// </summary>
    private static int[] SpreadColumns(int count, int first, int last)
    {
        if (count <= 1) return new[] { (first + last) / 2 };
        var columns = new int[count];
        for (int index = 0; index < count; index++)
            columns[index] = first + (int)MathF.Round((last - first) * index / (float)(count - 1));
        return columns;
    }

    /// <summary>
    /// Der Trauernde Engel: genau EINER je Verlies, und nur in den Wellenverliesen - im Thronsaal
    /// waere er eine Rettung mitten im Bosskampf. Bevorzugt in einem optionalen Raum, damit der
    /// Umweg sich lohnt; findet sich dort keiner, geht auch ein Raum am Weg.
    /// Mehr als einer waere zu viel: Der Verfall soll spuerbar bleiben.
    ///
    /// SCHATZRAEUME sind ausgenommen. Sie liegen hinter rissigen Waenden, die erst der Dash oeffnet
    /// - eine Flickstelle, die man nur mit einer Bossgabe erreicht, hilft genau dann nicht, wenn man
    /// sie braucht. Und weil auch ein Umweg mal hinter einer Sperre enden kann, wird der Platz
    /// gegen dieselbe Flutfuellung geprueft, die auch die Erreichbarkeitspruefung benutzt: Ein Engel,
    /// den man sieht und nicht erreicht, waere schlimmer als gar keiner.
    /// </summary>
    private void PlaceMendShrine(DungeonPlan plan, List<RoomNode> rooms)
    {
        if (plan.IsBossDungeon) return;
        bool[] reachable = DungeonReachability.Flood(_map, PlayerSpawnOf(rooms));
        IEnumerable<RoomNode> candidates = rooms
            .Where(room => room.Type is RoomType.Corridor or RoomType.Puzzle or RoomType.Start)
            .OrderBy(room => room.IsOptional ? 0 : 1)
            .ThenBy(_ => _random.Next());
        foreach (RoomNode room in candidates)
        {
            int before = _props.Count;
            if (!PlaceProp(room, "mending_angel", PropAnchor.Floor, "mend_shrine", 0)) continue;
            if (DungeonReachability.IsSpotReached(reachable, _map, _props[^1].BottomCenter)) return;
            RollbackProps(before);   // unerreichbar -> naechsten Raum versuchen
        }
    }

    /// <summary>Wie oft eine gewuerfelte Spiegelstellung versucht wird, bevor die feste greift.</summary>
    private const int MirrorAttempts = 12;

    /// <summary>
    /// Stellt eine Fassung des Spiegelraetsels auf und gibt sie nur zurueck, wenn genau EINE der
    /// 4³ Stellungen der drehbaren Spiegel das Standbild trifft und die Ausgangsstellung es noch
    /// nicht tut. Beides laesst sich sonst erst im Spiel bemerken - und ein Raetsel, das sich beim
    /// Betreten von selbst loest, faellt nicht einmal dort auf.
    /// </summary>
    private PuzzleSpec? TryPlaceMirrors(RoomNode room, bool useFixedFallback)
    {
        const int beamRow = FloorRow;            // Spiegel stehen auf dem Boden, Strahl in ihrer Reihe
        const int upperStandRow = beamRow - 6;   // hoch an der Wand, ausserhalb der Sprungweite

        // Die bewaehrte Fassung: Hebepaar in der Mitte und rechts, Sperre dazwischen.
        int sourceColumn = 2, targetColumn = 26;
        int[] turnable = { 6, 11, 21 };
        int liftFirst = 1;                       // Index in turnable: erster Spiegel des Hebepaars

        if (!useFixedFallback)
        {
            sourceColumn = 2 + _random.Next(2);
            targetColumn = 26 + _random.Next(2);
            // Drei Spalten mit Luft dazwischen, damit die Sperre noch dazwischenpasst.
            int first = sourceColumn + 3 + _random.Next(3);
            int second = first + 4 + _random.Next(3);
            int third = second + 5 + _random.Next(3);
            if (third >= targetColumn - 2) return null;
            turnable = new[] { first, second, third };
            liftFirst = _random.Next(2);         // hebt das linke oder das rechte Paar
        }

        int liftLeft = turnable[liftFirst];
        int liftRight = turnable[liftFirst + 1];
        int blockerColumn = (liftLeft + liftRight) / 2;
        if (blockerColumn <= liftLeft || blockerColumn >= liftRight) return null;

        bool ok = PlacePropAt(room, "lamp", "beam_source", 0, sourceColumn, beamRow)
                  & PlacePropAt(room, "mirror", "mirror", 0, turnable[0], beamRow)
                  & PlacePropAt(room, "mirror", "mirror", 1, turnable[1], beamRow)
                  & PlacePropAt(room, "mirror", "mirror", 2, turnable[2], beamRow)
                  & PlacePropAt(room, "mirror", "mirror_fixed", 0, liftLeft, upperStandRow)
                  & PlacePropAt(room, "mirror", "mirror_fixed", 1, liftRight, upperStandRow)
                  & PlacePropAt(room, "mirror", "mirror_fixed", 2, blockerColumn, beamRow)
                  & PlacePropAt(room, "statue", "beam_target", 0, targetColumn, beamRow);
        if (!ok) return null;

        // Order = Stellungen der FESTEN Spiegel (0 = "|", 1 = "/", 2 = "–", 3 = "\\"):
        // oben links lenkt hinauf-nach-rechts, oben rechts nach unten, die Sperre steht quer.
        var order = new[] { 1, 3, 0 };

        // Nachrechnen statt hoffen. Die Kachelreihen entsprechen PlacePropAt: ein Prop steht auf
        // der Bodenkachel, seine Mitte liegt eine Kachel darueber.
        Rectangle bounds = room.TileBounds;
        Point Tile(int column, int standRow, int height) =>
            new(bounds.X + column, TileMap.ToTile((bounds.Y + standRow) * TileSize - height / 2f));

        int mirrorHeight = _definitions.Props.Get("mirror").Height;
        var fixedMirrors = new Dictionary<Point, int>
        {
            [Tile(liftLeft, upperStandRow, mirrorHeight)] = order[0],
            [Tile(liftRight, upperStandRow, mirrorHeight)] = order[1],
            [Tile(blockerColumn, beamRow, mirrorHeight)] = order[2],
        };
        var turnableTiles = turnable.Select(column => Tile(column, beamRow, mirrorHeight)).ToList();

        (int solutions, _, bool startsSolved) = Puzzles.BeamTracer.CountSolutions(
            _map, bounds,
            Tile(sourceColumn, beamRow, _definitions.Props.Get("lamp").Height),
            Tile(targetColumn, beamRow, _definitions.Props.Get("statue").Height),
            turnableTiles, fixedMirrors, Array.Empty<int>());

        return solutions == 1 && !startsSolved ? new PuzzleSpec("mirrors", order) : null;
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

    /// <summary>
    /// Setzt ein Prop auf eine GENAU bestimmte Kachel, ohne die Suche in <see cref="FindSpot"/>.
    /// Für Rätsel mit fester Geometrie: Der Spiegelstrahl trifft nur, wenn Quelle, Spiegel und
    /// Ziel exakt auf einer Kachelreihe liegen – ein zufälliger Platz wäre dort unbrauchbar.
    /// Die Spalte wird als belegt vermerkt, damit die Deko später nicht darüber gesetzt wird.
    /// </summary>
    private bool PlacePropAt(RoomNode room, string propId, string tag, int index, int column, int standRow)
    {
        if (!_definitions.Props.Contains(propId)) return false;
        PropDefinition prop = _definitions.Props.Get(propId);
        // Zweiter Riegel gegen Loecher im Boden: steht unter der Kachel nichts Festes, meldet die
        // Methode Fehlschlag und der Aufrufer weicht auf ein Raetsel ohne feste Geometrie aus.
        // Wandhaenger (standRow ausserhalb) sind davon ausgenommen.
        int below = room.TileBounds.Y + standRow;
        int at = room.TileBounds.X + column;
        if (standRow == FloorRow && (!_map.IsInside(at, below) || !TileMap.IsBlocking(_map[at, below]))) return false;
        var bottomCenter = new Vector2(at * TileSize + TileSize / 2f, below * TileSize);
        _props.Add(new PropPlacement(prop, bottomCenter, room, tag, index));
        UsedColumns(room, PropAnchor.Floor).Add(column);
        return true;
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

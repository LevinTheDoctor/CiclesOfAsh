using CirclesOfAsh.Core;

namespace CirclesOfAsh.World;

/// <summary>
/// Erreichbarkeitsprüfung nach der Erzeugung: Flutfüllung vom Spielerstart über begehbare
/// Kacheln. Der Spieler springt ca. 96 px hoch (460² / (2·1100)) — die Füllung darf daher bis zu
/// 6 Kacheln nach oben und beliebig weit nach unten laufen, solange sie auf begehbarem Boden
/// landet. Nicht erreichbare Pflichträume (Siegeltor, Arenen, Kerker, Schatzräume) werden ins Log
/// geschrieben — erst messen, nicht raten: Schlägt die Prüfung regelmäßig an, wird der Generator
/// selbst überarbeitet (siehe AGENT_PROGRESS 1.8).
/// </summary>
public static class DungeonReachability
{
    private const int MaxJumpTiles = 6;

    public static void Check(DungeonLayout layout) => Check(layout, null);

    /// <param name="diagnostics">Optional: sammelt Detailzeilen je Befund (für den Seed-Sweep).</param>
    public static void Check(DungeonLayout layout, List<string>? diagnostics)
    {
        TileMap map = layout.Map;
        bool[] visited = new bool[map.Width * map.Height];
        Queue<Point> frontier = new();

        Point start = TileOf(layout.PlayerSpawn);
        if (!IsWalkable(map, start))
        {
            // Spielerstart kann in der Luft liegen (Spawn ist die Fußhöhe) -> nächsten Boden suchen
            for (int below = 0; below < MaxJumpTiles; below++)
            {
                Point probe = new(start.X, start.Y + below);
                if (!map.IsInside(probe.X, probe.Y)) break;
                if (IsWalkable(map, probe)) { start = probe; break; }
            }
        }
        if (IsWalkable(map, start)) { visited[start.Y * map.Width + start.X] = true; frontier.Enqueue(start); }

        while (frontier.Count > 0)
        {
            Point tile = frontier.Dequeue();
            // Horizontal: gleiche Zeile (Korridore) — Kachel muss frei oder Wasser sein.
            foreach (int dx in new[] { -1, 1 })
            {
                Point next = new(tile.X + dx, tile.Y);
                if (TryVisit(map, visited, next)) frontier.Enqueue(next);
            }
            // Nach oben: Sprünge bis 6 Kacheln, gelandet wird auf begehbarem Boden.
            for (int jump = 1; jump <= MaxJumpTiles; jump++)
            {
                Point next = new(tile.X, tile.Y - jump);
                if (map.IsInside(next.X, next.Y) && IsWalkable(map, next) && !visited[next.Y * map.Width + next.X])
                {
                    visited[next.Y * map.Width + next.X] = true;
                    frontier.Enqueue(next);
                }
            }
            // Nach unten: beliebig tief fallen, weiter geht es am Boden darunter.
            int fallRow = tile.Y + 1;
            while (fallRow < map.Height && !IsWalkable(map, new Point(tile.X, fallRow))) fallRow++;
            if (fallRow < map.Height)
            {
                Point next = new(tile.X, fallRow);
                if (TryVisit(map, visited, next)) frontier.Enqueue(next);
            }
            // Schächte: Kacheln, die nur durch vertikale Tunnel verbunden sind, über Deckenspalten
            // oben hinweg erreichen — der Generator carvet sie als durchgehende Spalte.
            foreach (int dy in new[] { -1, 1 })
            {
                Point next = new(tile.X, tile.Y + dy);
                if (TryVisit(map, visited, next)) frontier.Enqueue(next);
            }
        }

        ReportUnreachableRooms(layout, map, visited, diagnostics);
    }

    private static bool TryVisit(TileMap map, bool[] visited, Point tile)
    {
        if (!map.IsInside(tile.X, tile.Y) || !IsWalkable(map, tile)) return false;
        if (visited[tile.Y * map.Width + tile.X]) return false;
        visited[tile.Y * map.Width + tile.X] = true;
        return true;
    }

    /// <summary>Begehbare Kachel: frei oder Wasser; Plattformen zählen als Boden der Zeile darüber.</summary>
    private static bool IsWalkable(TileMap map, Point tile)
    {
        if (!map.IsInside(tile.X, tile.Y)) return false;
        TileType tileType = map[tile.X, tile.Y];
        return tileType is TileType.Empty or TileType.Water or TileType.Crumbling
            || TileMap.IsPlatform(tileType);
    }

    private static Point TileOf(Vector2 pixel) => new(TileMap.ToTile(pixel.X), TileMap.ToTile(pixel.Y));

    private static void ReportUnreachableRooms(DungeonLayout layout, TileMap map, bool[] visited, List<string>? diagnostics)
    {
        foreach (RoomNode room in layout.Rooms)
        {
            bool required = room.Type is RoomType.Exit or RoomType.Arena or RoomType.Prison
                         or RoomType.Puzzle or RoomType.Boss or RoomType.Start;
            // Schatzräume hängen per Design hinter rissigen Wänden (Dash-Sperre). Sie sind nur dann
            // ein Befund, wenn nicht einmal der rissige Zugang vom Start aus erreichbar ist.
            if (!required && room.Type == RoomType.Treasure && !IsTreasureEntranceReachable(room, map, visited)) required = true;
            if (!required) continue;

            bool reached = false;
            for (int tileY = room.TileBounds.Top; tileY < room.TileBounds.Bottom && !reached; tileY++)
                for (int tileX = room.TileBounds.Left; tileX < room.TileBounds.Right; tileX++)
                    if (map.IsInside(tileX, tileY) && IsWalkable(map, new Point(tileX, tileY)) && visited[tileY * map.Width + tileX])
                    { reached = true; break; }

            if (!reached)
            {
                Log.Warn($"ERREICHBARKEIT: {room.Type}-Raum {room.OwnerKey} ist vom Start aus nicht "
                       + "begehbar erreichbar (Flutfüllung). Seed erneut testen – schlägt das öfter "
                       + "fehl, gehört der Generator überarbeitet.");
                diagnostics?.Add($"{room.OwnerKey} ({room.Type}): Türen=[{string.Join(", ", room.DoorTiles)}], "
                    + $"Grid={room.GridPosition}, Exits=[{string.Join(", ", room.Exits)}]");
            }
        }
    }

    /// <summary>
    /// Hängt an mindestens einer Tür des Schatzraums eine besuchte Kachel AUSSERHALB des Raums?
    /// Der Schatzraum liegt hinter rissigen Wänden (Dash-Sperre) — die Flutfüllung läuft nicht
    /// hindurch. Erreichbar im Sinne der Prüfung ist: Die Sperre kann betreten werden.
    /// Achtung: Die Wand ist ZWEI Kacheln dick (beide Räume graben ihre eigene Wand), deshalb
    /// schreitet die Prüfung von der Tür aus durch Cracked-Kacheln hindurch, bis sie Freiraum findet.
    /// </summary>
    private static bool IsTreasureEntranceReachable(RoomNode room, TileMap map, bool[] visited)
    {
        foreach (Point door in room.DoorTiles)
        {
            bool verticalWall = door.X == room.TileBounds.Left || door.X == room.TileBounds.Right - 1;
            Point step = verticalWall
                ? new Point(door.X == room.TileBounds.Left ? -1 : 1, 0)
                : new Point(0, door.Y == room.TileBounds.Top ? -1 : 1);

            Point probe = door;
            for (int depth = 0; depth < 4; depth++)   // doppelt so tief wie die Wand dick ist
            {
                probe += step;
                if (!map.IsInside(probe.X, probe.Y)) break;
                if (visited[probe.Y * map.Width + probe.X]) return true;
                if (map[probe.X, probe.Y] is not TileType.Cracked) break;   // Hindernis außer der Sperre
            }
        }
        return false;
    }
}
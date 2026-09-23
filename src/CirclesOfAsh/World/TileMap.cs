namespace CirclesOfAsh.World;

/// <summary>": byte" legt den Speichertyp fest -> 1 Byte pro Kachel statt 4 (int).</summary>
public enum TileType : byte
{
    Empty,
    Solid,
    Platform,   // One-Way: von unten durchspringbar, mit Runter+Springen durchfallen
    Cracked,    // Wie Solid, aber per Dash zerstörbar (Metroidvania-Sperre)
    Gate,       // Siegeltor: massiv, bis das Rätsel gelöst ist
    Water,      // begehbar, bremst und lässt schwimmen
    Crumbling,  // wie Platform, zerbricht kurz nach dem Betreten und kehrt später zurück
}

/// <summary>Kachelkarte des gesamten Dungeons. Eindimensionales Array (Zeile * Breite + Spalte) = cachefreundlich.</summary>
public sealed class TileMap
{
    public const int TileSize = 16;

    // Indizes im Tileset (tiles_<kreis>.png, 16x16 pro Kachel) – siehe tools/assetgen/world.py
    private const int TileSurface = 0, TileWall = 1, TilePlatform = 2, TileCracked = 3, TileBackground = 4,
                      TileBackgroundBroken = 5, TileGate = 6, TileCrumbling = 7, TileWater = 8, TileBackgroundNiche = 9;

    private readonly TileType[] _tiles;

    public TileMap(int width, int height, TileType fill)
    {
        Width = width;
        Height = height;
        _tiles = new TileType[width * height];
        Array.Fill(_tiles, fill);
    }

    public int Width { get; }
    public int Height { get; }
    public Rectangle PixelBounds => new(0, 0, Width * TileSize, Height * TileSize);

    /// <summary>Außerhalb der Karte gilt alles als massiv -> niemand fällt aus der Welt.</summary>
    public TileType this[int x, int y]
    {
        get => IsInside(x, y) ? _tiles[y * Width + x] : TileType.Solid;
        set
        {
            if (IsInside(x, y)) _tiles[y * Width + x] = value;   // "value" = der zugewiesene Wert im Setter
        }
    }

    public bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    // "is A or B or C" = Pattern Matching mit Oder-Muster
    public static bool IsBlocking(TileType tile) => tile is TileType.Solid or TileType.Cracked or TileType.Gate;
    public static bool IsPlatform(TileType tile) => tile is TileType.Platform or TileType.Crumbling;

    public static int ToTile(float pixel) => (int)MathF.Floor(pixel / TileSize);

    public void Fill(Rectangle tileArea, TileType type)
    {
        for (int y = tileArea.Top; y < tileArea.Bottom; y++)
            for (int x = tileArea.Left; x < tileArea.Right; x++)
                this[x, y] = type;
    }

    /// <summary>
    /// Deterministischer Pseudo-Zufall pro Kachel (0..1). Gleiche Koordinate = gleicher Wert,
    /// dadurch "flackern" Hintergrundvarianten nicht von Frame zu Frame.
    /// </summary>
    public static float Hash(int x, int y)
    {
        unchecked   // Überlauf bei der Multiplikation ist hier gewollt
        {
            uint hash = (uint)(x * 374761393 + y * 668265263);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return (hash & 0xFFFF) / 65535f;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D tileset, Rectangle visibleArea, Color tint, float decay, float time)
    {
        int startX = Math.Max(0, ToTile(visibleArea.Left));
        int startY = Math.Max(0, ToTile(visibleArea.Top));
        int endX = Math.Min(Width - 1, ToTile(visibleArea.Right));
        int endY = Math.Min(Height - 1, ToTile(visibleArea.Bottom));

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                TileType tile = this[x, y];
                var destination = new Vector2(x * TileSize, y * TileSize);

                // Alles, was nicht massiv ist, bekommt eine Hintergrundmauer (mit Verfall-Varianten)
                if (tile is not (TileType.Solid or TileType.Cracked))
                {
                    float hash = Hash(x, y);
                    int background = hash < decay * 0.22f ? TileBackgroundBroken
                        : hash > 0.95f ? TileBackgroundNiche
                        : TileBackground;
                    spriteBatch.Draw(tileset, destination, Source(background), tint);
                }

                // switch-Ausdruck: Kacheltyp -> Index im Tileset. -1 = nichts weiter zeichnen
                int index = tile switch
                {
                    TileType.Solid => IsBlocking(this[x, y - 1]) ? TileWall : TileSurface,
                    TileType.Platform => TilePlatform,
                    TileType.Cracked => TileCracked,
                    TileType.Gate => TileGate,
                    TileType.Crumbling => TileCrumbling,
                    TileType.Water => TileWater,
                    _ => -1,
                };
                if (index < 0) continue;
                Color color = tile == TileType.Water
                    ? tint * (0.75f + 0.15f * MathF.Sin(time * 2f + x * 0.7f))   // Wasser "atmet"
                    : tint;
                spriteBatch.Draw(tileset, destination, Source(index), color);
            }
        }
    }

    private static Rectangle Source(int index) => new(index * TileSize, 0, TileSize, TileSize);
}

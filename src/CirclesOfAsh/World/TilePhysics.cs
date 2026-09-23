using CirclesOfAsh.Entities;

namespace CirclesOfAsh.World;

/// <summary>[Flags] erlaubt Kombinationen per bitweisem ODER: HitWallLeft | Landed.</summary>
[Flags]
public enum CollisionResult
{
    None = 0,
    HitWallLeft = 1,
    HitWallRight = 2,
    Landed = 4,
    HitCeiling = 8,
    HitWall = HitWallLeft | HitWallRight,
}

/// <summary>
/// AABB-gegen-Kacheln-Kollision mit getrennten Achsen: erst X bewegen und auflösen, dann Y.
/// Getrennte Achsen verhindern das typische "an Kanten hängenbleiben".
/// </summary>
public static class TilePhysics
{
    public const float Gravity = 1100f;
    public const float MaxFallSpeed = 460f;
    private const float Epsilon = 0.01f;

    public static CollisionResult MoveAndCollide(Entity body, TileMap map, float deltaSeconds, bool ignorePlatforms)
    {
        CollisionResult result = CollisionResult.None;
        float width = body.Size.X;
        float height = body.Size.Y;

        // ---------- X-Achse
        body.Position.X += body.Velocity.X * deltaSeconds;
        int top = TileMap.ToTile(body.Position.Y);
        int bottom = TileMap.ToTile(body.Position.Y + height - Epsilon);
        if (body.Velocity.X > 0f)
        {
            int tileX = TileMap.ToTile(body.Position.X + width - Epsilon);
            if (AnyBlockingInColumn(map, tileX, top, bottom))
            {
                body.Position.X = tileX * TileMap.TileSize - width;
                body.Velocity.X = 0f;
                result |= CollisionResult.HitWallRight;   // "|=" setzt das Bit zusätzlich
            }
        }
        else if (body.Velocity.X < 0f)
        {
            int tileX = TileMap.ToTile(body.Position.X);
            if (AnyBlockingInColumn(map, tileX, top, bottom))
            {
                body.Position.X = (tileX + 1) * TileMap.TileSize;
                body.Velocity.X = 0f;
                result |= CollisionResult.HitWallLeft;
            }
        }

        // ---------- Y-Achse
        float previousBottom = body.Position.Y + height;
        body.Position.Y += body.Velocity.Y * deltaSeconds;
        int left = TileMap.ToTile(body.Position.X);
        int right = TileMap.ToTile(body.Position.X + width - Epsilon);
        if (body.Velocity.Y > 0f)
        {
            int tileY = TileMap.ToTile(body.Position.Y + height - Epsilon);
            float tileTop = tileY * TileMap.TileSize;
            // One-Way-Plattform blockiert nur, wenn wir im letzten Frame darüber standen
            bool platformBlocks = !ignorePlatforms && previousBottom <= tileTop + Epsilon;
            for (int x = left; x <= right; x++)
            {
                TileType tile = map[x, tileY];
                if (TileMap.IsBlocking(tile) || (TileMap.IsPlatform(tile) && platformBlocks))
                {
                    body.Position.Y = tileTop - height;
                    body.Velocity.Y = 0f;
                    result |= CollisionResult.Landed;
                    break;
                }
            }
        }
        else if (body.Velocity.Y < 0f)
        {
            int tileY = TileMap.ToTile(body.Position.Y);
            for (int x = left; x <= right; x++)
            {
                if (!TileMap.IsBlocking(map[x, tileY])) continue;
                body.Position.Y = (tileY + 1) * TileMap.TileSize;
                body.Velocity.Y = 0f;
                result |= CollisionResult.HitCeiling;
                break;
            }
        }
        return result;
    }

    public static bool IsStandingOnPlatformOnly(Entity body, TileMap map)
    {
        int tileY = TileMap.ToTile(body.Position.Y + body.Size.Y + 1f);
        int left = TileMap.ToTile(body.Position.X);
        int right = TileMap.ToTile(body.Position.X + body.Size.X - Epsilon);
        bool anyPlatform = false;
        for (int x = left; x <= right; x++)
        {
            TileType tile = map[x, tileY];
            if (TileMap.IsBlocking(tile)) return false;
            anyPlatform |= TileMap.IsPlatform(tile);
        }
        return anyPlatform;
    }

    /// <summary>Steckt die Körpermitte in Wasser?</summary>
    public static bool IsInWater(Entity body, TileMap map) =>
        map[TileMap.ToTile(body.Center.X), TileMap.ToTile(body.Center.Y)] == TileType.Water;

    private static bool AnyBlockingInColumn(TileMap map, int tileX, int fromY, int toY)
    {
        for (int y = fromY; y <= toY; y++)
        {
            if (TileMap.IsBlocking(map[tileX, y])) return true;
        }
        return false;
    }
}

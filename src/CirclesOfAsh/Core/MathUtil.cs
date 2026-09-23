namespace CirclesOfAsh.Core;

public static class MathUtil
{
    /// <summary>Dreht einen Vektor um 'radians' (2D-Rotationsmatrix).</summary>
    public static Vector2 Rotate(Vector2 vector, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
    }

    /// <summary>Normalisiert sicher: Ein Nullvektor würde sonst NaN ergeben.</summary>
    public static Vector2 SafeNormalize(Vector2 vector, Vector2 fallback) =>
        vector.LengthSquared() > 0.0001f ? Vector2.Normalize(vector) : fallback;   // "? :" = ternärer Operator

    /// <summary>Framerate-unabhängiges Annähern (exponentielles Glätten).</summary>
    public static float Damp(float current, float target, float sharpness, float deltaSeconds) =>
        MathHelper.Lerp(current, target, 1f - MathF.Exp(-sharpness * deltaSeconds));

    public static Vector2 Damp(Vector2 current, Vector2 target, float sharpness, float deltaSeconds) =>
        Vector2.Lerp(current, target, 1f - MathF.Exp(-sharpness * deltaSeconds));

    public static Vector2 RandomDirection(Random random)
    {
        float angle = random.NextSingle() * MathHelper.TwoPi;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}

namespace CirclesOfAsh.Core;

/// <summary>Einfache 2D-Kamera: folgt einem Ziel weich und bleibt innerhalb eines Rechtecks (Metroidvania-Raumkamera).</summary>
public sealed class Camera2D
{
    public Camera2D(int viewWidth, int viewHeight)
    {
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
    }

    public int ViewWidth { get; }
    public int ViewHeight { get; }
    public Vector2 Position { get; private set; }   // linke obere Ecke der Sicht in Weltkoordinaten

    /// <summary>Verschiebungsmatrix für SpriteBatch.Begin. Gerundet -> keine "zitternden" Pixel.</summary>
    public Matrix Transform => Matrix.CreateTranslation(-MathF.Round(Position.X), -MathF.Round(Position.Y), 0f);

    public Rectangle VisibleArea => new((int)Position.X, (int)Position.Y, ViewWidth, ViewHeight);

    public void Follow(Vector2 target, Rectangle bounds, float deltaSeconds, float sharpness = 7f) =>
        Position = MathUtil.Damp(Position, ClampToBounds(target, bounds), sharpness, deltaSeconds);

    public void SnapTo(Vector2 target, Rectangle bounds) => Position = ClampToBounds(target, bounds);

    private Vector2 ClampToBounds(Vector2 target, Rectangle bounds)
    {
        Vector2 desired = target - new Vector2(ViewWidth / 2f, ViewHeight / 2f);
        return new Vector2(ClampAxis(desired.X, bounds.Left, bounds.Width, ViewWidth),
                           ClampAxis(desired.Y, bounds.Top, bounds.Height, ViewHeight));
    }

    // Ist der Bereich kleiner als die Sicht, wird zentriert, sonst begrenzt.
    private static float ClampAxis(float value, int start, int length, int view) =>
        length <= view ? start + (length - view) / 2f : Math.Clamp(value, start, start + length - view);
}

using CirclesOfAsh.Core;

namespace CirclesOfAsh.UI;

/// <summary>
/// Dantes Höllentrichter als Pixel-Ellipsen: oben weit und golden, unten eng und blutrot.
/// Statisch (Kreis-Übersicht) oder animiert (Ladebildschirm: die Ringe "sinken" endlos nach unten).
/// </summary>
public static class InfernoFunnel
{
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Vector2 top, float width, float depth,
                            int rings, int highlighted, float time, bool animate)
    {
        for (int ring = 0; ring < rings; ring++)
        {
            // t = 0 (oben, weit) .. 1 (unten, eng). Animiert wandert jeder Ring mit der Zeit nach unten ("%": Endlosschleife)
            float t = animate ? (ring + time * 0.6f) % rings / rings : ring / (float)Math.Max(1, rings - 1);
            float radiusX = width / 2f * (1f - t * 0.82f);
            float radiusY = radiusX * 0.26f;
            var center = new Vector2(top.X, top.Y + t * depth);

            Color color;
            if (animate)
            {
                color = Color.Lerp(Palette.Faith, Palette.Blood, t) * (0.25f + 0.75f * MathF.Sin(t * MathF.PI));
            }
            else
            {
                float pulse = 0.7f + 0.3f * MathF.Sin(time * 4f);
                color = ring < highlighted ? Palette.Ash * 0.7f
                      : ring == highlighted ? Palette.Faith * pulse
                      : Palette.Blood * 0.55f;
            }
            DrawEllipse(spriteBatch, pixel, center, radiusX, radiusY, color);
        }
    }

    public static void DrawEllipse(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radiusX, float radiusY, Color color)
    {
        int steps = Math.Max(24, (int)(radiusX * 1.6f));
        for (int step = 0; step < steps; step++)
        {
            float angle = step / (float)steps * MathHelper.TwoPi;
            var point = new Vector2(center.X + MathF.Cos(angle) * radiusX, center.Y + MathF.Sin(angle) * radiusY);
            // Hintere Hälfte (oben) dunkler -> Tiefenwirkung
            Color shade = MathF.Sin(angle) < 0f ? color * 0.5f : color;
            spriteBatch.Draw(pixel, new Rectangle((int)point.X, (int)point.Y, 2, 1), shade);
        }
    }
}

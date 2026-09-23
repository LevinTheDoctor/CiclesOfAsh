namespace CirclesOfAsh.World;

/// <summary>
/// 2D-Beleuchtung ohne Shader:
///  1. Lichtkarte (Render-Target) mit der Umgebungshelligkeit füllen (tiefer Kreis = dunkler).
///  2. Jede Lichtquelle als weichen Kreis ADDITIV hineinmalen (Licht addiert sich).
///  3. Lichtkarte mit MULTIPLY über die fertige Szene legen: Szene * Licht -> Dunkel bleibt dunkel.
/// Die Lichttextur hat harte Stufen + Dithering -> passt zum Pixel-Look.
/// </summary>
public sealed class LightingSystem : IDisposable
{
    /// <summary>Ergebnis = Quelle * Ziel. Alpha des Ziels bleibt unverändert.</summary>
    public static readonly BlendState Multiply = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private const int LightTextureSize = 64;
    private readonly RenderTarget2D _lightMap;
    private readonly Texture2D _lightTexture;
    private readonly List<(Vector2 Center, float Radius, Color Color)> _lights = new();

    public LightingSystem(GraphicsDevice device, int width, int height)
    {
        _lightMap = new RenderTarget2D(device, width, height);
        _lightTexture = CreateLightTexture(device);
    }

    public Color Ambient { get; set; } = Color.White;

    /// <summary>
    /// Globale Aufhellung 0..1: 0 = Umgebungsfarbe wie definiert, 1 = völlig hell.
    /// Wird vom Optionsmenü gesteuert, damit jeder die Dunkelheit selbst dosieren kann.
    /// </summary>
    public float Brightness { get; set; }

    public void Clear() => _lights.Clear();

    public void Add(Vector2 center, float radius, Color color)
    {
        if (radius > 1f) _lights.Add((center, radius, color));
    }

    /// <summary>Muss VOR dem Binden der Leinwand laufen (siehe IScene.PrepareDraw).</summary>
    public void Render(GraphicsDevice device, SpriteBatch spriteBatch, Matrix worldTransform)
    {
        device.SetRenderTarget(_lightMap);
        // Brightness hebt die Umgebungsfarbe linear Richtung Weiß -> die Hölle bleibt düster,
        // ist aber nie ganz schwarz (Standard 0.32 aus balance.json "ambientLift").
        Color ambient = Ambient;
        if (Brightness > 0f) ambient = Color.Lerp(Ambient, Color.White, Brightness);
        device.Clear(ambient);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, transformMatrix: worldTransform);
        foreach (var (center, radius, color) in _lights)
        {
            int size = (int)(radius * 2f);
            spriteBatch.Draw(_lightTexture, new Rectangle((int)(center.X - radius), (int)(center.Y - radius), size, size), color);
        }
        spriteBatch.End();
        device.SetRenderTarget(null);
    }

    /// <summary>Legt die Lichtkarte über das bereits Gezeichnete (Bildschirmraum, ohne Kamera).</summary>
    public void Composite(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(blendState: Multiply, samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_lightMap, Vector2.Zero, Color.White);
        spriteBatch.End();
    }

    private static Texture2D CreateLightTexture(GraphicsDevice device)
    {
        // Stufen (Abstand vom Zentrum 0..1 -> Helligkeit). An den Grenzen wird im Schachbrett gemischt.
        (float Edge, float Alpha)[] bands = { (0.35f, 1f), (0.6f, 0.7f), (0.82f, 0.4f), (1f, 0.15f) };
        const float DitherWidth = 0.05f;
        var pixels = new Color[LightTextureSize * LightTextureSize];
        float half = LightTextureSize / 2f;
        for (int y = 0; y < LightTextureSize; y++)
        {
            for (int x = 0; x < LightTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half)) / half;
                if ((x + y) % 2 == 0) distance += DitherWidth;   // Dithering verschiebt jeden zweiten Pixel nach außen
                float alpha = 0f;
                foreach (var (edge, bandAlpha) in bands)
                {
                    if (distance > edge) continue;
                    alpha = bandAlpha;
                    break;
                }
                // Weiß mit Alpha: BlendState.Additive rechnet "Farbe * Alpha + Ziel"
                pixels[y * LightTextureSize + x] = new Color(255, 255, 255, (int)(alpha * 255));
            }
        }
        var texture = new Texture2D(device, LightTextureSize, LightTextureSize);
        texture.SetData(pixels);
        return texture;
    }

    public void Dispose()
    {
        _lightMap.Dispose();
        _lightTexture.Dispose();
    }
}

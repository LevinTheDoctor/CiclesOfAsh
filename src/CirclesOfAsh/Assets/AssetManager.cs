using CirclesOfAsh.Core;

namespace CirclesOfAsh.Assets;

/// <summary>
/// Lädt Texturen, Spritesheets und Fonts zur Laufzeit direkt aus PNG-Dateien (Texture2D.FromFile)
/// und cached sie (jede Datei wird nur einmal geladen). Fehlende Dateien führen NICHT zum Absturz,
/// sondern zu einer magenta-schwarzen "Missing Texture" -> Modder sehen sofort, was fehlt.
/// </summary>
public sealed class AssetManager : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ContentLocator _locator;
    private readonly Dictionary<string, Texture2D> _texturesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpriteSheet> _spriteSheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapFont> _fonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Texture2D _missingTexture;

    private AssetManager(GraphicsDevice graphicsDevice, ContentLocator locator, AssetManifest manifest)
    {
        _graphicsDevice = graphicsDevice;
        _locator = locator;
        Manifest = manifest;
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });
        _missingTexture = CreateMissingTexture(graphicsDevice);
    }

    public AssetManifest Manifest { get; }

    /// <summary>1x1 weißer Pixel: Grundlage für Rechtecke, Balken und Partikel (per Tint eingefärbt).</summary>
    public Texture2D Pixel { get; }

    public static AssetManager Load(GraphicsDevice graphicsDevice, ContentLocator locator)
    {
        var manifest = new AssetManifest();
        foreach (string manifestPath in locator.FindAllLayered("manifest.json"))
        {
            manifest.MergeFrom(JsonDefaults.Load<AssetManifest>(manifestPath));
        }
        return new AssetManager(graphicsDevice, locator, manifest);
    }

    public Texture2D GetTexture(string id) =>
        Manifest.Textures.TryGetValue(id, out string? path) ? LoadTextureFile(path) : Missing($"Textur-ID '{id}'");

    // Für den Ladebildschirm: alle bekannten IDs, um sie vorab zu laden
    public IEnumerable<string> TextureIds => Manifest.Textures.Keys;
    public IEnumerable<string> SpriteSheetIds => Manifest.SpriteSheets.Keys;

    public bool HasTexture(string id) => Manifest.Textures.ContainsKey(id);

    public bool HasSpriteSheet(string id) => Manifest.SpriteSheets.ContainsKey(id);

    public SpriteSheet GetSpriteSheet(string id)
    {
        if (_spriteSheets.TryGetValue(id, out SpriteSheet? cached)) return cached;

        SpriteSheet sheet;
        if (Manifest.SpriteSheets.TryGetValue(id, out SpriteSheetEntry? entry))
        {
            // LINQ-Select projiziert jeden Manifest-Eintrag auf ein AnimationClip-Record
            IEnumerable<AnimationClip> clips = entry.Animations.Select(pair =>
                new AnimationClip(pair.Key, pair.Value.Row, Math.Max(1, pair.Value.Frames), pair.Value.Fps, pair.Value.Loop, pair.Value.Column));
            sheet = new SpriteSheet(LoadTextureFile(entry.Texture), entry.FrameWidth, entry.FrameHeight, clips);
        }
        else
        {
            sheet = new SpriteSheet(Missing($"Spritesheet-ID '{id}'"), 16, 16, Array.Empty<AnimationClip>());
        }
        _spriteSheets[id] = sheet;
        return sheet;
    }

    public BitmapFont GetFont(string id)
    {
        if (_fonts.TryGetValue(id, out BitmapFont? cached)) return cached;
        if (!Manifest.Fonts.TryGetValue(id, out string? descriptorPath))
        {
            throw new InvalidDataException($"Font '{id}' fehlt in manifest.json.");
        }
        FontEntry entry = JsonDefaults.Load<FontEntry>(_locator.Resolve(descriptorPath));
        var font = new BitmapFont(LoadTextureFile(entry.Texture), entry);
        _fonts[id] = font;
        return font;
    }

    private Texture2D LoadTextureFile(string relativePath)
    {
        if (_texturesByPath.TryGetValue(relativePath, out Texture2D? cached)) return cached;

        string? fullPath = _locator.TryResolve(relativePath);
        if (fullPath is null) return Missing($"Datei '{relativePath}'");

        // FromFile liefert NICHT-vormultipliziertes Alpha -> beim Zeichnen BlendState.NonPremultiplied nutzen!
        Texture2D texture = Texture2D.FromFile(_graphicsDevice, fullPath);
        _texturesByPath[relativePath] = texture;
        return texture;
    }

    private Texture2D Missing(string what)
    {
        Log.Warn($"{what} nicht gefunden – Platzhalter wird verwendet.");
        return _missingTexture;
    }

    private static Texture2D CreateMissingTexture(GraphicsDevice graphicsDevice)
    {
        const int size = 16;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // "^" = XOR: true, wenn genau eine Seite true ist -> Schachbrettmuster aus 4x4-Feldern
                bool isMagenta = (x / 4 % 2 == 0) ^ (y / 4 % 2 == 0);
                pixels[y * size + x] = isMagenta ? Color.Magenta : Color.Black;
            }
        }
        var texture = new Texture2D(graphicsDevice, size, size);
        texture.SetData(pixels);
        return texture;
    }

    public void Dispose()
    {
        foreach (Texture2D texture in _texturesByPath.Values) texture.Dispose();
        _missingTexture.Dispose();
        Pixel.Dispose();
    }
}

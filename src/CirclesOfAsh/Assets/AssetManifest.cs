namespace CirclesOfAsh.Assets;

/// <summary>
/// Abbild von Content/manifest.json. Der Code referenziert Assets nur über IDs ("player.mage"),
/// niemals über Dateipfade -> Dateien sind austauschbar, ohne Code anzufassen.
/// Properties mit "{ get; init; }" dürfen nur beim Erzeugen (z. B. durch den JSON-Deserializer) gesetzt werden.
/// </summary>
public sealed class AssetManifest
{
    public Dictionary<string, string> Textures { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SpriteSheetEntry> SpriteSheets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Fonts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Sounds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Loopende Musikstücke. Getrennt von "Sounds", weil sie anders abgespielt und geregelt werden.</summary>
    public Dictionary<string, string> Music { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Überlagert Einträge eines Mod-Manifests (spätere Einträge gewinnen).</summary>
    public void MergeFrom(AssetManifest other)
    {
        foreach (var (id, path) in other.Textures) Textures[id] = path;   // "var (a, b)" dekonstruiert KeyValuePair
        foreach (var (id, entry) in other.SpriteSheets) SpriteSheets[id] = entry;
        foreach (var (id, path) in other.Fonts) Fonts[id] = path;
        foreach (var (id, path) in other.Sounds) Sounds[id] = path;
        foreach (var (id, path) in other.Music) Music[id] = path;
    }
}

public sealed class SpriteSheetEntry
{
    public string Texture { get; init; } = "";
    public int FrameWidth { get; init; } = 16;
    public int FrameHeight { get; init; } = 16;
    public Dictionary<string, AnimationEntry> Animations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AnimationEntry
{
    public int Row { get; init; }
    /// <summary>Startspalte im Sheet. Erlaubt mehrere Animationen (z. B. Item-Icons) in einer Zeile.</summary>
    public int Column { get; init; }
    public int Frames { get; init; } = 1;
    public float Fps { get; init; } = 8f;
    public bool Loop { get; init; } = true;
}

/// <summary>Beschreibung eines Bitmap-Fonts (wird vom Asset-Generator erzeugt, siehe tools/).</summary>
public sealed class FontEntry
{
    public string Texture { get; init; } = "";
    public int CellWidth { get; init; }
    public int CellHeight { get; init; }
    public int LineHeight { get; init; }
    public string Charset { get; init; } = "";
    public int[] Advances { get; init; } = Array.Empty<int>();
}

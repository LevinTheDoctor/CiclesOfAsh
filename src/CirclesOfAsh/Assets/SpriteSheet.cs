namespace CirclesOfAsh.Assets;

/// <summary>
/// Eine Animation = eine Zeile im Spritesheet. "record" erzeugt automatisch Konstruktor,
/// Properties und Wertgleichheit (zwei Clips mit gleichen Werten sind "==").
/// </summary>
// "int StartColumn = 0" = optionaler Parameter mit Standardwert -> bestehende Aufrufe bleiben gültig
public sealed record AnimationClip(string Name, int Row, int FrameCount, float FramesPerSecond, bool Loop, int StartColumn = 0);

/// <summary>Textur + Raster + benannte Animationen. Einmal geladen, von vielen Entities geteilt (Flyweight-Pattern).</summary>
public sealed class SpriteSheet
{
    private readonly Dictionary<string, AnimationClip> _clips;

    public SpriteSheet(Texture2D texture, int frameWidth, int frameHeight, IEnumerable<AnimationClip> clips)
    {
        Texture = texture;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        _clips = clips.ToDictionary(clip => clip.Name, StringComparer.OrdinalIgnoreCase);
        if (_clips.Count == 0) _clips["idle"] = new AnimationClip("idle", 0, 1, 1f, true);
    }

    public Texture2D Texture { get; }
    public int FrameWidth { get; }
    public int FrameHeight { get; }

    /// <summary>Fehlt eine Animation (z. B. "hurt" bei einem Mod-Sprite), wird "idle" bzw. die erste genommen.</summary>
    public AnimationClip GetClip(string name) =>
        _clips.TryGetValue(name, out AnimationClip? clip) ? clip
        : _clips.TryGetValue("idle", out AnimationClip? idle) ? idle
        : _clips.Values.First();

    public bool HasClip(string name) => _clips.ContainsKey(name);

    public Rectangle GetFrameRectangle(AnimationClip clip, int frameIndex) =>
        new((clip.StartColumn + frameIndex) * FrameWidth, clip.Row * FrameHeight, FrameWidth, FrameHeight);

    /// <summary>Direkter Zugriff auf ein Einzelbild (Spalte/Zeile), z. B. für Runen-Symbole.</summary>
    public Rectangle GetCell(int column, int row) => new(column * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);
}

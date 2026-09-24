namespace CirclesOfAsh.Assets;

/// <summary>Spielt Animationen eines <see cref="SpriteSheet"/> ab. Jede Entity besitzt ihren eigenen Player (eigener Zeitzustand).</summary>
public sealed class AnimationPlayer
{
    private float _elapsedSeconds;

    public AnimationPlayer(SpriteSheet sheet)
    {
        Sheet = sheet;
        CurrentClip = sheet.GetClip("idle");
    }

    public SpriteSheet Sheet { get; }
    public AnimationClip CurrentClip { get; private set; }
    public int FrameIndex { get; private set; }

    public bool IsFinished => !CurrentClip.Loop && _elapsedSeconds * CurrentClip.FramesPerSecond >= CurrentClip.FrameCount;

    /// <summary>Wechselt die Animation. Läuft sie bereits, wird sie nicht neu gestartet (kein Flackern).</summary>
    public void Play(string clipName, bool restart = false)
    {
        AnimationClip clip = Sheet.GetClip(clipName);
        if (clip == CurrentClip && !restart) return;   // "==" nutzt hier die Record-Wertgleichheit
        CurrentClip = clip;
        _elapsedSeconds = 0f;
        FrameIndex = 0;
    }

    public void Update(float deltaSeconds)
    {
        _elapsedSeconds += deltaSeconds;
        int frame = (int)(_elapsedSeconds * CurrentClip.FramesPerSecond);
        // "%" = Modulo (Rest der Division) -> Endlosschleife 0,1,2,3,0,1...
        FrameIndex = CurrentClip.Loop ? frame % CurrentClip.FrameCount : Math.Min(frame, CurrentClip.FrameCount - 1);
    }

    /// <summary>Zeichnet mit Anker "unten mittig" -> Sprites stehen unabhängig von ihrer Größe auf dem Boden.</summary>
    /// <param name="scale">
    /// Getrennt in X und Y, damit sich eine Figur stauchen lässt (Ducken) ohne schmaler zu werden.
    /// null = 1:1. Der Ursprung liegt auf den Füßen, gestaucht wird also nach unten.
    /// </param>
    public void Draw(SpriteBatch spriteBatch, Vector2 bottomCenter, bool flipHorizontally, Color tint, Vector2? scale = null)
    {
        Rectangle source = Sheet.GetFrameRectangle(CurrentClip, FrameIndex);
        var origin = new Vector2(Sheet.FrameWidth / 2f, Sheet.FrameHeight);
        var position = new Vector2(MathF.Round(bottomCenter.X), MathF.Round(bottomCenter.Y));
        SpriteEffects effects = flipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        spriteBatch.Draw(Sheet.Texture, position, source, tint, 0f, origin, scale ?? Vector2.One, effects, 0f);
    }
}

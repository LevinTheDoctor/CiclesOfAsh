using CirclesOfAsh.Core;

namespace CirclesOfAsh.Assets;

/// <summary>
/// Mehrere Spritesheets übereinander, die immer dasselbe Frame zeigen (Körper, Haare, Outfit, Akzent).
/// Jede Ebene hat eine eigene Färbung -> der Charakter-Editor kombiniert frei, ohne neue Grafiken.
/// Composite-Idee: nach außen verhält sich das Objekt wie EIN AnimationPlayer.
/// </summary>
public sealed class LayeredSprite
{
    private readonly List<(AnimationPlayer Animation, Color Tint)> _layers;

    public LayeredSprite(IEnumerable<(SpriteSheet Sheet, Color Tint)> layers)
    {
        // Select projiziert jedes (Sheet, Tint)-Paar auf ein (AnimationPlayer, Tint)-Paar
        _layers = layers.Select(layer => (new AnimationPlayer(layer.Sheet), layer.Tint)).ToList();
    }

    public void Play(string clipName)
    {
        foreach (var (animation, _) in _layers) animation.Play(clipName);   // "_" = Discard, Wert wird nicht gebraucht
    }

    public void Update(float deltaSeconds)
    {
        foreach (var (animation, _) in _layers) animation.Update(deltaSeconds);
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 bottomCenter, bool flipHorizontally, Color tint, Vector2? scale = null)
    {
        foreach (var (animation, layerTint) in _layers)
            animation.Draw(spriteBatch, bottomCenter, flipHorizontally, ColorUtil.Multiply(layerTint, tint), scale);
    }
}

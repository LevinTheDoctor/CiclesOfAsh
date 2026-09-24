using CirclesOfAsh.Core;
using CirclesOfAsh.Progression;
using Microsoft.Xna.Framework.Audio;

namespace CirclesOfAsh.Assets;

/// <summary>
/// Spielt Soundeffekte per ID ab. Lazy Loading: Eine WAV wird erst beim ersten Abspielen geladen.
/// Ohne Audiogerät (z. B. CI-Server) deaktiviert sich der Dienst still, statt das Spiel abstürzen zu lassen.
/// </summary>
public sealed class AudioService : IDisposable
{
    private readonly ContentLocator _locator;
    private readonly IReadOnlyDictionary<string, string> _soundPaths;
    private readonly Dictionary<string, SoundEffect?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isAvailable = true;

    public AudioService(ContentLocator locator, IReadOnlyDictionary<string, string> soundPaths)
    {
        _locator = locator;
        _soundPaths = soundPaths;
    }

    /// <summary>Obergrenze für ALLES (Musik und Effekte). Kommt aus dem Optionsmenü.</summary>
    public float MasterVolume { get; set; } = 0.8f;

    /// <summary>Nur Soundeffekte – getrennt regelbar von der Musik.</summary>
    public float SfxVolume { get; set; } = 0.8f;

    /// <summary>Übernimmt die Regler aus dem Optionsmenü. Wird beim Start und nach jeder Änderung gerufen.</summary>
    public void ApplySettings(GameSettings settings)
    {
        MasterVolume = settings.MasterVolume;
        SfxVolume = settings.SfxVolume;
    }

    public void Play(string? soundId, float volume = 1f, float pitch = 0f)
    {
        if (!_isAvailable || string.IsNullOrEmpty(soundId)) return;
        SoundEffect? effect = GetOrLoad(soundId);
        // "?." = Null-Conditional: Play wird nur aufgerufen, wenn effect nicht null ist
        effect?.Play(Math.Clamp(volume * SfxVolume * MasterVolume, 0f, 1f), Math.Clamp(pitch, -1f, 1f), 0f);
    }

    public IEnumerable<string> SoundIds => _soundPaths.Keys;

    /// <summary>Lädt einen Sound vorab (Ladebildschirm), damit das erste Abspielen nicht ruckelt.</summary>
    public void Preload(string soundId)
    {
        if (_isAvailable) GetOrLoad(soundId);
    }

    private SoundEffect? GetOrLoad(string soundId)
    {
        if (_cache.TryGetValue(soundId, out SoundEffect? cached)) return cached;

        SoundEffect? effect = null;
        string? path = _soundPaths.TryGetValue(soundId, out string? relativePath) ? _locator.TryResolve(relativePath) : null;
        if (path is null)
        {
            // Bisher scheiterte das lautlos: kein Pfad -> null im Cache -> jeder Play tut nichts,
            // ohne eine einzige Zeile im Log. Genau so verschwindet Ton unerklaerlich.
            Log.Warn($"Klang '{soundId}' nicht gefunden (Manifest: '{relativePath ?? "fehlt"}').");
        }
        else
        {
            try
            {
                effect = SoundEffect.FromFile(path);
            }
            catch (Exception exception)   // bewusst breit: jede Audio-Ausnahme soll nur loggen
            {
                Log.Warn($"Audio deaktiviert ({exception.GetType().Name}): {exception.Message}");
                _isAvailable = false;
            }
        }
        _cache[soundId] = effect;
        return effect;
    }

    public void Dispose()
    {
        foreach (SoundEffect? effect in _cache.Values) effect?.Dispose();
    }
}

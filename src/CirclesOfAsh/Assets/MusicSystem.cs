using CirclesOfAsh.Core;
using CirclesOfAsh.Progression;
using Microsoft.Xna.Framework.Audio;

namespace CirclesOfAsh.Assets;

/// <summary>
/// Hintergrundmusik als loopende <see cref="SoundEffectInstance"/>.
/// Bewusst KEIN MonoGame-"Song": der bräuchte die Content-Pipeline (MGCB), die dieses Projekt
/// nicht benutzt – siehe CirclesOfAsh.csproj. So bleiben die WAVs zur Laufzeit austauschbar.
///
/// Beim Wechsel wird überblendet: das alte Stück blendet aus, das neue gleichzeitig ein.
/// Ohne Audiogerät (CI-Server) deaktiviert sich der Dienst still, genau wie der AudioService.
/// </summary>
public sealed class MusicSystem : IDisposable
{
    /// <summary>Dauer einer Überblendung in Sekunden. Kurz genug für Szenenwechsel, lang genug für einen weichen Übergang.</summary>
    private const float FadeSeconds = 1.2f;

    private readonly ContentLocator _locator;
    private readonly IReadOnlyDictionary<string, string> _musicPaths;
    private readonly Dictionary<string, SoundEffect?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isAvailable = true;

    // Zwei Spuren: die laufende und die ausblendende. Mehr braucht ein Crossfade nicht.
    private SoundEffectInstance? _current;
    private SoundEffectInstance? _fadingOut;
    private float _currentGain;    // 0..1, steigt beim Einblenden
    private float _fadingOutGain;

    public MusicSystem(ContentLocator locator, IReadOnlyDictionary<string, string> musicPaths)
    {
        _locator = locator;
        _musicPaths = musicPaths;
    }

    public float MasterVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.6f;

    /// <summary>Id des gerade laufenden Stücks (leer = Stille). Verhindert Neustarts beim selben Stück.</summary>
    public string CurrentId { get; private set; } = "";

    public void ApplySettings(GameSettings settings)
    {
        MasterVolume = settings.MasterVolume;
        MusicVolume = settings.MusicVolume;
        ApplyVolumes();
    }

    /// <summary>
    /// Startet ein Stück. Läuft es bereits, passiert nichts (Szenen dürfen das bei jedem OnEnter rufen).
    /// Eine unbekannte ID schaltet die Musik still, statt zu werfen – Mods dürfen Stücke weglassen.
    /// </summary>
    public void Play(string? musicId)
    {
        if (!_isAvailable) return;
        string id = musicId ?? "";
        if (id == CurrentId) return;

        BeginFadeOut();
        CurrentId = id;
        if (id.Length == 0) return;

        SoundEffect? effect = GetOrLoad(id);
        if (effect is null)
        {
            CurrentId = "";
            return;
        }

        try
        {
            _current = effect.CreateInstance();
            _current.IsLooped = true;
            _currentGain = 0f;          // von der Stille hochblenden
            _current.Volume = 0f;
            _current.Play();
        }
        catch (Exception exception)     // bewusst breit: Musik darf das Spiel nie zum Absturz bringen
        {
            Log.Warn($"Musik deaktiviert ({exception.GetType().Name}): {exception.Message}");
            _isAvailable = false;
            _current = null;
            CurrentId = "";
        }
    }

    public void Stop() => Play("");

    /// <summary>Treibt die Überblendung voran. Wird einmal pro Frame aus CirclesGame.Update gerufen.</summary>
    public void Update(float deltaSeconds)
    {
        if (!_isAvailable) return;
        float step = FadeSeconds <= 0f ? 1f : deltaSeconds / FadeSeconds;

        if (_current is not null && _currentGain < 1f)
            _currentGain = MathF.Min(1f, _currentGain + step);

        if (_fadingOut is not null)
        {
            _fadingOutGain -= step;
            if (_fadingOutGain <= 0f)
            {
                _fadingOut.Stop();
                _fadingOut.Dispose();
                _fadingOut = null;
                _fadingOutGain = 0f;
            }
        }

        ApplyVolumes();
    }

    private void BeginFadeOut()
    {
        // Ein noch ausblendendes Stück wird hart beendet: mehr als zwei Spuren wären hörbarer Matsch.
        _fadingOut?.Stop();
        _fadingOut?.Dispose();
        _fadingOut = _current;
        _fadingOutGain = _currentGain;
        _current = null;
        _currentGain = 0f;
    }

    private void ApplyVolumes()
    {
        float ceiling = Math.Clamp(MusicVolume * MasterVolume, 0f, 1f);
        if (_current is not null) _current.Volume = Math.Clamp(_currentGain * ceiling, 0f, 1f);
        if (_fadingOut is not null) _fadingOut.Volume = Math.Clamp(_fadingOutGain * ceiling, 0f, 1f);
    }

    private SoundEffect? GetOrLoad(string musicId)
    {
        if (_cache.TryGetValue(musicId, out SoundEffect? cached)) return cached;

        SoundEffect? effect = null;
        string? path = _musicPaths.TryGetValue(musicId, out string? relativePath) ? _locator.TryResolve(relativePath) : null;
        if (path is not null)
        {
            try
            {
                effect = SoundEffect.FromFile(path);
            }
            catch (Exception exception)
            {
                Log.Warn($"Musik deaktiviert ({exception.GetType().Name}): {exception.Message}");
                _isAvailable = false;
            }
        }
        else
        {
            Log.Warn($"Musikstück '{musicId}' ist nicht im Manifest oder die Datei fehlt.");
        }
        _cache[musicId] = effect;
        return effect;
    }

    public void Dispose()
    {
        _current?.Dispose();
        _fadingOut?.Dispose();
        foreach (SoundEffect? effect in _cache.Values) effect?.Dispose();
    }
}

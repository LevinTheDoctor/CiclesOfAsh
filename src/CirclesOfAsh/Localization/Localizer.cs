using CirclesOfAsh.Core;

namespace CirclesOfAsh.Localization;

/// <summary>
/// Eine Sprache aus <c>Content/Lang/&lt;id&gt;.json</c>. Die Id ist der Dateiname ("en"), damit
/// Datei und Eintrag nie auseinanderlaufen können.
/// </summary>
public sealed class LanguageDefinition
{
    /// <summary>Wird aus dem Dateinamen gesetzt, nicht aus der JSON.</summary>
    public string Id { get; set; } = "";
    /// <summary>Name in der EIGENEN Sprache ("English", "Deutsch") – so findet jeder seine Sprache, egal was gerade eingestellt ist.</summary>
    public string Name { get; set; } = "";
    /// <summary>Textur-Id der Flagge (manifest.json). Die kleine Fassung für die Reiterleiste heißt gleich mit ".small".</summary>
    public string Flag { get; set; } = "";
    /// <summary>Reihenfolge in der Auswahl (klein = weiter oben).</summary>
    public int Order { get; set; }
    /// <summary>Deutscher Quelltext -> Übersetzung. Leer bei der Quellsprache Deutsch.</summary>
    public Dictionary<string, string> Strings { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Hält alle Sprachen und übersetzt in die gewählte. Deutsch ist die Quellsprache: Ihre Texte
/// stehen direkt im Code und in Content/Data, ihre Sprachdatei hat deshalb keine Einträge.
/// </summary>
public sealed class Localizer
{
    public const string SourceLanguageId = "de";

    private readonly Dictionary<string, LanguageDefinition> _languages = new(StringComparer.OrdinalIgnoreCase);
    private LanguageDefinition? _current;

    /// <summary>Alle geladenen Sprachen in Anzeigereihenfolge.</summary>
    public IReadOnlyList<LanguageDefinition> Languages =>
        _languages.Values.OrderBy(language => language.Order).ThenBy(language => language.Id).ToList();

    /// <summary>Id der aktiven Sprache. Ohne geladene Sprachen die Quellsprache.</summary>
    public string CurrentId => _current?.Id ?? SourceLanguageId;

    public LanguageDefinition? Current => _current;

    /// <summary>
    /// Lädt alle Sprachdateien aus allen Content-Wurzeln. Mods dürfen eine vorhandene Sprache
    /// ergänzen (eigene Texte übersetzen) oder eine neue mitbringen – gleiche Datei = gleiche Sprache,
    /// spätere Ebenen überschreiben einzelne Einträge.
    /// </summary>
    public static Localizer Load(ContentLocator locator)
    {
        var localizer = new Localizer();
        foreach (string path in locator.FindAllLayeredInDirectory("Lang", "*.json"))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            LanguageDefinition layer = JsonDefaults.Load<LanguageDefinition>(path);
            if (!localizer._languages.TryGetValue(id, out LanguageDefinition? language))
            {
                layer.Id = id;
                localizer._languages[id] = layer;
                continue;
            }
            // Mod-Ebene: nur überschreiben, was sie wirklich angibt.
            if (layer.Name.Length > 0) language.Name = layer.Name;
            if (layer.Flag.Length > 0) language.Flag = layer.Flag;
            foreach (var (source, translation) in layer.Strings) language.Strings[source] = translation;
        }
        Log.Info($"Sprachen geladen: {string.Join(", ", localizer.Languages.Select(language => $"{language.Id} ({language.Strings.Count} Texte)"))}.");
        return localizer;
    }

    /// <summary>Stellt die Sprache um. true, wenn sich dadurch etwas geändert hat.</summary>
    public bool SetLanguage(string id)
    {
        if (!_languages.TryGetValue(id, out LanguageDefinition? language))
        {
            // Unbekannte Id (alter Spielstand, entfernte Mod-Sprache): auf Deutsch zurückfallen,
            // statt eine halbe Oberfläche ohne Texte zu zeigen.
            if (!_languages.TryGetValue(SourceLanguageId, out language)) return false;
            Log.Warn($"Sprache '{id}' ist nicht vorhanden – es gilt '{SourceLanguageId}'.");
        }
        if (language == _current) return false;
        _current = language;
        return true;
    }

    public bool HasLanguage(string id) => _languages.ContainsKey(id);

    /// <summary>
    /// Übersetzt einen Quelltext. Fehlt die Übersetzung (oder ist sie leer), bleibt der deutsche
    /// Text stehen – sichtbar unvollständig ist besser als unsichtbar.
    /// </summary>
    public string Translate(string source) =>
        source.Length > 0
        && _current is not null
        && _current.Strings.TryGetValue(source, out string? translated)
        && translated.Length > 0
            ? translated
            : source;
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CirclesOfAsh.Localization;

/// <summary>
/// Anzeigetext aus den Inhaltsdaten (Content/Data): der deutsche Quelltext, übersetzt erst beim
/// Anzeigen. Definitionen führen Namen, Beschreibungen und Dialogzeilen als diesen Typ statt als
/// string – damit kann keine Anzeigestelle das Übersetzen vergessen: Die implizite Umwandlung in
/// string und <see cref="ToString"/> (für "$"-Interpolation) liefern immer die aktuelle Sprache.
///
/// "readonly struct" = unveränderlicher Werttyp: kein null, keine Heap-Allokation, und ein nicht
/// gesetztes Feld (default) ist einfach ein leerer Text.
/// </summary>
[JsonConverter(typeof(LocalizedTextJsonConverter))]
public readonly struct LocalizedText : IEquatable<LocalizedText>, IComparable<LocalizedText>
{
    private readonly string? _source;
    private readonly bool _isVerbatim;

    public LocalizedText(string source) : this(source, isVerbatim: false) { }

    private LocalizedText(string source, bool isVerbatim)
    {
        _source = source;
        _isVerbatim = isVerbatim;
    }

    /// <summary>Leerer Text (dasselbe wie <c>default</c>).</summary>
    public static LocalizedText Empty => default;

    /// <summary>
    /// Text, der NICHT übersetzt wird – etwa ein Name, den der Spieler selbst vergeben hat. Ohne
    /// diese Ausnahme hieße eine Seele namens "Weiter" in der englischen Fassung "Continue".
    /// </summary>
    public static LocalizedText Verbatim(string text) => new(text, isVerbatim: true);

    /// <summary>Quelltext, so wie er in der JSON-Datei steht. Zugleich der Schlüssel der Übersetzung.</summary>
    public string Source => _source ?? "";

    public bool IsEmpty => string.IsNullOrEmpty(_source);

    /// <summary>Der Text in der aktuell gewählten Sprache.</summary>
    public string Translated => _isVerbatim ? Source : Loc.T(Source);

    public override string ToString() => Translated;

    /// <summary>
    /// "implicit operator" = automatische Umwandlung ohne Cast: Wo ein string erwartet wird
    /// (Zeichnen, Messen, Umbrechen), entsteht dabei die Übersetzung.
    /// </summary>
    public static implicit operator string(LocalizedText text) => text.Translated;

    /// <summary>Erlaubt <c>Label = "Weiter"</c> im Code – der Text gilt dann als übersetzbarer Quelltext.</summary>
    public static implicit operator LocalizedText(string source) => new(source);

    // Bewusst KEINE eigenen ==-Operatoren: Zusammen mit den beiden Umwandlungen wäre
    // "text == \"…\"" sonst mehrdeutig. So vergleicht == die angezeigten Texte (über string).
    public bool Equals(LocalizedText other) => Source == other.Source && _isVerbatim == other._isVerbatim;
    public override bool Equals(object? obj) => obj is LocalizedText other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Source, _isVerbatim);

    /// <summary>Sortiert nach dem ANGEZEIGTEN Text – eine Liste soll in jeder Sprache alphabetisch sein.</summary>
    public int CompareTo(LocalizedText other) => string.Compare(Translated, other.Translated, StringComparison.CurrentCulture);
}

/// <summary>Liest einen JSON-String als <see cref="LocalizedText"/> und schreibt ihn wieder als String.</summary>
public sealed class LocalizedTextJsonConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? LocalizedText.Empty : new LocalizedText(reader.GetString() ?? "");

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Source);
}

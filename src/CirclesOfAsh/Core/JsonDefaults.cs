using System.Text.Json;
using System.Text.Json.Serialization;

namespace CirclesOfAsh.Core;

/// <summary>Einheitliche JSON-Einstellungen für alle Daten- und Manifestdateien.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,              // "maxHealth" == "MaxHealth"
        ReadCommentHandling = JsonCommentHandling.Skip,  // Modder dürfen // Kommentare schreiben
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },  // Enums als Text: "Auto" statt 0
    };

    public static T Load<T>(string path)
    {
        try
        {
            // "??" = Null-Coalescing: Ist das Ergebnis null, wird die rechte Seite ausgeführt (hier: Exception).
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
                   ?? throw new InvalidDataException($"Datei '{path}' ist leer.");
        }
        catch (JsonException exception)
        {
            // Klare Fehlermeldung mit Zeilennummer -> Modder finden Tippfehler sofort
            throw new InvalidDataException(
                $"JSON-Fehler in '{path}' (Zeile {exception.LineNumber}): {exception.Message}", exception);
        }
    }
}

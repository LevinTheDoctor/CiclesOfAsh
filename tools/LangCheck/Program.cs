// Übersetzungsprüfung für Content/Lang.
// Aufruf: dotnet run --project tools/LangCheck [-- <sprache> ...]     (ohne Angabe: alle Sprachen)
// Rückgabewert 0 = vollständig, 1 = Befunde.
//
// 1. Quelltexte sammeln – genau das, was das Spiel durch die Übersetzung schickt:
//      * im Code jedes Loc.T("…") und Loc.N("…") (Zeichenketten-Literal als erstes Argument),
//      * in den Inhaltsdaten jeden LocalizedText (per Reflection über alle Definitionen),
//      * die Tastenbeschriftungen aus controllers.json (InputState.Glyph übersetzt sie).
// 2. Je Sprache (außer der Quellsprache Deutsch) prüfen:
//      * FEHLT        – Quelltext ohne Übersetzung (im Spiel erscheint dann der deutsche Text),
//      * PLATZHALTER  – {0}, {name} … stimmen zwischen Quelle und Übersetzung nicht überein,
//      * VERWAIST     – Eintrag, dessen Quelltext es nicht mehr gibt (meist ein umformulierter Text).
// Fehlende Einträge werden als fertige JSON-Zeilen ausgegeben – zum Einfügen in die Sprachdatei.
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Localization;

string gameRoot = FindGameRoot();
string sourceRoot = Path.GetFullPath(Path.Combine(gameRoot, ".."));   // src/ – dort liegt der Code
var locator = new ContentLocator(new[] { Path.Combine(gameRoot, "Content") });   // ohne Mods: nur das Basisspiel
Log.Initialize(Path.Combine(Path.GetTempPath(), "langcheck.log"));

var findings = 0;
var sources = new SortedDictionary<string, string>(StringComparer.Ordinal);   // Quelltext -> erster Fundort
CollectFromCode(sourceRoot, sources, ref findings);
CollectFromData(DefinitionRegistry.Load(locator), sources);
Console.WriteLine($"{sources.Count} übersetzbare Texte gefunden (Code und Inhaltsdaten).");

Localizer localizer = Localizer.Load(locator);
var wanted = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);
foreach (LanguageDefinition language in localizer.Languages)
{
    if (language.Id.Equals(Localizer.SourceLanguageId, StringComparison.OrdinalIgnoreCase)) continue;
    if (wanted.Count > 0 && !wanted.Contains(language.Id)) continue;
    findings += CheckLanguage(language, sources);
}

Console.WriteLine(findings == 0 ? "Alles übersetzt." : $"{findings} Befund(e).");
return findings == 0 ? 0 : 1;

// ------------------------------------------------------------------ Code
static void CollectFromCode(string sourceRoot, IDictionary<string, string> sources, ref int findings)
{
    // Loc.T( oder Loc.N( gefolgt von einem gewöhnlichen String-Literal. (?:[^"\\]|\\.)* = beliebige
    // Zeichen außer Anführungszeichen und Backslash, oder eine Escape-Folge wie \" oder \n.
    var literal = new Regex(@"Loc\.[TN]\(\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);
    // Ein interpolierter String als Schlüssel ($"…") findet nie eine Übersetzung – das ist ein Fehler.
    var interpolated = new Regex(@"Loc\.[TN]\(\s*\$", RegexOptions.Compiled);
    foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
    {
        if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
        string code = File.ReadAllText(file);
        string relative = Path.GetRelativePath(sourceRoot, file);
        foreach (Match match in literal.Matches(code))
            sources.TryAdd(Regex.Unescape(match.Groups[1].Value), $"{relative}:{LineOf(code, match.Index)}");
        foreach (Match match in interpolated.Matches(code))
        {
            Console.WriteLine($"INTERPOLIERT  {relative}:{LineOf(code, match.Index)} – Loc.T($\"…\") wird nie übersetzt, Werte als Argumente übergeben.");
            findings++;
        }
    }
}

static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

// ------------------------------------------------------------------ Inhaltsdaten
static void CollectFromData(DefinitionRegistry registry, IDictionary<string, string> sources)
{
    var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
    foreach (PropertyInfo property in typeof(DefinitionRegistry).GetProperties())
        Walk(property.GetValue(registry), $"Data/{property.Name}", sources, visited);
}

/// <summary>
/// Läuft rekursiv durch einen Objektbaum und sammelt jeden LocalizedText. So muss hier niemand
/// pflegen, welche Felder übersetzbar sind – das steht allein am Typ in Definitions.cs.
/// </summary>
static void Walk(object? node, string path, IDictionary<string, string> sources, HashSet<object> visited)
{
    switch (node)
    {
        case null:
            return;
        case LocalizedText text:
            if (!text.IsEmpty) sources.TryAdd(text.Source, path);
            return;
        case string:
            return;
        case ControllerProfileDefinition profile:
            // Tastenbeschriftungen sind string (keine Übersetzung nötig für "A" oder "R1"), laufen
            // aber durch Loc.T – "Menü" und "Ansicht" brauchen deshalb einen Eintrag.
            foreach (string label in profile.Labels.Values.Where(label => label.Any(char.IsLetter) && label.Length > 2))
                sources.TryAdd(label, $"{path}/labels");
            return;
    }
    Type type = node.GetType();
    if (type.IsPrimitive || type.IsEnum || !visited.Add(node)) return;
    if (node is System.Collections.IEnumerable sequence)
    {
        int index = 0;
        foreach (object? element in sequence) Walk(element, $"{path}[{index++}]", sources, visited);
        return;
    }
    if (type.Namespace?.StartsWith("CirclesOfAsh", StringComparison.Ordinal) != true) return;
    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (property.GetIndexParameters().Length > 0) continue;
        string name = node is IDefinition definition ? $"{path}/{definition.Id}.{property.Name}" : $"{path}.{property.Name}";
        Walk(property.GetValue(node), name, sources, visited);
    }
}

// ------------------------------------------------------------------ Prüfung je Sprache
static int CheckLanguage(LanguageDefinition language, IReadOnlyDictionary<string, string> sources)
{
    int findings = 0;
    var missing = sources.Keys.Where(source => !language.Strings.TryGetValue(source, out string? value) || value.Length == 0).ToList();
    foreach (var (source, translation) in language.Strings)
    {
        if (!sources.ContainsKey(source))
        {
            Console.WriteLine($"[{language.Id}] VERWAIST     \"{source}\"");
            findings++;
            continue;
        }
        if (translation.Length == 0) continue;
        string expected = string.Join(" ", Placeholders(source)), actual = string.Join(" ", Placeholders(translation));
        if (expected != actual)
        {
            Console.WriteLine($"[{language.Id}] PLATZHALTER  \"{source}\" – erwartet {expected}, gefunden {actual}");
            findings++;
        }
    }
    if (missing.Count > 0)
    {
        Console.WriteLine($"[{language.Id}] FEHLT: {missing.Count} Texte. Zum Einfügen in Content/Lang/{language.Id}.json:");
        foreach (string source in missing)
            Console.WriteLine($"    {JsonSerializer.Serialize(source, JsonOptions.Readable)}: \"\",   // {sources[source]}");
        findings += missing.Count;
    }
    Console.WriteLine($"[{language.Id}] {language.Strings.Count} Einträge, {missing.Count} fehlen.");
    return findings;
}

/// <summary>Platzhalter wie {0}, {1:0.0} oder {name}, sortiert – die Reihenfolge darf sich ändern.</summary>
static IEnumerable<string> Placeholders(string text) =>
    Regex.Matches(text, @"\{[^{}]+\}").Select(match => Regex.Replace(match.Value, @":[^}]*", "")).OrderBy(token => token);

static string FindGameRoot()
{
    string directory = AppContext.BaseDirectory;
    for (int depth = 0; depth < 8; depth++)
    {
        string candidate = Path.Combine(directory, "src", "CirclesOfAsh");
        if (Directory.Exists(Path.Combine(candidate, "Content"))) return candidate;
        directory = Path.GetFullPath(Path.Combine(directory, ".."));
    }
    throw new DirectoryNotFoundException("Content-Verzeichnis nicht gefunden (starte aus dem Repo aus).");
}

/// <summary>JSON-Ausgabe ohne \u-Escapes für Umlaute – die Zeilen sollen lesbar einfügbar sein.</summary>
static class JsonOptions
{
    public static readonly JsonSerializerOptions Readable = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

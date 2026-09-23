namespace CirclesOfAsh.Core;

/// <summary>
/// Findet Dateien in mehreren "Content-Wurzeln" mit Priorität (Overlay-Prinzip wie bei Minecraft-Resource-Packs):
///   1. Mods/&lt;ModName&gt;/...   (höchste Priorität, alphabetisch absteigend -> "zz_" gewinnt)
///   2. Content/...               (Basisspiel)
/// Ein Mod muss also nur die Dateien enthalten, die er ersetzen oder ergänzen will.
/// </summary>
public sealed class ContentLocator
{
    private readonly IReadOnlyList<string> _rootsByPriority;   // Index 0 = höchste Priorität

    public ContentLocator(IReadOnlyList<string> rootsByPriority) => _rootsByPriority = rootsByPriority;

    public IReadOnlyList<string> Roots => _rootsByPriority;

    public static ContentLocator Discover(string baseDirectory)
    {
        var roots = new List<string>();
        string modsDirectory = Path.Combine(baseDirectory, "Mods");
        if (Directory.Exists(modsDirectory))
        {
            // Lambda "path => ..." = anonyme Funktion, die für jedes Element den Sortierschlüssel liefert
            roots.AddRange(Directory.GetDirectories(modsDirectory)
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
        }
        roots.Add(Path.Combine(baseDirectory, "Content"));
        return new ContentLocator(roots);
    }

    /// <summary>Datei mit der höchsten Priorität oder null.</summary>
    public string? TryResolve(string relativePath)
    {
        foreach (string root in _rootsByPriority)
        {
            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Wie <see cref="TryResolve"/>, wirft aber eine verständliche Ausnahme, wenn die Datei fehlt.</summary>
    public string Resolve(string relativePath) =>
        TryResolve(relativePath)
        ?? throw new FileNotFoundException($"'{relativePath}' wurde in keinem Content-Ordner gefunden.", relativePath);

    /// <summary>
    /// ALLE Vorkommen einer Datei, niedrigste Priorität zuerst. So können Daten "geschichtet"
    /// werden: Basis lädt zuerst, Mods überschreiben/ergänzen danach einzelne Einträge.
    /// </summary>
    public IEnumerable<string> FindAllLayered(string relativePath) =>
        Enumerable.Reverse(_rootsByPriority)
            .Select(root => Path.Combine(root, relativePath))
            .Where(File.Exists);
}

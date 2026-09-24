using CirclesOfAsh.Definitions;

namespace CirclesOfAsh.Progression;

/// <summary>
/// Zerlegt die Körpertypen aus <c>appearance.json</c> in zwei Achsen: Geschlecht und Statur.
///
/// In den Daten sind beide in EINER Liste verschmolzen (<c>m_average</c>, <c>f_athletic</c>, …),
/// und <see cref="CharacterAppearance.BodyType"/> ist der Index genau in diese Liste. Dieser
/// Katalog ändert daran nichts – er rechnet nur hin und her. Dadurch bleiben Datenformat,
/// Persistenz (<c>run_profile</c>-Schlüssel <c>body_type</c>) und <see cref="CharacterVisuals"/>
/// unverändert, und der Charakter-Editor kann trotzdem zwei getrennte Zeilen anbieten.
///
/// Erkennt der Katalog keine Präfixe (etwa weil ein Mod eigene IDs mitbringt), meldet
/// <see cref="HasGenders"/> false. Der Editor blendet die Geschlechtszeile dann aus und listet
/// unter "Statur" wieder alle Körpertypen – nichts bricht.
/// </summary>
public sealed class BodyTypeCatalog
{
    /// <summary>ID-Präfix, angezeigter Name und Sortierrang des Geschlechts.</summary>
    private static readonly (string Prefix, string Name)[] KnownGenders =
    {
        ("m_", "Männlich"),
        ("f_", "Weiblich"),
    };

    /// <summary>
    /// Reihenfolge der Statur auf der Skala von schwer nach trainiert – so, wie man sie im Editor
    /// durchblättern will. In der JSON-Datei stehen sie anders; die bleibt unangetastet.
    /// </summary>
    private static readonly string[] BuildOrder = { "heavy", "average", "athletic" };

    private readonly IReadOnlyList<AppearanceOptionDefinition> _bodyTypes;
    /// <summary>[Geschlecht][Statur] -> Index in <see cref="_bodyTypes"/>. -1 = Kombination fehlt.</summary>
    private readonly int[,] _lookup;

    public BodyTypeCatalog(IReadOnlyList<AppearanceOptionDefinition> bodyTypes)
    {
        _bodyTypes = bodyTypes;

        var genders = new List<string>();
        var builds = new List<string>();
        foreach (AppearanceOptionDefinition option in bodyTypes)
        {
            if (!TrySplit(option.Id, out string prefix, out string build)) continue;
            string gender = GenderName(prefix);
            if (!genders.Contains(gender)) genders.Add(gender);
            if (!builds.Contains(build)) builds.Add(build);
        }
        // Geschlechter in der Reihenfolge von KnownGenders, Staturen auf der Skala schwer->trainiert.
        Genders = KnownGenders.Select(known => known.Name).Where(genders.Contains).ToArray();
        Builds = BuildOrder.Where(builds.Contains).Concat(builds.Except(BuildOrder)).ToArray();

        _lookup = new int[Math.Max(1, Genders.Length), Math.Max(1, Builds.Length)];
        for (int g = 0; g < _lookup.GetLength(0); g++)
            for (int b = 0; b < _lookup.GetLength(1); b++) _lookup[g, b] = -1;

        for (int index = 0; index < bodyTypes.Count; index++)
        {
            if (!TrySplit(bodyTypes[index].Id, out string prefix, out string build)) continue;
            int g = Array.IndexOf(Genders, GenderName(prefix));
            int b = Array.IndexOf(Builds, build);
            if (g >= 0 && b >= 0) _lookup[g, b] = index;
        }
    }

    public string[] Genders { get; }
    /// <summary>Roh-Bezeichner der Statur (z. B. "athletic"). Anzeigename über <see cref="BuildName"/>.</summary>
    public string[] Builds { get; }

    /// <summary>Lassen sich die Daten überhaupt in zwei Achsen zerlegen?</summary>
    public bool HasGenders => Genders.Length > 1 && Builds.Length > 0;

    public string GenderName(int genderIndex) =>
        Genders.Length == 0 ? "" : Genders[CharacterVisuals.Wrap(genderIndex, Genders.Length)];

    public string BuildName(int buildIndex)
    {
        if (Builds.Length == 0) return "";
        string build = Builds[CharacterVisuals.Wrap(buildIndex, Builds.Length)];
        return build switch
        {
            "heavy" => "Kräftig",
            "average" => "Normal",
            "athletic" => "Trainiert",
            _ => build,
        };
    }

    /// <summary>Die zwei Achsen zurück in den gespeicherten Körpertyp-Index.</summary>
    public int ToBodyType(int genderIndex, int buildIndex)
    {
        if (!HasGenders) return CharacterVisuals.Wrap(buildIndex, Math.Max(1, _bodyTypes.Count));
        int g = CharacterVisuals.Wrap(genderIndex, Genders.Length);
        int b = CharacterVisuals.Wrap(buildIndex, Builds.Length);
        int index = _lookup[g, b];
        if (index >= 0) return index;

        // Diese Kombination gibt es nicht (unvollständige Daten): die nächste Statur nehmen, die
        // es für dieses Geschlecht gibt – lieber eine andere Statur als ein anderes Geschlecht.
        for (int step = 1; step < Builds.Length; step++)
        {
            int fallback = _lookup[g, CharacterVisuals.Wrap(b + step, Builds.Length)];
            if (fallback >= 0) return fallback;
        }
        return 0;
    }

    /// <summary>Gespeicherter Körpertyp-Index in die zwei Achsen zerlegen.</summary>
    public (int Gender, int Build) FromBodyType(int bodyType)
    {
        if (!HasGenders || _bodyTypes.Count == 0) return (0, bodyType);
        int index = CharacterVisuals.Wrap(bodyType, _bodyTypes.Count);
        if (TrySplit(_bodyTypes[index].Id, out string prefix, out string build))
        {
            int g = Array.IndexOf(Genders, GenderName(prefix));
            int b = Array.IndexOf(Builds, build);
            if (g >= 0 && b >= 0) return (g, b);
        }
        return (0, 0);
    }

    /// <summary>"f_athletic" -> ("f_", "athletic"). false, wenn die ID kein bekanntes Präfix hat.</summary>
    private static bool TrySplit(string id, out string prefix, out string build)
    {
        foreach (var (known, _) in KnownGenders)
        {
            if (!id.StartsWith(known, StringComparison.OrdinalIgnoreCase)) continue;
            prefix = known;
            build = id[known.Length..];
            return build.Length > 0;
        }
        prefix = "";
        build = "";
        return false;
    }

    private static string GenderName(string prefix) =>
        KnownGenders.First(known => known.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase)).Name;
}

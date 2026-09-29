// Seed-Sweep: Erzeugt Dungeons für viele Seeds und prüft sie (AGENT_PROGRESS 1.8).
// Aufruf: dotnet run --project tools/SeedSweep [-- <anzahl>]
// Lädt nur JSON-Definitionen, keine Spiel-Assets. Rückgabewert 0 = sauber, 1 = Befunde.
//
// Zwei Prüfungen je Verlies:
//   1. Erreichbarkeit  – kommt man vom Start aus in jeden Pflichtraum?
//   2. Rätsel          – stehen die Teile vollständig, ohne Reste eines verworfenen Rätsels,
//                        und sind sie erreichbar? Ein Hebel hinter einer Sperre versiegelt den
//                        Ausgang für immer, weil LeverPuzzle ALLE Hebel verlangt.
//
// Davor läuft eine Gegenprobe: Ein absichtlich zugemauerter Raum MUSS anschlagen. Ohne sie ginge
// ein Fehler in der Prüfung selbst als "alles in Ordnung" durch – genau das war hier schon einmal
// der Fall (die Flutfüllung startete auf der massiven Bodenkachel und lief nie los).
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Persistence;
using CirclesOfAsh.Progression;
using CirclesOfAsh.World;

int count = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 200;

string contentRoot = FindGameRoot();
var locator = ContentLocator.Discover(contentRoot);
var definitions = DefinitionRegistry.Load(locator);
string logPath = Path.Combine(Path.GetTempPath(), "seed-sweep.log");
Log.Initialize(logPath);

var saves = new InMemorySaveRepository();
var progression = new ProgressionService(definitions, saves);

if (!SelfTestDetectsWalledRoom(definitions, progression))
{
    Console.WriteLine("ABBRUCH: Die Gegenprobe schlägt nicht an – die Erreichbarkeitsprüfung misst nichts mehr.");
    return 1;
}

int dungeonsChecked = 0;
int reachabilityFindings = 0;
int puzzleFindings = 0;
// Wie oft musste der Generator sein gewürfeltes Rätsel verwerfen? Das ist der Pfad, auf dem
// verwaiste Rätselteile entstehen können – ohne die Zahl weiss man nicht, ob er überhaupt läuft.
var rolled = new Dictionary<string, int>();
var placed = new Dictionary<string, int>();
int fallbacks = 0;
var diagnostics = new List<string>();
// Über ALLE Kreise, nicht nur den ersten: Jeder Kreis hat in worlds.json seine eigene
// Rätselauswahl. Kreis 1 kennt weder "braziers" noch "mirrors" – die blieben sonst ungeprüft.
int circles = definitions.Worlds.Get("inferno").Circles.Count;
int dungeonsPerCircle = definitions.Balance.DungeonsPerCircle;
for (int runSeed = 1; runSeed <= count; runSeed++)
{
    var run = new RunState { Seed = runSeed * 17, WorldId = "inferno", CircleIndex = 0, DungeonIndex = 0 };
    for (int slot = 0; slot < circles * dungeonsPerCircle; slot++)
    {
        run.CircleIndex = slot / dungeonsPerCircle;
        run.DungeonIndex = slot % dungeonsPerCircle;
        DungeonPlan plan = progression.CreateDungeonPlan(run);
        DungeonLayout layout = new DungeonGenerator(definitions).Generate(plan);

        string rolledKey = string.IsNullOrEmpty(plan.PuzzleKey) ? "(keines)" : plan.PuzzleKey;
        string placedKey = layout.Puzzle?.Key ?? "(keines)";
        rolled[rolledKey] = rolled.GetValueOrDefault(rolledKey) + 1;
        placed[placedKey] = placed.GetValueOrDefault(placedKey) + 1;
        if (rolledKey != placedKey) fallbacks++;

        diagnostics.Clear();
        DungeonReachability.Check(layout, diagnostics);
        reachabilityFindings += diagnostics.Count;

        int puzzleStart = diagnostics.Count;
        CheckPuzzle(layout, definitions.Balance.PuzzleScaling, run.CircleIndex, diagnostics);
        CheckMendShrine(layout, plan, diagnostics);
        puzzleFindings += diagnostics.Count - puzzleStart;

        foreach (string line in diagnostics) Console.WriteLine($"  [{runSeed}/K{run.CircleIndex}/V{run.DungeonIndex}] {line}");
        dungeonsChecked++;
    }
}
Console.WriteLine($"Rätsel gewürfelt -> gesetzt ({fallbacks} Rückfälle auf ein anderes Rätsel):");
foreach (string key in rolled.Keys.Union(placed.Keys).OrderBy(key => key))
    Console.WriteLine($"  {key,-12} gewürfelt {rolled.GetValueOrDefault(key),5}   gesetzt {placed.GetValueOrDefault(key),5}");
Console.WriteLine($"{dungeonsChecked} Dungeons geprüft. "
                + $"Erreichbarkeit: {reachabilityFindings} Befunde, Rätsel: {puzzleFindings} Befunde. "
                + $"Details: {logPath}");
return reachabilityFindings + puzzleFindings == 0 ? 0 : 1;

/// <summary>
/// Welche Teile ein Rätsel aufstellen muss. Ein negativer Wert ist eine Mindestzahl statt einer
/// genauen: Die Hebel verteilen sich über das ganze Verlies, ihre Zahl hängt am Kreis.
/// </summary>
static Dictionary<string, int> ExpectedParts(string key, PuzzleScalingDefinition scaling, int circleIndex) => key switch
{
    "levers" => new Dictionary<string, int> { ["lever"] = -2 },
    "rune_order" => new Dictionary<string, int> { ["rune"] = Math.Clamp(scaling.RuneOrderLength, 2, 4), ["mural"] = 1 },
    "braziers" => new Dictionary<string, int> { ["brazier"] = 3 },
    "weights" => new Dictionary<string, int>
    {
        ["plate"] = scaling.WeightPlatesAt(circleIndex),
        ["block"] = scaling.WeightPlatesAt(circleIndex) - 1,   // immer genau einer weniger
    },
    "mirrors" => new Dictionary<string, int>
    {
        ["beam_source"] = 1, ["mirror"] = 3, ["mirror_fixed"] = 3, ["beam_target"] = 1,
    },
    _ => new Dictionary<string, int>(),
};

/// <summary>Alle Tags, die überhaupt zu einem Rätsel gehören – für die Suche nach Resten.</summary>
static string[] AllPuzzleTags() => new[]
{
    "lever", "rune", "mural", "brazier", "plate", "block", "beam_source", "mirror", "mirror_fixed", "beam_target",
};

static void CheckPuzzle(DungeonLayout layout, PuzzleScalingDefinition scaling, int circleIndex, List<string> diagnostics)
{
    var counts = new Dictionary<string, int>();
    foreach (PropPlacement prop in layout.Props) counts[prop.Tag] = counts.GetValueOrDefault(prop.Tag) + 1;

    if (layout.Puzzle is not { } spec)
    {
        // Kein Rätsel gesetzt: Dann darf auch kein Rätselteil herumstehen.
        foreach (string tag in AllPuzzleTags())
            if (counts.GetValueOrDefault(tag) > 0)
                diagnostics.Add($"RÄTSEL: keines gesetzt, aber {counts[tag]}x '{tag}' im Verlies (Rest eines verworfenen Rätsels).");
        return;
    }

    Dictionary<string, int> expected = ExpectedParts(spec.Key, scaling, circleIndex);
    if (expected.Count == 0)
    {
        diagnostics.Add($"RÄTSEL: unbekannter Schlüssel '{spec.Key}' – der Sweep kennt seine Teile nicht.");
        return;
    }

    foreach ((string tag, int want) in expected)
    {
        int have = counts.GetValueOrDefault(tag);
        if (want < 0 && have < -want) diagnostics.Add($"RÄTSEL '{spec.Key}': nur {have}x '{tag}', mindestens {-want} nötig.");
        else if (want > 0 && have != want) diagnostics.Add($"RÄTSEL '{spec.Key}': {have}x '{tag}', erwartet {want}.");
    }
    foreach (string tag in AllPuzzleTags())
        if (!expected.ContainsKey(tag) && counts.GetValueOrDefault(tag) > 0)
            diagnostics.Add($"RÄTSEL '{spec.Key}': {counts[tag]}x '{tag}' übrig – gehört zu einem anderen Rätsel.");

    // Erreichbarkeit jedes einzelnen Teils.
    bool[] visited = DungeonReachability.Flood(layout.Map, layout.PlayerSpawn);
    foreach (PropPlacement prop in layout.Props)
    {
        if (!expected.ContainsKey(prop.Tag)) continue;
        if (prop.Tag == "mirror_fixed") continue;   // hängt bewusst ausser Sprungweite an der Wand
        if (!DungeonReachability.IsSpotReached(visited, layout.Map, prop.BottomCenter))
            diagnostics.Add($"RÄTSEL '{spec.Key}': Teil '{prop.Tag}' #{prop.Index} in {prop.Room.OwnerKey} ist nicht erreichbar.");
    }
}

/// <summary>
/// Der Trauernde Engel ist der Weg zurueck zu einer Ruestung. Steht keiner im Verlies, gibt es
/// ihn faktisch nicht; stehen mehrere, ist der Verfall entwertet; steht er unerreichbar, ist er
/// eine Verhoehnung. Alle drei Faelle sind hier ein Befund.
/// </summary>
static void CheckMendShrine(DungeonLayout layout, DungeonPlan plan, List<string> diagnostics)
{
    int shrines = layout.Props.Count(prop => prop.Tag == "mend_shrine");
    if (plan.IsBossDungeon)
    {
        if (shrines > 0) diagnostics.Add($"ENGEL: {shrines} im Thronsaal – dort soll keiner stehen.");
        return;
    }
    if (shrines != 1)
    {
        diagnostics.Add($"ENGEL: {shrines} im Verlies, erwartet genau 1.");
        return;
    }
    bool[] visited = DungeonReachability.Flood(layout.Map, layout.PlayerSpawn);
    PropPlacement shrine = layout.Props.First(prop => prop.Tag == "mend_shrine");
    if (!DungeonReachability.IsSpotReached(visited, layout.Map, shrine.BottomCenter))
        diagnostics.Add($"ENGEL: steht in {shrine.Room.OwnerKey} und ist nicht erreichbar.");
}

/// <summary>
/// Gegenprobe: Ein Verlies, dessen Ausgangsraum zugemauert wurde, MUSS als Befund auftauchen.
/// Schlägt das nicht an, misst die Prüfung nichts mehr und der ganze Sweep ist wertlos.
/// </summary>
static bool SelfTestDetectsWalledRoom(DefinitionRegistry definitions, ProgressionService progression)
{
    var run = new RunState { Seed = 4711, WorldId = "inferno", CircleIndex = 0, DungeonIndex = 0 };
    DungeonPlan plan = progression.CreateDungeonPlan(run);
    DungeonLayout layout = new DungeonGenerator(definitions).Generate(plan);

    var clean = new List<string>();
    DungeonReachability.Check(layout, clean);
    if (clean.Count != 0)
    {
        Console.WriteLine($"  Gegenprobe: Seed 4711 hat schon unverändert {clean.Count} Befund(e) – nicht aussagekräftig.");
        return false;
    }

    RoomNode? exit = layout.Rooms.FirstOrDefault(room => room.Type == RoomType.Exit);
    if (exit is null) return false;
    for (int y = exit.TileBounds.Top; y < exit.TileBounds.Bottom; y++)
        for (int x = exit.TileBounds.Left; x < exit.TileBounds.Right; x++)
            layout.Map[x, y] = TileType.Solid;

    var walled = new List<string>();
    DungeonReachability.Check(layout, walled);
    if (walled.Count == 0) return false;
    Console.WriteLine($"  Gegenprobe bestanden: zugemauerter Ausgangsraum wird gemeldet ({walled.Count} Befund(e)).");
    return true;
}

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

/// <summary>In-Memory-Speicher: Der Seed-Sweep liest/schreibt keine echten Spielstände.</summary>
sealed class InMemorySaveRepository : ISaveRepository
{
    private MetaState _meta = new();
    public MetaState LoadMeta() => _meta;
    public void SaveMeta(MetaState meta) => _meta = meta;
    public RunState? LoadRun() => null;
    public void SaveRun(RunState run) { }
    public void DeleteRun() { }
    public GameSettings LoadSettings() => new();
    public void SaveSettings(GameSettings settings) { }
    public List<HubDecoPlacement> LoadHubDeco() => new();
    public void SaveHubDeco(IEnumerable<HubDecoPlacement> placements) { }
    public List<PetState> LoadPets() => new();
    public void SavePets(IEnumerable<PetState> pets) { }
    public Dictionary<string, int> LoadCollectibles() => new();
    public void SaveCollectibles(IReadOnlyDictionary<string, int> counts) { }
}

// Seed-Sweep: Erzeugt Dungeons für viele Seeds und prüft Erreichbarkeit (AGENT_PROGRESS 1.8).
// Aufruf: dotnet run --project tools/SeedSweep [-- <anzahl>]
// Lädt nur JSON-Definitionen, keine Spiel-Assets.
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

int dungeonsChecked = 0;
List<string> diagnostics = new();
for (int runSeed = 1; runSeed <= count; runSeed++)
{
    var run = new RunState { Seed = runSeed * 17, WorldId = "inferno", CircleIndex = 0, DungeonIndex = 0 };
    for (int dungeon = 0; dungeon < 4; dungeon++)
    {
        run.DungeonIndex = dungeon;
        DungeonPlan plan = progression.CreateDungeonPlan(run);
        DungeonLayout layout = new DungeonGenerator(definitions).Generate(plan);
        int before = diagnostics.Count;
        DungeonReachability.Check(layout, diagnostics);
        foreach (string line in diagnostics.Skip(before)) Console.WriteLine($"  [{runSeed}/{dungeon}] {line}");
        dungeonsChecked++;
    }
}
Console.WriteLine($"{dungeonsChecked} Dungeons geprüft. Details: {logPath}");

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
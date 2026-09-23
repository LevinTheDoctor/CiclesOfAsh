using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Definitions;
using CirclesOfAsh.Modding;
using CirclesOfAsh.Persistence;
using CirclesOfAsh.Progression;

namespace CirclesOfAsh;

/// <summary>
/// "Composition Root": Hier (und nur hier) werden alle Dienste erzeugt und miteinander verbunden.
/// Szenen bekommen den Kontext übergeben, statt Dienste selbst zu erzeugen (Dependency Injection
/// per Konstruktor, ohne Framework). Wer z. B. das Savegame auf JSON umstellen will, ändert nur
/// eine Zeile in <see cref="Create"/>.
/// </summary>
public sealed class GameContext : IDisposable
{
    private readonly Action _requestExit;

    private GameContext(
        GraphicsDevice graphicsDevice,
        ContentLocator locator,
        AssetManager assets,
        AudioService audio,
        DefinitionRegistry definitions,
        BehaviorRegistry behaviors,
        ProgressionService progression,
        Action requestExit)
    {
        GraphicsDevice = graphicsDevice;
        Locator = locator;
        Assets = assets;
        Audio = audio;
        Definitions = definitions;
        Behaviors = behaviors;
        Progression = progression;
        _requestExit = requestExit;
    }

    public GraphicsDevice GraphicsDevice { get; }
    public ContentLocator Locator { get; }
    public AssetManager Assets { get; }
    public AudioService Audio { get; }
    public DefinitionRegistry Definitions { get; }
    public BehaviorRegistry Behaviors { get; }
    public ProgressionService Progression { get; }
    public InputState Input { get; } = new();
    public SceneManager Scenes { get; } = new();

    // Expression-bodied Properties ("=>"): reine Weiterleitung, kein eigenes Feld.
    public BitmapFont Font => Assets.GetFont("body");
    public BitmapFont TitleFont => Assets.GetFont("title");

    public static GameContext Create(GraphicsDevice graphicsDevice, Action requestExit)
    {
        // ApplicationData = %APPDATA% unter Windows, ~/.config unter Linux -> plattformneutral
        string saveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CirclesOfAsh");
        Directory.CreateDirectory(saveDirectory);
        Log.Initialize(Path.Combine(saveDirectory, "game.log"));

        var locator = ContentLocator.Discover(AppContext.BaseDirectory);
        var assets = AssetManager.Load(graphicsDevice, locator);
        var audio = new AudioService(locator, assets.Manifest.Sounds);
        var definitions = DefinitionRegistry.Load(locator);
        var behaviors = BehaviorRegistry.CreateWithBuiltIns();
        definitions.Validate(behaviors, assets);

        ISaveRepository saves = new SqliteSaveRepository(Path.Combine(saveDirectory, "save.db"));
        var progression = new ProgressionService(definitions, saves);

        Log.Info($"Content-Wurzeln: {string.Join(" | ", locator.Roots)}");
        return new GameContext(graphicsDevice, locator, assets, audio, definitions, behaviors, progression, requestExit);
    }

    public void RequestExit() => _requestExit();

    public void Dispose()
    {
        Audio.Dispose();
        Assets.Dispose();
    }
}

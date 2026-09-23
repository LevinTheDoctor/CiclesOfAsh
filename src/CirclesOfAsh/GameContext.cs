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
        MusicSystem music,
        DefinitionRegistry definitions,
        BehaviorRegistry behaviors,
        ProgressionService progression,
        ISaveRepository saves,
        Action requestExit)
    {
        GraphicsDevice = graphicsDevice;
        Locator = locator;
        Assets = assets;
        Audio = audio;
        Music = music;
        Definitions = definitions;
        Behaviors = behaviors;
        Progression = progression;
        Saves = saves;
        _requestExit = requestExit;
    }

    public GraphicsDevice GraphicsDevice { get; }
    public ContentLocator Locator { get; }
    public AssetManager Assets { get; }
    public AudioService Audio { get; }
    public MusicSystem Music { get; }
    public DefinitionRegistry Definitions { get; }
    public BehaviorRegistry Behaviors { get; }
    public ProgressionService Progression { get; }
    public ISaveRepository Saves { get; }
    public GameSettings Settings { get; private set; } = new();
    public InputState Input { get; } = new();
    public SceneManager Scenes { get; } = new();
    /// <summary>Haustier-Daten aller Begleitseelen (persistiert in SQLite, Migration V3).</summary>
    public List<PetState> Pets { get; private set; } = new();
    /// <summary>Heimwelt-Deko (persistiert in SQLite, Migration V3).</summary>
    public List<HubDecoPlacement> HubDeco { get; private set; } = new();
    /// <summary>Sammelobjekt-Zähler über alle Läufe (Schrein im Hub zeigt sie).</summary>
    public Dictionary<string, int> Collectibles { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dialogs.DialogService Dialogs { get; private set; } = null!;

    // Expression-bodied Properties ("=>"): reine Weiterleitung, kein eigenes Feld.
    public BitmapFont Font => Assets.GetFont("body");
    public BitmapFont TitleFont => Assets.GetFont("title");

    /// <summary>
    /// Ordner für Spielstand und Logdatei. ApplicationData = %APPDATA% unter Windows,
    /// ~/.config unter Linux, ~/Library/Application Support unter macOS – plattformneutral.
    /// Statisch, weil der Logger ihn schon vor <see cref="Create"/> braucht.
    /// </summary>
    public static string SaveDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CirclesOfAsh");

    /// <summary>Pfad der Logdatei. Wird als Erstes im Spielstart initialisiert.</summary>
    public static string LogFilePath => Path.Combine(SaveDirectory, "game.log");

    public static GameContext Create(GraphicsDevice graphicsDevice, Action requestExit)
    {
        string saveDirectory = SaveDirectory;
        Directory.CreateDirectory(saveDirectory);
        Log.Initialize(LogFilePath);   // idempotent: CirclesGame hat das beim Start schon erledigt

        var locator = ContentLocator.Discover(AppContext.BaseDirectory);
        var assets = AssetManager.Load(graphicsDevice, locator);
        var audio = new AudioService(locator, assets.Manifest.Sounds);
        var music = new MusicSystem(locator, assets.Manifest.Music);
        var definitions = DefinitionRegistry.Load(locator);
        var behaviors = BehaviorRegistry.CreateWithBuiltIns();
        definitions.Validate(behaviors, assets);

        ISaveRepository saves = new SqliteSaveRepository(Path.Combine(saveDirectory, "save.db"));
        var progression = new ProgressionService(definitions, saves);
        GameSettings settings = saves.LoadSettings();
        // 0 bedeutet jetzt "automatisch" und ist ein gueltiger, gespeicherter Wert – nicht mehr
        // "nie gesetzt". ScreenSetup waehlt daraus die groesste Stufe, die auf den Bildschirm passt.
        settings.Sanitize();

        var context = new GameContext(graphicsDevice, locator, assets, audio, music, definitions, behaviors, progression, saves, requestExit)
        {
            Settings = settings,
        };
        context.Pets = saves.LoadPets();
        context.HubDeco = saves.LoadHubDeco();
        context.Collectibles = saves.LoadCollectibles();
        context.Dialogs = new Dialogs.DialogService(context);
        CirclesOfAsh.Pets.PetService.EnsurePets(context);
        // Gespeicherte Schwierigkeit und Lautstärken sofort wirksam machen – sonst fiele beides
        // bei jedem Start auf die Voreinstellung zurück, obwohl es in der DB steht.
        progression.SetDifficulty(settings.DifficultyId);
        context.ApplyLiveSettings();
        context.Input.ControllerProfileResolver = context.ResolveControllerLabels;
        // Einmal schreiben, damit beim ersten Start die tatsächlich benutzten Werte in der DB
        // stehen (inklusive der Bildschirmgröße aus balance.json) statt einer leeren Tabelle.
        saves.SaveSettings(settings);
        return context;
    }

    public void RequestExit() => _requestExit();

    /// <summary>
    /// Sucht zum Gerätenamen eines Controllers das passende Profil aus controllers.json und
    /// übersetzt dessen Beschriftungen in GameActions. Kein Treffer -> das Auffangprofil (leeres "match").
    /// Public, damit der Steuerungs-Reiter im Optionsmenü das erkannte Profil anzeigen kann.
    /// </summary>
    public IReadOnlyDictionary<GameAction, string>? ResolveControllerLabels(string deviceName)
    {
        List<ControllerProfileDefinition> profiles = Definitions.ControllerProfiles.All.ToList();
        if (profiles.Count == 0) return null;

        string needle = deviceName.ToLowerInvariant();
        ControllerProfileDefinition profile =
            profiles.FirstOrDefault(candidate => candidate.Match.Any(token => needle.Contains(token.ToLowerInvariant())))
            ?? profiles.FirstOrDefault(candidate => candidate.Match.Count == 0)
            ?? profiles[0];
        Log.Info($"Controller erkannt: \"{deviceName}\" -> Profil '{profile.Id}' ({profile.Name}).");

        var labels = new Dictionary<GameAction, string>();
        foreach (var (actionName, label) in profile.Labels)
            if (Enum.TryParse(actionName, ignoreCase: true, out GameAction action))
                labels[action] = label;
        return labels;
    }

    /// <summary>Wird nach Änderungen im Optionsmenü aufgerufen: sofort hörbar machen und persistieren.</summary>
    public void SaveSettings()
    {
        Settings.Sanitize();
        ApplyLiveSettings();
        Saves.SaveSettings(Settings);
    }

    /// <summary>
    /// Reicht die Regler aus dem Optionsmenü an die Dienste weiter, die sie brauchen:
    /// Lautstärken an Audio/Musik, Vibrationsstärke an den Input (skaliert mit der Schwierigkeit).
    /// </summary>
    public void ApplyLiveSettings()
    {
        Audio.ApplySettings(Settings);
        Music.ApplySettings(Settings);
        Input.RumbleScale = Settings.RumbleIntensity * Progression.Difficulty.RumbleMultiplier;
    }

    /// <summary>Heimwelt-Deko speichern (nach jeder Platzierung/Löschung im Hub).</summary>
    public void SaveHubDeco() => Saves.SaveHubDeco(HubDeco);

    /// <summary>Sammelobjekt-Zähler aktualisieren und sichern (bei jedem Collectible-Fund).</summary>
    public void SaveCollectibles()
    {
        Saves.SaveCollectibles(Collectibles);
    }

    public void Dispose()
    {
        Music.Dispose();
        Audio.Dispose();
        Assets.Dispose();
    }
}

using CirclesOfAsh.Core;
using CirclesOfAsh.Scenes;

namespace CirclesOfAsh;

/// <summary>
/// Einstiegspunkt von MonoGame. Bewusst "dünn": Die Klasse kennt nur den Game-Loop
/// (Update/Draw) und delegiert alles an den <see cref="SceneManager"/>.
/// Pixel-Art-Trick: Es wird in eine kleine virtuelle Auflösung (480x270) gerendert und das
/// Ergebnis ganzzahlig hochskaliert -> scharfe, gleich große Pixel auf jedem Monitor.
/// </summary>
public sealed class CirclesGame : Game
{
    public const int VirtualWidth = 480;
    public const int VirtualHeight = 270;

    // Maximales Delta pro Frame. Verhindert, dass bei Rucklern (z. B. Fenster ziehen)
    // Objekte durch Wände "tunneln", weil sie in einem Schritt zu weit springen.
    private const float MaxDeltaSeconds = 1f / 30f;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;   // "null!" = Versprechen an den Compiler: wird in LoadContent gesetzt
    private RenderTarget2D _canvas = null!;
    private GameContext _context = null!;

    public CirclesGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = VirtualWidth * 3,
            PreferredBackBufferHeight = VirtualHeight * 3,
            SynchronizeWithVerticalRetrace = true,
        };
        Window.Title = "Circles of Ash";
        Window.AllowUserResizing = true;
        IsMouseVisible = false;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _canvas = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        // Methodengruppe "Exit" wird als Action übergeben -> Szenen können das Spiel beenden,
        // ohne die Game-Klasse zu kennen (lose Kopplung).
        _context = GameContext.Create(GraphicsDevice, Exit);
        // Der GraphicsDeviceManager gehört dieser Klasse. Szenen sollen ihn nicht kennen, brauchen
        // aber Zugriff auf Fenstergröße/Vollbild -> einmalig bei ScreenSetup hinterlegen.
        ScreenSetup.Register(_graphics);
        ScreenSetup.Apply(_context, _context.Settings);   // gespeicherte Bildschirm-Einstellungen sofort anwenden
        // Beim Beenden (Fenster schließen, Alt+F4, RequestExit) Meta-Fortschritt sichern:
        // Missionsfortschritt und Befreiungen gehen sonst seit dem letzten Speicherpunkt verloren.
        Exiting += OnExiting;
        Window.TextInput += OnTextInput;   // "+=" abonniert das Event: Zeichen landen im InputState
        _context.Scenes.Push(LoadingScene.ForStartup(_context));
    }

    private void OnExiting(object? sender, EventArgs args)
    {
        // Auch ohne Audiogerät/Lauf darf das nie abstürzen: Speichern ist wichtiger als ein sauberes Beenden.
        try
        {
            _context.Progression?.SaveMeta();
            _context.SaveSettings();
        }
        catch (Exception exception)
        {
            Log.Error($"Speichern beim Beenden fehlgeschlagen: {exception.Message}");
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs eventArgs) => _context.Input.OnTextInput(eventArgs.Character);

    protected override void Update(GameTime gameTime)
    {
        float deltaSeconds = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxDeltaSeconds);
        _context.Input.Update(deltaSeconds);
        _context.Music.Update(deltaSeconds);   // treibt die Überblendung zwischen zwei Stücken
        _context.Scenes.Update(deltaSeconds);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // 0) Szenen dürfen eigene Render-Targets vorbereiten (Lichtkarte), BEVOR die Leinwand gebunden wird
        _context.Scenes.PrepareDraw(_spriteBatch);

        // 1) Szene in die kleine Leinwand zeichnen
        GraphicsDevice.SetRenderTarget(_canvas);
        GraphicsDevice.Clear(Palette.Void);
        _context.Scenes.Draw(_spriteBatch);

        // 2) Leinwand pixelgenau (PointClamp = keine Weichzeichnung) aufs Fenster skalieren
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_canvas, ComputeLetterboxRectangle(), Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>Größter ganzzahliger Skalierungsfaktor, zentriert mit schwarzen Balken.</summary>
    private Rectangle ComputeLetterboxRectangle()
    {
        Rectangle window = GraphicsDevice.PresentationParameters.Bounds;
        int scale = Math.Max(1, Math.Min(window.Width / VirtualWidth, window.Height / VirtualHeight));
        int width = VirtualWidth * scale;
        int height = VirtualHeight * scale;
        return new Rectangle((window.Width - width) / 2, (window.Height - height) / 2, width, height);
    }

    protected override void UnloadContent()
    {
        Exiting -= OnExiting;
        Window.TextInput -= OnTextInput;
        _context.Dispose();
        _canvas.Dispose();
        _spriteBatch.Dispose();
        base.UnloadContent();
    }
}

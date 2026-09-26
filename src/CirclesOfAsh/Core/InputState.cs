using System.Text;
using Microsoft.Xna.Framework.Input;

namespace CirclesOfAsh.Core;

/// <summary>Abstrakte Spielaktionen statt fester Tasten -> Tastatur und Gamepad gleichzeitig, leicht umbelegbar.</summary>
public enum GameAction { Left, Right, Up, Down, Jump, Dash, AbilityOne, AbilityTwo, Interact, Confirm, Cancel, Pause, Randomize, Attack, Block }

/// <summary>Womit der Spieler zuletzt etwas getan hat. Steuert Mauszeiger und Tastenbeschriftungen.</summary>
public enum InputDevice { Keyboard, Gamepad, Mouse }

/// <summary>
/// Kapselt Tastatur, Gamepad und Texteingabe. Speichert den Zustand des aktuellen UND vorherigen Frames,
/// damit "gerade gedrückt" (Flanke) von "wird gehalten" unterschieden werden kann.
/// </summary>
public sealed class InputState
{
    private const float StickDeadZone = 0.35f;

    // Tupel (Keys[] Keys, Buttons[] Buttons) = leichtgewichtiger, benannter Datencontainer ohne eigene Klasse.
    // "new[] { ... }" = implizit typisiertes Array; der Compiler leitet Keys[] bzw. Buttons[] ab.
    private readonly Dictionary<GameAction, (Keys[] Keys, Buttons[] Buttons)> _bindings = new()
    {
        [GameAction.Left] = (new[] { Keys.A, Keys.Left }, new[] { Buttons.DPadLeft, Buttons.LeftThumbstickLeft }),
        [GameAction.Right] = (new[] { Keys.D, Keys.Right }, new[] { Buttons.DPadRight, Buttons.LeftThumbstickRight }),
        [GameAction.Up] = (new[] { Keys.W, Keys.Up }, new[] { Buttons.DPadUp, Buttons.LeftThumbstickUp }),
        [GameAction.Down] = (new[] { Keys.S, Keys.Down }, new[] { Buttons.DPadDown, Buttons.LeftThumbstickDown }),
        [GameAction.Jump] = (new[] { Keys.Space, Keys.K }, new[] { Buttons.A }),
        [GameAction.Dash] = (new[] { Keys.LeftShift, Keys.L }, new[] { Buttons.RightShoulder, Buttons.B }),
        [GameAction.AbilityOne] = (new[] { Keys.Q }, new[] { Buttons.X }),
        [GameAction.AbilityTwo] = (new[] { Keys.E }, new[] { Buttons.Y }),
        [GameAction.Interact] = (new[] { Keys.F, Keys.W }, new[] { Buttons.LeftShoulder, Buttons.DPadUp }),
        [GameAction.Confirm] = (new[] { Keys.Enter }, new[] { Buttons.A, Buttons.Start }),
        [GameAction.Cancel] = (new[] { Keys.Escape }, new[] { Buttons.B }),
        [GameAction.Pause] = (new[] { Keys.Escape, Keys.P }, new[] { Buttons.Start }),
        [GameAction.Randomize] = (new[] { Keys.F5 }, new[] { Buttons.Back }),
        // Manueller Nahkampf. Die Trigger sind frei, deshalb liegen Angriff und Block dort –
        // das ist die gewohnte Belegung und kollidiert mit keiner Faehigkeit.
        [GameAction.Attack] = (new[] { Keys.J }, new[] { Buttons.RightTrigger }),
        [GameAction.Block] = (new[] { Keys.K }, new[] { Buttons.LeftTrigger }),
    };

    private readonly StringBuilder _typedBuffer = new();
    private KeyboardState _currentKeys, _previousKeys;
    private GamePadState _currentPad, _previousPad;

    // Vibration: läuft als Restzeit-Timer. Solange er > 0 ist, liegt die gesetzte Stärke an.
    private float _rumbleTimer;
    private float _rumbleLow, _rumbleHigh;
    private bool _rumbleActive;

    /// <summary>
    /// Gesamtstärke der Vibration (Optionsmenü × Schwierigkeitsstufe). 0 schaltet sie ganz ab.
    /// Wird von GameContext gesetzt, damit InputState weder Settings noch Fortschritt kennen muss.
    /// </summary>
    public float RumbleScale { get; set; } = 0.6f;

    // Tastatur-Beschriftungen. Bewusst hier und nicht in JSON: die Tastenbelegung selbst steht
    // ebenfalls fest in _bindings – beides gehört zusammen.
    private static readonly Dictionary<GameAction, string> KeyboardLabels = new()
    {
        [GameAction.Jump] = "Leer", [GameAction.Dash] = "Umschalt",
        [GameAction.AbilityOne] = "Q", [GameAction.AbilityTwo] = "E",
        [GameAction.Interact] = "F", [GameAction.Confirm] = "Enter",
        [GameAction.Cancel] = "Esc", [GameAction.Pause] = "Esc", [GameAction.Randomize] = "F5",
        [GameAction.Attack] = "J", [GameAction.Block] = "K",
    };

    private IReadOnlyDictionary<GameAction, string>? _padLabels;
    private string _padName = "";

    private MouseState _currentMouse, _previousMouse;
    private bool _hasMouseBaseline;   // erster Frame: noch keine Vorher-Position, sonst "Maus bewegt"-Fehlalarm

    /// <summary>Zuletzt benutztes Eingabegerät. Wird jeden Frame aus der tatsächlichen Aktivität bestimmt.</summary>
    public InputDevice LastDevice { get; private set; } = InputDevice.Keyboard;

    /// <summary>
    /// Mausposition in der VIRTUELLEN Auflösung (480x270), nicht in Fensterpixeln. Umgerechnet über
    /// <see cref="CirclesGame.CanvasArea"/>, damit sie bei jeder Fenstergröße und im Vollbild auf
    /// dem liegt, was man sieht. Außerhalb der Leinwand (schwarze Balken) auch außerhalb 0..480/0..270.
    /// </summary>
    public Point MousePosition { get; private set; }

    /// <summary>true im ersten Frame des Drückens der linken Maustaste.</summary>
    public bool MouseWasPressed => _currentMouse.LeftButton == ButtonState.Pressed
                                   && _previousMouse.LeftButton == ButtonState.Released;

    public bool MouseIsDown => _currentMouse.LeftButton == ButtonState.Pressed;

    /// <summary>Mausrad seit dem letzten Frame: positiv = nach oben gedreht.</summary>
    public int ScrollDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    /// <summary>
    /// Liefert zu einem Gerätenamen die passenden Tastenbeschriftungen (Content/Data/controllers.json).
    /// Wird von GameContext gesetzt; so kommt InputState ohne Kenntnis der Definitionen aus.
    /// </summary>
    public Func<string, IReadOnlyDictionary<GameAction, string>?>? ControllerProfileResolver { get; set; }

    /// <summary>true, sobald ein Gamepad angeschlossen ist. Steuert, ob Glyphen oder Tasten angezeigt werden.</summary>
    public bool HasGamePad { get; private set; }

    /// <summary>Gerätename des angeschlossenen Controllers (leer = keiner). Für die Anzeige im Optionsmenü.</summary>
    public string CurrentPadName => HasGamePad ? _padName : "";

    /// <summary>Im aktuellen Frame getippte Zeichen (inkl. '\b' für Rücktaste). Für Namenseingaben.</summary>
    public string TypedText { get; private set; } = "";

    /// <summary>Wird vom Fenster-Event (Window.TextInput) aufgerufen – liefert Zeichen inkl. Umlauten und Tastaturlayout.</summary>
    public void OnTextInput(char character) => _typedBuffer.Append(character);

    public void Update(float deltaSeconds)
    {
        _previousKeys = _currentKeys;
        _previousPad = _currentPad;
        _previousMouse = _currentMouse;
        _currentKeys = Keyboard.GetState();
        _currentPad = GamePad.GetState(PlayerIndex.One);
        _currentMouse = Mouse.GetState();
        if (!_hasMouseBaseline)
        {
            // Ohne diesen Abgleich gilt der Sprung von (0,0) auf die echte Position als Bewegung
            // und der Mauszeiger blitzt beim Start kurz auf.
            _previousMouse = _currentMouse;
            _hasMouseBaseline = true;
        }
        MousePosition = ToVirtual(_currentMouse.Position);
        TypedText = _typedBuffer.ToString();
        _typedBuffer.Clear();
        UpdateRumble(deltaSeconds);
        UpdateControllerProfile();
        UpdateLastDevice();
    }

    /// <summary>Rechnet Fensterpixel in die virtuelle Auflösung um (Letterbox-Versatz und Maßstab).</summary>
    private static Point ToVirtual(Point windowPixel)
    {
        Rectangle canvas = CirclesGame.CanvasArea;
        if (canvas.Width <= 0 || canvas.Height <= 0) return Point.Zero;
        return new Point(
            (windowPixel.X - canvas.X) * CirclesGame.VirtualWidth / canvas.Width,
            (windowPixel.Y - canvas.Y) * CirclesGame.VirtualHeight / canvas.Height);
    }

    /// <summary>
    /// Erkennt, womit gerade gespielt wird. Bewusst nur bei echter Aktivität umschalten: Ein
    /// ruhender Controller darf den Mauszeiger nicht ausblenden und umgekehrt.
    /// </summary>
    private void UpdateLastDevice()
    {
        if (_currentMouse.Position != _previousMouse.Position || MouseIsDown || ScrollDelta != 0)
            LastDevice = InputDevice.Mouse;
        else if (_currentPad.IsConnected && _currentPad.PacketNumber != _previousPad.PacketNumber)
            LastDevice = InputDevice.Gamepad;
        else if (_currentKeys.GetPressedKeyCount() > 0)
            LastDevice = InputDevice.Keyboard;
    }

    /// <summary>
    /// Beschriftung einer Aktion für die Anzeige: bei angeschlossenem Controller die Taste des
    /// erkannten Profils ("A", "○", "L1"), sonst die Tastatur ("F", "Leer").
    /// </summary>
    public string Glyph(GameAction action)
    {
        if (HasGamePad && LastDevice == InputDevice.Gamepad
            && _padLabels is not null && _padLabels.TryGetValue(action, out string? label))
            return label;
        return KeyboardLabels.TryGetValue(action, out string? key) ? key : action.ToString();
    }

    /// <summary>Beschriftung in eckigen Klammern, wie sie über Interaktionspunkten steht: "[F]".</summary>
    public string Prompt(GameAction action) => $"[{Glyph(action)}]";

    /// <summary>Erkennt einen Wechsel des Controllers und holt das passende Profil genau dann neu.</summary>
    private void UpdateControllerProfile()
    {
        GamePadCapabilities capabilities = GamePad.GetCapabilities(PlayerIndex.One);
        HasGamePad = capabilities.IsConnected;
        string name = HasGamePad ? capabilities.DisplayName ?? "" : "";
        if (name == _padName) return;   // nichts geändert -> kein Nachschlagen

        _padName = name;
        _padLabels = HasGamePad ? ControllerProfileResolver?.Invoke(name) : null;
    }

    /// <summary>
    /// Lässt den Controller vibrieren. "low" ist der schwere Motor (dumpfes Grollen),
    /// "high" der leichte (feines Surren). Ein neuer, stärkerer Impuls überschreibt einen laufenden.
    /// Ohne Controller passiert schlicht nichts.
    /// </summary>
    public void Rumble(float low, float high, float seconds)
    {
        if (RumbleScale <= 0f || seconds <= 0f) return;
        float scaledLow = Math.Clamp(low * RumbleScale, 0f, 1f);
        float scaledHigh = Math.Clamp(high * RumbleScale, 0f, 1f);
        // Ein schwächerer Impuls darf einen laufenden starken nicht abwürgen.
        if (_rumbleTimer > 0f && scaledLow < _rumbleLow && scaledHigh < _rumbleHigh) return;
        _rumbleLow = scaledLow;
        _rumbleHigh = scaledHigh;
        _rumbleTimer = seconds;
    }

    private void UpdateRumble(float deltaSeconds)
    {
        if (_rumbleTimer > 0f)
        {
            _rumbleTimer -= deltaSeconds;
            SetVibration(_rumbleLow, _rumbleHigh);
            _rumbleActive = true;
        }
        else if (_rumbleActive)
        {
            SetVibration(0f, 0f);   // genau einmal ausschalten, nicht in jedem Frame
            _rumbleActive = false;
        }
    }

    private static void SetVibration(float low, float high)
    {
        // Ohne angeschlossenen Controller wirft MonoGame nicht, liefert aber false - beides ist uns egal.
        try
        {
            GamePad.SetVibration(PlayerIndex.One, low, high);
        }
        catch (Exception)
        {
            // Manche SDL-Treiber werfen bei Rumble ohne Haptik-Unterstützung. Kein Grund, das Spiel zu stören.
        }
    }

    public bool IsDown(GameAction action) => IsDown(action, _currentKeys, _currentPad);

    /// <summary>true nur im ersten Frame des Drückens (steigende Flanke).</summary>
    public bool WasPressed(GameAction action) =>
        IsDown(action, _currentKeys, _currentPad) && !IsDown(action, _previousKeys, _previousPad);

    public bool WasReleased(GameAction action) =>
        !IsDown(action, _currentKeys, _currentPad) && IsDown(action, _previousKeys, _previousPad);

    /// <summary>Rohe Tastenabfrage (Flanke) – z. B. im Namensfeld, wo W/A/S/D Buchstaben sein sollen.</summary>
    public bool WasKeyPressed(Keys key) => _currentKeys.IsKeyDown(key) && !_previousKeys.IsKeyDown(key);

    /// <summary>-1 (links) bis +1 (rechts). Analogstick hat Vorrang, falls ausgelenkt.</summary>
    public float Horizontal
    {
        get
        {
            float stick = _currentPad.ThumbSticks.Left.X;
            if (MathF.Abs(stick) > StickDeadZone) return stick;
            return (IsDown(GameAction.Right) ? 1f : 0f) - (IsDown(GameAction.Left) ? 1f : 0f);
        }
    }

    private bool IsDown(GameAction action, KeyboardState keys, GamePadState pad)
    {
        var (boundKeys, boundButtons) = _bindings[action];   // Dekonstruktion des Tupels in zwei Variablen
        return boundKeys.Any(key => keys.IsKeyDown(key)) || boundButtons.Any(button => pad.IsButtonDown(button));
    }
}

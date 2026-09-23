using System.Text;
using Microsoft.Xna.Framework.Input;

namespace CirclesOfAsh.Core;

/// <summary>Abstrakte Spielaktionen statt fester Tasten -> Tastatur und Gamepad gleichzeitig, leicht umbelegbar.</summary>
public enum GameAction { Left, Right, Up, Down, Jump, Dash, AbilityOne, AbilityTwo, Interact, Confirm, Cancel, Pause, Randomize }

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
        [GameAction.AbilityOne] = (new[] { Keys.J, Keys.Q }, new[] { Buttons.X }),
        [GameAction.AbilityTwo] = (new[] { Keys.I, Keys.E }, new[] { Buttons.Y }),
        [GameAction.Interact] = (new[] { Keys.F, Keys.W }, new[] { Buttons.LeftShoulder, Buttons.DPadUp }),
        [GameAction.Confirm] = (new[] { Keys.Enter }, new[] { Buttons.A, Buttons.Start }),
        [GameAction.Cancel] = (new[] { Keys.Escape }, new[] { Buttons.B }),
        [GameAction.Pause] = (new[] { Keys.Escape, Keys.P }, new[] { Buttons.Start }),
        [GameAction.Randomize] = (new[] { Keys.F5 }, new[] { Buttons.Back }),
    };

    private readonly StringBuilder _typedBuffer = new();
    private KeyboardState _currentKeys, _previousKeys;
    private GamePadState _currentPad, _previousPad;

    /// <summary>Im aktuellen Frame getippte Zeichen (inkl. '\b' für Rücktaste). Für Namenseingaben.</summary>
    public string TypedText { get; private set; } = "";

    /// <summary>Wird vom Fenster-Event (Window.TextInput) aufgerufen – liefert Zeichen inkl. Umlauten und Tastaturlayout.</summary>
    public void OnTextInput(char character) => _typedBuffer.Append(character);

    public void Update()
    {
        _previousKeys = _currentKeys;
        _previousPad = _currentPad;
        _currentKeys = Keyboard.GetState();
        _currentPad = GamePad.GetState(PlayerIndex.One);
        TypedText = _typedBuffer.ToString();
        _typedBuffer.Clear();
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

using Microsoft.Xna.Framework.Input;

namespace CirclesOfAsh.Core;

/// <summary>
/// Was eine Spielfigur von ihrer Eingabe braucht – nicht mehr. Drei Quellen erfüllen es:
///  * <see cref="InputState"/>: Tastatur und erster Controller gemeinsam (Einzelspiel, alle Menüs),
///  * <see cref="DeviceInput"/>: nur bestimmte Geräte (zwei Spieler an einem Rechner),
///  * die Netzwerk-Eingabe des Mitspielers (Online-Arena).
/// Strategy-Pattern: Der Spieler fragt nur die Schnittstelle, woher die Tasten kommen, weiß er nicht.
/// </summary>
public interface IPlayerInput
{
    /// <summary>-1 (links) bis +1 (rechts).</summary>
    float Horizontal { get; }
    bool IsDown(GameAction action);
    /// <summary>true nur im ersten Frame des Drückens (steigende Flanke).</summary>
    bool WasPressed(GameAction action);
    bool WasReleased(GameAction action);
}

/// <summary>Ein einzelnes Eingabegerät: die Tastatur oder einer von bis zu vier Controllern.</summary>
public enum InputDeviceId { Keyboard, Pad1, Pad2, Pad3, Pad4 }

/// <summary>
/// Eingabe aus genau den Geräten, die einem Spieler gehören – etwa "Tastatur" für Spieler 1 und
/// "Controller 1" für Spieler 2. Die Belegung ist dieselbe wie im <see cref="InputState"/>
/// (geteilte Tabelle, DRY); nur die Geräte sind getrennt, damit niemand die Figur des anderen lenkt.
/// </summary>
public sealed class DeviceInput : IPlayerInput
{
    private const int MaxPads = 4;

    private readonly bool _usesKeyboard;
    private readonly int[] _padIndices;
    private KeyboardState _keys, _previousKeys;
    private readonly GamePadState[] _pads = new GamePadState[MaxPads];
    private readonly GamePadState[] _previousPads = new GamePadState[MaxPads];

    public DeviceInput(IEnumerable<InputDeviceId> devices)
    {
        Devices = devices.Distinct().ToArray();
        _usesKeyboard = Devices.Contains(InputDeviceId.Keyboard);
        // Pad1..Pad4 liegen im Enum direkt hinter Keyboard -> "- Pad1" ergibt den Index 0..3.
        _padIndices = Devices.Where(device => device != InputDeviceId.Keyboard)
                             .Select(device => device - InputDeviceId.Pad1)
                             .ToArray();
    }

    public IReadOnlyList<InputDeviceId> Devices { get; }

    /// <summary>Einmal pro Frame, VOR dem Abfragen: aktueller Zustand wird zum vorherigen.</summary>
    public void Update()
    {
        _previousKeys = _keys;
        if (_usesKeyboard) _keys = Keyboard.GetState();
        foreach (int index in _padIndices)
        {
            _previousPads[index] = _pads[index];
            _pads[index] = GamePad.GetState((PlayerIndex)index);   // Cast int -> Enum: 0 = PlayerIndex.One
        }
    }

    public float Horizontal
    {
        get
        {
            // Ausgelenkter Stick hat Vorrang vor Steuerkreuz und Tasten – wie im InputState.
            foreach (int index in _padIndices)
            {
                float stick = _pads[index].ThumbSticks.Left.X;
                if (MathF.Abs(stick) > InputState.StickDeadZone) return stick;
            }
            return (IsDown(GameAction.Right) ? 1f : 0f) - (IsDown(GameAction.Left) ? 1f : 0f);
        }
    }

    public bool IsDown(GameAction action) => IsDown(action, _keys, _pads);

    public bool WasPressed(GameAction action) => IsDown(action, _keys, _pads) && !IsDown(action, _previousKeys, _previousPads);

    public bool WasReleased(GameAction action) => !IsDown(action, _keys, _pads) && IsDown(action, _previousKeys, _previousPads);

    private bool IsDown(GameAction action, KeyboardState keys, GamePadState[] pads)
    {
        // Eine Tastatur, die dem Spieler nicht gehört, zählt als "nichts gedrückt" (default-Zustand).
        KeyboardState ownKeys = _usesKeyboard ? keys : default;
        if (InputState.IsActionDown(action, ownKeys, default)) return true;
        foreach (int index in _padIndices)
            if (InputState.IsActionDown(action, default, pads[index])) return true;
        return false;
    }

    /// <summary>Ist ein Controller an diesem Platz angeschlossen?</summary>
    public static bool IsConnected(InputDeviceId device) =>
        device == InputDeviceId.Keyboard || GamePad.GetCapabilities((PlayerIndex)(device - InputDeviceId.Pad1)).IsConnected;

    /// <summary>Alle gerade nutzbaren Geräte: immer die Tastatur, dazu jeder angeschlossene Controller.</summary>
    public static IEnumerable<InputDeviceId> ConnectedDevices() =>
        Enum.GetValues<InputDeviceId>().Where(IsConnected);
}

/// <summary>
/// Eine Eingabe, die nie etwas drückt. Online läuft der Kampf während der Pause weiter – die eigene
/// Figur bekommt solange diese Eingabe, sonst würde das Blättern im Menü sie springen lassen.
/// </summary>
public sealed class IdleInput : IPlayerInput
{
    public static IdleInput Instance { get; } = new();

    public float Horizontal => 0f;
    public bool IsDown(GameAction action) => false;
    public bool WasPressed(GameAction action) => false;
    public bool WasReleased(GameAction action) => false;
}

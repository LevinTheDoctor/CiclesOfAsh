using CirclesOfAsh.Core;

namespace CirclesOfAsh.Networking;

/// <summary>
/// Die Tasten des Online-Gastes beim Gastgeber. Pro Frame des Gastgebers können null, eine oder
/// mehrere Eingaben ankommen: "gehalten" gilt der letzte Stand, gedrückte und losgelassene Tasten
/// werden bis zum Ende des Frames gesammelt (ODER-verknüpft). So geht kein kurzes Antippen verloren,
/// auch wenn zwei Eingaben im selben Frame eintreffen.
/// </summary>
public sealed class NetworkInput : IPlayerInput
{
    private ushort _held, _pressed, _released;
    private float _horizontal;

    public void Receive(InputFrame frame)
    {
        _held = frame.Held;
        _pressed |= frame.Pressed;     // "|=": Bits dazunehmen, vorhandene behalten
        _released |= frame.Released;
        _horizontal = frame.Horizontal;
    }

    /// <summary>Nach jedem Frame des Gastgebers: Die Flanken sind verbraucht.</summary>
    public void EndFrame()
    {
        _pressed = 0;
        _released = 0;
    }

    /// <summary>Verbindung weg: alles loslassen, sonst liefe die Figur endlos in eine Richtung.</summary>
    public void ReleaseAll()
    {
        _held = 0;
        _horizontal = 0f;
        EndFrame();
    }

    public float Horizontal => _horizontal;

    // Ein im selben Frame gedrücktes UND losgelassenes Antippen zählt in diesem Frame als gehalten.
    public bool IsDown(GameAction action) => Has(_held, action) || Has(_pressed, action);
    public bool WasPressed(GameAction action) => Has(_pressed, action);
    public bool WasReleased(GameAction action) => Has(_released, action);

    /// <summary>"&amp;" = bitweises UND: Ist das Bit dieser Aktion gesetzt?</summary>
    private static bool Has(ushort mask, GameAction action) => (mask & (1 << (int)action)) != 0;
}

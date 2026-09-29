using System.Net.Sockets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Progression;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Networking;

/// <summary>Was der Gast am Ende vom Gastgeber erfährt: Ergebnis (als Zahl von ArenaResult) und Kampfzeit.</summary>
public readonly record struct ArenaEnd(byte Result, float Seconds);

/// <summary>
/// Online-Arena, Seite des Gastes. Verbindet sich (ohne das Spiel anzuhalten), stellt sich mit dem
/// eigenen Kämpfer vor und empfängt danach Schnappschüsse, während er seine Tasten schickt.
///
/// Zustandsautomat: Connecting -> Handshaking -> Welcomed -> (Ready gesendet: Kampf).
/// Fehlschläge enden in Failed (keine Verbindung) oder Rejected (Gastgeber sagt nein).
/// </summary>
public sealed class ArenaClientSession : IDisposable
{
    public enum State { Connecting, Handshaking, Welcomed, Playing, Rejected, Failed, Closed }

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(6);

    private readonly ArenaFighter _ownFighter;
    private readonly Task<TcpClient> _connecting;
    private NetConnection? _connection;

    public ArenaClientSession(string address, ArenaFighter ownFighter)
    {
        _ownFighter = ownFighter;
        (string host, int port) = ParseAddress(address);
        Address = $"{host}:{port}";
        // Task = läuft im Hintergrund; die Lobby fragt jeden Frame nach, ob er fertig ist (kein await nötig).
        _connecting = ConnectAsync(host, port);
    }

    public State Current { get; private set; } = State.Connecting;
    public string Address { get; }
    public ArenaWelcome? Welcome { get; private set; }
    public RejectReason? Rejection { get; private set; }
    public bool IsConnected => _connection is { IsOpen: true };

    private static async Task<TcpClient> ConnectAsync(string host, int port)
    {
        var client = new TcpClient();
        using var timeout = new CancellationTokenSource(ConnectTimeout);
        try
        {
            // "await" gibt den Thread frei, bis die Verbindung steht oder die Zeit abläuft.
            await client.ConnectAsync(host, port, timeout.Token);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;   // "throw;" ohne Argument wirft dieselbe Ausnahme weiter (Stacktrace bleibt erhalten)
        }
    }

    /// <summary>
    /// "host", "host:port", "[IPv6]:port" oder eine nackte IPv6-Adresse. Ohne Port gilt der Standard.
    /// Tupel als Rückgabe: zwei benannte Werte ohne eigene Klasse.
    /// </summary>
    public static (string Host, int Port) ParseAddress(string text)
    {
        text = text.Trim();
        if (text.StartsWith('['))
        {
            int end = text.IndexOf(']');
            if (end > 0)
            {
                string host = text[1..end];   // Range: vom 2. Zeichen bis vor die Klammer
                string rest = text[(end + 1)..];
                return (host, rest.StartsWith(':') && int.TryParse(rest[1..], out int bracketPort) ? bracketPort : ArenaProtocol.DefaultPort);
            }
        }
        int colon = text.LastIndexOf(':');
        bool singleColon = colon > 0 && text.IndexOf(':') == colon;   // genau EIN Doppelpunkt -> Port angegeben
        if (singleColon && int.TryParse(text[(colon + 1)..], out int port) && port is > 0 and < 65536)
            return (text[..colon], port);
        return (text, ArenaProtocol.DefaultPort);
    }

    // ------------------------------------------------------------------ Lobby
    public void PollLobby()
    {
        switch (Current)
        {
            case State.Connecting when _connecting.IsCompleted:
                if (_connecting.IsCompletedSuccessfully)
                {
                    _connection = new NetConnection(_connecting.Result);
                    _connection.Send(MessageType.Hello, ArenaProtocol.Hello(_ownFighter));
                    Current = State.Handshaking;
                }
                else
                {
                    Log.Warn($"Arena: Verbindung zu {Address} fehlgeschlagen: {_connecting.Exception?.GetBaseException().Message}");
                    Current = State.Failed;
                }
                break;
            case State.Handshaking:
                while (_connection!.TryReceive(out NetMessage message))
                {
                    if (message.Type == MessageType.Welcome)
                    {
                        try
                        {
                            Welcome = ArenaProtocol.ReadWelcome(message.Payload);
                            Current = State.Welcomed;
                        }
                        catch (Exception exception) when (ArenaProtocol.IsMalformed(exception))
                        {
                            Log.Warn($"Arena: unlesbare Antwort des Gastgebers ({exception.Message}).");
                            Current = State.Failed;
                        }
                        return;
                    }
                    if (message.Type == MessageType.Reject)
                    {
                        Rejection = message.Payload.Length > 0 ? (RejectReason)message.Payload[0] : RejectReason.Busy;
                        Current = State.Rejected;
                        return;
                    }
                }
                if (!_connection.IsOpen) Current = State.Failed;
                break;
        }
    }

    /// <summary>Karte geprüft, los geht's.</summary>
    public void SendReady()
    {
        _connection?.Send(MessageType.Ready, Array.Empty<byte>());
        Current = State.Playing;
    }

    // ------------------------------------------------------------------ Kampf
    public void SendInput(InputFrame frame) => _connection?.Send(MessageType.Input, ArenaProtocol.Input(frame));

    /// <summary>
    /// Holt alles Angekommene ab: Schnappschüsse in <paramref name="frames"/> (älteste zuerst),
    /// dazu ggf. das Ende. false = Verbindung weg.
    /// </summary>
    public bool Receive(List<WorldMirrorFrame> frames, out ArenaEnd? end)
    {
        end = null;
        if (_connection is null) return false;
        try
        {
            while (_connection.TryReceive(out NetMessage message))
            {
                switch (message.Type)
                {
                    case MessageType.Snapshot:
                        frames.Add(ArenaProtocol.ReadSnapshot(message.Payload));
                        break;
                    case MessageType.End:
                        (byte result, float seconds) = ArenaProtocol.ReadEnd(message.Payload);
                        end = new ArenaEnd(result, seconds);
                        break;
                    case MessageType.Bye:
                        return end is not null;   // "Bye" direkt nach "End" ist ein sauberes Ende
                }
            }
        }
        catch (Exception exception) when (ArenaProtocol.IsMalformed(exception))
        {
            Log.Warn($"Arena: unlesbarer Schnappschuss – Verbindung gilt als gestört ({exception.Message}).");
            return false;
        }
        return _connection.IsOpen || end is not null;
    }

    public void Dispose()
    {
        if (Current == State.Closed) return;
        Current = State.Closed;
        _connection?.Send(MessageType.Bye, Array.Empty<byte>());
        _connection?.Dispose();
        if (_connection is not null) return;
        // Noch keine Verbindung übernommen: Kommt sie später doch zustande, gleich wieder schließen.
        // ContinueWith = "wenn der Task fertig ist, führe das aus" – ohne hier darauf zu warten.
        _connecting.ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully) task.Result.Dispose();
        });
    }
}

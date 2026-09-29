using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CirclesOfAsh.Assets;
using CirclesOfAsh.Core;
using CirclesOfAsh.Progression;
using CirclesOfAsh.Scenes;
using CirclesOfAsh.World;

namespace CirclesOfAsh.Networking;

/// <summary>
/// Online-Arena, Seite des Gastgebers. Lauscht auf einem TCP-Port, nimmt EINEN Gast an, wickelt den
/// Handschlag ab und bedient ihn danach im Kampf: Tasten empfangen, Schnappschüsse senden.
///
/// Zustandsautomat: Listening -> Handshaking -> Ready -> (Kampf). Die Lobby ruft
/// <see cref="PollLobby"/> in jedem Frame, bis <see cref="State.Ready"/> erreicht ist; danach
/// übernimmt die Arena-Szene (<see cref="PumpBeforeUpdate"/>, <see cref="SendSnapshot"/>).
/// </summary>
public sealed class ArenaHostSession : IDisposable
{
    public enum State { Listening, Handshaking, Ready, Closed }

    private readonly TcpListener _listener;
    private readonly ArenaWelcome _welcome;
    private readonly List<WorldEvent> _pendingEvents = new();
    private NetConnection? _guest;
    private DungeonWorld? _world;
    private AudioService? _audio;
    /// <summary>Nur Ereignisse WÄHREND eines Kampfschritts zählen – Menügeräusche des Gastgebers nicht.</summary>
    private bool _isRecording;

    public ArenaHostSession(ArenaWelcome welcome, int port = ArenaProtocol.DefaultPort)
    {
        _welcome = welcome;
        // Create: lauscht auf IPv4 UND IPv6 (Dual-Mode), sofern das System es kann.
        _listener = TcpListener.Create(port);
        _listener.Start();
        Log.Info($"Arena: warte auf Mitspieler an Port {Port}.");
    }

    public State Current { get; private set; } = State.Listening;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public ArenaFighter? GuestFighter { get; private set; }
    /// <summary>Die Tasten des Gastes – die Figur von Spieler 2 liest sie wie eine eigene Eingabe.</summary>
    public NetworkInput GuestInput { get; } = new();
    /// <summary>Zuletzt abgewiesen, weil die Version nicht passte (für einen Hinweis in der Lobby).</summary>
    public bool RejectedMismatch { get; private set; }
    public bool IsGuestConnected => _guest is { IsOpen: true };

    // ------------------------------------------------------------------ Lobby
    /// <summary>Einmal pro Frame, solange die Lobby wartet. Wartet selbst nie (kein Blockieren).</summary>
    public void PollLobby()
    {
        if (Current is State.Ready or State.Closed) return;
        if (_guest is null)
        {
            if (!_listener.Pending()) return;   // Pending: liegt eine Verbindung an? Dann blockiert Accept nicht.
            _guest = new NetConnection(_listener.AcceptTcpClient());
            Current = State.Handshaking;
            Log.Info($"Arena: Verbindung von {_guest.RemoteAddress}.");
            return;
        }

        while (_guest is not null && _guest.TryReceive(out NetMessage message))
        {
            try
            {
                if (HandleLobbyMessage(message)) return;
            }
            catch (Exception exception) when (ArenaProtocol.IsMalformed(exception))
            {
                Log.Warn($"Arena: unlesbare Nachricht von {_guest?.RemoteAddress} – Verbindung getrennt ({exception.Message}).");
                DropGuest();
            }
        }
        if (_guest is { IsOpen: false }) DropGuest();
    }

    /// <summary>Eine Nachricht im Handschlag. true = bereit, die Lobby kann den Kampf starten.</summary>
    private bool HandleLobbyMessage(NetMessage message)
    {
        NetConnection guest = _guest!;
        switch (message.Type)
        {
            case MessageType.Hello when ArenaProtocol.TryReadHello(message.Payload, out ArenaFighter? fighter):
                GuestFighter = fighter;
                guest.Send(MessageType.Welcome, ArenaProtocol.Welcome(_welcome));
                return false;
            case MessageType.Hello:
                Log.Warn($"Arena: {guest.RemoteAddress} hat eine andere Version – abgewiesen.");
                guest.Send(MessageType.Reject, new[] { (byte)RejectReason.VersionMismatch });
                RejectedMismatch = true;
                DropGuest();
                return false;
            case MessageType.Ready when GuestFighter is not null:
                Current = State.Ready;
                return true;
            case MessageType.Bye:
                DropGuest();
                return false;
            default:
                return false;
        }
    }

    /// <summary>Gast weg (oder abgewiesen): zurück zum Warten auf den nächsten.</summary>
    private void DropGuest()
    {
        _guest?.Dispose();
        _guest = null;
        GuestFighter = null;
        Current = State.Listening;
    }

    // ------------------------------------------------------------------ Kampf
    /// <summary>Hört Welt und Töne ab. Einmal zu Beginn des Kampfes.</summary>
    public void AttachWorld(DungeonWorld world, AudioService audio)
    {
        _listener.Stop();   // Der Platz ist besetzt: Weitere Gäste bekommen gar keine Verbindung mehr.
        _world = world;
        _audio = audio;
        _world.EventRecorded += Record;
        _audio.Played += RecordSound;
    }

    private void Record(WorldEvent worldEvent)
    {
        if (_isRecording) _pendingEvents.Add(worldEvent);
    }

    private void RecordSound(string soundId, float volume, float pitch) => Record(new SoundEvent(soundId, volume, pitch));

    /// <summary>
    /// Vor jedem Kampfschritt: Tasten des Gastes abholen. false = Gast weg (Verbindung zu oder "Bye").
    /// </summary>
    public bool PumpBeforeUpdate()
    {
        if (_guest is null) return false;
        while (_guest.TryReceive(out NetMessage message))
        {
            try
            {
                if (message.Type == MessageType.Input) GuestInput.Receive(ArenaProtocol.ReadInput(message.Payload));
                else if (message.Type == MessageType.Bye) return Disconnected();
            }
            catch (Exception exception) when (ArenaProtocol.IsMalformed(exception))
            {
                Log.Warn($"Arena: unlesbare Eingabe vom Gast – Kampf abgebrochen ({exception.Message}).");
                return Disconnected();
            }
        }
        if (!_guest.IsOpen) return Disconnected();
        _isRecording = true;
        return true;
    }

    private bool Disconnected()
    {
        GuestInput.ReleaseAll();
        return false;
    }

    /// <summary>Nach jedem Kampfschritt: Zustand und gesammelte Ereignisse an den Gast.</summary>
    public void SendSnapshot(DungeonWorld world)
    {
        _isRecording = false;
        GuestInput.EndFrame();
        if (_guest is not { IsOpen: true }) return;
        WorldMirrorFrame frame = world.CaptureMirror(_pendingEvents);
        _pendingEvents.Clear();
        // droppable: Bei Stau darf ein Schnappschuss entfallen, der nächste enthält wieder alles.
        _guest.Send(MessageType.Snapshot, ArenaProtocol.Snapshot(frame), droppable: true);
    }

    public void SendEnd(ArenaResult result, float seconds) =>
        _guest?.Send(MessageType.End, ArenaProtocol.End((byte)result, seconds));

    public void Dispose()
    {
        if (Current == State.Closed) return;
        Current = State.Closed;
        if (_world is not null) _world.EventRecorded -= Record;
        if (_audio is not null) _audio.Played -= RecordSound;
        _guest?.Send(MessageType.Bye, Array.Empty<byte>());
        _guest?.Dispose();
        try
        {
            _listener.Stop();
        }
        catch (SocketException)
        {
            // Schon gestoppt – egal.
        }
    }

    /// <summary>
    /// Die Adressen dieses Rechners im lokalen Netz (IPv4), damit der Gastgeber sie weitergeben
    /// kann. Über das Internet braucht es die öffentliche Adresse und eine Portfreigabe (README).
    /// </summary>
    public static IReadOnlyList<string> LocalAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                                  && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)   // SelectMany: Listen von Listen flach machen
                .Select(unicast => unicast.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return Array.Empty<string>();
        }
    }
}

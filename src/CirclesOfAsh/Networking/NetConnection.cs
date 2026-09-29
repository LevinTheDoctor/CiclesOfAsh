using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CirclesOfAsh.Core;

namespace CirclesOfAsh.Networking;

/// <summary>Nachrichtentypen der Online-Arena. Ein Byte statt Text: Jede Nachricht beginnt mit genau einem.</summary>
public enum MessageType : byte
{
    /// <summary>Gast -> Gastgeber: Protokoll- und Spielversion, eigener Kämpfer.</summary>
    Hello = 1,
    /// <summary>Gastgeber -> Gast: Kämpfer des Gastgebers, Gegner, Seed, Prüfsumme der Karte.</summary>
    Welcome = 2,
    /// <summary>Gastgeber -> Gast: abgelehnt (Grund als Code).</summary>
    Reject = 3,
    /// <summary>Gast -> Gastgeber: Karte erzeugt und geprüft, es kann losgehen.</summary>
    Ready = 4,
    /// <summary>Gast -> Gastgeber: Tasten dieses Frames.</summary>
    Input = 5,
    /// <summary>Gastgeber -> Gast: Zustand und Ereignisse eines Frames.</summary>
    Snapshot = 6,
    /// <summary>Gastgeber -> Gast: Der Kampf ist vorbei.</summary>
    End = 7,
    /// <summary>Beide Richtungen: Ich gehe.</summary>
    Bye = 8,
}

/// <summary>Eine empfangene Nachricht. "readonly record struct" = kleiner, unveränderlicher Werttyp.</summary>
public readonly record struct NetMessage(MessageType Type, byte[] Payload);

/// <summary>
/// Eine TCP-Verbindung mit Nachrichtenrahmen: [Länge int32][Typ byte][Nutzdaten].
///
/// Empfangen und Senden laufen in je einem Hintergrund-Thread; die Spielschleife legt nur in
/// Warteschlangen ab bzw. holt dort ab. So wartet das Spiel nie auf das Netz – ein langsamer
/// Mitspieler lässt den Gastgeber nicht ruckeln. <see cref="ConcurrentQueue{T}"/> ist threadsicher
/// ohne eigene Sperren (Producer/Consumer-Muster).
/// </summary>
public sealed class NetConnection : IDisposable
{
    /// <summary>Schutz vor kaputten oder böswilligen Längenangaben: mehr als 1 MiB ist nie gültig.</summary>
    private const int MaxMessageBytes = 1 << 20;   // "<<" = Bitverschiebung: 1 * 2^20
    /// <summary>
    /// Stehen so viele Nachrichten noch aus, werden weitere "verzichtbare" (Schnappschüsse)
    /// verworfen. Der nächste Schnappschuss enthält ohnehin den ganzen Zustand.
    /// </summary>
    private const int MaxQueuedDroppable = 6;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ConcurrentQueue<NetMessage> _inbox = new();
    private readonly ConcurrentQueue<byte[]> _outbox = new();
    private readonly SemaphoreSlim _outboxSignal = new(0);
    private int _queuedCount;
    // "volatile": Änderungen aus einem Thread sind in den anderen sofort sichtbar (kein Zwischenspeicher).
    private volatile bool _isOpen = true;

    public NetConnection(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;   // Nagle aus: kleine Pakete (Tasten, Schnappschüsse) sofort senden
        _stream = client.GetStream();
        RemoteAddress = client.Client.RemoteEndPoint is IPEndPoint endPoint ? endPoint.Address.ToString() : "?";
        new Thread(ReadLoop) { IsBackground = true, Name = "Arena-Empfang" }.Start();
        new Thread(WriteLoop) { IsBackground = true, Name = "Arena-Versand" }.Start();
    }

    public bool IsOpen => _isOpen;
    public string RemoteAddress { get; }

    /// <summary>Nächste empfangene Nachricht, falls eine da ist. Wartet nie.</summary>
    public bool TryReceive(out NetMessage message) => _inbox.TryDequeue(out message);

    /// <summary>
    /// Stellt eine Nachricht zum Versand ein. <paramref name="droppable"/>: darf bei Stau entfallen
    /// (Schnappschüsse). Rückgabe false = verworfen oder Verbindung schon zu.
    /// </summary>
    public bool Send(MessageType type, byte[] payload, bool droppable = false)
    {
        if (!_isOpen) return false;
        if (droppable && Volatile.Read(ref _queuedCount) >= MaxQueuedDroppable) return false;

        var frame = new byte[sizeof(int) + 1 + payload.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, sizeof(int)), 1 + payload.Length);   // Länge = Typ + Nutzdaten
        if (!BitConverter.IsLittleEndian) Array.Reverse(frame, 0, sizeof(int));          // immer Little-Endian auf der Leitung
        frame[sizeof(int)] = (byte)type;
        payload.CopyTo(frame, sizeof(int) + 1);
        _outbox.Enqueue(frame);
        Interlocked.Increment(ref _queuedCount);   // threadsicheres ++
        _outboxSignal.Release();
        return true;
    }

    /// <summary>Bequemlichkeit: Nutzdaten direkt mit einem BinaryWriter schreiben.</summary>
    public bool Send(MessageType type, Action<BinaryWriter> write, bool droppable = false)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer))
        {
            write(writer);
        }
        return Send(type, buffer.ToArray(), droppable);
    }

    private void ReadLoop()
    {
        try
        {
            using var reader = new BinaryReader(_stream, System.Text.Encoding.UTF8, leaveOpen: true);
            while (_isOpen)
            {
                int length = reader.ReadInt32();   // BinaryReader liest Little-Endian – passend zu Send
                if (length < 1 || length > MaxMessageBytes) throw new InvalidDataException($"Ungültige Nachrichtenlänge {length}.");
                var type = (MessageType)reader.ReadByte();
                byte[] payload = reader.ReadBytes(length - 1);
                if (payload.Length != length - 1) throw new EndOfStreamException();
                _inbox.Enqueue(new NetMessage(type, payload));
            }
        }
        catch (Exception exception) when (exception is IOException or EndOfStreamException or ObjectDisposedException
                                              or InvalidDataException or SocketException)
        {
            // "when" = Ausnahmefilter: nur diese Fälle bedeuten "Verbindung weg", alles andere ist ein Fehler.
            if (_isOpen) Log.Info($"Arena-Verbindung beendet: {exception.Message}");
        }
        finally
        {
            _isOpen = false;
        }
    }

    private void WriteLoop()
    {
        try
        {
            while (_isOpen)
            {
                // Wartet höchstens 200 ms: So merkt der Thread auch ein Schließen ohne neue Nachricht.
                if (!_outboxSignal.Wait(200)) continue;
                if (!_outbox.TryDequeue(out byte[]? frame)) continue;
                Interlocked.Decrement(ref _queuedCount);
                _stream.Write(frame);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or SocketException)
        {
            if (_isOpen) Log.Info($"Arena-Versand abgebrochen: {exception.Message}");
        }
        finally
        {
            _isOpen = false;
        }
    }

    /// <summary>
    /// Versucht, noch ausstehende Nachrichten (etwa "Bye") loszuwerden, und schließt dann.
    /// Wartet höchstens kurz – beim Verlassen soll das Spiel nicht hängen.
    /// </summary>
    public void Dispose()
    {
        for (int attempt = 0; attempt < 10 && Volatile.Read(ref _queuedCount) > 0 && _isOpen; attempt++)
            Thread.Sleep(10);
        _isOpen = false;
        try
        {
            _client.Close();
        }
        catch (SocketException)
        {
            // Beim Schließen darf nichts mehr stören.
        }
    }
}

namespace CirclesOfAsh.Core;

/// <summary>
/// Minimaler Logger (Konsole + Datei). Statisch, weil er von überall erreichbar sein muss und
/// keinen Zustand außer dem Dateipfad hat. Für größere Projekte: Microsoft.Extensions.Logging.
/// </summary>
public static class Log
{
    private static string? _logFilePath;   // "?" = nullable: vor Initialize gibt es keine Datei
    private static bool _isInitialized;
    private static readonly object FileLock = new();

    /// <summary>
    /// Legt die Logdatei an und leert sie. Mehrfach aufrufbar: Nur der ERSTE Aufruf leert, spätere
    /// tun nichts. Sonst würde ein späterer Aufruf (GameContext.Create) die Zeilen löschen, die
    /// schon vorher geschrieben wurden – etwa das Laden der Controller-Datenbank beim Start.
    /// </summary>
    public static void Initialize(string logFilePath)
    {
        if (_isInitialized) return;
        _isInitialized = true;
        _logFilePath = logFilePath;
        try { File.WriteAllText(logFilePath, string.Empty); }
        catch (IOException) { _logFilePath = null; }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        // $"..." = String-Interpolation, {Zeit:HH:mm:ss} = Formatangabe direkt im Platzhalter
        string line = $"[{DateTime.Now:HH:mm:ss}] {level}: {message}";
        Console.WriteLine(line);
        if (_logFilePath is null) return;
        lock (FileLock)   // lock: verhindert gleichzeitiges Schreiben aus mehreren Threads
        {
            try { File.AppendAllText(_logFilePath, line + Environment.NewLine); }
            catch (IOException) { /* Logging darf das Spiel nie abstürzen lassen */ }
        }
    }
}

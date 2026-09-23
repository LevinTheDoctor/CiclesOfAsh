namespace CirclesOfAsh.Core;

/// <summary>
/// Minimaler Logger (Konsole + Datei). Statisch, weil er von überall erreichbar sein muss und
/// keinen Zustand außer dem Dateipfad hat. Für größere Projekte: Microsoft.Extensions.Logging.
/// </summary>
public static class Log
{
    private static string? _logFilePath;   // "?" = nullable: vor Initialize gibt es keine Datei
    private static readonly object FileLock = new();

    public static void Initialize(string logFilePath)
    {
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

using System.Runtime.InteropServices;
using CirclesOfAsh.Core;

namespace CirclesOfAsh.Core;

/// <summary>
/// Lädt die SDL-Controller-Mapping-Datenbank (Content/gamecontrollerdb.txt) zur Laufzeit.
///
/// Hintergrund: MonoGame 3.8.2 lädt beim Start NUR eine eingebaute Kopie der DB aus der
/// Framework-DLL (GamePad.InitDatabase → GetManifestResourceStream, dekompiliert verifiziert)
/// und zwar über SDL_GameControllerAddMappingsFromRW. Ein Datei-Ladepunkt
/// (SDL_GameControllerAddMappingsFromFile) ist in MonoGames SDL-Build nicht enthalten –
/// wohl aber FromRW. Also lesen wir die Datei selbst und übergeben sie als Speicherquelle,
/// exakt wie MonoGame es für die eingebaute DB tut. SDL kombiniert beide Datenbanken:
/// Pads, die die eingebaute nicht kennt, werden durch die frische Datei erkannt.
///
/// Muss VOR dem ersten Öffnen eines Controllers laufen, also vor dem GraphicsDeviceManager-
/// Konstruktor (MonoGame öffnet Pads beim SDL_CONTROLLERDEVICEADDED-Event nach SDL_Init).
/// </summary>
public static class SdlControllerMappings
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate int AddMappingsFromRwDelegate(void* rw, int freesrc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void* RwFromMemDelegate(void* memory, int size);

    private static nint _nativeLibrary;
    private static RwFromMemDelegate? _rwFromMem;
    private static AddMappingsFromRwDelegate? _addMappings;

    /// <summary>
    /// Lädt die native SDL-Bibliothek. Pfad-Auflösung wie in MonoGame (FuncLoader.LoadLibraryExt,
    /// dekompiliert): direkt, macOS-Bündel ../Frameworks, runtimes/&lt;rid&gt;/native mit OS-Fallbacks.
    /// </summary>
    private static void ResolveFunctions()
    {
        if (_addMappings is not null) return;
        string libraryName = OperatingSystem.IsWindows() ? "SDL2.dll"
            : OperatingSystem.IsMacOS() ? "libSDL2.dylib"
            : "libSDL2-2.0.so.0";
        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        {
            Path.Combine(baseDirectory, libraryName),
            Path.Combine(baseDirectory, "..", "Frameworks", libraryName),        // macOS-App-Bündel
            Path.Combine(baseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", libraryName),
            Path.Combine(baseDirectory, "runtimes", "osx", "native", libraryName),   // MonoGame legt im Debug hier ab
            Path.Combine(baseDirectory, "runtimes", "osx-x64", "native", libraryName),
            Path.Combine(baseDirectory, "runtimes", "osx-arm64", "native", libraryName),
            Path.Combine(baseDirectory, "runtimes", "linux-x64", "native", libraryName),
            Path.Combine(baseDirectory, "runtimes", "linux-arm64", "native", libraryName),
        };
        foreach (string candidate in candidates)
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                _nativeLibrary = NativeLibrary.Load(candidate);
                _rwFromMem = Marshal.GetDelegateForFunctionPointer<RwFromMemDelegate>(
                    NativeLibrary.GetExport(_nativeLibrary, "SDL_RWFromMem"));
                _addMappings = Marshal.GetDelegateForFunctionPointer<AddMappingsFromRwDelegate>(
                    NativeLibrary.GetExport(_nativeLibrary, "SDL_GameControllerAddMappingsFromRW"));
                return;
            }
            catch (Exception exception)
            {
                Log.Warn($"SDL-Kandidat '{candidate}' scheiterte: {exception.Message.Split('\n')[0]}");
            }
        }
        throw new DllNotFoundException($"SDL-Bibliothek '{libraryName}' mit Mapping-Schnittstelle nicht gefunden.");
    }

    /// <summary>
    /// Lädt die Mapping-Datei, wenn vorhanden. Bewusst ohne Absturz bei Fehlern (dann gilt die
    /// eingebaute DB weiter), aber mit Log-Zeilen, damit sichtbar ist, was aktiv ist.
    /// </summary>
    public static unsafe void Load(string gamecontrollerDbPath)
    {
        if (!File.Exists(gamecontrollerDbPath))
        {
            Log.Warn($"Controller-Datenbank fehlt: {gamecontrollerDbPath} – es gilt nur die eingebaute.");
            return;
        }
        try
        {
            ResolveFunctions();
            // Datei in unveränderlichen Speicher laden (FromRW mit freesrc=1 übernimmt den Puffer
            // nicht – SDL liest synchron, wir halten den Span bis zum Aufruf-Ende am Leben).
            byte[] content = File.ReadAllBytes(gamecontrollerDbPath);
            fixed (byte* memory = content)
            {
                void* rw = _rwFromMem!(memory, content.Length);
                if (rw == null)
                {
                    Log.Warn("Controller-Datenbank: SDL_RWFromMem lieferte null – die eingebaute gilt weiter.");
                    return;
                }
                int added = _addMappings!(rw, 1);
                if (added >= 0)
                    Log.Info($"Controller-Datenbank geladen: {added} Mappings aus {Path.GetFileName(gamecontrollerDbPath)}.");
                else
                    Log.Warn($"Controller-Datenbank abgelehnt (Rückgabe {added}) – die eingebaute gilt weiter.");
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"Controller-Datenbank nicht ladbar: {exception.Message.Split('\n')[0]} – die eingebaute gilt weiter.");
        }
    }
}
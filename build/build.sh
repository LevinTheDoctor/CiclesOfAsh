#!/usr/bin/env bash
# Baut das Paket für das laufende System — ohne Argumente, ohne Nachfrage.
#   macOS   -> CirclesOfAsh.app (delegiert an build/macos-app.sh, keine Logik doppelt)
#   Linux   -> publish/linux-*/ mit Startskript und .desktop-Datei
#   Windows -> publish/win-x64/ (PowerShell nicht nötig; dotnet reicht)
# Für ausdrückliche Cross-Builds bleibt build/publish.sh (Standard: win-x64).
# Aufruf:  ./build/build.sh          oder   ./build/build.sh linux-x64
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# ---------- Runtime-Identifier bestimmen
OS="$(uname -s)"
ARCH="$(uname -m)"
RID="${1:-}"

if [ -z "$RID" ]; then
    case "$OS/$ARCH" in
        Darwin/arm64)  RID="osx-arm64" ;;
        Darwin/*)      RID="osx-x64" ;;
        Linux/aarch64) RID="linux-arm64" ;;
        Linux/*)       RID="linux-x64" ;;
        MINGW*/*|CYGWIN*/*|MSYS*/*)
            # Git-Bash/MSYS: uname -m meldet die Host-Architektur
            case "$ARCH" in
                arm64|aarch64) RID="win-arm64" ;;
                *)             RID="win-x64" ;;
            esac
            ;;
        *)
            echo "Unbekanntes System '$OS/$ARCH' – bitte RID als Argument übergeben (z. B. linux-x64)." >&2
            exit 1
            ;;
    esac
fi

echo "==> Baue Paket für $RID (erkannt: $OS/$ARCH)"

# ---------- Bauen pro System
case "$RID" in
    osx-arm64|osx-x64)
        exec bash "$ROOT/build/macos-app.sh" "$RID"
        ;;

    linux-x64|linux-arm64)
        OUT="$ROOT/publish/$RID"
        dotnet publish "$ROOT/src/CirclesOfAsh/CirclesOfAsh.csproj" -c Release -r "$RID" --self-contained true -o "$OUT"
        # Shell-Wrapper: führt die Binary aus, egal wo sie liegt (relativer Pfad zum Skript)
        cat > "$OUT/CirclesOfAsh.sh" <<'WRAP'
#!/usr/bin/env bash
DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$DIR/CirclesOfAsh" "$@"
WRAP
        chmod +x "$OUT/CirclesOfAsh.sh" "$OUT/CirclesOfAsh"
        # .desktop-Eintrag für Gnome/KDE-Anwendungsmenüs. Exec zeigt auf den Wrapper,
        # damit auch ein kopiertes Bündel mit relativen Content-Pfaden weiterläuft.
        cat > "$OUT/circlesofash.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Circles of Ash
Comment=Rogue-Light-Abstieg durch die Höllenkreise
Exec="$OUT/CirclesOfAsh.sh"
Path=$OUT
Terminal=false
Categories=Game;
DESKTOP
        echo "Fertig: $OUT (Binary, CirclesOfAsh.sh, circlesofash.desktop)"
        ;;

    win-x64|win-arm64)
        OUT="$ROOT/publish/$RID"
        dotnet publish "$ROOT/src/CirclesOfAsh/CirclesOfAsh.csproj" -c Release -r "$RID" --self-contained true -o "$OUT"
        echo "Fertig: $OUT\\CirclesOfAsh.exe"
        ;;

    *)
        echo "Unbekannter Runtime-Identifier '$RID' (erlaubt: osx-arm64, osx-x64, linux-x64, linux-arm64, win-x64, win-arm64)." >&2
        exit 1
        ;;
esac
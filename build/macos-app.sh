#!/usr/bin/env bash
# Baut ein fertiges macOS-Programmbündel (CirclesOfAsh.app) aus einem Self-contained-Publish.
# Beispiele:  ./build/macos-app.sh              -> Apple Silicon (osx-arm64)
#             ./build/macos-app.sh osx-x64      -> Intel
# Ergebnis:   publish/<RID>/CirclesOfAsh.app
#
# Das Bündel ist NICHT signiert. Beim ersten Start meldet sich Gatekeeper; der Weg dorthin steht
# in der README ("Rechtsklick -> Öffnen" bzw. xattr -dr com.apple.quarantine).
set -euo pipefail

RID="${1:-osx-arm64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/publish/$RID"
APP="$OUT/CirclesOfAsh.app"
VERSION="${VERSION:-1.0.0}"

case "$RID" in
  osx-arm64|osx-x64) ;;
  *) echo "Fehler: '$RID' ist kein macOS-Runtime-Identifier (osx-arm64 oder osx-x64)." >&2; exit 1 ;;
esac

# 1) Programm übersetzen. --self-contained: Spieler brauchen kein installiertes .NET.
echo "==> Publish für $RID"
rm -rf "$OUT"
dotnet publish "$ROOT/src/CirclesOfAsh/CirclesOfAsh.csproj" \
  -c Release -r "$RID" --self-contained true -o "$OUT/payload"

# 2) Icon bereitstellen (nur, wenn Pillow da ist - sonst bleibt das Bündel eben ohne Symbol).
if [ ! -f "$ROOT/build/icons/CirclesOfAsh.icns" ]; then
  echo "==> Icon fehlt, erzeuge es"
  python3 -c "import sys; sys.path.insert(0, '$ROOT/tools'); from assetgen import icons; icons.generate()" \
    || echo "    (übersprungen: Pillow nicht installiert)"
fi

# 3) Bündelstruktur nach Apples Vorgabe: Contents/{MacOS,Resources,Info.plist}
echo "==> Baue $APP"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
# Der gesamte Publish-Inhalt (Binary, Bibliotheken, Content/) wandert nach MacOS/,
# damit die relativen Content-Pfade des Spiels unverändert funktionieren.
cp -R "$OUT/payload/." "$APP/Contents/MacOS/"
rm -rf "$OUT/payload"
[ -f "$ROOT/build/icons/CirclesOfAsh.icns" ] && cp "$ROOT/build/icons/CirclesOfAsh.icns" "$APP/Contents/Resources/"

# Info.plist: macht aus dem Ordner eine "normale" Mac-App.
#   NSHighResolutionCapable            -> Retina-fähig; ohne das rendert macOS das ganze Fenster
#                                         unscharf im Kompatibilitätsmodus.
#   NSSupportsAutomaticGraphicsSwitching -> MacBooks mit zwei GPUs müssen nicht dauerhaft auf die
#                                         stromhungrige dedizierte Grafik umschalten.
#   Fenstergröße, freies Skalieren und Vollbild (grüner Knopf / Einstellungen) regelt das Spiel
#   selbst (CirclesGame + ScreenSetup) – dafür braucht das Bündel keine Sonderschlüssel.
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>                  <string>Circles of Ash</string>
    <key>CFBundleDisplayName</key>           <string>Circles of Ash</string>
    <key>CFBundleExecutable</key>            <string>CirclesOfAsh</string>
    <key>CFBundleIdentifier</key>            <string>de.circlesofash.game</string>
    <key>CFBundleVersion</key>               <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>    <string>$VERSION</string>
    <key>CFBundlePackageType</key>           <string>APPL</string>
    <key>CFBundleIconFile</key>              <string>CirclesOfAsh</string>
    <key>LSMinimumSystemVersion</key>        <string>12.0</string>
    <key>NSHighResolutionCapable</key>       <true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key> <true/>
    <key>LSApplicationCategoryType</key>     <string>public.app-category.games</string>
    <key>NSHumanReadableCopyright</key>      <string>MIT-Lizenz. Platzhalter-Assets: CC0.</string>
</dict>
</plist>
PLIST

chmod +x "$APP/Contents/MacOS/CirclesOfAsh"
# Das Änderungsdatum anfassen, damit der Finder das neue Icon sofort zeigt statt des alten aus dem Cache.
touch "$APP"

echo "Fertig: $APP"

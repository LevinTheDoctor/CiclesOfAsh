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
# Der Workflow reicht den Tag durch, also z. B. "v1.3.0".
#
# ACHTUNG, hier lag ein Fehler, der jede macOS-Veroeffentlichung abbrach: MSBuild liest
# Umgebungsvariablen als Properties, und aus VERSION wird damit die Property "Version".
# "v1.3.0" ist keine gueltige Versionsnummer -> schon "dotnet restore" bricht ab mit
# "'v1.3.0' is not a valid version string". Windows und Linux merkten nichts davon, weil der
# Workflow die Variable nur fuer den macOS-Schritt setzt.
# Deshalb: das "v" abschneiden, den Wert ausdruecklich uebergeben und die Variable aus der
# Umgebung des Aufrufs nehmen, damit sie nicht doch noch durchschlaegt.
VERSION="${VERSION:-1.0.0}"
VERSION="${VERSION#v}"
# Apple verlangt fuer CFBundleVersion rein Ziffern und Punkte - ein Vorabteil wie "-beta1"
# waere dort ungueltig, fuer .NET dagegen erlaubt.
PLIST_VERSION="${VERSION%%-*}"

case "$RID" in
  osx-arm64|osx-x64) ;;
  *) echo "Fehler: '$RID' ist kein macOS-Runtime-Identifier (osx-arm64 oder osx-x64)." >&2; exit 1 ;;
esac

# 1) Programm übersetzen. --self-contained: Spieler brauchen kein installiertes .NET.
echo "==> Publish für $RID"
rm -rf "$OUT"
env -u VERSION dotnet publish "$ROOT/src/CirclesOfAsh/CirclesOfAsh.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:Version="$VERSION" \
  -o "$OUT/payload"

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
    <key>CFBundleVersion</key>               <string>$PLIST_VERSION</string>
    <key>CFBundleShortVersionString</key>    <string>$PLIST_VERSION</string>
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

# Ad-hoc-Signatur ueber das ganze Buendel. Das Programm startet auch ohne sie - der Apphost bringt
# vom .NET-Build schon eine eigene mit, und genau die laesst der Kernel auf Apple Silicon gelten.
# Dem BUENDEL fehlt aber das _CodeSignature-Verzeichnis, weshalb "codesign --verify" meckert
# ("code has no resources but signature indicates they must be present"). Das sauber zu haben
# kostet eine Zeile und ist die Voraussetzung fuer eine spaetere echte Signatur samt Notarisierung.
# Bewusst nicht toedlich: Eine Veroeffentlichung soll nicht an einem kosmetischen Schritt scheitern.
if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP" 2>/dev/null \
    && echo "==> Buendel ad-hoc signiert" \
    || echo "    (Signieren uebersprungen - das Buendel laeuft trotzdem)"
fi
# Das Änderungsdatum anfassen, damit der Finder das neue Icon sofort zeigt statt des alten aus dem Cache.
touch "$APP"

echo "Fertig: $APP"

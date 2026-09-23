#!/usr/bin/env bash
# Plattformübergreifendes Publish. Standard: win-x64.
# Beispiele:  ./build/publish.sh            -> Windows
#             ./build/publish.sh linux-x64  -> Linux
#             ./build/publish.sh osx-arm64  -> macOS (Apple Silicon)
set -euo pipefail
RID="${1:-win-x64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet publish "$ROOT/src/CirclesOfAsh/CirclesOfAsh.csproj" -c Release -r "$RID" --self-contained true -o "$ROOT/publish/$RID"
echo "Fertig: $ROOT/publish/$RID"

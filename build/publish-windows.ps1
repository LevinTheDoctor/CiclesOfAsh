# Baut eine eigenständige Windows-Version (kein installiertes .NET beim Spieler nötig).
# Aufruf aus dem Repo-Root:  .\build\publish-windows.ps1
param(
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot "src/CirclesOfAsh/CirclesOfAsh.csproj"
$output   = Join-Path $repoRoot "publish/win-x64"

dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $output
# $LASTEXITCODE = Rückgabecode des letzten externen Programms. 0 = Erfolg, alles andere = Fehler.
# Nötig, weil $ErrorActionPreference nur PowerShell-Befehle abbricht, nicht externe Programme wie dotnet.
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build fehlgeschlagen (Exit-Code $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Fertig: $output\CirclesOfAsh.exe" -ForegroundColor Green

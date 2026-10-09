# Compile-checks the Unity presentation layer (Assets/LegitEnough/Scripts/Game) together with
# the simulation, against the same engine references Unity uses. Does not run anything.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/SimTests/compile-game.ps1
param(
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Data"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$out = Join-Path $env:TEMP "LegitEnoughGameCheck"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$dotnet = Join-Path $Unity "NetCoreRuntime\dotnet.exe"
$csc = Join-Path $Unity "DotNetSdkRoslyn\csc.dll"

# Reuse the reference list Unity generated for an engine-referencing package assembly.
$template = Get-ChildItem -Recurse -Filter "Unity.InputSystem.rsp" (Join-Path $root "Library\Bee") | Select-Object -First 1
if (-not $template) { Write-Host "No Unity build files yet (open the project in Unity once)." -ForegroundColor Yellow; exit 2 }
$refs = Get-Content $template.FullName | Where-Object { $_ -match '^-r:' -or $_ -match '^-define:' }
$inputSystem = Join-Path $root "Library\ScriptAssemblies\Unity.InputSystem.dll"

$sources = @()
$sources += Get-ChildItem -Recurse -Filter *.cs (Join-Path $root "Assets\LegitEnough\Scripts\Sim") | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -Recurse -Filter *.cs (Join-Path $root "Assets\LegitEnough\Scripts\Game") | ForEach-Object { $_.FullName }

$rsp = Join-Path $out "game.rsp"
$lines = @("-nologo", "-target:library", "-langversion:9.0", "-nullable:disable", "-warn:4", "-nowarn:1591,0649",
    "-nostdlib", "-out:`"$out\GameCheck.dll`"", "-r:`"$inputSystem`"")
$lines += $refs
$lines += $sources | ForEach-Object { "`"$_`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $dotnet $csc -noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "GAME COMPILE FAILED" -ForegroundColor Red; exit 1 }
Write-Host "Game layer compiles." -ForegroundColor Green

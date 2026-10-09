# Compile-checks the trailer capture scripts (unity/TrailerRig.cs, unity/TrailerRun.cs) against
# the game and simulation sources, with the same Sfx.cs patch trailer.ps1 applies to its copy.
# Seconds instead of a full Unity import. Does not run anything.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/Trailer/compile-check.ps1
param(
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Data"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$out = Join-Path $env:TEMP "LegitEnoughTrailerCheck"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$dotnet = Join-Path $Unity "NetCoreRuntime\dotnet.exe"
$csc = Join-Path $Unity "DotNetSdkRoslyn\csc.dll"
$template = Get-ChildItem -Recurse -Filter "Unity.InputSystem.rsp" (Join-Path $root "Library\Bee") | Select-Object -First 1
if (-not $template) { Write-Host "No Unity build files yet (open the project in Unity once)." -ForegroundColor Yellow; exit 2 }
$refs = Get-Content $template.FullName | Where-Object { $_ -match '^-r:' -or $_ -match '^-define:' }
$inputSystem = Join-Path $root "Library\ScriptAssemblies\Unity.InputSystem.dll"

$sfxSrc = Join-Path $root "Assets\LegitEnough\Scripts\Game\Audio\Sfx.cs"
$sfx = [IO.File]::ReadAllText($sfxSrc).Replace("internal static SfxPlayer Player;", "internal static SfxPlayer Player; public static Action<string, float, float> Hook;")
$patched = Join-Path $out "Sfx.cs"
[IO.File]::WriteAllText($patched, $sfx)

$sources = @()
$sources += Get-ChildItem -Recurse -Filter *.cs (Join-Path $root "Assets\LegitEnough\Scripts\Sim") | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -Recurse -Filter *.cs (Join-Path $root "Assets\LegitEnough\Scripts\Game") | Where-Object { $_.Name -ne "Sfx.cs" } | ForEach-Object { $_.FullName }
$sources += $patched
$sources += Join-Path $PSScriptRoot "unity\TrailerRig.cs"
$sources += Join-Path $PSScriptRoot "unity\TrailerRun.cs"

$rsp = Join-Path $out "trailer.rsp"
$lines = @("-nologo", "-target:library", "-langversion:9.0", "-nullable:disable", "-warn:4", "-nowarn:1591,0649",
    "-nostdlib", "-out:`"$out\TrailerCheck.dll`"", "-r:`"$inputSystem`"")
$lines += $refs
$lines += $sources | ForEach-Object { "`"$_`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $dotnet $csc -noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "TRAILER COMPILE FAILED" -ForegroundColor Red; exit 1 }
Write-Host "Trailer scripts compile." -ForegroundColor Green

# Builds the macOS player of the DEMO (Universal: Apple silicon + Intel, Mono), the Mac twin of build-demo.ps1.
# Same flow: copies the project to a throwaway folder and runs a batch-mode Unity there with
# Assets/LegitEnough/Editor/BuildDemo.cs (LE_BUILD_TARGET=mac), so your open Editor and project are never touched.
# The app lands in Builds/LegitEnoughDemoMac/Legit Enough.app. It refuses (exit 4) when GameRoot.DemoBuild
# is not true in source, and gives the app the store icon.
# Needs Unity's "Mac Build Support (Mono)" module (Unity Hub > Installs > Add modules, or
#   "Unity Hub.exe" -- --headless install-modules --version 6000.0.85f1 --module mac-mono).
# Windows has no "executable" permission, so the app's binary can't be marked executable here; upload the Mac
# depot with Tools/Build/steam/upload-demo.ps1, which sets it inside a Linux container (see that script).
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/Build/build-demo-mac.ps1 [-Work <folder>] [-Out <folder>]
param(
    [string]$Work = (Join-Path $env:TEMP "LegitEnoughBuildMac"),
    [string]$Out = "",
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Unity.exe",
    [int]$TimeoutMinutes = 40
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")

$gameRoot = Join-Path $root "Assets\LegitEnough\Scripts\Game\Boot\GameRoot.cs"
if (-not (Select-String -Path $gameRoot -SimpleMatch "public static bool DemoBuild = true;" -Quiet)) {
    Write-Host "REFUSED: GameRoot.DemoBuild is not 'true' in $gameRoot. The build is always the demo." -ForegroundColor Red
    exit 4
}
$macModule = Join-Path (Split-Path $UnityExe) "Data\PlaybackEngines\MacStandaloneSupport"
# The Hub's install needs an admin prompt; on 2026-10-29 the module went into a user-owned copy of the same
# editor instead (%LOCALAPPDATA%\UnityPortable\6000.0.85f1). Use that copy when the Hub editor lacks the module.
$portable = Join-Path $env:LOCALAPPDATA "UnityPortable\6000.0.85f1\Editor\Unity.exe"
if (-not (Test-Path $macModule) -and (Test-Path (Join-Path (Split-Path $portable) "Data\PlaybackEngines\MacStandaloneSupport"))) {
    $UnityExe = $portable
    $macModule = Join-Path (Split-Path $UnityExe) "Data\PlaybackEngines\MacStandaloneSupport"
    Write-Host "Using the editor copy with Mac Build Support: $UnityExe"
}
if (-not (Test-Path $macModule)) {
    Write-Host "Mac Build Support isn't installed for this Unity ($macModule missing)." -ForegroundColor Red
    exit 4
}
$icon = Join-Path $root "Steam\store\client_icon.ico"
if (-not (Test-Path $icon)) { Write-Host "App icon missing: $icon" -ForegroundColor Red; exit 4 }
$env:LE_BUILD_ICON = $icon
$env:LE_BUILD_TARGET = "mac"
if ($Out -eq "") { $Out = Join-Path $root "Builds\LegitEnoughDemoMac" }
$ci = Join-Path $Work "project"
New-Item -ItemType Directory -Force -Path $ci, $Out | Out-Null

foreach ($dir in @("Assets", "Packages", "ProjectSettings")) {
    robocopy (Join-Path $root $dir) (Join-Path $ci $dir) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
}

$app = Join-Path $Out "Legit Enough.app"
# A clean output folder, emptied only when it holds an earlier build (a mistyped -Out never wipes anything else).
if (Test-Path $app) { Get-ChildItem $Out -Force | Remove-Item -Recurse -Force }
$env:LE_BUILD_OUT = $app
$log = Join-Path $Work "build.log"
$unityArgs = @("-batchmode", "-projectPath", "`"$ci`"", "-buildTarget", "OSXUniversal",
    "-executeMethod", "LegitEnough.EditorTools.BuildDemo.Run", "-logFile", "`"$log`"")
$started = Get-Date
$proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    Stop-Process -Id $proc.Id -Force
    Write-Host "TIMED OUT (log: $log)" -ForegroundColor Red
    exit 3
}
$summary = Select-String -Path $log -Pattern "LEGIT ENOUGH BUILD:" | Select-Object -Last 1
if ($summary) { Write-Host $summary.Line }
$minutes = ((Get-Date) - $started).TotalMinutes
$binary = Join-Path $app "Contents\MacOS\Legit Enough"
if ($proc.ExitCode -ne 0 -or -not (Test-Path $binary)) {
    Write-Host ("BUILD FAILED (exit {0}, {1:0.0} min, log: {2})" -f $proc.ExitCode, $minutes, $log) -ForegroundColor Red
    exit 1
}
# A Universal binary starts with the fat-file magic CA FE BA BE.
$head = [System.IO.File]::ReadAllBytes($binary)[0..3] | ForEach-Object { $_.ToString("X2") }
$kind = if (($head -join "") -eq "CAFEBABE") { "Universal (Apple silicon + Intel)" } else { "single architecture (magic " + ($head -join "") + ")" }
$bytes = (Get-ChildItem $Out -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Built {0} ({1:0.0} MB, {2}, {3:0.0} min)" -f $app, ($bytes / 1MB), $kind, $minutes) -ForegroundColor Green

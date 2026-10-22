# Builds the Windows x64 player (the DEMO: GameRoot.DemoBuild defaults to true).
# Copies the project to a throwaway folder (like the smoke test) and runs a batch-mode Unity
# there with Assets/LegitEnough/Editor/BuildDemo.cs, so your open Editor and project are never
# touched. The player lands in Builds/LegitEnoughDemo/ under the project root (outside Assets).
# It refuses (exit 4) when GameRoot.DemoBuild is not true in source, and gives the exe the store icon.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/Build/build-demo.ps1 [-Work <folder>] [-Out <folder>]
param(
    [string]$Work = (Join-Path $env:TEMP "LegitEnoughBuild"),
    [string]$Out = "",
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe",
    [int]$TimeoutMinutes = 40
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")

# The player build is always the demo. BuildDemo.cs refuses otherwise; checking the source here
# too fails in a second instead of after the copy and the Unity import.
$gameRoot = Join-Path $root "Assets\LegitEnough\Scripts\Game\Boot\GameRoot.cs"
if (-not (Select-String -Path $gameRoot -SimpleMatch "public static bool DemoBuild = true;" -Quiet)) {
    Write-Host "REFUSED: GameRoot.DemoBuild is not 'true' in $gameRoot. The build is always the demo;" -ForegroundColor Red
    Write-Host "set it back to true (play the full game with Legit Enough > Play Full Game in Editor)." -ForegroundColor Red
    exit 4
}
# The exe's icon: every hand-drawn size in the store's .ico (BuildDemo imports them into the copy).
$icon = Join-Path $root "Steam\store\client_icon.ico"
if (-not (Test-Path $icon)) { Write-Host "App icon missing: $icon" -ForegroundColor Red; exit 4 }
$env:LE_BUILD_ICON = $icon
if ($Out -eq "") { $Out = Join-Path $root "Builds\LegitEnoughDemo" }
$ci = Join-Path $Work "project"
New-Item -ItemType Directory -Force -Path $ci, $Out | Out-Null

# The copy keeps its own Library between runs (same -Work folder), so only the first build imports everything.
foreach ($dir in @("Assets", "Packages", "ProjectSettings")) {
    robocopy (Join-Path $root $dir) (Join-Path $ci $dir) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
}

$exe = Join-Path $Out "Legit Enough.exe"
# A clean output folder: no stale files from an older build next to the new exe. Only a folder
# that holds an earlier build is emptied, so a mistyped -Out never wipes anything else.
if (Test-Path $exe) { Get-ChildItem $Out -Force | Remove-Item -Recurse -Force }
$env:LE_BUILD_OUT = $exe
$log = Join-Path $Work "build.log"
$unityArgs = @("-batchmode", "-projectPath", "`"$ci`"", "-buildTarget", "Win64",
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
if ($proc.ExitCode -ne 0 -or -not (Test-Path $exe)) {
    Write-Host ("BUILD FAILED (exit {0}, {1:0.0} min, log: {2})" -f $proc.ExitCode, $minutes, $log) -ForegroundColor Red
    exit 1
}
$bytes = (Get-ChildItem $Out -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Built {0} ({1:0.0} MB in {2}, {3:0.0} min)" -f $exe, ($bytes / 1MB), $Out, $minutes) -ForegroundColor Green
exit 0

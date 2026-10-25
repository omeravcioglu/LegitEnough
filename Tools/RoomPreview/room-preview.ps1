# Room art preview: copies the project to a throwaway folder (pass the same -Out again and the
# copy is reused, so later runs only recompile what changed) and runs RoomPreview.cs in batch
# mode. Writes the shop at tiers 1-3, day and night, as 480x270 PNGs to <Out>/shots.
# Your open Editor and project are never touched.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/RoomPreview/room-preview.ps1 -Out <folder>
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Unity.exe",
    [int]$TimeoutMinutes = 30
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$ci = Join-Path $Out "project"
$shots = Join-Path $Out "shots"
New-Item -ItemType Directory -Force -Path $ci, $shots | Out-Null

foreach ($dir in @("Assets", "Packages", "ProjectSettings")) {
    robocopy (Join-Path $root $dir) (Join-Path $ci $dir) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
}
$editorDir = Join-Path $ci "Assets\LegitEnoughCI\Editor"
New-Item -ItemType Directory -Force -Path $editorDir | Out-Null
Copy-Item (Join-Path $PSScriptRoot "RoomPreview.cs") $editorDir -Force
Remove-Item (Join-Path $shots "*") -Force -ErrorAction SilentlyContinue

$env:LE_ROOM_OUT = $shots
$log = Join-Path $Out "unity.log"
$unityArgs = @("-batchmode", "-projectPath", "`"$ci`"", "-executeMethod", "LegitEnoughCI.RoomPreview.Run", "-logFile", "`"$log`"")
$proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    Stop-Process -Id $proc.Id -Force
    Write-Host "TIMED OUT" -ForegroundColor Red
    exit 3
}
Write-Host "Unity exit code: $($proc.ExitCode)"
if (Test-Path (Join-Path $shots "error.txt")) { Get-Content (Join-Path $shots "error.txt") }
exit $proc.ExitCode

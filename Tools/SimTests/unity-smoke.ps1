# Copies the project to a throwaway folder and runs a batch-mode Unity there:
# Unity compiles + imports everything, then Tools/SimTests/UnitySmoke/SmokeTest.cs plays the
# DEMO end to end (title, settings, splash, the coached Day 1, the scripted Day 2, the locked
# FULL GAME screens, the demo end) and saves numbered screenshots. Your open Editor and project
# are never touched.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/SimTests/unity-smoke.ps1 -Out <folder>
# Exit:   0 = every step ran with no error log and no failed check, 1 = errors (see shots/errors.txt),
#         3 = Unity timed out.
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
Copy-Item (Join-Path $PSScriptRoot "UnitySmoke\SmokeTest.cs") $editorDir -Force
Remove-Item (Join-Path $shots "*") -Force -ErrorAction SilentlyContinue

$env:LE_SMOKE_OUT = $shots
$log = Join-Path $Out "unity.log"
$args = @("-batchmode", "-projectPath", "`"$ci`"", "-executeMethod", "LegitEnoughCI.SmokeTest.Run", "-logFile", "`"$log`"")
$proc = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    Stop-Process -Id $proc.Id -Force
    Write-Host "TIMED OUT" -ForegroundColor Red
    exit 3
}
$done = Select-String -Path (Join-Path $shots "log.txt") -Pattern "^DONE:" -ErrorAction SilentlyContinue | Select-Object -Last 1
if ($done) { Write-Host $done.Line }
Write-Host "Unity exit code: $($proc.ExitCode)"
exit $proc.ExitCode

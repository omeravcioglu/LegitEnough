# Copies the project to a throwaway folder and runs a batch-mode Unity there:
# Unity compiles + imports everything, then Tools/SimTests/UnitySmoke/SmokeTest.cs plays
# through Day 1 and saves screenshots. Your open Editor and project are never touched.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/SimTests/unity-smoke.ps1 -Out <folder>
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe",
    [int]$TimeoutMinutes = 20
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
Write-Host "Unity exit code: $($proc.ExitCode)"
exit $proc.ExitCode

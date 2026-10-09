# Trailer capture: copies the project to a throwaway folder, patches the copy so game time
# follows Time.captureDeltaTime (exact 60 fps steps) and every sound request is reported, then
# runs batch-mode Unity with TrailerRig.cs, which plays the scripted session and pipes every
# frame of each take into ffmpeg (lossless 1080p60) with a frame-stamped event log beside it.
# Your open Editor and project are never touched.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/Trailer/trailer.ps1 -Out <folder>
# Then:   python Tools/Trailer/make_trailer.py --takes <folder>/capture/takes
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe",
    [string]$Only = "",
    [int]$TimeoutMinutes = 75
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$ci = Join-Path $Out "project"
$capture = Join-Path $Out "capture"
New-Item -ItemType Directory -Force -Path $ci, $capture | Out-Null

foreach ($dir in @("Assets", "Packages", "ProjectSettings")) {
    robocopy (Join-Path $root $dir) (Join-Path $ci $dir) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
}

function Patch([string]$rel, [string]$find, [string]$replace) {
    $file = Join-Path $ci $rel
    $text = [IO.File]::ReadAllText($file)
    if (-not $text.Contains($find)) { throw "patch target not found in ${rel}: $find" }
    [IO.File]::WriteAllText($file, $text.Replace($find, $replace))
}

$sfx = "Assets\LegitEnough\Scripts\Game\Audio\Sfx.cs"
Patch $sfx "internal static SfxPlayer Player;" "internal static SfxPlayer Player; public static Action<string, float, float> Hook;"
Patch $sfx "if (Player != null) Player.PlayOneShot(id, volume, pitch);" "Hook?.Invoke(id.ToString(), volume, pitch); if (Player != null) Player.PlayOneShot(id, volume, pitch);"
Patch $sfx "int blips = Mathf.Clamp(text.Length / 7, 2, 9);" "int blips = Mathf.Clamp(text.Length / 7, 2, 9); Hook?.Invoke(""Voice"", blips, voicePitch);"
Patch $sfx "if (Player != null) Player.SetLoop(SfxPlayer.PrinterLoop, running, 0.35f);" "Hook?.Invoke(""Printer"", running ? 1f : 0f, 1f); if (Player != null) Player.SetLoop(SfxPlayer.PrinterLoop, running, 0.35f);"
Patch "Assets\LegitEnough\Scripts\Game\Boot\GameRoot.cs" "float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);" "float dt = Mathf.Min(Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime, 0.1f);"
Patch "Assets\LegitEnough\Scripts\Game\Photo\PortraitView.cs" "grainSeed = (int)(Time.unscaledTime * 12f);" "grainSeed = (int)(Time.time * 12f);"

$ciScripts = Join-Path $ci "Assets\LegitEnoughCI"
New-Item -ItemType Directory -Force -Path (Join-Path $ciScripts "Editor") | Out-Null
Copy-Item (Join-Path $PSScriptRoot "unity\TrailerRig.cs") $ciScripts -Force
Copy-Item (Join-Path $PSScriptRoot "unity\TrailerRun.cs") (Join-Path $ciScripts "Editor") -Force
Remove-Item (Join-Path $capture "*") -Recurse -Force -ErrorAction SilentlyContinue

$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$env:LE_TRAILER_OUT = $capture
$env:LE_FFMPEG = $ffmpeg
$env:LE_TRAILER_ONLY = $Only
$log = Join-Path $Out "unity.log"
$unityArgs = @("-batchmode", "-projectPath", "`"$ci`"", "-executeMethod", "LegitEnoughCI.TrailerRun.Run", "-logFile", "`"$log`"")
$proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    Stop-Process -Id $proc.Id -Force
    Write-Host "TIMED OUT" -ForegroundColor Red
    exit 3
}
Write-Host "Unity exit code: $($proc.ExitCode)"
exit $proc.ExitCode

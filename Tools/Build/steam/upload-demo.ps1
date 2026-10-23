# Uploads Builds/LegitEnoughDemo to Steam as a new build of Legit Enough Demo (app 5493490, depot 5493491).
# You run this yourself: SteamCMD asks for your Steam password and Steam Guard code in this window.
# Nothing goes live: the build lands in Steamworks > SteamPipe > Builds, where you set it live on "default".
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/Build/steam/upload-demo.ps1 -User <steam login> [-SteamCmd <path to steamcmd.exe>] [-Preview]
#   -Preview  dry run: SteamCMD scans and reports what it would upload, without uploading.
param(
    [Parameter(Mandatory = $true)][string]$User,
    [string]$SteamCmd = "C:\steamcmd\steamcmd.exe",
    [switch]$Preview
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$exe = Join-Path $root "Builds\LegitEnoughDemo\Legit Enough.exe"
if (-not (Test-Path $exe)) {
    Write-Host "No build at $exe. Run Tools/Build/build-demo.ps1 first." -ForegroundColor Red
    exit 4
}
if (-not (Test-Path $SteamCmd)) {
    Write-Host "SteamCMD not found at $SteamCmd." -ForegroundColor Red
    Write-Host "Download it from Valve (https://developer.valvesoftware.com/wiki/SteamCMD#Windows), unzip to C:\steamcmd,"
    Write-Host "run steamcmd.exe once so it updates itself, then run this script again (or pass -SteamCmd <path>)."
    exit 4
}

# The build description shows in Steamworks > SteamPipe > Builds: version from ProjectSettings plus the build time.
$version = (Select-String -Path (Join-Path $root "ProjectSettings\ProjectSettings.asset") -Pattern "^\s*bundleVersion: (.+)" -CaseSensitive).Matches[0].Groups[1].Value.Trim()
$built = (Get-Item $exe).LastWriteTime.ToString("yyyy-MM-dd HH:mm")
$desc = "Legit Enough Demo $version (built $built)"

# A filled-in copy of the script next to the original, so the relative ContentRoot still resolves.
$template = Join-Path $PSScriptRoot "app_build_5493490.vdf"
$script = Join-Path $PSScriptRoot "app_build_5493490.generated.vdf"
$vdf = (Get-Content $template -Raw) -replace '"Desc" "[^"]*"', ('"Desc" "' + $desc + '"')
if ($Preview) { $vdf = $vdf -replace '"Preview" "0"', '"Preview" "1"' }
Set-Content -Path $script -Value $vdf -Encoding ASCII

Write-Host "Uploading: $desc" -ForegroundColor Cyan
& $SteamCmd +login $User +run_app_build "$script" +quit
$code = $LASTEXITCODE
Remove-Item $script -ErrorAction SilentlyContinue
if ($code -ne 0) { Write-Host "SteamCMD failed (exit $code). Its log is in Builds\SteamPipeOutput." -ForegroundColor Red; exit $code }
if ($Preview) { Write-Host "Preview finished: nothing was uploaded." -ForegroundColor Green; exit 0 }
Write-Host "Uploaded. Next: Steamworks > Legit Enough Demo > SteamPipe > Builds, set this build live on 'default'," -ForegroundColor Green
Write-Host "then install the demo from your Steam library and play it for 5+ minutes (a checklist item)." -ForegroundColor Green

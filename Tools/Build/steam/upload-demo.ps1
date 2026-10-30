# Uploads the demo to Steam as one new build of Legit Enough Demo (app 5493490) with both depots:
#   5493491 Windows  <- Builds/LegitEnoughDemo     (Legit Enough.exe)
#   5493492 macOS    <- Builds/LegitEnoughDemoMac  (Legit Enough.app)
# You run this yourself: SteamCMD asks for your Steam password and Steam Guard code in this window.
# Nothing goes live: the build lands in Steamworks > SteamPipe > Builds, where you set it live on "default".
#
# By default it uploads from a Linux container (Docker Desktop must be running). Windows can't store the
# "executable" permission, so a Mac app uploaded with the Windows SteamCMD can't launch on players' Macs;
# the container copies the builds into Linux, sets the permissions and uploads with Linux SteamCMD
# (Tools/Build/steam/docker). The first run builds the image (a few minutes); the login is remembered in a
# Docker volume (legitenough-steamcmd), so Steam Guard asks only the first time.
#
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/Build/steam/upload-demo.ps1 -User <steam login> [options]
#   -Check        build the image, copy and set permissions, list the Mac binaries, upload nothing (no login).
#   -Preview      SteamCMD logs in and scans, but uploads nothing.
#   -WindowsOnly  only the Windows depot (Builds/LegitEnoughDemo).
#   -Native       Windows SteamCMD instead of Docker (-SteamCmd <path>); Windows depot only, because a Mac
#                 depot uploaded from Windows wouldn't launch.
#   -AllowUnsigned  upload the Mac app even though it isn't notarized (only for testing on a private branch;
#                 Steam requires notarized Mac apps, see Tools/Build/mac/notarize-mac.sh).
param(
    [Parameter(Mandatory = $true)][string]$User,
    [switch]$Check,
    [switch]$Preview,
    [switch]$WindowsOnly,
    [switch]$Native,
    [switch]$AllowUnsigned,
    [string]$SteamCmd = "C:\steamcmd\steamcmd.exe"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$builds = Join-Path $root "Builds"
$exe = Join-Path $builds "LegitEnoughDemo\Legit Enough.exe"
$app = Join-Path $builds "LegitEnoughDemoMac\Legit Enough.app"
$withMac = -not ($WindowsOnly -or $Native)

if (-not (Test-Path $exe)) { Write-Host "No Windows build at $exe. Run Tools/Build/build-demo.ps1 first." -ForegroundColor Red; exit 4 }
if ($withMac -and -not (Test-Path (Join-Path $app "Contents\MacOS\Legit Enough"))) {
    Write-Host "No Mac build at $app. Run Tools/Build/build-demo-mac.ps1 first, or pass -WindowsOnly." -ForegroundColor Red
    exit 4
}
# Steam requires new macOS apps to be notarized (partner.steamgames.com/doc/store/application/platforms).
# A notarized app has the Developer ID seal (_CodeSignature) and the stapled ticket (Contents/CodeResources):
# notarize-mac.sh makes one on a Mac and Tools/Build/mac/import-notarized.ps1 brings it back here.
if ($withMac -and -not ($Check -or $Preview -or $AllowUnsigned)) {
    $notarized = (Test-Path (Join-Path $app "Contents\_CodeSignature\CodeResources")) -and (Test-Path (Join-Path $app "Contents\CodeResources"))
    if (-not $notarized) {
        Write-Host "The Mac app isn't notarized (no stapled ticket in $app), and Steam requires notarized Mac apps." -ForegroundColor Red
        Write-Host "Notarize it first (Steam/store/DEMO_RELEASE.md, 'Signing and notarizing'), or pass -WindowsOnly." -ForegroundColor Red
        exit 4
    }
}

# The build description shows in Steamworks > SteamPipe > Builds. The version comes from the Mac app's
# Info.plist when there is one (it's the version that was built), otherwise from ProjectSettings.
$version = $null
$plist = Join-Path $app "Contents\Info.plist"
if ($withMac -and (Test-Path $plist) -and ((Get-Content $plist -Raw) -match '<key>CFBundleShortVersionString</key>\s*<string>([^<]+)</string>')) { $version = $Matches[1] }
if (-not $version) { $version = (Select-String -Path (Join-Path $root "ProjectSettings\ProjectSettings.asset") -Pattern "^\s*bundleVersion: (.+)" -CaseSensitive).Matches[0].Groups[1].Value.Trim() }
$built = (Get-Item $exe).LastWriteTime.ToString("yyyy-MM-dd HH:mm")
$desc = "Legit Enough Demo $version (" + $(if ($withMac) { "Windows + macOS" } else { "Windows" }) + ", Windows built $built)"

function Depot([string]$id, [string]$folder) {
    @"
		"$id"
		{
			"FileMapping"
			{
				"LocalPath" "$folder/*"
				"DepotPath" "."
				"recursive" "1"
			}
			"FileExclusion" "*.pdb"
			"FileExclusion" "*_BurstDebugInformation_DoNotShip*"
			"FileExclusion" "*_BackUpThisFolder_ButDontShipItWithYourGame*"
		}
"@
}

function BuildScript([string]$contentRoot, [string]$output) {
    $depots = (Depot "5493491" "LegitEnoughDemo")
    if ($withMac) { $depots += "`n" + (Depot "5493492" "LegitEnoughDemoMac") }
    @"
"AppBuild"
{
	"AppID" "5493490"
	"Desc" "$desc"
	"ContentRoot" "$contentRoot"
	"BuildOutput" "$output"
	"SetLive" ""
	"Preview" "$(if ($Preview) { '1' } else { '0' })"
	"Depots"
	{
$depots
	}
}
"@
}

if ($Native) {
    if (-not (Test-Path $SteamCmd)) {
        Write-Host "SteamCMD not found at $SteamCmd. Get it from https://developer.valvesoftware.com/wiki/SteamCMD#Windows" -ForegroundColor Red
        exit 4
    }
    $script = Join-Path $PSScriptRoot "app_build_5493490.generated.vdf"
    Set-Content -Path $script -Value (BuildScript ($builds + "\") (Join-Path $builds "SteamPipeOutput\")) -Encoding ASCII
    Write-Host "Uploading (Windows SteamCMD): $desc" -ForegroundColor Cyan
    & $SteamCmd +login $User +run_app_build "$script" +quit
    $code = $LASTEXITCODE
    Remove-Item $script -ErrorAction SilentlyContinue
} else {
    # Docker writes progress to stderr, which Windows PowerShell would turn into terminating errors under "Stop".
    $ErrorActionPreference = "Continue"
    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Docker isn't running. Start Docker Desktop and run this again (or pass -Native for a Windows-only upload)." -ForegroundColor Red
        exit 4
    }
    $image = "legitenough-steamcmd"
    docker image inspect $image *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Building the $image image (first run only)..." -ForegroundColor Cyan
        docker build -t $image (Join-Path $PSScriptRoot "docker")
        if ($LASTEXITCODE -ne 0) { Write-Host "Image build failed." -ForegroundColor Red; exit 1 }
    }
    $scripts = Join-Path $env:TEMP "legitenough-steampipe"
    New-Item -ItemType Directory -Force -Path $scripts | Out-Null
    $vdf = (BuildScript "/work/Builds/" "/work/output/") -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText((Join-Path $scripts "app_build.vdf"), $vdf, (New-Object System.Text.UTF8Encoding($false)))
    $mode = if ($Check) { "check" } else { "upload" }
    Write-Host $(if ($Check) { "Checking (no upload): $desc" } else { "Uploading (Linux SteamCMD in Docker): $desc" }) -ForegroundColor Cyan
    $tty = if ($Check) { @() } else { @("-it") }
    docker run --rm @tty -v "${builds}:/builds:ro" -v "${scripts}:/scripts:ro" -v "legitenough-steamcmd:/root/Steam" $image $User app_build.vdf $mode
    $code = $LASTEXITCODE
}

if ($code -ne 0) { Write-Host "Upload failed (exit $code)." -ForegroundColor Red; exit $code }
if ($Check) { Write-Host "Check finished: nothing was uploaded." -ForegroundColor Green; exit 0 }
if ($Preview) { Write-Host "Preview finished: nothing was uploaded." -ForegroundColor Green; exit 0 }
Write-Host "Uploaded. Next: Steamworks > Legit Enough Demo > SteamPipe > Builds, set this build live on 'default'." -ForegroundColor Green
Write-Host "Then tick 'App Bundles Are Notarized' (Application > General > Supported Operating Systems) and, in" -ForegroundColor Green
Write-Host "Steamworks Settings > Publish, publish the macOS settings (depot, launch option, supported OS)." -ForegroundColor Green

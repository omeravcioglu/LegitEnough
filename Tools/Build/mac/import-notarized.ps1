# Brings the notarized Mac app (made on a Mac by notarize-mac.sh) back into Builds/LegitEnoughDemoMac,
# so Tools/Build/steam/upload-demo.ps1 uploads the notarized one. The unsigned build is kept next to it.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/Build/mac/import-notarized.ps1 -Zip <LegitEnoughDemoMac-notarized.zip>
param(
    [Parameter(Mandatory = $true)][string]$Zip,
    [string]$Dest = ""
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
if ($Dest -eq "") { $Dest = Join-Path $root "Builds\LegitEnoughDemoMac" }
if (-not (Test-Path $Zip)) { Write-Host "No such file: $Zip" -ForegroundColor Red; exit 2 }

$tmp = Join-Path $env:TEMP ("le-notarized-" + [guid]::NewGuid().ToString("N"))
Expand-Archive -Path $Zip -DestinationPath $tmp
try {
    $app = Join-Path $tmp "Legit Enough.app"
    if (-not (Test-Path $app)) { Write-Host "The zip has no 'Legit Enough.app' at its top level." -ForegroundColor Red; exit 3 }

    # A zip made without ditto's --norsrc --noextattr carries extended attributes as "._name" AppleDouble
    # files (or a __MACOSX folder). Unpacked here they'd be extra files inside the sealed bundle, so drop them.
    $stray = @(Get-ChildItem $app -Recurse -Force -Filter "._*")
    if ($stray.Count -gt 0) {
        $stray | Remove-Item -Force
        Write-Host "Removed $($stray.Count) AppleDouble '._*' file(s) from the bundle."
    }

    # A Developer ID signature seals the bundle in _CodeSignature; stapling adds the ticket as Contents/CodeResources.
    $problems = @()
    if (-not (Test-Path (Join-Path $app "Contents\MacOS\Legit Enough"))) { $problems += "no executable in Contents/MacOS" }
    if (-not (Test-Path (Join-Path $app "Contents\_CodeSignature\CodeResources"))) { $problems += "not signed (no _CodeSignature)" }
    if (-not (Test-Path (Join-Path $app "Contents\CodeResources"))) { $problems += "no stapled notarization ticket (Contents/CodeResources)" }
    $plist = Get-Content (Join-Path $app "Contents\Info.plist") -Raw
    $appVersion = [regex]::Match($plist, "<key>CFBundleShortVersionString</key>\s*<string>([^<]+)</string>").Groups[1].Value
    $projVersion = (Select-String -Path (Join-Path $root "ProjectSettings\ProjectSettings.asset") -Pattern "^\s*bundleVersion: (.+)").Matches[0].Groups[1].Value.Trim()
    if ($appVersion -ne $projVersion) { $problems += "app version $appVersion, project version $projVersion" }
    if ($problems.Count -gt 0) {
        Write-Host ("Not imported: " + ($problems -join "; ")) -ForegroundColor Red
        exit 4
    }

    $current = Join-Path $Dest "Legit Enough.app"
    if (Test-Path $current) {
        $backup = $Dest + "-unsigned"
        if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        Move-Item $current $backup
        Write-Host "Kept the unsigned build in $backup"
    }
    New-Item -ItemType Directory -Force -Path $Dest | Out-Null
    Move-Item $app $Dest
    Write-Host "Imported the notarized Legit Enough.app ($appVersion) into $Dest" -ForegroundColor Green
    Write-Host "Next: upload both platforms with Tools/Build/steam/upload-demo.ps1, set the build live on 'default',"
    Write-Host "then tick 'App Bundles Are Notarized' (Steamworks > Application > General > Supported Operating Systems) and publish."
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
}

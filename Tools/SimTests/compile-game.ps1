# Compile-checks the Unity presentation layer (Assets/LegitEnough/Scripts/Game) together with
# the simulation, against the same engine references Unity uses. Does not run anything.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/SimTests/compile-game.ps1
param(
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Data"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$out = Join-Path $env:TEMP "LegitEnoughGameCheck"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$dotnet = Join-Path $Unity "NetCoreRuntime\dotnet.exe"
$csc = Join-Path $Unity "DotNetSdkRoslyn\csc.dll"

# Reuse the reference list Unity generated for an engine-referencing package assembly.
$template = Get-ChildItem -Recurse -Filter "Unity.InputSystem.rsp" (Join-Path $root "Library\Bee") | Select-Object -First 1
if (-not $template) { Write-Host "No Unity build files yet (open the project in Unity once)." -ForegroundColor Yellow; exit 2 }
$refs = Get-Content $template.FullName | Where-Object { $_ -match '^-r:' -or $_ -match '^-define:' }
# Unity prunes intermediate *.ref.dll files from Library/Bee; fall back to the built assembly in
# Library/ScriptAssemblies, and drop a reference that exists nowhere (it isn't needed here).
$refs = $refs | ForEach-Object {
    if ($_ -notmatch '^-r:"?([^"]+)"?$') { return $_ }
    $p = $Matches[1]
    $abs = if ([IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $root $p }
    if (Test-Path $abs) { return "-r:`"$abs`"" }
    $built = Join-Path $root ("Library\ScriptAssemblies\" + ([IO.Path]::GetFileName($p) -replace '\.ref\.dll$', '.dll'))
    if (Test-Path $built) { return "-r:`"$built`"" }
}
$inputSystem = Join-Path $root "Library\ScriptAssemblies\Unity.InputSystem.dll"

$sources = @()
$sources += Get-ChildItem -Recurse -Filter *.cs (Join-Path $root "Assets\LegitEnough\Scripts\Sim") | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -Recurse -Filter *.cs (Join-Path $root "Assets\LegitEnough\Scripts\Game") | ForEach-Object { $_.FullName }

$rsp = Join-Path $out "game.rsp"
$lines = @("-nologo", "-target:library", "-langversion:9.0", "-nullable:disable", "-warn:4", "-nowarn:1591,0649",
    "-nostdlib", "-out:`"$out\GameCheck.dll`"", "-r:`"$inputSystem`"")
$lines += $refs
$lines += $sources | ForEach-Object { "`"$_`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $dotnet $csc -noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "GAME COMPILE FAILED" -ForegroundColor Red; exit 1 }
Write-Host "Game layer compiles." -ForegroundColor Green

# Second pass: the editor-side scripts that drive the game (smoke test, store shots, room preview,
# the build script) compile together with Sim+Game against the same (editor) references. Otherwise an API
# change only shows up minutes into a batch Unity import. StoreShots reaches PixelFont.NoText by
# reflection (store-shots.ps1 patches it into its copy), so the unpatched sources compile as they are.
$editorSources = @()
$editorSources += Join-Path $root "Tools\SimTests\UnitySmoke\SmokeTest.cs"
$editorSources += Join-Path $root "Steam\store\harness\StoreShots.cs"
$editorSources += Join-Path $root "Tools\RoomPreview\RoomPreview.cs"
$editorDir = Join-Path $root "Assets\LegitEnough\Editor"
if (Test-Path $editorDir) { $editorSources += Get-ChildItem -Recurse -Filter *.cs $editorDir | ForEach-Object { $_.FullName } }
$rsp2 = Join-Path $out "editor.rsp"
$lines2 = @("-nologo", "-target:library", "-langversion:9.0", "-nullable:disable", "-warn:4", "-nowarn:1591,0649",
    "-nostdlib", "-out:`"$out\EditorCheck.dll`"", "-r:`"$inputSystem`"")
$lines2 += $refs
$lines2 += ($sources + $editorSources) | ForEach-Object { "`"$_`"" }
Set-Content -Path $rsp2 -Value $lines2 -Encoding UTF8

& $dotnet $csc -noconfig "@$rsp2"
if ($LASTEXITCODE -ne 0) { Write-Host "EDITOR SCRIPTS COMPILE FAILED (SmokeTest / StoreShots / RoomPreview / Assets/LegitEnough/Editor)" -ForegroundColor Red; exit 1 }
Write-Host "Smoke test, store shots, room preview and editor scripts compile." -ForegroundColor Green

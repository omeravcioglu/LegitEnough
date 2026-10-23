# Compiles the engine-free simulation (Assets/LegitEnough/Scripts/Sim) together with the
# tests in this folder using Unity's bundled Roslyn + .NET runtime, then runs them.
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/SimTests/run.ps1 [-Filter name] [-Unity <EditorDataPath>]
param(
    [string]$Filter = "",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Data"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$out = Join-Path $env:TEMP "LegitEnoughSimTests"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$dotnet = Join-Path $Unity "NetCoreRuntime\dotnet.exe"
$csc = Join-Path $Unity "DotNetSdkRoslyn\csc.dll"
$netstandard = Join-Path $Unity "NetStandard\ref\2.1.0\netstandard.dll"

$sources = @()
$sources += Get-ChildItem -Recurse -Filter *.cs (Join-Path $root "Assets\LegitEnough\Scripts\Sim") | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -Filter *.cs $PSScriptRoot | ForEach-Object { $_.FullName }

$rsp = Join-Path $out "build.rsp"
$lines = @("-nologo", "-nostdlib", "-langversion:9.0", "-nullable:disable", "-warn:4", "-nowarn:1591",
    "-target:exe", "-optimize-", "-debug:portable", "-out:`"$out\SimTests.dll`"", "-r:`"$netstandard`"")
$lines += $sources | ForEach-Object { "`"$_`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $dotnet $csc -noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }

$cfg = '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"},"rollForward":"LatestMajor"}}'
Set-Content -Path (Join-Path $out "SimTests.runtimeconfig.json") -Value $cfg -Encoding ASCII

& $dotnet (Join-Path $out "SimTests.dll") $Filter
exit $LASTEXITCODE

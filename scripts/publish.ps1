# Publish: .NET 8 self-contained single-file exe (win-x64)
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\DesktopNotepadWallpaper\DesktopNotepadWallpaper.csproj"
$outDir = Join-Path $root "publish"

if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet -and (Test-Path "C:\Program Files\dotnet\dotnet.exe")) {
    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
}
if (-not $dotnet) {
    throw "dotnet CLI not found"
}

& $dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o $outDir

Write-Host "Publish done: $outDir"
Get-ChildItem $outDir | Select-Object Name, Length

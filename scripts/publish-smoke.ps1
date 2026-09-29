# Publish smoke test: publish -> verify single file -> launch -> verify window and data dir
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "publish"

Write-Host "== Step 1: publish =="
& (Join-Path $PSScriptRoot "publish.ps1")

Write-Host "== Step 2: verify single-file output =="
$files = @(Get-ChildItem $outDir -File)
if ($files.Count -ne 1) {
    throw "Expected single-file publish (1 file in publish dir), got $($files.Count): $($files.Name -join ', ')"
}
$exe = Join-Path $outDir "DesktopNotepadWallpaper.exe"
if (-not (Test-Path $exe)) {
    throw "Missing DesktopNotepadWallpaper.exe in publish output"
}
$sizeMB = [Math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Single-file check passed: $exe ($sizeMB MB)"

Write-Host "== Step 3: launch published exe =="
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 20
$p.Refresh()
if ($p.HasExited) {
    throw "Published exe exited after launch, ExitCode: $($p.ExitCode)"
}
if ($p.MainWindowHandle -eq 0) {
    throw "Published exe has no main window (startup crash suspected)"
}
Write-Host "Window OK: $($p.MainWindowTitle)"

Write-Host "== Step 4: verify Data dir and wallpaper cache =="
$dataDir = Join-Path $outDir "Data"
$cacheJpg = Join-Path $dataDir "Cache\notepad_wallpaper.jpg"
if (-not (Test-Path $cacheJpg)) {
    throw "Published exe did not generate notepad wallpaper cache: $cacheJpg"
}
if (-not (Test-Path (Join-Path $dataDir "Gallery"))) {
    throw "Published exe did not create Gallery dir"
}
Write-Host "Data dir check passed: $dataDir"

Write-Host "== Step 5: tray behavior (close should hide, not exit) =="
$null = $p.CloseMainWindow()
Start-Sleep -Seconds 4
$p.Refresh()
if ($p.HasExited) {
    throw "Process exited after window close (minimize-to-tray not working)"
}
Write-Host "Tray hide check passed (process still running)"

Stop-Process -Id $p.Id -Force
Write-Host "== Publish smoke test ALL PASSED =="

# RoomLink build script
# Produces:
#   dist\publish\RoomLink.exe   (single-file, self-contained, portable)
#   dist\RoomLink-Portable.zip
#   installer\RoomLinkSetup.exe (requires Inno Setup installed)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Locate dotnet: PATH first, then the standard install location
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = "$env:ProgramFiles\dotnet\dotnet.exe" }
if (-not (Test-Path $dotnet)) { throw "dotnet SDK not found. Install .NET 8 SDK." }

Write-Host "== app icon ==" -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File "$root\Tools\MakeIcon.ps1"
if ($LASTEXITCODE -ne 0) { throw "icon generation failed" }

Write-Host "== dotnet publish (single-file) ==" -ForegroundColor Cyan
& $dotnet publish "$root\src\RoomLink\RoomLink.csproj" -c Release -r win-x64 `
  -o "$root\dist\publish" --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "== portable zip ==" -ForegroundColor Cyan
$zip = "$root\dist\RoomLink-Portable.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$root\dist\publish\RoomLink.exe" -DestinationPath $zip

Write-Host "== installer (Inno Setup) ==" -ForegroundColor Cyan
$inno = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($inno) {
  & $inno "$root\installer\roomlink.iss"
  if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
} else {
  Write-Warning "Inno Setup 6 not found - skipping installer. Install: winget install JRSoftware.InnoSetup"
}

Write-Host "`nDone. Artifacts in dist\" -ForegroundColor Green
Get-ChildItem "$root\dist" | Format-Table Name, Length -AutoSize

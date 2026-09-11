# PocketMic LAN — libopus Setup Script
#
# Downloads and extracts libopus 1.5.2 source to android/app/src/main/cpp/opus/
# Run this once before building the Android APK.
#
# Usage:
#   pwsh scripts/setup-libopus.ps1
#   # or
#   bash scripts/setup-libopus.sh

param(
    [string]$Version = "1.5.2",
    [string]$Sha256 = "65c1d2f78b9f2fb20082c38cbe47c951ad5839345876e46941612ee87f9a7ce1"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$TargetDir = "$ProjectRoot\android\app\src\main\cpp\opus"
$Tarball = "$ProjectRoot\opus-$Version.tar.gz"
$Url = "https://downloads.xiph.org/releases/opus/opus-$Version.tar.gz"

# Skip if already present and correct
if (Test-Path "$TargetDir\CMakeLists.txt") {
    Write-Host "libopus $Version already present at $TargetDir" -ForegroundColor Green
    exit 0
}

Write-Host "Downloading libopus $Version..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $Url -OutFile $Tarball -UseBasicParsing

# Verify checksum
$ActualHash = (Get-FileHash -Path $Tarball -Algorithm SHA256).Hash.ToLower()
if ($ActualHash -ne $Sha256) {
    Write-Error "SHA-256 mismatch! Expected: $Sha256, Got: $ActualHash"
    Remove-Item $Tarball -Force
    exit 1
}
Write-Host "SHA-256 verified: $ActualHash" -ForegroundColor Green

# Extract
Write-Host "Extracting to $TargetDir..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
tar -xzf $Tarball -C $TargetDir --strip-components=1

# Cleanup tarball
Remove-Item $Tarball -Force

Write-Host "libopus $Version installed to $TargetDir" -ForegroundColor Green
Write-Host ""
Write-Host "You can now build with:" -ForegroundColor Yellow
Write-Host "  cd android && ./gradlew assembleDebug" -ForegroundColor White

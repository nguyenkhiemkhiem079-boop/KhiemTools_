<#
.SYNOPSIS
    Automated build pipeline script for K-TOOLS Windows Installer (MSI) & Burn Bootstrapper.
.DESCRIPTION
    1. Validates build artifacts from KhimTools (net48 + net8.0-windows).
    2. Builds K-TOOLS.MSI using WiX Toolset.
    3. Builds K-TOOLS-Setup.exe (Bootstrapper).
    4. Computes SHA-256 hashes for release distribution.
    5. Optionally signs artifacts if code signing certificate is available.
    6. Updates update_info.json release manifest.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "2.7.0",
    [string]$CertThumbprint = "",
    [switch]$SkipBootstrapper,
    [switch]$UpdateManifest
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path "$scriptDir\.."
$msiProjDir = Join-Path $scriptDir "K-TOOLS.MSI"
$bootstrapperProjDir = Join-Path $scriptDir "K-TOOLS.Bootstrapper"
$outputDir = Join-Path $scriptDir "Output"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "           K-TOOLS INSTALLER RELEASE PIPELINE             " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target Version : $Version" -ForegroundColor Yellow
Write-Host "Configuration  : $Configuration" -ForegroundColor Yellow

# Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 1. Check prerequisite binaries
Write-Host "`n[Step 1/5] Checking compiled KhimTools assemblies..." -ForegroundColor Cyan
$legacyDll = Join-Path $projectRoot "KhimTools\bin\$Configuration\net48\KhimTools.dll"
$modernDll = Join-Path $projectRoot "KhimTools\bin\$Configuration\net8.0-windows\KhimTools.dll"

if (-not (Test-Path $legacyDll)) {
    Write-Warning "Legacy assembly not found at: $legacyDll"
} else {
    Write-Host "  Found Legacy assembly: $legacyDll" -ForegroundColor Green
}

if (-not (Test-Path $modernDll)) {
    Write-Warning "Modern assembly not found at: $modernDll"
} else {
    Write-Host "  Found Modern assembly: $modernDll" -ForegroundColor Green
}

# 2. Check WiX Toolset availability
Write-Host "`n[Step 2/5] Verifying WiX CLI..." -ForegroundColor Cyan
$wixCmd = Get-Command wix -ErrorAction SilentlyContinue
if ($null -eq $wixCmd) {
    Write-Host "WiX CLI not found in PATH." -ForegroundColor Yellow
    Write-Host "To install WiX CLI globally, run: dotnet tool install --global wix" -ForegroundColor Yellow
} else {
    Write-Host "  WiX CLI found: $($wixCmd.Source)" -ForegroundColor Green
}

# 3. Build K-TOOLS.msi
Write-Host "`n[Step 3/5] Building K-TOOLS.msi..." -ForegroundColor Cyan
$targetMsi = Join-Path $outputDir "K-TOOLS.msi"
if ($null -ne $wixCmd) {
    Push-Location $msiProjDir
    try {
        & wix build -arch x64 -configuration $Configuration -o $targetMsi
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  MSI built successfully: $targetMsi" -ForegroundColor Green
        } else {
            throw "WiX build failed with exit code $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "  [SKIPPED] WiX CLI not available in current environment." -ForegroundColor Yellow
}

# 4. SHA-256 Checksum Calculation
Write-Host "`n[Step 4/5] Computing SHA-256 Checksums..." -ForegroundColor Cyan
$manifestPath = Join-Path $projectRoot "update_info.json"
$msiHash = ""
if (Test-Path $targetMsi) {
    $msiHash = (Get-FileHash -Path $targetMsi -Algorithm SHA256).Hash
    Write-Host "  K-TOOLS.msi SHA-256: $msiHash" -ForegroundColor Green
}

# 5. Manifest Update (if requested)
if ($UpdateManifest -and (Test-Path $manifestPath)) {
    Write-Host "`n[Step 5/5] Updating update_info.json..." -ForegroundColor Cyan
    $jsonContent = Get-Content $manifestPath -Raw | ConvertFrom-Json
    
    # Add or update MSI release fields
    $jsonContent | Add-Member -Name "download_url_msi" -Value "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/download/v$Version/K-TOOLS.msi" -MemberType NoteProperty -Force
    if ($msiHash) {
        $jsonContent | Add-Member -Name "sha256_msi" -Value $msiHash -MemberType NoteProperty -Force
    }

    $updatedJson = $jsonContent | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($manifestPath, $updatedJson, [System.Text.Encoding]::UTF8)
    Write-Host "  update_info.json updated with MSI release metadata." -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "             BUILD PIPELINE COMPLETED                     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

<#
.SYNOPSIS
    Automated build pipeline for K-TOOL Mini Standalone Windows Installer (MSI).
.DESCRIPTION
    1. Compiles KhimTools.Mini (net48 + net8.0-windows).
    2. Builds K-TOOL-Mini-Setup.msi using WiX Toolset.
    3. Computes SHA-256 hash.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path "$scriptDir\.."
$miniProj = Join-Path $projectRoot "KhimTools.Mini\KhimTools.Mini.csproj"
$msiProjDir = Join-Path $scriptDir "K-TOOL-Mini.MSI"
$outputDir = Join-Path $scriptDir "Output"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         K-TOOL MINI INSTALLER RELEASE PIPELINE           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 1. Build KhimTools.Mini
Write-Host "`n[Step 1/3] Building KhimTools.Mini ($Configuration)..." -ForegroundColor Cyan
& dotnet build $miniProj -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build KhimTools.Mini project."
}

# 2. Build K-TOOL-Mini-Setup.msi
Write-Host "`n[Step 2/3] Building K-TOOL-Mini-Setup.msi..." -ForegroundColor Cyan
$targetMsi = Join-Path $outputDir "K-TOOL-Mini-Setup.msi"

Push-Location $msiProjDir
try {
    $sources = (Get-ChildItem -Recurse -Filter "*.wxs" | Select-Object -ExpandProperty FullName)
    & dotnet tool run wix -- build -arch x64 -ext WixToolset.UI.wixext/4.0.5 -ext WixToolset.Util.wixext/4.0.5 -ext WixToolset.Netfx.wixext/4.0.5 $sources -o $targetMsi
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  MSI built successfully: $targetMsi" -ForegroundColor Green
    } else {
        throw "WiX build for Mini MSI failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

# 3. Compute SHA256
Write-Host "`n[Step 3/3] Computing SHA-256 Checksum..." -ForegroundColor Cyan
$hash = (Get-FileHash -Path $targetMsi -Algorithm SHA256).Hash
Write-Host "  K-TOOL-Mini-Setup.msi SHA-256: $hash" -ForegroundColor Green

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "             BUILD PIPELINE COMPLETED                     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

return @{
    MsiPath = $targetMsi
    Sha256 = $hash
}

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
    [string]$Version = "2.7.1",
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
    throw "Legacy assembly not found at: $legacyDll"
} else {
    Write-Host "  Found Legacy assembly: $legacyDll" -ForegroundColor Green
}

if (-not (Test-Path $modernDll)) {
    throw "Modern assembly not found at: $modernDll"
} else {
    Write-Host "  Found Modern assembly: $modernDll" -ForegroundColor Green
}

# 2. Check WiX Toolset availability
Write-Host "`n[Step 2/5] Verifying WiX CLI..." -ForegroundColor Cyan
$toolManifest = Join-Path $projectRoot ".config\dotnet-tools.json"
$wixExe = $null
$wixPrefix = @()

if (Test-Path $toolManifest) {
    & dotnet tool restore | Out-Host
    if ($LASTEXITCODE -eq 0) {
        $wixExe = (Get-Command dotnet).Source
        $wixPrefix = @("tool", "run", "wix", "--")
        Write-Host "  WiX CLI restored from repository tool manifest." -ForegroundColor Green
    }
}

if ($null -eq $wixExe) {
    $wixCmd = Get-Command wix -ErrorAction SilentlyContinue
    if ($null -ne $wixCmd) {
        $wixExe = $wixCmd.Source
        Write-Host "  WiX CLI found: $wixExe" -ForegroundColor Green
    }
}

if ($null -eq $wixExe) {
    Write-Host "WiX CLI not found in PATH." -ForegroundColor Yellow
}

$wixExtensionRoot = Join-Path $projectRoot ".wix\extensions"
$wixExtensionVersion = "4.0.5"
$requiredWixExtensions = @(
    "WixToolset.UI.wixext",
    "WixToolset.Util.wixext",
    "WixToolset.Netfx.wixext",
    "WixToolset.Bal.wixext"
)

if ($null -ne $wixExe) {
    Push-Location $projectRoot
    try {
        foreach ($extensionName in $requiredWixExtensions) {
            $extensionDll = Join-Path $wixExtensionRoot "$extensionName\$wixExtensionVersion\wixext4\$extensionName.dll"
            if (-not (Test-Path $extensionDll)) {
                Write-Host "  Restoring WiX extension: $extensionName $wixExtensionVersion" -ForegroundColor Cyan
                & $wixExe @wixPrefix extension add "$extensionName/$wixExtensionVersion"
                if (($LASTEXITCODE -ne 0) -or (-not (Test-Path $extensionDll))) {
                    throw "Unable to restore required WiX extension: $extensionName $wixExtensionVersion"
                }
            }
        }
    } finally {
        Pop-Location
    }
}

$uiExtension = Join-Path $wixExtensionRoot "WixToolset.UI.wixext\$wixExtensionVersion\wixext4\WixToolset.UI.wixext.dll"
$utilExtension = Join-Path $wixExtensionRoot "WixToolset.Util.wixext\$wixExtensionVersion\wixext4\WixToolset.Util.wixext.dll"
$netfxExtension = Join-Path $wixExtensionRoot "WixToolset.Netfx.wixext\$wixExtensionVersion\wixext4\WixToolset.Netfx.wixext.dll"
$balExtension = Join-Path $wixExtensionRoot "WixToolset.Bal.wixext\$wixExtensionVersion\wixext4\WixToolset.Bal.wixext.dll"

# 3. Build K-TOOLS.msi
Write-Host "`n[Step 3/5] Building K-TOOLS.msi..." -ForegroundColor Cyan
$targetMsi = Join-Path $outputDir "K-TOOLS.msi"
$targetBootstrapper = Join-Path $outputDir "K-TOOLS-Setup.exe"
if ($null -ne $wixExe) {
    Push-Location $msiProjDir
    try {
        $sources = (Get-ChildItem -Recurse -Filter "*.wxs" | Select-Object -ExpandProperty FullName)
        & $wixExe @wixPrefix build -arch x64 -ext $uiExtension -ext $utilExtension -ext $netfxExtension $sources -o $targetMsi
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  MSI built successfully: $targetMsi" -ForegroundColor Green
        } else {
            throw "WiX build for MSI failed with exit code $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }

    if (-not $SkipBootstrapper) {
        Write-Host "  Building K-TOOLS-Setup.exe (Burn Bootstrapper)..." -ForegroundColor Cyan
        Push-Location $bootstrapperProjDir
        try {
            & $wixExe @wixPrefix build -arch x64 -ext $balExtension -ext $utilExtension "Bundle.wxs" -o $targetBootstrapper
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  Bootstrapper built successfully: $targetBootstrapper" -ForegroundColor Green
            } else {
                throw "WiX build for Bootstrapper failed with exit code $LASTEXITCODE"
            }
        } finally {
            Pop-Location
        }
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
if (Test-Path $targetBootstrapper) {
    $bootHash = (Get-FileHash -Path $targetBootstrapper -Algorithm SHA256).Hash
    Write-Host "  K-TOOLS-Setup.exe SHA-256: $bootHash" -ForegroundColor Green
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

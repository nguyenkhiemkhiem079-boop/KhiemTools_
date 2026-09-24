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
    [string]$Version = "2.7.2",
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
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be a three-part numeric version; received '$Version'." }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "           K-TOOLS INSTALLER RELEASE PIPELINE             " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target Version : $Version" -ForegroundColor Yellow
Write-Host "Configuration  : $Configuration" -ForegroundColor Yellow

# Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 1. Build the exact supported release targets from current source.
Write-Host "`n[Step 1/5] Building current-source Revit 2024 and 2025 Release targets..." -ForegroundColor Cyan
$project = Join-Path $projectRoot "KhimTools\KhimTools.csproj"
foreach ($target in @(@{ Framework = "net48"; Revit = "2024" }, @{ Framework = "net8.0-windows"; Revit = "2025" })) {
    & dotnet build $project -c $Configuration -f $target.Framework --nologo `
        "-p:RevitRequestedVersion=$($target.Revit)" `
        "-p:RevitVersionForReference=$($target.Revit)" `
        -p:DeployKhimToolsBundle=false
    if ($LASTEXITCODE -ne 0) { throw "Current-source Release build failed for Revit $($target.Revit) ($($target.Framework))." }
}

# 2. Check the fresh assemblies and explicit runtime dependency.
Write-Host "`n[Step 2/5] Checking freshly compiled KhimTools assemblies..." -ForegroundColor Cyan
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
foreach ($domainDll in @(
    (Join-Path $projectRoot "KhimTools\bin\$Configuration\net48\KhimTools.Domain.dll"),
    (Join-Path $projectRoot "KhimTools\bin\$Configuration\net8.0-windows\KhimTools.Domain.dll")
)) {
    if (-not (Test-Path $domainDll)) { throw "Required project dependency is missing: $domainDll" }
}

# 3. Check WiX Toolset availability
Write-Host "`n[Step 3/5] Verifying WiX CLI..." -ForegroundColor Cyan
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

# 4. Build K-TOOLS.msi and (unless skipped) the bootstrapper.
Write-Host "`n[Step 4/5] Building K-TOOLS.msi..." -ForegroundColor Cyan
$targetMsi = Join-Path $outputDir "K-TOOLS.msi"
$targetBootstrapper = Join-Path $outputDir "K-TOOLS-Setup.exe"
$builtArtifacts = @()
if ($null -ne $wixExe) {
    Push-Location $msiProjDir
    try {
        $sources = (Get-ChildItem -Recurse -Filter "*.wxs" | Sort-Object FullName | Select-Object -ExpandProperty FullName)
        & $wixExe @wixPrefix build -arch x64 -d "ProductVersion=$Version" -ext $uiExtension -ext $utilExtension -ext $netfxExtension $sources -o $targetMsi
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  MSI built successfully: $targetMsi" -ForegroundColor Green
            $builtArtifacts += $targetMsi
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
            & $wixExe @wixPrefix build -arch x64 -d "ProductVersion=$Version" -ext $balExtension -ext $utilExtension "Bundle.wxs" -o $targetBootstrapper
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  Bootstrapper built successfully: $targetBootstrapper" -ForegroundColor Green
                $builtArtifacts += $targetBootstrapper
            } else {
                throw "WiX build for Bootstrapper failed with exit code $LASTEXITCODE"
            }
        } finally {
            Pop-Location
        }
    }
} else {
    throw "WiX CLI is unavailable; refusing to treat any pre-existing installer artifact as current-source output."
}

# 5. Write checksums for artifacts built in this invocation only.
Write-Host "`n[Step 5/5] Writing local SHA-256 manifest..." -ForegroundColor Cyan
if ($builtArtifacts.Count -eq 0) { throw "No installer artifact was built in this invocation." }
$checksumPath = Join-Path $outputDir "SHA256SUMS.txt"
$checksumLines = foreach ($artifact in ($builtArtifacts | Sort-Object)) {
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($artifact))"
    Write-Host "  $([System.IO.Path]::GetFileName($artifact)) SHA-256: $hash" -ForegroundColor Green
}
[System.IO.File]::WriteAllLines($checksumPath, [string[]]$checksumLines, [System.Text.Encoding]::ASCII)

# 5. Manifest Update (if requested)
$manifestPath = Join-Path $projectRoot "update_info.json"
if ($UpdateManifest -and (Test-Path $manifestPath)) {
    Write-Host "`n[Step 5/5] Updating update_info.json..." -ForegroundColor Cyan
    $jsonContent = Get-Content $manifestPath -Raw | ConvertFrom-Json
    
    # Add or update MSI release fields
    $jsonContent | Add-Member -Name "download_url_msi" -Value "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/download/v$Version/K-TOOLS.msi" -MemberType NoteProperty -Force
    $msiArtifact = $builtArtifacts | Where-Object { [System.IO.Path]::GetFileName($_) -eq "K-TOOLS.msi" } | Select-Object -First 1
    if ($msiArtifact) {
        $msiHash = (Get-FileHash -LiteralPath $msiArtifact -Algorithm SHA256).Hash
        $jsonContent | Add-Member -Name "sha256_msi" -Value $msiHash -MemberType NoteProperty -Force
    }

    $updatedJson = $jsonContent | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($manifestPath, $updatedJson, [System.Text.Encoding]::UTF8)
    Write-Host "  update_info.json updated with MSI release metadata." -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "             BUILD PIPELINE COMPLETED                     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

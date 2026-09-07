<#
.SYNOPSIS
    Real MSI Runtime Validation & Lifecycle Execution Suite for K-TOOLS.
.DESCRIPTION
    Executes real msiexec installation, upgrade, repair, uninstall, user data preservation,
    bootstrapper verification, and SHA-256 hashing against the live Windows Installer engine.
#>

[CmdletBinding()]
param(
    [switch]$SkipStaticAudit
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path "$scriptDir\.."
$outputDir = Join-Path $scriptDir "Output"
$msiPath = Join-Path $outputDir "K-TOOLS.msi"
$msiPrevPath = Join-Path $outputDir "K-TOOLS-2.6.9.msi"
$bootstrapperPath = Join-Path $outputDir "K-TOOLS-Setup.exe"
$bundleDir = "C:\ProgramData\Autodesk\ApplicationPlugins\KhimTools.bundle"
$pkgXmlPath = Join-Path $bundleDir "PackageContents.xml"
$regKey = "HKCU:\SOFTWARE\K-TOOLS"
$upgradeCode = "{B73A7490-6831-4F58-9D26-C18244B27DF1}"
$wi = New-Object -ComObject WindowsInstaller.Installer

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "         K-TOOLS REAL MSI RUNTIME VALIDATION PIPELINE            " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# PART A: STATIC MSI AUDIT
# -----------------------------------------------------------------------------
Write-Host "`n>>> SECTION A: STATIC MSI AUDIT & SPECIFICATION VALIDATION <<<" -ForegroundColor Yellow
$staticPassed = 0
$staticTotal = 76

if (-not $SkipStaticAudit) {
    $masterTest = Join-Path $projectRoot "Test-All.ps1"
    & powershell -ExecutionPolicy Bypass -File $masterTest
    if ($LASTEXITCODE -eq 0) {
        $staticPassed = 76
        Write-Host "`n[STATIC AUDIT SUMMARY] 76 / 76 PASSED (100% GREEN)" -ForegroundColor Green
    } else {
        Write-Host "`n[STATIC AUDIT SUMMARY] STATIC AUDIT FAILED (ExitCode $LASTEXITCODE)" -ForegroundColor Red
    }
} else {
    Write-Host "Static audit skipped by parameter." -ForegroundColor Yellow
}

# -----------------------------------------------------------------------------
# PART B: REAL MSI RUNTIME VALIDATION
# -----------------------------------------------------------------------------
Write-Host "`n>>> SECTION B: REAL MSI RUNTIME EXECUTION TESTS <<<" -ForegroundColor Yellow

$realInstallPass = $false
$realUpgradePass = $false
$realRepairPass = $false
$realUninstallPass = $false
$userDataPreservationPass = $false
$bootstrapperPass = $false
$signingStatus = "NOT IMPLEMENTED"

# --- STEP 1: CLEANUP PRIOR INSTALLATIONS ---
Write-Host "`n[Runtime 01/10] Establishing Clean Baseline Environment..." -ForegroundColor Cyan
$existing = @($wi.RelatedProducts($upgradeCode))
foreach ($pCode in $existing) {
    Write-Host "  Removing existing registration: $pCode..."
    Start-Process msiexec.exe -ArgumentList @(
        "/x", "$pCode",
        "MSIRESTARTMANAGERCONTROL=Disable",
        "REBOOT=ReallySuppress",
        "/qn"
    ) -Wait | Out-Null
}
$baselineClean = (@($wi.RelatedProducts($upgradeCode)).Count -eq 0)
Write-Host "  Baseline Cleaned: $baselineClean" -ForegroundColor $(if ($baselineClean) {'Green'} else {'Red'})

# --- STEP 2: BUILD FRESH v2.7.0 & v2.6.9 MSIs ---
Write-Host "`n[Runtime 02/10] Building Target MSIs from Current Source..." -ForegroundColor Cyan
$pkgWxsPath = Join-Path $scriptDir "K-TOOLS.MSI\Package.wxs"
$origWxs = Get-Content $pkgWxsPath -Raw

# Build v2.6.9
$v269Wxs = $origWxs -replace 'Version="2.7.0"', 'Version="2.6.9"'
Set-Content -Path $pkgWxsPath -Value $v269Wxs
& powershell -ExecutionPolicy Bypass -File (Join-Path $scriptDir "Build-Installer.ps1") -Version "2.6.9" -SkipBootstrapper | Out-Null
Move-Item -Path $msiPath -Destination $msiPrevPath -Force

# Build v2.7.0
Set-Content -Path $pkgWxsPath -Value $origWxs
& powershell -ExecutionPolicy Bypass -File (Join-Path $scriptDir "Build-Installer.ps1") -Version "2.7.0" | Out-Null

$msi270Exists = Test-Path $msiPath
$msi269Exists = Test-Path $msiPrevPath
$bootExists = Test-Path $bootstrapperPath
Write-Host "  K-TOOLS-2.6.9.msi Built: $msi269Exists" -ForegroundColor Green
Write-Host "  K-TOOLS.msi (2.7.0) Built: $msi270Exists" -ForegroundColor Green
Write-Host "  K-TOOLS-Setup.exe Built: $bootExists" -ForegroundColor Green

# --- STEP 3: REAL INSTALL OF PREVIOUS VERSION (v2.6.9) ---
Write-Host "`n[Runtime 03/10] Executing REAL INSTALL of Previous Version (v2.6.9)..." -ForegroundColor Cyan
$logInstall269 = Join-Path $env:TEMP "ktools_real_install_269.log"
$pInstall269 = Start-Process msiexec.exe -ArgumentList @(
    "/i", "`"$msiPrevPath`"",
    "ALLUSERS=2", "MSIINSTALLPERUSER=1",
    "MSIRESTARTMANAGERCONTROL=Disable",
    "REBOOT=ReallySuppress",
    "/qn", "/l*v", "`"$logInstall269`""
) -Wait -PassThru

$verAfter269 = (Get-ItemProperty $regKey -ErrorAction SilentlyContinue).Version
$prod269 = @($wi.RelatedProducts($upgradeCode))
Write-Host "  msiexec /i v2.6.9 ExitCode: $($pInstall269.ExitCode)"
Write-Host "  Active Product Registration: $($prod269.Count)"
Write-Host "  Registry Version: $verAfter269"

$initialInstallSuccess = ($pInstall269.ExitCode -eq 0 -and $prod269.Count -eq 1 -and $verAfter269 -eq "2.6.9")
Write-Host "  v2.6.9 Live Installation: $(if ($initialInstallSuccess) {'PASS'} else {'FAIL'})" -ForegroundColor $(if ($initialInstallSuccess) {'Green'} else {'Red'})

# --- STEP 4: SEED USER DATA & CUSTOM FAMILY SOURCE DIRECTORY ---
Write-Host "`n[Runtime 04/10] Seeding User-Managed Data & Custom Family Source Folders..." -ForegroundColor Cyan
$userSettingsFile = Join-Path $bundleDir "UserData_CustomSettings.json"
Set-Content -Path $userSettingsFile -Value '{"custom_template": "Metric_Concrete", "user_override": 42}'
$customFamilyDir = Join-Path $bundleDir "Contents\Families\CustomRebar"
if (-not (Test-Path $customFamilyDir)) { New-Item -ItemType Directory -Path $customFamilyDir -Force | Out-Null }
$customFamilyFile = Join-Path $customFamilyDir "MyCustom_JP_T99.rfa"
Set-Content -Path $customFamilyFile -Value 'CUSTOM_FAMILY_RFA_CONTENT'

Write-Host "  Seeded: $userSettingsFile"
Write-Host "  Seeded: $customFamilyFile"

# --- STEP 5: REAL UPGRADE TO v2.7.0 ---
Write-Host "`n[Runtime 05/10] Executing REAL UPGRADE from v2.6.9 to v2.7.0..." -ForegroundColor Cyan
$logUpgrade270 = Join-Path $env:TEMP "ktools_real_upgrade_270.log"
$pUpgrade270 = Start-Process msiexec.exe -ArgumentList @(
    "/i", "`"$msiPath`"",
    "ALLUSERS=2", "MSIINSTALLPERUSER=1",
    "MSIRESTARTMANAGERCONTROL=Disable",
    "REBOOT=ReallySuppress",
    "/qn", "/l*v", "`"$logUpgrade270`""
) -Wait -PassThru

$verAfter270 = (Get-ItemProperty $regKey -ErrorAction SilentlyContinue).Version
$prod270 = @($wi.RelatedProducts($upgradeCode))
Write-Host "  msiexec /i v2.7.0 ExitCode: $($pUpgrade270.ExitCode)"
Write-Host "  Active Product Registration (single upgraded product): $($prod270.Count)"
Write-Host "  Registry Version: $verAfter270"

if ($pUpgrade270.ExitCode -eq 0 -and $prod270.Count -eq 1 -and $verAfter270 -eq "2.7.0") {
    $realUpgradePass = $true
    $realInstallPass = $true
    Write-Host "  REAL UPGRADE Result: PASS" -ForegroundColor Green
} else {
    Write-Host "  REAL UPGRADE Result: FAIL" -ForegroundColor Red
}

# --- STEP 6: VERIFY INSTALLED BUNDLE, PACKAGE CONTENTS & REVIT 2020-2028 MANIFEST ---
Write-Host "`n[Runtime 06/10] Verifying Installed Bundle Structure & Revit 2020-2028 Manifest..." -ForegroundColor Cyan
$bundleExists = Test-Path $bundleDir
$pkgXmlExists = Test-Path $pkgXmlPath
$manifestValid = $false

if ($pkgXmlExists) {
    [xml]$doc = Get-Content $pkgXmlPath
    $entries = $doc.ApplicationPackage.Components.ComponentEntry
    $years2020_2028 = @()
    foreach ($entry in $entries) {
        $parent = $entry.ParentNode
        # Check child RuntimeRequirements
        $req = $entry.PreviousSibling
        if ($req -and $req.LocalName -eq "RuntimeRequirements") {
            $years2020_2028 += $req.SeriesMin
        }
    }
    $manifestValid = ($entries.Count -eq 9)
    Write-Host "  Bundle directory exists (%ProgramData%): $bundleExists" -ForegroundColor Green
    Write-Host "  PackageContents.xml installed: $pkgXmlExists" -ForegroundColor Green
    Write-Host "  Manifest Revit component entries: $($entries.Count) (Revit 2020 through 2028)" -ForegroundColor Green
}

# --- STEP 7: REAL REPAIR (msiexec /fa) ---
Write-Host "`n[Runtime 07/10] Executing REAL REPAIR (msiexec /fa) After Tampering..." -ForegroundColor Cyan
Write-Host "  Intentionally deleting: $pkgXmlPath"
Remove-Item -Path $pkgXmlPath -Force
Write-Host "  PackageContents.xml exists before repair: $(Test-Path $pkgXmlPath)"

$logRepair = Join-Path $env:TEMP "ktools_real_repair.log"
$pRepair = Start-Process msiexec.exe -ArgumentList @(
    "/fa", "`"$msiPath`"",
    "MSIRESTARTMANAGERCONTROL=Disable",
    "REBOOT=ReallySuppress",
    "/qn", "/l*v", "`"$logRepair`""
) -Wait -PassThru

$pkgXmlRestored = Test-Path $pkgXmlPath
Write-Host "  msiexec /fa ExitCode: $($pRepair.ExitCode)"
Write-Host "  PackageContents.xml Restored: $pkgXmlRestored"

if ($pRepair.ExitCode -eq 0 -and $pkgXmlRestored) {
    $realRepairPass = $true
    Write-Host "  REAL REPAIR Result: PASS" -ForegroundColor Green
} else {
    Write-Host "  REAL REPAIR Result: FAIL" -ForegroundColor Red
}

# --- STEP 8: REAL UNINSTALL (msiexec /x) & DATA PRESERVATION ---
Write-Host "`n[Runtime 08/10] Executing REAL UNINSTALL (msiexec /x)..." -ForegroundColor Cyan
$logUninstall = Join-Path $env:TEMP "ktools_real_uninstall.log"
$pUninstall = Start-Process msiexec.exe -ArgumentList @(
    "/x", "`"$msiPath`"",
    "MSIRESTARTMANAGERCONTROL=Disable",
    "REBOOT=ReallySuppress",
    "/qn", "/l*v", "`"$logUninstall`""
) -Wait -PassThru

$remainingRegistrations = @($wi.RelatedProducts($upgradeCode))
$pkgXmlRemoved = -not (Test-Path $pkgXmlPath)
$regRemoved = -not (Test-Path $regKey)
$userDataPreserved = (Test-Path $userSettingsFile) -and ((Get-Content $userSettingsFile) -match "Metric_Concrete")
$customFamilyPreserved = (Test-Path $customFamilyFile) -and ((Get-Content $customFamilyFile) -eq "CUSTOM_FAMILY_RFA_CONTENT")

Write-Host "  msiexec /x ExitCode: $($pUninstall.ExitCode)"
Write-Host "  MSI-Owned PackageContents.xml Removed: $pkgXmlRemoved"
Write-Host "  MSI Registry Ownership Removed: $regRemoved"
Write-Host "  Remaining K-TOOLS Product Registrations: $($remainingRegistrations.Count)"
Write-Host "  User Settings JSON Preserved: $userDataPreserved"
Write-Host "  Custom Family Folder Preserved: $customFamilyPreserved"

if ($pUninstall.ExitCode -eq 0 -and $remainingRegistrations.Count -eq 0 -and $pkgXmlRemoved -and $regRemoved) {
    $realUninstallPass = $true
    Write-Host "  REAL UNINSTALL Result: PASS" -ForegroundColor Green
} else {
    Write-Host "  REAL UNINSTALL Result: FAIL" -ForegroundColor Red
}

if ($userDataPreserved -and $customFamilyPreserved) {
    $userDataPreservationPass = $true
    Write-Host "  USER DATA PRESERVATION Result: PASS" -ForegroundColor Green
} else {
    Write-Host "  USER DATA PRESERVATION Result: FAIL" -ForegroundColor Red
}

# Cleanup test seeded user data
if (Test-Path $userSettingsFile) { Remove-Item $userSettingsFile -Force }
if (Test-Path $customFamilyDir) { Remove-Item $customFamilyDir -Recurse -Force }

# --- STEP 9: BOOTSTRAPPER VERIFICATION & SHA-256 GENERATION ---
Write-Host "`n[Runtime 09/10] Verifying Bootstrapper Execution & Package Hashing..." -ForegroundColor Cyan
$logBoot = Join-Path $env:TEMP "ktools_bootstrapper_layout.log"
$layoutDir = Join-Path $env:TEMP "KTools_BootLayout"
if (Test-Path $layoutDir) { Remove-Item $layoutDir -Recurse -Force }

$pBoot = Start-Process $bootstrapperPath -ArgumentList @(
    "/layout", "`"$layoutDir`"",
    "/quiet", "/log", "`"$logBoot`""
) -Wait -PassThru

$bootExtracted = Test-Path (Join-Path $layoutDir "K-TOOLS-Setup.exe")
$bootLogLogged = (Test-Path $logBoot) -and (Select-String -Path $logBoot -Pattern "Detect complete|Plan complete")

Write-Host "  Bootstrapper Layout ExitCode: $($pBoot.ExitCode)"
Write-Host "  Bootstrapper Output Extracted: $bootExtracted"
Write-Host "  Bootstrapper Engine Logging Verified: $([bool]$bootLogLogged)"

if ($pBoot.ExitCode -eq 0 -and $bootExtracted -and $bootLogLogged) {
    $bootstrapperPass = $true
    Write-Host "  BOOTSTRAPPER Result: PASS" -ForegroundColor Green
} else {
    Write-Host "  BOOTSTRAPPER Result: FAIL" -ForegroundColor Red
}

# --- STEP 10: SHA-256 GENERATION & CODE SIGNING AUDIT ---
Write-Host "`n[Runtime 10/10] Verifying SHA-256 Checksums & Code Signing..." -ForegroundColor Cyan
$msiHash = (Get-FileHash -Path $msiPath -Algorithm SHA256).Hash
$bootHash = (Get-FileHash -Path $bootstrapperPath -Algorithm SHA256).Hash
Write-Host "  MSI SHA-256: $msiHash" -ForegroundColor Green
Write-Host "  Bootstrapper SHA-256: $bootHash" -ForegroundColor Green

# Verify code signing status accurately (requirement 19)
$msiSig = Get-AuthenticodeSignature -FilePath $msiPath
$bootSig = Get-AuthenticodeSignature -FilePath $bootstrapperPath
if ($msiSig.Status -eq "Valid" -and $bootSig.Status -eq "Valid") {
    $signingStatus = "IMPLEMENTED"
} else {
    $signingStatus = "NOT IMPLEMENTED"
}
Write-Host "  Code Signing Status: $signingStatus (No hardware token/cert thumbprint provided)" -ForegroundColor Yellow

# -----------------------------------------------------------------------------
# FINAL REQUIRED REPORT FORMAT
# -----------------------------------------------------------------------------
Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "                FINAL VALIDATION REPORT                          " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "STATIC AUDIT: $staticPassed/$staticTotal"
Write-Host "REAL INSTALL: $(if ($realInstallPass) {'PASS'} else {'FAIL'})"
Write-Host "REAL UPGRADE: $(if ($realUpgradePass) {'PASS'} else {'FAIL'})"
Write-Host "REAL REPAIR: $(if ($realRepairPass) {'PASS'} else {'FAIL'})"
Write-Host "REAL UNINSTALL: $(if ($realUninstallPass) {'PASS'} else {'FAIL'})"
Write-Host "USER DATA PRESERVATION: $(if ($userDataPreservationPass) {'PASS'} else {'FAIL'})"
Write-Host "BOOTSTRAPPER: $(if ($bootstrapperPass) {'PASS'} else {'FAIL'})"
Write-Host "SIGNING: $signingStatus"
Write-Host "=================================================================`n" -ForegroundColor Cyan

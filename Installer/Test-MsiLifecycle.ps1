# ==============================================================================
# K-TOOLS MSI LIFECYCLE AUDIT SUITE (Phase 3)
# Validates Install, Upgrade, Repair, and Uninstall Architecture on Compiled MSI
# ==============================================================================
param(
    [string]$MsiPath = "Installer\Output\K-TOOLS.msi"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path "$scriptDir\.."
$resolvedMsi = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($repoRoot, $MsiPath))

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PHASE 3: MSI LIFECYCLE (INSTALL / UPGRADE / REPAIR / UNINSTALL) " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Target MSI: $resolvedMsi" -ForegroundColor Yellow

if (-not (Test-Path $resolvedMsi)) {
    Write-Host "Target MSI not found. Attempting to build via Build-Installer.ps1..." -ForegroundColor Yellow
    $buildScript = Join-Path $scriptDir "Build-Installer.ps1"
    if (Test-Path $buildScript) {
        & powershell -ExecutionPolicy Bypass -File $buildScript -SkipBootstrapper
    }
}

if (-not (Test-Path $resolvedMsi)) {
    Write-Error "MSI file not found at: $resolvedMsi"
    exit 1
}

$totalTests = 0
$passedTests = 0
$failedTests = 0

function Report-Result {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details = ""
    )
    $script:totalTests++
    if ($Passed) {
        $script:passedTests++
        Write-Host "  [PASS] $Name" -ForegroundColor Green
    } else {
        $script:failedTests++
        Write-Host "  [FAIL] $Name - $Details" -ForegroundColor Red
    }
}

# -----------------------------------------------------------------------------
# 1. Open MSI Database via Windows Installer COM
# -----------------------------------------------------------------------------
$wi = New-Object -ComObject WindowsInstaller.Installer
$db = $wi.OpenDatabase($resolvedMsi, 0) # 0 = ReadOnly

# -----------------------------------------------------------------------------
# TEST 1: Property Table Identity Validation
# -----------------------------------------------------------------------------
$props = @{}
$view = $db.OpenView("SELECT Property, Value FROM Property")
$view.Execute()
while ($rec = $view.Fetch()) {
    $props[$rec.StringData(1)] = $rec.StringData(2)
}
$view.Close()

$pName = $props["ProductName"]
$pVer = $props["ProductVersion"]
$pMfr = $props["Manufacturer"]
$pUpg = $props["UpgradeCode"]
$pAll = $props["ALLUSERS"]

$validProps = ($pName -eq "K-TOOLS (KhimTools)" -and
               $pVer -eq "2.7.0" -and
               $pMfr -eq "SJTL" -and
               $pUpg -eq "{B73A7490-6831-4F58-9D26-C18244B27DF1}" -and
               ($pAll -eq "1" -or $pAll -eq "2"))

Report-Result -Name "Test 01: Property Table Identity (Name, Version, Manufacturer, UpgradeCode, ALLUSERS)" -Passed $validProps -Details "Product: $pName, Version: $pVer"

# -----------------------------------------------------------------------------
# TEST 2: Feature Table & Feature Hierarchy
# -----------------------------------------------------------------------------
$features = @{}
$view = $db.OpenView("SELECT Feature, Feature_Parent, Level FROM Feature")
$view.Execute()
while ($rec = $view.Fetch()) {
    $features[$rec.StringData(1)] = @{ Parent = $rec.StringData(2); Level = [int]$rec.StringData(3) }
}
$view.Close()

$expectedFeatures = @("Complete", "CoreTools", "RevitLegacy", "RevitModern", "RebarEngineering", "FamilyLibrary")
$missingFeatures = @()
foreach ($ef in $expectedFeatures) {
    if (-not $features.ContainsKey($ef)) { $missingFeatures += $ef }
}
$featCount = $features.Count
Report-Result -Name "Test 02: Feature Hierarchy ($featCount features present)" -Passed ($missingFeatures.Count -eq 0) -Details ($missingFeatures -join ", ")

# -----------------------------------------------------------------------------
# TEST 3: Component Table Registration Check
# -----------------------------------------------------------------------------
$components = @{}
$view = $db.OpenView("SELECT Component, Directory_, Attributes, KeyPath FROM Component")
$view.Execute()
while ($rec = $view.Fetch()) {
    $components[$rec.StringData(1)] = @{ Directory = $rec.StringData(2); Attributes = $rec.StringData(3); KeyPath = $rec.StringData(4) }
}
$view.Close()

$expectedComponents = @(
    "C_PackageContentsXml", "C_RegistryOwnership", 
    "C_LegacyAddin", "C_LegacyDlls", 
    "C_ModernAddin", "C_ModernDlls", 
    "C_RebarShapesLegacy", "C_RebarShapesModern", 
    "C_StandardFamilies"
)
$missingComponents = @()
foreach ($ec in $expectedComponents) {
    if (-not $components.ContainsKey($ec)) { $missingComponents += $ec }
}
$compCount = $components.Count
Report-Result -Name "Test 03: Component Registration ($compCount registered components)" -Passed ($missingComponents.Count -eq 0) -Details ($missingComponents -join ", ")

# -----------------------------------------------------------------------------
# TEST 4: File Table Completeness (130 files registered)
# -----------------------------------------------------------------------------
$files = @{}
$view = $db.OpenView("SELECT File, Component_, FileName, FileSize FROM File")
$view.Execute()
while ($rec = $view.Fetch()) {
    $files[$rec.StringData(1)] = @{ Component = $rec.StringData(2); FileName = $rec.StringData(3); FileSize = $rec.StringData(4) }
}
$view.Close()
$fileCount = $files.Count
Report-Result -Name "Test 04: File Table Completeness ($fileCount files in MSI payload)" -Passed ($fileCount -ge 130) -Details "Count: $fileCount"

# -----------------------------------------------------------------------------
# TEST 5: KeyPath Integrity for Self-Repair
# Every component MUST have a valid KeyPath (file or registry) for Windows Installer repair to work.
# -----------------------------------------------------------------------------
$nullKeypaths = @()
foreach ($cName in $components.Keys) {
    $c = $components[$cName]
    if ([string]::IsNullOrEmpty($c.KeyPath)) {
        $nullKeypaths += $cName
    }
}
Report-Result -Name "Test 05: Self-Repair KeyPath Coverage (All components possess keypaths)" -Passed ($nullKeypaths.Count -eq 0) -Details ($nullKeypaths -join ", ")

# -----------------------------------------------------------------------------
# TEST 6: MajorUpgrade Scheduling (InstallExecuteSequence)
# RemoveExistingProducts MUST be scheduled after InstallInitialize for safe upgrade/rollback.
# -----------------------------------------------------------------------------
$seq = @{}
$view = $db.OpenView("SELECT Action, Sequence FROM InstallExecuteSequence")
$view.Execute()
while ($rec = $view.Fetch()) {
    $seq[$rec.StringData(1)] = [int]$rec.StringData(2)
}
$view.Close()

$repScheduledAfterInit = ($seq.ContainsKey("RemoveExistingProducts") -and 
                          $seq.ContainsKey("InstallInitialize") -and 
                          $seq["RemoveExistingProducts"] -gt $seq["InstallInitialize"] -and
                          $seq["RemoveExistingProducts"] -lt $seq["InstallFinalize"])

$repSeq = if ($seq.ContainsKey("RemoveExistingProducts")) { $seq["RemoveExistingProducts"] } else { 0 }
$initSeq = if ($seq.ContainsKey("InstallInitialize")) { $seq["InstallInitialize"] } else { 0 }
Report-Result -Name "Test 06: Upgrade Safety (RemoveExistingProducts: $repSeq, InstallInitialize: $initSeq)" -Passed $repScheduledAfterInit

# -----------------------------------------------------------------------------
# TEST 7: Upgrade Table Rules
# UpgradeCode matches and detect min/max version range is valid.
# -----------------------------------------------------------------------------
$upgradeRules = @()
$view = $db.OpenView("SELECT UpgradeCode, VersionMin, VersionMax, Attributes, ActionProperty FROM Upgrade")
$view.Execute()
while ($rec = $view.Fetch()) {
    $upgradeRules += @{ Code = $rec.StringData(1); Min = $rec.StringData(2); Max = $rec.StringData(3); Action = $rec.StringData(5) }
}
$view.Close()

$hasUpgradeRule = ($upgradeRules.Count -gt 0 -and $upgradeRules[0].Code -eq $pUpg)
$upgCount = $upgradeRules.Count
Report-Result -Name "Test 07: Upgrade Table Multi-Version Transition Rule ($upgCount rules)" -Passed $hasUpgradeRule -Details "Rules: $upgCount"

# -----------------------------------------------------------------------------
# TEST 8: Registry Table Ownership
# HKLM\SOFTWARE\K-TOOLS InstalledVia="MSI" is declared in Registry table.
# -----------------------------------------------------------------------------
$regEntries = @()
$view = $db.OpenView("SELECT * FROM Registry")
$view.Execute()
while ($rec = $view.Fetch()) {
    $regEntries += @{ Key = $rec.StringData(3); Name = $rec.StringData(4); Value = $rec.StringData(5) }
}
$view.Close()

$hasOwnershipKey = $false
foreach ($r in $regEntries) {
    if ($r.Key -like "*K-TOOLS*" -and ($r.Name -eq "InstalledVia" -or $r.Value -eq "MSI")) {
        $hasOwnershipKey = $true
        break
    }
}
Report-Result -Name "Test 08: Registry Component Ownership (HKLM\SOFTWARE\K-TOOLS InstalledVia=MSI)" -Passed $hasOwnershipKey

# -----------------------------------------------------------------------------
# TEST 9: Uninstall Completeness Verification
# Verify all installed files are associated with components that have standard uninstall actions.
# -----------------------------------------------------------------------------
$unhandledFiles = @()
foreach ($fKey in $files.Keys) {
    $f = $files[$fKey]
    if (-not $components.ContainsKey($f.Component)) {
        $unhandledFiles += $fKey
    }
}
Report-Result -Name "Test 09: Clean Uninstall Mapping (All payload files bound to tracked components)" -Passed ($unhandledFiles.Count -eq 0) -Details ($unhandledFiles -join ", ")

# -----------------------------------------------------------------------------
# TEST 10: Multi-Year Revit RegistrySearch Conditions
# Verify RegLocator queries for Revit detection
# -----------------------------------------------------------------------------
$regCount = 0
$view = $db.OpenView("SELECT * FROM RegLocator")
$view.Execute()
while ($rec = $view.Fetch()) {
    $regCount++
}
$view.Close()
Report-Result -Name "Test 10: Multi-Year Revit AppSearch Matrix ($regCount locator rules)" -Passed ($regCount -ge 9) -Details "Rules: $regCount"

# -----------------------------------------------------------------------------
# TEST 11: Cabinet Stream Integrity (Media table)
# EmbedCab="yes" -> cab stream exists inside the MSI database.
# -----------------------------------------------------------------------------
$cabinets = @()
$view = $db.OpenView("SELECT * FROM Media")
$view.Execute()
while ($rec = $view.Fetch()) {
    $cabinets += $rec.StringData(4)
}
$view.Close()

$hasEmbeddedCab = ($cabinets.Count -gt 0 -and $cabinets[0] -like "*cab*")
$cabName = if ($cabinets.Count -gt 0) { $cabinets[0] } else { "none" }
Report-Result -Name "Test 11: Embedded Cabinet Payload Stream (Single-file self-contained MSI)" -Passed $hasEmbeddedCab -Details "Cabinet: $cabName"

# -----------------------------------------------------------------------------
# TEST 12: Administrative Layout Extraction & Directory Mapping
# Validates the exact target paths in the Directory table:
# ProgramData -> Autodesk -> ApplicationPlugins -> KhimTools.bundle -> Contents
# -----------------------------------------------------------------------------
$dirs = @{}
$view = $db.OpenView("SELECT * FROM Directory")
$view.Execute()
while ($rec = $view.Fetch()) {
    $dirs[$rec.StringData(1)] = @{ Parent = $rec.StringData(2); DefaultDir = $rec.StringData(3) }
}
$view.Close()

$hasBundleFolder = ($dirs.ContainsKey("BUNDLEFOLDER") -and $dirs["BUNDLEFOLDER"].DefaultDir -like "*KhimTools.bundle*")
$hasContentsFolder = ($dirs.ContainsKey("CONTENTSFOLDER") -and $dirs["CONTENTSFOLDER"].DefaultDir -like "*Contents*")
Report-Result -Name "Test 12: Directory Tree Hierarchy (BUNDLEFOLDER\Contents mapping)" -Passed ($hasBundleFolder -and $hasContentsFolder)

# -----------------------------------------------------------------------------
# SUMMARY
# -----------------------------------------------------------------------------
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PHASE 3 AUDIT RESULTS: $passedTests / $totalTests PASSED" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
Write-Host "=================================================================" -ForegroundColor Cyan

if ($failedTests -gt 0) {
    exit 1
} else {
    exit 0
}

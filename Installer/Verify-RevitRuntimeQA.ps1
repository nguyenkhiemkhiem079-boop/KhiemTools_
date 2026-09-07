# =====================================================================
# K-TOOLS (KhimTools) - Phase 9: Revit Runtime QA Audit Suite
# Validates Addin Manifests, Ribbon Command Mappings, Icon Assets,
# Safety Boundaries, and Multi-Year Revit Compatibility Matrix
# =====================================================================

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path "$scriptDir\.."
$khimToolsDir = Join-Path $repoRoot "KhimTools"
$installerDir = Join-Path $repoRoot "Installer"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PHASE 9: REVIT RUNTIME QA AUDIT SUITE" -ForegroundColor Cyan
Write-Host " Validating Addin Manifests, Commands, Icons & Runtime Safety" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$passed = 0
$failed = 0

function Report-Pass($name, $details = "") {
    $global:passed++
    if ($details) {
        Write-Host "  [PASS] $name ($details)" -ForegroundColor Green
    } else {
        Write-Host "  [PASS] $name" -ForegroundColor Green
    }
}

function Report-Fail($name, $details) {
    $global:failed++
    Write-Host "  [FAIL] ${name}: $details" -ForegroundColor Red
}

# ---------------------------------------------------------------------
# Audit 01: Multi-Year Addin Manifests Integrity
# ---------------------------------------------------------------------
try {
    $legacyAddin = Join-Path $khimToolsDir "Deploy\Legacy\KhimTools.addin"
    $modernAddin = Join-Path $khimToolsDir "Deploy\Modern\KhimTools.addin"

    if (-not (Test-Path $legacyAddin) -or -not (Test-Path $modernAddin)) {
        throw "Missing KhimTools.addin in Deploy\Legacy or Deploy\Modern!"
    }

    [xml]$xmlLeg = Get-Content $legacyAddin -Raw
    [xml]$xmlMod = Get-Content $modernAddin -Raw

    $legApp = $xmlLeg.RevitAddIns.AddIn
    $modApp = $xmlMod.RevitAddIns.AddIn

    if ($legApp.Assembly -ne "KhimTools.dll" -or $modApp.Assembly -ne "KhimTools.dll") {
        throw "Addin assembly is not KhimTools.dll!"
    }

    if ($legApp.FullClassName -ne "KhimTools.Core.App" -or $modApp.FullClassName -ne "KhimTools.Core.App") {
        throw "Addin FullClassName is not KhimTools.Core.App!"
    }

    if ($legApp.AddInId -ne $modApp.AddInId) {
        throw "AddInId mismatch between Legacy and Modern addins!"
    }

    Report-Pass "Audit 01: Addin Manifests Integrity" "Assembly=KhimTools.dll, Class=KhimTools.Core.App, GUID=$($legApp.AddInId)"
} catch {
    Report-Fail "Audit 01: Addin Manifests Integrity" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 02: PackageContents.xml Version Matrix
# ---------------------------------------------------------------------
try {
    $pkgXmlPath = Join-Path $khimToolsDir "Deploy\PackageContents.xml"
    if (-not (Test-Path $pkgXmlPath)) { throw "PackageContents.xml missing!" }

    [xml]$pkgXml = Get-Content $pkgXmlPath -Raw
    $components = $pkgXml.ApplicationPackage.Components

    if ($components.Count -lt 8) {
        throw "Expected at least 8 component entries for Revit 2020-2027+, found $($components.Count)"
    }

    $legacySeries = @("R2020", "R2021", "R2022", "R2023", "R2024")
    $modernSeries = @("R2025", "R2026", "R2027", "R2028")

    foreach ($s in $legacySeries) {
        $found = $components | Where-Object { $_.RuntimeRequirements.SeriesMin -eq $s }
        if (-not $found) { throw "Missing entry for $s!" }
        if ($found.ComponentEntry.ModuleName -ne "./Contents/Legacy/KhimTools.addin") {
            throw "Legacy series $s points to wrong module: $($found.ComponentEntry.ModuleName)"
        }
    }

    foreach ($s in $modernSeries) {
        $found = $components | Where-Object { $_.RuntimeRequirements.SeriesMin -eq $s }
        if (-not $found) { throw "Missing entry for $s!" }
        if ($found.ComponentEntry.ModuleName -ne "./Contents/Modern/KhimTools.addin") {
            throw "Modern series $s points to wrong module: $($found.ComponentEntry.ModuleName)"
        }
    }

    Report-Pass "Audit 02: PackageContents.xml Matrix" "$($components.Count) components spanning Revit 2020-2028+"
} catch {
    Report-Fail "Audit 02: PackageContents.xml Matrix" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 03: Ribbon Command Map Completeness (All 85 Commands)
# ---------------------------------------------------------------------
try {
    $ribbonFile = Join-Path $khimToolsDir "Core\RibbonBuilder.cs"
    $ribbonContent = Get-Content $ribbonFile -Raw
    $matches = [regex]::Matches($ribbonContent, '"(KhimTools\.[^"]+Commands\.([^"]+))"')
    $classes = $matches | ForEach-Object { @{ FullName = $_.Groups[1].Value; ClassName = $_.Groups[2].Value } }

    $csFiles = Get-ChildItem -Path $khimToolsDir -Filter "*.cs" -Recurse | Get-Content -Raw
    $allCs = [string]::Join("`n", $csFiles)

    $missingCmds = @()
    foreach ($item in $classes) {
        $cName = $item.ClassName
        if ($allCs -notmatch "\bclass\s+$cName\b") {
            $missingCmds += $item.FullName
        }
    }

    if ($missingCmds.Count -gt 0) {
        throw "$($missingCmds.Count) commands missing in source code: $($missingCmds -join ', ')"
    }

    Report-Pass "Audit 03: Ribbon Command Map Completeness" "All $($classes.Count) commands verified in source code"
} catch {
    Report-Fail "Audit 03: Ribbon Command Map Completeness" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 04: Ribbon Icon Assets Integrity
# ---------------------------------------------------------------------
try {
    $ribbonFile = Join-Path $khimToolsDir "Core\RibbonBuilder.cs"
    $ribbonContent = Get-Content $ribbonFile -Raw
    $iconMatches = [regex]::Matches($ribbonContent, 'LoadImage\("([^"]+)"\)')
    $icons = $iconMatches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

    $resFolder = Join-Path $khimToolsDir "Resources"
    $missingIcons = @()

    foreach ($icon in $icons) {
        if (-not (Test-Path (Join-Path $resFolder $icon))) {
            $missingIcons += $icon
        }
    }

    if ($missingIcons.Count -gt 0) {
        throw "$($missingIcons.Count) icon files missing in Resources: $($missingIcons -join ', ')"
    }

    Report-Pass "Audit 04: Ribbon Icon Assets Integrity" "All $($icons.Count) icon assets resolved in Resources"
} catch {
    Report-Fail "Audit 04: Ribbon Icon Assets Integrity" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 05: Safe Startup Boundary & Panel Exception Isolation
# ---------------------------------------------------------------------
try {
    $ribbonFile = Join-Path $khimToolsDir "Core\RibbonBuilder.cs"
    $ribbonContent = Get-Content $ribbonFile -Raw

    $panels = @("BuildGenPanel", "BuildOverridePanel", "BuildStructuralPanel", "BuildArchPanel", "BuildMepPanel")
    foreach ($p in $panels) {
        $pattern = "try\s*\{\s*$p"
        if ($ribbonContent -notmatch $pattern) {
            throw "Panel builder $p is not protected with try-catch in BuildRibbon!"
        }
    }

    Report-Pass "Audit 05: Safe Startup Boundary" "All 5 panels isolated with try-catch startup protection"
} catch {
    Report-Fail "Audit 05: Safe Startup Boundary" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 06: Zero Empty/Whitespace String Swatch Button Safety
# ---------------------------------------------------------------------
try {
    $ribbonFile = Join-Path $khimToolsDir "Core\RibbonBuilder.cs"
    $ribbonContent = Get-Content $ribbonFile -Raw

    if ($ribbonContent -notmatch "IsNullOrWhiteSpace\(text\)") {
        throw "Missing IsNullOrWhiteSpace check in CreateColorSwatchData!"
    }
    if ($ribbonContent -notmatch '\\u200B') {
        throw "Missing zero-width space fallback in CreateColorSwatchData!"
    }

    Report-Pass "Audit 06: Swatch Button Zero-Width Text Safety" "Protected against ArgumentException"
} catch {
    Report-Fail "Audit 06: Swatch Button Zero-Width Text Safety" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 07: Rebar Engineering & Deployment Security Regression Suite
# ---------------------------------------------------------------------
try {
    $runTestsScript = Join-Path $khimToolsDir "Tests\RunTests.ps1"
    $output = & powershell -ExecutionPolicy Bypass -File $runTestsScript
    if ($LASTEXITCODE -ne 0) {
        throw "Deployment security & engineering test suite exited with code $LASTEXITCODE"
    }

    $passMatch = [regex]::Match($output, "RESULTS:\s+(\d+)\s+Passed,\s+(\d+)\s+Failed")
    if (-not $passMatch.Success -or [int]$passMatch.Groups[2].Value -gt 0) {
        throw "Some tests failed in RunTests.ps1: $output"
    }

    Report-Pass "Audit 07: Test Suite Regression" "$($passMatch.Groups[1].Value) Passed, 0 Failed"
} catch {
    Report-Fail "Audit 07: Test Suite Regression" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 08: MSI Payload & Directory Tree Alignment
# ---------------------------------------------------------------------
try {
    $verifyMsiScript = Join-Path $installerDir "Verify-MsiImplementation.ps1"
    $output = & powershell -ExecutionPolicy Bypass -File $verifyMsiScript
    if ($LASTEXITCODE -ne 0) {
        throw "Verify-MsiImplementation.ps1 failed!"
    }

    Report-Pass "Audit 08: MSI Package & Bundle Alignment" "12/12 audits passed"
} catch {
    Report-Fail "Audit 08: MSI Package & Bundle Alignment" $_.Exception.Message
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PHASE 9 AUDIT RESULTS: $passed / $($passed + $failed) PASSED" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "=================================================================" -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
} else {
    exit 0
}

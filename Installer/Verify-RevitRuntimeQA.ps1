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
Write-Host " PHASE 9: REVIT SOURCE & PACKAGING AUDIT" -ForegroundColor Cyan
Write-Host " Static checks only; does not execute commands inside Revit" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$passed = 0
$failed = 0

function Report-Pass($name, $details = "") {
    $script:passed++
    if ($details) {
        Write-Host "  [PASS] $name ($details)" -ForegroundColor Green
    } else {
        Write-Host "  [PASS] $name" -ForegroundColor Green
    }
}

function Report-Fail($name, $details) {
    $script:failed++
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

    if ($components.Count -lt 7) {
        throw "Expected at least 7 component entries for Revit 2022-2028, found $($components.Count)"
    }

    $legacySeries = @("R2022", "R2023", "R2024")
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

    Report-Pass "Audit 02: PackageContents.xml Matrix" "$($components.Count) components spanning Revit 2022-2028"
} catch {
    Report-Fail "Audit 02: PackageContents.xml Matrix" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 03: Ribbon Command Map Completeness (all distinct active command types)
# ---------------------------------------------------------------------
try {
    $ribbonFile = Join-Path $khimToolsDir "Core\RibbonBuilder.cs"
    $ribbonContent = Get-Content $ribbonFile -Raw
    $matches = [regex]::Matches($ribbonContent, '"(KhimTools\.[\w.]+\.Cmd\w+)"')
    $classes = $matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique |
        ForEach-Object { @{ FullName = $_; ClassName = ($_ -split '\.')[-1] } }

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

    Report-Pass "Audit 03: Ribbon Command Map Completeness" "All $($classes.Count) distinct active ribbon command entrypoints verified in source code"
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

    $panels = @("BuildWorkspacePanel", "BuildGenPanel", "BuildLayoutPanel", "BuildPublishPanel", "BuildOverridePanel", "BuildStructuralPanel", "BuildArchPanel", "BuildMepPanel")
    foreach ($p in $panels) {
        $pattern = "try\s*\{\s*$p"
        if ($ribbonContent -notmatch $pattern) {
            throw "Panel builder $p is not protected with try-catch in BuildRibbon!"
        }
    }

    Report-Pass "Audit 05: Safe Startup Boundary" "All $($panels.Count) panels isolated with try-catch startup protection"
} catch {
    Report-Fail "Audit 05: Safe Startup Boundary" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 06: Headerless 4x4 WPF palette and one ribbon entry point
# ---------------------------------------------------------------------
try {
    [xml]$palette = Get-Content (Join-Path $khimToolsDir "Tools/KhimGen/OverrideTool/Forms/GraphicOverdriveWindow.xaml") -Raw
    $ns = [Xml.XmlNamespaceManager]::new($palette.NameTable)
    $ns.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
    $grid = $palette.SelectSingleNode("//w:ItemsPanelTemplate/w:UniformGrid", $ns)
    if ($grid.Rows -ne "4" -or $grid.Columns -ne "4") { throw "Palette must use a 4x4 grid." }
    $chip = $palette.SelectSingleNode("//w:ItemsControl.ItemTemplate/w:DataTemplate/w:Button", $ns)
    if ($chip.Background -ne "{Binding HexColor}" -or $chip.HasAttribute("Content")) {
        throw "Swatches must bind their exact color without visible text."
    }
    $settings = $palette.SelectSingleNode("//w:Expander", $ns)
    if ($settings.IsExpanded -ne "False") { throw "Advanced options must start collapsed." }
    if ($ribbonContent -match "CreateColorSwatchData|TryHideSwatchButtonTexts") {
        throw "Legacy ribbon color-button workarounds remain."
    }
    Report-Pass "Audit 06: Compact Color Palette" "4x4 direct color binding; options collapsed; no ribbon text hacks"
} catch {
    Report-Fail "Audit 06: Compact Color Palette" $_.Exception.Message
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

    $auditMatch = [regex]::Match(($output | Out-String), "PHASE 2 AUDIT RESULTS:\s+(\d+)\s+/\s+(\d+)\s+PASSED")
    $auditDetails = if ($auditMatch.Success) {
        "$($auditMatch.Groups[1].Value)/$($auditMatch.Groups[2].Value) audits passed"
    } else {
        "All MSI implementation audits passed"
    }
    Report-Pass "Audit 08: MSI Package & Bundle Alignment" $auditDetails
} catch {
    Report-Fail "Audit 08: MSI Package & Bundle Alignment" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Audit 09: Rebar input UX and validation contract
# ---------------------------------------------------------------------
try {
    $formsDir = Join-Path $khimToolsDir "Tools\KhimStructural\RebarTool\Forms"
    $guardPath = Join-Path $formsDir "RebarFormGuard.cs"
    if (-not (Test-Path $guardPath)) {
        throw "Missing shared RebarFormGuard.cs"
    }

    $guardSource = Get-Content $guardPath -Raw
    foreach ($token in @("ErrorProvider", "StatusStrip", "VALIDATION | mm", "Kiểm tra dữ liệu", "ValidateNow")) {
        if ($guardSource -notmatch [regex]::Escape($token)) {
            throw "RebarFormGuard is missing UX contract token: $token"
        }
    }

    $guardedForms = @(
        "RectangularColumnReinforcementForm.cs",
        "CircularColumnReinforcementForm.cs",
        "BeamReinforcementForm.cs",
        "FoundationReinforcementForm.cs",
        "SlabReinforcementForm.cs"
    )
    foreach ($formName in $guardedForms) {
        $source = Get-Content (Join-Path $formsDir $formName) -Raw
        if ($source -notmatch "RebarFormGuard\.Attach") {
            throw "$formName is not connected to the shared validation guard"
        }
    }

    $duplicateHeaders = Get-ChildItem $formsDir -Filter "*.cs" |
        Where-Object { (Get-Content $_.FullName -Raw) -match "CreateHeaderBanner" }
    if ($duplicateHeaders.Count -gt 0) {
        throw "Legacy duplicate header banners remain: $($duplicateHeaders.Name -join ', ')"
    }

    Report-Pass "Audit 09: Rebar Input UX Contract" "5 forms wired to validation guard; no brand headers in source"
} catch {
    Report-Fail "Audit 09: Rebar Input UX Contract" $_.Exception.Message
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PHASE 9 AUDIT RESULTS: $passed / $($passed + $failed) PASSED" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "=================================================================" -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
} else {
    exit 0
}

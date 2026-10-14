<#
.SYNOPSIS
    Comprehensive QA Audit & Lifecycle Test for K-TOOL Mini Standalone.
#>

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path "$scriptDir\.."
$miniProj = Join-Path $projectRoot "KhimTools.Mini"
$outputMsi = Join-Path $scriptDir "Output\K-TOOL-Mini-Setup.msi"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         K-TOOL MINI QA AUDIT & LIFECYCLE TEST            " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$passed = 0
$failed = 0

function Assert-Check($name, [bool]$condition, $details = "") {
    if ($condition) {
        $script:passed++
        Write-Host "  [PASS] $name $(if($details){"($details)"})" -ForegroundColor Green
    } else {
        $script:failed++
        Write-Host "  [FAIL] $name : $details" -ForegroundColor Red
    }
}

# 1. Manifest Audits
Write-Host "`n[Audit 1] Addin Manifests Integrity & Isolation..." -ForegroundColor Cyan
$legacyAddin = Join-Path $miniProj "Deploy\Legacy\KhimTools.Mini.addin"
$modernAddin = Join-Path $miniProj "Deploy\Modern\KhimTools.Mini.addin"
$packageXml = Join-Path $miniProj "Deploy\PackageContents.xml"

Assert-Check "Legacy AddIn manifest exists" (Test-Path $legacyAddin)
Assert-Check "Modern AddIn manifest exists" (Test-Path $modernAddin)
Assert-Check "PackageContents.xml exists" (Test-Path $packageXml)

[xml]$legXml = Get-Content $legacyAddin -Raw
[xml]$modXml = Get-Content $modernAddin -Raw
[xml]$pkgXml = Get-Content $packageXml -Raw

$miniGuid = "A5B4C3D2-1E0F-4A9B-8C7D-6E5F4A3B2C1D"
$fullKToolGuid = "4F1B2C3D-5E6F-4A7B-8C9D-0E1F2A3B4C5D"

Assert-Check "Legacy AddInId matches isolated GUID" ($legXml.RevitAddIns.AddIn.AddInId.ToUpper() -eq $miniGuid)
Assert-Check "Modern AddInId matches isolated GUID" ($modXml.RevitAddIns.AddIn.AddInId.ToUpper() -eq $miniGuid)
Assert-Check "GUID is strictly isolated from full K-TOOL" ($legXml.RevitAddIns.AddIn.AddInId.ToUpper() -ne $fullKToolGuid)
Assert-Check "Legacy Assembly is KhimTools.Mini.dll" ($legXml.RevitAddIns.AddIn.Assembly -eq "KhimTools.Mini.dll")
Assert-Check "Modern Assembly is KhimTools.Mini.dll" ($modXml.RevitAddIns.AddIn.Assembly -eq "KhimTools.Mini.dll")
Assert-Check "FullClassName is KhimTools.Mini.App" ($legXml.RevitAddIns.AddIn.FullClassName -eq "KhimTools.Mini.App")

# 2. PackageContents Series Coverage
Write-Host "`n[Audit 2] PackageContents Multi-Year Mapping..." -ForegroundColor Cyan
$entries = $pkgXml.ApplicationPackage.Components
$seriesList = @($entries | ForEach-Object { $_.RuntimeRequirements.SeriesMin })
Assert-Check "Contains Revit 2025 component" ($seriesList -contains "R2025")
Assert-Check "Contains Revit 2026 component" ($seriesList -contains "R2026")
Assert-Check "Contains Revit 2024 component" ($seriesList -contains "R2024")
Assert-Check "Contains Revit 2023 component" ($seriesList -contains "R2023")
Assert-Check "Contains Revit 2022 component" ($seriesList -contains "R2022")

# 3. Binary Static Reflection
Write-Host "`n[Audit 3] Binary Assembly Inspection..." -ForegroundColor Cyan
$modernDll = Join-Path $miniProj "bin\Release\net8.0-windows\KhimTools.Mini.dll"
$legacyDll = Join-Path $miniProj "bin\Release\net48\KhimTools.Mini.dll"

Assert-Check "Modern net8.0-windows DLL exists" (Test-Path $modernDll)
Assert-Check "Legacy net48 DLL exists" (Test-Path $legacyDll)

# 4. MSI Windows Installer COM Table Verification
Write-Host "`n[Audit 4] MSI Database COM Inspection..." -ForegroundColor Cyan
Assert-Check "K-TOOL-Mini-Setup.msi exists" (Test-Path $outputMsi)

$wi = New-Object -ComObject WindowsInstaller.Installer
$db = $wi.OpenDatabase($outputMsi, 0)

$props = @{}
$view = $db.OpenView("SELECT Property, Value FROM Property")
$view.Execute()
while ($rec = $view.Fetch()) {
    $props[$rec.StringData(1)] = $rec.StringData(2)
}
$view.Close()

Assert-Check "MSI ProductName is 'K-TOOL Mini'" ($props["ProductName"] -eq "K-TOOL Mini")
Assert-Check "MSI Manufacturer is 'SJTL'" ($props["Manufacturer"] -eq "SJTL")
Assert-Check "MSI UpgradeCode is isolated" ($props["UpgradeCode"] -eq "{E2B1A0F9-7C6D-4E5F-8A9B-0C1D2E3F4A5B}")
Assert-Check "MSI ALLUSERS is perMachine (1)" ($props["ALLUSERS"] -eq "1")

# Check Features
$features = @{}
$view = $db.OpenView("SELECT Feature, Level FROM Feature")
$view.Execute()
while ($rec = $view.Fetch()) {
    $features[$rec.StringData(1)] = [int]$rec.StringData(2)
}
$view.Close()
Assert-Check "Feature Complete registered" ($features.ContainsKey("Complete"))
Assert-Check "Feature CoreTools registered" ($features.ContainsKey("CoreTools"))
Assert-Check "Feature RevitModern registered" ($features.ContainsKey("RevitModern"))
Assert-Check "Feature RevitLegacy registered" ($features.ContainsKey("RevitLegacy"))

# Check Components
$components = @{}
$view = $db.OpenView("SELECT Component FROM Component")
$view.Execute()
while ($rec = $view.Fetch()) {
    $components[$rec.StringData(1)] = $true
}
$view.Close()
Assert-Check "Component C_PackageContentsXml registered" ($components.ContainsKey("C_PackageContentsXml"))
Assert-Check "Component C_RegistryOwnership registered" ($components.ContainsKey("C_RegistryOwnership"))
Assert-Check "Component C_ModernAddin registered" ($components.ContainsKey("C_ModernAddin"))
Assert-Check "Component C_ModernDlls registered" ($components.ContainsKey("C_ModernDlls"))
Assert-Check "Component C_LegacyAddin registered" ($components.ContainsKey("C_LegacyAddin"))
Assert-Check "Component C_LegacyDlls registered" ($components.ContainsKey("C_LegacyDlls"))

# 5. MSI Extraction & Clean Payload Verification
Write-Host "`n[Audit 5] MSI Extraction & Payload Verification..." -ForegroundColor Cyan
$tempExtract = Join-Path $env:TEMP "KToolMini_LifecycleTest"
if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }

$proc = Start-Process -FilePath "msiexec.exe" -ArgumentList "/a `"$outputMsi`" /qn TARGETDIR=`"$tempExtract`"" -Wait -PassThru
Assert-Check "MSI administrative extract succeeded" ($proc.ExitCode -eq 0)

$extractedBundle = Join-Path $tempExtract "CommApp\Autodesk\ApplicationPlugins\KhimTools.Mini.bundle"
Assert-Check "Extracted KhimTools.Mini.bundle exists" (Test-Path $extractedBundle)
Assert-Check "Extracted PackageContents.xml exists" (Test-Path (Join-Path $extractedBundle "PackageContents.xml"))
Assert-Check "Extracted Modern addin exists" (Test-Path (Join-Path $extractedBundle "Contents\Modern\KhimTools.Mini.addin"))
Assert-Check "Extracted Modern DLL exists" (Test-Path (Join-Path $extractedBundle "Contents\Modern\KhimTools.Mini.dll"))
Assert-Check "Extracted Modern deps.json exists" (Test-Path (Join-Path $extractedBundle "Contents\Modern\KhimTools.Mini.deps.json"))
Assert-Check "Extracted Modern Newtonsoft exists" (Test-Path (Join-Path $extractedBundle "Contents\Modern\Newtonsoft.Json.dll"))
Assert-Check "Extracted Legacy addin exists" (Test-Path (Join-Path $extractedBundle "Contents\Legacy\KhimTools.Mini.addin"))
Assert-Check "Extracted Legacy DLL exists" (Test-Path (Join-Path $extractedBundle "Contents\Legacy\KhimTools.Mini.dll"))
Assert-Check "Extracted Legacy Newtonsoft exists" (Test-Path (Join-Path $extractedBundle "Contents\Legacy\Newtonsoft.Json.dll"))

# Verify excluded modules: Rebar, MEP, Arch, PDF, etc.
$allExtracted = Get-ChildItem -Path $extractedBundle -Recurse | Select-Object -ExpandProperty FullName
$hasRebar = $allExtracted | Where-Object { $_ -match "RebarShapes" }
$hasPdfSharp = $allExtracted | Where-Object { $_ -match "PdfSharp" }
$hasCommunityToolkit = $allExtracted | Where-Object { $_ -match "CommunityToolkit" }

Assert-Check "Excluded RebarShapes families (clean payload)" ($null -eq $hasRebar)
Assert-Check "Excluded PdfSharp (clean payload)" ($null -eq $hasPdfSharp)
Assert-Check "Excluded CommunityToolkit (clean payload)" ($null -eq $hasCommunityToolkit)

# Clean up
Remove-Item $tempExtract -Recurse -Force

# 6. Summary
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Tests Passed: $passed, Failed: $failed" -ForegroundColor $(if($failed -eq 0){"Green"}else{"Red"})
Write-Host "==========================================================" -ForegroundColor Cyan

if ($failed -gt 0) {
    throw "Audit failed with $failed error(s)."
}

# ==============================================================================
# K-TOOLS MSI IMPLEMENTATION VERIFICATION AUDIT SUITE (Phase 2)
# ==============================================================================
param(
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path "$scriptDir\.."
$msiDir = Join-Path $scriptDir "K-TOOLS.MSI"
$bootDir = Join-Path $scriptDir "K-TOOLS.Bootstrapper"

$totalTests = 0
$passedTests = 0
$failedTests = 0

function Report-Result($name, $passed, $details = "") {
    $script:totalTests++
    if ($passed) {
        $script:passedTests++
        Write-Host "  [PASS] $name" -ForegroundColor Green
    } else {
        $script:failedTests++
        Write-Host "  [FAIL] $name - $details" -ForegroundColor Red
    }
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PHASE 2: VERIFY MSI IMPLEMENTATION AUDIT SUITE" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# AUDIT 1: WiX XML Well-Formedness
# -----------------------------------------------------------------------------
$wxsFiles = Get-ChildItem -Path $scriptDir -Recurse -Filter "*.wxs"
$xmlErrors = @()
foreach ($file in $wxsFiles) {
    try {
        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($file.FullName)
    } catch {
        $xmlErrors += "$($file.Name): $($_.Exception.Message)"
    }
}
Report-Result "Audit 01: XML Well-Formedness ($($wxsFiles.Count) files)" ($xmlErrors.Count -eq 0) ($xmlErrors -join "; ")

# -----------------------------------------------------------------------------
# AUDIT 2: Source File References Exist on Disk
# -----------------------------------------------------------------------------
$missingSources = @()
$foundSources = 0
foreach ($file in $wxsFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    $matches = [regex]::Matches($content, 'Source="([^"]+)"')
    foreach ($m in $matches) {
        $relPath = $m.Groups[1].Value
        # Resolve path relative to K-TOOLS.MSI project directory
        $resolved = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($msiDir, $relPath))
        if (Test-Path $resolved) {
            $foundSources++
        } else {
            $missingSources += "$($file.Name) -> $relPath"
        }
    }
}
Report-Result "Audit 02: Source File References Exist ($foundSources resolved, $($missingSources.Count) missing)" ($missingSources.Count -eq 0) ($missingSources -join "; ")

# -----------------------------------------------------------------------------
# AUDIT 3: Definition ID Uniqueness
# -----------------------------------------------------------------------------
$definedIds = @{}
$duplicateIds = @()
$tagsToInspect = @("Component", "File", "Directory", "Feature", "ComponentGroup", "Property", "RegistrySearch")
foreach ($file in $wxsFiles) {
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($file.FullName)
    foreach ($tag in $tagsToInspect) {
        $nodes = $doc.GetElementsByTagName($tag)
        foreach ($node in $nodes) {
            $id = $node.GetAttribute("Id")
            if (![string]::IsNullOrEmpty($id)) {
                $key = "$tag::$id"
                if ($definedIds.ContainsKey($key)) {
                    $duplicateIds += "$key in $($file.Name) and $($definedIds[$key])"
                } else {
                    $definedIds[$key] = $file.Name
                }
            }
        }
    }
}
Report-Result "Audit 03: Definition ID Uniqueness ($($definedIds.Count) IDs checked)" ($duplicateIds.Count -eq 0) ($duplicateIds -join "; ")

# -----------------------------------------------------------------------------
# AUDIT 4: GUID Uniqueness
# -----------------------------------------------------------------------------
$guids = @{}
$duplicateGuids = @()
foreach ($file in $wxsFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    $matches = [regex]::Matches($content, 'Guid="\{?([a-fA-F0-9\-]{36})\}?"')
    foreach ($m in $matches) {
        $g = $m.Groups[1].Value.ToUpper()
        if ($guids.ContainsKey($g)) {
            $duplicateGuids += "$g in $($file.Name) and $($guids[$g])"
        } else {
            $guids[$g] = $file.Name
        }
    }
}
Report-Result "Audit 04: Component & Package GUID Uniqueness ($($guids.Count) GUIDs checked)" ($duplicateGuids.Count -eq 0 -and $guids.Count -gt 0) ($duplicateGuids -join "; ")

# -----------------------------------------------------------------------------
# AUDIT 5: Feature-to-ComponentRef Linkage
# -----------------------------------------------------------------------------
$allComponentIds = @{}
$compFiles = Get-ChildItem -Path "$msiDir\Components" -Filter "*.wxs"
foreach ($cf in $compFiles) {
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($cf.FullName)
    foreach ($c in $doc.GetElementsByTagName("Component")) {
        $id = $c.GetAttribute("Id")
        if (![string]::IsNullOrEmpty($id)) {
            $allComponentIds[$id] = $true
        }
    }
}

$featFile = "$msiDir\Features.wxs"
$featDoc = New-Object System.Xml.XmlDocument
$featDoc.Load($featFile)
$unresolvedRefs = @()
$totalRefs = 0
foreach ($cr in $featDoc.GetElementsByTagName("ComponentRef")) {
    $totalRefs++
    $refId = $cr.GetAttribute("Id")
    if (!$allComponentIds.ContainsKey($refId)) {
        $unresolvedRefs += $refId
    }
}
Report-Result "Audit 05: Feature-to-ComponentRef Linkage ($totalRefs refs checked)" ($unresolvedRefs.Count -eq 0) ($unresolvedRefs -join "; ")

# -----------------------------------------------------------------------------
# AUDIT 6: Directory ID Consistency
# -----------------------------------------------------------------------------
$dirDoc = New-Object System.Xml.XmlDocument
$dirDoc.Load("$msiDir\Directories.wxs")
$definedDirs = @{}
foreach ($d in $dirDoc.GetElementsByTagName("Directory")) {
    $id = $d.GetAttribute("Id")
    if (![string]::IsNullOrEmpty($id)) { $definedDirs[$id] = $true }
}
foreach ($d in $dirDoc.GetElementsByTagName("StandardDirectory")) {
    $id = $d.GetAttribute("Id")
    if (![string]::IsNullOrEmpty($id)) { $definedDirs[$id] = $true }
}

$unresolvedDirs = @()
foreach ($cf in $compFiles) {
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($cf.FullName)
    foreach ($cg in $doc.GetElementsByTagName("ComponentGroup")) {
        $targetDir = $cg.GetAttribute("Directory")
        if (![string]::IsNullOrEmpty($targetDir) -and !$definedDirs.ContainsKey($targetDir)) {
            $unresolvedDirs += "$($cf.Name) -> $targetDir"
        }
    }
    foreach ($c in $doc.GetElementsByTagName("Component")) {
        $targetDir = $c.GetAttribute("Directory")
        if (![string]::IsNullOrEmpty($targetDir) -and !$definedDirs.ContainsKey($targetDir)) {
            $unresolvedDirs += "$($cf.Name) -> $targetDir"
        }
    }
}
Report-Result "Audit 06: Directory ID Consistency ($($definedDirs.Count) directories declared)" ($unresolvedDirs.Count -eq 0) ($unresolvedDirs -join "; ")

# -----------------------------------------------------------------------------
# AUDIT 7: Security Boundary — ProgramData Isolation
# -----------------------------------------------------------------------------
$appDataViolations = @()
foreach ($cf in $compFiles) {
    $content = [System.IO.File]::ReadAllText($cf.FullName, [System.Text.Encoding]::UTF8)
    if ($content -match 'AppDataFolder|LOCALAPPDATA|APPDATA') {
        $appDataViolations += $cf.Name
    }
}
Report-Result "Audit 07: Security Boundary (Per-machine ProgramData isolation, 0 AppData writes)" ($appDataViolations.Count -eq 0) ($appDataViolations -join "; ")

# -----------------------------------------------------------------------------
# AUDIT 8: UpgradeCode Separation (Package.wxs vs Bundle.wxs)
# -----------------------------------------------------------------------------
$pkgDoc = New-Object System.Xml.XmlDocument
$pkgDoc.Load("$msiDir\Package.wxs")
$bundleDoc = New-Object System.Xml.XmlDocument
$bundleDoc.Load("$bootDir\Bundle.wxs")

$msiUpgradeCode = $pkgDoc.GetElementsByTagName("Package")[0].GetAttribute("UpgradeCode")
$bundleUpgradeCode = $bundleDoc.GetElementsByTagName("Bundle")[0].GetAttribute("UpgradeCode")
$upgradeCodesDistinct = (![string]::IsNullOrEmpty($msiUpgradeCode) -and 
                         ![string]::IsNullOrEmpty($bundleUpgradeCode) -and 
                         $msiUpgradeCode -ne $bundleUpgradeCode)
Report-Result "Audit 08: UpgradeCode Separation (MSI: $msiUpgradeCode, Bundle: $bundleUpgradeCode)" $upgradeCodesDistinct

# -----------------------------------------------------------------------------
# AUDIT 9: MajorUpgrade Safety (Schedule="afterInstallInitialize")
# -----------------------------------------------------------------------------
$majorUpgrade = $pkgDoc.GetElementsByTagName("MajorUpgrade")
$scheduleSafe = ($majorUpgrade.Count -gt 0 -and $majorUpgrade[0].GetAttribute("Schedule") -eq "afterInstallInitialize")
Report-Result "Audit 09: MajorUpgrade Safety (Schedule='afterInstallInitialize')" $scheduleSafe

# -----------------------------------------------------------------------------
# AUDIT 10: Revit Version Detection Multi-Year Matrix (2022-2028)
# -----------------------------------------------------------------------------
$revitDetDoc = New-Object System.Xml.XmlDocument
$revitDetDoc.Load("$msiDir\Conditions\RevitDetection.wxs")
$searches = $revitDetDoc.GetElementsByTagName("RegistrySearch")
$missingYears = @()
foreach ($year in 2022..2028) {
    $prop = "REVIT_${year}_DETECTED"
    $found = $false
    foreach ($s in $searches) {
        if ($s.ParentNode.GetAttribute("Id") -eq $prop -or $s.GetAttribute("Property") -eq $prop) {
            $found = $true
            break
        }
    }
    if (!$found) { $missingYears += $year }
}
Report-Result "Audit 10: Revit Version Detection Matrix (2022-2028: $($searches.Count) rules)" ($missingYears.Count -eq 0) ($missingYears -join ", ")

# -----------------------------------------------------------------------------
# AUDIT 11: .NET Framework 4.8 and .NET 8.0 Prerequisite Rules
# -----------------------------------------------------------------------------
$dotnetDoc = New-Object System.Xml.XmlDocument
$dotnetDoc.Load("$msiDir\Conditions\DotNetPrereq.wxs")
$hasNet48 = ($dotnetDoc.InnerXml -match "NETFRAMEWORK48" -or $dotnetDoc.InnerXml -match "528040")
$hasNet8 = ($dotnetDoc.InnerXml -match "DOTNET8_DESKTOP_INSTALLED" -or $dotnetDoc.InnerXml -match "Microsoft.WindowsDesktop.App")
Report-Result "Audit 11: Runtime Prerequisites (.NET 4.8 & .NET 8.0 conditions)" ($hasNet48 -and $hasNet8)

# -----------------------------------------------------------------------------
# AUDIT 12: Deployment Security Test Suite Regression
# -----------------------------------------------------------------------------
$testRunSuccess = $false
$testDetails = ""
try {
    $testScript = Join-Path $repoRoot "KhimTools\Tests\RunTests.ps1"
    $testOut = & powershell -ExecutionPolicy Bypass -File $testScript 2>&1
    if ($LASTEXITCODE -eq 0 -and ($testOut -match "RESULTS:\s+(\d+)\s+Passed,\s+0\s+Failed")) {
        $testRunSuccess = $true
        $testDetails = "$($Matches[1])/$($Matches[1]) security tests passed"
    } else {
        $testDetails = "Failed or unexpected output: " + ($testOut | Out-String)
    }
} catch {
    $testDetails = $_.Exception.Message
}
Report-Result "Audit 12: Deployment Security Test Suite Regression" $testRunSuccess $testDetails

# -----------------------------------------------------------------------------
# AUDIT 13: MSI/updater ownership boundaries
# -----------------------------------------------------------------------------
$classifierSource = [System.IO.File]::ReadAllText(
    (Join-Path $repoRoot "KhimTools\App\Deployment\InstallationClassifier.cs"))
$updaterSource = [System.IO.File]::ReadAllText(
    (Join-Path $repoRoot "KhimTools\Tools\KhimGen\Updater\Services\UpdateService.cs"))
$projectSource = [System.IO.File]::ReadAllText(
    (Join-Path $repoRoot "KhimTools\KhimTools.csproj"))

$dualHiveDetection = $classifierSource.Contains("Registry.LocalMachine") -and
                     $classifierSource.Contains("Registry.CurrentUser")
$noLegacyExeLaunch = -not $updaterSource.Contains("LocalApplicationData") -and
                     -not $updaterSource.Contains("KhimTools_Installer.exe") -and
                     -not $updaterSource.Contains("K-TOOLS_Installer.exe")
$deployIsOptIn = $projectSource.Contains("'`$(DeployKhimToolsBundle)' == 'true'")

Report-Result "Audit 13: MSI/updater single-owner boundary" `
    ($dualHiveDetection -and $noLegacyExeLaunch -and $deployIsOptIn) `
    "DualHive=$dualHiveDetection, NoLegacyExe=$noLegacyExeLaunch, DeployOptIn=$deployIsOptIn"

# -----------------------------------------------------------------------------
# AUDIT 14: Release version and update metadata alignment
# -----------------------------------------------------------------------------
$manifest = Get-Content (Join-Path $repoRoot "update_info.json") -Raw | ConvertFrom-Json
$projectDoc = New-Object System.Xml.XmlDocument
$projectDoc.Load((Join-Path $repoRoot "KhimTools\KhimTools.csproj"))
$bundleDoc = New-Object System.Xml.XmlDocument
$bundleDoc.Load((Join-Path $repoRoot "KhimTools\Deploy\PackageContents.xml"))

$packageVersion = $pkgDoc.GetElementsByTagName("Package")[0].GetAttribute("Version")
$projectVersion = $projectDoc.Project.PropertyGroup.Version | Select-Object -First 1
$bundleVersion = $bundleDoc.ApplicationPackage.GetAttribute("AppVersion")
$manifestVersion = ([string]$manifest.latest_version).TrimStart("v")
$officialUpdateUrl = [string]$manifest.download_url_msi -match "^https://github\.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/"
$versionsAligned = $packageVersion -eq $projectVersion -and
                   $packageVersion -eq $bundleVersion -and
                   $packageVersion -eq $manifestVersion

Report-Result "Audit 14: Release metadata alignment" `
    ($versionsAligned -and $officialUpdateUrl) `
    "MSI=$packageVersion, Project=$projectVersion, Bundle=$bundleVersion, Manifest=$manifestVersion, OfficialUrl=$officialUpdateUrl"

# -----------------------------------------------------------------------------
# SUMMARY
# -----------------------------------------------------------------------------
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PHASE 2 AUDIT RESULTS: $passedTests / $totalTests PASSED" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
Write-Host "=================================================================" -ForegroundColor Cyan

if ($failedTests -gt 0) {
    exit 1
} else {
    exit 0
}

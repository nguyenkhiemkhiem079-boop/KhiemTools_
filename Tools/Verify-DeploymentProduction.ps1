$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$checks = 0

function Assert-Condition([bool]$condition, [string]$label) {
    if (-not $condition) { throw "FAIL: $label" }
    $script:checks++
    Write-Output "PASS deployment:$label"
}

$package = [xml](Get-Content (Join-Path $repoRoot 'KhimTools\Deploy\PackageContents.xml') -Raw)
$series = @($package.ApplicationPackage.Components | ForEach-Object { $_.RuntimeRequirements.SeriesMin } | Sort-Object)
$supportedSeries = @('R2022','R2023','R2024','R2025','R2026') | Sort-Object
Assert-Condition (($series -join ',') -eq ($supportedSeries -join ',')) 'manifest-has-exact-supported-years-2022-2026'
Assert-Condition (@($package.ApplicationPackage.Components | Where-Object { $_.RuntimeRequirements.SeriesMin -in @('R2027','R2028') }).Count -eq 0) 'manifest-excludes-unbuilt-2027-2028'

$detection = Get-Content (Join-Path $repoRoot 'Installer\K-TOOLS.MSI\Conditions\RevitDetection.wxs') -Raw
$legacyWxs = Get-Content (Join-Path $repoRoot 'Installer\K-TOOLS.MSI\Components\Components.Legacy.wxs') -Raw
$modernWxs = Get-Content (Join-Path $repoRoot 'Installer\K-TOOLS.MSI\Components\Components.Modern.wxs') -Raw
$legacyEntry = @($package.ApplicationPackage.Components | Where-Object { $_.RuntimeRequirements.SeriesMin -in @('R2022','R2023','R2024') } | ForEach-Object { $_.ComponentEntry.ModuleName } | Sort-Object -Unique)
$modernEntry = @($package.ApplicationPackage.Components | Where-Object { $_.RuntimeRequirements.SeriesMin -in @('R2025','R2026') } | ForEach-Object { $_.ComponentEntry.ModuleName } | Sort-Object -Unique)
Assert-Condition (($legacyEntry.Count -eq 1) -and ($legacyEntry[0] -eq './Contents/Legacy/KhimTools.addin')) 'legacy-years-map-to-legacy-manifest'
Assert-Condition (($modernEntry.Count -eq 1) -and ($modernEntry[0] -eq './Contents/Modern/KhimTools.addin')) 'modern-years-map-to-modern-manifest'
Assert-Condition ($legacyWxs.Contains('bin\Release\net48\KhimTools.dll') -and $legacyWxs.Contains('bin\Release\net48\KhimTools.Domain.dll')) 'legacy-msi-component-uses-net48-binaries-and-domain-dependency'
Assert-Condition ($modernWxs.Contains('bin\Release\net8.0-windows\KhimTools.dll') -and $modernWxs.Contains('bin\Release\net8.0-windows\KhimTools.Domain.dll')) 'modern-msi-component-uses-net8-binaries-and-domain-dependency'
Assert-Condition (($detection.Contains('SearchRevit2025') -and $detection.Contains('SearchRevit2026')) -and (-not $detection.Contains('SearchRevit2027')) -and (-not $detection.Contains('SearchRevit2028'))) 'msi-detection-matches-supported-years-only'

$builder = Get-Content (Join-Path $repoRoot 'Installer\Build-Installer.ps1') -Raw
$builderBuildsBoth = $builder.Contains('Framework = "net48"; Revit = "2024"') -and $builder.Contains('Framework = "net8.0-windows"; Revit = "2025"')
Assert-Condition ($builderBuildsBoth -and $builder.Contains('-p:DeployKhimToolsBundle=false')) 'release-package-builds-current-2024-and-2025-source-without-machine-deploy'
Assert-Condition ($builder.Contains('-d "ProductVersion=$Version"') -and $builder.Contains('[string]$Version = "2.7.2"')) 'msi-and-bootstrapper-use-aligned-explicit-version'

$output = Join-Path $repoRoot 'Installer\Output'
$msi = Join-Path $output 'K-TOOLS.msi'
$setup = Join-Path $output 'K-TOOLS-Setup.exe'
$checksums = Join-Path $output 'SHA256SUMS.txt'
Assert-Condition (Test-Path $msi) 'fresh-msi-artifact-exists'
Assert-Condition (Test-Path $setup) 'fresh-bootstrapper-artifact-exists'
Assert-Condition (Test-Path $checksums) 'local-checksum-manifest-exists'

$manifestLines = @(Get-Content $checksums | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$checksumNames = @($manifestLines | ForEach-Object { ($_ -split '\s+', 2)[1] } | Sort-Object)
$expectedChecksumNames = @('K-TOOLS.msi','K-TOOLS-Setup.exe') | Sort-Object
$checksumNamesExact = ($checksumNames.Count -eq 2) -and (@(Compare-Object -ReferenceObject $expectedChecksumNames -DifferenceObject $checksumNames).Count -eq 0)
Assert-Condition $checksumNamesExact 'checksum-manifest-lists-only-current-installer-artifacts'
$hashesValid = $true
foreach ($artifact in @($msi, $setup)) {
    $name = [System.IO.Path]::GetFileName($artifact)
    $line = $manifestLines | Where-Object { ($_ -split '\s+', 2)[1] -eq $name } | Select-Object -First 1
    if (-not $line) { $hashesValid = $false; break }
    $expected = ($line -split '\s+', 2)[0]
    $actual = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
    if ($expected -ine $actual) { $hashesValid = $false; break }
}
Assert-Condition $hashesValid 'checksum-manifest-matches-artifact-bytes'

Write-Output "DEPLOYMENT_PRODUCTION_CHECKS=$checks"
Write-Output 'DEPLOYMENT_PRODUCTION_ACCEPTANCE=PASS'
Write-Output 'INSTALL_RUNTIME=MANUAL_REQUIRED; no install/upgrade/repair/uninstall was executed.'

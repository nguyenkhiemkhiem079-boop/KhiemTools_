$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $root 'Tests/KhimTools.Revit.Compatibility.Tests/KhimTools.Revit.Compatibility.Tests.csproj'
$checks = 0
foreach ($contract in @(@{ Api = 'Old'; Count = 8 }, @{ Api = 'New'; Count = 12 })) {
    $output = & dotnet run --project $fixture -c Release "-p:CompatApi=$($contract.Api)"
    if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch "REVIT_COMPAT_UNIT=PASS \($($contract.Count) checks\)") {
        throw "Revit compatibility $($contract.Api) synthetic contract failed: $output"
    }
    $checks += $contract.Count
    Write-Output ($output -join "`n")
}
$logs = Join-Path $root 'artifacts/revit-compatibility'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($year in @('2022','2023','2024','2025','2026')) {
    $framework = if ([int]$year -ge 2025) { 'net8.0-windows' } else { 'net48' }
    $buildOutput = Join-Path $logs "outputs/$year/bin"
    $buildIntermediate = Join-Path $logs "outputs/$year/obj"
    if (!(Test-Path "C:\Program Files\Autodesk\Revit $year\RevitAPI.dll")) {
        throw "BLOCKED: Revit $year API is unavailable; the full advertised compatibility matrix cannot be certified."
    }
    $output = & dotnet build (Join-Path $root 'KhimTools/KhimTools.csproj') -c Release -f $framework --nologo `
        "-p:RevitRequestedVersion=$year" "-p:RevitVersionForReference=$year" "-p:OutputPath=$buildOutput/" "-p:IntermediateOutputPath=$buildIntermediate/" -p:DeployKhimToolsBundle=false 2>&1
    $exitCode = $LASTEXITCODE
    $output | Set-Content (Join-Path $logs "build-$year.log")
    if ($exitCode -ne 0) { throw "Revit $year compatibility build failed; see artifacts/revit-compatibility/build-$year.log" }
    $checks++
    Write-Output "PASS: Revit $year $framework source compilation; live host NOT_EXECUTED."
}
Write-Output "REVIT_COMPATIBILITY=PASS ($checks checks: 20 synthetic contract assertions + 5 actual API builds)"

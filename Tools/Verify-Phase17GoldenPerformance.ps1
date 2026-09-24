$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Tests\KhimTools.Golden.Regression\KhimTools.Golden.Regression.csproj'
$output = & dotnet run --project $project --configuration Release --no-launch-profile
if ($LASTEXITCODE -ne 0) { throw "Phase 17 golden regression harness failed (exit $LASTEXITCODE)." }
$text = $output -join "`n"
if ($text -notmatch 'GOLDEN_REGRESSION_ACCEPTANCE=PASS \(36 assertions\)') {
    throw 'Phase 17 harness did not report the expected 36 executed assertions.'
}
if ($text -notmatch 'EDGE_CASE_ACCEPTANCE=PASS \(QS/MEP/Architectural domain calculations\)' -or
    $text -notmatch 'REBAR_EDGE_CASE_STATUS=HOST_REQUIRED / NOT_EXECUTED' -or $text -notmatch 'PERFORMANCE_ACCEPTANCE=PASS' -or
    $text -notmatch 'STRESS_ACCEPTANCE=PASS \(100, 1,000, 10,000 synthetic records\)' -or
    $text -notmatch 'REBAR_HOST_GOLDEN=HOST_REQUIRED / NOT_EXECUTED') {
    throw 'Phase 17 harness omitted a required status or falsely omitted the Rebar host boundary.'
}
$text | ForEach-Object { Write-Host $_ }
exit 0

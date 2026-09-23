[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$base = Join-Path $repoRoot 'KhimTools\Tools\KhimStructural\RebarTool'
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [bool]$Pass, [string]$Evidence) {
    $script:checks.Add([pscustomobject]@{ Name = $Name; Pass = $Pass; Evidence = $Evidence })
}

$required = @(
    'Core\BeamRebarGenerator.cs',
    'Core\RectangularColumnRebarGenerator.cs',
    'Core\CircularColumnRebarGenerator.cs',
    'Core\SlabRebarGenerator.cs',
    'Core\FoundationRebarGenerator.cs',
    'Core\RebarGenerationReport.cs',
    'Core\RebarGenerationFailurePreprocessor.cs',
    'Core\RebarShapeCreationHelper.cs',
    'Core\RebarCoverHelper.cs',
    'Core\RebarHookHelper.cs',
    'Core\RebarLapSpliceHelper.cs',
    'Forms\BeamReinforcementForm.cs',
    'Forms\RectangularColumnReinforcementForm.cs',
    'Forms\CircularColumnReinforcementForm.cs',
    'Forms\SlabReinforcementForm.cs',
    'Forms\FoundationReinforcementForm.cs'
)
foreach ($relative in $required) {
    $path = Join-Path $base $relative
    Add-Check "production-source:$relative" (Test-Path -LiteralPath $path) $path
}

$runtimePath = Join-Path $repoRoot 'KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\RebarRuntimeFixtures.cs'
$runtimeText = if (Test-Path -LiteralPath $runtimePath) { Get-Content -Raw $runtimePath } else { '' }
foreach ($fixture in @('RC-STATION', 'RC-FULL', 'CC', 'BR', 'SR', 'FR')) {
    Add-Check "runtime-fixture:$fixture" ($runtimeText.Contains('"' + $fixture + '"')) $runtimePath
}

$columnText = Get-Content -Raw (Join-Path $base 'Forms\RectangularColumnReinforcementForm.cs')
$commonPreview = Get-ChildItem -LiteralPath $base -Recurse -File -Filter '*Preview*' |
    Where-Object { $_.Name -notmatch 'Reference|Drawing|View' }
$hasDetachedPreviewPipeline = $false
Add-Check 'rectangular-column-2d-form-preview' ($columnText.Contains('PreviewPanel_Paint')) 'Form paints a responsive 2D schematic.'
Add-Check 'common-detached-preview-pipeline' $hasDetachedPreviewPipeline 'No shared detached preview/request/execution pipeline was found.'
Add-Check 'solver-derived-precommit-3d-preview' $false 'Persistent post-generation inspection views are not a non-destructive preview.'

$wallCommand = Get-ChildItem -LiteralPath (Join-Path $base 'Commands') -File -Filter '*Wall*Rebar*.cs'
$wallGenerator = Get-ChildItem -LiteralPath (Join-Path $base 'Core') -File -Filter '*Wall*Rebar*.cs'
Add-Check 'wall-rebar-explicitly-unsupported' ($wallCommand.Count -eq 0 -and $wallGenerator.Count -eq 0) 'No wall command/generator; documentation must retain this boundary.'

$passCount = @($checks | Where-Object Pass).Count
$failures = @($checks | Where-Object { -not $_.Pass })
foreach ($check in $checks) {
    $status = if ($check.Pass) { 'PASS' } else { 'BLOCKED' }
    Write-Output ("{0} {1} — {2}" -f $status, $check.Name, $check.Evidence)
}
Write-Output ("KREBAR_STATIC_CHECKS={0}/{1}" -f $passCount, $checks.Count)
if ($failures.Count -gt 0) {
    Write-Output 'KREBAR_PREVIEW_ACCEPTANCE=BLOCKED'
    Write-Output 'KREBAR_STATIC_ACCEPTANCE=PARTIAL'
    exit 1
}

Write-Output 'KREBAR_STATIC_ACCEPTANCE=PASS'
exit 0

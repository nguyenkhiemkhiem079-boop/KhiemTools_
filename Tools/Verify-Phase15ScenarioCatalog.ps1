$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $root 'Docs\Runtime\Phase15-Host-Scenario-Catalog.json'
$registryPath = Join-Path $root 'KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaRegistry.cs'
$fixtureRoot = Join-Path $root 'KhimTools\Tools\KhimGen\RuntimeQa\Fixtures'
$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
$registry = Get-Content -LiteralPath $registryPath -Raw
$checks = 0

function Assert([bool]$condition, [string]$label) {
    if (-not $condition) { throw "FAIL: $label" }
    $script:checks++
}

Assert (($catalog.targetRevitVersions -join ',') -eq '2024,2025') 'Both supported host versions are explicitly catalogued'
Assert (($catalog.statusVocabulary -join ',') -eq 'NOT_EXECUTED,PASS,FAIL,BLOCKED,MANUAL_REQUIRED') 'Host status vocabulary preserves non-executed/manual states'

$fixtureIdsInSource = @{}
foreach ($file in Get-ChildItem -LiteralPath $fixtureRoot -Filter '*.cs' -File) {
    $source = Get-Content -LiteralPath $file.FullName -Raw
    $idMatches = [regex]::Matches($source, 'public override string Id\s*(?:\{\s*get\s*\{\s*return|=>)\s*"([^"]+)"')
    foreach ($idMatch in $idMatches) {
        $prefix = $source.Substring(0, $idMatch.Index)
        $classMatches = [regex]::Matches($prefix, 'public (?:(?:abstract|sealed) )?class\s+(\w+)')
        if ($classMatches.Count -eq 0) { continue }
        $className = $classMatches[$classMatches.Count - 1].Groups[1].Value
        $fixtureIdsInSource[$idMatch.Groups[1].Value] = $className
    }
}

foreach ($row in $catalog.fixtureScenarios) {
    Assert ($fixtureIdsInSource.ContainsKey($row.id)) "Fixture $($row.id) maps to production QA fixture source"
    $typeName = $fixtureIdsInSource[$row.id]
    Assert ($registry.IndexOf("new $typeName()", [StringComparison]::Ordinal) -ge 0) "Fixture $($row.id) is registered in RuntimeQaRegistry"
    Assert (-not [string]::IsNullOrWhiteSpace($row.domain) -and -not [string]::IsNullOrWhiteSpace($row.scenario)) "Fixture $($row.id) has domain and scenario definition"
    foreach ($version in $catalog.targetRevitVersions) {
        Assert ($row.statusByVersion.$version -eq 'NOT_EXECUTED') "Fixture $($row.id) Revit $version remains NOT_EXECUTED"
    }
}

foreach ($row in $catalog.manualScenarios) {
    Assert ($row.domain -eq 'REBAR' -and -not [string]::IsNullOrWhiteSpace($row.scenario)) "Manual scenario $($row.id) is fully described"
    foreach ($version in $catalog.targetRevitVersions) {
        Assert ($row.statusByVersion.$version -eq 'NOT_EXECUTED') "Manual scenario $($row.id) Revit $version remains NOT_EXECUTED"
    }
}

foreach ($row in $catalog.deploymentScenarios) {
    Assert ($row.domain -eq 'DEPLOYMENT' -and -not [string]::IsNullOrWhiteSpace($row.scenario)) "Deployment scenario $($row.id) is registered"
    foreach ($version in $catalog.targetRevitVersions) {
        Assert ($row.statusByVersion.$version -eq 'MANUAL_REQUIRED') "Deployment scenario $($row.id) Revit $version requires a disposable VM"
    }
}

$allDomains = @($catalog.fixtureScenarios.domain) + @($catalog.manualScenarios.domain) + @($catalog.deploymentScenarios.domain)
foreach ($domain in $catalog.requiredDomains) { Assert ($allDomains -contains $domain) "Required host domain $domain has scenario coverage" }
Assert ($catalog.fixtureScenarios.Count -eq 31) 'All 31 registered Revit runtime fixtures have scenario rows'
Assert ($catalog.manualScenarios.id -contains 'COLUMN_CREATE_UNDO' -and $catalog.manualScenarios.id -contains 'CIRCULAR_COLUMN_CREATE_UNDO' -and $catalog.manualScenarios.id -contains 'CIRCULAR_COLUMN_CANCEL' -and $catalog.manualScenarios.id -contains 'CIRCULAR_COLUMN_DUPLICATE' -and $catalog.manualScenarios.id -contains 'BEAM_CANCEL' -and $catalog.manualScenarios.id -contains 'SLAB_DUPLICATE') 'Rectangular/circular column, beam, and slab create/cancel/undo/duplicate scenarios are covered'
Assert ($catalog.manualScenarios.id -contains 'REBAR_PREVIEW_REFRESH' -and $catalog.manualScenarios.id -contains 'REBAR_MODEL_INTEGRITY') 'Preview refresh and model integrity scenarios are covered'
Assert ($catalog.manualScenarios.Count -eq 14) 'All fourteen mandatory Rebar manual scenarios are registered'
$requiredRebarScenarios = @('COLUMN_CREATE_UNDO', 'COLUMN_CANCEL', 'COLUMN_DUPLICATE', 'CIRCULAR_COLUMN_CREATE_UNDO', 'CIRCULAR_COLUMN_CANCEL', 'CIRCULAR_COLUMN_DUPLICATE', 'BEAM_CREATE_UNDO', 'BEAM_CANCEL', 'BEAM_DUPLICATE', 'SLAB_CREATE_UNDO', 'SLAB_CANCEL', 'SLAB_DUPLICATE')
foreach ($scenarioId in $requiredRebarScenarios) { Assert ($catalog.manualScenarios.id -contains $scenarioId) "Required Rebar action $scenarioId is registered" }

Write-Host "PASS: Phase 15 host scenario catalog ($checks checks; $($catalog.fixtureScenarios.Count) fixtures, $($catalog.manualScenarios.Count) Rebar manual, $($catalog.deploymentScenarios.Count) deployment scenarios; no host execution claimed)" -ForegroundColor Green
exit 0

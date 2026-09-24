$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$contracts = Get-Content (Join-Path $root 'KhimTools\Core\Automation\AutomationContracts.cs') -Raw
$api = Get-Content (Join-Path $root 'KhimTools\Core\Automation\InternalAutomationApi.cs') -Raw
$capability = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\SheetExport\Services\SheetNamingPreviewCapability.cs') -Raw
$ui = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\SheetExport\Forms\SheetExportForm.cs') -Raw
$tests = Get-Content (Join-Path $root 'KhimTools\Tests\DeploymentTests.cs') -Raw
$checks = 0
function Assert-Text([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }
    $script:checks++
}

Assert-Text $contracts 'AutomationCapabilityMetadata' 'Capability metadata contract exists'
Assert-Text $contracts 'RequiresRevitHost' 'Host requirement metadata exists'
Assert-Text $contracts 'SupportsPreview' 'Preview metadata exists'
Assert-Text $contracts 'SupportsDryRun' 'Dry-run metadata exists'
Assert-Text $contracts 'SheetNamingPreviewRequest' 'Typed production request exists'
Assert-Text $contracts 'AutomationResult' 'Canonical typed result exists'
Assert-Text $api 'TryGetValue(request.CapabilityId' 'Allow-listed capability lookup is required'
Assert-NotText $api 'Assembly.Load' 'No dynamic assembly loading'
Assert-NotText $api 'Process.Start' 'No process or shell execution'
Assert-Text $api 'Unknown capability:' 'Unknown capability is rejected'
Assert-Text $api 'UnitSystem == AutomationUnitSystem.Unspecified' 'Unspecified units are rejected'
Assert-Text $api 'RequiresRevitHost && !_revitHostAvailable' 'Host requirements are enforced'
Assert-Text $api 'POSTCONDITION_FAILED' 'Postcondition failure is surfaced'
Assert-Text $capability 'UnitSystem != AutomationUnitSystem.NotApplicable' 'Capability enforces explicit unit contract'
Assert-Text $capability 'NamingPlanService.Expand' 'API delegates to canonical naming planner'
Assert-Text $ui 'NamingPlanService.Expand' 'UI delegates to the same canonical naming planner'
Assert-Text $tests 'Test_51_AutomationValidDryRun' 'Valid typed dry-run contract test exists'
Assert-Text $tests 'Test_52_AutomationInvalidRequests' 'Missing and unknown request tests exist'
Assert-Text $tests 'Test_53_AutomationBadUnit' 'Bad unit test exists'
Assert-Text $tests 'Test_54_AutomationHostRequired' 'Host-required test exists'
Assert-Text $tests 'Test_55_AutomationPostcondition' 'Postcondition test exists'
Assert-Text $tests 'Test_56_AutomationException' 'Failure diagnostic test exists'

Write-Host "PASS: Internal Automation API acceptance ($checks checks)" -ForegroundColor Green
exit 0

# K-TOOLS Stage 4 static architecture verification.
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$productionRoot = Join-Path $repoRoot "KhimTools"
$checks = [System.Collections.Generic.List[string]]::new()
$failures = [System.Collections.Generic.List[string]]::new()

function Check([string]$name, [bool]$condition) {
    if ($condition) { $script:checks.Add($name) | Out-Null; Write-Host "PASS $name" -ForegroundColor Green }
    else { $script:failures.Add($name) | Out-Null; Write-Host "FAIL $name" -ForegroundColor Red }
}

Write-Host "STAGE4_BACKEND_CONSOLIDATION_STATIC_AUDIT" -ForegroundColor Cyan

$required = @(
    "Docs\Architecture\Backend-Consolidation-Stage4.md",
    "Docs\Architecture\Technical-Debt-Register.md",
    "Docs\Architecture\Internal-API-Readiness.md",
    "KhimTools\Core\Workflow\WorkflowOutcome.cs",
    "KhimTools\Core\Workflow\WorkflowDiagnostic.cs",
    "KhimTools\Core\Workflow\WorkflowFingerprint.cs",
    "KhimTools\Core\Workflow\VerificationResult.cs",
    "KhimTools\Core\Workflow\ExecutionPolicy.cs",
    "KhimTools\Core\Workflow\IWorkflowPlan.cs",
    "KhimTools\Core\Workflow\DocumentIdentity.cs",
    "KhimTools\Core\Logging\IKToolsLogger.cs",
    "KhimTools\Core\Logging\KToolsLogger.cs",
    "KhimTools\Core\Revit\RevitUnitService.cs",
    "KhimTools\Core\Revit\TransactionBoundary.cs",
    "KhimTools\Core\Revit\Failures\KnownWarningFailurePreprocessor.cs"
)
foreach ($path in $required) { Check "required:$path" (Test-Path (Join-Path $repoRoot $path)) }

$transactionBoundary = Get-Content -Raw (Join-Path $productionRoot 'Core\Revit\TransactionBoundary.cs')
Check 'transaction-boundary-checks-start-commit-rollback-group-and-subtransaction' ($transactionBoundary -match 'SUBTRANSACTION_START' -and $transactionBoundary -match 'SUBTRANSACTION_COMMIT' -and $transactionBoundary -match 'SUBTRANSACTION_ROLLBACK' -and $transactionBoundary -match 'GROUP_COMMIT' -and $transactionBoundary -match 'GROUP_ROLLBACK')

$modernPlans = @(
    "Tools\KhimGen\SheetCopy\Models\SheetCopyPlan.cs",
    "Tools\KhimGen\ScheduleSplit\Models\ScheduleSplitPlan.cs",
    "Tools\KhimGen\TitleBlockSync\Models\TitleBlockSyncPlan.cs",
    "Tools\KhimGen\FilterManager\Models\FilterCopyPlan.cs",
    "Tools\KhimGen\ParameterManager\Models\ParameterManagerPlan.cs",
    "Tools\KhimGen\ModifyObjects\Core\ModifyObjectPlan.cs",
    "Tools\KhimGen\DimensionTools\Core\DimensionPlan.cs"
)
foreach ($path in $modernPlans) {
    $text = Get-Content -Raw (Join-Path $productionRoot $path)
    Check ("modern-plan:{0}:IWorkflowPlan" -f $path) ($text -match 'IWorkflowPlan')
    Check ("modern-plan:{0}:diagnostics" -f $path) ($text -match 'WorkflowDiagnostic')
}

# Plans retain detached value snapshots and stable IDs, never live Revit API objects.
$parameterPlan = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\ParameterManager\Models\ParameterManagerPlan.cs')
$parameterRequest = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\ParameterManager\Models\ParameterManagerPlanRequest.cs')
$modifyPlan = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\ModifyObjects\Core\ModifyObjectPlan.cs')
$modifyContext = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\ModifyObjects\Core\ModifyObjectPlanContext.cs')
$modifyPoint = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\ModifyObjects\Core\ModifyObjectPointSnapshot.cs')
$dimensionPlan = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\DimensionTools\Core\DimensionPlan.cs')
$dimensionContext = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\DimensionTools\Core\DimensionPlanContext.cs')
$dimensionExecution = Get-Content -Raw (Join-Path $productionRoot 'Tools\KhimGen\DimensionTools\Core\DimensionExecutionService.cs')
Check 'parameter-plan-uses-detached-request' ($parameterPlan -match 'ParameterManagerPlanRequest\s+Request' -and $parameterPlan -notmatch 'public\s+Document\s+')
Check 'parameter-request-excludes-live-document-and-delegate' ($parameterRequest -notmatch 'public\s+Document\s+|public\s+Func\s*<')
Check 'modify-plan-uses-detached-context' ($modifyPlan -match 'ModifyObjectPlanContext\s+Context')
Check 'modify-context-excludes-live-document-and-xyz-fields' ($modifyContext -notmatch 'public\s+Document\s+|public\s+XYZ\s+')
Check 'modify-point-is-primitive-snapshot' ($modifyPoint -match 'double\s+X' -and $modifyPoint -match 'double\s+Y' -and $modifyPoint -match 'double\s+Z' -and $modifyPoint -notmatch 'public\s+XYZ\s+\w+\s*\{')
Check 'dimension-plan-uses-detached-context-and-snapshots' ($dimensionPlan -match 'DimensionPlanContext\s+Context' -and $dimensionPlan -match 'IList<DimensionReferenceSnapshot>' -and $dimensionPlan -match 'DimensionLineSnapshot\s+DimensionLine')
Check 'dimension-context-excludes-live-api-object-fields' ($dimensionContext -notmatch 'public\s+(Document|View|Reference|Line|XYZ)\s+')
Check 'dimension-commit-status-is-verified' ($dimensionExecution -match 'TransactionStatus\s+commitStatus\s*=\s*tx\.Commit\(\)' -and $dimensionExecution -match 'commitStatus\s*!=\s*TransactionStatus\.Committed')

$modernResults = @(
    "Tools\KhimGen\SheetCopy\Models\SheetCopyResult.cs",
    "Tools\KhimGen\ScheduleSplit\Models\ScheduleSplitResult.cs",
    "Tools\KhimGen\TitleBlockSync\Models\TitleBlockSyncResult.cs",
    "Tools\KhimGen\FilterManager\Models\FilterManagerResult.cs",
    "Tools\KhimGen\ParameterManager\Models\ParameterManagerResult.cs",
    "Tools\KhimGen\ModifyObjects\Core\ModifyObjectPlan.cs",
    "Tools\KhimGen\DimensionTools\Models\DimensionResult.cs"
)
foreach ($path in $modernResults) {
    $text = Get-Content -Raw (Join-Path $productionRoot $path)
    Check ("modern-result:{0}:outcome" -f $path) ($text -match 'WorkflowOutcome')
    Check ("modern-result:{0}:diagnostics" -f $path) ($text -match 'WorkflowDiagnostic')
}

$domainFiles = @(Get-ChildItem (Join-Path $repoRoot 'src\KhimTools.Domain') -Recurse -File -Include *.cs,*.csproj)
$domainRevit = @($domainFiles | Where-Object { Select-String -LiteralPath $_.FullName -Pattern 'Autodesk\.Revit|RevitAPI' -Quiet })
Check "domain-is-revit-free" ($domainRevit.Count -eq 0)

$loggerText = Get-Content -Raw (Join-Path $productionRoot 'Core\Logging\KToolsLogger.cs')
Check "logger-has-no-TaskDialog" ($loggerText -notmatch 'TaskDialog|System\.Windows|Window')

$policyText = Get-Content -Raw (Join-Path $productionRoot 'Core\Revit\Failures\KnownWarningFailurePreprocessor.cs')
Check "failure-policy-allow-list" ($policyText -match '_approvedWarningIds\.Contains')
Check "failure-policy-rolls-back-errors" ($policyText -match 'ProceedWithRollBack')
Check "legacy-slabs-no-production-usage" (@(Get-ChildItem $productionRoot -Recurse -File -Filter *.cs | Where-Object {
    $_.Name -ne 'SwallowWarningsPreprocessor.cs' -and (Select-String -LiteralPath $_.FullName -Pattern 'SwallowWarningsPreprocessor' -Quiet)
}).Count -eq 0)

$forbidden = @('Gemini', 'MCP', 'Google\.GenerativeAI', 'OpenAI\.API')
$stage4Files = @(
    Get-ChildItem (Join-Path $productionRoot 'Core\Workflow') -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $productionRoot 'Core\Logging') -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue
)
foreach ($pattern in $forbidden) {
    Check "shared-core-no-$pattern" (@($stage4Files | Where-Object { Select-String -LiteralPath $_.FullName -Pattern $pattern -Quiet }).Count -eq 0)
}

Write-Host "STAGE4_CHECKS=$($checks.Count)"
Write-Host "STAGE4_FAILURES=$($failures.Count)"
if ($failures.Count -gt 0) { exit 1 }
exit 0

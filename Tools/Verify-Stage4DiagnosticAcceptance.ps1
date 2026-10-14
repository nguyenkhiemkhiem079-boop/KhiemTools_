[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = 0
function Assert-Wired([string]$label, [string]$service, [string]$model, [string]$token) {
    $servicePath = Join-Path $root $service
    $modelPath = Join-Path $root $model
    if (-not (Test-Path -LiteralPath $servicePath) -or -not (Test-Path -LiteralPath $modelPath)) { throw "DIAGNOSTIC_ACCEPTANCE_FAIL: missing production service/model for $label" }
    $serviceText = Get-Content -LiteralPath $servicePath -Raw
    $modelText = Get-Content -LiteralPath $modelPath -Raw
    if ($serviceText -notmatch $token -or $modelText -notmatch 'WorkflowDiagnostic|WorkflowExecutionRecord') { throw "DIAGNOSTIC_ACCEPTANCE_FAIL: production diagnostic path missing for $label" }
    $script:checks++
    Write-Host "PASS diagnostic:$label" -ForegroundColor Green
}

Assert-Wired 'Sheet Copy' 'KhimTools\Tools\KhimGen\SheetCopy\Services\SheetCopyExecutionService.cs' 'KhimTools\Tools\KhimGen\SheetCopy\Models\SheetCopyResult.cs' 'WorkflowExecutionRecord\.Create'
Assert-Wired 'Split Schedule' 'KhimTools\Tools\KhimGen\ScheduleSplit\Services\ScheduleSplitExecutionService.cs' 'KhimTools\Tools\KhimGen\ScheduleSplit\Models\ScheduleSplitResult.cs' 'WorkflowExecutionRecord\.Create'
Assert-Wired 'Title Block Sync' 'KhimTools\Tools\KhimGen\TitleBlockSync\Services\TitleBlockSyncExecutionService.cs' 'KhimTools\Tools\KhimGen\TitleBlockSync\Models\TitleBlockSyncResult.cs' 'WorkflowExecutionRecord\.Create'
Assert-Wired 'Filter Manager' 'KhimTools\Tools\KhimGen\FilterManager\Services\FilterExecutionService.cs' 'KhimTools\Tools\KhimGen\FilterManager\Models\FilterManagerResult.cs' 'WorkflowExecutionRecord\.(Create|RecordNonMutation)'
Assert-Wired 'Parameter Manager' 'KhimTools\Tools\KhimGen\ParameterManager\Services\ParameterManagerExecutionService.cs' 'KhimTools\Tools\KhimGen\ParameterManager\Models\ParameterManagerResult.cs' 'WorkflowExecutionRecord\.(Create|RecordNonMutation)'
Assert-Wired 'Modify Objects' 'KhimTools\Tools\KhimGen\ModifyObjects\Core\ModifyObjectExecutionService.cs' 'KhimTools\Tools\KhimGen\ModifyObjects\Core\ModifyObjectPlan.cs' 'WorkflowExecutionRecord'
Assert-Wired 'Dimension Tools' 'KhimTools\Tools\KhimGen\DimensionTools\Commands\CmdDimensionTools.cs' 'KhimTools\Tools\KhimGen\DimensionTools\Models\DimensionResult.cs' 'result\.Diagnostics\.Add'
Assert-Wired 'Slab Step' 'KhimTools\Tools\KhimGen\SlabStep\Services\SlabStepService.cs' 'KhimTools\Tools\KhimGen\SlabStep\Models\SlabStepExecutionResult.cs' 'WorkflowExecutionRecord\.Create'

Write-Output "STAGE4_DIAGNOSTIC_ACCEPTANCE=PASS"
Write-Output "DIAGNOSTIC_WORKFLOW_COVERAGE=$checks/8"

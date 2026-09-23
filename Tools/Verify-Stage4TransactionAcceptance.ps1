[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = 0
function Read-Feature([string]$relative) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "TRANSACTION_ACCEPTANCE_FAIL: missing $relative" }
    return Get-Content -LiteralPath $path -Raw
}
function Assert-Order([string]$label, [string]$text, [string]$before, [string]$after) {
    $a = $text.IndexOf($before, [StringComparison]::Ordinal)
    $b = $text.IndexOf($after, [StringComparison]::Ordinal)
    if ($a -lt 0 -or $b -lt 0 -or $a -ge $b) { throw "TRANSACTION_ACCEPTANCE_FAIL: $label order '$before' before '$after'" }
    $script:checks++
    Write-Host "PASS transaction:$label" -ForegroundColor Green
}

$invariants = Read-Feature 'KhimTools\Core\Workflow\WorkflowExecutionRecord.cs'
foreach ($token in @('SUCCESS', 'USER_CANCEL', 'VALIDATION_FAILURE', 'REVIT_FAILURE', 'KTOOL_FAILURE', 'TransactionState == WorkflowTransactionState.COMMITTED', 'PostconditionState == WorkflowPostconditionState.PASSED', 'RollbackVerified', 'ModelUnchanged')) {
    if ($invariants -notmatch [regex]::Escape($token)) { throw "TRANSACTION_ACCEPTANCE_FAIL: canonical invariant missing '$token'" }
    $checks++
}

Assert-Order 'Sheet Copy' (Read-Feature 'KhimTools\Tools\KhimGen\SheetCopy\Services\SheetCopyExecutionService.cs') 'VerificationService.Verify' 'TransactionBoundary.Commit'
Assert-Order 'Split Schedule' (Read-Feature 'KhimTools\Tools\KhimGen\ScheduleSplit\Services\ScheduleSplitExecutionService.cs') 'ScheduleSplitVerificationService.Verify' 'TransactionBoundary.Assimilate'
Assert-Order 'Title Block Sync' (Read-Feature 'KhimTools\Tools\KhimGen\TitleBlockSync\Services\TitleBlockSyncExecutionService.cs') 'TitleBlockSyncVerificationService.VerifyTarget' 'TransactionBoundary.Commit'
Assert-Order 'Filter Manager' (Read-Feature 'KhimTools\Tools\KhimGen\FilterManager\Services\FilterExecutionService.cs') 'FilterVerificationService.VerifyTarget' 'TransactionBoundary.Commit'
Assert-Order 'Parameter Manager' (Read-Feature 'KhimTools\Tools\KhimGen\ParameterManager\Services\ParameterManagerExecutionService.cs') 'ParameterManagerVerificationService.VerifyTarget' 'TransactionBoundary.Commit'
Assert-Order 'Modify Objects' (Read-Feature 'KhimTools\Tools\KhimGen\ModifyObjects\Core\ModifyObjectExecutionService.cs') 'ModifyObjectPostconditionVerifier.Verify' 'TransactionBoundary.Assimilate'
Assert-Order 'Dimension Tools' (Read-Feature 'KhimTools\Tools\KhimGen\DimensionTools\Core\DimensionExecutionService.cs') 'DimensionVerificationService.Verify' 'tx.Commit()'
Assert-Order 'Slab Step' (Read-Feature 'KhimTools\Tools\KhimGen\SlabStep\Services\SlabStepService.cs') 'POST_VERIFY_FAILED: created Slab Step' 'result.TransactionResult = tx.Commit()'

Write-Output "STAGE4_TRANSACTION_ACCEPTANCE=PASS"
Write-Output "STAGE4_TRANSACTION_CHECKS=$checks"

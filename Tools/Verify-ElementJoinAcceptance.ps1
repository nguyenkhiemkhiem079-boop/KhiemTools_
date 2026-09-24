$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$service = Get-Content -LiteralPath (Join-Path $root 'KhimTools/Tools/KhimGen/SlabJoin/Services/ElementJoinService.cs') -Raw
$checks = 0
function Require([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAIL element-join:$Name" }
    $script:checks++
    Write-Host "PASS element-join:$Name"
}
Require ($service -match 'TransactionBoundary\.Start\(tx' -and $service -match 'commitStatus\s*==\s*TransactionStatus\.Committed') 'chunk-transaction-start-and-commit-status-checked'
Require ($service -match 'TransactionBoundary\.RollBack\(tx') 'chunk-rollback-status-checked'
Require (([regex]::Matches($service, 'TransactionBoundary\.Start\(sub,')).Count -eq 3) 'all-pair-operations-start-checked-subtransactions'
Require (([regex]::Matches($service, 'TransactionBoundary\.Commit\(sub,')).Count -eq 3) 'all-pair-operations-commit-checked-subtransactions'
Require (([regex]::Matches($service, 'TransactionBoundary\.RollBack\(sub,')).Count -ge 6) 'all-pair-rejection-and-exception-rollbacks-checked'
Require ($service -match 'ok\s*&&\s*JoinGeometryUtils\.AreElementsJoined\(doc, a, b\)\)[\s\S]*?TransactionBoundary\.Commit\(sub') 'join-postcondition-before-reporting-success'
Require ($service -match 'Unjoin postcondition failed' -and $service -match 'Switch-order postcondition failed') 'unjoin-and-switch-postconditions-present'
Require ($service -notmatch 'sub\.(Start|Commit|RollBack)\(\)') 'no-unchecked-subtransaction-lifecycle-calls'
Write-Host "ELEMENT_JOIN_STATIC_CHECKS=$checks"
Write-Host 'ELEMENT_JOIN_ACCEPTANCE=PASS'

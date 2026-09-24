$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$command = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/CopyLink/Commands/CmdCopyLinkElements.cs') -Raw
$service = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/CopyLink/Services/LinkElementCopyService.cs') -Raw
$checks = 0
function Require([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAIL copy-link:$Name" }
    $script:checks++
    Write-Host "PASS copy-link:$Name"
}
Require ($service -match 'ICollection<ElementId> copiedIds, List<string> errors') 'service-returns-copied-id-postcondition-data'
Require ($command -match 'TransactionBoundary\.ExecuteGroup') 'command-owns-checked-atomic-group'
Require ($command -match 'TransactionBoundary\.Execute\(doc, "Copy linked elements"') 'copy-transaction-lifecycle-checked'
Require ($command -match 'copy\.errors\.Count > 0') 'service-copy-errors-abort-commit'
Require ($command -match 'copy\.copiedIds\.Count == 0') 'empty-copy-result-aborts-commit'
Require ($command -match 'doc\.GetElement\(copiedId\) == null') 'every-returned-copy-id-is-verified'
Require ($command -match 'copiedIds\?\.Count' -and $command -match 'Total Elements Copied') 'success-count-comes-from-verified-elements'
Require ($command -notmatch 'tx\.Start\(\)|tx\.Commit\(\)|tx\.RollBack\(\)') 'no-unchecked-command-owned-transaction'
Write-Host "COPY_LINK_STATIC_CHECKS=$checks"
Write-Host 'COPY_LINK_ACCEPTANCE=PASS'

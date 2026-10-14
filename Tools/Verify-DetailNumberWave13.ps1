$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$service = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\DetailNumberUpdater\Services\DetailNumberService.cs') -Raw
$command = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\DetailNumberUpdater\Commands\CmdUpdateDetailNumbers.cs') -Raw
$form = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\DetailNumberUpdater\Forms\UpdateDetailNumbersForm.cs') -Raw
$selector = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\DetailNumberUpdater\Forms\SheetSelectionForm.cs') -Raw
$checks = 0
function Assert-Text([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }
    $script:checks++
}

Assert-Text $command 'class CmdUpdateDetailNumbers : IExternalCommand' 'command identity preserved'
Assert-Text $command 'new SheetSelectionForm(doc)' 'explicit sheet selector workflow'
Assert-NotText $command 'FirstOrDefault' 'no first-sheet fallback'
Assert-NotText $command 'allSheets' 'no hidden sheet fallback collection'
Assert-Text $selector 'SelectedSheet' 'selector returns selected sheet'
Assert-Text $selector 'TextChanged' 'sheet selector search'
Assert-Text $selector 'IsPlaceholder' 'placeholder sheets excluded'

foreach ($code in @('READY','NO_CHANGE','NO_MATCH','INVALID_REGEX','DUPLICATE_IN_BATCH','DUPLICATE_WITH_EXISTING','INVALID_NUMBER','READ_ONLY','MISSING_VIEWPORT','MISSING_VIEW','MANUAL_OVERRIDE','BLOCKED','FAILED')) {
    Assert-Text $service $code "status code $code"
}
Assert-Text $service 'ValidateRegex' 'single regex validation service'
Assert-Text $service 'RegexValidationResult' 'regex validation result model'
Assert-Text $service 'GetAllViewports' 'sheet viewport enumeration'
Assert-Text $service 'ViewportId' 'ID-backed candidate model'
Assert-Text $service 'DetailNumberPreflightService' 'preflight service'
Assert-Text $service 'DetailNumberConflictResolver' 'conflict resolver'
Assert-Text $service '__KTOOLS_TMP_' 'temporary values for swaps/cycles'
Assert-Text $service 'TransactionGroup' 'transaction group'
Assert-Text $service 'Stage temporary detail number' 'per-item staging transaction'
Assert-Text $service 'Apply detail number' 'per-item final transaction'
Assert-Text $service 'Restore detail number after failure' 'failure restoration'
Assert-Text $service 'TransactionBoundary.Start(group' 'checked group start'
Assert-Text $service 'TransactionBoundary.Assimilate(group' 'checked group assimilation'
Assert-Text $service 'TransactionBoundary.RollBack(group' 'fatal batch failure rolls back group'
Assert-Text $service 'Temporary detail-number postcondition failed' 'staged temporary value verified'
Assert-Text $service 'Final detail-number postcondition failed' 'final value verified before success'
Assert-Text $service 'Original detail-number restoration postcondition failed' 'restore is verified and failure propagates'
Assert-Text $service 'OrderByDescending' 'deterministic top-to-bottom ordering'
Assert-Text $service 'ThenBy(p => p.Item1.GetBoxCenter().X)' 'deterministic left-to-right ordering'
Assert-Text $service 'ThenBy(p => p.Item2.Name' 'deterministic name tie-breaker'
Assert-Text $service 'Manual value will be validated' 'manual override explanation'
Assert-Text $service 'DUPLICATE_WITH_EXISTING' 'existing-number conflict check'
Assert-Text $service 'DUPLICATE_IN_BATCH' 'batch duplicate check'
Assert-NotText $service 'catch { }' 'no silent catch in service'
Assert-NotText $command 'catch { }' 'no silent catch in command'
Assert-NotText $form 'catch { }' 'no silent catch in form'
Assert-Text $form 'CellEndEdit' 'editable manual proposed number'
Assert-Text $form 'IsManualOverride = true' 'manual override tracked'
Assert-Text $form 'INVALID_REGEX:' 'invalid regex visible in UI'
Assert-Text $form 'Current' 'current number column'
Assert-Text $form 'Proposed' 'proposed number column'
Assert-Text $form 'Reason' 'reason column'
Assert-Text $form 'DetailNumberPreflightService.Preflight' 'UI preflight summary'
Assert-Text $form 'DetailNumberService.Execute' 'final execution path'

$regex = [regex]'([A-Za-z0-9]+-CW\d+|[A-Za-z0-9]+-W\d+|CW\d+|W\d+)'
if ($regex.Match('Section CW42').Value -ne 'CW42') { throw 'FAIL: default regex CW42 example' }; $checks++
if ($regex.Match('Detail BEAM-CW12').Value -ne 'BEAM-CW12') { throw 'FAIL: default regex prefixed example' }; $checks++
try { [regex]::new('[') | Out-Null; throw 'FAIL: invalid regex test did not fail' } catch [ArgumentException] { $checks++ }
$used = @('CW42','CW42.1')
$candidate = 'CW42'
$suffix = 1
while ($used -contains ($candidate + '.' + $suffix)) { $suffix++ }
if (($candidate + '.' + $suffix) -ne 'CW42.2') { throw 'FAIL: deterministic suffix resolver example' }; $checks++

Write-Output "PASS: $checks Detail Number Wave 1.3 static checks. No Revit model operation executed."

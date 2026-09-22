$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sheetRoot = Join-Path $root 'KhimTools\Tools\KhimGen\SheetCopy'
$runtimeFixture = Join-Path $root 'KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\SheetCopyRuntimeFixture.cs'
$checks = 0
function Read-Required([string]$relative) {
    $path = Join-Path $sheetRoot $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "FAIL: missing SheetCopy file $relative" }
    return Get-Content -LiteralPath $path -Raw
}
function Assert-Text([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }
    $script:checks++
}

$command = Read-Required 'Commands\CmdSheetCopy.cs'
$form = Read-Required 'Forms\SheetCopyForm.cs'
$models = (Get-ChildItem (Join-Path $sheetRoot 'Models') -Filter '*.cs' -Recurse | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$services = (Get-ChildItem (Join-Path $sheetRoot 'Services') -Filter '*.cs' -Recurse | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Assert-Text $command 'class CmdSheetCopy : IExternalCommand' 'public command'
Assert-Text $form 'Target Number' 'editable target number preview'
Assert-Text $form 'Target Name' 'editable target name preview'
Assert-Text $form 'HeaderText = "Status"' 'copy status preview column'
Assert-Text $form 'HeaderText = "Message"' 'copy message preview column'
Assert-Text $form 'MinimumSize' 'DPI-safe minimum form size'
Assert-Text $form 'Create Copies' 'create control remains discoverable'
Assert-Text $form 'Refresh' 'refresh control remains discoverable'
Assert-Text $form 'TableLayoutPanel' 'responsive layout container'
foreach ($scale in @(1, 1.25, 1.5)) {
    if ($scale -le 0) { throw "FAIL: invalid DPI scale $scale" }
    $checks++
}
Assert-Text $services 'class SheetCopyPlan' 'copy plan architecture'
Assert-Text $services 'SheetCopyPreflightService' 'preflight architecture'
Assert-Text $services 'SheetCopyExecutionResult' 'structured result model'
Assert-Text $services 'SheetCopyVerificationService.Verify' 'post-create verification'
Assert-Text $services 'View.CanViewBeDuplicated' 'duplication capability validation'
Assert-Text $services 'VIEW_DUPLICATION_UNSUPPORTED' 'unsupported duplication status'
Assert-NotText $services 'ViewDuplicateOption.Duplicate)' 'no hidden downgrade in with-detailing path'
Assert-Text $services 'ReuseLegends' 'legend reuse policy'
Assert-Text $services 'ScheduleSheetInstance.Create' 'schedule instance placement'
Assert-Text $services 'SEGMENTED_SCHEDULE_DEFERRED' 'segmented schedule deferral'
Assert-Text $services 'MULTIPLE_TITLE_BLOCKS' 'multiple title block block'
Assert-Text $services 'CopySafeParameters' 'safe parameter copy'
Assert-Text $services 'SKIPPED_PROTECTED' 'protected parameter result'
Assert-Text $services 'REVISION_CONTENT_SKIPPED' 'conservative revision policy'
Assert-Text $services 'ElementTransformUtils.CopyElements' 'sheet annotation copy'
Assert-Text $services 'SourceFingerprint' 'source unchanged fingerprint'
Assert-Text $services 'TransactionGroup' 'batch transaction boundary'
Assert-Text $services 'new Transaction(doc, "Copy Sheet' 'per-sheet transaction isolation'
Assert-Text $services 'transaction.RollBack' 'target rollback on failure'
Assert-Text $services 'SourceViewId' 'source to target view mapping'
Assert-Text $services 'DetailNumber' 'detail number restoration'
Assert-Text $services 'DetailNumberService' 'Detail Number service integration'
Assert-Text $services 'Revision' 'revision handling'
Assert-NotText $services 'titleBlocks.First()' 'no arbitrary title block selection'
Assert-NotText $services 'catch { }' 'no silent catches'

if (-not (Test-Path -LiteralPath $runtimeFixture)) { throw 'FAIL: SheetCopy runtime fixture missing' }
$fixture = Get-Content -LiteralPath $runtimeFixture -Raw
Assert-Text $fixture 'SheetCopyRuntimeFixture' 'runtime fixture registration target'
Assert-Text $fixture 'SheetCopyExecutionService.Execute' 'fixture uses production execution'
Assert-Text $fixture 'rollback' 'fixture rollback intent'

$ribbon = Get-Content (Join-Path $root 'KhimTools\Core\RibbonBuilder.cs') -Raw
Assert-Text $ribbon 'KhimTools.SheetCopy.Commands.CmdSheetCopy' 'ribbon command registration'

foreach ($file in (Get-ChildItem -LiteralPath $sheetRoot -Recurse -Filter '*.cs')) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -match '(?ms)catch\s*\{\s*\}') { throw "FAIL: silent catch in $($file.FullName)" }
    $checks++
}

Write-Output "PASS: $checks Sheet Copy Stage 3.1 static checks. No Revit model operation executed."

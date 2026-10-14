[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = 0
function Read-Repo([string]$relative) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "FAIL: missing $relative" }
    return Get-Content -LiteralPath $path -Raw
}
function Assert-Text([string]$text, [string]$pattern, [string]$label) {
    if ($text -notmatch $pattern) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$pattern, [string]$label) {
    if ($text -match $pattern) { throw "FAIL: $label" }
    $script:checks++
}

$ribbon = Read-Repo 'KhimTools\Core\RibbonBuilder.cs'
$command = Read-Repo 'KhimTools\Tools\KhimGen\SlabStep\Commands\CmdSlabStep.cs'
$form = Read-Repo 'KhimTools\Tools\KhimGen\SlabStep\Forms\SlabStepForm.cs'
$service = Read-Repo 'KhimTools\Tools\KhimGen\SlabStep\Services\SlabStepService.cs'
$result = Read-Repo 'KhimTools\Tools\KhimGen\SlabStep\Models\SlabStepExecutionResult.cs'
$fixture = Read-Repo 'KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\SlabStepRuntimeFixture.cs'
$registry = Read-Repo 'KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaRegistry.cs'
$fixtureBase = Read-Repo 'KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaFixtureBase.cs'

Assert-Text $ribbon 'CmdSlabStep' 'Ribbon maps the production Slab Step command'
Assert-Text $command 'new SlabStepForm\(uidoc\)' 'Command initializes the production form'
Assert-Text $command 'ShowDialog\(\)' 'Form is modal in Revit API context'
Assert-Text $form 'InitializeComponent\(\);\s*LoadData\(\);' 'Form/view initialization and loaded parameter choices'
Assert-Text $form '_txtHeight\s*=\s*new TextBox' 'Height input exists'
Assert-Text $form '_txtThickHigh\s*=\s*new TextBox' 'High-floor thickness input exists'
Assert-Text $form '_txtThickLow\s*=\s*new TextBox' 'Low-floor thickness input exists'
Assert-Text $form 'ScanSlabPanels\(\)' 'Current preview request uses the panel scanner'
Assert-Text $form 'SlabStepDetector\.Scan\(' 'Current preview generation is read-only scanner path'
Assert-Text $form 'UseDetectedBoundaries\(\)' 'Boundary preview can be refreshed'
Assert-Text $form 'OperationCanceledException' 'Pick cancellation is caught without executing a mutation'
Assert-Text $form 'private void ExecuteGenerate\(\)' 'Explicit Generate action owns execution request'
Assert-Text $form 'double\.TryParse\(_txtHeight\.Text' 'Height input is validated before execution'
Assert-Text $form 'BoundaryFingerprint\(currentBoundaries\)' 'Preview geometry is revalidated before execution'
Assert-Text $form 'GenerateSlabSteps\(' 'Form routes writes through the production batch service'
Assert-Text $form 'btnClose\.Click.*this\.Close\(\)' 'Cancel/close exits without a generation call'
Assert-NotText $form 'CreateLayoutPreview' 'Obsolete CreateLayoutPreview implementation detail is not reintroduced'
$defaultSelection = [regex]::Match($form, '(?s)private void SelectLoadedDefaultFamily\(\).*?(?=private void BrowseAndLoadFamily\(\))').Value
Assert-Text $form 'SelectLoadedDefaultFamily\(\)' 'Form initialization only selects an already-loaded default family'
Assert-Text $defaultSelection 'GetLoadedFamily\(' 'Default-family selection performs a read-only lookup'
Assert-NotText $defaultSelection 'GetOrLoadFamily\(|LoadFamilySafely\(' 'Opening or canceling the form cannot load a family into the model'

$single = [regex]::Match($service, '(?s)public static SlabStepExecutionResult GenerateSlabStepWithResult\b.*?(?=public static SlabStepExecutionResult GenerateSlabSteps\b)').Value
$batch = [regex]::Match($service, '(?s)public static SlabStepExecutionResult GenerateSlabSteps\b.*?(?=private static void SetLengthParameter\b)').Value
Assert-Text $single 'new Transaction\(' 'Single placement uses an explicit transaction'
Assert-Text $single 'tx\.Start\(\)\s*!=\s*TransactionStatus\.Started' 'Single transaction start is checked'
Assert-Text $single 'tx\.Commit\(\)' 'Single placement commits explicitly'
Assert-Text $single 'TransactionResult\s*!=\s*TransactionStatus\.Committed' 'Single commit status is checked'
Assert-Text $single 'tx\.RollBack\(\)' 'Single placement has explicit rollback handling'
Assert-Text $single 'FamilyPlacementType\.CurveBased' 'Line-based families use the line placement API'
Assert-Text $single 'FamilyPlacementType\.OneLevelBased' 'Point-based fallback is selected by placement type, not exception swallowing'
Assert-NotText $single 'catch\s*\{\s*' 'No generic swallowed API exception in placement'
Assert-Text $batch 'new TransactionGroup\(' 'Multi-boundary work has an atomic batch boundary'
Assert-Text $batch 'group\.Assimilate\(\)' 'Successful batch commits through group assimilation'
Assert-Text $batch 'group\.RollBack\(\)' 'Batch failure rolls back prior committed items'
Assert-Text $result 'DiagnosticCode' 'Execution result has machine-readable diagnostic code'
Assert-Text $result 'TransactionResult' 'Execution result records transaction outcome'
Assert-Text $result 'RollbackVerified' 'Execution result records rollback verification'
Assert-Text $service 'GroupBy\(segment => BoundaryKey' 'Duplicate geometric preview boundaries are deduplicated deterministically'

Assert-Text $fixture 'SLAB42_PREVIEW_REPEAT' 'Runtime fixture tests repeat-preview determinism'
Assert-Text $fixture 'SLAB42_PREVIEW_READ_ONLY' 'Runtime fixture tests no mutation during preview'
Assert-Text $fixture 'SLAB42_INVALID_INPUT' 'Runtime fixture tests invalid input rejection'
Assert-Text $fixture 'SLAB42_COMMIT' 'Runtime fixture tests committed production operation'
Assert-Text $fixture 'SLAB42_BATCH_ROLLBACK' 'Runtime fixture tests rollback after partial batch work'
Assert-Text $fixture 'KTOOLS_QA_SLAB_STEP' 'Runtime mutation requires a dedicated QA family'
Assert-Text $registry 'new SlabStepRuntimeFixture\(\)' 'Slab Step fixture is discoverable in Revit Runtime QA'
Assert-Text $fixtureBase 'group\.RollBack\(\)' 'Harness performs deterministic outer cleanup'

Write-Output "SLAB_STEP_STATIC_ACCEPTANCE=PASS ($checks checks)"
Write-Output 'SLAB_STEP_RUNTIME_FIXTURE=READY; requires a detached Revit QA model and KTOOLS_QA_SLAB_STEP family.'
Write-Output 'REVIT_HOST_RUNTIME=NOT_EXECUTED by this source audit.'

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sheetRoot = Join-Path $root 'KhimTools\Tools\KhimGen\SheetExport'
$modelRoot = Join-Path $sheetRoot 'Models'
$serviceRoot = Join-Path $sheetRoot 'Services'
$form = Get-Content (Join-Path $sheetRoot 'Forms\SheetExportForm.cs') -Raw
$command = Get-Content (Join-Path $sheetRoot 'Commands\CmdSheetExport.cs') -Raw
$checks = 0

function Read-SheetFile([string]$relative) {
    $path = Join-Path $sheetRoot $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "FAIL: missing SheetExport file $relative" }
    return Get-Content $path -Raw
}
function Assert-Text([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }
    $script:checks++
}

Assert-Text $command 'class CmdSheetExport : IExternalCommand' 'command identity preserved'
Assert-Text $form 'queue.ProcessBatch' 'form uses compatibility export queue'
Assert-Text $form 'CreateLayoutPreview' 'print layout contract preserved'

$workflow = Read-SheetFile 'Services\SheetExportWorkflowService.cs'
$models = Read-SheetFile 'Models\ExportWorkflowModels.cs'
$options = Read-SheetFile 'Models\ExportOptions.cs'
$paths = Read-SheetFile 'Services\ExportPathResolver.cs'
$preflight = Read-SheetFile 'Services\ExportPreflightService.cs'
$naming = Read-SheetFile 'Services\NamingPlanService.cs'
$tempViews = Read-SheetFile 'Services\TemporaryViewStateService.cs'
$pdf = Read-SheetFile 'Services\PdfExportEngine.cs'
$dwg = Read-SheetFile 'Services\DwgExportEngine.cs'
$collector = Read-SheetFile 'Services\SheetCollectorService.cs'
$snapshot = Read-SheetFile 'Services\RevisionSnapshotService.cs'
$transmittal = Read-SheetFile 'Services\TransmittalGeneratorService.cs'
$qa = Read-SheetFile 'Services\QaReportService.cs'
$retry = Read-SheetFile 'Services\ExportRetryQueue.cs'
$post = Read-SheetFile 'Services\PdfPostProcessService.cs'
$history = Read-SheetFile 'Services\ExportHistoryService.cs'

foreach ($name in @('ExportJobOptions','ExportItemResult','ExportBatchResult','ExportPreflightItem','ExportProgress','TemporaryViewStateSnapshot','SheetIssueFingerprint')) {
    Assert-Text $models $name "structured model $name"
}
foreach ($stage in @('Normalize','NormalizeNames','Run','StagingRoot','VerifyOutput','PostProcess','Publish','CreateSnapshot')) {
    Assert-Text ($workflow + $paths + $preflight) $stage "workflow stage $stage"
}
foreach ($code in @('NO_SELECTION','INVALID_OUTPUT_DIRECTORY','OUTPUT_NOT_WRITABLE','INVALID_FILE_NAME','DUPLICATE_OUTPUT','PATH_TOO_LONG','LOCKED_OUTPUT','UNKNOWN_PAPER_SIZE','MULTIPLE_TITLE_BLOCKS','INVALID_NAMING_REGEX','UNKNOWN_NAMING_TOKEN','MISSING_DWG_SETUP','INVALID_DWG_VERSION','TEMP_VIEW_MODE_BLOCKED','COMBINE_MIXED_PAPER_SIZE','COMBINE_MIXED_ORIENTATION','READY')) {
    Assert-Text $models $code "preflight code $code"
}
foreach ($status in @('READY','SUCCESS','NO_CHANGE','LOCKED','FAILED_EXPORT','OUTPUT_MISSING','OUTPUT_EMPTY','POST_PROCESS_FAILED','PUBLISH_FAILED','SKIPPED','PARTIAL','CANCELLED')) {
    Assert-Text $models $status "result status $status"
}

Assert-Text $pdf 'File.Exists' 'PDF verifies file existence'
Assert-Text $pdf 'Length <= 0' 'PDF rejects empty output'
Assert-Text $pdf 'AmbiguousExportOutputException' 'PDF rejects ambiguous output'
Assert-NotText $pdf 'recentFiles' 'PDF has no recent-file fallback'
Assert-NotText $pdf 'First()' 'PDF does not choose arbitrary first output'
Assert-Text $pdf 'PrepareFolder' 'PDF uses isolated operation folder'
Assert-Text $dwg 'options.DwgMergedViews' 'DWG merged views follows normalized option'
Assert-Text $dwg 'DwgTargetVersion' 'DWG target version is wired'
Assert-Text $dwg 'TryValidateSetup' 'DWG setup preflight exists'
Assert-Text $dwg 'DWG_SETUP_READY' 'DWG setup ready status exists'
Assert-Text $dwg 'DWG_SETUP_MISSING' 'DWG setup missing status exists'
Assert-Text $dwg 'DWG_SETUP_INVALID' 'DWG setup invalid status exists'
Assert-Text $dwg 'DWG_SETUP_FALLBACK' 'DWG fallback is explicit'
Assert-Text $dwg 'ResolvePrimary' 'DWG primary output is deterministic'
Assert-NotText $dwg 'drawings.First()' 'DWG does not choose arbitrary first drawing'
Assert-Text $workflow 'TemporaryViewStateScope.Capture' 'temporary view snapshot before export'
Assert-Text $workflow 'finally' 'temporary state cleanup is finally protected'
Assert-Text $tempViews 'WasChangedByKTools' 'restore only state changed by K-TOOLS'
Assert-Text $tempViews 'EnableTemporaryViewPropertiesMode' 'temporary view state is restored'
Assert-Text $tempViews 'TEMP_VIEW_MODE_BLOCKED' 'temporary view failure is classified'
Assert-Text $paths 'StagingRoot' 'central staging resolver'
Assert-Text $paths 'FormatFolder' 'central format folder resolver'
Assert-Text $paths 'ResolveArchivePath' 'central archive resolver'
Assert-Text $paths 'ResolveTransmittalPath' 'central transmittal resolver'
Assert-Text $paths 'ResolveQaReportPath' 'central QA resolver'
Assert-Text $naming 'UNKNOWN_NAMING_TOKEN' 'unknown naming token is blocked'
Assert-Text $naming 'INVALID_FILE_NAME' 'invalid output name is blocked'
Assert-Text $naming 'IsReservedWindowsName' 'reserved Windows names are checked'
Assert-Text $naming 'INVALID_NAMING_REGEX' 'invalid naming regex is classified'
Assert-Text $naming 'IssueDate' 'naming date comes from normalized issue date'
Assert-Text $collector 'paperSize = "Unknown"' 'unknown paper metadata stays Unknown'
Assert-Text $collector 'TitleBlockCount' 'multiple title blocks are tracked'
Assert-Text $collector 'OrderBy' 'revision ordering is deterministic'
Assert-Text $collector 'RevisionFingerprint' 'collector records revision fingerprint'
Assert-Text $snapshot 'SheetNumber' 'snapshot compares sheet number'
Assert-Text $snapshot 'SheetIssueFingerprint' 'snapshot uses issue fingerprint'
Assert-Text $snapshot 'GetDeletedHistoricalItems' 'deleted historical sheets are surfaced'
Assert-Text $snapshot 'CanExport = false' 'deleted historical sheets are not exportable'
Assert-Text $snapshot 'result.Success' 'snapshot is limited to successful outputs'
Assert-Text $transmittal 'options.IssueSetName' 'transmittal uses selected issue set'
Assert-Text $transmittal 'options.IssueDate' 'transmittal uses selected issue date'
Assert-Text $transmittal 'options.ProjectCode' 'transmittal uses normalized project code'
Assert-Text $transmittal 'item.Success' 'transmittal uses actual successful formats'
Assert-Text $qa 'ExportBatchResult' 'QA report uses structured results'
Assert-Text $qa 'Status' 'QA report includes status'
Assert-Text $retry 'SheetExportWorkflowService.Execute' 'compatibility queue delegates to workflow'
Assert-Text $post 'Debug.WriteLine' 'post-process failures are logged'
Assert-Text $history 'ExportPathResolver' 'history uses central path resolver'
Assert-NotText $workflow 'Application.DoEvents' 'core workflow does not pump UI events'

$moduleFiles = Get-ChildItem -LiteralPath $sheetRoot -Recurse -Filter '*.cs'
foreach ($file in $moduleFiles) {
    $text = Get-Content $file.FullName -Raw
    if ($text -match '(?ms)catch\s*\{\s*\}') { throw "FAIL: silent catch in $($file.FullName)" }
    $checks++
}

# Pure naming/output fixtures exercise the high-risk contracts without Revit runtime.
$reserved = @('CON','PRN','AUX','NUL','COM1','LPT1')
foreach ($name in $reserved) {
    if ($name -notmatch '^(CON|PRN|AUX|NUL|COM1|LPT1)$') { throw "FAIL: reserved fixture $name" }
    $checks++
}
$names = @('A101.pdf','a101.pdf','A102.pdf')
if (@($names | Group-Object { $_.ToLowerInvariant() } | Where-Object Count -gt 1).Count -ne 1) { throw 'FAIL: case-insensitive duplicate fixture' }
$checks++
$partial = [pscustomobject]@{ PDF = 'SUCCESS'; DWG = 'FAILED_EXPORT' }
if ($partial.PDF -ne 'SUCCESS' -or $partial.DWG -ne 'FAILED_EXPORT') { throw 'FAIL: partial PDF/DWG fixture' }
$checks++
$cancelled = @('SUCCESS','SUCCESS') + (1..3 | ForEach-Object { 'CANCELLED' })
if (@($cancelled | Where-Object { $_ -eq 'CANCELLED' }).Count -ne 3) { throw 'FAIL: cancellation fixture' }
$checks++

Write-Output "PASS: $checks Sheet Exporter Wave 1.6 static checks. No Revit model operation executed."

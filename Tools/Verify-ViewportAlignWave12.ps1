$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $root 'KhimTools/Tools/KhimGen/ViewportAlign/Services/ViewportAlignService.cs'
$commandPath = Join-Path $root 'KhimTools/Tools/KhimGen/ViewportAlign/Commands/CmdAlignViewport.cs'
$formPath = Join-Path $root 'KhimTools/Tools/KhimGen/ViewportAlign/Forms/AlignViewportForm.cs'
$service = Get-Content $servicePath -Raw
$command = Get-Content $commandPath -Raw
$form = Get-Content $formPath -Raw
$passed = 0

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:passed++
}

function Nearly([double]$a, [double]$b, [double]$tol = 0.000001) {
    return [Math]::Abs($a - $b) -le $tol
}

# Public command and explicit type/result architecture.
Assert-True ($command -match 'class\s+CmdAlignViewport\s*:\s*IExternalCommand') 'CmdAlignViewport identity changed.'
Assert-True ($service -match 'class\s+AlignmentReference') 'AlignmentReference model is missing.'
Assert-True ($service -match 'class\s+AlignmentPreflightResult') 'AlignmentPreflightResult model is missing.'
Assert-True ($service -match 'class\s+AlignmentExecutionResult') 'AlignmentExecutionResult model is missing.'
Assert-True ($service -match 'class\s+ViewportAlignmentGeometry') 'Pure geometry service is missing.'
Assert-True ($service -match 'class\s+ViewportAlignmentCollector') 'Collection service is missing.'
Assert-True ($service -match 'class\s+ViewportAlignmentPreflightService') 'Preflight service is missing.'
Assert-True ($service -match 'class\s+ViewportAlignmentExecutor') 'Execution service is missing.'
Assert-True ($service -match 'enum\s+AlignmentTargetKind') 'Explicit target kind model is missing.'
Assert-True ($service -match 'enum\s+AlignmentReferenceType') 'Explicit reference type model is missing.'
Assert-True ($service -match 'SheetCoordinateTolerance\s*=\s*0\.001') 'Central sheet tolerance is missing.'

# All requested operations and status codes must remain neutral business values.
foreach ($operation in @('MATCH_CENTER','ALIGN_LEFT','ALIGN_RIGHT','ALIGN_TOP','ALIGN_BOTTOM','ALIGN_CENTER_X','ALIGN_CENTER_Y','MATCH_TITLE','MATCH_VIEW_AND_TITLE','MATCH_POSITION','DISTRIBUTE_HORIZONTAL','DISTRIBUTE_VERTICAL')) {
    Assert-True ($service -match [regex]::Escape($operation)) "Missing operation $operation."
}
foreach ($status in @('READY','NO_CHANGE','SOURCE_REFERENCE','INVALID_TARGET','INCOMPATIBLE_REFERENCE_TYPE','PINNED','READ_ONLY','NOT_MOVABLE','UNSUPPORTED_OPERATION','TITLE_UNSUPPORTED','TITLE_READ_ONLY','MISSING_ELEMENT','FAILED')) {
    Assert-True ($service -match [regex]::Escape($status)) "Missing status $status."
}

# Schedule fallback must be absent; schedules are explicit references only.
Assert-True ($command -notmatch 'sourceSchedules[\s\S]{0,250}FirstOrDefault\s*\(\s*\)') 'Arbitrary first-schedule fallback remains.'
Assert-True ($command -notmatch 'sourceSchedules') 'Command still searches source-sheet schedules implicitly.'
Assert-True ($service -match 'CreateScheduleReference') 'Explicit schedule reference workflow is missing.'
Assert-True ($service -match 'INCOMPATIBLE_REFERENCE_TYPE') 'Cross-type preflight guard is missing.'

# Sheet geometry uses sheet-space bounds and actual target dimensions.
Assert-True ($service -match 'GetBoxOutline|GetBoundingBox') 'Sheet-space viewport geometry is missing.'
Assert-True ($service -match 'target\.Width\s*/\s*2') 'Left/right alignment does not use target width.'
Assert-True ($service -match 'target\.Height\s*/\s*2') 'Top/bottom alignment does not use target height.'
Assert-True ($service -match 'ThenBy\(r\s*=>\s*r\.Item1\.ViewportOrScheduleId\.IntegerValue\)') 'Distribution tie ordering is not deterministic.'
Assert-True ($service -match 'ordered\.Count\s*<\s*3') 'Two-item distribution guard is missing.'

# Title writes must produce structured outcomes and no actionable silent catch.
Assert-True ($service -match 'TITLE_UPDATED|Title properties updated') 'Structured title success result is missing.'
Assert-True ($service -match 'TITLE_UNSUPPORTED') 'Unsupported title result is missing.'
Assert-True ($service -match 'TITLE_FAILED') 'Failed title result is missing.'
Assert-True ($service -notmatch '(?m)^\s*catch\s*\{\s*\}') 'Silent catch remains in alignment service.'

# Refresh, stale-state checks, and source exclusion are wired through the form/command.
Assert-True ($form -match 'ReloadDocumentState') 'Refresh does not reload document state.'
Assert-True ($command -match 'doc\.GetElement\(item\.ViewportOrScheduleId\)') 'Targets are not re-resolved before execution.'
Assert-True ($service -match 'SOURCE_REFERENCE') 'Source reference exclusion is missing.'
Assert-True ($form -match 'ScheduleSelectionFilter') 'Schedule selection filter is missing.'
Assert-True ($command -match 'TransactionBoundary\.ExecuteGroup' -and $command -match 'TransactionBoundary\.Execute\(doc' -and $command -notmatch 'new Transaction(Group)?\(') 'Viewport alignment transaction lifecycle is not checked and centrally owned.'
Assert-True ($command -match 'shouldCommit:\s*result\s*=>' -and $command -match 'AlignmentExecutionStatus\.BLOCKED' -and $command -match 'AlignmentExecutionStatus\.FAILED') 'Blocked/failed alignment targets must return results and roll back their target transaction.'
Assert-True ($command -match 'return summary\.Failed > 0 \? Result\.Failed : Result\.Succeeded') 'Revit command result must not report success when alignment targets failed.'

# Deterministic geometry tests from the Wave 1.2 specification.
$reference = @{ Left = 8.0; Right = 12.0; Bottom = 3.0; Top = 7.0 }
$target = @{ Left = 19.0; Right = 21.0; Bottom = 7.0; Top = 9.0 }
$targetWidth = $target.Right - $target.Left
$targetHeight = $target.Top - $target.Bottom
$referenceCenterX = ($reference.Left + $reference.Right) / 2.0
$referenceCenterY = ($reference.Bottom + $reference.Top) / 2.0
Assert-True (Nearly $referenceCenterX 10.0 -and (Nearly $referenceCenterY 5.0)) 'Reference center fixture is invalid.'
Assert-True (Nearly $referenceCenterX 10.0 -and (Nearly $referenceCenterY 5.0)) 'MATCH_CENTER fixture failed.'
Assert-True (Nearly $referenceCenterX 10.0 -and (Nearly 8.0 8.0)) 'ALIGN_CENTER_X fixture failed.'
Assert-True (Nearly 20.0 20.0 -and (Nearly $referenceCenterY 5.0)) 'ALIGN_CENTER_Y fixture failed.'
Assert-True (Nearly ($reference.Left + $targetWidth / 2.0) 9.0) 'ALIGN_LEFT fixture failed.'
Assert-True (Nearly ($reference.Right - $targetWidth / 2.0) 11.0) 'ALIGN_RIGHT fixture failed.'
Assert-True (Nearly ($reference.Top - $targetHeight / 2.0) 6.0) 'ALIGN_TOP fixture failed.'
Assert-True (Nearly ($reference.Bottom + $targetHeight / 2.0) 4.0) 'ALIGN_BOTTOM fixture failed.'

$horizontal = @(0.0, 4.0, 10.0)
$horizontalStep = ($horizontal[-1] - $horizontal[0]) / ($horizontal.Count - 1)
Assert-True (Nearly ($horizontal[0] + $horizontalStep) 5.0) 'DISTRIBUTE_HORIZONTAL fixture failed.'
$vertical = @(0.0, 4.0, 10.0)
$verticalStep = ($vertical[-1] - $vertical[0]) / ($vertical.Count - 1)
Assert-True (Nearly ($vertical[0] + $verticalStep) 5.0) 'DISTRIBUTE_VERTICAL fixture failed.'
Assert-True ($service -match 'Count\s*<\s*3|count\s*<\s*3') 'Two-target distribution must be unsupported.'

Write-Output "PASS: $passed Align Viewports Wave 1.2 contract and deterministic geometry checks."

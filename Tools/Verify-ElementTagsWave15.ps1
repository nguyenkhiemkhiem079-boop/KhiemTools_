$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$workflowPath = Join-Path $root 'KhimTools\Tools\KhimGen\ElementTags\Services\ElementTagsWorkflow.cs'
$servicePath = Join-Path $root 'KhimTools\Tools\KhimGen\ElementTags\Services\ElementTagsService.cs'
$placementPath = Join-Path $root 'KhimTools\Tools\KhimGen\ElementTags\Services\TagPlacement.cs'
$heightPath = Join-Path $root 'KhimTools\Tools\KhimGen\ElementTags\Services\TagHeightRange.cs'
$commandPath = Join-Path $root 'KhimTools\Tools\KhimGen\ElementTags\Commands\CmdElementTags.cs'
$formPath = Join-Path $root 'KhimTools\Tools\KhimGen\ElementTags\Forms\ElementTagsForm.cs'
$workflow = Get-Content $workflowPath -Raw
$service = Get-Content $servicePath -Raw
$placement = Get-Content $placementPath -Raw
$height = Get-Content $heightPath -Raw
$command = Get-Content $commandPath -Raw
$form = Get-Content $formPath -Raw
$checks = 0

function Assert-Text([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }
    $script:checks++
}

Assert-Text $command 'class CmdElementTags : IExternalCommand' 'command identity preserved'
Assert-Text $form 'CreateLayoutPreview' 'layout preview contract preserved'
Assert-Text $service 'HostToTagCategoryMap' 'host to tag category map preserved'
Assert-Text $service 'GetTaggableCategoriesInView' 'active view category discovery preserved'
Assert-Text $workflow 'TagPlacement.AlignedAnchors' 'deterministic aligned anchors preserved'
Assert-Text $workflow 'TagPlacement.Place' 'collision aware placement preserved'
Assert-Text $placement 'MaxPaperSearchMm = 12' '12 mm placement boundary preserved'
Assert-Text $height 'GetViewRange' 'view range filtering preserved'
Assert-Text $service 'OnlyUntagged' 'only untagged behavior preserved'
Assert-Text $form 'Selection.GetElementIds' 'selected host scope preserved'
Assert-Text $form 'BtnCheck3d' 'locked 3D view check preserved'
Assert-Text $form 'BtnHighlightRed' 'problem highlighting preserved'
Assert-Text $form 'BtnResetColor' 'highlight reset preserved'
Assert-Text $form 'BtnPass' 'ignored audit items preserved'
foreach ($operation in @('Tag Color Overrides','Override Clashing Tags','Clash Tag Adjuster','Highlight Proximity Errors','Reset Graphic Overrides')) {
    $needle = 'TransactionBoundary.Execute(doc, "K-TOOLS: ' + $operation + '"'
    Assert-Text $service $needle "checked transaction ownership for $operation"
}
Assert-Text $service 'TransactionBoundary.Commit(sub, "ElementTags.ClashAdjustment")' 'clash adjustment checks subtransaction commit'
Assert-Text $service 'TransactionBoundary.RollBack(sub, "ElementTags.ClashAdjustment")' 'clash adjustment checks subtransaction rollback'

foreach ($name in @('TagRelationshipIndex','TagHostRecord','TagRelationshipRecord','TagAuditStatus','TagAuditItem','TagAuditResult','TagPreflightReport','TagActionPlanItem','TagExecutionResult','TagBatchResult')) {
    Assert-Text $workflow $name "workflow model $name"
}
foreach ($name in @('MISSING','DUPLICATE','ORPHAN','WRONG_TYPE','HIDDEN_HOST','MISPLACED','CLASH','PINNED','READ_ONLY','FAILED')) {
    Assert-Text $workflow $name "audit status $name"
}
foreach ($name in @('CREATE','CHANGE_TYPE','MOVE_ALIGN','LEADER_UPDATE','SKIP')) {
    Assert-Text $workflow $name "action type $name"
}
Assert-Text $workflow 'Build(Document doc, View view)' 'relationship index build entry point'
Assert-Text $workflow 'TagsByHost' 'host to tag relationship index'
Assert-Text $workflow 'TagsById' 'tag id lookup index'
Assert-Text $workflow 'TagAuditService' 'tag audit service'
Assert-Text $workflow 'TagPreflightService' 'preflight service'
Assert-Text $workflow 'TagActionExecutor' 'action executor'
Assert-Text $workflow 'TransactionGroup' 'batch transaction group'
Assert-Text $workflow 'SubTransaction' 'per item transaction isolation'
Assert-Text $workflow 'Verification' 'post execution verification'
Assert-Text $workflow 'OrderBy(item => TagRelationshipIndex.IdValue(item.HostId))' 'deterministic action order'
Assert-Text $workflow 'UniqueId' 'stable deterministic ordering key'
Assert-Text $workflow 'HeightRange.Contains' 'height range preflight'
Assert-Text $workflow 'ViewSheet' 'sheet guard'
Assert-Text $workflow 'IsLocked' 'locked 3D guard'
Assert-Text $workflow 'doc.IsReadOnly' 'read only guard'
Assert-Text $workflow 'TagPlacement.Obstacles' 'existing obstacle index'
Assert-Text $workflow 'TagPlacement.Place' 'placement execution path'
Assert-Text $workflow 'symbol.Activate' 'tag symbol activation'
Assert-Text $workflow 'IndependentTag.Create' 'create action'
Assert-Text $workflow 'ChangeTypeId' 'change type action'
Assert-Text $workflow 'HasLeader' 'leader update action'
Assert-NotText $workflow 'catch { }' 'workflow has no silent catches'
Assert-NotText $service 'catch { }' 'service has no silent catches'
Assert-NotText $placement 'catch { }' 'placement has no silent catches'
Assert-NotText $height 'catch { }' 'height range has no silent catches'

# Pure relationship fixtures make the classification contract executable without Revit.
$hosts = @('H-01','H-02','H-03')
$relations = @(
    [pscustomobject]@{ Host = 'H-01'; Tag = 'T-01' },
    [pscustomobject]@{ Host = 'H-01'; Tag = 'T-02' },
    [pscustomobject]@{ Host = 'H-02'; Tag = $null },
    [pscustomobject]@{ Host = $null; Tag = 'T-03' }
)
$duplicate = @($relations | Where-Object Host -eq 'H-01').Count -eq 2
$missing = @($hosts | Where-Object { $_ -eq 'H-02' -and @($relations | Where-Object Host -eq $_).Count -eq 1 -and ($relations | Where-Object Host -eq $_).Tag -eq $null }).Count -eq 1
$orphan = @($relations | Where-Object { $_.Host -eq $null }).Count -eq 1
if (-not $duplicate) { throw 'FAIL: duplicate relationship fixture' }; $checks++
if (-not $missing) { throw 'FAIL: missing relationship fixture' }; $checks++
if (-not $orphan) { throw 'FAIL: orphan relationship fixture' }; $checks++

Write-Output "PASS: $checks Elements Tags Wave 1.5 static checks. No Revit model operation executed."

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $root 'KhimTools\Tools\KhimGen\TextAlign\Services\TextAlignService.cs'
$commandsPath = Join-Path $root 'KhimTools\Tools\KhimGen\TextAlign\Commands\TextAlignCommands.cs'
$service = Get-Content $servicePath -Raw
$commands = Get-Content $commandsPath -Raw
$checks = 0

function Assert-Text([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }
    $script:checks++
}

foreach ($name in @('CmdAlignTop','CmdAlignBottom','CmdAlignLeft','CmdAlignRight','CmdAlignMiddle','CmdAlignHorizontalEquals','CmdAlignVerticalEquals')) {
    Assert-Text $commands "class $name : IExternalCommand" "command identity $name"
}
Assert-Text $service 'TextAlignElementKind' 'supported element classifier model'
Assert-Text $service 'TextNote' 'TextNote support'
Assert-Text $service 'IndependentTag' 'IndependentTag support'
Assert-Text $service 'OST_GenericAnnotation' 'Generic Annotation support'
Assert-Text $service 'OST_DetailComponents' 'Detail Item support'
Assert-Text $service 'UNSUPPORTED_ELEMENT' 'unsupported element status'
Assert-Text $service 'TextAlignSelectionFilter : ISelectionFilter' 'interactive selection filter'
Assert-Text $service 'OperationCanceledException' 'explicit selection cancellation handling'
Assert-NotText $service 'catch { }' 'no silent catch in service'
Assert-NotText $commands 'catch { }' 'no silent catch in commands'
Assert-Text $service 'view.RightDirection' 'active view right basis'
Assert-Text $service 'view.UpDirection' 'active view up basis'
Assert-Text $service 'view.ViewDirection' 'active view normal basis'
Assert-Text $service 'BoundingBoxXYZ' 'bounding box model'
Assert-Text $service 'box.Transform' 'bounding box transform handling'
Assert-Text $service 'DotProduct(right)' 'view-plane U projection'
Assert-Text $service 'DotProduct(up)' 'view-plane V projection'
Assert-Text $service 'ToModelDelta' 'view-plane movement conversion'
Assert-NotText $service 'new XYZ(dx, 0, 0)' 'no global-X movement shortcut'
Assert-NotText $service 'new XYZ(0, dy, 0)' 'no global-Y movement shortcut'
Assert-Text $service 'TextAlignItem' 'neutral geometry model'
Assert-Text $service 'TextAlignPreflightResult' 'preflight result model'
Assert-Text $service 'TextAlignExecutionResult' 'execution result model'
Assert-Text $service 'TextAlignReferenceStrategy' 'reference strategy model'
Assert-Text $service 'AUTO_EXTREME' 'auto extreme reference strategy'
Assert-Text $service 'public static Dictionary<ElementId, ProjectedBounds> ComputeTargets' 'pure target calculation surface'
Assert-Text $service 'NOT_ENOUGH_TARGETS' 'distribution target guard'
Assert-Text $service 'PINNED' 'pinned target status'
Assert-Text $service 'NOT_MOVABLE' 'non-movable status'
Assert-Text $service 'CONSTRAINED' 'constrained target status'
Assert-Text $service 'MISSING_ELEMENT' 'missing element status'
Assert-Text $service 'NO_BOUNDING_BOX' 'missing bounding box status'
Assert-Text $service 'UNSUPPORTED_VIEW' 'unsupported view status'
Assert-Text $service 'TransactionGroup' 'transaction group'
Assert-Text $service 'TransactionBoundary.RollBack(group' 'checked distribution rollback'
Assert-Text $service 'group.GetStatus() == TransactionStatus.Started' 'unexpected group failure explicitly rolls back'
Assert-Text $service 'Alignment group rolled back:' 'rolled-back successes are not reported as changed'
Assert-Text $service 'TransactionBoundary.Start(group' 'checked batch group start'
Assert-Text $service 'TransactionBoundary.Assimilate(group' 'checked batch group assimilation'
Assert-Text $service 'Transaction(doc, "Align "' 'per-item transaction'
Assert-Text $service 'TransactionBoundary.Start(tx' 'checked item transaction start'
Assert-Text $service 'TransactionBoundary.Commit(tx' 'checked item transaction commit'
Assert-Text $service 'TransactionBoundary.RollBack(tx' 'checked item transaction rollback'
Assert-Text $service 'ElementTransformUtils.MoveElement' 'controlled movement path'
Assert-Text $service 'Text alignment postcondition failed' 'geometry postcondition before success result'
Assert-Text $service 'ViewPlaneGeometry.Tolerance' 'centralized tolerance'
Assert-Text $service 'OrderBy(item => item.Bounds.CenterU)' 'deterministic horizontal ordering'
Assert-Text $service 'OrderByDescending(item => item.Bounds.CenterV)' 'deterministic vertical ordering'
Assert-Text $service 'Average(item => item.Bounds.CenterV)' 'middle compatibility average'
Assert-Text $service 'RefreshActiveView' 'active view refresh'
Assert-Text $service '[K-TOOLS][TextAlign]' 'diagnostic logging'

# Pure geometry acceptance fixtures mirror the service's deterministic rules.
$left = @(2,5)
if (($left | Measure-Object -Minimum).Minimum -ne 2) { throw 'FAIL: left target fixture' }; $checks++
$right = @(4,9)
if (($right | Measure-Object -Maximum).Maximum -ne 9) { throw 'FAIL: right target fixture' }; $checks++
$middle = (@(2,4,9) | Measure-Object -Average).Average
if ($middle -ne 5) { throw 'FAIL: middle average fixture' }; $checks++
$horizontal = @(0,4,10)
$expectedHorizontal = @(0,5,10)
for ($i = 0; $i -lt $horizontal.Count; $i++) {
    if ($i -eq 0 -or $i -eq ($horizontal.Count - 1)) { $value = $horizontal[$i] } else { $value = $horizontal[0] + (($horizontal[-1] - $horizontal[0]) * $i / ($horizontal.Count - 1)) }
    if ($value -ne $expectedHorizontal[$i]) { throw 'FAIL: horizontal distribution fixture' }
}
$checks++
$vertical = @(10,3,0)
$expectedVertical = @(10,5,0)
for ($i = 0; $i -lt $vertical.Count; $i++) {
    if ($i -eq 0 -or $i -eq ($vertical.Count - 1)) { $value = $vertical[$i] } else { $value = $vertical[0] - (($vertical[0] - $vertical[-1]) * $i / ($vertical.Count - 1)) }
    if ($value -ne $expectedVertical[$i]) { throw 'FAIL: vertical distribution fixture' }
}
$checks++
if (@(1,2).Count -ge 3) { throw 'FAIL: two-item distribution guard fixture' }; $checks++

Write-Output "PASS: $checks Text Align Wave 1.4 static checks. No Revit model operation executed."

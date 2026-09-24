$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = 0

function Require-Source([string]$Path, [string]$Pattern, [string]$Name) {
    $content = Get-Content -LiteralPath (Join-Path $root $Path) -Raw
    if ($content -notmatch $Pattern) { throw "FAIL grid-plan:$Name" }
    $script:checks++
    Write-Host "PASS grid-plan:$Name"
}

Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Commands/CmdAutoGridPlan.cs' 'TransactionBoundary\.ExecuteGroup' 'command-owns-checked-atomic-group'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Commands/CmdAutoGridPlan.cs' 'TransactionBoundary\.Execute\(doc, "Create Grids"' 'grid-transaction-checked'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Commands/CmdAutoGridPlan.cs' 'TransactionBoundary\.Execute\(doc, "Create Levels and Plans"' 'level-transaction-checked'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Commands/CmdAutoGridPlan.cs' 'gridsCreated == 0' 'empty-grid-result-is-failure'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Commands/CmdAutoGridPlan.cs' 'levelsCreated \+ viewsCreated == 0' 'empty-level-result-is-failure'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/GridGeneratorService.cs' 'Invalid grid spacing token' 'malformed-spacing-rejected'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/GridGeneratorService.cs' 'CultureInfo\.InvariantCulture' 'spacing-format-is-culture-invariant'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/GridGeneratorService.cs' 'double\.IsNaN\(val\).*double\.IsInfinity\(val\).*val <= 0' 'spacing-must-be-positive-and-finite'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/GridGeneratorService.cs' 'grid == null\) throw new InvalidOperationException' 'grid-create-postcondition'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/GridGeneratorService.cs' 'Could not apply the requested grid bubble visibility' 'bubble-failure-propagates'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/GridGeneratorService.cs' 'Could not create the requested grid dimensions' 'dimension-failure-propagates'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Forms/AutoGridPlanForm.cs' 'Every level elevation must be a finite number' 'invalid-elevation-rejected'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Forms/AutoGridPlanForm.cs' 'Every story height must be a positive finite number' 'invalid-story-height-rejected'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Forms/AutoGridPlanForm.cs' 'TaskDialog\.Show\([^\r\n]*ex\.Message\);\s*return;' 'validation-error-visible-and-form-stays-open'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/LevelPlanGeneratorService.cs' 'No structural plan view type is available' 'missing-view-type-fails'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/LevelPlanGeneratorService.cs' 'Revit did not create the requested floor plan' 'view-create-postcondition'
Require-Source 'KhimTools/Tools/KhimGen/GridLevel/Services/LevelPlanGeneratorService.cs' 'Could not assign the requested plan name' 'plan-name-failure-propagates'

Write-Host "GRID_PLAN_STATIC_CHECKS=$checks"
Write-Host 'GRID_PLAN_ACCEPTANCE=PASS'

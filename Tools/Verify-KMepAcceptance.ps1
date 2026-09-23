$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ribbon = Get-Content (Join-Path $root 'KhimTools/Core/RibbonBuilder.cs') -Raw
$openingCommand = Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Penetrations/CmdMepOpenings.cs') -Raw
$openingService = Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Penetrations/MepOpeningAnalysisService.cs') -Raw
$measurement = Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/MepCurveMeasurementService.cs') -Raw
$noteCommand = Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Tags/CmdMepElevationTags.cs') -Raw
$noteService = Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Tags/MepElevationNoteService.cs') -Raw
$domain = Get-Content (Join-Path $root 'src/KhimTools.Domain/Models/Mep/MepMeasurementCalculator.cs') -Raw
$diag = Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/MepWorkflowDiagnostics.cs') -Raw
$fixture = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/RuntimeQa/Fixtures/KMepProductionRuntimeFixture.cs') -Raw
$registry = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/RuntimeQa/Core/RuntimeQaRegistry.cs') -Raw
$checks = 0
function Assert([bool]$condition, [string]$name) {
    if (!$condition) { throw "K-MEP acceptance failed: $name" }
    $script:checks++
    Write-Host "PASS mep:$name"
}

Assert ($ribbon.Contains('KhimTools.MEP.Penetrations.CmdMepOpenings')) 'ribbon-opening-command'
Assert ($ribbon.Contains('KhimTools.MEP.Tags.CmdMepElevationTags')) 'ribbon-elevation-command'
Assert ($openingCommand.Contains('class CmdMepOpenings : IExternalCommand')) 'opening-command-entry-point'
Assert ($noteCommand.Contains('class CmdMepElevationTags : IExternalCommand')) 'notes-command-entry-point'
Assert ($openingCommand.Contains('MepOpeningAnalysisService.Analyze')) 'opening-command-backend'
Assert ($noteCommand.Contains('MepElevationNoteService.Create')) 'notes-command-backend'
Assert ($measurement.Contains('BuiltInCategory.OST_DuctCurves') -and $measurement.Contains('BuiltInCategory.OST_PipeCurves') -and $measurement.Contains('BuiltInCategory.OST_CableTray')) 'supported-mep-categories-explicit'
Assert ($openingService.Contains('BuiltInCategory.OST_StructuralFraming') -and $openingService.Contains('BuiltInCategory.OST_Floors') -and $openingService.Contains('BuiltInCategory.OST_Walls')) 'supported-host-categories-explicit'
Assert ($openingService.Contains('BooleanOperationsUtils.ExecuteBooleanOperation') -and $openingService.Contains('overlap.Volume > GeometryTolerance')) 'exact-solid-clash-test'
Assert ($openingService.Contains('IntersectWithCurve(curve, options)')) 'intersection-location-resolution'
Assert ($openingService.Contains('new FilteredElementCollector(doc, view.Id)')) 'opening-active-view-host-scope'
Assert (!$openingCommand.Contains('new Transaction(') -and !$openingCommand.Contains('tx.Start()')) 'clash-review-read-only-no-transaction'
Assert ($openingCommand.Contains('does not create openings') -and $ribbon.Contains('does not create openings')) 'no-fake-opening-creation-claim'
Assert ($measurement.Contains('RBS_PIPE_OUTER_DIAMETER') -and $measurement.Contains('RBS_CURVE_DIAMETER_PARAM')) 'pipe-outer-diameter-source'
Assert ($measurement.Contains('RBS_CURVE_WIDTH_PARAM') -and $measurement.Contains('RBS_CURVE_HEIGHT_PARAM')) 'duct-section-source'
Assert ($measurement.Contains('RBS_CABLETRAY_WIDTH_PARAM') -and $measurement.Contains('RBS_CABLETRAY_HEIGHT_PARAM')) 'tray-section-source'
Assert ($domain.Contains('RoundOpening') -and $domain.Contains('RectangularOpening')) 'mep-domain-opening-dimensions'
Assert ($domain.Contains('clearanceEachSideMm') -and $domain.Contains('2 * clearanceEachSideMm')) 'clearance-applied-per-side'
Assert ($domain.Contains('VerticalRange') -and $domain.Contains('centerElevationMm - halfHeight')) 'elevation-domain-unit-math'
Assert ($noteCommand.Contains('view is ViewPlan') -and $noteCommand.Contains('view is ViewSection')) 'annotation-view-validation'
Assert ($noteService.Contains('MepMeasurementCalculator.VerticalRange') -and $noteService.Contains('MepCurveMeasurementService.ReadSection')) 'notes-use-shared-measurement-domain'
Assert ($noteService.Contains('TextNote.Create') -and $noteService.Contains('persisted.OwnerViewId != view.Id')) 'annotation-creation-postcondition'
Assert ($noteService.Contains('existingText.Add(noteText)') -and $noteService.Contains('TransactionBoundary.RollBack(sub')) 'annotation-idempotence-and-item-rollback'
Assert ($noteService.Contains('TransactionBoundary.Commit(transaction') -and $noteService.Contains('TransactionBoundary.RollBack(transaction')) 'annotation-batch-transaction-boundaries'
Assert ($diag.Contains('DocumentIdentity.From(document).StableKey') -and $diag.Contains('eligible=') -and $diag.Contains('skipped=') -and $diag.Contains('postcondition=')) 'canonical-measurement-diagnostics'
Assert ($fixture.Contains('MepOpeningAnalysisService.Analyze') -and $fixture.Contains('MepElevationNoteService.Create')) 'host-fixture-uses-production-services'
Assert ($fixture.Contains('KMEP_ANALYSIS_READ_ONLY') -and $fixture.Contains('KMEP_OUTER_ROLLBACK')) 'host-fixture-readonly-and-rollback-scenarios'
Assert ($registry.Contains('new KMepProductionRuntimeFixture()') -and $registry.Contains('Id = "MEP"')) 'host-fixture-registered'
Assert ($openingService -notmatch 'OST_Conduit|ConnectorManager|ConnectTo\(') 'no-unsupported-conduit-or-connector-scope'
Assert (($openingCommand + $noteCommand + $openingService + $noteService + $domain) -notmatch 'C:\\Users\\[^\\]+\\') 'no-machine-specific-paths'

Write-Host "KMEP_ACCEPTANCE=PASS"
Write-Host "KMEP_ACCEPTANCE_CHECKS=$checks"

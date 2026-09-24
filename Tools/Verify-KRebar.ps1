[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$base = Join-Path $repoRoot 'KhimTools\Tools\KhimStructural\RebarTool'
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [bool]$Pass, [string]$Evidence) {
    $script:checks.Add([pscustomobject]@{ Name = $Name; Pass = $Pass; Evidence = $Evidence })
}

$required = @(
    'Core\BeamRebarGenerator.cs',
    'Core\RectangularColumnRebarGenerator.cs',
    'Core\CircularColumnRebarGenerator.cs',
    'Core\SlabRebarGenerator.cs',
    'Core\FoundationRebarGenerator.cs',
    'Core\RebarGenerationReport.cs',
    'Core\RebarGenerationFailurePreprocessor.cs',
    'Core\RebarShapeCreationHelper.cs',
    'Core\RebarCoverHelper.cs',
    'Core\RebarHookHelper.cs',
    'Core\RebarLapSpliceHelper.cs',
    'Forms\BeamReinforcementForm.cs',
    'Forms\RectangularColumnReinforcementForm.cs',
    'Forms\CircularColumnReinforcementForm.cs',
    'Forms\SlabReinforcementForm.cs',
    'Forms\FoundationReinforcementForm.cs'
)
foreach ($relative in $required) {
    $path = Join-Path $base $relative
    Add-Check "production-source:$relative" (Test-Path -LiteralPath $path) $path
}

$runtimePath = Join-Path $repoRoot 'KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\RebarRuntimeFixtures.cs'
$runtimeText = if (Test-Path -LiteralPath $runtimePath) { Get-Content -Raw $runtimePath } else { '' }
foreach ($fixture in @('RC-STATION', 'RC-FULL', 'CC', 'BR', 'SR', 'FR', 'RC-PREVIEW', 'BR-PREVIEW-RECT', 'BR-PREVIEW-ROTATED', 'SR-PREVIEW-RECT', 'SR-PREVIEW-OPENING', 'CC-PREVIEW')) {
    Add-Check "runtime-fixture:$fixture" ($runtimeText.Contains('"' + $fixture + '"')) $runtimePath
}

$columnText = Get-Content -Raw (Join-Path $base 'Forms\RectangularColumnReinforcementForm.cs')
Add-Check 'rectangular-column-2d-form-preview' ($columnText.Contains('PreviewPanel_Paint')) 'Form paints a responsive 2D schematic.'
Add-Check 'rectangular-column-create-gated-by-current-preview' ($columnText.Contains('_previewLifecycle.State == PreviewLifecycleState.Valid && _lastPreview != null') -and $columnText.Contains('UpdatePreviewStateUi();') -and $columnText.Contains('_formGuard?.ValidateNow();')) 'Create stays disabled until a solver-backed preview is accepted and current.'
$previewServicePath = Join-Path $base 'Core\RebarPreviewService.cs'
$previewService = if (Test-Path -LiteralPath $previewServicePath) { Get-Content -Raw $previewServicePath } else { '' }
$previewFormPath = Join-Path $base 'Forms\RebarSolverPreviewForm.cs'
$previewForm = if (Test-Path -LiteralPath $previewFormPath) { Get-Content -Raw $previewFormPath } else { '' }
$registryPath = Join-Path $repoRoot 'KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaRegistry.cs'
$registryText = if (Test-Path -LiteralPath $registryPath) { Get-Content -Raw $registryPath } else { '' }
Add-Check 'common-detached-preview-pipeline' ($previewService.Contains('class RebarPreviewService') -and $previewService.Contains('class RebarPreviewSnapshot')) $previewServicePath
Add-Check 'rollback-only-solver-capture' ($previewService.Contains('transaction.RollBack()') -and $previewService.Contains('WorkflowFingerprint') -and $previewService.Contains('RolledBack')) 'Capture rolls back and verifies pre/post document fingerprints.'
Add-Check 'solver-derived-centerline-geometry' ($previewService.Contains('GetCenterlineCurves') -and $previewService.Contains('Tessellate')) 'Detached path coordinates are captured from Revit-solved centerlines.'
Add-Check 'solver-preview-inspection-controls' ($previewForm.Contains('"ISO"') -and $previewForm.Contains('"TOP"') -and $previewForm.Contains('"FRONT"') -and $previewForm.Contains('"RIGHT"') -and $previewForm.Contains('"FIT"') -and $previewForm.Contains('Canvas_MouseWheel') -and $previewForm.Contains('Canvas_MouseMove') -and $previewForm.Contains('0.82f') -and $previewForm.Contains('Filter solver preview by host') -and $previewForm.Contains('GetVisibleComponents()')) 'Detached Rebar solver viewer provides four engineering projections, per-host filtering, 82% fit, bounded wheel zoom, pan and Fit.'
$referenceViews = Get-Content -Raw (Join-Path $base 'Forms\RebarReferenceViews.cs')
Add-Check 'rebar-reference-view-vietnamese-encoding' ($referenceViews -cnotmatch '(?:\u00C3.|\u00E1[\u00BA\u00BB]|\u00C4.|\u00E2\u20AC\u00A2|\uFFFD)') 'Rebar reference-view text contains no common UTF-8 mojibake signatures or replacement characters.'
Add-Check 'stable-input-fingerprints' ($previewService.Contains('VersionGuid') -and $previewService.Contains('BarsAlongB') -and $previewService.Contains('TieLayout') -and $previewService.Contains('AdjacentColumnAbove.VersionGuid') -and $previewService.Contains('AdjacentColumnBelow.VersionGuid')) 'Fingerprint includes host/type/adjacent-host versions and generation settings.'
Add-Check 'preview-ui-wired' ($columnText.Contains('RebarPreviewService.Capture') -and $columnText.Contains('new RebarSolverPreviewForm') -and $previewForm.Contains('RebarPreviewSnapshot') -and $previewForm.Contains('DrawLines')) 'Rectangular-column UI renders detached solver paths in a disposable preview form.'
Add-Check 'precommit-preview-parity' ($columnText.Contains('RebarPreviewService.Matches') -and $columnText.Contains('_lastPreview')) 'Generation rejects missing/stale preview and compares solved geometry before commit.'
Add-Check 'preview-runtime-fixture-registered' ($registryText.Contains('new RectangularColumnPreviewRuntimeFixture()')) $registryPath

$beamText = Get-Content -Raw (Join-Path $base 'Forms\BeamReinforcementForm.cs')
$slabText = Get-Content -Raw (Join-Path $base 'Forms\SlabReinforcementForm.cs')
$beamGenerator = Get-Content -Raw (Join-Path $base 'Core\BeamRebarGenerator.cs')
Add-Check 'beam-generator-vietnamese-encoding' ($beamGenerator -cnotmatch '(?:\u00C3.|\u00E1[\u00BA\u00BB]|\u00C4.|\u00E2\u20AC\u00A2|\uFFFD)') 'Beam generator comments and diagnostics contain no common UTF-8 mojibake signatures or replacement characters.'
$slabGenerator = Get-Content -Raw (Join-Path $base 'Core\SlabRebarGenerator.cs')
$foundationText = Get-Content -Raw (Join-Path $base 'Forms\FoundationReinforcementForm.cs')
$foundationRoleNavigation = $foundationText.Contains('Foundation role-oriented settings') -and $foundationText.Contains('AddRoleNavigation(roleNavigation') -and $foundationText.Contains('UpdateRoleNavigation();') -and $foundationText.Contains('ItemSize = new Size(0, 1)')
Add-Check 'foundation-role-navigation-replaces-duplicate-tabs' $foundationRoleNavigation 'Foundation settings use a compact role navigator with the duplicate tab strip hidden.'
$foundationGenerator = Get-Content -Raw (Join-Path $base 'Core\FoundationRebarGenerator.cs')
$foundationRuntime = Get-Content -Raw $runtimePath
$circularText = Get-Content -Raw (Join-Path $base 'Forms\CircularColumnReinforcementForm.cs')
$circularRuntime = $runtimeText
Add-Check 'beam-solver-preview-calls-production-generator' ($beamText.Contains('RebarPreviewService.Capture') -and $beamText.Contains('generator.Generate(input)') -and $beamText.Contains('RebarPreviewService.Matches')) 'Beam preview and pre-commit verification invoke BeamRebarGenerator.'
Add-Check 'beam-preview-fingerprint-covers-discovered-support-context' ($previewService.Contains('GetReinforcementContextFingerprint(input.Beam)') -and $beamGenerator.Contains('GetReinforcementContextFingerprint(FamilyInstance beam)') -and $beamGenerator.Contains('FindIntersectingSecondaryBeams(beam, intersectingBeams)') -and $beamGenerator.Contains('FindSupportingColumn(endPoint)') -and $beamGenerator.Contains('FindSupportingBeam(endPoint, beam)')) 'Beam preview invalidates when generator-discovered supports or intersecting secondary beams change.'
Add-Check 'slab-solver-preview-calls-production-generator' ($slabText.Contains('RebarPreviewService.Capture') -and $slabText.Contains('generator.GeneratePanel(panel') -and $slabText.Contains('RebarPreviewService.Matches')) 'Slab preview and pre-commit verification invoke SlabRebarGenerator.'
Add-Check 'slab-opening-trim-rejects-unsupported-shapes' ($slabGenerator.Contains('TryGetAxisAlignedRectangularOpeningBounds') -and $slabGenerator.Contains('report.AddError(floor, "Opening trim geometry", unsupported)') -and $slabGenerator.Contains('Automatic opening trim bars currently support axis-aligned rectangular openings only')) 'Opening trim bars reject rotated/non-rectangular openings instead of using a misleading global bounding box.'
Add-Check 'beam-and-slab-canonical-solver-plans' ($beamGenerator.Contains('Generate(') -and $slabGenerator.Contains('GeneratePanel(') -and $previewService.Contains('request.Generate()')) 'Preview delegates to production generators in the rollback-only request boundary; execution uses those same generators.'
Add-Check 'all-host-stale-preview-revalidation' ($columnText.Contains('FingerprintInputs(inputGroups.SelectMany(group => group).Select(RebarPreviewService.Fingerprint))') -and $beamText.Contains('FingerprintInputs(generationInputs.Select(RebarPreviewService.Fingerprint))') -and $slabText.Contains('FingerprintInputs(selectedPanels.Select(generator.GetPanelInputFingerprint))') -and $columnText.Contains('_previewLifecycle.TryGetValid(currentFingerprint') -and $beamText.Contains('_previewLifecycle.TryGetValid(currentFingerprint') -and $slabText.Contains('_previewLifecycle.TryGetValid(currentFingerprint') -and $circularText.Contains('CurrentInputFingerprint()') -and $circularText.Contains('_previewLifecycle.TryGetValid(currentFingerprint') -and $previewService.Contains('CurrentInputFingerprint()')) 'Rectangular/circular columns, beam and slab recompute live fingerprints and require their accepted lifecycle snapshot before execution.'
Add-Check 'all-host-duplicate-bar-protection' ($columnText.Contains('HasExistingDuplicateBar') -and $circularText.Contains('HasExistingDuplicateBar') -and $beamText.Contains('HasExistingDuplicateBar') -and $slabText.Contains('HasExistingDuplicateBar')) 'All supported preview hosts reject existing bars matching the planned type and centerline signatures.'
Add-Check 'all-host-preview-refresh-cancel-controls' ($columnText.Contains('BtnPreview3D_Click') -and $circularText.Contains('BtnPreview3D_Click') -and $beamText.Contains('BtnPreview_Click') -and $slabText.Contains('BtnPreviewRebar_Click') -and $previewForm.Contains('DialogResult.Cancel')) 'Each host can explicitly refresh through a new solve; cancel closes the detached preview without accepting it.'
Add-Check 'all-host-ui-inputs-shared-by-preview-and-create' ($columnText.Contains('BuildGenerationInputGroups(') -and $circularText.Contains('BuildGenerationInputGroups(') -and $beamText.Contains('CreateGenerationInput') -and $slabText.Contains('AssignSettingsToPanels(') -and $columnText.Contains('RebarPreviewService.Capture') -and $circularText.Contains('RebarPreviewService.Capture') -and $beamText.Contains('RebarPreviewService.Capture') -and $slabText.Contains('RebarPreviewService.Capture')) 'Preview and create consume the same UI-derived inputs for rectangular/circular columns, beam and assigned slab panels.'
$slabPaintBody = [regex]::Match($slabText, '(?s)private void PaintSlabPreview\(.*?(?=\r?\n        private static PointF\[\]\[\] DetachLoopViews)').Value
Add-Check 'slab-preview-paint-uses-detached-geometry' ($slabPaintBody.Contains('_detachedPanelGeometry.TryGetValue') -and $slabPaintBody.Contains('ProjectPath') -and $slabPaintBody -notmatch '\.Tessellate\(\)|UnitUtils\.') 'WinForms painting reads detached panel/path coordinates only; no Revit geometry/API calls occur in Paint.'
Add-Check 'slab-create-disabled-until-preview-valid' ($slabText.Contains('new RebarValidationRule(_btnCreateRebar') -and $slabText.Contains('_previewLifecycle.State == PreviewLifecycleState.Valid && _lastPreview != null') -and $slabText.Contains('MarkPreviewStale();')) 'The create-button validation guard requires an accepted preview and input changes invalidate it.'
Add-Check 'foundation-preview-calls-production-generator-in-rollback-capture' ($foundationText.Contains('RebarPreviewService.Capture') -and $foundationText.Contains('GenerateFoundation(generator, profile, _settings)') -and $previewService.Contains('transaction.RollBack()') -and $foundationGenerator.Contains('Generate(FoundationProfile profile')) 'Foundation preview solves production generator output in the shared rollback-only capture boundary.'
Add-Check 'foundation-input-fingerprint-covers-host-settings-and-types' ($previewService.Contains('Fingerprint(FoundationProfile profile, FoundationRebarSettings settings)') -and $previewService.Contains('FoundationRebarGenerator.FindBarType(types, label)') -and $previewService.Contains('FoundationElement.VersionGuid') -and $previewService.Contains('SerializeObject(settings')) 'Foundation input fingerprint includes host revision, bounding geometry, serialized settings and resolved bar types.'
Add-Check 'foundation-create-requires-fresh-preview-and-parity' ($foundationText.Contains('_previewLifecycle.TryGetValid(currentFingerprint') -and $foundationText.Contains('HasExistingDuplicateBar(_doc, profile.FoundationElement') -and $foundationText.Contains('RebarPreviewService.Matches(acceptedPreview') -and $foundationText.Contains('TransactionBoundary.Execute(_doc')) 'Foundation create rejects stale/missing preview and duplicates, then checks centerline parity before transaction completion.'
$foundationPaintBody = [regex]::Match($foundationText, '(?s)private void PreviewPanel_Paint\(.*?(?=\r?\n        private void AttachPreviewInvalidationHandlers)').Value
Add-Check 'foundation-2d-preview-draws-detached-solver-output' ($foundationPaintBody.Contains('_previewOutlines.TryGetValue') -and $foundationPaintBody.Contains('_lastPreview?.Find(fingerprint)') -and $foundationPaintBody.Contains('component.Paths') -and $foundationPaintBody -notmatch 'DrawLine\(pRed|new XYZ|\.get_BoundingBox\(') 'Foundation 2D view uses detached host bounds and actual solver paths; hard-coded schematic rebar was removed from Paint.'
Add-Check 'foundation-preview-host-fixture-registered-and-truthful' ($registryText.Contains('new FoundationRebarRuntimeFixture()') -and $foundationRuntime.Contains('RebarPreviewService.Capture') -and $foundationRuntime.Contains('FR_PARITY') -and $foundationRuntime.Contains('FR_CANCEL')) 'Foundation host preview, rollback, and parity fixture is registered for host execution; static audit does not claim it ran.'
Add-Check 'circular-column-preview-calls-production-generator-in-rollback-capture' ($circularText.Contains('RebarPreviewService.Capture') -and $circularText.Contains('generator.Generate(input, report)') -and $circularText.Contains('RebarSolverPreviewForm')) 'Circular-column preview uses production generator output and detached solved centerlines.'
Add-Check 'circular-column-create-requires-fresh-preview-and-parity' ($circularText.Contains('_previewLifecycle.TryGetValid(currentFingerprint') -and $circularText.Contains('HasExistingDuplicateBar(_doc, input.Column') -and $circularText.Contains('RebarPreviewService.Matches(acceptedPreview') -and $circularText.Contains('CurrentInputFingerprint()')) 'Circular-column Create requires the current aggregate fingerprint, rejects duplicates and verifies solved centerline parity.'
Add-Check 'circular-column-preview-fixture-registered-and-truthful' ($registryText.Contains('new CircularColumnPreviewRuntimeFixture()') -and $circularRuntime.Contains('CC-PREVIEW_REFRESH') -and $circularRuntime.Contains('CC-PREVIEW_CANCEL') -and $circularRuntime.Contains('CC-PREVIEW_PARITY') -and $circularRuntime.Contains('CC-PREVIEW_ROLLBACK')) 'Circular-column runtime scenarios are registered/not executed; fixture checks refresh, cancellation, parity and rollback.'
Add-Check 'circular-column-no-independent-schematic-preview' ($circularText.Contains('No solved geometry displayed') -and $circularText -notmatch 'g.DrawEllipse\(penColumn' -and $circularText.Contains('PreviewPanel_Paint')) 'The former decorative bar sketch is removed; the form directs users to the production solver preview.'
Add-Check 'circular-column-visible-stale-preview-state' ($circularText.Contains('AttachPreviewInvalidationHandlers(tabControl)') -and $circularText.Contains('MarkPreviewStale();') -and $circularText.Contains('The previous preview is stale.')) 'Circular-column host selection and parameter edits mark the displayed preview stale and refresh the guarded Create state.'
Add-Check 'all-host-runtime-fixtures-registered' ($registryText.Contains('new RectangularColumnPreviewRuntimeFixture()') -and $registryText.Contains('new CircularColumnPreviewRuntimeFixture()') -and $registryText.Contains('new RectangularBeamPreviewRuntimeFixture()') -and $registryText.Contains('new RotatedBeamPreviewRuntimeFixture()') -and $registryText.Contains('new RectangularSlabPreviewRuntimeFixture()') -and $registryText.Contains('new SlabOpeningPreviewRuntimeFixture()')) $registryPath
$hostScenariosPath = Join-Path $repoRoot 'Tools\RebarPreviewHostScenarios.md'
$hostScenarios = if (Test-Path -LiteralPath $hostScenariosPath) { Get-Content -Raw $hostScenariosPath } else { '' }
Add-Check 'column-beam-slab-foundation-undo-scenarios-registered' (@('COLUMN_UNDO', 'CIRCULAR_COLUMN_UNDO', 'CIRCULAR_COLUMN_CANCEL', 'CIRCULAR_COLUMN_DUPLICATE', 'BEAM_UNDO', 'BEAM_SUPPORT_STALE', 'SLAB_UNDO', 'SLAB_OPENING_SHAPE_REJECTION', 'FOUNDATION_UNDO', 'FOUNDATION_CANCEL', 'FOUNDATION_DUPLICATE' | Where-Object { -not $hostScenarios.Contains($_) }).Count -eq 0) 'Tools/RebarPreviewHostScenarios.md registers host-only column/circular-column/beam/slab/foundation create/Undo/cancel/duplicate/stale-support scenarios as NOT_EXECUTED.'

$wallCommand = Get-ChildItem -LiteralPath (Join-Path $base 'Commands') -File -Filter '*Wall*Rebar*.cs'
$wallGenerator = Get-ChildItem -LiteralPath (Join-Path $base 'Core') -File -Filter '*Wall*Rebar*.cs'
Add-Check 'wall-rebar-explicitly-unsupported' ($wallCommand.Count -eq 0 -and $wallGenerator.Count -eq 0) 'No wall command/generator; documentation must retain this boundary.'

$columnDrawingPath = Join-Path $base 'Commands\CmdColumnDrawing.cs'
$columnDrawing = Get-Content -Raw $columnDrawingPath
Add-Check 'column-drawing-checked-transaction-ownership' ($columnDrawing.Contains('TransactionBoundary.ExecuteGroup') -and $columnDrawing.Contains('TransactionBoundary.Execute(doc') -and $columnDrawing -notmatch 'new Transaction\(|tx\.Start\(\)|tx\.Commit\(\)') $columnDrawingPath
Add-Check 'column-drawing-three-view-postconditions' ($columnDrawing.Contains('ViewDrafting drawingView') -and $columnDrawing.Contains('ViewPlan sectionView') -and $columnDrawing.Contains('View3D inspectionView') -and $columnDrawing.Contains('generatedViewIds.Any') -and $columnDrawing.Contains('doc.GetElement(id)')) 'Drafting, section, and 3D views are checked before transaction-group assimilation.'

$passCount = @($checks | Where-Object Pass).Count
$failures = @($checks | Where-Object { -not $_.Pass })
foreach ($check in $checks) {
    $status = if ($check.Pass) { 'PASS' } else { 'BLOCKED' }
    Write-Output ("{0} {1} - {2}" -f $status, $check.Name, $check.Evidence)
}
Write-Output ("KREBAR_STATIC_CHECKS={0}/{1}" -f $passCount, $checks.Count)
if ($failures.Count -gt 0) {
    Write-Output 'KREBAR_PREVIEW_ACCEPTANCE=BLOCKED'
    Write-Output 'KREBAR_STATIC_ACCEPTANCE=PARTIAL'
    exit 1
}

Write-Output 'KREBAR_STATIC_ACCEPTANCE=PASS'
exit 0

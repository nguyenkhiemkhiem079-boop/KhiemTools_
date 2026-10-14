$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source=Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Penetrations/MepOpeningCreationServiceV1.cs') -Raw
$plan=Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Penetrations/MepOpeningCutPlanV1.cs') -Raw
$command=Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Penetrations/CmdMepOpeningCreateV1.cs') -Raw
$preview=Get-Content (Join-Path $root 'KhimTools/Tools/KhimMEP/Penetrations/MepOpeningPreviewFormV1.cs') -Raw
$fixture=Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/RuntimeQa/Fixtures/MepOpeningCreationRuntimeFixtureV1.cs') -Raw
$checks=0
function Check([bool]$condition,[string]$label){if(!$condition){throw "Opening creation gate failed: $label"};$script:checks++;Write-Host "PASS opening:$label"}
Check ($plan.Contains('double? clearanceMm') -and $plan.Contains('no default')) 'explicit-clearance-no-engineering-default'
Check ($plan.Contains('never clamp') -and $plan.Contains('plan.X0<=0')) 'no-boundary-clamping'
Check ($source.Contains('host?.Document!=doc') -and $source.Contains('mep?.Document!=doc')) 'local-host-ownership'
Check ($source.Contains('JoinGeometryUtils.GetJoinedElements') -and $source.Contains('get_ElementsAtJoin')) 'no-unverified-join-side-effects'
Check ($source.Contains('BooleanOperationsType.Intersect') -and $source.Contains('No actual solid clash')) 'real-solid-clash-required'
Check ($source.Contains('completely traverse both host faces') -and $source.Contains('Oblique or parallel penetration unsupported')) 'perpendicular-through-penetration'
Check ($source.Contains('if(fresh.Fingerprint!=accepted.Fingerprint)')) 'stale-review-blocked'
Check ($source.Contains('doc.Create.NewOpening(wall') -and $source.Contains('doc.Create.NewOpening(fresh.Host,loop,false)')) 'real-wall-and-floor-native-creation'
Check ($source.Contains('removed.Volume-fresh.Cut.Volume') -and $source.Contains('Boolean(remaining,fresh.Before')) 'actual-cut-no-extra-concrete-postconditions'
Check ($source.Contains('RejectReinforcement') -and $source.Contains('element is RebarInSystem') -and $source.Contains('requires separate approved reinforcement redesign')) 'no-silent-rebar-cut-or-system-omission'
Check ($command.Contains('DefaultButton=TaskDialogResult.Cancel') -and $command.Contains('dialog.Show()!=TaskDialogResult.Ok') -and $command.Contains('TransactionBoundary.ExecuteGroup')) 'explicit-confirmation-before-atomic-mutation'
Check ($fixture.Contains('QA_ONLY_CONTROLLED_FAILURE_AFTER_VERIFIED_CUT') -and $fixture.Contains('RuntimeQaSafetyGuard.VerifyRollback')) 'host-fixture-model-state-and-rollback'
Check ($fixture.Contains('BLOCKED: no suitable disposable cut resources') -and $fixture.Contains('registration/builds are not host execution')) 'missing-resources-never-pass'
Check ($plan.Contains('minimumWidthMm') -and $plan.Contains('Offset would lose the full actual clash')) 'explicit-minima-offset-clearance-containment'
Check ($source.Contains('accepted.Plan.MinimumWidthMm,accepted.Plan.MinimumHeightMm,accepted.Plan.OffsetXMm,accepted.Plan.OffsetYMm') -and $source.Contains('p.Plan.OffsetXMm,p.Plan.OffsetYMm')) 'all-reviewed-inputs-fresh-solve-and-fingerprint'
Check ($source.Contains('FLOOR_PARAM_IS_STRUCTURAL')) 'structural-floor-eligibility'
Check ($command.Contains('MepOpeningPreviewFormV1') -and $command.Contains('values=new double[5]')) 'detached-graphical-review-required-before-confirm'
Check ($preview.Contains('3D wireframe') -and $preview.Contains('2D host plane') -and !$preview.Contains('using Autodesk.Revit')) 'paint-detached-no-revit-api'
Write-Host "MEP_OPENING_CREATION_STATIC=PASS ($checks checks); LIVE_HOST=NOT_EXECUTED"

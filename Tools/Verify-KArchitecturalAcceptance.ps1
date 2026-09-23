param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ribbon = Get-Content (Join-Path $root 'KhimTools/Core/RibbonBuilder.cs') -Raw
$sources = @{}
Get-ChildItem (Join-Path $root 'KhimTools/Tools/KhimArchitectural') -Recurse -File -Filter '*.cs' |
    ForEach-Object { $sources[$_.FullName.Substring($root.Length + 1).Replace('\','/')] = Get-Content $_.FullName -Raw }
$checks = 0
function Assert([bool]$condition, [string]$name) {
    if (!$condition) { throw "K-Architectural acceptance failed: $name" }
    $script:checks++
    Write-Host "PASS arch:$name"
}
$commands = @(
    'KhimTools.Architectural.QuickArchi.Commands.CmdQuickArchi',
    'KhimTools.Architectural.Rooms.CmdRoom3DView',
    'KhimTools.Architectural.Finishes.CmdWallFloorFinishes',
    'KhimTools.Architectural.Lintels.CmdCreateLintels',
    'KhimTools.Architectural.DoorDetails.CmdCreateDoorAssemblies'
)
foreach ($command in $commands) {
    Assert ($ribbon.Contains($command)) "ribbon:$command"
    $body = ($sources.Values -join "`n")
    Assert ($body.Contains("class $($command.Split('.')[-1])") -and $body.Contains('IExternalCommand')) "command:$command"
}
$quick = $sources['KhimTools/Tools/KhimArchitectural/QuickArchi/Services/QuickArchiService.cs']
$finish = $sources['KhimTools/Tools/KhimArchitectural/Finishes/CmdWallFloorFinishes.cs']
$room = $sources['KhimTools/Tools/KhimArchitectural/Rooms/CmdRoom3DView.cs']
$roomService = $sources['KhimTools/Tools/KhimArchitectural/Rooms/Room3DViewService.cs']
$lintel = $sources['KhimTools/Tools/KhimArchitectural/Lintels/LintelService.cs']
$door = $sources['KhimTools/Tools/KhimArchitectural/DoorDetails/CmdCreateDoorAssemblies.cs']
$diag = $sources['KhimTools/Tools/KhimArchitectural/ArchitecturalDiagnostics.cs']
$runtime = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/RuntimeQa/Fixtures/KArchitecturalRuntimeFixture.cs') -Raw
$registry = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/RuntimeQa/Core/RuntimeQaRegistry.cs') -Raw
Assert ($quick.Contains('TransactionBoundary.Start') -and $quick.Contains('TransactionBoundary.Commit') -and $quick.Contains('TransactionBoundary.RollBack')) 'quick-archi-transaction-boundaries'
Assert ($quick.Contains('doc.GetElement(wall.Id) is not Wall') -and $quick.Contains('CreateRooms(Document doc, ViewPlan planView)')) 'quick-archi-postconditions'
Assert ($quick.Contains('ArchitecturalDiagnostics.Log')) 'quick-archi-diagnostics'
Assert ($finish.Contains('TransactionBoundary.Start') -and $finish.Contains('TransactionBoundary.Commit') -and $finish.Contains('TransactionBoundary.RollBack')) 'room-finish-transaction-boundaries'
Assert ($finish.Contains('doc.GetElement(finishFloor.Id) is not Floor') -and $finish.Contains('room.LevelId')) 'room-finish-postconditions'
Assert ($finish.Contains('RoomUniqueId=') -and $finish.Contains('existingFinishMarkers.Contains(marker)')) 'room-finish-idempotence'
Assert ($finish.Contains('foreach (var ring in boundarySegments)') -and !$finish.Contains('boundarySegments.First()')) 'room-finish-holes-preserved'
Assert ($finish.Contains('doc.ActiveView.Id')) 'room-finish-view-scoped-fallback'
Assert ($finish.Contains('ArchitecturalDiagnostics.Log')) 'room-finish-diagnostics'
Assert ($room.Contains('doc.ActiveView.Id') -and $roomService.Contains('TransactionBoundary.Start')) 'room-3d-explicit-scope-and-transaction'
Assert ($roomService.Contains('verified.IsSectionBoxActive') -and $roomService.Contains('TransactionBoundary.Commit')) 'room-3d-postcondition'
Assert ($roomService.Contains('existingNames') -and !$roomService.Contains('OfClass(typeof(View3D)).Any')) 'room-3d-no-repeat-collector'
Assert ($room.Contains('ArchitecturalDiagnostics.Log')) 'room-3d-diagnostics'
Assert ($roomService.Contains('transform.OfPoint') -and $roomService.Contains('worldMin')) 'room-3d-transformed-bounding-box'
Assert ($runtime.Contains('Room3DViewService.Create') -and $runtime.Contains('context.TrackCreated(created.Id)')) 'room-3d-host-fixture-uses-production-service'
Assert ($runtime.Contains('KARCH_ROOM3D_SCOPE') -and $runtime.Contains('KARCH_ROOM3D_OUTER_ROLLBACK')) 'room-3d-host-scenario-and-rollback'
Assert ($registry.Contains('new KArchitecturalRuntimeFixture()') -and $registry.Contains('Id = "ARCHITECTURAL"')) 'room-3d-fixture-registered'
Assert ($lintel.Contains('KhimTools.Domain.Models.Architectural') -and $lintel.Contains('LintelLayout.Compute')) 'lintel-pure-domain-layout'
Assert ($lintel.Contains('TransactionBoundary.Start') -and $lintel.Contains('TransactionBoundary.Commit') -and $lintel.Contains('TransactionBoundary.RollBack')) 'lintel-transaction-boundaries'
Assert ($lintel.Contains('generatedCurve.Curve.Length') -and $lintel.Contains('CreatedIds')) 'lintel-postconditions'
Assert ($lintel.Contains('duplicate prevention cannot be guaranteed') -and $lintel.Contains('comments.AsString()')) 'lintel-idempotence-marker-verified'
Assert ($lintel.Contains('GetExistingSourceIds(doc)') -and $lintel.Contains('existing.Contains(opening.UniqueId)')) 'lintel-idempotence-marker'
Assert ($door.Contains('TransactionBoundary.Start') -and $door.Contains('TransactionBoundary.Commit') -and $door.Contains('TransactionBoundary.RollBack')) 'door-assembly-transaction-boundaries'
Assert ($door.Contains('persisted.GetMemberIds().Contains(door.Id)') -and $door.Contains('door.AssemblyInstanceId != assembly.Id')) 'door-assembly-membership-postcondition'
Assert ($door.Contains('existingNames') -and $door.Contains('AllocateUniqueName(existingNames, door)')) 'door-assembly-name-cache'
Assert ($door.Contains('RequireView(') -and !$door.Contains('catch { }')) 'door-assembly-view-failure-visible'
Assert ($door.Contains('ArchitecturalDiagnostics.Log')) 'door-assembly-diagnostics'
Assert ($diag.Contains('WorkflowDiagnostic') -and $diag.Contains('DocumentIdentity.From(document).StableKey') -and $diag.Contains('durationMs=')) 'canonical-structured-diagnostics'
Assert (!(($sources.Values -join "`n") -match 'C:\\Users\\[^\\]+\\')) 'no-user-machine-absolute-paths'

Write-Host "KARCH_ACCEPTANCE=PASS"
Write-Host "KARCH_ACCEPTANCE_CHECKS=$checks"

# Phase 5 — K-Architectural Inventory, Acceptance, and QA Evidence

Branch: `phase5/k-architectural`, based on the local Phase 4 closure commit. No push or merge.

## Inventory and classification

Five unique production commands are exposed by the K-Architectural ribbon: Quick Archi,
Room 3D View, Room Finishes, Create Lintels, and Create Door Assemblies. Room 3D View and
Room Finishes are additionally bound into the workspace. Room Finishes is intentionally
reachable from two ribbon locations. All five compile as public `IExternalCommand` entry
points.

| Capability | Production path | Current disposition |
| --- | --- | --- |
| Quick Archi | Ribbon/command → WPF form → `QuickArchiService` → wall/room transactions | Production; wall segments are isolated by subtransactions and verified; room creation is a separately verified transaction |
| Room 3D View | Ribbon/workspace → command → `Room3DViewService` | Production; selection then active-view scope, transformed bounds, unique name, verified section box |
| Room Finishes | Ribbon/workspace → command | Production; selection then active-view scope, all boundary rings retained, per-room subtransaction, stable room-ID marker prevents duplicate runs, and floor/level postcondition |
| Create Lintels | Ribbon → command/WinForms options → `LintelService` → domain layout | Production; validates inputs, checks hosted opening/width/type, isolates each opening, verifies created framing and writable/read-back duplicate marker |
| Door Assemblies | Ribbon → command/selection → command backend | Production; unique-name cache, per-door subtransactions, assembly membership and requested detail-view postconditions |
| QuickArchi settings | QuickArchi model | Production input contract; finite values and positive wall height are validated at form/service boundary |
| QuickArchi window | WPF, no ViewModel | UI-only orchestration retained; geometry and Revit mutations remain in service |
| Lintel settings | WinForms options dialog, no ViewModel | UI-only; provides family type, extension, offset, and existing-lintel policy |
| Room/finish/door command forms | None | UI handled by Revit selection/task dialogs; no hidden form/backend duplicates found |
| PHM crosswalk | No unpacked PHM architectural reference files located in this checkout | Not available; no external behavior is claimed or copied |

## Architecture and correctness changes

- Added Revit-free `LintelLayout` in `src/KhimTools.Domain`; Revit internal distances are
  converted at the adapter boundary, and geometry validation/endpoint math runs in millimetres.
- Centralized stable document key + operation/requested/affected/failure/duration logging in
  `ArchitecturalDiagnostics`; no local document path is logged.
- Replaced unchecked commit calls with checked transaction-boundary operations in all mutation
  workflows touched. Failed wall/room/floor/lintel/assembly items are counted, not silently
  reported as created. Local failures are isolated where safe; batch-level exceptions roll back.
- Added operation-specific creation and membership checks before commits.
- Removed the full-model Room 3D fallback; automatic discovery is active-view-scoped.
- Room finish creation supplies all room boundary rings, preserving holes/inner boundaries.
- Door assembly names are allocated from one cached name set instead of re-collecting the full
  document per door.
- Added a registered Revit-host `KARCH_ROOM3D_ROLLBACK` fixture using the production service.
  It requires a writable disposable QA project and a placed Room selected or visible in the
  active view; it verifies the generated view and harness-owned rollback.

## Verification

| Gate | Result |
| --- | --- |
| K-Architectural production acceptance | PASS, 39 source/host-fixture assertions |
| K-Architectural domain QA | PASS, 14 geometry and invalid-input assertions |
| Revit 2025 / net8 build | PASS, 0 errors, 208 CS0618 warnings |
| Revit 2024 / net48 build | PASS, 0 errors, 210 CS0618 warnings |
| K-Architectural/Rebar/UI/QS + preserved Stage 4/Test-All regression | PASS, `Test-All.ps1` 1166/1166 |
| Rebar-specific QA | PASS: configuration 36, input 60, references 40, column safety 20, layout 9235 controls/252 renders |
| UI acceptance | PASS: command/ribbon contract 92 commands +17 bindings; 100 icons, 10 XAML surfaces, 43 renders |
| Revit host runtime | DEFERRED; fixture registered, not executed |

### Host scenario matrix

| Scenario | Fixture/project prerequisites | Automated host assertions | Status |
| --- | --- | --- | --- |
| Room 3D create + rollback | Writable disposable local model; valid placed Room selected/visible; 3D view family type | Production service, active section box, exactly one expected view added, outer transaction-group rollback restores fingerprint | Ready; not executed |
| Quick Archi wall/room creation | Writable model; straight model curves, wall type, level, enclosed plan region | Wall IDs and types resolve; invalid curve isolation; room IDs resolve; final fingerprint restored | Manual/runtime recipe documented; fixture extension deferred |
| Room finish boundaries | Writable model; enclosed Room with outer and inner rings; suitable floor type | Floor type/level resolve; floor generated for boundary profile; no extra floor outside scope; rollback restores fingerprint | Manual/runtime recipe documented; fixture extension deferred |
| Lintel creation | Writable model; wall-hosted door/window, suitable structural framing type | Correct host-relative endpoints/level/type/marker; repeated run skip policy; invalid opening isolated; rollback restores fingerprint | Manual/runtime recipe documented; fixture extension deferred |
| Door assembly/views | Writable model; unassembled valid door and assembly detail-view support | Membership, deterministic unique type name, requested views resolve, rollback restores fingerprint | Manual/runtime recipe documented; fixture extension deferred |

The code-side acceptance and domain QA are PASS. Host runtime remains DEFERRED; the registered
fixture covers Room 3D creation/rollback, while the remaining workflows require purpose-built
host fixture resources before they can be automated. No host PASS is claimed.

## Warning and risk disposition

Both supported builds complete with zero errors. All current warnings are CS0618 obsolete
`ElementId.IntegerValue` API warnings; no warning was emitted from the modified Architectural
paths. They are Category B maintenance debt, not evidence of a known runtime/corruption risk.
No Category C Architectural warnings or known model-corruption risks were identified in the
changed workflows.

`CATEGORY_C_ARCHITECTURAL_WARNINGS = NONE`
`KNOWN_ARCHITECTURAL_MODEL_CORRUPTION_RISK = NONE_IDENTIFIED`
`REVIT_HOST_RUNTIME = DEFERRED`

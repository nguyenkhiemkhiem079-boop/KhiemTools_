# Phase 6 — K-MEP Inventory, Capability Crosswalk, and QA Evidence

Branch: `phase6/k-mep`, based on final Phase 5 HEAD. No push or merge.

## Inventory and disposition

Two unique production K-MEP commands are exposed through the ribbon and workspace: MEP
Openings (now accurately named read-only clash review in its tooltip/report) and MEP Elevation
Notes (retaining the command class identity while removing the former placeholder behavior).
No MEP-specific forms, ViewModels, connector/routing services, or legacy duplicate backends were
found. The workspace links these same two commands; there are no duplicate command classes.

| Capability | Current disposition | Scope / constraints |
| --- | --- | --- |
| MEP Openings | PRODUCTION — read-only exact-solid analysis | Ducts, pipes, and cable trays against structural framing, floors, and walls in the active view; selected curves take precedence; no opening creation/cutting |
| MEP Elevation Tags | PRODUCTION — view-scoped elevation notes | Creates idempotent Revit TextNotes in plan/section/elevation views; selected curves take precedence; content reports midpoint BOP/invert, top, and section size |
| MEP measurement adapter | PRODUCTION | Reads supported built-in diameter/width/height parameters, converts Revit internal feet to mm at adapter boundary |
| MEP domain | PRODUCTION | Pure mm arithmetic for recommended opening clearance and vertical range; no Revit references |
| MEP connectors, systems, routing, fittings | NOT USED / DEFERRED | Existing exposed tools do not connect, disconnect, size, or route systems; no speculative capability added |
| Duct fittings/accessories/equipment/conduit | DEFERRED | Not part of the two existing commands' evidenced category scope |
| PHM MEP crosswalk | UNAVAILABLE | No unpacked PHM MEP reference assets found in this checkout; no external behavior is claimed |

## Production behavior and safety

- Clash analysis uses active-view structural host collection, category-validated MEP curves,
  cached host geometry, bounding-box broad phase, Boolean solid intersection for confirmation,
  and curve/solid intersection to locate the report point. It opens no transaction and mutates
  no model elements.
- Recommendations use outside diameter for round pipe, width/height for ducts and cable trays,
  and 50 mm clearance on each side. They are coordination estimates; actual openings are not
  automatically cut because there is no validated host-specific opening family/void policy.
- Elevation notes validate plan/section/elevation view scope and supported MEP size inputs;
  height/BOP/top calculations use a Revit-free domain service. Notes are created in per-item
  subtransactions, checked for text/view ownership before commit, and repeat runs skip identical
  notes already in that view.
- Batch failures are counted; item failures are isolated; all-failure batches roll back. Shared
  structured diagnostics include a stable document key, requested/eligible/processed/changed/
  skipped/failed counts, transaction disposition, postcondition state, and duration.
- No connectors are connected/disconnected, no systems rerouted, and no temporary model
  geometry is used.

## Verification

| Gate | Result |
| --- | --- |
| K-MEP production acceptance | PASS, 30 acceptance assertions |
| K-MEP domain QA | PASS, 12 arithmetic and invalid-input assertions |
| Revit 2025 / net8.0-windows build | PASS, 0 errors, 208 CS0618 warnings |
| Revit 2024 / net48 build | PASS, 0 errors, 210 CS0618 warnings |
| Full `Test-All.ps1` regression | PASS, 1208/1208 checks |
| UI command contract | PASS, 92 command entry points and 17 workspace bindings |
| UI layout | PASS, 100 icons, 10 XAML surfaces, 43 renders |
| Domain target builds | PASS, netstandard2.0 and net8.0, 0 warnings/errors |
| Revit host runtime | DEFERRED; registered fixture not executed |

### Host scenario matrix

| Scenario | Prerequisites | Assertions | State |
| --- | --- | --- | --- |
| Read-only solid clash review | Writable disposable model; supported duct/pipe/tray and at least one real solid intersection with visible framing/floor/wall | Analysis preserves document fingerprint; exact clash has positive recommended sizes; no opening/model mutation | Registered `KMEP_ANALYSIS_AND_NOTES`; not executed |
| Elevation-note create + rollback | Same plan/section/elevation view; supported MEP curves; TextNoteType | Notes resolve, belong to active view, contain calculated BOP/invert/TOP/size; only expected note IDs appear; outer harness rollback restores fingerprint | Registered `KMEP_ANALYSIS_AND_NOTES`; not executed |

`REVIT_HOST_RUNTIME = DEFERRED`; source/domain/build gates are not represented as host execution.

## Priorities and warning disposition

P0/P1 behavior addressed: the old fake BBox-only “clash” count is now confirmed against actual
solids, and the old “Ready to auto-tag” placeholder now creates checked annotations. P2 items
include dedicated graphics/preview, user-configurable clearance, section-by-section sloped
elevation labels, and a dedicated MEP runtime fixture family/model. P3/speculative connector
routing, conduit, and accessory automation remain deferred.

Both supported builds remain zero-error. Current compiler warnings are the existing
`ElementId.IntegerValue` CS0618 maintenance warnings; none originate in the changed MEP paths.
No Category C MEP warning or known model-corruption risk was identified in the changed code.

`CATEGORY_C_MEP_WARNINGS = NONE`
`KNOWN_MEP_MODEL_CORRUPTION_RISK = NONE_IDENTIFIED`
`REVIT_HOST_RUNTIME = DEFERRED`

# K-REBAR QA Matrix — Phase 8 audit

The matrix separates source/domain checks from Revit host execution. A static or
domain pass is not evidence that reinforcement solved correctly in a model.

| Domain | Static/domain evidence | Runtime fixture | Current status / limitation |
|---|---|---|---|
| Rectangular column | `Verify-RectangularColumnRebarFix.ps1` (20 checks); configuration/input/layout checks | `RC-STATION`, `RC-FULL` | Static PASS; host execution deferred |
| Circular column | Generator, geometry helper and form present | `CC` | Static presence only; host execution deferred |
| Beam | Generator, local geometry helper, form validation present | `BR` | Static presence only; rotated-host and actual shape results need host execution |
| Slab | Generator/profile analysis and settings present | `SR` | Static presence only; openings/directions need host execution |
| Wall | No generator/command found | None | NOT IMPLEMENTED; explicitly unsupported |
| Foundation | Generator/profile analysis/settings present | `FR` | Static presence only; supported geometry needs host execution |
| 2D preview | Form canvases and offline UI render checks | No solver-backed preview fixture | PARTIAL; schematic form previews are not certified layouts |
| 3D preview | Persistent column inspection-view generator exists after generation | None before-commit | BLOCKED; this is not a non-destructive layout preview |
| Transactions | Safe shape helper and command fixture rollback checks exist | Rebar fixture scenarios | PARTIAL; not all major generators have common host-level rollback/postcondition coverage |
| Postconditions | Runtime helper checks generated elements/host/report | `RC`, `RC-STATION`, `RC-FULL`, `CC`, `BR`, `SR`, `FR` | Static harness ready; runtime not executed; production entry points differ in checks |
| Diagnostics | `RebarGenerationReport` and failure preprocessor exist | Runtime fixtures record failures | PARTIAL; unified duration/transaction/host result record is absent |
| Revit compatibility | Revit 2024 Release and Revit 2025 Release target builds | Manual/runtime host needed | Both builds PASS, 0 errors; runtime deferred |

## Current QA evidence

- `Test-All.ps1`: PASS, 1271/1271.
- `Verify-RebarConfiguration.ps1`: PASS, 36 checks.
- `Verify-RebarInputState.ps1`: PASS, 60 checks.
- `Verify-RebarLayout.ps1`: PASS, 9,235 layout control checks and 252 offline renders;
  not live Revit DPI or geometry validation.
- `Verify-RebarReferences.ps1`: PASS, 40 schematic reference renders; not conformance
  or generated-bar validation.
- `Verify-RectangularColumnRebarFix.ps1`: PASS, 20 checks.
- Revit 2024 / net48 Release build: PASS, 0 warnings, 0 errors (incremental).
- Revit 2025 / net8.0-windows Release build: PASS, 0 warnings, 0 errors (incremental).
- Registered host fixtures exist for supported domains but were not run against a
  live Revit session/model. A runtime result is not marked PASS.

## Gate disposition at audit time

`KREBAR_DOMAIN_QA = PASS` for the existing offline/domain regression evidence.
`KREBAR_STATIC_ACCEPTANCE = PARTIAL` pending common plan/preview/postcondition coverage.
`KREBAR_TRANSACTION_ACCEPTANCE = PARTIAL` pending consistent per-workflow outcome
semantics and host rollback scenarios.
`KREBAR_POSTCONDITION_ACCEPTANCE = PARTIAL` pending production entry-point coverage.
`KREBAR_PREVIEW_ACCEPTANCE = BLOCKED` because no solver-derived, non-destructive 3D
pre-commit preview exists; current 2D canvases are partly schematic.
`KREBAR_DIAGNOSTIC_ACCEPTANCE = PARTIAL` pending unified execution diagnostics.
`REVIT_HOST_RUNTIME = DEFERRED` (harness and tool-specific fixtures exist, but no
available Revit QA host/model was executed in this environment).
`KNOWN_MODEL_CORRUPTION_RISK = NOT_CERTIFIED` until preview, postconditions and host
rollback scenarios pass; absence of a detected static defect is not a certification.

Phase 8 exit criteria are therefore not met. Later phases must not start.

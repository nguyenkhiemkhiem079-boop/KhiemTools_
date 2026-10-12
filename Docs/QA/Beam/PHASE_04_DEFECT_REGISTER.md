# Phase 04 — Beam Reinforcement Defect Register

Branch: `fix/preview-hardening`

Starting SHA: `aa0fc5ddfba4f2234487c673ffb5977a167845db`

Implementation checkpoint: `19fca67cda2cdcd004bff91f042b1af0724a01e7`

Status: Phase 04B findings below are historical; the latest Phase 04C disposition is appended at EOF. Host acceptance remains `HOST_REQUIRED / NOT_EXECUTED`.

This Beam-specific register supplements, and does not replace, the cumulative Slab history in the repository-root `PHASE_DEFECT_REGISTER.md`.

## Capability matrix

| Feature | UI | Domain/generator | Preview | QA | Disposition |
|---|---|---|---|---|---|
| Host geometry | Supported profile summary; unsupported host shown as unavailable | One straight constant rectangular prism; axis-aligned and plan-rotated axes; no curved, sloped, tapered, cutback, stepped, irregular or multi-solid hosts | Same analyzed profile drives generation and snapshot | Static source checks; rectangular/rotated host fixtures registered | `PARTIAL / HOST_REQUIRED` |
| Continuous top/bottom bars | Exact loaded Rebar types and counts ≥ 2; no type substitution | Symmetric single row, cover/tie/bar-radius offsets, support anchorage; overcrowded rows and cross-row overlap rejected | Production-generator rollback capture | Pure numerical section-layout goldens; Revit fixture not run | `PARTIAL / HOST_REQUIRED`; no multiple layers |
| Additional top/bottom bars | Legacy controls visibly disabled as unavailable | Positive legacy extra quantities rejected; ambiguous legacy L/3 and 15–85% placements are not generated | Cannot display these roles as solved output | Static fail-closed assertion | `UNSUPPORTED` pending an approved position/anchorage contract |
| Side/skin bars | Reference-only controls disabled; automatic threshold removed | Positive quantity and automatic requests rejected until layer spacing, anchorage and preview are defined | Not part of accepted plan | Static fail-closed assertion | `UNSUPPORTED` pending validated detailing inputs |
| Stirrups | Uniform or A1/A2 end/middle spacing; hanger quantity/spacing | Deterministic A1/A2/A1 stations; boundary deduplication and bar-diameter separation; exact Rebar shape creation required | Real solver centerlines and role tags in detached preview | Seven deterministic station assertions; host fixtures registered | `IMPLEMENTED_CODE_SIDE / HOST_REQUIRED` |
| Supports | No arbitrary support selection fallback | Actual structural column/beam solid intersections only; ambiguous/missing support and missing assigned cover abort; walls, slabs, cantilevers and unconnected ends unsupported | Nearby support geometry/location/cover participates in stale fingerprint | `BEAM_SUPPORT_STALE` registered, not executed | `PARTIAL / HOST_REQUIRED` |
| Anchorage/hooks | Multipliers disclosed as not code-checked; no user claim of code compliance | Uses available support embedment and assigned cover; creates curve returns when straight embedment is insufficient; no loaded `RebarHookType` selection; `Auto` material grades use the configured multiplier fallback | Engineering-scope disclosure is carried into the snapshot | Calculator goldens are domain-only; geometry/hook result requires Revit | `PARTIAL / HOST_REQUIRED`; no structural-code acceptance |
| Cover/spacing | Assigned Revit cover is required unless an explicit API-level custom cover is supplied | Uses the maximum positive assigned face/common cover for the Beam and support as a conservative uniform inset; longitudinal bar overlap and stirrup-station overlap are rejected | Input fingerprint includes explicit values and cover/support context | Numerical layout goldens; no live cover placement run | `PARTIAL`; face-specific cover placement remains unsupported |
| 2D preview | Existing elevation is visibly labeled illustrative, not solved | No static sketch data is passed to generation | Only the modal solver snapshot claims solved geometry | Static disclosure assertion; offline layout rendering | `IMPLEMENTED_AS_DISCLOSED` |
| 3D solver preview | Accept/Cancel modal and role filter | Rollback-only production generator capture | Detached centerline snapshots from actual generated Rebar sets | Runtime fixture registered, not executed | `IMPLEMENTED_CODE_SIDE / HOST_REQUIRED` |
| Preview/Create parity | Create guarded by a current accepted preview | Regenerates inside per-Beam transactions nested in one atomic transaction group; mismatch throws | Complete Rebar multiset parity includes host/type/shape/hooks/layout/positions/centerlines | Static parity assertion and registered host fixtures | `IMPLEMENTED_CODE_SIDE / HOST_REQUIRED` |
| Stale preview | Input changes disable Create and show lifecycle state | Fingerprint covers Beam dimensions/orientation, inputs, nearby support bounds/location/solid geometry, assigned face covers, and cover-type distances | Recomputed before Create | Static check; unsaved support move/resize host case registered, not run | `IMPLEMENTED_CODE_SIDE / HOST_REQUIRED` |
| Duplicate protection | Duplicate is surfaced before commit | Complete plan multiset and same-host selection | Preview fingerprint is the duplicate expectation | Shared multiset goldens and host scenario | `IMPLEMENTED_CODE_SIDE / HOST_REQUIRED` |
| Transactions/cancel | Preview Cancel leaves no model edits | Create uses checked transaction/group boundary; generator errors and parity mismatch abort the enclosing group | Capture rolls back temporary Rebar | Registered rollback/cancel fixture, not run live | `IMPLEMENTED_CODE_SIDE / HOST_REQUIRED` |
| UI and language | Minimum supported form; Beam labels remain mixed Vietnamese/English | Paint reads schematic/cached data only | Preview is separate from illustrative elevation | Beam-only and canonical offline layout suites | `PARTIAL`; no Beam language selector; live Revit UI not run |

## Phase 04B contract audit

Repository search of `Docs/`, `Tools/`, settings schemas, generator inputs, and Rebar helpers found no approved Beam detailing schedule, construction-detail standard, or configuration contract that specifies cut-off stations, secondary layer elevations, skin-bar distribution, hook identity/orientation, or face-to-centerline placement. Existing form defaults and legacy field labels are not an approved engineering source. The current code-side implementation therefore supports only the explicit continuous top/bottom rows and supported stirrup/hanger inputs in the capability matrix; it does not claim structural design-code verification.

| Feature requested by Phase 04B | Safe disposition | Required approval/input before implementation |
|---|---|---|
| Additional top-left, top-right, bottom-mid bars | `REQUIRES_ENGINEERING_APPROVAL`; positive quantities are rejected before generation. Legacy private routines contain fixed fractions and are unreachable behind that guard; they are not an accepted contract. | Actual user-entered local beam start/end stations, local layer coordinate/index, exact Rebar type, end detail/anchorage and support boundary semantics; explicit statement that this is user-defined detailing, not code verification. |
| Multiple longitudinal layers | `REQUIRES_ENGINEERING_APPROVAL`; only one supported top row and one bottom row are generated. | Layer-coordinate convention and clear-spacing requirements, including interaction with stirrups and other bars. |
| Side/skin bars | `REQUIRES_ENGINEERING_APPROVAL`; positive quantities and automatic requests fail closed; no depth-triggered automatic rule is enabled. | Explicit per-bar local coordinates or a verified spacing rule, exact bar type, clear spacing, face cover, end detail and role semantics. |
| RebarHookType | `REQUIRES_ENGINEERING_APPROVAL`; Beam longitudinal returns are geometric curves, not a loaded hook identity. | Exact loaded hook selection, supported shape/type compatibility, orientation convention, geometric clearance and parity contract. No generic hook-helper fallback is accepted as Beam behavior. |
| Anchorage | Geometry preview only; configured grade/multiplier calculations and return curves are not represented as code-verified design. | Approved supported code edition/material inputs or explicit manual embedment/return geometry, with support boundary and acceptance criteria. |
| Face-specific cover | Current generator uses a uniform maximum positive assigned cover, not per-face placement. | Verified mapping between Revit host faces and Beam local axes for Beam and support solids, plus face-by-face centerline and bend-clearance checks. Missing assignments must continue to fail closed. |
| Expanded supports | Structural column and structural beam solid intersections remain the only supported support candidates. | Per-category geometry/boundary semantics and explicit treatment of walls, slabs, cantilevers and free ends. |
| Localization/UI | Legacy extra/skin controls are disabled/disclosed; the working configuration UI still has mixed English/Vietnamese labels. | Approved bilingual terminology and interaction copy; no detailing controls should become editable before their full input contract is consumed by preview and create. |

Persisted `BeamRebarSettings` has legacy positive defaults for additional bars and automatic/quantity side bars. It is not consumed by the current Beam form/generator path; the active `CreateGenerationInput` explicitly supplies zero extras and disables automatic/quantity side bars. Do not interpret those dormant persisted defaults as supported generation inputs. Any future template wiring must include explicit legacy migration/disclosure and validation before consumption.

## Consolidated audit disposition

- No P0 production corruption defect was confirmed in the supported Beam path during this source audit. Previously implemented rollback capture, complete-plan parity, duplicate checks, staleness and checked transaction boundaries remain the code-side safety baseline; no live host outcome is inferred.
- P1 engineering-contract blockers remain: additional bars, multiple layers, skin bars, explicit hook identity, code/anchorage claims, face-specific cover, expanded supports, and live host execution.
- P2 remains: incomplete Beam localization and illustrative-only static elevation.
- No production code was changed in this audit because no missing engineering decision could be safely inferred from repository evidence. Existing unsupported requests remain fail-closed.
- Overall `PHASE CODE COMPLETE = NO`, `PHASE ACCEPTANCE COMPLETE = NO`, and `PRODUCTION READY = NO` until the listed detailing decisions and required host acceptance are resolved.

### Phase 04B validation (2026-10-12)

- `Verify-KRebar.ps1`: 148/148 static assertions PASS, including the extra-detail fail-closed guard.
- `Verify-RebarConfiguration.ps1`: 37/37 PASS; persistence/UI bindings only.
- Golden Regression: 96 assertions PASS; Rebar host geometry remains `HOST_REQUIRED / NOT_EXECUTED`.
- Beam/Rebar offline layout through `Test-All.ps1`: 17,443 control checks / 348 renders PASS; WPF UI layout: 100 icons / 10 surfaces / 43 renders PASS.
- Runtime QA harness structural audit: 50/50 PASS; these are source/structure checks only.
- Canonical `Test-All.ps1`: 2,767/2,767 PASS.
- Revit 2024 Release: 0 errors, 210 warnings. Revit 2025 Release: 0 errors, 208 warnings.
- `git diff --check`: PASS before checkpointing.
- Live Revit QA: `NOT_EXECUTED`; no confirmed disposable Revit model was available for mutation tests.

## P0 — Critical findings

### Resolved in implementation checkpoint `19fca67`

- Arbitrary or first-available `RebarBarType` substitution was removed. Each active role must resolve to an exact loaded type in the active document.
- Required Rebar creation/shape failure can no longer silently omit a bar or replace a bent bar with a straight bar. Missing results abort generation.
- Bounding-box support guesses, fabricated support dimensions, invented free-end extensions, and the global 25 mm production fallback were removed from Beam generation. Only actual column/beam solid intersections and assigned cover are accepted.
- Beam geometry analysis now proves a single six-planar-face rectangular prism with eight corners and matching physical/location-line end extents; unsupported host geometry fails closed.
- Longitudinal rows now reject impossible envelope and overlapping bar centerlines before Revit creation. Tests exercise symmetric coordinates, valid separation, crowded rows, and overlapping layers.
- A deterministic station planner now rejects invalid spacing and stirrup overlap, merges duplicate zone/hanger stations, and imposes a station-count ceiling.
- Production-created Beam/Rebar centerline paths are checked against the physical Beam/support solid union before returning success.
- Support fingerprints no longer rely on `VersionGuid` alone; nearby candidate locations, bounds, solid geometry, face-cover assignments and cover-type distances are included.

No live model-corruption outcome was observed because no host mutation fixture was executed. Host safety remains unverified, not proven by static checks.

## P1 — Acceptance defects / blockers

- `BEAM-EXTRA-DETAIL`: additional top/bottom quantities are rejected and legacy UI controls are disabled. The current form provides ambiguous ratio/point/type fields but no validated mapping to cut-off station, layer elevation, end anchorage and required development. Implement only after that input contract is specified; do not restore the legacy fixed L/3 or 15–85% guesses.
- `BEAM-SKIN-DETAIL`: side-bar and automatic side-bar requests are rejected. Valid vertical spacing, end support anchorage and exact preview roles are not exposed as production inputs.
- `BEAM-SUPPORT-SCOPE`: supported anchorage detection is limited to structural columns and structural beams. Structural walls/slabs, cantilevers and unconnected ends deliberately fail closed.
- `BEAM-HOOK-IDENTITY`: end return geometry is represented by generated curve segments, not an explicitly loaded/selected `RebarHookType`. Anchorage multipliers and automatic-grade fallback are not code-verified and must not be marketed as a design-code check.
- `BEAM-FACE-COVER`: the solver uses the maximum positive assigned face/common cover as a uniform Beam/support inset. It does not implement face-specific clear-cover offsets or prove minimum cover along every hooked/support centerline segment.
- `BEAM-LIVE-HOST`: rectangular, rotated, create/Undo, cancel, duplicate, stale-support, geometry-rejection, parity and rollback scenarios are registered but not run against a confirmed detached disposable model.

These items keep `PHASE CODE COMPLETE = NO` and `PHASE ACCEPTANCE COMPLETE = NO` until resolved/accepted by a Beam detailing owner and host QA is performed.

## P2 — Non-blocking

- Beam UI labels are mixed English/Vietnamese; the Beam form has no language selector or full dynamic localization.
- The long elevation remains an illustrative schematic; it is explicitly not a solved preview. Users must open the separate solver-backed preview to review generated geometry.
- Exact section rotations, unsupported family profile categories and actual Revit display/Undo behavior still require host observation.

## Validation evidence at checkpoint `19fca67`

- `Verify-KRebar.ps1`: 147/147 static assertions PASS.
- `Verify-RebarConfiguration.ps1`: 37/37 persistence/UI-binding checks PASS; no Revit geometry executed.
- Golden Regression: 96 assertions PASS, including seven Beam stirrup station checks and four Beam longitudinal section-layout checks.
- Beam offline layout matrix: 3,114 visible-control bounds checks and 54 renders PASS; minimum, 1366×768, 1920×1080, simulated 100%, 125%, 150%.
- Canonical `Test-All.ps1`: 2,766/2,766 configured weighted audits PASS (296.44 seconds). The full configured Rebar layout suite passed; a fresh Beam-only rerun passed 3,114 bounds checks and 54 renders.
- Revit 2024 Release (`net48`): PASS, 0 errors, 210 warnings.
- Revit 2025 Release (`net8.0-windows`): PASS, 0 errors, 208 warnings.
- Live Revit host QA: `NOT_EXECUTED`; no confirmed disposable detached QA file is available. Static registration, compilation and offline rendering do not constitute a live pass.
- `git diff --check`: PASS before checkpoint commit.

## Cumulative disposition

The Beam section is added to the repository-root `PHASE_DEFECT_REGISTER.md`; the prior Slab findings remain intact. Beam-specific continuation and exact resume instructions are in `PHASE_04_HANDOFF.md` in this directory.

## Phase 04C — Manual Detailing v1 (2026-10-12)

Starting SHA: `843d99218e5f85ef2a97ab3b0d56e59a911f4c13`. Pushed implementation checkpoints: `1d3c01ed3f908a6941bac92155368c3fe50c1bb5`, `9ca81a943be9f482ca6f5f7a84804c62825a658a`, and `530b875ef7037edabe33aa0c8715e54feefea781`.

### Consolidated severity disposition

- **P0 resolved:** Manual v1 does not consume legacy `BeamRebarInput` or infer design values. The plan requires explicit absolute start/end stations, explicit local section positions, exact loaded `RebarBarType` identity, explicit group enabled state and explicit concrete-cover face values. The generator checks proposed segments against all pre-existing hosted Rebar using detached centerline geometry and exact model diameters; existing hosted Rebar state is part of the stale-input fingerprint. Unknown existing type/path state fails closed. `BeamManualSegmentDistanceV1` has deterministic crossing, parallel-clearance and separated-collinear regression cases.
- **P1 acceptance pending:** Live Revit execution is still required for Manual v1 load/save, rollback-only solver preview, actual role/type/host/centerline parity, stale-input behavior, full-vs-partial duplicate selectivity, existing-bar overlap rejection, Undo, and model-integrity restoration. `BR-MANUAL-V1-ROLLBACK` is registered and compiled, not executed.
- **P1 explicitly unsupported, not silently approximated:** Enabled stirrup groups; hooks and curve returns; verified support extensions; multi-host manual plans; unverified/non-rectangular Beam geometry; automatic quantities, code-derived station placement, code-derived cover/anchorage, and any unprovided structural-code compliance rules. Requests outside the implemented straight longitudinal slice fail closed. These capabilities require additional approved detailing inputs and are not claimed as Manual v1 support.
- **P2:** The input schematic is a detached, disclosed input drawing; only accepted Revit solver capture may be described as solved geometry. Existing Beam and Slab workflows remain separately labeled/preserved. Manual v1 is available for one selected supported Beam at a time.

### Phase 04C evidence

- Golden Regression: 117 assertions PASS, including finite-segment interference cases.
- K-Rebar static acceptance: 156/156 PASS.
- Rebar layout: 17,479 offline control checks / 348 renders PASS; WPF UI layout: 100 icons / 10 surfaces / 43 renders PASS. These are source/render tests, not host UI tests.
- Runtime QA structural audit: 50/50 PASS; fixture is registered/compiled only.
- Canonical `Test-All.ps1`: 2,796/2,796 PASS.
- Revit 2024 Release (`net48`): 0 errors; Revit 2025 Release (`net8.0-windows`): 0 errors. Existing repository warnings remain.
- Live Revit QA: `NOT_EXECUTED`; no confirmed detached disposable Beam model was used.
- No supported-path P0 remains known after the additive existing-bar collision/staleness fix. Code-side completion applies only to the explicitly supported straight longitudinal Manual v1 slice. Overall Beam acceptance and production readiness remain **NO** until host acceptance is recorded. Unsupported stirrups, hooks/returns, verified supports and design-code checks are not claimed.

## Phase 04C consolidated implementation/audit reconciliation (2026-10-12)

This is the latest Phase 04C disposition and supersedes the earlier checkpoint statements above about hooks, returns, stirrups and the supported code-side boundary.

- **P0:** None confirmed in the narrowed supported code-side scope. Exact user input and exact loaded Revit identity are required; unsupported support extension and multi-Beam plan assignment remain rejected.
- **P1 code-side defects resolved:** exact loaded hook identity/orientation and explicit curve-return polylines flow UI → typed plan → persistence/fingerprint → production generator → rollback solver preview → parity-guarded Create. Explicit stirrup/hanger role, exact type/shape, section path, stations and collision/cover checks use the same path. Self-intersecting and zero-area closed contours are rejected. The Manual form layout has an offline matrix fix.
- **P1 host acceptance remains open:** `BR-MANUAL-V1-ROLLBACK` exercises straight-bar host behavior only. `BEAM_MANUAL_V1_EXTENDED_DETAILS` records host verification for hooks, returns and closed/open stirrups/hangers. Both remain `REGISTERED_NOT_EXECUTED`; shape compatibility, actual Revit bends, parity, cancel, rollback, duplicates, staleness and Undo are not host-proven.
- **P2/narrowed boundary:** verified support extension, multi-Beam plans, inferred engineering values, unsupported host profiles and structural-code checks remain visibly unavailable and fail closed. The supported editor accepts one supported rectangular Beam at a time.
- Final current evidence: Golden Regression `124` assertions PASS; K-Rebar `157/157` PASS; Rebar configuration `37/37` PASS; focused Manual layout `720` checks / `18` renders PASS; full Rebar layout `17,959` controls / `360` renders PASS; canonical `Test-All.ps1` `2804/2804` PASS; Revit 2024/2025 Release builds `0` errors (210/208 repository-wide warnings). Offline layout simulation is not OS DPI or host QA.
- `HOST_ACCEPTANCE=NO`; `PRODUCTION_READY=NO`. Build/static results are not live Revit acceptance.

## Independent continuation audit — 2026-10-12

- **P0:** none confirmed in the claimed one-supported-rectangular-Beam code-side scope after an independent editor→plan→generator→rollback-preview/Create/persistence trace.
- **P1 host-only remains:** `BR-MANUAL-V1-ROLLBACK` and `BEAM_MANUAL_V1_EXTENDED_DETAILS` are registered but unexecuted. Revit 2025/2026 processes were present, but no detached disposable Beam model was confirmed; no runtime mutation was attempted.
- **P2 / non-blocking QA accounting fixed:** `Test-All.ps1` weighted UI command-contract acceptance at 215 while its verifier emitted 218 checks. Updated the suite weight to 218; this is aggregate reporting only and does not affect production behavior.
- Fresh evidence: K-Rebar `157/157`; Rebar configuration `37/37`; Golden Regression `124`; Runtime QA structural audit `50/50`; Rebar layout `17,959` checks / `360` renders in both canonical and repeated standalone runs; corrected canonical `Test-All.ps1` `2807/2807`; Revit 2024 `0` errors / `210` warnings; Revit 2025 `0` errors / `208` warnings. `git diff --check` and remote handoff verification remain final steps.

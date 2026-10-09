# Phase 04 — Beam Reinforcement Defect Register

Branch: `fix/preview-hardening`

Starting SHA: `aa0fc5ddfba4f2234487c673ffb5977a167845db`

Implementation checkpoint: `19fca67cda2cdcd004bff91f042b1af0724a01e7`

Status: code-side audit in progress; host acceptance is `HOST_REQUIRED / NOT_EXECUTED`.

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

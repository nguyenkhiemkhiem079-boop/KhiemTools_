# Phase 04 — Beam Reinforcement Handoff

Branch: `fix/preview-hardening`

Required starting SHA: `aa0fc5ddfba4f2234487c673ffb5977a167845db`

Pushed implementation checkpoint: `19fca67cda2cdcd004bff91f042b1af0724a01e7`

Final handoff branch tip: read with `git rev-parse origin/fix/preview-hardening` after fetching; the user-facing Phase 04 report records the exact final SHA.

Master baseline: `601d1feb8731356f3ac2f509aa5aabeecbd8fd3a`.

This Beam handoff is separate from the prior Slab handoff in repository-root `PHASE_HANDOFF.md`; that existing Slab section and its register were preserved. Continue Phase 04 only. Do not start Column, Foundation, K-Architectural, K-MEP, K-QS or a later roadmap phase.

## Implemented and pushed code-side checkpoint

- Beam host analysis now supports one straight, constant rectangular concrete prism and rejects unverified profile/cutback/slope geometry.
- Top and bottom continuous bars use exact loaded Rebar types, checked symmetric section coordinates and geometric non-overlap validation.
- A1/A2/A1 and secondary-beam hanger stations are planned deterministically, merged at boundaries and rejected when centerline spacing would overlap.
- Required Rebar/shape creation failures abort. No arbitrary type substitution, fallback shape, support dimension or free-end anchorage is used.
- Support discovery for the current code-side scope is based on actual structural-column/structural-beam solid intersections. Missing/ambiguous supports and missing assigned cover block generation.
- Rollback-captured solver preview and pre-commit parity use the same production generator. Create remains behind the accepted current fingerprint, duplicate protection, checked per-Beam transactions and one atomic transaction group.
- Relevant nearby support location/bounds/solid geometry and assigned cover values participate in preview staleness; this avoids depending only on save-scoped `VersionGuid`.
- Beam host fixtures for axis-aligned and rotated rectangular hosts remain registered and compiled, not executed.

See `PHASE_04_DEFECT_REGISTER.md` for the full capability matrix, findings, severity and code-side evidence.

## Remaining Phase 04 blockers

1. Additional top/bottom bars are intentionally rejected. Define a production input contract for actual bar station start/end, layer elevation, end detail and anchorage before implementing. Do not re-enable the legacy ambiguous ratio/point fields or fixed L/3 / 15–85% locations by assumption.
2. Side/skin bars and automatic side bars are intentionally rejected. Define validated layer spacing, support anchorage, type, role and preview behavior first.
3. The support workflow currently accepts structural columns and structural beams only. Walls, slabs, cantilevers and unconnected ends fail closed.
4. Anchorage returns are curve geometry, not explicit `RebarHookType` selection. `Auto` grade length uses the visible multiplier fallback. No code compliance is claimed.
5. Beam/support face cover uses a conservative maximum uniform inset and does not verify face-specific clear cover over every bend/support segment.
6. Live host acceptance is pending. The active Revit environment did not provide a confirmed detached disposable QA copy; do not run mutation fixtures on named project models.

Because items 1–5 are core implementation boundaries, `PHASE CODE COMPLETE = NO`. Host runtime acceptance is separately `HOST_REQUIRED / NOT_EXECUTED`; it must not be represented as PASS.

## Validation performed

- `Verify-KRebar.ps1`: 147/147 static assertions PASS.
- `Verify-RebarConfiguration.ps1`: 37/37 checks PASS (settings persistence and UI binding; no Revit geometry).
- Golden Regression: 96 assertions PASS (including seven Beam station-planning and four Beam section-layout assertions).
- Beam offline layout simulation: 3,114 control checks and 54 renders PASS across minimum, 1366×768, 1920×1080 and 100%, 125%, 150% simulated scaling.
- Revit 2024 Release: PASS, 0 errors, 210 warnings.
- Revit 2025 Release: PASS, 0 errors, 208 warnings.
- Live Revit host: NOT_EXECUTED.
- Canonical `Test-All.ps1`: final run PASS, 2,766/2,766 weighted audits (296.44 seconds). The full Rebar layout suite passed; Beam-only rerun passed 3,114 checks and 54 renders.

## Resume instructions

1. `git fetch origin --prune`; switch to `fix/preview-hardening`; confirm clean status and record `git rev-parse HEAD`, `git rev-parse origin/fix/preview-hardening`, and `git rev-parse origin/master`.
2. Confirm local and remote feature tips match. The current pushed implementation checkpoint is `19fca67cda2cdcd004bff91f042b1af0724a01e7`; final documentation/final-test commits, if any, will be descendants. Master must remain at the recorded baseline.
3. Run `Test-All.ps1` and record the final weighted suite total (expected 2,766 after the additional Beam stale-context assertion), plus `git diff --check`.
4. Close P1 product-contract gaps with an approved Beam detailing specification, not inferred semantics; add domain/host assertions and update both Phase 04 docs and the cumulative root register.
5. Only after a disposable detached QA file is explicitly confirmed, run `BR-PREVIEW-RECT`, `BR-PREVIEW-ROTATED`, `BEAM_UNDO`, `BEAM_CANCEL`, `BEAM_DUPLICATE`, `BEAM_SUPPORT_STALE`, and `BEAM_GEOMETRY_REJECTION`. Record actual model fingerprints, transaction rollback and parity evidence. Until then report `HOST_REQUIRED / NOT_EXECUTED`.
6. Do not merge into master, force push, start a later phase or work on another K-TOOL module.

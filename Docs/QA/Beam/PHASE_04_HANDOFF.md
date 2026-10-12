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

## Phase 04B consolidated contract audit (2026-10-12)

The repository was searched for approved detailing schedules, construction-detail conventions, and Beam-specific input schemas. No authoritative rule was found for additional-bar curtailment stations, secondary-layer elevations, skin-bar spacing, explicit hook identity/orientation, support-end anchorage, or local-face cover placement. These are `REQUIRES_ENGINEERING_APPROVAL`; do not re-enable legacy fixed-fraction routines or claim code compliance. The supported generator remains fail-closed for positive additional/skin-bar requests.

The active Beam form supplies zero extra counts and disables automatic/quantity side bars. The `BeamRebarSettings` persistence model still has positive legacy defaults for these fields but is not consumed by the active form/generator path; any future template integration must explicitly migrate/disclose legacy values. A focused static regression assertion now verifies the extra-bar rejection precedes the legacy generation call, and `Test-All.ps1` is synchronized to the resulting K-Rebar assertion count. No production Beam source was changed because repository evidence does not resolve the required engineering decisions.

Fresh validation on 2026-10-12: K-Rebar 148/148; Rebar configuration 37/37; Golden Regression 96 assertions; Beam/Rebar offline layout 17,443 checks / 348 renders; WPF UI layout 100 icons / 10 surfaces / 43 renders; Runtime QA structural audit 50/50; `Test-All.ps1` 2,767/2,767; Revit 2024 Release 0 errors / 210 warnings; Revit 2025 Release 0 errors / 208 warnings; `git diff --check` PASS. Live Revit QA remains `NOT_EXECUTED`; static checks and compilation are not host execution.

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
3. Run focused Beam/Rebar regression, Golden Regression, full `Test-All.ps1`, Revit 2024/2025 Release builds, and `git diff --check`; use the checked-in expected counts and report fresh outputs.
4. Obtain an approved Beam detailing specification that resolves each `REQUIRES_ENGINEERING_APPROVAL` item before implementing the corresponding input model, generator geometry, UI, fingerprint, preview roles, parity and host assertions. Do not infer stations, layers, hooks, cover mapping, support conditions or code compliance from legacy labels/defaults.
5. Only after a disposable detached QA file is explicitly confirmed, run `BR-PREVIEW-RECT`, `BR-PREVIEW-ROTATED`, `BEAM_UNDO`, `BEAM_CANCEL`, `BEAM_DUPLICATE`, `BEAM_SUPPORT_STALE`, and `BEAM_GEOMETRY_REJECTION`. Record actual model fingerprints, transaction rollback and parity evidence. Until then report `HOST_REQUIRED / NOT_EXECUTED`.
6. Do not merge into master, force push, start a later phase or work on another K-TOOL module.

---

## Phase 04C — Beam Manual Detailing v1 handoff (2026-10-12)

Phase 04C started from `843d99218e5f85ef2a97ab3b0d56e59a911f4c13`. Implementation checkpoints pushed during this run: `1d3c01ed3f908a6941bac92155368c3fe50c1bb5` (typed plan and companion store), `9ca81a943be9f482ca6f5f7a84804c62825a658a` (Manual editor/preview/create/fixture), `530b875ef7037edabe33aa0c8715e54feefea781` (existing hosted Rebar collision and stale-state guard). Read `Docs/Engineering/Beam/MANUAL_DETAILING_CONTRACT_V1.md` and `Docs/QA/Beam/PHASE_04C_ACCEPTANCE_MATRIX.md` for the full software boundary. The contract mirror is `Docs/Engineering/Beam/MANUAL_DETAILING_CONTRACT_V1.md`.

### Code-side support boundary

Supported adapter slice: one analyzed straight constant rectangular Structural Framing Beam at a time; explicit per-bar local section coordinates and absolute station endpoints; explicit enabled groups; exact same-document RebarBarType identity; explicit cover-face values; straight centerlines fully contained by verified host concrete; rollback-only production solver preview, freshness gate, exact output parity, complete duplicate detection, transaction-group create, and companion plan save/load. Proposed bars are rejected if their finite centerline segments overlap hosted Rebar after accounting for exact model diameters. Host Rebar geometry/identity participates in staleness.

Do not claim Manual support for hooks, curve returns, stirrups, verified support extension, auto design quantities/stations/cover/anchorage, multi-host edits, or code compliance. Those remain explicit fail-closed requirements pending additional approved engineering inputs. Do not change the legacy Beam or Slab feature paths to make Manual output appear supported.

### Fresh validation from the final code checkpoint

- K-Rebar: 156/156 static checks PASS.
- Golden Regression: 117 assertions PASS, including finite segment intersection/clearance goldens.
- Runtime QA structural audit: 50/50 PASS; `BR-MANUAL-V1-ROLLBACK` is registered/compiled, not executed.
- Offline Rebar layout: 17,479 control checks / 348 renders PASS; WPF UI layout: 100 icons / 10 surfaces / 43 renders PASS. These are simulations/source QA only.
- Canonical `Test-All.ps1`: 2,796/2,796 PASS.
- Revit 2024 Release (`net48`): 0 errors; Revit 2025 Release (`net8.0-windows`): 0 errors.
- Live Revit host QA: `NOT_EXECUTED`. No disposable QA model was confirmed or mutated.

The final documentation/handoff commit is the next SHA after `530b875ef7037edabe33aa0c8715e54feefea781`; fetch and verify the exact branch tip and clean tree. Run the registered Manual fixture and host interaction matrix only on an explicitly confirmed detached disposable Revit model. Record exact model fingerprints, preview/create parity, rollback, existing-rebar preservation, stale invalidation, duplicate selectivity and Undo. Only after those outputs exist may host acceptance be relabeled. Do not claim production readiness or modify master.

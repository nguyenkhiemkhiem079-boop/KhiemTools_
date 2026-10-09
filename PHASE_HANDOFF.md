# Slab Phase End-to-End Handoff — 03D.2B + 03E

Branch: `fix/preview-hardening`

Required start SHA: `662f776ddbb71aaa25dc6e2964f91b22c3e9b827`.

Implementation checkpoints pushed:

- `35f09c98bedcf0eb01b7ebf48bb81e82900b08f7` — corrected the actual Slab accessory settings overflow at minimum window layouts.
- `773928e6ab03a069011c9960f7d9c1471f04ed19` — added stale-preview, host identity, complete duplicate-selectivity, opening/accessory runtime gates and deterministic multiset regression coverage.

## Scope completed in code

- 03D.2B coverage is registered in `SR-PREVIEW-CONTRACT`: distinct host identity, a stale input matrix (resolved Rebar type, spacing, top/bottom cover, layer inversion, support and spacer configuration, selected panel set and per-panel reassignment), fresh production re-solve, full preview/create parity, exact same-host duplicate detection, partial-plan rejection, cross-host selectivity and rollback/model integrity.
- 03E coverage is registered in `SR-OPENING-ACCESSORY`: supported opening slab, bottom/top meshes, support bars, spacers and automatic opening trims; production role-set, type/shape/hooks/layout/quantity/centerline parity; opening fingerprint invalidation; cancel/preview cleanup; and fixture-only injected transaction failure with whole-model rollback.
- Duplicate detection uses a complete fingerprint multiset, preserving duplicate multiplicity and host identity. Six pure Golden Regression checks cover exact, partial, similar, cross-host and unrelated-reinforcement cases.
- The real Slab minimum-layout defect was corrected. Focused Slab rendering at minimum, 1366×768 and 1920×1080 with 100%, 125% and 150% simulations passed 9,743 control checks / 126 renders. Canonical layout passed 17,407 checks / 348 renders. Both scripts state these are offline simulation, not live DPI or Revit QA.

## Validation evidence

- `Verify-KRebar.ps1`: 140/140 PASS.
- Golden Regression: 85 assertions PASS.
- `Test-All.ps1`: 100% GREEN, 2,748/2,748 audits.
- Revit 2024 Release build: PASS, 0 errors (210 warnings).
- Revit 2025 Release build: PASS, 0 errors (208 warnings).
- `git diff --check`: PASS at implementation checkpoint; final docs commit has its own check.
- Live Revit QA: `NOT_EXECUTED`. Running Revit windows showed named project files and no confirmed detached disposable QA model. No runtime fixture was run against those models.

## Consolidated defect disposition

See `PHASE_DEFECT_REGISTER.md` for the cumulative P0/P1/P2 record. P0: none. The Slab layout P1 and code-side duplicate-selectivity defect are resolved. Host-only P1 acceptance remains open: live execution of `SR-BATCH-ATOMIC`, `SR-PREVIEW-CONTRACT`, and `SR-OPENING-ACCESSORY`; stale Create UI/no-added-bars interaction; injected pre-commit parity mismatch UI handling; and opening 40d-clearance positive/negative host scenarios. All must remain `HOST_REQUIRED` / `REGISTERED_NOT_EXECUTED` until genuinely run.

## Resume instructions

1. Fetch `origin`; confirm `fix/preview-hardening` and the final SHA reported for this handoff is clean and equals `origin/fix/preview-hardening`.
2. Provide/open a detached disposable Revit QA copy with two supported ≥2000 mm structural Floors without openings, explicit resolvable RebarBarTypes, plus a supported rectangular-opening slab with adequate clearances and required shape resources. Confirm the Runtime QA safety dialog before running only the Slab fixtures (`SR-BATCH-ATOMIC`, `SR-PREVIEW-CONTRACT`, `SR-OPENING-ACCESSORY`).
3. Record fixture output and model fingerprint/rollback results. Separately execute the stale Create and deliberate parity-mismatch UI checks and the opening clearance positive/negative cases. Never use named production/project models.
4. Close the phase only after those host results are recorded. Do not start Beam, Column, Foundation, K-Arch, K-MEP, K-QS, merge master, or infer PASS from compilation/static registration.

Code-side scope is complete; phase acceptance is `HOST_QA_PENDING` until the above host validations genuinely execute.

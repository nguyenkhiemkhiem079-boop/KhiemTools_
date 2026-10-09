# Cumulative Phase Defect Register — Slab Reinforcement

Scope: Slab completion work through Micro-Wave 03D.2B and 03E. The handoff baseline was `662f776ddbb71aaa25dc6e2964f91b22c3e9b827` on `fix/preview-hardening`. Findings below are based on the consolidated source, regression, layout, and build audit for this task.

## P0 — Critical

- None found in the code-side audit.

## P1 — Host acceptance pending

- `SLAB-HOST-RUNTIME`: `SR-BATCH-ATOMIC`, `SR-PREVIEW-CONTRACT`, and `SR-OPENING-ACCESSORY` are registered and compiled, but were not executed in Autodesk Revit. The running Revit windows identified named project models rather than a confirmed detached disposable QA model. Run the fixtures only after a disposable model is prepared and the runtime safety consent is satisfied; registration, compilation, static assertions, and parity code are not live host evidence.
- `SLAB-STALE-CREATE-UI`: The executable fixture varies the production fingerprints and proves changed input requires a fresh production solve, but the UI interaction that attempts Create with a stale accepted preview and proves no bars were added remains `HOST_REQUIRED`.
- `SLAB-PARITY-MISMATCH-UI`: The runtime fixtures compare full preview/create host, type, shape, hooks, layout, quantities and centerlines, and inject transaction failure in fixture code. A host-operator/fault-harness run that deliberately reaches the production pre-commit parity-mismatch UI path and verifies its user-visible failure remains `HOST_REQUIRED`.
- `SLAB-OPENING-EDGE-ANCHORAGE`: Successful supported-opening accessory parity and rollback are covered by `SR-OPENING-ACCESSORY`; the paired host cases for a full 40d clearance opening and an opening too close to an exterior edge/another void remain `HOST_REQUIRED` until the fixture model is executed and recorded.

## P2 — Non-blocking / explicit support boundaries

- No additional code-side defects were confirmed. Rotated/non-rectangular opening details and unsupported slab geometry remain explicit fail-closed product boundaries, not silently accepted behavior; their registered rejection scenarios still require host execution for model-specific acceptance.

## Resolved in this phase

- `PH8-SLAB-UI-LAYOUT` (P1, resolved): The original layout QA recorded five Slab `TableLayoutPanel` bounds failures at minimum window profiles. Inspection of `issues.txt`, matching control-bound JSON and renders showed a real autosizing defect, not hidden controls or a harness-only failure: the localized spacer/accessory table could grow beyond the containing `GroupBox`'s available height after simulated scaling. Commit `35f09c98bedcf0eb01b7ebf48bb81e82900b08f7` added a content-aware minimum-size adjustment scoped to the affected Slab anchor group. The focused Slab matrix then passed 9,743 control checks and 126 renders over minimum, 1366×768 and 1920×1080 profiles at 100%, 125% and 150% simulated scaling. The canonical layout suite passed 17,407 control checks and 348 renders. These are offline UI simulations, not live Windows DPI or Revit QA. The ignored `artifacts/ui-qa/rebar/issues.txt` may retain the earlier baseline failures and is not current passing-run evidence.
- `SLAB-DUPLICATE-MULTISET` (P1, code-side resolved): Complete-plan duplicate matching was extracted to a pure helper used by production and Golden Regression. Six deterministic tests prove order independence, multiplicity, rejection of partial/shared sets, host selectivity, tolerance of unrelated existing reinforcement alongside a full duplicate, and rejection of empty plans. Live host verification is covered by `SR-PREVIEW-CONTRACT` and remains pending as above.

## Consolidated code-side evidence

- `Verify-KRebar.ps1`: 140/140 static assertions PASS.
- Golden Regression: 85 assertions PASS; Rebar geometry edge cases explicitly remain `HOST_REQUIRED / NOT_EXECUTED`.
- `Test-All.ps1`: 2,748/2,748 audits PASS; canonical Slab layout issue is resolved.
- Revit 2024 Release (`net48`): build succeeded, 210 warnings, 0 errors.
- Revit 2025 Release (`net8.0-windows`): build succeeded, 208 warnings, 0 errors.
- `git diff --check`: PASS for the implementation checkpoint. Final documentation changes are checked before commit.

Code-side scope is complete; Phase acceptance remains open solely for the listed host-only checks. Do not relabel any registered fixture as PASS before actual Revit execution.

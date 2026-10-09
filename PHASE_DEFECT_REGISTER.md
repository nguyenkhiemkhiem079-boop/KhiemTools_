# Phase Defect Register

Scope: Phase 8, Micro-Wave 03D.2A — slab multi-panel transaction atomicity.

This register records findings verified in this wave. It does not treat prior phase reports as evidence for uninspected work.

## P0 — Critical

- None found in the reviewed wave scope.

## P1 — Acceptance / runtime follow-up

- `PH8-RUNTIME-ATOMICITY`: The new `SR-BATCH-ATOMIC` fixture is registered and code-side verified, but live Revit execution remains `NOT_EXECUTED`. Revit processes were present; their active windows showed named project models, and no confirmed detached disposable QA model was available. Do not run the fixture against those working models. Close this item only after a host operator provides a detached disposable model with two supported structural Floors and the fixture reports its actual result.
- `PH8-SLAB-UI-LAYOUT`: The canonical `Test-All.ps1` run failed only the Rebar layout rendering suite, which reported five Slab `TableLayoutPanel` bounds overflows at minimum/wide size simulations. The failing controls are in the slab UI and were not changed in this wave; this remains an acceptance blocker and was not altered to preserve 03D.2A's bounded QA-fixture scope. Re-run `Tools/Verify-RebarLayout.ps1` after the responsible UI-layout correction.

## P2 — Non-blocking

- None found in the focused final review.

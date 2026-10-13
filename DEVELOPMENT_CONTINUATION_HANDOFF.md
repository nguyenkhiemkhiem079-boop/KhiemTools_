# PR #3 development continuation checkpoint

Branch `fix/preview-hardening`; resumed `171bdf8f2f31e9de16dcda6772fc01e7fe0ec1f1`. Do not restart recovery or overwrite retained history. This is PARTIAL delivery of the new development mission, not completion of all requested features.

## Implemented

- Independent Manual multi-Beam assignment: each host has separately reviewed/saved explicit geometry and accepted solver preview. No shared plan copying/scaling. All-host identity, stale/duplicate preflight; checked per-host transactions inside one group, per-host parity, final all-host geometry/output recheck before assimilation. Cancel/decline never creates a partial batch.
- 11 production-linked batch/transaction-boundary orchestration tests using clearly synthetic API doubles; six pure identity goldens. These do not establish Revit geometry, rollback or Undo acceptance.
- Legacy preview is marked stale after returning from Manual creation.
- Real MSI harness requires exact explicitly confirmed disposable-machine name before COM/installer work; mandatory static QA cannot be skipped and failure stops mutation. Canonical suite totals come from actual per-suite evidence, not hard-coded 76. Two rejected launches plus source gate-order validation execute safely.
- Signing readiness is read-only: candidate Authenticode/timestamp/hashes, optional exact certificate identity/EKU/private-key availability. Missing signatures return BLOCKED/exit2. Nothing is signed or installed.
- Disposable host/installer test plan: `Docs/QA/DISPOSABLE_HOST_ACCEPTANCE_PLAN.md`.

## Still incomplete — resume in this order

1. Beam support extension: implement exact same-document support selection for approved Column/StructuralBeam modes, independently verified concrete solids and explicit user face cover/continuation intent. Resolve actual support pose, connected Beam end and physical-radius/bend containment across the joint, not bbox-only or centerline-only checks. Fingerprint support geometry/orientation/material/type/cover state; save/load exact identities; no inferred lengths or defaults. Existing validator/generator/UI still deliberately reject extension. Do not simply remove those guards. Add support-valid/missing/wrong-end/ambiguous/moved/rotated/hook-fit/clearance and solved parity goldens/host fixtures, then build/regression/commit/push.
2. Wall reinforcement: define explicit user-entered type/positions/stations/directions/covers, exact structural Wall host and physically verified local frame. Implement supported geometry with fail-closed opening/join/curved/slanted boundaries, canonical generator, detached solved preview, stale/duplicates and checked atomic Create. No Wall command/generator exists yet. Do not reuse a Beam profile merely by renaming it.
3. MEP clash openings: keep existing read-only review. Add reviewed explicit clearance/cut geometry, exact clash/host identity and orientation, supported opening API/family behavior, stale/duplicate guards, verified actual host cut and whole rollback. Existing analysis recommendations (50mm legacy review clearance) are NOT approved creation values and must not silently become defaults. Linked hosts require explicit separate handling, never cut another document implicitly.
4. Full 2D/3D parity: Manual 2D currently says INPUT SCHEMATIC, not solved. Audit true cuts and physical solver positions for all supported Beam/Column/Slab/Wall outputs; retain that distinction until actual solved 2D views are wired/tested. No actual host parity is certified.
5. Execute registered/extended QA only after user confirms exact disposable detached Revit model. Revit2025/2026 processes were observed; no model confirmation received. No model mutation was performed.
6. Real install/upgrade/repair/uninstall and trusted signing need confirmed disposable Windows VM and authorized certificate/publisher identity. Not available/confirmed; not executed. Do not use developer/production machine.

## Restart commands / evidence

Fetch origin; verify current feature tip equals final delivered SHA/PR head and tree is clean. Preserve any divergence, no reset/force push. Read authoritative Beam contract and root reports. Candidate artifacts are ignored local files; rebuild `Installer/Build-Installer.ps1 -Configuration Release` if absent/stale before `Test-All.ps1`. Do not run `Test-RealMsiRuntime.ps1` without confirmed disposable VM. `Verify-SigningReadiness.ps1` exit2 is a truthful release blocker, not a source-regression PASS.

Wave checkpoints are in COMMIT_PUSH_LOG.md and PR #3 comments; the final commit cannot self-embed its own hash. Final response/remote branch tip supplies exact next SHA. Keep PR draft, master untouched, no production release. Overall `FULL_CONTRACT_COMPLETE=NO`, `HOST_ACCEPTANCE=NOT_EXECUTED`, `PRODUCTION_READY=NO`.

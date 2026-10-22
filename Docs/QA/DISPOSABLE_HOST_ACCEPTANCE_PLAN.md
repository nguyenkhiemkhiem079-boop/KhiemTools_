# Disposable host and installer acceptance — PR #3 continuation

STATUS = NOT_EXECUTED. A running Revit process is not permission to mutate its model. Revit2025/2026 were observed; neither model has been confirmed disposable. User confirmation is requested. No model has been changed in this continuation.

## Current user safety gate — 2026-10-23

**Do not run or trigger any Revit test while KHIM (JAYK) is working in Revit.** This applies to read-only smoke commands as well as model-changing fixtures. Wait until the user is not working, then require the detached disposable-model entry gate below before any host acceptance that reads or changes a model. Several `.rvt` project files exist in Downloads, but none is designated as a QA fixture; their presence does not authorize opening or copying them.

The Beam multi-host batch, dual-host Wall A/B, and bounded native MEP opening paths have code-side implementation records in `Docs/QA/Beam/PHASE_04C_ACCEPTANCE_MATRIX.md`, `Docs/QA/Wall/DUAL_HOST_CONTRACTS_WAVE.md`, and `Docs/QA/MEP/WAVE3_HANDOFF.md` / `CONFIRMED_OPENING_WAVE.md`. Older statements below that call multi-Beam, Wall reinforcement, or MEP creation unavailable are historical and are superseded only for those documented supported slices. Their live Revit acceptance is still `NOT_EXECUTED`.

## Entry gate

User must explicitly identify a detached disposable QA project path/name, Revit year and authorization to create/rollback/Undo. Record model identity, backup, worksharing/detachment state, active document and initial element identities/observable state fingerprint. Reject production/project models and uncertainty. Load only the candidate for the intended host year after closing any DLL-locking host; don't overwrite a running host installation. Runtime harness safety guard must approve before fixtures. Preserve logs and never report BLOCKED as PASS.

## Beam Manual independent multi-host assignment

Use two physically verified straight rectangular Beams, including unequal dimensions/direction. Enter separately supplied QA-labeled synthetic plans using exact loaded types; do not copy/scale another host's coordinates. On each host accept rollback-only solved preview. Cancel second editor: no bars on either Beam. Decline final batch confirmation: no bars. Create both: exact host/type/shape/hooks/physical quantity/centerlines equal accepted components. Undo: both disappear as one grouped operation; unrelated pre-existing reinforcement survives.

Before batch creation change host A and separately host B geometry/type/existing Rebar: reject stale acceptance before any mutation. Attempt repeated host identity, foreign-document plan, empty plan and missing preview: blocked. Complete duplicate on either host blocks all; partial duplicate is not classified as complete but physical interference still blocks. Trigger a real unsupported shape/API failure in second host, using QA-only test setup, then verify identities/state/host IDs and temporary elements prove whole group rollback including host A. Never add failure injection to production generator. Until executed, transaction-group source inspection is code-side evidence only.

## Remaining feature acceptance

Support extension: explicitly select actual support at each end, explicit covers/intent/coordinates, prove real solid and physical bar-radius containment through junction, direction reversal and changed support stale acceptance, missing/wrong/ambiguous support fails. Do not execute unsupported current output as though implemented.

Wall reinforcement: confirmed structural wall host, actual local frame and thickness, explicit face covers/positions/stations/types, openings/joins/curvature rejection or correctly bounded implementation; horizontal/vertical roles and both faces, rotated/flipped wall, no substituted types. The feature is currently not implemented; this is a future execution matrix, not registration or PASS.

MEP opening creation: verify exact MEP/host solid clash, explicit clearance and reviewed cut geometry, supported local or linked host identity, orientation/oblique/round/rectangular cases, boundary containment, pre-existing openings and repeat selectivity. Confirm actual host cut and resulting clearance; failure rolls back all. Current command is read-only, so creation acceptance remains NOT_IMPLEMENTED.

## Preview parity matrix

For Beam/Column/Slab and subsequently Wall, compare solver 3D paths, true section intersections and actual created Rebar: host/type/shape/hooks/count/each physical position; different rotations, directions, covers and bar diameters. Distinguish 2D input schematic from solved view: a schematic cannot satisfy solved 2D parity. Test stale inputs, cancel, refresh, duplicate filtering, opening cuts, unsupported hosts, whole rollback and Undo. Inspect Paint for detached-data-only use. Save screenshot plus quantitative centerline/parity evidence; screenshot alone is insufficient.

## Installer lifecycle and signing

Use a separately confirmed disposable Windows VM/Sandbox; not the developer machine. Record OS/Revit versions, candidate hash and Authenticode chain/timestamp/revocation result. Certificate identity/private-key authorization is required; do not manufacture trusted signing. Current candidate is NotSigned. Execute install, load, upgrade from actual previous version, repair missing payload, uninstall, offline/dependency failure and locked-DLL failure. Capture MSI verbose logs, return/reboot codes, manifest/path/payload checks, per-user/machine isolation and restoration; no real project opened. Existing lifecycle source checks are not these operations.

## Exit

Record PASS/FAIL/BLOCKED per actual scenario, candidate/source SHA and exact model/VM identity. HOST_ACCEPTANCE and PRODUCTION_READY remain NO until mandatory scenarios and signing/lifecycle/review gates pass. Master stays untouched; PR #3 remains draft, no production release.

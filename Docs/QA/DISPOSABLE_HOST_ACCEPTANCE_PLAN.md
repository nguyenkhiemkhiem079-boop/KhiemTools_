# Disposable host and installer acceptance — REA execution plan

STATUS = NOT_EXECUTED. A running Revit process is not permission to read or mutate its model. Revit2025/2026 were observed; neither active model has been confirmed disposable. User confirmation is requested. No model has been changed in this continuation.

## Current user safety gate — 2026-10-23

**Do not run or trigger any Revit test while KHIM (JAYK) is working in Revit.** This applies to read-only smoke commands as well as model-changing fixtures. Wait until the user is not working, then require the detached disposable-model entry gate below before any host acceptance that reads or changes a model. Several `.rvt` project files exist in Downloads, including construction and architectural project names, but none is designated as a QA fixture; their presence does not authorize opening or copying them.

The Beam multi-host batch, dual-host Wall A/B, and bounded native MEP opening paths have code-side implementation records in `Docs/QA/Beam/PHASE_04C_ACCEPTANCE_MATRIX.md`, `Docs/QA/Wall/DUAL_HOST_CONTRACTS_WAVE.md`, and `Docs/QA/MEP/WAVE3_HANDOFF.md` / `CONFIRMED_OPENING_WAVE.md`. Older statements below that call multi-Beam, Wall reinforcement, or MEP creation unavailable are historical and are superseded only for those documented supported slices. Their live Revit acceptance is still `NOT_EXECUTED`.

## Entry gate

User must explicitly identify a detached disposable QA project path/name, Revit year and authorization to create/rollback/Undo. Record model identity, backup, worksharing/detachment state, active document and initial element identities/observable state fingerprint. Reject production/project models and uncertainty. Load only the candidate for the intended host year after closing any DLL-locking host; don't overwrite a running host installation. Runtime harness safety guard must approve before fixtures. Preserve logs and never report BLOCKED as PASS.

## Beam Manual independent multi-host assignment

Use two physically verified straight rectangular Beams, including unequal dimensions/direction. Enter separately supplied QA-labeled synthetic plans using exact loaded types; do not copy/scale another host's coordinates. On each host accept rollback-only solved preview. Cancel second editor: no bars on either Beam. Decline final batch confirmation: no bars. Create both: exact host/type/shape/hooks/physical quantity/centerlines equal accepted components. Undo: both disappear as one grouped operation; unrelated pre-existing reinforcement survives.

Before batch creation change host A and separately host B geometry/type/existing Rebar: reject stale acceptance before any mutation. Attempt repeated host identity, foreign-document plan, empty plan and missing preview: blocked. Complete duplicate on either host blocks all; partial duplicate is not classified as complete but physical interference still blocks. Trigger a real unsupported shape/API failure in second host, using QA-only test setup, then verify identities/state/host IDs and temporary elements prove whole group rollback including host A. Never add failure injection to production generator. Until executed, transaction-group source inspection is code-side evidence only.

## Current code-side slices and remaining host acceptance

The following describes source-level availability, not host acceptance. Keep scenarios `NOT_EXECUTED` until a designated detached fixture is opened in a confirmed safe Revit session and evidence is recorded against the tested source SHA.

- **Beam support extension:** the bounded same-document rectangular Beam/Column support path is implemented with explicit support identity, cover and continuation intent. Execute the physical CSG/swept-bar containment, start/finish and reversal matrix, stale-support checks, rollback and Undo from `Beam/SUPPORT_EXTENSION_WAVE.md`. Unsupported support solids and unapproved detailing/anchorage remain fail-closed; no engineering adequacy is inferred.
- **Wall reinforcement:** code-side contracts `WALL_MANUAL_V1` (actual structural Wall primary host) and `COLUMN_HOST_WALL_RELATED_V1` (actual Column primary host) are implemented as separate workflows. Execute Wall-only, Column-only, boundary intersection, rotation/mirror, exact ownership, cover/detail, stale-input, duplicate/interference, whole-batch rollback and Undo scenarios from `Wall/DUAL_HOST_CONTRACTS_WAVE.md`. The contracts must not be treated as equivalent; compilation and offline fixtures are not host results.
- **MEP opening creation:** `CmdMepOpenings` remains the read-only coordination review; `CmdMepOpeningCreateV1` is a separate bounded native-creation command for supported local Wall/Floor hosts and straight perpendicular penetrants. Execute actual Wall and Floor cuts separately, with duct/pipe/tray, measured clash and removed-volume checks, stale/repeat rejection, rollback, unrelated-state preservation and Undo from `MEP/CONFIRMED_OPENING_WAVE.md`. Unsupported geometry remains fail-closed; source and registered fixtures are not native acceptance.

## Preview parity matrix

For Beam/Column/Slab and subsequently Wall, compare solver 3D paths, true section intersections and actual created Rebar: host/type/shape/hooks/count/each physical position; different rotations, directions, covers and bar diameters. Distinguish 2D input schematic from solved view: a schematic cannot satisfy solved 2D parity. Test stale inputs, cancel, refresh, duplicate filtering, opening cuts, unsupported hosts, whole rollback and Undo. Inspect Paint for detached-data-only use. Save screenshot plus quantitative centerline/parity evidence; screenshot alone is insufficient.

## Installer lifecycle and signing

Use a separately confirmed disposable Windows VM/Sandbox; not the developer machine. Record OS/Revit versions, candidate hash and Authenticode chain/timestamp/revocation result. Certificate identity/private-key authorization is required; do not manufacture trusted signing. Current candidate is NotSigned. Execute install, load, upgrade from actual previous version, repair missing payload, uninstall, offline/dependency failure and locked-DLL failure. Capture MSI verbose logs, return/reboot codes, manifest/path/payload checks, per-user/machine isolation and restoration; no real project opened. Existing lifecycle source checks are not these operations.

## Exit

Record PASS/FAIL/BLOCKED per actual scenario, candidate/source SHA and exact model/VM identity. HOST_ACCEPTANCE and PRODUCTION_READY remain NO until mandatory scenarios and signing/lifecycle/review gates pass. Master stays untouched; PR #3 remains draft, no production release.

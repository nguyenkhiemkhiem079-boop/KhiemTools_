# Phase 04C — Beam Manual Detailing v1 Acceptance Matrix

Branch: `fix/preview-hardening`
Starting SHA: `843d99218e5f85ef2a97ab3b0d56e59a911f4c13`
Implementation checkpoints pushed: `1d3c01ed3f908a6941bac92155368c3fe50c1bb5`, `9ca81a943be9f482ca6f5f7a84804c62825a658a`, `530b875ef7037edabe33aa0c8715e54feefea781`, `f4e418c42e09c42b980f3f55653ecf43c6bb9377`, `92b79f7e0660b6ec7df85dec6db056834fd98b72`. The consolidated code/audit checkpoint SHA is recorded in `PHASE_04_HANDOFF.md`.

**Current-source correction — 2026-10-23:** This file contains historical checkpoint notes that are superseded by [`SUPPORT_EXTENSION_WAVE.md`](SUPPORT_EXTENSION_WAVE.md). The current source includes bounded verified support extension and independently authored multi-Beam Manual assignment flows. Their live Revit acceptance remains `NOT_EXECUTED`. Historical test/build counts below are not evidence for the current REA branch unless the recorded source SHA matches.

The authoritative approved software input/geometry contract is mirrored verbatim in [`Docs/Engineering/Beam/MANUAL_DETAILING_CONTRACT_V1.md`](../../Engineering/Beam/MANUAL_DETAILING_CONTRACT_V1.md). Project reinforcement values and structural-code compliance rules were not supplied and have not been invented.

| Acceptance area | Code-side disposition | Revit host disposition |
|---|---|---|
| Typed plan, identity, versioned save/load | Explicit document/host-bound plan and atomic per-host companion JSON store; synthetic round-trip checks | Load/save against a detached Beam: `HOST_REQUIRED / NOT_EXECUTED` |
| Manual longitudinal roles/layers/positions/stations | Continuous, additional and side/skin roles are user labels over individually explicit local coordinates, exact types, layers and start/end stations; no role-derived placement | Verify actual paths/quantities: `HOST_REQUIRED / NOT_EXECUTED` |
| Hooks and returns | Exact loaded RebarHookType identity/orientation OR explicit ordered return vertices; no fallback or mixed modes; envelope and generated path are checked | Exact Revit hook/shape compatibility and resulting bends: `HOST_REQUIRED / NOT_EXECUTED` |
| Stirrups and hangers | Explicit role, exact loaded type/shape, explicit closed/open section path and absolute stations; station, contour, cover, bar/stirrup and path collisions fail closed | Exact shape/hook compatibility, created shape and parity: `HOST_REQUIRED / NOT_EXECUTED` |
| Independent covers | Top/bottom/left/right and stirrup-outside clear covers are separately supplied; generated Rebar physical centerlines/bends are checked against their respective limits | Verify actual assigned/custom cover behavior in Revit: `HOST_REQUIRED / NOT_EXECUTED` |
| Production preview and paint isolation | Same production generator is captured rollback-only; detached centerline snapshot is offered for acceptance; schematic uses copied local coordinates only | Cancel, inspect 3D preview, and verify model fingerprint: `HOST_REQUIRED / NOT_EXECUTED` |
| Freshness, duplicate selectivity, interference | Plan/frame/exact refs/existing host Rebar are fingerprinted; complete-plan duplicates are distinct from partial matches; proposed output is checked against existing and newly-created Rebar | Change one input at a time; verify duplicate/collision/Undo behavior: `HOST_REQUIRED / NOT_EXECUTED` |
| Atomic create and parity | Checked transaction group; all enabled groups are generated; host/type/shape/hooks/quantity/centerlines must match accepted preview or the group rolls back | Run fixture on confirmed disposable model: `HOST_REQUIRED / NOT_EXECUTED` |
| Explicitly bounded boundaries | Verified same-document rectangular support extension and independently authored multi-Beam batch are implemented in the later support wave; automatic design/detailing values and code compliance remain unsupported and fail closed | Support geometry, host ownership, whole-batch rollback and Undo remain `HOST_REQUIRED / NOT_EXECUTED` |
| Offline UI matrix | Manual form included at minimum, 1366×768 and 1920×1080 with 100/125/150% scale simulation | Offline controls/render QA only; not Revit-host or OS DPI acceptance |
| Final code-side regression/build | K-Rebar `157/157`; Golden Regression `124`; Rebar config `37/37`; canonical `Test-All.ps1` `2807/2807` after the UI-contract audit weight was reconciled to its emitted 218 checks; full Rebar layout `17,959` controls / `360` renders (canonical and repeated standalone run); Revit 2024 `0` errors / `210` warnings; Revit 2025 `0` errors / `208` warnings | Compilation/static/layout evidence is not live model validation |

## Final disposition

Wave1 superseding acceptance: support extensions now wired for exact same-document physically verified rectangular structural Column/Beam supports with explicit uniform exposed support cover and continuation intent. Real Line/Arc volume + endpoint envelope is checked against concrete union/exposed cover shells; same production capture/Create, support-neighbor staleness, batch atomicity and typed store. Golden157/K-Rebar163, five source builds, full50 suites PASS; focused UI812/27. BR-MANUAL-SUPPORT-ROLLBACK REGISTERED/COMPILED, NOT_EXECUTED. Historical unavailable-support notes below no longer describe that bounded code slice. Unsupported support geometry, design sufficiency and actual host acceptance are still not certified.

### Superseding recovery verification — 2026-10-13

Historical counts below are retained as dated context, not current proof. Current code checkpoint `7529d6cce2b359763ecd3c77e43a5213ba781863`: K-Rebar159, Golden134, config37, canonical48/48 suites (2984 configured weight), full offline layout17987/360, focused Manual734/18. Revit2022–2026 source builds zero errors; 2024/2025 warnings10/8. The previous 720-check Manual result did not detect minimum-height clamping at 1366x768; corrected minimum size and explicit requested-profile assertions supersede it. Four Manual geometry findings were corrected; see root REBAR_QA_REPORT.md. Full contract PARTIAL, supported slice code complete, HOST_ACCEPTANCE NOT_EXECUTED and PRODUCTION_READY NO.

- Each Manual v1 plan describes explicit geometry for one supported rectangular Structural Framing Beam: bar-role/layer stations, exact bar/hook/shape identities, hook/return choices, stirrup/hanger geometry, independent covers, save/load, rollback preview and parity-guarded Create. The current form opens a separate editor for each selected Beam; `BeamManualBatchV1` creates the independently reviewed plans atomically without copying or scaling a plan.
- No quantity, cover, station, layer, anchorage or design value is inferred from role names or legacy settings.
- Verified same-document rectangular support extension and multi-Beam assignment are documented in the superseding support wave. Unsupported support geometry, inferred design values and structural-code checks remain unavailable and fail closed. This is geometry software scope, not structural adequacy.
- Registered baseline runtime fixture `BR-MANUAL-V1-ROLLBACK` exercises straight explicit bars only. Extended hook/return/stirrup acceptance is separately recorded as a host scenario and remains unexecuted.
- The full configured offline Rebar layout suite includes the Manual form and passes; a focused Manual-only run passed 720 checks / 18 renders at minimum, 1366×768 and 1920×1080, with 100/125/150% simulated scaling.
- Live Revit Manual acceptance: `NOT_EXECUTED`; no confirmed detached disposable model was used.
- `CODE_COMPLETE` is reported only for the narrowed supported code-side scope after final regression/build. `HOST_ACCEPTANCE=NO` and `PRODUCTION_READY=NO` remain until host execution and scope review.

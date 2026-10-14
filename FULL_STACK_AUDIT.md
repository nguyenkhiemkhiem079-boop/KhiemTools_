# Full-stack recovery and audit — 2026-10-13

LATEST Wave3 from218f5882: existing native workflow reused, not recovery/reimplementation. Explicit minima/offsets and detached2D/3D review feed one canonical cut, approval, fresh physical fingerprint, native ownership/CSG verification and atomic rollback. Consolidated identity/round-duct/stale/projection fixes at018b054d8739dd4ad37d8f51e2ea6c806cc76de5. Final clean source55/55 suites, Golden238, MEP34/12/20, offline graphical54/18, five source API builds0 errors; no Rebar production changes. Docs/QA/MEP/WAVE3_HANDOFF.md authoritative. Linked/Beam/oblique/advanced cuts UNSUPPORTED; native/loading/lifecycle acceptance pending, PRODUCTION_READY=NO. No recovery restart/merge/release.

2026-10-14 revised dual-host Wall wave is code-complete for explicitly supported A/B slices: A actual Wall with verified boundary-Column extensions; B actual rectangular structural Column with explicit scenario/ownership and Column-contained straight geometry. Separate typed plans/native frames/generators/commands; independent multi-host review, current previews/complete duplicates, checked atomic group and final all-host input/output recheck. Explicit terminal embed/extension inputs are physically consumed, no engineering defaults or calculated Ld. Details/limits: Docs/QA/Wall/DUAL_HOST_CONTRACTS_WAVE.md. Final canonical54/54 suites and all five source API builds PASS; no host acceptance or production readiness. B external extensions/hooks, unverified geometries and system/fabric clearance reject, not falsely implemented. Existing bounded Wave3 MEP creation retained and independently rechecked, not reimplemented.

Current preservation checkpoint: solved inline2D/true polyline cut for supported Beam Manual, Column and Wall; existing Slab true cuts preserved. Canonical final-world set/moved-bar positions and native curved Beam/Wall envelope clearance corrected. Consolidated audit: Docs/QA/REBAR_PREVIEW_PARITY_WAVE.md. User subsequently superseded Wall-only ownership decision: ContractA Wall host PLUS isolated ContractB actual Column host are now required. ContractB is next work, not covered by this checkpoint. Preserve all advanced commits; no reset to f6a588d. Live host acceptance remains NOT_EXECUTED, product readiness NO.

Wave3 current source: separate user-confirmed native MEP opening command for physically verified unjoined rectangular Wall/horizontal axis-aligned Floor and perpendicular local straight duct/pipe/tray. Actual solid clash, explicit clearance, conservative reviewed rectangle, fresh fingerprint and actual removed-solid/clash postconditions; whole checked transaction-group rollback on failure. Existing-Rebar envelope interference/system-fabric resources block, no reinforcing redesign. Legacy review remains read-only. Native cut/Undo fixture NOT_EXECUTED. Details/limitations: Docs/QA/MEP/CONFIRMED_OPENING_WAVE.md. Historical MEP-creation-unimplemented notes below are superseded for this bounded slice only.

Wave2 (2026-10-14): explicit Wall reinforcement supported slice is now implemented; user confirmed Wall host with selected verified boundary Column extensions. Independent physical Wall frame, six explicit covers/endpoints/exact types, native concrete union/cover envelopes, actual solver preview and parity-protected atomic Create; no design quantities/values inferred. Old Wall-unimplemented notes below are historical. Advanced Wall shapes/hooks/ties are rejected, not falsely implemented. Audit fixes and host-required matrix: Docs/QA/Wall/WALL_MANUAL_WAVE.md. MEP creation and consolidated preview wave remain pending; product readiness NO.

Next-implementation Wave1 fromcc31ab52: bound rectangular Column/Beam support extensions wired UI→versioned plan/store→exact solid envelope→production preview/Create, with support-neighborhood stale/collision checks and atomic batch postconditions. Golden157/K-Rebar163 and API2022–2026 builds pass; offline Manual/support UI812 checks/27renders pass. Host fixture registered, not executed. No engineering defaults. Recovery is not repeated; older single-host/support-unavailable statements below are historical. Details: Docs/QA/Beam/SUPPORT_EXTENSION_WAVE.md.

Repository: `nguyenkhiemkhiem079-boop/KhiemTools_`; integration branch: `fix/preview-hardening`.
Verified remote master baseline: `601d1feb8731356f3ac2f509aa5aabeecbd8fd3a`.
Recovery starting feature tip: `308bf4146663e00e04f5d7bcb500cbf188af438f`.

## Wave 0 — recovery inventory

- Main checkout and index were clean, no stashes existed, and local/remote feature tips matched after fetching all remote branches and tags.
- The feature branch contains 173 commits after baseline; baseline-to-feature diff covers 467 files (+32,251 / -3,294). All local Stage 3–18, Slab and Beam feature tips and local `master` are ancestors of the pushed feature tip. Local `master` is `612c4cd63ca641e22579161730c47b81e7cb0961`, one runtime-harness commit ahead of remote master, already contained in the pushed feature history. No master ref is changed by this mission.
- `backup/master-pre-promote-0137941` has five commits absent by SHA from remote history. `git cherry HEAD` identifies four as patch-equivalent to integrated work: `5561290`, `17c754e`, `dc0735f`, `0137941`. The remaining `a0a7c8e` rectangular-Rebar snapshot differs from later integrated source; preserve it for historical comparison rather than replacing evolved production code.
- `git fsck --full --no-reflogs --unreachable` found 15 unreachable commits (historical Stage 3 candidates and expired stash/index/untracked snapshots). Each was preserved under `refs/recovery/fullstack-20261013/<full-sha>`. No object, stash, source file or old ref was deleted.
- A verified Git bundle contains all 89 repository refs, including the recovery refs and historical backup branch. Local archive: `../recovery/fullstack-20261013.bundle`; SHA-256 `7599bd00fc52c548a55e1cbc0f7fb9158d56f3694084c47fa63203fab17a8bfb`. This archive is intentionally outside the product repository and is not an installer payload.
- A detached package worktree at historical `4542170` is clean and retained. The outer workspace Git root has untracked source fragments, build logs and sibling directories; they are preserved in place and are not silently staged into the product repository.
- GitHub lookup found no open PR for this feature branch. GitHub CLI is unavailable in PATH; the connected GitHub tools are available for the integration PR.

## Audit method and acceptance boundary

Inspect actual source and current executable gates; historical Phase reports are context, not proof. Prioritize physical Rebar geometry, exact types, canonical plans, preview/Create parity and rollback. Compile both supported acceptance targets and validate the current-source installer package. Live mutation tests require a confirmed disposable supported Revit model; offline/source tests never certify host acceptance.

## Wave 1 — coordinated Rebar correction

Confirmed and corrected four P1 findings: non-coplanar longitudinal return paths were accepted by the input validator; every return used the local-right normal even for a horizontal return; stirrup end stations and solved centerlines omitted physical radius at Beam end faces; malformed persisted return/contour points could reach null dereferences in collision checks. Two regressions failed against the original code (non-coplanar path, stirrup at station zero); the coordinated correction passes ten additional numerical/validation regressions. A pure geometry helper now derives the canonical path plane and shares the physical end-envelope rule with the Revit adapter. Existing straight/vertical hook-plane conventions remain stable; no engineering value is added.

Wave 1 verification: Golden Regression 134 executed assertions PASS; K-Rebar 159/159 static checks PASS; dependency restore PASS; Revit 2024 Release 0 errors / 210 warnings; Revit 2025 Release 0 errors / 208 warnings. Actual Revit generation, hook compatibility and undo remain host-required. Full canonical regression/package validation is scheduled after the cross-module audit.

The cross-module audit and current validation results will be recorded below as they complete. Unsupported Wall reinforcement, support extensions, design-code calculations or inferred project reinforcement values will not be introduced without an accepted contract.

## Wave 2 — Revit API compatibility recovery

Actual builds against installed API DLLs exposed 42 errors on Revit 2022, 20 on 2023 and 101 on 2026. Corrected production ID reads through the existing version-aware adapter: modern identities retain 64-bit values; legacy int-backed data uses checked conversion so large IDs are never truncated. Category identities use the same compatibility boundary. Mechanical call-site edits preserve existing geometry, quantities and transaction behavior; snapshot `IntegerValue` data properties are unchanged.

Schedule Split previously claimed capability unconditionally and directly referenced APIs absent in Revit 2022. Its adapter now discovers the exact segment/placement signatures at runtime, blocks the workflow before mutation when unavailable, uses the existing checked execution path when available, and propagates underlying API failures. Revit 2022 Schedule Split is explicitly unavailable; no synthetic split or placement fallback is generated.

New canonical gate `Tools/Verify-RevitCompatibility.ps1`: 20 executable synthetic old/new API assertions plus five actual source builds. Revit 2022/2023/2024/2025/2026 all compile with zero errors after correction. Warnings in the captured full builds: 0/0/10/8/144. Those results verify source API compatibility, not live runtime loading or model behavior. Focused K-Rebar 159, Schedule Split 49, Parameter Manager 174, Viewport Align 67, Architectural 39, MEP 31, QS 49 and runtime-harness static 50 gates pass. Legacy int-backed workflows still reject IDs above Int32 range rather than silently changing persisted contracts; full 64-bit storage migration is a documented remaining boundary.

## Wave 3 — release truth, payload integrity and Manual UI

Removed fabricated offline updater version/date/changelog. Discovery now fails closed with a visible unavailable state; six production-linked fake-HTTP contract checks cover success, offline/malformed/missing metadata and official URL routing. This is not a live update/installation test.

The installer captures all 132 actual build-input hashes, a normalized product-source fingerprint and MSI hash. A canonical verifier extracts the MSI and checks every identity and byte hash. Compatibility builds use isolated output/intermediate directories so they cannot overwrite packaged inputs. Package build/inspection never installs the product. Category conversion additionally rejects overflow explicitly.

Manual editor minimum height 800 previously enlarged the requested 1366x768 test window; the harness did not detect that discrepancy. Corrected minimum height to 720, retained scrolling, and gave detailed grids readable horizontally scrollable columns. The harness now rejects enlarged requested window profiles. Focused minimum/1366x768/1920x1080 at 100/125/150% simulated scaling passes 734 checks and 18 renders; real Windows DPI/Revit UI remains unexecuted.

Canonical reporting now records actual suite execution separately from configured audit weights. It fails when any executable suite fails; counts alone are not implementation or runtime evidence.

## Cross-module audit

| Area | Current verified implementation / boundary |
|---|---|
| Rebar | Existing Beam, Column, Slab and bounded Foundation plans/generators/previews retained. Manual geometry defects corrected in Wave 1. Wall reinforcement remains unsupported, not an invented feature. All model behavior needs host acceptance. |
| Beam contract | Explicit Manual slice exists; full approved contract is PARTIAL because verified support extension and multi-Beam Manual assignment are unavailable. No project engineering values/code compliance claimed. |
| Architectural | QuickArchi, room finish/holes, transformed Room3D, lintel and door workflows inspected; checked mutation/postconditions and current static/domain gates. Real-host acceptance pending. |
| MEP | Read-only solid clash analysis and checked elevation-note workflows exist. Actual clash-opening creation is NOT_IMPLEMENTED. |
| QS | Collector native volume/material and explicitly derived steel-density quantities, units, grouping and atomic export inspected. Project quantities/compliance not certified. |
| Sheets/docs | Existing Stage 3/Wave 1 and schedule/viewport gates retained; unavailable 2022 split API blocks safely. No live sheet mutations tested. |
| Ribbon/settings | Command metadata, UI contracts, configuration/domain and offline layout gates execute. Reflection registration is not live command execution. |
| Deployment | Current-source candidate build and complete payload inspection; unsigned and no install/repair/uninstall or real multi-year load acceptance. |

No P0 confirmed in this code-side audit. Confirmed P1 defects were corrected with regression evidence. Host-required risks remain open, not downgraded to PASS. Legacy int-backed workflows deliberately reject >Int32 identities; complete 64-bit persistence migration remains a future boundary.

## Delivery state

### PR #3 development continuation

Second coordinated batch adds final all-host postcondition recheck (late commit changes to earlier hosts/output), 11 production-linked synthetic orchestration assertions and legacy-preview invalidation. Installer harness now blocks mutation without explicit disposable-machine confirmation, prevents skipped/failed static QA, and reports actual suites. Read-only signing inspection returns BLOCKED for unsigned candidates. These are development/QA-safety checkpoints, not completion of the remaining feature mission; DEVELOPMENT_CONTINUATION_HANDOFF.md lists exact remaining priority order.

No recovery restart. Starting tip `171bdf8f2f31e9de16dcda6772fc01e7fe0ec1f1`. Added independent Manual multi-Beam assignment: per-host editing/solved acceptance, no copying/scaling, all-host stale/duplicate preflight and parity-protected transaction group. Cancel before final confirmation creates nothing. Six identity-policy goldens execute; actual transaction rollback is not certified by them. Host2025/2026 processes exist but no model is confirmed disposable; user confirmation requested, no mutation performed. Disposable host/installer acceptance plan is in Docs/QA/DISPOSABLE_HOST_ACCEPTANCE_PLAN.md. Support extension, Wall and MEP creation remain subsequent incomplete waves.

Recovery inventory, cross-module code audit, coordinated corrections and code-side validation are complete. Final Test-All executes 48/48 passing suites (2984 configured weight), Golden134, K-Rebar159, UI contracts218, full offline Rebar17987 checks/360 renders and five zero-error API builds. Current-source installer build/extracted payload verification passes. See TEST_RESULTS.md for all executed scripts and evidence boundaries. Draft PR #3 is open/unmerged; required-check/review acceptance is pending and no server check results were returned. This is audited supported-scope delivery, not full feature/host/production readiness.

External preservation additionally retains the 16 loose sibling C# fragments (two identical, fourteen historical variants) in place and in `../recovery/workspace-fragments-20261013.zip`, SHA256 `16d302e96752df294343b75111641ea7f042fd36583a17b9167140f35c5166a1`; all have corresponding evolved production files. The complete object archive `../recovery/git-objects-20261013.zip`, SHA256 `0ecf6240d0048e4b287c8aa5258625d283daee86ee3d4a9ca4ad668913b8b637`, also preserves unreachable blobs/trees. Divergent old remote dev/ribbon/updater/UI and gh-pages histories were retained, not blindly merged over evolved source. No preservation archive is a product deliverable.

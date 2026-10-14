# Cumulative Phase Defect Register — Slab Reinforcement

## Wave3 MEP graphical proposal checkpoint

P1 code gaps addressed: explicit minimum dimensions/offsets were missing; accepted fields now resolve one canonical rectangle and re-enter fresh pre-Create validation/fingerprint. Missing/negative/nonfinite/overflow/clearance-losing inputs reject. Graphic review was absent; required detached 2D/3D wireframe now shares this cut, with actual centerline explicitly distinguished from outer penetrant profile. Structural Floor eligibility is explicit. P0 none confirmed in the bounded source scope. P1 HOST_REQUIRED: actual Wall/Floor/duct/pipe/tray cut kernel, stale/duplicate/cancel/rollback/Undo and per-year loading. P2 linked/Beam/oblique/advanced host cuts remain unsupported, never transformed/rehosted implicitly. Next sub-wave strengthens source ownership diagnostics, native rollback scenario coverage and offline UI regression before consolidated validation.

## Revised Wave2 dual-host closure

Docs/QA/Wall/DUAL_HOST_CONTRACTS_WAVE.md supersedes earlier single-host decision. ContractA Wall and isolated ContractB actual Column implemented, with explicit per-host multi-selection review and atomic batch. P1 source fixes: mandatory terminal-detail fields actually checked against native primary/terminal intervals; no default/missing migration values; actual mirrored Column frame; exact ownership/contract mismatch guards; all-host final input/output recheck and complete duplicates; unsupported system/fabric clearance explicitly blocked. P0 none confirmed supported code-side. P1 actual A/B geometry/cover/rotated-mirrored poses/extension/anchorage, multi-host native rollback/model state and Undo remain HOST_REQUIRED. P2 deferred: B external extensions/hooks/unverified host geometries, mixed UI batching, richer JSON editor, system/fabric clearance and native large-batch performance. No unsupported geometry or engineering approval is invented. No live fixture result implied by registration/compile/synthetic orchestration tests.

## Consolidated assigned Wave1–4 audit (2026-10-14)

Current authoritative audit: Docs/QA/REBAR_PREVIEW_PARITY_WAVE.md. P0 none confirmed code-side. P1 source defects resolved as a coordinated batch: final distributed/moved Rebar position reader (raw base curves were wrong for sets); actual accepted inline2D/true polyline cuts/exact host filtering; native curved Beam envelope collision guard; previously corrected Wall input/layout/strict JSON and MEP join/cut/reinforcement safety. Missing final curves/segments fail closed. P1 HOST_REQUIRED remains actual geometry/cover/hooks/clearance, stale/duplicates, whole rollback/model integrity and Undo on confirmed disposable models; no live acceptance claim. P2 resolved verifier regex/antialiased pixel accounting; deferred conservative MEP rectangle/system limits, tessellation/symbolic marker precision, richer Wall input UI, native large-batch CSG performance and support-solid rendering. Signing/lifecycle/loading pending; no release readiness inferred.

## Next implementation Wave3 — MEP opening

Bounded local Wall/Floor native opening implementation: Docs/QA/MEP/CONFIRMED_OPENING_WAVE.md. No P0 confirmed in supported code-side slice. P1 native cut/clash/rollback/Undo/UI and safety matrix remains HOST_REQUIRED, fixture registered NOT_EXECUTED; do not treat code builds as closeout. P2 conservative geometry may enlarge rotated-Wall cuts and system/fabric resources block globally; deliberately disclosed limitations pending reviewed exact profile/system adapter expansion. Beam/link/oblique/advanced hosts not supported; engineering/fire approval not inferred.

## Next implementation Wave2 — Wall (2026-10-14)

Confirmed ownership: Wall-hosted bars extending into independently selected verified boundary Columns. See Docs/QA/Wall/WALL_MANUAL_WAVE.md. P0: none confirmed in the bounded supported code-side slice. P1 resolved: root layout width overflow; ignored unknown JSON fields; approximated curved-neighbor collision (now explicitly blocked). P2 resolved: blank-render sampling alias with unchanged threshold and bitmap self-tests; quadratic collision diagnostics; misleading Beam design-code tooltip. Host acceptance remains P1 HOST_REQUIRED: native Wall/Column CSG, cover/flip/rotation, stale/duplicates, rollback/unrelated bar integrity and Undo; fixture REGISTERED/COMPILED, NOT_EXECUTED. Advanced non-prismatic/opened/curved/slanted Walls and unsupported detailing remain explicitly unsupported, not complete.

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

---

# Phase 04 — Beam Reinforcement (2026-10-09)

Branch: `fix/preview-hardening`; start `aa0fc5ddfba4f2234487c673ffb5977a167845db`; pushed implementation checkpoint `19fca67cda2cdcd004bff91f042b1af0724a01e7`.

- P0 items fixed in the Beam code-side checkpoint: exact loaded bar-type identity, no silent shape/creation fallback, physical rectangular host validation, actual solid support intersection, no invented support/free-end dimensions or production cover fallback, longitudinal overlap rejection, deterministic non-overlapping stirrup stations, created centerline containment, atomic Create/parity, and unsaved support-geometry/cover fingerprint inputs.
- P1 remaining: additional top/bottom curtailment contract unresolved and requests fail closed; side/skin bars and automatic side bars fail closed; supports limited to columns/structural beams; no explicit `RebarHookType` identity or code-verified anchorage; maximum uniform rather than face-specific cover; and all live Beam host scenarios remain `HOST_REQUIRED / NOT_EXECUTED`.
- P2: mixed English/Vietnamese Beam UI without a Beam language selector; static elevation remains disclosed as illustrative.
- Evidence: K-Rebar 147/147, Rebar configuration 37/37, Golden Regression 96, Beam offline layout 3,114 checks / 54 renders, canonical `Test-All.ps1` 2,766/2,766, Revit 2024 Release 0 errors / 210 warnings, Revit 2025 Release 0 errors / 208 warnings.
- `PHASE CODE COMPLETE = NO`; do not call the Beam product production-ready. The Beam-specific capability matrix and remediation detail are in [`Docs/QA/Beam/PHASE_04_DEFECT_REGISTER.md`](Docs/QA/Beam/PHASE_04_DEFECT_REGISTER.md). Handoff/resume instructions are in [`Docs/QA/Beam/PHASE_04_HANDOFF.md`](Docs/QA/Beam/PHASE_04_HANDOFF.md).

## Phase 04B consolidated contract audit (2026-10-12)

- Repository evidence does not include an approved Beam detailing schedule or construction-detail contract for additional-bar cut-offs/layers, skin-bar coordinates, hook identity/orientation, support-end anchorage or local-face cover mapping. These items are `REQUIRES_ENGINEERING_APPROVAL`; unsupported generation remains fail-closed.
- The active Beam form supplies zero extra-bar counts and disables side-bar requests. The generator rejects positive extra/skin inputs before its legacy fixed-fraction private routines can execute. A new K-Rebar static check explicitly protects this gate; canonical `Test-All.ps1` expected count is updated with the check.
- `BeamRebarSettings` retains legacy positive defaults for extras and automatic/quantity side bars, but current search confirms that this settings model is not consumed by the active Beam UI/generator. Future template wiring must disclose/migrate these legacy fields before use; do not interpret persisted values as generated reinforcement.
- No P0 was confirmed in the supported code path. Phase code completion and production readiness remain NO due unresolved P1 detailing contracts and unexecuted host acceptance. Detailed disposition is in the Beam-specific register and handoff.

## Phase 04C — Beam Manual Detailing v1 (2026-10-12)

Starting SHA `843d99218e5f85ef2a97ab3b0d56e59a911f4c13`; pushed implementation checkpoints: `1d3c01ed3f908a6941bac92155368c3fe50c1bb5`, `9ca81a943be9f482ca6f5f7a84804c62825a658a`, `530b875ef7037edabe33aa0c8715e54feefea781`.

- P0 resolved in supported slice: explicit saved plan/document/host/type identity, caller-owned and atomic Create transaction, solver rollback preview, stale fingerprint/parity/complete duplicate gates, and proposed-bar collision/staleness checks against all existing hosted Rebar. Unknown existing type/geometry fails closed.
- P1 host acceptance pending: `BR-MANUAL-V1-ROLLBACK` and actual Manual UI/model lifecycle remain registered/compiled but `NOT_EXECUTED` in a confirmed disposable Revit model.
- Explicit non-support, not hidden approximations: hooks/returns, stirrups, verified support extensions, multi-host plan editing, nonrectangular/unverified hosts, code-derived quantities/anchorage/cover/stations and structural-code compliance. These need further approved input from the responsible designer.
- P2: input schematic is disclosed as non-solved; existing Beam and Slab paths are separately retained; one supported Beam per Manual plan.
- Evidence: K-Rebar 156/156; Golden Regression 117; canonical `Test-All.ps1` 2,796/2,796; offline Rebar layout 17,479 checks / 348 renders; WPF UI layout 100 icons / 10 surfaces / 43 renders; Runtime QA structural audit 50/50; Revit 2024 `net48` and Revit 2025 `net8.0-windows` builds 0 errors.
- P0 remaining: none known in the supported code-side slice. Phase 04C is not production-ready: host acceptance and model-specific engineering validation are pending. Live Revit QA is `NOT_EXECUTED`.

## Phase 04C consolidated implementation/audit reconciliation (2026-10-12)

This is the latest Beam disposition; earlier Phase 04C checkpoint text in this cumulative register is historical and superseded by `Docs/QA/Beam/PHASE_04C_ACCEPTANCE_MATRIX.md` and the Beam-specific register.

- **P0:** none confirmed in the supported one-Beam code-side workflow.
- **P1 code-side defects resolved:** Manual hook/return/stirrup/hanger paths are wired through the editor, typed model, persistence, fingerprint, production generator, rollback preview, Create parity and synthetic checks; self-intersecting closed contours fail closed; Manual UI layout passes the new offline matrix.
- **P1 host-only:** run `BR-MANUAL-V1-ROLLBACK` and `BEAM_MANUAL_V1_EXTENDED_DETAILS` only on a confirmed detached disposable Revit model. Live QA remains `NOT_EXECUTED`.
- **Narrowed/unavailable:** verified support extensions, one-plan multi-Beam editing, inferred engineering values and code compliance remain visibly unavailable and fail closed; production readiness remains NO.

## Full-stack recovery audit — 2026-10-13

Four confirmed Beam Manual v1 P1 findings are corrected in a coordinated batch: reject non-coplanar return/core paths, derive the actual curve-plane normal (including horizontal returns), enforce actual bar radius at raw stirrup and solved-path end faces, and safely diagnose malformed persisted path points. No engineering detailing values were invented. Ten new numerical/validation goldens pass (134 total); two new source-integration guards pass (159 K-Rebar total); Revit 2024/2025 builds have zero errors. See `REBAR_QA_REPORT.md` for reproduction and precise scope. All live Revit acceptance remains pending. Full-stack release findings and fresh canonical/package results are recorded in `FULL_STACK_AUDIT.md` and `RELEASE_READINESS.md` when complete.

Full-stack API P1 defects corrected: version-specific ElementId access failed 2022/2023/2026 compilation, and Schedule Split directly referenced newer APIs while reporting availability on 2022. Shared ID/category adapters and explicit runtime schedule capability checks now allow all five advertised years to compile. Twenty old/new synthetic contract assertions pass. Live loading on each advertised host remains unexecuted; legacy int-backed storage deliberately rejects overflow and has not been silently migrated.
## Full-stack recovery audit — 2026-10-13 release/UI batch

Next Wave1: prior missing support-extension implementation closed for exact verified rectangular Column/Beam supports, explicit exposed uniform cover/intent and physical CSG gate. Guard station permission does not certify containment; exact real generator proves it inside preview/Create or throws. New tests157 goldens/163 source gates; full50 suites PASS. Host CSG, hooks/bends, neighbor interference, rollback and Undo remain HOST_REQUIRED, not code-side PASS. Column-hosted Wall meaning queried before the next production host contract; never silently move Wall reinforcement to a Column.

PR #3 continuation: multi-Beam independent assignment implemented; full requested continuation remains incomplete. P1 batch late-change risk resolved with final all-host geometry/output parity recheck and 11 synthetic orchestration cases. P1 real-installer safety gaps resolved with disposable-machine opt-in, mandatory fail-closed static QA and actual suite accounting; no installer mutation. Support extension, Wall, MEP creation and solved 2D completeness remain acceptance work, not closed defects. Host/VM/certificate confirmations requested and pending. See DEVELOPMENT_CONTINUATION_HANDOFF.md.

- P1 UPDATER-TRUTH: offline failures fabricated release metadata; RESOLVED with unavailable UI and six executable discovery checks.
- P1 PAYLOAD-PROVENANCE: no complete current-source/extracted-byte evidence; RESOLVED with 132-file manifest, source fingerprint and 134-check canonical payload verifier.
- P1 BUILD-OUTPUT-ISOLATION: compatibility builds could replace packaged shared outputs; RESOLVED with year-specific isolated build paths.
- P1 MANUAL-WINDOW-CLAMP: 800px minimum invalidated requested 1366x768 QA; RESOLVED with 720px minimum and explicit harness assertion.
- P2 MANUAL-GRID-READABILITY: compressed detailing headers; RESOLVED using readable widths and horizontal scroll, without hiding inputs.
- P2 QA-ACCOUNTING: configured weights previously presented as actual audits; RESOLVED with per-suite result manifest and separately labeled aggregate weight.
- OPEN HOST_REQUIRED: disposable Revit generation/parity/rollback/undo and installer lifecycle/signing. No live PASS claimed.

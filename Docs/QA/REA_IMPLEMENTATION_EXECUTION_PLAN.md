# K-TOOL REA roadmap — implementation and handoff plan

**Current stage:** CORE-00 gates remain open; RB-01 code-side work is in progress. Roadmap acceptance is not complete.

**Source:** `KTOOL_REA_REVIEW_ROADMAP_2026-10-22 (1).xlsx`, sheets `Ke hoach 16 Wave`, `Cong viec 68 muc`, and `Thay doi baseline`. Workbook rows marked `PLANNED` are planned work, not source status or acceptance evidence.

**Starting baseline:** source commit `18c2cc27350808390b0e7e5a383dafb38b945b40`, isolated branch `codex/rea-core00-20261023`. Keep the main checkout and user draft unchanged. Do not merge to `master` or release a production build during these waves.

**Previous branch snapshot — 2026-10-23:** branch tip `81738fbe9a3e8a5f87d5136d00c956f40c36da3a`; the remote branch matched and the worktree was clean at that checkpoint. This has since been superseded by the source and evidence updates below. The installer payload remains tied to source revision `fa13ee570bb4e10e7481167978d57d245dcc51c6`; do not confuse that package source with later branch commits.

## Operating gates

1. Reconcile each task against current source before creating or rewriting code. Existing code or dated QA records count as source evidence only; they do not pass a current host acceptance gate.
2. Keep a task tied to its contract ID, source SHA, dependencies, expected output, failure behavior, and evidence path. Do not silently choose engineering dimensions, unsupported hosts, or quantity rules.
3. Build and inspect the affected source slice before handing it off. Record compile-only results separately from tests and host acceptance. Do not add or run test suites unless the user asks.
4. **Do not run or trigger any Revit test while KHIM (JAYK) is working in Revit, including read-only smoke commands.** Later host acceptance also requires a user-designated detached disposable model. Never infer that a downloaded or currently open project is disposable.
5. Do not install, deploy, sign, extract, or execute third-party installers until their artifact identity and safe acceptance environment are established. Do not turn an unavailable dependency into a guessed fallback.
6. Close each wave only when its defined exit evidence exists. A blocked host or installer gate stays `BLOCKED`; unrun checks remain `NOT TESTED`.

## Wave sequence

| Order | Wave | Workbook task IDs | Execution focus and exit gate |
|---:|---|---|---|
| 1 | CORE-00 | BL-055, BL-058, BL-059; RA-01, RA-02, RA-07, RA-08 | Pin baseline; inventory modules; finish logging/loader/smoke preparation; reconcile PHM hashes; restore REA evidence; normalize ten checkpoints. Exit only with an accepted P0 subset and required runtime/installer evidence. Host and installer gates are currently blocked. |
| 2 | RB-01 | BL-001, BL-003, BL-004, BL-008, BL-009, BL-012, BL-015; RA-03 | Reconcile Beam, Column, both Wall host contracts, shape resources, and solved preview against code. Exit requires native Revit evidence for every supported slice, physical preview/create parity, multi-host rollback, and Undo. |
| 3 | ARCH-01 | BL-018, BL-019, BL-020, BL-021; RA-06 | Verify standalone provenance, door schedule/legend identity, and envelope quantities. Reconcile wall and curtain quantities to independent model/schedule truth; distinguish missing values from zero. Keep EnergyExtractor audit tied to each package hash. |
| 4 | MEP-01 | BL-031, BL-035 | Accept Wall and Floor native cuts independently; verify clash, clearances, stale/repeat behavior, rollback, elevation tags, and Undo on disposable fixtures. |
| 5 | RB-02 | BL-002, BL-005, BL-006, BL-007, BL-011 | Close Beam parity and missing Column/Slab/Foundation/Dowel geometry against exact detailing contracts and independent golden models. |
| 6 | RB-03 | BL-010, BL-013, BL-014, BL-016 | Implement wall openings/complex reinforcement, numbering, BBS, and bar edits only after RB-02. Reconcile native bar count, lengths, shapes, marks, metadata, schedule, and rollback. |
| 7 | DOC-01 | BL-046, BL-047, BL-048, BL-049, BL-050, BL-051; RA-05 | Harden Stage 3 commands and inspect PHM manifest/DLLs only after an accepted extractor, exact installer hash, and isolated analysis environment are verified. Current parser candidates have only partial format/safety evidence and remain unaccepted; see `Docs/Research/RA-05-PHM-Observed-Binary-Extraction-Blocker.md`. Exit requires native workflow, atomic rollback, and command/API trace. |
| 8 | QS-01 | BL-039, BL-040, BL-044 | Reconcile QTO and architectural envelope using source-element trace, exact units, and independent schedule/Excel comparisons. |
| 9 | ARCH-02 | BL-022, BL-023, BL-024, BL-026, BL-029, BL-030; RA-04 | Verify room boundaries, finishes, lintels, CAD assists, and views on truth-set models; validate Revit 2025 APIs per callsite and fail closed for unsupported geometry. |
| 10 | MEP-02 | BL-032, BL-033, BL-034, BL-038 | Specify and validate Beam/Column openings, linked coordination, penetration families, and quantities. Never mutate linked documents silently; verify host ownership and transforms. |
| 11 | DOC-02 | BL-052, BL-053, BL-054 | Generate repeatable sheets/callouts and exports; verify layout, naming, content parity, templates, and preservation of protected views. |
| 12 | QS-02 | BL-041, BL-043, BL-045 | Reconcile host plus linked BOQ, link transforms/repeated instances/phases, and Rebar takeoff against native schedules and source elements. |
| 13 | CORE-01 | BL-056, BL-057, BL-060 | After CORE-00 gates, validate standalone DLL loading, workspace UX, command-ID compatibility, and explicit 2025/future API matrix. Installer and Add-in Manager evidence are both required. |
| 14 | ARCH-03 | BL-025, BL-027 | Implement tile, plaster, and material painting only after room/finish boundaries are accepted. Confirm read-back area and no phantom surfaces. |
| 15 | MEP-03 | BL-036, BL-037 | Validate connector/system QA and routing separately from opening creation; prove physical joins and reviewed fixes. |
| 16 | RB-04 | BL-017 | Implement stairs/ramps after RB-03 with approved inclined-host contracts, shape/cover checks, and native proof across host variants. |

## Workbook issues to resolve before task dispatch

The `Cong viec 68 muc` sheet assigns `BL-028` to `ARCH-04` and `BL-042` to `QS-03`, but `Ke hoach 16 Wave` has no `ARCH-04` or `QS-03` wave. The wave schedule also does not include either task ID. Keep these two items unassigned until the roadmap owner confirms placement; do not infer they belong in ARCH-03 or QS-02.

The sheet labels all 60 BL and 8 RA rows `PLANNED`; source audit shows that several feature slices already have code. For example, Beam multi-host, dual-host Wall A/B, bounded MEP openings, and Rebar preview parity have code-side records. Reconcile those slices against the current baseline and record native acceptance separately; do not rebuild them by default or report them as passed.

## CORE-00 implementation record

Current code/evidence changes in this branch:

- Add a rotating UTF-8 file sink for `KToolsLogger` and log add-in startup, shutdown, startup exceptions, and workspace-pane registration warnings.
- Add a minimal Revit 2025 read-only smoke-command project and solution entry. Its assembly has been compiled; it has not been loaded or invoked in Revit.
- Correct Rebar Shape name resolution to require an exact unique catalog name, avoiding prefix collisions such as `JP_T11` and `JP_T11a`. This closes that lookup defect only; manifest/identity code was added at `4bfbda0`, while native identity, bend/hook mapping, and shape-creation acceptance remain open.
- BL-012 code-side update at `4bfbda0c7671e52836e27dcf722fb659343ee91c`: add a versioned SHA-256 manifest for all 43 packaged Rebar Shape families; verify exact asset bytes before and after load; persist manifest/asset hashes plus the loaded shape's `UniqueId` in Extensible Storage; refuse to reuse unverified, mismatched, duplicate, or legacy-unstamped shapes. Bundle and WiX source are configured to stage the manifest in both MSI Rebar payload variants. The MSI and bootstrapper were subsequently built from source commit `fa13ee570bb4e10e7481167978d57d245dcc51c6`; see `Docs/QA/REA_MSI_BUILD_EVIDENCE.md`. They remain unsigned and uninstalled; native geometry, persistence, rollback, and compatibility remain unaccepted.
- Rebaseline current RB-01 source and acceptance gaps without rerunning historical tests; see `Docs/QA/REA_RB01_SOURCE_REBASELINE.md`. Existing implementations are retained and not relabeled as host-accepted.
- Record PHM hash separation, the Inno 6.7.0 extraction blocker, EnergyExtractor package hashes and static audit, RA-02's verified Pilot inventory, ten QA checkpoint semantics, and the user's Revit-use safety gate.

Latest compile-only evidence is recorded in `Docs/QA/REA_RB01_COMPILE_MATRIX.md`: the product DLL builds against Revit 2022–2026 API references with 0 errors. The separate read-only smoke DLL also has compile evidence, but has not been loaded or invoked in Revit. No test suite, Revit command, installer, deployment, or model mutation was run for this matrix.

CORE-00 remains open: RA-01 lacks the baseline `571A…` binary; RA-02 has no verified REA runner or REA Pilot assignment (see `Docs/Research/RA-02-REA-Engine-and-Pilot-Inventory.md`); BL-058 cannot run while the user is working in Revit and lacks a designated disposable model; BL-059 has a technically compatible extractor candidate but lacks accepted provenance, a disposable isolated analysis environment, signing authority, and a disposable installer VM; BL-055 must remain frozen until those gates and the accepted source subset are reviewed. A local unsigned installer package was built and inspected at source SHA `fa13ee5`; this closes only compile/package inspection work, not the CORE-00 installer acceptance gate. See `Docs/QA/REA_MSI_BUILD_EVIDENCE.md`.

## RA-05 research update — 2026-10-23

Static review of `scgm0/InnoUnpack.NET` commit `b221a08e7b60a6507067e246de1879ef5cf2df4b` found an upstream fixture for Inno Setup 6.7.3 / setup-data version 6.7.0 and a successful CI run on that exact commit (80 passed, 0 failed, 0 skipped). This establishes a plausible technical candidate, not accepted provenance or PHM compatibility. A targeted source review found no obvious process launch, network client, native library load, or dynamic assembly load calls in the core library and CLI; it is not a full security review. The candidate remains unapproved for extraction because the commit is unsigned, the repository has no release, and no isolated analysis environment is established. The `87DF…` PHM artifact remains unextracted and unexecuted; the `571A…` baseline remains a separate missing artifact. See the RA-05 report for evidence and limitations.

## RB-01 / BL-012 code update — 2026-10-23

Source commit `4bfbda0c7671e52836e27dcf722fb659343ee91c` adds `rebar-shape-assets.v1.sha256` for all 43 packaged `.rfa` files. The asset manifest was checked against each source file and both `net48` and `net8.0-windows` build outputs. Loading validates the file hash before and after `Document.LoadFamily`, writes a versioned Extensible Storage record on the exact `RebarShape` element, and returns the shape only when its saved `UniqueId`, asset hash, and manifest hash still match. The loader UI distinguishes verified shapes from legacy/ambiguous identities and prevents selecting unverified assets. The manifest is wired into the bundle and both MSI Rebar payloads.

This is code-side provenance and byte-integrity checking, not a signature or validation of the shape's engineering geometry. Existing models with legacy unstamped shapes will show those shapes as unverified and K-TOOLS shape-driven calls will fail closed; no automatic overwrite or re-stamp is attempted. A safe migration path and host acceptance still need review in a detached disposable model. BL-012 remains incomplete for bend/hook mapping, native shape creation, and geometry parity. No Revit command or test suite was run.

Compile-only evidence for source SHA `4bfbda0c7671e52836e27dcf722fb659343ee91c`:

| Target | Command result | Scope / limitation |
|---|---|---|
| Revit 2025 (`net8.0-windows`) | Exit 0; 10 warnings; 0 errors | Compile only; warnings remain in unrelated `ColumnWallRelatedFormV1` and `SheetGenService` code. |
| Revit 2024 (`net48`) | Exit 0; 12 warnings; 0 errors | Compile only; warnings remain in unrelated `ColumnWallRelatedFormV1`, `ElementTagsForm`, and `SheetGenService` code. |

Compile-only compatibility now passes for Revit 2022–2026; see `Docs/QA/REA_RB01_COMPILE_MATRIX.md`. Rebar Shape runtime behavior, Extensible Storage persistence, transaction rollback, MSI build/install, and host acceptance remain unverified. Wave RB-01 is not host-accepted and Wave 3 remains gated on RB-01 exit evidence.

## Installer build follow-up — 2026-10-23

The canonical `Installer/Build-Installer.ps1` completed against source commit `fa13ee570bb4e10e7481167978d57d245dcc51c6`, product version `2.7.2`, with no uncommitted source changes. It produced unsigned MSI and bootstrapper artifacts; their SHA-256 hashes and read-only MSI database manifest-row inspection are recorded in `Docs/QA/REA_MSI_BUILD_EVIDENCE.md`. No install, signing, deployment, Revit host invocation, or model mutation occurred. The artifacts prove packaging/build integrity only; installer lifecycle and RB-01 native acceptance remain open.

## Runtime QA safety-preflight update — 2026-10-23

Source checkpoint `5da3765` defers the full model fingerprint until a selected run passes preflight. Fixture actions stay disabled until the operator confirms both that KHIM (JAYK) is not working in any Revit session for the whole run and that the active project is a detached disposable QA copy. The runner enforces the same gate independently, records blocked fixtures when it fails, and reports model rollback as `NOT RUN` when no baseline snapshot exists. The dashboard opening no longer fingerprints or scans the model.

Validation at source checkpoint `5da3765`:

| Check | Result | Scope / limitation |
|---|---|---|
| `Tools/Verify-RuntimeQaHarness.ps1` | Exit 0; 60/60 static assertions passed | Source-contract audit only; no Revit fixture was executed. |
| Revit 2025 product build | Exit 0; 10 warnings; 0 errors | `dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net8.0-windows -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 -p:DeployKhimToolsBundle=false --no-restore --verbosity minimal -t:Rebuild`; warnings are existing `ColumnWallRelatedFormV1.Load()` hiding and `ElementId(int)` obsolescence warnings. |

This is harness hardening, not runtime acceptance. No Revit process, command, model, or fixture was opened or run. QA-08/QA-10 remain blocked on an identified detached disposable model and a safe user work window; compile and static audit evidence do not close them. CORE-00 and RB-01 remain open.

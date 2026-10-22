# K-TOOL REA roadmap — implementation and handoff plan

**Current stage:** CORE-00 code and evidence preparation. Roadmap acceptance is not complete.

**Source:** `KTOOL_REA_REVIEW_ROADMAP_2026-10-22 (1).xlsx`, sheets `Ke hoach 16 Wave`, `Cong viec 68 muc`, and `Thay doi baseline`. Workbook rows marked `PLANNED` are planned work, not source status or acceptance evidence.

**Implementation baseline:** source commit `18c2cc27350808390b0e7e5a383dafb38b945b40`, isolated branch `codex/rea-core00-20261023`. Keep the main checkout and user draft unchanged. Do not merge to `master` or release a production build during these waves.

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
| 7 | DOC-01 | BL-046, BL-047, BL-048, BL-049, BL-050, BL-051; RA-05 | Harden Stage 3 commands and inspect PHM manifest/DLLs only after a compatible extractor and exact installer hash are verified. Exit requires native workflow, atomic rollback, and command/API trace. |
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
- Correct Rebar Shape name resolution to require an exact unique catalog name, avoiding prefix collisions such as `JP_T11` and `JP_T11a`. This closes that lookup defect only; BL-012 still needs verified resource identity/version, bend/hook mapping, and native acceptance.
- Record PHM hash separation, the Inno 6.7.0 extraction blocker, EnergyExtractor package hashes and static audit, RA-02's verified Pilot inventory, ten QA checkpoint semantics, and the user's Revit-use safety gate.

Latest compile-only evidence is recorded in `REA_ROADMAP_10_CHECKPOINTS.md`: the smoke DLL builds with 0 warnings and 0 errors; product DLL builds with 10 warnings and 0 errors. No test suite, Revit command, installer, deployment, or model mutation has been run in this continuation.

CORE-00 remains open: RA-01 lacks the baseline `571A…` binary; RA-02 has no verified REA runner or REA Pilot assignment (see `Docs/Research/RA-02-REA-Engine-and-Pilot-Inventory.md`); BL-058 cannot run while the user is working in Revit and lacks a designated disposable model; BL-059 lacks a verified compatible extractor, signing authority, and disposable installer VM; BL-055 must remain frozen until those gates and the accepted source subset are reviewed.

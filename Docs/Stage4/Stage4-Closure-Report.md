# Stage 4.1 + 4.2 closure report

This report records source/build/harness evidence separately from execution inside Revit.
Branch: `stage4/backend-consolidation`. No push or master merge is authorized by this task.

## STATIC VERIFIED

- The former supplemental test reflected `SlabStepForm.CreateLayoutPreview`, which is not the
  current production API. Current flow is ribbon → `CmdSlabStep` → `SlabStepForm` →
  `SlabStepDetector.Scan` preview → fresh boundary fingerprint/re-resolution →
  `SlabStepService.GenerateSlabSteps` → transaction group and per-step transaction.
- `Tools/Verify-SlabStep.ps1` checks that current path and reports 40/40 source assertions.
- Stage 4 architecture audit: 61/61; Stage 3.1–3.7 structural regression: PASS (57, 49, 56,
  114, 174, 146, 271).
- `TransactionBoundary` checks transaction/group/subtransaction start, commit/assimilation,
  and rollback statuses. It is now used by Slab Step, Stage 3.1–3.5 covered paths, Modify
  Objects operation services, and Dimension create/edit/rebuild/spot-elevation paths. SlabJoin
  keeps its explicit commit status check.
- Transaction/previews are documented in `Docs/QA/Stage3-Transaction-Audit.md` and
  `Docs/QA/Stage4-Preview-Safety-Audit.md`.

## BUILD VERIFIED

| Target | Status | Errors |
| --- | --- | ---: |
| Revit 2024 / net48 | PASS | 0 |
| Revit 2025 / net8.0-windows | PASS | 0 |

Forced rebuilds report API-obsolescence warnings (230 net48 and 228 net8); this wave does not
claim warning-free builds.

## HARNESS VERIFIED

- Slab Step static acceptance: PASS, 40 checks; identifies host portion as NOT EXECUTED.
- Stage 4 architecture static acceptance: PASS, 61 checks.
- `Test-All.ps1`: PASS, 1061/1061. Stage 3.1–3.7 tests, Rebar/deployment, UI, and Stage 4
  static checks are included.
- Rebar-specific QA: configuration 36, input-state 60, layout 9235 controls + 252 renders,
  references 40 renders (schematic only; no reinforcement geometry/model execution).
- UI QA: 92 command/ribbon contracts; 100 icons, 10 XAML surfaces, 43 layout renders.
- K-QS Domain: PASS for netstandard2.0 and net8.0, 0 warnings / 0 errors.
- A Revit runtime fixture is registered for Slab Step; it requires a disposable QA model,
  adjacent floor pair, and `KTOOLS_QA_SLAB_STEP` Generic Model family. It tests repeated scan,
  no preview mutation, validation, committed creation, and batch rollback.

## REVIT HOST VERIFIED

NOT EXECUTED for both versions. Status remains DEFERRED. Do not interpret the fixture being
registered or the source audit passing as runtime acceptance.

## Current disposition

Lifecycle static acceptance is green, and both host-target builds and the complete regression
suite passed. Stage 4 still has a code-side diagnostic coverage gap: not every command result
emits the full requested record (document, normalized input summary, affected count, duration,
warning/failure counts, exception type, and verified rollback result), while Modify Objects'
shared postcondition is still mostly source-existence based. These are retained as explicit
follow-up blockers rather than represented as closed. Revit transaction behavior,
worksharing/cancellation, and model integrity remain NOT EXECUTED. No push, merge, or Stage 5
work performed.

## Current autonomous hardening pass (2026-09-23)

The following evidence supersedes the earlier test/build counts above. The code-side changes
remain uncommitted until the complete Phase 4 gate and clean-worktree check pass.

| Gate | Latest observed result |
| --- | --- |
| Revit 2025 / net8 target build | PASS, 0 errors (forced rebuild: 208 CS0618 API-obsolescence warnings) |
| Revit 2024 / net48 target build | PASS, 0 errors (forced rebuild: 210 CS0618 API-obsolescence warnings) |
| Stage 3.1–3.7 targeted structural checks | PASS (57, 49, 56, 114, 174, 168, 271) |
| Stage 4 architecture static acceptance | PASS, 61 checks |
| Stage 4 Slab Step acceptance | PASS, 40 checks |
| Stage 4 diagnostic acceptance | PASS, 8/8 production workflow entry points |
| Stage 4 transaction acceptance | PASS, 17 checks across 8 workflows and canonical result invariants |
| `Test-All.ps1` | PASS, 1113/1113 at the last complete regression run |
| K-QS Domain Release build | PASS, 0 warnings / 0 errors |
| Revit host runtime | DEFERRED / NOT EXECUTED |

Hardening in this pass includes fixed Revit 2024/2025 reference defaults and an explicit
requested/resolved mismatch verifier; Parameter Manager now checks values before committing
each item and the all-or-nothing batch; Filter Manager verifies copied state before commit;
Title Block Sync rolls back the group if its final source-preservation check fails; Modify
Objects has operation-specific postconditions; and Slab Step verifies created instances and
mapped length parameters before commit and verifies rollback cleanup. The runtime matrix and
registered fixtures remain the only host-execution path; their availability does not constitute
runtime PASS.

Warning triage follows the Phase 4 master prompt: Category A is harmless compiler/framework
compatibility, Category B is technical debt, and Category C is possible runtime risk. Forced
Revit 2024/2025 rebuilds emitted only CS0618 (`ElementId.IntegerValue` obsolete API) warnings;
these are Category B unless a call site creates a known runtime risk. The modified production
paths were audited for ElementId narrowing/casts and for the newly changed ID sort/log paths
now use the long-safe `ToLongValue()` adapter. No Category C warning was identified in the
modified production paths: `CATEGORY_C_WARNINGS = NONE`. No blanket warning cleanup was done.

# Phase 18 K-Rebar Continuation Checkpoint

Checkpoint: 2026-09-25, local-only. This records code-side evidence and open host/deployment gaps; it does not authorize or imply a release.

## Repository and latest changes

- Start of this continuation: `phase18/production-gate` at `309dccb`.
- Current code branch / source HEAD: `phase18/production-gate` / `4e0d9a8`.
- Worktree: clean after the local hardening/checkpoint commits.
- Local commits in this continuation:
  - `2c94c86 fix(rebar): localize shared validation feedback`
  - `0be0caf fix(rebar): localize circular column workflow`
  - `2985eed`, `4472249`, and `2daecdd` record/update this checkpoint and its isolated-bundle validation.
  - `9452cab fix(rebar): fail closed on candidate rollback errors`
  - `681a8d2 fix(rebar): abort on tie station rollback failure`
  - `4e0d9a8 fix(rebar): verify fixture group rollback`
  - `dcd9313 docs(rebar): record tie rollback hardening checkpoint`
- No push, merge, installer action, machine-wide deployment, or Revit model operation was performed.

The first commit localizes shared validation status, action text and error messages across Beam, rectangular Column, Slab and Foundation. The second adds a bilingual selector and complete UI-state localization to Circular Column, including preview/count text, reference/configuration pages, validation feedback and bilingual layout coverage. Circular Column was previously mixed-language and did not expose the global selector. Candidate rollback hardening verifies every candidate `SubTransaction.RollBack` result and aborts to the owner if rollback cannot be confirmed; it also aligns the canonical suite's Rebar expected count with the direct verifier result. Commit `681a8d2` applies the same fail-closed policy to rectangular-column tie-station subtransactions and prevents per-column error recovery from continuing the batch when station rollback cannot be confirmed. Commit `4e0d9a8` makes the standalone Rebar fixture command require a confirmed `TransactionGroup` rollback before reporting the fixture model unchanged; uncertain cleanup is now recorded as a failed check and an operator warning.

## Current reproducible code-side evidence

| Gate | Result |
| --- | --- |
| Revit 2024 Release build | PASS, 0 errors; 210 existing `CS0618` API-obsolescence warnings |
| Revit 2025 Release build | PASS, 0 errors; 208 existing `CS0618` API-obsolescence warnings |
| K-Rebar static acceptance | PASS, 107/107 |
| Rebar layout under Test-All's Windows PowerShell 5.1 host | PASS; 16,591 control checks, 348 renders |
| Standalone Rebar layout under PowerShell 7 | PASS; 18,327 control checks, 384 renders |
| UI layout acceptance | PASS; 100 embedded icons, 10 XAML surfaces, 43 renders |
| UI contracts | PASS; 218 assertions |
| Rebar configuration persistence | PASS; 37 checks |
| Runtime QA harness audit | PASS; 25 checks; this audits harness structure only |
| Canonical `Test-All.ps1` | PASS; 2,653/2,653 configured audits; Rebar acceptance is explicitly budgeted as 107 checks |
| `git diff --check` | PASS |

The latest source-only audit found no `TODO`, `FIXME`, `HACK`, `PLACEHOLDER`, or `NotImplementedException` markers under active Rebar production code/runtime fixtures. Foundation side ties remain an explicitly disclosed engineering gap rather than an implicit PASS. No Rebar-specific build warning or new Category C warning was identified; the repository builds still emit existing `ElementId` `CS0618` obsolescence warnings.

After `4e0d9a8`, both Release targets were rebuilt from committed source: Revit 2024 produced 210 existing `CS0618` warnings and 0 errors; Revit 2025 produced 208 existing `CS0618` warnings and 0 errors. Product version: `2.7.2+4e0d9a82d66f64def61331fa5245e649a8492e29`.

The layout verifier reports host-dependent check/render totals in PowerShell 5.1 versus PowerShell 7; each run passed in its own runtime. The Test-All total is configured-suite accounting, not Revit runtime evidence. The layout checks are offline rendering simulations, not live Windows DPI or Revit command QA.

## Runtime, host QA, and deployment truth

- Rebar host scenario definitions: 28; 0 executed; 28 remain `REGISTERED_NOT_EXECUTED` / `HOST_REQUIRED`.
- Revit host runtime: `DEFERRED`, not PASS. No safe disposable model or supported Revit host runner is available in this task; the active user model was not touched.
- Runtime fixtures are registered/compiled; their registration is not evidence that the fixtures ran.
- Deployment sanity found active Revit 2025 PID `26356` still loads `C:\ProgramData\Autodesk\ApplicationPlugins\KhimTools.bundle\Contents\Modern\KhimTools.dll`, product version `2.7.2+88b8cddcc75dae58cd6929e074465c010cb05129`, which is older than this local build.
- A fresh, versioned QA bundle was prepared at `%LOCALAPPDATA%\KhimToolsQA\2.7.2-4e0d9a8\KhimTools.bundle`. Revit 2024/Legacy SHA-256 is `9AA631EF2BD7847C650FB97283D893C629FD8444394FCE4F8C892EA58B557CF9`; Revit 2025/Modern SHA-256 is `CD70A404B778C86A384AC064F674EEFB9FC8062DD8B5C68FBDDAC18097B17759`; both match the committed build outputs. The Legacy and Modern manifests have the same AddInId, modern `.deps.json` is present, PackageContents supports R2022–R2026 with no R2027, and the bundle remains outside Autodesk discovery paths and unregistered. Earlier bundles were not modified.
- Build warnings observed are `ElementId.IntegerValue` obsolescence (`CS0618`); no RebarTool warning line was found in either build log. No new Category C warning was identified in the changed Rebar paths. This is not a claim that all historical warnings across the repository were exhaustively reclassified.

## Remaining real blockers

- Foundation side-tie detailing remains unresolved. The option is disabled by default and explicitly fails closed if requested; do not claim complete Foundation capability.
- No Wall Rebar generator/workflow is present; classify Wall as `NOT_APPLICABLE` unless an active product requirement is identified.
- Rebar semantic geometry golden/edge scenarios still require actual Revit host comparison. The registered rollback/parity fixtures have not run.
- The active Revit bundle remains stale (loaded product version `2.7.2+88b8cddcc75dae58cd6929e074465c010cb05129`). The new user-scope QA bundle matches `4e0d9a8` but is unregistered; runtime still requires a disposable QA model and explicit safe host execution.
- Code-side static/build green does not close host-runtime acceptance or the overall production gate and does not authorize push/release. The fresh QA bundle is only prepared and unregistered.

## Resume point

`NEXT_EXACT_ACTION = Continue remaining focused parity, warning/debt, responsive/localization, and transaction-boundary audits; compare current Rebar behavior against registered host scenarios and keep them NOT_EXECUTED. The isolated QA bundle is prepared but must remain unregistered unless a disposable model and safe host runner are available. Do not alter the active model or machine-wide plugin. Keep host runtime DEFERRED and stop at the 18-hour limit.`

`SAFE_TO_PUSH_FOR_REVIEW = NO` — host semantic goldens remain unexecuted, Foundation side ties remain unresolved, and the active loaded DLL is stale.

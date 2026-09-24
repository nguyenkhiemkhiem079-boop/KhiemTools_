# Phase 18 K-Rebar Continuation Checkpoint

Checkpoint: 2026-09-25, local-only. This records code-side evidence and open host/deployment gaps; it does not authorize or imply a release.

## Repository and latest changes

- Start of this continuation: `phase18/production-gate` at `309dccb`.
- Current code branch / source HEAD: `phase18/production-gate` / `0be0caf` (the checkpoint is recorded in a documentation-only commit after that code head).
- Worktree: clean at checkpoint creation.
- Local commits in this continuation:
  - `2c94c86 fix(rebar): localize shared validation feedback`
  - `0be0caf fix(rebar): localize circular column workflow`
  - `2985eed` and `4472249` record this checkpoint and clarify its code-head reference.
- No push, merge, installer action, machine-wide deployment, or Revit model operation was performed.

The first commit localizes shared validation status, action text and error messages across Beam, rectangular Column, Slab and Foundation. The second adds a bilingual selector and complete UI-state localization to Circular Column, including preview/count text, reference/configuration pages, validation feedback and bilingual layout coverage. Circular Column was previously mixed-language and did not expose the global selector.

## Current reproducible code-side evidence

| Gate | Result |
| --- | --- |
| Revit 2024 Release build | PASS, 0 errors; 210 existing `CS0618` API-obsolescence warnings |
| Revit 2025 Release build | PASS, 0 errors; 208 existing `CS0618` API-obsolescence warnings |
| K-Rebar static acceptance | PASS, 105/105 |
| Rebar layout under Test-All's Windows PowerShell 5.1 host | PASS; 16,591 control checks, 348 renders |
| Standalone Rebar layout under PowerShell 7 | PASS; 18,327 control checks, 384 renders |
| UI layout acceptance | PASS; 100 embedded icons, 10 XAML surfaces, 43 renders |
| UI contracts | PASS; 218 assertions |
| Rebar configuration persistence | PASS; 37 checks |
| Runtime QA harness audit | PASS; 25 checks; this audits harness structure only |
| Canonical `Test-All.ps1` | PASS; 2,639/2,639 configured audits |
| `git diff --check` | PASS |

After the code commits, both Release targets were rebuilt from the clean committed tree. Both DLLs report product version `2.7.2+4472249cc04d8eae2e9d5fe54f0e15cdc86f6cb0`; because `4472249` is documentation-only on top of code commit `0be0caf`, the compiled source matches `0be0caf`.

The layout verifier reports host-dependent check/render totals in PowerShell 5.1 versus PowerShell 7; each run passed in its own runtime. The Test-All total is configured-suite accounting, not Revit runtime evidence. The layout checks are offline rendering simulations, not live Windows DPI or Revit command QA.

## Runtime, host QA, and deployment truth

- Rebar host scenario definitions: 28; 0 executed; 28 remain `REGISTERED_NOT_EXECUTED` / `HOST_REQUIRED`.
- Revit host runtime: `DEFERRED`, not PASS. No safe disposable model or supported Revit host runner is available in this task; the active user model was not touched.
- Runtime fixtures are registered/compiled; their registration is not evidence that the fixtures ran.
- Deployment sanity found active Revit 2025 PID `26356` still loads `C:\ProgramData\Autodesk\ApplicationPlugins\KhimTools.bundle\Contents\Modern\KhimTools.dll`, product version `2.7.2+88b8cddcc75dae58cd6929e074465c010cb05129`, which is older than this local build.
- A new, versioned QA bundle was prepared at `%LOCALAPPDATA%\KhimToolsQA\2.7.2-4472249\KhimTools.bundle`. Its Revit 2024 and 2025 DLL SHA-256 values match the clean build outputs (`88E2177B426C331E994ACBEA7D3FD942928D73D2A0640895F62C3EF7E7BD5740` and `6580400F6A0FF29540440DE5BFC1DD8D8E5626B54CD19AADC0904F6C0E238827`); both manifests have the same AddInId, the modern `.deps.json` is present, package series are R2022–R2026, and R2027 is absent. The bundle is outside Autodesk discovery paths and remains unregistered. The older `2.7.2-309dccb` bundle was not modified.
- Build warnings observed are `ElementId.IntegerValue` obsolescence (`CS0618`); no RebarTool warning line was found in either build log. No new Category C warning was identified in the changed Rebar paths. This is not a claim that all historical warnings across the repository were exhaustively reclassified.

## Remaining real blockers

- Foundation side-tie detailing remains unresolved. The option is disabled by default and explicitly fails closed if requested; do not claim complete Foundation capability.
- No Wall Rebar generator/workflow is present; classify Wall as `NOT_APPLICABLE` unless an active product requirement is identified.
- Rebar semantic geometry golden/edge scenarios still require actual Revit host comparison. The registered rollback/parity fixtures have not run.
- The active Revit bundle and isolated QA bundle are stale against `0be0caf`; host QA cannot establish current-build runtime behavior until a safe user-scope deployment and disposable fixture model are prepared.
- Code-side green does not close the overall production gate or authorize push/release.

## Resume point

`NEXT_EXACT_ACTION = Continue focused code-side audits for preview/execute parity, warning/debt classification, and remaining responsive/localization cases; then obtain a safe disposable Revit QA model/runner before considering registration or launch of the new bundle. Do not alter the active model or machine-wide plugin. Keep host runtime DEFERRED and stop at the 18-hour limit.`

`SAFE_TO_PUSH_FOR_REVIEW = NO` — host semantic goldens remain unexecuted, Foundation side ties remain unresolved, and the active loaded DLL is stale.

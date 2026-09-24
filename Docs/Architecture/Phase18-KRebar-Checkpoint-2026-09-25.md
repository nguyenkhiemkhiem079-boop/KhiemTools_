# Phase 18 K-Rebar Continuation Checkpoint

Checkpoint: 2026-09-25, local-only. This records code-side evidence and open host/deployment gaps; it does not authorize or imply a release.

## Repository and latest changes

- Start of this continuation: `phase18/production-gate` at `309dccb`.
- Current code branch / source HEAD: `phase18/production-gate` / `681a8d2`.
- Worktree: clean after the local hardening/checkpoint commits.
- Local commits in this continuation:
  - `2c94c86 fix(rebar): localize shared validation feedback`
  - `0be0caf fix(rebar): localize circular column workflow`
  - `2985eed`, `4472249`, and `2daecdd` record/update this checkpoint and its isolated-bundle validation.
  - `9452cab fix(rebar): fail closed on candidate rollback errors`
  - `681a8d2 fix(rebar): abort on tie station rollback failure`
- No push, merge, installer action, machine-wide deployment, or Revit model operation was performed.

The first commit localizes shared validation status, action text and error messages across Beam, rectangular Column, Slab and Foundation. The second adds a bilingual selector and complete UI-state localization to Circular Column, including preview/count text, reference/configuration pages, validation feedback and bilingual layout coverage. Circular Column was previously mixed-language and did not expose the global selector. Candidate rollback hardening verifies every candidate `SubTransaction.RollBack` result and aborts to the owner if rollback cannot be confirmed; it also aligns the canonical suite's Rebar expected count with the direct 106-check verifier result. Commit `681a8d2` applies the same fail-closed policy to rectangular-column tie-station subtransactions and prevents per-column error recovery from continuing the batch when station rollback cannot be confirmed.

## Current reproducible code-side evidence

| Gate | Result |
| --- | --- |
| Revit 2024 Release build | PASS, 0 errors; 210 existing `CS0618` API-obsolescence warnings |
| Revit 2025 Release build | PASS, 0 errors; 208 existing `CS0618` API-obsolescence warnings |
| K-Rebar static acceptance | PASS, 106/106 |
| Rebar layout under Test-All's Windows PowerShell 5.1 host | PASS; 16,591 control checks, 348 renders |
| Standalone Rebar layout under PowerShell 7 | PASS; 18,327 control checks, 384 renders |
| UI layout acceptance | PASS; 100 embedded icons, 10 XAML surfaces, 43 renders |
| UI contracts | PASS; 218 assertions |
| Rebar configuration persistence | PASS; 37 checks |
| Runtime QA harness audit | PASS; 25 checks; this audits harness structure only |
| Canonical `Test-All.ps1` | PASS; 2,652/2,652 configured audits; Rebar acceptance is explicitly budgeted as 106 checks |
| `git diff --check` | PASS |

After `681a8d2`, both Release targets were rebuilt from the committed source. Refresh exact DLL product-version/hash evidence after packaging this build.

The layout verifier reports host-dependent check/render totals in PowerShell 5.1 versus PowerShell 7; each run passed in its own runtime. The Test-All total is configured-suite accounting, not Revit runtime evidence. The layout checks are offline rendering simulations, not live Windows DPI or Revit command QA.

## Runtime, host QA, and deployment truth

- Rebar host scenario definitions: 28; 0 executed; 28 remain `REGISTERED_NOT_EXECUTED` / `HOST_REQUIRED`.
- Revit host runtime: `DEFERRED`, not PASS. No safe disposable model or supported Revit host runner is available in this task; the active user model was not touched.
- Runtime fixtures are registered/compiled; their registration is not evidence that the fixtures ran.
- Deployment sanity found active Revit 2025 PID `26356` still loads `C:\ProgramData\Autodesk\ApplicationPlugins\KhimTools.bundle\Contents\Modern\KhimTools.dll`, product version `2.7.2+88b8cddcc75dae58cd6929e074465c010cb05129`, which is older than this local build.
- A new, versioned QA bundle was prepared at `%LOCALAPPDATA%\KhimToolsQA\2.7.2-9452cab\KhimTools.bundle`. Its Revit 2024 and 2025 DLL SHA-256 values match the clean build outputs (`A8C794A0C78FD4D420D02D7B61820D6B8A75AEA6C091B265D9AD3B86EE418AE5` and `E58E1C8FDAB42B9281415EEF4476E7B85129DA9E68854563F6C2BA1C8716D6E9`); both manifests have the same AddInId, the modern `.deps.json` is present, package series are R2022–R2026, and R2027 is absent. The bundle is outside Autodesk discovery paths and remains unregistered. Earlier `2.7.2-309dccb` and `2.7.2-4472249` bundles were not modified.
- Build warnings observed are `ElementId.IntegerValue` obsolescence (`CS0618`); no RebarTool warning line was found in either build log. No new Category C warning was identified in the changed Rebar paths. This is not a claim that all historical warnings across the repository were exhaustively reclassified.

## Remaining real blockers

- Foundation side-tie detailing remains unresolved. The option is disabled by default and explicitly fails closed if requested; do not claim complete Foundation capability.
- No Wall Rebar generator/workflow is present; classify Wall as `NOT_APPLICABLE` unless an active product requirement is identified.
- Rebar semantic geometry golden/edge scenarios still require actual Revit host comparison. The registered rollback/parity fixtures have not run.
- The active Revit bundle and previously prepared isolated QA bundles are stale against `681a8d2`; host QA cannot establish current-build runtime behavior until a safe user-scope deployment and disposable fixture model are prepared.
- Code-side green does not close the overall production gate or authorize push/release.

## Resume point

`NEXT_EXACT_ACTION = Continue the remaining focused code-side audits for preview/execute parity, warning/debt classification, responsive/localization cases, and remaining transactional paths; refresh the isolated QA bundle from 681a8d2 after code-side review. Obtain a safe disposable Revit QA model/runner before considering registration or launch. Do not alter the active model or machine-wide plugin. Keep host runtime DEFERRED and stop at the 18-hour limit.`

`SAFE_TO_PUSH_FOR_REVIEW = NO` — host semantic goldens remain unexecuted, Foundation side ties remain unresolved, and the active loaded DLL is stale.

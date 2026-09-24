# Phase 18 K-Rebar Continuation Checkpoint

Checkpoint: 2026-09-25, local-only. This records code-side evidence and open host/deployment gaps; it does not authorize or imply a release.

## Repository and latest changes

- Start of this continuation: `phase18/production-gate` at `309dccb`.
- Current branch / HEAD: `phase18/production-gate` / `0be0caf`.
- Worktree: clean at checkpoint creation.
- Local commits in this continuation:
  - `2c94c86 fix(rebar): localize shared validation feedback`
  - `0be0caf fix(rebar): localize circular column workflow`
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

The layout verifier reports host-dependent check/render totals in PowerShell 5.1 versus PowerShell 7; each run passed in its own runtime. The Test-All total is configured-suite accounting, not Revit runtime evidence. The layout checks are offline rendering simulations, not live Windows DPI or Revit command QA.

## Runtime, host QA, and deployment truth

- Rebar host scenario definitions: 28; 0 executed; 28 remain `REGISTERED_NOT_EXECUTED` / `HOST_REQUIRED`.
- Revit host runtime: `DEFERRED`, not PASS. No safe disposable model or supported Revit host runner is available in this task; the active user model was not touched.
- Runtime fixtures are registered/compiled; their registration is not evidence that the fixtures ran.
- Deployment sanity previously found the active Revit 2025 process loaded an older machine-wide bundle than the current local build. The existing isolated user-scope QA bundle is also based on an older checkpoint. Neither was replaced or registered in this continuation.
- Build warnings observed are `ElementId.IntegerValue` obsolescence (`CS0618`); no RebarTool warning line was found in either build log. No new Category C warning was identified in the changed Rebar paths. This is not a claim that all historical warnings across the repository were exhaustively reclassified.

## Remaining real blockers

- Foundation side-tie detailing remains unresolved. The option is disabled by default and explicitly fails closed if requested; do not claim complete Foundation capability.
- No Wall Rebar generator/workflow is present; classify Wall as `NOT_APPLICABLE` unless an active product requirement is identified.
- Rebar semantic geometry golden/edge scenarios still require actual Revit host comparison. The registered rollback/parity fixtures have not run.
- The active Revit bundle and isolated QA bundle are stale against `0be0caf`; host QA cannot establish current-build runtime behavior until a safe user-scope deployment and disposable fixture model are prepared.
- Code-side green does not close the overall production gate or authorize push/release.

## Resume point

`NEXT_EXACT_ACTION = Inspect the existing isolated user-scope QA bundle and the current 2024/2025 build outputs; prepare a new versioned, unregistered QA bundle from HEAD 0be0caf only if its manifest/dependency payload can be validated without changing Revit discovery or the active model. Then continue the focused code-side audits for preview/execute parity, warning/debt classification, and remaining responsive/localization cases. Keep host runtime DEFERRED and stop at the 18-hour limit.`

`SAFE_TO_PUSH_FOR_REVIEW = NO` — host semantic goldens remain unexecuted, Foundation side ties remain unresolved, and the active loaded DLL is stale.

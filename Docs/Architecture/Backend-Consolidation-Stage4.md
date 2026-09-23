# K-TOOLS Stage 4 Backend Consolidation

Status: working checkpoint on `stage4/backend-consolidation`  
Baseline: Stage 3.7 (`eba9339825eb1a7d26505c646e7dc90f8c9948c3`)  
Scope: consolidation and hardening only; no new user-facing feature behavior.

## Target operating rules

- Plans are built from stable request data and are fingerprinted before model mutation.
- Preflight is allowed to block an operation; execution must not silently downgrade a failed mutation.
- Verification is explicit and reports an outcome plus diagnostics.
- Revit API objects belong at the adapter/executor boundary. Neutral contracts must use IDs,
  unique IDs, primitive values, and immutable snapshots.
- A transaction policy is part of the operation contract: atomic, per-item isolation, or
  batch all-or-nothing. Failure processors may only remove explicitly approved benign warnings.
- UI code gathers input and renders a result; it does not own workflow policy, logging, or
  failure recovery.

## Baseline inventory

The inventory was generated from the production tree (`KhimTools/Tools` and `src`) on the
Stage 3.7 baseline. Counts are intentionally recorded so future audits can detect drift:

| Signal | Baseline |
| --- | ---: |
| C# files under `KhimTools/Tools/KhimGen` | 340 |
| Revit command/application entrypoint files in KhimGen | 37 |
| KhimGen files containing a `Transaction` call | 79 |
| KhimGen files containing an empty `catch { }` | 19 |
| Legacy generic warning preprocessor | 1 (`SlabJoin`) |
| Rebar failure processor that records failures and rolls back errors | 1 |
| Domain projects referencing Autodesk.Revit | 0 |

The transaction-file count is a risk indicator, not a defect count: legacy rebar and
geometry tools still contain transaction ownership in forms/services. Modern Stage 3
workflows are inventoried below and are the migration target.

## Workflow architecture matrix

Legend: **GOOD** = contract is present and separated; **PARTIAL** = useful separation exists
but live API state, UI ownership, or inconsistent outcomes remain; **LEGACY** = command/form
centric; **CRITICAL** = unsafe failure or transaction behavior requires hardening before an
internal API is exposed.

| Workflow | Request | Plan | Preflight | Executor | Verify/result | Fingerprint | Transaction/failure | UI leakage | API readiness | Risk |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Wave 1.1 SheetGen | PARTIAL | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |
| Wave 1.2 ViewportAlign | PARTIAL | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |
| Wave 1.3 DetailNumber | PARTIAL | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |
| Wave 1.4 TextAlign | PARTIAL | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |
| Wave 1.5 ElementTags | PARTIAL | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |
| Wave 1.6 SheetExport | PARTIAL | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |
| Stage 3.1 SheetCopy | GOOD | GOOD | GOOD | GOOD | GOOD | GOOD | PARTIAL | LOW | PARTIAL | P2 |
| Stage 3.2 ScheduleSplit | GOOD | GOOD | GOOD | GOOD | GOOD | GOOD | PARTIAL | LOW | PARTIAL | P2 |
| Stage 3.3 TitleBlockSync | GOOD | GOOD | GOOD | GOOD | GOOD | GOOD | PARTIAL | LOW | PARTIAL | P2 |
| Stage 3.4 FilterManager | GOOD | GOOD | GOOD | GOOD | GOOD | GOOD | PARTIAL | LOW | PARTIAL | P2 |
| Stage 3.5 ParameterManager | GOOD | GOOD | GOOD | GOOD | PARTIAL | GOOD | PARTIAL | LOW | PARTIAL | P1 |
| Stage 3.6 ModifyObjects | PARTIAL | GOOD | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | MEDIUM | PARTIAL | P1 |
| Stage 3.7 DimensionTools | GOOD | GOOD | PARTIAL | GOOD | PARTIAL | GOOD | PARTIAL | LOW | PARTIAL | P1 |
| RebarTool | PARTIAL | PARTIAL | PARTIAL | PARTIAL | GOOD | GOOD | GOOD for errors / PARTIAL for warnings | HIGH in legacy forms | PARTIAL | P0 |
| SlabJoin | PARTIAL | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | GOOD for unknown errors / PARTIAL for legacy batching | HIGH | NOT_READY | P1 |
| GridLevel / Grid Generator | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | MEDIUM | PARTIAL | P1 |
| SectionCut | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | MEDIUM | PARTIAL | P1 |
| QuantityTakeoff / K-QS | GOOD domain boundary | GOOD snapshots | GOOD | PARTIAL Revit adapter | GOOD snapshot result | GOOD | PARTIAL | LOW in domain | PARTIAL | P1 |
| MEP openings | LEGACY | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |
| Architectural tools | LEGACY | LEGACY | PARTIAL | LEGACY | PARTIAL | PARTIAL | LEGACY | HIGH | NOT_READY | P1 |

The matrix is deliberately conservative. `PARTIAL` means at least one API-readiness gate
is incomplete. No row is classified READY until the complete typed contract, detached plan,
preflight, diagnostics, transaction ownership, verification, and stale-plan requirements are
demonstrated together.

The previous static audit used token-presence checks and regex heuristics; those counts do not
prove plan portability or transaction safety. The current focused audit checks detached
ParameterManager, ModifyObjects, and DimensionTools plan shapes, while runtime validation and
consistent commit-status checking remain open as listed in the debt register.

## Modern Stage 3 chain

The preserved chain is:

`request -> collect -> plan -> preflight -> execute -> verify -> result`

Stage 3.1 through 3.7 retain this chain and their stage-specific regression suites. Stage 4
adds neutral diagnostics, outcomes, metrics, identity, fingerprint, logging, progress, and
transaction/failure policy contracts around the chain. It does not merge the modules into a
god manager and does not move Revit API behavior into the domain assembly.

## Known hardening targets

1. The generic SlabJoin warning/error swallow was removed from production call sites. The
   compatibility type remains deprecated and uses the empty allow-list policy; a later pass
   can delete it once downstream references are proven absent.
2. Keep the Rebar failure processor's error rollback behavior and expose its records through
   the shared outcome/diagnostic shape.
3. Route backend diagnostics through a non-UI logger. `TaskDialog` remains a presentation
   concern and is not a logging primitive.
4. Centralize feet/mm conversion and tolerance constants at the Revit adapter boundary.
5. Treat document identity and plan fingerprints as first-class preflight inputs. A stale
   plan must be blocked before execution.
6. Inventory and retire empty catches in backend paths; cleanup-only UI/deployment catches
   may remain when their intent is documented.

## Acceptance boundary

Stage 4 is complete only when the architecture audit and static verification scripts pass,
Stage 3 and Wave 1 regressions remain green, both Revit builds remain at zero errors, and the
working tree contains only intentional local Stage 4 commits. Runtime acceptance remains
deferred unless a Revit host session is explicitly available.

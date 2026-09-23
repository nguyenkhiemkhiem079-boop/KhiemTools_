# Internal API Readiness

Readiness is assessed against a small internal adapter contract: stable request, deterministic
plan/fingerprint, preflight gate, explicit transaction policy, observable diagnostics, and
verification against the same document identity. `READY` does not imply runtime acceptance.

| Surface | Readiness | Boundary | Conditions before reuse |
| --- | --- | --- | --- |
| Stage 3.1 SheetCopy | READY | request/plan/executor/verify services | use shared diagnostics and transaction policy |
| Stage 3.2 ScheduleSplit | READY | request/plan/executor/verify services | stale-plan and failure policy wiring |
| Stage 3.3 TitleBlockSync | READY | request/plan/executor/verify services | map result statuses to shared outcomes |
| Stage 3.4 FilterManager | READY | request/plan/preflight/executor/verify | keep template-controlled views blocked |
| Stage 3.5 ParameterManager | READY | request/plan/preflight/executor/verify | keep writable/type/instance checks in preflight |
| Stage 3.6 ModifyObjects | READY | snapshot/plan/preflight/executor/result | preserve per-operation transaction semantics |
| Stage 3.7 DimensionTools | READY | plan/preflight/executor/verify/adapters | move live context resolution behind adapter |
| RebarTool | PARTIAL | specialized failure records exist | migrate legacy form-owned transactions |
| GridLevel / Grid Generator | PARTIAL | services and QA exist | unify outcomes, units, and failure reporting |
| SectionCut | PARTIAL | generator/preflight seams exist | remove empty catches and generic failure handling |
| QuantityTakeoff / K-QS | PARTIAL | Revit-free Domain snapshots | add a stable Revit application port |
| SlabJoin | NOT_READY | legacy service/form path | replace generic failure swallowing |
| Wave 1.1–1.6 | NOT_READY | command/form-centric | add adapter seams without changing behavior |
| MEP / Architectural legacy tools | NOT_READY | direct command/service mutation | staged migration after Stage 4 inventory |

## Contract shape

The shared infrastructure intentionally stays small:

`request -> plan (fingerprint) -> preflight (diagnostics) -> execute (policy) -> verify
(identity/fingerprint) -> result (outcome/metrics)`

No internal API should accept a `Window`, display a dialog, or silently catch a model failure.
Revit objects may be used inside an adapter/executor, but a plan crossing the boundary must be
reconstructable from stable identifiers and primitive snapshots.

# Internal API Readiness

## Phase 13 implementation boundary

The in-process allow-list now exposes one verified capability: `sheet-export.naming-preview`.
Its typed `SheetNamingPreviewRequest` delegates filename expansion and validation to
`NamingPlanService.Expand`, the same service used by the Sheet Export UI and workflow. It is
read-only, performs no filesystem or document mutation, and requires `UnitSystem.NotApplicable`
explicitly. The dispatcher rejects unknown IDs, missing/unspecified units, unavailable host
requirements, handler exceptions, and failed postconditions. It provides no network listener,
reflection-based dispatch, shell, PowerShell, or arbitrary code execution surface.

This is a deliberately small Phase 13 internal API implementation, not a claim that the modules
below are all API-ready or that Revit-host execution has been verified. Phase 14 wraps this
allow-list with the stateless MCP JSON-RPC and bounded local stdio adapters; no listener or remote
service is published. Add further capabilities only when their production service and contract
have independent acceptance coverage.

Readiness is assessed against a small internal adapter contract: stable request, deterministic
plan/fingerprint, preflight gate, explicit transaction policy, observable diagnostics, and
verification against the same document identity. `READY` does not imply runtime acceptance.

| Surface | Readiness | Boundary | Conditions before reuse |
| --- | --- | --- | --- |
| Stage 3.1 SheetCopy | PARTIAL | request/plan/executor/verify services | verify diagnostics and transaction failure outcomes before reusable API exposure |
| Stage 3.2 ScheduleSplit | PARTIAL | request/plan/executor/verify services | verify stale-plan and failure-policy wiring under runtime model cases |
| Stage 3.3 TitleBlockSync | PARTIAL | request/plan/executor/verify services | verify shared outcome mapping and transaction failure cases |
| Stage 3.4 FilterManager | PARTIAL | request/plan/preflight/executor/verify | verify template-controlled view blocks and mutation outcome reporting |
| Stage 3.5 ParameterManager | PARTIAL | detached request snapshot/plan/preflight/executor/verify | runtime verification and diagnostics wiring remain incomplete |
| Stage 3.6 ModifyObjects | PARTIAL | detached value-snapshot plan/preflight/executor/result | check commit status consistently across operation services |
| Stage 3.7 DimensionTools | PARTIAL | detached plan/reference snapshots/preflight/executor/verify | edit and spot-elevation paths need consistent commit-status verification |
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

# Stage 4 Technical-Debt Register

This register separates known risk from the Stage 4 consolidation work. Items are not new
feature requests; they describe seams that must be hardened before reuse as an internal API.

| ID | Module | Severity | Description | Reason deferred | Recommended phase | Risk |
| --- | --- | --- | --- | --- | --- | --- |
| TD-001 | SlabJoin | P1 | Deprecated generic failure wrapper remains, with no current production construction sites. | Retain compatibility until downstream/reflection references are proven absent. | Stage 4 follow-up | Low; unknown failures remain rollback-capable. |
| TD-002 | Rebar | P1 | Legacy forms and a command still own model transactions; selected commits now check final status. | Moving all orchestration could alter proven geometry behavior and needs runtime QA. | Stage 5 | Medium; transaction ownership and UI coupling limit reuse. |
| TD-003 | Production backend | P1 | Empty and unreported catches remain outside targeted paths. | Path-level audit and runtime validation are needed before bulk changes. | Stage 4 follow-up | Medium; some failures may lack structured diagnostics. |
| TD-004 | Modern workflows | P1 | Shared logger exists, but diagnostics are not consistently emitted by every result. | Transaction outcome correctness was prioritized in this pass. | Stage 4 follow-up | Medium; callers may still parse legacy messages. |
| TD-005 | Units and tolerances | P2 | Older tools retain literal conversion factors and module-local tolerances. | Some values have domain-specific meaning and lack sufficient regression coverage. | Stage 5 | Low to medium; duplicated constants may drift. |
| TD-006 | ModifyObjects | P1 | Detached plan context is now used, but multiple operation services still do not validate Transaction.Commit() status before reporting success. | Broad transaction-result hardening needs per-operation rollback semantics and regression coverage. | Stage 4 follow-up | Medium; a failed commit could otherwise be reported as a successful modification. |
| TD-007 | Wave 1.1–1.6 | P1 | Wave workflows remain command/form-centric without consistent detached plans, preflight, and verification. | Behavior-preserving adapter work is a separate migration wave. | Stage 5 | Medium; workflows are hard to call and audit uniformly. |
| TD-008 | DimensionTools | P1 | Plans retain detached stable-reference snapshots; edit/spot-elevation paths still need consistent commit-status checks and model-level verification. | Additional model fixtures are needed to validate edit semantics. | Stage 4 follow-up | Medium; end-state evidence for edits remains incomplete. |
| TD-009 | Legacy workflows | P2 | Some legacy fingerprints still concatenate API-derived values. | These fingerprints are not all part of modern plan pipelines. | Stage 5 | Low to medium; collision/order sensitivity can weaken staleness checks. |
| TD-010 | Runtime QA | P2 | Runtime acceptance is deferred; no Revit host fixture run occurred in this pass. | No host execution was performed in this environment. | Release gate | Medium; static/build checks cannot prove live behavior. |
| TD-011 | K-QS | P2 | Domain remains Revit-free; the Revit application adapter contract is incomplete. | K-QS stays separate from the Revit workflow core by design. | Stage 5 | Low; adapter boundary needs validation. |

Severity means release risk, not implementation order. P0 items block reuse of the affected
workflow; P1 items block broad consolidation; P2 items are follow-up hardening.

## Silent-catch audit disposition

The exact-empty-catch regex does not classify broad catches that log or return fallbacks, so a
repository-wide classified count is not claimed here. Targeted high-risk paths were hardened;
remaining backend catches are tracked under TD-003 until the path-level audit records file,
line, category, severity, and rationale.

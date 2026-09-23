# Stage 4 Technical-Debt Register

This register separates known risk from the Stage 4 consolidation work. Items are not new
feature requests; they describe seams that must be hardened before reuse as an internal API.

| ID | Priority | Area | Debt / risk | Required treatment | Target |
| --- | --- | --- | --- | --- | --- |
| TD-001 | P0 | SlabJoin | `SwallowWarningsPreprocessor` deletes every warning and attempts to resolve/delete elements for errors, with an empty catch. | Replace with explicit benign-warning allow-list; record unknown failures and roll back. | Stage 4 hardening |
| TD-002 | P0 | Rebar | Several legacy forms still own large model transactions. | Keep the existing error-rollback processor; migrate orchestration behind an executor boundary before public reuse. | Stage 4/5 |
| TD-003 | P1 | Backend | Empty catches obscure failed reads, geometry, and cleanup outcomes. | Replace backend empty catches with diagnostics or narrowly documented cleanup handling. | Stage 4 |
| TD-004 | P1 | Logging | Diagnostics are split between `Debug.WriteLine`, dialogs, and ad-hoc strings. | Use the shared structured logger; UI may render returned diagnostics only. | Stage 4 |
| TD-005 | P1 | Units | Multiple tools use literal `304.8` conversions. | Centralize Revit-unit conversion and migrate high-traffic adapters. | Stage 4 |
| TD-006 | P1 | Plans | Some plans retain `Document`, `View`, or live `ElementId` collections. | Keep stable IDs/snapshots in neutral contracts; resolve live objects only in adapters/executors. | Stage 4/5 |
| TD-007 | P1 | Wave 1 | Wave 1.1–1.6 remain command/form-centric and do not consistently expose plan/preflight/verify seams. | Preserve behavior and add adapter seams before internal API exposure. | Stage 5 |
| TD-008 | P1 | SectionCut/GridLevel | Workflow outcomes and verification are not uniform across older services. | Map existing statuses to shared outcomes and add explicit verification diagnostics. | Stage 4/5 |
| TD-009 | P2 | Fingerprints | Several legacy fingerprints concatenate API object `ToString()` values. | Use deterministic primitive tokens and the shared SHA-256 builder. | Stage 4/5 |
| TD-010 | P2 | Runtime QA | Runtime acceptance is deferred because no Revit host session is attached. | Run the existing fixture/registry suites in a host before release. | Release gate |
| TD-011 | P2 | K-QS | Domain separation is clean, but Revit adapter/API readiness is incomplete. | Keep Domain Revit-free and formalize an application-port adapter. | Stage 5 |

Priority means release risk, not implementation order. P0 items block public/internal reuse of
the affected workflow; P1 items block broad consolidation; P2 items are follow-up hardening.

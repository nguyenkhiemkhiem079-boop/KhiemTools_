# Stage 4 preview safety audit

Source-level disposition only; Revit host tests remain NOT EXECUTED.

| Tool | Current preview mechanism | Safety observation |
| --- | --- | --- |
| Slab Step | `SlabStepDetector.Scan` reads floor geometry and returns in-memory candidates/curves; the form fingerprints selected floors/boundaries and re-resolves them before generation | No preview elements are created. Batch execution has an explicit group and rollback on a later invalid item. |
| Dimension Tools | Plan/reference snapshots and geometric validation before `DimensionExecutionService` | Preview is data-only; actual dimensions are created/edited only in execution transactions. |
| Modify Objects | `ModifyObjectPlan` + detached context, `PreviewOnly` exits before opening a transaction | No preview write in the shared execution entry point; operation-specific runtime proof remains required. |
| Parameter Manager | Plan and proposed-value result data; preview command/form does not call mutation service | No preview write identified in the covered command path. |
| Reinforcement / Rebar | Existing QA fixture runs under `RuntimeQaFixtureBase` transaction group and rolls back; production preview paths were not exhaustively proven across legacy forms | QA fixture cleanup is deterministic; do not generalize to every legacy reinforcement screen without host audit. |
| Quantity Takeoff / QS | Model scanner and domain calculation produce detached quantity rows; preview/reporting is not an element-creation workflow in the covered path | Read-only by source inspection; no live document mutation test performed. |

Acceptance bar: a UI preview is not sufficient evidence by itself. Runtime checklist requires a
before/after model fingerprint around Preview and Cancel for each command, and should include
views, sheets, element IDs/counts, and tool-specific values. Any preview relying on a real
element plus cleanup must fail review unless its temporary transaction rollback is guaranteed
and verified.

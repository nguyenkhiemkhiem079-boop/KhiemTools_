# Stage 3.1–3.7 transaction audit (Stage 4.2)

Scope: source-level audit on `stage4/backend-consolidation`. This is not evidence of live
Revit behavior. `TransactionBoundary` checks lifecycle status and throws a diagnostic when a
start, commit, group assimilation, or rollback does not reach its required status.

| Feature | Command → mutation owner | Transaction structure / commit | Rollback and failure | Cancel / preview / partial risk | Disposition |
| --- | --- | --- | --- | --- | --- |
| 3.1 Sheet Copy | `CmdSheetCopy` → `SheetCopyExecutionService.ExecuteOne` | Batch `TransactionGroup`; one `Transaction` per sheet; checked start/commit and group assimilation | Target exception rolls back its transaction; start/commit/rollback failures are surfaced; batch continues per target and then assimilates successful targets | Dialog cancel occurs before Execute. Plan/preview is snapshot-only. Per-target partial success is intentional and returned per item. | STATIC VERIFIED; Revit runtime NOT EXECUTED |
| 3.2 Split Schedule | `CmdScheduleSplit` → `ScheduleSplitExecutionService` | Batch group with per-duplicate/split/place transactions; checked starts and commits | Item exceptions/status failures roll back that item; group rollback path is checked | User cancellation occurs before execute; preview uses planned data. Batch semantics permit independent item outcomes. | STATIC VERIFIED; Revit runtime NOT EXECUTED |
| 3.3 Title Block Sync | `CmdTitleBlockSync` → `TitleBlockSyncExecutionService` | Batch group and per-target transactions; checked starts/commits; target verification and final source verification both precede assimilation | Failed target rolls back its transaction; final source mismatch rolls back the enclosing group and clears affected-success counts | Cancel is form-level before execute; plan snapshot does not write. Per-target outcomes can be partial by design. | STATIC VERIFIED; Revit runtime NOT EXECUTED |
| 3.4 Filter Manager | `CmdFilterManager` → `FilterExecutionService` | One transaction per target view; checked start/commit; filter membership/state/order and source fingerprint verified before commit | Target exceptions and postcondition failures roll back target; failures reported per target | Preview/cancel are before execute; filter reads do not mutate. Independent targets may partially succeed. | STATIC VERIFIED; Revit runtime NOT EXECUTED |
| 3.5 Parameter Manager | `CmdParameterManager` → `ParameterManagerExecutionService` | Default path: group plus one transaction per item; each value is verified before commit. Strict option: one group/transaction; the complete set is verified before commit. | Per-item failure explicitly rolls back that item; strict failure rolls back group and clears committed counts | Preview and cancel precede write. Default partial update is explicit; strict mode is atomic at the group boundary. | STATIC VERIFIED; Revit runtime NOT EXECUTED |
| 3.6 Modify Objects | `CmdModifyObjects` → operation service, wrapped by `ModifyObjectExecutionService` | Outer group; operation-specific transaction, and Parts uses per-element subtransactions. Every mutating operation now has a registered operation-specific postcondition; Slab Split is explicit no-mutation capability refusal. | Failed/preflight/postverify operation rolls back group; complete element-ID delta and source fingerprints are checked after rollback | PreviewOnly returns before group. Cancel precedes Execute. Host geometry/identity and worksharing still require the registered Revit fixture. | TRANSACTION AND POSTCONDITION STATIC VERIFIED; runtime NOT EXECUTED |
| 3.7 Dimension Tools | `CmdDimensionTools` → create/edit/rebuild/elevation services | Create uses group + transaction and checks transaction commit. Text move, rebuild and spot elevation now use checked `TransactionBoundary`; rebuild operations replace source only after preverification. | Creation/edit/rebuild failures roll back transaction/group. Locked, pinned, stale references, unsupported constraints are preflight-blocked in their specific paths. | Preview planning uses reference/value snapshots; no production model write in preview. Exact live edit and rollback behavior still needs model fixture runs. | TRANSACTION LIFECYCLE STATIC VERIFIED; host runtime NOT EXECUTED |

## Failure cases and limits

The shared boundary standardizes Revit lifecycle statuses; it does not make every business
failure predictable. Deleted/invalid IDs, unsupported geometry, invalid storage, pinned or
read-only state, groups/design options, worksharing ownership, duplicate names, and unavailable
types must still be preflighted or reported by the operation's Revit API exception/status.
No blanket failure preprocessor is added here. `KnownWarningFailurePreprocessor` is allow-list
based and errors/corruption are not deleted or suppressed. User cancellation happens in the
UI/pick phase before execution for these workflows; host cancellation/failure processing still
requires runtime validation. Worksharing ownership and central-model contention are NOT
EXECUTED in this environment.

## Stage 3 runtime matrix

| Feature | Status | Evidence required |
| --- | --- | --- |
| Sheet Copy | NOT EXECUTED | See runtime checklist; inspect sheets/views/schedules and Undo |
| Split Schedule | NOT EXECUTED | Confirm segments, ordering, values, and Undo |
| Title Block Sync | NOT EXECUTED | Confirm target values, source unchanged, and Undo |
| Filter Manager | NOT EXECUTED | Confirm target filters/overrides/order and Undo |
| Parameter Manager | NOT EXECUTED | Confirm value/storage/unit and strict rollback cases |
| Modify Objects | NOT EXECUTED | Per-operation geometry, constraints, host, and Undo |
| Dimension Tools | NOT EXECUTED | Create/edit/rebuild, exact references, dimensions, and Undo |

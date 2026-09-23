# Stage 4 Revit host runtime acceptance

Status at this checkpoint: NOT EXECUTED in Revit 2024 or Revit 2025. Static tests and builds
are not host-runtime evidence. Run in a disposable, detached local QA model, never the only
copy of a production model. Keep Revit version results separate.

For each feature/version, record `PASS`, `FAIL`, `BLOCKED`, or `NOT EXECUTED`; attach a report,
screenshots where useful, model fingerprint before/after, and Revit journal/log excerpt without
unnecessary local path disclosure.

## Common protocol

1. Open the disposable test model and record version, model identity (non-sensitive), active
   view, and initial element/view/sheet fingerprint.
2. Launch the command; enter a valid operation; request preview; compare fingerprint; cancel;
   compare again. Any mutation is FAIL.
3. Reopen command, execute the valid operation, verify expected IDs/counts/values/relationships,
   then Undo and confirm restoration.
4. Repeat to detect duplicates. Try an invalid/stale/pinned/read-only input appropriate to the
   tool and confirm a useful failure result and no partial mutation.
5. Close without saving, reopen the detached model, and verify integrity. Save evidence outside
   the model only after verifying the model path is a disposable QA copy.

## Required feature matrix

| Feature | Revit 2024 | Revit 2025 | Focus / required fixture |
| --- | --- | --- | --- |
| Sheet Copy | NOT EXECUTED | NOT EXECUTED | Sheet with title block, regular/legend viewports, schedule, safe annotation; source unchanged |
| Split Schedule | NOT EXECUTED | NOT EXECUTED | Split and place schedule; verify segment count/order/data and rollback |
| Title Block Sync | NOT EXECUTED | NOT EXECUTED | Multiple targets, protected/read-only value, source unchanged |
| Filter Manager | NOT EXECUTED | NOT EXECUTED | View with filters, overrides, order, and unsupported target case |
| Parameter Manager | NOT EXECUTED | NOT EXECUTED | writable/read-only, wrong storage, unit-aware value, partial and all-or-nothing modes |
| Modify Objects | NOT EXECUTED | NOT EXECUTED | Representative move, split, join, part, opening; inspect constraints and replacement identity |
| Dimension Tools | NOT EXECUTED | NOT EXECUTED | Create chain, move text, spot elevation, rebuild, stale/pinned/locked refusal |
| Slab Step | NOT EXECUTED | NOT EXECUTED | Two adjacent floors, QA family `KTOOLS_QA_SLAB_STEP` with writable instance Double `h`; scan twice, cancel, create, inject invalid later boundary, verify group rollback |

Suggested result record per cell: `Status =`; date/time; operator; model fixture ID; command;
expected/actual; before/after fingerprint; transaction/rollback status; warnings/failures;
evidence location; notes. Never fill a cell with PASS from source inspection or a unit test.

# Phase 15 — Revit Host Scenario Catalog

The machine-readable source of truth is [Phase15-Host-Scenario-Catalog.json](Phase15-Host-Scenario-Catalog.json).
It enumerates the 30 registered Revit QA fixtures for both supported host versions, 11 explicit
manual Rebar actions, and four deployment actions that require an isolated Windows VM. Fixture
scenarios start as `NOT_EXECUTED`; deployment-only actions remain `MANUAL_REQUIRED`. Catalog
registration, compilation, and source acceptance are not Revit execution evidence.

The existing `CmdRuntimeQa` add-in and synchronous transaction-group rollback runner are the
available host mechanism. Fixture requirements are enforced by each fixture's `CanRun` checks;
Revit 2024/2025 local, detached, writable, non-workshared project copies are required. The
dashboard now requires an explicit disposable-copy confirmation, and the guard rejects an
unconfirmed run before a fixture opens a transaction. Binary RVT files were not fabricated.

Environment inspection found Revit 2025 running on a non-QA project and existing journal files for
that session. That model/journal was left untouched and was not replayed. RevitTestFramework was
not found in the standard Autodesk/RevitTestFramework install locations checked. The source QA
command remains the smallest justified harness; use it only from an explicitly opened disposable
test model. Install/upgrade/repair/uninstall acceptance remains assigned to a disposable VM, never
the user's workstation.

Host evidence remains deferred. No scenario is marked PASS in the catalog until it has an actual
Revit-versioned host run with transaction result, postcondition, affected elements, warnings,
errors, and (for create workflows) Undo result recorded.

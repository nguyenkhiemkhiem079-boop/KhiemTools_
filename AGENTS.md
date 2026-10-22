# K-TOOLS agent instructions

These instructions apply to the whole repository and to every coding agent working here. Read `AGENT_WORKFLOW.md` for task splitting, assignment, handoff, and integration rules. Before changing a subsystem, read its local specifications and project guide.

## Coordinator and ownership

- Codex is the task coordinator: clarify acceptance criteria, inspect the current branch, split work, keep the task ledger, integrate changes, and report what was verified.
- Assign one primary writer per task and reserve its files or subsystem before work starts. Agents must not edit the same working tree or overlapping files concurrently. Use separate branches/worktrees for parallel code changes; use analysis-only tasks when isolation is unavailable.
- Every agent must read this file and `AGENT_WORKFLOW.md`, then the relevant local guide/spec. Return a diff or commit, changed paths, verification performed, assumptions, and unresolved questions.
- Do not merge, push, publish a release, or deploy an installer as part of a task unless that outcome was explicitly requested.

## Repository map

- `KhimTools/`: Revit add-in, commands, UI, tools, resources, and deployment assets.
- `src/KhimTools.Domain/`: domain project; check its `.csproj` and callers before changing public contracts.
- `KhimTools/App/`: companion app project; check its own `.csproj` and launch/deployment flow.
- `Installer/`: WiX MSI/bootstrapper and install, upgrade, repair, uninstall verification scripts.
- `Docs/UIUX/`, `Docs/QTO/`, and other `Docs/`: product specifications, constraints, contracts, and QA notes.
- `Tools/` and `KhimTools/Tests/`: focused verification scripts and regression checks; `Test-All.ps1` is the repository-wide entry point.
- `README.md` and `KhimTools/DEV_GUIDE.md` may lag the current tree. Confirm paths and behavior in the current project files and recent code before relying on them.

## Engineering constraints

- The Revit project multi-targets `net48` and `net8.0-windows`. Preserve both target frameworks unless the task explicitly changes supported Revit versions. Check conditional API references and build/deploy targets in `KhimTools/KhimTools.csproj` before editing them.
- Revit API calls must run in a valid Revit API context on Revit's main thread. For modeless or asynchronous UI, follow the current `ActionEventHandler` implementation and relevant tool patterns. Do not move Revit API work to background threads.
- Treat `KhimTools/Deploy/`, Revit family assets, manifests, installer source, and install/upgrade behavior as coupled. When changing them, inspect every dependent `.csproj`, WiX file, manifest, and verification script. Preserve existing file paths unless the task covers all references.
- Do not enable post-build deployment or write into `%ProgramData%` as a side effect of an ordinary build. Never run install/upgrade/uninstall verification against the user's machine without an explicitly requested, isolated test environment.
- Keep each change within the requested feature. Avoid broad formatting, unrelated cleanup, generated build output, binaries, and secrets.

## Verification and reporting

- Follow relevant `Tools/Verify-*.ps1`, `KhimTools/Tests/RunTests.ps1`, or `Test-All.ps1` guidance when verification is part of the task. Select the narrowest meaningful check and report exact commands/results; state clearly when a check could not be run.
- Do not claim a build, Revit runtime check, or MSI lifecycle check succeeded unless it was actually run in an environment that supports it.
- At handoff, list changed files, behavior changed, verification status, remaining risk, and any decision Codex or the user must make.

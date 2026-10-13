# Full-stack recovery and audit — 2026-10-13

Repository: `nguyenkhiemkhiem079-boop/KhiemTools_`; integration branch: `fix/preview-hardening`.
Verified remote master baseline: `601d1feb8731356f3ac2f509aa5aabeecbd8fd3a`.
Recovery starting feature tip: `308bf4146663e00e04f5d7bcb500cbf188af438f`.

## Wave 0 — recovery inventory

- Main checkout and index were clean, no stashes existed, and local/remote feature tips matched after fetching all remote branches and tags.
- The feature branch contains 173 commits after baseline; baseline-to-feature diff covers 467 files (+32,251 / -3,294). All local Stage 3–18, Slab and Beam feature tips and local `master` are ancestors of the pushed feature tip. Local `master` is `612c4cd63ca641e22579161730c47b81e7cb0961`, one runtime-harness commit ahead of remote master, already contained in the pushed feature history. No master ref is changed by this mission.
- `backup/master-pre-promote-0137941` has five commits absent by SHA from remote history. `git cherry HEAD` identifies four as patch-equivalent to integrated work: `5561290`, `17c754e`, `dc0735f`, `0137941`. The remaining `a0a7c8e` rectangular-Rebar snapshot differs from later integrated source; preserve it for historical comparison rather than replacing evolved production code.
- `git fsck --full --no-reflogs --unreachable` found 15 unreachable commits (historical Stage 3 candidates and expired stash/index/untracked snapshots). Each was preserved under `refs/recovery/fullstack-20261013/<full-sha>`. No object, stash, source file or old ref was deleted.
- A verified Git bundle contains all 89 repository refs, including the recovery refs and historical backup branch. Local archive: `../recovery/fullstack-20261013.bundle`; SHA-256 `7599bd00fc52c548a55e1cbc0f7fb9158d56f3694084c47fa63203fab17a8bfb`. This archive is intentionally outside the product repository and is not an installer payload.
- A detached package worktree at historical `4542170` is clean and retained. The outer workspace Git root has untracked source fragments, build logs and sibling directories; they are preserved in place and are not silently staged into the product repository.
- GitHub lookup found no open PR for this feature branch. GitHub CLI is unavailable in PATH; the connected GitHub tools are available for the integration PR.

## Audit method and acceptance boundary

Inspect actual source and current executable gates; historical Phase reports are context, not proof. Prioritize physical Rebar geometry, exact types, canonical plans, preview/Create parity and rollback. Compile both supported acceptance targets and validate the current-source installer package. Live mutation tests require a confirmed disposable supported Revit model; offline/source tests never certify host acceptance.

## Wave 1 — implementation audit (in progress)

The Beam Manual v1 trace identified two candidate issues requiring deterministic reproduction: longitudinal return curves use a fixed local-right plane normal, and stirrup station/solved-path checks may omit bar radius at Beam end faces. Confirm and fix them before claiming the affected support.

The cross-module audit and current validation results will be recorded below as they complete. Unsupported Wall reinforcement, support extensions, design-code calculations or inferred project reinforcement values will not be introduced without an accepted contract.

## Delivery state

Recovery inventory is complete. Module audit, coordinated corrections, fresh full regression/build/package evidence and PR review remain in progress. This report does not certify production readiness.

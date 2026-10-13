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

## Wave 1 — coordinated Rebar correction

Confirmed and corrected four P1 findings: non-coplanar longitudinal return paths were accepted by the input validator; every return used the local-right normal even for a horizontal return; stirrup end stations and solved centerlines omitted physical radius at Beam end faces; malformed persisted return/contour points could reach null dereferences in collision checks. Two regressions failed against the original code (non-coplanar path, stirrup at station zero); the coordinated correction passes ten additional numerical/validation regressions. A pure geometry helper now derives the canonical path plane and shares the physical end-envelope rule with the Revit adapter. Existing straight/vertical hook-plane conventions remain stable; no engineering value is added.

Wave 1 verification: Golden Regression 134 executed assertions PASS; K-Rebar 159/159 static checks PASS; dependency restore PASS; Revit 2024 Release 0 errors / 210 warnings; Revit 2025 Release 0 errors / 208 warnings. Actual Revit generation, hook compatibility and undo remain host-required. Full canonical regression/package validation is scheduled after the cross-module audit.

The cross-module audit and current validation results will be recorded below as they complete. Unsupported Wall reinforcement, support extensions, design-code calculations or inferred project reinforcement values will not be introduced without an accepted contract.

## Wave 2 — Revit API compatibility recovery

Actual builds against installed API DLLs exposed 42 errors on Revit 2022, 20 on 2023 and 101 on 2026. Corrected production ID reads through the existing version-aware adapter: modern identities retain 64-bit values; legacy int-backed data uses checked conversion so large IDs are never truncated. Category identities use the same compatibility boundary. Mechanical call-site edits preserve existing geometry, quantities and transaction behavior; snapshot `IntegerValue` data properties are unchanged.

Schedule Split previously claimed capability unconditionally and directly referenced APIs absent in Revit 2022. Its adapter now discovers the exact segment/placement signatures at runtime, blocks the workflow before mutation when unavailable, uses the existing checked execution path when available, and propagates underlying API failures. Revit 2022 Schedule Split is explicitly unavailable; no synthetic split or placement fallback is generated.

New canonical gate `Tools/Verify-RevitCompatibility.ps1`: 20 executable synthetic old/new API assertions plus five actual source builds. Revit 2022/2023/2024/2025/2026 all compile with zero errors after correction. Warnings in the captured full builds: 0/0/10/8/144. Those results verify source API compatibility, not live runtime loading or model behavior. Focused K-Rebar 159, Schedule Split 49, Parameter Manager 174, Viewport Align 67, Architectural 39, MEP 31, QS 49 and runtime-harness static 50 gates pass. Legacy int-backed workflows still reject IDs above Int32 range rather than silently changing persisted contracts; full 64-bit storage migration is a documented remaining boundary.

## Delivery state

Recovery inventory is complete. Module audit, coordinated corrections, fresh full regression/build/package evidence and PR review remain in progress. This report does not certify production readiness.

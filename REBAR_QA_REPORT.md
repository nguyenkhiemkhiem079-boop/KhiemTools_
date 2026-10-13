# Rebar QA report — 2026-10-13

Wave3 MEP cut safety: reject conservative existing-Rebar envelope overlap regardless of host, without deleting/trimming/moving bars. System/fabric reinforcement resources block because safe cut behavior is not implemented. Host joins block to avoid unverified connected-host effects. Native cut postconditions and QA-only failure rollback fixture are registered, NOT_EXECUTED; this is not structural redesign acceptance. See Docs/QA/MEP/CONFIRMED_OPENING_WAVE.md.

Current Wall wave supersedes old unimplemented-Wall notes: exact Wall-only ownership with verified boundary Columns; explicit straight supported bars and physical covers, rollback-only production preview, complete duplicate/current fingerprint and atomic exact-output parity. Pure Wall goldens exercise input direction/spacing/identity/persistence, not native CSG. Unknown JSON and curved neighbor clearance fail closed. WALL-MANUAL-OWNERSHIP-ROLLBACK is REGISTERED/COMPILED, NOT_EXECUTED. See Docs/QA/Wall/WALL_MANUAL_WAVE.md and final TEST_RESULTS.md. Host coverage/Undo/advanced geometry are not certified by builds or offline UI.

Wave1 support implementation supersedes prior unavailable-support boundary: exact bound rectangular Column/Beam support identity, explicit uniform exposed cover/intent, exact transient concrete/cover-shell and Line/Arc envelope gate, support-neighbor stale/interference handling, same solver/Create plan. Golden157 and K-Rebar163 pass; host fixture BR-MANUAL-SUPPORT-ROLLBACK is REGISTERED/COMPILED, NOT_EXECUTED. Undo/kernel/bends/support resource matrix remains host-required. No project engineering values inferred. See Docs/QA/Beam/SUPPORT_EXTENSION_WAVE.md.

Latest continuation evidence: canonical50/50 suites PASS (3005 configured weight); batch11 synthetic assertions additionally check whole rollback on later failure, final host/output recheck, empty/foreign/duplicate assignments and failed group start. These are orchestration tests, not live model fingerprints or geometry proof. Host acceptance remains NOT_EXECUTED. See DEVELOPMENT_CONTINUATION_HANDOFF.md for unimplemented support/Wall work and 2D schematic parity gaps.

PR #3 continuation: independent multi-Beam editing/accepted preview and atomic Create are wired; no plan copy/scale. K-Rebar160, Golden140, full canonical48 suites PASS; live multi-host parity/rollback/Undo pending. Support extensions/Wall remain unsupported. The historical single-host support statements below are superseded only for independently authored batch assignment.

Start: `308bf4146663e00e04f5d7bcb500cbf188af438f`; branch `fix/preview-hardening`.

## Confirmed findings and coordinated fix

| ID | Severity | Trigger / previous behavior | Correction / evidence |
|---|---|---|---|
| BR-MANUAL-PLANE-VALIDATION | P1 | Four non-coplanar return/core vertices passed domain validation; a new golden failed against the old validator. | Reject non-finite, zero-segment or non-coplanar paths before Revit creation. |
| BR-MANUAL-RETURN-NORMAL | P1 | Horizontal return vertices change x, but the adapter always supplied local Right as the curve-plane normal. | Derive normal from actual local vertices and map it through the verified Beam frame; vertical/straight normals and canonical sign are tested. |
| BR-MANUAL-END-ENVELOPE | P1 | A stirrup at station 0/L passed domain checks; its circular cross-section extends outside the physical end face. Solved-path checks only tested the centerline. | Input and generated paths use the same actual diameter/radius end-envelope rule. Station-zero golden failed before correction; start, finish and exact-radius boundary cases pass after it. |
| BR-MANUAL-MALFORMED-POINTS | P1 | Null persisted contour/return points could be dereferenced by collision validation after an issue had already been recorded. | Preserve diagnostic rejection and avoid processing malformed paths. Two malformed-input goldens pass. |

Golden Regression: 134 assertions PASS (124 existing + 10 new). K-Rebar: 159/159 PASS (157 existing + 2 integration guards). Revit 2024 and 2025 Release builds pass with zero errors. These are code-side results, not live host results.

## Existing host support and preview boundary

Beam Manual v1 supports one verified straight rectangular Beam at a time, explicit exact types/roles/layers/stations/returns/hooks/stirrup paths, independently entered face covers, saved plans, rollback solver preview, stale/duplicate guards and atomic parity-protected Create. Verified support extensions and multi-Beam Manual plan editing remain unavailable. Legacy Beam behavior is separate.

Rectangular/circular Column, supported planar Slab and bounded Foundation generators and detached previews already exist. Their current source/automated geometry, rollback and parity gates passed in the final canonical regression. This does not verify the actual host model behavior. Wall reinforcement has no implemented command/generator and remains explicitly unsupported; no contract authorizes inventing it here.

Final code checkpoint `7529d6cce2b359763ecd3c77e43a5213ba781863`: K-Rebar159, Golden134, config37, full offline Rebar17987 checks/360 renders and focused Manual734/18 PASS. Canonical48/48 suites PASS. All five source API builds PASS; runtime fixture/catalog registration intact. Manual1366x768 clamping and compressed headers corrected, without hiding controls or weakening assertions. Live generation, host orientation/cover/bends, supports/opening/spacer geometry, rollback and Undo remain HOST_REQUIRED. Beam preview is a detached solved-path reference, not proof that other host adapters passed live acceptance.

Live Revit QA: NOT_EXECUTED. No running supported Revit host with a confirmed disposable model is available in this session. Execute only on a confirmed disposable model: Manual straight and extended-details scenarios; Column position/role/duplicate/undo cases; Slab stale/multi-panel/accessory rollback; Foundation supported/rejected host cases. Registration/builds do not close host acceptance.

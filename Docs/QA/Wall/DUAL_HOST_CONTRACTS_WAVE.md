# Wave2 revised delivery — explicit Wall and Column primary-host contracts

User's decision to support BOTH actual host conditions supersedes the earlier option1-only restriction. Requested baseline f6a588ddc429c676f72f9d943a298b40d79ccec6 remains an ancestor. Advanced Wall/MEP/preview work was preserved; actual implementation resumes from verified6ffe8d4f11fdf58563f9d2f07b7edd5d29e7926d. No reset, force push or master merge.

## Distinct supported software contracts

| Contract | Primary native host / eligibility | Explicit geometry / ownership | Rejected scope |
|---|---|---|---|
| A WALL_MANUAL_V1 | Actual structural Basic Wall, one verified straight vertical rectangular six-face solid, valid Rebar host | Same Wall ownership, exact user endpoints/types/six face covers; verified explicitly bound rectangular boundary Columns may contain extensions | Curved/slanted/opened/cut/non-prismatic Wall; links; unsupported hook/tie/design geometry |
| B COLUMN_HOST_WALL_RELATED_V1 | Actual structural rectangular Column, valid native Rebar host; independently measured physical solid and actual family axes including rotation/mirror | Separate HostColumnUniqueId, explicit ownership confirmation and scenario intent. Column is NOT assumed to be a Wall. Straight Column-contained paths, exact loaded types and six Column face covers | Circular/slanted/cut/non-prismatic Column; links; hooks/ties; positive outside-Column extension or implicit Column→Wall rehosting |

ContractB does not silently emulate ContractA or rename a Beam profile. The Column generator uses physical rectangular Column analysis plus actual family X/Y axes and bottom center. All bars are created with the actual selected Column passed to the native API and verified GetHostId. Existing general Column reinforcement behavior remains separate and unchanged. Boundary-column intersections/extensions remain ContractA Wall-owned; the B contained-path boundary is disclosed as a software limitation, NOT an Autodesk API impossibility or structural approval.

Separate ribbon commands/labels and strict JSON plans cannot be interchanged. Same-contract multiple selected hosts are supported: each gets independently authored inputs and accepted production preview; no plan cloning/scaling. Mixed A/B UI selections reject explicitly instead of omitting or rehosting elements. The shared batch engine can safely compose explicitly typed A/B assignments (used by the registered cross-contract fixture), but mixed selection is not automatically interpreted by either UI command.

## Detail values are required and consumed

No production reinforcement quantity, diameter, cover, anchorage or extension default is supplied. Both ends require StartAnchorageMm/FinishAnchorageMm and StartExtensionMm/FinishExtensionMm; null is not zero. Exact loaded type is selected deliberately; every physical bar is explicitly listed. B also requires UserConfirmedActualColumnHost=true and a nonempty ScenarioIntent.

Software geometry definition: terminal anchorage is the user-declared **straight terminal embedded segment**, not code-calculated Ld, bend/hook anchorage, development adequacy or material/code compliance. Sum of terminal segments cannot exceed actual line length. Native primary-solid intersection checks both actual outside-primary extension lengths against the explicit values; terminal segments must lie entirely in their correct verified primary/boundary region. Full actual circular bar/end envelope and exposed cover checks remain mandatory. B contained-path extensions must each be explicitly0; positive external geometry fails before mutation. These values are not merely annotations/ignored engineering settings.

Old A plans lacking these new mandatory values can open as an explicitly labeled **LEGACY DRAFT / NOT SOLVED** only after exact current host/document/frame checks. No values are inferred or written automatically. Solve/Create remain blocked until the user supplies detail lengths; original files are not overwritten without an explicit save/overwrite action. New B plans have a separate mode/host field/file extension. Unknown JSON settings, foreign/stale bindings and fake CodeVerified claims reject.

## Preview → Preflight → Create → Verify

Production-generator rollback-only capture, exact input/host fingerprint, actual final-world included bar positions, detached shared2D/3D, explicit accepted assignment and final batch confirmation. All assignments preflight before any mutation: distinct valid same-document primary hosts, exact contract/category, one unambiguous host-bound preview, current inputs and complete duplicate protection.

One checked outer transaction group, checked transaction per host, actual host/type/quantity/geometry parity, then a final ALL-host input/ownership/output recheck before assimilation. Only the batch's own newly created bars are excluded from input-neighbor staleness; generation still sees earlier batch bars for real interference checks. Any later failure rolls back earlier hosts; no production QA failure switch. Native Undo should undo the assimilated group as one operation but is NOT certified before a genuine host run.

Native Line/Arc sweep collision guard covers curved neighboring Rebar, including other-host bars crossing the Column. Unsupported area/path/system/fabric reinforcing resources block A/B explicitly because their clearance is not yet implemented; they are never silently omitted. Geometry/kernel uncertainty blocks rather than producing speculative bars.

## Automated evidence and required live matrix

Golden223 adds15 pure B contract/identity/direction/cover/detail/collision/roundtrip assertions and4 explicit A-detail assertions. Production-linked DualWallHostBatchV1 + TransactionBoundary execute13 tests with synthetic API/transaction doubles: success, stale/duplicate preflight, second-host failure/parity, changed earlier output/frame, foreign host, contract mismatch, duplicate host, ambiguous snapshot, failed group start and empty output. These do not verify native rollback or Undo.

Focused A/B controls-only matrix: minimum/1366x768/1920x1080 ×100/125/150% simulated scaling,320 bounds checks/18 renders at the UI checkpoint. Final exact-source canonical/build results are recorded in TEST_RESULTS.md after completion. Metadata, source assertions and bitmaps are NOT live Revit acceptance.

DUAL-WALL-COLUMN-OWNERSHIP-ATOMICITY: REGISTERED/COMPILED, NOT_EXECUTED. Disposable model requires both suitable actual Wall and Column plus loaded exact bar type. Both production previews solve; A creates first, B creates with actual Column ownership, then fixture-only failure verifies whole-group model identity/state rollback and unrelated preservation. Missing/unsolvable resources BLOCKED, not PASS. Wall boundary-extension fixture remains intact with explicitly labeled QA-only numeric inputs.

Mandatory host operator matrix on a CONFIRMED detached disposable model: Wall-only; Column-only (explicit purpose, no automatic Wall equivalence); actual Wall/boundary-Column intersection with positive/zero/bad declared extensions and terminal embeds; independently entered multiple Wall and multiple Column plans; mixed UI selection rejection; rotated/reversed Wall and rotated/mirrored Column; exact six covers/diameter/spacing/invalid and unsupported geometry; missing/foreign/moved inputs; preview cancel/refresh; complete/partial/other-host duplicates; cross-host bar interference; later host failure/whole rollback; unrelated identity/state preservation; successful native Undo. Record real resources/results separately by2022/2023/2024/2025/2026.

HOST_ACCEPTANCE=NOT_EXECUTED; PRODUCTION_READY=NO. No project reinforcement values, structural-code/fire approval, real host mutation, deployment, merge or release is inferred from these code-side gates. Existing bounded Wave3 MEP opening code is preserved; continue verification from current source, do not reimplement it or treat its host fixture as executed.

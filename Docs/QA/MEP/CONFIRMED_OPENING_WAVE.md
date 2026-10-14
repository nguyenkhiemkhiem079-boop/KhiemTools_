# Reviewed native MEP opening — Wave3 bounded implementation

## Current Wave3 graphical proposal checkpoint

The command now requires five empty-by-default fields: per-side clearance, minimum width, minimum height, signed local X and Y offsets, all in mm. Explicit zero means no added minimum/offset, never a hidden design value. Minimum dimensions enlarge the real-clash rectangle around its center; offsets are accepted only if the whole clash plus clearance stays contained and the cut remains strictly interior to the physical host. All five values are preserved in the accepted plan, fingerprint and fresh pre-Create solve.

The required detached graphical review displays 2D host-plane and 3D isometric wireframe views of the measured prism and the identical resolved full-depth cut. Blue is the actual MEP centerline, explicitly **not** an outer profile or insulation/fire allowance. Host/penetrant native identities, sizes, coordinate ranges, clearance, minima and offsets are disclosed. Review approval is followed by a separate native confirmation defaulting to Cancel. Paint contains no Revit API calls or persistent temporary elements. Only explicitly structural Floors qualify.

Checkpoint evidence: pure production-linked Golden238 (15 new numerical geometry assertions), opening source-integration18. These are offline tests, not native geometry/UI/Undo acceptance. Linked-model elements remain explicitly unsupported for creation; no transform is guessed and host ownership cannot switch.

The existing CmdMepOpenings remains a read-only coordination review. Its legacy50mm estimate is never a creation default. CmdMepOpeningCreateV1 is separate: explicitly pick local Wall/Floor and straight supported duct/pipe/tray, enter finite non-negative per-side clearance (empty initial input), inspect the rectangle/coordinates/identities and explicitly confirm. Cancel is default. This is geometry coordination, not structural/fire/code approval.

## Supported and rejected geometry

- Wall: physically verified straight vertical structural Basic Wall, one rectangular six-face concrete solid (same independent physical frame as Wall reinforcement).
- Floor: one horizontal global-axis-aligned rectangular six-planar-face solid matching its actual volume. No slope, holes, joined/non-prismatic geometry or rotation extrapolation.
- MEP: local straight curve, perpendicular to the host and completely crossing both faces; one actual solid, positive actual CSG clash. Oblique/curved/bend/fitting/link/Beam/family-host cuts unsupported.
- Rectangle contains the **entire native clash solid bounding box projected into host coordinates**, expanded by the user-supplied clearance. Deliberately conservative, NOT a minimum or exact circular profile; rotated-wall projection may enlarge the rectangle. Full sizes and coordinate limits are disclosed before confirmation. Cuts reaching host boundaries reject, never clamp or invent engineering edge distances.
- Existing openings/non-prismatic hosts reject before proposal; repeated creation cannot silently add duplicate cuts. Rebar envelope overlap rejects with no bar deletion/trimming. System/fabric reinforcing resources anywhere in the model currently block because their safe cut behavior is not implemented. This is conservative limitation, not pretending reinforcing redesign is supported.

## Atomic native execution

Host solid joins and Wall endpoint joins to other elements are explicitly blocked. A rectangular visible solid alone must not be used to infer absence of native join side effects.

Fresh proposal/fingerprint/reinforcement safety recheck inside caller-owned transaction. Wall rectangle uses native NewOpening(Wall,XYZ,XYZ); Floor uses native profile/vertical cut. One checked transaction within one transaction group. Native opening host identity, regenerated remaining concrete, actual removed-solid equality with the entire reviewed cut, no extra added concrete and complete removal of the actual MEP clash are mandatory postconditions. Any uncertainty or failure throws and rolls back the whole group. No hidden model mutation during proposal/dialog and no QA failure switch in production.

Pure MepOpeningCutPlanV1 tests validate explicit/per-side clearance, zero versus missing, nonfinite/reversed/zero extents and no boundary clamping. These do not test native solid kernels. Source integration gates explicitly execute in Test-All.

## Live acceptance — NOT_EXECUTED

MEP-NATIVE-OPENING-ROLLBACK is registered/compiled. On a confirmed disposable model it finds one supported actual clash (Wall OR Floor), generates/validates the real cut, injects a test-only failure after generation, rolls back, and compares model identities/state with RuntimeQaSafetyGuard. Missing usable resources BLOCKED. A single resource run must not be called both Wall and Floor acceptance.

Required operator matrix, on confirmed detached disposable copies only: Wall and Floor separately; pipes/ducts/trays; reversed axis; rotated/flipped supported Wall; explicit clearance positive/zero/boundary-negative; oblique/endpoint/unsupported host rejection; non-Rebar system reinforcement rejection; neighboring/different-host Rebar interference; stale host/MEP edits between review and Create; repeat proposal/duplicate rejection; cancel; actual removed-volume/clash checks; injected failure/unrelated identity-state preservation; successful native Undo. Record fixtures and model identity before/after. Builds, source assertions and synthetic rectangle tests never replace these host results.

HOST_ACCEPTANCE=NOT_EXECUTED. PRODUCTION_READY=NO. Unsupported cuts and engineering redesign remain explicit boundaries. No installer lifecycle, signing or release is performed by this wave.

## Wave3 consolidated source audit and executable coverage

The read-only analysis now rejects foreign-document identities before grouping ElementIds. Selected link instances no longer trigger an unrelated native-view fallback; all link exclusions are disclosed with a whole-document instance count, not a fabricated clash count. Linked geometry/transforms and linked-host/penetrant cuts are unsupported and explicitly rejected. No mutation is attempted in linked documents. Round ducts now use a valid native diameter rather than being silently skipped for missing rectangular section parameters.

Stale fingerprints include measured host frame and actual penetrant endpoints/solid volume as well as element VersionGuid, document identity and all five reviewed inputs. VersionGuid alone cannot prove unchanged geometry within an open transaction. The existing actual CSG, full through-penetration, join, reinforcement, native ownership and removed-volume postconditions remain mandatory.

MEP-NATIVE-OPENING-ROLLBACK now additionally checks proposal model-state preservation, fixture-only penetrant movement/stale rejection with rollback, and repeat-cut rejection after native creation. It reacquires native references after rollback and still compares full observable model state/identities, not only counts. No artificial failure is placed in production. UI Cancel and successful Undo remain operator-only HOST_REQUIRED; fixture registration/compilation is not their PASS.

Offline production form QA: 54 bounds/pixel assertions, 18 renders, minimum/1366×768/1920×1080 at 100/125/150% simulated scale, both 2D and 3D. Perpendicular 2D centerline projection is a symbolic point, not a fabricated bar/penetrant diameter. The canonical suite explicitly runs Verify-MepOpeningPreview, alongside MEP acceptance34, domain12, native opening integration20 and Golden238. Package/source rebuild and final Test-All results are recorded in TEST_RESULTS.md.

Audit: no confirmed P0 in the supported bounded source slice; P1 fixed missing minima/offsets/graphic review, foreign-ID ambiguity, round-duct skipping, in-transaction stale geometry and degenerate 2D projection. P1 HOST_REQUIRED: all actual host CSG/clearance/duplicate/stale/cancel/rollback/Undo results and per-year loading. P2 intentional limitations: one reviewed clash at a time, conservative rectangle rather than exact round profile, straight perpendicular penetrants, unjoined uncut rectangular structural Wall/horizontal axis-aligned Floor, global conservative system-reinforcement exclusion. Advanced/non-prismatic/Beam/linked/oblique cases are **UNSUPPORTED**, not implementation success. Engineering/fire/insulation constraints must be supplied and approved externally; no compliance or production readiness is claimed.

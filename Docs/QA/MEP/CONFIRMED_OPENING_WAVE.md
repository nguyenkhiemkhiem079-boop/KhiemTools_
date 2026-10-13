# Reviewed native MEP opening — Wave3 bounded implementation

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

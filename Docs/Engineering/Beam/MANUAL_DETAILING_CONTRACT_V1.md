# Repository Copy Metadata

Source: `C:\Users\khiem.nguyen\Downloads\KTOOL_BEAM_MANUAL_DETAILING_CONTRACT_V1.md` supplied by the user.

Copied on 2026-10-12. The authoritative contract text below is preserved verbatim; only this source/change-log metadata is added.

Change log: Initial repository copy of the approved v1 software/input contract. No project-specific reinforcement values or engineering approvals are added.

---

# K-TOOL — BEAM MANUAL DETAILING CONTRACT v1.0

**Status:** PRODUCT WORKFLOW APPROVED — MANUAL DETAILING ONLY  
**Scope:** K-TOOL / K-Rebar / Beam, Autodesk Revit 2024 and 2025  
**Code baseline inspected:** `fix/preview-hardening` at `843d99218e5f85ef2a97ab3b0d56e59a911f4c13`  
**Classification:** Software geometry-and-data contract. **NOT** a structural design check or approval of real reinforcement quantities/detailing.  
**Authoritative inputs during operation:** user's explicit Beam settings, loaded Revit types, and independently verified host/support geometry. No guessed project figures.

## 1. Scope and immutable engineering boundaries

1. Implement an explicit, user-driven manual detailing workflow to define **continuous top/bottom bars, additional top-left/top-right/bottom-midspan bars, multiple longitudinal layers, side/skin bars and stirrup groups** as selectable and verifiable data. A bar role may be enabled only when all necessary geometric inputs exist and the actual Revit generator supports it.
2. Do **not** size reinforcement for ULS/SLS, decide reinforcement need, prescribe required skin bars, calculate code anchorage, select steel/concrete grades, or assert compliance with Eurocode/TCVN/AS/ACI. The UI must say **"Manual reinforcement detailing — geometry validation only; structural design/compliance not verified."**
3. There are **no product defaults** for diameters, cover, start/end cut-off stations, anchorage lengths, side-bar spacing, layer offsets, hook angles, additional-bar fractions, or reinforcement counts newly introduced by v1. A field's absence is not permission to insert a plausible engineering value.
4. Synthetic numbers are allowed **only in clearly labeled isolated tests**, never silently as production configuration or project design data. Preserve validated existing legacy behavior behind a clearly named legacy mode if needed, but prevent silent transfer of legacy assumptions into Manual v1.
5. Explicit unsupported geometry or a missing/ambiguous host relationship must **fail closed** before committing Rebar.

## 2. Existing verified code baseline and migration intent

- `BeamGeometryHelper.BeamProfile` currently exposes dimensions `B/H/Length`, `StartPoint`, `EndPoint`, `Direction`, `RightVector`, `UpVector`; `TransformLocalToWorld(profile,x,y,z)` maps width/height/longitudinal local coordinates to world.
- `BeamLongitudinalLayout.CreateSymmetricRow` currently supports one symmetric top and one bottom row. `BeamStirrupLayout` supports validated stations. Preserve these as legacy-supported operations and reuse their collision checks where applicable.
- `BeamRebarInput` currently has legacy `TopLeftExtraQty`, `TopRightExtraQty`, `BottomMidExtraQty`, `SideBarQty`, `AutoSideBars`, and length multipliers. The production generator rejects positive additional/side requests before reaching private hard-coded fractional placement routines. **Do not unlock those routines.** Replace them with a separate explicit geometry-based manual model and generator path.
- `BeamReinforcementForm.CreateGenerationInput` currently sets unsupported extra/side quantities to zero; retain the fail-closed behavior until the new manual path is fully wired and tested.
- `RebarPreviewService.Capture` and `Matches` implement rollback-only solved preview and create parity. `TransactionBoundary.ExecuteGroup` protects multi-beam Create. These are non-regression requirements.
- `RebarConfiguration` current schema is numeric-only with `SchemaVersion=1`. Do not stuff bar-array identities into decimal slots or silently change this schema. Add a versioned, validated, backward-compatible manual detailing storage contract or a dedicated companion document keyed to model/host identity.

## 3. Coordinate system and unit contract — normative

Use the existing `BeamProfile` physical-section-centered local frame:

- `s` (also code's local `z`) = **longitudinal station** along `Direction`, from the **physical verified beam start section center** at `s=0` to the end section at `s=L`.
- `x` = lateral coordinate along `RightVector`, with beam section center `x=0` and physical side faces at `x=±B/2`.
- `y` = vertical coordinate along `UpVector`, with beam section center `y=0` and physical top/bottom faces at `y=±H/2`.
- Transform any `P(s,x,y)` using `BeamGeometryHelper.TransformLocalToWorld(profile,x,y,s)`. Verify the actual code's `StartPoint` is shifted to the physical section center; do not assume family insertion origin equals concrete center.
- The **manual-mode UI accepts all dimensions in mm**, with a clear unit label. Convert to Revit internal units exactly once at the UI/domain boundary; internal computations and Revit API use internal feet. Preview annotations convert back to mm at display. Persist units or a schema-defined canonical mm convention.
- Station semantics: absolute local station **in mm from verified physical beam start**. No percentages/L/3/L/4/15–85% or face-offset guesses are accepted as implicit defaults. For an end outside `[0,L]`, the selected and validated support solid/end-detail must explicitly permit extension and verify its physical containment.
- Host `UniqueId`, local-frame orientation and geometry/support state are part of the fingerprint. Reversing beam direction or swapping ends must invalidate acceptance and must not silently swap left/right support detailing.

## 4. Versioned data model — required fields

Introduce an auditable `BeamManualDetailingPlanV1` or equivalent **typed** domain model:

**Plan header** (required): `schemaVersion`, `mode=MANUAL_V1`, `hostUniqueId`, supported-host snapshot signature, coordinate-contract ID, explicit unit convention, list of bar groups, stirrup groups, and an explicit support/end-condition map. Include a stable generated group ID for role identity.

**BarGroup** (each enabled group requires):

- `groupId` (stable, unique within plan), `role` (`TOP_CONTINUOUS`, `BOTTOM_CONTINUOUS`, `TOP_LEFT_EXTRA`, `TOP_RIGHT_EXTRA`, `BOTTOM_MID_EXTRA`, `SIDE_LEFT`, `SIDE_RIGHT`, or a clearly disclosed user-defined role), `enabled`.
- Exact loaded `RebarBarType` document identity, named type and actual model diameter. On template load into another Revit document, never substitute by closest diameter; require explicit rebind/confirmation.
- `placement`: explicit local `(xMm,yMm)` for **each physical bar**, or a user-selected symmetric row/regular distribution whose input count, boundaries and spacing policy are fully specified and whose resulting concrete coordinates are shown before solving. Quantity must equal resolved physical bar positions. No duplicate corner/position entries.
- `startStationMm`, `endStationMm` as **explicit bar centerline stations**; require finite values, `end>start`, and actual beam/support envelope fit. No fixed fractions. If multiple bars share stations, groups may use a common pair; otherwise provide per-bar station overrides explicitly.
- `startEndDetail` and `finishEndDetail` independently chosen from `STRAIGHT_AT_STATION`, `EXPLICIT_CURVE_RETURN`, `EXPLICIT_LOADED_REVIT_HOOK`, or `UNSUPPORTED`. An explicit return requires direction/plane and all physical dimensions; an explicit hook requires an exact loaded `RebarHookType`, validated orientation/type/shape compatibility and generated path. `UNSUPPORTED` cannot be enabled.
- `layerId` / user-facing descriptive position if applicable, with **explicit y/clear spacing** or individual coordinates. Layer labels alone never generate geometry.

**StirrupGroup** (when enabled): exact loaded stirrup bar type; exact supported shape and hooks/end behavior; explicit `stationsMm[]` **or** a user-chosen deterministic spacing mode with start/end stations, spacing, inclusion/remainder policy; concrete face-clearance specification; optional verified hanger/group role. Do not silently turn zero A1 length into L/4 in Manual v1.

**SupportEnd**: `START/END`, `boundaryMode` (`VERIFIED_COLUMN`, `VERIFIED_STRUCTURAL_BEAM`, `USER_DEFINED_FREE_END_WITHIN_HOST`, or other explicitly implemented and host-validated modes), supported host UniqueId if applicable, explicit intended continuation/return/hook. Structural wall, slab, cantilever-specific details stay `UNSUPPORTED` until their geometry and contract are actually implemented and tested. A straight terminated bar lying entirely inside supported Beam concrete may be modeled as a manual free-end geometry, **not** as proof of sufficient development length.

**CoverFaces**: mapping for top, bottom, left, right and stirrup outside; source is verified Revit face assignment or **explicit user-provided face clear cover**. Each face must resolve independently. The old maximum assigned cover **is not** face-specific. No global 25 mm or similar default. Validation considers physical Rebar envelope (bar radius/bend geometry) rather than checking centerline alone.

**Metadata**: source/mode, bar role, generated curve/rebar identity, plan revision, warnings that geometry-only checking was performed; deterministic creation result ledger.

## 5. Manual reinforcement behaviors — v1 acceptance

- **Main top/bottom:** preserve current supported legacy single-row behavior; additionally permit explicitly positioned manual bars. Multiple manual layers are permitted only if the plan supplies actual `(x,y)` positions and station/end details and they pass collision/cover checks. Do not hard-code "2 bars" as a requirement for every user-defined new group if the new modeling contract explicitly permits one bar; preserve legacy minimum where legacy is used.
- **Additional top-left/right and bottom-mid:** names are role labels, **not automatic location rules**. The user supplies each group's positions and both station boundaries; extension into a support is allowed only with actual verified host solids and an explicit end contract. Never use old fixed L/3 or 15–85% routines.
- **Side/skin:** user supplies exact local coordinates per side or a fully explicit spacing mode. No automatic height threshold; no implied code compliance; no silent symmetric replication without user choosing symmetry. Restraint/ties required for structural compliance are **not automatically proven** by geometric placement.
- **Stirrups/hangers:** preserve currently working supported modes; Manual v1 stations/zone endpoints are explicit. De-duplicate only coincident planned stations according to an openly documented same-role/shape policy; do not suppress a genuinely distinct required bar. Validate actual spacing and prevent colliding geometry.
- **Hooks:** choose exact loaded `RebarHookType` and supported orientation, or explicitly dimensioned return curves; two modes cannot be conflated. Unsupported shape/hook combinations are rejected; no hidden fallback to straight bars.
- **Support/anchorage:** manual input defines what is drawn, not sufficiency of development/lap. If a path extends outside the Beam, containment inside correctly identified concrete support solids and clear cover must be verified. If unknown, reject with diagnostic information.

## 6. Validation gates — no partial success

**PRE-SOLVE**: host category/validity, geometric prism and orientation, document identity, parameter completeness, exact type/hook/shape, units and finite values, station order, positive spacing/count, role IDs, section/cross-layer collision, face cover, end condition and support availability. Invalid settings are rejected with a group ID/field and actionable message.

**SOLVE**: invoke the **same production geometry builder and generator** inside rollback-only preview; verify rollback state is clean; capture all bar-set physical positions, type/shape/hooks/layout, roles and centerlines. No Revit API calls during paint.

**PRE-CREATE**: recompute a full fingerprint from host geometry, all manual bars/stations/coordinates/end details, stirrups, cover, supports, bar/hook/shape identity and document state. Reject stale, missing, failed or partial preview. Reject complete-plan duplicates using host-bound multiset; do not reject partially overlapping sets as complete duplicate.

**CREATE**: one atomic group across all selected hosts; each Beam generated in checked transaction; compare created centerlines/type/shape/hooks/quantity/role against accepted preview; rollback entire operation on *any* failure or mismatch; show explicit final status and errors.

**POST-CREATE / CANCEL**: successful Create only if all intended groups are generated; Cancel leaves zero temporary elements. Model rollback checked through Runtime QA fingerprints; do not claim host PASS without actually running inside Revit.

## 7. Numeric and geometric checks — boundaries, not code design

- Validate Revit/engineering geometry for bar envelope inside concrete, face cover, bars/stirrup interference, section spacing, longitudinal station fit, host/support containment, bend fit and generated Rebar shape. Use Revit geometry tolerances, transparently documented implementation tolerances, and actual bar radii; do not silently pick an arbitrary engineering limit.
- Geometry-only checks do **not** certify EC2, TCVN 5574, AS 3600, ACI, anchorage strength, seismic detailing or reinforcement adequacy. Output always separates `GEOMETRY_VALIDATED` from `CODE_VERIFIED=false`.
- Where a real physical/manual input required for reliable geometry is missing, block that bar/group or entire Create. Do not downgrade requested geometry without confirmation.

## 8. UI/UX contract

- Top-level **Legacy Supported** versus **Manual v1** mode, with truthful support badges/text. Do not make legacy unsupported controls appear operational.
- Allow individual Beam selection and multi-beam assignment explicitly. Each selected Beam may have a distinct plan; warn and require confirmation before applying one geometric plan to multiple geometrically different beams. Never silently scale/manual-map its stations.
- Manual editor: Beam dimension and physical frame summary; role/group navigator; table of bar coordinates and stations; per-end detailing editor; exact type/hook selection; face-cover map; live input validation; cross-section, elevation and separate **solver-backed** 3D preview.
- Tooltips and error messages distinguish **input**, **resolved actual geometry** and **structural verification not performed**. Use bilingual Vietnamese/English texts; keep existing good Beam preview rather than replacing it with a weak generic sketch.
- Visual preview shows top/bottom/additional/side/stirrup/hanger roles distinct from schematic art. Stale state obvious; Create disabled unless current solved preview accepted.

## 9. Persistence, migration, rollback, compatibility

- Avoid corrupting numeric-only `RebarConfiguration SchemaVersion=1`. Store structured arrays in a dedicated versioned `BeamManualDetailingPlanV1` serialization (or add a proven backward-compatible supported schema), validate project/host identity, impose sensible file-size/count caps **as software resource protection, not detailing defaults**.
- Current dormant legacy `BeamRebarSettings` additional/side positive defaults **must not activate** during load. Show an explicit migration/disclosure: legacy values are not automatically converted into stations/coordinates/hooks. Unsupported legacy fields remain disabled until the user completes a valid manual plan.
- Save/load round-trip must preserve every bar position, exact role/type reference resolution state, stations, end details, face covers, and view labels. A foreign-document type/hook remains **UNRESOLVED**, requiring user rebind before Solve. Persist atomic revisions; reject stale simultaneous edits.
- Verify both Revit 2024 (.NET Framework) and Revit 2025 (.NET 8) API compatibility; don't assume all constructors/overloads are identical. Preserve rollback and no-force-push policies.

## 10. Test and acceptance matrix

Minimum domain scenarios with synthetic, explicitly labeled fixtures (synthetic dimensions are **not defaults**): straight and rotated rectangular Beam; two or more manual top/bottom rows; uneven bar diameters; asymmetric positions; top-left/top-right/bottom-mid independent explicit stations; side-left/right positions; explicit hook supported/unsupported; curve return; face-specific dissimilar covers; support valid/missing/ambiguous; bar collision; station out of range; missing rebar type; changed hook/type/station/cover/host geometry stales accepted preview; partial/full duplicate; multi-Beam failure atomic rollback; Preview Cancel cleanup; shape failure; UI 100/125/150% simulated DPI and 1366×768, 1920×1080.

Acceptance is **two-tier**:

- `CODE_COMPLETE=YES` only if every Manual v1 feature declared supported is wired UI→model→generation→preview→create→persistence and passes code-side tests/builds; any unsupported feature must be explicitly marked/deactivated and the completion scope truthfully narrowed.
- `HOST_ACCEPTANCE=YES` only after actual tests on a **confirmed disposable detached Revit QA model**, with recorded bar quantities, centerlines, type/shape/hooks, fingerprint before/after rollback, Undo and duplicate results. No fixture registration or offline rendering counts as live host execution.
- `PRODUCTION_READY=YES` only after mandatory host gate passes, no open P0/P1 in claimed supported scope, and a final review/approval of that release scope.

## 11. Git and governance

Working integration branch `fix/preview-hardening`, starting verified SHA `843d99218e5f85ef2a97ab3b0d56e59a911f4c13` unless remote advanced. Agent fetches and reconciles safely before modifying. Commit and push at stable E2E milestones, remote SHA must match local SHA; master untouched; no force-push.

**Checkpoint A:** typed manual plan model, persistence/migration, domain validators + goldens.  
**B:** geometry builder and generator for supported manual roles; face-cover/end contracts.  
**C:** UI/manual editor, detached 2D+solver preview, stale/parity/duplicates.  
**D:** Revit fixture registration, synthetic tests, build/layout regression.  
**E:** **one consolidated final audit**, batch P0/P1 fixes, `Test-All`, builds, final report/handoff.

Record unresolved feature decisions in Beam `PHASE_04_DEFECT_REGISTER.md`; code-side blockers cannot be disguised as success. If quota runs out, push the last stable verified checkpoint and record exact remaining work and next SHA. **No merge to master.**

## 12. Approval boundary

**Approved now:** the **Manual v1 software/input contract and rules above**, including explicit geometry-based data entry, fail-closed validation, transaction protection, and classification of code-versus-host verification.  
**Not approved or supplied:** any particular project's beam bar schedules, grades, bar counts, cover values, cut-off positions, anchorage/development/lap lengths, seismic confinement rules or structural calculation results. User must supply or confirm those project-specific inputs when detailing.  
**Intentionally deferred:** automatic reinforcement design/engineering code checks and inference of required reinforcing quantities or rebar development lengths.



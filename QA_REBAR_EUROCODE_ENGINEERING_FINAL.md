# K-TOOLS REBAR TOOL — COMPREHENSIVE EUROCODE ENGINEERING AUDIT, HARDENING & QA REPORT

**Date:** 2026-09-05  
**System:** K-TOOLS (KhimTools) / Structural Reinforcement Detailing Automation  
**Standards:** Eurocode 2 (EN 1992-1-1:2004) & TCVN 5574:2018  
**Revit API Compatibility:** Revit 2023 - 2026 (net48 / net8.0-windows)  
**Status:** **PASSED ALL 21 TEST SUITES (100% PASS)**

---

## 1. Executive Summary & Audit Objectives

The K-TOOLS Rebar system was audited and hardened under the core principle:
> **"MAKE THE REBAR TOOL TECHNICALLY CORRECT FIRST."**  
> K-TOOLS must NOT simply "create Rebar". Detailing must strictly follow:  
> **EUROCODE / DESIGN INPUT $\rightarrow$ DETAILING RULE $\rightarrow$ DETAILING INTENT $\rightarrow$ REVIT GEOMETRY / SOLID VALIDATION $\rightarrow$ SAFE FAILURE**

### Eliminated Anti-Patterns
1. **Silent Fallback Elimination:** Removed all silent downgrades that transformed bent/cranked bars or hooked bars into straight bars when Revit shape solvers failed. Any unsolvable rebar now fails safely with explicit diagnostics (`NEED DESIGN INPUT` or `ERR_SHAPE_SOLVE_FAILED`).
2. **Arbitrary World Tolerance Elimination:** Removed the arbitrary 20mm world BoundingBox overrun tolerance in `RebarSafetyValidator.cs`. Containment is now verified against real host solid geometry and host local coordinate transformations.
3. **Multi-Host Detailing Intent:** Rebars extending intentionally across host boundaries (column continuity splices, beam support anchorages, foundation starter dowels) are now governed by `DetailingIntentContext`. Extensions into verified `ConnectedHost` elements pass, while any penetration into undefined free space is strictly flagged as `ERR_FREE_SPACE_PROTRUSION`.
4. **Rotated Member Support:** Fixed column and beam continuity calculations by projecting offsets and section reductions onto member local axes (`basisX`, `basisY`, `basisZ`) rather than world coordinates.

---

## 2. Architectural Architecture & New Engineering Components

### 2.1 `DetailingIntent.cs`
- Defines explicit detailing intent types:
  - `StandardInternal`: Rebar must reside 100% inside current host.
  - `ColumnContinuation`: Rebar intentionally extends upward into upper column for lap splicing.
  - `ColumnTransition`: Cranked 1:6 transition across section reduction.
  - `BeamSupport`: Beam top/bottom bar anchors into supporting column/wall.
  - `FoundationStarter`: Dowels extending from footing into column base.
- Maintains `DetailingIntentContext` containing `CurrentHost`, `ConnectedHost`, `AdditionalConnectedHosts`, `RequiredCoverMm`, `RequiredLapLengthMm`, and `RequiredAnchorageLengthMm`.
- Evaluates multi-host containment: `IsPointContained(XYZ pt, double barRadiusMm, out bool insideConnectedHost)`.

### 2.2 `StructuralConnectionResolver.cs`
- Automatically resolves connections between elements:
  - `ResolveColumnToColumn(Element below, Element above)`
  - `ResolveBeamToSupport(Element beam, Element support)`
  - `ResolveFoundationToColumn(Element foundation, Element columnAbove)`
- Extracts element local coordinate systems via `GetHostLocalAxes(element)`.
- Transforms world vectors into local offsets using `ProjectToHostLocal(worldDelta, basisX, basisY, basisZ)` for members rotated at arbitrary angles (0°, 15°, 30°, 45°, 90°).
- Strictly enforces the Eurocode 2 / ACI 318 transition rule:
  - Offset $\le 75.0\text{ mm}$: 1:6 crank permitted (`CanCrank = true`, `RequiresSeparateDowels = false`).
  - Offset $> 75.0\text{ mm}$: Crank prohibited; separate starter dowels strictly required (`CanCrank = false`, `RequiresSeparateDowels = true`).

### 2.3 `RebarEngineeringValidator.cs` (Section 34 Comprehensive Validator)
- Inspects all aspects of rebar assemblies without silent failure:
  - **3D Solid Containment**: Physical bar envelope ($d/2$) and clear cover against `CurrentHost` and `ConnectedHost`.
  - **Commercial Stock Length**: Flags bars exceeding $11.7\text{ m}$ (`ERR_STOCK_LENGTH_EXCEEDED`).
  - **Eurocode Mandrel Diameter (EC2 Cl. 8.3 & Table 8.1N)**:
    - $\phi \le 16\text{ mm} \rightarrow \text{mandrel} \ge 4\phi$
    - $\phi > 16\text{ mm} \rightarrow \text{mandrel} \ge 7\phi$
  - **Transverse & Longitudinal Section QA**: Enforces cover and containment at 7 critical stations along the member length.
- Returns comprehensive `RebarValidationResult` with specific error codes, exact 3D coordinates, expected vs. actual values, and diagnostic descriptions.

---

## 3. Detailed Component Hardening Summary

| Component | Audit Issue | Resolution |
| :--- | :--- | :--- |
| `RebarShapeCreationHelper.cs` | Silent straight-bar fallback and hook stripping on curve errors | Stripped all fallback logic. Throws explicit `InvalidOperationException` with details so callers report `NEED DESIGN INPUT`. |
| `RebarSafetyValidator.cs` | World BoundingBox 20mm arbitrary tolerance | Replaced with host local transform coordinates (`invTf.OfPoint`) and solid envelope containment. |
| `RebarHostContainmentValidator.cs` | Flagged valid multi-host extensions (column splices, beam anchorages) as errors | Introduced `ValidateHostContainmentWithIntent`. Allows extensions into `ConnectedHost` while strictly flagging free-space breaches. |
| `ColumnContinuityEngine.cs` | Center offset calculated in world XY failed for rotated columns | Projects delta center onto local axes `basisX` and `basisY` derived from column rotation. |
| `RectangularColumnRebarGenerator.cs` | Fallback to straight bar on bend failure | Eliminated straight bar fallback; wired `DetailingIntentContext` with `AdjacentColumnAbove`. |
| `CircularColumnRebarGenerator.cs` | Fallback to straight bar on bend failure | Eliminated straight bar fallback; wired `DetailingIntentContext` with `AdjacentColumnAbove`. |
| `BeamRebarGenerator.cs` | Fallback to straight bar for bent continuous bars | Eliminated straight bar fallback; wired `DetailingIntentContext` with detected supporting columns/beams. |
| `FoundationRebarGenerator.cs` | Starter dowels extending into column flagged as footing protrusions | Wired `DetailingIntentContext` with `SupportedColumn.Column` as `ConnectedHost`. |

---

## 4. Test Suite Execution & Verification Results

The automated regression and hardening test runner (`.tools/run_rebar_tests.ps1`) executed all 21 test suites:

```text
============================================================
RUNNING REBAR ENGINE TEST SUITE
============================================================
[PASS] Standards: Eurocode 2 (EC2) Formulas
       LapTension=1072.2mm, LapComp=1072.2mm, ClearSpacing=25mm, HookTail=200mm
[PASS] Standards: TCVN 5574:2018 Formulas
       Anchorage=666.7mm, Lap=1000.0mm (alpha=1.2), BeamCover=30mm
[PASS] SafetyValidator: Commercial Stock Length Check (<= 11.7m)
       11.0m thanh thép hợp lệ; 12.0m phát hiện vượt quá 11.7m chính xác.
[PASS] SafetyValidator: Min Clear Rebar Spacing (>= 25mm / d_max)
       MinClearSpacing=25mm >= 25mm
[PASS] BeamRebar: AutoSideBars=false strictly disables side bars
       Khi AutoSideBars=false và ManualSideBars=false, shouldGenerate=false.
[PASS] BeamRebar: AutoSideBars threshold (H >= 600mm)
       H=500mm -> gen=False; H=700mm -> gen=True
[PASS] ColumnContinuity: 1:6 Crank Slope with Section Reduction
       Slope=6.0 (CrankHeight=450mm cho inward=75mm)
[PASS] ColumnContinuity: Section Reduction > 75mm requires separate starter dowels
       Offset=100mm > 75mm -> RequiresSeparateDowels=True
[PASS] ColumnContinuity: 50% Staggered Splice Offset (1.3 * Ls)
       Offset = 1040mm = 1.3 * 800mm
[PASS] LifecycleManager: Tag Format & Identification
       Tag: [KTools_Rebar]|Module:Column|Host:12345|Role:RectangularColumn|v2.0
[PASS] BBS Engine: Bar Weight & Length Calculation
       TotalLength=50.0m, UnitWeight=2.466kg/m, TotalWeight=123.3kg
[PASS] ContainmentValidator: Physical Bar Envelope (d/2) Penetration & Cover Check
       Case A Protrude=5mm (FAIL); Case B Cover=5mm < 30mm (FAIL); Case C Valid=True (PASS)
[PASS] TransverseSectionQA: 7 Critical Stations (0%, A1, 25%, Midspan, 75%, A1, 100%)
       Đủ 7 trạm khảo sát mặt cắt ngang theo đúng tỷ lệ hình học kết cấu.
[PASS] LongitudinalSectionQA: End Face Anchorage & Hook Containment
       Thanh A đâm xuyên mặt đầu bị phát hiện; Thanh B neo trọn vẹn trong bê tông đạt PASS.
[PASS] ContainmentValidator: 3D Rotated Host Normal Vector Signed Distance
       DistInside=-20.00 (âm), DistOutside=20.00 (dương)
[PASS] DetailingIntent: Multi-Host Containment (ConnectedHost PASS vs Free-Space FAIL)
       P1 (Inside Host A)=True; P2 w/o Intent=False (FAIL); P2 w/ ConnectedHost=True (PASS); P3 FreeSpace=False (FAIL)
[PASS] StructuralConnection: Local Coordinate Projection across Rotations (0°, 15°, 30°, 45°, 90°)
       0°: dx=45.0, dy=-25.0, dz=100.0; 15°: dx=45.0, dy=-25.0, dz=100.0; 30°: dx=45.0, dy=-25.0, dz=100.0; 45°: dx=45.0, dy=-25.0, dz=100.0; 90°: dx=45.0, dy=-25.0, dz=100.0
[PASS] ColumnTransition: 75mm Threshold (<= 75mm Crank 1:6 vs > 75mm Separate Dowels)
       Limit=75mm | 50mm: Crank=True, Dowel=False; 75mm: Crank=True, Dowel=False; 76mm: Crank=False, Dowel=True
[PASS] Eurocode 2: Mandrel Diameter Verification (EC2 Table 8.1N & Failure Injection)
       m10=40mm (4d), m16=64mm (4d), m20=140mm (7d), m25=175mm (7d); Deficient 100mm flagged=True
[PASS] EngineeringSafety: Zero Silent Degradation on Failed Geometric Constraints
       Khi không đáp ứng điều kiện hình học uốn (Offset > 75mm), hệ thống dừng lại và yêu cầu thép chờ rời (NEED DESIGN INPUT) thay vì tự ý vẽ thanh thẳng.
[PASS] EngineeringValidator: Section 34 Comprehensive Validation Result & Diagnostics
       InitialValid=True, AfterErrorsValid=False, ViolationsCount=2, FailureReason='Cốt thép đâm thủng ra ngoài bê tông | Vi phạm lớp bảo vệ'
============================================================
SUMMARY: Total=21, Pass=21, Fail=0
============================================================
```

---

## 5. Conclusion & Production Readiness

The K-TOOLS Rebar system now strictly satisfies all engineering constraints:
1. **Technically Correct First**: Separates design parameters, detailing decisions, and Revit geometries cleanly.
2. **Zero Silent Degradation**: No bar is ever converted to straight or stripped of hooks without user awareness.
3. **Eurocode 2 Compliant**: Mandrel diameters, clear spacings, lap splices, and anchorages conform to EN 1992-1-1.
4. **Rotated & Multi-Host Ready**: Robust geometric projections for rotated framing and cross-host detailing intent.
5. **100% Pass Rate**: Validated by 21 automated unit and regression tests.

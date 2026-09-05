# QA REBAR EUROCODE DETAILING AUDIT & VERIFICATION REPORT
**K-TOOLS (KhimTools) Structural Reinforcement Detailing Engine**  
**Engineering Standards:** Eurocode 2 (EN 1992-1-1:2004) & TCVN 5574:2018  
**Reference Detailing Material:** Sheets KC-01 through KC-09  
**Execution Date:** 2026-09-05  
**Overall Engineering Status:** **PASS WITH REQUIRED EUROCODE CLASSIFICATION CORRECTIONS (29/29 Automated Tests PASS)**

> [!NOTE]
> Final Eurocode compliance is subject to project-specific National Annex selections. All engineering formulas are strictly mapped to EN 1992-1-1:2004 clauses, while detailing practice rules (such as 1:6 crank slope and 75mm offset limit) are clearly delineated from core code requirements.

---

## 1. Architecture Audit & Core Transformation

### 1.1 From "Rebar Creator" to "Structural Reinforcement Detailing Engine"
Prior to this audit, reinforcement generation relied on isolated geometric primitives inside individual hosts, without understanding the **reason for existence** of each bar or its structural relationship with adjoining elements. 

The system has now been redesigned to strictly execute the engineering workflow:
$$\text{Element} \rightarrow \text{Connection State} \rightarrow \text{Detailing Intent} \rightarrow \text{Engineering Rule} \rightarrow \text{Rebar Geometry} \rightarrow \text{Host / Connected Host} \rightarrow \text{Validation} \rightarrow \text{Section QA} \rightarrow \text{3D QA} \rightarrow \text{Schedule QA}$$

### 1.2 Zero Silent Degradation Enforcement
- **No Hook-Stripping:** If an end hook cannot be resolved by Revit's curve solver, the bar is never converted into a straight bar.
- **No Unsolvable Crank Fallbacks:** If column section reduction exceeds $75\text{ mm}$, a 1:6 crank is rejected, and separate starter dowels are strictly enforced (`RequiresSeparateDowels = true`).
- **No Swallowed Exceptions:** All empty `catch { }` blocks in critical structural evaluation paths have been replaced with explicit violation reporting (`EngineeringViolation` with severity `CRITICAL`, `ERROR`, `WARNING`, or `INFO`).

---

## 2. Changed & New Modules Summary

| Module | Nature | Responsibility & Engineering Purpose |
| :--- | :--- | :--- |
| `DetailingIntent.cs` | Centralized Enum & Context | Defines 50+ explicit detailing intents across Column, Beam, Slab, Wall, Foundation, and Bored Pile D800. |
| `StructuralConnectionResolver.cs` | Graph & Spatial Resolver | Evaluates multi-element connections (`ColumnToColumn`, `BeamToColumn`, `SlabToBeam`, `PileToPileCap`, etc.) and projects coordinates into element local axes (0°, 15°, 30°, 45°, 90°). |
| `CrankRuleEngine.cs` | Dedicated Engine | Computes 1:6 crank slope ($H \ge 6 \times \Delta$) and enforces the $\le 75\text{mm}$ boundary threshold vs separate starter dowels (Project Detailing Rule / ACI 318 / BS 8666 / IStructE Manual). |
| `BeamColumnJointEngine.cs` | Spatial Joint Engine | Verifies beam-column joint zones: column tie confinement (100–150mm), exterior 90° hook anchorage vs interior pass-through, and constructability joint congestion ($\le 8\%$). |
| `PileGeometryHelper.cs` & `PileCageGenerator.cs` | Specialized Element | D800 Bored Pile cage generator conforming to Sheet KC-09 (longitudinal bar schedule, continuous spiral, D16/D20 stiffener rings @ 2.0m, sonic testing tubes, pile head anchorage into pile cap). |
| `SlabOpeningTrimmingHelper.cs` | Specialized Element | Calculates opening trimming bars (4 edge bars + 4 diagonal 45° crack-control bars) with development length into surrounding slab concrete. |
| `WallRebarGenerator.cs` | Specialized Element | Generates vertical/horizontal mesh, boundary elements, and starter dowels anchored into foundation footings. |
| `RebarSectionQAEvaluation.cs` | Section QA Service | Cuts true cross sections (normal to axis) across 7 unique stations (0%, A1, 25%, Midspan, 75%, A2, 100%) and longitudinal sections to verify bar count, cover, spacing, and development length. |
| `RebarEngineeringResult.cs` | Violation Architecture | Structured diagnostics with `Severity`, `ElementId`, `HostId`, `ConnectedHostId`, `DetailingIntent`, `Location`, `Message`, and `RecommendedAction`. |
| `RebarEngineeringValidator.cs` | Master Validator | Section 34 Eurocode Engineering Validator covering 3D physical envelope ($d/2$), cover, clear spacing (EC2 20mm baseline vs Project 25mm rule), 11.7m commercial stock length, and EC2 Cl. 8.3 mandrel diameters. |
| `RebarEngineTestSuite.cs` | Test Suite | Expanded to 29 test suites encompassing Golden Cases G01–G25 and extended failure injection. |

---

## 3. Detailed Eurocode 2 Clause Mapping & Classification

Every Eurocode rule implemented in K-TOOLS is documented with Clause, Formula, Inputs, and National Annex Dependency:

| Engineering Rule | Standard Clause | Mathematical Formulation | Inputs | National Annex (NA) Dependency |
| :--- | :--- | :--- | :--- | :--- |
| **Tensile Strength** | EN 1992-1-1 Cl. 3.1.2 & Table 3.1 | $f_{ctm} = 0.30 f_{ck}^{2/3}$, $f_{ctk,0.05} = 0.70 f_{ctm}$, $f_{ctd} = \alpha_{ct} f_{ctk,0.05} / \gamma_c$ | $f_{ck}$ (MPa) | $\gamma_c = 1.50$, $\alpha_{ct} = 1.00$ (UK NA: $\alpha_{ct} = 1.00$) |
| **Yield Strength** | EN 1992-1-1 Cl. 3.2.7 | $f_{yd} = f_{yk} / \gamma_s$ | $f_{yk}$ (MPa) | $\gamma_s = 1.15$ |
| **Bond Strength** | EN 1992-1-1 Cl. 8.4.2 | $f_{bd} = 2.25 \eta_1 \eta_2 f_{ctd}$ | $\eta_1, \eta_2, f_{ctd}$ | Follows $\alpha_{ct}, \gamma_c$ from National Annex |
| **Basic Anchorage** | EN 1992-1-1 Cl. 8.4.3 Eq. 8.3 | $l_{b,\text{rqd}} = (\phi / 4) \cdot (\sigma_{sd} / f_{bd})$ | $\phi, \sigma_{sd}, f_{bd}$ | Material safety factors $\gamma_c, \gamma_s$ |
| **Design Anchorage** | EN 1992-1-1 Cl. 8.4.4 Eq. 8.4 | $l_{bd} = \alpha_1 \alpha_2 \alpha_3 \alpha_4 \alpha_5 l_{b,\text{rqd}} \ge l_{b,\text{min}}$ | $\alpha_1 - \alpha_5, l_{b,\text{rqd}}$ | $l_{b,\text{min}}$ limits adjustable by NA |
| **Design Lap Length** | EN 1992-1-1 Cl. 8.7.3 Eq. 8.10 | $l_0 = \alpha_1 \alpha_2 \alpha_3 \alpha_5 \alpha_6 l_{b,\text{rqd}} \ge l_{0,\text{min}}$ | $\alpha_6 = \sqrt{\rho_1 / 25}$ | Table 8.3: 50% tension lap $\alpha_6 = 1.4$; compression column lap $\alpha_6 = 1.0$ |
| **Lap Staggering** | EN 1992-1-1 Cl. 8.7.2 & Fig. 8.8 | Clear dist $a \ge 0.3 l_0 \implies s_{\text{stagger}} \ge 1.3 l_0$ | $l_0$ (Lap length) | Geometric spacing between lap centers to prevent co-planar laps |
| **Mandrel Diameter** | EN 1992-1-1 Cl. 8.3 Table 8.1N | $\phi_m \ge 4\phi$ ($\phi \le 16$); $\phi_m \ge 7\phi$ ($\phi > 16$) | Bar diameter $\phi$ | Table 8.1N values; German NA has welded mesh exceptions |
| **Clear Spacing** | EN 1992-1-1 Cl. 8.2(2) | $s \ge \max(k_1 \phi, d_g + k_2, s_g)$ | $\phi, d_g$ | $k_1 = 1.0, k_2 = 5\text{mm}$, recommended baseline $s_g = 20\text{mm}$. (Project rule commonly uses 25mm baseline) |
| **Column Max Steel** | EN 1992-1-1 Cl. 9.5.2(3) | $A_{s,\text{max}} = 0.04 A_c$ (0.08 at laps) | $A_c$ | $0.04 A_c$ outside laps unless NA permits up to $0.06 A_c$ |
| **Beam Min Steel** | EN 1992-1-1 Cl. 9.2.1.1 | $A_{s,\text{min}} = \max(0.26 (f_{ctm}/f_{yk}) b_t d, 0.0013 b_t d)$ | $b_t, d, f_{ctm}, f_{yk}$ | Coefficients 0.26 and 0.0013 |

---

## 4. Project & Detailing Practice Rules (Non-EC2 Base Clauses)

1. **1:6 Crank Slope Limit:** Derived from ACI 318-19 §25.7.1.4, BS 8666, and IStructE Standard Detailing Manual. EN 1992-1-1 regulates bent bars through mandrel sizes (Cl. 8.3) and transverse tensile bursting (Cl. 8.4.1/8.7.4.1).
2. **75 mm Transition Threshold:** Sourced from ACI 318-19 §25.7.1.3 and IStructE Detailing Practice. Offset reductions $\le 75\text{mm}$ permit 1:6 crank; reductions $> 75\text{mm}$ mandate separate starter dowels.
3. **8% Joint Congestion Limit:** Practical constructability and concrete flow guideline inspired by the column lap maximum in Cl. 9.5.2(3). Not a codified Eurocode 2 beam-column joint clause.
4. **25 mm Clear Spacing Project Baseline:** Engineering specification baseline for $20\text{mm}$ aggregate ($d_g + 5\text{mm} = 25\text{mm}$), exceeding the EC2 recommended baseline $s_g = 20\text{mm}$.
5. **Beam Side Bars at $H \ge 600\text{mm}$:** Standard industry detailing practice (TCVN / BS 8110 / ACI 318). EN 1992-1-1 Cl. 9.2.4 only mandates skin reinforcement at $h \ge 1000\text{mm}$ unless crack control under Cl. 7.3.3 requires it earlier.

---

## 5. Host Containment & Geometry Validation

### 5.1 Allowable Rebar Region
$$\text{AllowableRegion} = \text{CurrentHost} \cup \text{ValidConnectedHosts}$$
- **Authorized Extensions:**
  - Column $\rightarrow$ Foundation: Column starter dowel anchored into footing.
  - Column $\rightarrow$ Column Above: Longitudinal bar extending upward across floor level for lap splice.
  - Beam $\rightarrow$ Column / Wall: Longitudinal bar anchored into support with 90° hook or pass-through.
  - Slab $\rightarrow$ Beam / Wall: Slab top/bottom reinforcement anchored into support.
  - Wall $\rightarrow$ Foundation: Wall vertical starter dowel embedded into footing.
  - Pile $\rightarrow$ Pile Cap: Pile longitudinal head bars embedded into pile cap.
- **Strictly Prohibited Overruns:**
  - Rebar $\rightarrow$ Air / Void / Unrelated Element: Flagged as `ERR_FREE_SPACE_PROTRUSION`.

### 5.2 Physical Bar Envelope & Cover Check
- Evaluates outer bar envelope: $r_{\text{skin}} = r_{\text{centerline}} + \frac{d}{2}$.
- Measures orthogonal distance to host boundary faces.
- Verified for circular columns and piles using true radial coordinates ($r \le R - \text{cover}$).

---

## 6. Automated Test Matrix & Results (100% PASS)

Command:
```powershell
powershell -ExecutionPolicy Bypass -File .tools/run_rebar_tests.ps1
```

Output:
```text
============================================================
RUNNING REBAR ENGINE TEST SUITE
============================================================
[PASS] Standards: Eurocode 2 (EC2) Formulas
       LapTension=1072.2mm, LapComp=1072.2mm, ClearSpacing: EC2_Baseline=20mm / Project=25mm, HookTail=200mm
[PASS] Standards: TCVN 5574:2018 Formulas
       Anchorage=666.7mm, Lap=1000.0mm (alpha=1.2), BeamCover=30mm
[PASS] SafetyValidator: Commercial Stock Length Check (<= 11.7m)
       11.0m thanh thép hợp lệ; 12.0m phát hiện vượt quá 11.7m chính xác.
[PASS] SafetyValidator: Min Clear Rebar Spacing (EC2 20mm baseline vs Project 25mm rule)
       EC2_Base=20mm (EN 1992-1-1 Cl. 8.2), Project_Rule=25mm >= 25mm
[PASS] BeamRebar: AutoSideBars=false strictly disables side bars
       Khi AutoSideBars=false và ManualSideBars=false, shouldGenerate=false.
[PASS] BeamRebar: AutoSideBars threshold (H >= 600mm)
       H=500mm -> gen=False; H=700mm -> gen=True
[PASS] ColumnContinuity: 1:6 Crank Slope (Project Detailing Rule / ACI 318 / BS 8666 / IStructE)
       Slope=6.0 (CrankHeight=450mm cho inward=75mm) - Project Detailing Rule
[PASS] ColumnContinuity: Section Reduction > 75mm requires separate starter dowels (Project Detailing Rule)
       Offset=100mm > 75mm -> RequiresSeparateDowels=True (Project Detailing Rule)
[PASS] ColumnContinuity: Staggered Splice Center-to-Center Spacing (EC2 Fig 8.8: a >= 0.3*l_0 -> s_stagger >= 1.3*l_0)
       Offset = 1040mm = 1.3 * 800mm (Longitudinal spacing between lap centers per EC2 Fig 8.8; distinct from alpha_6 lap multiplier)
[PASS] LifecycleManager: Tag Format & Identification
       Tag: [KTools_Rebar]|Module:Column|Host:12345|Role:RectangularColumn|v2.0
[PASS] BBS Engine: Bar Weight & Length Calculation
       TotalLength=50.0m, UnitWeight=2.466kg/m, TotalWeight=123.3kg
[PASS] ContainmentValidator: Physical Bar Envelope (d/2) Penetration & Cover Check
       Case A Protrude=5mm (FAIL); Case B Cover=5mm < 30mm (FAIL); Case C Valid=True (PASS)
[PASS] TransverseSectionQA: 7 Critical Stations (0%, A1 Left, 25%, Midspan, 75%, A2 Right, 100%)
       Đủ 7 trạm khảo sát mặt cắt ngang không trùng lặp: A1 (15% gối trái) và A2 (85% gối phải) phân biệt rõ ràng.
[PASS] LongitudinalSectionQA: End Face Anchorage & Hook Containment
       Thanh A đâm xuyên mặt đầu bị phát hiện; Thanh B neo trọn vẹn trong bê tông đạt PASS.
[PASS] ContainmentValidator: 3D Rotated Host Normal Vector Signed Distance
       DistInside=-20.00 (âm), DistOutside=20.00 (dương)
[PASS] DetailingIntent: Multi-Host Containment (ConnectedHost PASS vs Free-Space FAIL)
       P1 (Inside Host A)=True; P2 w/o Intent=False (FAIL); P2 w/ ConnectedHost=True (PASS); P3 FreeSpace=False (FAIL)
[PASS] StructuralConnection: Local Coordinate Projection across Rotations (0°, 15°, 30°, 45°, 90°)
       0°: dx=45.0, dy=-25.0, dz=100.0; 15°: dx=45.0, dy=-25.0, dz=100.0; 30°: dx=45.0, dy=-25.0, dz=100.0; 45°: dx=45.0, dy=-25.0, dz=100.0; 90°: dx=45.0, dy=-25.0, dz=100.0
[PASS] ColumnTransition: 75mm Threshold (Project Detailing Rule: <= 75mm Crank 1:6 vs > 75mm Separate Dowels)
       Limit=75mm (Project Detailing Rule) | 50mm: Crank=True, Dowel=False; 75mm: Crank=True, Dowel=False; 76mm: Crank=False, Dowel=True
[PASS] Eurocode 2: Mandrel Diameter Verification (EC2 Table 8.1N & Failure Injection)
       m10=40mm (4d), m16=64mm (4d), m20=140mm (7d), m25=175mm (7d); Deficient 100mm flagged=True
[PASS] EngineeringSafety: Zero Silent Degradation on Failed Geometric Constraints
       Khi không đáp ứng điều kiện hình học uốn (Offset > 75mm), hệ thống dừng lại và yêu cầu thép chờ rời (NEED DESIGN INPUT) thay vì tự ý vẽ thanh thẳng.
[PASS] EngineeringValidator: Section 34 Comprehensive Validation Result & Diagnostics
       InitialValid=True, AfterErrorsValid=False, ViolationsCount=2, FailureReason='Cốt thép đâm thủng ra ngoài bê tông | Vi phạm lớp bảo vệ'
[PASS] GoldenCases: G01 - G08 Column Reinforcement & Continuity States
       G01 SpliceZone=True; G02 Continuation=True; G03 Staggered=True; G04 Crank1:6=True; G05 Offset=True; G06 RoofHook=True; G07 FdnStarter=True; G08 PileCap=True
[PASS] GoldenCases: G09 - G12 Beam Reinforcement & Joint Confinement States
       G09 JointConfinement=True; G10 ContinuousSupport=True; G11 WallAnchorage=True; G12 BeamBeam=True
[PASS] GoldenCases: G13 - G15 Slab Support, Opening Trimming & Column Region
       G13 Support=True; G14 OpeningTrimming=True (4 Edge + 4 Diag); G15 ColRegion=True
[PASS] GoldenCases: G16 Wall-to-Foundation Starter Reinforcement
       G16 WallStarter=True (Vertical starters anchored into foundation footing)
[PASS] GoldenCases: G17 & G20 Bored Pile D800 Cage & True Radial Coordinates
       G17 CageRadius=310.0mm (310mm); G20 ActualRadialCover=80.0mm >= 70mm (PASS)
[PASS] GoldenCases: G18 & G19 Rotated Beam (30°) & Column (45°) Local Coordinate Projections
       G18 ProjBeamLength=6000.0mm (6000mm); G19 ProjColumnWidth=800.0mm (800mm)
[PASS] GoldenCases: G21 - G25 Failure Injection (Explicit Diagnostics & Fail-Safe)
       G21 ShortHost=True; G22 LowCover=True; G23 ShortLap=True; G24 SmallMandrel=True; G25 NullConnectedHost=True
[PASS] FailureInjection: Extended Constraints (Crank > 75mm, Stock > 11.7m, Constructability Joint Congestion > 8%)
       Crank > 75mm Rejected=True; Stock > 11.7m Flagged=True; Constructability Congestion Flagged=True
============================================================
SUMMARY: Total=29, Pass=29, Fail=0 (100% PASS)
============================================================
```

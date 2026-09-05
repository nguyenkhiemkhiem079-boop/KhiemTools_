# K-TOOLS REBAR ENGINEERING RULEBOOK
**Internal Technical & Detailing Specification**  
**Version:** 3.1  
**Scope:** KhimTools Automated Structural Reinforcement Detailing System  

---

## 1. Hierarchy of Engineering Rules

When detailing structural reinforcement in KhimTools, conflicts are resolved strictly according to this hierarchy:
1. **Active Design Standard / Code of Practice:** Eurocode 2 (EN 1992-1-1:2004) or TCVN 5574:2018 (Governing structural safety).
2. **National Annex (NA) Parameters:** Country-specific coefficients ($\gamma_c, \gamma_s, \alpha_{ct}, \alpha_{cc}, s_g, A_{s,max}$).
3. **Project-Specific Engineering / Detailing Rules:** Project cover setup, aggregate size, 1:6 crank limit, 75mm dowel threshold, 8% constructability joint congestion ceiling, client drawing standards.
4. **Element Configuration & Connection State:** Boundary constraints and adjacent connected framing members.
5. **Detailing Intent:** Structural role (lap splice, continuation, starter, trimming, confinement).
6. **Geometry Generator:** Local coordinate transformation and curve discretization.
7. **Revit API Constraints:** Revit shape limitations must **NEVER** override structural rules. When unsolvable, the system fails safe.

---

## 2. Category 1: Observed Drawing Detailing (Reference Material: KC-01 to KC-09)

The detailing conventions observed in project sheets KC-01 to KC-09 serve as **physical layout reference data**. Universal defaults are not hard-coded; all schedule-specific parameters remain fully configurable:

| Drawing Sheet | Structural Component | Detailing Observations & Physical Arrangement |
| :--- | :--- | :--- |
| **KC-01** | Typical Slab | Dual mat reinforcement (Top & Bottom grids); opening trimming consists of 4 orthogonal edge trimming bars (U-bars or straight bars) plus 4 diagonal 45° crack-control bars extending $\ge l_{bd}$ past opening corners into sound concrete. |
| **KC-03** | Structural Frame | Rectangular columns with perimeter ties and internal cross-ties (diamond/rhombus and C-hooks); main longitudinal bars aligned along perimeter faces with equal spacing. |
| **KC-04** | Multi-Floor Frame | Column longitudinal bars lapped above floor finish; splice locations concentrated in middle column height (avoiding plastic hinge zones $A_1$ at top and bottom); staggered lap arrangements. |
| **KC-05** | Upper Levels / Roof | Column top termination details: longitudinal bars bend 90° inward into roof slab/beams; no accidental protrusion above structural concrete. |
| **KC-06** | Beam Detailing | Longitudinal bars separated into Bottom Span, Bottom Support, Top Support (negative moment), and Top Span; stirrups grouped into dense support zones ($A_1$) and wider midspan zones ($A_2$). |
| **KC-07** | Beam-Column Joints | Continuous column ties maintained through beam-column intersection zones; beam bars hook downward into exterior columns; joint confinement maintained. |
| **KC-08** | Foundation & Footings | Bottom mat hooked upward 90°; top mat hooked downward 90°; column starter dowels have 90° horizontal legs resting on the bottom mat with embedment depth $\ge l_{bd}$. |
| **KC-09** | Bored Pile D800 | Circular cage: Longitudinal bar count and diameter are governed by the project structural schedule (configurable via `PileCageSettings.MainBarCount`, default 16 bars); continuous D10 spiral with pitch (100mm head/toe, 200mm body); Stiffener rings (D16 or D20 per project schedule and lifting rigidity calculation) @ 2.0m; 3 sonic testing tubes @ 120°; longitudinal bars extend $\ge 1000\text{mm}$ into pile cap. |

---

## 3. Category 2: Codified Eurocode 2 Engineering Rules (EN 1992-1-1:2004)

Every Eurocode rule in K-TOOLS is documented with Clause, Formula, Inputs, and National Annex (NA) Dependency:

### 3.1. Concrete Tensile & Compressive Strength
- **Clause:** EN 1992-1-1:2004 Table 3.1 & Cl. 3.1.2, Cl. 3.1.6
- **Formulas:**
  $$f_{ctm} = 0.30 \times f_{ck}^{2/3} \quad (\text{for } f_{ck} \le 50\text{ MPa})$$
  $$f_{ctk,0.05} = 0.70 \times f_{ctm}$$
  $$f_{ctd} = \frac{\alpha_{ct} \cdot f_{ctk,0.05}}{\gamma_c}, \quad f_{cd} = \frac{\alpha_{cc} \cdot f_{ck}}{\gamma_c}$$
- **Inputs:** $f_{ck}$ (Characteristic cylinder compressive strength, MPa).
- **National Annex (NA) Dependency:**
  - $\gamma_c$: Recommended = 1.50 (Persistent & Transient).
  - $\alpha_{ct}$: Recommended = 1.00 (UK NA = 1.00; French NA = 1.00; Irish NA = 1.00).
  - $\alpha_{cc}$: Recommended = 1.00 (UK NA = 0.85; Singapore NA = 0.85; Malaysia NA = 0.85).

### 3.2. Steel Design Yield Strength
- **Clause:** EN 1992-1-1:2004 Cl. 3.2.7
- **Formula:**
  $$f_{yd} = \frac{f_{yk}}{\gamma_s}$$
- **Inputs:** $f_{yk}$ (Characteristic yield strength of reinforcement, MPa).
- **National Annex (NA) Dependency:**
  - $\gamma_s$: Recommended = 1.15.

### 3.3. Ultimate Bond Strength
- **Clause:** EN 1992-1-1:2004 Cl. 8.4.2
- **Formula:**
  $$f_{bd} = 2.25 \cdot \eta_1 \cdot \eta_2 \cdot f_{ctd}$$
- **Inputs:**
  - $\eta_1 = 1.0$ ('good' bond conditions) or $0.7$ ('poor' bond conditions, e.g. horizontal top bars in elements $> 250\text{mm}$).
  - $\eta_2 = 1.0$ (for $\phi \le 32\text{ mm}$) or $(132 - \phi)/100$ (for $\phi > 32\text{ mm}$).
  - $f_{ctd}$ (Design tensile strength).
- **National Annex (NA) Dependency:** Governed by NA choices for $\alpha_{ct}$ and $\gamma_c$.

### 3.4. Basic Required Anchorage Length
- **Clause:** EN 1992-1-1:2004 Cl. 8.4.3 Eq. (8.3)
- **Formula:**
  $$l_{b,\text{rqd}} = \left(\frac{\phi}{4}\right) \cdot \left(\frac{\sigma_{sd}}{f_{bd}}\right)$$
- **Inputs:** Bar diameter $\phi$, design stress $\sigma_{sd}$ (taken as $f_{yd}$ for full capacity), $f_{bd}$.
- **National Annex (NA) Dependency:** Directly governed by NA values for material safety factors.

### 3.5. Design Anchorage Length
- **Clause:** EN 1992-1-1:2004 Cl. 8.4.4 Eq. (8.4) & Table 8.2
- **Formula:**
  $$l_{bd} = \alpha_1 \alpha_2 \alpha_3 \alpha_4 \alpha_5 l_{b,\text{rqd}} \ge l_{b,\text{min}}$$
  $$l_{b,\text{min}} \ge \max\left(0.3 l_{b,\text{rqd}}, 10\phi, 100\text{ mm}\right) \quad (\text{Tension})$$
  $$l_{b,\text{min}} \ge \max\left(0.6 l_{b,\text{rqd}}, 10\phi, 100\text{ mm}\right) \quad (\text{Compression})$$
- **Inputs:** Coefficients $\alpha_1$ (bar form: 0.7 for standard hook if $c_d > 3\phi$), $\alpha_2$ (cover), $\alpha_3$ (confinement), $\alpha_4$ (welded bars), $\alpha_5$ (transverse pressure).
- **National Annex (NA) Dependency:** Limits for $l_{b,\text{min}}$ and coefficient bounds may be adjusted by NA.

### 3.6. Design Lap Length & Staggering Distance
- **Clause:** EN 1992-1-1:2004 Cl. 8.7.3 Eq. (8.10), Table 8.3, and Cl. 8.7.2 / Figure 8.8
- **Formulas:**
  $$l_0 = \alpha_1 \alpha_2 \alpha_3 \alpha_5 \alpha_6 l_{b,\text{rqd}} \ge l_{0,\text{min}}$$
  $$l_{0,\text{min}} \ge \max\left(0.3 \alpha_6 l_{b,\text{rqd}}, 15\phi, 200\text{ mm}\right)$$
  $$\alpha_6 = \sqrt{\frac{\rho_1}{25}}, \quad 1.0 \le \alpha_6 \le 1.5$$
  - Table 8.3:
    - $\rho_1 \le 25\% \implies \alpha_6 = 1.0$
    - $\rho_1 = 33\% \implies \alpha_6 = 1.15$
    - $\rho_1 = 50\% \implies \alpha_6 = 1.4$ (Tension lap)
    - $\rho_1 > 50\% \implies \alpha_6 = 1.5$
    - Compression columns (predominantly compression): $\alpha_6 = 1.0$ (all bars may be lapped at one section per Cl. 8.7.3 Note).
  - **Staggering Distance Between Laps (Cl. 8.7.2 & Figure 8.8):**
    Clear distance between adjacent laps $a \ge 0.3 l_0$. Longitudinal center-to-center offset between lap centers:
    $$s_{\text{stagger}} \ge l_0 + a = 1.3 l_0$$
    *(Important: $1.3 l_0$ represents the longitudinal geometric staggering offset between adjacent lap centers, NOT a generic lap length multiplier).*
- **National Annex (NA) Dependency:** $\alpha_6$ values and $l_{0,\text{min}}$ minimums can be specified by NA.

### 3.7. Minimum Mandrel Diameter for Bends and Hooks
- **Clause:** EN 1992-1-1:2004 Cl. 8.3 & Table 8.1N
- **Formulas:**
  $$\phi_m \ge 4\phi \quad (\text{for } \phi \le 16\text{ mm})$$
  $$\phi_m \ge 7\phi \quad (\text{for } \phi > 16\text{ mm})$$
- **Inputs:** Bar diameter $\phi$.
- **National Annex (NA) Dependency:** Recommended values in Table 8.1N. National Annexes (e.g. German NA) specify requirements for welded wire mesh bends.

### 3.8. Minimum Clear Spacing Between Bars
- **Clause:** EN 1992-1-1:2004 Cl. 8.2(2)
- **Formula:**
  $$s_{\text{clear}} \ge \max\left(k_1 \cdot \phi, d_g + k_2, s_g\right)$$
- **Inputs:** Bar diameter $\phi$, maximum aggregate size $d_g$.
- **National Annex (NA) Dependency:**
  - Recommended values: $k_1 = 1.0$, $k_2 = 5\text{ mm}$, and $s_g = 20\text{ mm}$.
  - Note: $s_g = 20\text{ mm}$ is the EC2 recommended baseline. A project detailing rule often imposes $25\text{ mm}$ (accounting for standard $d_g = 20\text{ mm} + 5\text{ mm} = 25\text{ mm}$ or contractor minimum).

### 3.9. Column Longitudinal Reinforcement Ratio Limits
- **Clause:** EN 1992-1-1:2004 Cl. 9.5.2(1)-(3)
- **Formulas:**
  $$A_{s,\text{min}} = \max\left(\frac{0.10 N_{Ed}}{f_{yd}}, 0.002 A_c\right)$$
  $$A_{s,\text{max}} = 0.04 A_c \quad (\text{outside laps})$$
  $$A_{s,\text{max,lap}} = 0.08 A_c \quad (\text{at laps})$$
- **Inputs:** Column concrete area $A_c$, design axial force $N_{Ed}$, $f_{yd}$.
- **National Annex (NA) Dependency:** Maximum ratio outside laps is 0.04 unless higher values (up to 0.06) are permitted by NA provided concrete compaction is maintained.

### 3.10. Beam Longitudinal Reinforcement Ratio Limits
- **Clause:** EN 1992-1-1:2004 Cl. 9.2.1.1
- **Formulas:**
  $$A_{s,\text{min}} = \max\left(0.26 \frac{f_{ctm}}{f_{yk}} b_t d, 0.0013 b_t d\right)$$
  $$A_{s,\text{max}} = 0.04 A_c$$
- **Inputs:** Tension zone width $b_t$, effective depth $d$, $f_{ctm}, f_{yk}, A_c$.
- **National Annex (NA) Dependency:** Coefficients 0.26 and 0.0013 may be adjusted by NA.

---

## 4. Category 3: Project & Detailing Practice Rules (Non-EC2 Direct Clauses)

The following rules represent **industry detailing practice, constructability requirements, or project specifications**; they must **NOT** be claimed as direct Eurocode 2 clauses:

1. **1:6 Crank Slope Limit:**
   - **Origin:** Standard Detailing Practice / ACI 318-19 §25.7.1.4 / BS 8666 / IStructE Detailing Manual.
   - **Rule:** The slope of the inclined portion of an offset bent column bar shall not exceed 1:6 ($H \ge 6 \times \Delta$).
   - *(EC2 Note: EN 1992-1-1 does not codify a 1:6 ratio; it governs bent bars via mandrel diameter Cl. 8.3 and transverse tensile bursting reinforcement Cl. 8.4.1/8.7.4.1).*
2. **75 mm Transition Offset Threshold:**
   - **Origin:** ACI 318-19 §25.7.1.3 / IStructE Standard Method of Detailing.
   - **Rule:** If column edge offset $\Delta \le 75\text{ mm}$, cranking at 1:6 slope is permitted. If $\Delta > 75\text{ mm}$, cranking is prohibited; lower bars terminate with 90° hooks and separate starter dowels must be installed.
3. **8% Joint Congestion Limit:**
   - **Origin:** Constructability & Detailing Practice (Concrete Placement & Honeycombing Prevention).
   - **Rule:** Total longitudinal steel ratio in the beam-column joint core should not exceed 8.0%.
   - *(EC2 Note: EN 1992-1-1 has no explicit 8% joint congestion clause. This limit is adopted as a constructability rule inspired by the 0.08 column lap ceiling in Cl. 9.5.2(3)).*
4. **25 mm Clear Spacing Project Baseline:**
   - **Origin:** Project Detailing Specification (Aggregate $d_g = 20\text{ mm} + 5\text{ mm} = 25\text{ mm}$).
   - **Rule:** Baseline clear spacing between parallel bars $\ge 25\text{ mm}$.
   - *(EC2 Note: EN 1992-1-1 Cl. 8.2(2) recommends $s_g = 20\text{ mm}$ as baseline).*
5. **Beam Side Bars (Skin Reinforcement) at $H \ge 600\text{ mm}$:**
   - **Origin:** Project Detailing Practice (TCVN 5574 / BS 8110 / ACI 318).
   - **Rule:** Skin/side bars provided when overall beam depth $H \ge 600\text{ mm}$.
   - *(EC2 Note: EN 1992-1-1 Cl. 9.2.4 only mandates skin reinforcement for beams with depth $h \ge 1000\text{ mm}$, unless crack control under Cl. 7.3.3 requires earlier web steel).*
6. **Commercial Stock Length Limit:**
   - Continuous straight stock bar length is capped at $11.7\text{ m}$ (or $12.0\text{ m}$). Any longer run requires a lap splice or mechanical coupler.

---

## 5. Category 4: Software Implementation Rules

1. **Local Coordinate System Invariance:**
   Every member evaluates offsets, section reductions, covers, and cut lines along local axes ($\text{BasisX}, \text{BasisY}, \text{BasisZ}$), maintaining complete mathematical invariance across 0°, 15°, 30°, 45°, 90° rotations.
2. **Allowable Multi-Host Rebar Region:**
   $$\text{AllowableRegion} = \text{CurrentHost} \cup \text{ValidConnectedHosts}$$
   Extensions into authorized connected hosts pass; extensions into air or unrelated elements fail with `ERR_FREE_SPACE_PROTRUSION`.
3. **Fail-Safe Policy (Zero Silent Degradation):**
   When shape solvers fail or inputs are missing, return `NEED_DESIGN_INPUT` or `ENGINEERING_VALIDATION_FAILED`. Never substitute straight bars for failed hooks or cranks.
4. **Section QA 7-Station Evaluation:**
   Cross-sections are evaluated at 7 unique, non-duplicated stations along the member span:
   - Station 1: Start Support Face (0%)
   - Station 2: Left Confinement Zone A1 (15%)
   - Station 3: Quarter Span (25%)
   - Station 4: Midspan (50%)
   - Station 5: Three-Quarter Span (75%)
   - Station 6: Right Confinement Zone A2 (85%)
   - Station 7: End Support Face (100%)

---

## 6. Category 5: Software Limitations

1. **Variable Cross-Section Tapering:** Non-prismatic elements with variable section taper require stepped modeling.
2. **Coupler 3D Solids:** Mechanical couplers are validated via lap length equivalents; 3D coupler hardware geometry requires external family libraries.
3. **Curved Slabs:** Curved shell slabs must be partitioned into polygonal planar panels for detailing.

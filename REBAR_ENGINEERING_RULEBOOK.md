# K-TOOLS REBAR ENGINEERING RULEBOOK
**Internal Technical & Detailing Specification**  
**Version:** 3.0  
**Scope:** KhimTools Automated Structural Reinforcement Detailing System  

---

## 1. Hierarchy of Engineering Rules

When detailing structural reinforcement in KhimTools, conflicts are resolved strictly according to this hierarchy:
1. **Active Design Standard / Code of Practice:** Eurocode 2 (EN 1992-1-1:2004) or TCVN 5574:2018 (Governing structural safety).
2. **Project-Specific Engineering / Detailing Rules:** Project cover setup, designated material strengths, and client drawing standards.
3. **Element Configuration & Connection State:** Boundary constraints and adjacent connected framing members.
4. **Detailing Intent:** Structural role (lap splice, continuation, starter, trimming, confinement).
5. **Geometry Generator:** Local coordinate transformation and curve discretization.
6. **Revit API Constraints:** Revit shape limitations must **NEVER** override structural rules. When unsolvable, the system fails safe.

---

## 2. Category 1: Observed Drawing Detailing (Reference Material: KC-01 to KC-09)

The detailing conventions observed in project sheets KC-01 to KC-09 serve as **physical layout reference data**:

| Drawing Sheet | Structural Component | Detailing Observations & Physical Arrangement |
| :--- | :--- | :--- |
| **KC-01** | Typical Slab | Dual mat reinforcement (Top & Bottom grids); opening trimming consists of 2–4 edge U-bars plus 2x45° diagonal crack-control bars per corner extending $\ge l_{bd}$ past opening corners. |
| **KC-03** | Structural Frame | Rectangular columns with perimeter ties and internal cross-ties (diamond/rhombus and C-hooks); main longitudinal bars aligned along perimeter faces with equal spacing. |
| **KC-04** | Multi-Floor Frame | Column longitudinal bars lapped above floor finish; splice locations concentrated in middle column height (avoiding plastic hinge zones $A_1$ at top and bottom); 50% staggered offsets. |
| **KC-05** | Upper Levels / Roof | Column top termination details: longitudinal bars bend 90° inward into roof slab/beams; no accidental protrusion above structural concrete. |
| **KC-06** | Beam Detailing | Longitudinal bars separated into Bottom Span, Bottom Support, Top Support (negative moment), and Top Span; stirrups grouped into dense support zones ($A_1$) and wider midspan zones ($A_2$). |
| **KC-07** | Beam-Column Joints | Continuous column ties maintained through beam-column intersection zones; beam bars hook downward into exterior columns; joint confinement maintained. |
| **KC-08** | Foundation & Footings | Bottom mat hooked upward 90°; top mat hooked downward 90°; column starter dowels have 90° horizontal legs resting on the bottom mat with embedment depth $\ge l_{bd}$. |
| **KC-09** | Bored Pile D800 | Circular cage: 16–24 longitudinal bars D20/D25 arranged uniformly in a circle; continuous D10 spiral at 100mm pitch (head/toe) and 200mm pitch (body); D16 stiffener rings @ 2.0m; 3 sonic testing tubes @ 120°; longitudinal bars extend $\ge 1000\text{mm}$ into pile cap. |

---

## 3. Category 2: Eurocode 2 Engineering Rules (EN 1992-1-1:2004)

1. **Basic Anchorage Length (Clause 8.4.3):**
   $$l_{b,\text{rqd}} = \frac{\phi}{4} \frac{f_{yd}}{f_{bd}}$$
   where $f_{bd} = 2.25 \eta_1 \eta_2 f_{ctd}$.
2. **Design Anchorage Length (Clause 8.4.4):**
   $$l_{bd} = \alpha_1 \alpha_2 \alpha_3 \alpha_4 \alpha_5 l_{b,\text{rqd}} \ge l_{b,\text{min}}$$
   where $\alpha_1 = 0.7$ for hooked bars in tension, and $l_{b,\text{min}} \ge \max(0.3 l_{b,\text{rqd}}, 10\phi, 100\text{mm})$.
3. **Design Lap Length (Clause 8.7.3):**
   $$l_0 = \alpha_1 \alpha_2 \alpha_3 \alpha_5 \alpha_6 l_{b,\text{rqd}} \ge l_{0,\text{min}}$$
   where $\alpha_6 = 1.5$ for $100\%$ lapped bars, and $\alpha_6 = 1.15$ for $50\%$ staggered laps.
4. **Minimum Mandrel Diameter (Clause 8.3 & Table 8.1N):**
   - For $\phi \le 16\text{ mm}$: $\text{Mandrel} \ge 4\phi$.
   - For $\phi > 16\text{ mm}$: $\text{Mandrel} \ge 7\phi$.
5. **Clear Spacing (Clause 8.2):**
   $$s_{\text{clear}} \ge \max\left(k_1 \phi, d_g + k_2, 20\text{ mm}\right) \ge 25\text{ mm}$$
6. **Column Transition Crank Limit (ACI 318 / Eurocode practice):**
   - Slope $\le 1:6$ (Height $\ge 6 \times \text{inward offset}$).
   - Section reduction offset $\le 75\text{ mm}$. If offset $> 75\text{ mm}$, cranking is prohibited; separate starter dowels are strictly required.

---

## 4. Category 3: Project Configuration Rules

1. **Commercial Stock Length Limit:**
   Standard maximum length of continuous straight stock bars is $11.7\text{ m}$ (or $12.0\text{ m}$ by special order). Any bar exceeding $11.7\text{ m}$ must introduce a lap splice or mechanical coupler.
2. **Concrete Cover Conventions:**
   - Slabs: $20\text{ mm} - 25\text{ mm}$ (Internal exposure XC1).
   - Beams & Columns: $30\text{ mm} - 35\text{ mm}$ (XC1 / XC2).
   - Foundations & Footings: $50\text{ mm}$ (Contact with ground).
   - Bored Piles: $70\text{ mm}$ (Underground cast against soil).
3. **Staggered Splice Stagger Offset:**
   $1.3 \times l_0$ between alternating longitudinal bar splices.

---

## 5. Category 4: Software Implementation Rules

1. **Local Coordinate Systems:**
   Every member evaluates offsets, section reductions, covers, and cut lines along local axes ($\text{BasisX}, \text{BasisY}, \text{BasisZ}$), maintaining complete invariance across 0°, 15°, 30°, 45°, 90° rotations.
2. **Allowable Rebar Region:**
   $$\text{AllowableRegion} = \text{CurrentHost} \cup \text{ValidConnectedHosts}$$
   Extensions into authorized connected hosts pass; extensions into air or unrelated elements fail with `ERR_FREE_SPACE_PROTRUSION`.
3. **Fail-Safe Policy:**
   When shape solvers fail or inputs are missing, return `NEED_DESIGN_INPUT` or `ENGINEERING_VALIDATION_FAILED`. Never substitute straight bars for failed hooks or cranks.
4. **Identity Traceability:**
   Each rebar stores its `BarMark`, `DetailingIntent`, `Host`, and `ConnectedHost` to guarantee that Plan, Section, 3D, and Bar Schedule (BBS) represent the identical physical bar.

---

## 6. Category 5: Software Limitations

1. **Variable Cross-Section Tapering:** Non-prismatic elements with variable section taper require stepped modeling.
2. **Coupler 3D Solids:** Mechanical couplers are validated via lap length equivalents; 3D coupler hardware geometry requires external family libraries.
3. **Curved Slabs:** Curved shell slabs must be partitioned into polygonal planar panels for detailing.

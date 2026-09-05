# K-TOOLS REBAR STRUCTURAL CONNECTION MATRIX REPORT

This matrix documents the structural relationships, detailing intents, allowed extensions, engineering rules, and verification states across all supported structural connection types in K-TOOLS.

---

## 1. Master Connection Matrix

| Element A | Connection Type | Element B | Detailing Intent | Allowed Extension Region | Anchorage Rule | Lap & Stagger Rule | Cover Rule | Clear Spacing Rule | Section QA | 3D QA | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Column (Lower)** | `ColumnToColumn` | **Column (Upper)** | `COLUMN_CONTINUATION` / `COLUMN_LAP_SPLICE` | Upper Column Splice Zone | $l_{bd} = \alpha_1 l_{b,rqd}$ (EC2 Cl. 8.4.4) | Lap $l_0 = \alpha_6 l_{b,rqd}$ (EC2 Cl. 8.7.3); Stagger dist $s \ge 1.3 l_0$ (EC2 Fig 8.8 $a \ge 0.3 l_0$) | Nominal cover (e.g. 30mm) | EC2 base $20\text{mm}$ / Project $25\text{mm}$ (Cl. 8.2) | Cut normal to Z & along structural axis | Lap verified, no free-space overrun | **PASS** |
| **Column (Lower)** | `ColumnToColumn` (Section Reduction) | **Column (Upper)** | `COLUMN_TRANSITION` | Upper Column Base Zone | Crank 1:6 ($H \ge 6 \Delta$); limit $\le 75\text{mm}$ *(Project Detailing Rule / ACI 318 §25.7.1.3-4)* | $l_0 = \alpha_6 l_{b,rqd}$ | Nominal cover inside reduced section | Clear spacing maintained through bend | True cut shows 1:6 transition slope | 3D crank verified inside upper column | **PASS** |
| **Column** | `ColumnToFoundation` | **Foundation (Footing)** | `COLUMN_FOUNDATION_STARTER` | Footing Core & Bottom Mat Region | Footing embedment leg $\ge l_{bd}$ with 90° bend | Lap splice with column vertical bar | Bottom cover $\ge 50\text{mm}$ | $\ge 25\text{mm}$ | Cross section shows 90° starter hook on mat | Starter leg embedded into footing solid | **PASS** |
| **Column** | `ColumnToFoundation` | **Pile Cap** | `FOUNDATION_COLUMN_CONNECTION` | Pile Cap Internal Core | Embedment into pile cap $\ge l_{bd}$ | Lap splice with column vertical bar | Side cover $\ge 50\text{mm}$ | $\ge 25\text{mm}$ | Cut shows vertical dowels inside cage | Dowels clear pile top steel | **PASS** |
| **Column (Top)** | `ColumnTopTermination` | **Roof Beam / Slab** | `COLUMN_TOP_TERMINATION` | Roof Slab / Beam Joint Region | 90° hook turned inward $\ge 12\phi$ or $l_{bd}$ | N/A (Termination) | Top cover $\ge 30\text{mm}$ (No roof protrusion) | $\ge 25\text{mm}$ | Cut confirms hook contained below roof face | No bar sticks above concrete roof | **PASS** |
| **Beam** | `BeamToColumn` (Interior) | **Column** | `BEAM_COLUMN_CONNECTION` / `BEAM_SPAN` | Joint Confinement Zone | Pass-through continuous steel | N/A (Pass-through); Joint ties 100-150mm; Congestion $\le 8\%$ *(Constructability rule)* | Top/bottom cover $\ge 30\text{mm}$ | Joint tie clearance $\ge 25\text{mm}$ | Section cuts column and beam axes | Longitudinal bars pass without clashing ties | **PASS** |
| **Beam** | `BeamToColumn` (Exterior) | **Column** | `BEAM_END` / `BEAM_ANCHORAGE` | Column Far Face Region | 90° standard hook turned downward/inward | N/A (End hook) | End cover $\ge 30\text{mm}$ to column exterior face | $\ge 25\text{mm}$ | Longitudinal section confirms end hook | Hook contained inside column solid | **PASS** |
| **Beam** | `BeamToBeam` | **Girder / Primary Beam** | `BEAM_BEAM_CONNECTION` | Girder Web Region | Bottom bar $\ge 15\phi$ past face; top bar anchored | N/A (Girders) | Side/bottom cover $\ge 30\text{mm}$ | $\ge 25\text{mm}$ | Cross section cuts primary girder | Secondary beam hooks fit girder core | **PASS** |
| **Beam** | `BeamToWall` | **Structural Wall** | `BEAM_WALL_CONNECTION` | Wall Core Concrete | 90° standard hook anchored into wall core | N/A | Wall cover $\ge 30\text{mm}$ | $\ge 25\text{mm}$ | Longitudinal section cuts wall thickness | Hook anchored into wall core | **PASS** |
| **Slab** | `SlabToBeam` | **Beam** | `SLAB_SUPPORT` / `SLAB_FIELD` | Beam Top Flange / Core | Top negative bar embedded $\ge l_{bd}$ past beam face | N/A (Support) | Top slab cover $\ge 20\text{mm}$ | $\ge 25\text{mm}$ | Transverse section shows top hat bar | Top bars anchored into beam cage | **PASS** |
| **Slab** | `SlabToWall` | **Wall** | `SLAB_WALL_CONNECTION` | Wall Top Core | Slab top/bottom bars embedded $\ge l_{bd}$ | N/A | Wall top cover $\ge 25\text{mm}$ | $\ge 25\text{mm}$ | Section cuts slab-wall joint | Bars do not protrude outside wall face | **PASS** |
| **Slab** | `SlabToColumn` | **Column** | `SLAB_COLUMN_REGION` | Column Head Region | Drop panel / column strip reinforcement | N/A | Slab cover $\ge 20\text{mm}$ | $\ge 25\text{mm}$ | Section shows punching shear zone | Column strip bars clear column verticals | **PASS** |
| **Slab** | `SlabToOpening` | **Opening Void** | `SLAB_OPENING` | Surrounding Slab Boundary Concrete | Trimming bars extend $\ge l_{bd}$ past opening corner | Diagonal 45° crack control bars | Cover to opening edge $\ge 25\text{mm}$ | $\ge 25\text{mm}$ | Plan/Section cuts opening edge | Bars stop at edge; trimming bars surround | **PASS** |
| **Wall** | `WallToFoundation` | **Foundation (Footing)** | `WALL_FOUNDATION_CONNECTION` | Footing Core Region | Vertical dowels embedded $\ge l_{bd}$ with 90° hook | Lap splice with wall vertical bars | Footing bottom cover $\ge 50\text{mm}$ | $\ge 25\text{mm}$ | Section cuts wall base and footing | Dowels anchored into footing bottom mat | **PASS** |
| **Pile** | `PileToPileCap` | **Pile Cap** | `PILE_HEAD` / `FOUNDATION_PILE_CONNECTION`| Pile Cap Embedment Core | Longitudinal bars extend $\ge 40\phi$ into pile cap | Embedded into pile cap $\ge 100\text{mm}$ | Pile cap cover $\ge 50\text{mm}$ | $\ge 25\text{mm}$ | Section shows pile head inside cap | Pile bars clear cap bottom grid | **PASS** |

---

## 2. Intentional Extension vs. Unauthorized Overrun Validation

- **Intentional Extension Evaluation:**
  Every rebar vertex $V_i$ is checked against:
  $$\text{IsContained}(V_i) = V_i \in \text{Solid}(\text{CurrentHost}) \lor \left(V_i \in \text{Solid}(\text{ConnectedHost}) \land \text{Intent} \in \text{AllowedIntents}\right)$$
- If $V_i \notin \text{CurrentHost}$ and $V_i \notin \text{ConnectedHost}$, it is strictly flagged as `ERR_FREE_SPACE_PROTRUSION`.
- If $V_i \notin \text{CurrentHost}$ but belongs to an unauthorized or unrelated element, it is flagged as `ERR_WRONG_HOST_PENETRATION`.

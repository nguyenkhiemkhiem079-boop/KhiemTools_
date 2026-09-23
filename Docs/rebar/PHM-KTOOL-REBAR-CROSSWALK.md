# PHM–K-TOOL Rebar Crosswalk

Status: Phase 8 audit baseline; no PHM reference package was found in this checkout
or the available workspace download folder. The crosswalk therefore records current
K-TOOL evidence and the comparison action to take if the reference is supplied. PHM
is not a runtime dependency and no external behavior is inferred.

## Capability inventory and disposition

| Capability / entry point | Current classification | Action | Priority | Evidence / limitation |
|---|---|---|---|---|
| Beam rebar (`CmdBeamRebar`) | PARTIAL | HARDEN | P0 | Generator exists for longitudinal bars and stirrup zoning; UI generates directly, with no common detached layout plan or common pre-commit preview contract. Rotated/local-axis, shape solving and host behavior need host QA. |
| Rectangular column cage (`CmdColumnRebar`) | PARTIAL | HARDEN | P0 | Dedicated generator, cover lookup/override, longitudinal bars, ties, zones, hook/lap options and transaction-group handling exist. Batch result semantics and complete postconditions need consistent acceptance. |
| Circular column cage (`CmdColumnRebar`, `CmdMultiRoundColumnRebar`) | PARTIAL | DEFER | P1 | Generator and batch entry exist; scope is not rectangular-column-equivalent and requires separate geometry/host evidence. |
| Slab reinforcement (`CmdSlabRebar`) | PARTIAL | HARDEN | P0 | Slab profile/generator and top/bottom/distribution settings exist. Opening exclusion, local direction, complete postconditions and preview fidelity require verification. |
| Foundation reinforcement (`CmdFoundationRebar`) | PARTIAL | HARDEN | P1 | Foundation profile/generator and settings exist. Do not infer pile-cap, footing-family, dowel or starter-bar coverage beyond tested supported geometry. |
| Wall reinforcement | MISSING | DEFER | P2 | No wall generator or exposed wall command found. |
| Rebar shape loading/library | PRODUCTION (bounded) | KEEP | P1 | Shape family library/loader includes the checked-in JP_T* family assets and deterministic missing-shape handling. Template-specific runtime availability still matters. |
| Hooks, anchorage and lap calculations | PARTIAL | HARDEN | P1 | Helpers and domain formula tests exist for TCVN 5574:2018 and Eurocode 2; this is geometry assistance, not structural design approval. Mechanical splice generation was not found. |
| Concrete cover | PARTIAL | HARDEN | P1 | Host cover helpers and project cover setup exist; custom overrides are available in some requests. Verify face selection/orientation for rotated hosts. |
| Column drawing / section / 3D inspection views | PARTIAL | KEEP | P2 | Persistent drawing/view generators operate on already-created/hosted bars; they are not a non-destructive 3D layout preview. |
| Beam/slab/column/foundation form diagrams | UI_ONLY / PARTIAL | HARDEN | P0 | 2D schematic/pre-entry canvases exist. They do not represent a canonical solver-backed preview for every generated bar; no shared preview state or full 3D pre-commit preview was found. |
| Batch operations | PARTIAL | HARDEN | P1 | Multi-column and multi-round commands exist. Atomic versus controlled-partial-success behavior and per-host summaries need a consistent contract. |
| QA fixture command/runtime fixtures | TEST ONLY | KEEP | P0 | Registered fixtures cover straight bar, rectangular/circular column, beam, slab and foundation; require a live Revit host/model. Fixture execution is not claimed here. |
| FreeFormRebar / RebarContainer | MISSING / UNUSED | IGNORE | P3 | No production usage found in the RebarTool source. |
| Wall opening/boundary reinforcement, standalone opening reinforcement, editing/copy/delete/split tools | MISSING or LEGACY | DEFER | P2 | No deterministic production workflow found; do not advertise as supported. |

## Ribbon surface

The active Rebar ribbon/workspace surfaces resolve to column, multi-column,
multi-round-column, beam, slab, foundation, column drawing, project cover setup,
shape loading and the explicit Rebar QA fixture. `CmdColumnRebarV2` delegates to
the existing column command and is a duplicate compatibility entry rather than an
independent generator. No wall, opening, free-form or mechanical splice command was
found. QA-only fixture commands are not production reinforcement commands.

## Phase 8 action order

1. P0: give supported beam, rectangular-column and slab workflows a detached,
   solver-derived layout/preview contract; keep all preview operations read-only.
2. P0: make creation postconditions and controlled failure behavior uniform before
   claiming success; transaction commit alone is insufficient.
3. P1: complete host-coordinate, cover, shape/hook and per-host batch verification.
4. P2/P3: circular-column parity, wall/opening features, FreeFormRebar and
   RebarContainer remain deferred/ignored until deterministic evidence exists.

## Reference comparison limitation

`PHM_COMPARISON_STATUS = BLOCKED_NO_REFERENCE_MATERIAL`. The rest of this file is
an evidence-grounded K-TOOL inventory, not a claimed PHM behavioral comparison.

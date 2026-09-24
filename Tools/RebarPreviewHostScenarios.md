# Phase 8 Rebar preview host scenarios

These are Autodesk Revit host checks. Registration is not evidence of execution; all scenarios below remain `REGISTERED_NOT_EXECUTED` until run in Revit and recorded by a host operator.

| Scenario | Class | Required host action and expected result | Status |
|---|---|---|---|
| COLUMN_UNDO | HOST_REQUIRED | Rectangular column: solve preview, accept, create, Undo once; compare prior bar count/model fingerprint, confirm no preview elements remain, then repeat preview/create successfully. | REGISTERED_NOT_EXECUTED |
| BEAM_UNDO | HOST_REQUIRED | Rectangular beam: solve preview, accept, create, Undo once; verify prior model state and successful rerun. Repeat with a horizontally rotated beam. | REGISTERED_NOT_EXECUTED |
| SLAB_UNDO | HOST_REQUIRED | Slab: solve preview, accept, create, Undo once; verify prior model state and successful rerun. Repeat for an opening slab where supported. | REGISTERED_NOT_EXECUTED |
| FOUNDATION_UNDO | HOST_REQUIRED | Foundation: solve preview, create, Undo once; verify prior model state and successful rerun. | REGISTERED_NOT_EXECUTED |
| COLUMN_CANCEL | HOST_REQUIRED | Open solver preview and Cancel/close; verify no persistent Rebar or preview element and no committed transaction. | REGISTERED_NOT_EXECUTED |
| BEAM_CANCEL | HOST_REQUIRED | Open solver preview and Cancel/close; verify no persistent Rebar or preview element and no committed transaction. | REGISTERED_NOT_EXECUTED |
| SLAB_CANCEL | HOST_REQUIRED | Open solver preview and Cancel/close; verify no persistent Rebar or preview element and no committed transaction. | REGISTERED_NOT_EXECUTED |
| FOUNDATION_CANCEL | HOST_REQUIRED | Solve foundation preview and close the detached 3D viewer; verify the preview capture left no persistent Rebar or mutation. | REGISTERED_NOT_EXECUTED |
| COLUMN_DUPLICATE | HOST_REQUIRED | Preview, refresh unchanged input, accept and create; rerun the identical request and confirm it is rejected without adding bars. | REGISTERED_NOT_EXECUTED |
| BEAM_DUPLICATE | HOST_REQUIRED | Axis-aligned and rotated beam: preview, refresh unchanged input, create, then rerun identical request and confirm rejection without adding bars. | REGISTERED_NOT_EXECUTED |
| SLAB_DUPLICATE | HOST_REQUIRED | Preview, refresh unchanged input, create, then rerun identical panel configuration and confirm rejection without adding bars; include opening slab when supported. | REGISTERED_NOT_EXECUTED |
| SLAB_OPENING_SHAPE_REJECTION | HOST_REQUIRED | On a disposable slab with a rotated or non-rectangular opening, attempt preview/create; verify the unsupported trim geometry is reported and no Rebar is committed. | REGISTERED_NOT_EXECUTED |
| FOUNDATION_DUPLICATE | HOST_REQUIRED | Preview and create a foundation, then rerun the identical host/settings request and confirm rejection without adding bars. | REGISTERED_NOT_EXECUTED |
| CIRCULAR_COLUMN_UNDO | HOST_REQUIRED | Circular column: solve preview, accept, create, Undo once; verify original bar count/model state, no temporary preview elements, and a clean successful rerun. | REGISTERED_NOT_EXECUTED |
| CIRCULAR_COLUMN_CANCEL | HOST_REQUIRED | Open the circular-column solver preview and Cancel/close; confirm no persistent Rebar or model mutation. | REGISTERED_NOT_EXECUTED |
| CIRCULAR_COLUMN_DUPLICATE | HOST_REQUIRED | Circular column: preview, accept and create; repeat the identical request and verify duplicate rejection with no added bars. | REGISTERED_NOT_EXECUTED |

The registered `RC-PREVIEW`, `CC-PREVIEW`, `BR-PREVIEW-RECT`, `BR-PREVIEW-ROTATED`, `SR-PREVIEW-RECT`, `SR-PREVIEW-OPENING`, and `FR` Revit QA fixtures cover rollback-only capture, unchanged-input refresh determinism where available, stale-input invalidation, changed-input re-solve where available, in-transaction duplicate detection, and generator-to-preview geometry parity. They are compiled but have not been run as part of this code-side acceptance. Foundation-specific `FR_PREVIEW`, `FR_CANCEL`, `FR_PARITY`, and `FR_DUPLICATE` checks, and circular-column `CC-PREVIEW_*` checks, are registered and remain `NOT_EXECUTED`.

Known Foundation code-side gap: `FoundationRebarSettings.EnableSideTies`, `SideTieDiaLabel`, and `SideTieSpacingMm` are persisted, but `FoundationRebarGenerator` does not consume them to create side-tie bars. The preview faithfully reflects the current generator output and does not imply that side ties are implemented. Engineering detailing for those bars remains unresolved and blocks a complete Foundation production-readiness claim.

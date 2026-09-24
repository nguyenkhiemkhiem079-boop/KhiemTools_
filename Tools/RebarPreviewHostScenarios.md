# Phase 8 Rebar preview host scenarios

These are Autodesk Revit host checks. Registration is not evidence of execution; all scenarios below remain `REGISTERED_NOT_EXECUTED` until run in Revit and recorded by a host operator.

| Scenario | Class | Required host action and expected result | Status |
|---|---|---|---|
| COLUMN_UNDO | HOST_REQUIRED | Rectangular column: solve preview, accept, create, Undo once; compare prior bar count/model fingerprint, confirm no preview elements remain, then repeat preview/create successfully. | REGISTERED_NOT_EXECUTED |
| BEAM_UNDO | HOST_REQUIRED | Rectangular beam: solve preview, accept, create, Undo once; verify prior model state and successful rerun. Repeat with a horizontally rotated beam. | REGISTERED_NOT_EXECUTED |
| SLAB_UNDO | HOST_REQUIRED | Slab: solve preview, accept, create, Undo once; verify prior model state and successful rerun. Repeat for an opening slab where supported. | REGISTERED_NOT_EXECUTED |
| COLUMN_CANCEL | HOST_REQUIRED | Open solver preview and Cancel/close; verify no persistent Rebar or preview element and no committed transaction. | REGISTERED_NOT_EXECUTED |
| BEAM_CANCEL | HOST_REQUIRED | Open solver preview and Cancel/close; verify no persistent Rebar or preview element and no committed transaction. | REGISTERED_NOT_EXECUTED |
| SLAB_CANCEL | HOST_REQUIRED | Open solver preview and Cancel/close; verify no persistent Rebar or preview element and no committed transaction. | REGISTERED_NOT_EXECUTED |
| COLUMN_DUPLICATE | HOST_REQUIRED | Preview, refresh unchanged input, accept and create; rerun the identical request and confirm it is rejected without adding bars. | REGISTERED_NOT_EXECUTED |
| BEAM_DUPLICATE | HOST_REQUIRED | Axis-aligned and rotated beam: preview, refresh unchanged input, create, then rerun identical request and confirm rejection without adding bars. | REGISTERED_NOT_EXECUTED |
| SLAB_DUPLICATE | HOST_REQUIRED | Preview, refresh unchanged input, create, then rerun identical panel configuration and confirm rejection without adding bars; include opening slab when supported. | REGISTERED_NOT_EXECUTED |

The registered `RC-PREVIEW`, `BR-PREVIEW-RECT`, `BR-PREVIEW-ROTATED`, `SR-PREVIEW-RECT`, and `SR-PREVIEW-OPENING` Revit QA fixtures cover rollback-only capture, unchanged-input refresh determinism, stale-input invalidation, changed-input re-solve, in-transaction duplicate detection, and generator-to-preview geometry parity. They are compiled but have not been run as part of this code-side acceptance.

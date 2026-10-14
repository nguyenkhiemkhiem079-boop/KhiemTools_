# Beam support extension wave — implementation and acceptance boundary

Start: cc31ab52d5406d3330e30386db088a6f51a64293; branch fix/preview-hardening; PR #3. Existing recovery is not repeated.

## Implemented supported slice

Manual longitudinal/additional/side paths may have explicitly entered stations outside the Beam at independently bound START/FINISH supports. Supported supports: exact same-document rectangular structural Column or straight rectangular structural Beam, valid RebarHostData, verified real prism, contact with the specified physical Beam end. No inference of support, bar count, anchorage/development, cover or structural compliance. Start/finish cannot use the same support identity. Wall/slab/circular/non-prismatic/sloped/linked supports remain unsupported.

UI requires exact support choice, explicit finite non-negative uniform clear cover on exposed support faces in mm, and continuation intent. No selection/cover default. Input is bound to document, support UniqueId and support state signature. A changed or foreign/missing support must be rebound and preview solved again. Saved schema1 free-end plans still load; previously unbound verified-support entries are explicitly blocked instead of treated as honored. Dedicated companion storage roundtrip preserves all new fields and negative station coordinates. Main/stirrup face covers remain independently explicit; stirrups remain within Beam, not silently extended.

## Physical geometry proof

Generator transforms the canonical local-mm path using the existing verified Beam frame. Same production generator runs in rollback preview and Create. Transient CSG unions real verified Beam/support concrete. Exact bound Line/Arc swept circular volumes and conservative spherical endpoint envelopes must remain inside that union. Exposed cover shells are constructed from real prism minus explicitly inset prism; portions inside neighboring concrete are removed so internal Beam/support interfaces do not invent an exposed end-face cover. Every solved curve, including actual hooks/bends and multiplanar centerline output, is checked, not a bbox or tessellated-only containment approximation. Unknown curves, excessive cover, failed Boolean/sweep or uncertain geometry fail closed.

Numerical tolerances: contact probe0.1mm (not support length); CSG residual max1e-12ft³ or1e-8 envelope volume (not an engineering clearance). Spherical endpoint padding is deliberately conservative and retains the previous physical end-radius rule. This does not certify sufficient development/lap or any design code.

Support-neighborhood Rebar joins the stale fingerprint/interference preflight, including other-host bars intersecting the support bounds. Batch freshness excludes only its own newly generated bars while still checking original neighbor state; those new bars participate in generation interference checks. Final batch rechecks all accepted inputs/support state and output before group assimilation. No artificial failure behavior enters production code. Actual Undo/rollback and support CSG kernel behavior require host execution.

## Tests and host boundary

Golden157 includes17 new support input-permission, wrong-end/missing/foreign/duplicate binding, nullable/nonfinite/negative/explicit-zero cover, fingerprint, unclamped station and production-store roundtrip assertions. These are not CSG geometry tests. K-Rebar163 source integration checks. Five actual API builds2022–2026 pass. Dedicated UI offline matrix passes812 controls/27 renders. Canonical regression/package results are recorded in TEST_RESULTS.md after completion.

Registered fixture BR-MANUAL-SUPPORT-ROLLBACK invokes real bound support, production solver preview, atomic batch Create, type/host/centerline parity and QA-only outer failure rollback with model-state/identity fingerprints. Missing resources or unsuccessful preview are BLOCKED, not PASS. Catalog NOT_EXECUTED. Fixture registration/compilation is NOT live acceptance. Before running, confirm an exact detached disposable model; never infer permission from a running Revit process.

Mandatory additional host matrix: start/finish/both supports; rotated Beam/Column and reversed ends; different covers; loaded hooks and explicit returns; near-face/edge envelope rejection; wrong/gapped/missing/moved support; support bar interference; cancel, rebind, refresh, duplicates; second-host failure whole rollback and Undo. No production readiness until mandatory host evidence and release gates close.

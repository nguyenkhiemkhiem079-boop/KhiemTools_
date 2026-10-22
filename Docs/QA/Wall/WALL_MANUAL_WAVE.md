# Wall reinforcement — explicit geometry wave

## Confirmed ownership and supported boundary

User clarified option 1: every new bar remains **Wall-hosted**; selected, verified boundary Columns only receive extensions. No Column-only path or host remapping is permitted.

Supported input is one straight, vertical structural Basic Wall with one verified rectangular concrete solid and six planar physical faces. Local x follows the physical wall, y is upward from the actual bottom, depth runs from exterior inward; mm is converted exactly once. Curved, slanted, opened, cut/joined/non-prismatic walls, links, curtain walls, hooks, ties and automatic design are explicitly rejected. This is a bounded geometry implementation, not all Wall engineering functionality.

Users supply every physical bar, its exact loaded type, direction (VERTICAL/HORIZONTAL/USER_DIRECTED), both endpoints and all six face covers. No counts, spacing, diameters, cover, anchorage or code values are inferred. Boundary Columns require exact identity, same document, verified actual rectangular profile, explicit uniform exposed cover and continuation intent. Clearance uses actual circular sweep/end envelopes in the concrete union and exposed cover shells. Curved/unknown neighboring bars block creation rather than being accepted from tessellated clearance estimates. Physical clearance is not a structural design rule.

## Production flow and UI

Ribbon → Wall-only selection → explicit versioned JSON editor / exact-type picker / boundary Column binding → validation → production-generator rollback-only solver capture → accepted detached 2D/3D → current fingerprint / complete duplicate check → one transaction group with exact type/host/quantity/path parity → commit or whole rollback.

Saved plans retain exact document/host/frame/type/support bindings. Unknown JSON properties, foreign/stale resources, absent explicit values and invalid geometry reject; unsupported engineering settings are never silently accepted. Input is bounded to 2MiB/depth32 and up to10000 physical bars for software safety, not design quantities. Save uses a uniquely owned temporary file and atomic replacement after explicit overwrite confirmation.

2D is an actual solved orthographic **elevation**, with all depth layers projected and disclosure that it is NOT a section. Paint uses only detached numeric arrays; it does not access Revit. 3D uses the existing solver snapshot viewer. The later preview-parity consolidation is recorded in [`REBAR_PREVIEW_PARITY_WAVE.md`](../REBAR_PREVIEW_PARITY_WAVE.md); its code-side work does not replace live host acceptance. Advanced detailing and richer grid editing remain outside this slice.

## Audit corrections

- P1: actual root TableLayoutPanel bounds overflow: corrected percent-width column; no hidden controls/assertion changes.
- P1: ignored unknown JSON engineering properties: strict missing-member rejection in editor and file loading.
- P1: curved neighbor clearance would have used approximation: fail closed until exact curved collision support exists.
- P2: coarse blank-render sampling aliased sparse controls: dense actual-pixel fallback retains the same ten-color threshold; real blank/sparse bitmap self-tests added.
- P2: collision diagnostics could allocate quadratic strings: fail after first confirmed collision.
- P2: misleading pre-existing Beam ribbon tooltip implied TCVN/EC approval and inferred fractions: truthful geometry-only wording.

## Disposable host acceptance — REGISTERED / NOT_EXECUTED

`WALL-MANUAL-OWNERSHIP-ROLLBACK` is registered/compiled. Synthetic fixture-only zero-cover inputs are NOT production defaults. Baseline actual solver/create/rollback plus required boundary Column extension check exact Wall host, type, quantity, centerline parity and safety-framework identities/state restoration. Missing Wall/type/Column or unsolved shape resources report BLOCKED; baseline alone does not certify extension.

On a confirmed detached disposable model only: exercise rotated and flipped supported Walls; six covers and diameter-boundary negative cases; left/right Column continuation; foreign/changed support/type/frame invalidation; exact versus partial/other-host duplicates; cancel; later-bar injected failure; unrelated bar preservation; native Undo; hooks/curved neighbors/unsupported geometry rejection. Capture pre/post model identities, fingerprints, host IDs and resources. Never use open production/project models.

Code-side test results belong in TEST_RESULTS.md after the final exact-source run. Source builds/offline layout/synthetic assertions do not establish host acceptance. HOST_ACCEPTANCE=NOT_EXECUTED; PRODUCTION_READY=NO.

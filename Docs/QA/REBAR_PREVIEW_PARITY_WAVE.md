# Wave4 — consolidated supported Rebar preview parity audit

## Implemented source flow

All supported domains retain production-generator rollback capture and accepted-preview Create parity. The canonical reader now uses native GetTransformedCenterlineCurves for each included position: varying shape, distribution and individual moved-bar transforms are handled by Revit exactly once. No manual second transform or base-position fallback. This affects detached 2D/3D, stale/duplicate fingerprints and actual existing-bar tie elevation/spacing summaries consistently.

Evidence for the API correction: installed Autodesk XML documents the method in2022/2023/2024/2025/2026. [Autodesk final-position API reference](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/650b9879-3378-77f8-65f0-efeb05daa399.htm) specifies distribution and moved transforms. The previous raw centerline reader did not apply those transforms; matching preview/Create fingerprints alone could therefore miss repeated base-position rendering. This is a confirmed source-contract defect, not an invented live test failure.

| Surface | Accepted 2D | Host/cut mapping | Before acceptance / stale |
|---|---|---|---|
| Beam Manual | Same accepted solver paths as3D; actual local elevation and mid-span polyline cut | Actual verified Beam Right/Direction/Up; plane station=L/2 | Existing clearly labeled input schematic, Create disabled |
| Rectangular Column | Accepted actual local elevation and mid-height polyline cut | Physical column rotation/base; exact first selected host UID only | Existing disclosed configuration schematic; no solved claim |
| Circular Column | Accepted actual elevation and mid-height polyline cut with verified circular host guide | Actual cylindrical base/diameter/height; exact first selected host UID only | No solved bars displayed |
| Wall | Accepted actual elevation and mid-length cut across thickness/up | Verified actual Wall depth/length/up; Wall remains host | No bars, old cache cannot become a fallback |
| Slab | Existing solved plan/true SectionX/Y, opening/disconnected concrete intervals retained | Existing canonical panel cut planes and detached actual elevations | Existing stale/cancel/refresh behavior retained |

For multi-Column selection, inline2D explicitly shows the first selected exact host, never concatenates another host into it; the existing accepted solver viewer includes all captured components. Host-only concrete guides are labeled; support solids are NOT asserted to be drawn. Elevations project all layers and are not called sections. Cut intersections are symbolic centerline markers, not bar diameter. Arc data is native tessellation: this is a true cut of the detached solver **polyline**, not an analytic circular cross-section certification. Paint has no native model calls. Shared2D/3D role palette is preserved.

## One consolidated audit / coordinated corrections

- P1 CODE RESOLVED: raw distributed/moved position capture corrected in the shared adapter and existing tie-station reader. Missing final curves/invalid positions fail closed, never fall back to the first bar. A curve without a segment can no longer be silently dropped.
- P1 CODE RESOLVED: Beam/Column inline diagrams could not represent accepted solved hooks/returns/actual positions; accepted solver cache now supersedes configuration schematics, exact host identity preserved. Wall gets a real cut separate from projection.
- P1 CODE RESOLVED: Beam curved collision clearance had only tessellated chord comparisons. Added actual final-position Line/Arc physical sweeps with native solid-intersection rejection; uncertain kernels/curve types block. Existing exact straight segment checks remain; no engineering spacing inferred.
- P1 CODE RESOLVED earlier waves: Wall input/host/strict JSON/layout and safe MEP confirming/cut/joins/reinforcement postconditions; details in their wave records.
- P2 CODE RESOLVED: verifier method-boundary regex incorrectly included the new native cache builder in the circular Paint body; method boundary corrected, native-call prohibition retained. Shared role-color access is checked under its actual internal signature.
- P2 QA RESOLVED: exact RGB assertion missed real antialiased1.5px strokes. Inspected failure bitmap, used distinct palette hue tests with unchanged minimum counts; white/gray pixels cannot pass. Off-plane section asserts zero corresponding role pixels, projection asserts positive pixels. No synthetic pixels added to production output.
- P0: none confirmed in the supported code-side audit. No live-model corruption absence is certified without host execution.

## Automated versus host evidence

Golden204 includes15 new pure cut/projection/role/defensive-copy/tolerance assertions. Production-linked native-position adapter8 uses explicit synthetic API doubles (raw API deliberately returns repeated base coordinates, final API returns distributed/moved coordinates), not Revit. Renderer12 pixel assertions/3 synthetic bitmaps invoke the actual production drawing helper at100/125/150% simulated sizes, not native DPI/host QA. Both scripts execute explicitly in Test-All; no count-only pass. Actual API builds are recorded separately in TEST_RESULTS.md.

REBAR-WORLD-POSITION-PREVIEW is registered/compiled, NOT_EXECUTED: requires an actual dispersed existing set, compares canonical paths with native final positions and verifies unchanged model state. Missing dispersed resources BLOCKED. Its single resource run is NOT certification of moved/varying/excluded sets; those require dedicated real resources.

## Required disposable-host matrix / remaining acceptance

Confirm exact detached disposable model before any fixture/creation. For every supported host, compare accepted2D/3D to actual created element UID/host/type/shape/hooks/quantity/final-position centerlines. Include multiple positions, excluded first/last/interior bars, independently moved bars and varying-length sets; rotated/reversed Beam and Column; exact boundary supports; slab opening/support/chair roles; Wall flip/directions/both boundary Columns. Capture fresh input fingerprints, partial versus complete duplicates, cross-host selectivity, cancel/refresh, later-item injected failure/whole rollback, unrelated identity/state preservation and native Undo. Exercise curved collision positive/tangent/clear cases with actual loaded hook/shape resources. Large native CSG batch performance remains P2 HOST_REQUIRED, not established by pure stress tests.

P1 HOST_ACCEPTANCE remains NOT_EXECUTED. MSI lifecycle/signing and multi-year actual loading remain pending. Advanced unsupported engineering functionality is not fabricated. Full product/contract and PRODUCTION_READY are NOT claimed complete.

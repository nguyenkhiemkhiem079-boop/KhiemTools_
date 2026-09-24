# Phase 17 — Golden Regression, Edge Cases and Synthetic Stress

`Tests/KhimTools.Golden.Regression` links actual production calculations and runs golden
expectations for K-QS quantity math, K-MEP measurement/elevation math, the active Architectural
lintel layout, and `RebarAnchorageCalculator` (TCVN/Eurocode anchorage, lap and fallback lengths).
Rebar calculator inputs reject non-finite/non-positive diameters, invalid enum values, invalid
fallback/lap factors, and non-finite computed results. Semantic comparisons use values stable across
sessions (count, type, spacing, diameter, cover, orientation and host relationship); the
comparator contract explicitly ignores ElementId-like volatile identifiers.

The harness also tests exact/tolerance/unordered/range/semantic comparison modes, invalid and
boundary inputs, and pure calculation stress at 100, 1,000 and 10,000 records. These are synthetic
calculations, not large Revit models or host transaction tests.

The MCP contract harness additionally stress-tests the actual local JSON-RPC/stdio serialization
path at 100, 1,000 and 10,000 requests. This remains an in-process serialization/transport test,
not a remote service or network load test.

Rebar column, rotated-column, beam, rotated-beam, slab, and slab-opening semantic geometry golden outputs require
actual Revit-solver centerlines and host metadata. No such output was fabricated or inferred from
unexecuted fixtures. The relevant Phase 8 host scenarios remain `NOT_EXECUTED` in
`Docs/Runtime/Phase15-Host-Scenario-Catalog.json`; therefore `REBAR_HOST_GEOMETRY_GOLDEN` remains
`HOST_REQUIRED / NOT_EXECUTED`. The Rebar-specific geometry edge cases likewise remain
`HOST_REQUIRED / NOT_EXECUTED`; `REBAR_DOMAIN_EDGE_ACCEPTANCE=PASS` covers only the production
anchorage/lap calculator's input boundary checks. `EDGE_CASE_ACCEPTANCE=PASS` applies to executable
QS/MEP/Architectural domain calculations. The Phase 8 Rebar static acceptance continues as an
independent regression gate and none of these replaces Revit geometry goldens or geometry edge cases.

The reproducible entry point is `Tools/Verify-Phase17GoldenPerformance.ps1`, also run by
`Test-All.ps1`.

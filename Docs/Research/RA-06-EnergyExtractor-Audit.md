# RA-06 — EnergyExtractor artifact and provenance audit

**Static audit result:** complete for the identified 2026-10-19 package and a later 2026-10-20 comparison package. **Roadmap acceptance:** BLOCKED until Room, zone, and envelope quantities are compared against an identified Revit model and an independent truth set.

**Review baseline:** branch `codex/rea-core00-20261023`, starting source commit `18c2cc27350808390b0e7e5a383dafb38b945b40`.

## Artifact identity

The roadmap's “19-10” package is the v1.0.0 ZIP. v1.5.0 is a later package and is recorded separately; the packages and their findings must not be treated as one binary.

| Evidence ID | Package | Local ZIP metadata | SHA-256 | Embedded DLL SHA-256 |
|---|---|---|---|---|
| RA06-EV-01 | `K-TOOL-EnergyExtractor-Standalone-Revit2025-v1.0.0.zip` | 52,085 bytes; 2026-10-19 23:54:58 | `64A93B7445A0CD6ADD4A7D223132D04455C6E80E8EE4915068FC7E322FE828AA` | `43E1330F60BBB1EF75B9FC6C25C2A9D754E52CB81B7D59F402C9E7E1BEACF242` (66,560 bytes) |
| RA06-EV-02 | `K-TOOL-EnergyExtractor-Standalone-Revit2025-v1.5.0.zip` | 89,470 bytes; 2026-10-20 22:21:21 | `F92B5F86785676D627B56175128E09EF393577B90317FD059073A648AAD8B3B4` | `B287C360A19D4BD636BFBFDB24EF4F773CF996A4EFA4149CF21CFCFC90AFF929` (124,416 bytes) |

Archive members were read without executing the product. Every non-directory member in each ZIP matched its separately extracted folder by SHA-256 (23/23 for v1.0.0; 24/24 for v1.5.0). The local file times are filesystem metadata, not a trusted release timestamp or signature.

## Capability and provenance findings

| Data | v1.0.0 evidence | v1.5.0 evidence | Remaining limit |
|---|---|---|---|
| Rooms and boundaries | Host-document Rooms and boundary segments; SI exports and missing-data/QA rows are documented. | Adds base/upper limits, offsets, effective height, boundary diagnostics, and shared-wall adjacency candidates. | No linked-element extraction or transformed linked quantities. Boundary references may remain unresolved. No comparison to a currently supplied native `.rvt` and schedule was performed in this audit. |
| Room area and volume | Area/volume conversion and output fields are described, but the v1.0.0 QA record reports no Room volume values in its reviewed export. | `Room.Area` is converted to m²; volume is obtained from `SpatialElementGeometryCalculator` solids; effective height is volume/area. | v1.5.0 source marks derived volume provisional. No current Revit host run verified the solid result or reconciled totals. |
| Zones and HAP | Blank zone mapping and HAP fields remain missing until supplied/reviewed. | Jamona room-name rules classify functional type/conditioning; user-confirmed laundry names map to `NO_COOLING`; apartment/HVAC group mapping remains optional and review-bound. HAP stays manual/missing. | Name rules are project-specific; ApartmentID/group membership is not inferred. No HVAC load or energy simulation is produced. |
| Envelope areas | Category inventory and available computed area parameters; exposure is intentionally `UNVERIFIED`. | Adds glass-like material takeoff, orientation methods, wall adjacency candidates, compound construction layers, and roof/waterproofing candidates. | Glass-material area is not verified net glazing area; category/computed areas are not geometry-derived gross/net areas. Host/panel double counting, openings, exposure, orientation, roof candidacy, and waterproofing remain provisional pending model/spec review. |
| Reproducibility | Package QA report describes a synthetic CSV/manifest test that emitted 21 files; this is a package-recorded result, not rerun here. | v1.5.0 QA report states that its new geometry/adjacency/material feature set had no synthetic or live Revit test run. | No test was run during this audit. Static package inspection and claimed prior synthetic tests are not native host acceptance. |

The v1.5.0 source corroborates the above boundaries in `Source/Services/ModelExtractor.cs`, `ZoneMappingService.cs`, `JamonaRoomRules.cs`, and `ExportService.cs`; output status fields include `VERIFIED`, `PROVISIONAL`, `MISSING`, and `UNVERIFIED`. In particular, material-name matching is a takeoff heuristic, a wall with one Room boundary is only an exposure candidate, and a shared host wall establishes a topology relationship rather than an ApartmentID or door-connected space.

## Acceptance disposition

- **Static scope/provenance audit:** complete for EV-01 and EV-02.
- **Room/zone/area comparison acceptance:** `BLOCKED`. The review has no confirmed disposable Revit 2025 fixture plus authoritative Room schedule/zone register and independently measured envelope truth set for repeatable comparison.
- **Live Revit/Add-in Manager acceptance:** not executed by this audit.
- **Energy or cooling-load validation:** out of scope for these packages; neither package claims a validated simulation.
- **K-TOOL integration:** none performed. This audit does not authorize copying standalone binaries or source into the production add-in.

## Next evidence needed

1. Identify the exact Revit 2025 QA model and owner-approved disposable copy.
2. Supply the matching Room schedule, reviewed Room-to-HVAC mapping, phase/scope definition, and independently checked wall/window/panel quantities with units.
3. Run v1.5.0 from its identified package on that copy, preserve the raw export, and reconcile Room counts/area/volume, boundary references, zones, panel-host accounting, orientation, and construction-layer candidates.
4. Record pre/post model fingerprints and confirm the extractor made no model changes.
5. Keep every output tied to the ZIP/DLL hashes above; do not back-apply v1.5.0 results to v1.0.0.

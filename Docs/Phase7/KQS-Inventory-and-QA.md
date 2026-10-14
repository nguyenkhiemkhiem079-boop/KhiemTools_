# Phase 7 — K-QS Inventory, Hardening, and QA Evidence

Branch: `phase7/k-qs`, created from the final Phase 6 commit. Local commits only; no push.

## Inventory and capability crosswalk

Repository search found no PHM QS reference files. `Docs/QTO/QTO-SPEC.md` is an internal
product specification, not an external behavior baseline. No PHM capability claims or hidden
runtime dependencies are introduced.

| Surface | Disposition | Evidence / boundary |
| --- | --- | --- |
| Ribbon: Quantity Takeoff | PRODUCTION | `CmdQuantityTakeoff`; opens the modal K-QS workbench |
| Ribbon: Data Check | PRODUCTION / same backend | `CmdQtoDataCheck`; opens the same workbench directly on Data Check |
| Workbench form | PRODUCTION | Revit takeoff, rule profile editor, group/search filtering, source selection, Cubicost comparison/import/export, findings |
| Host QTO collector | PRODUCTION, limited scope | Whole host document; material volume/area, rebar derived mass, selected instance counts, curve lengths, room area; links excluded |
| Measurement rules | PRODUCTION, limited | Per-code enablement, non-negative waste, 0–6 UI rounding digits, rounding after aggregation, trace string |
| Classification | PARTIAL / heuristic | Material-name token mapping (English/Vietnamese); unmatched lines get a finding; no project-stable classification parameter/profile mapping in the active UI |
| Aggregation | PRODUCTION / hardened | Code + category ID + material + family + type UniqueId/name + level UniqueId/name + unit; deterministic hashed key and output ordering |
| Snapshot comparison service | PARTIAL / service-only | Schema 2 detailed keys; legacy snapshots compare at their prior coarser grouping; no workbench entry point found |
| Cubicost reconciliation | PRODUCTION / limited | First worksheet or CSV import, manual Revit-code mapping, compare by code/unit; generated internal baseline is explicitly not an independent Cubicost result |
| XLSX export | PRODUCTION / limited | Comparison and QTO summary/detail rows; invariant-culture numeric text; temp-write then atomic replace, cancellation leaves files unchanged |
| `QsModelScanner` | PARTIAL / service-only | Active view, explicit selection IDs, and entire-model scopes; it has no UI call site; invalid/missing scope now fails closed |
| BOQ, cost, progress, planning, procurement, project controls, change control | LEGACY / dormant services | Source files exist without workbench call sites; not claimed as production UI capabilities |
| `KqsProjectStore` | DORMANT / unintegrated | Source-only project store has no workbench call site; excluded from active K-QS workflow |
| Transactions/model writes | NOT USED | Production QTO scan, rule application, comparison, and export do not modify the Revit model |

The primary workbench has no selection/view/level filter. It intentionally scans all supported
instances in the current host document, and visibly states “whole host document; no links.”
View and Selection are separate scanner API modes; Selection requires an explicit ID list and
does not degrade to an entire-document scan. Linked models are not counted, avoiding silent
host/link duplication. Unsupported and unclassified material quantities are not inferred.

## Correctness, grouping, export, and performance

- Revit material volumes and areas are converted from internal units through `UnitUtils`;
  pipe/duct/tray/conduit centerline length uses internal length; room area uses `Room.Area`;
  counts are instance counts. Rebar and structural steel mass are derived with the documented
  7,850 kg/m³ density factor and remain labelled `Derived`.
- Zero/unplaced room area and zero/unreadable rebar volume have explicit findings. Rule math
  rejects negative/non-finite raw quantities and waste, invalid precision, and decimal overflow;
  affected lines are marked excluded and a warning with source element IDs is surfaced.
- Rounding uses decimal midpoint-away-from-zero arithmetic and happens after line aggregation.
  Raw quantities remain unchanged. Waste is never silently defaulted from a hidden rule.
- Material-name classification remains intentionally heuristic and can miss local names;
  false-positive broad “metal” matching was removed. Unmatched material rows are reported as
  unclassified instead of being guessed. A governed stable project code mapping remains a P1
  product follow-up, not a claim of this phase.
- Group keys now distinguish category, material, family, type identity, level identity and unit.
  Snapshot schema 2 stores the detailed key; legacy snapshots retain their old grouping semantics
  when compared, so upgrading the grouping does not fabricate per-category Added/Removed deltas.
- The collector materializes one whole-document instance list and performs the supported passes
  over it; it no longer invokes separate full-document collectors for every category. Scan
  diagnostics record a hashed document identity, scanned/eligible/excluded/group counts and
  elapsed duration without logging local paths.
- User-selected exports are written to a unique sibling temp file and only replace/move into the
  target after successful generation. Export numeric strings use invariant culture. Rule profile
  writes are atomic and preserve a versioned backup. Snapshot writes use a temp file and rename.
- XLSX currently stores values as inline strings through the shared lightweight writer. It is a
  valid audit workbook but numeric cells are text, not spreadsheet-native numeric cells; styling,
  full BOQ hierarchy, element-level workbook, manifest and issue approvals remain deferred.

## Verification

| Gate | Result |
| --- | --- |
| K-QS production acceptance | PASS, 47 static production checks |
| K-QS domain QA | PASS, 16 arithmetic, culture, and invalid-input assertions |
| Revit 2024 / net48 build | PASS, 0 errors, 212 CS0618 API-obsolescence warnings |
| Revit 2025 / net8.0-windows build | PASS, 0 errors, 210 CS0618 API-obsolescence warnings |
| Full `Test-All.ps1` | PASS, 1271/1271 expected after final acceptance expansion |
| UI contracts | PASS, 92 compiled command entry points, 17 workspace bindings |
| UI layout | PASS, 100 icons, 10 XAML surfaces, 43 renders |
| Rebar domain / K-MEP / K-Architectural / Stage 4 | PASS through full regression |
| Runtime host fixture | DEFERRED; registered `KQS_SCOPE_AND_READONLY_QA` requires a safe writable Revit model with an active non-template view and one selected categorized element |

`KQS_RUNTIME_ACCEPTANCE = DEFERRED`; no live Revit model was opened or mutated in this
verification environment. Static and domain checks are not reported as host runtime execution.

## Remaining bounded limitations

No linked-document policy, phase/design-option/workset filtering, type-parameter quantities,
material compound-layer deductions, opening deductions, governed stable-code mapping, true
numeric XLSX cells, or integrated BOQ/approval workspace was added without an evidenced and
tested production path. The current collector's supported category/measurement matrix is in
`KhimTools/Tools/KhimGen/QuantityTakeoff/README.md`.

`CATEGORY_C_KQS_WARNINGS = NONE_IDENTIFIED`
`KNOWN_KQS_MODEL_CORRUPTION_RISK = NONE_IDENTIFIED`
`KQS_RUNTIME_ACCEPTANCE = DEFERRED`

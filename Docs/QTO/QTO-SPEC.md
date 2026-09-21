# K-TOOLS Quantity Takeoff — Functional and Technical Specification

## 1. Objective

Build an auditable Quantity Takeoff (QTO) workflow for Revit models. Every reported
quantity must retain its source element, source parameter or geometry method,
measurement rule, unit conversion, adjustment and issue snapshot.

The module supports model checking and quantity production. It does not assume that
model geometry alone is contractually measurable.

## 2. Governing principles

1. **Traceable:** every quantity line links to Revit `UniqueId`, document/link identity,
   category, family, type, level, phase and workset.
2. **Reproducible:** a saved rule profile and a model snapshot must reproduce the same
   result.
3. **Separated facts and rules:** native model values are stored as `RawQuantity`;
   deductions, waste and rounding produce `PayQuantity` without destroying the source.
4. **Explicit fallback:** parameter, material quantity and geometry calculations have
   different confidence levels. A fallback is shown in QA instead of being silent.
5. **Controlled issue:** Draft, Checked, Approved and Issued states are recorded with
   author, time, model identity and rule-profile version.
6. **Configurable measurement:** classification and measurement profiles are project
   inputs. ISO 19650 information management does not replace a project's method of
   measurement.

## 3. User workflow

```text
Select model scope
    -> Run information preflight
    -> Resolve blocking data issues
    -> Extract immutable raw facts
    -> Apply classification and measurement rules
    -> Review quantities and exceptions
    -> Compare with previous snapshot
    -> Check / approve
    -> Export BOQ and audit package
```

### 3.0 K-QS Measurement Workbench

K-QS contains its own compact 5D quantity workbench inside Revit. It provides the core
workflow commonly expected from dedicated takeoff software while retaining direct access
to native Revit elements.

The workbench has five coordinated panes:

1. **Model Tree** — discipline, category, level/zone, family/type, material and element.
2. **Measurement Rules** — match conditions, quantity source, deductions, waste, unit,
   rounding and grouping.
3. **Quantity Sheet** — BOQ hierarchy, raw/pay quantities, rates and amounts.
4. **Element Inspector** — Revit parameters, calculated facts, formula trace and QA state.
5. **Revision Control** — snapshot comparison, variance reasons, review and issue status.

Selecting any node highlights its source elements in Revit. Selecting an element shows
which rule matched it and every step from native value to BOQ quantity.

K-QS also supports a separate Cubicost source created directly from the Revit takeoff.
This creates an independent editable baseline inside Revit (code, location, unit and
quantity), so the user can adjust the Cubicost side and compare it without first
exporting or importing a Cubicost workbook. External XLSX/CSV import remains available
when a project already has a Cubicost export.

### 3.1 Select scope

- Current document, selected elements, current view or explicit levels.
- Optional Revit links, selected by link instance rather than only linked document.
- Phase and phase-status filters.
- Design option, workset and category filters.
- Exclude element types, templates and non-physical analytical objects by default.
- Detect the same linked model inserted more than once and require an explicit include
  policy for each instance.

### 3.2 Information preflight

Preflight runs before calculation and assigns each finding one of three severities:

- **Blocker:** quantity cannot be trusted or classified.
- **Warning:** quantity can be produced but requires review.
- **Information:** accepted fallback or project-specific condition.

Default checks:

- Missing classification/item code.
- Missing Type Mark, material or required shared parameters.
- Duplicate Mark/asset code where uniqueness is required.
- Zero or negative area, volume or length.
- Unplaced rooms, unbounded rooms and redundant room boundaries.
- In-place families or Generic Models without an approved QTO mapping.
- Elements in an excluded phase/design option/workset.
- Invalid or unloaded links.
- Elements without stable host/level association.
- Model quantity and geometry-derived quantity outside the configured tolerance.

The user can isolate affected elements in Revit from every finding.

### 3.3 Review and issue

- Review by work breakdown, classification, category, level, zone, material and type.
- Expand every BOQ line to its contributing elements.
- Select a row to highlight or isolate its source elements.
- Show raw value, applied rule steps and final value side by side.
- Compare Added, Removed, Changed and Unchanged quantities with a prior snapshot.
- Require blockers to be resolved or explicitly waived before Approved/Issued status.

## 4. Data pipeline

### Stage A — Source identity

Capture before extraction:

- Host document GUID/fingerprint, title and central/local path fingerprint.
- Revit build, project information and active phase context.
- Link document identity and link instance `UniqueId`.
- Extraction time, Windows user and K-TOOLS version.
- Rule profile ID, semantic version and content hash.

### Stage B — Raw extraction

Read Revit facts once and cache them by element. Values remain in Revit internal units
until normalization. Each fact contains:

- Element ID and `UniqueId`.
- Link instance ID when applicable.
- Category, family, type and type ID.
- Level, phase, design option and workset.
- Material ID/name when the row is material-based.
- Parameter GUID or BuiltInParameter ID.
- Raw numeric value and specification/unit type.
- Source method: `InstanceParameter`, `TypeParameter`, `MaterialQuantity`,
  `CalculatedGeometry` or `ManualOverride`.
- Confidence: `Authoritative`, `Native`, `Derived` or `Override`.

### Stage C — Normalize

- Convert length, area, volume, mass and count with Revit `UnitUtils`.
- Use the unit specified by the measurement rule, independent of project display units.
- Preserve unrounded values for aggregation.
- Normalize text used for mapping without changing the original text.

### Stage D — Classify and measure

For each raw fact, the rule engine performs:

1. Scope filtering.
2. Classification mapping.
3. Base quantity selection.
4. Opening/void/deduction rule.
5. Waste or procurement factor.
6. Minimum measurable quantity.
7. Rounding after aggregation.
8. BOQ grouping.

Formula:

```text
NetQuantity  = max(0, RawQuantity - Deductions)
PayQuantity  = round(max(MinimumQuantity, NetQuantity * (1 + WastePercent)), Precision)
Variance     = PayQuantity(current issue) - PayQuantity(previous issue)
```

### Stage E — Snapshot and export

The immutable issue snapshot contains source rows, rules, QA findings, waivers,
aggregations and approval metadata. Editing a profile creates a new calculation run;
it never rewrites an issued run.

## 5. Default measurement adapters

| Scope | Preferred source | Typical output | Required QA |
|---|---|---|---|
| Walls | Material area/volume; wall length as secondary | m2, m3, m | joins, openings, stacked/curtain walls |
| Floors/Roofs | Material area/volume | m2, m3 | openings, slopes, shape edits, parts |
| Ceilings | Area and material area | m2 | room/zone and voids |
| Doors/Windows | Instance count, type dimensions | ea, m2 | Mark, type, From/To Room |
| Columns/Framing | Native volume/length and material volume | m3, m | cutbacks, joins and coping |
| Rebar | total bar length/volume; density conversion | kg, t, m | bar diameter, quantity, couplers |
| Pipes/Ducts/Cable trays | centerline length | m | fittings, accessories, insulation |
| Fittings/Equipment/Fixtures | instance count | ea | system, type and classification |
| Rooms/Spaces | area/volume | m2, m3 | placed, enclosed and phase |
| Façade panels | panel count/area and mullion length | ea, m2, m | nested panels and replacements |

Material quantities are preferred where the contract item is a material. Autodesk
notes that Revit can approximate material volumes for some joined wall conditions;
these rows must retain their native-source flag and may be reconciled against geometry.

## 6. Rule profile

Rule profiles are versioned JSON documents validated before use. A profile contains:

- Project measurement basis and classification system.
- Category/family/type/material match conditions.
- Required information fields.
- Quantity source priority and fallback policy.
- Units, deductions, waste, minimums and rounding.
- Grouping dimensions and output item descriptions.
- Tolerances and blocker/warning thresholds.

The starter profile is stored in `QTO-RULES.example.json`.

## 7. User interface

Use one focused WPF window with six steps:

1. **Scope** — documents, links, phases, levels, zones and categories.
2. **Data Check** — blockers/warnings with Fix, Select and Isolate actions.
3. **Rules** — choose a versioned rule profile and preview matches.
4. **Quantities** — grouped grid with source-element drill-down.
5. **Changes** — compare with an earlier snapshot.
6. **Issue** — approval metadata and export.

The main grid columns are Code, Description, Unit, Raw, Deduction, Waste, Pay Quantity,
Variance, Element Count, Confidence and QA Status. Expanding a row shows every source
element and formula step.

### 7.1 Revit ribbon placement

Quantity Takeoff remains inside the existing **K-TOOLS** ribbon tab. It has a dedicated
panel named **K-QS**; no separate Revit tab is created.

The panel stays compact:

- **Quantity Takeoff** — large primary button opening the complete six-step workflow.
- **Data Check** — direct access to model-information preflight.
- **Compare** — compare the current calculation with an earlier snapshot.
- **Rules / Issue History** — pulldown containing rule-profile management and issued runs.

The main Quantity Takeoff window remains the authoritative workspace. Ribbon items are
entry points and do not duplicate the full workflow.

### 7.2 Workbench layout

```text
+----------------------+--------------------------------+----------------------+
| MODEL TREE           | QUANTITY / BOQ                 | ELEMENT INSPECTOR    |
| Discipline           | Code Description Unit Quantity | Source parameter     |
|  Category            | Raw Deduction Waste Pay Amount | Formula trace        |
|   Level / Zone       |                                | Rule + confidence    |
|    Type / Material   |                                | QA / variance        |
+----------------------+--------------------------------+----------------------+
| Scope | Data Check | Rules | Quantities | Changes | Issue                  |
+--------------------------------------------------------------------------+
```

The initial K-QS implementation already provides native collection, grouped quantity
rows, Data Check, source-element selection and XLSX audit export. The next implementation
increment replaces hard-coded classification with the rule editor and adds saved runs.

## 8. Output package

Each issue produces a folder containing:

- `BOQ.xlsx`: formatted summary and discipline sheets.
- `QTO-detail.xlsx`: element-level audit rows.
- `QTO-data.json`: machine-readable snapshot.
- `QTO-rules.json`: exact rule profile used.
- `QTO-QA.xlsx`: findings, waivers and responsible person.
- `QTO-manifest.json`: hashes, model identities, timestamps and approvals.

CSV export is optional. Revit schedules can be created for model-side review, but the
external audit package remains the issued record.

## 9. Application architecture

```text
Tools/KhimGen/QuantityTakeoff/
  Commands/       CmdQuantityTakeoff, CmdQtoCompare
  Forms/          QuantityTakeoffWindow
  Models/         RawFact, QuantityLine, RuleProfile, Snapshot, QaFinding
  Collectors/     HostCollector, LinkCollector, MaterialCollector
  Adapters/       Wall, Floor, Framing, Rebar, MEP, Room, Facade
  Rules/          Matcher, FormulaEngine, UnitNormalizer
  Services/       Preflight, Aggregation, Snapshot, Comparison, Export
```

Revit API access remains on the Revit thread. Collectors extract immutable DTOs in one
pass. Validation, rules, aggregation and file generation operate on DTOs and can run
without repeatedly querying the model.

### 9.1 Optional Cubicost reconciliation

K-QS can use Cubicost as an independent quantity-control channel. K-QS remains the
source trace back to Revit elements; Cubicost supplies a second calculation based on
the QS measurement rules configured there.

```text
Revit model
  -> K-QS native calculation -> K-QS snapshot
  -> RVT/IFC exchange        -> Cubicost TAS/TRB/TME calculation
                                -> Cubicost XLSX/CSV result
K-QS snapshot + Cubicost result
  -> mapping -> variance report -> reviewed/accepted exception
```

Exchange requirements:

- Write a stable shared parameter such as `KTOOLS_QTO_ID` before IFC exchange when the
  project permits it; also retain Revit `UniqueId` and IFC GUID in the audit map.
- Export classification code, category, family/type, material, level, zone, phase,
  host ID, Bar Mark, diameter and quantity unit.
- Record the exact Cubicost product/workbook, rule set and calculation date used.
- Never match rows by description alone. Use stable ID where available; otherwise use
  an approved composite key.
- Import Cubicost results as a new immutable reconciliation run. Do not overwrite K-QS
  raw quantities.

Default comparison keys:

| Quantity | Comparison key |
|---|---|
| Concrete | classification + category + concrete grade/material + level/zone |
| Rebar | host + level/zone + diameter + shape + Bar/Schedule Mark |
| Structural steel | category + section/type + grade/material + level/zone |
| Masonry wall | wall type + material + thickness + level/zone |
| Paint/plaster | finish material + host/room + level/zone |
| MEP | system + service + type/size + level/zone |

Variance fields:

```text
Difference    = KQSQuantity - CubicostQuantity
DifferencePct = Difference / CubicostQuantity * 100
```

Zero denominators are reported as Added/Removed rather than a percentage. Tolerance is
configured by item and unit. A total passing tolerance cannot hide failing detail rows.
Every variance is assigned a reason such as mapping, scope, opening deduction, rounding,
waste, model revision or measurement-rule difference before approval.

### 9.2 Internal measurement engine

The internal engine is the primary calculator and has no runtime dependency on Cubicost.
It consists of:

- `FactExtractor`: reads native Revit quantities and stable source identities once.
- `ClassificationMapper`: maps elements/materials to project work items.
- `RuleMatcher`: evaluates ordered rule conditions and reports unmatched/ambiguous rows.
- `FormulaEngine`: applies deductions, factors, minimums and rounding with a visible trace.
- `Aggregator`: groups only after element-level calculation.
- `SnapshotStore`: saves immutable calculation and issue runs.
- `VarianceEngine`: compares model revisions, rule revisions and optional external results.
- `BoqExporter`: creates summary, detail, QA, rules and manifest outputs.

Rules use a restricted operation set rather than executable scripts. Supported operations
include arithmetic, percentage, threshold, conditional deduction and unit conversion.
Every operation stores its inputs and output in the formula trace.

## 10. Delivery phases

### Phase 1 — Reliable baseline

- Host model scope, categories, levels and phases.
- Wall, floor, roof, column, framing, door/window and room adapters.
- Preflight, raw facts, rule engine and element drill-down.
- XLSX/JSON export and immutable snapshots.

### Phase 2 — Coordination

- Revit links and duplicate-link controls.
- Material-level takeoff, Rebar and MEP adapters.
- Snapshot comparison and model isolation actions.
- Rule-profile editor and mapping assistant.

### Phase 3 — Information exchange

- IDS-aligned requirement import/export for classification and required properties.
- IFC identity/property mapping.
- Checked/Approved/Issued workflow and audit manifest.
- Optional BOQ templates for a selected local or contractual method of measurement.

## 11. Acceptance criteria

- Every quantity row drills down to one or more stable source identities.
- Re-running the same snapshot and rule profile produces the same totals.
- Unit conversion occurs once and rounding occurs after aggregation.
- Host and linked elements cannot be double-counted silently.
- Missing classification and invalid quantities appear in preflight.
- Every fallback and manual override is visible in the audit output.
- Issued runs are immutable and comparable.
- A sample model is reconciled against native Revit schedules within configured
  tolerances, with known Revit approximation cases documented.
- Calculation core can be tested without opening Revit.

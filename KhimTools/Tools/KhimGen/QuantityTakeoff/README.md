# K-QS Quantity Takeoff

Current baseline collector supports:

- Concrete volume by material (`m³`).
- Reinforcement mass from native Rebar volume at 7,850 kg/m³ (`kg`).
- Structural steel mass from material volume at 7,850 kg/m³ (`kg`).
- Masonry volume (`m³`).
- Paint, plaster and finish material area (`m²`).
- Door, window and equipment counts (`ea`).
- Pipe, duct, cable tray and conduit centerline length (`m`).
- Room area (`m²`).

The baseline has zero waste by default. Waste, deductions and contractual rounding must
come from an approved project rule profile. Every aggregate retains source Element IDs
and UniqueIds for Revit selection and exported audit data.

The Revit workbench's primary collector reads the entire host document (all supported
instances), not the active view or current selection; links are excluded. This scope is
shown in the workbench. The separate information scanner supports active-view, explicit
selection-ID, and entire-model scopes; selection requires IDs and an invalid view/scope
fails closed rather than widening to the whole document. Linked-document scope remains
deferred.

The rules editor and basic source comparison are wired to the workbench. BOQ mapping,
revision/project controls, issue snapshots, and the project-store services currently have
no workbench call sites and are not represented as delivered UI capabilities. Material
classification remains a deterministic English/Vietnamese name heuristic; unknown or
unmatched materials stay unclassified and are reported for review.

The target product is an internal K-QS Measurement Workbench: model tree, editable rules,
formula trace, BOQ hierarchy, revision comparison and issue control inside Revit. Cubicost
import is an optional reconciliation adapter, not a runtime requirement.

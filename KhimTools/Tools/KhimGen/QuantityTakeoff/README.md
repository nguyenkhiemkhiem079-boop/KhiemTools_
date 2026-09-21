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

The current collector reads the host document. Linked-document scope, snapshot comparison
and the JSON rule-profile engine are planned in the QTO specification under `Docs/QTO`.

The target product is an internal K-QS Measurement Workbench: model tree, editable rules,
formula trace, BOQ hierarchy, revision comparison and issue control inside Revit. Cubicost
import is an optional reconciliation adapter, not a runtime requirement.

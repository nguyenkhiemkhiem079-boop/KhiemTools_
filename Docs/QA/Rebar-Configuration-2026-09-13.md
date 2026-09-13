# Rebar configuration implementation

## Implemented

- Project defaults and member-kind overrides for beam, rectangular column,
  circular column, slab and foundation forms.
- Explicit per-field override checkbox, native boolean value cells, scope draft
  preservation, reload, JSON import/export and save/apply.
- Values write into existing generation controls; local form edits remain possible.
- Beam generation input now carries configured anchorage multiplier, hook-tail
  multiplier and side-bar threshold. Uniform stirrup mode supplies equal A1/A2
  spacing and rejects non-positive spacing.
- Project-scoped local JSON storage requires a saved RVT. Files use a hash of
  ProjectInformation.UniqueId and document path. Save As/relocation requires
  explicitly importing the exported configuration into the new project scope.
- Schema validation, revision conflict rejection, serialized writes, atomic file
  replacement and previous-revision backups. Invalid files are not reset silently.
- Separate full-height reference tabs preserve existing previews. Reference
  diagrams are schematic, not generated reinforcement or compliance checks.

## Verification

- Release net48 (Revit 2023 references) and net8.0-windows builds passed without
  compiler warnings/errors.
- 36 configuration checks: inheritance, file persistence, revision backups,
  stale-write protection, invalid JSON/ranges, control binding, scope drafts,
  beam generation-input mapping and uniform-mode spacing.
- Offline layout fixtures cover five generation forms at minimum/wide sizes.
  These are not tests of real Revit DPI or reinforcement creation.

## Not Implemented / Not Certified

- Element-specific overrides exist in the storage/resolution model only; no
  element-scope editor or per-element generation application is wired yet.
- Created rebars do not yet carry the configuration revision as model metadata.
- Stair/ramp generation and configuration are not implemented.
- Arbitrary bar editing (crank/anchor/splice at a user-picked point), copy,
  delete, explode, extend and split tools are not implemented by this change.
- Opening reinforcement still uses the existing generator; no dedicated opening
  settings editor or rewritten opening geometry is included.
- Legacy beam per-end anchor text fields and column general hook/bend controls
  still require an input/geometry audit. Do not assume every legacy field affects
  generated bars.
- Material/code selection remains in existing dialogs/templates. These new
  numeric presets are not a standard-derived design calculation or approval.
- No live Revit model fixture, host/cover/clash proof, PDF shape conformance,
  technical sign-off, installed DLL update, MSI release or GitHub push is claimed.

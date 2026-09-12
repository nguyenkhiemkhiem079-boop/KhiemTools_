# K-TOOLS UI/UX rebuild

This folder is the single source of truth for the UI/UX rebuild:

- [Specification](SPEC.md)
- [Roadmap](ROADMAP.md)
- [Scope](SCOPE.md)
- [Implementation constraints](CONSTRAINTS.md)
- [UX contract](UX_CONTRACT.md)

## Product principles

- Build task-oriented workspaces: selection, configuration, validation, execution, result.
- Keep one primary action per screen and disable it while input has blocking errors.
- Show units beside numeric fields; never encode units only in placeholder text.
- Keep technical warnings in a persistent validation panel instead of long modal dialogs.
- Preserve Revit context: selected element count, active level/view and affected host IDs.
- Use color semantically: blue for action, green for success, amber for review, red for blocking errors.

## Shared shell

- Neutral canvas, white working surfaces and charcoal headers.
- Segoe UI with 30 px minimum inputs and 32 px minimum buttons.
- Consistent Escape-to-close behavior and visible keyboard focus.
- Resizable data-heavy windows; scrolling content for smaller or high-DPI displays.
- Standard tables with fixed headers, 32 px rows and non-color-only selection feedback.

## Rebar workspace

Every Beam, Column, Slab and Foundation screen should converge on three regions:

1. **Hosts**: filterable element list with level, type and validation state.
2. **Reinforcement**: Main bars, Stirrups, Anchorage & Splice, Advanced tabs.
3. **Preview & QA**: section/elevation preview plus live errors and warnings.

The footer contains only Validate, Create/Update Rebar and Cancel. Template and language
commands belong in the toolbar. Create/Update remains disabled while blocking validation exists.

## Migration order

1. Rectangular and circular columns: multi-story continuity, lap zones and connection preview.
2. Beams: left support, span and right support zoning with anchorage state.
3. Slabs: panel map, openings, support edges and X/Y layers.
4. Foundations: real column/wall footprint, dowels and perimeter reinforcement.
5. General tools: convert absolute layouts to table/grid layouts and remove duplicated styles.

## QA acceptance

- No clipped or overlapping controls at 100%, 125%, 150% and 200% DPI.
- Keyboard traversal follows visual order; Escape cancels and Enter invokes only the primary action.
- Every visible command has an event/command binding and a disabled, busy and failure state.
- Vietnamese and English strings fit without changing the window geometry.
- Empty, loading, partial-success and error states are visible without inspecting logs.

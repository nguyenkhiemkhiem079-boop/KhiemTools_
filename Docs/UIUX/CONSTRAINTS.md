# Anti Brief — K-TOOLS UI/UX 2.0

Implement the UI/UX rebuild against the latest master.

## Hard constraints
- Preserve all existing functionality and command routing.
- Do not modify Rebar Engineering logic, geometry generation, detailing rules, validators, or safety behavior.
- Do not modify Family Manager backend loading semantics, Family/Type behavior, source roots, priority, or preservation rules.
- Do not modify installer/updater behavior.
- Keep command IDs/handlers wired unless a pure UI navigation wrapper is required.

## Product direction
K-TOOLS should feel like a compact BIM Engineering Workspace, not a collection of unrelated utilities.

## Ribbon
- Reduce clutter and duplicated entry points.
- Keep Ribbon for navigation + high-frequency commands.
- Group by module: K-TOOLS, STRUCTURE, REBAR, ARCHI, MEP, DOCS.
- Use split/pulldown for secondary actions.
- Remove the large K-TOOLS visual bar/panel that consumes workspace.
- Preserve access to every existing command.

## Workspace
- Keep as dockable command center.
- Replace long always-expanded tool list with Search + Recent + Favorites + module navigation.
- Module sections should be collapsible.
- Maintain compact width and low vertical density.

## Design system
Create shared reusable WPF resources/tokens for:
- typography
- surfaces/backgrounds
- borders
- primary/secondary/tertiary/destructive buttons
- cards
- text fields/comboboxes
- status chips
- spacing/radius
- 16px/32px icons
Remove emoji as primary product iconography.

## Shared tool shell
Use a common visual shell for Quick Structure, Quick Archi, and future Quick MEP:
Header -> purpose -> mode selection -> parameters -> primary action -> status.

## Rebar UX
Organize discovery into CREATE / DETAIL / ENGINEERING / QA. This is only navigation/presentation; do not change engineering code.

## Family Manager UX
Use Library -> Family -> Type hierarchy; retain search/filter/status and all safety semantics. Compact action hierarchy.

## Acceptance
- Build succeeds for supported targets.
- All existing commands remain invokable.
- No Rebar/Family backend regressions.
- Ribbon visibly more compact.
- Workspace no longer depends on a long all-tools scroll.
- No emoji used as primary product icons.
- Shared visual language across Workspace/Quick/Family/Rebar.
- Provide before/after screenshots and a changed-file summary.

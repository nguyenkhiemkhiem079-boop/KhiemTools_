# K-TOOLS UI/UX 2.0 — Implementation Roadmap

Phase 1 — Foundation
- Shared WPF design tokens/resource dictionary
- Shared typography, buttons, cards, fields, status chips
- Shared icon conventions

Phase 2 — Workspace
- Compact command center
- Search
- Recent/Favorites
- Collapsed module navigation
- Remove long always-expanded tool catalog

Phase 3 — Ribbon
- Reduce panels and duplicate entry points
- Promote Workspace / Family / module entry commands
- Repack secondary actions into split/pulldown controls
- Preserve command IDs and handlers

Phase 4 — Reusable tool shells
- Quick Structure
- Quick Archi
- Future Quick MEP
- Standard title/header/parameters/actions/status pattern

Phase 5 — Rebar UX
- CREATE / DETAIL / ENGINEERING / QA hierarchy
- Keep all existing engineering back-end logic untouched
- Do not alter detailing behavior

Phase 6 — Family Manager UX
- Library / Family / Type hierarchy
- Compact data presentation
- Clear states and actions
- Preserve existing source root and safety semantics

Phase 7 — Acceptance
- Build all supported targets
- Revit runtime smoke test
- Confirm command invocation
- Confirm no functional regressions
- Compare ribbon/workspace density before vs after

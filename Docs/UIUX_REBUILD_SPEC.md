# K-TOOLS UI/UX 2.0 — Rebuild Specification

## Goal
Transform K-TOOLS from a collection of individual Revit commands into a compact BIM Engineering Workspace while preserving all existing functionality and engineering logic.

## Non-negotiables
- Do not change Rebar Engineering algorithms, geometry, detailing rules, validators, or fallback behavior.
- Do not change Family Manager loading semantics, Family/Type model, source-root behavior, or preservation rules.
- Do not change installer/updater behavior.
- UI-only / presentation / navigation / interaction refactor, except lightweight ViewModel bindings required to support UX.
- No destructive replacement of user content.

## Information architecture
1. Ribbon = navigation and high-frequency entry points only.
2. Khim Workspace = command center: search, recent/favorites, module navigation.
3. Tool windows = focused configuration workflows.
4. Search/command launcher = fastest path to any command.

## Primary modules
- Quick Structure
- Rebar
- Families
- Architecture
- MEP
- Documentation
- General / Model

## Ribbon target
Compact panels, minimal duplication, no large K-TOOLS bar. Group by module and intent rather than by implementation.

Target panels:
- K-TOOLS: Workspace, Search, Family Manager, Update/About
- STRUCTURE: Quick Structure, Structure utilities
- REBAR: Rebar Create, Rebar Engineering, Rebar QA
- ARCHI: Quick Archi, Room/Finish/View tools
- MEP: Openings, Elevation Tags
- DOCS: Section, Viewport, Sheets, Tags

Use split/pulldown buttons for secondary commands. Keep primary commands discoverable and avoid 1-button-per-micro-operation ribbon clutter.

## Workspace target
Dockable, compact command center with:
- Header / product identity
- Search box
- Recent commands
- Favorites
- Module navigation
- Optional contextual actions

Do not display every tool as a long always-expanded list.

## Design system
- Segoe UI
- Shared colors/tokens
- Shared button hierarchy: Primary / Secondary / Tertiary / Destructive
- Shared card / field / status components
- Shared 16px and 32px icon system
- Remove emoji as primary product icons
- Consistent spacing scale and corner radius

Suggested tokens:
Brand.Primary, Brand.PrimaryHover, Surface.Background, Surface.Card, Surface.Border,
Text.Primary, Text.Secondary, Text.Muted, Status.Success, Status.Warning, Status.Error.

## Family Manager target
Library -> Family -> Type hierarchy; search/filter; clear loaded/update/missing state; preserve multi-root library semantics; compact action bar.

## Quick tools target
Use a common shell for Quick Structure / Quick Archi / future Quick MEP:
- tool selection cards
- focused parameters
- clear primary action
- consistent empty/loading/error/success states

## Rebar UX target
Organize by workflow:
- CREATE
- DETAIL
- ENGINEERING
- QA

Engineering and QA should be presented as distinct workflows from creation.

## Acceptance criteria
- Ribbon has materially lower visual density than current implementation.
- Workspace is navigable without a long all-tools scroll list.
- No emoji as primary iconography.
- Family Manager, Quick Structure, Quick Archi use shared visual language.
- Rebar command discovery is organized by workflow.
- Existing command handlers remain wired and functional.
- Build succeeds for supported Revit targets.

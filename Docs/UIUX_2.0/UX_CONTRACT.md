# K-TOOLS UI/UX 2.0 — MASTER UX CONTRACT & SPECIFICATION

**Project**: K-TOOLS (KhimTools) Autodesk Revit Multi-Year Add-In Suite  
**Role**: Senior Product Designer + WPF/Revit UI Architect + Senior Code Reviewer  
**Status**: DRAFT / PHASE 0 AUDIT COMPLETE  
**Base Commit**: `7245418bafd444df09ff4e7c350a6cd0d35c80ca`  
**Target Delivery**: Commercial-Grade BIM Engineering Workspace  

---

## 0. Executive Mandate & Non-Negotiable Boundaries

The objective of K-TOOLS UI/UX 2.0 is **NOT** to make individual dialogs marginally prettier with ad-hoc colors. The objective is to reconstruct the **Product-Level Information Architecture, Navigation, Density, Interaction Model, and Shared Design System** so that K-TOOLS feels and behaves as **ONE unified, commercial-grade BIM Engineering Platform**.

### Strictly Forbidden Modifications (Blacklist)
1. **Rebar Engineering Calculations**: All TCVN 5574:2018 and Eurocode 2 formulas (`RebarEngineeringService.cs`, `RebarEngineeringCalculations.cs`).
2. **Rebar Geometry & Placement**: Generation algorithms for columns, beams, slabs, foundations, and hooks (`ColumnRebarGenerator.cs`, `BeamRebarGenerator.cs`).
3. **Rebar Validation Rules**: Containment, bar spacing, stirrup spacing, diameter ratio rules (`RebarValidationRules.cs`, `RebarHostValidator.cs`).
4. **Rebar Anchorage, Lap, & Splice Lengths**: Calculation engines and table lookup logic.
5. **Family Manager Backend & Loading**: Multi-root discovery, transactional family loading, duplicate resolution (`FamilyManagerService.cs`, `FamilyPathResolver.cs`).
6. **Family Source-Root Resolution**: Detection of MSI-managed vs UserManaged vs custom probe roots.
7. **UserManaged / Unknown Preservation**: Directory classification and preservation rules (`DeploymentSecurity.cs`).
8. **Quick Structure Engineering**: Grid intersection mathematics (`GridIntersectionHelper.cs`) and structural element generation logic.
9. **Quick Archi Engineering**: Wall alignment algorithms, room boundary loops, and finish generation transactions.
10. **Installer / MSI / Updater Architecture**: WiX packages, update manifests, bootstrap pipelines, and installation boundaries.
11. **Existing Command Functionality & Command IDs**: Zero command removals; 100% command backwards compatibility.

---

## A. Current UI Inventory

### 1. Revit Ribbon Panels (`KhimTools\Core\RibbonBuilder.cs`)
The current Ribbon is organized across **5 panels** on tab `"K-TOOLS"`:
- **Panel 1: `K-GEN`**
  - Large PushButtons: `CmdToggleWorkspace`, `CmdCopyLinkElements`, `CmdJoinElements`, `CmdGridPlanGenerator`, `CmdSheetExport`, `CmdElementTags`.
  - Stacked Pulldowns:
    - `VisibilityShowPulldown` ("Hiển thị") with 17 sub-buttons (`CmdShowWindow` .. `CmdShowTag`).
    - `VisibilityHidePulldown` ("Ẩn") with 17 sub-buttons (`CmdHideWindow` .. `CmdHideTag`).
  - Large Pulldown: `KhimLayoutPulldown` ("Layout") with 12 sub-buttons (`CmdSheetGen`, `CmdSlabStep`, `CmdFamilyManager`, `CmdAlignViewport`, `CmdUpdateDetailNumbers`, text alignments).
  - Large Pulldown: `KhimViewToolsPulldown` ("View Tools") with 3 sub-buttons (`CmdSectionBox`, `CmdCalloutPro`, `CmdViewFromCallout`).
  - Stacked Pulldown: `LanguagePulldown` (3 items) & PushButton `CmdCheckUpdate`.
- **Panel 2: `Override`**
  - 3x3 Color Swatch Matrix (9 PushButtons: Red, Orange, Yellow, Green, Cyan, Blue, Magenta, Gray, Custom).
  - Large PushButtons: `CmdQuickHalftone`, `CmdQuickResetOverride`, `CmdGraphicOverdrive`.
- **Panel 3: `K-STRUCTURAL`**
  - Large PushButtons: `CmdQuickStructure`, `CmdBeamRebar`, `CmdSlabRebar`, `CmdFoundationRebar`, `CmdSectionCut`, `CmdProjectCoverSetup`, `CmdLoadRebarShapesMain`.
  - SplitButton: `ColumnRebarSplitButton` with 6 items (`CmdColumnRebar`, `CmdMultiColumnRebar`, `CmdMultiRoundColumnRebar`, `CmdColumnDrawing`, `CmdUpdateColumnDrawing`, `CmdLoadRebarShapes`).
- **Panel 4: `K-ARCHITECTURAL`**
  - Large PushButtons: `CmdQuickArchi`, `CmdRoom3DView`, `CmdWallFloorFinishes`.
- **Panel 5: `K-MEP`**
  - Large PushButtons: `CmdMepOpenings`, `CmdMepElevationTags`.

### 2. Dockable Workspace Pane (`KhimTools\Tools\KhimGen\Workspace\`)
- **View**: `Views\KhimWorkspacePane.xaml` + `KhimWorkspacePane.xaml.cs`.
- **ViewModel**: `ViewModels\KhimWorkspaceViewModel.cs`.
- **Current Layout**: Flat vertical `StackPanel` containing 13 hardcoded action cards with emoji icons (`🏛`, `🏗`, `🔲`, `🧱`, `🔗`, `🏢`, `🏠`, `🕳`, `🏷`, `✂`, `📐`, `🔢`, `🖨`), gradient header banner, and footer text block. No search, no category tabs, no recent history, no pinning.

### 3. Dialogs & Tool Windows
| Window / Form | Technology | Location | Primary Purpose | Styling Status |
| :--- | :--- | :--- | :--- | :--- |
| `QuickStructureWindow.xaml` | WPF | `Tools\KhimStructural\QuickStructure\Forms\` | Multi-grid structural column/beam/foundation generator | Hardcoded Blue Theme, emoji in header |
| `QuickArchiWindow.xaml` | WPF | `Tools\KhimArchitectural\QuickArchi\Forms\` | Model/CAD curve to wall & room generator | Hardcoded Green Theme, emoji in header |
| `FamilyManagerWindow.xaml` | WPF | `Tools\KhimGen\FamilyManager\Forms\` | Revit Family Library inspection & loading | Hardcoded Slate Theme, emoji icons |
| `RebarShapeLoaderWindow.xaml` | WPF | `Tools\KhimStructural\RebarTool\Forms\` | 43 standard rebar shape loader | Hardcoded Dark Theme (`#181B22`), inconsistent |
| `GraphicOverdriveWindow.xaml` | WPF | `Tools\KhimGen\OverrideTool\Forms\` | Element graphic override settings | Hardcoded Dark Theme (`#0F172A`) |
| `SectionBoxWindow.xaml` | WPF | `Tools\KhimGen\SectionBox\Forms\` | 3D Section box generator | Standalone WPF layout |
| `CalloutProWindow.xaml` | WPF | `Tools\KhimGen\CalloutPro\Forms\` | Advanced callout view manager | Standalone WPF layout |
| `ViewFromCalloutWindow.xaml`| WPF | `Tools\KhimGen\ViewFromCallout\Forms\` | Batch view generator from callouts | Standalone WPF layout |
| `UpdaterWindow.xaml` | WPF | `Tools\KhimGen\Updater\Views\` | In-app release updater dialog | Standalone WPF layout |
| `AppUpdaterWindow.xaml` | WPF | `App\` | Standalone updater launcher | Standalone WPF layout |
| `BeamReinforcementForm.cs` | WinForms | `Tools\KhimStructural\RebarTool\Forms\` | Full beam rebar detail configuration | Uses `KhimUiStyle` base |
| `RectangularColumnReinforcementForm.cs`| WinForms | `Tools\KhimStructural\RebarTool\Forms\` | Rectangular column rebar configuration | Uses `KhimUiStyle` base |
| `CircularColumnReinforcementForm.cs` | WinForms | `Tools\KhimStructural\RebarTool\Forms\` | Round column rebar configuration | Uses `KhimUiStyle` base |
| `SlabReinforcementForm.cs` | WinForms | `Tools\KhimStructural\RebarTool\Forms\` | Slab rebar configuration | Uses `KhimUiStyle` base |
| `FoundationReinforcementForm.cs` | WinForms | `Tools\KhimStructural\RebarTool\Forms\` | Foundation rebar configuration | Uses `KhimUiStyle` base |
| `ProjectCoverSetupForm.cs` | WinForms | `Tools\KhimStructural\RebarTool\Forms\` | Concrete cover setup dialog | Uses `KhimUiStyle` base |

---

## B. Command Compatibility Map (100% Preservation)

All 89 registered button instances (85 unique command classes) must retain exact assembly and full class names. **NO COMMAND SHALL BE BROKEN OR ORPHANED.**

| Button ID | Label | Command Class Binding | Panel (Current) | Target Architecture |
| :--- | :--- | :--- | :--- | :--- |
| `CmdToggleWorkspace` | Khim Workspace | `KhimTools.Workspace.Commands.CmdToggleWorkspace` | K-GEN | WORKSPACE |
| `CmdCopyLinkElements` | Copy Link Elements | `KhimTools.CopyLink.Commands.CmdCopyLinkElements` | K-GEN | GENERAL |
| `CmdJoinElements` | Join Elements | `KhimTools.SlabJoin.Commands.CmdJoinElements` | K-GEN | STRUCTURE |
| `CmdGridPlanGenerator` | Grid & Floor Plan | `KhimTools.GridLevel.Commands.CmdAutoGridPlan` | K-GEN | GENERAL |
| `CmdShowWindow` .. `CmdShowTag` (17 items) | Hiển thị [Category] | `KhimTools.VisibilityTool.Commands.CmdShow*` | K-GEN (Show) | GENERAL (Pulldown) |
| `CmdHideWindow` .. `CmdHideTag` (17 items) | Ẩn [Category] | `KhimTools.VisibilityTool.Commands.CmdHide*` | K-GEN (Hide) | GENERAL (Pulldown) |
| `CmdSheetGen` | Create Sheets (CSV) | `KhimTools.SheetGen.Commands.CmdSheetGen` | K-GEN (Layout) | GENERAL (Doc) |
| `CmdSlabStep` | Slab Step Generator | `KhimTools.SlabStep.Commands.CmdSlabStep` | K-GEN (Layout) | STRUCTURE |
| `CmdFamilyManager` | Family Manager | `KhimTools.FamilyManager.Commands.CmdFamilyManager` | K-GEN (Layout) | FAMILIES |
| `CmdAlignViewport` | Align Viewports | `KhimTools.ViewportAlign.Commands.CmdAlignViewport` | K-GEN (Layout) | GENERAL (Doc) |
| `CmdUpdateDetailNumbers` | Update Detail No | `KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers` | K-GEN (Layout) | GENERAL (Doc) |
| `CmdAlignTop` .. `VerticalEquals` (7 items) | Align Text [*] | `KhimTools.TextAlign.Commands.CmdAlign*` | K-GEN (Layout) | GENERAL (Doc) |
| `CmdSectionBox` | Section Box Pro | `KhimTools.SectionBox.Commands.CmdSectionBox` | K-GEN (View) | GENERAL (View) |
| `CmdCalloutPro` | Callout Pro | `KhimTools.CalloutPro.Commands.CmdCalloutPro` | K-GEN (View) | GENERAL (View) |
| `CmdViewFromCallout` | View from Callout | `KhimTools.ViewFromCallout.Commands.CmdViewFromCallout` | K-GEN (View) | GENERAL (View) |
| `CmdSheetExport` | Sheet Exporter | `KhimTools.SheetExport.Commands.CmdSheetExport` | K-GEN | GENERAL (Doc) |
| `CmdElementTags` | Elements Tags | `KhimTools.ElementTags.Commands.CmdElementTags` | K-GEN | GENERAL (Doc) |
| `CmdSwitchLanguage` .. `English` (3 items) | Ngôn ngữ | `KhimTools.LanguageSwitcher.Commands.Cmd*` | K-GEN (Lang) | GENERAL (System) |
| `CmdCheckUpdate` | Check Update | `KhimTools.Updater.Commands.CmdCheckUpdate` | K-GEN | GENERAL (System) |
| `CmdOverrideRed` .. `Custom` (9 items) | Color Swatches | `KhimTools.OverrideTool.Commands.CmdOverride*` | Override | GENERAL (Override) |
| `CmdQuickHalftone` | Halftone | `KhimTools.OverrideTool.Commands.CmdQuickHalftone` | Override | GENERAL (Override) |
| `CmdQuickResetOverride` | Reset Override | `KhimTools.OverrideTool.Commands.CmdQuickResetOverride` | Override | GENERAL (Override) |
| `CmdGraphicOverdrive` | Graphic Overdrive | `KhimTools.OverrideTool.Commands.CmdGraphicOverdrive` | Override | GENERAL (Override) |
| `CmdQuickStructure` | Quick Structure | `KhimTools.Structural.QuickStructure.Commands.CmdQuickStructure` | K-STRUCTURAL | STRUCTURE |
| `CmdColumnRebar` | Column Rebar Auto | `KhimTools.RebarTool.Commands.CmdColumnRebar` | K-STRUCTURAL | REBAR (Create) |
| `CmdMultiColumnRebar` | Cột Vuông/Chữ Nhật | `KhimTools.RebarTool.Commands.CmdMultiColumnRebar` | K-STRUCTURAL | REBAR (Create) |
| `CmdMultiRoundColumnRebar` | Cột Tròn | `KhimTools.RebarTool.Commands.CmdMultiRoundColumnRebar` | K-STRUCTURAL | REBAR (Create) |
| `CmdColumnDrawing` | Column Drawing | `KhimTools.RebarTool.Commands.CmdColumnDrawing` | K-STRUCTURAL | REBAR (Detail) |
| `CmdUpdateColumnDrawing` | Update Drawing | `KhimTools.RebarTool.Commands.CmdUpdateColumnDrawing` | K-STRUCTURAL | REBAR (Detail) |
| `CmdLoadRebarShapes` | Rebar Shapes (43) | `KhimTools.RebarTool.Commands.CmdLoadRebarShapes` | K-STRUCTURAL | REBAR (Library) |
| `CmdBeamRebar` | Beam Rebar | `KhimTools.RebarTool.Commands.CmdBeamRebar` | K-STRUCTURAL | REBAR (Create) |
| `CmdSlabRebar` | Slab Rebar | `KhimTools.RebarTool.Commands.CmdSlabRebar` | K-STRUCTURAL | REBAR (Create) |
| `CmdFoundationRebar` | Foundation Rebar | `KhimTools.RebarTool.Commands.CmdFoundationRebar` | K-STRUCTURAL | REBAR (Create) |
| `CmdSectionCut` | Section Cut | `KhimTools.SectionCutTool.Commands.CmdSectionCut` | K-STRUCTURAL | STRUCTURE / REBAR |
| `CmdProjectCoverSetup` | Cover Setup | `KhimTools.RebarTool.Commands.CmdProjectCoverSetup` | K-STRUCTURAL | REBAR (Engineering) |
| `CmdLoadRebarShapesMain` | Rebar Shapes Main | `KhimTools.RebarTool.Commands.CmdLoadRebarShapes` | K-STRUCTURAL | REBAR (Library) |
| `CmdQuickArchi` | Quick Archi | `KhimTools.Architectural.QuickArchi.Commands.CmdQuickArchi` | K-ARCHITECTURAL | ARCHI |
| `CmdRoom3DView` | Room 3D View | `KhimTools.Architectural.Rooms.CmdRoom3DView` | K-ARCHITECTURAL | ARCHI |
| `CmdWallFloorFinishes` | Room Finishes | `KhimTools.Architectural.Finishes.CmdWallFloorFinishes` | K-ARCHITECTURAL | ARCHI |
| `CmdMepOpenings` | MEP Openings | `KhimTools.MEP.Penetrations.CmdMepOpenings` | K-MEP | MEP |
| `CmdMepElevationTags` | Elevation Tags | `KhimTools.MEP.Tags.CmdMepElevationTags` | K-MEP | MEP |

---

## C. Current vs. Target Information Architecture

```
CURRENT ARCHITECTURE (Sprawling, uneven density):
K-TOOLS (Tab)
├── K-GEN (Large panel, 10 primary buttons, 4 pulldowns, 54 nested actions)
├── Override (3x3 grid + 3 large buttons)
├── K-STRUCTURAL (Quick Structure + 6 large buttons + 6-item split button)
├── K-ARCHITECTURAL (Quick Archi + 2 large buttons)
└── K-MEP (2 large buttons)

TARGET PRODUCT ARCHITECTURE (Clear domain hierarchy, commercial grade):
K-TOOLS (Tab)
│
├── WORKSPACE [Primary Entry / Command Center]
│   └── Khim Workspace (Large prominent button)
│
├── GENERAL [Common Productivity & Viewport Management]
│   ├── Grid & Floor Plan (Primary)
│   ├── Copy Link Elements (Primary)
│   ├── View Tools (Pulldown: Section Box, Callout Pro, View From Callout)
│   ├── Visibility (Pulldown: Show / Hide 17 categories)
│   ├── Text & Annotations (Pulldown: Align Text, Update Detail No, Element Tags)
│   └── System (Split: Language, Check Update, Sheet Exporter)
│
├── STRUCTURE [Core Structural Automation]
│   ├── Quick Structure (Primary generator)
│   ├── Join Elements (Primary geometry manager)
│   ├── Slab Step (Tool)
│   └── Section Cut (Structure views)
│
├── REBAR [Dedicated Rebar Engineering Suite]
│   ├── Create Rebar (SplitButton: Column, Beam, Slab, Foundation)
│   ├── Detail & Drawing (SplitButton: Column 2D Cut, Drawing Update)
│   ├── Engineering & QA (SplitButton: Cover Setup, Code Check QA)
│   └── Rebar Shapes (43 Library loader)
│
├── ARCHI [Architectural Automation]
│   ├── Quick Archi (Primary generator)
│   ├── Room 3D View (Section box isolator)
│   └── Room Finishes (Wall & floor finish generator)
│
├── MEP [MEP Coordination & Annotation]
│   ├── MEP Openings (Collision penetration generator)
│   └── Elevation Tags (BOP / Invert elevation annotator)
│
└── FAMILIES [BIM Library Management]
    └── BIM Library Manager (Central Family & Type Manager)
```

---

## D. Before → After UI Surface Map

| Surface | Current State (Before) | Target State (After 2.0) |
| :--- | :--- | :--- |
| **Ribbon Sprawl** | 5 panels, excessive horizontal width (~1400px minimum), mixed visual hierarchy, duplicate Rebar Shape entries | 7 compact panels aligned by domain; horizontal width reduced by 25-30%; high-frequency buttons elevated; deep commands cleanly organized into logical Pulldowns |
| **Workspace** | Flat list of 13 cards, emoji icons (`🏛`, `🏗`, `🔲`), no search, no recents, no scaling capacity | Command Center with Search/Filter (`Ctrl+K`), Recent Actions, Module Groups (Structure, Rebar, Archi, MEP, Doc), compact 28px density, consistent vector glyphs |
| **Quick Structure** | Standalone dialog, blue gradient, non-resizable, emoji headers (`🏗`), inconsistent button styles | Standardized interaction model (Header → Object Selection → Parameters → Primary Action → Status Bar), consuming Shared Design System tokens |
| **Quick Archi** | Standalone dialog, green gradient, duplicated style code, emoji headers (`🏛`, `🧱`) | Identical interaction model to Quick Structure, sharing the exact same card, form, combo, and button components |
| **Family Manager** | Monolithic dialog with emoji buttons (`📦`, `🔄`, `📂`, `📥`), flat list, ambiguous Family vs Type distinction | **BIM Library Manager** with explicit Library → Family → Type visual hierarchy, clean status badges, vector action buttons, high-speed filtering |
| **Rebar Shape Loader** | Dark-themed anomaly (`#181B22`) isolated from rest of light-themed suite | Unified light/dark neutral canvas aligned with the Shared Design System tokens, clean shape category filtering, clear load progress |
| **Override Panel** | Hardcoded 3x3 swatches with hacky AdWindows reflection for text hiding | Clean 3x3 color grid with native tooltips, elegant halftone/reset actions, consistent vector icon set |

---

## E. Design Tokens Proposal (WPF XAML)

To be housed in `KhimTools\UI\DesignSystem\Theme.xaml`:

### 1. Colors (`SolidColorBrush`)
- **Brand**:
  - `KTools.Brush.Brand.Primary`: `#0F172A` (Slate 900)
  - `KTools.Brush.Brand.Accent`: `#0284C7` (Sky 600)
  - `KTools.Brush.Brand.AccentHover`: `#0369A1` (Sky 700)
- **Surfaces & Cards**:
  - `KTools.Brush.Surface.Background`: `#F8FAFC` (Slate 50)
  - `KTools.Brush.Surface.Card`: `#FFFFFF` (Pure White)
  - `KTools.Brush.Surface.Elevated`: `#FFFFFF` (Shadow depth 2)
  - `KTools.Brush.Surface.Subtle`: `#F1F5F9` (Slate 100)
- **Borders**:
  - `KTools.Brush.Border.Default`: `#E2E8F0` (Slate 200)
  - `KTools.Brush.Border.Hover`: `#CBD5E1` (Slate 300)
  - `KTools.Brush.Border.Focus`: `#0284C7` (Sky 600)
- **Typography & Text**:
  - `KTools.Brush.Text.Primary`: `#0F172A` (Slate 900)
  - `KTools.Brush.Text.Secondary`: `#475569` (Slate 600)
  - `KTools.Brush.Text.Muted`: `#94A3B8` (Slate 400)
  - `KTools.Brush.Text.Disabled`: `#CBD5E1` (Slate 300)
  - `KTools.Brush.Text.OnAccent`: `#FFFFFF` (White)
- **Semantic Feedback**:
  - `KTools.Brush.Status.Success`: `#10B981` (Emerald 500)
  - `KTools.Brush.Status.SuccessBg`: `#ECFDF5` (Emerald 50)
  - `KTools.Brush.Status.Warning`: `#F59E0B` (Amber 500)
  - `KTools.Brush.Status.WarningBg`: `#FFFBEB` (Amber 50)
  - `KTools.Brush.Status.Error`: `#EF4444` (Rose 500)
  - `KTools.Brush.Status.ErrorBg`: `#FEF2F2` (Rose 50)
  - `KTools.Brush.Status.Info`: `#3B82F6` (Blue 500)
  - `KTools.Brush.Status.InfoBg`: `#EFF6FF` (Blue 50)

### 2. Spacing (`Thickness` & `GridLength`)
- `KTools.Spacing.XXS`: `4`
- `KTools.Spacing.XS`: `8`
- `KTools.Spacing.S`: `12`
- `KTools.Spacing.M`: `16`
- `KTools.Spacing.L`: `24`

### 3. Corner Radii (`CornerRadius`)
- `KTools.Radius.Small`: `4` (Badges, small tags, sub-items)
- `KTools.Radius.Default`: `6` (Buttons, TextBoxes, ComboBoxes)
- `KTools.Radius.Card`: `8` (Cards, Dialog sections)
- `KTools.Radius.Modal`: `10` (Window root, flyout panels)

### 4. Typography (`FontFamily="Segoe UI"`)
- `KTools.Font.Title`: `15pt`, SemiBold, Primary Text
- `KTools.Font.Section`: `13pt`, SemiBold, Primary Text
- `KTools.Font.Body`: `11pt` (12px), Regular, Primary Text
- `KTools.Font.Secondary`: `10pt` (11px), Regular, Secondary Text
- `KTools.Font.Caption`: `8.5pt` (9px), Medium, Muted Text
- `KTools.Font.Button`: `10.5pt` (11px), SemiBold, Center Aligned

---

## F. Shared Component Specification

To be created in `KhimTools\UI\Controls\`:

1. **`PrimaryButton`**:
   - Background: `Brand.Accent` (`#0284C7`), Hover: `#0369A1`, Pressed: `#075985`.
   - Foreground: `#FFFFFF`, Radius: `6px`, Height: `32px`, Padding: `16px, 0px`.
2. **`SecondaryButton`**:
   - Background: `Surface.Subtle` (`#F1F5F9`), Border: `Border.Default` (`#E2E8F0`), Foreground: `Text.Primary`.
   - Hover: `#E2E8F0`, Radius: `6px`, Height: `32px`.
3. **`TertiaryButton` (Ghost)**:
   - Background: `Transparent`, Border: `None`, Foreground: `Brand.Accent`, Hover: `Surface.Subtle`.
4. **`DestructiveButton`**:
   - Background: `Status.ErrorBg`, Border: `Status.Error`, Foreground: `Status.Error`, Hover: `Status.Error` (Filled).
5. **`IconButton`**:
   - Square `32x32px` or `24x24px`, vector SVG/Path icon, hover highlight, tooltip mandatory.
6. **`SearchBox`**:
   - Height `32px`, integrated magnifying glass icon, watermark placeholder text ("Search tools or commands..."), clear button (`X`).
7. **`Card` / `GroupBox`**:
   - Background: White, Border: `#E2E8F0`, Radius: `8px`, DropShadow: `Blur 6, Opacity 0.04`.
8. **`SectionHeader`**:
   - Title text + optional counter pill badge + optional right-aligned action button.
9. **`StatusBadge`**:
   - Compact pill (`Padding 6,2`, `Radius 4`), supporting: `PASS`, `WARNING`, `ERROR`, `INFO`, `NOT CHECKED`.
10. **`EmptyState`**:
    - Centered placeholder with subtle vector icon, headline, helper message, and action CTA.
11. **`LoadingState`**:
    - Indeterminate progress bar/ring with descriptive operation label.

---

## G. Iconography Modernization Policy

### Strict Ban on Emojis
Revit Ribbon buttons and in-app action cards currently using raw emojis (`🏛`, `🏗`, `🔲`, `🧱`, `🔗`, `🏢`, `🏠`, `🕳`, `🏷`, `✂`, `📐`, `🔢`, `🖨`, `📦`, `🔄`, `📂`, `📥`, `⚡`) **MUST BE COMPLETELY REPLACED** with consistent, high-DPI vector PNG/SVG icons matching Autodesk Fluent design standards (16x16px and 32x32px).

---

## H. Files Allowed to Modify (Whitelist)

```
KhimTools/Core/RibbonBuilder.cs
KhimTools/Core/KhimUiStyle.cs
KhimTools/Tools/KhimGen/Workspace/Views/KhimWorkspacePane.xaml
KhimTools/Tools/KhimGen/Workspace/Views/KhimWorkspacePane.xaml.cs
KhimTools/Tools/KhimGen/Workspace/ViewModels/KhimWorkspaceViewModel.cs
KhimTools/Tools/KhimStructural/QuickStructure/Forms/QuickStructureWindow.xaml
KhimTools/Tools/KhimStructural/QuickStructure/Forms/QuickStructureWindow.xaml.cs
KhimTools/Tools/KhimArchitectural/QuickArchi/Forms/QuickArchiWindow.xaml
KhimTools/Tools/KhimArchitectural/QuickArchi/Forms/QuickArchiWindow.xaml.cs
KhimTools/Tools/KhimGen/FamilyManager/Forms/FamilyManagerWindow.xaml
KhimTools/Tools/KhimGen/FamilyManager/Forms/FamilyManagerWindow.xaml.cs
KhimTools/Tools/KhimStructural/RebarTool/Forms/RebarShapeLoaderWindow.xaml
KhimTools/Tools/KhimStructural/RebarTool/Forms/RebarShapeLoaderWindow.xaml.cs
KhimTools/Tools/KhimGen/OverrideTool/Forms/GraphicOverdriveWindow.xaml
KhimTools/Tools/KhimGen/OverrideTool/Forms/GraphicOverdriveWindow.xaml.cs
KhimTools/Tools/KhimGen/SectionBox/Forms/SectionBoxWindow.xaml
KhimTools/Tools/KhimGen/SectionBox/Forms/SectionBoxWindow.xaml.cs
KhimTools/Tools/KhimGen/CalloutPro/Forms/CalloutProWindow.xaml
KhimTools/Tools/KhimGen/CalloutPro/Forms/CalloutProWindow.xaml.cs
KhimTools/Tools/KhimGen/ViewFromCallout/Forms/ViewFromCalloutWindow.xaml
KhimTools/Tools/KhimGen/ViewFromCallout/Forms/ViewFromCalloutWindow.xaml.cs
KhimTools/App/App.xaml
[NEW] KhimTools/UI/DesignSystem/*.xaml (Shared design tokens and styles)
[NEW] KhimTools/UI/Controls/*.xaml (Shared reusable WPF controls)
[NEW] KhimTools/Resources/*.png (Modernized 16px/32px icon assets)
```

---

## I. Files Explicitly Forbidden to Modify (Blacklist)

```
KhimTools/Tools/KhimStructural/RebarTool/Core/RebarEngineeringService.cs
KhimTools/Tools/KhimStructural/RebarTool/Core/RebarEngineeringCalculations.cs
KhimTools/Tools/KhimStructural/RebarTool/Core/RebarHostValidator.cs
KhimTools/Tools/KhimStructural/RebarTool/Core/ColumnRebarGenerator.cs
KhimTools/Tools/KhimStructural/RebarTool/Core/BeamRebarGenerator.cs
KhimTools/Tools/KhimStructural/RebarTool/Core/RebarValidationRules.cs
KhimTools/Tools/KhimStructural/RebarTool/Models/**
KhimTools/Tools/KhimGen/FamilyManager/Services/FamilyManagerService.cs
KhimTools/Tools/KhimGen/FamilyManager/Services/FamilyLoadEngine.cs
KhimTools/Tools/KhimGen/FamilyManager/Services/FamilyDiscoveryService.cs
KhimTools/Core/Family/FamilyPathResolver.cs
KhimTools/Core/Family/StandardFamilyInventory.cs
KhimTools/Tools/KhimStructural/QuickStructure/Services/GridIntersectionHelper.cs
KhimTools/Tools/KhimStructural/QuickStructure/Services/QuickStructureService.cs
KhimTools/Tools/KhimArchitectural/QuickArchi/Services/QuickArchiService.cs
KhimTools/Core/DeploymentSecurity.cs
KhimTools/Tests/**
Installer/**
```

---

## J. Risk Assessment & Mitigation Matrix

| Risk ID | Description | Impact | Probability | Mitigation Strategy |
| :--- | :--- | :--- | :--- | :--- |
| **R-01** | Ribbon button ID change breaks external macros or audit | High | Low | Lock all 89 button IDs verbatim in `RibbonBuilder.cs`. Verify with `Installer\Verify-RevitRuntimeQA.ps1`. |
| **R-02** | WPF resource dictionary merge fails across AppDomain boundaries | High | Med | Bundle design tokens as compiled component resources using `pack://application:,,,/KhimTools;component/...` and fallback local lookup. |
| **R-03** | Rebar form logic regression when updating UI wrappers | Critical | Low | Strictly touch only presentation code. Run all 44 unit & engineering tests (`RunTests.ps1`) before and after every edit. |
| **R-04** | Windows High-DPI scaling (125%, 150%, 175%) clips labels | Medium | Med | Eliminate hardcoded pixel widths for text containers; use Auto/Star sizing and minimum window dimensions. |
| **R-05** | Revit API thread marshalling deadlock on Workspace async calls | High | Low | Maintain strict usage of `App.EventHandler.Raise(...)` via `ActionEventHandler.cs` for all Revit model interactions. |

---

## K. Validation & Verification Strategy

Each Phase (1 through 6) must pass 4 distinct layers of automated and visual verification before moving to the next Gate:

1. **Build Verification**: `dotnet build KhimTools\KhimTools.csproj -c Release` (Must compile with 0 errors).
2. **Static QA Regression**: `Installer\Verify-RevitRuntimeQA.ps1` (Audit 03 Ribbon commands 85/85 must pass, Audit 04 Icons must pass).
3. **Engineering Regression**: `KhimTools\Tests\RunTests.ps1` (All 44 engineering tests across TCVN, Eurocode 2, Grid Intersections, Family Resolver must be 100% green).
4. **Visual & Interaction Review**: Explicit inspection of spacing, typography, hierarchy, focus states, and zero-emoji compliance.

---

## L. Acceptance Criteria Checklist

- [ ] **Gate 0**: `Docs/UIUX_2.0/UX_CONTRACT.md` approved. Phase 0 status reported.
- [ ] **Gate 1**: Shared design tokens (`Theme.xaml`) and controls created. 0 compilation errors.
- [ ] **Gate 2**: Ribbon 2.0 rebuilt with 7 domain panels. Width reduced by >20%. 85/85 command IDs preserved.
- [ ] **Gate 3**: Khim Workspace 2.0 deployed with Search (`Ctrl+K`), Recents, Module Grouping, compact density.
- [ ] **Gate 4**: Rebar presentation modernised with Create / Detail / Engineering / QA hierarchy. Zero engineering logic touched.
- [ ] **Gate 5**: BIM Library Manager updated with Library → Family → Type distinction, status badges, vector actions.
- [ ] **Gate 6**: Quick Structure & Quick Archi refactored to share identical interaction model and tokens.
- [ ] **Gate 7 / Final QA**: Master QA dashboard passes 76/76 audits. All before/after evidence documented.

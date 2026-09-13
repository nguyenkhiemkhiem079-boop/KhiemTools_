# UI/UX Rebuild: Implementation and QA

Date: 2026-09-12

Status: implemented in the working tree; not installed, not released, and not
signed off for live Revit operation.

## Changes in This Pass

- Removed in-app brand banners from the ten WPF surfaces and the remaining
  WinForms banner call sites. Standard window title bars, field labels, grid
  column headers and operational validation messages remain.
- Unified shared WPF/WinForms colors around blue #1677D2, white inputs and
  neutral #F5F7FA surfaces. Shared styles no longer enlarge fixed-size swatches
  or repeatedly register button hover handlers.
- Generated 96 embedded PNG assets (48 symbols in 16/32-pixel variants) from
  Lucide. Transparent backgrounds replace solid icon tiles. Family Library
  has its own library icon. Generation validates names before writing and
  does not delete unrelated images. Dependency versions are pinned in Tools.
- Ribbon: Workspace only toggles the dockable. Family Library and Slab Step
  are in K-GEN, drawing operations in Layout, export in Publish, and
  visibility in Graphics. Secondary structure/MEP/architecture commands are
  stacked or grouped; Rebar utilities are in a menu.
- Overdrive: sixteen 34x34 square swatches with direct color binding, accessible
  names and tooltips; no visible color labels. Advanced settings start
  collapsed. Older palettes preserve valid custom slots and fill to sixteen.
  Removed visual-tree color painting and obsolete ribbon text-hiding helpers.
- Workspace: dedicated icon/name/group columns, narrow-width search/filter
  rows, Family Library command and an empty search state.
- Family/Shape Library: minimum column widths prevent collapsed columns,
  horizontal scrolling retains access to paths, and Family names have
  ellipsis/tooltips. Fixed Shape button padding.
- Quick Structure/Archi: unframed field groups, consistent input heights,
  explicit mm offsets, shorter labels and no overlapping footing checkbox.
- Print/Export: auto-height wrapping toolbars, responsive setup text fields,
  layout-managed footer/progress/actions, and disposal of the secondary
  settings form. Existing export-pipeline changes were preserved.
- Added an internal document-free Print layout fixture. It does not call
  LoadDataFromRevit and disables export. The public constructor still rejects
  a null Document.

## Verification Results

| Check | Result | What It Does Not Prove |
| --- | --- | --- |
| net48 Release, Revit 2023 references | 0 errors, 0 warnings | In-Revit execution |
| net8.0-windows Release | 0 errors, 0 warnings | Revit 2025/2026 runtime |
| Compiled command contracts | 78 entry points, 17 workspace bindings | Successful Execute calls |
| WPF source layout harness | 10 surfaces, 43 renders | Real window/DPI/model interaction |
| Embedded icons | 96 loaded; size/alpha checks passed | Installed bundle uses this DLL |
| Palette | 16 exact-color 34px buttons | Overrides applied to model elements |
| Print layout fixture | 57 bounds checks, 3 window sizes | Print quality, output files, retention |
| Existing regression suite | 44 passed | Full structural-design compliance |
| Source/packaging audit | 9/9, including nested 14/14 MSI source audits | MSI lifecycle or live Revit QA |
| git diff --check | Passed | Functional correctness |

WPF renders use 96/144/192 DPI rasterization and dockable widths 240/300/420.
Library windows also render at 720 pixels. This is not a Windows DPI-setting
change or a WinForms DPI test. Print fixture sizes are 1120x700, 1360x820,
and 1600x900. Empty fields/selectors in fixtures are not populated from Revit.

## Evidence

Generated locally under artifacts/ui-qa (ignored by Git):

- [Icon catalog](../../artifacts/ui-qa/ribbon-icons.png)
- [Compact Overdrive](../../artifacts/ui-qa/GraphicOverdriveWindow-248-1.png)
- [Expanded Overdrive](../../artifacts/ui-qa/GraphicOverdrive-expanded.png)
- [Narrow Workspace](../../artifacts/ui-qa/KhimWorkspacePane-240-1.png)
- [Family Library fixture](../../artifacts/ui-qa/FamilyManagerWindow-720-1.png)
- [Print layout fixture](../../artifacts/ui-qa/PrintExport-1120x700.png)
- [WPF render inventory](../../artifacts/ui-qa/layout-results.json)

## Reproduce

Run from the repository root on Windows with Revit 2023 API assemblies installed:

```powershell
dotnet build KhimTools/KhimTools.csproj -c Release -f net48 -p:RevitVersionForReference=2023
dotnet build KhimTools/KhimTools.csproj -c Release -f net8.0-windows
powershell -NoProfile -STA -File Tools/Verify-UiLayout.ps1
powershell -NoProfile -STA -File Tools/Verify-PrintLayout.ps1
powershell -NoProfile -File Tools/Verify-UiContracts.ps1
powershell -NoProfile -File Installer/Verify-RevitRuntimeQA.ps1
npm install --prefix Tools
npm --prefix Tools run generate:icons -- --preview
```

## Runtime Gate Still Open

The Windows computer-use runtime failed to initialize with:
"failed to write kernel assets: The system cannot find the path specified.
(os error 3)". Reset/retry returned the same failure.

No installed Revit bundle or MSI was replaced in this pass. No new add-in
manifest was created. No user model was opened, modified or closed.

Before release:

1. Install a candidate through the existing MSI-managed deployment path;
   verify there is exactly one applicable K-TOOLS application registration.
2. Verify ribbon rendering and Workspace open/close in Revit 2023, including
   narrow docking, document switching, keyboard focus and long names.
3. Execute Family load/reload, palette apply/reset and every moved command
   against disposable fixtures; test cancellation and no-document states.
4. QA populated Print filters, saved/date-based sets, combined/separate PDF
   and DWG, prior-output retention, failure/retry, and actual output quality.
5. Exercise remaining WinForms/Rebar dialogs at Windows 100/150/200% scaling.
   This pass does not certify rebar geometry or Eurocode engineering.
6. Build and verify the candidate MSI; do not present source/metadata checks
   as installation or runtime certification.

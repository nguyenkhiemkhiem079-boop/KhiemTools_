# K-TOOLS RIBBON 2.0 — COMMAND AUDIT & MAPPING MATRIX

**Document ID:** DOC-UIUX-2.0-RIBBON-MAP  
**Status:** APPROVED FOR PHASE 2 IMPLEMENTATION  
**Target File:** `KhimTools/Core/RibbonBuilder.cs`  
**Architecture:** 6 Primary Panels (`WORKSPACE`, `GENERAL`, `STRUCTURE`, `REBAR`, `ARCHI`, `MEP`)

---

## 1. Executive Summary & Verification Guarantees

This document audits and maps **100% of all 88 Revit commands** exposed by K-TOOLS.
Every single command ID, target class, display name, and runtime behavior is strictly preserved.

### Verification Principles:
1. **Zero Command Deletions:** Every command from previous revisions is maintained.
2. **Zero Command Renaming:** Exact command IDs (`name` parameter in `PushButtonData`) remain identical.
3. **Zero Behavioral Changes:** The backing C# command classes and namespaces are untouched.
4. **Information Architecture 2.0:** Eliminates horizontal ribbon sprawl by grouping commands into 6 logical domain panels with clear hierarchy (Large Buttons, SplitButtons, Pulldowns, and Stacked Controls).

---

## 2. Ribbon Information Architecture Overview

```
Tab: "K-TOOLS"
├── 1. WORKSPACE
│   ├── Large Button: Khim Workspace (CmdToggleWorkspace)
│   ├── Large Button: Family Manager (CmdFamilyManager)
│   └── Stacked: Settings (Language Switcher) / Check Update
│
├── 2. GENERAL
│   ├── Stacked Pulldowns: Model Tools (Join, Link, Slab Step, Grid) & View Tools (Section Box, Callout Pro, View Callout)
│   ├── Stacked Pulldowns: Visibility (Show / Hide) & Layout (Sheet Gen, Align Viewport, Detail No, Text Align)
│   ├── Pulldown / Stack: Graphic Override (Halftone, Reset, Overdrive Settings, 3x3 Swatches)
│   ├── Large Button: Sheet Exporter (CmdSheetExport)
│   └── Large Button: Element Tags (CmdElementTags)
│
├── 3. STRUCTURE
│   ├── Large Button: Quick Structure (CmdQuickStructure)
│   ├── Large Button: Section Cut (CmdSectionCut)
│   └── Large Button: Cover Setup (CmdProjectCoverSetup)
│
├── 4. REBAR
│   ├── [CREATE] SplitButton: Column Rebar (Auto-detect, Rectangular 2.0, Round 2.0)
│   ├── [CREATE] Large Button: Beam Rebar (CmdBeamRebar)
│   ├── [CREATE] Large Button: Slab Rebar (CmdSlabRebar)
│   ├── [CREATE] Large Button: Foundation Rebar (CmdFoundationRebar)
│   ├── [DETAIL] Stacked Pulldown: Rebar Detailing & Drawing (Column Drawing, Update Drawing)
│   └── Large Button: Rebar Shapes (CmdLoadRebarShapesMain)
│
├── 5. ARCHI
│   ├── Large Button: Quick Archi (CmdQuickArchi)
│   ├── Large Button: Room 3D View (CmdRoom3DView)
│   └── Large Button: Room Finishes (CmdWallFloorFinishes)
│
└── 6. MEP
    ├── Large Button: MEP Openings (CmdMepOpenings)
    └── Large Button: Elevation Tags (CmdMepElevationTags)
```

---

## 3. Comprehensive Command Mapping Table (All 88 Commands)

| # | Command ID | Display Name | Target Command Class | Legacy Panel | New Panel 2.0 | Group / Control Type | Status |
|:---|:---|:---|:---|:---|:---|:---|:---:|
| 1 | `CmdToggleWorkspace` | Khim Workspace | `KhimTools.Workspace.Commands.CmdToggleWorkspace` | K-GEN | **WORKSPACE** | Primary Large Button | PRESERVED |
| 2 | `CmdFamilyManager` | Family Manager | `KhimTools.FamilyManager.Commands.CmdFamilyManager` | Quick | **WORKSPACE** | Primary Large Button | PRESERVED |
| 3 | `CmdSwitchLanguage` | Đổi Ngôn Ngữ (Switch) | `KhimTools.LanguageSwitcher.Commands.CmdSwitchLanguage` | K-GEN | **WORKSPACE** | Pulldown: Settings | PRESERVED |
| 4 | `CmdSetVietnamese` | Tiếng Việt (VN) | `KhimTools.LanguageSwitcher.Commands.CmdSetVietnamese` | K-GEN | **WORKSPACE** | Pulldown: Settings | PRESERVED |
| 5 | `CmdSetEnglish` | English (EN) | `KhimTools.LanguageSwitcher.Commands.CmdSetEnglish` | K-GEN | **WORKSPACE** | Pulldown: Settings | PRESERVED |
| 6 | `CmdCheckUpdate` | Check Update | `KhimTools.Updater.Commands.CmdCheckUpdate` | K-GEN | **WORKSPACE** | Stacked PushButton | PRESERVED |
| 7 | `CmdJoinElements` | Join Elements | `KhimTools.SlabJoin.Commands.CmdJoinElements` | K-GEN | **GENERAL** | Pulldown: Model Tools | PRESERVED |
| 8 | `CmdCopyLinkElements` | Copy Link Elements | `KhimTools.CopyLink.Commands.CmdCopyLinkElements` | K-GEN | **GENERAL** | Pulldown: Model Tools | PRESERVED |
| 9 | `CmdSlabStep` | Slab Step Generator | `KhimTools.SlabStep.Commands.CmdSlabStep` | K-GEN | **GENERAL** | Pulldown: Model Tools | PRESERVED |
| 10 | `CmdGridPlanGenerator` | Grid & Floor Plan | `KhimTools.GridLevel.Commands.CmdAutoGridPlan` | K-GEN | **GENERAL** | Pulldown: Model Tools | PRESERVED |
| 11 | `CmdSectionBox` | Section Box Pro | `KhimTools.SectionBox.Commands.CmdSectionBox` | K-GEN | **GENERAL** | Pulldown: View Tools | PRESERVED |
| 12 | `CmdCalloutPro` | Callout Pro | `KhimTools.CalloutPro.Commands.CmdCalloutPro` | K-GEN | **GENERAL** | Pulldown: View Tools | PRESERVED |
| 13 | `CmdViewFromCallout` | Create View from Callout | `KhimTools.ViewFromCallout.Commands.CmdViewFromCallout` | K-GEN | **GENERAL** | Pulldown: View Tools | PRESERVED |
| 14 | `CmdShowWindow` | Hiển thị Window | `KhimTools.VisibilityTool.Commands.CmdShowWindow` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 15 | `CmdHideWindow` | Ẩn Window | `KhimTools.VisibilityTool.Commands.CmdHideWindow` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 16 | `CmdShowDoor` | Hiển thị Door | `KhimTools.VisibilityTool.Commands.CmdShowDoor` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 17 | `CmdHideDoor` | Ẩn Door | `KhimTools.VisibilityTool.Commands.CmdHideDoor` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 18 | `CmdShowCeiling` | Hiển thị Ceiling | `KhimTools.VisibilityTool.Commands.CmdShowCeiling` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 19 | `CmdHideCeiling` | Ẩn Ceiling | `KhimTools.VisibilityTool.Commands.CmdHideCeiling` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 20 | `CmdShowRoof` | Hiển thị Roof | `KhimTools.VisibilityTool.Commands.CmdShowRoof` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 21 | `CmdHideRoof` | Ẩn Roof | `KhimTools.VisibilityTool.Commands.CmdHideRoof` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 22 | `CmdShowStair` | Hiển thị Stair | `KhimTools.VisibilityTool.Commands.CmdShowStair` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 23 | `CmdHideStair` | Ẩn Stair | `KhimTools.VisibilityTool.Commands.CmdHideStair` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 24 | `CmdShowRailing` | Hiển thị Railing | `KhimTools.VisibilityTool.Commands.CmdShowRailing` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 25 | `CmdHideRailing` | Ẩn Railing | `KhimTools.VisibilityTool.Commands.CmdHideRailing` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 26 | `CmdShowColumn` | Hiển thị Column | `KhimTools.VisibilityTool.Commands.CmdShowColumn` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 27 | `CmdHideColumn` | Ẩn Column | `KhimTools.VisibilityTool.Commands.CmdHideColumn` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 28 | `CmdShowFraming` | Hiển thị Framing | `KhimTools.VisibilityTool.Commands.CmdShowFraming` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 29 | `CmdHideFraming` | Ẩn Framing | `KhimTools.VisibilityTool.Commands.CmdHideFraming` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 30 | `CmdShowFloor` | Hiển thị Floor | `KhimTools.VisibilityTool.Commands.CmdShowFloor` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 31 | `CmdHideFloor` | Ẩn Floor | `KhimTools.VisibilityTool.Commands.CmdHideFloor` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 32 | `CmdShowWall` | Hiển thị Wall | `KhimTools.VisibilityTool.Commands.CmdShowWall` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 33 | `CmdHideWall` | Ẩn Wall | `KhimTools.VisibilityTool.Commands.CmdHideWall` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 34 | `CmdShowFoundation` | Hiển thị Foundation | `KhimTools.VisibilityTool.Commands.CmdShowFoundation` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 35 | `CmdHideFoundation` | Ẩn Foundation | `KhimTools.VisibilityTool.Commands.CmdHideFoundation` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 36 | `CmdShowRebar` | Hiển thị Rebar | `KhimTools.VisibilityTool.Commands.CmdShowRebar` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 37 | `CmdHideRebar` | Ẩn Rebar | `KhimTools.VisibilityTool.Commands.CmdHideRebar` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 38 | `CmdShowGrid` | Hiển thị Grid | `KhimTools.VisibilityTool.Commands.CmdShowGrid` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 39 | `CmdHideGrid` | Ẩn Grid | `KhimTools.VisibilityTool.Commands.CmdHideGrid` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 40 | `CmdShowLevel` | Hiển thị Level | `KhimTools.VisibilityTool.Commands.CmdShowLevel` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 41 | `CmdHideLevel` | Ẩn Level | `KhimTools.VisibilityTool.Commands.CmdHideLevel` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 42 | `CmdShowSection` | Hiển thị Section | `KhimTools.VisibilityTool.Commands.CmdShowSection` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 43 | `CmdHideSection` | Ẩn Section | `KhimTools.VisibilityTool.Commands.CmdHideSection` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 44 | `CmdShowElevation` | Hiển thị Elevation | `KhimTools.VisibilityTool.Commands.CmdShowElevation` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 45 | `CmdHideElevation` | Ẩn Elevation | `KhimTools.VisibilityTool.Commands.CmdHideElevation` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 46 | `CmdShowTag` | Hiển thị Tag | `KhimTools.VisibilityTool.Commands.CmdShowTag` | K-GEN | **GENERAL** | Pulldown: Visibility (Show) | PRESERVED |
| 47 | `CmdHideTag` | Ẩn Tag | `KhimTools.VisibilityTool.Commands.CmdHideTag` | K-GEN | **GENERAL** | Pulldown: Visibility (Hide) | PRESERVED |
| 48 | `CmdSheetGen` | Create Sheets (CSV) | `KhimTools.SheetGen.Commands.CmdSheetGen` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 49 | `CmdAlignViewport` | Align Viewports | `KhimTools.ViewportAlign.Commands.CmdAlignViewport` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 50 | `CmdUpdateDetailNumbers` | Update Detail No | `KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 51 | `CmdAlignTop` | Align Text - Top | `KhimTools.TextAlign.Commands.CmdAlignTop` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 52 | `CmdAlignBottom` | Align Text - Bottom | `KhimTools.TextAlign.Commands.CmdAlignBottom` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 53 | `CmdAlignLeft` | Align Text - Left | `KhimTools.TextAlign.Commands.CmdAlignLeft` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 54 | `CmdAlignRight` | Align Text - Right | `KhimTools.TextAlign.Commands.CmdAlignRight` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 55 | `CmdAlignMiddle` | Align Text - Middle | `KhimTools.TextAlign.Commands.CmdAlignMiddle` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 56 | `CmdAlignHorizontalEquals` | Align Text - Horiz Equal | `KhimTools.TextAlign.Commands.CmdAlignHorizontalEquals` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 57 | `CmdAlignVerticalEquals` | Align Text - Vert Equal | `KhimTools.TextAlign.Commands.CmdAlignVerticalEquals` | K-GEN | **GENERAL** | Pulldown: Layout | PRESERVED |
| 58 | `CmdSheetExport` | Sheet Exporter | `KhimTools.SheetExport.Commands.CmdSheetExport` | K-GEN | **GENERAL** | Large Button | PRESERVED |
| 59 | `CmdElementTags` | Elements Tags | `KhimTools.ElementTags.Commands.CmdElementTags` | K-GEN | **GENERAL** | Large Button | PRESERVED |
| 60 | `CmdOverrideRed` | Đỏ (Red Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideRed` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 61 | `CmdOverrideOrange` | Cam (Orange Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideOrange` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 62 | `CmdOverrideYellow` | Vàng (Yellow Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideYellow` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 63 | `CmdOverrideGreen` | Lá (Green Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideGreen` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 64 | `CmdOverrideCyan` | Cyan (Cyan Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideCyan` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 65 | `CmdOverrideBlue` | Lam (Blue Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideBlue` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 66 | `CmdOverrideMagenta` | Hồng (Magenta Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideMagenta` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 67 | `CmdOverrideGray` | Xám (Gray Swatch) | `KhimTools.OverrideTool.Commands.CmdOverrideGray` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 68 | `CmdOverrideCustom` | Chọn (Custom Color) | `KhimTools.OverrideTool.Commands.CmdOverrideCustom` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 69 | `CmdQuickHalftone` | On/Off Halftone | `KhimTools.OverrideTool.Commands.CmdQuickHalftone` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 70 | `CmdQuickResetOverride` | Reset Override | `KhimTools.OverrideTool.Commands.CmdQuickResetOverride` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 71 | `CmdGraphicOverdrive` | Override Settings | `KhimTools.OverrideTool.Commands.CmdGraphicOverdrive` | Override | **GENERAL** | Pulldown: Graphic Override | PRESERVED |
| 72 | `CmdQuickStructure` | Quick Structure | `KhimTools.Structural.QuickStructure.Commands.CmdQuickStructure` | K-STRUCTURAL | **STRUCTURE** | Large Button | PRESERVED |
| 73 | `CmdSectionCut` | Section Cut | `KhimTools.SectionCutTool.Commands.CmdSectionCut` | K-STRUCTURAL | **STRUCTURE** | Large Button | PRESERVED |
| 74 | `CmdProjectCoverSetup` | Cover Setup | `KhimTools.RebarTool.Commands.CmdProjectCoverSetup` | K-STRUCTURAL | **STRUCTURE** | Large Button | PRESERVED |
| 75 | `CmdColumnRebar` | Column Rebar (Auto) | `KhimTools.RebarTool.Commands.CmdColumnRebar` | K-STRUCTURAL | **REBAR** | [CREATE] SplitButton: Column | PRESERVED |
| 76 | `CmdMultiColumnRebar` | Cột Vuông / Chữ Nhật 2.0 | `KhimTools.RebarTool.Commands.CmdMultiColumnRebar` | K-STRUCTURAL | **REBAR** | [CREATE] SplitButton: Column | PRESERVED |
| 77 | `CmdMultiRoundColumnRebar` | Cột Tròn 2.0 | `KhimTools.RebarTool.Commands.CmdMultiRoundColumnRebar` | K-STRUCTURAL | **REBAR** | [CREATE] SplitButton: Column | PRESERVED |
| 78 | `CmdBeamRebar` | Beam Rebar | `KhimTools.RebarTool.Commands.CmdBeamRebar` | K-STRUCTURAL | **REBAR** | [CREATE] Large Button | PRESERVED |
| 79 | `CmdSlabRebar` | Slab Rebar | `KhimTools.RebarTool.Commands.CmdSlabRebar` | K-STRUCTURAL | **REBAR** | [CREATE] Large Button | PRESERVED |
| 80 | `CmdFoundationRebar` | Foundation Rebar | `KhimTools.RebarTool.Commands.CmdFoundationRebar` | K-STRUCTURAL | **REBAR** | [CREATE] Large Button | PRESERVED |
| 81 | `CmdColumnDrawing` | Column Drawing | `KhimTools.RebarTool.Commands.CmdColumnDrawing` | K-STRUCTURAL | **REBAR** | [DETAIL] Pulldown: Detailing | PRESERVED |
| 82 | `CmdUpdateColumnDrawing` | Update Drawing | `KhimTools.RebarTool.Commands.CmdUpdateColumnDrawing` | K-STRUCTURAL | **REBAR** | [DETAIL] Pulldown: Detailing | PRESERVED |
| 83 | `CmdLoadRebarShapesMain` | Rebar Shapes | `KhimTools.RebarTool.Commands.CmdLoadRebarShapes` | K-STRUCTURAL | **REBAR** | Large Button | PRESERVED |
| 84 | `CmdQuickArchi` | Quick Archi | `KhimTools.Architectural.QuickArchi.Commands.CmdQuickArchi` | K-ARCHITECTURAL | **ARCHI** | Large Button | PRESERVED |
| 85 | `CmdRoom3DView` | Room 3D View | `KhimTools.Architectural.Rooms.CmdRoom3DView` | K-ARCHITECTURAL | **ARCHI** | Large Button | PRESERVED |
| 86 | `CmdWallFloorFinishes` | Room Finishes | `KhimTools.Architectural.Finishes.CmdWallFloorFinishes` | K-ARCHITECTURAL | **ARCHI** | Large Button | PRESERVED |
| 87 | `CmdMepOpenings` | MEP Openings | `KhimTools.MEP.Penetrations.CmdMepOpenings` | K-MEP | **MEP** | Large Button | PRESERVED |
| 88 | `CmdMepElevationTags` | Elevation Tags | `KhimTools.MEP.Tags.CmdMepElevationTags` | K-MEP | **MEP** | Large Button | PRESERVED |

---

## 4. Quantitative Audit & Command Preservation Verification

* **Total Commands Before Ribbon 2.0:** 88
* **Total Commands After Ribbon 2.0:** 88
* **Delta:** 0 (Zero lost, Zero deleted, Zero added)
* **Command ID Integrity:** 100% Identical
* **Target Class Path Integrity:** 100% Identical
* **Engineering Logic Modified:** 0 files

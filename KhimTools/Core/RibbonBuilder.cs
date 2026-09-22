using System;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>Ribbon navigation grouped by modeling, drawing, graphics and publishing workflows.</summary>
    public static class RibbonBuilder
    {
        public const string TabName = "K-TOOLS";
        public const string WorkspacePanelName = "Workspace";
        public const string GenPanelName = "K-GEN";
        public const string LayoutPanelName = "Layout";
        public const string PublishPanelName = "Publish";
        public const string OverridePanelName = "Graphics";
        public const string StructuralPanelName = "Structure";
        public const string ArchPanelName = "Architecture";
        public const string MepPanelName = "MEP";
        public const string QuantitySurveyingPanelName = "K-QS";

        public static void BuildRibbon(UIControlledApplication application)
        {
            CreateTabSafely(application, TabName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // Workspace is navigation only: toggle the right-hand dockable pane.
            try { BuildWorkspacePanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] Workspace error: " + ex); }

            // K-GEN contains model, view and documentation operations.
            try { BuildGenPanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] K-GEN error: " + ex); }

            try { BuildLayoutPanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] Layout error: " + ex); }
            try { BuildPublishPanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] Publish error: " + ex); }

            // One palette entry point; category visibility stays in Graphics.
            try { BuildOverridePanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] Override error: " + ex); }

            // 3. Panel: K-STRUCTURAL
            try { BuildStructuralPanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] K-STRUCTURAL error: " + ex); }

            // 4. Panel: K-ARCHITECTURAL
            try { BuildArchPanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] K-ARCHITECTURAL error: " + ex); }

            // 5. Panel: K-MEP
            try { BuildMepPanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] K-MEP error: " + ex); }

            // 6. Panel: K-QS (Quantity Surveying remains inside the K-TOOLS tab)
            try { BuildQuantitySurveyingPanel(application, assemblyPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[K-TOOLS] K-QS error: " + ex); }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 1. PANEL: WORKSPACE (DOCKABLE PANE TOGGLE ONLY)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildWorkspacePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, WorkspacePanelName);
            var wsData = new PushButtonData(
                "CmdToggleWorkspace",
                "Workspace",
                assemblyPath,
                "KhimTools.Workspace.Commands.CmdToggleWorkspace")
            {
                ToolTip = "Bật hoặc tắt thanh công cụ Workspace bên phải màn hình.",
                LargeImage = LoadImage("icon_workspace_32.png"),
                Image = LoadImage("icon_workspace_16.png")
            };
            panel.AddItem(wsData);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 2. PANEL: K-GEN (MODEL, VIEW AND DOCUMENT OPERATIONS)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildGenPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, GenPanelName);

            // Copy Link Elements
            var copyLinkData = new PushButtonData(
                "CmdCopyLinkElements",
                "Copy Link Elements",
                assemblyPath,
                "KhimTools.CopyLink.Commands.CmdCopyLinkElements")
            {
                ToolTip = "Sao chép đối tượng từ file Revit Link sang dự án chính chuẩn 100% tọa độ.",
                LargeImage = LoadImage("icon_copylink_32.png"),
                Image = LoadImage("icon_copylink_16.png")
            };
            // Model actions share one compact stack with Join and Slab Step.

            // Family content belongs with model resources, not drawing layout.
            var familyLibraryData = new PushButtonData(
                "CmdFamilyManager",
                "Family" + Environment.NewLine + "Library",
                assemblyPath,
                "KhimTools.FamilyManager.Commands.CmdFamilyManager")
            {
                ToolTip = "Tìm, xem trước và nạp Family vào project hiện tại.",
                LongDescription = "Quản lý thư viện Family dùng cho model, bao gồm Rebar Shape và nội dung tiêu chuẩn của dự án.",
                LargeImage = LoadImage("icon_family_32.png"),
                Image = LoadImage("icon_family_16.png")
            };
            panel.AddItem(familyLibraryData);

            // ── CỤM 2: MODEL & GEOMETRY ──
            // 3. Join Elements (Large Button)
            var joinElementsData = new PushButtonData(
                "CmdJoinElements",
                "Join Elements",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.CmdJoinElements")
            {
                ToolTip = "Mở công cụ Join/Unjoin/Switch chuyên nghiệp cho tất cả loại cấu kiện.",
                LongDescription = "Hỗ trợ join/unjoin/switch geometry giữa bất kỳ cặp Category: Floors, Walls, Columns, Beams, Foundations...",
                LargeImage = LoadImage("icon_join_32.png"),
                Image = LoadImage("icon_join_16.png")
            };
            var slabStepData = new PushButtonData("CmdSlabStep", "Slab Step", assemblyPath,
                "KhimTools.SlabStep.Commands.CmdSlabStep")
            {
                Image = LoadImage("icon_slabstep_16.png"),
                ToolTip = "Tạo chênh cao sàn."
            };
            panel.AddStackedItems(copyLinkData, joinElementsData, slabStepData);

            // 4. Auto Grid & Floor Plan Generator (Large Button)
            var gridPlanData = new PushButtonData(
                "CmdGridPlanGenerator",
                "Grid &" + Environment.NewLine + "Floor Plan",
                assemblyPath,
                "KhimTools.GridLevel.Commands.CmdAutoGridPlan")
            {
                ToolTip = "Tự động sinh Hệ Lưới Trục (Grid) và Mặt Bằng / Cao Độ Tầng (Level & Floor Plan) từ CAD/DWG.",
                LargeImage = LoadImage("icon_grid_plan_32.png"),
                Image = LoadImage("icon_grid_plan_16.png")
            };
            panel.AddItem(gridPlanData);

            // Stack 3: Language & Check Update
            var splitLangData = new PulldownButtonData(
                "LanguagePulldown",
                "Ngôn ngữ (Lang)")
            {
                ToolTip = "Chuyển đổi ngôn ngữ giao diện (Song ngữ Tiếng Việt - English).",
                Image = LoadImage("icon_language_16.png")
            };

            var updateData = new PushButtonData(
                "CmdCheckUpdate",
                "Check Update",
                assemblyPath,
                "KhimTools.Updater.Commands.CmdCheckUpdate")
            {
                ToolTip = "Kiểm tra phiên bản mới nhất của KhimTools từ GitHub Releases.",
                Image = LoadImage("icon_update_16.png")
            };

            var stackedSystem = panel.AddStackedItems(splitLangData, updateData);
            if (stackedSystem.Count == 2)
            {
                var pLang = stackedSystem[0] as PulldownButton;
                if (pLang != null)
                {
                    AddPulldownItem(pLang, "CmdSwitchLanguage", "Đổi Ngôn Ngữ (Switch)", "KhimTools.LanguageSwitcher.Commands.CmdSwitchLanguage", assemblyPath, "icon_language_16.png");
                    pLang.AddSeparator();
                    AddPulldownItem(pLang, "CmdSetVietnamese", "Tiếng Việt (VN)", "KhimTools.LanguageSwitcher.Commands.CmdSetVietnamese", assemblyPath, "icon_language_16.png");
                    AddPulldownItem(pLang, "CmdSetEnglish", "English (EN)", "KhimTools.LanguageSwitcher.Commands.CmdSetEnglish", assemblyPath, "icon_language_16.png");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 2. PANEL: OVERRIDE (ONE ENTRY POINT, 4x4 PALETTE IN MODELESS WINDOW)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildLayoutPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, LayoutPanelName);
            // ── CỤM 3: LAYOUT (Large Pulldown Button) ──
            var layoutPulldownData = new PulldownButtonData("KhimLayoutPulldown", "Layout")
            {
                ToolTip = "Các công cụ dàn trang, quản lý bản vẽ, căn chỉnh và tạo Sheet.",
                LargeImage = LoadImage("icon_align_32.png"),
                Image = LoadImage("icon_align_16.png")
            };
            var layoutPulldown = panel.AddItem(layoutPulldownData) as PulldownButton;
            if (layoutPulldown != null)
            {
                AddPulldownItem(layoutPulldown, "CmdSheetGen", "Create Sheets (CSV)", "KhimTools.SheetGen.Commands.CmdSheetGen", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdSheetCopy", "Sheet Copy", "KhimTools.SheetCopy.Commands.CmdSheetCopy", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdSplitSchedule", "Split Schedule", "KhimTools.ScheduleSplit.Commands.CmdSplitSchedule", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdTitleBlockSync", "Title Block Sync", "KhimTools.TitleBlockSync.Commands.CmdTitleBlockSync", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdFilterManager", "Filter Manager", "KhimTools.FilterManager.Commands.CmdFilterManager", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdParameterManager", "Parameter Manager", "KhimTools.ParameterManager.Commands.CmdParameterManager", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdModifyObjects", "Modify Objects", "KhimTools.ModifyObjects.Commands.CmdModifyObjects", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdDimensionTools", "Dimension Tools", "KhimTools.DimensionTools.Commands.CmdDimensionTools", assemblyPath, "export_sheet_16.png");
                AddPulldownItem(layoutPulldown, "CmdAlignViewport", "Align Viewports", "KhimTools.ViewportAlign.Commands.CmdAlignViewport", assemblyPath, "icon_align_16.png");
                AddPulldownItem(layoutPulldown, "CmdUpdateDetailNumbers", "Update Detail No", "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers", assemblyPath, "icon_detail_16.png");
                
                layoutPulldown.AddSeparator();
                AddPulldownItem(layoutPulldown, "CmdAlignTop", "Align Text - Top", "KhimTools.TextAlign.Commands.CmdAlignTop", assemblyPath, "icon_align_16.png");
                AddPulldownItem(layoutPulldown, "CmdAlignBottom", "Align Text - Bottom", "KhimTools.TextAlign.Commands.CmdAlignBottom", assemblyPath, "icon_align_16.png");
                AddPulldownItem(layoutPulldown, "CmdAlignLeft", "Align Text - Left", "KhimTools.TextAlign.Commands.CmdAlignLeft", assemblyPath, "icon_align_16.png");
                AddPulldownItem(layoutPulldown, "CmdAlignRight", "Align Text - Right", "KhimTools.TextAlign.Commands.CmdAlignRight", assemblyPath, "icon_align_16.png");
                AddPulldownItem(layoutPulldown, "CmdAlignMiddle", "Align Text - Middle", "KhimTools.TextAlign.Commands.CmdAlignMiddle", assemblyPath, "icon_align_16.png");
                AddPulldownItem(layoutPulldown, "CmdAlignHorizontalEquals", "Align Text - Horiz Equal", "KhimTools.TextAlign.Commands.CmdAlignHorizontalEquals", assemblyPath, "icon_align_16.png");
                AddPulldownItem(layoutPulldown, "CmdAlignVerticalEquals", "Align Text - Vert Equal", "KhimTools.TextAlign.Commands.CmdAlignVerticalEquals", assemblyPath, "icon_align_16.png");
            }

            // ── CỤM 4: VIEW TOOLS (Large Pulldown Button) ──
            var viewToolsPulldownData = new PulldownButtonData("KhimViewToolsPulldown", "View Tools")
            {
                ToolTip = "Các công cụ nâng cao hỗ trợ tạo Section Box, Callout Pro và sinh View liên quan.",
                LargeImage = LoadImage("icon_sectionbox_32.png"),
                Image = LoadImage("icon_sectionbox_16.png")
            };
            var viewToolsPulldown = panel.AddItem(viewToolsPulldownData) as PulldownButton;
            if (viewToolsPulldown != null)
            {
                AddPulldownItem(viewToolsPulldown, "CmdSectionBox", "Section Box Pro", "KhimTools.SectionBox.Commands.CmdSectionBox", assemblyPath, "icon_sectionbox_16.png");
                AddPulldownItem(viewToolsPulldown, "CmdCalloutPro", "Callout Pro", "KhimTools.CalloutPro.Commands.CmdCalloutPro", assemblyPath, "icon_callout_pro_16.png");
                AddPulldownItem(viewToolsPulldown, "CmdViewFromCallout", "Create View from Callout", "KhimTools.ViewFromCallout.Commands.CmdViewFromCallout", assemblyPath, "icon_view_callout_16.png");
            }

            // Elements Tags (Large Button)
            var elementTagsData = new PushButtonData(
                "CmdElementTags",
                "Elements" + Environment.NewLine + "Tags",
                assemblyPath,
                "KhimTools.ElementTags.Commands.CmdElementTags")
            {
                ToolTip = "Quản lý và gán thẻ Tag hàng loạt cho các đối tượng trong View hiện hành.",
                LargeImage = LoadImage("icon_mep_tags_32.png"),
                Image = LoadImage("icon_mep_tags_16.png")
            };
            panel.AddItem(elementTagsData);
            panel.AddItem(new PushButtonData("CmdDrawingCheck", "Drawing" + Environment.NewLine + "Check",
                assemblyPath, "KhimTools.DrawingCheck.CmdDrawingCheck")
            {
                ToolTip = "Kiểm tra sàn/tường thiếu tag và tag mất host trong view hoặc sheet.",
                LargeImage = LoadImage("icon_mep_tags_32.png"),
                Image = LoadImage("icon_mep_tags_16.png")
            });
            panel.AddItem(new PushButtonData("CmdCheckSlabStep", "Check" + Environment.NewLine + "Step",
                assemblyPath, "KhimTools.SlabStep.Commands.CmdCheckSlabStep")
            {
                ToolTip = "Đối chiếu giá trị chiều cao và hướng Step với hai sàn liên kết. Step cũ kiểm tra trong Slab Step.",
                LargeImage = LoadImage("icon_mep_tags_32.png"),
                Image = LoadImage("icon_slabstep_16.png")
            });

        }

        private static void BuildPublishPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, PublishPanelName);
            // ── CỤM 5: PUBLISH & SYSTEM ──
            // Sheet Exporter (Large Button)
            var sheetExportData = new PushButtonData(
                "CmdSheetExport",
                "Sheet" + Environment.NewLine + "Exporter",
                assemblyPath,
                "KhimTools.SheetExport.Commands.CmdSheetExport")
            {
                ToolTip = "Công cụ Batch Print & Export Sheet/View chuyên nghiệp (PDF, DWG, Issue Manager).",
                LongDescription = "Hỗ trợ Naming Templates với Regex validation, Issue Revision Diffing, " +
                    "Tự động tạo file Excel Transmittal Register và QA Technical Log, " +
                    "PDFsharp Bookmarks, Watermark Status Stamp, Cover Sheet, và Auto-Retry.",
                LargeImage = LoadImage("export_sheet_32.png"),
                Image = LoadImage("export_sheet_16.png")
            };
            panel.AddItem(sheetExportData);

        }

        private static void BuildOverridePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, OverridePanelName);
            var data = new PushButtonData(
                "CmdGraphicOverdrive",
                "Graphic" + Environment.NewLine + "Overdrive",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdGraphicOverdrive")
            {
                ToolTip = "Mở bảng 16 màu override nhanh cho đối tượng đang chọn.",
                LongDescription = "Palette 4 x 4, tùy chọn Surface/Cut/Line, transparency, line weight, halftone và reset override.",
                LargeImage = LoadImage("override_palette_32.png"),
                Image = LoadImage("override_palette_16.png")
            };
            panel.AddItem(data);
            // ── CỤM 3: VIEW & DETAIL (GOM STACK/PULLDOWN) ──
            // Stack 1: BỘ ĐÔI HIỂN THỊ & ẨN CATEGORY
            var pulldownShowData = new PulldownButtonData("VisibilityShowPulldown", "Hiển thị")
            {
                ToolTip = "Bật hiển thị các Category đối tượng trong View hiện hành.",
                Image = LoadImage("icon_detail_16.png")
            };

            var pulldownHideData = new PulldownButtonData("VisibilityHidePulldown", "Ẩn")
            {
                ToolTip = "Ẩn các Category đối tượng trong View hiện hành.",
                Image = LoadImage("icon_detail_16.png")
            };

            var stackedVis = panel.AddStackedItems(pulldownShowData, pulldownHideData);
            if (stackedVis.Count == 2)
            {
                var pShow = stackedVis[0] as PulldownButton;
                var pHide = stackedVis[1] as PulldownButton;

                if (pShow != null)
                {
                    AddPulldownItem(pShow, "CmdShowWindow", "Hiển thị Window", "KhimTools.VisibilityTool.Commands.CmdShowWindow", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pShow, "CmdShowDoor", "Hiển thị Door", "KhimTools.VisibilityTool.Commands.CmdShowDoor", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pShow, "CmdShowCeiling", "Hiển thị Ceiling", "KhimTools.VisibilityTool.Commands.CmdShowCeiling", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pShow, "CmdShowRoof", "Hiển thị Roof", "KhimTools.VisibilityTool.Commands.CmdShowRoof", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pShow, "CmdShowStair", "Hiển thị Stair", "KhimTools.VisibilityTool.Commands.CmdShowStair", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pShow, "CmdShowRailing", "Hiển thị Railing", "KhimTools.VisibilityTool.Commands.CmdShowRailing", assemblyPath, "icon_detail_16.png");
                    pShow.AddSeparator();
                    AddPulldownItem(pShow, "CmdShowColumn", "Hiển thị Column", "KhimTools.VisibilityTool.Commands.CmdShowColumn", assemblyPath, "rebar_col_16.png");
                    AddPulldownItem(pShow, "CmdShowFraming", "Hiển thị Framing", "KhimTools.VisibilityTool.Commands.CmdShowFraming", assemblyPath, "rebar_beam_16.png");
                    AddPulldownItem(pShow, "CmdShowFloor", "Hiển thị Floor", "KhimTools.VisibilityTool.Commands.CmdShowFloor", assemblyPath, "rebar_slab_16.png");
                    AddPulldownItem(pShow, "CmdShowWall", "Hiển thị Wall", "KhimTools.VisibilityTool.Commands.CmdShowWall", assemblyPath, "icon_join_16.png");
                    AddPulldownItem(pShow, "CmdShowFoundation", "Hiển thị Foundation", "KhimTools.VisibilityTool.Commands.CmdShowFoundation", assemblyPath, "rebar_fdn_16.png");
                    AddPulldownItem(pShow, "CmdShowRebar", "Hiển thị Rebar", "KhimTools.VisibilityTool.Commands.CmdShowRebar", assemblyPath, "rebar_draw_16.png");
                    pShow.AddSeparator();
                    AddPulldownItem(pShow, "CmdShowGrid", "Hiển thị Grid", "KhimTools.VisibilityTool.Commands.CmdShowGrid", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pShow, "CmdShowLevel", "Hiển thị Level", "KhimTools.VisibilityTool.Commands.CmdShowLevel", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pShow, "CmdShowSection", "Hiển thị Section", "KhimTools.VisibilityTool.Commands.CmdShowSection", assemblyPath, "rebar_draw_16.png");
                    AddPulldownItem(pShow, "CmdShowElevation", "Hiển thị Elevation", "KhimTools.VisibilityTool.Commands.CmdShowElevation", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pShow, "CmdShowTag", "Hiển thị Tag", "KhimTools.VisibilityTool.Commands.CmdShowTag", assemblyPath, "icon_detail_16.png");
                }

                if (pHide != null)
                {
                    AddPulldownItem(pHide, "CmdHideWindow", "Ẩn Window", "KhimTools.VisibilityTool.Commands.CmdHideWindow", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pHide, "CmdHideDoor", "Ẩn Door", "KhimTools.VisibilityTool.Commands.CmdHideDoor", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pHide, "CmdHideCeiling", "Ẩn Ceiling", "KhimTools.VisibilityTool.Commands.CmdHideCeiling", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pHide, "CmdHideRoof", "Ẩn Roof", "KhimTools.VisibilityTool.Commands.CmdHideRoof", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pHide, "CmdHideStair", "Ẩn Stair", "KhimTools.VisibilityTool.Commands.CmdHideStair", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pHide, "CmdHideRailing", "Ẩn Railing", "KhimTools.VisibilityTool.Commands.CmdHideRailing", assemblyPath, "icon_detail_16.png");
                    pHide.AddSeparator();
                    AddPulldownItem(pHide, "CmdHideColumn", "Ẩn Column", "KhimTools.VisibilityTool.Commands.CmdHideColumn", assemblyPath, "rebar_col_16.png");
                    AddPulldownItem(pHide, "CmdHideFraming", "Ẩn Framing", "KhimTools.VisibilityTool.Commands.CmdHideFraming", assemblyPath, "rebar_beam_16.png");
                    AddPulldownItem(pHide, "CmdHideFloor", "Ẩn Floor", "KhimTools.VisibilityTool.Commands.CmdHideFloor", assemblyPath, "rebar_slab_16.png");
                    AddPulldownItem(pHide, "CmdHideWall", "Ẩn Wall", "KhimTools.VisibilityTool.Commands.CmdHideWall", assemblyPath, "icon_join_16.png");
                    AddPulldownItem(pHide, "CmdHideFoundation", "Ẩn Foundation", "KhimTools.VisibilityTool.Commands.CmdHideFoundation", assemblyPath, "rebar_fdn_16.png");
                    AddPulldownItem(pHide, "CmdHideRebar", "Ẩn Rebar", "KhimTools.VisibilityTool.Commands.CmdHideRebar", assemblyPath, "rebar_draw_16.png");
                    pHide.AddSeparator();
                    AddPulldownItem(pHide, "CmdHideGrid", "Ẩn Grid", "KhimTools.VisibilityTool.Commands.CmdHideGrid", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pHide, "CmdHideLevel", "Ẩn Level", "KhimTools.VisibilityTool.Commands.CmdHideLevel", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pHide, "CmdHideSection", "Ẩn Section", "KhimTools.VisibilityTool.Commands.CmdHideSection", assemblyPath, "rebar_draw_16.png");
                    AddPulldownItem(pHide, "CmdHideElevation", "Ẩn Elevation", "KhimTools.VisibilityTool.Commands.CmdHideElevation", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pHide, "CmdHideTag", "Ẩn Tag", "KhimTools.VisibilityTool.Commands.CmdHideTag", assemblyPath, "icon_detail_16.png");
                }
            }

        }

        private static void BuildStructuralPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, StructuralPanelName);

            // 0. Quick Structure (Large Button)
            var quickStructData = new PushButtonData(
                "CmdQuickStructure",
                "Quick" + Environment.NewLine + "Structure",
                assemblyPath,
                "KhimTools.Structural.QuickStructure.Commands.CmdQuickStructure")
            {
                ToolTip = "Tự động sinh hệ Cột, Dầm và Móng hàng loạt theo lưới trục (Grids).",
                LargeImage = LoadImage("icon_grid_plan_32.png"),
                Image = LoadImage("icon_grid_plan_16.png")
            };
            panel.AddItem(quickStructData);

            // 1. SplitButton: Column Rebar
            var splitButtonData = new SplitButtonData(
                "ColumnRebarSplitButton",
                "Column" + Environment.NewLine + "Rebar")
            {
                ToolTip = "Bố trí thép cột tự động (phát hiện vuông/tròn từ phần tử đang chọn)."
            };

            var splitButton = panel.AddItem(splitButtonData) as SplitButton;
            if (splitButton != null)
            {
                AddPushButton(splitButton, "CmdColumnRebar", "Column" + Environment.NewLine + "Rebar",
                    "KhimTools.RebarTool.Commands.CmdColumnRebar", assemblyPath,
                    "Tự động phát hiện loại cột (vuông/tròn) và mở giao diện phù hợp.",
                    "rebar_col_32.png", "rebar_col_16.png");

                AddPushButton(splitButton, "CmdMultiColumnRebar", "Cột Vuông / Chữ Nhật 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột vuông/chữ nhật.",
                    "rebar_col_32.png", "rebar_col_rect_16.png");

                AddPushButton(splitButton, "CmdMultiRoundColumnRebar", "Cột Tròn 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiRoundColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột tròn.",
                    "rebar_col_circ_32.png", "rebar_col_circ_16.png");

                splitButton.AddSeparator();

                AddPushButton(splitButton, "CmdColumnDrawing", "Column Drawing",
                    "KhimTools.RebarTool.Commands.CmdColumnDrawing", assemblyPath,
                    "Tự động xuất bản vẽ mặt cắt 2D & thống kê thép cột.",
                    "rebar_col_32.png", "rebar_draw_16.png");

                AddPushButton(splitButton, "CmdUpdateColumnDrawing", "Update Drawing",
                    "KhimTools.RebarTool.Commands.CmdUpdateColumnDrawing", assemblyPath,
                    "Đồng bộ cập nhật lại bản vẽ 2D đã xuất theo mô hình thép mới nhất.",
                    "rebar_col_32.png", "rebar_draw_16.png");

                splitButton.AddSeparator();

                AddPushButton(splitButton, "CmdLoadRebarShapes", "Thư Viện Rebar Shapes (43)",
                    "KhimTools.RebarTool.Commands.CmdLoadRebarShapes", assemblyPath,
                    "Quản lý & nạp toàn bộ 43 Rebar Shape tiêu chuẩn BS 8666 / JIS vào dự án.",
                    "column_rebar_32.png", "column_rebar_16.png");
            }

            // 2. Beam Rebar
            var beamData = new PushButtonData(
                "CmdBeamRebar",
                "Beam Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdBeamRebar")
            {
                ToolTip = "Bố trí thép dầm (Beam Rebar v2.0) chuẩn kết cấu TCVN & Eurocode.",
                LongDescription = "Hỗ trợ thép chủ chạy suốt (top/bottom), thép gia cường gối L/3, " +
                    "thép gia cường bụng L/6, thép sườn (skin bars), đai phân vùng A1/A2/A1 và đai treo dầm phụ.",
                LargeImage = LoadImage("rebar_beam_32.png"),
                Image = LoadImage("rebar_beam_16.png")
            };


            // 3. Slab Rebar
            var slabData = new PushButtonData(
                "CmdSlabRebar",
                "Slab Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdSlabRebar")
            {
                ToolTip = "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                LargeImage = LoadImage("rebar_slab_32.png"),
                Image = LoadImage("rebar_slab_16.png")
            };


            // 4. Foundation Rebar
            var fdnData = new PushButtonData(
                "CmdFoundationRebar",
                "Foundation Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdFoundationRebar")
            {
                ToolTip = "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                LargeImage = LoadImage("rebar_fdn_32.png"),
                Image = LoadImage("rebar_fdn_16.png")
            };
            panel.AddStackedItems(beamData, slabData, fdnData);

            // 5. Section Cut
            var sectionData = new PushButtonData(
                "CmdSectionCut",
                "Section" + Environment.NewLine + "Cut",
                assemblyPath,
                "KhimTools.SectionCutTool.Commands.CmdSectionCut")
            {
                ToolTip = "Tự động tạo mặt cắt dọc & ngang (Section Views) phục vụ bản vẽ thép.",
                LargeImage = LoadImage("icon_section_cut_32.png"),
                Image = LoadImage("icon_section_cut_16.png")
            };


            // 6. Cover Setup
            var coverData = new PushButtonData(
                "CmdProjectCoverSetup",
                "Cover" + Environment.NewLine + "Setup",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdProjectCoverSetup")
            {
                ToolTip = "Cấu hình Lớp bê tông bảo vệ (Concrete Cover) toàn dự án.",
                LargeImage = LoadImage("icon_cover_setup_32.png"),
                Image = LoadImage("icon_cover_setup_16.png")
            };


            // 7. Rebar Shapes (Large Button)
            var shapesData = new PushButtonData(
                "CmdLoadRebarShapesMain",
                "Rebar" + Environment.NewLine + "Shapes",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdLoadRebarShapes")
            {
                ToolTip = "Quản lý & nạp toàn bộ 43 Rebar Shape tiêu chuẩn BS 8666 / JIS vào dự án.",
                LargeImage = LoadImage("column_rebar_32.png"),
                Image = LoadImage("column_rebar_16.png")
            };


            // 8. Non-destructive Revit fixture: creates temporary host/rebar, validates, then rolls back.
            var qaData = new PushButtonData(
                "CmdRebarFixtureQa",
                "Rebar QA" + Environment.NewLine + "Fixture",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdRebarFixtureQa")
            {
                ToolTip = "Chạy fixture Rebar trực tiếp trong Revit và rollback toàn bộ dữ liệu thử.",
                LongDescription = "Kiểm tra tạo Rebar, host, đường kính, shape và containment bằng Revit API thực. " +
                    "Mọi phần tử QA được rollback sau khi ghi báo cáo.",
                LargeImage = LoadImage("rebar_qa_32.png"),
                Image = LoadImage("rebar_qa_16.png")
            };
            var rebarTools = panel.AddItem(new PulldownButtonData("RebarToolsPulldown", "Rebar Tools")
            {
                LargeImage = LoadImage("rebar_draw_32.png"),
                Image = LoadImage("rebar_draw_16.png"),
                ToolTip = "Mặt cắt, lớp bảo vệ, thư viện shape và kiểm tra Rebar."
            }) as PulldownButton;
            if (rebarTools != null)
            {
                rebarTools.AddPushButton(sectionData);
                rebarTools.AddPushButton(coverData);
                rebarTools.AddPushButton(shapesData);
                rebarTools.AddSeparator();
                rebarTools.AddPushButton(qaData);
                rebarTools.AddSeparator();
                AddPulldownItem(rebarTools, "CmdRuntimeQa", "Runtime QA",
                    "KhimTools.RuntimeQa.Commands.CmdRuntimeQa", assemblyPath,
                    "rebar_qa_16.png");
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 4. PANEL: K-ARCHITECTURAL
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildArchPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ArchPanelName);

            // 0. Quick Archi (Large Button)
            var quickArchiData = new PushButtonData(
                "CmdQuickArchi",
                "Quick" + Environment.NewLine + "Archi",
                assemblyPath,
                "KhimTools.Architectural.QuickArchi.Commands.CmdQuickArchi")
            {
                ToolTip = "Tự động sinh Tường Kiến Trúc & Khởi tạo Phòng (Rooms) từ đường nét Model/CAD.",
                LargeImage = LoadImage("icon_finishes_32.png"),
                Image = LoadImage("icon_finishes_16.png")
            };
            panel.AddItem(quickArchiData);

            // 1. Room 3D View
            var room3dData = new PushButtonData(
                "CmdRoom3DView",
                "Room 3D View",
                assemblyPath,
                "KhimTools.Architectural.Rooms.CmdRoom3DView")
            {
                ToolTip = "Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.",
                LargeImage = LoadImage("icon_room3d_32.png"),
                Image = LoadImage("icon_room3d_16.png")
            };


            // 2. Room Finishes
            var finishData = new PushButtonData(
                "CmdWallFloorFinishes",
                "Room Finishes",
                assemblyPath,
                "KhimTools.Architectural.Finishes.CmdWallFloorFinishes")
            {
                ToolTip = "Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng.",
                LargeImage = LoadImage("icon_finishes_32.png"),
                Image = LoadImage("icon_finishes_16.png")
            };
            panel.AddStackedItems(room3dData, finishData);

            var createTools = panel.AddItem(new PulldownButtonData("ArchCreatePulldown", "Tạo kiến trúc")
            {
                ToolTip = "Các công cụ tạo cấu kiện và lớp hoàn thiện kiến trúc.",
                LargeImage = LoadImage("icon_finishes_32.png"),
                Image = LoadImage("icon_finishes_16.png")
            }) as PulldownButton;
            if (createTools != null)
            {
                AddPulldownItem(createTools, "CmdCreateLintels", "Tạo lanh tô",
                    "KhimTools.Architectural.Lintels.CmdCreateLintels", assemblyPath, "icon_detail_16.png");
                AddPulldownItem(createTools, "CmdRoomFinishesMenu", "Hoàn thiện theo Room",
                    "KhimTools.Architectural.Finishes.CmdWallFloorFinishes", assemblyPath, "icon_finishes_16.png");
            }

            var doorTools = panel.AddItem(new PulldownButtonData("ArchDoorDetailsPulldown", "Triển khai" + Environment.NewLine + "cửa")
            {
                ToolTip = "Tạo view triển khai cửa theo Assembly hoặc Legend.",
                LargeImage = LoadImage("icon_detail_32.png"),
                Image = LoadImage("icon_detail_16.png")
            }) as PulldownButton;
            if (doorTools != null)
            {
                AddPulldownItem(doorTools, "CmdCreateDoorAssemblies", "Assembly",
                    "KhimTools.Architectural.DoorDetails.CmdCreateDoorAssemblies", assemblyPath, "icon_detail_16.png");
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 5. PANEL: K-MEP
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildMepPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, MepPanelName);

            // 1. MEP Openings
            var openingData = new PushButtonData(
                "CmdMepOpenings",
                "MEP Openings",
                assemblyPath,
                "KhimTools.MEP.Penetrations.CmdMepOpenings")
            {
                ToolTip = "Tự động kiểm tra xung đột ống MEP với Dầm/Sàn/Vách và đục lỗ mở (Openings).",
                LargeImage = LoadImage("icon_mep_openings_32.png"),
                Image = LoadImage("icon_mep_openings_16.png")
            };


            // 2. MEP Elevation Tags
            var tagData = new PushButtonData(
                "CmdMepElevationTags",
                "Elevation Tags",
                assemblyPath,
                "KhimTools.MEP.Tags.CmdMepElevationTags")
            {
                ToolTip = "Tự động gán nhãn cao độ đáy (BOP/Invert Elevation) cho ống gió và ống nước.",
                LargeImage = LoadImage("icon_mep_tags_32.png"),
                Image = LoadImage("icon_mep_tags_16.png")
            };
            panel.AddStackedItems(openingData, tagData);
        }

        private static void BuildQuantitySurveyingPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, QuantitySurveyingPanelName);
            panel.AddItem(new PushButtonData("CmdQuantityTakeoff", "Quantity" + Environment.NewLine + "Takeoff",
                assemblyPath, "KhimTools.QuantityTakeoff.Commands.CmdQuantityTakeoff")
            {
                ToolTip = "Bóc khối lượng BIM: bê tông, thép, tường xây, sơn, hoàn thiện, cửa, phòng và MEP.",
                LongDescription = "Kiểm tra dữ liệu, giữ liên kết tới phần tử Revit, phân biệt Raw Quantity và Pay Quantity, xuất Excel có audit Element UniqueId.",
                LargeImage = LoadImage("icon_quantity_32.png"),
                Image = LoadImage("icon_quantity_16.png")
            });
            panel.AddItem(new PushButtonData("CmdQtoDataCheck", "Data" + Environment.NewLine + "Check",
                assemblyPath, "KhimTools.QuantityTakeoff.Commands.CmdQtoDataCheck")
            {
                ToolTip = "Kiểm tra dữ liệu đầu vào trước khi phát hành khối lượng.",
                LargeImage = LoadImage("icon_data_check_32.png"),
                Image = LoadImage("icon_data_check_16.png")
            });
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ════════════════════════════════════════════════════════════════════════════════
        private static void CreateTabSafely(UIControlledApplication app, string tabName)
        {
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch { }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tabName, string panelName)
        {
            var panels = app.GetRibbonPanels(tabName);
            var existing = panels.FirstOrDefault(p => p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            try
            {
                return app.CreateRibbonPanel(tabName, panelName);
            }
            catch
            {
                return app.GetRibbonPanels(tabName).FirstOrDefault(p => p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static void AddPulldownItem(PulldownButton pulldown, string name, string text,
            string className, string assemblyPath, string smallIconName)
        {
            if (string.IsNullOrWhiteSpace(text)) text = name;
            try
            {
                var data = new PushButtonData(name, text, assemblyPath, className)
                {
                    ToolTip = "Bật/Tắt hiển thị hoặc căn chỉnh đối tượng trong Active View.",
                    Image = LoadImage(smallIconName)
                };
                pulldown.AddPushButton(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[K-TOOLS] Lỗi thêm pulldown item '" + name + "': " + ex.Message);
            }
        }

        private static void AddPushButton(SplitButton splitButton, string name, string text,
            string className, string assemblyPath, string toolTip, string largeIconName, string smallIconName)
        {
            if (string.IsNullOrWhiteSpace(text)) text = name;
            try
            {
                var data = new PushButtonData(name, text, assemblyPath, className)
                {
                    ToolTip = toolTip,
                    LargeImage = LoadImage(largeIconName),
                    Image = LoadImage(smallIconName)
                };
                splitButton.AddPushButton(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[K-TOOLS] Lỗi thêm split button item '" + name + "': " + ex.Message);
            }
        }

        private static BitmapImage LoadImage(string resourceOrFileName)
        {
            try { return UI.UiIconExtension.Load(resourceOrFileName); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[K-TOOLS] Icon " + resourceOrFileName + ": " + ex.Message);
                return null;
            }
        }
    }
}

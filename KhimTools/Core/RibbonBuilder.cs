using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Xây dựng Ribbon UI chuyên nghiệp trên Tab: "K-TOOLS"
    /// Được phân chia thành 5 cụm Panel chuyên môn:
    ///   1. K-GEN           (Workspace, Join, Overdrive, Ẩn/Hiện, Căn chỉnh Text, Align Viewport, Grid, Link, Detail No, Update, Exporter, Language)
    ///   2. Override        (Palette màu 3x3 + Halftone + Reset + Setting Color)
    ///   3. K-STRUCTURAL    (Column Rebar, Beam Rebar, Slab Rebar, Foundation Rebar, Section Cut, Cover Setup)
    ///   4. K-ARCHITECTURAL (Room 3D View, Room Finishes)
    ///   5. K-MEP           (MEP Openings, Elevation Tags)
    ///
    /// NGUYÊN TẮC THIẾT KẾ KIẾN TRÚC BẢO VỆ CÁCH LY LỖI:
    ///   - ONE MODULE FAIL ≠ WHOLE RIBBON FAIL.
    ///   - Mỗi Panel được khởi tạo trong một sandbox độc lập (RegisterPanelModule).
    ///   - Mỗi Nút bấm/Stacked Item/Pulldown Item được validate và nạp an toàn (SafeAddItem, SanitizeButtonText).
    ///   - KHÔNG CATCH RỖNG: Mọi ngoại lệ đều được ghi log đầy đủ Module, Panel, Tool, Command, Exception, StackTrace.
    ///   - Khắc phục triệt để lỗi ArgumentException trên nhãn nút trống của Revit API.
    /// </summary>
    public static class RibbonBuilder
    {
        public const string TabName = "K-TOOLS";
        public const string GenPanelName = "K-GEN";
        public const string OverridePanelName = "Override";
        public const string QuickPanelName = "Quick";
        public const string StructuralPanelName = "K-STRUCTURAL";
        public const string ArchPanelName = "K-ARCHITECTURAL";
        public const string MepPanelName = "K-MEP";

        /// <summary>
        /// Ký tự Zero-Width Space (Unicode U+200B, [char]0x200B).
        /// Revit API kiểm tra string rỗng bằng .Trim(). Ký tự này không bị trim,
        /// thỏa mãn quy tắc non-empty của Revit API đồng thời có độ rộng 0 pixel
        /// giúp các nút swatch trong cụm bảng màu 3x3 giữ nguyên dạng ô vuông icon-only.
        /// </summary>
        public const string ZeroWidthSpace = "\u200B";

        public static void BuildRibbon(UIControlledApplication application)
        {
            if (application == null)
            {
                RegistrationDiagnostics.RecordError("RibbonRoot", "RibbonRoot", "Application", string.Empty, "UIControlledApplication is null");
                return;
            }

            RegistrationDiagnostics.Reset();
            CreateTabSafely(application, TabName);
            string assemblyPath = GetSafeAssemblyPath();

            // 1. Panel: K-GEN (General Tools & Productivity)
            RegisterPanelModule(GenPanelName, () => BuildGenPanel(application, assemblyPath));

            // 2. Panel: Override (Palette màu 3x3 + Halftone + Reset + Setting Color)
            RegisterPanelModule(OverridePanelName, () => BuildOverridePanel(application, assemblyPath));

            // 3. Panel: Quick (Quick Structure + Family Manager)
            RegisterPanelModule(QuickPanelName, () => BuildQuickPanel(application, assemblyPath));

            // 4. Panel: K-STRUCTURAL (Rebar Engineering & Documentation)
            RegisterPanelModule(StructuralPanelName, () => BuildStructuralPanel(application, assemblyPath));

            // 5. Panel: K-ARCHITECTURAL
            RegisterPanelModule(ArchPanelName, () => BuildArchPanel(application, assemblyPath));

            // 6. Panel: K-MEP
            RegisterPanelModule(MepPanelName, () => BuildMepPanel(application, assemblyPath));

            // Ghi nhật ký diagnostics hoàn chỉnh ra AppData
            RegistrationDiagnostics.PersistLog();
        }

        /// <summary>
        /// Bộ bọc cách ly lỗi cấp độ Panel Module (Module Failure Isolation).
        /// Đảm bảo nếu một module ném exception thì chỉ module đó bị ảnh hưởng,
        /// toàn bộ các module còn lại vẫn tiếp tục được nạp bình thường.
        /// </summary>
        private static void RegisterPanelModule(string moduleName, Action buildAction)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                buildAction();
                sw.Stop();
                var record = RegistrationDiagnostics.GetOrCreate(moduleName);
                RegistrationDiagnostics.RecordSuccess(moduleName, record.RegisteredCount, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                RegistrationDiagnostics.RecordError(
                    moduleName,
                    moduleName,
                    "PanelModule",
                    string.Empty,
                    $"Ngoại lệ nghiêm trọng khi khởi tạo Panel [{moduleName}]: {ex.Message}",
                    ex);
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 1. PANEL: K-GEN (GOM GỌN TỐI ƯU KHÔNG GIAN)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildGenPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, GenPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(GenPanelName, GenPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // ── CỤM 1: WORKSPACE ──
            // 1. Khim Workspace (Large Button)
            var wsData = CreateSafePushButtonData(
                "CmdToggleWorkspace",
                "Khim" + Environment.NewLine + "Workspace",
                assemblyPath,
                "KhimTools.Workspace.Commands.CmdToggleWorkspace",
                GenPanelName,
                "Khim Workspace",
                "Bật/Tắt bảng điều khiển Khim Workspace (Dockable Pane).",
                "icon_workspace_32.png",
                "icon_workspace_16.png");
            SafeAddItem(panel, wsData, GenPanelName, "Khim Workspace", "KhimTools.Workspace.Commands.CmdToggleWorkspace");

            // ── CỤM 2: MODEL TOOLS & VIEW TOOLS (STACKED) ──
            var modelToolsData = new PulldownButtonData("KhimModelToolsPulldown", "Model Tools")
            {
                ToolTip = "Các công cụ dựng hình, liên kết và quản lý hình học cấu kiện.",
                Image = LoadImage("icon_join_16.png")
            };

            var viewToolsData = new PulldownButtonData("KhimViewToolsPulldown", "View Tools")
            {
                ToolTip = "Các công cụ tạo Section Box, Callout Pro và sinh View liên quan.",
                Image = LoadImage("icon_sectionbox_16.png")
            };

            var stackedTools = SafeAddStackedItems(panel, modelToolsData, viewToolsData, GenPanelName, "Model & View Tools Stack");
            if (stackedTools != null && stackedTools.Count == 2)
            {
                var pModel = stackedTools[0] as PulldownButton;
                var pView = stackedTools[1] as PulldownButton;

                if (pModel != null)
                {
                    SafeAddPulldownItem(pModel, "CmdJoinElements", "Join Elements", "KhimTools.SlabJoin.Commands.CmdJoinElements", assemblyPath, "icon_join_16.png", GenPanelName);
                    SafeAddPulldownItem(pModel, "CmdCopyLinkElements", "Copy Link Elements", "KhimTools.CopyLink.Commands.CmdCopyLinkElements", assemblyPath, "icon_copylink_16.png", GenPanelName);
                    SafeAddPulldownItem(pModel, "CmdSlabStep", "Slab Step Generator", "KhimTools.SlabStep.Commands.CmdSlabStep", assemblyPath, "icon_join_16.png", GenPanelName);
                    SafeAddPulldownItem(pModel, "CmdGridPlanGenerator", "Grid & Floor Plan", "KhimTools.GridLevel.Commands.CmdAutoGridPlan", assemblyPath, "icon_grid_plan_16.png", GenPanelName);
                }

                if (pView != null)
                {
                    SafeAddPulldownItem(pView, "CmdSectionBox", "Section Box Pro", "KhimTools.SectionBox.Commands.CmdSectionBox", assemblyPath, "icon_sectionbox_16.png", GenPanelName);
                    SafeAddPulldownItem(pView, "CmdCalloutPro", "Callout Pro", "KhimTools.CalloutPro.Commands.CmdCalloutPro", assemblyPath, "icon_callout_pro_16.png", GenPanelName);
                    SafeAddPulldownItem(pView, "CmdViewFromCallout", "Create View from Callout", "KhimTools.ViewFromCallout.Commands.CmdViewFromCallout", assemblyPath, "icon_view_callout_16.png", GenPanelName);
                }
            }

            // ── CỤM 3: VISIBILITY & LAYOUT (STACKED) ──
            var visPulldownData = new PulldownButtonData("KhimVisibilityPulldown", "Visibility")
            {
                ToolTip = "Bật/Tắt hiển thị nhanh các Category đối tượng trong View hiện hành.",
                Image = LoadImage("icon_detail_16.png")
            };

            var layoutPulldownData = new PulldownButtonData("KhimLayoutPulldown", "Layout")
            {
                ToolTip = "Các công cụ dàn trang, quản lý bản vẽ, căn chỉnh và tạo Sheet.",
                Image = LoadImage("icon_align_16.png")
            };

            var stackedVisLayout = SafeAddStackedItems(panel, visPulldownData, layoutPulldownData, GenPanelName, "Visibility & Layout Stack");
            if (stackedVisLayout != null && stackedVisLayout.Count == 2)
            {
                var pVis = stackedVisLayout[0] as PulldownButton;
                var pLayout = stackedVisLayout[1] as PulldownButton;

                if (pVis != null)
                {
                    // Architectural
                    SafeAddPulldownItem(pVis, "CmdShowWindow", "Hiển thị Window", "KhimTools.VisibilityTool.Commands.CmdShowWindow", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideWindow", "Ẩn Window", "KhimTools.VisibilityTool.Commands.CmdHideWindow", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowDoor", "Hiển thị Door", "KhimTools.VisibilityTool.Commands.CmdShowDoor", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideDoor", "Ẩn Door", "KhimTools.VisibilityTool.Commands.CmdHideDoor", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowCeiling", "Hiển thị Ceiling", "KhimTools.VisibilityTool.Commands.CmdShowCeiling", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideCeiling", "Ẩn Ceiling", "KhimTools.VisibilityTool.Commands.CmdHideCeiling", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowRoof", "Hiển thị Roof", "KhimTools.VisibilityTool.Commands.CmdShowRoof", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideRoof", "Ẩn Roof", "KhimTools.VisibilityTool.Commands.CmdHideRoof", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowStair", "Hiển thị Stair", "KhimTools.VisibilityTool.Commands.CmdShowStair", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideStair", "Ẩn Stair", "KhimTools.VisibilityTool.Commands.CmdHideStair", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowRailing", "Hiển thị Railing", "KhimTools.VisibilityTool.Commands.CmdShowRailing", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideRailing", "Ẩn Railing", "KhimTools.VisibilityTool.Commands.CmdHideRailing", assemblyPath, "icon_detail_16.png", GenPanelName);

                    SafeAddSeparator(pVis, GenPanelName);

                    // Structural
                    SafeAddPulldownItem(pVis, "CmdShowColumn", "Hiển thị Column", "KhimTools.VisibilityTool.Commands.CmdShowColumn", assemblyPath, "rebar_col_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideColumn", "Ẩn Column", "KhimTools.VisibilityTool.Commands.CmdHideColumn", assemblyPath, "rebar_col_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowFraming", "Hiển thị Framing", "KhimTools.VisibilityTool.Commands.CmdShowFraming", assemblyPath, "rebar_beam_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideFraming", "Ẩn Framing", "KhimTools.VisibilityTool.Commands.CmdHideFraming", assemblyPath, "rebar_beam_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowFloor", "Hiển thị Floor", "KhimTools.VisibilityTool.Commands.CmdShowFloor", assemblyPath, "rebar_slab_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideFloor", "Ẩn Floor", "KhimTools.VisibilityTool.Commands.CmdHideFloor", assemblyPath, "rebar_slab_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowWall", "Hiển thị Wall", "KhimTools.VisibilityTool.Commands.CmdShowWall", assemblyPath, "icon_join_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideWall", "Ẩn Wall", "KhimTools.VisibilityTool.Commands.CmdHideWall", assemblyPath, "icon_join_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowFoundation", "Hiển thị Foundation", "KhimTools.VisibilityTool.Commands.CmdShowFoundation", assemblyPath, "rebar_fdn_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideFoundation", "Ẩn Foundation", "KhimTools.VisibilityTool.Commands.CmdHideFoundation", assemblyPath, "rebar_fdn_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowRebar", "Hiển thị Rebar", "KhimTools.VisibilityTool.Commands.CmdShowRebar", assemblyPath, "rebar_draw_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideRebar", "Ẩn Rebar", "KhimTools.VisibilityTool.Commands.CmdHideRebar", assemblyPath, "rebar_draw_16.png", GenPanelName);

                    SafeAddSeparator(pVis, GenPanelName);

                    // Documentation
                    SafeAddPulldownItem(pVis, "CmdShowGrid", "Hiển thị Grid", "KhimTools.VisibilityTool.Commands.CmdShowGrid", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideGrid", "Ẩn Grid", "KhimTools.VisibilityTool.Commands.CmdHideGrid", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowLevel", "Hiển thị Level", "KhimTools.VisibilityTool.Commands.CmdShowLevel", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideLevel", "Ẩn Level", "KhimTools.VisibilityTool.Commands.CmdHideLevel", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowSection", "Hiển thị Section", "KhimTools.VisibilityTool.Commands.CmdShowSection", assemblyPath, "rebar_draw_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideSection", "Ẩn Section", "KhimTools.VisibilityTool.Commands.CmdHideSection", assemblyPath, "rebar_draw_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowElevation", "Hiển thị Elevation", "KhimTools.VisibilityTool.Commands.CmdShowElevation", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideElevation", "Ẩn Elevation", "KhimTools.VisibilityTool.Commands.CmdHideElevation", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowTag", "Hiển thị Tag", "KhimTools.VisibilityTool.Commands.CmdShowTag", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideTag", "Ẩn Tag", "KhimTools.VisibilityTool.Commands.CmdHideTag", assemblyPath, "icon_detail_16.png", GenPanelName);
                }

                if (pLayout != null)
                {
                    SafeAddPulldownItem(pLayout, "CmdSheetGen", "Create Sheets (CSV)", "KhimTools.SheetGen.Commands.CmdSheetGen", assemblyPath, "export_sheet_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignViewport", "Align Viewports", "KhimTools.ViewportAlign.Commands.CmdAlignViewport", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdUpdateDetailNumbers", "Update Detail No", "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddSeparator(pLayout, GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignTop", "Align Text - Top", "KhimTools.TextAlign.Commands.CmdAlignTop", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignBottom", "Align Text - Bottom", "KhimTools.TextAlign.Commands.CmdAlignBottom", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignLeft", "Align Text - Left", "KhimTools.TextAlign.Commands.CmdAlignLeft", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignRight", "Align Text - Right", "KhimTools.TextAlign.Commands.CmdAlignRight", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignMiddle", "Align Text - Middle", "KhimTools.TextAlign.Commands.CmdAlignMiddle", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignHorizontalEquals", "Align Text - Horiz Equal", "KhimTools.TextAlign.Commands.CmdAlignHorizontalEquals", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignVerticalEquals", "Align Text - Vert Equal", "KhimTools.TextAlign.Commands.CmdAlignVerticalEquals", assemblyPath, "icon_align_16.png", GenPanelName);
                }
            }

            // ── CỤM 4: PUBLISH & SYSTEM ──
            // Sheet Exporter (Large Button)
            var sheetExportData = CreateSafePushButtonData(
                "CmdSheetExport",
                "Sheet" + Environment.NewLine + "Exporter",
                assemblyPath,
                "KhimTools.SheetExport.Commands.CmdSheetExport",
                GenPanelName,
                "Sheet Exporter",
                "Công cụ Batch Print & Export Sheet/View chuyên nghiệp (PDF, DWG, Issue Manager).",
                "export_sheet_32.png",
                "export_sheet_16.png",
                longDescription: "Hỗ trợ Naming Templates với Regex validation, Issue Revision Diffing, " +
                "Tự động tạo file Excel Transmittal Register & QA Technical Log, " +
                "PDFsharp Bookmarks, Watermark Status Stamp, Cover Sheet, và Auto-Retry.");
            SafeAddItem(panel, sheetExportData, GenPanelName, "Sheet Exporter", "KhimTools.SheetExport.Commands.CmdSheetExport");

            // Elements Tags (Large Button)
            var elementTagsData = CreateSafePushButtonData(
                "CmdElementTags",
                "Elements" + Environment.NewLine + "Tags",
                assemblyPath,
                "KhimTools.ElementTags.Commands.CmdElementTags",
                GenPanelName,
                "Elements Tags",
                "Quản lý và gán thẻ Tag hàng loạt cho các đối tượng trong View hiện hành.",
                "icon_mep_tags_32.png",
                "icon_mep_tags_16.png");
            SafeAddItem(panel, elementTagsData, GenPanelName, "Elements Tags", "KhimTools.ElementTags.Commands.CmdElementTags");

            // Stack 3: Settings Hub & Check Update
            var settingsPulldownData = new PulldownButtonData(
                "SettingsPulldown",
                "Settings")
            {
                ToolTip = "Trung tâm cấu hình tùy chọn K-TOOLS (Ngôn ngữ, Tham số hệ thống).",
                Image = LoadImage("icon_workspace_16.png")
            };

            var updateData = CreateSafePushButtonData(
                "CmdCheckUpdate",
                "Check Update",
                assemblyPath,
                "KhimTools.Updater.Commands.CmdCheckUpdate",
                GenPanelName,
                "Check Update",
                "Kiểm tra phiên bản mới nhất của KhimTools từ GitHub Releases.",
                null,
                "icon_update_16.png");

            var stackedSystem = SafeAddStackedItems(panel, settingsPulldownData, updateData, GenPanelName, "Settings Hub / Update");
            if (stackedSystem != null && stackedSystem.Count == 2)
            {
                var pSettings = stackedSystem[0] as PulldownButton;
                if (pSettings != null)
                {
                    SafeAddPulldownItem(pSettings, "CmdSwitchLanguage", "Đổi Ngôn Ngữ (Switch)", "KhimTools.LanguageSwitcher.Commands.CmdSwitchLanguage", assemblyPath, "icon_workspace_16.png", GenPanelName);
                    SafeAddSeparator(pSettings, GenPanelName);
                    SafeAddPulldownItem(pSettings, "CmdSetVietnamese", "Tiếng Việt (VN)", "KhimTools.LanguageSwitcher.Commands.CmdSetVietnamese", assemblyPath, "icon_workspace_16.png", GenPanelName);
                    SafeAddPulldownItem(pSettings, "CmdSetEnglish", "English (EN)", "KhimTools.LanguageSwitcher.Commands.CmdSetEnglish", assemblyPath, "icon_workspace_16.png", GenPanelName);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 2. PANEL: OVERRIDE (3x3 COLOR PALETTE + HALFTONE + RESET + SETTING)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildOverridePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, OverridePanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(OverridePanelName, OverridePanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // ── STACK 1: ĐỎ, CAM, VÀNG ──
            var redData = CreateColorSwatchData("CmdOverrideRed", "Đỏ", "KhimTools.OverrideTool.Commands.CmdOverrideRed", assemblyPath, "override_red_16.png", "Gán màu Đỏ (Red) cho đối tượng đang chọn");
            var orangeData = CreateColorSwatchData("CmdOverrideOrange", "Cam", "KhimTools.OverrideTool.Commands.CmdOverrideOrange", assemblyPath, "override_orange_16.png", "Gán màu Cam (Orange) cho đối tượng đang chọn");
            var yellowData = CreateColorSwatchData("CmdOverrideYellow", "Vàng", "KhimTools.OverrideTool.Commands.CmdOverrideYellow", assemblyPath, "override_yellow_16.png", "Gán màu Vàng (Yellow) cho đối tượng đang chọn");
            SafeAddStackedItems(panel, redData, orangeData, yellowData, OverridePanelName, "Color Stack 1 (Red-Orange-Yellow)");

            // ── STACK 2: XANH LÁ, CYAN, XANH DƯƠNG ──
            var greenData = CreateColorSwatchData("CmdOverrideGreen", "Lá", "KhimTools.OverrideTool.Commands.CmdOverrideGreen", assemblyPath, "override_green_16.png", "Gán màu Xanh lá (Green) cho đối tượng đang chọn");
            var cyanData = CreateColorSwatchData("CmdOverrideCyan", "Cyan", "KhimTools.OverrideTool.Commands.CmdOverrideCyan", assemblyPath, "override_cyan_16.png", "Gán màu Xanh lơ (Cyan) cho đối tượng đang chọn");
            var blueData = CreateColorSwatchData("CmdOverrideBlue", "Lam", "KhimTools.OverrideTool.Commands.CmdOverrideBlue", assemblyPath, "override_blue_16.png", "Gán màu Xanh dương (Blue) cho đối tượng đang chọn");
            SafeAddStackedItems(panel, greenData, cyanData, blueData, OverridePanelName, "Color Stack 2 (Green-Cyan-Blue)");

            // ── STACK 3: MAGENTA, XÁM, TÙY CHỌN (GRADIENT) ──
            var magentaData = CreateColorSwatchData("CmdOverrideMagenta", "Hồng", "KhimTools.OverrideTool.Commands.CmdOverrideMagenta", assemblyPath, "override_magenta_16.png", "Gán màu Hồng cánh sen (Magenta) cho đối tượng đang chọn");
            var grayData = CreateColorSwatchData("CmdOverrideGray", "Xám", "KhimTools.OverrideTool.Commands.CmdOverrideGray", assemblyPath, "override_gray_16.png", "Gán màu Xám (Gray) cho đối tượng đang chọn");
            var customData = CreateColorSwatchData("CmdOverrideCustom", "Chọn", "KhimTools.OverrideTool.Commands.CmdOverrideCustom", assemblyPath, "override_custom_16.png", "Chọn màu tùy chỉnh từ bảng màu (Custom Color Picker)");
            SafeAddStackedItems(panel, magentaData, grayData, customData, OverridePanelName, "Color Stack 3 (Magenta-Gray-Custom)");

            // ── LARGE BUTTON 1: ON/OFF HALFTONE ──
            var halftoneData = CreateSafePushButtonData(
                "CmdQuickHalftone",
                "On/Off" + Environment.NewLine + "Halftone",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdQuickHalftone",
                OverridePanelName,
                "On/Off Halftone",
                "Bật/Tắt nhanh chế độ mờ Halftone 50% cho đối tượng đang chọn.",
                "override_halftone_32.png",
                "override_halftone_16.png");
            SafeAddItem(panel, halftoneData, OverridePanelName, "On/Off Halftone", "KhimTools.OverrideTool.Commands.CmdQuickHalftone");

            // ── LARGE BUTTON 2: RESET OVERRIDE ──
            var resetData = CreateSafePushButtonData(
                "CmdQuickResetOverride",
                "Reset" + Environment.NewLine + "Override",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdQuickResetOverride",
                OverridePanelName,
                "Reset Override",
                "Xóa toàn bộ màu sắc, đường nét, halftone đã override của đối tượng đang chọn.",
                "override_reset_32.png",
                "override_reset_16.png");
            SafeAddItem(panel, resetData, OverridePanelName, "Reset Override", "KhimTools.OverrideTool.Commands.CmdQuickResetOverride");

            // ── LARGE BUTTON 3: OVERRIDE SETTINGS ──
            var settingData = CreateSafePushButtonData(
                "CmdGraphicOverdrive",
                "Override" + Environment.NewLine + "Settings",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdGraphicOverdrive",
                OverridePanelName,
                "Override Settings",
                "Mở bảng điều khiển Graphic Overdrive chi tiết (Độ trong suốt Transparency, Nét vẽ Line Weight, 12 Presets màu).",
                "override_setting_32.png",
                "override_setting_16.png");
            SafeAddItem(panel, settingData, OverridePanelName, "Override Settings", "KhimTools.OverrideTool.Commands.CmdGraphicOverdrive");
        }

        private static PushButtonData CreateColorSwatchData(
            string id,
            string fallbackLabel,
            string className,
            string assemblyPath,
            string iconName,
            string tooltip)
        {
            // ROOT CAUSE FIX & SELF-HEALING ARCHITECTURE:
            // 1. Ưu tiên sử dụng ZeroWidthSpace ("\u200B") để giữ layout 3x3 icon-only chuẩn xác.
            // 2. Tích hợp fallbackLabel có nghĩa ("Đỏ", "Cam", "Vàng"...) tự động fallback nếu Revit từ chối.
            return CreateSafePushButtonData(
                id,
                ZeroWidthSpace,
                assemblyPath,
                className,
                OverridePanelName,
                fallbackLabel,
                tooltip,
                iconName,
                iconName);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 3. PANEL: QUICK (QUICK STRUCTURE + FAMILY MANAGER)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildQuickPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, QuickPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(QuickPanelName, QuickPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. Quick Structure SplitButton (Renamed from "Quick Draft")
            var quickSplitData = new SplitButtonData("QuickStructureSplitButton", "Quick" + Environment.NewLine + "Structure")
            {
                ToolTip = "Đặt nhanh các cấu kiện kết cấu cơ bản (Cột, Dầm, Móng, Tường, Sàn). Tự động kiểm tra và hướng dẫn nạp Family nếu thiếu."
            };

            var quickSplit = SafeAddItem(panel, quickSplitData, QuickPanelName, "Quick Structure SplitButton", string.Empty) as SplitButton;
            if (quickSplit != null)
            {
                SafeAddSplitButtonItem(quickSplit, "CmdQuickColumn", "Quick Column",
                    "KhimTools.QuickDraft.Commands.CmdQuickColumn", assemblyPath,
                    "Đặt Cột Kết Cấu nhanh. Nếu Family chưa nạp sẽ gợi ý tải ngay.",
                    "rebar_col_32.png", "rebar_col_16.png", QuickPanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickBeam", "Quick Beam",
                    "KhimTools.QuickDraft.Commands.CmdQuickBeam", assemblyPath,
                    "Đặt Dầm Kết Cấu nhanh. Nếu Family chưa nạp sẽ gợi ý tải ngay.",
                    "rebar_beam_32.png", "rebar_beam_16.png", QuickPanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickFoundation", "Quick Foundation",
                    "KhimTools.QuickDraft.Commands.CmdQuickFoundation", assemblyPath,
                    "Đặt Móng Kết Cấu nhanh. Nếu Family chưa nạp sẽ gợi ý tải ngay.",
                    "rebar_fdn_32.png", "rebar_fdn_16.png", QuickPanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickWall", "Quick Wall",
                    "KhimTools.QuickDraft.Commands.CmdQuickWall", assemblyPath,
                    "Kích hoạt lệnh tạo Tường Kết Cấu nhanh (Structural Wall).",
                    "icon_cover_setup_32.png", "icon_cover_setup_16.png", QuickPanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickSlab", "Quick Slab",
                    "KhimTools.QuickDraft.Commands.CmdQuickSlab", assemblyPath,
                    "Kích hoạt lệnh tạo Sàn Kết Cấu nhanh (Structural Floor).",
                    "rebar_slab_32.png", "rebar_slab_16.png", QuickPanelName);
            }

            // 2. Family Manager (Large Button)
            var famMgrData = CreateSafePushButtonData(
                "CmdFamilyManager",
                "Family" + Environment.NewLine + "Manager",
                assemblyPath,
                "KhimTools.FamilyManager.Commands.CmdFamilyManager",
                QuickPanelName,
                "Family Manager",
                "Quản lý và nạp Family thư viện KhimTools cho tất cả các bộ môn.",
                "icon_family_mgr_32.png",
                "icon_family_mgr_16.png");
            SafeAddItem(panel, famMgrData, QuickPanelName, "Family Manager", "KhimTools.FamilyManager.Commands.CmdFamilyManager");
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 4. PANEL: K-STRUCTURAL (REBAR & DETAILING)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildStructuralPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, StructuralPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(StructuralPanelName, StructuralPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. SplitButton: Column Rebar
            var splitButtonData = new SplitButtonData(
                "ColumnRebarSplitButton",
                "Column" + Environment.NewLine + "Rebar")
            {
                ToolTip = "Bố trí thép cột tự động (phát hiện vuông/tròn từ phần tử đang chọn)."
            };

            var splitButton = SafeAddItem(panel, splitButtonData, StructuralPanelName, "Column Rebar SplitButton", string.Empty) as SplitButton;
            if (splitButton != null)
            {
                SafeAddSplitButtonItem(splitButton, "CmdColumnRebar", "Column Rebar (Auto-detect)",
                    "KhimTools.RebarTool.Commands.CmdColumnRebar", assemblyPath,
                    "Tự động phát hiện loại cột (vuông/tròn) và mở giao diện phù hợp.",
                    "rebar_col_32.png", "rebar_col_16.png", StructuralPanelName);

                SafeAddSplitButtonItem(splitButton, "CmdMultiColumnRebar", "Cột Vuông / Chữ Nhật 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột vuông/chữ nhật.",
                    "rebar_col_32.png", "rebar_col_rect_16.png", StructuralPanelName);

                SafeAddSplitButtonItem(splitButton, "CmdMultiRoundColumnRebar", "Cột Tròn 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiRoundColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột tròn.",
                    "rebar_col_circ_32.png", "rebar_col_circ_16.png", StructuralPanelName);
            }

            // 2. Beam Rebar
            var beamData = CreateSafePushButtonData(
                "CmdBeamRebar",
                "Beam" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdBeamRebar",
                StructuralPanelName,
                "Beam Rebar",
                "Bố trí thép dầm (Beam Rebar v2.0) chuẩn kết cấu TCVN & Eurocode.",
                "rebar_beam_32.png",
                "rebar_beam_16.png",
                longDescription: "Hỗ trợ thép chủ chạy suốt (top/bottom), thép gia cường gối L/3, " +
                "thép gia cường bụng L/6, thép sườn (skin bars), đai phân vùng A1/A2/A1 và đai treo dầm phụ.");
            SafeAddItem(panel, beamData, StructuralPanelName, "Beam Rebar", "KhimTools.RebarTool.Commands.CmdBeamRebar");

            // 3. Slab Rebar
            var slabData = CreateSafePushButtonData(
                "CmdSlabRebar",
                "Slab" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdSlabRebar",
                StructuralPanelName,
                "Slab Rebar",
                "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                "rebar_slab_32.png",
                "rebar_slab_16.png");
            SafeAddItem(panel, slabData, StructuralPanelName, "Slab Rebar", "KhimTools.RebarTool.Commands.CmdSlabRebar");

            // 4. Foundation Rebar
            var fdnData = CreateSafePushButtonData(
                "CmdFoundationRebar",
                "Foundation" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdFoundationRebar",
                StructuralPanelName,
                "Foundation Rebar",
                "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                "rebar_fdn_32.png",
                "rebar_fdn_16.png");
            SafeAddItem(panel, fdnData, StructuralPanelName, "Foundation Rebar", "KhimTools.RebarTool.Commands.CmdFoundationRebar");

            panel.AddSeparator();

            // 5. Stacked: Drawing ▼ / Setup ▼
            var drawingPulldownData = new PulldownButtonData("KhimDrawingPulldown", "Drawing")
            {
                ToolTip = "Các công cụ xuất bản vẽ chi tiết & mặt cắt kết cấu thép.",
                Image = LoadImage("rebar_draw_16.png")
            };

            var setupPulldownData = new PulldownButtonData("KhimSetupPulldown", "Setup")
            {
                ToolTip = "Cấu hình tham số kết cấu & lớp bê tông bảo vệ (Cover).",
                Image = LoadImage("icon_cover_setup_16.png")
            };

            var stackedDoc = SafeAddStackedItems(panel, drawingPulldownData, setupPulldownData, StructuralPanelName, "Drawing / Setup Stack");
            if (stackedDoc != null && stackedDoc.Count == 2)
            {
                var pDrawing = stackedDoc[0] as PulldownButton;
                var pSetup = stackedDoc[1] as PulldownButton;

                if (pDrawing != null)
                {
                    SafeAddPulldownItem(pDrawing, "CmdColumnDrawing", "Column Drawing",
                        "KhimTools.RebarTool.Commands.CmdColumnDrawing", assemblyPath, "rebar_draw_16.png", StructuralPanelName);
                    SafeAddPulldownItem(pDrawing, "CmdUpdateColumnDrawing", "Update Drawing",
                        "KhimTools.RebarTool.Commands.CmdUpdateColumnDrawing", assemblyPath, "rebar_draw_16.png", StructuralPanelName);
                    SafeAddSeparator(pDrawing, StructuralPanelName);
                    SafeAddPulldownItem(pDrawing, "CmdSectionCut", "Section Cut",
                        "KhimTools.SectionCutTool.Commands.CmdSectionCut", assemblyPath, "icon_section_cut_16.png", StructuralPanelName);
                }

                if (pSetup != null)
                {
                    SafeAddPulldownItem(pSetup, "CmdProjectCoverSetup", "Cover Setup",
                        "KhimTools.RebarTool.Commands.CmdProjectCoverSetup", assemblyPath, "icon_cover_setup_16.png", StructuralPanelName);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 4. PANEL: K-ARCHITECTURAL
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildArchPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ArchPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(ArchPanelName, ArchPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. Room 3D View
            var room3dData = CreateSafePushButtonData(
                "CmdRoom3DView",
                "Room 3D" + Environment.NewLine + "View",
                assemblyPath,
                "KhimTools.Architectural.Rooms.CmdRoom3DView",
                ArchPanelName,
                "Room 3D View",
                "Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.",
                "icon_room3d_32.png",
                "icon_room3d_16.png");
            SafeAddItem(panel, room3dData, ArchPanelName, "Room 3D View", "KhimTools.Architectural.Rooms.CmdRoom3DView");

            // 2. Room Finishes
            var finishData = CreateSafePushButtonData(
                "CmdWallFloorFinishes",
                "Room" + Environment.NewLine + "Finishes",
                assemblyPath,
                "KhimTools.Architectural.Finishes.CmdWallFloorFinishes",
                ArchPanelName,
                "Room Finishes",
                "Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng.",
                "icon_finishes_32.png",
                "icon_finishes_16.png");
            SafeAddItem(panel, finishData, ArchPanelName, "Room Finishes", "KhimTools.Architectural.Finishes.CmdWallFloorFinishes");
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 5. PANEL: K-MEP
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildMepPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, MepPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(MepPanelName, MepPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. MEP Openings
            var openingData = CreateSafePushButtonData(
                "CmdMepOpenings",
                "MEP" + Environment.NewLine + "Openings",
                assemblyPath,
                "KhimTools.MEP.Penetrations.CmdMepOpenings",
                MepPanelName,
                "MEP Openings",
                "Tự động kiểm tra xung đột ống MEP với Dầm/Sàn/Vách và đục lỗ mở (Openings).",
                "icon_mep_openings_32.png",
                "icon_mep_openings_16.png");
            SafeAddItem(panel, openingData, MepPanelName, "MEP Openings", "KhimTools.MEP.Penetrations.CmdMepOpenings");

            // 2. MEP Elevation Tags
            var tagData = CreateSafePushButtonData(
                "CmdMepElevationTags",
                "Elevation" + Environment.NewLine + "Tags",
                assemblyPath,
                "KhimTools.MEP.Tags.CmdMepElevationTags",
                MepPanelName,
                "Elevation Tags",
                "Tự động gán nhãn cao độ đáy (BOP/Invert Elevation) cho ống gió và ống nước.",
                "icon_mep_tags_32.png",
                "icon_mep_tags_16.png");
            SafeAddItem(panel, tagData, MepPanelName, "Elevation Tags", "KhimTools.MEP.Tags.CmdMepElevationTags");
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // SAFE REGISTRATION HELPERS (FAULT TOLERANCE & DETERMINISTIC SANITIZATION)
        // ════════════════════════════════════════════════════════════════════════════════

        public static PushButtonData CreateSafePushButtonData(
            string name,
            string text,
            string assemblyPath,
            string className,
            string moduleName,
            string toolName = null,
            string toolTip = null,
            string largeIcon = null,
            string smallIcon = null,
            string longDescription = null,
            string fallbackLabel = null)
        {
            string safeName = string.IsNullOrWhiteSpace(name) ? ("Btn_" + Guid.NewGuid().ToString("N").Substring(0, 8)) : name.Trim();
            string safeText = SanitizeButtonText(text, toolName ?? safeName);

            PushButtonData data = null;
            try
            {
                data = new PushButtonData(safeName, safeText, assemblyPath, className);
            }
            catch (Exception ex)
            {
                // Self-healing fallback: Nếu chuỗi đặc biệt bị từ chối, thử lại bằng tên công cụ rõ ràng
                string fallback = !string.IsNullOrWhiteSpace(toolName) ? toolName : safeName;
                RegistrationDiagnostics.RecordWarning(moduleName, 
                    $"PushButtonData '{safeName}' lỗi khi dùng text '{safeText}' ({ex.Message}). Tự động phục hồi với fallback '{fallback}'.");

                try
                {
                    data = new PushButtonData(safeName, fallback, assemblyPath, className);
                }
                catch (Exception exFallback)
                {
                    RegistrationDiagnostics.RecordError(moduleName, moduleName, toolName, className,
                        $"Khởi tạo PushButtonData hoàn toàn thất bại: {exFallback.Message}", exFallback);
                    return null;
                }
            }

            if (!string.IsNullOrEmpty(toolTip)) data.ToolTip = toolTip;
            if (!string.IsNullOrEmpty(longDescription)) data.LongDescription = longDescription;
            if (!string.IsNullOrEmpty(largeIcon)) data.LargeImage = LoadImage(largeIcon);
            if (!string.IsNullOrEmpty(smallIcon)) data.Image = LoadImage(smallIcon);

            return data;
        }

        public static string SanitizeButtonText(string text, string fallbackName)
        {
            if (string.IsNullOrEmpty(text))
            {
                return !string.IsNullOrWhiteSpace(fallbackName) ? fallbackName : ZeroWidthSpace;
            }

            if (text.Trim().Length == 0)
            {
                // Tránh lỗi Revit API ArgumentException trên chuỗi chỉ toàn khoảng trắng thường
                return ZeroWidthSpace;
            }

            return text;
        }

        public static RibbonItem SafeAddItem(
            RibbonPanel panel,
            RibbonItemData itemData,
            string moduleName,
            string toolName = null,
            string commandClass = null)
        {
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(moduleName, moduleName, toolName, commandClass, "RibbonPanel is null khi gọi SafeAddItem.");
                return null;
            }
            if (itemData == null)
            {
                RegistrationDiagnostics.RecordError(moduleName, panel.Name, toolName, commandClass, "RibbonItemData is null khi gọi SafeAddItem.");
                return null;
            }

            try
            {
                var item = panel.AddItem(itemData);
                if (item != null)
                {
                    var record = RegistrationDiagnostics.GetOrCreate(moduleName);
                    record.RegisteredCount++;
                }
                return item;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordError(moduleName, panel.Name, toolName, commandClass,
                    $"Không thể thêm nút [{itemData.Name}] vào panel [{panel.Name}]: {ex.Message}", ex);
                return null;
            }
        }

        public static System.Collections.Generic.IList<RibbonItem> SafeAddStackedItems(
            RibbonPanel panel,
            RibbonItemData item1,
            RibbonItemData item2,
            string moduleName,
            string groupName = null)
        {
            if (panel == null || item1 == null || item2 == null)
            {
                RegistrationDiagnostics.RecordError(moduleName, panel?.Name ?? moduleName, groupName, string.Empty,
                    "Tham số null khi gọi SafeAddStackedItems (2 items).");
                return null;
            }

            try
            {
                var items = panel.AddStackedItems(item1, item2);
                if (items != null)
                {
                    var record = RegistrationDiagnostics.GetOrCreate(moduleName);
                    record.RegisteredCount += items.Count;
                }
                return items;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordError(moduleName, panel.Name, groupName, string.Empty,
                    $"Không thể thêm 2 stacked items [{item1.Name}, {item2.Name}]: {ex.Message}", ex);
                return null;
            }
        }

        public static System.Collections.Generic.IList<RibbonItem> SafeAddStackedItems(
            RibbonPanel panel,
            RibbonItemData item1,
            RibbonItemData item2,
            RibbonItemData item3,
            string moduleName,
            string groupName = null)
        {
            if (panel == null || item1 == null || item2 == null || item3 == null)
            {
                RegistrationDiagnostics.RecordError(moduleName, panel?.Name ?? moduleName, groupName, string.Empty,
                    "Tham số null khi gọi SafeAddStackedItems (3 items).");
                return null;
            }

            try
            {
                var items = panel.AddStackedItems(item1, item2, item3);
                if (items != null)
                {
                    var record = RegistrationDiagnostics.GetOrCreate(moduleName);
                    record.RegisteredCount += items.Count;
                }
                return items;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordError(moduleName, panel.Name, groupName, string.Empty,
                    $"Không thể thêm 3 stacked items [{item1.Name}, {item2.Name}, {item3.Name}]: {ex.Message}", ex);
                return null;
            }
        }

        public static PushButton SafeAddPulldownItem(
            PulldownButton pulldown,
            string name,
            string text,
            string className,
            string assemblyPath,
            string smallIconName,
            string moduleName)
        {
            if (pulldown == null)
            {
                RegistrationDiagnostics.RecordError(moduleName, moduleName, name, className, "PulldownButton is null.");
                return null;
            }

            try
            {
                var data = CreateSafePushButtonData(name, text, assemblyPath, className, moduleName,
                    text, "Bật/Tắt hiển thị hoặc căn chỉnh đối tượng trong Active View.", null, smallIconName);

                if (data == null) return null;

                var btn = pulldown.AddPushButton(data);
                if (btn != null)
                {
                    var record = RegistrationDiagnostics.GetOrCreate(moduleName);
                    record.RegisteredCount++;
                }
                return btn;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordError(moduleName, moduleName, text, className,
                    $"Không thể thêm pulldown item [{name}]: {ex.Message}", ex);
                return null;
            }
        }

        public static PushButton SafeAddSplitButtonItem(
            SplitButton splitButton,
            string name,
            string text,
            string className,
            string assemblyPath,
            string toolTip,
            string largeIconName,
            string smallIconName,
            string moduleName)
        {
            if (splitButton == null)
            {
                RegistrationDiagnostics.RecordError(moduleName, moduleName, name, className, "SplitButton is null.");
                return null;
            }

            try
            {
                var data = CreateSafePushButtonData(name, text, assemblyPath, className, moduleName,
                    text, toolTip, largeIconName, smallIconName);

                if (data == null) return null;

                var btn = splitButton.AddPushButton(data);
                if (btn != null)
                {
                    var record = RegistrationDiagnostics.GetOrCreate(moduleName);
                    record.RegisteredCount++;
                }
                return btn;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordError(moduleName, moduleName, text, className,
                    $"Không thể thêm split button item [{name}]: {ex.Message}", ex);
                return null;
            }
        }

        private static void SafeAddSeparator(PulldownButton pulldown, string moduleName)
        {
            try
            {
                pulldown?.AddSeparator();
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordWarning(moduleName, $"Không thể thêm Separator vào Pulldown: {ex.Message}");
            }
        }

        private static void SafeAddSeparator(SplitButton splitButton, string moduleName)
        {
            try
            {
                splitButton?.AddSeparator();
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordWarning(moduleName, $"Không thể thêm Separator vào SplitButton: {ex.Message}");
            }
        }

        private static void CreateTabSafely(UIControlledApplication app, string tabName)
        {
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch (Exception ex)
            {
                // Tab đã tồn tại từ trước là hoàn toàn bình thường trong Revit
                RegistrationDiagnostics.RecordWarning("RibbonRoot", $"CreateRibbonTab('{tabName}') notice: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tabName, string panelName)
        {
            try
            {
                var panels = app.GetRibbonPanels(tabName);
                var existing = panels?.FirstOrDefault(p => p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordWarning(panelName, $"GetRibbonPanels('{tabName}') thông báo: {ex.Message}");
            }

            try
            {
                return app.CreateRibbonPanel(tabName, panelName);
            }
            catch (Exception exCreate)
            {
                RegistrationDiagnostics.RecordWarning(panelName, 
                    $"CreateRibbonPanel('{tabName}', '{panelName}') throw: {exCreate.Message}. Đang thử lấy lại panel đã tạo...");
                try
                {
                    return app.GetRibbonPanels(tabName)?.FirstOrDefault(p => p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception exRetry)
                {
                    RegistrationDiagnostics.RecordError(panelName, panelName, "GetOrCreatePanel", string.Empty,
                        $"Hoàn toàn không thể lấy hoặc tạo RibbonPanel [{panelName}]: {exRetry.Message}", exRetry);
                    return null;
                }
            }
        }

        private static string GetSafeAssemblyPath()
        {
            try
            {
                string loc = typeof(App).Assembly.Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) return loc;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordWarning("AssemblyResolver", $"Lỗi đọc typeof(App).Assembly.Location: {ex.Message}");
            }

            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) return loc;
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordWarning("AssemblyResolver", $"Lỗi đọc Assembly.GetExecutingAssembly().Location: {ex.Message}");
            }

            return Assembly.GetExecutingAssembly().Location ?? string.Empty;
        }

        private static BitmapImage LoadImage(string resourceOrFileName)
        {
            if (string.IsNullOrEmpty(resourceOrFileName)) return null;

            try
            {
                var assembly = typeof(RibbonBuilder).Assembly;

                // 1. Thử load từ Embedded Resource
                string resourceName = assembly.GetManifestResourceNames()
                    ?.FirstOrDefault(r => r.EndsWith(resourceOrFileName, StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = stream;
                            img.CacheOption = BitmapCacheOption.OnLoad;
                            img.EndInit();
                            img.Freeze();
                            return img;
                        }
                    }
                }

                // 2. Thử load từ disk (cạnh DLL / Resources)
                string loc = assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    string dir = Path.GetDirectoryName(loc) ?? string.Empty;
                    string diskPath = Path.Combine(dir, "Resources", resourceOrFileName);
                    if (!File.Exists(diskPath)) diskPath = Path.Combine(dir, resourceOrFileName);

                    if (File.Exists(diskPath))
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.UriSource = new Uri(diskPath, UriKind.Absolute);
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.EndInit();
                        img.Freeze();
                        return img;
                    }
                }

                RegistrationDiagnostics.RecordWarning("ResourceLoader", $"Không tìm thấy icon '{resourceOrFileName}' trong Embedded Resource hoặc Resources folder.");
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordWarning("ResourceLoader", $"Ngoại lệ khi nạp ảnh '{resourceOrFileName}': {ex.GetType().Name} - {ex.Message}");
            }

            return null;
        }
    }
}
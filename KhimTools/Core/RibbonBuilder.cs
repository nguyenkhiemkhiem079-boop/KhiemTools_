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
    /// Được phân chia thành 5 cụm Panel chuyên môn gom gọn:
    ///   1. K-GEN           (Workspace, Join, Overdrive, Ẩn/Hiện, Căn chỉnh Text, Align Viewport, Grid, Link, Detail No, Update, Exporter, Language)
    ///   2. Override        (Palette màu 3x3 + Halftone + Reset + Setting Color)
    ///   3. K-STRUCTURAL    (Column Rebar, Beam Rebar, Slab Rebar, Foundation Rebar, Section Cut, Cover Setup)
    ///   4. K-ARCHITECTURAL (Room 3D View, Room Finishes)
    ///   5. K-MEP           (MEP Openings, Elevation Tags)
    ///
    /// KIẾN TRÚC BẢO VỆ CÁCH LY LỖI (FAULT-TOLERANT ARCHITECTURE):
    ///   - Mỗi Panel được khởi tạo trong một sandbox riêng biệt (RegisterPanelModule).
    ///   - Mỗi Nút bấm/Stacked Item/Pulldown Item được bảo vệ và validate (SafeAddItem, SanitizeText).
    ///   - Bất kỳ lỗi nào xảy ra ở 1 công cụ sẽ được ghi nhận vào RegistrationDiagnostics,
    ///     TUYỆT ĐỐI KHÔNG làm ngắt chuỗi khởi động hay làm mất các panel khác.
    /// </summary>
    public static class RibbonBuilder
    {
        public const string TabName = "K-TOOLS";
        public const string GenPanelName = "K-GEN";
        public const string OverridePanelName = "Override";
        public const string StructuralPanelName = "K-STRUCTURAL";
        public const string ArchPanelName = "K-ARCHITECTURAL";
        public const string MepPanelName = "K-MEP";

        /// <summary>
        /// Ký tự Zero-Width Space dùng cho các nút icon-only (như ô màu swatch).
        /// Revit API coi chuỗi này là hợp lệ (không empty), trong khi UI hiển thị gọn gàng không bị vỡ layout.
        /// </summary>
        public const string ZeroWidthSpace = "\u200B";

        public static void BuildRibbon(UIControlledApplication application)
        {
            if (application == null) return;

            RegistrationDiagnostics.Reset();
            CreateTabSafely(application, TabName);
            string assemblyPath = GetSafeAssemblyPath();

            // 1. Panel: K-GEN
            RegisterPanelModule(GenPanelName, () => BuildGenPanel(application, assemblyPath));

            // 2. Panel: Override (Palette màu 3x3 + Halftone + Reset + Setting Color)
            RegisterPanelModule(OverridePanelName, () => BuildOverridePanel(application, assemblyPath));

            // 3. Panel: K-STRUCTURAL
            RegisterPanelModule(StructuralPanelName, () => BuildStructuralPanel(application, assemblyPath));

            // 4. Panel: K-ARCHITECTURAL
            RegisterPanelModule(ArchPanelName, () => BuildArchPanel(application, assemblyPath));

            // 5. Panel: K-MEP
            RegisterPanelModule(MepPanelName, () => BuildMepPanel(application, assemblyPath));

            // Ghi nhật ký diagnostics sau khi kết thúc toàn bộ chuỗi đăng ký
            RegistrationDiagnostics.PersistLog();
        }

        /// <summary>
        /// Bộ bọc cách ly lỗi cấp độ Module/Panel (Module Failure Isolation).
        /// Nếu 1 module ném exception, ghi log chi tiết và tiếp tục module tiếp theo.
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
                RegistrationDiagnostics.RecordError(moduleName, $"Lỗi nghiêm trọng khi khởi tạo Panel [{moduleName}]", ex);
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
                RegistrationDiagnostics.RecordError(GenPanelName, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // ── CỤM 1: WORKSPACE & LINK ──
            // 1. Khim Workspace (Large Button)
            var wsData = CreateSafePushButtonData(
                "CmdToggleWorkspace",
                "Khim" + Environment.NewLine + "Workspace",
                assemblyPath,
                "KhimTools.Workspace.Commands.CmdToggleWorkspace",
                GenPanelName,
                "Bật/Tắt bảng điều khiển Khim Workspace (Dockable Pane).",
                "icon_workspace_32.png",
                "icon_workspace_16.png");
            SafeAddItem(panel, wsData, GenPanelName);

            // 2. Copy Link Elements (Large Button)
            var copyLinkData = CreateSafePushButtonData(
                "CmdCopyLinkElements",
                "Copy Link" + Environment.NewLine + "Elements",
                assemblyPath,
                "KhimTools.CopyLink.Commands.CmdCopyLinkElements",
                GenPanelName,
                "Sao chép đối tượng từ file Revit Link sang dự án chính chuẩn 100% tọa độ.",
                "icon_copylink_32.png",
                "icon_copylink_16.png");
            SafeAddItem(panel, copyLinkData, GenPanelName);

            // ── CỤM 2: MODEL & GEOMETRY ──
            // 3. Join Elements (Large Button)
            var joinElementsData = CreateSafePushButtonData(
                "CmdJoinElements",
                "Join" + Environment.NewLine + "Elements",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.CmdJoinElements",
                GenPanelName,
                "Mở công cụ Join/Unjoin/Switch chuyên nghiệp cho tất cả loại cấu kiện.",
                "icon_join_32.png",
                "icon_join_16.png",
                "Hỗ trợ join/unjoin/switch geometry giữa bất kỳ cặp Category: Floors, Walls, Columns, Beams, Foundations...");
            SafeAddItem(panel, joinElementsData, GenPanelName);

            // 4. Auto Grid & Floor Plan Generator (Large Button)
            var gridPlanData = CreateSafePushButtonData(
                "CmdGridPlanGenerator",
                "Grid &" + Environment.NewLine + "Floor Plan",
                assemblyPath,
                "KhimTools.GridLevel.Commands.CmdAutoGridPlan",
                GenPanelName,
                "Tự động sinh Hệ Lưới Trục (Grid) và Mặt Bằng / Cao Độ Tầng (Level & Floor Plan) từ CAD/DWG.",
                "icon_grid_plan_32.png",
                "icon_grid_plan_16.png");
            SafeAddItem(panel, gridPlanData, GenPanelName);

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

            var stackedVis = SafeAddStackedItems(panel, pulldownShowData, pulldownHideData, GenPanelName);
            if (stackedVis != null && stackedVis.Count == 2)
            {
                var pShow = stackedVis[0] as PulldownButton;
                var pHide = stackedVis[1] as PulldownButton;

                if (pShow != null)
                {
                    SafeAddPulldownItem(pShow, "CmdShowWindow", "Hiển thị Window", "KhimTools.VisibilityTool.Commands.CmdShowWindow", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowDoor", "Hiển thị Door", "KhimTools.VisibilityTool.Commands.CmdShowDoor", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowCeiling", "Hiển thị Ceiling", "KhimTools.VisibilityTool.Commands.CmdShowCeiling", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowRoof", "Hiển thị Roof", "KhimTools.VisibilityTool.Commands.CmdShowRoof", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowStair", "Hiển thị Stair", "KhimTools.VisibilityTool.Commands.CmdShowStair", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowRailing", "Hiển thị Railing", "KhimTools.VisibilityTool.Commands.CmdShowRailing", assemblyPath, "icon_detail_16.png", GenPanelName);
                    try { pShow.AddSeparator(); } catch { }
                    SafeAddPulldownItem(pShow, "CmdShowColumn", "Hiển thị Column", "KhimTools.VisibilityTool.Commands.CmdShowColumn", assemblyPath, "rebar_col_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowFraming", "Hiển thị Framing", "KhimTools.VisibilityTool.Commands.CmdShowFraming", assemblyPath, "rebar_beam_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowFloor", "Hiển thị Floor", "KhimTools.VisibilityTool.Commands.CmdShowFloor", assemblyPath, "rebar_slab_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowWall", "Hiển thị Wall", "KhimTools.VisibilityTool.Commands.CmdShowWall", assemblyPath, "icon_join_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowFoundation", "Hiển thị Foundation", "KhimTools.VisibilityTool.Commands.CmdShowFoundation", assemblyPath, "rebar_fdn_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowRebar", "Hiển thị Rebar", "KhimTools.VisibilityTool.Commands.CmdShowRebar", assemblyPath, "rebar_draw_16.png", GenPanelName);
                    try { pShow.AddSeparator(); } catch { }
                    SafeAddPulldownItem(pShow, "CmdShowGrid", "Hiển thị Grid", "KhimTools.VisibilityTool.Commands.CmdShowGrid", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowLevel", "Hiển thị Level", "KhimTools.VisibilityTool.Commands.CmdShowLevel", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowSection", "Hiển thị Section", "KhimTools.VisibilityTool.Commands.CmdShowSection", assemblyPath, "rebar_draw_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowElevation", "Hiển thị Elevation", "KhimTools.VisibilityTool.Commands.CmdShowElevation", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pShow, "CmdShowTag", "Hiển thị Tag", "KhimTools.VisibilityTool.Commands.CmdShowTag", assemblyPath, "icon_detail_16.png", GenPanelName);
                }

                if (pHide != null)
                {
                    SafeAddPulldownItem(pHide, "CmdHideWindow", "Ẩn Window", "KhimTools.VisibilityTool.Commands.CmdHideWindow", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideDoor", "Ẩn Door", "KhimTools.VisibilityTool.Commands.CmdHideDoor", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideCeiling", "Ẩn Ceiling", "KhimTools.VisibilityTool.Commands.CmdHideCeiling", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideRoof", "Ẩn Roof", "KhimTools.VisibilityTool.Commands.CmdHideRoof", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideStair", "Ẩn Stair", "KhimTools.VisibilityTool.Commands.CmdHideStair", assemblyPath, "icon_detail_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideRailing", "Ẩn Railing", "KhimTools.VisibilityTool.Commands.CmdHideRailing", assemblyPath, "icon_detail_16.png", GenPanelName);
                    try { pHide.AddSeparator(); } catch { }
                    SafeAddPulldownItem(pHide, "CmdHideColumn", "Ẩn Column", "KhimTools.VisibilityTool.Commands.CmdHideColumn", assemblyPath, "rebar_col_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideFraming", "Ẩn Framing", "KhimTools.VisibilityTool.Commands.CmdHideFraming", assemblyPath, "rebar_beam_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideFloor", "Ẩn Floor", "KhimTools.VisibilityTool.Commands.CmdHideFloor", assemblyPath, "rebar_slab_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideWall", "Ẩn Wall", "KhimTools.VisibilityTool.Commands.CmdHideWall", assemblyPath, "icon_join_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideFoundation", "Ẩn Foundation", "KhimTools.VisibilityTool.Commands.CmdHideFoundation", assemblyPath, "rebar_fdn_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideRebar", "Ẩn Rebar", "KhimTools.VisibilityTool.Commands.CmdHideRebar", assemblyPath, "rebar_draw_16.png", GenPanelName);
                    try { pHide.AddSeparator(); } catch { }
                    SafeAddPulldownItem(pHide, "CmdHideGrid", "Ẩn Grid", "KhimTools.VisibilityTool.Commands.CmdHideGrid", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideLevel", "Ẩn Level", "KhimTools.VisibilityTool.Commands.CmdHideLevel", assemblyPath, "icon_grid_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideSection", "Ẩn Section", "KhimTools.VisibilityTool.Commands.CmdHideSection", assemblyPath, "rebar_draw_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideElevation", "Ẩn Elevation", "KhimTools.VisibilityTool.Commands.CmdHideElevation", assemblyPath, "icon_align_16.png", GenPanelName);
                    SafeAddPulldownItem(pHide, "CmdHideTag", "Ẩn Tag", "KhimTools.VisibilityTool.Commands.CmdHideTag", assemblyPath, "icon_detail_16.png", GenPanelName);
                }
            }

            // ── CỤM 3: LAYOUT (Large Pulldown Button) ──
            var layoutPulldownData = new PulldownButtonData("KhimLayoutPulldown", "Layout")
            {
                ToolTip = "Các công cụ dàn trang, quản lý bản vẽ, căn chỉnh và tạo Sheet.",
                LargeImage = LoadImage("icon_align_32.png"),
                Image = LoadImage("icon_align_16.png")
            };
            var layoutPulldown = SafeAddItem(panel, layoutPulldownData, GenPanelName) as PulldownButton;
            if (layoutPulldown != null)
            {
                SafeAddPulldownItem(layoutPulldown, "CmdSheetGen", "Create Sheets (CSV)", "KhimTools.SheetGen.Commands.CmdSheetGen", assemblyPath, "export_sheet_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdSlabStep", "Slab Step Generator", "KhimTools.SlabStep.Commands.CmdSlabStep", assemblyPath, "icon_join_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdAlignViewport", "Align Viewports", "KhimTools.ViewportAlign.Commands.CmdAlignViewport", assemblyPath, "icon_align_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdUpdateDetailNumbers", "Update Detail No", "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers", assemblyPath, "icon_detail_16.png", GenPanelName);

                try { layoutPulldown.AddSeparator(); } catch { }
                SafeAddPulldownItem(layoutPulldown, "CmdAlignTop", "Align Text - Top", "KhimTools.TextAlign.Commands.CmdAlignTop", assemblyPath, "icon_align_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdAlignBottom", "Align Text - Bottom", "KhimTools.TextAlign.Commands.CmdAlignBottom", assemblyPath, "icon_align_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdAlignLeft", "Align Text - Left", "KhimTools.TextAlign.Commands.CmdAlignLeft", assemblyPath, "icon_align_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdAlignRight", "Align Text - Right", "KhimTools.TextAlign.Commands.CmdAlignRight", assemblyPath, "icon_align_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdAlignMiddle", "Align Text - Middle", "KhimTools.TextAlign.Commands.CmdAlignMiddle", assemblyPath, "icon_align_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdAlignHorizontalEquals", "Align Text - Horiz Equal", "KhimTools.TextAlign.Commands.CmdAlignHorizontalEquals", assemblyPath, "icon_align_16.png", GenPanelName);
                SafeAddPulldownItem(layoutPulldown, "CmdAlignVerticalEquals", "Align Text - Vert Equal", "KhimTools.TextAlign.Commands.CmdAlignVerticalEquals", assemblyPath, "icon_align_16.png", GenPanelName);
            }

            // ── CỤM 4: VIEW TOOLS (Large Pulldown Button) ──
            var viewToolsPulldownData = new PulldownButtonData("KhimViewToolsPulldown", "View Tools")
            {
                ToolTip = "Các công cụ nâng cao hỗ trợ tạo Section Box, Callout Pro và sinh View liên quan.",
                LargeImage = LoadImage("icon_sectionbox_32.png"),
                Image = LoadImage("icon_sectionbox_16.png")
            };
            var viewToolsPulldown = SafeAddItem(panel, viewToolsPulldownData, GenPanelName) as PulldownButton;
            if (viewToolsPulldown != null)
            {
                SafeAddPulldownItem(viewToolsPulldown, "CmdSectionBox", "Section Box Pro", "KhimTools.SectionBox.Commands.CmdSectionBox", assemblyPath, "icon_sectionbox_16.png", GenPanelName);
                SafeAddPulldownItem(viewToolsPulldown, "CmdCalloutPro", "Callout Pro", "KhimTools.CalloutPro.Commands.CmdCalloutPro", assemblyPath, "icon_callout_pro_16.png", GenPanelName);
                SafeAddPulldownItem(viewToolsPulldown, "CmdViewFromCallout", "Create View from Callout", "KhimTools.ViewFromCallout.Commands.CmdViewFromCallout", assemblyPath, "icon_view_callout_16.png", GenPanelName);
            }

            // ── CỤM 5: PUBLISH & SYSTEM ──
            // Sheet Exporter (Large Button)
            var sheetExportData = CreateSafePushButtonData(
                "CmdSheetExport",
                "Sheet" + Environment.NewLine + "Exporter",
                assemblyPath,
                "KhimTools.SheetExport.Commands.CmdSheetExport",
                GenPanelName,
                "Công cụ Batch Print & Export Sheet/View chuyên nghiệp (PDF, DWG, Issue Manager).",
                "export_sheet_32.png",
                "export_sheet_16.png",
                "Hỗ trợ Naming Templates với Regex validation, Issue Revision Diffing, " +
                "Tự động tạo file Excel Transmittal Register & QA Technical Log, " +
                "PDFsharp Bookmarks, Watermark Status Stamp, Cover Sheet, và Auto-Retry.");
            SafeAddItem(panel, sheetExportData, GenPanelName);

            // Elements Tags (Large Button)
            var elementTagsData = CreateSafePushButtonData(
                "CmdElementTags",
                "Elements" + Environment.NewLine + "Tags",
                assemblyPath,
                "KhimTools.ElementTags.Commands.CmdElementTags",
                GenPanelName,
                "Quản lý và gán thẻ Tag hàng loạt cho các đối tượng trong View hiện hành.",
                "icon_mep_tags_32.png",
                "icon_mep_tags_16.png");
            SafeAddItem(panel, elementTagsData, GenPanelName);

            // Stack 3: Language & Check Update
            var splitLangData = new PulldownButtonData(
                "LanguagePulldown",
                "Ngôn ngữ (Lang)")
            {
                ToolTip = "Chuyển đổi ngôn ngữ giao diện (Song ngữ Tiếng Việt - English).",
                Image = LoadImage("icon_workspace_16.png")
            };

            var updateData = CreateSafePushButtonData(
                "CmdCheckUpdate",
                "Check Update",
                assemblyPath,
                "KhimTools.Updater.Commands.CmdCheckUpdate",
                GenPanelName,
                "Kiểm tra phiên bản mới nhất của KhimTools từ GitHub Releases.",
                null,
                "icon_update_16.png");

            var stackedSystem = SafeAddStackedItems(panel, splitLangData, updateData, GenPanelName);
            if (stackedSystem != null && stackedSystem.Count == 2)
            {
                var pLang = stackedSystem[0] as PulldownButton;
                if (pLang != null)
                {
                    SafeAddPulldownItem(pLang, "CmdSwitchLanguage", "Đổi Ngôn Ngữ (Switch)", "KhimTools.LanguageSwitcher.Commands.CmdSwitchLanguage", assemblyPath, "icon_workspace_16.png", GenPanelName);
                    try { pLang.AddSeparator(); } catch { }
                    SafeAddPulldownItem(pLang, "CmdSetVietnamese", "Tiếng Việt (VN)", "KhimTools.LanguageSwitcher.Commands.CmdSetVietnamese", assemblyPath, "icon_workspace_16.png", GenPanelName);
                    SafeAddPulldownItem(pLang, "CmdSetEnglish", "English (EN)", "KhimTools.LanguageSwitcher.Commands.CmdSetEnglish", assemblyPath, "icon_workspace_16.png", GenPanelName);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 2. PANEL: OVERRIDE (MATCH SCREENSHOT: 3x3 COLOR PALETTE + HALFTONE + RESET + SETTING)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildOverridePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, OverridePanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(OverridePanelName, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // ── STACK 1: ĐỎ, CAM, VÀNG ──
            var redData = CreateColorSwatchData("CmdOverrideRed", "KhimTools.OverrideTool.Commands.CmdOverrideRed", assemblyPath, "override_red_16.png", "Gán màu Đỏ (Red) cho đối tượng đang chọn");
            var orangeData = CreateColorSwatchData("CmdOverrideOrange", "KhimTools.OverrideTool.Commands.CmdOverrideOrange", assemblyPath, "override_orange_16.png", "Gán màu Cam (Orange) cho đối tượng đang chọn");
            var yellowData = CreateColorSwatchData("CmdOverrideYellow", "KhimTools.OverrideTool.Commands.CmdOverrideYellow", assemblyPath, "override_yellow_16.png", "Gán màu Vàng (Yellow) cho đối tượng đang chọn");
            SafeAddStackedItems(panel, redData, orangeData, yellowData, OverridePanelName);

            // ── STACK 2: XANH LÁ, CYAN, XANH DƯƠNG ──
            var greenData = CreateColorSwatchData("CmdOverrideGreen", "KhimTools.OverrideTool.Commands.CmdOverrideGreen", assemblyPath, "override_green_16.png", "Gán màu Xanh lá (Green) cho đối tượng đang chọn");
            var cyanData = CreateColorSwatchData("CmdOverrideCyan", "KhimTools.OverrideTool.Commands.CmdOverrideCyan", assemblyPath, "override_cyan_16.png", "Gán màu Xanh lơ (Cyan) cho đối tượng đang chọn");
            var blueData = CreateColorSwatchData("CmdOverrideBlue", "KhimTools.OverrideTool.Commands.CmdOverrideBlue", assemblyPath, "override_blue_16.png", "Gán màu Xanh dương (Blue) cho đối tượng đang chọn");
            SafeAddStackedItems(panel, greenData, cyanData, blueData, OverridePanelName);

            // ── STACK 3: MAGENTA, XÁM, TÙY CHỌN (GRADIENT) ──
            var magentaData = CreateColorSwatchData("CmdOverrideMagenta", "KhimTools.OverrideTool.Commands.CmdOverrideMagenta", assemblyPath, "override_magenta_16.png", "Gán màu Hồng cánh sen (Magenta) cho đối tượng đang chọn");
            var grayData = CreateColorSwatchData("CmdOverrideGray", "KhimTools.OverrideTool.Commands.CmdOverrideGray", assemblyPath, "override_gray_16.png", "Gán màu Xám (Gray) cho đối tượng đang chọn");
            var customData = CreateColorSwatchData("CmdOverrideCustom", "KhimTools.OverrideTool.Commands.CmdOverrideCustom", assemblyPath, "override_custom_16.png", "Chọn màu tùy chỉnh từ bảng màu (Custom Color Picker)");
            SafeAddStackedItems(panel, magentaData, grayData, customData, OverridePanelName);

            // ── LARGE BUTTON 1: ON/OFF HALFTONE ──
            var halftoneData = CreateSafePushButtonData(
                "CmdQuickHalftone",
                "On/Off" + Environment.NewLine + "Halftone",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdQuickHalftone",
                OverridePanelName,
                "Bật/Tắt nhanh chế độ mờ Halftone 50% cho đối tượng đang chọn.",
                "override_halftone_32.png",
                "override_halftone_16.png");
            SafeAddItem(panel, halftoneData, OverridePanelName);

            // ── LARGE BUTTON 2: RESET OVERRIDE ──
            var resetData = CreateSafePushButtonData(
                "CmdQuickResetOverride",
                "Reset" + Environment.NewLine + "Override",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdQuickResetOverride",
                OverridePanelName,
                "Xóa toàn bộ màu sắc, đường nét, halftone đã override của đối tượng đang chọn.",
                "override_reset_32.png",
                "override_reset_16.png");
            SafeAddItem(panel, resetData, OverridePanelName);

            // ── LARGE BUTTON 3: SETTING COLOR ──
            var settingData = CreateSafePushButtonData(
                "CmdGraphicOverdrive",
                "Setting" + Environment.NewLine + "Color",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdGraphicOverdrive",
                OverridePanelName,
                "Mở bảng điều khiển Graphic Overdrive chi tiết (Độ trong suốt Transparency, Nét vẽ Line Weight, 12 Presets màu).",
                "override_setting_32.png",
                "override_setting_16.png");
            SafeAddItem(panel, settingData, OverridePanelName);
        }

        private static PushButtonData CreateColorSwatchData(string id, string className, string assemblyPath, string iconName, string tooltip)
        {
            // ROOT CAUSE FIX: Dùng Zero-Width Space ("\u200B") thay cho " " (whitespace)
            // Revit API không cho phép text rỗng/whitespace, nhưng chấp nhận "\u200B"
            // Giúp hiển thị ô swatch icon-only không nhãn chữ chuẩn xác tuyệt đối.
            return CreateSafePushButtonData(
                id,
                ZeroWidthSpace,
                assemblyPath,
                className,
                OverridePanelName,
                tooltip,
                iconName,
                iconName);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 3. PANEL: K-STRUCTURAL
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildStructuralPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, StructuralPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(StructuralPanelName, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. SplitButton: Column Rebar
            var splitButtonData = new SplitButtonData(
                "ColumnRebarSplitButton",
                "Column" + Environment.NewLine + "Rebar")
            {
                ToolTip = "Bố trí thép cột tự động (phát hiện vuông/tròn từ phần tử đang chọn)."
            };

            var splitButton = SafeAddItem(panel, splitButtonData, StructuralPanelName) as SplitButton;
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

                try { splitButton.AddSeparator(); } catch { }

                SafeAddSplitButtonItem(splitButton, "CmdColumnDrawing", "Column Drawing",
                    "KhimTools.RebarTool.Commands.CmdColumnDrawing", assemblyPath,
                    "Tự động xuất bản vẽ mặt cắt 2D & thống kê thép cột.",
                    "rebar_col_32.png", "rebar_draw_16.png", StructuralPanelName);

                SafeAddSplitButtonItem(splitButton, "CmdUpdateColumnDrawing", "Update Drawing",
                    "KhimTools.RebarTool.Commands.CmdUpdateColumnDrawing", assemblyPath,
                    "Đồng bộ cập nhật lại bản vẽ 2D đã xuất theo mô hình thép mới nhất.",
                    "rebar_col_32.png", "rebar_draw_16.png", StructuralPanelName);
            }

            // 2. Beam Rebar
            var beamData = CreateSafePushButtonData(
                "CmdBeamRebar",
                "Beam" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdBeamRebar",
                StructuralPanelName,
                "Bố trí thép dầm (Beam Rebar v2.0) chuẩn kết cấu TCVN & Eurocode.",
                "rebar_beam_32.png",
                "rebar_beam_16.png",
                "Hỗ trợ thép chủ chạy suốt (top/bottom), thép gia cường gối L/3, " +
                "thép gia cường bụng L/6, thép sườn (skin bars), đai phân vùng A1/A2/A1 và đai treo dầm phụ.");
            SafeAddItem(panel, beamData, StructuralPanelName);

            // 3. Slab Rebar
            var slabData = CreateSafePushButtonData(
                "CmdSlabRebar",
                "Slab" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdSlabRebar",
                StructuralPanelName,
                "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                "rebar_slab_32.png",
                "rebar_slab_16.png");
            SafeAddItem(panel, slabData, StructuralPanelName);

            // 4. Foundation Rebar
            var fdnData = CreateSafePushButtonData(
                "CmdFoundationRebar",
                "Foundation" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdFoundationRebar",
                StructuralPanelName,
                "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                "rebar_fdn_32.png",
                "rebar_fdn_16.png");
            SafeAddItem(panel, fdnData, StructuralPanelName);

            // 5. Section Cut
            var sectionData = CreateSafePushButtonData(
                "CmdSectionCut",
                "Section" + Environment.NewLine + "Cut",
                assemblyPath,
                "KhimTools.SectionCutTool.Commands.CmdSectionCut",
                StructuralPanelName,
                "Tự động tạo mặt cắt dọc & ngang (Section Views) phục vụ bản vẽ thép.",
                "icon_section_cut_32.png",
                "icon_section_cut_16.png");
            SafeAddItem(panel, sectionData, StructuralPanelName);

            // 6. Cover Setup
            var coverData = CreateSafePushButtonData(
                "CmdProjectCoverSetup",
                "Cover" + Environment.NewLine + "Setup",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdProjectCoverSetup",
                StructuralPanelName,
                "Cấu hình Lớp bê tông bảo vệ (Concrete Cover) toàn dự án.",
                "icon_cover_setup_32.png",
                "icon_cover_setup_16.png");
            SafeAddItem(panel, coverData, StructuralPanelName);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 4. PANEL: K-ARCHITECTURAL
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildArchPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ArchPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(ArchPanelName, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. Room 3D View
            var room3dData = CreateSafePushButtonData(
                "CmdRoom3DView",
                "Room 3D" + Environment.NewLine + "View",
                assemblyPath,
                "KhimTools.Architectural.Rooms.CmdRoom3DView",
                ArchPanelName,
                "Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.",
                "icon_room3d_32.png",
                "icon_room3d_16.png");
            SafeAddItem(panel, room3dData, ArchPanelName);

            // 2. Room Finishes
            var finishData = CreateSafePushButtonData(
                "CmdWallFloorFinishes",
                "Room" + Environment.NewLine + "Finishes",
                assemblyPath,
                "KhimTools.Architectural.Finishes.CmdWallFloorFinishes",
                ArchPanelName,
                "Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng.",
                "icon_finishes_32.png",
                "icon_finishes_16.png");
            SafeAddItem(panel, finishData, ArchPanelName);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 5. PANEL: K-MEP
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildMepPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, MepPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(MepPanelName, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. MEP Openings
            var openingData = CreateSafePushButtonData(
                "CmdMepOpenings",
                "MEP" + Environment.NewLine + "Openings",
                assemblyPath,
                "KhimTools.MEP.Penetrations.CmdMepOpenings",
                MepPanelName,
                "Tự động kiểm tra xung đột ống MEP với Dầm/Sàn/Vách và đục lỗ mở (Openings).",
                "icon_mep_openings_32.png",
                "icon_mep_openings_16.png");
            SafeAddItem(panel, openingData, MepPanelName);

            // 2. MEP Elevation Tags
            var tagData = CreateSafePushButtonData(
                "CmdMepElevationTags",
                "Elevation" + Environment.NewLine + "Tags",
                assemblyPath,
                "KhimTools.MEP.Tags.CmdMepElevationTags",
                MepPanelName,
                "Tự động gán nhãn cao độ đáy (BOP/Invert Elevation) cho ống gió và ống nước.",
                "icon_mep_tags_32.png",
                "icon_mep_tags_16.png");
            SafeAddItem(panel, tagData, MepPanelName);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // SAFE REGISTRATION HELPERS (FAULT TOLERANCE & SANITIZATION)
        // ════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tạo PushButtonData với cơ chế làm sạch tên và nhãn hiển thị (Sanitization).
        /// Đảm bảo không bao giờ ném ArgumentException rỗng của Revit.
        /// </summary>
        public static PushButtonData CreateSafePushButtonData(
            string name,
            string text,
            string assemblyPath,
            string className,
            string moduleName,
            string toolTip = null,
            string largeIcon = null,
            string smallIcon = null,
            string longDescription = null)
        {
            string safeName = string.IsNullOrWhiteSpace(name) ? ("Btn_" + Guid.NewGuid().ToString("N").Substring(0, 8)) : name.Trim();
            string safeText = SanitizeButtonText(text, safeName);

            var data = new PushButtonData(safeName, safeText, assemblyPath, className);

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
                return ZeroWidthSpace;
            }

            // Nếu chuỗi chỉ toàn khoảng trắng thường, thay bằng ZeroWidthSpace để không crash Revit
            if (text.Trim().Length == 0)
            {
                return ZeroWidthSpace;
            }

            return text;
        }

        public static RibbonItem SafeAddItem(RibbonPanel panel, RibbonItemData itemData, string moduleName)
        {
            if (panel == null || itemData == null) return null;

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
                RegistrationDiagnostics.RecordError(moduleName, $"Không thể thêm nút [{itemData.Name}] vào panel [{panel.Name}]", ex);
                return null;
            }
        }

        public static System.Collections.Generic.IList<RibbonItem> SafeAddStackedItems(
            RibbonPanel panel, RibbonItemData item1, RibbonItemData item2, string moduleName)
        {
            if (panel == null || item1 == null || item2 == null) return null;

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
                RegistrationDiagnostics.RecordError(moduleName, $"Không thể thêm 2 stacked items [{item1.Name}, {item2.Name}]", ex);
                return null;
            }
        }

        public static System.Collections.Generic.IList<RibbonItem> SafeAddStackedItems(
            RibbonPanel panel, RibbonItemData item1, RibbonItemData item2, RibbonItemData item3, string moduleName)
        {
            if (panel == null || item1 == null || item2 == null || item3 == null) return null;

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
                RegistrationDiagnostics.RecordError(moduleName, $"Không thể thêm 3 stacked items [{item1.Name}, {item2.Name}, {item3.Name}]", ex);
                return null;
            }
        }

        public static PushButton SafeAddPulldownItem(PulldownButton pulldown, string name, string text,
            string className, string assemblyPath, string smallIconName, string moduleName)
        {
            if (pulldown == null) return null;

            try
            {
                var data = CreateSafePushButtonData(name, text, assemblyPath, className, moduleName,
                    "Bật/Tắt hiển thị hoặc căn chỉnh đối tượng trong Active View.", null, smallIconName);

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
                RegistrationDiagnostics.RecordError(moduleName, $"Không thể thêm pulldown item [{name}]", ex);
                return null;
            }
        }

        public static PushButton SafeAddSplitButtonItem(SplitButton splitButton, string name, string text,
            string className, string assemblyPath, string toolTip, string largeIconName, string smallIconName, string moduleName)
        {
            if (splitButton == null) return null;

            try
            {
                var data = CreateSafePushButtonData(name, text, assemblyPath, className, moduleName,
                    toolTip, largeIconName, smallIconName);

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
                RegistrationDiagnostics.RecordError(moduleName, $"Không thể thêm split button item [{name}]", ex);
                return null;
            }
        }

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
            try
            {
                var panels = app.GetRibbonPanels(tabName);
                var existing = panels?.FirstOrDefault(p => p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;
            }
            catch { }

            try
            {
                return app.CreateRibbonPanel(tabName, panelName);
            }
            catch
            {
                try
                {
                    return app.GetRibbonPanels(tabName)?.FirstOrDefault(p => p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
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
            catch { }

            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) return loc;
            }
            catch { }

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
            }
            catch { }

            return null;
        }
    }
}
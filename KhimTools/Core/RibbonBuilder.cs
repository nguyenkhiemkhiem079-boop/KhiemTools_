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
    /// K-TOOLS RIBBON 2.0 ARCHITECTURE
    /// Tab: "K-TOOLS"
    /// 
    /// Phân chia thành 6 cụm Panel chuyên môn theo kiến trúc thông tin chuẩn:
    ///   1. WORKSPACE  (Khim Workspace, Family Manager, Settings Hub / Language, Update)
    ///   2. GENERAL    (Model Tools, View Tools, Visibility, Layout, Graphic Override, Sheet Exporter, Element Tags)
    ///   3. STRUCTURE  (Quick Structure SplitButton, Section Cut, Cover Setup)
    ///   4. REBAR      (CREATE: Column/Beam/Slab/Foundation Rebar | DETAIL: Column Drawing, Update Drawing)
    ///   5. ARCHI      (Room 3D View, Room Finishes)
    ///   6. MEP        (MEP Openings, Elevation Tags)
    ///
    /// NGUYÊN TẮC BẢO TOÀN & BẢO VỆ CÁCH LY LỖI:
    ///   - 100% bảo toàn 90 Command ID, Command Class, và hành vi gốc.
    ///   - Mỗi Panel khởi tạo trong sandbox độc lập (RegisterPanelModule).
    ///   - Giảm mật độ ngang (density reduction) bằng SplitButton, Pulldown, và Stacked Items.
    /// </summary>
    public static class RibbonBuilder
    {
        public const string TabName = "K-TOOLS";
        public const string WorkspacePanelName = "WORKSPACE";
        public const string GeneralPanelName = "GENERAL";
        public const string StructurePanelName = "STRUCTURE";
        public const string RebarPanelName = "REBAR";
        public const string ArchiPanelName = "ARCHI";
        public const string MepPanelName = "MEP";

        /// <summary>
        /// Ký tự Zero-Width Space (Unicode U+200B).
        /// Tránh lỗi ArgumentException của Revit API khi nhãn nút là chuỗi rỗng.
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

            // 1. WORKSPACE (Workspace, Discovery & High-level Project Utilities)
            RegisterPanelModule(WorkspacePanelName, () => BuildWorkspacePanel(application, assemblyPath));

            // 2. GENERAL (Model, View, Visibility, Layout, Override, Export, Tags)
            RegisterPanelModule(GeneralPanelName, () => BuildGeneralPanel(application, assemblyPath));

            // 3. STRUCTURE (Quick Structure, Section Cut, Cover Setup)
            RegisterPanelModule(StructurePanelName, () => BuildStructurePanel(application, assemblyPath));

            // 4. REBAR (CREATE & DETAIL Rebar Suite)
            RegisterPanelModule(RebarPanelName, () => BuildRebarPanel(application, assemblyPath));

            // 5. ARCHI (Room 3D View & Room Finishes)
            RegisterPanelModule(ArchiPanelName, () => BuildArchiPanel(application, assemblyPath));

            // 6. MEP (MEP Openings & Elevation Tags)
            RegisterPanelModule(MepPanelName, () => BuildMepPanel(application, assemblyPath));

            // Ghi nhật ký diagnostics hoàn chỉnh ra AppData
            RegistrationDiagnostics.PersistLog();
        }

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
        // 1. PANEL: WORKSPACE (PANEL CHÍNH ĐIỀU HƯỚNG & CẤU HÌNH DỰ ÁN)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildWorkspacePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, WorkspacePanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(WorkspacePanelName, WorkspacePanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. Khim Workspace (Primary Large Button)
            var wsData = CreateSafePushButtonData(
                "CmdToggleWorkspace",
                "Khim" + Environment.NewLine + "Workspace",
                assemblyPath,
                "KhimTools.Workspace.Commands.CmdToggleWorkspace",
                WorkspacePanelName,
                "Khim Workspace",
                "Bật/Tắt bảng điều khiển Khim Workspace (Dockable Pane).",
                "icon_workspace_32.png",
                "icon_workspace_16.png");
            SafeAddItem(panel, wsData, WorkspacePanelName, "Khim Workspace", "KhimTools.Workspace.Commands.CmdToggleWorkspace");

            // 2. Family Manager (Large Button - High-level Project Utility)
            var famMgrData = CreateSafePushButtonData(
                "CmdFamilyManager",
                "Family" + Environment.NewLine + "Manager",
                assemblyPath,
                "KhimTools.FamilyManager.Commands.CmdFamilyManager",
                WorkspacePanelName,
                "Family Manager",
                "Quản lý và nạp Family thư viện KhimTools cho tất cả các bộ môn.",
                "icon_workspace_32.png",
                "icon_workspace_16.png");
            SafeAddItem(panel, famMgrData, WorkspacePanelName, "Family Manager", "KhimTools.FamilyManager.Commands.CmdFamilyManager");

            // 3. Stacked: Settings (Language Switcher) / Check Update
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
                WorkspacePanelName,
                "Check Update",
                "Kiểm tra phiên bản mới nhất của KhimTools từ GitHub Releases.",
                null,
                "icon_update_16.png");

            var stackedSystem = SafeAddStackedItems(panel, settingsPulldownData, updateData, WorkspacePanelName, "Settings Hub / Update");
            if (stackedSystem != null && stackedSystem.Count == 2)
            {
                var pSettings = stackedSystem[0] as PulldownButton;
                if (pSettings != null)
                {
                    SafeAddPulldownItem(pSettings, "CmdSwitchLanguage", "Đổi Ngôn Ngữ (Switch)", "KhimTools.LanguageSwitcher.Commands.CmdSwitchLanguage", assemblyPath, "icon_workspace_16.png", WorkspacePanelName);
                    SafeAddSeparator(pSettings, WorkspacePanelName);
                    SafeAddPulldownItem(pSettings, "CmdSetVietnamese", "Tiếng Việt (VN)", "KhimTools.LanguageSwitcher.Commands.CmdSetVietnamese", assemblyPath, "icon_workspace_16.png", WorkspacePanelName);
                    SafeAddPulldownItem(pSettings, "CmdSetEnglish", "English (EN)", "KhimTools.LanguageSwitcher.Commands.CmdSetEnglish", assemblyPath, "icon_workspace_16.png", WorkspacePanelName);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 2. PANEL: GENERAL (CÔNG CỤ HÌNH HỌC, HIỂN THỊ, BỐ CỤC, MÀU SẮC & XUẤT BẢN)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildGeneralPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, GeneralPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(GeneralPanelName, GeneralPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // ── CỤM 1: MODEL TOOLS & VIEW TOOLS (STACKED) ──
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

            var stackedModelView = SafeAddStackedItems(panel, modelToolsData, viewToolsData, GeneralPanelName, "Model & View Tools Stack");
            if (stackedModelView != null && stackedModelView.Count == 2)
            {
                var pModel = stackedModelView[0] as PulldownButton;
                var pView = stackedModelView[1] as PulldownButton;

                if (pModel != null)
                {
                    SafeAddPulldownItem(pModel, "CmdJoinElements", "Join Elements", "KhimTools.SlabJoin.Commands.CmdJoinElements", assemblyPath, "icon_join_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pModel, "CmdCopyLinkElements", "Copy Link Elements", "KhimTools.CopyLink.Commands.CmdCopyLinkElements", assemblyPath, "icon_copylink_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pModel, "CmdSlabStep", "Slab Step Generator", "KhimTools.SlabStep.Commands.CmdSlabStep", assemblyPath, "icon_join_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pModel, "CmdGridPlanGenerator", "Grid & Floor Plan", "KhimTools.GridLevel.Commands.CmdAutoGridPlan", assemblyPath, "icon_grid_plan_16.png", GeneralPanelName);
                }

                if (pView != null)
                {
                    SafeAddPulldownItem(pView, "CmdSectionBox", "Section Box Pro", "KhimTools.SectionBox.Commands.CmdSectionBox", assemblyPath, "icon_sectionbox_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pView, "CmdCalloutPro", "Callout Pro", "KhimTools.CalloutPro.Commands.CmdCalloutPro", assemblyPath, "icon_callout_pro_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pView, "CmdViewFromCallout", "Create View from Callout", "KhimTools.ViewFromCallout.Commands.CmdViewFromCallout", assemblyPath, "icon_view_callout_16.png", GeneralPanelName);
                }
            }

            // ── CỤM 2: VISIBILITY & LAYOUT (STACKED) ──
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

            var stackedVisLayout = SafeAddStackedItems(panel, visPulldownData, layoutPulldownData, GeneralPanelName, "Visibility & Layout Stack");
            if (stackedVisLayout != null && stackedVisLayout.Count == 2)
            {
                var pVis = stackedVisLayout[0] as PulldownButton;
                var pLayout = stackedVisLayout[1] as PulldownButton;

                if (pVis != null)
                {
                    // Architectural
                    SafeAddPulldownItem(pVis, "CmdShowWindow", "Hiển thị Window", "KhimTools.VisibilityTool.Commands.CmdShowWindow", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideWindow", "Ẩn Window", "KhimTools.VisibilityTool.Commands.CmdHideWindow", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowDoor", "Hiển thị Door", "KhimTools.VisibilityTool.Commands.CmdShowDoor", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideDoor", "Ẩn Door", "KhimTools.VisibilityTool.Commands.CmdHideDoor", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowCeiling", "Hiển thị Ceiling", "KhimTools.VisibilityTool.Commands.CmdShowCeiling", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideCeiling", "Ẩn Ceiling", "KhimTools.VisibilityTool.Commands.CmdHideCeiling", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowRoof", "Hiển thị Roof", "KhimTools.VisibilityTool.Commands.CmdShowRoof", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideRoof", "Ẩn Roof", "KhimTools.VisibilityTool.Commands.CmdHideRoof", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowStair", "Hiển thị Stair", "KhimTools.VisibilityTool.Commands.CmdShowStair", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideStair", "Ẩn Stair", "KhimTools.VisibilityTool.Commands.CmdHideStair", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowRailing", "Hiển thị Railing", "KhimTools.VisibilityTool.Commands.CmdShowRailing", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideRailing", "Ẩn Railing", "KhimTools.VisibilityTool.Commands.CmdHideRailing", assemblyPath, "icon_detail_16.png", GeneralPanelName);

                    SafeAddSeparator(pVis, GeneralPanelName);

                    // Structural
                    SafeAddPulldownItem(pVis, "CmdShowColumn", "Hiển thị Column", "KhimTools.VisibilityTool.Commands.CmdShowColumn", assemblyPath, "rebar_col_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideColumn", "Ẩn Column", "KhimTools.VisibilityTool.Commands.CmdHideColumn", assemblyPath, "rebar_col_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowFraming", "Hiển thị Framing", "KhimTools.VisibilityTool.Commands.CmdShowFraming", assemblyPath, "rebar_beam_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideFraming", "Ẩn Framing", "KhimTools.VisibilityTool.Commands.CmdHideFraming", assemblyPath, "rebar_beam_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowFloor", "Hiển thị Floor", "KhimTools.VisibilityTool.Commands.CmdShowFloor", assemblyPath, "rebar_slab_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideFloor", "Ẩn Floor", "KhimTools.VisibilityTool.Commands.CmdHideFloor", assemblyPath, "rebar_slab_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowWall", "Hiển thị Wall", "KhimTools.VisibilityTool.Commands.CmdShowWall", assemblyPath, "icon_join_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideWall", "Ẩn Wall", "KhimTools.VisibilityTool.Commands.CmdHideWall", assemblyPath, "icon_join_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowFoundation", "Hiển thị Foundation", "KhimTools.VisibilityTool.Commands.CmdShowFoundation", assemblyPath, "rebar_fdn_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideFoundation", "Ẩn Foundation", "KhimTools.VisibilityTool.Commands.CmdHideFoundation", assemblyPath, "rebar_fdn_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowRebar", "Hiển thị Rebar", "KhimTools.VisibilityTool.Commands.CmdShowRebar", assemblyPath, "rebar_draw_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideRebar", "Ẩn Rebar", "KhimTools.VisibilityTool.Commands.CmdHideRebar", assemblyPath, "rebar_draw_16.png", GeneralPanelName);

                    SafeAddSeparator(pVis, GeneralPanelName);

                    // Documentation
                    SafeAddPulldownItem(pVis, "CmdShowGrid", "Hiển thị Grid", "KhimTools.VisibilityTool.Commands.CmdShowGrid", assemblyPath, "icon_grid_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideGrid", "Ẩn Grid", "KhimTools.VisibilityTool.Commands.CmdHideGrid", assemblyPath, "icon_grid_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowLevel", "Hiển thị Level", "KhimTools.VisibilityTool.Commands.CmdShowLevel", assemblyPath, "icon_grid_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideLevel", "Ẩn Level", "KhimTools.VisibilityTool.Commands.CmdHideLevel", assemblyPath, "icon_grid_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowSection", "Hiển thị Section", "KhimTools.VisibilityTool.Commands.CmdShowSection", assemblyPath, "rebar_draw_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideSection", "Ẩn Section", "KhimTools.VisibilityTool.Commands.CmdHideSection", assemblyPath, "rebar_draw_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowElevation", "Hiển thị Elevation", "KhimTools.VisibilityTool.Commands.CmdShowElevation", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideElevation", "Ẩn Elevation", "KhimTools.VisibilityTool.Commands.CmdHideElevation", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdShowTag", "Hiển thị Tag", "KhimTools.VisibilityTool.Commands.CmdShowTag", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pVis, "CmdHideTag", "Ẩn Tag", "KhimTools.VisibilityTool.Commands.CmdHideTag", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                }

                if (pLayout != null)
                {
                    SafeAddPulldownItem(pLayout, "CmdSheetGen", "Create Sheets (CSV)", "KhimTools.SheetGen.Commands.CmdSheetGen", assemblyPath, "export_sheet_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignViewport", "Align Viewports", "KhimTools.ViewportAlign.Commands.CmdAlignViewport", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdUpdateDetailNumbers", "Update Detail No", "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers", assemblyPath, "icon_detail_16.png", GeneralPanelName);
                    SafeAddSeparator(pLayout, GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignTop", "Align Text - Top", "KhimTools.TextAlign.Commands.CmdAlignTop", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignBottom", "Align Text - Bottom", "KhimTools.TextAlign.Commands.CmdAlignBottom", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignLeft", "Align Text - Left", "KhimTools.TextAlign.Commands.CmdAlignLeft", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignRight", "Align Text - Right", "KhimTools.TextAlign.Commands.CmdAlignRight", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignMiddle", "Align Text - Middle", "KhimTools.TextAlign.Commands.CmdAlignMiddle", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignHorizontalEquals", "Align Text - Horiz Equal", "KhimTools.TextAlign.Commands.CmdAlignHorizontalEquals", assemblyPath, "icon_align_16.png", GeneralPanelName);
                    SafeAddPulldownItem(pLayout, "CmdAlignVerticalEquals", "Align Text - Vert Equal", "KhimTools.TextAlign.Commands.CmdAlignVerticalEquals", assemblyPath, "icon_align_16.png", GeneralPanelName);
                }
            }

            // ── CỤM 3: GRAPHIC OVERRIDE (PULLDOWN - COMPACT & PRESERVED) ──
            var overridePulldownData = new PulldownButtonData("KhimOverridePulldown", "Override")
            {
                ToolTip = "Công cụ gán màu sắc hiển thị, Halftone và Overdrive đồ họa cho các đối tượng.",
                Image = LoadImage("override_setting_16.png")
            };

            var overridePulldown = SafeAddItem(panel, overridePulldownData, GeneralPanelName, "Graphic Override Pulldown", string.Empty) as PulldownButton;
            if (overridePulldown != null)
            {
                SafeAddPulldownItem(overridePulldown, "CmdQuickHalftone", "On/Off Halftone", "KhimTools.OverrideTool.Commands.CmdQuickHalftone", assemblyPath, "override_halftone_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdQuickResetOverride", "Reset Override", "KhimTools.OverrideTool.Commands.CmdQuickResetOverride", assemblyPath, "override_reset_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdGraphicOverdrive", "Override Settings", "KhimTools.OverrideTool.Commands.CmdGraphicOverdrive", assemblyPath, "override_setting_16.png", GeneralPanelName);

                SafeAddSeparator(overridePulldown, GeneralPanelName);

                SafeAddPulldownItem(overridePulldown, "CmdOverrideRed", "Màu Đỏ (Red)", "KhimTools.OverrideTool.Commands.CmdOverrideRed", assemblyPath, "override_red_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideOrange", "Màu Cam (Orange)", "KhimTools.OverrideTool.Commands.CmdOverrideOrange", assemblyPath, "override_orange_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideYellow", "Màu Vàng (Yellow)", "KhimTools.OverrideTool.Commands.CmdOverrideYellow", assemblyPath, "override_yellow_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideGreen", "Màu Lá (Green)", "KhimTools.OverrideTool.Commands.CmdOverrideGreen", assemblyPath, "override_green_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideCyan", "Màu Xanh lơ (Cyan)", "KhimTools.OverrideTool.Commands.CmdOverrideCyan", assemblyPath, "override_cyan_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideBlue", "Màu Xanh dương (Blue)", "KhimTools.OverrideTool.Commands.CmdOverrideBlue", assemblyPath, "override_blue_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideMagenta", "Màu Hồng (Magenta)", "KhimTools.OverrideTool.Commands.CmdOverrideMagenta", assemblyPath, "override_magenta_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideGray", "Màu Xám (Gray)", "KhimTools.OverrideTool.Commands.CmdOverrideGray", assemblyPath, "override_gray_16.png", GeneralPanelName);
                SafeAddPulldownItem(overridePulldown, "CmdOverrideCustom", "Chọn Màu Tùy Chỉnh", "KhimTools.OverrideTool.Commands.CmdOverrideCustom", assemblyPath, "override_custom_16.png", GeneralPanelName);
            }

            // ── CỤM 4: PUBLISH & DOCUMENTATION (LARGE BUTTONS) ──
            var sheetExportData = CreateSafePushButtonData(
                "CmdSheetExport",
                "Sheet" + Environment.NewLine + "Exporter",
                assemblyPath,
                "KhimTools.SheetExport.Commands.CmdSheetExport",
                GeneralPanelName,
                "Sheet Exporter",
                "Công cụ Batch Print & Export Sheet/View chuyên nghiệp (PDF, DWG, Issue Manager).",
                "export_sheet_32.png",
                "export_sheet_16.png",
                longDescription: "Hỗ trợ Naming Templates với Regex validation, Issue Revision Diffing, " +
                "Tự động tạo file Excel Transmittal Register & QA Technical Log, " +
                "PDFsharp Bookmarks, Watermark Status Stamp, Cover Sheet, và Auto-Retry.");
            SafeAddItem(panel, sheetExportData, GeneralPanelName, "Sheet Exporter", "KhimTools.SheetExport.Commands.CmdSheetExport");

            var elementTagsData = CreateSafePushButtonData(
                "CmdElementTags",
                "Elements" + Environment.NewLine + "Tags",
                assemblyPath,
                "KhimTools.ElementTags.Commands.CmdElementTags",
                GeneralPanelName,
                "Elements Tags",
                "Quản lý và gán thẻ Tag hàng loạt cho các đối tượng trong View hiện hành.",
                "icon_mep_tags_32.png",
                "icon_mep_tags_16.png");
            SafeAddItem(panel, elementTagsData, GeneralPanelName, "Elements Tags", "KhimTools.ElementTags.Commands.CmdElementTags");
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 3. PANEL: STRUCTURE (QUICK STRUCTURE, SECTION CUT, COVER SETUP)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildStructurePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, StructurePanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(StructurePanelName, StructurePanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. Quick Structure SplitButton
            var quickSplitData = new SplitButtonData("QuickStructureSplitButton", "Quick" + Environment.NewLine + "Structure")
            {
                ToolTip = "Đặt nhanh các cấu kiện kết cấu cơ bản (Cột, Dầm, Móng, Tường, Sàn). Tự động kiểm tra và hướng dẫn nạp Family nếu thiếu."
            };

            var quickSplit = SafeAddItem(panel, quickSplitData, StructurePanelName, "Quick Structure SplitButton", string.Empty) as SplitButton;
            if (quickSplit != null)
            {
                SafeAddSplitButtonItem(quickSplit, "CmdQuickColumn", "Quick Column",
                    "KhimTools.QuickDraft.Commands.CmdQuickColumn", assemblyPath,
                    "Đặt Cột Kết Cấu nhanh. Nếu Family chưa nạp sẽ gợi ý tải ngay.",
                    "rebar_col_32.png", "rebar_col_16.png", StructurePanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickBeam", "Quick Beam",
                    "KhimTools.QuickDraft.Commands.CmdQuickBeam", assemblyPath,
                    "Đặt Dầm Kết Cấu nhanh. Nếu Family chưa nạp sẽ gợi ý tải ngay.",
                    "rebar_beam_32.png", "rebar_beam_16.png", StructurePanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickFoundation", "Quick Foundation",
                    "KhimTools.QuickDraft.Commands.CmdQuickFoundation", assemblyPath,
                    "Đặt Móng Kết Cấu nhanh. Nếu Family chưa nạp sẽ gợi ý tải ngay.",
                    "rebar_fdn_32.png", "rebar_fdn_16.png", StructurePanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickWall", "Quick Wall",
                    "KhimTools.QuickDraft.Commands.CmdQuickWall", assemblyPath,
                    "Kích hoạt lệnh tạo Tường Kết Cấu nhanh (Structural Wall).",
                    "icon_cover_setup_32.png", "icon_cover_setup_16.png", StructurePanelName);

                SafeAddSplitButtonItem(quickSplit, "CmdQuickSlab", "Quick Slab",
                    "KhimTools.QuickDraft.Commands.CmdQuickSlab", assemblyPath,
                    "Kích hoạt lệnh tạo Sàn Kết Cấu nhanh (Structural Floor).",
                    "rebar_slab_32.png", "rebar_slab_16.png", StructurePanelName);
            }

            // 2. Section Cut (Primary Large Button)
            var sectionCutData = CreateSafePushButtonData(
                "CmdSectionCut",
                "Section" + Environment.NewLine + "Cut",
                assemblyPath,
                "KhimTools.SectionCutTool.Commands.CmdSectionCut",
                StructurePanelName,
                "Section Cut",
                "Tạo và quản lý mặt cắt cấu kiện kết cấu chuyên nghiệp.",
                "icon_section_cut_32.png",
                "icon_section_cut_16.png");
            SafeAddItem(panel, sectionCutData, StructurePanelName, "Section Cut", "KhimTools.SectionCutTool.Commands.CmdSectionCut");

            // 3. Cover Setup (Primary Large Button)
            var coverSetupData = CreateSafePushButtonData(
                "CmdProjectCoverSetup",
                "Cover" + Environment.NewLine + "Setup",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdProjectCoverSetup",
                StructurePanelName,
                "Cover Setup",
                "Cấu hình lớp bê tông bảo vệ theo tiêu chuẩn Eurocode & TCVN.",
                "icon_cover_setup_32.png",
                "icon_cover_setup_16.png");
            SafeAddItem(panel, coverSetupData, StructurePanelName, "Cover Setup", "KhimTools.RebarTool.Commands.CmdProjectCoverSetup");
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 4. PANEL: REBAR (CREATE & DETAIL REBAR SUITE)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildRebarPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, RebarPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(RebarPanelName, RebarPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // ── CỤM CREATE: 4 BỘ TẠO THÉP CHÍNH ──

            // 1. Column Rebar SplitButton
            var colSplitData = new SplitButtonData(
                "ColumnRebarSplitButton",
                "Column" + Environment.NewLine + "Rebar")
            {
                ToolTip = "Bố trí thép cột tự động (phát hiện vuông/tròn từ phần tử đang chọn)."
            };

            var colSplit = SafeAddItem(panel, colSplitData, RebarPanelName, "Column Rebar SplitButton", string.Empty) as SplitButton;
            if (colSplit != null)
            {
                SafeAddSplitButtonItem(colSplit, "CmdColumnRebar", "Column Rebar (Auto-detect)",
                    "KhimTools.RebarTool.Commands.CmdColumnRebar", assemblyPath,
                    "Tự động phát hiện loại cột (vuông/tròn) và mở giao diện phù hợp.",
                    "rebar_col_32.png", "rebar_col_16.png", RebarPanelName);

                SafeAddSplitButtonItem(colSplit, "CmdMultiColumnRebar", "Cột Vuông / Chữ Nhật 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột vuông/chữ nhật.",
                    "rebar_col_32.png", "rebar_col_rect_16.png", RebarPanelName);

                SafeAddSplitButtonItem(colSplit, "CmdMultiRoundColumnRebar", "Cột Tròn 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiRoundColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột tròn.",
                    "rebar_col_circ_32.png", "rebar_col_circ_16.png", RebarPanelName);
            }

            // 2. Beam Rebar (Large Button)
            var beamData = CreateSafePushButtonData(
                "CmdBeamRebar",
                "Beam" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdBeamRebar",
                RebarPanelName,
                "Beam Rebar",
                "Bố trí thép dầm (Beam Rebar v2.0) chuẩn kết cấu TCVN & Eurocode.",
                "rebar_beam_32.png",
                "rebar_beam_16.png",
                longDescription: "Hỗ trợ thép chủ chạy suốt (top/bottom), thép gia cường gối L/3, " +
                "thép gia cường bụng L/6, thép sườn (skin bars), đai phân vùng A1/A2/A1 và đai treo dầm phụ.");
            SafeAddItem(panel, beamData, RebarPanelName, "Beam Rebar", "KhimTools.RebarTool.Commands.CmdBeamRebar");

            // 3. Slab Rebar (Large Button)
            var slabData = CreateSafePushButtonData(
                "CmdSlabRebar",
                "Slab" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdSlabRebar",
                RebarPanelName,
                "Slab Rebar",
                "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                "rebar_slab_32.png",
                "rebar_slab_16.png");
            SafeAddItem(panel, slabData, RebarPanelName, "Slab Rebar", "KhimTools.RebarTool.Commands.CmdSlabRebar");

            // 4. Foundation Rebar (Large Button)
            var fdnData = CreateSafePushButtonData(
                "CmdFoundationRebar",
                "Foundation" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdFoundationRebar",
                RebarPanelName,
                "Foundation Rebar",
                "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                "rebar_fdn_32.png",
                "rebar_fdn_16.png");
            SafeAddItem(panel, fdnData, RebarPanelName, "Foundation Rebar", "KhimTools.RebarTool.Commands.CmdFoundationRebar");

            panel.AddSeparator();

            // ── CỤM DETAIL: XUẤT BẢN VẼ CHI TIẾT THÉP ──
            var rebarDetailData = new PulldownButtonData("KhimRebarDetailPulldown", "Detailing")
            {
                ToolTip = "Các công cụ tạo bản vẽ và cập nhật chi tiết thép kết cấu.",
                Image = LoadImage("rebar_draw_16.png")
            };

            var pDetail = SafeAddItem(panel, rebarDetailData, RebarPanelName, "Rebar Detailing Pulldown", string.Empty) as PulldownButton;
            if (pDetail != null)
            {
                SafeAddPulldownItem(pDetail, "CmdColumnDrawing", "Column Drawing",
                    "KhimTools.RebarTool.Commands.CmdColumnDrawing", assemblyPath, "rebar_draw_16.png", RebarPanelName);
                SafeAddPulldownItem(pDetail, "CmdUpdateColumnDrawing", "Update Drawing",
                    "KhimTools.RebarTool.Commands.CmdUpdateColumnDrawing", assemblyPath, "rebar_draw_16.png", RebarPanelName);
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 5. PANEL: ARCHI (ROOM 3D VIEW & ROOM FINISHES)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildArchiPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ArchiPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(ArchiPanelName, ArchiPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. Room 3D View (Large Button)
            var room3dData = CreateSafePushButtonData(
                "CmdRoom3DView",
                "Room 3D" + Environment.NewLine + "View",
                assemblyPath,
                "KhimTools.Architectural.Rooms.CmdRoom3DView",
                ArchiPanelName,
                "Room 3D View",
                "Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.",
                "icon_room3d_32.png",
                "icon_room3d_16.png");
            SafeAddItem(panel, room3dData, ArchiPanelName, "Room 3D View", "KhimTools.Architectural.Rooms.CmdRoom3DView");

            // 2. Room Finishes (Large Button)
            var finishData = CreateSafePushButtonData(
                "CmdWallFloorFinishes",
                "Room" + Environment.NewLine + "Finishes",
                assemblyPath,
                "KhimTools.Architectural.Finishes.CmdWallFloorFinishes",
                ArchiPanelName,
                "Room Finishes",
                "Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng.",
                "icon_finishes_32.png",
                "icon_finishes_16.png");
            SafeAddItem(panel, finishData, ArchiPanelName, "Room Finishes", "KhimTools.Architectural.Finishes.CmdWallFloorFinishes");
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 6. PANEL: MEP (MEP OPENINGS & ELEVATION TAGS)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildMepPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, MepPanelName);
            if (panel == null)
            {
                RegistrationDiagnostics.RecordError(MepPanelName, MepPanelName, "PanelCreation", string.Empty, "Không thể tạo hoặc lấy RibbonPanel.");
                return;
            }

            // 1. MEP Openings (Large Button)
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

            // 2. MEP Elevation Tags (Large Button)
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
                    text, "Bật/Tắt hiển thị hoặc thao tác đối tượng trong Active View.", null, smallIconName);

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
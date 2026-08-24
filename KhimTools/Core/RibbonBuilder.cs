using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Xây dựng Ribbon UI chuyên nghiệp trên Tab: "K-TOOLS"
    /// Được phân chia thành 4 cụm Panel chuyên môn gom gọn:
    ///   1. K-GEN           (Workspace, Join, Overdrive, Ẩn/Hiện, Căn chỉnh Text, Align Viewport, Grid, Link, Detail No, Update, Exporter, Language)
    ///   2. K-STRUCTURAL    (Column Rebar, Beam Rebar, Slab Rebar, Foundation Rebar, Section Cut, Cover Setup)
    ///   3. K-ARCHITECTURAL (Room 3D View, Room Finishes)
    ///   4. K-MEP           (MEP Openings, Elevation Tags)
    /// </summary>
    public static class RibbonBuilder
    {
        public const string TabName = "K-TOOLS";
        public const string GenPanelName = "K-GEN";
        public const string OverridePanelName = "Override";
        public const string StructuralPanelName = "K-STRUCTURAL";
        public const string ArchPanelName = "K-ARCHITECTURAL";
        public const string MepPanelName = "K-MEP";

        public static void BuildRibbon(UIControlledApplication application)
        {
            CreateTabSafely(application, TabName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // 1. Panel: K-GEN (Gom gọn đẹp mắt)
            BuildGenPanel(application, assemblyPath);

            // 2. Panel: Override (Palette màu 3x3 + Halftone + Reset + Setting Color)
            BuildOverridePanel(application, assemblyPath);

            // 3. Panel: K-STRUCTURAL
            BuildStructuralPanel(application, assemblyPath);

            // 4. Panel: K-ARCHITECTURAL
            BuildArchPanel(application, assemblyPath);

            // 5. Panel: K-MEP
            BuildMepPanel(application, assemblyPath);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 1. PANEL: K-GEN (GOM GỌN TỐI ƯU KHÔNG GIAN)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildGenPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, GenPanelName);

            // 1. Khim Workspace (Large Button)
            var wsData = new PushButtonData(
                "CmdToggleWorkspace",
                "Khim" + Environment.NewLine + "Workspace",
                assemblyPath,
                "KhimTools.Workspace.Commands.CmdToggleWorkspace")
            {
                ToolTip = "Bật/Tắt bảng điều khiển Khim Workspace (Dockable Pane).",
                LargeImage = LoadImage("icon_workspace_32.png"),
                Image = LoadImage("icon_workspace_16.png")
            };
            panel.AddItem(wsData);

            // 2. Join Elements (Large Button)
            var joinElementsData = new PushButtonData(
                "CmdJoinElements",
                "Join" + Environment.NewLine + "Elements",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.CmdJoinElements")
            {
                ToolTip = "Mở công cụ Join/Unjoin/Switch chuyên nghiệp cho tất cả loại cấu kiện.",
                LongDescription = "Hỗ trợ join/unjoin/switch geometry giữa bất kỳ cặp Category: Floors, Walls, Columns, Beams, Foundations...",
                LargeImage = LoadImage("icon_join_32.png"),
                Image = LoadImage("icon_join_16.png")
            };
            panel.AddItem(joinElementsData);

            // ── STACK 1: BỘ ĐÔI HIỂN THỊ & ẨN CATEGORY ──
            var pulldownShowData = new PulldownButtonData("VisibilityShowPulldown", "Hiển thị")
            {
                ToolTip = "Bật hiển thị các Category đối tượng trong View hiện hành.",
                Image = LoadImage("icon_workspace_16.png")
            };

            var pulldownHideData = new PulldownButtonData("VisibilityHidePulldown", "Ẩn")
            {
                ToolTip = "Ẩn các Category đối tượng trong View hiện hành.",
                Image = LoadImage("icon_workspace_16.png")
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

            // ── STACK 2: CĂN CHỈNH TEXT & CĂN CHỈNH VIEWPORT ──
            var pulldownAlignTextData = new PulldownButtonData("AlignTextPulldown", "Align")
            {
                ToolTip = "Căn chỉnh lề và chia đều khoảng cách cho Text, Tag, Annotation.",
                Image = LoadImage("icon_align_16.png")
            };

            var alignVpData = new PushButtonData(
                "CmdAlignViewport",
                "Align Viewport",
                assemblyPath,
                "KhimTools.ViewportAlign.Commands.CmdAlignViewport")
            {
                ToolTip = "Đồng bộ và căn chỉnh vị trí Viewport & Bảng Schedule trên nhiều Sheet (TreeView chọn lọc).",
                Image = LoadImage("icon_align_16.png")
            };

            var stackedAlign = panel.AddStackedItems(pulldownAlignTextData, alignVpData);
            if (stackedAlign.Count == 2)
            {
                var pAlignText = stackedAlign[0] as PulldownButton;
                if (pAlignText != null)
                {
                    AddPulldownItem(pAlignText, "CmdAlignTop", "Top", "KhimTools.TextAlign.Commands.CmdAlignTop", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pAlignText, "CmdAlignBottom", "Bot", "KhimTools.TextAlign.Commands.CmdAlignBottom", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pAlignText, "CmdAlignLeft", "Left", "KhimTools.TextAlign.Commands.CmdAlignLeft", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pAlignText, "CmdAlignRight", "Right", "KhimTools.TextAlign.Commands.CmdAlignRight", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pAlignText, "CmdAlignMiddle", "Middle", "KhimTools.TextAlign.Commands.CmdAlignMiddle", assemblyPath, "icon_align_16.png");
                    pAlignText.AddSeparator();
                    AddPulldownItem(pAlignText, "CmdAlignHorizontalEquals", "Horizontal Equals", "KhimTools.TextAlign.Commands.CmdAlignHorizontalEquals", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pAlignText, "CmdAlignVerticalEquals", "Vertical Equals", "KhimTools.TextAlign.Commands.CmdAlignVerticalEquals", assemblyPath, "icon_align_16.png");
                }
            }

            // ── STACK 3: GRID & LEVEL FULL PULDOWN & COPY LINK ──
            var pulldownGridLevelData = new PulldownButtonData("GridLevelPulldown", "Grid & Level")
            {
                ToolTip = "Bộ công cụ toàn diện xử lý Hệ Lưới Trục (Grid) và Cao Độ Tầng (Level).",
                Image = LoadImage("icon_grid_16.png")
            };

            var copyLinkData = new PushButtonData(
                "CmdCopyLinkElements",
                "Copy Link",
                assemblyPath,
                "KhimTools.CopyLink.Commands.CmdCopyLinkElements")
            {
                ToolTip = "Sao chép đối tượng từ file Revit Link sang dự án chính chuẩn 100% tọa độ.",
                Image = LoadImage("icon_copylink_16.png")
            };

            var stackedGridLink = panel.AddStackedItems(pulldownGridLevelData, copyLinkData);
            if (stackedGridLink.Count == 2)
            {
                var pGridLevel = stackedGridLink[0] as PulldownButton;
                if (pGridLevel != null)
                {
                    AddPulldownItem(pGridLevel, "CmdCreateGrid", "Tạo Grid", "KhimTools.GridLevel.Commands.CmdCreateGrid", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pGridLevel, "CmdCreateLevel", "Tạo Level", "KhimTools.GridLevel.Commands.CmdCreateLevel", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pGridLevel, "CmdCutLevel", "Cắt Level", "KhimTools.GridLevel.Commands.CmdCutLevel", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pGridLevel, "CmdLevelBubble", "Level Bubble", "KhimTools.GridLevel.Commands.CmdLevelBubble", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pGridLevel, "CmdConvertLevel2D", "Chuyển Level 2D", "KhimTools.GridLevel.Commands.CmdConvertLevel2D", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pGridLevel, "CmdConvertLevel3D", "Chuyển Level 3D", "KhimTools.GridLevel.Commands.CmdConvertLevel3D", assemblyPath, "icon_detail_16.png");
                    pGridLevel.AddSeparator();
                    AddPulldownItem(pGridLevel, "CmdCutGrid3D", "Cắt Grid 3D", "KhimTools.GridLevel.Commands.CmdCutGrid3D", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pGridLevel, "CmdTrimGrid2D", "Trim Grid 2D", "KhimTools.GridLevel.Commands.CmdTrimGrid2D", assemblyPath, "icon_align_16.png");
                    AddPulldownItem(pGridLevel, "CmdGridBubble", "Grid Bubble", "KhimTools.GridLevel.Commands.CmdGridBubble", assemblyPath, "icon_grid_16.png");
                    AddPulldownItem(pGridLevel, "CmdConvertGrid2D", "Chuyển Grid 2D", "KhimTools.GridLevel.Commands.CmdConvertGrid2D", assemblyPath, "icon_detail_16.png");
                    AddPulldownItem(pGridLevel, "CmdConvertGrid3D", "Chuyển Grid 3D", "KhimTools.GridLevel.Commands.CmdConvertGrid3D", assemblyPath, "icon_detail_16.png");
                }
            }

            // ── STACK 4: DETAIL NUMBER & CHECK UPDATE ──
            var updateDetailNumData = new PushButtonData(
                "CmdUpdateDetailNumbers",
                "Update Detail No",
                assemblyPath,
                "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers")
            {
                ToolTip = "Tự động trích xuất và cập nhật số hiệu chi tiết (Detail Number) từ tên View.",
                Image = LoadImage("icon_detail_16.png")
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

            panel.AddStackedItems(updateDetailNumData, updateData);

            // 4. Tạo Sheet Tự Động (SheetGen - Large Button)
            var sheetGenData = new PushButtonData(
                "CmdSheetGen",
                "Auto" + Environment.NewLine + "SheetGen",
                assemblyPath,
                "KhimTools.SheetGen.Commands.CmdSheetGen")
            {
                ToolTip = "Công cụ Tạo Sheet Tự Động (Batch Create Sheets, TitleBlock, Viewport, Import/Export CSV chuẩn DiRoots).",
                LargeImage = LoadImage("export_sheet_32.png"),
                Image = LoadImage("export_sheet_16.png")
            };
            panel.AddItem(sheetGenData);

            // 5. Sheet Exporter (Large Button)
            var sheetExportData = new PushButtonData(
                "CmdSheetExport",
                "Sheet" + Environment.NewLine + "Exporter",
                assemblyPath,
                "KhimTools.SheetExport.Commands.CmdSheetExport")
            {
                ToolTip = "Công cụ Batch Print & Export Sheet/View chuyên nghiệp (PDF, DWG, Issue Manager).",
                LongDescription = "Hỗ trợ Naming Templates với Regex validation, Issue Revision Diffing, " +
                    "Tự động tạo file Excel Transmittal Register & QA Technical Log, " +
                    "PDFsharp Bookmarks, Watermark Status Stamp, Cover Sheet, và Auto-Retry.",
                LargeImage = LoadImage("export_sheet_32.png"),
                Image = LoadImage("export_sheet_16.png")
            };
            panel.AddItem(sheetExportData);

            // 5. Ngôn ngữ / Language (SplitButton)
            var splitLangData = new SplitButtonData(
                "LanguageSplitButton",
                "Ngôn ngữ" + Environment.NewLine + "Language")
            {
                ToolTip = "Chuyển đổi ngôn ngữ giao diện (Song ngữ Tiếng Việt - English)."
            };

            var splitLang = panel.AddItem(splitLangData) as SplitButton;
            if (splitLang != null)
            {
                AddPushButton(splitLang, "CmdSwitchLanguage", "Đổi Ngôn Ngữ (Switch)",
                    "KhimTools.LanguageSwitcher.Commands.CmdSwitchLanguage", assemblyPath,
                    "Chuyển đổi nhanh qua lại giữa Tiếng Việt và English.",
                    "icon_workspace_32.png", "icon_workspace_16.png");

                splitLang.AddSeparator();

                AddPushButton(splitLang, "CmdSetVietnamese", "Tiếng Việt (VN)",
                    "KhimTools.LanguageSwitcher.Commands.CmdSetVietnamese", assemblyPath,
                    "Thiết lập ngôn ngữ toàn bộ hệ thống là Tiếng Việt.",
                    "icon_workspace_32.png", "icon_workspace_16.png");

                AddPushButton(splitLang, "CmdSetEnglish", "English (EN)",
                    "KhimTools.LanguageSwitcher.Commands.CmdSetEnglish", assemblyPath,
                    "Set the entire system language to English.",
                    "icon_workspace_32.png", "icon_workspace_16.png");
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 2. PANEL: OVERRIDE (MATCH SCREENSHOT: 3x3 COLOR PALETTE + HALFTONE + RESET + SETTING)
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildOverridePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, OverridePanelName);

            // ── STACK 1: ĐỎ, CAM, VÀNG ──
            var redData = CreateColorSwatchData("CmdOverrideRed", "KhimTools.OverrideTool.Commands.CmdOverrideRed", assemblyPath, "override_red_16.png", "Gán màu Đỏ (Red) cho đối tượng đang chọn");
            var orangeData = CreateColorSwatchData("CmdOverrideOrange", "KhimTools.OverrideTool.Commands.CmdOverrideOrange", assemblyPath, "override_orange_16.png", "Gán màu Cam (Orange) cho đối tượng đang chọn");
            var yellowData = CreateColorSwatchData("CmdOverrideYellow", "KhimTools.OverrideTool.Commands.CmdOverrideYellow", assemblyPath, "override_yellow_16.png", "Gán màu Vàng (Yellow) cho đối tượng đang chọn");
            panel.AddStackedItems(redData, orangeData, yellowData);

            // ── STACK 2: XANH LÁ, CYAN, XANH DƯƠNG ──
            var greenData = CreateColorSwatchData("CmdOverrideGreen", "KhimTools.OverrideTool.Commands.CmdOverrideGreen", assemblyPath, "override_green_16.png", "Gán màu Xanh lá (Green) cho đối tượng đang chọn");
            var cyanData = CreateColorSwatchData("CmdOverrideCyan", "KhimTools.OverrideTool.Commands.CmdOverrideCyan", assemblyPath, "override_cyan_16.png", "Gán màu Xanh lơ (Cyan) cho đối tượng đang chọn");
            var blueData = CreateColorSwatchData("CmdOverrideBlue", "KhimTools.OverrideTool.Commands.CmdOverrideBlue", assemblyPath, "override_blue_16.png", "Gán màu Xanh dương (Blue) cho đối tượng đang chọn");
            panel.AddStackedItems(greenData, cyanData, blueData);

            // ── STACK 3: MAGENTA, XÁM, TÙY CHỌN (GRADIENT) ──
            var magentaData = CreateColorSwatchData("CmdOverrideMagenta", "KhimTools.OverrideTool.Commands.CmdOverrideMagenta", assemblyPath, "override_magenta_16.png", "Gán màu Hồng cánh sen (Magenta) cho đối tượng đang chọn");
            var grayData = CreateColorSwatchData("CmdOverrideGray", "KhimTools.OverrideTool.Commands.CmdOverrideGray", assemblyPath, "override_gray_16.png", "Gán màu Xám (Gray) cho đối tượng đang chọn");
            var customData = CreateColorSwatchData("CmdOverrideCustom", "KhimTools.OverrideTool.Commands.CmdOverrideCustom", assemblyPath, "override_custom_16.png", "Chọn màu tùy chỉnh từ bảng màu (Custom Color Picker)");
            panel.AddStackedItems(magentaData, grayData, customData);

            // ── LARGE BUTTON 1: ON/OFF HALFTONE ──
            var halftoneData = new PushButtonData(
                "CmdQuickHalftone",
                "On/Off" + Environment.NewLine + "Halftone",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdQuickHalftone")
            {
                ToolTip = "Bật/Tắt nhanh chế độ mờ Halftone 50% cho đối tượng đang chọn.",
                LargeImage = LoadImage("override_halftone_32.png"),
                Image = LoadImage("override_halftone_16.png")
            };
            panel.AddItem(halftoneData);

            // ── LARGE BUTTON 2: RESET OVERRIDE ──
            var resetData = new PushButtonData(
                "CmdQuickResetOverride",
                "Reset" + Environment.NewLine + "Override",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdQuickResetOverride")
            {
                ToolTip = "Xóa toàn bộ màu sắc, đường nét, halftone đã override của đối tượng đang chọn.",
                LargeImage = LoadImage("override_reset_32.png"),
                Image = LoadImage("override_reset_16.png")
            };
            panel.AddItem(resetData);

            // ── LARGE BUTTON 3: SETTING COLOR ──
            var settingData = new PushButtonData(
                "CmdGraphicOverdrive",
                "Setting" + Environment.NewLine + "Color",
                assemblyPath,
                "KhimTools.OverrideTool.Commands.CmdGraphicOverdrive")
            {
                ToolTip = "Mở bảng điều khiển Graphic Overdrive chi tiết (Độ trong suốt Transparency, Nét vẽ Line Weight, 12 Presets màu).",
                LargeImage = LoadImage("override_setting_32.png"),
                Image = LoadImage("override_setting_16.png")
            };
            panel.AddItem(settingData);
        }

        private static PushButtonData CreateColorSwatchData(string id, string className, string assemblyPath, string iconName, string tooltip)
        {
            return new PushButtonData(id, " ", assemblyPath, className)
            {
                ToolTip = tooltip,
                Image = LoadImage(iconName),
                LargeImage = LoadImage(iconName)
            };
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 3. PANEL: K-STRUCTURAL
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildStructuralPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, StructuralPanelName);

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
                AddPushButton(splitButton, "CmdColumnRebar", "Column Rebar (Auto-detect)",
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
            }

            // 2. Beam Rebar
            var beamData = new PushButtonData(
                "CmdBeamRebar",
                "Beam" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdBeamRebar")
            {
                ToolTip = "Bố trí thép dầm (Beam Rebar v2.0) chuẩn kết cấu TCVN & Eurocode.",
                LongDescription = "Hỗ trợ thép chủ chạy suốt (top/bottom), thép gia cường gối L/3, " +
                    "thép gia cường bụng L/6, thép sườn (skin bars), đai phân vùng A1/A2/A1 và đai treo dầm phụ.",
                LargeImage = LoadImage("rebar_beam_32.png"),
                Image = LoadImage("rebar_beam_16.png")
            };
            panel.AddItem(beamData);

            // 3. Slab Rebar
            var slabData = new PushButtonData(
                "CmdSlabRebar",
                "Slab" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdSlabRebar")
            {
                ToolTip = "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                LargeImage = LoadImage("rebar_slab_32.png"),
                Image = LoadImage("rebar_slab_16.png")
            };
            panel.AddItem(slabData);

            // 4. Foundation Rebar
            var fdnData = new PushButtonData(
                "CmdFoundationRebar",
                "Foundation" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdFoundationRebar")
            {
                ToolTip = "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                LargeImage = LoadImage("rebar_fdn_32.png"),
                Image = LoadImage("rebar_fdn_16.png")
            };
            panel.AddItem(fdnData);

            // 5. Section Cut
            var sectionData = new PushButtonData(
                "CmdSectionCut",
                "Section" + Environment.NewLine + "Cut",
                assemblyPath,
                "KhimTools.SectionCutTool.Commands.CmdSectionCut")
            {
                ToolTip = "Tự động tạo mặt cắt dọc & ngang (Section Views) phục vụ bản vẽ thép.",
                LargeImage = LoadImage("rebar_draw_16.png") ?? LoadImage("export_sheet_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(sectionData);

            // 6. Cover Setup
            var coverData = new PushButtonData(
                "CmdProjectCoverSetup",
                "Cover" + Environment.NewLine + "Setup",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdProjectCoverSetup")
            {
                ToolTip = "Cấu hình Lớp bê tông bảo vệ (Concrete Cover) toàn dự án.",
                LargeImage = LoadImage("rebar_cover_16.png") ?? LoadImage("rebar_col_16.png"),
                Image = LoadImage("rebar_cover_16.png")
            };
            panel.AddItem(coverData);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 3. PANEL: K-ARCHITECTURAL
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildArchPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ArchPanelName);

            // 1. Room 3D View
            var room3dData = new PushButtonData(
                "CmdRoom3DView",
                "Room 3D" + Environment.NewLine + "View",
                assemblyPath,
                "KhimTools.Architectural.Rooms.CmdRoom3DView")
            {
                ToolTip = "Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.",
                LargeImage = LoadImage("icon_workspace_32.png"),
                Image = LoadImage("icon_workspace_16.png")
            };
            panel.AddItem(room3dData);

            // 2. Room Finishes
            var finishData = new PushButtonData(
                "CmdWallFloorFinishes",
                "Room" + Environment.NewLine + "Finishes",
                assemblyPath,
                "KhimTools.Architectural.Finishes.CmdWallFloorFinishes")
            {
                ToolTip = "Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng.",
                LargeImage = LoadImage("icon_detail_32.png"),
                Image = LoadImage("icon_detail_16.png")
            };
            panel.AddItem(finishData);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 4. PANEL: K-MEP
        // ════════════════════════════════════════════════════════════════════════════════
        private static void BuildMepPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, MepPanelName);

            // 1. MEP Openings
            var openingData = new PushButtonData(
                "CmdMepOpenings",
                "MEP" + Environment.NewLine + "Openings",
                assemblyPath,
                "KhimTools.MEP.Penetrations.CmdMepOpenings")
            {
                ToolTip = "Tự động kiểm tra xung đột ống MEP với Dầm/Sàn/Vách và đục lỗ mở (Openings).",
                LargeImage = LoadImage("icon_grid_32.png"),
                Image = LoadImage("icon_grid_16.png")
            };
            panel.AddItem(openingData);

            // 2. MEP Elevation Tags
            var tagData = new PushButtonData(
                "CmdMepElevationTags",
                "Elevation" + Environment.NewLine + "Tags",
                assemblyPath,
                "KhimTools.MEP.Tags.CmdMepElevationTags")
            {
                ToolTip = "Tự động gán nhãn cao độ đáy (BOP/Invert Elevation) cho ống gió và ống nước.",
                LargeImage = LoadImage("icon_detail_32.png"),
                Image = LoadImage("icon_detail_16.png")
            };
            panel.AddItem(tagData);
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
            var data = new PushButtonData(name, text, assemblyPath, className)
            {
                ToolTip = "Bật/Tắt hiển thị hoặc căn chỉnh đối tượng trong Active View.",
                Image = LoadImage(smallIconName)
            };
            pulldown.AddPushButton(data);
        }

        private static void AddPushButton(SplitButton splitButton, string name, string text,
            string className, string assemblyPath, string toolTip, string largeIconName, string smallIconName)
        {
            var data = new PushButtonData(name, text, assemblyPath, className)
            {
                ToolTip = toolTip,
                LargeImage = LoadImage(largeIconName),
                Image = LoadImage(smallIconName)
            };
            splitButton.AddPushButton(data);
        }

        private static BitmapImage LoadImage(string resourceOrFileName)
        {
            if (string.IsNullOrEmpty(resourceOrFileName)) return null;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // 1. Thử load từ Embedded Resource
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith(resourceOrFileName, StringComparison.OrdinalIgnoreCase));

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
                string dir = Path.GetDirectoryName(assembly.Location) ?? "";
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
            catch { }

            return null;
        }
    }
}
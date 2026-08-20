using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Xây dựng toàn bộ hệ thống Ribbon của KhimTools phân chia theo 4 phân hệ chuẩn Revit:
    ///   1. KhimGen           (Quản lý chung, Workspace, Join, Align Viewport, Sheet Exporter)
    ///   2. KhimStructural    (Bố trí thép Cột, Dầm, Sàn, Móng, Cover, Mặt cắt & Bản vẽ thép)
    ///   3. KhimArchitectural (Quản lý Phòng, 3D Room, Lớp hoàn thiện)
    ///   4. KhimMEP           (Lỗ mở xuyên cấu kiện, Tag cao độ MEP)
    /// </summary>
    public static class RibbonBuilder
    {
        // Ribbon Tabs Constants
        public const string TabGen = "KhimGen";
        public const string TabStr = "KhimStructural";
        public const string TabArc = "KhimArchitectural";
        public const string TabMep = "KhimMEP";

        public static void BuildRibbon(UIControlledApplication application)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // 1. Tab KHIM GEN
            CreateTabSafely(application, TabGen);
            BuildGenWorkspacePanel(application, assemblyPath);
            BuildGenJoinPanel(application, assemblyPath);
            BuildGenViewSheetPanel(application, assemblyPath);
            BuildGenExportPanel(application, assemblyPath);

            // 2. Tab KHIM STRUCTURAL
            CreateTabSafely(application, TabStr);
            BuildStrRebarPanel(application, assemblyPath);
            BuildStrDetailingPanel(application, assemblyPath);

            // 3. Tab KHIM ARCHITECTURAL
            CreateTabSafely(application, TabArc);
            BuildArcRoomsPanel(application, assemblyPath);
            BuildArcFinishesPanel(application, assemblyPath);

            // 4. Tab KHIM MEP
            CreateTabSafely(application, TabMep);
            BuildMepPenetrationsPanel(application, assemblyPath);
            BuildMepTagsPanel(application, assemblyPath);
        }

        #region TAB 1: KHIM GEN
        private static void BuildGenWorkspacePanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabGen, "Workspace & System");

            var wsData = new PushButtonData(
                "CmdToggleWorkspace",
                "Khim" + Environment.NewLine + "Workspace",
                assemblyPath,
                "KhimTools.Workspace.Commands.CmdToggleWorkspace")
            {
                ToolTip = "Bật/Tắt bảng điều khiển Khim Workspace (Dockable Pane).",
                LargeImage = LoadImage("icon_workspace_32.png") ?? LoadImage("rebar_col_32.png"),
                Image = LoadImage("icon_workspace_16.png") ?? LoadImage("rebar_col_16.png")
            };
            panel.AddItem(wsData);

            var updateData = new PushButtonData(
                "CmdCheckUpdate",
                "Check" + Environment.NewLine + "Update",
                assemblyPath,
                "KhimTools.Updater.Commands.CmdCheckUpdate")
            {
                ToolTip = "Kiểm tra và cập nhật phiên bản mới nhất của KhimTools.",
                Image = LoadImage("rebar_cover_16.png")
            };
            panel.AddItem(updateData);
        }

        private static void BuildGenJoinPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabGen, "Geometry & Join");

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

            var joinSlabData = new PushButtonData(
                "JoinSlabsLegacy",
                "Join Slabs",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.JoinSlabsCommand")
            {
                ToolTip = "Join sàn nhanh.",
                Image = LoadImage("icon_join_16.png")
            };

            var unjoinSlabData = new PushButtonData(
                "UnjoinSlabsLegacy",
                "Unjoin Slabs",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.UnjoinSlabsCommand")
            {
                ToolTip = "Unjoin sàn nhanh.",
                Image = LoadImage("icon_unjoin_16.png")
            };

            panel.AddStackedItems(joinSlabData, unjoinSlabData);
        }

        private static void BuildGenViewSheetPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabGen, "View & Sheet Manager");

            var alignVpData = new PushButtonData(
                "CmdAlignViewport",
                "Align" + Environment.NewLine + "Viewport",
                assemblyPath,
                "KhimTools.ViewportAlign.Commands.CmdAlignViewport")
            {
                ToolTip = "Đồng bộ và căn chỉnh vị trí Viewport & Bảng Schedule trên nhiều Sheet.",
                LargeImage = LoadImage("rebar_draw_16.png") ?? LoadImage("rebar_col_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(alignVpData);

            var updateDetailNumData = new PushButtonData(
                "CmdUpdateDetailNumbers",
                "Update Detail No",
                assemblyPath,
                "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers")
            {
                ToolTip = "Tự động trích xuất và cập nhật số hiệu chi tiết (Detail Number) từ tên View.",
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(updateDetailNumData);
        }

        private static void BuildGenExportPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabGen, "Publish & Export");

            var sheetExportData = new PushButtonData(
                "CmdSheetExport",
                "Sheet" + Environment.NewLine + "Exporter",
                assemblyPath,
                "KhimTools.SheetExport.Commands.CmdSheetExport")
            {
                ToolTip = "Xuất in bản vẽ hàng loạt (PDF & AutoCAD DWG) + Tạo Bảng Kê Transmittal.",
                LargeImage = LoadImage("icon_export_32.png") ?? LoadImage("rebar_draw_16.png"),
                Image = LoadImage("icon_export_16.png") ?? LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(sheetExportData);
        }
        #endregion

        #region TAB 2: KHIM STRUCTURAL
        private static void BuildStrRebarPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabStr, "Rebar Modeling");

            // SplitButton: Column Rebar
            var splitButtonData = new SplitButtonData("ColumnRebarSplitButton", "Column" + Environment.NewLine + "Rebar")
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
            }

            // Beam Rebar
            var beamData = new PushButtonData("CmdBeamRebar", "Beam" + Environment.NewLine + "Rebar", assemblyPath, "KhimTools.RebarTool.Commands.CmdBeamRebar")
            {
                ToolTip = "Bố trí thép dầm (Beam Rebar v2.0) chuẩn kết cấu TCVN & Eurocode.",
                LargeImage = LoadImage("rebar_beam_32.png"),
                Image = LoadImage("rebar_beam_16.png")
            };
            panel.AddItem(beamData);

            // Slab Rebar
            var slabData = new PushButtonData("CmdSlabRebar", "Slab" + Environment.NewLine + "Rebar", assemblyPath, "KhimTools.RebarTool.Commands.CmdSlabRebar")
            {
                ToolTip = "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                LargeImage = LoadImage("rebar_slab_32.png"),
                Image = LoadImage("rebar_slab_16.png")
            };
            panel.AddItem(slabData);

            // Foundation Rebar
            var fdnData = new PushButtonData("CmdFoundationRebar", "Foundation" + Environment.NewLine + "Rebar", assemblyPath, "KhimTools.RebarTool.Commands.CmdFoundationRebar")
            {
                ToolTip = "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                LargeImage = LoadImage("rebar_fdn_32.png"),
                Image = LoadImage("rebar_fdn_16.png")
            };
            panel.AddItem(fdnData);

            // Cover Setup
            var coverData = new PushButtonData("CmdProjectCoverSetup", "Cover" + Environment.NewLine + "Setup", assemblyPath, "KhimTools.RebarTool.Commands.CmdProjectCoverSetup")
            {
                ToolTip = "Cấu hình Lớp bê tông bảo vệ (Concrete Cover) toàn dự án.",
                Image = LoadImage("rebar_cover_16.png")
            };
            panel.AddItem(coverData);
        }

        private static void BuildStrDetailingPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabStr, "Structural Detailing");

            // Section Cut
            var sectionData = new PushButtonData("CmdSectionCut", "Section" + Environment.NewLine + "Cut", assemblyPath, "KhimTools.SectionCutTool.Commands.CmdSectionCut")
            {
                ToolTip = "Tự động tạo mặt cắt dọc & ngang (Section Views) phục vụ bản vẽ thép.",
                LargeImage = LoadImage("rebar_beam_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(sectionData);

            // Column Drawing
            var drawColData = new PushButtonData("CmdColumnDrawing", "Column" + Environment.NewLine + "Drawing", assemblyPath, "KhimTools.RebarTool.Commands.CmdColumnDrawing")
            {
                ToolTip = "Tự động xuất bản vẽ mặt cắt 2D & thống kê thép cột.",
                LargeImage = LoadImage("rebar_col_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(drawColData);
        }
        #endregion

        #region TAB 3: KHIM ARCHITECTURAL
        private static void BuildArcRoomsPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabArc, "Rooms & Views");

            var room3dData = new PushButtonData("CmdRoom3DView", "Room 3D" + Environment.NewLine + "View", assemblyPath, "KhimTools.Architectural.Rooms.CmdRoom3DView")
            {
                ToolTip = "Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.",
                LargeImage = LoadImage("icon_workspace_32.png") ?? LoadImage("rebar_col_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(room3dData);
        }

        private static void BuildArcFinishesPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabArc, "Finishes & Layout");

            var finishData = new PushButtonData("CmdWallFloorFinishes", "Room" + Environment.NewLine + "Finishes", assemblyPath, "KhimTools.Architectural.Finishes.CmdWallFloorFinishes")
            {
                ToolTip = "Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng.",
                LargeImage = LoadImage("icon_join_32.png"),
                Image = LoadImage("icon_join_16.png")
            };
            panel.AddItem(finishData);
        }
        #endregion

        #region TAB 4: KHIM MEP
        private static void BuildMepPenetrationsPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabMep, "Penetrations & Clash");

            var openingData = new PushButtonData("CmdMepOpenings", "MEP" + Environment.NewLine + "Openings", assemblyPath, "KhimTools.MEP.Penetrations.CmdMepOpenings")
            {
                ToolTip = "Tự động kiểm tra xung đột ống MEP với Dầm/Sàn/Vách và đục lỗ mở (Openings).",
                LargeImage = LoadImage("rebar_beam_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(openingData);
        }

        private static void BuildMepTagsPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabMep, "Annotation & Tags");

            var tagData = new PushButtonData("CmdMepElevationTags", "Elevation" + Environment.NewLine + "Tags", assemblyPath, "KhimTools.MEP.Tags.CmdMepElevationTags")
            {
                ToolTip = "Tự động gán nhãn cao độ đáy (BOP/Invert Elevation) cho ống gió và ống nước.",
                LargeImage = LoadImage("icon_export_32.png") ?? LoadImage("rebar_draw_16.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(tagData);
        }
        #endregion

        #region Helper Methods
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
        #endregion
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Xây dựng Ribbon trên 1 Tab duy nhất: "Khim Tools"
    /// Được phân chia thành 4 cụm Panel chuyên nghiệp:
    ///   1. KhimGen           (Workspace, Join, Align Viewport, Sheet Exporter, Updater)
    ///   2. KhimStructural    (Column Rebar, Beam Rebar, Slab Rebar, Foundation Rebar, Section Cut, Cover)
    ///   3. KhimArchitectural (Room 3D View, Room Finishes)
    ///   4. KhimMEP           (MEP Openings, Elevation Tags)
    /// </summary>
    public static class RibbonBuilder
    {
        public const string TabName = "Khim Tools";

        public static void BuildRibbon(UIControlledApplication application)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            CreateTabSafely(application, TabName);

            // 1. Panel KhimGen
            BuildKhimGenPanel(application, assemblyPath);

            // 2. Panel KhimStructural
            BuildKhimStructuralPanel(application, assemblyPath);

            // 3. Panel KhimArchitectural
            BuildKhimArchitecturalPanel(application, assemblyPath);

            // 4. Panel KhimMEP
            BuildKhimMepPanel(application, assemblyPath);
        }

        #region 1. PANEL: KHIM GEN
        private static void BuildKhimGenPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, "KhimGen");

            // 1. Workspace
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

            // 1.5 Auto Grid & Plan
            var gridPlanData = new PushButtonData(
                "CmdAutoGridPlan",
                "Grid &" + Environment.NewLine + "Plans",
                assemblyPath,
                "KhimTools.GridLevel.Commands.CmdAutoGridPlan")
            {
                ToolTip = "Tự động tạo Hệ Lưới Trục (Grids) & Cao Độ Tầng, Mặt Bằng (Levels & Plans).",
                LongDescription = "Hỗ trợ nhập khoảng cách trục theo chuỗi (VD: 6000, 7200, 3x6000), tự động đánh Dimension và sinh các mặt bằng kiến trúc, kết cấu.",
                LargeImage = LoadImage("icon_grid_32.png"),
                Image = LoadImage("icon_grid_16.png")
            };
            panel.AddItem(gridPlanData);

            // 1.6 Copy Elements from Revit Link
            var copyLinkData = new PushButtonData(
                "CmdCopyLinkElements",
                "Copy" + Environment.NewLine + "Link",
                assemblyPath,
                "KhimTools.CopyLink.Commands.CmdCopyLinkElements")
            {
                ToolTip = "Sao chép đối tượng từ file Revit Link sang dự án chính chuẩn 100% tọa độ.",
                LongDescription = "Tự động quét toàn bộ Category có đối tượng trong file Link, áp dụng ma trận biến đổi tọa độ (Transform) chính xác tuyệt đối.",
                LargeImage = LoadImage("icon_copylink_32.png"),
                Image = LoadImage("icon_copylink_16.png")
            };
            panel.AddItem(copyLinkData);

            // 2. Join Elements
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

            // 3. Align Viewport
            var alignVpData = new PushButtonData(
                "CmdAlignViewport",
                "Align" + Environment.NewLine + "Viewport",
                assemblyPath,
                "KhimTools.ViewportAlign.Commands.CmdAlignViewport")
            {
                ToolTip = "Đồng bộ và căn chỉnh vị trí Viewport & Bảng Schedule trên nhiều Sheet.",
                LargeImage = LoadImage("icon_align_32.png"),
                Image = LoadImage("icon_align_16.png")
            };
            panel.AddItem(alignVpData);

            // 4. Sheet Exporter
            var sheetExportData = new PushButtonData(
                "CmdSheetExport",
                "Sheet" + Environment.NewLine + "Exporter",
                assemblyPath,
                "KhimTools.SheetExport.Commands.CmdSheetExport")
            {
                ToolTip = "Xuất in bản vẽ hàng loạt (PDF & AutoCAD DWG) + Tạo Bảng Kê Transmittal.",
                LargeImage = LoadImage("icon_export_32.png"),
                Image = LoadImage("icon_export_16.png")
            };
            panel.AddItem(sheetExportData);

            // 5. Stacked: Update Detail No & Check Update
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
                ToolTip = "Kiểm tra và cập nhật phiên bản mới nhất của KhimTools.",
                Image = LoadImage("icon_update_16.png")
            };

            panel.AddStackedItems(updateDetailNumData, updateData);
        }
        #endregion

        #region 2. PANEL: KHIM STRUCTURAL
        private static void BuildKhimStructuralPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, "KhimStructural");

            // 1. SplitButton: Column Rebar
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
                    "rebar_col_32.png", "rebar_col_16.png");

                AddPushButton(splitButton, "CmdMultiRoundColumnRebar", "Cột Tròn 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiRoundColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột tròn.",
                    "rebar_col_32.png", "rebar_col_16.png");

                splitButton.AddSeparator();

                AddPushButton(splitButton, "CmdColumnDrawing", "Column Drawing",
                    "KhimTools.RebarTool.Commands.CmdColumnDrawing", assemblyPath,
                    "Tự động xuất bản vẽ mặt cắt 2D & thống kê thép cột.",
                    "rebar_draw_16.png", "rebar_draw_16.png");
            }

            // 2. Beam Rebar
            var beamData = new PushButtonData("CmdBeamRebar", "Beam" + Environment.NewLine + "Rebar", assemblyPath, "KhimTools.RebarTool.Commands.CmdBeamRebar")
            {
                ToolTip = "Bố trí thép dầm (Beam Rebar v2.0) chuẩn kết cấu TCVN & Eurocode.",
                LargeImage = LoadImage("rebar_beam_32.png"),
                Image = LoadImage("rebar_beam_16.png")
            };
            panel.AddItem(beamData);

            // 3. Slab Rebar
            var slabData = new PushButtonData("CmdSlabRebar", "Slab" + Environment.NewLine + "Rebar", assemblyPath, "KhimTools.RebarTool.Commands.CmdSlabRebar")
            {
                ToolTip = "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                LargeImage = LoadImage("rebar_slab_32.png"),
                Image = LoadImage("rebar_slab_16.png")
            };
            panel.AddItem(slabData);

            // 4. Foundation Rebar
            var fdnData = new PushButtonData("CmdFoundationRebar", "Foundation" + Environment.NewLine + "Rebar", assemblyPath, "KhimTools.RebarTool.Commands.CmdFoundationRebar")
            {
                ToolTip = "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                LargeImage = LoadImage("rebar_fdn_32.png"),
                Image = LoadImage("rebar_fdn_16.png")
            };
            panel.AddItem(fdnData);

            // 5. Section Cut
            var sectionData = new PushButtonData("CmdSectionCut", "Section" + Environment.NewLine + "Cut", assemblyPath, "KhimTools.SectionCutTool.Commands.CmdSectionCut")
            {
                ToolTip = "Tự động tạo mặt cắt dọc & ngang (Section Views) phục vụ bản vẽ thép.",
                LargeImage = LoadImage("rebar_draw_16.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(sectionData);

            // 6. Cover Setup
            var coverData = new PushButtonData("CmdProjectCoverSetup", "Cover" + Environment.NewLine + "Setup", assemblyPath, "KhimTools.RebarTool.Commands.CmdProjectCoverSetup")
            {
                ToolTip = "Cấu hình Lớp bê tông bảo vệ (Concrete Cover) toàn dự án.",
                LargeImage = LoadImage("rebar_cover_16.png"),
                Image = LoadImage("rebar_cover_16.png")
            };
            panel.AddItem(coverData);
        }
        #endregion

        #region 3. PANEL: KHIM ARCHITECTURAL
        private static void BuildKhimArchitecturalPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, "KhimArchitectural");

            var room3dData = new PushButtonData("CmdRoom3DView", "Room 3D" + Environment.NewLine + "View", assemblyPath, "KhimTools.Architectural.Rooms.CmdRoom3DView")
            {
                ToolTip = "Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.",
                LargeImage = LoadImage("icon_align_32.png"),
                Image = LoadImage("icon_align_16.png")
            };
            panel.AddItem(room3dData);

            var finishData = new PushButtonData("CmdWallFloorFinishes", "Room" + Environment.NewLine + "Finishes", assemblyPath, "KhimTools.Architectural.Finishes.CmdWallFloorFinishes")
            {
                ToolTip = "Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng.",
                LargeImage = LoadImage("icon_detail_32.png"),
                Image = LoadImage("icon_detail_16.png")
            };
            panel.AddItem(finishData);
        }
        #endregion

        #region 4. PANEL: KHIM MEP
        private static void BuildKhimMepPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, "KhimMEP");

            var openingData = new PushButtonData("CmdMepOpenings", "MEP" + Environment.NewLine + "Openings", assemblyPath, "KhimTools.MEP.Penetrations.CmdMepOpenings")
            {
                ToolTip = "Tự động kiểm tra xung đột ống MEP với Dầm/Sàn/Vách và đục lỗ mở (Openings).",
                LargeImage = LoadImage("icon_export_32.png"),
                Image = LoadImage("icon_export_16.png")
            };
            panel.AddItem(openingData);

            var tagData = new PushButtonData("CmdMepElevationTags", "Elevation" + Environment.NewLine + "Tags", assemblyPath, "KhimTools.MEP.Tags.CmdMepElevationTags")
            {
                ToolTip = "Tự động gán nhãn cao độ đáy (BOP/Invert Elevation) cho ống gió và ống nước.",
                LargeImage = LoadImage("icon_align_32.png"),
                Image = LoadImage("icon_align_16.png")
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
        #endregion
    }
}

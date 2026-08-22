using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Class chịu trách nhiệm duy nhất cho việc dựng Ribbon UI cho KhimTools theo chuẩn chuyên nghiệp:
    ///   Tab: "K-TOOLS"
    ///   Panel 1: "Rebar"            — SplitButton (Column Rebar main + sub-items), Beam Rebar, Cover Setup
    ///   Panel 2: "Join / Geometry"  — Join Elements (large), Join Slabs + Unjoin Slabs (stacked legacy)
    /// </summary>
    public static class RibbonBuilder
    {
        // Ribbon Names Constants
        public const string TabName = "K-TOOLS";
        public const string RebarPanelName = "Rebar";
        public const string JoinPanelName = "Join / Geometry";
        public const string ExportPanelName = "Publish / Export";

        public static void BuildRibbon(UIControlledApplication application)
        {
            CreateTabSafely(application, TabName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            BuildRebarPanel(application, assemblyPath);
            BuildJoinPanel(application, assemblyPath);
            BuildExportPanel(application, assemblyPath);
        }

        // ══════════════════════════════════════════════════════════════════
        // PANEL 1: REBAR
        // ══════════════════════════════════════════════════════════════════
        private static void BuildRebarPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, RebarPanelName);

            // 1. SplitButton: Column Rebar (Main action = CmdColumnRebar auto-detect)
            var splitButtonData = new SplitButtonData(
                "ColumnRebarSplitButton",
                "Column" + Environment.NewLine + "Rebar")
            {
                ToolTip = "Bố trí thép cột tự động (phát hiện vuông/tròn từ phần tử đang chọn)."
            };

            var splitButton = panel.AddItem(splitButtonData) as SplitButton;
            if (splitButton != null)
            {
                // Main / First item: Auto-detect column rebar
                AddPushButton(splitButton, "CmdColumnRebar", "Column Rebar (Auto-detect)",
                    "KhimTools.RebarTool.Commands.CmdColumnRebar", assemblyPath,
                    "Tự động phát hiện loại cột (vuông/tròn) và mở giao diện phù hợp.",
                    "rebar_col_32.png", "rebar_col_16.png");

                // Sub-item 1: Rectangular columns batch
                AddPushButton(splitButton, "CmdMultiColumnRebar", "Cột Vuông / Chữ Nhật 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột vuông/chữ nhật.",
                    "rebar_col_32.png", "rebar_col_rect_16.png");

                // Sub-item 2: Round columns batch
                AddPushButton(splitButton, "CmdMultiRoundColumnRebar", "Cột Tròn 2.0",
                    "KhimTools.RebarTool.Commands.CmdMultiRoundColumnRebar", assemblyPath,
                    "Giao diện thiết lập & tạo thép hàng loạt cho cột tròn.",
                    "rebar_col_circ_32.png", "rebar_col_circ_16.png");

                splitButton.AddSeparator();

                // Sub-item 3: Column Drawing
                AddPushButton(splitButton, "CmdColumnDrawing", "Column Drawing",
                    "KhimTools.RebarTool.Commands.CmdColumnDrawing", assemblyPath,
                    "Tự động xuất bản vẽ mặt cắt 2D & thống kê thép cột.",
                    "rebar_col_32.png", "rebar_draw_16.png");

                // Sub-item 4: Update Column Drawing
                AddPushButton(splitButton, "CmdUpdateColumnDrawing", "Update Drawing",
                    "KhimTools.RebarTool.Commands.CmdUpdateColumnDrawing", assemblyPath,
                    "Đồng bộ cập nhật lại bản vẽ 2D đã xuất theo mô hình thép mới nhất.",
                    "rebar_col_32.png", "rebar_draw_16.png");
            }

            // 2. Large button: Beam Rebar
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

            // 3. Large button: Slab Rebar
            var slabData = new PushButtonData(
                "CmdSlabRebar",
                "Slab" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdSlabRebar")
            {
                ToolTip = "Bố trí thép sàn tự động (Slab Rebar v2.5).",
                LongDescription = "Bố trí thép sàn 2 lớp (Bottom/Top Mat), thép mũ gối (Top Support Hats), " +
                    "thép chân chó (High Chairs) và thép gia cường lỗ mở (Opening Trim Bars).",
                LargeImage = LoadImage("rebar_slab_32.png"),
                Image = LoadImage("rebar_slab_16.png")
            };
            panel.AddItem(slabData);

            // 4. Large button: Foundation Rebar
            var fdnData = new PushButtonData(
                "CmdFoundationRebar",
                "Foundation" + Environment.NewLine + "Rebar",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdFoundationRebar")
            {
                ToolTip = "Bố trí thép móng tự động (Foundation Rebar v2.5).",
                LongDescription = "Bố trí thép lưới dưới/lưới trên móng, thép đai mép và thép chờ cột theo TCVN 5574 & Eurocode 2/7.",
                LargeImage = LoadImage("rebar_fdn_32.png"),
                Image = LoadImage("rebar_fdn_16.png")
            };
            // 5. Large button: Section Cut
            var sectionData = new PushButtonData(
                "CmdSectionCut",
                "Section" + Environment.NewLine + "Cut",
                assemblyPath,
                "KhimTools.SectionCutTool.Commands.CmdSectionCut")
            {
                ToolTip = "Tự động tạo mặt cắt dọc & ngang (Section Views) phục vụ bản vẽ thép.",
                LongDescription = "Hỗ trợ cắt dọc theo trục tim và cắt ngang theo % hoặc khoảng cách đều cho Dầm, Cột, Vách, Sàn, Móng. Tự động đặt tên, gán View Template, scale và crop box.",
                LargeImage = LoadImage("rebar_draw_16.png") ?? LoadImage("rebar_beam_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(sectionData);

            // 6. Small button: Cover Setup
            var coverData = new PushButtonData(
                "CmdProjectCoverSetup",
                "Cover" + Environment.NewLine + "Setup",
                assemblyPath,
                "KhimTools.RebarTool.Commands.CmdProjectCoverSetup")
            {
                ToolTip = "Cấu hình Lớp bê tông bảo vệ (Concrete Cover) toàn dự án.",
                Image = LoadImage("rebar_cover_16.png")
            };
            panel.AddItem(coverData);
        }

        // ══════════════════════════════════════════════════════════════════
        // PANEL 2: JOIN / GEOMETRY
        // ══════════════════════════════════════════════════════════════════
        private static void BuildJoinPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, JoinPanelName);

            // 1. Large button: Join Elements
            var joinElementsData = new PushButtonData(
                "CmdJoinElements",
                "Join" + Environment.NewLine + "Elements",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.CmdJoinElements")
            {
                ToolTip = "Mở công cụ Join/Unjoin/Switch chuyên nghiệp cho tất cả loại cấu kiện.",
                LongDescription = "Hỗ trợ join/unjoin/switch geometry giữa bất kỳ cặp Category: " +
                    "Floors, Walls, Columns, Beams, Foundations, Roofs, Ceilings. " +
                    "Với Category Matching rules, Scope selector, Terminal Output realtime, và Template Save/Load.",
                LargeImage = LoadImage("icon_join_32.png"),
                Image = LoadImage("icon_join_16.png")
            };
            panel.AddItem(joinElementsData);

            // 2. Stacked small buttons for legacy commands
            var joinSlabData = new PushButtonData(
                "JoinSlabsLegacy",
                "Join Slabs",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.JoinSlabsCommand")
            {
                ToolTip = "Join sàn (phiên bản legacy — TaskDialog).",
                Image = LoadImage("icon_join_16.png")
            };

            var unjoinSlabData = new PushButtonData(
                "UnjoinSlabsLegacy",
                "Unjoin Slabs",
                assemblyPath,
                "KhimTools.SlabJoin.Commands.UnjoinSlabsCommand")
            {
                ToolTip = "Unjoin sàn (phiên bản legacy — TaskDialog).",
                Image = LoadImage("icon_unjoin_16.png")
            };

            panel.AddStackedItems(joinSlabData, unjoinSlabData);
        }

        // ══════════════════════════════════════════════════════════════════
        // PANEL 3: PUBLISH / EXPORT
        // ══════════════════════════════════════════════════════════════════
        private static void BuildExportPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ExportPanelName);

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

            var alignVpData = new PushButtonData(
                "CmdAlignViewport",
                "Align" + Environment.NewLine + "Viewport",
                assemblyPath,
                "KhimTools.ViewportAlign.Commands.CmdAlignViewport")
            {
                ToolTip = "Đồng bộ và căn chỉnh vị trí Viewport trên nhiều Sheet (Bản vẽ).",
                LongDescription = "Chọn một Viewport nguồn làm chuẩn, sau đó tự động căn chỉnh vị trí các Viewport trên danh sách Sheet được chọn trùng khớp 100%. Tự động bỏ qua Legends và Schedules.",
                LargeImage = LoadImage("rebar_draw_16.png") ?? LoadImage("export_sheet_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(alignVpData);

            var updateDetailNumData = new PushButtonData(
                "CmdUpdateDetailNumbers",
                "Update" + Environment.NewLine + "Detail No",
                assemblyPath,
                "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers")
            {
                ToolTip = "Tự động trích xuất và cập nhật số hiệu chi tiết (Detail Number) từ tên View.",
                LongDescription = "Hỗ trợ trích xuất CW, W hoặc pattern tùy biến theo Regex, tự động thêm đuôi .1, .2 chống trùng lặp trên cùng Sheet.",
                LargeImage = LoadImage("rebar_draw_16.png") ?? LoadImage("export_sheet_32.png"),
                Image = LoadImage("rebar_draw_16.png")
            };
            panel.AddItem(updateDetailNumData);
        }

        // ══════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════

        private static void AddPushButton(SplitButton parent, string name, string text,
            string className, string assemblyPath, string tooltip, string largeImage, string smallImage)
        {
            var data = new PushButtonData(name, text, assemblyPath, className)
            {
                ToolTip = tooltip,
                LargeImage = LoadImage(largeImage),
                Image = LoadImage(smallImage)
            };
            parent.AddPushButton(data);
        }

        private static void CreateTabSafely(UIControlledApplication application, string tabName)
        {
            try { application.CreateRibbonTab(tabName); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            var existingPanel = application
                .GetRibbonPanels(tabName)
                .FirstOrDefault(p => string.Equals(p.Name, panelName, StringComparison.OrdinalIgnoreCase));
            return existingPanel ?? application.CreateRibbonPanel(tabName, panelName);
        }

        private static BitmapImage LoadImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            try
            {
                string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                var uri = new Uri($"pack://application:,,,/{assemblyName};component/Resources/{fileName}", UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            catch
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(n => n.EndsWith("Resources." + fileName, StringComparison.OrdinalIgnoreCase));
                    if (resourceName != null)
                    {
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            return bitmap;
                        }
                    }
                }
                catch { }
                return null;
            }
        }
    }
}

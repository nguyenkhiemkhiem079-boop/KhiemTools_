using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KhimTools.Mini
{
    public class App : IExternalApplication
    {
        public const string TabName = "K-TOOL Mini";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbon(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("K-TOOL Mini", "Error initializing K-TOOL Mini: " + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static void CreateRibbon(UIControlledApplication app)
        {
            try
            {
                app.CreateRibbonTab(TabName);
            }
            catch { }

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // ── PANEL 1: TOOLS (Core Link & Tags) ──
            RibbonPanel toolsPanel = GetOrCreatePanel(app, TabName, "Tools");
            if (toolsPanel != null)
            {
                var elementTagsData = new PushButtonData(
                    "CmdElementTags",
                    "Element Tags",
                    assemblyPath,
                    "KhimTools.ElementTags.Commands.CmdElementTags")
                {
                    ToolTip = "Quản lý và gán thẻ Tag hàng loạt cho các đối tượng trong View hiện hành.",
                    LargeImage = LoadImage("icon_mep_tags_32.png"),
                    Image = LoadImage("icon_mep_tags_16.png")
                };
                toolsPanel.AddItem(elementTagsData);

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
                toolsPanel.AddItem(copyLinkData);
            }

            // ── PANEL 2: LAYOUT ──
            RibbonPanel layoutPanel = GetOrCreatePanel(app, TabName, "Layout");
            if (layoutPanel != null)
            {
                var layoutPulldownData = new PulldownButtonData("KhimLayoutPulldown", "Layout")
                {
                    ToolTip = "Các công cụ dàn trang, quản lý bản vẽ, căn chỉnh và tạo Sheet.",
                    LargeImage = LoadImage("icon_align_32.png"),
                    Image = LoadImage("icon_align_16.png")
                };
                var layoutPulldown = layoutPanel.AddItem(layoutPulldownData) as PulldownButton;
                if (layoutPulldown != null)
                {
                    AddPulldownItem(layoutPulldown, "CmdSheetGen", "Create Sheets (CSV)", "KhimTools.SheetGen.Commands.CmdSheetGen", assemblyPath, "export_sheet_16.png");
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
            }

            // ── PANEL 3: VIEW TOOLS ──
            RibbonPanel viewPanel = GetOrCreatePanel(app, TabName, "View");
            if (viewPanel != null)
            {
                var viewToolsPulldownData = new PulldownButtonData("KhimViewToolsPulldown", "View Tools")
                {
                    ToolTip = "Các công cụ nâng cao hỗ trợ tạo Section Box, Callout Pro và sinh View liên quan.",
                    LargeImage = LoadImage("icon_sectionbox_32.png"),
                    Image = LoadImage("icon_sectionbox_16.png")
                };
                var viewToolsPulldown = viewPanel.AddItem(viewToolsPulldownData) as PulldownButton;
                if (viewToolsPulldown != null)
                {
                    AddPulldownItem(viewToolsPulldown, "CmdSectionBox", "Section Box Pro", "KhimTools.SectionBox.Commands.CmdSectionBox", assemblyPath, "icon_sectionbox_16.png");
                    AddPulldownItem(viewToolsPulldown, "CmdCalloutPro", "Callout Pro", "KhimTools.CalloutPro.Commands.CmdCalloutPro", assemblyPath, "icon_callout_pro_16.png");
                    AddPulldownItem(viewToolsPulldown, "CmdViewFromCallout", "Create View from Callout", "KhimTools.ViewFromCallout.Commands.CmdViewFromCallout", assemblyPath, "icon_view_callout_16.png");
                }
            }

            // ── PANEL 4: PUBLISH (Sheet Exporter) ──
            RibbonPanel publishPanel = GetOrCreatePanel(app, TabName, "Publish");
            if (publishPanel != null)
            {
                var sheetExportData = new PushButtonData(
                    "CmdSheetExport",
                    "Sheet" + Environment.NewLine + "Exporter",
                    assemblyPath,
                    "KhimTools.SheetExport.Commands.CmdSheetExport")
                {
                    ToolTip = "Công cụ Batch Print & Export Sheet/View chuyên nghiệp (PDF, DWG, Issue Manager).",
                    LongDescription = "Hỗ trợ Naming Templates, Issue Revision Diffing, tự động tạo file Excel Transmittal Register, QA Log và Watermark.",
                    LargeImage = LoadImage("export_sheet_32.png"),
                    Image = LoadImage("export_sheet_16.png")
                };
                publishPanel.AddItem(sheetExportData);
            }
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

        private static void AddPulldownItem(PulldownButton pulldown, string name, string text, string className, string assemblyPath, string iconName)
        {
            var btnData = new PushButtonData(name, text, assemblyPath, className)
            {
                Image = LoadImage(iconName),
                LargeImage = LoadImage(iconName)
            };
            pulldown.AddPushButton(btnData);
        }

        private static BitmapSource LoadImage(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string name = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(name)) return null;

                using (var stream = assembly.GetManifestResourceStream(name))
                {
                    if (stream == null) return null;
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return decoder.Frames.Count > 0 ? decoder.Frames[0] : null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}

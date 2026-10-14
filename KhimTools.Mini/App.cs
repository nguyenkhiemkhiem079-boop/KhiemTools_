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
        public const string PanelName = "Tools";

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
            catch
            {
                // Tab may already exist if reloaded
            }

            RibbonPanel panel = null;
            var panels = app.GetRibbonPanels(TabName);
            panel = panels.FirstOrDefault(p => p.Name.Equals(PanelName, StringComparison.OrdinalIgnoreCase));
            if (panel == null)
            {
                try
                {
                    panel = app.CreateRibbonPanel(TabName, PanelName);
                }
                catch
                {
                    panel = app.GetRibbonPanels(TabName).FirstOrDefault(p => p.Name.Equals(PanelName, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (panel == null) return;

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // 1. Element Tags PushButton
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
            panel.AddItem(elementTagsData);

            // 2. Copy Link Elements PushButton
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
            panel.AddItem(copyLinkData);
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

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.OverrideTool.Forms;

namespace KhimTools.OverrideTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdGraphicOverdrive : IExternalCommand
    {
        // Giữ reference singleton window (Modeless)
        private static GraphicOverdriveWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;

                // Nếu window đang mở thì focus lại, không mở mới
                if (_window != null && _window.IsVisible)
                {
                    _window.Activate();
                    _window.Focus();
                    return Result.Succeeded;
                }

                _window = new GraphicOverdriveWindow(uiApp);
                _window.Closed += (s, e) => _window = null;

                // Modeless: dùng Show() không chặn Revit UI
                _window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
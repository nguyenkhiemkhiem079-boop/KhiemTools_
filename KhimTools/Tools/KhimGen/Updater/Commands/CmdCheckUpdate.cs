using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Tools.Updater.Services;
using KhimTools.Tools.Updater.Views;

namespace KhimTools.Updater.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCheckUpdate : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var updateService = new UpdateService();
                var task = updateService.CheckForUpdatesAsync();
                task.Wait();
                var updateInfo = task.Result;

                var window = new UpdaterWindow(updateInfo, updateService);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("KhimTools Update", "Lỗi kiểm tra cập nhật: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Tools.Workspace.Views;

namespace KhimTools.Tools.Workspace.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdToggleWorkspace : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                DockablePane pane = commandData.Application.GetDockablePane(KhimWorkspacePane.PaneId);
                if (pane == null)
                {
                    TaskDialog.Show("KhimTools Workspace", "Dockable Pane chưa được đăng ký trong phiên làm việc hiện tại.");
                    return Result.Failed;
                }

                if (pane.IsShown())
                {
                    pane.Hide();
                }
                else
                {
                    pane.Show();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("KhimTools Workspace", "Lỗi: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}

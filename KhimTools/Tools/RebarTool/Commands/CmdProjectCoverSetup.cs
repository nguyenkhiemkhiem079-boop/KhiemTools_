using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.RebarTool.Forms;

namespace KhimTools.RebarTool.Commands
{
    /// <summary>
    /// Lệnh "Project Cover Setup" — Cấu hình & cập nhật Lớp bê tông bảo vệ (Concrete Cover)
    /// đồng bộ cho tất cả cấu kiện trong dự án (Cột, Dầm, Sàn, Móng).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdProjectCoverSetup : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;

                if (doc == null) return Result.Cancelled;

                var form = new ProjectCoverSetupForm(doc);
                form.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Project Cover Setup Error", $"[{ex.GetType().Name}] {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}

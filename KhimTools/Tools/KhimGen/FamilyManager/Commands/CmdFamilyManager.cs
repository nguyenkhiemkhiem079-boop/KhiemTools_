using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.FamilyManager.Forms;

namespace KhimTools.FamilyManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdFamilyManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Vui lòng mở một dự án Revit trước khi chạy Family Manager.";
                return Result.Failed;
            }

            if (uidoc.Document.IsFamilyDocument)
            {
                TaskDialog.Show("K-TOOLS", "Load Family nhanh chỉ hoạt động trong Project Document.");
                return Result.Cancelled;
            }

            try
            {
                var window = new FamilyManagerWindow(uidoc.Document);
                window.ShowDialog();
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

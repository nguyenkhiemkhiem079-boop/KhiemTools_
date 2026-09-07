using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Structural.QuickStructure.Forms;

namespace KhimTools.Structural.QuickStructure.Commands
{
    /// <summary>
    /// Lệnh mở công cụ dựng nhanh hệ kết cấu (Quick Structure) từ lưới trục.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickStructure : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Vui lòng mở một dự án Revit trước khi chạy Quick Structure.";
                return Result.Failed;
            }

            try
            {
                var window = new QuickStructureWindow(uidoc.Document);
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

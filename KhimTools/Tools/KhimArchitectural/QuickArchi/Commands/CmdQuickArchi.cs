using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Architectural.QuickArchi.Forms;

namespace KhimTools.Architectural.QuickArchi.Commands
{
    /// <summary>
    /// Lệnh mở công cụ dựng nhanh các phần tử kiến trúc (Quick Archi) từ đường nét Model/CAD.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickArchi : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Vui lòng mở một dự án Revit trước khi chạy Quick Archi.";
                return Result.Failed;
            }

            try
            {
                var window = new QuickArchiWindow(uidoc);
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

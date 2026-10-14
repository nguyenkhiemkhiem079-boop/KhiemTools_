using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
using KhimTools.Architectural;
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
            var timer = Stopwatch.StartNew();
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
                timer.Stop();
                ArchitecturalDiagnostics.Log(nameof(CmdQuickArchi), uidoc.Document, "open-quick-archi",
                    0, window.HasCompletedOperation ? 1 : 0, 0, timer.Elapsed);
                return window.HasCompletedOperation ? Result.Succeeded :
                    window.OperationExecuted ? Result.Failed : Result.Cancelled;
            }
            catch (Exception ex)
            {
                timer.Stop();
                message = ex.Message;
                ArchitecturalDiagnostics.Log(nameof(CmdQuickArchi), uidoc.Document, "open-quick-archi",
                    0, 0, 1, timer.Elapsed, ex.GetType().FullName);
                return Result.Failed;
            }
        }
    }
}

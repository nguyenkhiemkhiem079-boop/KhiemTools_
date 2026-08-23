using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.OverrideTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickHalftone : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null) return Result.Cancelled;

                var doc = uidoc.Document;
                var view = doc.ActiveView;
                var selIds = uidoc.Selection.GetElementIds().ToList();

                if (!selIds.Any())
                {
                    TaskDialog.Show("K-TOOLS Graphic Overdrive", "Vui lòng chọn ít nhất 1 đối tượng trong View để áp dụng/bật tắt Halftone.");
                    return Result.Cancelled;
                }

                using (var t = new Transaction(doc, "KhimTools: Quick Halftone Toggle"))
                {
                    t.Start();

                    // Kiểm tra trạng thái halftone của phần tử đầu tiên để toggle
                    var firstOgs = view.GetElementOverrides(selIds.First());
                    bool targetHalftone = !firstOgs.Halftone;

                    foreach (var id in selIds)
                    {
                        var ogs = view.GetElementOverrides(id);
                        ogs = ogs.SetHalftone(targetHalftone);
                        view.SetElementOverrides(id, ogs);
                    }

                    t.Commit();
                }

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
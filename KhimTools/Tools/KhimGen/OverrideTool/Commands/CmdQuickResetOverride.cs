using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.OverrideTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickResetOverride : IExternalCommand
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
                    TaskDialog.Show("K-TOOLS Graphic Overdrive", "Vui lòng chọn ít nhất 1 đối tượng trong View để xóa bỏ (Reset) toàn bộ Override.");
                    return Result.Cancelled;
                }

                using (var t = new Transaction(doc, "KhimTools: Reset All Graphic Overrides"))
                {
                    t.Start();
                    var emptyOgs = new OverrideGraphicSettings();

                    foreach (var id in selIds)
                    {
                        view.SetElementOverrides(id, emptyOgs);
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
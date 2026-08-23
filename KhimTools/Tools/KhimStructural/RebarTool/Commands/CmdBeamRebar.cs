using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.RebarTool.Forms;

namespace KhimTools.RebarTool.Commands
{
    /// <summary>
    /// Lệnh "Beam Rebar": Tự động phát hiện dầm (Structural Framing) được chọn trong viewport
    /// và mở Form bố trí thép dầm (BeamReinforcementForm).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdBeamRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;
                if (doc == null) return Result.Cancelled;

                var selectedIds = uidoc.Selection.GetElementIds();
                List<FamilyInstance> selectedBeams = selectedIds.Any()
                    ? selectedIds.Select(id => doc.GetElement(id)).OfType<FamilyInstance>()
                        .Where(c => c.Category.IsCategory(BuiltInCategory.OST_StructuralFraming)).ToList()
                    : new List<FamilyInstance>();

                List<FamilyInstance> allBeams = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                if (!allBeams.Any())
                {
                    TaskDialog.Show("Beam Rebar", "Không tìm thấy dầm (Structural Framing) nào trong mô hình.");
                    return Result.Cancelled;
                }

                var form = new BeamReinforcementForm(doc, allBeams, selectedBeams);
                form.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Beam Rebar Error", $"[{ex.GetType().Name}] {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}

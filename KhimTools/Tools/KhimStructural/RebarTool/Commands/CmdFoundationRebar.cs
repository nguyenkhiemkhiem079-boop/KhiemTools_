using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.RebarTool.Forms;

namespace KhimTools.RebarTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdFoundationRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Trích xuất danh sách Móng (Structural Foundations)
                var selectedIds = uidoc.Selection.GetElementIds();
                var foundations = new List<FamilyInstance>();

                if (selectedIds.Count > 0)
                {
                    foreach (ElementId id in selectedIds)
                    {
                        Element elem = doc.GetElement(id);
                        if (elem is FamilyInstance fdn && elem.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralFoundation)
                        {
                            foundations.Add(fdn);
                        }
                    }
                }

                if (!foundations.Any())
                {
                    foundations = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .ToList();
                }

                if (!foundations.Any())
                {
                    KhimDialogHelper.ShowWarning("Không tìm thấy đối tượng Móng (Structural Foundation) nào trong mô hình.");
                    return Result.Cancelled;
                }

                var form = new FoundationReinforcementForm(doc, foundations);
                form.ShowDialog();

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

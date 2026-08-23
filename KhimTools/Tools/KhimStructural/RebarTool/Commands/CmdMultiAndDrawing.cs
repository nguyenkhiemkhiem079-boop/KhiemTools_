using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.RebarTool.Forms;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Commands
{
    /// <summary>"Multi-Column Rebar 2.0" — tạo thép hàng loạt cho cột vuông/chữ nhật đã chọn.</summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMultiColumnRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                var selectedIds = uidoc.Selection.GetElementIds();
                List<FamilyInstance> columns = selectedIds.Any()
                    ? selectedIds.Select(id => doc.GetElement(id)).OfType<FamilyInstance>().ToList()
                    : new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .Where(c => !IsCircular(c))
                        .ToList();

                if (!columns.Any())
                {
                    TaskDialog.Show("Multi-Column Rebar 2.0", "Chưa chọn cột vuông/chữ nhật nào và model không có cột phù hợp.");
                    return Result.Cancelled;
                }

                var form = new RectangularColumnReinforcementForm(doc, columns);
                form.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Multi-Column Rebar Error", $"[{ex.GetType().Name}] {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        private static bool IsCircular(FamilyInstance col)
        {
            string typeName = col.Symbol?.Name?.ToLowerInvariant() ?? "";
            string famName = col.Symbol?.Family?.Name?.ToLowerInvariant() ?? "";
            return typeName.Contains("round") || typeName.Contains("circular") || typeName.Contains("tron")
                || famName.Contains("round") || famName.Contains("circular") || famName.Contains("tron");
        }
    }

    /// <summary>"Multi-Round Column Rebar 2.0" — chạy hàng loạt cho nhiều cột tròn cùng lúc, dùng chung form hiện tại.</summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMultiRoundColumnRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                var selectedIds = uidoc.Selection.GetElementIds();
                List<FamilyInstance> columns = selectedIds.Any()
                    ? selectedIds.Select(id => doc.GetElement(id)).OfType<FamilyInstance>().ToList()
                    : new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .ToList();

                if (!columns.Any())
                {
                    TaskDialog.Show("Multi-Round Column Rebar", "Chưa chọn cột nào và model không có cột.");
                    return Result.Cancelled;
                }

                var form = new CircularColumnReinforcementForm(doc, columns);
                form.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Multi-Round Column Error", $"[{ex.GetType().Name}] {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}

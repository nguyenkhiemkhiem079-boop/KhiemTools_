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
    /// Lệnh chính "Column Rebar": Tự động phát hiện loại cột (vuông/chữ nhật hoặc tròn)
    /// dựa trên lựa chọn của người dùng hoặc mở Dialog chọn loại cột.
    /// Bọc Try-Catch cấp cao nhất để hiển thị chi tiết Exception nếu xảy ra lỗi.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdColumnRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;

                if (doc == null) return Result.Cancelled;

                // 1. Kiểm tra phần tử đang được chọn trong Revit
                var selectedIds = uidoc.Selection.GetElementIds();
                List<FamilyInstance> selectedColumns = selectedIds.Any()
                    ? selectedIds.Select(id => doc.GetElement(id)).OfType<FamilyInstance>()
                        .Where(c => c.Category.IsCategory(BuiltInCategory.OST_StructuralColumns)).ToList()
                    : new List<FamilyInstance>();

                bool hasSelection = selectedColumns.Any();

                // Nếu user đã chọn cột trong model (Pre-selection từ viewport Revit)
                if (hasSelection)
                {
                    bool isCirc = selectedColumns.Any(c => IsCircular(c));
                    List<FamilyInstance> allMatchingColumns = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .Where(c => isCirc ? IsCircular(c) : !IsCircular(c))
                        .ToList();

                    if (isCirc)
                    {
                        var circularForm = new CircularColumnReinforcementForm(doc, allMatchingColumns, selectedColumns);
                        circularForm.ShowDialog();
                    }
                    else
                    {
                        var rectForm = new RectangularColumnReinforcementForm(doc, allMatchingColumns, selectedColumns);
                        rectForm.ShowDialog();
                    }
                    return Result.Succeeded;
                }

                // 2. Nếu chưa chọn cột, mở TaskDialog cho phép chọn loại tiết diện cột
                var dialog = new TaskDialog("Column Rebar Tool")
                {
                    MainInstruction = "Chọn loại tiết diện cột cần bố trí thép",
                    MainContent = "Bạn chưa chọn trước cột trong mô hình. Hãy chọn loại cột:",
                    CommonButtons = TaskDialogCommonButtons.Close
                    // LƯU Ý: Không gán DefaultButton = TaskDialogResult.CommandLink1 vì Revit API
                    // sẽ ném System.ArgumentException ("Corresponding button not found. Parameter name: defaultButton")
                };

                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "⏹ Cột Vuông / Chữ Nhật", "Mở công cụ bố trí thép cho cột hình chữ nhật/vuông.");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "⏺ Cột Tròn", "Mở công cụ bố trí thép cho cột tròn.");

                TaskDialogResult result = dialog.Show();

                if (result == TaskDialogResult.CommandLink1)
                {
                    List<FamilyInstance> rectColumns = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .Where(c => !IsCircular(c))
                        .ToList();

                    if (!rectColumns.Any())
                    {
                        TaskDialog.Show("Column Rebar", "Không tìm thấy cột vuông/chữ nhật nào trong mô hình.");
                        return Result.Cancelled;
                    }

                    var rectForm = new RectangularColumnReinforcementForm(doc, rectColumns);
                    rectForm.ShowDialog();
                }
                else if (result == TaskDialogResult.CommandLink2)
                {
                    List<FamilyInstance> circColumns = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .Where(c => IsCircular(c))
                        .ToList();

                    if (!circColumns.Any())
                    {
                        TaskDialog.Show("Column Rebar", "Không tìm thấy cột tròn nào trong mô hình.");
                        return Result.Cancelled;
                    }

                    var circularForm = new CircularColumnReinforcementForm(doc, circColumns);
                    circularForm.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                string details = $"Lỗi thực thi lệnh Column Rebar:\n\n[{ex.GetType().Name}] {ex.Message}\n\nStackTrace:\n{ex.StackTrace}";
                TaskDialog.Show("Column Rebar - Failure Details", details);
                return Result.Failed;
            }
        }

        public static bool IsCircular(FamilyInstance col)
        {
            if (col == null || col.Symbol == null) return false;
            string typeName = col.Symbol.Name?.ToLowerInvariant() ?? "";
            string famName = col.Symbol.Family?.Name?.ToLowerInvariant() ?? "";

            Parameter diaParam = col.Symbol.LookupParameter("Diameter")
                              ?? col.Symbol.LookupParameter("Radius")
                              ?? col.LookupParameter("Diameter");
            if (diaParam != null && diaParam.HasValue) return true;

            return typeName.Contains("round") || typeName.Contains("circular") || typeName.Contains("tron")
                || famName.Contains("round") || famName.Contains("circular") || famName.Contains("tron");
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdColumnRebarV2 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return new CmdColumnRebar().Execute(commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Column Rebar V2 Error", $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}

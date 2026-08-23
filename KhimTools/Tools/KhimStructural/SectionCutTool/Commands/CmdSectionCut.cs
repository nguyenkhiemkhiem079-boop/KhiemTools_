using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.SectionCutTool.Forms;

namespace KhimTools.SectionCutTool.Commands
{
    /// <summary>
    /// Bộ lọc đối tượng kết cấu (Dầm, Cột, Vách, Sàn, Móng) khi người dùng Pick chọn trên Revit.
    /// </summary>
    public class StructuralElementSelectionFilter : ISelectionFilter
    {
        private static readonly HashSet<BuiltInCategory> StructuralCategories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFoundation
        };

        public bool AllowElement(Element elem)
        {
            if (elem == null || elem.Category == null) return false;
            return StructuralCategories.Any(cat => elem.Category.IsCategory(cat));
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }

    /// <summary>
    /// Lệnh "Section Cut": Nhấn vào tool -> Pick chọn cấu kiện trên Revit -> Tool mở -> Chọn cắt mặt cắt dọc / ngang.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSectionCut : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;
                if (doc == null) return Result.Cancelled;

                var structuralCategories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_StructuralFoundation
                };

                var filter = new ElementMulticategoryFilter(structuralCategories);

                // 1. Kiểm tra nếu đã có cấu kiện được chọn sẵn (Pre-selection)
                var selectedIds = uidoc.Selection.GetElementIds();
                List<Element> pickedElements = selectedIds.Any()
                    ? selectedIds.Select(id => doc.GetElement(id))
                        .Where(e => e != null && structuralCategories.Any(cat => e.Category != null && e.Category.IsCategory(cat)))
                        .ToList()
                    : new List<Element>();

                // 2. Nếu chưa chọn sẵn -> Bật bước tương tác Pick chọn cấu kiện trên Revit (Interactive Pick)
                if (!pickedElements.Any())
                {
                    try
                    {
                        var selectionFilter = new StructuralElementSelectionFilter();
                        string promptMsg = LanguageManager.IsEnglish
                            ? "Select structural elements (Beams, Columns, Walls, Floors, Foundations) to cut sections, then click Finish on Options Bar..."
                            : "Chọn các cấu kiện kết cấu (Dầm, Cột, Vách, Sàn, Móng) cần cắt Section, sau đó bấm Finish trên thanh Options Bar...";

                        var pickedRefs = uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            selectionFilter,
                            promptMsg);

                        if (pickedRefs != null && pickedRefs.Any())
                        {
                            pickedElements = pickedRefs
                                .Select(r => doc.GetElement(r))
                                .Where(e => e != null && structuralCategories.Any(cat => e.Category != null && e.Category.IsCategory(cat)))
                                .ToList();
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // Người dùng bấm ESC khi pick -> Thoát nhẹ nhàng không báo lỗi
                        return Result.Cancelled;
                    }
                }

                // 3. Lấy toàn bộ cấu kiện kết cấu trong Document để hiển thị đầy đủ
                List<Element> allStructuralElements = new FilteredElementCollector(doc)
                    .WherePasses(filter)
                    .WhereElementIsNotElementType()
                    .ToList();

                if (!allStructuralElements.Any() && !pickedElements.Any())
                {
                    TaskDialog.Show("Section Cut Tool",
                        LanguageManager.IsEnglish
                            ? "No structural elements (Beams, Columns, Walls, Floors, Foundations) found in the model."
                            : "Không tìm thấy cấu kiện kết cấu (Dầm, Cột, Vách, Sàn, Móng) nào trong mô hình.");
                    return Result.Cancelled;
                }

                // 4. Mở Form cắt Section với các cấu kiện đã pick
                using (var form = new SectionCutForm(doc, uidoc, allStructuralElements, pickedElements))
                {
                    form.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Section Cut Error", $"[{ex.GetType().Name}] {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}

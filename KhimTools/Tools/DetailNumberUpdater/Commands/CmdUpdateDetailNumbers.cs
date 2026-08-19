using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.DetailNumberUpdater.Forms;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.DetailNumberUpdater.Commands
{
    /// <summary>
    /// Command: Cập nhật Detail Number cho các Viewport trên Sheet từ tên View.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdUpdateDetailNumbers : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("Khim Tools", "Không tìm thấy tài liệu Revit đang mở.");
                return Result.Cancelled;
            }

            try
            {
                // Kiểm tra xem view hiện tại có phải là Sheet không
                ViewSheet sheet = doc.ActiveView as ViewSheet;

                if (sheet == null)
                {
                    // Nếu không phải đang đứng ở Sheet, hỗ trợ người dùng chọn 1 Sheet
                    var allSheets = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .Where(s => !s.IsPlaceholder)
                        .OrderBy(s => s.SheetNumber)
                        .ToList();

                    if (!allSheets.Any())
                    {
                        TaskDialog.Show("Khim Tools — Update Detail Numbers",
                            "Dự án không có Sheet nào hoặc bạn chưa mở Sheet. Vui lòng mở 1 Sheet để sử dụng tool.");
                        return Result.Cancelled;
                    }

                    sheet = allSheets.FirstOrDefault();
                }

                if (sheet == null)
                {
                    TaskDialog.Show("Khim Tools — Update Detail Numbers",
                        "Vui lòng mở một Sheet view để chạy công cụ cập nhật Detail Number.");
                    return Result.Cancelled;
                }

                var form = new UpdateDetailNumbersForm(doc, sheet);
                form.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Khim Tools — Lỗi Update Detail Numbers", $"Lỗi không mong đợi:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}

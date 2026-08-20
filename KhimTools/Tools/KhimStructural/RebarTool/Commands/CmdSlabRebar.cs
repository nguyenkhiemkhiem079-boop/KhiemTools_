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
    /// Lệnh chính "Slab Rebar": Tự động phát hiện Sàn (Floors) được chọn trong viewport Revit
    /// hoặc mở Form chọn danh sách sàn trong dự án để bố trí thép 2 lớp, thép mũ gối, thép chân chó.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSlabRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;
                if (doc == null) return Result.Cancelled;

                // 1. Kiểm tra sàn đang chọn trong viewport Revit
                var selectedIds = uidoc.Selection.GetElementIds();
                List<Floor> selectedFloors = selectedIds.Any()
                    ? selectedIds.Select(id => doc.GetElement(id)).OfType<Floor>()
                        .Where(f => f.Category.IsCategory(BuiltInCategory.OST_Floors)).ToList()
                    : new List<Floor>();

                // 2. Thu thập toàn bộ sàn trong dự án
                List<Floor> allFloors = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .WhereElementIsNotElementType()
                    .Cast<Floor>()
                    .OrderBy(f => doc.GetElement(f.LevelId)?.Name ?? "")
                    .ThenBy(f => f.Name)
                    .ToList();

                if (!allFloors.Any())
                {
                    KhimDialogHelper.ShowWarning("Không tìm thấy Sàn (Structural Floor) nào trong mô hình.");
                    return Result.Cancelled;
                }

                // 3. Hiển thị Form Bố trí Thép Sàn
                using (var form = new SlabReinforcementForm(doc, allFloors, selectedFloors))
                {
                    form.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                KhimDialogHelper.ShowError($"Lỗi khi chạy công cụ Bố trí Thép Sàn: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}

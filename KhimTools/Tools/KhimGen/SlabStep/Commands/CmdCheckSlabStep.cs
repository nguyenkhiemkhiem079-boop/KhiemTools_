using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.SlabStep.Services;

namespace KhimTools.SlabStep.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CmdCheckSlabStep : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                var selected = uidoc.Selection.GetElementIds();
                var steps = (selected.Count > 0 ? selected.Select(uidoc.Document.GetElement)
                    : new FilteredElementCollector(uidoc.Document, uidoc.ActiveView.Id).OfClass(typeof(FamilyInstance)).Cast<Element>())
                    .OfType<FamilyInstance>().Where(SlabStepAudit.HasAssociation).ToList();
                if (steps.Count == 0)
                {
                    TaskDialog.Show("Check Step", "Không có Step đã lưu liên kết trong phạm vi chọn/view. Step cũ: mở Slab Step → chọn hai sàn → cấu hình family/chiều → Kiểm tra Step cũ.");
                    return Result.Cancelled;
                }
                var results = steps.Select(s => new { Step = s, Result = SlabStepAudit.Check(s) }).ToList();
                var problems = results.Where(r => !r.Result.StartsWith("Giá trị đúng:") || !r.Result.Contains("Chiều đúng.")).ToList();
                if (problems.Count > 0) uidoc.Selection.SetElementIds(problems.Select(r => r.Step.Id).ToList());
                TaskDialog.Show("Check Step — Giá trị & chiều", $"Đã kiểm tra {results.Count} Step. Cần rà soát: {problems.Count}. Các Step cần rà soát được chọn trong mô hình.\n\n" +
                    string.Join("\n", results.Take(25).Select(r => "ID " + r.Step.Id + ": " + r.Result)) +
                    (results.Count > 25 ? "\nHiện 25 kết quả đầu. Chọn một nhóm nhỏ hơn để xem chi tiết." : ""));
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; TaskDialog.Show("Check Step", ex.Message); return Result.Failed; }
        }
    }
}

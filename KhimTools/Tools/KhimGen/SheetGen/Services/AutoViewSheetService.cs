using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace KhimTools.SheetGen.Services
{
    internal sealed class AutoViewRow
    {
        public bool Selected { get; set; }
        public ElementId ViewId { get; set; }
        public string ViewName { get; set; }
        public string ViewType { get; set; }
        public string SheetNumber { get; set; }
        public string Status { get; set; }
        public bool CreateNew { get; set; }
        public ElementId LevelId { get; set; }
        public ElementId ViewTypeId { get; set; }
        public ElementId TemplateId { get; set; } = ElementId.InvalidElementId;
    }

    internal static class AutoViewSheetService
    {
        internal static bool MatchesNumber(string viewName, string number)
            => !string.IsNullOrWhiteSpace(number) && Regex.IsMatch(viewName ?? "",
                @"(?<![\p{L}\p{N}])" + Regex.Escape(number) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase);

        internal static List<AutoViewRow> Preview(Document doc)
        {
            var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => !s.IsPlaceholder).ToList();
            var placed = new HashSet<ElementId>(new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(v => v.ViewId));
            return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted && v.ViewType != ViewType.DrawingSheet && v.ViewType != ViewType.Schedule && !placed.Contains(v.Id))
                .OrderBy(v => v.Name).Select(view =>
                {
                    var matches = sheets.Where(s => MatchesNumber(view.Name, s.SheetNumber)).ToList();
                    return new AutoViewRow { ViewId = view.Id, ViewName = view.Name, ViewType = view.ViewType.ToString(),
                        SheetNumber = matches.Count == 1 ? matches[0].SheetNumber : "",
                        Selected = false, Status = matches.Count == 1 ? "Đã gợi ý sheet — chọn để chạy" : matches.Count > 1 ? "Nhiều sheet phù hợp — chọn thủ công" : "Chọn sheet đích" };
                }).ToList();
        }

        internal static int Place(Document doc, IList<AutoViewRow> rows, double marginMm)
        {
            int count = 0;
            var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => !s.IsPlaceholder)
                .ToDictionary(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase);
            using (var tx = new Transaction(doc, "K-TOOLS - Auto place views on sheets"))
            {
                tx.Start();
                foreach (var row in rows.Where(r => r.Selected))
                {
                    using (var sub = new SubTransaction(doc))
                    {
                        sub.Start();
                        try
                        {
                            if (!sheets.TryGetValue(row.SheetNumber ?? "", out var sheet)) throw new InvalidOperationException("Sheet đích không tồn tại.");
                            if (row.CreateNew)
                            {
                                if (new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Any(v => v.Name.Equals(row.ViewName, StringComparison.OrdinalIgnoreCase)))
                                    throw new InvalidOperationException("Tên view đã tồn tại; không tạo trùng.");
                                var plan = ViewPlan.Create(doc, row.ViewTypeId, row.LevelId);
                                plan.Name = row.ViewName;
                                if (row.TemplateId != ElementId.InvalidElementId)
                                {
                                    if (!plan.IsValidViewTemplate(row.TemplateId)) throw new InvalidOperationException("Template không tương thích loại view.");
                                    plan.ViewTemplateId = row.TemplateId;
                                }
                                row.ViewId = plan.Id;
                                doc.Regenerate();
                            }
                            if (!Viewport.CanAddViewToSheet(doc, sheet.Id, row.ViewId)) throw new InvalidOperationException("View đã được đặt hoặc không thể thêm vào sheet.");
                            var outline = sheet.Outline;
                            double margin = marginMm / 304.8;
                            var min = new XYZ(outline.Min.U + margin, outline.Min.V + margin, 0);
                            var max = new XYZ(outline.Max.U - margin, outline.Max.V - margin, 0);
                            var obstacles = sheet.GetAllViewports().Select(id => Bounds(doc.GetElement(id) as Viewport)).ToList();
                            foreach (var schedule in new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(ScheduleSheetInstance)))
                            {
                                var box = schedule.get_BoundingBox(sheet);
                                if (box != null) obstacles.Add(new[] { box.Min, box.Max });
                            }
                            var viewport = Viewport.Create(doc, sheet.Id, row.ViewId, (min + max) / 2);
                            doc.Regenerate();
                            var bounds = Bounds(viewport);
                            var center = viewport.GetBoxCenter();
                            var offsetMin = bounds[0] - center;
                            var offsetMax = bounds[1] - center;
                            bool fitted = false;
                            double gap = 10 / 304.8;
                            for (double y = max.Y - offsetMax.Y; y >= min.Y - offsetMin.Y && !fitted; y -= gap)
                                for (double x = min.X - offsetMin.X; x <= max.X - offsetMax.X; x += gap)
                                {
                                    var candidate = new XYZ(x, y, 0);
                                    var a = candidate + offsetMin; var b = candidate + offsetMax;
                                    if (obstacles.Any(o => Overlaps(a, b, o[0], o[1], gap))) continue;
                                    viewport.SetBoxCenter(candidate);
                                    fitted = true; break;
                                }
                            if (!fitted) throw new InvalidOperationException("Không đủ chỗ trong vùng đặt view. Điều chỉnh scale hoặc bố cục/lề sheet.");
                            doc.Regenerate();
                            if (sub.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit không chấp nhận đặt view.");
                            row.Status = "Đã đặt"; row.Selected = false; count++;
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            if (row.CreateNew) row.ViewId = ElementId.InvalidElementId;
                            row.Status = "Bỏ qua: " + ex.Message;
                        }
                    }
                }
                if (tx.Commit() != TransactionStatus.Committed)
                {
                    foreach (var row in rows.Where(r => r.Status == "Đã đặt")) { row.Status = "Đã rollback — chưa đặt"; row.Selected = true; }
                    return 0;
                }
                foreach (var row in rows.Where(r => r.CreateNew && r.Status == "Đã đặt")) row.CreateNew = false;
            }
            return count;
        }

        private static XYZ[] Bounds(Viewport viewport)
        {
            var box = viewport.GetBoxOutline();
            var label = viewport.GetLabelOutline();
            if (label == null || label.MinimumPoint.IsAlmostEqualTo(label.MaximumPoint))
                return new[] { box.MinimumPoint, box.MaximumPoint };
            return new[] { new XYZ(Math.Min(box.MinimumPoint.X,label.MinimumPoint.X), Math.Min(box.MinimumPoint.Y,label.MinimumPoint.Y),0),
                new XYZ(Math.Max(box.MaximumPoint.X,label.MaximumPoint.X), Math.Max(box.MaximumPoint.Y,label.MaximumPoint.Y),0) };
        }

        private static bool Overlaps(XYZ a, XYZ b, XYZ c, XYZ d, double gap)
            => a.X < d.X + gap && b.X > c.X - gap && a.Y < d.Y + gap && b.Y > c.Y - gap;
    }
}

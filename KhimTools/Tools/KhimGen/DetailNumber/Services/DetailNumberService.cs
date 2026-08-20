using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace KhimTools.DetailNumberUpdater.Services
{
    public class DetailNumberPreviewItem
    {
        public Viewport Viewport { get; set; }
        public View View { get; set; }
        public string ViewName => View?.Name ?? "";
        public string CurrentDetailNumber { get; set; }
        public string NewDetailNumber { get; set; }
        public bool IsMatched { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    public static class DetailNumberService
    {
        public const string DefaultPattern = @"([A-Za-z0-9]+-CW\d+|[A-Za-z0-9]+-W\d+|CW\d+|W\d+)";

        /// <summary>
        /// Trích xuất mã Detail Number từ tên View dựa trên Regex pattern.
        /// </summary>
        public static string ExtractDetailNumber(string viewName, string pattern = DefaultPattern)
        {
            if (string.IsNullOrWhiteSpace(viewName)) return string.Empty;
            try
            {
                var match = Regex.Match(viewName, string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Value.Trim();
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// Tính toán trước danh sách Detail Number mới cho các Viewport trên Sheet.
        /// Tự động xử lý trùng lặp trong cùng 1 Sheet (ví dụ: CW25.1 cho phần tử trùng thứ 2).
        /// </summary>
        public static List<DetailNumberPreviewItem> GeneratePreview(Document doc, ViewSheet sheet, string pattern = DefaultPattern)
        {
            var result = new List<DetailNumberPreviewItem>();
            if (doc == null || sheet == null) return result;

            var viewports = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            // Thu thập các Detail Number hiện có trên sheet
            var existingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var vp in viewports)
            {
                var p = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (p != null && p.HasValue && !string.IsNullOrWhiteSpace(p.AsString()))
                {
                    existingNumbers.Add(p.AsString());
                }
            }

            var usedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var vp in viewports)
            {
                View view = doc.GetElement(vp.ViewId) as View;
                if (view == null) continue;

                string currentNum = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString() ?? "";
                string baseNum = ExtractDetailNumber(view.Name, pattern);

                var item = new DetailNumberPreviewItem
                {
                    Viewport = vp,
                    View = view,
                    CurrentDetailNumber = currentNum,
                    IsMatched = !string.IsNullOrEmpty(baseNum)
                };

                if (item.IsMatched)
                {
                    string finalNum = baseNum;
                    int suffix = 1;

                    // Nếu đã tồn tại hoặc đã được gán trước đó thì thêm đuôi .1, .2
                    while ((usedNumbers.Contains(finalNum) || (existingNumbers.Contains(finalNum) && finalNum != currentNum)))
                    {
                        finalNum = $"{baseNum}.{suffix}";
                        suffix++;
                    }

                    usedNumbers.Add(finalNum);
                    item.NewDetailNumber = finalNum;
                    item.IsSelected = true;
                }
                else
                {
                    item.NewDetailNumber = currentNum;
                    item.IsSelected = false;
                }

                result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Áp dụng cập nhật Detail Number cho các Viewport được chọn.
        /// </summary>
        public static (int Success, int Failed, List<string> Errors) ApplyDetailNumbers(
            Document doc, List<DetailNumberPreviewItem> items)
        {
            int success = 0;
            int failed = 0;
            var errors = new List<string>();

            if (doc == null || items == null || !items.Any())
                return (0, 0, errors);

            using (var tx = new Transaction(doc, "Update Detail Numbers from View Names"))
            {
                tx.Start();

                foreach (var item in items.Where(i => i.IsSelected && i.IsMatched))
                {
                    try
                    {
                        var param = item.Viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (param == null || param.IsReadOnly)
                        {
                            errors.Add($"View '{item.ViewName}': Detail Number parameter is read-only or null.");
                            failed++;
                            continue;
                        }

                        param.Set(item.NewDetailNumber);
                        success++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"View '{item.ViewName}': {ex.Message}");
                        failed++;
                    }
                }

                tx.Commit();
            }

            return (success, failed, errors);
        }
    }
}

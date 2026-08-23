using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using KhimTools.SheetGen.Models;

namespace KhimTools.SheetGen.Services
{
    public class TitleBlockOption
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }

        public override string ToString() => Name;
    }

    public class ViewOption
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }

        public override string ToString() => $"[{ViewType}] {Name}";
    }

    /// <summary>
    /// Service thực thi tạo hàng loạt Sheet theo từng Phân Hệ Series và đặt Viewport tự động chuẩn DiRoots SheetGen.
    /// </summary>
    public static class SheetGenService
    {
        public static List<TitleBlockOption> GetAvailableTitleBlocks(Document doc)
        {
            var list = new List<TitleBlockOption>();
            if (doc == null) return list;

            var titleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .OrderBy(fs => fs.FamilyName)
                .ThenBy(fs => fs.Name);

            foreach (var tb in titleBlocks)
            {
                list.Add(new TitleBlockOption
                {
                    Id = tb.Id,
                    Name = $"{tb.FamilyName} : {tb.Name}"
                });
            }

            return list;
        }

        public static List<ViewOption> GetAvailableViews(Document doc)
        {
            var list = new List<ViewOption>();
            if (doc == null) return list;

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.DrawingSheet && v.ViewType != ViewType.Internal)
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name);

            foreach (var v in views)
            {
                list.Add(new ViewOption
                {
                    Id = v.Id,
                    Name = v.Name,
                    ViewType = v.ViewType.ToString()
                });
            }

            return list;
        }

        /// <summary>
        /// Sinh danh sách chi tiết các Sheet từ danh sách cấu hình Phân Hệ Series.
        /// </summary>
        public static List<SheetGenItem> GenerateFromSeries(List<SheetSeriesConfig> seriesList, List<TitleBlockOption> availableTb)
        {
            var result = new List<SheetGenItem>();
            if (seriesList == null) return result;

            foreach (var series in seriesList.Where(s => s.IsEnabled))
            {
                var tb = availableTb.FirstOrDefault(t => t.Name == series.TitleBlockName);
                var tbId = tb?.Id ?? series.TitleBlockId ?? ElementId.InvalidElementId;

                for (int i = 0; i < series.Count; i++)
                {
                    int currentNum = series.StartNumber + (i * series.Step);
                    string sheetNum = $"{series.Prefix}{currentNum:D2}{series.Suffix}";

                    int index1Based = i + 1;
                    string sheetName = series.NamePattern ?? "BẢN VẼ";
                    sheetName = sheetName.Replace("{n}", index1Based.ToString());
                    sheetName = sheetName.Replace("{Index}", index1Based.ToString());
                    sheetName = sheetName.Replace("{0n}", index1Based.ToString("D2"));
                    sheetName = sheetName.Replace("{Number}", currentNum.ToString());

                    result.Add(new SheetGenItem
                    {
                        IsSelected = true,
                        SheetNumber = sheetNum,
                        SheetName = sheetName,
                        TitleBlockId = tbId,
                        TitleBlockName = series.TitleBlockName,
                        Discipline = series.Discipline
                    });
                }
            }

            return result;
        }

        public static (int createdCount, List<string> errors) CreateSheets(Document doc, List<SheetGenItem> items)
        {
            var errors = new List<string>();
            int createdCount = 0;

            if (doc == null || items == null || !items.Any())
                return (0, errors);

            var existingNumbers = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(s => s.SheetNumber),
                StringComparer.OrdinalIgnoreCase);

            using (var tx = new Transaction(doc, "K-TOOLS - Auto Create Sheets"))
            {
                tx.Start();

                foreach (var item in items.Where(it => it.IsSelected))
                {
                    if (string.IsNullOrWhiteSpace(item.SheetNumber))
                    {
                        errors.Add("Bỏ qua hàng có số hiệu Sheet rỗng.");
                        continue;
                    }

                    if (existingNumbers.Contains(item.SheetNumber))
                    {
                        errors.Add($"Số hiệu Sheet '{item.SheetNumber}' đã tồn tại trong dự án.");
                        continue;
                    }

                    try
                    {
                        ViewSheet sheet = null;
                        if (item.TitleBlockId != null && item.TitleBlockId != ElementId.InvalidElementId)
                        {
                            sheet = ViewSheet.Create(doc, item.TitleBlockId);
                        }
                        else
                        {
                            sheet = ViewSheet.Create(doc, ElementId.InvalidElementId);
                        }

                        if (sheet != null)
                        {
                            sheet.SheetNumber = item.SheetNumber;
                            if (!string.IsNullOrWhiteSpace(item.SheetName))
                            {
                                sheet.Name = item.SheetName;
                            }

                            // Đặt View nếu có chọn
                            if (item.AssignedViewId != null && item.AssignedViewId != ElementId.InvalidElementId)
                            {
                                if (Viewport.CanAddViewToSheet(doc, sheet.Id, item.AssignedViewId))
                                {
                                    XYZ center = new XYZ(1.5, 1.0, 0);
                                    Viewport.Create(doc, sheet.Id, item.AssignedViewId, center);
                                }
                            }

                            // Gán tham số phụ nếu có
                            if (!string.IsNullOrWhiteSpace(item.DrawnBy))
                            {
                                var p = sheet.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY);
                                if (p != null && !p.IsReadOnly) p.Set(item.DrawnBy);
                            }
                            if (!string.IsNullOrWhiteSpace(item.CheckedBy))
                            {
                                var p = sheet.get_Parameter(BuiltInParameter.SHEET_CHECKED_BY);
                                if (p != null && !p.IsReadOnly) p.Set(item.CheckedBy);
                            }

                            existingNumbers.Add(item.SheetNumber);
                            createdCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Lỗi khi tạo Sheet '{item.SheetNumber}': {ex.Message}");
                    }
                }

                tx.Commit();
            }

            return (createdCount, errors);
        }

        public static bool ExportToCsv(string filePath, List<SheetGenItem> items)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Sheet Number,Sheet Name,Title Block,Assigned View,Drawn By,Checked By,Discipline");
                foreach (var it in items)
                {
                    sb.AppendLine($"\"{it.SheetNumber}\",\"{it.SheetName}\",\"{it.TitleBlockName}\",\"{it.AssignedViewName}\",\"{it.DrawnBy}\",\"{it.CheckedBy}\",\"{it.Discipline}\"");
                }
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static List<SheetGenItem> ImportFromCsv(string filePath, List<TitleBlockOption> titleBlocks)
        {
            var list = new List<SheetGenItem>();
            if (!File.Exists(filePath)) return list;

            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var parts = line.Split(',').Select(p => p.Trim('\"', ' ')).ToArray();
                    if (parts.Length >= 2)
                    {
                        var item = new SheetGenItem
                        {
                            IsSelected = true,
                            SheetNumber = parts[0],
                            SheetName = parts[1]
                        };

                        if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                        {
                            var tb = titleBlocks.FirstOrDefault(t => t.Name.IndexOf(parts[2], StringComparison.OrdinalIgnoreCase) >= 0);
                            if (tb != null)
                            {
                                item.TitleBlockId = tb.Id;
                                item.TitleBlockName = tb.Name;
                            }
                        }

                        if (parts.Length >= 5) item.DrawnBy = parts[4];
                        if (parts.Length >= 6) item.CheckedBy = parts[5];
                        if (parts.Length >= 7) item.Discipline = parts[6];

                        list.Add(item);
                    }
                }
            }
            catch { }

            return list;
        }
    }
}
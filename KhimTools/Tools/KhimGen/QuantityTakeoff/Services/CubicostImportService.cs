using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class CubicostImportService
    {
        public static CubicostImportResult Import(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file Cubicost.", path);
            string sheetName = "";
            List<List<string>> rows = string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)
                ? ReadCsv(path)
                : ReadFirstWorksheet(path, out sheetName);
            var result = Parse(rows);
            result.FilePath = path;
            if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)) result.SheetName = sheetName;
            else result.SheetName = "CSV";
            return result;
        }

        private static CubicostImportResult Parse(List<List<string>> rows)
        {
            var result = new CubicostImportResult();
            int headerRow = -1, codeCol = -1, descriptionCol = -1, unitCol = -1, quantityCol = -1, locationCol = -1;
            for (int r = 0; r < Math.Min(rows.Count, 30); r++)
            {
                var normalized = rows[r].Select(Normalize).ToList();
                int q = FindColumn(normalized, "quantity", "qty", "khoi luong", "total quantity", "工程量");
                int u = FindColumn(normalized, "unit", "don vi", "uom", "单位");
                if (q < 0 || u < 0) continue;
                headerRow = r; quantityCol = q; unitCol = u;
                codeCol = FindColumn(normalized, "code", "item code", "ma", "ma hieu", "编码");
                descriptionCol = FindColumn(normalized, "description", "item name", "name", "dien giai", "mo ta", "项目名称");
                locationCol = FindColumn(normalized, "location", "floor", "level", "zone", "vi tri", "tang");
                break;
            }
            if (headerRow < 0) throw new InvalidDataException("Không nhận diện được cột Quantity và Unit trong file Cubicost. Hãy xuất bảng có tiêu đề Code/Description/Unit/Quantity.");

            for (int r = headerRow + 1; r < rows.Count; r++)
            {
                string rawQuantity = Cell(rows[r], quantityCol);
                if (!TryNumber(rawQuantity, out double quantity)) continue;
                string unit = NormalizeUnit(Cell(rows[r], unitCol));
                if (string.IsNullOrWhiteSpace(unit)) continue;
                string cubicostCode = Cell(rows[r], codeCol).Trim();
                result.Lines.Add(new CubicostQtoLine
                {
                    SourceRow = r + 1, CubicostCode = cubicostCode, RevitCode = cubicostCode,
                    Description = Cell(rows[r], descriptionCol).Trim(), Location = Cell(rows[r], locationCol).Trim(),
                    Unit = unit, Quantity = quantity
                });
            }
            if (codeCol < 0) result.Warnings.Add("Không tìm thấy cột Code; cần ánh xạ Revit Code thủ công.");
            if (descriptionCol < 0) result.Warnings.Add("Không tìm thấy cột Description.");
            if (result.Lines.Count == 0) result.Warnings.Add("Không có dòng khối lượng hợp lệ sau khi đọc file.");
            return result;
        }

        private static List<List<string>> ReadFirstWorksheet(string path, out string sheetName)
        {
            using var archive = ZipFile.OpenRead(path);
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
            var shared = new List<string>();
            ZipArchiveEntry sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedEntry != null)
            {
                XDocument doc = LoadXml(sharedEntry);
                shared.AddRange(doc.Descendants(main + "si").Select(x => string.Concat(x.Descendants(main + "t").Select(t => t.Value))));
            }
            XDocument workbook = LoadXml(archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("File không có workbook Excel hợp lệ."));
            XElement sheet = workbook.Descendants(main + "sheet").FirstOrDefault() ?? throw new InvalidDataException("Workbook không có worksheet.");
            sheetName = (string)sheet.Attribute("name") ?? "Sheet1";
            string relationshipId = (string)sheet.Attribute(rel + "id");
            XDocument relationships = LoadXml(archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException("Workbook thiếu relationships."));
            string target = (string)relationships.Descendants(packageRel + "Relationship").First(x => (string)x.Attribute("Id") == relationshipId).Attribute("Target");
            string sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target.Replace("../", "");
            XDocument worksheet = LoadXml(archive.GetEntry(sheetPath) ?? throw new InvalidDataException("Không đọc được worksheet đầu tiên."));
            var output = new List<List<string>>();
            foreach (XElement row in worksheet.Descendants(main + "row"))
            {
                var values = new List<string>();
                foreach (XElement cell in row.Elements(main + "c"))
                {
                    int column = ColumnIndex((string)cell.Attribute("r"));
                    while (values.Count <= column) values.Add("");
                    string type = (string)cell.Attribute("t");
                    string value = type == "inlineStr" ? string.Concat(cell.Descendants(main + "t").Select(x => x.Value)) : (string)cell.Element(main + "v") ?? "";
                    if (type == "s" && int.TryParse(value, out int sharedIndex) && sharedIndex >= 0 && sharedIndex < shared.Count) value = shared[sharedIndex];
                    values[column] = value;
                }
                output.Add(values);
            }
            return output;
        }

        private static List<List<string>> ReadCsv(string path)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            char separator = lines.FirstOrDefault()?.Count(c => c == ';') > lines.FirstOrDefault()?.Count(c => c == ',') ? ';' : ',';
            return lines.Select(line => ParseCsvLine(line, separator)).ToList();
        }

        private static List<string> ParseCsvLine(string line, char separator)
        {
            var cells = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < (line ?? "").Length; i++)
            {
                char c = line[i];
                if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = !quoted;
                else if (c == separator && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(c);
            }
            cells.Add(cell.ToString()); return cells;
        }

        private static XDocument LoadXml(ZipArchiveEntry entry) { using var stream = entry.Open(); return XDocument.Load(stream); }
        private static int ColumnIndex(string reference)
        {
            int value = 0;
            foreach (char c in reference ?? "") { if (!char.IsLetter(c)) break; value = value * 26 + char.ToUpperInvariant(c) - 'A' + 1; }
            return Math.Max(0, value - 1);
        }
        private static int FindColumn(IList<string> values, params string[] aliases)
        {
            for (int i = 0; i < values.Count; i++) if (aliases.Any(x => values[i] == x || values[i].Contains(x))) return i;
            return -1;
        }
        private static string Cell(IList<string> row, int index) => index >= 0 && index < row.Count ? row[index] ?? "" : "";
        private static string Normalize(string value)
        {
            string text = (value ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            return new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Replace('đ', 'd');
        }
        private static string NormalizeUnit(string value)
        {
            string unit = Normalize(value).Replace(" ", "");
            if (unit == "m3" || unit == "m³" || unit == "cum") return "m³";
            if (unit == "m2" || unit == "m²" || unit == "sqm") return "m²";
            if (unit == "m" || unit == "lm") return "m";
            if (unit == "kg" || unit == "kilogram") return "kg";
            if (unit == "ea" || unit == "each" || unit == "no" || unit == "pcs" || unit == "cai" || unit == "bo") return "ea";
            return value.Trim();
        }
        private static bool TryNumber(string value, out double number)
        {
            value = (value ?? "").Trim().Replace(" ", "");
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number)) return true;
            return double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out number);
        }
    }
}

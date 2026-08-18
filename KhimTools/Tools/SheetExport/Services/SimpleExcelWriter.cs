using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace KhimTools.SheetExport.Services
{
    /// <summary>
    /// Bộ máy tạo file Microsoft Excel (.xlsx) chuẩn OpenXML độc lập 100%,
    /// không phụ thuộc vào bất kỳ thư viện bên ngoài nào (ClosedXML, EPPlus, OpenXml SDK),
    /// hoàn toàn miễn nhiễm với lỗi xung đột DLL (TypeLoadException) trong Revit AppDomain.
    /// </summary>
    public class SimpleExcelWriter : IDisposable
    {
        private readonly List<List<string>> _rows = new List<List<string>>();
        private readonly string _sheetName;

        public SimpleExcelWriter(string sheetName = "Sheet1")
        {
            _sheetName = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName;
        }

        public void AddRow(params string[] cells)
        {
            var row = new List<string>(cells);
            _rows.Add(row);
        }

        public void AddRow(IEnumerable<string> cells)
        {
            var row = new List<string>(cells);
            _rows.Add(row);
        }

        public void AddEmptyRow()
        {
            _rows.Add(new List<string>());
        }

        public void Save(string filePath)
        {
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }

            using (var zipStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // 1. [Content_Types].xml
                CreateEntry(archive, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\r\n" +
                    "  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\r\n" +
                    "  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\r\n" +
                    "  <Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>\r\n" +
                    "  <Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\r\n" +
                    "  <Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>\r\n" +
                    "</Types>");

                // 2. _rels/.rels
                CreateEntry(archive, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
                    "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>\r\n" +
                    "</Relationships>");

                // 3. xl/_rels/workbook.xml.rels
                CreateEntry(archive, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
                    "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>\r\n" +
                    "  <Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\r\n" +
                    "</Relationships>");

                // 4. xl/workbook.xml
                CreateEntry(archive, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">\r\n" +
                    "  <sheets>\r\n" +
                    $"    <sheet name=\"{EscapeXml(_sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/>\r\n" +
                    "  </sheets>\r\n" +
                    "</workbook>");

                // 5. xl/styles.xml
                CreateEntry(archive, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n" +
                    "  <fonts count=\"2\">\r\n" +
                    "    <font><name val=\"Segoe UI\"/><sz val=\"10\"/></font>\r\n" +
                    "    <font><b/><name val=\"Segoe UI\"/><sz val=\"10\"/></font>\r\n" +
                    "  </fonts>\r\n" +
                    "  <fills count=\"2\">\r\n" +
                    "    <fill><patternFill patternType=\"none\"/></fill>\r\n" +
                    "    <fill><patternFill patternType=\"gray125\"/></fill>\r\n" +
                    "  </fills>\r\n" +
                    "  <borders count=\"1\">\r\n" +
                    "    <border><left/><right/><top/><bottom/><diagonal/></border>\r\n" +
                    "  </borders>\r\n" +
                    "  <cellStyleXfs count=\"1\">\r\n" +
                    "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>\r\n" +
                    "  </cellStyleXfs>\r\n" +
                    "  <cellXfs count=\"2\">\r\n" +
                    "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>\r\n" +
                    "    <xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>\r\n" +
                    "  </cellXfs>\r\n" +
                    "</styleSheet>");

                // 6. xl/worksheets/sheet1.xml
                var sbSheet = new StringBuilder();
                sbSheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
                sbSheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n");
                sbSheet.Append("  <sheetData>\r\n");

                for (int r = 0; r < _rows.Count; r++)
                {
                    int rowIdx = r + 1;
                    var rowData = _rows[r];
                    if (rowData.Count == 0) continue;

                    sbSheet.Append($"    <row r=\"{rowIdx}\">\r\n");
                    for (int c = 0; c < rowData.Count; c++)
                    {
                        string colRef = GetColumnReference(c + 1);
                        string cellRef = $"{colRef}{rowIdx}";
                        string val = rowData[c] ?? "";

                        sbSheet.Append($"      <c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{EscapeXml(val)}</t></is></c>\r\n");
                    }
                    sbSheet.Append("    </row>\r\n");
                }

                sbSheet.Append("  </sheetData>\r\n");
                sbSheet.Append("</worksheet>");

                CreateEntry(archive, "xl/worksheets/sheet1.xml", sbSheet.ToString());
            }
        }

        private static void CreateEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.Write(content);
            }
        }

        private static string GetColumnReference(int colIndex)
        {
            string col = "";
            while (colIndex > 0)
            {
                int mod = (colIndex - 1) % 26;
                col = (char)(65 + mod) + col;
                colIndex = (colIndex - mod) / 26;
            }
            return col;
        }

        private static string EscapeXml(string unescaped)
        {
            if (string.IsNullOrEmpty(unescaped)) return "";
            return unescaped
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        public void Dispose()
        {
            _rows.Clear();
        }
    }
}

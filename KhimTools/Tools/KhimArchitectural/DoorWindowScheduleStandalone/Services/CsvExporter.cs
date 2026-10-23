using System.Globalization;
using System.Text;
using KhimTools.DoorSchedule.Models;

namespace KhimTools.DoorSchedule.Services;

/// <summary>Exports the same materialized rows shown in preview. Does not query or modify Revit.</summary>
public static class CsvExporter
{
    public static void Export(PreviewResult preview, string path)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var roles = preview.Rows.SelectMany(r => r.OptionalValues.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
        using (var writer = NewWriter(fullPath))
        {
            WriteRow(writer, new[] { "STT (chỉ CSV)", "LOẠI", "MÃ LOẠI CỬA (Type Mark)", "FAMILY / TYPE", "TẦNG", "RỘNG (mm)", "CAO (mm)", "SỐ LƯỢNG", "ELEMENT IDS", "CẢNH BÁO" }.Concat(roles.Select(role => preview.OptionalHeadings.GetValueOrDefault(role, role))));
            int sequence = 0;
            foreach (var row in preview.Rows)
                WriteRow(writer, new[]
                {
                    (++sequence).ToString(CultureInfo.InvariantCulture), row.Category, row.TypeMark, row.FamilyType, row.Level,
                    Number(row.WidthMm), Number(row.HeightMm), row.Count.ToString(CultureInfo.InvariantCulture), row.Elements, row.Warning
                }.Concat(roles.Select(role => row.OptionalValues.GetValueOrDefault(role, ""))));
        }
        string diagnosticPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(fullPath) + ".diagnostics.csv");
        using var diagnosticWriter = NewWriter(diagnosticPath);
        WriteRow(diagnosticWriter, new[] { "ElementId (Int64)", "UniqueId", "Category", "TypeUniqueId", "Family / Type", "Type Mark", "Parameter provenance", "Severity", "Warning code", "Message" });
        foreach (var record in preview.Records)
        {
            var diagnostics = preview.Diagnostics.Where(d => d.ElementId == record.ElementId).ToList();
            if (diagnostics.Count == 0)
                WriteRecordDiagnostic(diagnosticWriter, record, null);
            else
                foreach (var diagnostic in diagnostics)
                    WriteRecordDiagnostic(diagnosticWriter, record, diagnostic);
        }
        foreach (var diagnostic in preview.Diagnostics.Where(d => !d.ElementId.HasValue || !preview.Records.Any(r => r.ElementId == d.ElementId.Value)))
            WriteRow(diagnosticWriter, new[] { diagnostic.ElementId?.ToString(CultureInfo.InvariantCulture) ?? "", diagnostic.UniqueId, "", "", "", "", "", diagnostic.Severity, diagnostic.Code, diagnostic.Message });
    }

    private static void WriteRecordDiagnostic(StreamWriter writer, DoorWindowRecord record, Diagnostic? diagnostic)
    {
        string provenance = string.Join("; ", record.Provenance.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value));
        WriteRow(writer, new[] { record.ElementId.ToString(CultureInfo.InvariantCulture), record.UniqueId, record.Category.ToString(), record.TypeUniqueId, record.FamilyType, record.TypeMark, provenance, diagnostic?.Severity ?? "INFO", diagnostic?.Code ?? "", diagnostic?.Message ?? "" });
    }

    private static StreamWriter NewWriter(string path) => new(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)) { NewLine = "\r\n" };
    private static string Number(double? value) => value?.ToString("G17", CultureInfo.InvariantCulture) ?? "";
    private static void WriteRow(StreamWriter writer, IEnumerable<string> cells) => writer.WriteLine(string.Join(",", cells.Select(Quote)));
    private static string Quote(string? value)
    {
        string cell = value ?? "";
        return cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
    }
}

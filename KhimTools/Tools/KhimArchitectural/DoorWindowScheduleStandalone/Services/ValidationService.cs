using System.Globalization;
using KhimTools.DoorSchedule.Models;

namespace KhimTools.DoorSchedule.Services;

/// <summary>Pure validation of already materialized, native schedule-eligible records.</summary>
public static class ValidationService
{
    public const string MultipleValues = "<Nhiều giá trị>";

    public static PreviewResult ValidateAndGroup(
        IEnumerable<DoorWindowRecord> records,
        ScheduleOptions options,
        IEnumerable<Diagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);
        var result = new PreviewResult
        {
            Records = records.ToList(),
            Diagnostics = diagnostics?.ToList() ?? new List<Diagnostic>()
        };
        foreach (CategoryKind category in Enum.GetValues<CategoryKind>())
            result.RawInventory[category] = result.Records.Count(r => r.Category == category);

        foreach (var mapping in options.Mappings.Where(m => result.Records.Any(r => r.Category == m.Category)))
            foreach (var column in mapping.OptionalColumns)
                if (!string.IsNullOrWhiteSpace(column.Role))
                    result.OptionalHeadings.TryAdd(column.Role, string.IsNullOrWhiteSpace(column.Heading) ? column.Role : column.Heading);

        result.Diagnostics.AddRange(ValidateOptionalRoles(options));
        foreach (var record in result.Records)
        {
            CheckDimension(record, "WIDTH_INVALID", "Rộng", record.WidthInternal, record.WidthMm, result);
            CheckDimension(record, "HEIGHT_INVALID", "Cao", record.HeightInternal, record.HeightMm, result);
            if (string.IsNullOrWhiteSpace(record.TypeUniqueId))
                Add(result, record, "ERROR", "TYPE_ID_MISSING", "Không có định danh type để đối chiếu với native schedule.");
            if (string.IsNullOrWhiteSpace(record.FamilyType))
                Add(result, record, "ERROR", "FAMILY_TYPE_MISSING", "Không đọc được Family / Type.");
            if (string.IsNullOrWhiteSpace(record.TypeMark))
                Add(result, record, "WARNING", "TYPE_MARK_MISSING", "Type Mark trống; type vẫn được giữ riêng theo định danh.");
            foreach (string warning in record.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct(StringComparer.Ordinal))
                Add(result, record, "WARNING", "RECORD_WARNING", warning);
        }

        foreach (var marks in result.Records.Where(r => !string.IsNullOrWhiteSpace(r.TypeMark))
            .GroupBy(r => new { r.Category, r.TypeMark }))
        {
            if (marks.Select(r => r.TypeUniqueId).Distinct(StringComparer.Ordinal).Count() > 1)
                foreach (var record in marks)
                    Add(result, record, "WARNING", "TYPE_MARK_DUPLICATE", $"Type Mark '{record.TypeMark}' được dùng bởi nhiều type; không dùng làm khóa gom nhóm.");
        }

        if (options.Grouping != GroupingMode.EveryInstance)
        {
            // Revit sorts native Family and Type display values. A textual collision could merge distinct type IDs.
            foreach (var types in result.Records.GroupBy(r => new { r.Category, r.FamilyType }))
            {
                if (types.Select(r => r.TypeUniqueId).Distinct(StringComparer.Ordinal).Count() > 1)
                    foreach (var record in types)
                        Add(result, record, "ERROR", "TYPE_DISPLAY_COLLISION", $"Family / Type '{record.FamilyType}' trùng tên ở các type khác nhau. Chọn chế độ từng instance để tránh gom sai.");
            }
        }

        var groups = result.Records.GroupBy(r => new GroupKey(
            r.Category,
            r.TypeUniqueId,
            r.WidthInternal,
            r.HeightInternal,
            options.Grouping == GroupingMode.LevelTypeDimensions ? r.LevelUniqueId : "",
            options.Grouping == GroupingMode.EveryInstance ? r.UniqueId + ":" + r.ElementId.ToString(CultureInfo.InvariantCulture) : ""));

        foreach (var group in groups)
        {
            var members = group.ToList();
            var first = members[0];
            var row = new PreviewRow
            {
                Category = first.Category == CategoryKind.Doors ? "Cửa đi" : "Cửa sổ",
                FamilyType = Consensus(members.Select(r => r.FamilyType)),
                TypeMark = Consensus(members.Select(r => r.TypeMark)),
                Level = options.Grouping == GroupingMode.TypeDimensions ? "" : Consensus(members.Select(r => r.LevelName)),
                WidthMm = first.WidthMm,
                HeightMm = first.HeightMm,
                Count = members.Count,
                Elements = string.Join("; ", members.Select(r => r.ElementId.ToString(CultureInfo.InvariantCulture)).OrderBy(id => id, StringComparer.Ordinal))
            };
            if (row.TypeMark == MultipleValues && options.Grouping != GroupingMode.EveryInstance)
                foreach (var record in members)
                    Add(result, record, "ERROR", "TYPE_MARK_CONFLICT", "Type Mark thay đổi trong cùng nhóm type/kích thước; dùng từng instance hoặc chọn đúng tham số type.");
            if (members.Select(r => r.WidthMm).Distinct().Count() > 1 || members.Select(r => r.HeightMm).Distinct().Count() > 1)
                foreach (var record in members)
                    Add(result, record, "ERROR", "UNIT_VALUE_CONFLICT", "Cùng kích thước internal nhưng kết quả mm không nhất quán; không thể xuất kích thước đại diện.");

            var mapping = options.Mappings.FirstOrDefault(m => m.Category == first.Category);
            var roles = (mapping?.OptionalColumns.Select(c => c.Role) ?? Enumerable.Empty<string>())
                .Concat(members.SelectMany(r => r.Values.Keys))
                .Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.Ordinal);
            foreach (string role in roles)
            {
                string value = Consensus(members.Select(r => r.Values.TryGetValue(role, out string? v) ? v ?? "" : ""));
                row.OptionalValues[role] = value;
                if (value == MultipleValues)
                    foreach (var record in members)
                        Add(result, record, "WARNING", "OPTIONAL_MULTIPLE_VALUES", $"Cột '{role}' có nhiều giá trị trong nhóm; không lấy giá trị của instance đầu tiên.");
            }
            result.Rows.Add(row);
        }

        foreach (var row in result.Rows)
        {
            var ids = row.Elements.Split("; ", StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            row.Warning = string.Join("; ", result.Diagnostics
                .Where(d => d.ElementId.HasValue && ids.Contains(d.ElementId.Value.ToString(CultureInfo.InvariantCulture)))
                .Select(d => d.Code).Distinct(StringComparer.Ordinal));
        }
        return result;
    }

    /// <summary>Prevent an optional role from addressing two values or producing duplicate output columns.</summary>
    public static List<Diagnostic> ValidateOptionalRoles(ScheduleOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var mapping in options.Mappings)
        {
            foreach (var columns in mapping.OptionalColumns.GroupBy(c => c.Role, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(columns.Key))
                    diagnostics.Add(new Diagnostic { Severity = "ERROR", Code = "OPTIONAL_ROLE_MISSING", Message = $"{mapping.Category}: cột tùy chọn thiếu mã role." });
                else if (columns.Count() > 1)
                    diagnostics.Add(new Diagnostic
                    {
                        Severity = "ERROR", Code = "OPTIONAL_ROLE_DUPLICATE",
                        Message = $"{mapping.Category}: role '{columns.Key}' được ánh xạ nhiều lần: {string.Join("; ", columns.Select(c => c.Field.Key))}. Mỗi role phải có đúng một nguồn."
                    });
            }
        }
        return diagnostics;
    }

    /// <summary>Pure checks for explicitly selected dimension sources; no name matching or fallback is attempted.</summary>
    public static List<Diagnostic> ValidateDimensionMappings(ScheduleOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var category in Enum.GetValues<CategoryKind>().Where(c => c == CategoryKind.Doors ? options.Doors : options.Windows))
        {
            var mappings = options.Mappings.Where(m => m.Category == category).ToList();
            if (mappings.Count != 1)
            {
                diagnostics.Add(new Diagnostic { Severity = "ERROR", Code = "CATEGORY_MAPPING_INVALID", Message = $"{category}: cần đúng một cấu hình ánh xạ tham số." });
                continue;
            }
            foreach (var selection in new[] { (Role: "Width", Field: mappings[0].Width), (Role: "Height", Field: mappings[0].Height) })
            {
                FieldChoice? field = selection.Field;
                bool identityKnown = field != null && (field.Identity.StartsWith("BIP:", StringComparison.Ordinal) || field.Identity.StartsWith("GUID:", StringComparison.Ordinal) || field.Identity.StartsWith("PID:", StringComparison.Ordinal));
                if (field == null || field.Category != category || !identityKnown || string.IsNullOrWhiteSpace(field.FieldContext) ||
                    field.StorageType != "Double" || !field.IsLength || field.Binding is not (ParameterBinding.Instance or ParameterBinding.Type) ||
                    (options.Grouping != GroupingMode.EveryInstance && !field.IsSortable))
                    diagnostics.Add(new Diagnostic { Severity = "ERROR", Code = "DIMENSION_MAPPING_INVALID", Message = $"{category}/{selection.Role}: phải chọn đúng field native, nguồn instance/type, StorageType.Double, Length và khả năng sort khi gom nhóm." });
            }
        }
        return diagnostics;
    }

    private static string Consensus(IEnumerable<string?> source)
    {
        string[] values = source.Select(v => v ?? "").Distinct(StringComparer.Ordinal).Take(2).ToArray();
        return values.Length > 1 ? MultipleValues : values.FirstOrDefault() ?? "";
    }

    private static void CheckDimension(DoorWindowRecord record, string code, string label, double? internalValue, double? mm, PreviewResult result)
    {
        if (!internalValue.HasValue || !double.IsFinite(internalValue.Value) || internalValue.Value <= 0 ||
            !mm.HasValue || !double.IsFinite(mm.Value) || mm.Value <= 0)
            Add(result, record, "ERROR", code, $"{label}: thiếu, bằng 0 hoặc giá trị không hợp lệ tại nguồn tham số đã chọn. Không dùng fallback hay bounding box.");
    }

    private static void Add(PreviewResult result, DoorWindowRecord record, string severity, string code, string message)
    {
        if (!result.Diagnostics.Any(d => d.ElementId == record.ElementId && d.Code == code && d.Message == message))
            result.Diagnostics.Add(new Diagnostic { Severity = severity, Code = code, Message = message, ElementId = record.ElementId, UniqueId = record.UniqueId });
    }

    private sealed record GroupKey(CategoryKind Category, string TypeId, double? Width, double? Height, string LevelId, string InstanceId);
}

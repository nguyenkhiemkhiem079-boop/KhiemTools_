namespace KhimTools.DoorSchedule.Models;

public enum CategoryKind { Doors, Windows }
public enum GroupingMode { TypeDimensions, LevelTypeDimensions, EveryInstance }
public enum ParameterBinding { Instance, Type, Related, Unknown }

/// <summary>A user-selected native schedulable field. Identity and context are both significant.</summary>
public sealed class FieldChoice
{
    public CategoryKind Category { get; set; }
    public long ParameterId { get; set; }
    public string FieldContext { get; set; } = "";
    public string Name { get; set; } = "";
    public string Identity { get; set; } = "";
    public ParameterBinding Binding { get; set; } = ParameterBinding.Unknown;
    public string StorageType { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsLength { get; set; }
    public bool IsSortable { get; set; }
    public bool IsFilterable { get; set; }
    public string Provenance { get; set; } = "";
    public string DisplayLabel => $"{Name} [{Identity}; {ParameterId}; {FieldContext}; {Binding}]";
    public string Key => $"{Identity}|{FieldContext}|{Binding}";
}

public sealed class MappedColumn
{
    public string Role { get; set; } = "";
    public string Heading { get; set; } = "";
    public FieldChoice Field { get; set; } = new();
}

public sealed class CategoryMapping
{
    public CategoryKind Category { get; set; }
    public FieldChoice? Width { get; set; }
    public FieldChoice? Height { get; set; }
    public FieldChoice? TypeMark { get; set; }
    public FieldChoice? FamilyType { get; set; }
    public FieldChoice? Level { get; set; }
    public List<MappedColumn> OptionalColumns { get; set; } = new();
}

public sealed class ScheduleOptions
{
    public bool Doors { get; set; } = true;
    public bool Windows { get; set; } = true;
    public string? LevelUniqueId { get; set; }
    public string PhaseUniqueId { get; set; } = "";
    public string PhaseFilterUniqueId { get; set; } = "";
    public GroupingMode Grouping { get; set; } = GroupingMode.TypeDimensions;
    public string Prefix { get; set; } = "K-TOOL";
    public List<CategoryMapping> Mappings { get; set; } = new();
    public bool CreateSeparateCopy { get; set; }
    public string? SheetUniqueId { get; set; }
    public double SheetXmm { get; set; }
    public double SheetYmm { get; set; }
    public bool OpenAfterCreate { get; set; } = true;
}

public sealed class DoorWindowRecord
{
    public CategoryKind Category { get; set; }
    public long ElementId { get; set; }
    public string UniqueId { get; set; } = "";
    public string TypeUniqueId { get; set; } = "";
    public string FamilyType { get; set; } = "";
    public string TypeMark { get; set; } = "";
    public string LevelUniqueId { get; set; } = "";
    public string LevelName { get; set; } = "";
    public double? WidthInternal { get; set; }
    public double? HeightInternal { get; set; }
    public double? WidthMm { get; set; }
    public double? HeightMm { get; set; }
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Provenance { get; set; } = new(StringComparer.Ordinal);
    public List<string> Warnings { get; set; } = new();
}

public sealed class PreviewRow
{
    public string Category { get; set; } = "";
    public string TypeMark { get; set; } = "";
    public string FamilyType { get; set; } = "";
    public string Level { get; set; } = "";
    public double? WidthMm { get; set; }
    public double? HeightMm { get; set; }
    public int Count { get; set; }
    public Dictionary<string, string> OptionalValues { get; set; } = new(StringComparer.Ordinal);
    public string Elements { get; set; } = "";
    public string Warning { get; set; } = "";
}

public sealed class Diagnostic
{
    public string Severity { get; set; } = "WARNING";
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public long? ElementId { get; set; }
    public string UniqueId { get; set; } = "";
}

public sealed class PreviewResult
{
    public List<DoorWindowRecord> Records { get; set; } = new();
    public List<PreviewRow> Rows { get; set; } = new();
    public List<Diagnostic> Diagnostics { get; set; } = new();
    public Dictionary<CategoryKind, int> RawInventory { get; set; } = new();
    public Dictionary<string, string> OptionalHeadings { get; set; } = new(StringComparer.Ordinal);
    public bool IsValid => !Diagnostics.Any(d => string.Equals(d.Severity, "ERROR", StringComparison.OrdinalIgnoreCase));
    public int TotalCount => Rows.Sum(row => row.Count);
    public string ConfigurationFingerprint { get; set; } = "";
}

public sealed class BuildResult
{
    public List<long> ScheduleIds { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

public sealed class NamedChoice
{
    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayLabel => Name;
}

public sealed class ModelCatalog
{
    public List<NamedChoice> Levels { get; set; } = new();
    public List<NamedChoice> Phases { get; set; } = new();
    public List<NamedChoice> PhaseFilters { get; set; } = new();
    public List<NamedChoice> Sheets { get; set; } = new();
    public Dictionary<CategoryKind, List<FieldChoice>> Fields { get; set; } = new();
    public string? SeedPhaseUniqueId { get; set; }
    public string? SeedPhaseFilterUniqueId { get; set; }
}

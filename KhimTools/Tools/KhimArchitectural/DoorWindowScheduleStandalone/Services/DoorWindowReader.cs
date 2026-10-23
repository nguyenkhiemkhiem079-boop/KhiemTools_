using System.Globalization;
using Autodesk.Revit.DB;
using KhimTools.DoorSchedule.Models;

namespace KhimTools.DoorSchedule.Services;

public sealed class DoorWindowReader
{
    private readonly Document _doc;
    private readonly ParameterResolver _resolver;
    public DoorWindowReader(Document doc) { _doc = doc; _resolver = new(doc); }

    public List<DoorWindowRecord> Read(CategoryKind category, IEnumerable<Element> members, CategoryMapping mapping,
        Phase phase, ICollection<Diagnostic> diagnostics)
    {
        var result = new List<DoorWindowRecord>();
        foreach (var instance in members)
        {
            var type = _doc.GetElement(instance.GetTypeId()) as ElementType;
            var levelId = ReadLevel(instance, mapping.Level);
            var level = _doc.GetElement(levelId) as Level;
            var record = new DoorWindowRecord
            {
                Category = category, ElementId = instance.Id.Value, UniqueId = instance.UniqueId,
                TypeUniqueId = type?.UniqueId ?? "", FamilyType = type == null ? "" : $"{type.FamilyName}: {type.Name}",
                LevelUniqueId = level?.UniqueId ?? "", LevelName = level?.Name ?? ""
            };
            record.TypeMark = ReadText(instance, mapping.TypeMark, phase, record, "TYPE_MARK", diagnostics);
            record.WidthInternal = ReadLength(instance, mapping.Width, phase, record, "WIDTH", diagnostics);
            record.HeightInternal = ReadLength(instance, mapping.Height, phase, record, "HEIGHT", diagnostics);
            record.WidthMm = record.WidthInternal.HasValue ? UnitUtils.ConvertFromInternalUnits(record.WidthInternal.Value, UnitTypeId.Millimeters) : null;
            record.HeightMm = record.HeightInternal.HasValue ? UnitUtils.ConvertFromInternalUnits(record.HeightInternal.Value, UnitTypeId.Millimeters) : null;
            if (mapping.FamilyType != null) record.Provenance["FAMILY_TYPE"] = mapping.FamilyType.Provenance;
            if (mapping.Level != null) record.Provenance["LEVEL"] = mapping.Level.Provenance;
            foreach (var optional in mapping.OptionalColumns)
                record.Values[optional.Role] = ReadText(instance, optional.Field, phase, record, optional.Role, diagnostics);
            if (instance is FamilyInstance family && family.SuperComponent != null)
            {
                record.Warnings.Add("SHARED_NESTED");
                diagnostics.Add(new Diagnostic { Code = "SHARED_NESTED", ElementId = record.ElementId, UniqueId = record.UniqueId,
                    Message = $"Element {record.ElementId} là thành phần nested có SuperComponent {family.SuperComponent.Id.Value}; kiểm tra khả năng đếm cả parent và child." });
            }
            if (instance.DesignOption != null)
                record.Provenance["DESIGN_OPTION"] = $"Native schedule membership; {instance.DesignOption.Name}; {instance.DesignOption.UniqueId}";
            result.Add(record);
        }
        return result;
    }

    private ElementId ReadLevel(Element instance, FieldChoice? choice)
    {
        if (choice == null) return ElementId.InvalidElementId;
        var source = choice.Binding == ParameterBinding.Type ? _doc.GetElement(instance.GetTypeId()) : instance;
        var parameter = _resolver.FindParameter(source, choice.ParameterId);
        return parameter is { StorageType: StorageType.ElementId, HasValue: true } ? parameter.AsElementId() : ElementId.InvalidElementId;
    }

    private Element? Source(Element instance, FieldChoice choice, Phase phase)
    {
        if (choice.FieldContext == "FromRoom") return (instance as FamilyInstance)?.get_FromRoom(phase);
        if (choice.FieldContext == "ToRoom") return (instance as FamilyInstance)?.get_ToRoom(phase);
        return choice.Binding switch
        {
            ParameterBinding.Instance => instance,
            ParameterBinding.Type => _doc.GetElement(instance.GetTypeId()),
            _ => null
        };
    }

    private double? ReadLength(Element instance, FieldChoice? choice, Phase phase, DoorWindowRecord record,
        string role, ICollection<Diagnostic> diagnostics)
    {
        if (choice == null) return null;
        var source = Source(instance, choice, phase);
        record.Provenance[role] = choice.Provenance + $"; resolved source={source?.Id.Value}; source UniqueId={source?.UniqueId}; phase={phase.UniqueId}";
        var parameter = _resolver.FindParameter(source, choice.ParameterId);
        if (parameter == null || !parameter.HasValue) return null;
        if (parameter.StorageType != StorageType.Double || parameter.Definition.GetDataType() != SpecTypeId.Length)
        {
            diagnostics.Add(new Diagnostic { Severity = "ERROR", Code = "NON_LENGTH_PARAMETER", ElementId = record.ElementId,
                UniqueId = record.UniqueId, Message = $"{role} trên Element {record.ElementId} không phải Double/Length: {choice.DisplayLabel}." });
            return null;
        }
        return parameter.AsDouble();
    }

    private string ReadText(Element instance, FieldChoice? choice, Phase phase, DoorWindowRecord record,
        string role, ICollection<Diagnostic> diagnostics)
    {
        if (choice == null) return "";
        var source = Source(instance, choice, phase);
        record.Provenance[role] = choice.Provenance + $"; resolved source={source?.Id.Value}; source UniqueId={source?.UniqueId}; phase={phase.UniqueId}";
        var parameter = _resolver.FindParameter(source, choice.ParameterId);
        if (parameter == null || !parameter.HasValue)
        {
            if (role != "TYPE_MARK")
                diagnostics.Add(new Diagnostic { Code = "OPTIONAL_MISSING", ElementId = record.ElementId, UniqueId = record.UniqueId,
                    Message = $"{role} trống trên Element {record.ElementId}; nguồn {choice.DisplayLabel}." });
            return "";
        }
        switch (parameter.StorageType)
        {
            case StorageType.String: return parameter.AsString() ?? "";
            case StorageType.Integer: return parameter.AsValueString() ?? parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
            case StorageType.Double:
                if (parameter.Definition.GetDataType() == SpecTypeId.Length)
                    return UnitUtils.ConvertFromInternalUnits(parameter.AsDouble(), UnitTypeId.Millimeters).ToString("G17", CultureInfo.InvariantCulture) + " mm";
                return parameter.AsValueString() ?? parameter.AsDouble().ToString("G17", CultureInfo.InvariantCulture);
            case StorageType.ElementId:
                var id = parameter.AsElementId();
                if (id == ElementId.InvalidElementId) return "";
                var related = _doc.GetElement(id);
                // Material/image/other related element data resolves to native name, never numeric IDs.
                return related?.Name ?? parameter.AsValueString() ?? "";
            default: return "";
        }
    }
}

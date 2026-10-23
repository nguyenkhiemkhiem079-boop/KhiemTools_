using Autodesk.Revit.DB;
using KhimTools.DoorSchedule.Models;

namespace KhimTools.DoorSchedule.Services;

/// <summary>Native field identity + context determine a mapping; names are display-only.</summary>
public sealed class ParameterResolver
{
    private readonly Document _doc;
    public ParameterResolver(Document doc) => _doc = doc;

    public static BuiltInCategory NativeCategory(CategoryKind category) =>
        category == CategoryKind.Doors ? BuiltInCategory.OST_Doors : BuiltInCategory.OST_Windows;

    public List<FieldChoice> Discover(ViewSchedule schedule, CategoryKind category, IReadOnlyList<Element> instances)
    {
        var result = new List<FieldChoice>();
        var types = instances.Select(e => _doc.GetElement(e.GetTypeId())).OfType<ElementType>()
            .DistinctBy(e => e.Id.Value).ToList();
        foreach (var sf in schedule.Definition.GetSchedulableFields())
        {
            if (sf.FieldType is not (ScheduleFieldType.Instance or ScheduleFieldType.ElementType
                or ScheduleFieldType.FromRoom or ScheduleFieldType.ToRoom)) continue;
            var field = schedule.Definition.AddField(sf);
            var samples = instances.Select(e => FindParameter(e, sf.ParameterId.Value)).Where(p => p != null).ToList();
            var typeSamples = types.Select(e => FindParameter(e, sf.ParameterId.Value)).Where(p => p != null).ToList();
            ParameterBinding binding;
            if (sf.FieldType is ScheduleFieldType.FromRoom or ScheduleFieldType.ToRoom) binding = ParameterBinding.Related;
            else if (sf.FieldType == ScheduleFieldType.ElementType) binding = ParameterBinding.Type;
            else if (samples.Count > 0 && typeSamples.Count == 0) binding = ParameterBinding.Instance;
            else if (typeSamples.Count > 0 && samples.Count == 0) binding = ParameterBinding.Type;
            else binding = ParameterBinding.Unknown;
            // A project binding is authoritative even in a category with no placed samples.
            var parameterElement = _doc.GetElement(sf.ParameterId) as ParameterElement;
            if (parameterElement != null && binding == ParameterBinding.Unknown)
            {
                var projectBinding = _doc.ParameterBindings.get_Item(parameterElement.GetDefinition());
                if (projectBinding is InstanceBinding) binding = ParameterBinding.Instance;
                else if (projectBinding is TypeBinding) binding = ParameterBinding.Type;
            }
            var selectedSamples = binding == ParameterBinding.Type ? typeSamples : samples;
            var spec = field.GetSpecTypeId();
            var storage = selectedSamples.Select(p => p!.StorageType.ToString()).Distinct().ToList();
            var dataTypes = selectedSamples.Select(p => p!.Definition.GetDataType().TypeId).Distinct().ToList();
            var identity = Identity(sf.ParameterId.Value);
            result.Add(new FieldChoice
            {
                Category = category, ParameterId = sf.ParameterId.Value, FieldContext = sf.FieldType.ToString(),
                Name = sf.GetName(_doc), Identity = identity, Binding = binding,
                StorageType = storage.Count == 1 ? storage[0] : storage.Count > 1 ? "Mixed" : "Unavailable",
                DataType = dataTypes.Count == 1 ? dataTypes[0] : spec.TypeId,
                // ForgeTypeId equality ignores schema-version suffixes; string equality does not.
                IsLength = spec == SpecTypeId.Length && selectedSamples.All(p => p!.Definition.GetDataType() == SpecTypeId.Length),
                IsSortable = schedule.Definition.CanSortByField(field.FieldId),
                IsFilterable = schedule.Definition.CanFilterByValue(field.FieldId),
                Provenance = $"Native schedulable field {identity}; context={sf.FieldType}; binding={binding}; samples instance={samples.Count}, type={typeSamples.Count}; no name lookup/fallback"
            });
            schedule.Definition.RemoveField(field.FieldId);
        }
        return result.OrderBy(f => f.Name, StringComparer.CurrentCulture).ThenBy(f => f.DisplayLabel, StringComparer.Ordinal).ToList();
    }

    public string Identity(long parameterId)
    {
        if (parameterId < 0) return "BIP:" + ((BuiltInParameter)parameterId);
        var element = _doc.GetElement(new ElementId(parameterId));
        if (element is SharedParameterElement shared) return "GUID:" + shared.GuidValue.ToString("D");
        return element is ParameterElement ? "PID:" + parameterId + ":" + element.UniqueId : "PID:" + parameterId;
    }

    public Parameter? FindParameter(Element? source, long parameterId)
    {
        if (source == null) return null;
        if (parameterId < 0) return source.get_Parameter((BuiltInParameter)parameterId);
        var parameterElement = _doc.GetElement(new ElementId(parameterId));
        if (parameterElement is SharedParameterElement shared) return source.get_Parameter(shared.GuidValue);
        if (parameterElement is ParameterElement parameter) return source.get_Parameter(parameter.GetDefinition());
        // Explicit identity fallback, never localized names or first-name wins.
        return source.Parameters.Cast<Parameter>().FirstOrDefault(p => p.Id.Value == parameterId);
    }

    public SchedulableField ResolveNative(ScheduleDefinition definition, FieldChoice choice)
    {
        if (Identity(choice.ParameterId) != choice.Identity)
            throw new InvalidOperationException($"Mapping không còn hợp lệ: {choice.DisplayLabel}. Hãy xem lại mapping.");
        var matches = definition.GetSchedulableFields().Where(f => f.ParameterId.Value == choice.ParameterId
            && f.FieldType.ToString() == choice.FieldContext).ToList();
        if (matches.Count != 1) throw new InvalidOperationException($"Không tìm được một native field duy nhất: {choice.DisplayLabel}.");
        return matches[0];
    }

    public static CategoryMapping DefaultMapping(CategoryKind category, ModelCatalog catalog)
    {
        var fields = catalog.Fields.GetValueOrDefault(category) ?? new List<FieldChoice>();
        FieldChoice? Select(params BuiltInParameter[] ids) => ids.SelectMany(id => fields.Where(f => f.ParameterId == (long)id
            && f.FieldContext is "Instance" or "ElementType")).FirstOrDefault();
        var width = Select(category == CategoryKind.Doors ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH);
        var height = Select(category == CategoryKind.Doors ? BuiltInParameter.DOOR_HEIGHT : BuiltInParameter.WINDOW_HEIGHT);
        return new CategoryMapping
        {
            Category = category,
            Width = width is { IsLength: true, StorageType: "Double", Binding: ParameterBinding.Instance or ParameterBinding.Type } ? width : null,
            Height = height is { IsLength: true, StorageType: "Double", Binding: ParameterBinding.Instance or ParameterBinding.Type } ? height : null,
            TypeMark = Select(BuiltInParameter.ALL_MODEL_TYPE_MARK),
            FamilyType = Select(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM),
            Level = Select(BuiltInParameter.FAMILY_LEVEL_PARAM, BuiltInParameter.SCHEDULE_LEVEL_PARAM, BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM)
        };
    }
}

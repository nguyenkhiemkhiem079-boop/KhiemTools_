using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using KhimTools.DoorSchedule.Models;

namespace KhimTools.DoorSchedule.Services;

/// <summary>Ownership uses fixed Extensible Storage metadata, never a schedule name.</summary>
public sealed class ManagedScheduleStore
{
    public static readonly Guid SchemaGuid = new("87555feb-3955-4ccb-a153-a44b05795342");
    public const int SchemaVersion = 1;
    private readonly Document _doc;
    public ManagedScheduleStore(Document doc) => _doc = doc;

    public sealed class State
    {
        public int Version { get; set; } = SchemaVersion;
        public string Slot { get; set; } = "";
        public string Category { get; set; } = "";
        public string ConfigurationJson { get; set; } = "";
        public string Signature { get; set; } = "";
        public List<int> ManagedFieldIds { get; set; } = new();
    }

    public static string Slot(ScheduleOptions options, CategoryKind category) => category + "|level=" + (options.LevelUniqueId ?? "all")
        + "|phase=" + options.PhaseUniqueId + "|phaseFilter=" + options.PhaseFilterUniqueId + "|grouping=" + options.Grouping;

    public (ViewSchedule? Schedule, State? Metadata) Find(string slot)
    {
        var matches = new FilteredElementCollector(_doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate).Select(s => (Schedule: s, Metadata: Read(s)))
            .Where(pair => pair.Metadata?.Slot == slot).ToList();
        if (matches.Count > 1)
            throw new InvalidOperationException($"Có nhiều bảng có cùng managed slot '{slot}'. Hãy chọn Tạo bản riêng để bảo toàn các bảng hiện có.");
        return matches.Count == 1 ? matches[0] : (null, null);
    }

    public State? Read(ViewSchedule schedule)
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema == null) return null;
        var entity = schedule.GetEntity(schema);
        if (!entity.IsValid()) return null;
        var json = entity.Get<string>(schema.GetField("Configuration"));
        try { return JsonSerializer.Deserialize<State>(json) ?? throw new InvalidOperationException("Ownership metadata trống."); }
        catch (JsonException ex) { throw new InvalidOperationException($"Ownership metadata không đọc được tại schedule {schedule.Id.Value}; dùng Tạo bản riêng.", ex); }
    }

    public void Write(ViewSchedule schedule, State state)
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema == null)
        {
            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName("KhimToolsDoorWindowSchedule");
            builder.SetDocumentation("K-TOOL standalone native schedule ownership/configuration. Versioned JSON, stable model unique IDs, owned native field IDs and guarded definition signature.");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("Configuration", typeof(string));
            schema = builder.Finish();
        }
        state.Signature = DefinitionSignature(schedule, state.ManagedFieldIds);
        var entity = new Entity(schema);
        entity.Set(schema.GetField("Configuration"), JsonSerializer.Serialize(state));
        schedule.SetEntity(entity);
    }

    public void AssertSafeUpdate(ViewSchedule schedule, State state)
    {
        if (state.Version != SchemaVersion)
            throw new InvalidOperationException($"Schedule {schedule.Id.Value} dùng metadata version {state.Version}. Chọn Tạo bản riêng.");
        if (schedule.ViewTemplateId != ElementId.InvalidElementId)
            throw new InvalidOperationException($"Schedule '{schedule.Name}' có View Template. Tool không thay template hoặc gỡ template; chọn Tạo bản riêng.");
        if (DefinitionSignature(schedule, state.ManagedFieldIds) != state.Signature)
            throw new InvalidOperationException($"Schedule '{schedule.Name}' đã được sửa các managed fields, filter, sort/group hoặc phase. Các thay đổi được bảo toàn; chọn Tạo bản riêng.");
        AssertEditable(schedule);
    }

    public void AssertEditable(Element element)
    {
        if (!_doc.IsWorkshared) return;
        if (WorksharingUtils.GetCheckoutStatus(_doc, element.Id) == CheckoutStatus.OwnedByOtherUser)
        {
            var info = WorksharingUtils.GetWorksharingTooltipInfo(_doc, element.Id);
            throw new InvalidOperationException($"'{element.Name}' (Element {element.Id.Value}) đang thuộc '{info.Owner}'. Yêu cầu relinquish/synchronize hoặc chọn Tạo bản riêng.");
        }
    }

    public static string DefinitionSignature(ViewSchedule schedule, IEnumerable<int> managedFieldIds)
    {
        var owned = managedFieldIds.ToHashSet();
        var definition = schedule.Definition;
        var fields = definition.GetFieldOrder().Select(id => definition.GetField(id))
            .Where(field => owned.Contains(field.FieldId.IntegerValue))
            .Select(field => $"{field.FieldId.IntegerValue}:{field.ParameterId.Value}:{field.FieldType}:{field.IsHidden}:{field.ColumnHeading}");
        var filters = definition.GetFilters().Select(filter =>
        {
            string value = filter.IsElementIdValue ? "id=" + filter.GetElementIdValue().Value
                : filter.IsStringValue ? "s=" + filter.GetStringValue()
                : filter.IsIntegerValue ? "i=" + filter.GetIntegerValue()
                : filter.IsDoubleValue ? "d=" + filter.GetDoubleValue().ToString("R", CultureInfo.InvariantCulture) : "null";
            return $"{filter.FieldId.IntegerValue}:{filter.FilterType}:{value}";
        });
        var sorting = definition.GetSortGroupFields().Select(sort =>
            $"{sort.FieldId.IntegerValue}:{sort.SortOrder}:{sort.ShowHeader}:{sort.ShowFooter}:{sort.ShowBlankLine}");
        string signature = string.Join("|", fields) + "\n" + string.Join("|", filters) + "\n" + string.Join("|", sorting)
            + $"\nitemized={definition.IsItemized};links={definition.IncludeLinkedFiles};grand={definition.ShowGrandTotal}"
            + $";phase={schedule.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId().Value};filter={schedule.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER)?.AsElementId().Value}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
    }
}

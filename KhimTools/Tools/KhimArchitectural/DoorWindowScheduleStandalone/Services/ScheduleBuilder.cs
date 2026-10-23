using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using KhimTools.DoorSchedule.Models;

namespace KhimTools.DoorSchedule.Services;

/// <summary>All methods must be called synchronously from the Revit API thread.</summary>
public sealed class ScheduleBuilder
{
    private readonly Document _doc;
    private readonly View _activeView;
    private readonly ParameterResolver _resolver;
    private readonly ManagedScheduleStore _store;
    public ScheduleBuilder(Document doc, View activeView)
    { _doc = doc; _activeView = activeView; _resolver = new(doc); _store = new(doc); }

    public CategoryMapping DefaultMapping(CategoryKind category, ModelCatalog catalog) => ParameterResolver.DefaultMapping(category, catalog);

    public ModelCatalog Inspect()
    {
        var catalog = new ModelCatalog
        {
            Levels = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(e => e.Elevation).Select(Choice).ToList(),
            Phases = _doc.Phases.Cast<Phase>().Select(Choice).ToList(),
            PhaseFilters = new FilteredElementCollector(_doc).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>().Select(Choice).OrderBy(c => c.Name).ToList(),
            Sheets = new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => !s.IsPlaceholder)
                .Select(s => new NamedChoice { UniqueId = s.UniqueId, Name = $"{s.SheetNumber} — {s.Name}" }).OrderBy(s => s.Name).ToList()
        };
        var phase = _doc.GetElement(_activeView.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId() ?? ElementId.InvalidElementId) as Phase;
        var filter = _doc.GetElement(_activeView.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER)?.AsElementId() ?? ElementId.InvalidElementId) as PhaseFilter;
        catalog.SeedPhaseUniqueId = phase?.UniqueId ?? catalog.Phases.FirstOrDefault()?.UniqueId;
        catalog.SeedPhaseFilterUniqueId = filter?.UniqueId;
        // Discovery adds native fields only to temporary native schedules and rolls back every document change.
        using var group = new TransactionGroup(_doc, "K-TOOL: tạm đọc native field catalog");
        group.Start();
        try
        {
            using var transaction = new Transaction(_doc, "Tạm khám phá fields");
            transaction.Start();
            foreach (CategoryKind category in Enum.GetValues<CategoryKind>())
            {
                var schedule = ViewSchedule.CreateSchedule(_doc, new ElementId(ParameterResolver.NativeCategory(category)));
                catalog.Fields[category] = _resolver.Discover(schedule, category, RawInstances(category));
            }
            RequireCommitted(transaction.Commit());
            group.RollBack();
            return catalog;
        }
        catch
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            throw;
        }
    }

    public PreviewResult Preview(ScheduleOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var records = new List<DoorWindowRecord>();
        var inventory = new Dictionary<CategoryKind, int>();
        if (string.IsNullOrWhiteSpace(options.PhaseUniqueId) || string.IsNullOrWhiteSpace(options.PhaseFilterUniqueId)
            || _doc.GetElement(options.PhaseUniqueId) is not Phase phase || _doc.GetElement(options.PhaseFilterUniqueId) is not PhaseFilter phaseFilter)
            return InvalidPreview(options, "PHASE_REQUIRED", "Chọn Phase và Phase Filter hợp lệ của project.");
        if (options.LevelUniqueId != null && _doc.GetElement(options.LevelUniqueId) is not Level)
            return InvalidPreview(options, "LEVEL_INVALID", "Level đã chọn không còn tồn tại trong host project.");
        if (!options.Doors && !options.Windows) return InvalidPreview(options, "CATEGORY_REQUIRED", "Chọn Cửa đi, Cửa sổ hoặc cả hai.");
        using var group = new TransactionGroup(_doc, "K-TOOL: preview (hoàn toàn rollback)");
        group.Start();
        try
        {
            foreach (var category in Requested(options))
            {
                var raw = RawInstances(category);
                inventory[category] = raw.Count;
                if (raw.Count == 0)
                {
                    AddZeroMessage(category, true, diagnostics);
                    continue;
                }
                var mappings = options.Mappings.Where(m => m.Category == category).ToList();
                if (mappings.Count != 1)
                {
                    diagnostics.Add(Error("MAPPING_REQUIRED", $"{category}: cần đúng một bộ parameter mapping."));
                    continue;
                }
                var mapping = mappings[0];
                ViewSchedule schedule;
                using (var transaction = new Transaction(_doc, "Tạm tạo schedule để xác định membership"))
                {
                    transaction.Start();
                    schedule = ViewSchedule.CreateSchedule(_doc, new ElementId(ParameterResolver.NativeCategory(category)));
                    ConfigureBase(schedule, options, mapping, phase, phaseFilter);
                    RequireCommitted(transaction.Commit());
                }
                var preliminary = Members(schedule, category);
                if (preliminary.Count == 0)
                {
                    AddZeroMessage(category, false, diagnostics);
                    continue;
                }
                var errors = ValidateMapping(mapping, options);
                diagnostics.AddRange(errors);
                if (errors.Count > 0)
                {
                    // Missing mappings must not make real eligible instances disappear from the preview.
                    // These rows are explicitly incomplete and cannot be used to create/export a schedule.
                    records.AddRange(new DoorWindowReader(_doc).Read(category, preliminary, mapping, phase, diagnostics));
                    diagnostics.Add(new Diagnostic { Severity = "INFO", Code = "INCOMPLETE_PREVIEW",
                        Message = $"{category}: đã đọc {preliminary.Count} host instances theo scope/phase. Sửa ánh xạ để hoàn tất kiểm tra native schedule; Tạo và Xuất CSV đang bị khóa." });
                    continue;
                }
                using (var transaction = new Transaction(_doc, "Tạm áp dụng đúng native cấu hình"))
                {
                    transaction.Start();
                    var formattingMessages = new List<string>();
                    Configure(schedule, options, mapping, null, formattingMessages);
                    diagnostics.AddRange(formattingMessages.Select(message => new Diagnostic { Code = "FORMATTING_PARTIAL", Message = message }));
                    RequireCommitted(transaction.Commit());
                }
                var members = Members(schedule, category);
                // Materialize model data first. A native footer read failure still blocks creation,
                // but no longer turns the existing membership into an apparently empty model.
                records.AddRange(new DoorWindowReader(_doc).Read(category, members, mapping, phase, diagnostics));
                try
                {
                    int nativeCount = NativeCount(schedule);
                    if (nativeCount != members.Count)
                        diagnostics.Add(Error("NATIVE_COUNT_MISMATCH", $"{category}: native Count {nativeCount} khác schedule membership {members.Count}."));
                    else diagnostics.Add(new Diagnostic { Severity = "INFO", Code = "NATIVE_COUNT_RECONCILED",
                        Message = $"{category}: native grand-total Count = {nativeCount}; {members.Count} eligible host instances. Raw category inventory = {raw.Count}." });
                }
                catch (Exception ex)
                {
                    var log = LocalLog.Write(ex);
                    diagnostics.Add(Error("NATIVE_COUNT_READ_FAILED", $"{category}: đã đọc {members.Count} instance nhưng chưa đối chiếu được native Count. {ex.Message}"
                        + (log == null ? "" : "\nLocal log: " + log)));
                }
                diagnostics.Add(new Diagnostic { Severity = "INFO", Code = "NATIVE_DESIGN_OPTIONS",
                    Message = $"{category}: membership lấy từ chính native schedule, dùng design-option visibility mặc định của schedule; không cộng tất cả alternative options." });
            }
            group.RollBack();
        }
        catch (Exception ex)
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            var log = LocalLog.Write(ex);
            diagnostics.Add(Error("NATIVE_PREVIEW_FAILED", ex.Message + (log == null ? " (Không ghi được local log.)" : "\nLocal log: " + log)));
        }
        var result = ValidationService.ValidateAndGroup(records, options, diagnostics);
        result.RawInventory = inventory;
        result.ConfigurationFingerprint = ConfigurationFingerprint(options);
        return result;
    }

    public BuildResult CreateOrUpdate(ScheduleOptions options, PreviewResult preview)
    {
        if (!preview.IsValid) throw new InvalidOperationException("Preview còn lỗi; sửa mapping/scope và xem lại preview trước khi tạo.");
        if (preview.ConfigurationFingerprint != ConfigurationFingerprint(options))
            throw new InvalidOperationException("Cấu hình đã thay đổi sau preview. Xem lại preview trước khi tạo.");
        var fresh = Preview(options);
        if (!fresh.IsValid) throw new InvalidOperationException(string.Join("\n", fresh.Diagnostics.Where(d => d.Severity == "ERROR").Select(d => d.Message)));
        if (DataFingerprint(fresh) != DataFingerprint(preview))
            throw new InvalidOperationException("Dữ liệu model đã thay đổi sau preview. Xem lại preview để kiểm tra số lượng/kích thước.");
        if (fresh.TotalCount == 0) throw new InvalidOperationException("Không có host instances đủ điều kiện trong scope/phase đã chọn; không tạo bảng trống.");
        var result = new BuildResult();
        var phase = (Phase)_doc.GetElement(options.PhaseUniqueId);
        var phaseFilter = (PhaseFilter)_doc.GetElement(options.PhaseFilterUniqueId);
        using var group = new TransactionGroup(_doc, "K-TOOL — tạo/cập nhật thống kê cửa");
        group.Start();
        try
        {
            using var transaction = new Transaction(_doc, "Native Door & Window schedules");
            transaction.Start();
            var createdSchedules = new List<ViewSchedule>();
            foreach (var category in Requested(options))
            {
                var expected = fresh.Records.Where(r => r.Category == category).ToList();
                if (expected.Count == 0)
                { result.Messages.Add($"{category}: 0 eligible host instances — không tạo bảng trống."); continue; }
                var mapping = options.Mappings.Single(m => m.Category == category);
                var slot = ManagedScheduleStore.Slot(options, category);
                var existing = options.CreateSeparateCopy ? (Schedule: (ViewSchedule?)null, Metadata: (ManagedScheduleStore.State?)null) : _store.Find(slot);
                ViewSchedule schedule;
                if (existing.Schedule != null && existing.Metadata != null)
                {
                    schedule = existing.Schedule;
                    _store.AssertSafeUpdate(schedule, existing.Metadata);
                    var previousConfiguration = JsonSerializer.Deserialize<ScheduleOptions>(existing.Metadata.ConfigurationJson)
                        ?? throw new InvalidOperationException("Managed configuration metadata trống; chọn Tạo bản riêng.");
                    if (previousConfiguration.Prefix.Trim() != options.Prefix.Trim())
                    {
                        var desiredName = options.Prefix.Trim() + (category == CategoryKind.Doors ? " — THỐNG KÊ CỬA ĐI" : " — THỐNG KÊ CỬA SỔ");
                        schedule.Name = UniqueViewName(desiredName, schedule.Id);
                        result.Messages.Add($"Đổi prefix trên cùng schedule ElementId {schedule.Id.Value}: '{schedule.Name}'.");
                    }
                    result.Messages.Add($"Cập nhật tại chỗ '{schedule.Name}', ElementId {schedule.Id.Value}; giữ sheet placement và manual fields tương thích.");
                }
                else
                {
                    schedule = ViewSchedule.CreateSchedule(_doc, new ElementId(ParameterResolver.NativeCategory(category)));
                    var desired = options.Prefix.Trim() + (category == CategoryKind.Doors ? " — THỐNG KÊ CỬA ĐI" : " — THỐNG KÊ CỬA SỔ");
                    schedule.Name = UniqueViewName(desired);
                    if (schedule.Name != desired) result.Messages.Add($"Tên '{desired}' đã có; bảng hiện có được giữ nguyên, tạo '{schedule.Name}'.");
                    ConfigureBase(schedule, options, mapping, phase, phaseFilter);
                    result.Messages.Add($"Tạo '{schedule.Name}', ElementId {schedule.Id.Value}.");
                }
                var owned = Configure(schedule, options, mapping, existing.Metadata, result.Messages);
                SetPhase(schedule, phase, phaseFilter);
                _doc.Regenerate();
                var actualIds = Members(schedule, category).Select(e => e.UniqueId).OrderBy(id => id, StringComparer.Ordinal);
                if (!actualIds.SequenceEqual(expected.Select(r => r.UniqueId).OrderBy(id => id, StringComparer.Ordinal)))
                    throw new InvalidOperationException($"{category}: final schedule membership không khớp preview; thao tác được rollback.");
                var state = new ManagedScheduleStore.State
                {
                    Slot = options.CreateSeparateCopy ? slot + "|copy:" + schedule.UniqueId : slot,
                    Category = category.ToString(), ConfigurationJson = JsonSerializer.Serialize(options), ManagedFieldIds = owned
                };
                _store.Write(schedule, state);
                createdSchedules.Add(schedule);
                result.ScheduleIds.Add(schedule.Id.Value);
            }
            _doc.Regenerate();
            double nextYmm = options.SheetYmm;
            foreach (var schedule in createdSchedules)
            {
                var category = schedule.Definition.CategoryId == new ElementId(BuiltInCategory.OST_Doors) ? CategoryKind.Doors : CategoryKind.Windows;
                int expectedCount = fresh.Records.Count(r => r.Category == category);
                int actualCount = NativeCount(schedule);
                if (actualCount != expectedCount) throw new InvalidOperationException($"Native Count tại '{schedule.Name}' = {actualCount}, preview = {expectedCount}; rollback.");
                result.Messages.Add($"'{schedule.Name}': native grand-total Count {actualCount} khớp {expectedCount} eligible instances.");
                if (options.SheetUniqueId != null) nextYmm = PlaceOnSheet(schedule, options, nextYmm, result.Messages);
            }
            RequireCommitted(transaction.Commit());
            RequireCommitted(group.Assimilate());
            return result;
        }
        catch
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            throw;
        }
    }

    private void ConfigureBase(ViewSchedule schedule, ScheduleOptions options, CategoryMapping mapping, Phase phase, PhaseFilter phaseFilter)
    {
        if (schedule.Definition.CanIncludeLinkedFiles()) schedule.Definition.IncludeLinkedFiles = false;
        SetPhase(schedule, phase, phaseFilter);
        if (!schedule.Definition.GetFieldOrder().Select(id => schedule.Definition.GetField(id)).Any(f => f.FieldType == ScheduleFieldType.Count))
            schedule.Definition.AddField(ScheduleFieldType.Count);
        if (options.LevelUniqueId == null) return;
        if (mapping.Level == null) throw new InvalidOperationException($"{mapping.Category}: chọn native Level field để lọc một tầng.");
        var levelField = schedule.Definition.AddField(_resolver.ResolveNative(schedule.Definition, mapping.Level));
        levelField.IsHidden = true;
        if (!schedule.Definition.CanFilterByValue(levelField.FieldId)) throw new InvalidOperationException("Level field không hỗ trợ native value filter.");
        schedule.Definition.AddFilter(new ScheduleFilter(levelField.FieldId, ScheduleFilterType.Equal, _doc.GetElement(options.LevelUniqueId).Id));
    }

    private List<int> Configure(ViewSchedule schedule, ScheduleOptions options, CategoryMapping mapping,
        ManagedScheduleStore.State? existing, ICollection<string> messages)
    {
        var definition = schedule.Definition;
        if (definition.CanIncludeLinkedFiles()) definition.IncludeLinkedFiles = false;
        var ownedIds = existing?.ManagedFieldIds.ToHashSet() ?? definition.GetFieldOrder().Select(id => id.IntegerValue).ToHashSet();
        var nativeFields = definition.GetFieldOrder().Select(id => definition.GetField(id)).ToList();
        var retainedIds = new List<int>();
        var byRole = new Dictionary<string, ScheduleField>(StringComparer.Ordinal);
        var requests = RequestedFields(mapping, options);
        foreach (var request in requests)
        {
            var choice = request.Choice;
            var native = _resolver.ResolveNative(definition, choice);
            var matching = nativeFields.Where(f => f.ParameterId.Value == choice.ParameterId && f.FieldType.ToString() == choice.FieldContext).ToList();
            if (matching.Any(f => !ownedIds.Contains(f.FieldId.IntegerValue)))
                throw new InvalidOperationException($"Manual field '{choice.Name}' trong '{schedule.Name}' trùng với mapping tool. Chọn Tạo bản riêng để bảo toàn manual field.");
            var field = matching.FirstOrDefault() ?? definition.AddField(native);
            if (matching.Count > 1) throw new InvalidOperationException($"Có nhiều native fields trùng mapping {choice.DisplayLabel}; chọn Tạo bản riêng.");
            field.ColumnHeading = request.Heading;
            field.IsHidden = request.Hidden;
            field.MultipleValuesDisplayType = ScheduleFieldMultipleValuesDisplayType.Custom;
            field.MultipleValuesCustomText = ValidationService.MultipleValues;
            field.HorizontalAlignment = request.Role is "WIDTH" or "HEIGHT" or "TYPE_MARK" ? ScheduleHorizontalAlignment.Center : ScheduleHorizontalAlignment.Left;
            field.SheetColumnWidth = UnitUtils.ConvertToInternalUnits(request.Role == "FAMILY_TYPE" ? 65 : request.Role is "WIDTH" or "HEIGHT" ? 27 : 35, UnitTypeId.Millimeters);
            if ((request.Role is "WIDTH" or "HEIGHT") && field.GetSpecTypeId() != SpecTypeId.Length)
                throw new InvalidOperationException($"{request.Role}: native field không phải Length.");
            if (field.GetSpecTypeId() == SpecTypeId.Length)
            {
                var format = new FormatOptions(UnitTypeId.Millimeters) { UseDefault = false, Accuracy = 0.1 };
                field.SetFormatOptions(format);
            }
            ApplyBodyStyle(field);
            byRole[request.Role] = field;
            retainedIds.Add(field.FieldId.IntegerValue);
        }
        var countFields = nativeFields.Where(f => f.FieldType == ScheduleFieldType.Count).ToList();
        if (countFields.Any(f => !ownedIds.Contains(f.FieldId.IntegerValue)))
            throw new InvalidOperationException("Manual Count field đã có; chọn Tạo bản riêng.");
        var count = countFields.FirstOrDefault() ?? definition.AddField(ScheduleFieldType.Count);
        count.ColumnHeading = "SỐ LƯỢNG";
        count.IsHidden = false;
        count.HorizontalAlignment = ScheduleHorizontalAlignment.Center;
        count.SheetColumnWidth = UnitUtils.ConvertToInternalUnits(25, UnitTypeId.Millimeters);
        if (count.CanTotal()) count.DisplayType = ScheduleFieldDisplayType.Totals;
        ApplyBodyStyle(count);
        retainedIds.Add(count.FieldId.IntegerValue);
        definition.ClearFilters();
        definition.ClearSortGroupFields();
        foreach (var field in nativeFields.Where(f => ownedIds.Contains(f.FieldId.IntegerValue) && !retainedIds.Contains(f.FieldId.IntegerValue)))
            definition.RemoveField(field.FieldId);
        definition.IsItemized = options.Grouping == GroupingMode.EveryInstance;
        if (!definition.IsItemized)
        {
            var keys = options.Grouping == GroupingMode.LevelTypeDimensions
                ? new[] { "LEVEL", "FAMILY_TYPE", "WIDTH", "HEIGHT" } : new[] { "FAMILY_TYPE", "WIDTH", "HEIGHT" };
            if (keys.Length > 4) throw new InvalidOperationException("Revit 2025 chỉ hỗ trợ tối đa 4 sort/group fields.");
            foreach (var key in keys)
            {
                var field = byRole[key];
                if (!definition.CanSortByField(field.FieldId)) throw new InvalidOperationException($"Native {key} field không hỗ trợ sorting/grouping.");
                definition.AddSortGroupField(new ScheduleSortGroupField(field.FieldId, ScheduleSortOrder.Ascending));
            }
        }
        if (options.LevelUniqueId != null)
        {
            var level = byRole["LEVEL"];
            if (!definition.CanFilterByValue(level.FieldId)) throw new InvalidOperationException("Native Level field không hỗ trợ value filter.");
            definition.AddFilter(new ScheduleFilter(level.FieldId, ScheduleFilterType.Equal, _doc.GetElement(options.LevelUniqueId).Id));
        }
        definition.ShowTitle = true; definition.ShowHeaders = true; definition.ShowGridLines = true;
        definition.ShowGrandTotal = true; definition.ShowGrandTotalTitle = true; definition.ShowGrandTotalCount = true;
        definition.GrandTotalTitle = "TỔNG CỘNG";
        // Keep manual columns in place; reorder only the tool-owned block for a clean default table.
        var order = retainedIds.Select(id => new ScheduleFieldId(id))
            .Concat(definition.GetFieldOrder().Where(id => !retainedIds.Contains(id.IntegerValue))).ToList();
        definition.SetFieldOrder(order);
        try { ApplyHeaderStyle(schedule); }
        catch (Autodesk.Revit.Exceptions.ArgumentException ex) { messages.Add("Định dạng header một phần: " + ex.Message); }
        return retainedIds;
    }

    private static List<(string Role, string Heading, FieldChoice Choice, bool Hidden)> RequestedFields(CategoryMapping mapping, ScheduleOptions options)
    {
        var fields = new List<(string, string, FieldChoice, bool)>
        {
            ("TYPE_MARK", "MÃ LOẠI CỬA", mapping.TypeMark!, false),
            ("FAMILY_TYPE", "FAMILY / TYPE", mapping.FamilyType!, false),
            ("WIDTH", "RỘNG (mm)", mapping.Width!, false),
            ("HEIGHT", "CAO (mm)", mapping.Height!, false)
        };
        if (options.Grouping == GroupingMode.LevelTypeDimensions || options.Grouping == GroupingMode.EveryInstance || options.LevelUniqueId != null)
            fields.Add(("LEVEL", "TẦNG", mapping.Level!, options.Grouping == GroupingMode.TypeDimensions));
        fields.AddRange(mapping.OptionalColumns.Select(c => (c.Role, c.Heading, c.Field, false)));
        return fields;
    }

    private List<Diagnostic> ValidateMapping(CategoryMapping mapping, ScheduleOptions options)
    {
        var errors = new List<Diagnostic>();
        void Need(FieldChoice? field, string role)
        {
            if (field == null) { errors.Add(Error("MAPPING_REQUIRED", $"{mapping.Category}: chưa chọn {role}.")); return; }
            if (field.Category != mapping.Category || _resolver.Identity(field.ParameterId) != field.Identity)
                errors.Add(Error("MAPPING_IDENTITY_INVALID", $"{mapping.Category}: {role} sai category/identity hoặc parameter đã thay đổi."));
        }
        Need(mapping.TypeMark, "Type Mark"); Need(mapping.FamilyType, "Family and Type"); Need(mapping.Width, "Width"); Need(mapping.Height, "Height");
        if (mapping.TypeMark != null && (mapping.TypeMark.Binding != ParameterBinding.Type || mapping.TypeMark.StorageType != "String"))
            errors.Add(Error("TYPE_MARK_BINDING", "MÃ LOẠI CỬA phải lấy từ tham số type/String; instance Mark là cột riêng."));
        if (mapping.FamilyType != null && mapping.FamilyType.ParameterId != (long)BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM)
            errors.Add(Error("FAMILY_TYPE_IDENTITY", "Family / Type phải là native Family and Type field, không dùng một chuỗi tự đặt."));
        foreach (var dimension in new[] { ("Width", mapping.Width), ("Height", mapping.Height) })
            if (dimension.Item2 != null && (!dimension.Item2.IsLength || dimension.Item2.StorageType != "Double"
                || dimension.Item2.Binding is not (ParameterBinding.Instance or ParameterBinding.Type)))
                errors.Add(Error("DIMENSION_MAPPING_INVALID", $"{dimension.Item1}: cần Double/Length và binding instance/type duy nhất; không fallback theo từng element."));
        bool needLevel = options.Grouping != GroupingMode.TypeDimensions || options.LevelUniqueId != null;
        if (needLevel)
        {
            Need(mapping.Level, "Level");
            if (mapping.Level != null && !NativeLevelParameterIds.Contains(mapping.Level.ParameterId))
                errors.Add(Error("LEVEL_MAPPING_INVALID", "Chọn actual Door/Window Level field; Room Level không được dùng cho door/window scope."));
        }
        foreach (var column in mapping.OptionalColumns)
        {
            Need(column.Field, column.Role);
            if (column.Field.Binding is ParameterBinding.Unknown || column.Field.FieldContext is not ("Instance" or "ElementType" or "FromRoom" or "ToRoom"))
                errors.Add(Error("OPTIONAL_BINDING_UNRESOLVED", $"{column.Role}: native source/binding chưa xác định: {column.Field.DisplayLabel}."));
            if (column.Role == "IMAGE" && column.Field.ParameterId != (long)BuiltInParameter.ALL_MODEL_IMAGE
                && column.Field.ParameterId != (long)BuiltInParameter.ALL_MODEL_TYPE_IMAGE)
                errors.Add(Error("IMAGE_MAPPING_INVALID", "Ảnh phải dùng native Image/Type Image field có sẵn; không dùng một parameter bất kỳ làm hình minh họa."));
            if (options.Grouping != GroupingMode.EveryInstance && (column.Role is "MARK" or "FROM_NUMBER" or "FROM_NAME" or "TO_NUMBER" or "TO_NAME"
                || column.Field.FieldContext is "FromRoom" or "ToRoom"))
                errors.Add(Error("INSTANCE_COLUMN_IN_GROUPED", $"{column.Role}: chọn Từng instance để dùng instance Mark/From Room/To Room."));
        }
        if (errors.Count == 0)
        {
            var fields = RequestedFields(mapping, options);
            foreach (var duplicate in fields.GroupBy(f => f.Choice.ParameterId + "|" + f.Choice.FieldContext).Where(g => g.Count() > 1))
                errors.Add(Error("NATIVE_FIELD_DUPLICATE_ROLE", $"Một native field được chọn cho nhiều role: {string.Join(", ", duplicate.Select(f => f.Role))}. Chọn nguồn khác hoặc bỏ cột trùng."));
            if (options.Grouping != GroupingMode.EveryInstance)
                foreach (var key in fields.Where(f => f.Role is "FAMILY_TYPE" or "WIDTH" or "HEIGHT" || (options.Grouping == GroupingMode.LevelTypeDimensions && f.Role == "LEVEL")))
                    if (!key.Choice.IsSortable) errors.Add(Error("SORT_UNSUPPORTED", $"{key.Role}: native field không hỗ trợ sort/group."));
        }
        return errors;
    }

    private static readonly HashSet<long> NativeLevelParameterIds = new()
    {
        (long)BuiltInParameter.FAMILY_LEVEL_PARAM, (long)BuiltInParameter.SCHEDULE_LEVEL_PARAM,
        (long)BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM, (long)BuiltInParameter.LEVEL_PARAM
    };

    private static void SetPhase(ViewSchedule schedule, Phase phase, PhaseFilter filter)
    {
        void Set(BuiltInParameter id, ElementId value)
        {
            var parameter = schedule.get_Parameter(id);
            if (parameter == null || parameter.StorageType != StorageType.ElementId)
                throw new InvalidOperationException($"Schedule không cho đặt {id}; không thể bảo đảm phase/filter trùng preview.");
            if (parameter.AsElementId() != value)
            {
                if (parameter.IsReadOnly) throw new InvalidOperationException($"Schedule không cho thay {id}; kiểm tra controlling View Template.");
                parameter.Set(value);
            }
            if (parameter.AsElementId() != value)
                throw new InvalidOperationException($"Schedule không giữ được {id} đã chọn; không thể bảo đảm phase/filter trùng preview.");
        }
        Set(BuiltInParameter.VIEW_PHASE, phase.Id); Set(BuiltInParameter.VIEW_PHASE_FILTER, filter.Id);
    }

    private static void ApplyHeaderStyle(ViewSchedule schedule)
    {
        var header = schedule.GetTableData().GetSectionData(SectionType.Header);
        var style = new TableCellStyle
        {
            BackgroundColor = new Color(255, 255, 255),
            TextColor = new Color(0, 0, 0), IsFontBold = true
        };
        var overrides = new TableCellStyleOverrideOptions { BackgroundColor = true, FontColor = true, Bold = true };
        style.SetCellStyleOverrideOptions(overrides);
        header.SetCellStyle(style);
    }

    private static void ApplyBodyStyle(ScheduleField field)
    {
        var style = new TableCellStyle { BackgroundColor = new Color(255, 255, 255), TextColor = new Color(0, 0, 0) };
        style.SetCellStyleOverrideOptions(new TableCellStyleOverrideOptions { BackgroundColor = true, FontColor = true });
        field.SetStyle(style);
    }

    private int NativeCount(ViewSchedule schedule)
    {
        if (!schedule.RefreshData()) throw new InvalidOperationException("Native schedule data chưa cập nhật; không thể đối chiếu Count.");
        var definition = schedule.Definition;
        var visible = definition.GetFieldOrder().Select(id => definition.GetField(id)).Where(f => !f.IsHidden).ToList();
        int countColumn = visible.FindIndex(f => f.FieldType == ScheduleFieldType.Count);
        var body = schedule.GetTableData().GetSectionData(SectionType.Body);
        if (countColumn < 0 || body.NumberOfRows == 0) throw new InvalidOperationException("Không đọc được native grand-total Count; xem schedule configuration.");
        string value = schedule.GetCellText(SectionType.Body, body.LastRowNumber, body.FirstColumnNumber + countColumn);
        if (int.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out int count)
            || int.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out count)) return count;
        throw new InvalidOperationException($"Native grand-total Count không phải số nguyên: '{value}'. Không dùng body row count làm quantity.");
    }

    private double PlaceOnSheet(ViewSchedule schedule, ScheduleOptions options, double yMm, ICollection<string> messages)
    {
        var sheet = _doc.GetElement(options.SheetUniqueId!) as ViewSheet;
        if (sheet == null || sheet.IsPlaceholder) throw new InvalidOperationException("Sheet đã chọn không hợp lệ.");
        _store.AssertEditable(sheet);
        var placed = new FilteredElementCollector(_doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>()
            .FirstOrDefault(i => i.ScheduleId == schedule.Id && i.OwnerViewId == sheet.Id);
        if (placed != null)
        {
            _doc.Regenerate();
            var existingBox = placed.get_BoundingBox(sheet) ?? throw new InvalidOperationException("Không đọc được bounds của placement hiện có.");
            messages.Add($"'{schedule.Name}' đã trên sheet {sheet.SheetNumber}; giữ nguyên vị trí, không tạo duplicate.");
            return UnitUtils.ConvertFromInternalUnits(existingBox.Min.Y, UnitTypeId.Millimeters) - 10;
        }
        var outline = sheet.Outline;
        if (outline.Max.U <= outline.Min.U || outline.Max.V <= outline.Min.V)
            throw new InvalidOperationException("Sheet không có bounds dùng được; chọn sheet có title block hiện có.");
        if (!double.IsFinite(options.SheetXmm) || !double.IsFinite(yMm)) throw new InvalidOperationException("Tọa độ sheet phải là số hữu hạn.");
        var point = new XYZ(UnitUtils.ConvertToInternalUnits(options.SheetXmm, UnitTypeId.Millimeters), UnitUtils.ConvertToInternalUnits(yMm, UnitTypeId.Millimeters), 0);
        if (point.X < outline.Min.U || point.X > outline.Max.U || point.Y < outline.Min.V || point.Y > outline.Max.V)
            throw new InvalidOperationException("Điểm đặt nằm ngoài sheet bounds. Tọa độ mm tính từ gốc sheet; tool không thay nội dung sheet khác.");
        var instance = ScheduleSheetInstance.Create(_doc, sheet.Id, schedule.Id, point);
        _doc.Regenerate();
        var box = instance.get_BoundingBox(sheet) ?? throw new InvalidOperationException("Không đọc được schedule viewport bounds sau đặt.");
        double tolerance = UnitUtils.ConvertToInternalUnits(0.5, UnitTypeId.Millimeters);
        if (box.Min.X < outline.Min.U - tolerance || box.Max.X > outline.Max.U + tolerance || box.Min.Y < outline.Min.V - tolerance || box.Max.Y > outline.Max.V + tolerance)
            throw new InvalidOperationException($"Bảng '{schedule.Name}' vượt sheet bounds; đổi vị trí/chọn sheet lớn hơn. Cả thao tác được rollback.");
        messages.Add($"Đặt '{schedule.Name}' trên {sheet.SheetNumber}; X={options.SheetXmm:G} mm, Y={yMm:G} mm từ sheet origin.");
        return UnitUtils.ConvertFromInternalUnits(box.Min.Y, UnitTypeId.Millimeters) - 10;
    }

    private List<Element> RawInstances(CategoryKind category) => new FilteredElementCollector(_doc).OfCategory(ParameterResolver.NativeCategory(category)).WhereElementIsNotElementType().ToElements().ToList();
    private List<Element> Members(ViewSchedule schedule, CategoryKind category) => new FilteredElementCollector(_doc, schedule.Id)
        .OfCategory(ParameterResolver.NativeCategory(category)).WhereElementIsNotElementType().ToElements().ToList();

    private void AddZeroMessage(CategoryKind category, bool emptyRawHost, ICollection<Diagnostic> diagnostics)
    {
        var linkedCount = 0;
        if (emptyRawHost)
            foreach (var link in new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                var linkDoc = link.GetLinkDocument();
                if (linkDoc != null) linkedCount += new FilteredElementCollector(linkDoc).OfCategory(ParameterResolver.NativeCategory(category)).WhereElementIsNotElementType().GetElementCount();
            }
        diagnostics.Add(new Diagnostic { Severity = "INFO", Code = linkedCount > 0 ? "HOST_EMPTY_LINKS_PRESENT" : "CATEGORY_EMPTY",
            Message = linkedCount > 0 ? $"{category}: host trống, loaded links có {linkedCount} raw category instances. Nguồn đang là Chỉ model hiện tại; linked-model aggregation thuộc đợt sau."
                : $"{category}: không có host instances đủ điều kiện theo level/phase/filter đã chọn; không tạo bảng trống." });
    }

    private string UniqueViewName(string desired, ElementId? excludeId = null)
    {
        var names = new FilteredElementCollector(_doc).OfClass(typeof(View)).Cast<View>().Where(v => v.Id != excludeId).Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.Contains(desired)) return desired;
        int suffix = 2; while (names.Contains(desired + " (" + suffix + ")")) suffix++;
        return desired + " (" + suffix + ")";
    }

    private static NamedChoice Choice(Element element) => new() { UniqueId = element.UniqueId, Name = element.Name };
    private static IEnumerable<CategoryKind> Requested(ScheduleOptions options)
    { if (options.Doors) yield return CategoryKind.Doors; if (options.Windows) yield return CategoryKind.Windows; }
    private static Diagnostic Error(string code, string message) => new() { Severity = "ERROR", Code = code, Message = message };
    private static PreviewResult InvalidPreview(ScheduleOptions options, string code, string message) => new()
    { ConfigurationFingerprint = ConfigurationFingerprint(options), Diagnostics = new List<Diagnostic> { Error(code, message) } };
    private static void RequireCommitted(TransactionStatus status)
    { if (status != TransactionStatus.Committed) throw new InvalidOperationException("Revit transaction không commit thành công: " + status); }
    private static string ConfigurationFingerprint(ScheduleOptions options) => Hash(JsonSerializer.Serialize(options));
    private static string DataFingerprint(PreviewResult preview) => Hash(JsonSerializer.Serialize(preview.Records.OrderBy(r => r.UniqueId, StringComparer.Ordinal)));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

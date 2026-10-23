using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using KhimTools.DoorSchedule.Models;
using KhimTools.DoorSchedule.Services;
using Microsoft.Win32;
namespace KhimTools.DoorSchedule.UI;

public partial class ScheduleOptionsWindow : Window
{
    private readonly ScheduleBuilder _builder;
    private readonly ModelCatalog _catalog;
    private readonly Dictionary<CategoryKind, MappingEditor> _editors = new();
    private PreviewResult? _preview;
    private BuildResult? _built;
    private bool _initializing = true;
    public long? ViewToOpen { get; private set; }
    public bool HasCommittedChanges => _built is not null;

    public ScheduleOptionsWindow(ScheduleBuilder builder, ModelCatalog catalog)
    {
        InitializeComponent();
        Title += " · v1.0.1";
        _builder = builder; _catalog = catalog;
        LevelCombo.ItemsSource = new[] { new NamedChoice { Name = "Tất cả các tầng", UniqueId = "" } }.Concat(catalog.Levels).ToList();
        LevelCombo.SelectedIndex = 0;
        GroupingCombo.ItemsSource = new[] { new NamedChoice { UniqueId = "TypeDimensions", Name = "Type + rộng + cao" }, new NamedChoice { UniqueId = "LevelTypeDimensions", Name = "Tầng + type + rộng + cao" }, new NamedChoice { UniqueId = "EveryInstance", Name = "Mỗi instance một dòng" } };
        GroupingCombo.SelectedIndex = 0;
        PhaseCombo.ItemsSource = catalog.Phases;
        PhaseCombo.SelectedItem = catalog.Phases.FirstOrDefault(p => p.UniqueId == catalog.SeedPhaseUniqueId) ?? catalog.Phases.LastOrDefault();
        PhaseFilterCombo.ItemsSource = catalog.PhaseFilters;
        PhaseFilterCombo.SelectedItem = catalog.PhaseFilters.FirstOrDefault(p => p.UniqueId == catalog.SeedPhaseFilterUniqueId);
        SheetCombo.ItemsSource = catalog.Sheets;
        foreach (var category in Enum.GetValues<CategoryKind>())
        {
            var fields = catalog.Fields.GetValueOrDefault(category) ?? new();
            var editor = new MappingEditor(category, fields, _builder.DefaultMapping(category, catalog), InvalidatePreview);
            _editors[category] = editor; MappingHost.Children.Add(editor.Panel);
        }
        foreach (var combo in new[] { LevelCombo, GroupingCombo, PhaseCombo, PhaseFilterCombo, SheetCombo }) combo.SelectionChanged += (_, _) => InvalidatePreview();
        foreach (var check in new[] { DoorsCheck, WindowsCheck, PlaceCheck, SeparateCopyCheck }) { check.Checked += (_, _) => InvalidatePreview(); check.Unchecked += (_, _) => InvalidatePreview(); }
        foreach (var text in new[] { PrefixText, XText, YText }) text.TextChanged += (_, _) => InvalidatePreview();
        PlaceCheck.Checked += (_, _) => SheetCombo.IsEnabled = true;
        PlaceCheck.Unchecked += (_, _) => SheetCombo.IsEnabled = false;
        PreviewButton.Click += (_, _) => RunPreview();
        CreateButton.Click += (_, _) => CreateSchedules();
        ExportButton.Click += (_, _) => ExportCsv();
        CopyDiagnosticButton.Click += (_, _) => CopyDiagnostics();
        OpenButton.Click += (_, _) => { ViewToOpen = _built?.ScheduleIds.FirstOrDefault(); DialogResult = true; };
        _initializing = false;
        Loaded += (_, _) => TryInitialPreview();
        PreviewTab.AddHandler(System.Windows.Controls.Primitives.Selector.SelectedEvent,
            new RoutedEventHandler((_, e) => { if (ReferenceEquals(e.OriginalSource, PreviewTab)) TryInitialPreview(); }));
    }

    private void TryInitialPreview()
    {
        if (_initializing || _preview is not null) return;
        if (PhaseCombo.SelectedItem is NamedChoice && PhaseFilterCombo.SelectedItem is NamedChoice)
            RunPreview();
        else
            EmptyPreviewHint.Text = "Chọn Phase và Phase Filter của project, rồi bấm Làm mới xem trước.\nLưới chưa được đọc dữ liệu; chưa thể kết luận model không có cửa.";
    }

    private void InvalidatePreview()
    {
        if (_initializing) return;
        _preview = null;
        CreateButton.IsEnabled = ExportButton.IsEnabled = CopyDiagnosticButton.IsEnabled = false;
        PreviewGrid.ItemsSource = null;
        DiagnosticGrid.ItemsSource = null;
        EmptyPreviewHint.Visibility = Visibility.Visible;
        EmptyPreviewHint.Text = "Thiết lập đã đổi. Bấm Làm mới xem trước để đọc dữ liệu theo thiết lập mới.";
        StatusText.Text = "Thiết lập đã đổi. Làm mới xem trước để kiểm tra nguồn parameter và số lượng trước khi tạo.";
    }

    private ScheduleOptions ReadOptions()
    {
        if (DoorsCheck.IsChecked != true && WindowsCheck.IsChecked != true) throw new InvalidOperationException("Chọn ít nhất Cửa đi hoặc Cửa sổ.");
        if (PhaseCombo.SelectedItem is not NamedChoice phase || PhaseFilterCombo.SelectedItem is not NamedChoice filter) throw new InvalidOperationException("Chọn Phase và Phase Filter thực trong project. Không tự đếm tất cả các phase.");
        var options = new ScheduleOptions
        {
            Doors = DoorsCheck.IsChecked == true, Windows = WindowsCheck.IsChecked == true,
            LevelUniqueId = (LevelCombo.SelectedItem as NamedChoice)?.UniqueId is { Length: > 0 } levelId ? levelId : null,
            PhaseUniqueId = phase.UniqueId, PhaseFilterUniqueId = filter.UniqueId,
            Grouping = Enum.Parse<GroupingMode>(((NamedChoice)GroupingCombo.SelectedItem).UniqueId),
            Prefix = PrefixText.Text.Trim(), CreateSeparateCopy = SeparateCopyCheck.IsChecked == true,
            Mappings = _editors.Values.Select(e => e.Read()).ToList()
        };
        if (string.IsNullOrWhiteSpace(options.Prefix)) throw new InvalidOperationException("Nhập tiền tố tên bảng.");
        if (PlaceCheck.IsChecked == true)
        {
            if (SheetCombo.SelectedItem is not NamedChoice sheet) throw new InvalidOperationException("Chọn sheet có sẵn để đặt bảng.");
            if (!double.TryParse(XText.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var x) || !double.TryParse(YText.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var y) || !double.IsFinite(x) || !double.IsFinite(y)) throw new InvalidOperationException("Tọa độ sheet phải là số mm hợp lệ.");
            options.SheetUniqueId = sheet.UniqueId; options.SheetXmm = x; options.SheetYmm = y;
        }
        return options;
    }

    private void RunPreview()
    {
        try
        {
            SetBusy(true);
            var options = ReadOptions();
            _preview = _builder.Preview(options);
            ConfigurePreviewColumns(options);
            PreviewGrid.ItemsSource = _preview.Rows;
            DiagnosticGrid.ItemsSource = _preview.Diagnostics;
            var feedback = PreviewFeedback.From(_preview);
            StatusText.Text = feedback.StatusText;
            EmptyPreviewHint.Text = feedback.EmptyText;
            EmptyPreviewHint.Visibility = _preview.Rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            CreateButton.IsEnabled = ExportButton.IsEnabled = feedback.CanCreate;
            CopyDiagnosticButton.IsEnabled = true;
            PreviewTab.IsSelected = true;
            if (feedback.ShowDiagnosticTab) DiagnosticTab.IsSelected = true;
        }
        catch (Exception ex)
        {
            _preview = null;
            CreateButton.IsEnabled = ExportButton.IsEnabled = CopyDiagnosticButton.IsEnabled = false;
            PreviewGrid.ItemsSource = null;
            EmptyPreviewHint.Visibility = Visibility.Visible;
            EmptyPreviewHint.Text = ex.Message;
            ShowFailure(ex);
        }
        finally { SetBusy(false); }
    }

    private void ConfigurePreviewColumns(ScheduleOptions options)
    {
        PreviewGrid.Columns.Clear();
        void Add(string heading, string path, int width) => PreviewGrid.Columns.Add(new DataGridTextColumn { Header = heading, Binding = new Binding(path), Width = width });
        Add("Category", "Category", 80); Add("MÃ LOẠI CỬA", "TypeMark", 110); Add("FAMILY / TYPE", "FamilyType", 220);
        if (options.Grouping != GroupingMode.TypeDimensions) Add("TẦNG", "Level", 100);
        Add("RỘNG (mm)", "WidthMm", 95); Add("CAO (mm)", "HeightMm", 95); Add("SỐ LƯỢNG", "Count", 90);
        foreach (var column in options.Mappings.SelectMany(m => m.OptionalColumns).DistinctBy(c => c.Role)) Add(column.Heading, $"OptionalValues[{column.Role}]", 140);
        Add("ElementId", "Elements", 150); Add("Cảnh báo", "Warning", 220);
    }

    private void CreateSchedules()
    {
        try
        {
            if (_preview is null || !_preview.IsValid) return;
            SetBusy(true);
            _built = _builder.CreateOrUpdate(ReadOptions(), _preview);
            OpenButton.IsEnabled = _built.ScheduleIds.Count > 0;
            StatusText.Text = string.Join(Environment.NewLine, _built.Messages) + $"\nĐã commit {_built.ScheduleIds.Count} bảng. Mở bảng để kết thúc dialog và chuyển sang schedule.";
        }
        catch (Exception ex) { ShowFailure(ex); }
        finally { SetBusy(false); }
    }

    private void ExportCsv()
    {
        if (_preview is null) return;
        var dialog = new SaveFileDialog { Title = "Xuất CSV cùng dữ liệu xem trước", FileName = "K-TOOL-DoorWindowSchedule.csv", Filter = "CSV UTF-8 (*.csv)|*.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try { CsvExporter.Export(_preview, dialog.FileName); StatusText.Text = $"Đã xuất {dialog.FileName} và CSV diagnostics. UTF-8 BOM; mm; STT chỉ thuộc file xuất."; }
        catch (Exception ex) { ShowFailure(ex); }
    }

    private void CopyDiagnostics()
    {
        if (_preview is null) return;
        try
        {
            var text = new System.Text.StringBuilder("K-TOOL DoorSchedule Revit 2025 v1.0.1\n");
            text.AppendLine("Nguồn: Chỉ model hiện tại");
            text.AppendLine($"Level: {(LevelCombo.SelectedItem as NamedChoice)?.Name}; Phase: {(PhaseCombo.SelectedItem as NamedChoice)?.Name}; Phase Filter: {(PhaseFilterCombo.SelectedItem as NamedChoice)?.Name}");
            text.AppendLine(PreviewFeedback.From(_preview).StatusText);
            foreach (var mapping in _editors.Values.Select(editor => editor.Read()))
            {
                text.AppendLine($"{mapping.Category} WIDTH: {mapping.Width?.DisplayLabel ?? "Chưa map"}");
                text.AppendLine($"{mapping.Category} HEIGHT: {mapping.Height?.DisplayLabel ?? "Chưa map"}");
                text.AppendLine($"{mapping.Category} TYPE MARK: {mapping.TypeMark?.DisplayLabel ?? "Chưa map"}");
            }
            foreach (var diagnostic in _preview.Diagnostics.Take(500))
                text.AppendLine($"{diagnostic.Severity} | {diagnostic.Code} | ElementId={diagnostic.ElementId} | {diagnostic.Message}");
            if (_preview.Diagnostics.Count > 500) text.AppendLine("Chỉ copy 500 thông báo đầu; tab Cảnh báo giữ đầy đủ thông báo.");
            Clipboard.SetText(text.ToString());
            StatusText.Text += "\nĐã copy chẩn đoán; có thể dán nội dung để kiểm tra lỗi.";
        }
        catch (Exception ex) { ShowFailure(ex); }
    }
    private void SetBusy(bool busy) { PreviewButton.IsEnabled = !busy; Cursor = busy ? System.Windows.Input.Cursors.Wait : null; }
    private void ShowFailure(Exception ex)
    {
        var log = LocalLog.Write(ex);
        MessageBox.Show(this, ex.Message + (log is null ? "" : $"\nLog: {log}"), "K-TOOL — Kiểm tra dữ liệu / quyền chỉnh sửa", MessageBoxButton.OK, MessageBoxImage.Warning);
        StatusText.Text = ex.Message;
    }
}

internal sealed class MappingEditor
{
    public StackPanel Panel { get; } = new();
    private readonly CategoryKind _category;
    private readonly CategoryMapping _defaults;
    private readonly Dictionary<string, ComboBox> _required = new();
    private readonly List<(string Role, string Heading, ComboBox Combo)> _optional = new();
    public MappingEditor(CategoryKind category, List<FieldChoice> fields, CategoryMapping defaults, Action changed)
    {
        _category = category; _defaults = defaults;
        Panel.Children.Add(new TextBlock { Text = category == CategoryKind.Doors ? "CỬA ĐI — Doors" : "CỬA SỔ — Windows", FontWeight = FontWeights.SemiBold, FontSize = 17, Margin = new Thickness(0, 12, 0, 6) });
        Panel.Children.Add(new TextBlock { Text = "Identity + field context + Instance/Type được hiển thị cho từng lựa chọn. Chọn đúng kích thước nominal/opening/frame/leaf theo model; không có fallback per-element.", TextWrapping = TextWrapping.Wrap });
        ComboBox Row(string label, IEnumerable<FieldChoice> candidates, FieldChoice? selected, bool optional)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(215) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var list = candidates.ToList();
            var combo = new ComboBox { ItemsSource = list, DisplayMemberPath = "DisplayLabel", MinWidth = 200 };
            Grid.SetColumn(combo, 1); grid.Children.Add(combo);
            if (optional) list.Insert(0, new FieldChoice { Category = category, Name = "— Không thêm —" });
            combo.SelectedItem = selected is null ? (optional ? list.First() : null) : list.FirstOrDefault(f => f.Key == selected.Key);
            void UpdateTooltip() { if (combo.SelectedItem is FieldChoice selectedField) combo.ToolTip = $"{selectedField.DisplayLabel}\nStorageType: {selectedField.StorageType}\nDataType: {selectedField.DataType}\n{selectedField.Provenance}"; }
            var choiceStyle = new Style(typeof(ComboBoxItem));
            choiceStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding("Provenance")));
            combo.ItemContainerStyle = choiceStyle;
            UpdateTooltip();
            combo.SelectionChanged += (_, _) => { UpdateTooltip(); changed(); }; Panel.Children.Add(grid); return combo;
        }
        _required["WIDTH"] = Row("Rộng — Length / Double", fields.Where(f => f.IsLength && f.StorageType == "Double" && (f.Binding == ParameterBinding.Instance || f.Binding == ParameterBinding.Type)), defaults.Width, false);
        _required["HEIGHT"] = Row("Cao — Length / Double", fields.Where(f => f.IsLength && f.StorageType == "Double" && (f.Binding == ParameterBinding.Instance || f.Binding == ParameterBinding.Type)), defaults.Height, false);
        _required["TYPEMARK"] = Row("Mã loại — Type Mark", fields.Where(f => f.Binding == ParameterBinding.Type && f.StorageType == "String"), defaults.TypeMark, false);
        Panel.Children.Add(new TextBlock { Text = $"Identity type: {defaults.FamilyType?.DisplayLabel ?? "Chưa resolve"}\nLevel: {defaults.Level?.DisplayLabel ?? "Không hỗ trợ"}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
        void Optional(string role, string heading, IEnumerable<FieldChoice> candidates) => _optional.Add((role, heading, Row(heading, candidates, null, true)));
        var ordinary = fields.Where(f => f.FieldContext is "Instance" or "ElementType").ToList();
        Optional("MATERIAL", "VẬT LIỆU", ordinary); Optional("FRAME", "VẬT LIỆU KHUNG", ordinary); Optional("PANEL", "VẬT LIỆU CÁNH", ordinary);
        Optional("LEAVES", "SỐ CÁNH", ordinary); Optional("FIRE", "CHỐNG CHÁY", ordinary); Optional("DESCRIPTION", "MÔ TẢ", ordinary); Optional("COMMENTS", "TYPE COMMENTS", ordinary);
        Optional("IMAGE", "ẢNH CÓ SẴN", ordinary.Where(f => f.ParameterId == (long)Autodesk.Revit.DB.BuiltInParameter.ALL_MODEL_IMAGE || f.ParameterId == (long)Autodesk.Revit.DB.BuiltInParameter.ALL_MODEL_TYPE_IMAGE));
        Optional("MARK", "MARK INSTANCE (itemized)", ordinary.Where(f => f.Binding == ParameterBinding.Instance));
        Optional("FROM_NUMBER", "FROM ROOM — SỐ (itemized)", fields.Where(f => f.FieldContext == "FromRoom" && f.ParameterId == (long)Autodesk.Revit.DB.BuiltInParameter.ROOM_NUMBER));
        Optional("FROM_NAME", "FROM ROOM — TÊN (itemized)", fields.Where(f => f.FieldContext == "FromRoom" && f.ParameterId == (long)Autodesk.Revit.DB.BuiltInParameter.ROOM_NAME));
        Optional("TO_NUMBER", "TO ROOM — SỐ (itemized)", fields.Where(f => f.FieldContext == "ToRoom" && f.ParameterId == (long)Autodesk.Revit.DB.BuiltInParameter.ROOM_NUMBER));
        Optional("TO_NAME", "TO ROOM — TÊN (itemized)", fields.Where(f => f.FieldContext == "ToRoom" && f.ParameterId == (long)Autodesk.Revit.DB.BuiltInParameter.ROOM_NAME));
        Panel.Children.Add(new TextBlock { Text = "Ảnh có sẵn: hiển thị ảnh khi schedule đặt trên sheet; editor có thể chỉ hiện tên. Không tự tạo hoặc sửa family.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 6, 0, 12) });
    }
    public CategoryMapping Read() => new()
    {
        Category = _category, Width = _required["WIDTH"].SelectedItem as FieldChoice, Height = _required["HEIGHT"].SelectedItem as FieldChoice,
        TypeMark = _required["TYPEMARK"].SelectedItem as FieldChoice, FamilyType = _defaults.FamilyType, Level = _defaults.Level,
        OptionalColumns = _optional.Where(p => p.Combo.SelectedItem is FieldChoice { Identity.Length: > 0 }).Select(p => new MappedColumn { Role = p.Role, Heading = p.Heading, Field = (FieldChoice)p.Combo.SelectedItem }).ToList()
    };
}

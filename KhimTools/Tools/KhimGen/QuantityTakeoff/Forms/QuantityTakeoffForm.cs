using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.QuantityTakeoff.Models;
using KhimTools.QuantityTakeoff.Services;
using KhimTools.SheetExport.Services;
using Button = System.Windows.Forms.Button;
using Color = System.Drawing.Color;
using ComboBox = System.Windows.Forms.ComboBox;
using Form = System.Windows.Forms.Form;
using TextBox = System.Windows.Forms.TextBox;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace KhimTools.QuantityTakeoff.Forms
{
    public sealed class QuantityTakeoffForm : Form
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly DataGridView _revitGrid = Grid(true);
        private readonly DataGridView _cubicostGrid = Grid(false);
        private readonly DataGridView _compareGrid = Grid(true);
        private readonly DataGridView _qaGrid = Grid(true);
        private readonly DataGridView _rulesGrid = Grid(false);
        private readonly RichTextBox _trace = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(248, 250, 252), Font = new Font("Consolas", 9F) };
        private readonly ComboBox _codeFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _search = new TextBox { Dock = DockStyle.Fill };
        private readonly Label _summary = new Label { AutoSize = true, Left = 16, Top = 35, ForeColor = Color.FromArgb(100, 116, 139) };
        private readonly Label _status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(71, 85, 105) };
        private readonly Label _cubicostInfo = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(71, 85, 105) };
        private QtoResult _revit;
        private CubicostImportResult _cubicost;
        private QtoRuleProfile _profile;
        private List<QtoLine> _visibleRevit = new List<QtoLine>();
        private List<QtoSourceComparison> _comparison = new List<QtoSourceComparison>();
        private bool _loaded;

        public QuantityTakeoffForm(UIDocument uidoc, bool openDataCheck = false)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
            _doc = uidoc.Document;
            Text = "K-QS — Revit / Cubicost Comparison";
            Width = 1480; Height = 850; MinimumSize = new Size(1120, 680);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(248, 250, 252);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var header = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            header.Controls.Add(new Label { Text = "K-QS MEASUREMENT WORKBENCH", Font = new Font("Segoe UI", 15F, FontStyle.Bold), AutoSize = true, Left = 14, Top = 8, ForeColor = Color.FromArgb(15, 23, 42) });
            _summary.Text = "Đang chuẩn bị dữ liệu..."; header.Controls.Add(_summary); root.Controls.Add(header, 0, 0);

            var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 6, 0, 4) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.Controls.Add(new Label { Text = "Nhóm QTO", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
            _codeFilter.SelectedIndexChanged += (s, e) => FilterRevit(); toolbar.Controls.Add(_codeFilter, 1, 0);
            toolbar.Controls.Add(new Label { Text = "Tìm kiếm", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 0);
            _search.TextChanged += (s, e) => FilterRevit(); toolbar.Controls.Add(_search, 3, 0); root.Controls.Add(toolbar, 0, 1);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var revitTab = new TabPage("QS Revit") { Padding = new Padding(6), BackColor = Color.White };
            BuildRevitColumns();
            _revitGrid.CellDoubleClick += (s, e) => SelectRevit(e.RowIndex);
            _revitGrid.SelectionChanged += (s, e) => ShowTrace();
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 1080, FixedPanel = FixedPanel.Panel2 };
            split.Panel1.Controls.Add(_revitGrid); split.Panel2.Padding = new Padding(10); split.Panel2.Controls.Add(_trace); revitTab.Controls.Add(split);

            var cubicostTab = new TabPage("QS Cubicost") { Padding = new Padding(6), BackColor = Color.White };
            BuildCubicostColumns();
            var cubicostLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            cubicostLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); cubicostLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var importBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            importBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); importBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); importBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            importBar.Controls.Add(ActionButton("Nhập Cubicost Excel", (s, e) => ImportCubicost()), 0, 0);
            importBar.Controls.Add(ActionButton("Tạo từ QS Revit", (s, e) => CreateCubicost()), 1, 0);
            _cubicostInfo.Text = "Chưa có dữ liệu. Có thể tạo baseline Cubicost ngay từ QS Revit rồi chỉnh sửa."; importBar.Controls.Add(_cubicostInfo, 2, 0);
            cubicostLayout.Controls.Add(importBar, 0, 0); cubicostLayout.Controls.Add(_cubicostGrid, 0, 1); cubicostTab.Controls.Add(cubicostLayout);

            var compareTab = new TabPage("Compare") { Padding = new Padding(6), BackColor = Color.White };
            BuildCompareColumns(); compareTab.Controls.Add(_compareGrid);
            var qaTab = new TabPage("Data Check") { Padding = new Padding(6), BackColor = Color.White };
            BuildQaColumns(); _qaGrid.CellDoubleClick += (s, e) => SelectFinding(e.RowIndex); qaTab.Controls.Add(_qaGrid);
            var rulesTab = new TabPage("Rules Revit") { Padding = new Padding(6), BackColor = Color.White };
            BuildRuleColumns();
            var rulesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            rulesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); rulesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rulesLayout.Controls.Add(_rulesGrid, 0, 0);
            var ruleBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            ruleBar.Controls.Add(ActionButton("Lưu Rules", (s, e) => SaveRules())); ruleBar.Controls.Add(ActionButton("Apply", (s, e) => ApplyRules()));
            rulesLayout.Controls.Add(ruleBar, 0, 1); rulesTab.Controls.Add(rulesLayout);
            tabs.TabPages.Add(revitTab); tabs.TabPages.Add(cubicostTab); tabs.TabPages.Add(compareTab); tabs.TabPages.Add(qaTab); tabs.TabPages.Add(rulesTab);
            if (openDataCheck) tabs.SelectedTab = qaTab; root.Controls.Add(tabs, 0, 2);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int width in new[] { 130, 130, 110, 90 }) footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
            _status.Text = "Sẵn sàng"; footer.Controls.Add(_status, 0, 0);
            footer.Controls.Add(ActionButton("Chọn trong Revit", (s, e) => SelectRevit(_revitGrid.CurrentCell?.RowIndex ?? -1)), 1, 0);
            footer.Controls.Add(ActionButton("Xuất Comparison", (s, e) => Export()), 2, 0);
            footer.Controls.Add(ActionButton("Tính lại", (s, e) => LoadRevit()), 3, 0);
            footer.Controls.Add(ActionButton("Đóng", (s, e) => Close()), 4, 0); root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
            Shown += (s, e) => { if (_loaded) return; _loaded = true; BeginInvoke(new Action(LoadRevit)); };
        }

        private void LoadRevit()
        {
            UseWaitCursor = true; _status.Text = "Đang bóc khối lượng Revit..."; Refresh();
            try
            {
                _profile = QtoRuleProfileService.Load(_doc.Title);
                _revit = QtoCollectorService.Collect(_doc); QtoRuleEngine.Apply(_revit, _profile);
                PopulateRules(); PopulateFilter(); PopulateQa(); FilterRevit(); RefreshComparison(); UpdateSummary();
                _status.Text = $"QS Revit tính lúc {_revit.CalculatedAt:HH:mm:ss}.";
            }
            catch (Exception ex) { _status.Text = "Không thể bóc khối lượng Revit."; TaskDialog.Show("K-QS", ex.Message); }
            finally { UseWaitCursor = false; }
        }

        private void ImportCubicost()
        {
            using var dialog = new OpenFileDialog { Filter = "Cubicost export (*.xlsx;*.csv)|*.xlsx;*.csv", Title = "Nhập bảng khối lượng Cubicost" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                UseWaitCursor = true; _cubicost = CubicostImportService.Import(dialog.FileName); PopulateCubicost(); RefreshComparison(); UpdateSummary();
                _status.Text = $"Đã nhập {_cubicost.Lines.Count:N0} dòng Cubicost.";
            }
            catch (Exception ex) { TaskDialog.Show("K-QS — Cubicost Import", ex.Message); }
            finally { UseWaitCursor = false; }
        }

        private void CreateCubicost()
        {
            if (_revit == null) { LoadRevit(); if (_revit == null) return; }
            _cubicost = QtoSourceComparisonService.CreateCubicostBaseline(_revit);
            PopulateCubicost(); RefreshComparison(); UpdateSummary();
            _status.Text = "Đã tạo bộ QS Cubicost nội bộ từ kết quả Revit. Có thể chỉnh Map Revit Code và Quantity.";
        }

        private void PopulateCubicost()
        {
            _cubicostGrid.Rows.Clear();
            foreach (CubicostQtoLine line in _cubicost.Lines)
                _cubicostGrid.Rows.Add(line.SourceRow, line.CubicostCode, line.RevitCode, line.Description, line.Location, line.Unit, line.Quantity.ToString("N3"));
            _cubicostInfo.Text = $"{Path.GetFileName(_cubicost.FilePath)} • {_cubicost.SheetName} • {_cubicost.Lines.Count:N0} dòng" +
                                  (_cubicost.Warnings.Count > 0 ? " • " + string.Join("; ", _cubicost.Warnings) : "");
        }

        private void PullCubicostMappings()
        {
            if (_cubicost == null) return; _cubicostGrid.EndEdit();
            for (int i = 0; i < _cubicost.Lines.Count && i < _cubicostGrid.Rows.Count; i++)
                _cubicost.Lines[i].RevitCode = Convert.ToString(_cubicostGrid.Rows[i].Cells[2].Value)?.Trim() ?? "";
        }

        private void RefreshComparison()
        {
            PullCubicostMappings(); _comparison = QtoSourceComparisonService.Compare(_revit, _cubicost); _compareGrid.Rows.Clear();
            foreach (QtoSourceComparison line in _comparison)
                _compareGrid.Rows.Add(line.Status, line.RevitCode, line.Unit, line.RevitQuantity.ToString("N3"), line.CubicostQuantity.ToString("N3"),
                    line.Difference.ToString("+N3;-N3;0.000"), line.DifferencePercent?.ToString("+N2;-N2;0.00") + "%");
        }

        private void PopulateFilter()
        {
            string old = _codeFilter.SelectedItem?.ToString(); _codeFilter.Items.Clear(); _codeFilter.Items.Add("(Tất cả nhóm)");
            foreach (string code in _revit.Lines.Where(x => x.IsIncluded).Select(x => x.Code).Distinct().OrderBy(x => x)) _codeFilter.Items.Add(code);
            int index = old == null ? 0 : _codeFilter.Items.IndexOf(old); _codeFilter.SelectedIndex = index >= 0 ? index : 0;
        }

        private void FilterRevit()
        {
            if (_revit == null) return;
            string code = _codeFilter.SelectedItem?.ToString() ?? "(Tất cả nhóm)"; string query = (_search.Text ?? "").Trim();
            IEnumerable<QtoLine> rows = _revit.Lines.Where(x => x.IsIncluded);
            if (!code.StartsWith("(")) rows = rows.Where(x => x.Code == code);
            if (query.Length > 0) rows = rows.Where(x => Contains(x.Code, query) || Contains(x.Description, query) || Contains(x.Material, query) || Contains(x.TypeName, query) || Contains(x.Level, query));
                _visibleRevit = rows.ToList(); _revitGrid.Rows.Clear();
            foreach (QtoLine line in _visibleRevit)
                _revitGrid.Rows.Add(line.Code, line.Description, line.Category, line.Material, line.FamilyName, line.TypeName, line.Level, line.Unit,
                    line.RawQuantity.ToString("N3"), line.WastePercent.ToString("N1"), line.PayQuantity.ToString("N" + line.RoundingDigits), line.ElementCount, line.Confidence);
            ShowTrace();
        }

        private void ShowTrace()
        {
            int row = _revitGrid.CurrentCell?.RowIndex ?? -1;
            if (row < 0 || row >= _visibleRevit.Count) { _trace.Text = "Chọn một dòng QS Revit để xem công thức."; return; }
            QtoLine line = _visibleRevit[row];
            _trace.Text = $"FORMULA TRACE\n\nRule: {line.RuleId}\nCode: {line.Code}\nUnit: {line.Unit}\nSource: {line.Source}\nConfidence: {line.Confidence}\n\n{line.FormulaTrace}\n\nELEMENT TRACE ({line.ElementCount:N0})\n\n" + string.Join("\n", line.ElementUniqueIds.Take(20));
        }

        private void PopulateQa() { _qaGrid.Rows.Clear(); foreach (QtoFinding x in _revit.Findings) _qaGrid.Rows.Add(x.Severity, x.Check, x.Message, x.Count); }
        private void PopulateRules() { _rulesGrid.Rows.Clear(); foreach (QtoMeasurementRule x in _profile.Rules.OrderBy(x => x.Code)) _rulesGrid.Rows.Add(x.Enabled, x.Code, x.Description, x.WastePercent, x.RoundingDigits); }

        private void ApplyRules()
        {
            if (_profile == null || _revit == null) return; _rulesGrid.EndEdit();
            foreach (DataGridViewRow row in _rulesGrid.Rows)
            {
                QtoMeasurementRule rule = _profile.Rules.FirstOrDefault(x => x.Code == Convert.ToString(row.Cells[1].Value)); if (rule == null) continue;
                rule.Enabled = Convert.ToBoolean(row.Cells[0].Value ?? false);
                rule.WastePercent = Number(row.Cells[3].Value, 0); rule.RoundingDigits = Math.Max(0, Math.Min(6, Integer(row.Cells[4].Value, 3)));
            }
            QtoRuleEngine.Apply(_revit, _profile); PopulateFilter(); FilterRevit(); RefreshComparison(); UpdateSummary(); _status.Text = "Đã áp dụng Rules cho QS Revit và tính lại Compare.";
        }

        private void SaveRules()
        {
            try { ApplyRules(); QtoRuleProfileService.Save(_doc.Title, _profile); PopulateRules(); UpdateSummary(); _status.Text = $"Đã lưu Rules Revit v{_profile.Version}."; }
            catch (Exception ex) { TaskDialog.Show("K-QS — Rules", ex.Message); }
        }

        private void SelectRevit(int row)
        {
            if (row < 0 || row >= _visibleRevit.Count) return; IList<ElementId> ids = _visibleRevit[row].ElementIds;
            if (ids.Count == 0) return; _uidoc.Selection.SetElementIds(ids); _uidoc.ShowElements(ids); _status.Text = $"Đã chọn {ids.Count:N0} phần tử Revit.";
        }

        private void SelectFinding(int row)
        {
            if (_revit == null || row < 0 || row >= _revit.Findings.Count) return; IList<ElementId> ids = _revit.Findings[row].ElementIds;
            if (ids.Count == 0) return; _uidoc.Selection.SetElementIds(ids); _uidoc.ShowElements(ids);
        }

        private void UpdateSummary()
        {
            int revitLines = _revit?.Lines.Count(x => x.IsIncluded) ?? 0, cubicostLines = _cubicost?.Lines.Count ?? 0;
            _summary.Text = $"Phạm vi: toàn bộ tài liệu chủ (không gồm links) • QS Revit: {revitLines:N0} dòng • QS Cubicost: {cubicostLines:N0} dòng • Compare: {_comparison.Count:N0} nhóm • Rules v{_profile?.Version ?? 0}";
        }

        private void Export()
        {
            if (_revit == null) return; RefreshComparison();
            using var dialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true, FileName = $"K-QS_Compare_{Safe(_doc.Title)}_{DateTime.Now:yyyyMMdd-HHmm}.xlsx" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string fullPath = Path.GetFullPath(dialog.FileName);
                string directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    throw new IOException("The selected export folder does not exist.");
                string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                using var writer = new SimpleExcelWriter("K-QS Compare");
                writer.AddRow("K-QS SOURCE COMPARISON", _doc.Title, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.AddRow("COMPARE", "Revit Code", "Unit", "QS Revit", "QS Cubicost", "Difference (Revit-Cubicost)", "Difference %");
                foreach (QtoSourceComparison x in _comparison) writer.AddRow(x.Status, x.RevitCode, x.Unit, N(x.RevitQuantity), N(x.CubicostQuantity), N(x.Difference), x.DifferencePercent.HasValue ? N(x.DifferencePercent.Value, "0.###") : "");
                writer.AddEmptyRow(); writer.AddRow("QS REVIT", "Description", "Material", "Family", "Type", "Level", "Unit", "Raw", "Waste %", "Pay", "Source", "Formula", "UniqueIds");
                foreach (QtoLine x in _revit.Lines.Where(x => x.IsIncluded)) writer.AddRow(x.Code, x.Description, x.Material, x.FamilyName, x.TypeName, x.Level, x.Unit, N(x.RawQuantity), N(x.WastePercent, "0.###"), N(x.PayQuantity), x.Source, x.FormulaTrace, string.Join(";", x.ElementUniqueIds));
                writer.AddEmptyRow(); writer.AddRow("QS CUBICOST", "Cubicost Code", "Map Revit Code", "Description", "Location", "Unit", "Quantity", "Source Row");
                if (_cubicost != null) foreach (CubicostQtoLine x in _cubicost.Lines) writer.AddRow("Cubicost", x.CubicostCode, x.RevitCode, x.Description, x.Location, x.Unit, N(x.Quantity), x.SourceRow.ToString(CultureInfo.InvariantCulture));
                writer.Save(temporaryPath);
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null);
                else File.Move(temporaryPath, fullPath);
                _status.Text = "Đã xuất: " + fullPath;
                }
                finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            }
            catch (Exception ex) { TaskDialog.Show("K-QS — Export", ex.Message); }
        }

        private void BuildRevitColumns() { Add(_revitGrid, "Code", "Mô tả", "Category", "Material", "Family", "Type", "Level", "Đơn vị", "Raw", "Hao hụt %", "Pay", "Elements", "Confidence"); }
        private void BuildCubicostColumns()
        {
            Add(_cubicostGrid, "Source Row", "Cubicost Code", "Map Revit Code", "Description", "Location", "Unit", "Quantity");
            for (int i = 0; i < _cubicostGrid.Columns.Count; i++) _cubicostGrid.Columns[i].ReadOnly = i != 2;
            _cubicostGrid.CellEndEdit += (s, e) => { if (e.ColumnIndex == 2) { PullCubicostMappings(); RefreshComparison(); UpdateSummary(); } };
            _cubicostGrid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }
        private void BuildCompareColumns() { Add(_compareGrid, "Status", "Revit Code", "Unit", "QS Revit", "QS Cubicost", "Difference", "%"); _compareGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
        private void BuildQaColumns() { Add(_qaGrid, "Severity", "Check", "Message", "Elements"); _qaGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
        private void BuildRuleColumns()
        {
            _rulesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Dùng" }); Add(_rulesGrid, "Code", "Mô tả", "Hao hụt %", "Làm tròn");
            _rulesGrid.Columns[1].ReadOnly = true; _rulesGrid.Columns[2].ReadOnly = true; _rulesGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private static DataGridView Grid(bool readOnly) => new DataGridView { Dock = DockStyle.Fill, ReadOnly = readOnly, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
        private static void Add(DataGridView grid, params string[] names) { foreach (string name in names) grid.Columns.Add(name, name); }
        private static Button ActionButton(string text, EventHandler click) { var button = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(4, 5, 4, 5), FlatStyle = FlatStyle.Flat, BackColor = Color.White }; button.Click += click; return button; }
        private static bool Contains(string value, string query) => value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        private static double Number(object value, double fallback) => double.TryParse(Convert.ToString(value), out double x) ? x : fallback;
        private static int Integer(object value, int fallback) => int.TryParse(Convert.ToString(value), out int x) ? x : fallback;
        private static string N(double value, string format = "0.########") => value.ToString(format, CultureInfo.InvariantCulture);
        private static string Safe(string value) { foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return value; }
    }
}

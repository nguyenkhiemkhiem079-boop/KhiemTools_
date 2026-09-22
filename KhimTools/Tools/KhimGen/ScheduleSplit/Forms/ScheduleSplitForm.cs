using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core.UI;
using KhimTools.Core;
using KhimTools.ScheduleSplit.Models;
using KhimTools.ScheduleSplit.Services;

namespace KhimTools.ScheduleSplit.Forms
{
    public sealed class ScheduleSplitForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly List<ScheduleSourceInfo> _sources;
        private readonly ComboBox _source = new ComboBox();
        private readonly CheckedListBox _targets = new CheckedListBox();
        private readonly Label _details = new Label();
        private readonly ComboBox _mode = new ComboBox();
        private readonly ComboBox _distribution = new ComboBox();
        private readonly NumericUpDown _count = new NumericUpDown();
        private readonly NumericUpDown _maxHeight = new NumericUpDown();
        private readonly CheckBox _workingCopy = new CheckBox();
        private readonly CheckBox _allowCollision = new CheckBox();
        private readonly CheckBox _relayout = new CheckBox();
        private readonly DataGridView _preview = new DataGridView();
        private readonly Label _status = new Label();
        public ScheduleSplitRequest Request { get; private set; }

        public ScheduleSplitForm(Document doc) : this(doc, true) { }
        private ScheduleSplitForm(Document doc, bool load)
        {
            _doc = doc;
            _sources = load ? ScheduleSplitCollector.Collect(doc) : new List<ScheduleSourceInfo>();
            KhimUiStyle.ApplyFormTheme(this);
            BuildLayout();
            if (load) LoadData();
        }
        internal static ScheduleSplitForm CreateLayoutPreview() { return new ScheduleSplitForm(null, false); }

        private void BuildLayout()
        {
            Text = "K-TOOLS - Split Schedule 2.0"; Width = 1120; Height = 680; MinimumSize = new Size(860, 540); StartPosition = FormStartPosition.CenterScreen;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 }; left.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); left.RowStyles.Add(new RowStyle(SizeType.Absolute, 112)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.Controls.Add(new Label { Text = "Source Schedule placement", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _source.Dock = DockStyle.Fill; _source.DropDownStyle = ComboBoxStyle.DropDownList; _source.SelectedIndexChanged += (s, e) => UpdateDetails(); left.Controls.Add(_source, 0, 1);
            _details.Dock = DockStyle.Fill; _details.AutoSize = false; _details.Text = "No source selected."; left.Controls.Add(_details, 0, 2);
            _targets.Dock = DockStyle.Fill; _targets.CheckOnClick = true; left.Controls.Add(_targets, 0, 3); root.Controls.Add(left, 0, 0);
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 }; main.RowStyles.Add(new RowStyle(SizeType.Absolute, 116)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(4) };
            options.Controls.Add(new Label { Text = "Mode", AutoSize = true, Margin = new Padding(4, 8, 4, 4) }); _mode.DropDownStyle = ComboBoxStyle.DropDownList; _mode.Width = 180; _mode.Items.AddRange(new object[] { "By segment count", "By max height (mm)" }); _mode.SelectedIndex = 0; _mode.SelectedIndexChanged += (s, e) => { _count.Enabled = _mode.SelectedIndex == 0; _maxHeight.Enabled = _mode.SelectedIndex == 1; }; options.Controls.Add(_mode);
            options.Controls.Add(new Label { Text = "Count", AutoSize = true, Margin = new Padding(8, 8, 4, 4) }); _count.Minimum = 2; _count.Maximum = 99; _count.Value = 2; _count.Width = 65; options.Controls.Add(_count);
            options.Controls.Add(new Label { Text = "Max height", AutoSize = true, Margin = new Padding(8, 8, 4, 4) }); _maxHeight.Minimum = 10; _maxHeight.Maximum = 5000; _maxHeight.Value = 180; _maxHeight.Width = 80; _maxHeight.Enabled = false; options.Controls.Add(_maxHeight);
            options.Controls.Add(new Label { Text = "Distribution", AutoSize = true, Margin = new Padding(8, 8, 4, 4) }); _distribution.DropDownStyle = ComboBoxStyle.DropDownList; _distribution.Width = 190; _distribution.Items.AddRange(new object[] { "One segment per Sheet", "All segments on same Sheet", "Multiple segments per Sheet" }); _distribution.SelectedIndex = 0; options.Controls.Add(_distribution);
            _workingCopy.Text = "WORKING_COPY (preserve source)"; _workingCopy.Checked = true; _workingCopy.AutoSize = true; options.Controls.Add(_workingCopy);
            _allowCollision.Text = "Allow collision with warning"; _allowCollision.AutoSize = true; options.Controls.Add(_allowCollision);
            _relayout.Text = "Relayout existing split"; _relayout.AutoSize = true; options.Controls.Add(_relayout);
            main.Controls.Add(options, 0, 0); ConfigurePreview(); main.Controls.Add(_preview, 0, 1); root.Controls.Add(main, 1, 0);
            _status.Text = "Select one explicit ScheduleSheetInstance and one or more target Sheets. Preview runs before any transaction."; _status.Dock = DockStyle.Fill; root.Controls.Add(_status, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var apply = new Button { Text = "Split & Place", Width = 120 }; apply.Click += ApplyClicked; buttons.Controls.Add(apply); var preview = new Button { Text = "Refresh Plan", Width = 105 }; preview.Click += PreviewClicked; buttons.Controls.Add(preview); var cancel = new Button { Text = "Close", Width = 90, DialogResult = DialogResult.Cancel }; buttons.Controls.Add(cancel); root.Controls.Add(buttons, 1, 1); Controls.Add(root); CancelButton = cancel;
        }
        private void ConfigurePreview()
        {
            _preview.Dock = DockStyle.Fill; _preview.AllowUserToAddRows = false; _preview.ReadOnly = true; _preview.AutoGenerateColumns = false; _preview.RowHeadersVisible = false;
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Segment", Width = 70 }); _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Height (mm)", Width = 90 }); _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target Sheet", Width = 150 }); _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Position", Width = 180 }); _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 150 });
        }
        private void LoadData()
        {
            _source.Items.Clear(); _targets.Items.Clear();
            foreach (ScheduleSourceInfo info in _sources) _source.Items.Add(new SourceRow(info));
            _source.SelectedIndex = -1;
            foreach (ViewSheet sheet in new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase)) _targets.Items.Add(new TargetRow(sheet), false);
        }
        private void UpdateDetails()
        {
            SourceRow row = _source.SelectedItem as SourceRow;
            if (row == null) { _details.Text = "No source selected."; return; }
            ScheduleSourceInfo info = row.Info;
            string bounds = info.Bounds == null ? "unknown" : "available";
            _details.Text = "Segments: " + info.SegmentCount + " (" + (info.IsSplit ? "split" : "unsplit") + ")\r\n" +
                "Position: " + (info.Position == null ? "unknown" : info.Position.ToString()) + "  Bounds: " + bounds + "\r\n" +
                "Placements: " + info.PlacementInstanceIds.Count + "  Type: " + (info.IsRevisionSchedule ? "revision" : info.IsKeySchedule ? "key" : info.IsInternalSchedule ? "internal" : "standard");
        }
        private void ApplyClicked(object sender, EventArgs e)
        {
            ScheduleSplitPlan plan; List<ScheduleSplitPreflightResult> diagnostics; ScheduleSplitRequest request;
            if (!TryBuildPlan(out request, out plan, out diagnostics)) return;
            if (diagnostics.Any(d => d.Severity == ScheduleSplitSeverity.ERROR)) { _status.Text = string.Join(" | ", diagnostics.Where(d => d.Severity == ScheduleSplitSeverity.ERROR).Select(d => d.Status + ": " + d.Message).Distinct()); return; }
            Request = request; DialogResult = DialogResult.OK; Close();
        }
        private void PreviewClicked(object sender, EventArgs e)
        {
            ScheduleSplitRequest request; ScheduleSplitPlan plan; List<ScheduleSplitPreflightResult> diagnostics;
            if (TryBuildPlan(out request, out plan, out diagnostics)) _status.Text = diagnostics.Count == 0 ? "Plan ready." : string.Join(" | ", diagnostics.Select(d => d.Status + ": " + d.Message).Distinct());
        }
        private bool TryBuildPlan(out ScheduleSplitRequest request, out ScheduleSplitPlan plan, out List<ScheduleSplitPreflightResult> diagnostics)
        {
            request = null; plan = null; diagnostics = new List<ScheduleSplitPreflightResult>();
            if (!(_source.SelectedItem is SourceRow selected)) { MessageBox.Show(this, "Select an explicit ScheduleSheetInstance.", "Split Schedule", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            var options = new ScheduleSplitOptions { Mode = _mode.SelectedIndex == 1 ? ScheduleSplitMode.BY_MAX_HEIGHT : ScheduleSplitMode.BY_SEGMENT_COUNT, SegmentCount = (int)_count.Value, MaxHeightMillimeters = (double)_maxHeight.Value, Distribution = (ScheduleSplitDistribution)_distribution.SelectedIndex, SourceMode = _workingCopy.Checked ? ScheduleSplitSourceMode.WORKING_COPY : ScheduleSplitSourceMode.MODIFY_SOURCE_SCHEDULE, AllowWithWarningOnCollision = _allowCollision.Checked, ReLayoutExistingSegments = _relayout.Checked };
            request = new ScheduleSplitRequest { Document = _doc, SourceScheduleId = selected.Info.ScheduleId, SourceInstanceId = selected.Info.InstanceId, SourceSheetId = selected.Info.SourceSheetId, Options = options };
            foreach (TargetRow row in _targets.CheckedItems.Cast<TargetRow>()) request.TargetSheets.Add(new ScheduleTargetSheet { SheetId = row.Sheet.Id, SheetNumber = row.Sheet.SheetNumber, SheetName = row.Sheet.Name, Order = request.TargetSheets.Count, IsSelected = true });
            plan = ScheduleSplitPlanner.BuildPlan(_doc, request); diagnostics = ScheduleSplitPreflightService.Validate(_doc, plan, options); LoadPreview(plan, diagnostics); return true;
        }
        private void LoadPreview(ScheduleSplitPlan plan, IList<ScheduleSplitPreflightResult> diagnostics)
        {
            _preview.Rows.Clear(); if (plan == null) return;
            foreach (ScheduleSegmentPlan segment in plan.Segments) { ScheduleSplitPreflightResult d = null; foreach (ScheduleSplitPreflightResult candidate in diagnostics) if (candidate.SegmentIndex == segment.SegmentIndex) { d = candidate; break; } _preview.Rows.Add(segment.SegmentIndex + 1, Math.Round(UnitUtils.ConvertFromInternalUnits(segment.HeightInternal, UnitTypeId.Millimeters), 1), segment.TargetSheetNumber, segment.PlannedPosition == null ? "" : segment.PlannedPosition.ToString(), d == null ? segment.Status.ToString() : d.Status.ToString()); }
        }
        private sealed class SourceRow { public ScheduleSourceInfo Info { get; } public SourceRow(ScheduleSourceInfo info) { Info = info; } public override string ToString() { return Info.SourceSheetNumber + " - " + Info.ScheduleName + " [" + Info.InstanceId.IntegerValue + "]"; } }
        private sealed class TargetRow { public ViewSheet Sheet { get; } public TargetRow(ViewSheet sheet) { Sheet = sheet; } public override string ToString() { return Sheet.SheetNumber + " - " + Sheet.Name; } }
    }
}

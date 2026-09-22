using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;
using KhimTools.DimensionTools.Services;

namespace KhimTools.DimensionTools.Forms
{
    public sealed class DimensionToolsForm : KTBaseForm
    {
        private readonly Document _doc; private readonly View _view; private readonly IList<ElementId> _selection; private readonly ComboBox _operation = new ComboBox(); private readonly ComboBox _axis = new ComboBox(); private readonly ComboBox _strategy = new ComboBox(); private readonly NumericUpDown _offset = new NumericUpDown(); private readonly Label _status = new Label(); private Button _apply; public DimensionPlan Plan { get; private set; }
        public DimensionToolsForm(Document doc, View view, IList<ElementId> selection) { _doc = doc; _view = view; _selection = selection ?? new List<ElementId>(); KhimUiStyle.ApplyFormTheme(this); BuildLayout(); }
        private DimensionToolsForm(bool preview) { _doc = null; _view = null; _selection = new List<ElementId>(); KhimUiStyle.ApplyFormTheme(this); BuildLayout(); }
        internal static DimensionToolsForm CreateLayoutPreview() { return new DimensionToolsForm(true); }
        private void BuildLayout()
        {
            Text = "K-TOOLS - Dimension Tools 2.0"; Width = 1160; Height = 720; MinimumSize = new Size(900, 580); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9F);
            var tabs = new TabControl { Dock = DockStyle.Fill }; var auto = new TabPage("AUTO DIM"); var edit = new TabPage("EDIT DIM"); tabs.TabPages.Add(auto); tabs.TabPages.Add(edit);
            _operation.DropDownStyle = ComboBoxStyle.DropDownList; _operation.Items.AddRange(Enum.GetNames(typeof(DimensionOperation))); _operation.SelectedIndex = 0; _axis.DropDownStyle = ComboBoxStyle.DropDownList; _axis.Items.AddRange(Enum.GetNames(typeof(DimensionAxis))); _axis.SelectedItem = DimensionAxis.AUTO; _strategy.DropDownStyle = ComboBoxStyle.DropDownList; _strategy.Items.AddRange(new object[] { "EXPLICIT", "CENTER", "NEAR_FACE", "FAR_FACE" }); _strategy.SelectedItem = "EXPLICIT"; _offset.Minimum = -1000; _offset.Maximum = 1000; _offset.DecimalPlaces = 1; _offset.Value = 100;
            foreach (TabPage page in tabs.TabPages) { var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 }; grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); grid.Controls.Add(new Label { Text = "Operation", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); grid.Controls.Add(_operation, 1, 0); grid.Controls.Add(new Label { Text = "Axis", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); grid.Controls.Add(_axis, 1, 1); grid.Controls.Add(new Label { Text = "Offset (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); grid.Controls.Add(_offset, 1, 2); grid.Controls.Add(new Label { Text = "Reference strategy", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3); grid.Controls.Add(_strategy, 1, 3); page.Controls.Add(grid); }
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), RowCount = 2 }; root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); root.Controls.Add(tabs, 0, 0); var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; _apply = new Button { Text = "Apply", Width = 100, Enabled = false }; _apply.Click += ApplyClicked; var preview = new Button { Text = "Preview", Width = 100 }; preview.Click += PreviewClicked; var refresh = new Button { Text = "Refresh", Width = 100 }; refresh.Click += (s, e) => { Plan = null; _status.Text = "Refresh re-reads current selection and View."; }; var close = new Button { Text = "Close", Width = 100, DialogResult = DialogResult.Cancel }; buttons.Controls.AddRange(new Control[] { _apply, preview, refresh, close }); _status.Text = "Preview does not write model state."; _status.AutoSize = true; buttons.Controls.Add(_status); root.Controls.Add(buttons, 0, 1); Controls.Add(root); CancelButton = close;
        }
        private void PreviewClicked(object sender, EventArgs e) { if (_doc == null) { _status.Text = "Layout preview only; no model writes occurred."; return; } DimensionOperation operation; if (!Enum.TryParse(_operation.SelectedItem == null ? string.Empty : _operation.SelectedItem.ToString(), out operation)) return; var options = new DimensionOptions { Axis = (DimensionAxis)_axis.SelectedItem, OffsetMillimeters = (double)_offset.Value, ReferenceStrategy = Convert.ToString(_strategy.SelectedItem) }; Plan = BuildPlan(operation, options); _apply.Enabled = Plan != null && Plan.CanExecute; _status.Text = "Preview: " + (Plan == null ? "no plan" : Plan.Status + " / " + Plan.References.Count + " reference(s)") + ". No model writes occurred."; }
        private DimensionPlan BuildPlan(DimensionOperation operation, DimensionOptions options)
        {
            switch (operation)
            {
                case DimensionOperation.GRID: return GridDimensionService.BuildPlan(_doc, _view, _selection, options);
                case DimensionOperation.COLUMN: return ColumnDimensionService.BuildPlan(_doc, _view, _selection, "FACE_TO_FACE", options);
                case DimensionOperation.BEAM: return BeamDimensionService.BuildPlan(_doc, _view, _selection, "END_TO_END", options);
                case DimensionOperation.FOUNDATION: return FoundationDimensionService.BuildPlan(_doc, _view, _selection, "FACE_CHAIN", options);
                case DimensionOperation.OPENING: return OpeningDimensionService.BuildPlan(_doc, _view, _selection, options);
                case DimensionOperation.ELEVATION: return ElevationDimensionService.BuildLevelPlan(_doc, _view, _selection, options);
                case DimensionOperation.QUICK: return QuickDimensionService.BuildPlan(_doc, _view, _selection, options.ReferenceStrategy, options);
                case DimensionOperation.GENERAL: return GeneralDimensionService.BuildPlan(_doc, _view, new List<DimensionReferenceInfo>(), null, options);
                default: return new DimensionPlan { Context = new DimensionContext { Document = _doc, View = _view, Operation = operation }, ViewId = _view.Id, Operation = operation, Status = DimensionStatus.INVALID_SELECTION };
            }
        }
        private void ApplyClicked(object sender, EventArgs e) { if (Plan == null || !Plan.CanExecute) { _status.Text = "Preflight blocked this operation."; return; } DialogResult = DialogResult.OK; Close(); }
    }
}

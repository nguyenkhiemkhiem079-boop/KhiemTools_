using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.General;
using KhimTools.ModifyObjects.Structural;
using KhimTools.ModifyObjects.Wall;

namespace KhimTools.ModifyObjects.Forms
{
    public sealed class ModifyObjectsForm : KTBaseForm
    {
        private readonly Document _doc; private readonly IList<ElementId> _selection; private readonly ComboBox _operation = new ComboBox(); private readonly TextBox _distance = new TextBox(); private readonly NumericUpDown _count = new NumericUpDown(); private readonly Label _status = new Label(); private Button _apply; private ModifyObjectPlan _plan;
        public ModifyObjectPlan Plan { get { return _plan; } private set { _plan = value; } }
        public ModifyObjectsForm(Document doc, IList<ElementId> selection) { _doc = doc; _selection = selection ?? new List<ElementId>(); KhimUiStyle.ApplyFormTheme(this); BuildLayout(); }
        private ModifyObjectsForm(bool preview) { _doc = null; _selection = new List<ElementId>(); KhimUiStyle.ApplyFormTheme(this); BuildLayout(); }
        internal static ModifyObjectsForm CreateLayoutPreview() { return new ModifyObjectsForm(true); }
        private void BuildLayout()
        {
            Text = "K-TOOLS - Modify Objects 2.0"; Width = 1160; Height = 720; MinimumSize = new Size(900, 580); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9F);
            var tabs = new TabControl { Dock = DockStyle.Fill }; var structure = new TabPage("STRUCTURE"); var wall = new TabPage("WALL"); var general = new TabPage("GENERAL"); tabs.TabPages.AddRange(new[] { structure, wall, general });
            _operation.DropDownStyle = ComboBoxStyle.DropDownList; _operation.Items.AddRange(Enum.GetNames(typeof(ModifyObjectOperation))); _operation.SelectedIndex = 0; _operation.Dock = DockStyle.Fill; _distance.Text = "100"; _distance.Dock = DockStyle.Fill; _count.Minimum = 1; _count.Maximum = 1000; _count.Value = 2;
            foreach (TabPage page in tabs.TabPages) { var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 }; grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); grid.Controls.Add(new Label { Text = "Operation", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); grid.Controls.Add(_operation, 1, 0); grid.Controls.Add(new Label { Text = "Distance (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); grid.Controls.Add(_distance, 1, 1); grid.Controls.Add(new Label { Text = "Array count", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); grid.Controls.Add(_count, 1, 2); var hint = new Label { AutoSize = true, Text = "Preview runs ANALYZE/PLAN/PREFLIGHT only. Model writes occur only after Apply." }; grid.Controls.Add(hint, 0, 3); grid.SetColumnSpan(hint, 2); page.Controls.Add(grid); }
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), RowCount = 2 }; root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); root.Controls.Add(tabs, 0, 0); var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; _apply = new Button { Text = "Apply", Width = 100, Enabled = false }; _apply.Click += ApplyClicked; var preview = new Button { Text = "Preview", Width = 100 }; preview.Click += PreviewClicked; var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel }; buttons.Controls.Add(_apply); buttons.Controls.Add(preview); buttons.Controls.Add(cancel); _status.Text = "Select an operation and click Preview."; _status.AutoSize = true; buttons.Controls.Add(_status); root.Controls.Add(buttons, 0, 1); Controls.Add(root); CancelButton = cancel;
        }
        private void PreviewClicked(object sender, EventArgs e)
        {
            if (_doc == null) { _status.Text = "Layout preview only; no model writes occurred."; return; }
            ModifyObjectOperation operation; if (!Enum.TryParse(_operation.SelectedItem == null ? string.Empty : _operation.SelectedItem.ToString(), out operation)) return; IList<ElementId> ids = _selection;
            Plan = BuildPreviewPlan(operation, ids);
            _apply.Enabled = Plan != null && Plan.CanExecute; _status.Text = "Preview: " + (Plan == null ? "no plan" : Plan.Status + " / " + Plan.Warnings.Count + " warning(s)") + ". No model writes occurred.";
        }
        private ModifyObjectPlan BuildPreviewPlan(ModifyObjectOperation operation, IList<ElementId> ids)
        {
            ElementId first = ids == null ? ElementId.InvalidElementId : ids.FirstOrDefault(); ElementId second = ids != null && ids.Count > 1 ? ids[1] : ElementId.InvalidElementId;
            switch (operation)
            {
                case ModifyObjectOperation.MOVE_3D: return Move3DService.Analyze(_doc, ids, new XYZ(ParseDistance(), 0, 0));
                case ModifyObjectOperation.ARRAY_3D: return Array3DService.Analyze(_doc, first, (int)_count.Value, new XYZ(ParseDistance(), 0, 0), true);
                case ModifyObjectOperation.CREATE_PARTS: return PartsService.Analyze(_doc, ids);
                case ModifyObjectOperation.COLUMN_SPLIT: return ColumnSplitService.Analyze(_doc, first, PickSplitLevel(first));
                case ModifyObjectOperation.COLUMN_JOIN: return ColumnJoinService.Analyze(_doc, first, second, ModifyObjectConflictPolicy.KEEP_LOWER);
                case ModifyObjectOperation.BEAM_SPLIT: return BeamSplitService.Analyze(_doc, first, Midpoint(first));
                case ModifyObjectOperation.BEAM_JOIN: return BeamJoinService.Analyze(_doc, first, second);
                case ModifyObjectOperation.SLAB_JOIN: return SlabJoinAdapter.Analyze(_doc, first, second);
                case ModifyObjectOperation.SLAB_SPLIT: return SlabSplitService.CapabilityAudit(_doc, first);
                case ModifyObjectOperation.WALL_SPLIT: return WallSplitService.Analyze(_doc, first, Midpoint(first));
                case ModifyObjectOperation.WALL_TRIM: return WallTrimService.Analyze(_doc, first, Midpoint(first));
                default: var context = new ModifyObjectContext { Document = _doc, Operation = operation }; foreach (ElementId id in ids ?? new List<ElementId>()) context.ElementIds.Add(id); return ModifyObjectPlanBuilder.Build(context);
            }
        }
        private ElementId PickSplitLevel(ElementId columnId) { var levels = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x => x.Elevation).ToList(); FamilyInstance column = _doc.GetElement(columnId) as FamilyInstance; Level baseLevel = column == null ? null : _doc.GetElement(column.LevelId) as Level; return levels.FirstOrDefault(x => baseLevel != null && x.Elevation > baseLevel.Elevation)?.Id ?? ElementId.InvalidElementId; }
        private XYZ Midpoint(ElementId id) { Element element = _doc.GetElement(id); LocationCurve curve = element == null ? null : element.Location as LocationCurve; return curve == null ? null : curve.Curve.Evaluate(0.5, true); }
        private double ParseDistance() { double value; return double.TryParse(_distance.Text, out value) ? value : 0; }
        private void ApplyClicked(object sender, EventArgs e) { if (Plan == null || !Plan.CanExecute) { _status.Text = "Preflight blocked this operation."; return; } DialogResult = DialogResult.OK; Close(); }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.Core.UI;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;
using KhimTools.DimensionTools.Services;
using RevitView = Autodesk.Revit.DB.View;
using RevitUIDocument = Autodesk.Revit.UI.UIDocument;
using WinControl = System.Windows.Forms.Control;

namespace KhimTools.DimensionTools.Forms
{
    public sealed class DimensionToolsForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly RevitUIDocument _uidoc;
        private readonly RevitView _view;
        private readonly IList<ElementId> _selection;
        private readonly ComboBox _axis = new ComboBox();
        private readonly ComboBox _strategy = new ComboBox();
        private readonly ComboBox _dimensionType = new ComboBox();
        private readonly NumericUpDown _offset = new NumericUpDown();
        private readonly NumericUpDown _horizontalMove = new NumericUpDown();
        private readonly NumericUpDown _verticalMove = new NumericUpDown();
        private readonly NumericUpDown _boundary = new NumericUpDown();
        private readonly TextBox _preview = new TextBox();
        private readonly Label _status = new Label();
        private readonly Dictionary<WinControl, Label> _labels = new Dictionary<WinControl, Label>();
        private Button _apply;
        private DimensionOperation _selectedOperation = DimensionOperation.GRID;
        private IList<DimensionReferenceInfo> _generalReferences = new List<DimensionReferenceInfo>();
        private Line _generalLine;
        public DimensionPlan Plan { get; private set; }

        public DimensionToolsForm(RevitUIDocument uidoc, RevitView view, IList<ElementId> selection) { _uidoc = uidoc; _doc = uidoc == null ? null : uidoc.Document; _view = view; _selection = selection ?? new List<ElementId>(); KhimUiStyle.ApplyFormTheme(this); BuildLayout(); }
        private DimensionToolsForm(bool preview) { _selection = new List<ElementId>(); KhimUiStyle.ApplyFormTheme(this); BuildLayout(); }
        internal static DimensionToolsForm CreateLayoutPreview() { return new DimensionToolsForm(true); }

        private void BuildLayout()
        {
            Text = "K-TOOLS - Dimension Tools 2.0"; Width = 1180; Height = 720; MinimumSize = new Size(940, 600); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9F);
            ConfigureInputs();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 3, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildOperationPage("AUTO DIMENSION", new[] { DimensionOperation.GRID, DimensionOperation.COLUMN, DimensionOperation.BEAM, DimensionOperation.FOUNDATION, DimensionOperation.OPENING, DimensionOperation.ELEVATION, DimensionOperation.SPOT_ELEVATION, DimensionOperation.QUICK, DimensionOperation.GENERAL }));
            tabs.TabPages.Add(BuildOperationPage("EDIT DIMENSION", new[] { DimensionOperation.CUT, DimensionOperation.JOIN, DimensionOperation.REMOVE_ZERO, DimensionOperation.MOVE_TEXT }));
            root.Controls.Add(tabs, 0, 0);

            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 8 };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddSetting(settings, 0, "Axis", _axis); AddSetting(settings, 1, "Dimension type", _dimensionType); AddSetting(settings, 2, "Reference strategy", _strategy); AddSetting(settings, 3, "Dimension offset (mm)", _offset); AddSetting(settings, 4, "Cut boundary", _boundary); AddSetting(settings, 5, "Move horizontal (mm)", _horizontalMove); AddSetting(settings, 6, "Move vertical (mm)", _verticalMove);
            root.Controls.Add(settings, 1, 0);

            _preview.Dock = DockStyle.Fill; _preview.Multiline = true; _preview.ReadOnly = true; _preview.ScrollBars = ScrollBars.Both; _preview.Font = new Font("Consolas", 9F); _preview.Text = "Preview is required. No model writes occur during Preview.";
            root.Controls.Add(_preview, 2, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            _apply = new Button { Text = "Apply", Width = 100, Enabled = false }; _apply.Click += ApplyClicked;
            var previewButton = new Button { Text = "Preview", Width = 100 }; previewButton.Click += PreviewClicked;
            var refresh = new Button { Text = "Refresh", Width = 100 }; refresh.Click += delegate { Plan = null; _apply.Enabled = false; _status.Text = "Refresh cleared the cached Preview; current command selection will be read again."; };
            var close = new Button { Text = "Close", Width = 100, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(_apply); buttons.Controls.Add(previewButton); buttons.Controls.Add(refresh); buttons.Controls.Add(close);
            _status.AutoSize = true; _status.Text = "Preview does not write model state."; buttons.Controls.Add(_status);
            root.SetColumnSpan(buttons, 3); root.Controls.Add(buttons, 0, 1); Controls.Add(root); CancelButton = close;
            UpdateDynamicSettings();
        }

        private void ConfigureInputs()
        {
            _axis.DropDownStyle = ComboBoxStyle.DropDownList; _axis.Items.AddRange(Enum.GetValues(typeof(DimensionAxis)).Cast<object>().ToArray()); _axis.SelectedItem = DimensionAxis.AUTO;
            _strategy.DropDownStyle = ComboBoxStyle.DropDownList; _strategy.Items.AddRange(new object[] { "EXPLICIT", "CENTER", "NEAR_FACE", "FAR_FACE" }); _strategy.SelectedItem = "EXPLICIT";
            foreach (NumericUpDown value in new[] { _offset, _horizontalMove, _verticalMove }) { value.Minimum = -100000; value.Maximum = 100000; value.DecimalPlaces = 1; value.Increment = 5; }
            _offset.Value = 100; _boundary.Minimum = 1; _boundary.Maximum = 1000; _boundary.Value = 1;
            _dimensionType.DropDownStyle = ComboBoxStyle.DropDownList;
            if (_doc != null) foreach (DimensionType type in new FilteredElementCollector(_doc).OfClass(typeof(DimensionType)).Cast<DimensionType>().OrderBy(x => x.Name)) _dimensionType.Items.Add(new DimensionTypeItem(type));
            if (_dimensionType.Items.Count > 0) _dimensionType.SelectedIndex = 0;
        }

        private TabPage BuildOperationPage(string title, IEnumerable<DimensionOperation> operations)
        {
            var page = new TabPage(title); var list = new ListBox { Dock = DockStyle.Fill };
            foreach (DimensionOperation operation in operations) list.Items.Add(operation);
            list.SelectedIndexChanged += delegate { if (list.SelectedItem == null) return; _selectedOperation = (DimensionOperation)list.SelectedItem; Plan = null; _apply.Enabled = false; UpdateDynamicSettings(); };
            if (title.StartsWith("AUTO", StringComparison.Ordinal)) list.SelectedIndex = 0;
            page.Controls.Add(list); return page;
        }

        private void AddSetting(TableLayoutPanel panel, int row, string text, WinControl control)
        {
            var label = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left }; control.Dock = DockStyle.Top; _labels[control] = label; panel.Controls.Add(label, 0, row); panel.Controls.Add(control, 1, row);
        }

        private void UpdateDynamicSettings()
        {
            bool edit = _selectedOperation == DimensionOperation.CUT || _selectedOperation == DimensionOperation.JOIN || _selectedOperation == DimensionOperation.REMOVE_ZERO || _selectedOperation == DimensionOperation.MOVE_TEXT;
            SetVisible(_axis, !edit); SetVisible(_dimensionType, !edit && _selectedOperation != DimensionOperation.SPOT_ELEVATION); SetVisible(_strategy, _selectedOperation == DimensionOperation.QUICK); SetVisible(_offset, !edit && _selectedOperation != DimensionOperation.GENERAL); SetVisible(_boundary, _selectedOperation == DimensionOperation.CUT); SetVisible(_horizontalMove, _selectedOperation == DimensionOperation.MOVE_TEXT); SetVisible(_verticalMove, _selectedOperation == DimensionOperation.MOVE_TEXT);
            _status.Text = _selectedOperation + " selected. Preview is required before Apply.";
        }
        private void SetVisible(WinControl control, bool visible) { control.Visible = visible; Label label; if (_labels.TryGetValue(control, out label)) label.Visible = visible; }

        private void PreviewClicked(object sender, EventArgs e)
        {
            if (_doc == null) { _status.Text = "Layout preview only; no model writes occurred."; return; }
            var options = new DimensionOptions { Axis = (DimensionAxis)_axis.SelectedItem, OffsetMillimeters = (double)_offset.Value, BoundaryIndex = (int)_boundary.Value, ReferenceStrategy = Convert.ToString(_strategy.SelectedItem), DimensionTypeId = _dimensionType.SelectedItem is DimensionTypeItem ? ((DimensionTypeItem)_dimensionType.SelectedItem).Id : ElementId.InvalidElementId };
            if (_selectedOperation == DimensionOperation.GENERAL && !CaptureGeneralInput()) return;
            Plan = BuildPlan(_selectedOperation, options); _apply.Enabled = Plan != null && Plan.CanExecute;
            _preview.Text = FormatPreview(Plan); _status.Text = "Preview completed. No model writes occurred.";
        }

        private DimensionPlan BuildPlan(DimensionOperation operation, DimensionOptions options)
        {
            switch (operation)
            {
                case DimensionOperation.GRID: return GridDimensionService.BuildPlan(_doc, _view, _selection, options);
                case DimensionOperation.COLUMN: return ColumnDimensionService.BuildPlan(_doc, _view, _selection, options.ReferenceStrategy == "CENTER" ? "CENTERLINE" : "FACE_TO_FACE", options);
                case DimensionOperation.BEAM: return BeamDimensionService.BuildPlan(_doc, _view, _selection, "END_TO_END", options);
                case DimensionOperation.FOUNDATION: return FoundationDimensionService.BuildPlan(_doc, _view, _selection, "FACE_CHAIN", options);
                case DimensionOperation.OPENING: return OpeningDimensionService.BuildPlan(_doc, _view, _selection, options);
                case DimensionOperation.ELEVATION: return ElevationDimensionService.BuildLevelPlan(_doc, _view, _selection, options);
                case DimensionOperation.SPOT_ELEVATION: return BuildSpotPlan(options);
                case DimensionOperation.QUICK: return QuickDimensionService.BuildPlan(_doc, _view, _selection, options.ReferenceStrategy, options);
                case DimensionOperation.GENERAL:
                    return GeneralDimensionService.BuildPlan(_doc, _view, _generalReferences, _generalLine, options);
                default: return BuildEditPlan(operation, options);
            }
        }

        private DimensionPlan BuildEditPlan(DimensionOperation operation, DimensionOptions options)
        {
            options.HorizontalMoveMillimeters = (double)_horizontalMove.Value; options.VerticalMoveMillimeters = (double)_verticalMove.Value;
            var context = new DimensionContext { Document = _doc, View = _view, Operation = operation, Options = options };
            foreach (ElementId id in _selection) if (_doc.GetElement(id) is Dimension) context.DimensionIds.Add(id);
            var plan = DimensionPlanBuilder.CreateBasePlan(context);
            plan.Status = DimensionStatus.READY;
            plan.Fingerprint = DimensionPlanBuilder.BuildFingerprint(plan);
            int required = operation == DimensionOperation.JOIN ? 2 : 1;
            if (context.DimensionIds.Count != required) { plan.Status = DimensionStatus.INVALID_SELECTION; plan.Errors.Add(operation + " requires exactly " + required + " selected Dimension" + (required == 1 ? "." : "s.")); }
            return plan;
        }

        private DimensionPlan BuildSpotPlan(DimensionOptions options)
        {
            var context = new DimensionContext { Document = _doc, View = _view, Operation = DimensionOperation.SPOT_ELEVATION, Options = options };
            IEnumerable<Element> elements = _selection.Select(id => _doc.GetElement(id)).Where(element => element != null);
            foreach (DimensionReferenceInfo info in DimensionReferenceService.FromElements(_doc, _view, elements, DimensionReferenceRole.GENERIC_FACE, options.Axis).Take(1)) context.References.Add(info);
            var plan = DimensionPlanBuilder.CreateBasePlan(context);
            plan.Status = DimensionStatus.READY;
            plan.DimensionTypeId = ElementId.InvalidElementId;
            foreach (DimensionReferenceInfo info in context.References) plan.References.Add(DimensionPlanSnapshotAdapter.Capture(info));
            plan.Fingerprint = DimensionPlanBuilder.BuildFingerprint(plan);
            if (context.References.Count != 1) { plan.Status = DimensionStatus.REFERENCE_NOT_FOUND; plan.Errors.Add("Select one element exposing a valid planar face Reference for Spot Elevation."); }
            return plan;
        }

        private bool CaptureGeneralInput()
        {
            if (_uidoc == null) return false;
            Hide();
            try
            {
                IList<Reference> picked = _uidoc.Selection.PickObjects(ObjectType.PointOnElement, "K-TOOLS: pick two or more dimension references");
                if (picked == null || picked.Count < 2) { _status.Text = "GENERAL requires at least two picked References."; return false; }
                XYZ first = _uidoc.Selection.PickPoint("K-TOOLS: pick the first point of the dimension line");
                XYZ second = _uidoc.Selection.PickPoint("K-TOOLS: pick the second point of the dimension line");
                ViewPlane plane = ViewPlane.FromView(_view); first = plane.Project(first); second = plane.Project(second);
                if (first.DistanceTo(second) < DimensionGeometryService.Mm(0.1)) { _status.Text = "INVALID_DIMENSION_LINE"; return false; }
                _generalReferences = DimensionReferenceService.FromReferences(_doc, _view, picked, DimensionReferenceRole.GENERIC_REFERENCE);
                _generalLine = Line.CreateBound(first, second);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { _status.Text = "General Dimension picking was cancelled."; return false; }
            finally { Show(); BringToFront(); }
        }

        private static string FormatPreview(DimensionPlan plan)
        {
            if (plan == null) return "No plan.";
            var lines = new List<string> { "Operation: " + plan.Operation, "Status: " + plan.Status, "View: " + (plan.ViewId == null ? "" : plan.ViewId.ToLongValue().ToString()), "Dimension type: " + (plan.DimensionTypeId == null ? "" : plan.DimensionTypeId.ToLongValue().ToString()), "References: " + plan.References.Count, "Expected segments: " + plan.ExpectedSegments };
            for (int i = 0; i < plan.References.Count; i++) lines.Add((i + 1) + ". " + plan.References[i].SourceRole + " | " + plan.References[i].StableRepresentation);
            lines.AddRange(plan.Warnings.Select(x => "WARNING: " + x)); lines.AddRange(plan.Errors.Select(x => "BLOCKED: " + x)); return string.Join(Environment.NewLine, lines.ToArray());
        }

        private void ApplyClicked(object sender, EventArgs e) { if (Plan == null || !Plan.CanExecute) { _status.Text = "Preflight blocked this operation."; return; } DialogResult = DialogResult.OK; Close(); }
        private sealed class DimensionTypeItem { public ElementId Id { get; private set; } private readonly string _name; public DimensionTypeItem(DimensionType type) { Id = type.Id; _name = type.Name; } public override string ToString() { return _name; } }
    }
}

using KhimTools.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Form = System.Windows.Forms.Form;
using Color = System.Drawing.Color;
using Panel = System.Windows.Forms.Panel;
using Rectangle = System.Drawing.Rectangle;
using KhimTools.Core.Preview;
using KhimTools.Core.Revit;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>
    /// Explicit Manual v1 editor. All generated quantities and geometry are user
    /// specified; bound verified support geometry is permitted, structural code checks remain unavailable.
    /// </summary>
    internal sealed class BeamManualDetailingFormV1 : Form
    {
        private sealed class BarTypeChoice
        {
            public RebarBarType Type { get; }
            public string Display { get; }
            public BarTypeChoice(RebarBarType type)
            {
                Type = type;
                Display = type.Name + " · Ø" + UnitUtils.ConvertFromInternalUnits(type.BarModelDiameter, UnitTypeId.Millimeters).ToString("0.###", CultureInfo.InvariantCulture) + " mm";
            }
            public override string ToString() => Display;
        }

        private sealed class HookTypeChoice
        {
            public RebarHookType Type { get; }
            public string Display { get; }
            public HookTypeChoice(RebarHookType type) { Type = type; Display = type.Name; }
            public override string ToString() => Display;
        }

        private sealed class ShapeChoice
        {
            public RebarShape Shape { get; }
            public string Display { get; }
            public ShapeChoice(RebarShape shape) { Shape = shape; Display = shape.Name; }
            public override string ToString() => Display;
        }

        private readonly Document _document;
        private readonly FamilyInstance _beam;
        private readonly BeamManualRebarGeneratorV1 _generator;
        private readonly BeamManualDetailingPlanStoreV1 _store;
        private readonly List<RebarBarType> _barTypes;
        private readonly List<RebarHookType> _hookTypes;
        private readonly List<RebarShape> _stirrupShapes;
        private BeamManualDetailingPlanV1 _plan;
        private RebarPreviewSnapshot _preview;
        private DetachedSolvedViewV1 _solved2D;
        private readonly DataGridView _bars = new DataGridView();
        private readonly DataGridView _stirrups = new DataGridView();
        private readonly TextBox[] _covers = new TextBox[5];
        private readonly ComboBox _supportStart = new ComboBox();
        private readonly ComboBox _supportFinish = new ComboBox();
        private readonly Button _save = new Button();
        private readonly Button _solve = new Button();
        private readonly Button _create = new Button();
        private readonly Label _state = new Label();
        private readonly Panel _sketch = new Panel();
        private BeamManualFrameV1 _sketchFrame;
        private Tuple<BeamManualBarRole, double, double, double, double>[] _sketchBars = Array.Empty<Tuple<BeamManualBarRole, double, double, double, double>>();
        private Tuple<BeamManualStirrupRole, double[][], double[]>[] _sketchStirrupPaths = Array.Empty<Tuple<BeamManualStirrupRole, double[][], double[]>>();
        private string _planLoadWarning;
        private readonly bool _assignmentOnly;
        internal BeamManualDetailingPlanV1 AssignedPlan { get; private set; }
        internal RebarPreviewSnapshot AssignedPreview { get; private set; }

        public BeamManualDetailingFormV1(Document document, FamilyInstance beam, bool assignmentOnly = false)
        {
            _assignmentOnly = assignmentOnly;
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _beam = beam ?? throw new ArgumentNullException(nameof(beam));
            _generator = new BeamManualRebarGeneratorV1(document);
            _barTypes = new FilteredElementCollector(document).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                .Where(type => type.IsValidObject).OrderBy(type => type.Name, StringComparer.Ordinal).ToList();
            _hookTypes = new FilteredElementCollector(document).OfClass(typeof(RebarHookType)).Cast<RebarHookType>()
                .Where(type => type.IsValidObject).OrderBy(type => type.Name, StringComparer.Ordinal).ToList();
            _stirrupShapes = new FilteredElementCollector(document).OfClass(typeof(RebarShape)).Cast<RebarShape>()
                .Where(shape => shape.IsValidObject && shape.RebarStyle == RebarStyle.StirrupTie)
                .OrderBy(shape => shape.Name, StringComparer.Ordinal).ToList();
            _plan = _generator.CreateBlankPlan(beam);
            _sketchFrame = _plan.Frame;
            _store = BeamManualDetailingPlanStoreV1.ForCurrentUser(_plan.DocumentKey, beam.UniqueId);
            ConfigureWindow();
            BuildUi();
            if (_assignmentOnly) _create.Text = "Accept host plan / Nhận plan";
            try { LoadSavedPlan(); }
            catch (Exception ex) { _planLoadWarning = "Saved Manual v1 plan is blocked and was not overwritten: " + ex.Message; }
            WireInvalidation();
            UpdateState(_planLoadWarning ?? "NOT SOLVED — geometry only; structural design/compliance not verified.");
        }

        // Controls-only factory for offline layout QA; it never creates a Revit object or runs a command.
        private BeamManualDetailingFormV1(bool layoutOnly)
        {
            _document = null;
            _beam = null;
            _generator = null;
            _store = null;
            _barTypes = new List<RebarBarType>();
            _hookTypes = new List<RebarHookType>();
            _stirrupShapes = new List<RebarShape>();
            ConfigureWindow();
            BuildUi();
            WireInvalidation();
            UpdateState("LAYOUT PREVIEW ONLY — no Revit document or generated geometry.");
        }

        private static Form CreateLayoutPreview() => new BeamManualDetailingFormV1(layoutOnly: true);

        private void ConfigureWindow()
        {
            Text = "Beam Manual Detailing v1 / Bố trí thép dầm thủ công v1 — geometry only / chỉ kiểm tra hình học";
            Width = 1180;
            Height = 760;
            MinimumSize = new Size(1080, 720);
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            Height = Math.Max(MinimumSize.Height, Math.Min(Height, workingArea.Height));
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, RowCount = 7, ColumnCount = 1, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 56,
                Text = "BEAM MANUAL DETAILING V1 — geometry validation only; structural design/compliance not verified.\r\n" +
                    "Beam / Dầm: " + (_beam == null ? "SYNTHETIC LAYOUT PREVIEW" : _beam.Name + " / " + _beam.Id.ToLongValue().ToString(CultureInfo.InvariantCulture)) +
                    " · Frame x=Right, y=Up, s=Beam direction · dimensions in mm.",
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Color.FromArgb(31, 41, 55),
                Margin = new Padding(4, 4, 4, 12)
            };
            root.Controls.Add(header, 0, 0);

            ConfigureBarGrid();
            ConfigureStirrupGrid();
            var groupTabs = new TabControl { Dock = DockStyle.Fill };
            var barsTab = new TabPage("Longitudinal bars / Thép dọc");
            barsTab.Controls.Add(_bars);
            var stirrupsTab = new TabPage("Stirrups & hangers / Đai và thép treo");
            stirrupsTab.Controls.Add(_stirrups);
            groupTabs.TabPages.Add(barsTab);
            groupTabs.TabPages.Add(stirrupsTab);
            root.Controls.Add(groupTabs, 0, 1);

            _sketch.Dock = DockStyle.Fill;
            _sketch.BackColor = Color.White;
            _sketch.BorderStyle = BorderStyle.FixedSingle;
            _sketch.Paint += SketchPaint;
            root.Controls.Add(_sketch, 0, 2);

            var editButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight };
            var add = new Button { Text = "Add explicit bar / Thêm thanh thép", AutoSize = true };
            add.Click += (s, e) => { _bars.Rows.Add(Guid.NewGuid().ToString("N"), true, null, null, "", "", "", "", "", null, null); InvalidatePreview(); };
            var remove = new Button { Text = "Remove selected / Xóa dòng chọn", AutoSize = true };
            remove.Click += (s, e) => { foreach (DataGridViewRow row in _bars.SelectedRows) if (!row.IsNewRow) _bars.Rows.Remove(row); InvalidatePreview(); };
            editButtons.Controls.Add(add);
            editButtons.Controls.Add(remove);
            var addStirrup = new Button { Text = "Add explicit tie / Thêm đai", AutoSize = true };
            addStirrup.Click += (s, e) => { _stirrups.Rows.Add(Guid.NewGuid().ToString("N"), true, null, null, null, "", "", null, null, null, null, "REJECT_DUPLICATE_STATIONS"); InvalidatePreview(); };
            var removeStirrup = new Button { Text = "Remove tie / Xóa đai", AutoSize = true };
            removeStirrup.Click += (s, e) => { foreach (DataGridViewRow row in _stirrups.SelectedRows) if (!row.IsNewRow) _stirrups.Rows.Remove(row); InvalidatePreview(); };
            editButtons.Controls.Add(addStirrup);
            editButtons.Controls.Add(removeStirrup);
            root.Controls.Add(editButtons, 0, 3);

            var disclosure = new Label
            {
                Dock = DockStyle.Fill, AutoSize = false, Height = 38,
                Text = "Geometry only; extensions need explicit verified rectangular support + cover. / Kéo dài cần gối chữ nhật và lớp bảo vệ xác minh.",
                ForeColor = Color.DarkRed, Padding = new Padding(4, 4, 4, 2)
            };
            root.Controls.Add(disclosure, 0, 4);

            var settings = new TableLayoutPanel { AutoSize = false, Dock = DockStyle.Fill, Height = 170, ColumnCount = 5, RowCount = 3, Padding = new Padding(0, 4, 0, 4) };
            for (int column = 0; column < 5; column++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            string[] faceNames = { "Top cover / Lớp bảo vệ trên (mm)", "Bottom cover / Lớp bảo vệ dưới (mm)", "Left cover / Lớp bảo vệ trái (mm)", "Right cover / Lớp bảo vệ phải (mm)", "Stirrup cover / Lớp bảo vệ ngoài đai (mm)" };
            for (int i = 0; i < _covers.Length; i++)
            {
                var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(3, 0, 3, 0) };
                cell.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
                cell.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
                cell.Controls.Add(new Label { Text = faceNames[i], Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
                _covers[i] = new TextBox { Dock = DockStyle.Fill, AccessibleName = faceNames[i] };
                cell.Controls.Add(_covers[i]);
                settings.Controls.Add(cell, i, 0);
            }
            settings.Controls.Add(new Label { Text = "Start end", Dock = DockStyle.Fill, AutoSize = false, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            settings.Controls.Add(_supportStart, 1, 1);
            settings.Controls.Add(new Label { Text = "Finish end", Dock = DockStyle.Fill, AutoSize = false, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 2, 1);
            settings.Controls.Add(_supportFinish, 3, 1);
            ConfigureSupportCombo(_supportStart);
            ConfigureSupportCombo(_supportFinish);
            settings.Controls.Add(new Label { Text = "Explicit face covers only; no defaults. Free-end development is unverified. / Lớp bảo vệ phải nhập rõ; neo đầu tự do chưa xác nhận.", Dock = DockStyle.Fill, AutoSize = false, ForeColor = Color.FromArgb(80, 80, 80) }, 0, 2);
            settings.SetColumnSpan(settings.GetControlFromPosition(0, 2), 5);
            root.Controls.Add(settings, 0, 5);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
            _state.AutoSize = false;
            _state.Dock = DockStyle.Fill;
            _state.Height = 38;
            _state.ForeColor = Color.FromArgb(120, 53, 15);
            footer.Controls.Add(_state, 0, 0);
            footer.SetColumnSpan(_state, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var close = new Button { Text = "Close / Đóng", AutoSize = true };
            close.Click += (s, e) => Close();
            _create.Text = "Create / Tạo";
            _create.AutoSize = true;
            _create.Enabled = false;
            _create.Click += CreateClicked;
            _solve.Text = "Solve preview / Giải xem trước";
            _solve.AutoSize = true;
            _solve.Click += SolveClicked;
            _save.Text = "Save plan / Lưu kế hoạch";
            _save.AutoSize = true;
            _save.Click += SaveClicked;
            actions.Controls.Add(close);
            actions.Controls.Add(_create);
            actions.Controls.Add(_solve);
            actions.Controls.Add(_save);
            footer.Controls.Add(actions, 0, 1);
            footer.SetColumnSpan(actions, 2);
            root.Controls.Add(footer, 0, 6);
            Controls.Add(root);
        }

        private void ConfigureBarGrid()
        {
            _bars.Dock = DockStyle.Fill;
            _bars.AllowUserToAddRows = true;
            _bars.AllowUserToDeleteRows = true;
            _bars.AutoGenerateColumns = false;
            _bars.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _bars.RowHeadersVisible = false;
            _bars.EditMode = DataGridViewEditMode.EditOnEnter;
            _bars.Columns.Add(new DataGridViewTextBoxColumn { Name = "GroupId", HeaderText = "Stable group ID", FillWeight = 135 });
            _bars.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Enabled", TrueValue = true, FalseValue = false, FillWeight = 55 });
            var roles = new DataGridViewComboBoxColumn { Name = "Role", HeaderText = "Role", DataSource = new[] { BeamManualBarRole.TopContinuous, BeamManualBarRole.BottomContinuous, BeamManualBarRole.TopLeftExtra, BeamManualBarRole.TopRightExtra, BeamManualBarRole.BottomMidExtra, BeamManualBarRole.SideLeft, BeamManualBarRole.SideRight, BeamManualBarRole.UserDefined }, FillWeight = 100, FlatStyle = FlatStyle.Flat };
            _bars.Columns.Add(roles);
            var types = new DataGridViewComboBoxColumn { Name = "BarType", HeaderText = "Exact loaded Rebar type", DataSource = _barTypes.Select(type => new BarTypeChoice(type)).ToList(), DisplayMember = "Display", FlatStyle = FlatStyle.Flat, FillWeight = 150 };
            _bars.Columns.Add(types);
            foreach (string key in new[] { "X mm", "Y mm", "Start station mm", "Finish station mm" })
                _bars.Columns.Add(new DataGridViewTextBoxColumn { Name = key, HeaderText = key, FillWeight = 85 });
            _bars.Columns.Add(new DataGridViewTextBoxColumn { Name = "Layer", HeaderText = "Explicit layer ID", FillWeight = 90 });
            AddEndDetailColumn("Start detail", "Start end detail");
            AddHookDetailColumns("Start");
            AddEndDetailColumn("Finish detail", "Finish end detail");
            AddHookDetailColumns("Finish");
            ConfigureReadableColumns(_bars);
            _bars.DataError += (s, e) => { e.ThrowException = false; };
        }

        private void AddEndDetailColumn(string name, string header)
        {
            _bars.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = name, HeaderText = header, DataSource = new[] { "STRAIGHT_AT_STATION", "EXPLICIT_CURVE_RETURN", "EXPLICIT_LOADED_REVIT_HOOK" },
                FlatStyle = FlatStyle.Flat, FillWeight = 105,
                ToolTipText = "Select the exact end mode. Return points or exact RebarHookType and orientation are required for the corresponding modes."
            });
        }

        private void AddHookDetailColumns(string prefix)
        {
            _bars.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = prefix + " hook type", HeaderText = prefix + " exact hook type",
                DataSource = _hookTypes.Select(type => new HookTypeChoice(type)).ToList(), DisplayMember = "Display",
                FlatStyle = FlatStyle.Flat, FillWeight = 120,
                ToolTipText = "Exact loaded RebarHookType; no type/angle substitution."
            });
            _bars.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = prefix + " orientation", HeaderText = prefix + " orientation",
                DataSource = new[] { BeamManualHookOrientation.Left, BeamManualHookOrientation.Right },
                FlatStyle = FlatStyle.Flat, FillWeight = 75
            });
            _bars.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = prefix + " return points", HeaderText = prefix + " return points (s,x,y; mm)", FillWeight = 150,
                ToolTipText = "Explicit local mm points formatted s,x,y;... . Start path is ordered tip-to-anchor; finish is anchor-to-tip."
            });
        }

        private void ConfigureStirrupGrid()
        {
            _stirrups.Dock = DockStyle.Fill;
            _stirrups.AllowUserToAddRows = true;
            _stirrups.AllowUserToDeleteRows = true;
            _stirrups.AutoGenerateColumns = false;
            _stirrups.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _stirrups.RowHeadersVisible = false;
            _stirrups.EditMode = DataGridViewEditMode.EditOnEnter;
            _stirrups.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stirrup group ID", HeaderText = "Stable group ID", FillWeight = 125 });
            _stirrups.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Stirrup enabled", HeaderText = "Enabled", FillWeight = 55 });
            _stirrups.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Stirrup role", HeaderText = "Stirrup/hanger role",
                DataSource = new[] { BeamManualStirrupRole.Stirrup, BeamManualStirrupRole.Hanger, BeamManualStirrupRole.UserDefined }, FillWeight = 90
            });
            _stirrups.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Stirrup bar type", HeaderText = "Exact loaded bar type",
                DataSource = _barTypes.Select(type => new BarTypeChoice(type)).ToList(), DisplayMember = "Display", FillWeight = 140
            });
            _stirrups.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Stirrup shape", HeaderText = "Exact loaded StirrupTie shape",
                DataSource = _stirrupShapes.Select(shape => new ShapeChoice(shape)).ToList(), DisplayMember = "Display", FillWeight = 130
            });
            _stirrups.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stirrup stations", HeaderText = "Explicit stations (mm;...)", FillWeight = 115 });
            _stirrups.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stirrup contour", HeaderText = "Section path x,y (mm;...; repeat first to close)", FillWeight = 180 });
            foreach (string prefix in new[] { "Stirrup start", "Stirrup finish" })
            {
                _stirrups.Columns.Add(new DataGridViewComboBoxColumn
                {
                    Name = prefix + " hook", HeaderText = prefix + " exact hook",
                    DataSource = _hookTypes.Select(type => new HookTypeChoice(type)).ToList(), DisplayMember = "Display", FillWeight = 110
                });
                _stirrups.Columns.Add(new DataGridViewComboBoxColumn
                {
                    Name = prefix + " orientation", HeaderText = prefix + " orientation",
                    DataSource = new[] { BeamManualHookOrientation.Left, BeamManualHookOrientation.Right }, FillWeight = 70
                });
            }
            _stirrups.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Coincident policy", HeaderText = "Coincident-station policy",
                DataSource = new[] { "REJECT_DUPLICATE_STATIONS" }, FillWeight = 105,
                ToolTipText = "Same-role coincident stations are rejected; no required bars are silently deduplicated."
            });
            ConfigureReadableColumns(_stirrups);
            _stirrups.DataError += (s, e) => { e.ThrowException = false; };
        }

        private static void ConfigureReadableColumns(DataGridView grid)
        {
            grid.ScrollBars = ScrollBars.Both;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.MinimumWidth = column is DataGridViewCheckBoxColumn ? 65 : 100;
                column.Width = column is DataGridViewCheckBoxColumn ? 65 :
                    column.Name.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0 || column.Name == "Stirrup contour" ? 240 :
                    column.Name.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0 || column.Name == "Stirrup shape" ? 180 :
                    column.Name == "GroupId" || column.Name == "Stirrup group ID" || column.Name == "Stirrup stations" || column.Name == "Coincident policy" ? 180 : 130;
            }
        }

        private static void ConfigureSupportCombo(ComboBox combo)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Add("Select / Chọn điều kiện đầu mút…");
            combo.Items.Add("Free end in Beam / Đầu tự do trong dầm (development not verified / chưa xác nhận neo)");
            combo.Items.Add("Bind support + cover… / Chọn gối + lớp bảo vệ…");
            combo.SelectedIndex = 0;
            combo.Width = 260;
        }

        private void LoadSavedPlan()
        {
            BeamManualDetailingPlanV1 saved = _store.Load();
            if (saved == null) return;
            _plan = saved;
            _sketchFrame = saved.Frame;
            foreach (BeamManualCoverV1 cover in saved.Covers)
            {
                int index = Array.IndexOf(new[] { BeamManualCoverFace.Top, BeamManualCoverFace.Bottom, BeamManualCoverFace.Left, BeamManualCoverFace.Right, BeamManualCoverFace.StirrupOutside }, cover.Face);
                if (index >= 0) _covers[index].Text = cover.ClearCoverMm.ToString("R", CultureInfo.InvariantCulture);
            }
            foreach (BeamManualSupportEndV1 support in saved.SupportEnds)
            {
                ComboBox combo = support.End == BeamManualEnd.Start ? _supportStart : _supportFinish;
                combo.Tag = support.Mode == BeamManualSupportMode.UserDefinedFreeEndWithinHost ? null : support;
                combo.SelectedIndex = support.Mode == BeamManualSupportMode.UserDefinedFreeEndWithinHost ? 1 : 2;
            }
            foreach (BeamManualBarGroupV1 group in saved.BarGroups ?? new List<BeamManualBarGroupV1>())
            foreach (BeamManualBarV1 bar in group.Bars ?? new List<BeamManualBarV1>())
            {
                RebarBarType type = ResolveStoredType(group.BarType);
                int row = _bars.Rows.Add(group.GroupId, group.Enabled, group.Role, type == null ? null : ChoiceFor(type),
                    bar.XMm.ToString("R", CultureInfo.InvariantCulture), bar.YMm.ToString("R", CultureInfo.InvariantCulture),
                    bar.StartStationMm.ToString("R", CultureInfo.InvariantCulture), bar.EndStationMm.ToString("R", CultureInfo.InvariantCulture),
                    bar.LayerId, DetailText(bar.StartDetail), HookChoiceFor(bar.StartDetail?.HookType, "Start hook type"), bar.StartDetail?.HookOrientation,
                    FormatReturnPoints(bar.StartDetail?.ReturnPoints), DetailText(bar.FinishDetail), HookChoiceFor(bar.FinishDetail?.HookType, "Finish hook type"),
                    bar.FinishDetail?.HookOrientation, FormatReturnPoints(bar.FinishDetail?.ReturnPoints));
                if (type == null) _bars.Rows[row].Cells["BarType"].Value = null;
                if (bar.StartDetail.Kind != BeamManualEndDetailKind.StraightAtStation || bar.FinishDetail.Kind != BeamManualEndDetailKind.StraightAtStation)
                {
                    if (bar.StartDetail?.Kind == BeamManualEndDetailKind.ExplicitLoadedRevitHook && HookChoiceFor(bar.StartDetail.HookType) == null ||
                        bar.FinishDetail?.Kind == BeamManualEndDetailKind.ExplicitLoadedRevitHook && HookChoiceFor(bar.FinishDetail.HookType) == null)
                        _bars.Rows[row].ErrorText = "A saved hook identity is unresolved. Explicitly rebind the exact loaded RebarHookType; no substitution was made.";
                }
            }
            foreach (BeamManualStirrupGroupV1 group in saved.StirrupGroups ?? new List<BeamManualStirrupGroupV1>())
            {
                RebarBarType type = ResolveStoredType(group.BarType);
                RebarShape shape = ResolveStoredShape(group.RebarShape);
                int row = _stirrups.Rows.Add(group.GroupId, group.Enabled, group.Role,
                    type == null ? null : StirrupBarChoiceFor(type), shape == null ? null : ShapeChoiceFor(shape),
                    string.Join(";", group.StationsMm.Select(value => value.ToString("R", CultureInfo.InvariantCulture))),
                    FormatSectionPath(group.SectionPath), HookChoiceFor(group.StartDetail?.HookType, "Stirrup start hook"), group.StartDetail?.HookOrientation,
                    HookChoiceFor(group.FinishDetail?.HookType, "Stirrup finish hook"), group.FinishDetail?.HookOrientation,
                    group.CoincidentStationPolicy);
                if (type == null || shape == null) _stirrups.Rows[row].ErrorText = "A saved exact bar type or RebarShape is unresolved; explicitly rebind it. No substitute was selected.";
            }
            // Preserve unresolved references as unresolved; the user must explicitly select the exact loaded type.
        }

        private RebarBarType ResolveStoredType(BeamManualElementReferenceV1 reference)
        {
            if (reference == null || reference.DocumentKey != _plan.DocumentKey || reference.State != BeamManualReferenceState.Resolved) return null;
            Element element = _document.GetElement(reference.UniqueId);
            var type = element as RebarBarType;
            return type != null && type.Name == reference.Name &&
                Math.Abs(UnitUtils.ConvertFromInternalUnits(type.BarModelDiameter, UnitTypeId.Millimeters) - reference.DiameterMm) <= BeamManualPlanValidatorV1.GeometryToleranceMm
                ? type : null;
        }

        private RebarShape ResolveStoredShape(BeamManualElementIdentityV1 reference)
        {
            if (reference == null || reference.DocumentKey != _plan.DocumentKey || reference.State != BeamManualReferenceState.Resolved) return null;
            RebarShape shape = _document.GetElement(reference.UniqueId) as RebarShape;
            return shape != null && shape.IsValidObject && shape.Name == reference.Name && shape.RebarStyle == RebarStyle.StirrupTie ? shape : null;
        }

        private BarTypeChoice ChoiceFor(RebarBarType type)
        {
            var column = (DataGridViewComboBoxColumn)_bars.Columns["BarType"];
            return ((IEnumerable<BarTypeChoice>)column.DataSource).FirstOrDefault(choice => choice.Type.UniqueId == type.UniqueId);
        }

        private static string DetailText(BeamManualEndDetailV1 detail) => detail?.Kind == BeamManualEndDetailKind.StraightAtStation ? "STRAIGHT_AT_STATION" :
            detail?.Kind == BeamManualEndDetailKind.ExplicitCurveReturn ? "EXPLICIT_CURVE_RETURN" :
            detail?.Kind == BeamManualEndDetailKind.ExplicitLoadedRevitHook ? "EXPLICIT_LOADED_REVIT_HOOK" : null;

        private HookTypeChoice HookChoiceFor(BeamManualElementIdentityV1 reference)
        {
            return HookChoiceFor(reference, "Start hook type");
        }

        private HookTypeChoice HookChoiceFor(BeamManualElementIdentityV1 reference, string columnName)
        {
            if (reference == null || reference.State != BeamManualReferenceState.Resolved || reference.DocumentKey != _plan.DocumentKey) return null;
            RebarHookType type = _document.GetElement(reference.UniqueId) as RebarHookType;
            return type != null && type.IsValidObject && type.Name == reference.Name
                ? ((IEnumerable<HookTypeChoice>)((DataGridViewComboBoxColumn)(columnName.StartsWith("Stirrup", StringComparison.Ordinal) ? _stirrups.Columns[columnName] : _bars.Columns[columnName])).DataSource)
                    .FirstOrDefault(choice => choice.Type.UniqueId == type.UniqueId)
                : null;
        }

        private BarTypeChoice StirrupBarChoiceFor(RebarBarType type) =>
            ((IEnumerable<BarTypeChoice>)((DataGridViewComboBoxColumn)_stirrups.Columns["Stirrup bar type"]).DataSource)
                .FirstOrDefault(choice => choice.Type.UniqueId == type.UniqueId);

        private ShapeChoice ShapeChoiceFor(RebarShape shape) =>
            ((IEnumerable<ShapeChoice>)((DataGridViewComboBoxColumn)_stirrups.Columns["Stirrup shape"]).DataSource)
                .FirstOrDefault(choice => choice.Shape.UniqueId == shape.UniqueId);

        private static string FormatReturnPoints(IList<BeamManualPointV1> points) => points == null ? string.Empty :
            string.Join(";", points.Select(point => string.Join(",", point.StationMm.ToString("R", CultureInfo.InvariantCulture),
                point.XMm.ToString("R", CultureInfo.InvariantCulture), point.YMm.ToString("R", CultureInfo.InvariantCulture))));

        private static string FormatSectionPath(IList<BeamManualPointV1> points) => points == null ? string.Empty :
            string.Join(";", points.Select(point => string.Join(",", point.XMm.ToString("R", CultureInfo.InvariantCulture), point.YMm.ToString("R", CultureInfo.InvariantCulture))));

        private void WireInvalidation()
        {
            _bars.CellValueChanged += (s, e) => InvalidatePreview();
            _bars.RowsRemoved += (s, e) => InvalidatePreview();
            _bars.CurrentCellDirtyStateChanged += (s, e) => { if (_bars.IsCurrentCellDirty) _bars.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _bars.CellValueChanged += (s, e) => RefreshSketchSnapshot();
            _bars.RowsRemoved += (s, e) => RefreshSketchSnapshot();
            _stirrups.CellValueChanged += (s, e) => InvalidatePreview();
            _stirrups.RowsRemoved += (s, e) => InvalidatePreview();
            _stirrups.CurrentCellDirtyStateChanged += (s, e) => { if (_stirrups.IsCurrentCellDirty) _stirrups.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _stirrups.CellValueChanged += (s, e) => RefreshSketchSnapshot();
            _stirrups.RowsRemoved += (s, e) => RefreshSketchSnapshot();
            foreach (TextBox cover in _covers) cover.TextChanged += (s, e) => InvalidatePreview();
            _supportStart.SelectedIndexChanged += (s, e) => InvalidatePreview();
            _supportFinish.SelectedIndexChanged += (s, e) => InvalidatePreview();
            _supportStart.SelectionChangeCommitted += (s, e) => BindSupportFromUi(_supportStart, BeamManualEnd.Start);
            _supportFinish.SelectionChangeCommitted += (s, e) => BindSupportFromUi(_supportFinish, BeamManualEnd.Finish);
            RefreshSketchSnapshot();
        }

        private BeamManualDetailingPlanV1 ReadPlanFromUi()
        {
            var plan = _plan;
            plan.BarGroups = new List<BeamManualBarGroupV1>();
            foreach (DataGridViewRow row in _bars.Rows)
            {
                if (row.IsNewRow) continue;
                string id = CellText(row, "GroupId");
                bool enabled = row.Cells["Enabled"].Value is bool valueEnabled && valueEnabled;
                BeamManualBarRole? role = row.Cells["Role"].Value is BeamManualBarRole selectedRole ? selectedRole : (BeamManualBarRole?)null;
                BarTypeChoice choice = row.Cells["BarType"].Value as BarTypeChoice;
                double x = ParseMm(CellText(row, "X mm"), id, "x");
                double y = ParseMm(CellText(row, "Y mm"), id, "y");
                double start = ParseMm(CellText(row, "Start station mm"), id, "start station");
                double finish = ParseMm(CellText(row, "Finish station mm"), id, "finish station");
                string layer = CellText(row, "Layer");
                if (string.IsNullOrWhiteSpace(id) || !role.HasValue || choice?.Type == null || string.IsNullOrWhiteSpace(layer))
                    throw new InvalidOperationException("Every row needs a stable group ID, role, exact loaded Rebar type and explicit layer ID.");
                BeamManualEndDetailV1 startDetail = ReadEndDetail(row, "Start", id);
                BeamManualEndDetailV1 finishDetail = ReadEndDetail(row, "Finish", id);
                BeamManualBarGroupV1 group = plan.BarGroups.FirstOrDefault(item => item.GroupId == id);
                BeamManualElementReferenceV1 typeRef = MakeTypeReference(choice.Type);
                if (group == null)
                {
                    group = new BeamManualBarGroupV1 { GroupId = id, Role = role.Value, Enabled = enabled,
                        UserLabel = role.Value == BeamManualBarRole.UserDefined ? "User-defined manual bar" : role.Value.ToString(),
                        BarType = typeRef, Bars = new List<BeamManualBarV1>() };
                    plan.BarGroups.Add(group);
                }
                else if (group.Role != role.Value || group.Enabled != enabled || group.BarType.UniqueId != typeRef.UniqueId)
                    throw new InvalidOperationException("Rows sharing one stable group ID must share one enabled state, role and exact bar type.");
                group.Bars.Add(new BeamManualBarV1 { XMm = x, YMm = y, StartStationMm = start, EndStationMm = finish, LayerId = layer,
                    StartDetail = startDetail, FinishDetail = finishDetail });
            }
            plan.Covers = ReadCovers();
            if (_supportStart.SelectedIndex < 1 || _supportFinish.SelectedIndex < 1)
                throw new InvalidOperationException("Choose an explicit supported end condition for both Beam ends.");
            plan.SupportEnds = new List<BeamManualSupportEndV1>
            {
                ReadSupport(_supportStart, BeamManualEnd.Start), ReadSupport(_supportFinish, BeamManualEnd.Finish)
            };
            plan.StirrupGroups = ReadStirrupsFromUi();
            plan.CodeVerified = false;
            plan.Source = "MANUAL_DETAILING";
            return plan;
        }

        private List<BeamManualCoverV1> ReadCovers()
        {
            BeamManualCoverFace[] faces = { BeamManualCoverFace.Top, BeamManualCoverFace.Bottom, BeamManualCoverFace.Left, BeamManualCoverFace.Right, BeamManualCoverFace.StirrupOutside };
            return faces.Select((face, index) => new BeamManualCoverV1 { Face = face, Source = BeamManualCoverSource.ExplicitUserInput,
                ClearCoverMm = ParseMm(_covers[index].Text, "cover", face.ToString()) }).ToList();
        }

        private static BeamManualSupportEndV1 FreeEnd(BeamManualEnd end) => new BeamManualSupportEndV1
        {
            End = end,
            Mode = BeamManualSupportMode.UserDefinedFreeEndWithinHost,
            IntendedContinuation = "User explicitly terminates bar at the entered station within Beam host; development length is not verified."
        };

        private static BeamManualSupportEndV1 ReadSupport(ComboBox combo, BeamManualEnd end)
        {
            if (combo.SelectedIndex == 1) return FreeEnd(end);
            var support = combo.Tag as BeamManualSupportEndV1;
            if (combo.SelectedIndex != 2 || support == null || support.End != end || !BeamManualSupportPolicyV1.IsBound(support))
                throw new InvalidOperationException("Explicitly bind the " + end + " support/cover before saving or solving.");
            return support;
        }

        private sealed class SupportChoice
        {
            public FamilyInstance Host { get; }
            private readonly string _display;
            public SupportChoice(FamilyInstance host) { Host = host; _display = host.Category.Name + " · " + host.Name + " · " + host.Id.ToLongValue(); }
            public override string ToString() => _display;
        }

        private void BindSupportFromUi(ComboBox combo, BeamManualEnd end)
        {
            InvalidatePreview();
            if (combo.SelectedIndex != 2 || _document == null) { combo.Tag = null; return; }
            try
            {
                var choices = new FilteredElementCollector(_document).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(host => host.Id != _beam.Id && (host.Category.IsCategory(BuiltInCategory.OST_StructuralColumns) ||
                        host.Category.IsCategory(BuiltInCategory.OST_StructuralFraming)))
                    .OrderBy(host => host.Id.ToLongValue()).Select(host => new SupportChoice(host)).ToList();
                using (var dialog = new BeamSupportBindingFormV1(end.ToString()))
                {
                    var picker = dialog.SupportPicker; picker.DataSource=choices; picker.SelectedIndex = -1;
                    var cover = dialog.CoverInput;
                    var intent = dialog.IntentInput;
                    var accept = dialog.BindButton;
                    accept.Click += (s,e) =>
                    {
                        try {
                            var choice = picker.SelectedItem as SupportChoice;
                            if (choice == null) throw new InvalidOperationException("Choose the exact support; no automatic selection.");
                            var bound = _generator.BindSupport(_beam, end, choice.Host, ParseMm(cover.Text, "support", "clear cover"), intent.Text);
                            combo.Tag = bound; dialog.DialogResult = DialogResult.OK;
                        } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Support blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                    };
                    if (dialog.ShowDialog(this) != DialogResult.OK) { combo.Tag = null; combo.SelectedIndex = 0; }
                }
                UpdateState("Support selection changed / Gối đã đổi — solve again; development and structural compliance not verified.");
            }
            catch (Exception ex) { combo.Tag = null; combo.SelectedIndex = 0; UpdateState("Support binding blocked: " + ex.Message); }
        }

        private static BeamManualEndDetailV1 StraightDetail() => new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation };

        private BeamManualEndDetailV1 ReadEndDetail(DataGridViewRow row, string prefix, string groupId)
        {
            string mode = Convert.ToString(row.Cells[prefix + " detail"].Value, CultureInfo.InvariantCulture);
            if (string.Equals(mode, "STRAIGHT_AT_STATION", StringComparison.Ordinal)) return StraightDetail();
            if (string.Equals(mode, "EXPLICIT_CURVE_RETURN", StringComparison.Ordinal))
                return new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.ExplicitCurveReturn,
                    ReturnPoints = ParseReturnPoints(CellText(row, prefix + " return points"), groupId, prefix + " return points") };
            if (string.Equals(mode, "EXPLICIT_LOADED_REVIT_HOOK", StringComparison.Ordinal))
            {
                HookTypeChoice choice = row.Cells[prefix + " hook type"].Value as HookTypeChoice;
                BeamManualHookOrientation? orientation = row.Cells[prefix + " orientation"].Value is BeamManualHookOrientation value
                    ? (BeamManualHookOrientation?)value : null;
                if (choice?.Type == null || !orientation.HasValue)
                    throw new InvalidOperationException("Group " + groupId + " / " + prefix + " end: choose an exact loaded RebarHookType and orientation.");
                return new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.ExplicitLoadedRevitHook,
                    HookType = MakeHookIdentity(choice.Type), HookOrientation = orientation };
            }
            throw new InvalidOperationException("Group " + groupId + " / " + prefix + " end: explicitly choose a supported end detail.");
        }

        private BeamManualElementIdentityV1 MakeHookIdentity(RebarHookType type) => new BeamManualElementIdentityV1
        {
            DocumentKey = _plan.DocumentKey, UniqueId = type.UniqueId, Name = type.Name, State = BeamManualReferenceState.Resolved
        };

        private BeamManualElementIdentityV1 MakeShapeIdentity(RebarShape shape) => new BeamManualElementIdentityV1
        {
            DocumentKey = _plan.DocumentKey, UniqueId = shape.UniqueId, Name = shape.Name, State = BeamManualReferenceState.Resolved
        };

        private List<BeamManualStirrupGroupV1> ReadStirrupsFromUi()
        {
            var groups = new List<BeamManualStirrupGroupV1>();
            foreach (DataGridViewRow row in _stirrups.Rows)
            {
                if (row.IsNewRow) continue;
                string id = CellText(row, "Stirrup group ID");
                bool enabled = row.Cells["Stirrup enabled"].Value is bool value && value;
                BeamManualStirrupRole? role = row.Cells["Stirrup role"].Value is BeamManualStirrupRole selectedRole ? (BeamManualStirrupRole?)selectedRole : null;
                BarTypeChoice barChoice = row.Cells["Stirrup bar type"].Value as BarTypeChoice;
                ShapeChoice shapeChoice = row.Cells["Stirrup shape"].Value as ShapeChoice;
                if (string.IsNullOrWhiteSpace(id) || !role.HasValue || barChoice?.Type == null || shapeChoice?.Shape == null)
                    throw new InvalidOperationException("Every stirrup/hanger row needs a stable group ID, explicit role, exact loaded RebarBarType and exact loaded StirrupTie RebarShape.");
                BeamManualEndDetailV1 startDetail = ReadStirrupEnd(row, "Stirrup start");
                BeamManualEndDetailV1 finishDetail = ReadStirrupEnd(row, "Stirrup finish");
                groups.Add(new BeamManualStirrupGroupV1
                {
                    GroupId = id, Role = role.Value, Enabled = enabled,
                    BarType = MakeTypeReference(barChoice.Type), RebarShape = MakeShapeIdentity(shapeChoice.Shape),
                    StationsMm = ParseMmList(CellText(row, "Stirrup stations"), id, "stations"),
                    SectionPath = ParseSectionPath(CellText(row, "Stirrup contour"), id),
                    StartDetail = startDetail, FinishDetail = finishDetail,
                    CoincidentStationPolicy = Convert.ToString(row.Cells["Coincident policy"].Value, CultureInfo.InvariantCulture)
                });
            }
            return groups;
        }

        private BeamManualEndDetailV1 ReadStirrupEnd(DataGridViewRow row, string prefix)
        {
            HookTypeChoice choice = row.Cells[prefix + " hook"].Value as HookTypeChoice;
            if (choice == null) return StraightDetail();
            if (!(row.Cells[prefix + " orientation"].Value is BeamManualHookOrientation orientation))
                throw new InvalidOperationException(prefix + " requires an explicit hook orientation.");
            return new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.ExplicitLoadedRevitHook,
                HookType = MakeHookIdentity(choice.Type), HookOrientation = orientation };
        }

        private static List<double> ParseMmList(string text, string group, string field)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Group " + group + " / " + field + " requires explicitly listed millimetre values separated by semicolons.");
            return text.Split(';').Select(token => ParseMm(token, group, field)).ToList();
        }

        private static List<BeamManualPointV1> ParseSectionPath(string text, string group)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Group " + group + " / section path requires explicit x,y mm pairs separated by semicolons; repeat the first pair to close a contour.");
            var points = new List<BeamManualPointV1>();
            foreach (string token in text.Split(';'))
            {
                string[] values = token.Split(',');
                if (values.Length != 2) throw new InvalidOperationException("Group " + group + " / section path entries must be x,y mm pairs.");
                points.Add(new BeamManualPointV1 { XMm = ParseMm(values[0], group, "section x"), YMm = ParseMm(values[1], group, "section y") });
            }
            return points;
        }

        private static List<BeamManualPointV1> ParseReturnPoints(string text, string group, string field)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Group " + group + " / " + field + " needs explicit s,x,y point triples in mm, separated by semicolons.");
            var points = new List<BeamManualPointV1>();
            foreach (string token in text.Split(';'))
            {
                string[] values = token.Split(',');
                if (values.Length != 3) throw new InvalidOperationException("Group " + group + " / " + field + " entries must be s,x,y mm triples.");
                points.Add(new BeamManualPointV1
                {
                    StationMm = ParseMm(values[0], group, field + " station"),
                    XMm = ParseMm(values[1], group, field + " x"),
                    YMm = ParseMm(values[2], group, field + " y")
                });
            }
            return points;
        }

        private BeamManualElementReferenceV1 MakeTypeReference(RebarBarType type) => new BeamManualElementReferenceV1
        {
            DocumentKey = _plan.DocumentKey, UniqueId = type.UniqueId, Name = type.Name,
            DiameterMm = UnitUtils.ConvertFromInternalUnits(type.BarModelDiameter, UnitTypeId.Millimeters),
            State = BeamManualReferenceState.Resolved
        };

        private static string CellText(DataGridViewRow row, string column) => Convert.ToString(row.Cells[column].Value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

        private static double ParseMm(string text, string group, string field)
        {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("Group " + group + " / " + field + " must be an explicit finite millimetre value.");
            return value;
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            try
            {
                BeamManualDetailingPlanV1 candidate = ReadPlanFromUi();
                BeamManualPlanValidatorV1.Validate(candidate, requireResolvedReferences: false).ThrowIfInvalid();
                _plan = _store.Save(candidate, _plan.Revision);
                InvalidatePreview();
                UpdateState("SAVED revision " + _plan.Revision.ToString(CultureInfo.InvariantCulture) + " · not solved · geometry only.");
            }
            catch (Exception ex) { UpdateState("Save blocked: " + ex.Message); }
        }

        private void SolveClicked(object sender, EventArgs e)
        {
            try
            {
                BeamManualDetailingPlanV1 candidate = ReadPlanFromUi();
                string fingerprint = _generator.Fingerprint(candidate, _beam);
                var roleByBar = new Dictionary<string, string>(StringComparer.Ordinal);
                var request = new RebarPreviewRequest(fingerprint,
                    () => _generator.Generate(candidate, _beam, roleByBar),
                    () => _generator.Fingerprint(ReadPlanFromUi(), _beam),
                    new Dictionary<string, string> { { "HostId", BeamHostId(_beam) }, { "HostGeometry", candidate.Frame.HostGeometrySignature } },
                    bar => roleByBar.TryGetValue(bar.Id.ToLongValue().ToString(CultureInfo.InvariantCulture), out string role) ? role : string.Empty);
                RebarPreviewSnapshot detached = RebarPreviewService.Capture(_document, new[] { request });
                using (var preview = new RebarSolverPreviewForm(detached))
                {
                    if (preview.ShowDialog(this) != DialogResult.OK)
                    {
                        _preview = null;
                        UpdateState("PREVIEW CANCELLED · rollback completed · create disabled.");
                        return;
                    }
                }
                _plan = candidate;
                CacheSolved2D(detached);
                _preview = detached;
                UpdateState("SOLVED · " + detached.Components[0].BarCount.ToString(CultureInfo.InvariantCulture) + " explicit bars · geometry validated only · CODE_VERIFIED=false.");
            }
            catch (Exception ex)
            {
                _preview = null;
                UpdateState("Solve blocked: " + ex.Message);
            }
        }

        private void CreateClicked(object sender, EventArgs e)
        {
            try
            {
                if (_preview == null) throw new InvalidOperationException("Solve and accept the current solver-backed preview before Create.");
                BeamManualDetailingPlanV1 candidate = ReadPlanFromUi();
                string fingerprint = _generator.Fingerprint(candidate, _beam);
                RebarPreviewSnapshot accepted = _preview;
                if (!RebarPreviewService.MatchesInput(accepted, fingerprint))
                    throw new InvalidOperationException("Manual input, host, or loaded type changed after preview. Solve again.");
                if (RebarPreviewService.HasExistingDuplicateBar(_document, _beam, accepted, fingerprint))
                    throw new InvalidOperationException("The complete planned bar set already exists on this Beam; duplicate creation is blocked.");

                if (_assignmentOnly)
                {
                    AssignedPlan = candidate;
                    AssignedPreview = accepted;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                TransactionBoundary.ExecuteGroup(_document, "K-TOOLS — Create Beam Manual v1", () =>
                {
                    IList<Rebar> actual = TransactionBoundary.Execute(_document, "Create explicit Manual Beam bars", () =>
                    {
                        IList<Rebar> generated = _generator.Generate(candidate, _beam);
                        _document.Regenerate();
                        if (!RebarPreviewService.Matches(accepted, fingerprint, generated.ToList()))
                            throw new InvalidOperationException("Created Rebar differs from accepted preview; the full transaction group will roll back.");
                        return generated;
                    });
                    if (actual == null || actual.Count == 0) throw new InvalidOperationException("No intended Manual bars were created.");
                    return true;
                });
                _preview = null;
                UpdateState("CREATE COMMITTED · geometry only · structural adequacy and code compliance not verified.");
                MessageBox.Show(this, "Manual Beam reinforcement was created from the accepted explicit plan. This does not verify structural design or code compliance.", "K-TOOL Manual v1", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                _preview = null;
                UpdateState("Create rolled back/blocked: " + ex.Message);
            }
        }

        private void InvalidatePreview()
        {
            if (_preview != null) _preview = null;
            _solved2D=null;_sketch?.Invalidate();
            if (_create != null) _create.Enabled = false;
            if (_state != null && !IsDisposed) UpdateState("STALE / NOT SOLVED · solve the current inputs before Create.");
        }

        private void UpdateState(string text)
        {
            _state.Text = text;
            _create.Enabled = _preview != null;
            _sketch?.Invalidate();
        }

        private void CacheSolved2D(RebarPreviewSnapshot snapshot)
        {
            var frame=BeamGeometryHelper.GetBeamProfile(_beam);
            var paths=snapshot.Components.Where(c=>c.HostId==BeamHostId(_beam)).SelectMany(c=>c.Paths).Select(path=>
                new SolvedLocalPathV1(path.Role,path.Points.Select(point=>{
                    var local=new XYZ(point.X,point.Y,point.Z)-frame.StartPoint;
                    return new SlabSectionPoint(local.DotProduct(frame.RightVector),local.DotProduct(frame.Direction),local.DotProduct(frame.UpVector));
                }))).ToArray();
            if(paths.Length==0)throw new InvalidOperationException("Accepted solver snapshot has no paths for this exact Beam.");
            _solved2D=new DetachedSolvedViewV1(paths,frame.Length/2){SectionX0=-frame.B/2,SectionZ0=-frame.H/2,SectionWidth=frame.B,SectionHeight=frame.H,
                ElevationX0=0,ElevationY0=-frame.H/2,ElevationWidth=frame.Length,ElevationHeight=frame.H,HostLabel="Beam "+_beam.Id.ToLongValue()};
            _sketch.Invalidate();
        }

        private void RefreshSketchSnapshot()
        {
            var values = new List<Tuple<BeamManualBarRole, double, double, double, double>>();
            foreach (DataGridViewRow row in _bars.Rows)
            {
                if (row.IsNewRow || !(row.Cells["Role"].Value is BeamManualBarRole role) ||
                    !(row.Cells["Enabled"].Value is bool enabled) || !enabled) continue;
                double x, y, start, finish;
                if (!TryRead(row, "X mm", out x) || !TryRead(row, "Y mm", out y) ||
                    !TryRead(row, "Start station mm", out start) || !TryRead(row, "Finish station mm", out finish)) continue;
                values.Add(Tuple.Create(role, x, y, start, finish));
            }
            _sketchBars = values.ToArray();
            var stirrupPaths = new List<Tuple<BeamManualStirrupRole, double[][], double[]>>();
            foreach (DataGridViewRow row in _stirrups.Rows)
            {
                if (row.IsNewRow || !(row.Cells["Stirrup role"].Value is BeamManualStirrupRole role) ||
                    !(row.Cells["Stirrup enabled"].Value is bool enabled) || !enabled) continue;
                try
                {
                    double[][] points = ParseSectionPath(CellText(row, "Stirrup contour"), CellText(row, "Stirrup group ID"))
                        .Select(point => new[] { point.XMm, point.YMm }).ToArray();
                    double[] stations = ParseMmList(CellText(row, "Stirrup stations"), CellText(row, "Stirrup group ID"), "stations").ToArray();
                    stirrupPaths.Add(Tuple.Create(role, points, stations));
                }
                catch (InvalidOperationException) { /* Invalid editable text is not painted as solved geometry. */ }
            }
            _sketchStirrupPaths = stirrupPaths.ToArray();
            _sketch?.Invalidate();
        }

        private static bool TryRead(DataGridViewRow row, string column, out double value) =>
            double.TryParse(CellText(row, column), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !double.IsNaN(value) && !double.IsInfinity(value);

        private void SketchPaint(object sender, PaintEventArgs e)
        {
            if(_preview!=null && _solved2D!=null){DetachedSolvedViewRendererV1.Paint(e.Graphics,_sketch.ClientRectangle,_solved2D);return;}
            // Paint uses only a detached frame and copied millimetre coordinates; no Revit API access.
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            BeamManualFrameV1 frame = _sketchFrame;
            if (frame == null || frame.WidthMm <= 0 || frame.HeightMm <= 0 || frame.LengthMm <= 0) return;
            using (var font = new Font("Segoe UI", 8F))
            using (var pen = new Pen(Color.FromArgb(75, 85, 99), 1.4F))
            using (var brush = new SolidBrush(Color.FromArgb(249, 250, 251)))
            {
                g.DrawString("MANUAL INPUT SCHEMATIC — not Revit-solved. Use Solve preview to inspect actual Rebar.", font, Brushes.DarkSlateGray, 8, 6);
                Rectangle cross = FitRectangle(new Rectangle(18, 28, Math.Max(40, _sketch.Width / 3 - 30), Math.Max(45, _sketch.Height - 44)), frame.WidthMm, frame.HeightMm);
                g.FillRectangle(brush, cross); g.DrawRectangle(pen, cross);
                g.DrawString("Section · x / y (mm)", font, Brushes.Black, cross.Left, cross.Bottom + 2);
                int elevationLeft = _sketch.Width / 3 + 24;
                Rectangle elevation = FitRectangle(new Rectangle(elevationLeft, 28, Math.Max(70, _sketch.Width - elevationLeft - 18), Math.Max(45, _sketch.Height - 44)), frame.LengthMm, frame.HeightMm);
                g.FillRectangle(brush, elevation); g.DrawRectangle(pen, elevation);
                g.DrawString("Elevation · station / y (mm)", font, Brushes.Black, elevation.Left, elevation.Bottom + 2);
                foreach (var bar in _sketchBars)
                {
                    Color color = RoleColor(bar.Item1);
                    using (var barPen = new Pen(color, 2F))
                    {
                        float px = cross.Left + (float)((bar.Item2 + frame.WidthMm / 2.0) / frame.WidthMm * cross.Width);
                        float py = cross.Bottom - (float)((bar.Item3 + frame.HeightMm / 2.0) / frame.HeightMm * cross.Height);
                        using (var dotBrush = new SolidBrush(color)) g.FillEllipse(dotBrush, px - 3, py - 3, 6, 6);
                        float x0 = elevation.Left + (float)(bar.Item4 / frame.LengthMm * elevation.Width);
                        float x1 = elevation.Left + (float)(bar.Item5 / frame.LengthMm * elevation.Width);
                        float ey = elevation.Bottom - (float)((bar.Item3 + frame.HeightMm / 2.0) / frame.HeightMm * elevation.Height);
                        g.DrawLine(barPen, x0, ey, x1, ey);
                    }
                }
                foreach (var group in _sketchStirrupPaths)
                {
                    Color color = StirrupRoleColor(group.Item1);
                    using (var tiePen = new Pen(color, 1.8F))
                    {
                        PointF[] sectionPoints = group.Item2.Select(point => new PointF(
                            cross.Left + (float)((point[0] + frame.WidthMm / 2.0) / frame.WidthMm * cross.Width),
                            cross.Bottom - (float)((point[1] + frame.HeightMm / 2.0) / frame.HeightMm * cross.Height))).ToArray();
                        if (sectionPoints.Length > 1) g.DrawLines(tiePen, sectionPoints);
                        foreach (double station in group.Item3)
                        {
                            if (station < 0 || station > frame.LengthMm) continue;
                            float sx = elevation.Left + (float)(station / frame.LengthMm * elevation.Width);
                            g.DrawLine(tiePen, sx, elevation.Top + 2, sx, elevation.Bottom - 2);
                        }
                    }
                }
            }
        }

        private static Rectangle FitRectangle(Rectangle target, double width, double height)
        {
            double scale = Math.Min(target.Width / width, target.Height / height);
            int w = Math.Max(1, (int)Math.Round(width * scale));
            int h = Math.Max(1, (int)Math.Round(height * scale));
            return new Rectangle(target.Left + (target.Width - w) / 2, target.Top + (target.Height - h) / 2, w, h);
        }

        private static Color RoleColor(BeamManualBarRole role)
        {
            switch (role)
            {
                case BeamManualBarRole.TopContinuous: return Color.FromArgb(37, 99, 235);
                case BeamManualBarRole.BottomContinuous: return Color.FromArgb(22, 163, 74);
                case BeamManualBarRole.TopLeftExtra: return Color.FromArgb(147, 51, 234);
                case BeamManualBarRole.TopRightExtra: return Color.FromArgb(192, 38, 211);
                case BeamManualBarRole.BottomMidExtra: return Color.FromArgb(234, 88, 12);
                case BeamManualBarRole.SideLeft: return Color.FromArgb(8, 145, 178);
                case BeamManualBarRole.SideRight: return Color.FromArgb(13, 148, 136);
                default: return Color.FromArgb(71, 85, 105);
            }
        }

        private static Color StirrupRoleColor(BeamManualStirrupRole role) => role == BeamManualStirrupRole.Hanger
            ? Color.FromArgb(217, 70, 239) : role == BeamManualStirrupRole.Stirrup
                ? Color.FromArgb(220, 38, 38) : Color.FromArgb(71, 85, 105);

        private static string BeamHostId(FamilyInstance beam) => beam.UniqueId;
    }
}

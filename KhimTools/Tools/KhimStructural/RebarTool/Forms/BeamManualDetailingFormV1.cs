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
    /// Narrow, explicit Manual v1 editor. Only straight longitudinal bars fully
    /// inside one verified Beam are enabled; all other detailing remains disclosed
    /// as unsupported instead of being inherited from legacy inferred settings.
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

        private readonly Document _document;
        private readonly FamilyInstance _beam;
        private readonly BeamManualRebarGeneratorV1 _generator;
        private readonly BeamManualDetailingPlanStoreV1 _store;
        private readonly List<RebarBarType> _barTypes;
        private BeamManualDetailingPlanV1 _plan;
        private RebarPreviewSnapshot _preview;
        private readonly DataGridView _bars = new DataGridView();
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
        private string _planLoadWarning;

        public BeamManualDetailingFormV1(Document document, FamilyInstance beam)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _beam = beam ?? throw new ArgumentNullException(nameof(beam));
            _generator = new BeamManualRebarGeneratorV1(document);
            _barTypes = new FilteredElementCollector(document).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                .Where(type => type.IsValidObject).OrderBy(type => type.Name, StringComparer.Ordinal).ToList();
            _plan = _generator.CreateBlankPlan(beam);
            _sketchFrame = _plan.Frame;
            _store = BeamManualDetailingPlanStoreV1.ForCurrentUser(_plan.DocumentKey, beam.UniqueId);
            ConfigureWindow();
            BuildUi();
            try { LoadSavedPlan(); }
            catch (Exception ex) { _planLoadWarning = "Saved Manual v1 plan is blocked and was not overwritten: " + ex.Message; }
            WireInvalidation();
            UpdateState(_planLoadWarning ?? "NOT SOLVED — geometry only; structural design/compliance not verified.");
        }

        private void ConfigureWindow()
        {
            Text = "Beam Manual Detailing v1 / Bố trí thép dầm thủ công v1 — geometry only / chỉ kiểm tra hình học";
            Width = 1180;
            Height = 760;
            MinimumSize = new Size(960, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new Label
            {
                AutoSize = true,
                Text = "MANUAL REINFORCEMENT DETAILING / BỐ TRÍ CỐT THÉP THỦ CÔNG — geometry validation only; structural design/compliance not verified. / Chỉ kiểm tra hình học; không xác nhận thiết kế hoặc tiêu chuẩn kết cấu.\r\n" +
                    "Host / Dầm: " + _beam.Name + " / " + _beam.Id.Value.ToString(CultureInfo.InvariantCulture) +
                    "    Frame / Hệ trục: x=Right, y=Up, s=Direction; dimensions / kích thước in mm. Plan is bound to this Beam and Revit document. / Kế hoạch gắn với dầm và tài liệu Revit này.",
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Color.FromArgb(31, 41, 55),
                Margin = new Padding(4, 4, 4, 12)
            };
            root.Controls.Add(header, 0, 0);

            ConfigureBarGrid();
            root.Controls.Add(_bars, 0, 1);

            _sketch.Dock = DockStyle.Fill;
            _sketch.BackColor = Color.White;
            _sketch.BorderStyle = BorderStyle.FixedSingle;
            _sketch.Paint += SketchPaint;
            root.Controls.Add(_sketch, 0, 2);

            var editButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            var add = new Button { Text = "Add explicit bar / Thêm thanh thép", AutoSize = true };
            add.Click += (s, e) => { _bars.Rows.Add(Guid.NewGuid().ToString("N"), true, null, null, "", "", "", "", "", null, null); InvalidatePreview(); };
            var remove = new Button { Text = "Remove selected / Xóa dòng chọn", AutoSize = true };
            remove.Click += (s, e) => { foreach (DataGridViewRow row in _bars.SelectedRows) if (!row.IsNewRow) _bars.Rows.Remove(row); InvalidatePreview(); };
            editButtons.Controls.Add(add);
            editButtons.Controls.Add(remove);
            editButtons.Controls.Add(new Label { Text = "Ends / Đầu thép: choose STRAIGHT_AT_STATION at each end. / Chọn thẳng tại trạm cho từng đầu. Returns, hooks, stirrups and support extension are unsupported. / Chưa hỗ trợ đoạn bẻ, móc, đai hoặc neo ra gối.", AutoSize = true, Padding = new Padding(8, 9, 0, 0), ForeColor = Color.DarkRed });
            root.Controls.Add(editButtons, 0, 3);

            var settings = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 3, Padding = new Padding(0, 8, 0, 4) };
            string[] faceNames = { "Top cover / Lớp bảo vệ trên (mm)", "Bottom cover / Lớp bảo vệ dưới (mm)", "Left cover / Lớp bảo vệ trái (mm)", "Right cover / Lớp bảo vệ phải (mm)", "Stirrup cover / Lớp bảo vệ ngoài đai (mm)" };
            for (int i = 0; i < _covers.Length; i++)
            {
                var cell = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(3) };
                cell.Controls.Add(new Label { Text = faceNames[i], AutoSize = true });
                _covers[i] = new TextBox { Width = 110, AccessibleName = faceNames[i] };
                cell.Controls.Add(_covers[i]);
                settings.Controls.Add(cell, i, 0);
            }
            settings.Controls.Add(new Label { Text = "Start end / Điều kiện đầu", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            settings.Controls.Add(_supportStart, 1, 1);
            settings.Controls.Add(new Label { Text = "Finish end / Điều kiện cuối", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
            settings.Controls.Add(_supportFinish, 3, 1);
            ConfigureSupportCombo(_supportStart);
            ConfigureSupportCombo(_supportFinish);
            settings.Controls.Add(new Label { Text = "Explicit face covers only; no defaults. Free ends do not verify development. / Nhập lớp bảo vệ từng mặt, không có mặc định. Đầu tự do không xác nhận chiều dài neo.", AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) }, 0, 2);
            settings.SetColumnSpan(settings.GetControlFromPosition(0, 2), 7);
            root.Controls.Add(settings, 0, 4);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
            _state.AutoSize = true;
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
            root.Controls.Add(footer, 0, 5);
            Controls.Add(root);
        }

        private void ConfigureBarGrid()
        {
            _bars.Dock = DockStyle.Fill;
            _bars.AllowUserToAddRows = true;
            _bars.AllowUserToDeleteRows = true;
            _bars.AutoGenerateColumns = false;
            _bars.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
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
            AddEndDetailColumn("Finish detail", "Finish end detail");
            _bars.DataError += (s, e) => { e.ThrowException = false; };
        }

        private void AddEndDetailColumn(string name, string header)
        {
            _bars.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = name, HeaderText = header, DataSource = new[] { "STRAIGHT_AT_STATION" },
                FlatStyle = FlatStyle.Flat, FillWeight = 105,
                ToolTipText = "This end must be explicitly set to STRAIGHT_AT_STATION. Returns and RebarHookType generation are not supported in this build."
            });
        }

        private static void ConfigureSupportCombo(ComboBox combo)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Add("Select / Chọn điều kiện đầu mút…");
            combo.Items.Add("Free end in Beam / Đầu tự do trong dầm (development not verified / chưa xác nhận neo)");
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
                combo.SelectedIndex = support.Mode == BeamManualSupportMode.UserDefinedFreeEndWithinHost ? 1 : 0;
            }
            foreach (BeamManualBarGroupV1 group in saved.BarGroups ?? new List<BeamManualBarGroupV1>())
            foreach (BeamManualBarV1 bar in group.Bars ?? new List<BeamManualBarV1>())
            {
                RebarBarType type = ResolveStoredType(group.BarType);
                int row = _bars.Rows.Add(group.GroupId, group.Enabled, group.Role, type == null ? null : ChoiceFor(type),
                    bar.XMm.ToString("R", CultureInfo.InvariantCulture), bar.YMm.ToString("R", CultureInfo.InvariantCulture),
                    bar.StartStationMm.ToString("R", CultureInfo.InvariantCulture), bar.EndStationMm.ToString("R", CultureInfo.InvariantCulture),
                    bar.LayerId, SupportedEndText(bar.StartDetail), SupportedEndText(bar.FinishDetail));
                if (type == null) _bars.Rows[row].Cells["BarType"].Value = null;
                if (bar.StartDetail.Kind != BeamManualEndDetailKind.StraightAtStation || bar.FinishDetail.Kind != BeamManualEndDetailKind.StraightAtStation)
                    _bars.Rows[row].ErrorText = "Stored return/hook end details are not editable or generatable in this supported Manual v1 slice; data was not converted.";
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

        private BarTypeChoice ChoiceFor(RebarBarType type)
        {
            var column = (DataGridViewComboBoxColumn)_bars.Columns["BarType"];
            return ((IEnumerable<BarTypeChoice>)column.DataSource).FirstOrDefault(choice => choice.Type.UniqueId == type.UniqueId);
        }

        private static string SupportedEndText(BeamManualEndDetailV1 detail) =>
            detail != null && detail.Kind == BeamManualEndDetailKind.StraightAtStation ? "STRAIGHT_AT_STATION" : null;

        private void WireInvalidation()
        {
            _bars.CellValueChanged += (s, e) => InvalidatePreview();
            _bars.RowsRemoved += (s, e) => InvalidatePreview();
            _bars.CurrentCellDirtyStateChanged += (s, e) => { if (_bars.IsCurrentCellDirty) _bars.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _bars.CellValueChanged += (s, e) => RefreshSketchSnapshot();
            _bars.RowsRemoved += (s, e) => RefreshSketchSnapshot();
            foreach (TextBox cover in _covers) cover.TextChanged += (s, e) => InvalidatePreview();
            _supportStart.SelectedIndexChanged += (s, e) => InvalidatePreview();
            _supportFinish.SelectedIndexChanged += (s, e) => InvalidatePreview();
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
                if (!string.Equals(Convert.ToString(row.Cells["Start detail"].Value, CultureInfo.InvariantCulture), "STRAIGHT_AT_STATION", StringComparison.Ordinal) ||
                    !string.Equals(Convert.ToString(row.Cells["Finish detail"].Value, CultureInfo.InvariantCulture), "STRAIGHT_AT_STATION", StringComparison.Ordinal))
                    throw new InvalidOperationException("Choose STRAIGHT_AT_STATION independently for both ends. Hooks and curve returns remain unsupported and are never converted to straight bars.");
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
                    StartDetail = StraightDetail(), FinishDetail = StraightDetail() });
            }
            plan.Covers = ReadCovers();
            if (_supportStart.SelectedIndex != 1 || _supportFinish.SelectedIndex != 1)
                throw new InvalidOperationException("Choose an explicit supported end condition for both Beam ends.");
            plan.SupportEnds = new List<BeamManualSupportEndV1>
            {
                FreeEnd(BeamManualEnd.Start), FreeEnd(BeamManualEnd.Finish)
            };
            plan.StirrupGroups = plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>();
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

        private static BeamManualEndDetailV1 StraightDetail() => new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation };

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
                    bar => roleByBar.TryGetValue(bar.Id.Value.ToString(CultureInfo.InvariantCulture), out string role) ? role : string.Empty);
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
            if (_create != null) _create.Enabled = false;
            if (_state != null && !IsDisposed) UpdateState("STALE / NOT SOLVED · solve the current inputs before Create.");
        }

        private void UpdateState(string text)
        {
            _state.Text = text;
            _create.Enabled = _preview != null;
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
            _sketch?.Invalidate();
        }

        private static bool TryRead(DataGridViewRow row, string column, out double value) =>
            double.TryParse(CellText(row, column), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !double.IsNaN(value) && !double.IsInfinity(value);

        private void SketchPaint(object sender, PaintEventArgs e)
        {
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

        private static string BeamHostId(FamilyInstance beam) => beam.UniqueId;
    }
}

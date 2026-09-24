using KhimTools.Core.UI;
using Control = System.Windows.Forms.Control;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.Core.Preview;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Models;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using ComboBox = System.Windows.Forms.ComboBox;
using CheckBox = System.Windows.Forms.CheckBox;
using GroupBox = System.Windows.Forms.GroupBox;
using NumericUpDown = System.Windows.Forms.NumericUpDown;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace KhimTools.RebarTool.Forms
{
    public class SlabReinforcementForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly List<Floor> _availableFloors;
        private readonly List<Floor> _preSelectedFloors;
        private readonly SlabPanelManager _panelManager;

        // DataGridView Controls (Right side)
        private DataGridView _gridPanels;
        private Label _lblPanelCount;
        private Button _btnSelectAll;
        private Button _btnSelectNone;
        private Button _btnDeletePanel;
        private Button _btnPickEdge;

        // Tab 1: Bottom Layer
        private CheckBox _chkBotDraw;
        private CheckBox _chkBotInvert;
        private ComboBox _cmbBotXDia;
        private NumericUpDown _numBotXSpacing;
        private ComboBox _cmbBotYDia;
        private NumericUpDown _numBotYSpacing;

        // Tab 2: Top Layer (Full Mesh)
        private CheckBox _chkTopDraw;
        private CheckBox _chkTopInvert;
        private ComboBox _cmbTopXDia;
        private NumericUpDown _numTopXSpacing;
        private ComboBox _cmbTopYDia;
        private NumericUpDown _numTopYSpacing;

        // Tab 3: Hat (Mũ Gối) & Top Distribution
        private CheckBox _chkHatDraw;
        private ComboBox _cmbHatXDia;
        private NumericUpDown _numHatXSpacing;
        private ComboBox _cmbHatYDia;
        private NumericUpDown _numHatYSpacing;
        private ComboBox _cmbHatFactor;
        private CheckBox _chkHatFullSpan;
        private CheckBox _chkHatHookDown;
        private NumericUpDown _numHatHookDownLen;

        private CheckBox _chkDistDraw;
        private ComboBox _cmbDistDia;
        private NumericUpDown _numDistSpacing;

        // Tab 4: Spacer & Anchors & Tolerances
        private CheckBox _chkSpacerDraw;
        private ComboBox _cmbSpacerDia;
        private NumericUpDown _numSpacerStepX;
        private NumericUpDown _numSpacerStepY;
        private NumericUpDown _numSpacerHookLen;

        private NumericUpDown _numBeamAnchorA;
        private NumericUpDown _numSlabAnchorB;
        private NumericUpDown _numRounding;
        private NumericUpDown _numMinSpan;

        // Tab 5: Standards & Templates
        private ComboBox _cmbDesignCode;
        private ComboBox _cmbConcreteGrade;
        private ComboBox _cmbSteelGrade;
        private ComboBox _cmbTemplates;
        private Button _btnSaveTemplate;
        private Button _btnLoadTemplate;

        // Bottom Controls
        private ComboBox _cmbLanguage;
        private Button _btnAssignData;
        private Button _btnCreateRebar;
        private Button _btnPreviewRebar;
        private Button _btnSolve3D;
        private Button _btnClose;
        private Label _lblPreviewState;
        private Label _lblPreviewTarget;
        private ComboBox _cmbPreviewPanel;
        private ComboBox _cmbPreviewView;
        private Panel _previewCanvas;
        private TabControl _workflowTabs;
        private readonly Dictionary<string, string> _previewFingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SlabPreviewGeometry> _detachedPanelGeometry = new Dictionary<string, SlabPreviewGeometry>(StringComparer.Ordinal);
        private ToolTip _previewToolTip;
        private float _previewZoom = 1f;
        private Point _previewPan = Point.Empty;
        private Point _previewPanOrigin;
        private bool _previewPanning;
        private RebarPreviewSnapshot _lastPreview;
        private readonly PreviewLifecycleSession<RebarPreviewSnapshot> _previewLifecycle = new PreviewLifecycleSession<RebarPreviewSnapshot>();
        private RebarFormGuard _formGuard;

        private sealed class SlabPreviewGeometry
        {
            public PointF[] PlanBoundary { get; set; }
            public PointF[] SectionXBoundary { get; set; }
            public PointF[] SectionYBoundary { get; set; }
            public PointF[][] PlanOpenings { get; set; }
            public PointF[][] SectionXOpenings { get; set; }
            public PointF[][] SectionYOpenings { get; set; }

            public PointF[] Boundary(int axis) => axis == 1 ? SectionXBoundary : axis == 2 ? SectionYBoundary : PlanBoundary;
            public PointF[][] Openings(int axis) => axis == 1 ? SectionXOpenings : axis == 2 ? SectionYOpenings : PlanOpenings;
        }

        public SlabReinforcementForm(Document doc, List<Floor> availableFloors, List<Floor> preSelectedFloors = null)
            : this(doc, availableFloors, preSelectedFloors, true)
        {
        }

        internal static SlabReinforcementForm CreateLayoutPreview()
        {
            return new SlabReinforcementForm(null, null, null, false);
        }

        private SlabReinforcementForm(Document doc, List<Floor> availableFloors, List<Floor> preSelectedFloors, bool loadDocument)
        {
            _doc = doc;
            _availableFloors = availableFloors ?? new List<Floor>();
            _preSelectedFloors = preSelectedFloors ?? new List<Floor>();
            _panelManager = new SlabPanelManager();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            RebarLayout.EnableFullTypeNames(this);
            if (loadDocument) PopulateBarCombos();

            // Khởi tạo danh sách panel từ sàn được chọn hoặc toàn bộ sàn
            var initialFloors = _preSelectedFloors.Any() ? _preSelectedFloors : _availableFloors;
            if (loadDocument) _panelManager.InitializeFromFloors(_doc, initialFloors);
            RefreshGridPanels();
            if (loadDocument) LoadTemplateList();
            _formGuard = RebarFormGuard.Attach(this, _btnCreateRebar,
                new RebarValidationRule(_gridPanels,
                    () => _gridPanels.Rows.Count > 0,
                    "Chọn ít nhất một panel sàn."),
                new RebarValidationRule(_chkBotDraw,
                    () => _chkBotDraw.Checked || _chkTopDraw.Checked || _chkHatDraw.Checked || _chkSpacerDraw.Checked,
                    "Bật ít nhất một lớp hoặc nhóm thép cần tạo."),
                new RebarValidationRule(_cmbBotXDia,
                    () => !_chkBotDraw.Checked || (_cmbBotXDia.SelectedIndex >= 0 && _cmbBotYDia.SelectedIndex >= 0),
                    "Chọn đủ thép đáy phương X/Y."),
                new RebarValidationRule(_cmbTopXDia,
                    () => !_chkTopDraw.Checked || (_cmbTopXDia.SelectedIndex >= 0 && _cmbTopYDia.SelectedIndex >= 0),
                    "Chọn đủ thép trên phương X/Y."),
                new RebarValidationRule(_cmbHatXDia,
                    () => !_chkHatDraw.Checked || (_cmbHatXDia.SelectedIndex >= 0 && _cmbHatYDia.SelectedIndex >= 0),
                    "Chọn đủ thép mũ phương X/Y."),
                new RebarValidationRule(_btnCreateRebar,
                    () => _previewLifecycle.State == PreviewLifecycleState.Valid && _lastPreview != null,
                    "Cập nhật Preview cho thông số hiện tại trước khi tạo thép."));
            AttachPreviewInvalidationHandlers(this);
            UpdatePreviewStateUi();
        }

        private void BuildUi()
        {
            SetFormTitle("Rebar - Sàn", "Lưới đáy, lưới trên, mũ gối và thép kê");
            Width = 1280;
            Height = 900;
            MinimumSize = new Size(1080, 820);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;

            // 1. Bottom Control Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White };
            var workflow = new Label { Text = "1  Chọn panel     2  Cấu hình lớp thép     3  Gán thông số     4  Tạo thép", AutoSize = true, Left = 16, Top = 24, ForeColor = KhimUiStyle.TextSecondary, Font = new Font("Segoe UI Semibold", 9F) };
            var lblLang = new Label { Text = "Ngôn ngữ", AutoSize = true, Left = 480, Top = 24, ForeColor = KhimUiStyle.TextSecondary };
            _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 108, Left = 550, Top = 18 };
            _cmbLanguage.Items.Add("Tiếng Việt");
            _cmbLanguage.Items.Add("English");
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;

            _btnAssignData = new Button { Text = "Gán thông số", Width = 130, Height = 38, Top = 13, Left = 620 };
            KhimUiStyle.ApplySecondaryButton(_btnAssignData);
            _btnAssignData.Click += BtnAssignData_Click;

            _btnCreateRebar = new Button { Text = "Tạo thép sàn", Width = 142, Height = 38, Top = 13, Left = 760 };
            KhimUiStyle.ApplyPrimaryButton(_btnCreateRebar, KhimUiStyle.CreateButtonBg);
            _btnCreateRebar.Click += BtnCreateRebar_Click;
            _btnPreviewRebar = new Button { Text = "CẬP NHẬT PREVIEW", Width = 170, Height = 38, Top = 13, Left = 600, AccessibleName = "Refresh slab reinforcement preview" };
            KhimUiStyle.ApplySecondaryButton(_btnPreviewRebar);
            _btnPreviewRebar.Click += BtnPreviewRebar_Click;
            _previewToolTip = new ToolTip();

            _btnSolve3D = new Button { Text = "Solve 3D", Width = 100, Height = 38, Top = 13, Left = 600, Enabled = false, AccessibleName = "Open slab 3D solver preview" };
            KhimUiStyle.ApplySecondaryButton(_btnSolve3D);
            _btnSolve3D.Click += (s, e) =>
            {
                if (_lastPreview == null || _previewLifecycle.State != PreviewLifecycleState.Valid) return;
                using (var preview = new RebarSolverPreviewForm(_lastPreview)) preview.ShowDialog(this);
            };

            _btnClose = new Button { Text = "Đóng", Width = 88, Height = 38, Top = 13, Left = 915 };
            KhimUiStyle.ApplySecondaryButton(_btnClose);
            _btnClose.Click += (s, e) => Close();

            bottomPanel.Controls.Add(workflow);
            bottomPanel.Controls.Add(lblLang);
            bottomPanel.Controls.Add(_cmbLanguage);
            bottomPanel.Controls.Add(_btnAssignData);
            bottomPanel.Controls.Add(_btnCreateRebar);
            bottomPanel.Controls.Add(_btnClose);
            var footer = RebarLayout.Footer(_cmbLanguage, _btnAssignData, _btnPreviewRebar, _btnSolve3D, _btnCreateRebar, _btnClose);
            bottomPanel.Dispose();
            Controls.Add(footer);

            // One continuous editing workspace above a persistent solver-backed host preview.
            var pnlMain = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 1, RowCount = 2 };
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 64));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
            pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var editorAndPanels = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            editorAndPanels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editorAndPanels.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            editorAndPanels.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

            // ── LEFT: TabControl (Thông số cốt thép)
            var tabControl = _workflowTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Font = new Font("Segoe UI", 9F),
                Appearance = TabAppearance.FlatButtons,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(0, 1),
                AccessibleName = "Slab role-oriented settings"
            };

            // TAB 1: 🔽 Lớp Dưới (Bottom Layer)
            var tabBottom = new TabPage("Lưới Đáy") { BackColor = KhimUiStyle.FormBg };
            BuildTabBottom(tabBottom);
            tabControl.TabPages.Add(tabBottom);

            // TAB 2: 🔼 Lớp Trên (Top Layer Full)
            var tabTop = new TabPage("Lưới Trên") { BackColor = KhimUiStyle.FormBg };
            BuildTabTop(tabTop);
            tabControl.TabPages.Add(tabTop);

            // TAB 3: 🎓 Mũ Gối & Thép Phân Bố (Hat & Top Dist)
            var tabHat = new TabPage("Mũ Gối & Phân Bố") { BackColor = KhimUiStyle.FormBg };
            BuildTabHat(tabHat);
            tabControl.TabPages.Add(tabHat);

            // TAB 4: 🪑 Spacer & Neo Cạnh (Spacer & Anchors)
            var tabAccessories = new TabPage("Spacer & Neo Cạnh") { BackColor = KhimUiStyle.FormBg };
            BuildTabAccessories(tabAccessories);
            tabControl.TabPages.Add(tabAccessories);

            // TAB 5: Tiêu Chuẩn & Template
            var tabDesign = new TabPage("Tiêu Chuẩn & Mẫu") { BackColor = KhimUiStyle.FormBg };
            BuildTabDesign(tabDesign);
            tabControl.TabPages.Add(tabDesign);

            tabControl.TabPages.Add(RebarReferenceViews.CreatePage(RebarReferenceKind.Slab));
            tabControl.TabPages.Add(RebarConfigurationPage.Create(this, _doc, RebarReferenceKind.Slab,
                RebarConfigurationField.Number("Slab.BeamAnchorMm", "Neo vào dầm (mm)", _numBeamAnchorA),
                RebarConfigurationField.Number("Slab.AdjacentAnchorMm", "Neo sàn giáp cạnh (mm)", _numSlabAnchorB),
                RebarConfigurationField.Number("Slab.HatHookMm", "Móc thép mũ (mm)", _numHatHookDownLen),
                RebarConfigurationField.Number("Slab.SpacerHookMm", "Chân con kê (mm)", _numSpacerHookLen),
                RebarConfigurationField.Number("Slab.RoundingMm", "Bước làm tròn (mm)", _numRounding),
                RebarConfigurationField.Number("Slab.BottomSpacingX", "Lưới đáy X (mm)", _numBotXSpacing),
                RebarConfigurationField.Number("Slab.BottomSpacingY", "Lưới đáy Y (mm)", _numBotYSpacing),
                RebarConfigurationField.Number("Slab.TopSpacingX", "Lưới trên X (mm)", _numTopXSpacing),
                RebarConfigurationField.Number("Slab.TopSpacingY", "Lưới trên Y (mm)", _numTopYSpacing),
                RebarConfigurationField.Number("Slab.SpacerX", "Bước con kê X (mm)", _numSpacerStepX),
                RebarConfigurationField.Number("Slab.SpacerY", "Bước con kê Y (mm)", _numSpacerStepY),
                RebarConfigurationField.Flag("Slab.TopMesh", "Tạo lưới trên", _chkTopDraw),
                RebarConfigurationField.Flag("Slab.InvertBottom", "Đảo lớp đáy X/Y", _chkBotInvert),
                RebarConfigurationField.Flag("Slab.InvertTop", "Đảo lớp trên X/Y", _chkTopInvert)));
            var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 0, 6, 0) };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var roles = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 2, 0, 2) };
            AddRoleSelector(roles, "Lưới dưới X/Y", 0);
            AddRoleSelector(roles, "Lưới trên X/Y", 1);
            AddRoleSelector(roles, "Mũ gối", 2);
            AddRoleSelector(roles, "Kê & neo", 3);
            AddRoleSelector(roles, "Thiết lập", 4);
            AddRoleSelector(roles, "Tham khảo", 5);
            AddRoleSelector(roles, "Cấu hình", 6);
            editor.Controls.Add(roles, 0, 0);
            editor.Controls.Add(tabControl, 0, 1);
            editorAndPanels.Controls.Add(editor, 0, 0);

            // ── RIGHT: Panel List DataGridView (460px)
            var pnlRight = new Panel { Dock = DockStyle.Fill };
            BuildPanelGridSection(pnlRight);
            editorAndPanels.Controls.Add(pnlRight, 0, 1);

            var previewGroup = new GroupBox { Text = "PREVIEW KỸ THUẬT SÀN — geometry đã solve từ generator sản xuất", Dock = DockStyle.Fill, Padding = new Padding(8), Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold) };
            var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var previewToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Padding = new Padding(2) };
            _cmbPreviewPanel = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Active panel shown in preview" };
            _cmbPreviewPanel.SelectedIndexChanged += (s, e) => { UpdatePreviewTargetLabel(); _previewCanvas?.Invalidate(); };
            _cmbPreviewView = new ComboBox { Width = 125, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Preview projection" };
            _cmbPreviewView.Items.AddRange(new object[] { "Mặt bằng", "Mặt cắt X", "Mặt cắt Y" });
            _cmbPreviewView.SelectedIndex = 0;
            _cmbPreviewView.SelectedIndexChanged += (s, e) => _previewCanvas?.Invalidate();
            var fitButton = new Button { Text = "Fit All", AutoSize = true, Height = 28 };
            fitButton.Click += (s, e) => { _previewZoom = 1f; _previewPan = Point.Empty; _previewCanvas?.Invalidate(); };
            var zoomInButton = new Button { Text = "+", Width = 34, Height = 28, AccessibleName = "Zoom in" };
            zoomInButton.Click += (s, e) => { _previewZoom = Math.Min(8f, _previewZoom * 1.25f); _previewCanvas?.Invalidate(); };
            var zoomOutButton = new Button { Text = "−", Width = 34, Height = 28, AccessibleName = "Zoom out" };
            zoomOutButton.Click += (s, e) => { _previewZoom = Math.Max(0.25f, _previewZoom / 1.25f); _previewCanvas?.Invalidate(); };
            _lblPreviewState = new Label { AutoSize = true, Padding = new Padding(8, 6, 2, 0), Text = "Chưa tạo Preview", ForeColor = Color.FromArgb(100, 116, 139), AccessibleName = "Slab preview state" };
            _lblPreviewTarget = new Label { AutoSize = true, Padding = new Padding(8, 6, 2, 0), ForeColor = Color.FromArgb(71, 85, 105), AccessibleName = "Preview scope" };
            previewToolbar.Controls.Add(new Label { Text = "Panel xem:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            previewToolbar.Controls.Add(_cmbPreviewPanel);
            previewToolbar.Controls.Add(_cmbPreviewView);
            previewToolbar.Controls.Add(fitButton);
            previewToolbar.Controls.Add(zoomInButton);
            previewToolbar.Controls.Add(zoomOutButton);
            previewToolbar.Controls.Add(_lblPreviewState);
            previewToolbar.Controls.Add(_lblPreviewTarget);
            _previewCanvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 250, 252), AccessibleName = "Slab plan and section preview", TabStop = true };
            _previewCanvas.Paint += PaintSlabPreview;
            _previewCanvas.Resize += (s, e) => _previewCanvas.Invalidate();
            _previewCanvas.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _previewPanning = true; _previewPanOrigin = e.Location; _previewCanvas.Cursor = Cursors.Hand; } };
            _previewCanvas.MouseMove += (s, e) => { if (_previewPanning) { _previewPan.X += e.X - _previewPanOrigin.X; _previewPan.Y += e.Y - _previewPanOrigin.Y; _previewPanOrigin = e.Location; _previewCanvas.Invalidate(); } };
            _previewCanvas.MouseUp += (s, e) => { _previewPanning = false; _previewCanvas.Cursor = Cursors.Default; };
            _previewCanvas.MouseWheel += (s, e) => { _previewZoom = Math.Max(0.25f, Math.Min(8f, _previewZoom * (e.Delta > 0 ? 1.1f : 0.9f))); _previewCanvas.Invalidate(); };
            previewLayout.Controls.Add(previewToolbar, 0, 0);
            previewLayout.Controls.Add(_previewCanvas, 0, 1);
            previewGroup.Controls.Add(previewLayout);
            pnlMain.Controls.Add(editorAndPanels, 0, 0);
            pnlMain.Controls.Add(previewGroup, 0, 1);

            Controls.Add(pnlMain);
            pnlMain.BringToFront();
            footer.SendToBack();
        }

        private void AddRoleSelector(FlowLayoutPanel host, string text, int tabIndex)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(2), Tag = tabIndex, AccessibleName = "Show " + text + " slab settings" };
            KhimUiStyle.ApplySecondaryButton(button);
            button.Click += (s, e) =>
            {
                int index = (int)((Button)s).Tag;
                if (_workflowTabs != null && index >= 0 && index < _workflowTabs.TabPages.Count)
                    _workflowTabs.SelectedIndex = index;
            };
            host.Controls.Add(button);
        }

        private void AttachPreviewInvalidationHandlers(Control root)
        {
            Control[] inputs =
            {
                _chkBotDraw, _chkBotInvert, _cmbBotXDia, _numBotXSpacing, _cmbBotYDia, _numBotYSpacing,
                _chkTopDraw, _chkTopInvert, _cmbTopXDia, _numTopXSpacing, _cmbTopYDia, _numTopYSpacing,
                _chkHatDraw, _cmbHatXDia, _numHatXSpacing, _cmbHatYDia, _numHatYSpacing, _cmbHatFactor,
                _chkHatFullSpan, _chkHatHookDown, _numHatHookDownLen, _chkDistDraw, _cmbDistDia, _numDistSpacing,
                _chkSpacerDraw, _cmbSpacerDia, _numSpacerStepX, _numSpacerStepY, _numSpacerHookLen,
                _numBeamAnchorA, _numSlabAnchorB, _numRounding, _numMinSpan,
                _cmbDesignCode, _cmbConcreteGrade, _cmbSteelGrade, _cmbTemplates
            };
            foreach (Control input in inputs)
            {
                if (input is NumericUpDown number) number.ValueChanged += (s, e) => MarkPreviewStale();
                else if (input is CheckBox check) check.CheckedChanged += (s, e) => MarkPreviewStale();
                else if (input is ComboBox combo) combo.SelectedIndexChanged += (s, e) => MarkPreviewStale();
                else if (input is TextBox text) text.TextChanged += (s, e) => MarkPreviewStale();
            }
            if (_gridPanels != null)
            {
                _gridPanels.CellValueChanged += (s, e) =>
                {
                    if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                    {
                        MarkPreviewStale();
                        GetSelectedPanelsFromGrid();
                        UpdatePanelCountLabel();
                        RefreshPreviewPanelChoices();
                        UpdatePreviewTargetLabel();
                    }
                };
                _gridPanels.CurrentCellDirtyStateChanged += (s, e) =>
                {
                    if (_gridPanels.IsCurrentCellDirty) _gridPanels.CommitEdit(DataGridViewDataErrorContexts.Commit);
                };
                _gridPanels.CurrentCellChanged += (s, e) => { UpdatePreviewTargetLabel(); _previewCanvas?.Invalidate(); };
            }
        }

        private void MarkPreviewStale()
        {
            if (_previewLifecycle.State == PreviewLifecycleState.Valid) _previewLifecycle.MarkStale();
            UpdatePreviewStateUi();
            _previewCanvas?.Invalidate();
        }

        private void UpdatePreviewStateUi()
        {
            if (_lblPreviewState == null) return;
            switch (_previewLifecycle.State)
            {
                case PreviewLifecycleState.Valid:
                    _lblPreviewState.Text = "Preview hợp lệ";
                    _lblPreviewState.ForeColor = Color.FromArgb(21, 128, 61);
                    break;
                case PreviewLifecycleState.Stale:
                    _lblPreviewState.Text = "Thông số đã thay đổi — cập nhật Preview";
                    _lblPreviewState.ForeColor = Color.FromArgb(180, 83, 9);
                    break;
                case PreviewLifecycleState.Invalid:
                    _lblPreviewState.Text = "Dữ liệu không hợp lệ";
                    _lblPreviewState.ForeColor = Color.FromArgb(185, 28, 28);
                    break;
                default:
                    _lblPreviewState.Text = "Chưa tạo Preview";
                    _lblPreviewState.ForeColor = Color.FromArgb(100, 116, 139);
                    break;
            }
            bool valid = _previewLifecycle.State == PreviewLifecycleState.Valid && _lastPreview != null;
            if (_btnCreateRebar != null)
            {
                _btnCreateRebar.Enabled = valid;
                _previewToolTip?.SetToolTip(_btnCreateRebar, valid ? "Preview hiện tại khớp với thông số." : "Cần cập nhật Preview trước khi tạo thép.");
            }
            if (_btnSolve3D != null) _btnSolve3D.Enabled = valid;
        }

        private void RefreshPreviewPanelChoices()
        {
            if (_cmbPreviewPanel == null) return;
            string prior = _cmbPreviewPanel.SelectedItem as string;
            List<SlabPanel> targets = _panelManager.Panels.Where(panel => panel.IsSelected).ToList();
            _cmbPreviewPanel.BeginUpdate();
            _cmbPreviewPanel.Items.Clear();
            foreach (SlabPanel panel in targets) _cmbPreviewPanel.Items.Add(panel.PanelId + " — " + panel.LevelName);
            int selected = -1;
            for (int i = 0; i < _cmbPreviewPanel.Items.Count; i++)
                if (string.Equals(_cmbPreviewPanel.Items[i] as string, prior, StringComparison.Ordinal)) { selected = i; break; }
            _cmbPreviewPanel.SelectedIndex = selected >= 0 ? selected : (_cmbPreviewPanel.Items.Count > 0 ? 0 : -1);
            _cmbPreviewPanel.EndUpdate();
            UpdatePreviewTargetLabel();
        }

        private SlabPanel GetActivePreviewPanel()
        {
            string choice = _cmbPreviewPanel?.SelectedItem as string;
            string id = string.IsNullOrEmpty(choice) ? null : choice.Split(new[] { " — " }, StringSplitOptions.None)[0];
            return _panelManager.Panels.FirstOrDefault(panel => panel.PanelId == id) ??
                _panelManager.Panels.FirstOrDefault(panel => panel.IsSelected) ?? _panelManager.Panels.FirstOrDefault();
        }

        private void UpdatePanelCountLabel()
        {
            if (_lblPanelCount == null) return;
            int count = _panelManager.Panels.Count(panel => panel.IsSelected);
            _lblPanelCount.Text = "Đã chọn: " + count + " / " + _panelManager.Panels.Count + " panels";
        }

        private void UpdatePreviewTargetLabel()
        {
            if (_lblPreviewTarget == null) return;
            int targetCount = _panelManager.Panels.Count(panel => panel.IsSelected);
            SlabPanel active = GetActivePreviewPanel();
            _lblPreviewTarget.Text = active == null ? "Không có panel được chọn" :
                "Xem panel đang hoạt động: " + active.PanelId + "  |  Batch: " + targetCount + " panel";
        }

        private void PaintSlabPreview(object sender, PaintEventArgs e)
        {
            Panel canvas = (Panel)sender;
            SlabPanel panel = GetActivePreviewPanel();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(canvas.BackColor);
            if (panel == null)
            {
                TextRenderer.DrawText(e.Graphics, "Chọn panel sàn để xem hình học.", Font, canvas.ClientRectangle, Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            bool plan = _cmbPreviewView == null || _cmbPreviewView.SelectedIndex == 0;
            int axis = _cmbPreviewView == null ? 0 : _cmbPreviewView.SelectedIndex;
            SlabPreviewGeometry geometry;
            _detachedPanelGeometry.TryGetValue(panel.PanelId, out geometry);
            var projectedBoundary = geometry?.Boundary(axis) ?? new PointF[0];
            var projectedOpenings = geometry?.Openings(axis) ?? new PointF[0][];
            var pathSets = new List<PointF[]>();
            string fingerprint;
            RebarPreviewComponent component = null;
            if (_lastPreview != null && _previewFingerprints.TryGetValue(panel.PanelId, out fingerprint))
                component = _lastPreview.Find(fingerprint);
            if (component != null) pathSets.AddRange(component.Paths.Select(path => ProjectPath(path, axis)).Where(points => points.Length > 1));

            PointF[] all = projectedBoundary.Concat(projectedOpenings.SelectMany(points => points)).Concat(pathSets.SelectMany(points => points)).ToArray();
            if (all.Length < 2)
            {
                TextRenderer.DrawText(e.Graphics, "Panel geometry is unavailable.", Font, canvas.ClientRectangle, Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            float minX = all.Min(point => point.X), maxX = all.Max(point => point.X);
            float minY = all.Min(point => point.Y), maxY = all.Max(point => point.Y);
            float width = Math.Max(1e-4f, maxX - minX), height = Math.Max(1e-4f, maxY - minY);
            float scale = Math.Min((canvas.ClientSize.Width - 100f) / width, (canvas.ClientSize.Height - 70f) / height);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) return;
            scale *= _previewZoom;
            float ox = (canvas.ClientSize.Width - width * scale) / 2f;
            float oy = (canvas.ClientSize.Height - height * scale) / 2f;
            PointF Map(PointF point) => new PointF(ox + _previewPan.X + (point.X - minX) * scale, oy + _previewPan.Y + (maxY - point.Y) * scale);
            using (var boundaryPen = new Pen(Color.FromArgb(51, 65, 85), 2f))
            using (var openingPen = new Pen(Color.FromArgb(220, 38, 38), 1.5f) { DashStyle = DashStyle.Dash })
            using (var barPen = new Pen(_previewLifecycle.State == PreviewLifecycleState.Valid ? Color.FromArgb(37, 99, 235) : Color.FromArgb(148, 163, 184), 1.3f))
            using (var textBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
            using (var font = new Font("Segoe UI", 9f))
            {
                if (projectedBoundary.Length > 2) e.Graphics.DrawPolygon(boundaryPen, projectedBoundary.Select(Map).ToArray());
                foreach (PointF[] opening in projectedOpenings) e.Graphics.DrawPolygon(openingPen, opening.Select(Map).ToArray());
                foreach (PointF[] path in pathSets)
                {
                    PointF[] projected = path.Select(Map).ToArray();
                    float pathWidth = projected.Max(point => point.X) - projected.Min(point => point.X);
                    float pathHeight = projected.Max(point => point.Y) - projected.Min(point => point.Y);
                    if (axis != 0 && pathWidth < 0.5f && pathHeight < 0.5f)
                    {
                        PointF center = projected[0];
                        e.Graphics.FillEllipse(barPen.Brush, center.X - 3f, center.Y - 3f, 6f, 6f);
                    }
                    else e.Graphics.DrawLines(barPen, projected);
                }
                string title = plan ? "PLAN · model axes X/Y" : axis == 1 ? "SECTION X · elevation X/Z" : "SECTION Y · elevation Y/Z";
                e.Graphics.DrawString(title, font, textBrush, 10, 8);
                string dimensions = plan ? string.Format("{0}  ·  {1:N0} × {2:N0} mm", panel.PanelId, panel.WidthMm, panel.LengthMm) :
                    string.Format("{0}  ·  thickness {1:N0} mm  ·  cover top/bottom {2:N0}/{3:N0} mm", panel.PanelId, panel.ThicknessMm,
                        panel.CoverTopFeet * 304.8, panel.CoverBottomFeet * 304.8);
                e.Graphics.DrawString(dimensions, font, textBrush, 10, canvas.ClientSize.Height - 22);
                if (component == null)
                    e.Graphics.DrawString("Geometry only — refresh to solve reinforcement centerlines.", font, textBrush, 10, 28);
                else if (_previewLifecycle.State == PreviewLifecycleState.Stale)
                    e.Graphics.DrawString("STALE · bars shown from the last valid solve", font, textBrush, 10, 28);
            }
        }

        private static PointF[][] DetachLoopViews(CurveLoop loop)
        {
            if (loop == null) return new[] { new PointF[0], new PointF[0], new PointF[0] };
            var points = loop.SelectMany(curve => curve.Tessellate()).Select(point => new[] { point.X, point.Y, point.Z }).ToArray();
            return Enumerable.Range(0, 3).Select(axis => points.Select(point => ProjectPoint(point[0], point[1], point[2], axis)).ToArray()).ToArray();
        }

        private static SlabPreviewGeometry DetachPanelGeometry(SlabPanel panel)
        {
            var openings = panel.Openings ?? new List<CurveLoop>();
            PointF[][] boundary = DetachLoopViews(panel.Boundary);
            PointF[][][] openingViews = openings.Select(DetachLoopViews).ToArray();
            return new SlabPreviewGeometry
            {
                PlanBoundary = boundary[0],
                SectionXBoundary = boundary[1],
                SectionYBoundary = boundary[2],
                PlanOpenings = openingViews.Select(views => views[0]).Where(points => points.Length > 1).ToArray(),
                SectionXOpenings = openingViews.Select(views => views[1]).Where(points => points.Length > 1).ToArray(),
                SectionYOpenings = openingViews.Select(views => views[2]).Where(points => points.Length > 1).ToArray()
            };
        }

        private static PointF[] ProjectPath(RebarPreviewPath path, int axis) =>
            path.Points.Select(point => ProjectPoint(point.X, point.Y, point.Z, axis)).ToArray();

        private static PointF ProjectPoint(double x, double y, double z, int axis) =>
            axis == 1 ? new PointF((float)x, (float)z) : axis == 2 ? new PointF((float)y, (float)z) : new PointF((float)x, (float)y);

        private void BuildTabBottom(TabPage page)
        {
            var grp = new GroupBox { Text = "Bố Trí Thép Lưới Đáy (Bottom Layer)", Left = 15, Top = 15, Width = 525, Height = 250 };
            KhimUiStyle.ApplyCardStyle(grp);

            _chkBotDraw = new CheckBox { Text = "Bật tạo thép lưới đáy (Draw Bottom)", Left = 20, Top = 30, Width = 260, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _chkBotInvert = new CheckBox { Text = "Đảo phương chịu lực (Invert Layer: Y nằm dưới X)", Left = 20, Top = 60, Width = 360, Checked = false };

            var grpX = new GroupBox { Text = "Phương X", Left = 20, Top = 95, Width = 235, Height = 135 };
            var lblDiaX = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbBotXDia = new ComboBox { Left = 110, Top = 30, Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblSpX = new Label { Text = "Khoảng rải (s):", Left = 15, Top = 75, AutoSize = true };
            _numBotXSpacing = new NumericUpDown { Left = 110, Top = 72, Width = 105, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            grpX.Controls.Add(lblDiaX);
            grpX.Controls.Add(_cmbBotXDia);
            grpX.Controls.Add(lblSpX);
            grpX.Controls.Add(_numBotXSpacing);

            var grpY = new GroupBox { Text = "Phương Y", Left = 270, Top = 95, Width = 235, Height = 135 };
            var lblDiaY = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbBotYDia = new ComboBox { Left = 110, Top = 30, Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblSpY = new Label { Text = "Khoảng rải (s):", Left = 15, Top = 75, AutoSize = true };
            _numBotYSpacing = new NumericUpDown { Left = 110, Top = 72, Width = 105, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            grpY.Controls.Add(lblDiaY);
            grpY.Controls.Add(_cmbBotYDia);
            grpY.Controls.Add(lblSpY);
            grpY.Controls.Add(_numBotYSpacing);

            grp.Controls.Add(_chkBotDraw);
            grp.Controls.Add(_chkBotInvert);
            grp.Controls.Add(grpX);
            grp.Controls.Add(grpY);
            RebarLayout.Fields(grpX, RebarLayout.Field("Đường kính", _cmbBotXDia), RebarLayout.Field("Khoảng rải s (mm)", _numBotXSpacing));
            RebarLayout.Fields(grpY, RebarLayout.Field("Đường kính", _cmbBotYDia), RebarLayout.Field("Khoảng rải s (mm)", _numBotYSpacing));
            RebarLayout.Fields(grp, new Control[] { _chkBotDraw }, new Control[] { _chkBotInvert }, new Control[] { grpX }, new Control[] { grpY });
            page.Controls.Add(grp);
            RebarLayout.Stack(page, grp);
        }

        private void BuildTabTop(TabPage page)
        {
            var grp = new GroupBox { Text = "Bố Trí Thép Lưới Trên Full Nhịp (Top Layer Mesh)", Left = 15, Top = 15, Width = 525, Height = 250 };
            KhimUiStyle.ApplyCardStyle(grp);

            _chkTopDraw = new CheckBox { Text = "Bật tạo thép lưới trên chạy full nhịp (Draw Top Mesh)", Left = 20, Top = 30, Width = 380, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _chkTopInvert = new CheckBox { Text = "Đảo phương chịu lực (Invert Layer: Y nằm ngoài X)", Left = 20, Top = 60, Width = 360, Checked = false };

            var grpX = new GroupBox { Text = "Phương X", Left = 20, Top = 95, Width = 235, Height = 135 };
            var lblDiaX = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbTopXDia = new ComboBox { Left = 110, Top = 30, Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblSpX = new Label { Text = "Khoảng rải (s):", Left = 15, Top = 75, AutoSize = true };
            _numTopXSpacing = new NumericUpDown { Left = 110, Top = 72, Width = 105, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            grpX.Controls.Add(lblDiaX);
            grpX.Controls.Add(_cmbTopXDia);
            grpX.Controls.Add(lblSpX);
            grpX.Controls.Add(_numTopXSpacing);

            var grpY = new GroupBox { Text = "Phương Y", Left = 270, Top = 95, Width = 235, Height = 135 };
            var lblDiaY = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbTopYDia = new ComboBox { Left = 110, Top = 30, Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblSpY = new Label { Text = "Khoảng rải (s):", Left = 15, Top = 75, AutoSize = true };
            _numTopYSpacing = new NumericUpDown { Left = 110, Top = 72, Width = 105, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            grpY.Controls.Add(lblDiaY);
            grpY.Controls.Add(_cmbTopYDia);
            grpY.Controls.Add(lblSpY);
            grpY.Controls.Add(_numTopYSpacing);

            grp.Controls.Add(_chkTopDraw);
            grp.Controls.Add(_chkTopInvert);
            grp.Controls.Add(grpX);
            grp.Controls.Add(grpY);
            RebarLayout.Fields(grpX, RebarLayout.Field("Đường kính", _cmbTopXDia), RebarLayout.Field("Khoảng rải s (mm)", _numTopXSpacing));
            RebarLayout.Fields(grpY, RebarLayout.Field("Đường kính", _cmbTopYDia), RebarLayout.Field("Khoảng rải s (mm)", _numTopYSpacing));
            RebarLayout.Fields(grp, new Control[] { _chkTopDraw }, new Control[] { _chkTopInvert }, new Control[] { grpX }, new Control[] { grpY });
            page.Controls.Add(grp);
            RebarLayout.Stack(page, grp);
        }

        private void BuildTabHat(TabPage page)
        {
            var grpHat = new GroupBox { Text = "Thép Mũ Gối (Hat / Reinforce)", Left = 15, Top = 10, Width = 525, Height = 280 };
            KhimUiStyle.ApplyCardStyle(grpHat);

            _chkHatDraw = new CheckBox { Text = "Bật bố trí thép mũ gối (Draw Support Hats)", Left = 20, Top = 25, Width = 300, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _chkHatFullSpan = new CheckBox { Text = "Chạy suốt nhịp (Full Span)", Left = 330, Top = 25, Width = 180, Checked = false };

            var lblFac = new Label { Text = "Tỷ lệ vươn (Hat Fac):", Left = 20, Top = 58, AutoSize = true };
            _cmbHatFactor = new ComboBox { Left = 160, Top = 55, Width = 95, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbHatFactor.Items.AddRange(new object[] { "L/4", "L/3", "L/5" });
            _cmbHatFactor.SelectedIndex = 0;

            _chkHatHookDown = new CheckBox { Text = "Bẻ móc mép biên:", Left = 270, Top = 57, Width = 130, Checked = true };
            _numHatHookDownLen = new NumericUpDown { Left = 405, Top = 55, Width = 80, Minimum = 50, Maximum = 300, Value = 100, Increment = 10 };

            var grpX = new GroupBox { Text = "Mũ Gối Phương X", Left = 20, Top = 90, Width = 235, Height = 120 };
            var lblDiaX = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbHatXDia = new ComboBox { Left = 110, Top = 30, Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblSpX = new Label { Text = "Khoảng rải (s):", Left = 15, Top = 75, AutoSize = true };
            _numHatXSpacing = new NumericUpDown { Left = 110, Top = 72, Width = 105, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            grpX.Controls.Add(lblDiaX);
            grpX.Controls.Add(_cmbHatXDia);
            grpX.Controls.Add(lblSpX);
            grpX.Controls.Add(_numHatXSpacing);

            var grpY = new GroupBox { Text = "Mũ Gối Phương Y", Left = 270, Top = 90, Width = 235, Height = 120 };
            var lblDiaY = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbHatYDia = new ComboBox { Left = 110, Top = 30, Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblSpY = new Label { Text = "Khoảng rải (s):", Left = 15, Top = 75, AutoSize = true };
            _numHatYSpacing = new NumericUpDown { Left = 110, Top = 72, Width = 105, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            grpY.Controls.Add(lblDiaY);
            grpY.Controls.Add(_cmbHatYDia);
            grpY.Controls.Add(lblSpY);
            grpY.Controls.Add(_numHatYSpacing);

            grpHat.Controls.Add(_chkHatDraw);
            grpHat.Controls.Add(_chkHatFullSpan);
            grpHat.Controls.Add(lblFac);
            grpHat.Controls.Add(_cmbHatFactor);
            grpHat.Controls.Add(_chkHatHookDown);
            grpHat.Controls.Add(_numHatHookDownLen);
            grpHat.Controls.Add(grpX);
            grpHat.Controls.Add(grpY);
            page.Controls.Add(grpHat);

            // Group Top Distribution Rebar
            var grpDist = new GroupBox { Text = "Thép Phân Bố Vuông Góc Mũ Gối (Top Distribution Rebar)", Left = 15, Top = 300, Width = 525, Height = 110 };
            KhimUiStyle.ApplyCardStyle(grpDist);
            _chkDistDraw = new CheckBox { Text = "Bật bố trí thép phân bố mũ gối", Left = 20, Top = 30, Width = 250, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblDistDia = new Label { Text = "Đường kính:", Left = 20, Top = 65, AutoSize = true };
            _cmbDistDia = new ComboBox { Left = 115, Top = 60, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblDistSp = new Label { Text = "Khoảng rải (s):", Left = 250, Top = 65, AutoSize = true };
            _numDistSpacing = new NumericUpDown { Left = 350, Top = 62, Width = 100, Minimum = 50, Maximum = 400, Value = 200, Increment = 10 };

            grpDist.Controls.Add(_chkDistDraw);
            grpDist.Controls.Add(lblDistDia);
            grpDist.Controls.Add(_cmbDistDia);
            grpDist.Controls.Add(lblDistSp);
            grpDist.Controls.Add(_numDistSpacing);
            page.Controls.Add(grpDist);
            RebarLayout.Fields(grpHat,
                new Control[] { _chkHatDraw }, new Control[] { _chkHatFullSpan },
                RebarLayout.Field("Tỷ lệ vươn", _cmbHatFactor),
                new Control[] { _chkHatHookDown },
                RebarLayout.Field("Chiều dài móc mép (mm)", _numHatHookDownLen),
                RebarLayout.Field("Đường kính X", _cmbHatXDia),
                RebarLayout.Field("Khoảng rải X (mm)", _numHatXSpacing),
                RebarLayout.Field("Đường kính Y", _cmbHatYDia),
                RebarLayout.Field("Khoảng rải Y (mm)", _numHatYSpacing));
            RebarLayout.Fields(grpDist, new Control[] { _chkDistDraw },
                RebarLayout.Field("Đường kính", _cmbDistDia), RebarLayout.Field("Khoảng rải s (mm)", _numDistSpacing));
            RebarLayout.Stack(page, grpHat, grpDist);
        }

        private void BuildTabAccessories(TabPage page)
        {
            // Group Spacer
            var grpSpacer = new GroupBox { Text = "Con Kê / Thép Chân Chó (Spacer / High Chair)", Left = 15, Top = 10, Width = 525, Height = 150 };
            KhimUiStyle.ApplyCardStyle(grpSpacer);
            _chkSpacerDraw = new CheckBox { Text = "Bật bố trí con kê / thép chân chó", Left = 20, Top = 25, Width = 280, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblSpDia = new Label { Text = "Đường kính:", Left = 20, Top = 60, AutoSize = true };
            _cmbSpacerDia = new ComboBox { Left = 110, Top = 55, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblHookLen = new Label { Text = "Chiều dài móc chân (mm):", Left = 240, Top = 60, AutoSize = true };
            _numSpacerHookLen = new NumericUpDown { Left = 400, Top = 57, Width = 90, Minimum = 50, Maximum = 300, Value = 100, Increment = 10 };

            var lblStepX = new Label { Text = "Bước X (mm):", Left = 20, Top = 100, AutoSize = true };
            _numSpacerStepX = new NumericUpDown { Left = 110, Top = 97, Width = 100, Minimum = 300, Maximum = 2000, Value = 800, Increment = 50 };
            var lblStepY = new Label { Text = "Bước Y (mm):", Left = 240, Top = 100, AutoSize = true };
            _numSpacerStepY = new NumericUpDown { Left = 400, Top = 97, Width = 90, Minimum = 300, Maximum = 2000, Value = 800, Increment = 50 };

            grpSpacer.Controls.Add(_chkSpacerDraw);
            grpSpacer.Controls.Add(lblSpDia);
            grpSpacer.Controls.Add(_cmbSpacerDia);
            grpSpacer.Controls.Add(lblHookLen);
            grpSpacer.Controls.Add(_numSpacerHookLen);
            grpSpacer.Controls.Add(lblStepX);
            grpSpacer.Controls.Add(_numSpacerStepX);
            grpSpacer.Controls.Add(lblStepY);
            grpSpacer.Controls.Add(_numSpacerStepY);
            page.Controls.Add(grpSpacer);

            // Group Anchors & Tolerances
            var grpAnchor = new GroupBox { Text = "Chiều Dài Neo & Dung Sai Nhịp", Left = 15, Top = 170, Width = 525, Height = 170 };
            KhimUiStyle.ApplyCardStyle(grpAnchor);

            var lblBeamA = new Label { Text = "Beam Anchor A (mm) [Neo Dầm]:", Left = 20, Top = 35, AutoSize = true };
            _numBeamAnchorA = new NumericUpDown { Left = 250, Top = 30, Width = 100, Minimum = 100, Maximum = 1000, Value = 250, Increment = 10 };

            var lblSlabB = new Label { Text = "Slab Anchor B (mm) [Neo Giáp Sàn]:", Left = 20, Top = 70, AutoSize = true };
            _numSlabAnchorB = new NumericUpDown { Left = 250, Top = 67, Width = 100, Minimum = 100, Maximum = 1000, Value = 300, Increment = 10 };

            var lblRound = new Label { Text = "Làm tròn chiều dài thép (mm):", Left = 20, Top = 110, AutoSize = true };
            _numRounding = new NumericUpDown { Left = 250, Top = 107, Width = 100, Minimum = 1, Maximum = 100, Value = 10, Increment = 5 };

            var lblMinSpan = new Label { Text = "Min Span ngưỡng chạy suốt (mm):", Left = 20, Top = 140, AutoSize = true };
            _numMinSpan = new NumericUpDown { Left = 250, Top = 137, Width = 100, Minimum = 500, Maximum = 3000, Value = 1200, Increment = 50 };

            grpAnchor.Controls.Add(lblBeamA);
            grpAnchor.Controls.Add(_numBeamAnchorA);
            grpAnchor.Controls.Add(lblSlabB);
            grpAnchor.Controls.Add(_numSlabAnchorB);
            grpAnchor.Controls.Add(lblRound);
            grpAnchor.Controls.Add(_numRounding);
            grpAnchor.Controls.Add(lblMinSpan);
            grpAnchor.Controls.Add(_numMinSpan);
            page.Controls.Add(grpAnchor);
            RebarLayout.Fields(grpSpacer, new Control[] { _chkSpacerDraw },
                RebarLayout.Field("Đường kính", _cmbSpacerDia),
                RebarLayout.Field("Chiều dài móc chân (mm)", _numSpacerHookLen),
                RebarLayout.Field("Bước X (mm)", _numSpacerStepX),
                RebarLayout.Field("Bước Y (mm)", _numSpacerStepY));
            RebarLayout.Fields(grpAnchor,
                RebarLayout.Field("Neo dầm A (mm)", _numBeamAnchorA),
                RebarLayout.Field("Neo giáp sàn B (mm)", _numSlabAnchorB),
                RebarLayout.Field("Làm tròn chiều dài (mm)", _numRounding),
                RebarLayout.Field("Ngưỡng nhịp chạy suốt (mm)", _numMinSpan));
            RebarLayout.Stack(page, grpSpacer, grpAnchor);
        }

        private void BuildTabDesign(TabPage page)
        {
            var grpCode = new GroupBox { Text = "Tiêu Chuẩn Thiết Kế & Vật Liệu", Left = 15, Top = 15, Width = 525, Height = 130 };
            KhimUiStyle.ApplyCardStyle(grpCode);
            var lblCode = new Label { Text = "Tiêu chuẩn neo:", Left = 15, Top = 35, AutoSize = true };
            _cmbDesignCode = new ComboBox { Left = 130, Top = 30, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDesignCode.Items.Add("TCVN 5574:2018");
            _cmbDesignCode.Items.Add("Eurocode 2");
            _cmbDesignCode.SelectedIndex = 0;

            var lblConc = new Label { Text = "Mác bê tông:", Left = 15, Top = 75, AutoSize = true };
            _cmbConcreteGrade = new ComboBox { Left = 130, Top = 70, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblSteel = new Label { Text = "Mác thép:", Left = 310, Top = 75, AutoSize = true };
            _cmbSteelGrade = new ComboBox { Left = 380, Top = 70, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };

            _cmbDesignCode.SelectedIndexChanged += (s, e) => UpdateGradeCombos();
            UpdateGradeCombos();

            grpCode.Controls.Add(lblCode);
            grpCode.Controls.Add(_cmbDesignCode);
            grpCode.Controls.Add(lblConc);
            grpCode.Controls.Add(_cmbConcreteGrade);
            grpCode.Controls.Add(lblSteel);
            grpCode.Controls.Add(_cmbSteelGrade);

            var grpTpl = new GroupBox { Text = "Quản Lý Mẫu Thiết Lập (Template JSON)", Left = 15, Top = 160, Width = 525, Height = 100 };
            KhimUiStyle.ApplyCardStyle(grpTpl);
            _cmbTemplates = new ComboBox { Left = 15, Top = 35, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _btnSaveTemplate = new Button { Text = "Lưu mẫu", Left = 290, Top = 33, Width = 100, Height = 32 };
            KhimUiStyle.ApplySecondaryButton(_btnSaveTemplate);
            _btnLoadTemplate = new Button { Text = "Nạp mẫu", Left = 400, Top = 33, Width = 100, Height = 32 };
            KhimUiStyle.ApplySecondaryButton(_btnLoadTemplate);
            _btnSaveTemplate.Click += (s, e) => SaveCurrentTemplate();
            _btnLoadTemplate.Click += (s, e) => LoadSelectedTemplate();

            grpTpl.Controls.Add(_cmbTemplates);
            grpTpl.Controls.Add(_btnSaveTemplate);
            grpTpl.Controls.Add(_btnLoadTemplate);

            page.Controls.Add(grpCode);
            page.Controls.Add(grpTpl);
            RebarLayout.Fields(grpCode, RebarLayout.Field("Tiêu chuẩn neo", _cmbDesignCode),
                RebarLayout.Field("Mác bê tông", _cmbConcreteGrade), RebarLayout.Field("Mác thép", _cmbSteelGrade));
            RebarLayout.Fields(grpTpl, RebarLayout.Field("Mẫu thiết lập", _cmbTemplates),
                new Control[] { _btnSaveTemplate, _btnLoadTemplate });
            RebarLayout.Stack(page, grpCode, grpTpl);
        }

        private void BuildPanelGridSection(Panel pnl)
        {
            var lblTitle = new Label { Text = "DANH SÁCH PANEL SÀN", Top = 5, Left = 5, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59) };

            var pnlToolBar = new Panel { Top = 30, Left = 0, Width = 465, Height = 32 };
            _btnDeletePanel = new Button { Text = "Xóa", Left = 0, Top = 0, Width = 80, Height = 30 };
            KhimUiStyle.ApplySecondaryButton(_btnDeletePanel);
            _btnDeletePanel.Click += BtnDeletePanel_Click;

            _btnPickEdge = new Button { Text = "Pick Edge", Left = 85, Top = 0, Width = 100, Height = 30 };
            KhimUiStyle.ApplySecondaryButton(_btnPickEdge);
            _btnPickEdge.Click += (s, e) => OpenEdgePickerForSelectedPanel();

            pnlToolBar.Controls.Add(_btnDeletePanel);
            pnlToolBar.Controls.Add(_btnPickEdge);

            _gridPanels = new DataGridView
            {
                Top = 68,
                Left = 0,
                Width = 465,
                Height = 415,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };
            _gridPanels.CellDoubleClick += (s, e) => OpenEdgePickerForSelectedPanel();

            var colCheck = new DataGridViewCheckBoxColumn { HeaderText = "✓", Width = 35, DataPropertyName = "IsSelected", Name = "colCheck" };
            var colId = new DataGridViewTextBoxColumn { HeaderText = "Panel", Width = 55, DataPropertyName = "PanelId", ReadOnly = true };
            var colLevel = new DataGridViewTextBoxColumn { HeaderText = "Tầng", Width = 90, DataPropertyName = "LevelName", ReadOnly = true };
            var colSize = new DataGridViewTextBoxColumn { HeaderText = "Kích thước (WxL)", Width = 140, ReadOnly = true };
            var colThick = new DataGridViewTextBoxColumn { HeaderText = "Dày (mm)", Width = 80, DataPropertyName = "ThicknessMm", ReadOnly = true };

            _gridPanels.Columns.AddRange(colCheck, colId, colLevel, colSize, colThick);

            var pnlSelection = new Panel { Top = 488, Left = 0, Width = 465, Height = 35 };
            _btnSelectAll = new Button { Text = "Chọn tất cả", Left = 0, Top = 2, Width = 90, Height = 28 };
            KhimUiStyle.ApplySecondaryButton(_btnSelectAll);
            _btnSelectAll.Click += (s, e) => SetAllSelection(true);

            _btnSelectNone = new Button { Text = "Bỏ chọn", Left = 95, Top = 2, Width = 80, Height = 28 };
            KhimUiStyle.ApplySecondaryButton(_btnSelectNone);
            _btnSelectNone.Click += (s, e) => SetAllSelection(false);

            _lblPanelCount = new Label { Text = "Đã chọn: 0 / 0 panels", Left = 185, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.DarkGreen };

            pnlSelection.Controls.Add(_btnSelectAll);
            pnlSelection.Controls.Add(_btnSelectNone);
            pnlSelection.Controls.Add(_lblPanelCount);

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(pnlToolBar);
            pnl.Controls.Add(_gridPanels);
            pnl.Controls.Add(pnlSelection);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.AutoSize = false;
            lblTitle.Height = 32;
            pnlToolBar.Dock = DockStyle.Fill;
            pnlToolBar.Height = 38;
            pnlSelection.Dock = DockStyle.Fill;
            pnlSelection.Height = 40;
            _gridPanels.Dock = DockStyle.Fill;
            _gridPanels.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.Controls.Add(lblTitle, 0, 0);
            layout.Controls.Add(pnlToolBar, 0, 1);
            layout.Controls.Add(_gridPanels, 0, 2);
            layout.Controls.Add(pnlSelection, 0, 3);
            pnl.Controls.Add(layout);
        }

        private void RefreshGridPanels()
        {
            _gridPanels.Rows.Clear();
            int selectedCount = 0;

            foreach (var p in _panelManager.Panels)
            {
                if (!_detachedPanelGeometry.ContainsKey(p.PanelId))
                    _detachedPanelGeometry[p.PanelId] = DetachPanelGeometry(p);
                int rowIdx = _gridPanels.Rows.Add();
                var row = _gridPanels.Rows[rowIdx];
                row.Cells["colCheck"].Value = p.IsSelected;
                row.Cells[1].Value = p.PanelId;
                row.Cells[2].Value = p.LevelName;
                row.Cells[3].Value = $"{p.WidthMm:N0} x {p.LengthMm:N0} mm";
                row.Cells[4].Value = $"{p.ThicknessMm:N0}";

                if (p.IsSelected) selectedCount++;
            }

            var activeIds = new HashSet<string>(_panelManager.Panels.Select(panel => panel.PanelId), StringComparer.Ordinal);
            foreach (string staleId in _detachedPanelGeometry.Keys.Where(id => !activeIds.Contains(id)).ToArray())
                _detachedPanelGeometry.Remove(staleId);

            _lblPanelCount.Text = $"Đã chọn: {selectedCount} / {_panelManager.Panels.Count} panels";
            RefreshPreviewPanelChoices();
        }

        private void SetAllSelection(bool select)
        {
            MarkPreviewStale();
            foreach (var p in _panelManager.Panels) p.IsSelected = select;
            RefreshGridPanels();
            RefreshPreviewPanelChoices();
        }

        private void BtnMergeSelected_Click(object sender, EventArgs e)
        {
            var selectedIds = new List<string>();
            for (int i = 0; i < _gridPanels.Rows.Count; i++)
            {
                bool isChecked = Convert.ToBoolean(_gridPanels.Rows[i].Cells["colCheck"].Value);
                if (isChecked)
                {
                    selectedIds.Add(_panelManager.Panels[i].PanelId);
                }
            }

            if (selectedIds.Count < 2)
            {
                KhimDialogHelper.ShowWarning("Vui lòng chọn ít nhất 2 Panel để thực hiện gộp (Merge).");
                return;
            }

            if (_panelManager.MergeSelectedPanels(selectedIds))
            {
                MarkPreviewStale();
                _detachedPanelGeometry.Clear();
                RefreshGridPanels();
                KhimDialogHelper.ShowInfo($"Đã gộp thành công {selectedIds.Count} panels thành 1 panel liên tục.");
            }
        }

        private void BtnDeletePanel_Click(object sender, EventArgs e)
        {
            var selectedIds = new List<string>();
            for (int i = 0; i < _gridPanels.Rows.Count; i++)
            {
                bool isChecked = Convert.ToBoolean(_gridPanels.Rows[i].Cells["colCheck"].Value);
                if (isChecked)
                {
                    selectedIds.Add(_panelManager.Panels[i].PanelId);
                }
            }

            if (!selectedIds.Any())
            {
                KhimDialogHelper.ShowWarning("Vui lòng chọn ít nhất 1 Panel để xóa khỏi danh sách.");
                return;
            }

            _panelManager.DeletePanels(selectedIds);
            MarkPreviewStale();
            RefreshGridPanels();
        }

        private void OpenEdgePickerForSelectedPanel()
        {
            if (_gridPanels.SelectedRows.Count == 0 && _panelManager.Panels.Any())
            {
                _gridPanels.Rows[0].Selected = true;
            }

            if (_gridPanels.SelectedRows.Count > 0)
            {
                int idx = _gridPanels.SelectedRows[0].Index;
                if (idx >= 0 && idx < _panelManager.Panels.Count)
                {
                    var panel = _panelManager.Panels[idx];
                    using (var picker = new SlabEdgePickerForm(panel))
                    {
                        picker.ShowDialog(this);
                    }
                    MarkPreviewStale();
                    _previewCanvas?.Invalidate();
                }
            }
            else
            {
                KhimDialogHelper.ShowWarning("Vui lòng chọn 1 Panel trong bảng để cấu hình từng cạnh.");
            }
        }

        private void BtnAssignData_Click(object sender, EventArgs e)
        {
            List<SlabPanel> selectedPanels = GetSelectedPanelsFromGrid();
            AssignSettingsToPanels(selectedPanels);
            KhimDialogHelper.ShowInfo($"Đã gán thành công thông số cấu hình cốt thép cho {selectedPanels.Count} panel.");
        }

        private List<SlabPanel> GetSelectedPanelsFromGrid()
        {
            for (int i = 0; i < _gridPanels.Rows.Count && i < _panelManager.Panels.Count; i++)
                _panelManager.Panels[i].IsSelected = Convert.ToBoolean(_gridPanels.Rows[i].Cells["colCheck"].Value);
            return _panelManager.Panels.Where(panel => panel.IsSelected).ToList();
        }

        private void AssignSettingsToPanels(IList<SlabPanel> selectedPanels)
        {
            foreach (SlabPanel panel in selectedPanels)
            {
                var cfg = panel.Config;

                    // Bottom Layer
                    cfg.BottomLayer.Enabled = _chkBotDraw.Checked;
                    cfg.BottomLayer.InvertLayer = _chkBotInvert.Checked;
                    cfg.BottomLayer.DiaXLabel = _cmbBotXDia.Text;
                    cfg.BottomLayer.SpacingXMm = (double)_numBotXSpacing.Value;
                    cfg.BottomLayer.DiaYLabel = _cmbBotYDia.Text;
                    cfg.BottomLayer.SpacingYMm = (double)_numBotYSpacing.Value;

                    // Top Layer
                    cfg.TopLayer.Enabled = _chkTopDraw.Checked;
                    cfg.TopLayer.InvertLayer = _chkTopInvert.Checked;
                    cfg.TopLayer.DiaXLabel = _cmbTopXDia.Text;
                    cfg.TopLayer.SpacingXMm = (double)_numTopXSpacing.Value;
                    cfg.TopLayer.DiaYLabel = _cmbTopYDia.Text;
                    cfg.TopLayer.SpacingYMm = (double)_numTopYSpacing.Value;

                    // Hat
                    cfg.HatReinforce.Enabled = _chkHatDraw.Checked;
                    cfg.HatReinforce.DiaXLabel = _cmbHatXDia.Text;
                    cfg.HatReinforce.SpacingXMm = (double)_numHatXSpacing.Value;
                    cfg.HatReinforce.DiaYLabel = _cmbHatYDia.Text;
                    cfg.HatReinforce.SpacingYMm = (double)_numHatYSpacing.Value;
                    cfg.HatReinforce.IsFullSpan = _chkHatFullSpan.Checked;
                    cfg.HatReinforce.HatFactor = _cmbHatFactor.Text;
                    cfg.HatReinforce.HookDownEdge = _chkHatHookDown.Checked;
                    cfg.HatReinforce.HookDownLenMm = (double)_numHatHookDownLen.Value;

                    // Top Dist
                    cfg.TopDistribution.Enabled = _chkDistDraw.Checked;
                    cfg.TopDistribution.DiaLabel = _cmbDistDia.Text;
                    cfg.TopDistribution.SpacingMm = (double)_numDistSpacing.Value;

                    // Spacer
                    cfg.Spacer.Enabled = _chkSpacerDraw.Checked;
                    cfg.Spacer.DiaLabel = _cmbSpacerDia.Text;
                    cfg.Spacer.StepXMm = (double)_numSpacerStepX.Value;
                    cfg.Spacer.StepYMm = (double)_numSpacerStepY.Value;
                    cfg.Spacer.HookLenMm = (double)_numSpacerHookLen.Value;

                    // Anchors & Tolerances
                    cfg.Anchors.BeamAnchorAMm = (double)_numBeamAnchorA.Value;
                    cfg.Anchors.SlabAnchorBMm = (double)_numSlabAnchorB.Value;
                    cfg.Tolerances.RoundingMm = (double)_numRounding.Value;
                    cfg.Tolerances.MinSpanMm = (double)_numMinSpan.Value;

            }
        }

        private void BtnPreviewRebar_Click(object sender, EventArgs e)
        {
            try
            {
                List<SlabPanel> selectedPanels = GetSelectedPanelsFromGrid();
                if (selectedPanels.Count == 0) throw new InvalidOperationException("Select at least one slab panel before previewing.");
                AssignSettingsToPanels(selectedPanels);
                var generator = new SlabRebarGenerator(_doc);
                _previewFingerprints.Clear();
                RebarPreviewRequest[] requests = selectedPanels.Select(panel =>
                {
                    string fingerprint = generator.GetPanelInputFingerprint(panel);
                    _previewFingerprints[panel.PanelId] = fingerprint;
                    return new RebarPreviewRequest(fingerprint, () =>
                    {
                        var report = new RebarGenerationReport();
                        List<Rebar> bars = generator.GeneratePanel(panel, report);
                        if (report.HasErrors) throw new InvalidOperationException(report.Errors[0].ErrorReason);
                        return bars;
                    }, () => generator.GetPanelInputFingerprint(panel), RebarPreviewService.Describe(panel, generator.BarTypes));
                }).ToArray();
                _previewLifecycle.BeginGeneration();
                UpdatePreviewStateUi();
                RebarPreviewSnapshot snapshot = RebarPreviewService.Capture(_doc, requests);
                _lastPreview = snapshot;
                _previewLifecycle.Complete(snapshot, snapshot.PlanFingerprint);
                UpdatePreviewStateUi();
                UpdatePreviewTargetLabel();
                _previewCanvas?.Invalidate();
            }
            catch (Exception ex)
            {
                _lastPreview = null;
                _previewFingerprints.Clear();
                _previewLifecycle.Invalidate();
                UpdatePreviewStateUi();
                _previewCanvas?.Invalidate();
                KhimDialogHelper.ShowError("Unable to create solver-backed slab preview: " + ex.Message);
            }
        }

        private void BtnCreateRebar_Click(object sender, EventArgs e)
        {
            var selectedPanels = GetSelectedPanelsFromGrid();
            if (!selectedPanels.Any())
            {
                KhimDialogHelper.ShowWarning("Vui lòng chọn ít nhất 1 Panel sàn để tạo thép.");
                return;
            }

            AssignSettingsToPanels(selectedPanels);
            var generator = new SlabRebarGenerator(_doc);
            string currentFingerprint = RebarPreviewService.FingerprintInputs(selectedPanels.Select(generator.GetPanelInputFingerprint));
            RebarPreviewSnapshot acceptedPreview;
            if (_lastPreview == null || !_previewLifecycle.TryGetValid(currentFingerprint, out acceptedPreview) || selectedPanels.Any(panel =>
                acceptedPreview.Find(generator.GetPanelInputFingerprint(panel)) == null))
            {
                KhimDialogHelper.ShowWarning("Create or refresh the solver-backed preview for the current slab panels and settings before generating.");
                return;
            }
            foreach (SlabPanel panel in selectedPanels)
                if (RebarPreviewService.HasExistingDuplicateBar(_doc, panel.HostFloor, acceptedPreview, generator.GetPanelInputFingerprint(panel)))
                {
                    KhimDialogHelper.ShowWarning("Equivalent reinforcement already exists on slab " + panel.HostFloor.Id + ". Remove or edit existing bars before generating to avoid duplicates.");
                    return;
                }

            var report = new RebarGenerationReport();

            try
            {
                TransactionBoundary.Execute(_doc, "KHIM TOOLS — Tạo Thép Sàn Theo Panel", () =>
                {
                    // Lặp qua từng panel và sinh thép theo cấu hình riêng của panel đó
                    foreach (var panel in selectedPanels)
                    {
                        List<Rebar> generated = generator.GeneratePanel(panel, report);
                        _doc.Regenerate();
                        if (report.HasErrors || !RebarPreviewService.Matches(acceptedPreview, generator.GetPanelInputFingerprint(panel), generated))
                            throw new InvalidOperationException("Generated slab centerlines differ from the accepted solver preview, or a generator error occurred; the entire slab transaction was rolled back.");
                    }
                }, configure: transaction =>
                {
                    FailureHandlingOptions failOptions = transaction.GetFailureHandlingOptions();
                    failOptions.SetFailuresPreprocessor(new KhimTools.Core.Revit.Failures.KnownWarningFailurePreprocessor());
                    transaction.SetFailureHandlingOptions(failOptions);
                });
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError($"Lỗi khi tạo thép sàn: {ex.Message}");
                _lastPreview = null;
                _previewLifecycle.Invalidate();
                return;
            }

            KhimDialogHelper.ShowRebarGenerationReport(report, "Tạo Thép Sàn (Slab Rebar Panel System)", selectedPanels.Count);
            _lastPreview = null;
            _previewLifecycle.Invalidate();
        }

        private void PopulateBarCombos()
        {
            var barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .Select(b => b.Name)
                .OrderBy(n => n)
                .ToList();

            if (!barTypes.Any()) barTypes = new List<string> { "d6", "d8", "d10", "d12", "d14", "d16" };

            PopulateCombo(_cmbBotXDia, barTypes, "10");
            PopulateCombo(_cmbBotYDia, barTypes, "10");
            PopulateCombo(_cmbTopXDia, barTypes, "10");
            PopulateCombo(_cmbTopYDia, barTypes, "10");
            PopulateCombo(_cmbHatXDia, barTypes, "10");
            PopulateCombo(_cmbHatYDia, barTypes, "10");
            PopulateCombo(_cmbDistDia, barTypes, "8");
            PopulateCombo(_cmbSpacerDia, barTypes, "10");
        }

        private void PopulateCombo(ComboBox cmb, List<string> items, string defaultDia)
        {
            cmb.Items.Clear();
            foreach (var item in items) cmb.Items.Add(item);

            int matchIdx = -1;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i].ToString().Contains(defaultDia)) { matchIdx = i; break; }
            }
            cmb.SelectedIndex = (matchIdx >= 0) ? matchIdx : (cmb.Items.Count > 0 ? 0 : -1);
        }

        private void UpdateGradeCombos()
        {
            bool isEurocode = _cmbDesignCode.SelectedIndex == 1;
            _cmbConcreteGrade.Items.Clear();
            _cmbSteelGrade.Items.Clear();

            if (isEurocode)
            {
                _cmbConcreteGrade.Items.AddRange(new object[] { "C20/25", "C25/30", "C30/37", "C35/45", "C40/50" });
                _cmbSteelGrade.Items.AddRange(new object[] { "B400", "B500" });
            }
            else
            {
                _cmbConcreteGrade.Items.AddRange(new object[] { "B15", "B20", "B25", "B30", "B35", "B40", "B45", "B50" });
                _cmbSteelGrade.Items.AddRange(new object[] { "CB240-T", "CB300-V", "CB400-V", "CB500-V" });
            }

            _cmbConcreteGrade.SelectedIndex = 1;
            _cmbSteelGrade.SelectedIndex = Math.Min(1, _cmbSteelGrade.Items.Count - 1);
        }

        private void LoadTemplateList()
        {
            _cmbTemplates.Items.Clear();
            var templates = SlabRebarSettings.GetSavedTemplateNames();
            foreach (string t in templates) _cmbTemplates.Items.Add(t);
            if (_cmbTemplates.Items.Count > 0) _cmbTemplates.SelectedIndex = 0;
        }

        private void SaveCurrentTemplate()
        {
            var settings = CaptureTemplateSettings();
            string name = _cmbTemplates.Text;
            if (string.IsNullOrWhiteSpace(name)) name = settings.TemplateName;

            if (!SlabRebarSettings.SaveTemplate(settings, name))
            {
                KhimDialogHelper.ShowError("Không thể lưu template sàn.");
                return;
            }

            LoadTemplateList();
            _cmbTemplates.SelectedItem = name;
            KhimDialogHelper.ShowInfo("Đã lưu template: " + name);
        }

        private void LoadSelectedTemplate()
        {
            var settings = SlabRebarSettings.LoadTemplate(_cmbTemplates.Text);
            if (settings == null)
            {
                KhimDialogHelper.ShowError("Không thể nạp template đã chọn.");
                return;
            }

            _cmbBotXDia.Text = settings.BotXDiaLabel;
            _numBotXSpacing.Value = Clamp(_numBotXSpacing, settings.BotXSpacingMm);
            _cmbBotYDia.Text = settings.BotYDiaLabel;
            _numBotYSpacing.Value = Clamp(_numBotYSpacing, settings.BotYSpacingMm);
            _cmbHatXDia.Text = settings.TopXDiaLabel;
            _numHatXSpacing.Value = Clamp(_numHatXSpacing, settings.TopXSpacingMm);
            _cmbHatYDia.Text = settings.TopYDiaLabel;
            _numHatYSpacing.Value = Clamp(_numHatYSpacing, settings.TopYSpacingMm);
            _cmbHatFactor.Text = settings.TopExtensionRatio;
            _chkHatHookDown.Checked = settings.TopHookDown;
            _numHatHookDownLen.Value = Clamp(_numHatHookDownLen, settings.TopHookTailMm);
            _chkSpacerDraw.Checked = settings.EnableChairRebar;
            _cmbSpacerDia.Text = settings.ChairDiaLabel;
            _numSpacerStepX.Value = Clamp(_numSpacerStepX, settings.ChairSpacingXmm);
            _numSpacerStepY.Value = Clamp(_numSpacerStepY, settings.ChairSpacingYmm);
            _cmbDesignCode.Text = settings.DesignCode;
            _cmbConcreteGrade.Text = settings.ConcreteGrade;
            _cmbSteelGrade.Text = settings.SteelGrade;
            KhimDialogHelper.ShowInfo("Đã nạp template: " + settings.TemplateName);
        }

        private SlabRebarSettings CaptureTemplateSettings()
        {
            return new SlabRebarSettings
            {
                TemplateName = string.IsNullOrWhiteSpace(_cmbTemplates.Text)
                    ? "Mặc định Sàn 2 Lớp (150mm)"
                    : _cmbTemplates.Text,
                BotXDiaLabel = _cmbBotXDia.Text,
                BotXSpacingMm = (double)_numBotXSpacing.Value,
                BotYDiaLabel = _cmbBotYDia.Text,
                BotYSpacingMm = (double)_numBotYSpacing.Value,
                TopXDiaLabel = _cmbHatXDia.Text,
                TopXSpacingMm = (double)_numHatXSpacing.Value,
                TopYDiaLabel = _cmbHatYDia.Text,
                TopYSpacingMm = (double)_numHatYSpacing.Value,
                TopExtensionRatio = _cmbHatFactor.Text,
                TopHookDown = _chkHatHookDown.Checked,
                TopHookTailMm = (double)_numHatHookDownLen.Value,
                EnableChairRebar = _chkSpacerDraw.Checked,
                ChairDiaLabel = _cmbSpacerDia.Text,
                ChairSpacingXmm = (double)_numSpacerStepX.Value,
                ChairSpacingYmm = (double)_numSpacerStepY.Value,
                DesignCode = _cmbDesignCode.Text,
                ConcreteGrade = _cmbConcreteGrade.Text,
                SteelGrade = _cmbSteelGrade.Text
            };
        }

        private static decimal Clamp(NumericUpDown control, double value)
        {
            decimal result = (decimal)value;
            return Math.Max(control.Minimum, Math.Min(control.Maximum, result));
        }
    }
}

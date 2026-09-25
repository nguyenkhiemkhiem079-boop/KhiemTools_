using KhimTools.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.Core.Preview;
using KhimTools.RebarTool.Core;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Control = System.Windows.Forms.Control;
using Button = System.Windows.Forms.Button;
using GroupBox = System.Windows.Forms.GroupBox;
using Label = System.Windows.Forms.Label;
using CheckBox = System.Windows.Forms.CheckBox;
using ComboBox = System.Windows.Forms.ComboBox;
using ListBox = System.Windows.Forms.ListBox;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>
    /// Form "Multi-Column Rebar 2.0" cao cấp cho Cột Vuông / Chữ Nhật.
    /// Tự động giữ và highlight 100% danh sách cột đã chọn trước trong Revit viewport.
    /// </summary>
    public class RectangularColumnReinforcementForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableColumns;
        private readonly List<FamilyInstance> _preSelectedColumns;

        // UI Controls
        private ListBox _columnListBox;
        private Label _lblSelectedCount;
        private Label _lblPreviewState;
        private Panel _previewPanel;
        private TabControl _workflowTabs;
        private readonly List<Button> _workflowNavigationButtons = new List<Button>();
        private bool _isPopulatingColumnList;
        private double _previewWidthMm;
        private double _previewDepthMm;
        private double _previewHeightMm;
        private double _previewMainBarDiameterMm;
        private double _previewStirrupDiameterMm;
        private string _previewMark = "<not set>";
        private string _previewLevelName;
        private string _previewHostError;
        private string ColumnSchematicDisclosure => LanguageManager.IsEnglish
            ? "CONFIG SKETCH · NOT SOLVER OUTPUT"
            : "SƠ ĐỒ MINH HỌA · KHÔNG PHẢI KẾT QUẢ GIẢI";

        // Tab 1: Thép Chủ & Cover
        private NumericUpDown _numBarsB;
        private NumericUpDown _numBarsH;
        private ComboBox _cmbMainDia;
        private CheckBox _chkTopAnchor;
        private RadioButton _rdBaseFoundation;
        private RadioButton _rdBaseStandardLevel;
        private CheckBox _chkCrankedSplice;
        private NumericUpDown _numLapMultiplier;
        private CheckBox _chkStaggeredSplice;

        // Concrete Cover
        private CheckBox _chkCustomCover;
        private NumericUpDown _numCustomCover;

        // Tab 2: Thép Đai
        private ComboBox _cmbStirrupDia;
        private NumericUpDown _numStirrupSpacingA1;
        private NumericUpDown _numStirrupSpacingA2;
        private NumericUpDown _numZoneA1Length;
        private CheckBox _chkInnerDiamond;
        private CheckBox _chkCrossLinks;

        // Scope Filter
        private RadioButton _rdScopeSelected;
        private RadioButton _rdScopeAll;

        // General Settings Controls (Image 3 match)
        private RadioButton _rdHookLengthFixed;
        private RadioButton _rdHookLengthDia;
        private NumericUpDown _numHookFixedMm;
        private NumericUpDown _numHookDiaxD;

        private NumericUpDown _numBendConditionE;
        private NumericUpDown _numBendRatioHd;

        private RadioButton _rdTopRoofHook;
        private RadioButton _rdTopRoofContinue;

        private NumericUpDown _numSpliceDistBase;
        private RadioButton _rdSpliceTwoPos;

        private CheckBox _chkAssignElevation;
        private CheckBox _chkAssignPartition;

        private NumericUpDown _numDefaultBeamHd;

        // Tab 4: Bản vẽ & View
        private CheckBox _chkAutoDrawing;
        private CheckBox _chkAutoSection3D;
        private Button _btnCreateRebar;
        private Button _btnPreview3D;
        private Button _btnClose;
        private RebarPreviewSnapshot _lastPreview;
        private readonly PreviewLifecycleSession<RebarPreviewSnapshot> _previewLifecycle = new PreviewLifecycleSession<RebarPreviewSnapshot>();

        private ComboBox _cmbLanguage;
        private Label _lblColTitle;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;

        // Configuration Templates
        private Label _lblTemplate;
        private ComboBox _cmbTemplate;
        private Button _btnSaveTemplate;
        private Button _btnApplyTemplate;
        private Button _btnDeleteTemplate;

        private TabPage _tabMain;
        private TabPage _tabStirrup;
        private TabPage _tabGenSettings;
        private TabPage _tabViews;

        private GroupBox _grpMainSection;
        private Label _lblBarsB;
        private Label _lblBarsH;
        private Label _lblMainDia;

        private GroupBox _grpCover;
        private Label _lblCustomCover;
        private Button _btnProjectCover;
        private GroupBox _grpMainAnchor;
        private GroupBox _grpStirrupZone;
        private Label _lblStirrupDia;
        private Label _lblStirrupA1;
        private Label _lblStirrupA2;
        private Label _lblZoneA1Len;
        private GroupBox _grpInnerStirrup;

        private GroupBox _grpHook;
        private GroupBox _grpBendCut;
        private Label _lblBendE;
        private Label _lblBendRatio;
        private GroupBox _grpTopRoof;
        private GroupBox _grpSplicePos;
        private Label _lblSpliceDist;
        private GroupBox _grpAssignInfo;
        private GroupBox _grpSlabBeam;
        private Label _lblDefaultHd;
        private GroupBox _grpViews;
        private RebarFormGuard _formGuard;

        public RectangularColumnReinforcementForm(Document doc, List<FamilyInstance> availableColumns, List<FamilyInstance> preSelectedColumns = null)
            : this(doc, availableColumns, preSelectedColumns, true)
        {
        }

        internal static RectangularColumnReinforcementForm CreateLayoutPreview()
        {
            return new RectangularColumnReinforcementForm(null, null, null, false);
        }

        private RectangularColumnReinforcementForm(Document doc, List<FamilyInstance> availableColumns, List<FamilyInstance> preSelectedColumns, bool loadDocument)
        {
            _doc = doc;
            _availableColumns = availableColumns ?? new List<FamilyInstance>();
            _preSelectedColumns = preSelectedColumns ?? new List<FamilyInstance>();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            RebarLayout.EnableFullTypeNames(this);
            PopulateColumnList();
            if (loadDocument)
            {
                PopulateBarTypeCombos();
                LoadTemplateList();
            }
            ApplyLanguage();
            _formGuard = RebarFormGuard.Attach(this, _btnCreateRebar,
                RebarFormGuard.RequireSelection(_columnListBox, "Chọn ít nhất một cột."),
                RebarFormGuard.RequireCombo(_cmbMainDia, "Chọn loại thép chủ."),
                RebarFormGuard.RequireCombo(_cmbStirrupDia, "Chọn loại thép đai."),
                new RebarValidationRule(_numStirrupSpacingA1,
                    () => _numStirrupSpacingA1.Value <= _numStirrupSpacingA2.Value,
                    "Khoảng cách đai vùng A1 phải nhỏ hơn hoặc bằng A2."),
                new RebarValidationRule(_numBarsB,
                    () => GetSelectedTieLayoutType() != ColumnTieLayoutType.MultiCellClosed || _numBarsB.Value >= 5,
                    "Bố trí đai đa ô cần ít nhất 5 thanh chủ theo cạnh B."),
                new RebarValidationRule(_btnCreateRebar,
                    () => _previewLifecycle.State == PreviewLifecycleState.Valid && _lastPreview != null,
                    "Giải Preview cho cột và thông số hiện tại trước khi tạo thép."));
        }

        private void BuildUi()
        {
            SetFormTitle("Rebar - Cột chữ nhật", "Thép chủ, đai, neo và liên kết giữa các tầng");
            Width = 1080;
            Height = 760;
            MinimumSize = new Size(920, 680);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;

            // 1. Bottom Control Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12) };
            var workflowStatus = new Label
            {
                Text = "1  Chọn cột     2  Nhập thông số     3  Kiểm tra     4  Tạo thép",
                AutoSize = true,
                Left = 16,
                Top = 23,
                ForeColor = KhimUiStyle.TextSecondary,
                Font = new Font("Segoe UI Semibold", 9F)
            };
            var lblLang = new Label { Text = "Ngôn ngữ", AutoSize = true, Left = 430, Top = 24, ForeColor = KhimUiStyle.TextSecondary };
            _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 108, Left = 500, Top = 18, Font = new Font("Segoe UI", 8.5F) };
            _cmbLanguage.Items.Add("Tiếng Việt");
            _cmbLanguage.Items.Add("English");
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;
            _cmbLanguage.SelectedIndexChanged += (s, e) =>
            {
                LanguageManager.CurrentLanguage = _cmbLanguage.SelectedIndex == 1 ? AppLanguage.English : AppLanguage.Vietnamese;
                ApplyLanguage();
            };

            _btnCreateRebar = new Button
            {
                Text = "Tạo thép",
                Width = 142,
                Height = 38,
                Top = 13
            };
            KhimUiStyle.ApplyPrimaryButton(_btnCreateRebar, KhimUiStyle.CreateButtonBg);

            _btnClose = new Button
            {
                Text = "Đóng",
                Width = 88,
                Height = 38,
                Top = 13
            };
            _btnPreview3D = new Button { Text = "Solve preview", Width = 132, Height = 38, Enabled = _doc != null };
            KhimUiStyle.ApplySecondaryButton(_btnClose);

            _btnCreateRebar.Click += BtnCreateRebar_Click;
            _btnPreview3D.Click += BtnPreview3D_Click;
            _btnClose.Click += (s, e) => Close();

            bottomPanel.Controls.Add(workflowStatus);
            bottomPanel.Controls.Add(lblLang);
            bottomPanel.Controls.Add(_cmbLanguage);
            bottomPanel.Controls.Add(_btnCreateRebar);
            bottomPanel.Controls.Add(_btnClose);
            bottomPanel.Resize += (s, e) =>
            {
                _btnClose.Left = bottomPanel.Width - _btnClose.Width - 15;
                _btnCreateRebar.Left = _btnClose.Left - _btnCreateRebar.Width - 10;
            };
            var footer = RebarLayout.Footer(_cmbLanguage, _btnPreview3D, _btnCreateRebar, _btnClose);
            bottomPanel.Dispose();
            Controls.Add(footer);

            // 2. Right Column Selection Panel
            var rightPanel = new Panel { Dock = DockStyle.Right, Width = 270, Padding = new Padding(14), BackColor = Color.White };
            _lblColTitle = new Label { Text = "CẤU KIỆN ÁP DỤNG", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI Semibold", 9F), ForeColor = KhimUiStyle.TextSecondary };

            var scopePanel = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = KhimUiStyle.SecondaryButtonBg, Padding = new Padding(8) };
            _rdScopeSelected = new RadioButton { Text = $"Cột đang chọn ({_preSelectedColumns.Count})", Checked = _preSelectedColumns.Any(), AutoSize = true, Top = 7, Left = 8, Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = KhimUiStyle.SelectionText };
            _rdScopeAll = new RadioButton { Text = $"Tất cả cột trong model ({_availableColumns.Count})", Checked = !_preSelectedColumns.Any(), AutoSize = true, Top = 33, Left = 8, Font = new Font("Segoe UI", 8.5F) };

            _rdScopeSelected.CheckedChanged += (s, e) => PopulateColumnList();
            _rdScopeAll.CheckedChanged += (s, e) => PopulateColumnList();

            scopePanel.Controls.Add(_rdScopeSelected);
            scopePanel.Controls.Add(_rdScopeAll);

            _lblSelectedCount = new Label { Text = "0 cột được chọn", Dock = DockStyle.Bottom, Height = 30, ForeColor = Color.FromArgb(22, 101, 52), Font = new Font("Segoe UI Semibold", 8.5F), TextAlign = ContentAlignment.MiddleLeft };

            var selectButtonsPanel = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            _btnSelectAll = new Button { Text = "Chọn tất cả", Width = 112, Height = 32, Top = 6, Left = 0 };
            _btnDeselectAll = new Button { Text = "Bỏ chọn", Width = 102, Height = 32, Top = 6, Left = 120 };
            KhimUiStyle.ApplySecondaryButton(_btnSelectAll);
            KhimUiStyle.ApplySecondaryButton(_btnDeselectAll);

            _btnSelectAll.Click += (s, e) => SetAllColumnsSelected(true);
            _btnDeselectAll.Click += (s, e) => SetAllColumnsSelected(false);

            selectButtonsPanel.Controls.Add(_btnSelectAll);
            selectButtonsPanel.Controls.Add(_btnDeselectAll);

            _columnListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.MultiExtended
            };
            _columnListBox.SelectedIndexChanged += (s, e) =>
            {
                UpdateSelectedCount();
                MarkPreviewStale();
                if (!_isPopulatingColumnList) RefreshSelectedHostPreview();
                _previewPanel?.Invalidate();
            };

            rightPanel.Controls.Add(_columnListBox);
            rightPanel.Controls.Add(scopePanel);
            rightPanel.Controls.Add(selectButtonsPanel);
            rightPanel.Controls.Add(_lblSelectedCount);
            rightPanel.Controls.Add(_lblColTitle);
            Controls.Add(rightPanel);

            // 2.5 Top Template Configuration Panel
            var templatePanel = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White, Padding = new Padding(14, 10, 14, 10) };
            _lblTemplate = new Label { Text = "PRESET", AutoSize = true, Left = 15, Top = 21, Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = KhimUiStyle.TextSecondary };
            _cmbTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Left = 82, Top = 16 };
            
            _btnApplyTemplate = new Button { Text = "Áp dụng", Width = 88, Height = 32, Left = 312, Top = 15 };
            _btnSaveTemplate = new Button { Text = "Lưu mẫu", Width = 88, Height = 32, Left = 408, Top = 15 };
            _btnDeleteTemplate = new Button { Text = "Xóa", Width = 72, Height = 32, Left = 504, Top = 15 };
            KhimUiStyle.ApplyPrimaryButton(_btnApplyTemplate);
            KhimUiStyle.ApplySecondaryButton(_btnSaveTemplate);
            KhimUiStyle.ApplySecondaryButton(_btnDeleteTemplate);

            _btnSaveTemplate.Click += (s, e) => SaveTemplate();
            _btnApplyTemplate.Click += (s, e) => ApplyTemplate();
            _btnDeleteTemplate.Click += (s, e) => DeleteTemplate();

            templatePanel.Controls.Add(_lblTemplate);
            templatePanel.Controls.Add(_cmbTemplate);
            templatePanel.Controls.Add(_btnSaveTemplate);
            templatePanel.Controls.Add(_btnApplyTemplate);
            templatePanel.Controls.Add(_btnDeleteTemplate);
            Controls.Add(templatePanel);

            // 3. TabControl Trung tâm
            var tabControl = _workflowTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6),
                Appearance = TabAppearance.FlatButtons,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(0, 1),
                AccessibleName = "Rectangular column workflow settings"
            };

            // --- TAB 1: THÉP CHỦ & REVIEW ---
            _tabMain = new TabPage { Text = "Thép Chủ & Review", Padding = new Padding(8), BackColor = Color.White };
            var pnlMainLeft = new Panel { Dock = DockStyle.Left, Width = 400, AutoScroll = true };

            _grpMainSection = new GroupBox { Text = "Bố trí Thép Chủ Tiết Diện", Dock = DockStyle.Top, Height = 135, Padding = new Padding(8) };
            var layoutMainSec = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _numBarsB = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 7, Width = 80 };
            _numBarsH = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 3, Width = 80 };
            _numBarsB.ValueChanged += (s, e) => _previewPanel?.Invalidate();
            _numBarsH.ValueChanged += (s, e) => _previewPanel?.Invalidate();

            _lblBarsB = AddRowToLayout(layoutMainSec, "Thép chủ cạnh B (kể cả góc):", _numBarsB);
            _lblBarsH = AddRowToLayout(layoutMainSec, "Thép chủ cạnh H (kể cả góc):", _numBarsH);
            _lblMainDia = AddRowToLayout(layoutMainSec, "Đường kính thép chủ:", _cmbMainDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 });
            _cmbMainDia.SelectedIndexChanged += (s, e) => UpdatePreviewBarDiameters();
            _grpMainSection.Controls.Add(layoutMainSec);

            _grpCover = new GroupBox { Text = "Cover Bê Tông", Dock = DockStyle.Top, Height = 95, Padding = new Padding(8) };
            var layoutCover = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _chkCustomCover = new CheckBox { Text = "Nhập tay Cover (mm)", Checked = false, AutoSize = true, Margin = new Padding(3, 4, 3, 4) };
            _numCustomCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 70, Enabled = false };
            _chkCustomCover.CheckedChanged += (s, e) => _numCustomCover.Enabled = _chkCustomCover.Checked;

            _btnProjectCover = new Button { Text = "Cover Dự Án", Width = 105, Height = 25, FlatStyle = FlatStyle.System };
            _btnProjectCover.Click += (s, e) => new ProjectCoverSetupForm(_doc).ShowDialog();

            _lblCustomCover = AddRowToLayout(layoutCover, "Cover tùy chỉnh (mm):", _numCustomCover);
            layoutCover.Controls.Add(_chkCustomCover);
            layoutCover.Controls.Add(_btnProjectCover);
            _grpCover.Controls.Add(layoutCover);

            _grpMainAnchor = new GroupBox { Text = "Cấu tạo Neo & Nối Thép", Dock = DockStyle.Top, Height = 175, Padding = new Padding(8) };
            var pnlAnchor = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _rdBaseFoundation = new RadioButton { Text = "Cột tầng móng (Nối chân quỳ 90° vào móng)", AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _rdBaseStandardLevel = new RadioButton { Text = "Cột tầng sàn / điển hình (Thép chờ nối tầng)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _chkCrankedSplice = new CheckBox { Text = "Nhấn vắt nghiêng 1:6 vị trí nối (Ảnh 1)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _chkTopAnchor = new CheckBox { Text = "Neo uốn móc 90° đỉnh mái (Ảnh 2)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _chkStaggeredSplice = new CheckBox { Text = "Nối so le 50% (Staggered 1.3 Ls)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };

            _rdBaseFoundation.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _rdBaseStandardLevel.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _chkCrankedSplice.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _chkTopAnchor.CheckedChanged += (s, e) => _previewPanel?.Invalidate();

            var pnlLapMult = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(3, 2, 3, 2) };
            pnlLapMult.Controls.Add(new Label { Text = "Ls = n × d:", AutoSize = true, Margin = new Padding(0, 5, 5, 0) });
            _numLapMultiplier = new NumericUpDown { Minimum = 20, Maximum = 60, Value = 30, Increment = 5, Width = 55 };
            pnlLapMult.Controls.Add(_numLapMultiplier);
            pnlLapMult.Controls.Add(new Label { Text = "d (30d/40d)", AutoSize = true, Margin = new Padding(3, 5, 0, 0) });

            pnlAnchor.Controls.Add(_rdBaseStandardLevel);
            pnlAnchor.Controls.Add(_rdBaseFoundation);
            pnlAnchor.Controls.Add(_chkCrankedSplice);
            pnlAnchor.Controls.Add(_chkTopAnchor);
            pnlAnchor.Controls.Add(_chkStaggeredSplice);
            pnlAnchor.Controls.Add(pnlLapMult);
            _grpMainAnchor.Controls.Add(pnlAnchor);

            pnlMainLeft.Controls.Add(_grpMainAnchor);
            pnlMainLeft.Controls.Add(_grpCover);
            pnlMainLeft.Controls.Add(_grpMainSection);

            _tabMain.Controls.Add(pnlMainLeft);

            // GDI+ Preview Panel 2D Column Elevation Review
            _previewPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(252, 252, 254) };
            _previewPanel.Paint += PreviewPanel_Paint;
            var previewViewport = RebarLayout.ScrollPreview(_previewPanel, new Size(560, 420));
            _previewPanel.Resize += (s, e) => _previewPanel.Invalidate();
            var previewArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _lblPreviewState = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(8, 7, 4, 2),
                Font = new Font("Segoe UI Semibold", 8.5F),
                AccessibleName = "Rectangular column preview state"
            };
            previewArea.Controls.Add(previewViewport);
            previewArea.Controls.Add(_lblPreviewState);
            _tabMain.Controls.Add(previewArea);

            tabControl.TabPages.Add(_tabMain);

            // --- TAB 2: THÉP ĐAI ---
            _tabStirrup = new TabPage { Text = "Thép Đai (Stirrups)", Padding = new Padding(12), BackColor = Color.White };

            _grpStirrupZone = new GroupBox { Text = "Phân Vùng Đai A1 / A2 / A1 (Chuẩn Kết Cấu)", Dock = DockStyle.Top, Height = 175, Padding = new Padding(10) };
            var layoutStirrupZone = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutStirrupZone.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layoutStirrupZone.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _lblStirrupDia = AddRowToLayout(layoutStirrupZone, "Đường kính thép đai:", _cmbStirrupDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 });
            _cmbStirrupDia.SelectedIndexChanged += (s, e) => UpdatePreviewBarDiameters();
            _lblStirrupA1 = AddRowToLayout(layoutStirrupZone, "Khoảng cách đai dầy A1 (mm):", _numStirrupSpacingA1 = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, Increment = 10, Width = 90 });
            _lblStirrupA2 = AddRowToLayout(layoutStirrupZone, "Khoảng cách đai thưa A2 (mm):", _numStirrupSpacingA2 = new NumericUpDown { Minimum = 100, Maximum = 500, Value = 200, Increment = 10, Width = 90 });
            _lblZoneA1Len = AddRowToLayout(layoutStirrupZone, "Chiều dài vùng dầy A1 (mm):", _numZoneA1Length = new NumericUpDown { Minimum = 300, Maximum = 2000, Value = 600, Increment = 50, Width = 90 });
            _grpStirrupZone.Controls.Add(layoutStirrupZone);

            _grpInnerStirrup = new GroupBox { Text = "Tùy chọn đai Legacy / Nâng cao", Dock = DockStyle.Top, Height = 110, Padding = new Padding(10) };
            var pnlInnerStirrup = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkInnerDiamond = new CheckBox { Text = "Legacy — đai lồng / đai thoi JP_T80", Checked = false, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
            _chkCrossLinks = new CheckBox { Text = "Nâng cao — đai móc phụ / Crosslink JP_T68", Checked = false, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
            _chkInnerDiamond.CheckedChanged += (s, e) =>
            {
                if (_chkInnerDiamond.Checked && _chkCrossLinks.Checked) _chkCrossLinks.Checked = false;
                _previewPanel?.Invalidate();
            };
            _chkCrossLinks.CheckedChanged += (s, e) =>
            {
                if (_chkCrossLinks.Checked && _chkInnerDiamond.Checked) _chkInnerDiamond.Checked = false;
                _previewPanel?.Invalidate();
            };
            pnlInnerStirrup.Controls.Add(_chkInnerDiamond);
            pnlInnerStirrup.Controls.Add(_chkCrossLinks);
            _grpInnerStirrup.Controls.Add(pnlInnerStirrup);

            _tabStirrup.Controls.Add(_grpInnerStirrup);
            _tabStirrup.Controls.Add(_grpStirrupZone);
            tabControl.TabPages.Add(_tabStirrup);

            // --- TAB 3: GENERAL SETTINGS ---
            _tabGenSettings = new TabPage { Text = "General Settings", Padding = new Padding(10), BackColor = Color.White };
            var layoutGenSettings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            layoutGenSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layoutGenSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _grpHook = new GroupBox { Text = "REBAR HOOK BENDING SECTION", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlHook = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _rdHookLengthFixed = new RadioButton { Text = "By fixed length L (mm):", Checked = true, AutoSize = true };
            _numHookFixedMm = new NumericUpDown { Minimum = 50, Maximum = 500, Value = 150, Width = 70 };
            _rdHookLengthDia = new RadioButton { Text = "By diameter (xD):", AutoSize = true };
            _numHookDiaxD = new NumericUpDown { Minimum = 5, Maximum = 30, Value = 10, Width = 70 };
            pnlHook.Controls.Add(_rdHookLengthFixed); pnlHook.Controls.Add(_numHookFixedMm);
            pnlHook.Controls.Add(_rdHookLengthDia); pnlHook.Controls.Add(_numHookDiaxD);
            _grpHook.Controls.Add(pnlHook);

            _grpBendCut = new GroupBox { Text = "REBAR BENDING OR CUTTING CONDITIONS", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlBendCut = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _lblBendE = new Label { Text = "Bend rebar if e ≤ (mm):", AutoSize = true };
            _numBendConditionE = new NumericUpDown { Minimum = 10, Maximum = 300, Value = 75, Width = 70 };
            _lblBendRatio = new Label { Text = "Bend by ratio Hd/e ≥:", AutoSize = true };
            _numBendRatioHd = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 6, Width = 70 };
            pnlBendCut.Controls.Add(_lblBendE); pnlBendCut.Controls.Add(_numBendConditionE);
            pnlBendCut.Controls.Add(_lblBendRatio); pnlBendCut.Controls.Add(_numBendRatioHd);
            _grpBendCut.Controls.Add(pnlBendCut);

            _grpTopRoof = new GroupBox { Text = "SET TOP ROOF REBAR", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlTopRoof = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _rdTopRoofHook = new RadioButton { Text = "Bend hook for top floor rebar", Checked = true, AutoSize = true };
            _rdTopRoofContinue = new RadioButton { Text = "Continue waiting for top floor rebar", AutoSize = true };
            pnlTopRoof.Controls.Add(_rdTopRoofHook); pnlTopRoof.Controls.Add(_rdTopRoofContinue);
            _grpTopRoof.Controls.Add(pnlTopRoof);

            _grpSplicePos = new GroupBox { Text = "REBAR SPLICE POSITION", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlSplicePos = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _lblSpliceDist = new Label { Text = "Splice distance from column base L = (mm):", AutoSize = true };
            _numSpliceDistBase = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = 50, Width = 70 };
            _rdSpliceTwoPos = new RadioButton { Text = "Splice rebar at two positions (Nối so le 50%)", Checked = true, AutoSize = true };
            pnlSplicePos.Controls.Add(_lblSpliceDist); pnlSplicePos.Controls.Add(_numSpliceDistBase); pnlSplicePos.Controls.Add(_rdSpliceTwoPos);
            _grpSplicePos.Controls.Add(pnlSplicePos);

            _grpAssignInfo = new GroupBox { Text = "ASSIGN ADDITIONAL INFORMATION TO REBAR", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlAssignInfo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkAssignElevation = new CheckBox { Text = "Assign column elevation to rebar", Checked = true, AutoSize = true };
            _chkAssignPartition = new CheckBox { Text = "Automatically assign Partition to rebar", Checked = true, AutoSize = true };
            pnlAssignInfo.Controls.Add(_chkAssignElevation); pnlAssignInfo.Controls.Add(_chkAssignPartition);
            _grpAssignInfo.Controls.Add(pnlAssignInfo);

            _grpSlabBeam = new GroupBox { Text = "OPTION AT SLAB BEAM POSITION", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlSlabBeam = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _lblDefaultHd = new Label { Text = "Default height Hd (mm):", AutoSize = true };
            _numDefaultBeamHd = new NumericUpDown { Minimum = 100, Maximum = 2000, Value = 500, Increment = 50, Width = 80 };
            pnlSlabBeam.Controls.Add(_lblDefaultHd); pnlSlabBeam.Controls.Add(_numDefaultBeamHd);
            _grpSlabBeam.Controls.Add(pnlSlabBeam);

            // These legacy controls are not mapped into RectangularColumnRebarInput.
            // Keep them visible for roadmap context, but do not present them as active settings.
            _grpHook.Enabled = false;
            _grpBendCut.Enabled = false;
            _grpTopRoof.Enabled = false;
            _grpSplicePos.Enabled = false;
            _grpAssignInfo.Enabled = false;
            _grpSlabBeam.Enabled = false;

            layoutGenSettings.Controls.Add(_grpHook, 0, 0); layoutGenSettings.Controls.Add(_grpBendCut, 1, 0);
            layoutGenSettings.Controls.Add(_grpTopRoof, 0, 1); layoutGenSettings.Controls.Add(_grpSplicePos, 1, 1);
            layoutGenSettings.Controls.Add(_grpAssignInfo, 0, 2); layoutGenSettings.Controls.Add(_grpSlabBeam, 1, 2);

            _tabGenSettings.Controls.Add(layoutGenSettings);
            RebarLayout.Stack(_tabGenSettings, _grpHook, _grpBendCut, _grpTopRoof, _grpSplicePos, _grpAssignInfo, _grpSlabBeam);
            tabControl.TabPages.Add(_tabGenSettings);

            // --- TAB 4: BẢN VẼ & VIEW 3D ---
            _tabViews = new TabPage { Text = "Bản Vẽ & View 3D", Padding = new Padding(12), BackColor = Color.White };
            _grpViews = new GroupBox { Text = "Tự động Tạo View & Triển khai Bản vẽ", Dock = DockStyle.Top, Height = 130, Padding = new Padding(10) };
            var pnlViews = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkAutoDrawing = new CheckBox { Text = "Tự động tạo bản vẽ 2D (Mặt cắt tiết diện & Thống kê thép)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
            _chkAutoSection3D = new CheckBox { Text = "Tự động tạo View xem thép 3D (Plan View + 3D View)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
            pnlViews.Controls.Add(_chkAutoDrawing);
            pnlViews.Controls.Add(_chkAutoSection3D);
            _grpViews.Controls.Add(pnlViews);

            _tabViews.Controls.Add(_grpViews);
            tabControl.TabPages.Add(_tabViews);

            tabControl.TabPages.Add(RebarReferenceViews.CreatePage(RebarReferenceKind.RectangularColumn));
            tabControl.TabPages.Add(RebarConfigurationPage.Create(this, _doc, RebarReferenceKind.RectangularColumn,
                RebarConfigurationField.Number("Column.LapMultiplier", "Chiều dài nối (k × d)", _numLapMultiplier),
                RebarConfigurationField.Number("Column.StirrupA1", "Đai vùng đầu (mm)", _numStirrupSpacingA1),
                RebarConfigurationField.Number("Column.StirrupA2", "Đai vùng giữa (mm)", _numStirrupSpacingA2),
                RebarConfigurationField.Number("Column.ZoneA1", "Chiều dài vùng đai đầu (mm)", _numZoneA1Length),
                RebarConfigurationField.Flag("Column.TopHook", "Neo đỉnh", _chkTopAnchor),
                RebarConfigurationField.Flag("Column.Cranked", "Nhấn tại nối tầng", _chkCrankedSplice),
                RebarConfigurationField.Flag("Column.Staggered", "Nối so le", _chkStaggeredSplice),
                RebarConfigurationField.Flag("Column.UseCustomCover", "Dùng cover tùy chỉnh", _chkCustomCover),
                RebarConfigurationField.Number("Column.CoverMm", "Cover tùy chỉnh (mm)", _numCustomCover)));
            tabControl.Multiline = true;
            var workflowWorkspace = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var workflowNavigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(8, 5, 8, 3),
                BackColor = Color.White
            };
            AddWorkflowNavigation(workflowNavigation, 0);
            AddWorkflowNavigation(workflowNavigation, 1);
            AddWorkflowNavigation(workflowNavigation, 2);
            AddWorkflowNavigation(workflowNavigation, 3);
            AddWorkflowNavigation(workflowNavigation, 4);
            AddWorkflowNavigation(workflowNavigation, 5);
            tabControl.SelectedIndexChanged += (s, e) => UpdateWorkflowNavigation();
            workflowWorkspace.Controls.Add(tabControl);
            workflowWorkspace.Controls.Add(workflowNavigation);
            Controls.Add(workflowWorkspace);
            workflowWorkspace.BringToFront();
            AttachPreviewInvalidationHandlers(tabControl);
            RebarLayout.FitColumnGroups(tabControl);
            RebarLayout.Stack(pnlMainLeft, _grpMainSection, _grpCover, _grpMainAnchor);
            RebarLayout.ColumnEditor(_tabMain, pnlMainLeft, previewArea);
            footer.SendToBack();
            RebarLayout.PresetBar(templatePanel, _lblTemplate, _cmbTemplate, _btnApplyTemplate, _btnSaveTemplate, _btnDeleteTemplate);
            UpdatePreviewStateUi();
            UpdateWorkflowNavigation();
        }

        private void AddWorkflowNavigation(FlowLayoutPanel host, int pageIndex)
        {
            var button = new Button
            {
                Tag = pageIndex,
                AutoSize = true,
                Height = 30,
                AccessibleName = "Show rectangular column workflow page " + (pageIndex + 1),
                Margin = new Padding(3, 0, 3, 0)
            };
            button.Click += (s, e) =>
            {
                if (_workflowTabs != null && pageIndex >= 0 && pageIndex < _workflowTabs.TabPages.Count)
                    _workflowTabs.SelectedIndex = pageIndex;
            };
            _workflowNavigationButtons.Add(button);
            KhimUiStyle.ApplySecondaryButton(button);
            host.Controls.Add(button);
        }

        private void UpdateWorkflowNavigation()
        {
            bool isEn = LanguageManager.IsEnglish;
            string[] vietnamese = { "Thép chủ", "Đai", "Neo & nối", "Bản vẽ", "Tham khảo", "Cấu hình" };
            string[] english = { "Main bars", "Ties", "Anchorage & splice", "Drawings", "Reference", "Configuration" };
            for (int index = 0; index < _workflowNavigationButtons.Count; index++)
            {
                Button button = _workflowNavigationButtons[index];
                button.Text = (isEn ? english : vietnamese)[index];
                if (_workflowTabs != null && _workflowTabs.SelectedIndex == (int)button.Tag)
                    KhimUiStyle.ApplyPrimaryButton(button);
                else
                    KhimUiStyle.ApplySecondaryButton(button);
            }
        }

        private void AttachPreviewInvalidationHandlers(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (control is NumericUpDown number) number.ValueChanged += (s, e) => MarkPreviewStale();
                else if (control is ComboBox combo) combo.SelectedIndexChanged += (s, e) => MarkPreviewStale();
                else if (control is CheckBox check) check.CheckedChanged += (s, e) => MarkPreviewStale();
                else if (control is RadioButton radio) radio.CheckedChanged += (s, e) => { if (radio.Checked) MarkPreviewStale(); };
                if (control.HasChildren) AttachPreviewInvalidationHandlers(control);
            }
        }

        private void MarkPreviewStale()
        {
            if (_previewLifecycle.State == PreviewLifecycleState.Valid) _previewLifecycle.MarkStale();
            UpdatePreviewStateUi();
        }

        private void UpdatePreviewStateUi()
        {
            if (_lblPreviewState == null) return;
            bool isEn = LanguageManager.IsEnglish;
            switch (_previewLifecycle.State)
            {
                case PreviewLifecycleState.Valid:
                    _lblPreviewState.Text = isEn ? "PREVIEW · Valid — current inputs solved" : "XEM TRƯỚC · Hợp lệ — đã giải theo thông số hiện tại";
                    _lblPreviewState.ForeColor = Color.FromArgb(21, 128, 61);
                    break;
                case PreviewLifecycleState.Stale:
                    _lblPreviewState.Text = isEn ? "PREVIEW · Inputs changed — solve again" : "XEM TRƯỚC · Thông số đổi — cần giải lại";
                    _lblPreviewState.ForeColor = Color.FromArgb(180, 83, 9);
                    break;
                case PreviewLifecycleState.Invalid:
                    _lblPreviewState.Text = isEn ? "PREVIEW · Solve failed — review inputs" : "XEM TRƯỚC · Giải không thành công — kiểm tra thông số";
                    _lblPreviewState.ForeColor = Color.FromArgb(185, 28, 28);
                    break;
                default:
                    _lblPreviewState.Text = isEn ? "PREVIEW · Not solved — solve before Create" : "XEM TRƯỚC · Chưa giải — hãy giải trước khi tạo";
                    _lblPreviewState.ForeColor = KhimUiStyle.TextSecondary;
                    break;
            }
            _formGuard?.ValidateNow();
        }

        private static Label AddRowToLayout(TableLayoutPanel layout, string labelText, Control control)
        {
            var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 4, 3, 4) };
            layout.Controls.Add(lbl);
            layout.Controls.Add(control);
            return lbl;
        }

        private void SetAllColumnsSelected(bool selectAll)
        {
            for (int i = 0; i < _columnListBox.Items.Count; i++)
            {
                _columnListBox.SetSelected(i, selectAll);
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int count = _columnListBox.SelectedItems.Count;
            if (_preSelectedColumns.Any())
            {
                _lblSelectedCount.Text = LanguageManager.IsEnglish
                    ? $"Preselected: {count} columns from Revit"
                    : $"Đã chọn sẵn: {count} cột từ Revit";
                _lblSelectedCount.ForeColor = Color.DarkGreen;
            }
            else
            {
                _lblSelectedCount.Text = LanguageManager.IsEnglish
                    ? $"Selected: {count} / {_columnListBox.Items.Count} columns"
                    : $"Đã chọn: {count} / {_columnListBox.Items.Count} cột";
                _lblSelectedCount.ForeColor = Color.DarkBlue;
            }
        }

        private void PopulateColumnList()
        {
            _isPopulatingColumnList = true;
            _columnListBox.Items.Clear();
            bool showSelectedOnly = _rdScopeSelected != null && _rdScopeSelected.Checked;

            List<FamilyInstance> targetCols = (showSelectedOnly && _preSelectedColumns.Any())
                ? _preSelectedColumns
                : _availableColumns;

            var preSelectedIds = new HashSet<ElementId>(_preSelectedColumns.Select(c => c.Id));

            for (int i = 0; i < targetCols.Count; i++)
            {
                var col = targetCols[i];
                ElementId lvlId = (col.LevelId != ElementId.InvalidElementId)
                    ? col.LevelId
                    : (col.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);

                string levelName = (lvlId != ElementId.InvalidElementId ? _doc.GetElement(lvlId)?.Name : null)
                    ?? col.LookupParameter("Base Level")?.AsString()
                    ?? "Level N/A";

                string mark = col.LookupParameter("Mark")?.AsString() ?? col.Id.ToLongValue().ToString();
                var item = new ColumnListItem(col, $"{levelName} - {mark}");
                _columnListBox.Items.Add(item);

                if (preSelectedIds.Contains(col.Id))
                {
                    _columnListBox.SetSelected(i, true);
                }
            }

            _isPopulatingColumnList = false;
            UpdateSelectedCount();
            RefreshSelectedHostPreview();
        }

        private void RefreshSelectedHostPreview()
        {
            bool isEn = LanguageManager.IsEnglish;
            var item = _columnListBox?.SelectedItems.Count > 0
                ? _columnListBox.SelectedItems[0] as ColumnListItem
                : null;
            var column = item?.Column;

            _previewWidthMm = 0;
            _previewDepthMm = 0;
            _previewHeightMm = 0;
            _previewMark = "<not set>";
            _previewLevelName = null;
            _previewHostError = null;

            if (column == null || _doc == null)
            {
                _previewHostError = isEn ? "Select a column to show host geometry." : "Hãy chọn cột để hiển thị hình học.";
                return;
            }

            try
            {
                var profile = RectangularColumnGeometryHelper.GetRectangularProfile(column);
                _previewWidthMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters));
                _previewDepthMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters));
                _previewHeightMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.Height, UnitTypeId.Millimeters));
                _previewMark = column.LookupParameter("Mark")?.AsString() ?? "<not set>";
                _previewLevelName = _doc.GetElement(column.LevelId)?.Name;
            }
            catch (Exception ex)
            {
                _previewHostError = (isEn ? "Host geometry unavailable: " : "Hình học cấu kiện không khả dụng: ") + ex.Message;
            }
        }

        private void PopulateBarTypeCombos()
        {
            var names = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();

            _cmbMainDia.Items.AddRange(names);
            _cmbStirrupDia.Items.AddRange(names);
            if (names.Any())
            {
                _cmbMainDia.SelectedIndex = 0;
                _cmbStirrupDia.SelectedIndex = 0;
            }
            UpdatePreviewBarDiameters();
        }

        private void UpdatePreviewBarDiameters()
        {
            RebarBarType mainType = FindBarType(_cmbMainDia?.Text);
            RebarBarType stirrupType = FindBarType(_cmbStirrupDia?.Text);
            _previewMainBarDiameterMm = mainType == null ? 0 :
                UnitUtils.ConvertFromInternalUnits(mainType.BarModelDiameter, UnitTypeId.Millimeters);
            _previewStirrupDiameterMm = stirrupType == null ? 0 :
                UnitUtils.ConvertFromInternalUnits(stirrupType.BarModelDiameter, UnitTypeId.Millimeters);
            _previewPanel?.Invalidate();
        }

        private void BtnCreateRebar_Click(object sender, EventArgs e)
        {
            var selectedItems = _columnListBox.SelectedItems.Cast<ColumnListItem>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show(this, "Vui lòng chọn ít nhất 1 cột trong danh sách bên phải.", "Thiếu thông tin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RebarBarType mainType = FindBarType(_cmbMainDia.Text);
            RebarBarType stirrupType = FindBarType(_cmbStirrupDia.Text);
            if (mainType == null || stirrupType == null)
            {
                MessageBox.Show(this, "Chưa chọn đủ đường kính thép.", "Thiếu thông tin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            double? customCoverFeet = _chkCustomCover.Checked
                ? UnitUtils.ConvertToInternalUnits((double)_numCustomCover.Value, UnitTypeId.Millimeters)
                : null;

            List<List<RectangularColumnRebarInput>> inputGroups = BuildGenerationInputGroups(
                selectedItems, mainType, stirrupType, customCoverFeet);
            string currentFingerprint = RebarPreviewService.FingerprintInputs(inputGroups.SelectMany(group => group).Select(RebarPreviewService.Fingerprint));
            RebarPreviewSnapshot acceptedPreview;
            if (_lastPreview == null || !_previewLifecycle.TryGetValid(currentFingerprint, out acceptedPreview) || inputGroups.SelectMany(g => g).Any(input =>
                acceptedPreview.Find(RebarPreviewService.Fingerprint(input)) == null))
            {
                UpdatePreviewStateUi();
                MessageBox.Show(this, "Create or refresh the solver-backed 3D preview for the current columns and settings before generating rebar.",
                    "Preview required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (RectangularColumnRebarInput input in inputGroups.SelectMany(group => group))
            {
                if (RebarPreviewService.HasExistingDuplicateBar(_doc, input.Column, acceptedPreview,
                    RebarPreviewService.Fingerprint(input)))
                {
                    MessageBox.Show(this, "Equivalent reinforcement already exists on column " + input.Column.Id + ". Remove or edit existing bars before generating to avoid duplicates.",
                        "Duplicate reinforcement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int axisGroupCount = inputGroups.Count;
            var report = new RebarGenerationReport();
            int committedColumnCount = 0;
            int rolledBackColumnCount = 0;
            bool commonShapesPreloaded = false;

            try
            {
                TransactionBoundary.ExecuteGroup(_doc, "K-TOOLS - Create Rectangular Column Rebar", () =>
                {
                    var generator = new RectangularColumnRebarGenerator(_doc);
                    var drawingGen = new ColumnRebarDrawingGenerator(_doc);
                    var sectionGen = new ColumnRebarSectionViewGenerator(_doc);
                    var view3DGen = new ColumnRebar3DViewGenerator(_doc);
                    ColumnTieLayoutType tieLayoutType = GetSelectedTieLayoutType();

                    foreach (List<RectangularColumnRebarInput> inputs in inputGroups)
                    {
                        foreach (var input in inputs)
                        {
                            if (!TryGenerateRectangularColumn(
                                    generator,
                                    input,
                                    report,
                                    ref commonShapesPreloaded,
                                    acceptedPreview))
                            {
                                rolledBackColumnCount++;
                                continue;
                            }

                            committedColumnCount++;
                            CreatePostCommitColumnArtifacts(
                                input,
                                customCoverFeet,
                                drawingGen,
                                sectionGen,
                                view3DGen,
                                report);
                        }
                    }
                    return true;
                });
            }
            catch (Exception ex)
            {
                _lastPreview = null;
                _previewLifecycle.Invalidate();
                UpdatePreviewStateUi();
                System.Diagnostics.Debug.WriteLine("Rectangular column Rebar creation failed: " + ex);
                string errTitle = LanguageManager.IsEnglish ? "Error Creating Rebar" : "Lỗi Tạo Thép Cột";
                KhimDialogHelper.ShowError(errTitle, ex.Message);
                return;
            }

            if (rolledBackColumnCount > 0)
            {
                ShowRolledBackColumnReport(report, committedColumnCount, rolledBackColumnCount);
            }
            else if (report.HasErrors)
            {
                KhimDialogHelper.ShowRebarGenerationReport(report, "Cột Chữ Nhật (Column)", selectedItems.Count);
            }
            else
            {
                KhimDialogHelper.ShowColumnRebarSuccess(committedColumnCount, axisGroupCount, _chkAutoDrawing.Checked, _chkAutoSection3D.Checked);
            }
            _lastPreview = null;
            _previewLifecycle.Invalidate();
            UpdatePreviewStateUi();
        }

        private void BtnPreview3D_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedItems = _columnListBox.SelectedItems.Cast<ColumnListItem>().ToList();
                if (selectedItems.Count == 0) throw new InvalidOperationException("Select at least one rectangular column first.");
                RebarBarType mainType = FindBarType(_cmbMainDia.Text);
                RebarBarType stirrupType = FindBarType(_cmbStirrupDia.Text);
                if (mainType == null || stirrupType == null) throw new InvalidOperationException("Select both main and tie bar types.");
                double? customCoverFeet = _chkCustomCover.Checked
                    ? UnitUtils.ConvertToInternalUnits((double)_numCustomCover.Value, UnitTypeId.Millimeters)
                    : (double?)null;
                List<List<RectangularColumnRebarInput>> groups = BuildGenerationInputGroups(selectedItems, mainType, stirrupType, customCoverFeet);
                var generator = new RectangularColumnRebarGenerator(_doc);
                bool shapesLoaded = false;
                var requests = groups.SelectMany(group => group).Select(input =>
                {
                    string fingerprint = RebarPreviewService.Fingerprint(input);
                    var roleByBarId = new Dictionary<string, string>(StringComparer.Ordinal);
                    return new RebarPreviewRequest(fingerprint, () =>
                    {
                        if (!shapesLoaded)
                        {
                            RebarShapeLibrary.PreloadCommonShapes(_doc);
                            shapesLoaded = true;
                        }
                        var report = new RebarGenerationReport();
                        List<Rebar> bars = generator.Generate(input, report, roleByBarId);
                        if (report.HasErrors) throw new InvalidOperationException(report.Errors[0].ErrorReason);
                        return bars;
                    }, () => RebarPreviewService.Fingerprint(input), RebarPreviewService.Describe(input),
                    bar =>
                    {
                        string role;
                        return roleByBarId.TryGetValue(bar.Id.Value.ToString(CultureInfo.InvariantCulture), out role) ? role : string.Empty;
                    });
                }).ToArray();
                _previewLifecycle.BeginGeneration();
                _lastPreview = RebarPreviewService.Capture(_doc, requests);
                using (var preview = new RebarSolverPreviewForm(_lastPreview))
                {
                    if (preview.ShowDialog(this) == DialogResult.OK) _previewLifecycle.Complete(_lastPreview, _lastPreview.PlanFingerprint);
                    else { _lastPreview = null; _previewLifecycle.Invalidate(); }
                }
                UpdatePreviewStateUi();
            }
            catch (Exception ex)
            {
                _lastPreview = null;
                _previewLifecycle.Invalidate();
                UpdatePreviewStateUi();
                KhimDialogHelper.ShowError("Unable to create Rebar solver preview: " + ex.Message);
            }
        }

        private List<List<RectangularColumnRebarInput>> BuildGenerationInputGroups(
            IList<ColumnListItem> selectedItems, RebarBarType mainType, RebarBarType stirrupType, double? customCoverFeet)
        {
            List<List<FamilyInstance>> axisGroups = RebarLapSpliceHelper.GroupColumnsByAxis(
                selectedItems.Select(i => i.Column).ToList(), _doc);
            var result = new List<List<RectangularColumnRebarInput>>();
            ColumnTieLayoutType tieLayoutType = GetSelectedTieLayoutType();
            foreach (List<FamilyInstance> axisGroup in axisGroups)
            {
                var inputs = axisGroup.Select(column => new RectangularColumnRebarInput
                {
                    Column = column,
                    MainBarType = mainType,
                    StirrupBarType = stirrupType,
                    BarsAlongB = (int)_numBarsB.Value,
                    BarsAlongH = (int)_numBarsH.Value,
                    StirrupSpacingA1 = UnitUtils.ConvertToInternalUnits((double)_numStirrupSpacingA1.Value, UnitTypeId.Millimeters),
                    StirrupSpacingA2 = UnitUtils.ConvertToInternalUnits((double)_numStirrupSpacingA2.Value, UnitTypeId.Millimeters),
                    ZoneA1Length = UnitUtils.ConvertToInternalUnits((double)_numZoneA1Length.Value, UnitTypeId.Millimeters),
                    TieLayout = tieLayoutType,
                    HasInnerDiamondStirrup = tieLayoutType == ColumnTieLayoutType.DiamondLegacy,
                    HasCrossLinks = tieLayoutType == ColumnTieLayoutType.CrossTie,
                    HasDowel = !_rdBaseFoundation.Checked,
                    IsFoundationColumn = _rdBaseFoundation.Checked,
                    EnableCrankedSplice = _chkCrankedSplice.Checked,
                    HasTopAnchor = _chkTopAnchor.Checked,
                    CustomCoverFeet = customCoverFeet,
                    DesignStandard = GetSelectedDesignStandard(),
                    ConcreteGrade = GetSelectedConcreteGrade(),
                    SteelGrade = GetSelectedSteelGrade(),
                    LapLengthMultiplier = (double)_numLapMultiplier.Value,
                    StaggeredSplice = _chkStaggeredSplice.Checked
                }).ToList();
                ConfigureAdjacentColumns(inputs);
                result.Add(inputs);
            }
            return result;
        }

        private ColumnTieLayoutType GetSelectedTieLayoutType()
        {
            if (_chkInnerDiamond?.Checked == true) return ColumnTieLayoutType.DiamondLegacy;
            if (_chkCrossLinks?.Checked == true) return ColumnTieLayoutType.CrossTie;
            return ColumnTieLayoutType.MultiCellClosed;
        }

        private static void ConfigureAdjacentColumns(IList<RectangularColumnRebarInput> inputs)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                RectangularColumnRebarInput input = inputs[i];
                input.AdjacentColumnBelow = i > 0 &&
                    RebarLapSpliceHelper.AreConsecutiveColumns(inputs[i - 1].Column, input.Column)
                    ? inputs[i - 1].Column
                    : null;
                input.AdjacentColumnAbove = i < inputs.Count - 1 &&
                    RebarLapSpliceHelper.AreConsecutiveColumns(input.Column, inputs[i + 1].Column)
                    ? inputs[i + 1].Column
                    : null;
                input.IsTopRoofColumn = input.AdjacentColumnAbove == null;
            }
        }

        private bool TryGenerateRectangularColumn(
            RectangularColumnRebarGenerator generator,
            RectangularColumnRebarInput input,
            RebarGenerationReport aggregateReport,
            ref bool commonShapesPreloaded,
            RebarPreviewSnapshot preview)
        {
            var columnReport = new RebarGenerationReport();
            var failurePreprocessor = new RebarGenerationFailurePreprocessor();
            bool abortBatch = false;
            bool shapesWerePreloaded = commonShapesPreloaded;

            try
            {
                bool committed = TransactionBoundary.Execute(_doc,
                    $"K-TOOLS - Rectangular Column Rebar [{GetColumnLabel(input.Column)}]", () =>
                {
                    // RebarShape loading is part of the first column transaction. If
                    // that column fails, its type-loading changes roll back with it.
                    if (!shapesWerePreloaded)
                        RebarShapeLibrary.PreloadCommonShapes(_doc);

                    List<Rebar> createdRebars = generator.Generate(input, columnReport);

                    // Revit can post a shape-solver failure during regeneration or
                    // commit. Force geometry validation while failure handling is active.
                    _doc.Regenerate();

                    if (!RebarPreviewService.Matches(preview, RebarPreviewService.Fingerprint(input), createdRebars))
                    {
                        columnReport.AddError(input.Column, "Preview parity",
                            new InvalidOperationException("Generated centerlines differ from the reviewed solver preview; this column was rolled back."));
                        return false;
                    }

                    if (createdRebars == null || createdRebars.Count == 0)
                    {
                        columnReport.AddError(
                            input.Column,
                            "Rectangular column rebar",
                            new InvalidOperationException("Generator did not create a valid rebar set for this column."));
                    }

                    return !columnReport.HasErrors && !failurePreprocessor.HasUnrecoverableFailure;
                }, configure: transaction => ConfigureFailureHandling(transaction, failurePreprocessor),
                    shouldCommit: valid => valid);

                if (!committed)
                {
                    AddRolledBackColumnDiagnostic(aggregateReport, input, columnReport, failurePreprocessor, null);
                    return false;
                }

                if (failurePreprocessor.HasUnrecoverableFailure)
                {
                    AddRolledBackColumnDiagnostic(
                        aggregateReport,
                        input,
                        columnReport,
                        failurePreprocessor,
                        new InvalidOperationException("Column transaction committed with an unrecoverable Revit failure."));
                    abortBatch = true;
                    throw new InvalidOperationException("Column transaction committed with an unrecoverable Revit failure; the enclosing batch was rolled back.");
                }

                commonShapesPreloaded = true;
                aggregateReport.Merge(columnReport);
                return true;
            }
            catch (RebarShapeCreationHelper.RebarSubTransactionRollbackException ex)
            {
                abortBatch = true;
                AddRolledBackColumnDiagnostic(aggregateReport, input, columnReport, failurePreprocessor, ex);
                throw;
            }
            catch (Exception ex)
            {
                if (abortBatch) throw;
                AddRolledBackColumnDiagnostic(aggregateReport, input, columnReport, failurePreprocessor, ex);
                return false;
            }
        }

        private void CreatePostCommitColumnArtifacts(
            RectangularColumnRebarInput input,
            double? customCoverFeet,
            ColumnRebarDrawingGenerator drawingGen,
            ColumnRebarSectionViewGenerator sectionGen,
            ColumnRebar3DViewGenerator view3DGen,
            RebarGenerationReport report)
        {
            if (!_chkAutoDrawing.Checked && !_chkAutoSection3D.Checked) return;

            var failurePreprocessor = new RebarGenerationFailurePreprocessor();
            bool abortBatch = false;
            try
            {
                TransactionBoundary.Execute(_doc,
                    $"K-TOOLS - Rectangular Column Rebar Artifacts [{GetColumnLabel(input.Column)}]", () =>
                {
                    if (_chkAutoDrawing.Checked)
                    {
                        var profile = RectangularColumnGeometryHelper.GetRectangularProfile(input.Column);
                        double coverFeet = customCoverFeet ?? RebarCoverHelper.GetColumnCover(input.Column, RebarFace.Exterior);

                        drawingGen.CreateOrUpdate(new ColumnRebarDrawingInput
                        {
                            Shape = ColumnShapeType.Rectangular,
                            ColumnMark = input.Column.LookupParameter("Mark")?.AsString() ?? input.Column.Id.ToString(),
                            ColumnWidthMm = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters),
                            ColumnHeightMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters),
                            BarsAlongB = input.BarsAlongB,
                            BarsAlongH = input.BarsAlongH,
                            MainBarLabel = input.MainBarType?.Name,
                            StirrupLabel = input.StirrupBarType?.Name,
                            StirrupSpacingMm = UnitUtils.ConvertFromInternalUnits(input.StirrupSpacingA1, UnitTypeId.Millimeters),
                            CoverMm = UnitUtils.ConvertFromInternalUnits(coverFeet, UnitTypeId.Millimeters)
                        });
                    }

                    if (_chkAutoSection3D.Checked)
                    {
                        var itemRebars = HostedRebarQuery.GetHostedRebar(_doc, input.Column);
                        sectionGen.CreateOrUpdate(input.Column, itemRebars);
                        view3DGen.CreateOrUpdate(input.Column, itemRebars);
                    }
                }, configure: transaction => ConfigureFailureHandling(transaction, failurePreprocessor));

                if (failurePreprocessor.HasUnrecoverableFailure)
                {
                    report.AddError(input.Column, "Column rebar drawing / view",
                        new InvalidOperationException("Drawing/view transaction committed with an unrecoverable Revit failure."));
                    abortBatch = true;
                    throw new InvalidOperationException("Drawing/view transaction committed with an unrecoverable Revit failure; the enclosing batch was rolled back.");
                }
            }
            catch (Exception ex)
            {
                if (abortBatch) throw;
                report.AddError(input.Column, "Column rebar drawing / view", ex);
            }
        }

        private static void ConfigureFailureHandling(
            Transaction transaction,
            RebarGenerationFailurePreprocessor failurePreprocessor)
        {
            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetClearAfterRollback(true);
            options.SetFailuresPreprocessor(failurePreprocessor);
            transaction.SetFailureHandlingOptions(options);
        }

        private static string GetColumnLabel(FamilyInstance column)
        {
            if (column == null) return "Unknown";
            string mark = column.LookupParameter("Mark")?.AsString();
            return string.IsNullOrWhiteSpace(mark) ? column.Id.ToString() : mark;
        }

        private static void AddRolledBackColumnDiagnostic(
            RebarGenerationReport aggregateReport,
            RectangularColumnRebarInput input,
            RebarGenerationReport columnReport,
            RebarGenerationFailurePreprocessor failurePreprocessor,
            Exception exception)
        {
            var failureDetails = new List<string>();

            if (columnReport != null)
            {
                failureDetails.AddRange(columnReport.Errors
                    .Select(error => $"{error.RebarCategory}: {error.ErrorReason}"));
            }

            if (failurePreprocessor != null)
            {
                failureDetails.AddRange(failurePreprocessor.Records
                    .Where(record => record.RequiresRollback)
                    .Select(record =>
                        $"[{record.Severity}] {record.Description} " +
                        $"(FailureDefinitionId: {record.FailureDefinitionId}; " +
                        $"Failing ElementIds: {record.GetFailingElementIdsText()})"));
            }

            if (exception != null && !string.IsNullOrWhiteSpace(exception.Message))
            {
                failureDetails.Add(exception.Message);
            }

            string conciseFailures = string.Join(
                Environment.NewLine + "• ",
                failureDetails.Where(detail => !string.IsNullOrWhiteSpace(detail)).Distinct().Take(3));
            if (string.IsNullOrWhiteSpace(conciseFailures))
            {
                conciseFailures = "Revit rejected the candidate rebar during validation.";
            }

            string message = new StringBuilder()
                .AppendLine("COLUMN REBAR FAILED")
                .AppendLine($"Column: {GetColumnLabel(input.Column)}")
                .AppendLine($"ElementId: {input.Column?.Id}")
                .AppendLine("Stage: Rebar generation / regeneration / transaction commit")
                .AppendLine($"Rebar Type: Main {input.MainBarType?.Name ?? "Unknown"}; Tie {input.StirrupBarType?.Name ?? "Unknown"}")
                .AppendLine("Shape: Candidate shape was not accepted by Revit before commit.")
                .AppendLine($"Failure: • {conciseFailures}")
                .Append("Action: Column transaction rolled back. No partial reinforcement was kept.")
                .ToString();

            aggregateReport.AddError(
                input.Column,
                "Column transaction rolled back",
                new InvalidOperationException(message));
        }

        private static void ShowRolledBackColumnReport(
            RebarGenerationReport report,
            int committedColumnCount,
            int rolledBackColumnCount)
        {
            bool isEnglish = LanguageManager.IsEnglish;
            string content = isEnglish
                ? $"Completed columns: {committedColumnCount}\nRolled back columns: {rolledBackColumnCount}\n\nNo partial reinforcement was kept for a failed column. Open the details for Revit failure records."
                : $"Cột hoàn tất: {committedColumnCount}\nCột đã rollback: {rolledBackColumnCount}\n\nKhông giữ lại thép dở dang cho cột lỗi. Mở chi tiết để xem bản ghi lỗi Revit.";
            string details = string.Join(
                Environment.NewLine + Environment.NewLine,
                report.Errors.Select(error => error.ErrorReason));

            KhimDialogHelper.ShowError(
                isEnglish ? "COLUMN REBAR FAILED" : "TẠO THÉP CỘT THẤT BẠI",
                content,
                details);
        }

        private RebarBarType FindBarType(string label) =>
            new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(t => t.Name.Equals(label, StringComparison.OrdinalIgnoreCase));

        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool isEn = LanguageManager.IsEnglish;

            // K-TOOLS Engineering Color Palette
            var cBg      = Color.FromArgb(247, 248, 250);
            var cOutline = Color.FromArgb(38,  50,  56);
            var cBar     = Color.FromArgb(21,  101, 192);
            var cTie     = Color.FromArgb(211, 47,  47);
            var cDim     = Color.FromArgb(84,  110, 122);
            var cConc    = Color.FromArgb(236, 239, 241);
            var cHatch   = Color.FromArgb(189, 199, 205);
            var cHdr     = Color.FromArgb(38,  50,  56);
            var cOk      = Color.FromArgb(27,  94,  32);
            var cWarn    = Color.FromArgb(183, 28,  28);

            // Data from form controls
            double bMm = _previewWidthMm;
            double hMm = _previewDepthMm;
            double heightMm = _previewHeightMm;
            string mark = _previewMark;
            string levelName = _previewLevelName ?? (isEn ? "Level unavailable" : "Cao do khong kha dung");

            int nB  = (int)(_numBarsB?.Value ?? 3);
            int nH  = (int)(_numBarsH?.Value ?? 3);
            int tot = 2 * (nB + nH - 2);
            double cover = _chkCustomCover?.Checked == true ? (double)(_numCustomCover?.Value ?? 25) : 25;
            double a1mm  = (double)(_numStirrupSpacingA1?.Value ?? 100);
            double a2mm  = (double)(_numStirrupSpacingA2?.Value ?? 200);
            bool diamond = _chkInnerDiamond?.Checked == true && nB >= 3 && nH >= 3;
            bool xcross  = _chkCrossLinks?.Checked   == true;
            bool isFdn   = _rdBaseFoundation?.Checked  == true;
            bool topHook = _chkTopAnchor?.Checked    == true;
            bool cranked = _chkCrankedSplice?.Checked == true;
            string mD = _cmbMainDia?.Text    ?? "?";
            string tD = _cmbStirrupDia?.Text ?? "?";

            using var fHdr = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold);
            using var fLbl = new Font("Segoe UI Semibold", 7f);
            using var fBdy = new Font("Segoe UI", 6.5f);
            using var fSml = new Font("Segoe UI", 6f);
            using var fMon = new Font("Consolas", 6.5f);
            using var sfC  = new StringFormat { Alignment = StringAlignment.Center,  LineAlignment = StringAlignment.Center };
            using var sfL  = new StringFormat { Alignment = StringAlignment.Near,    LineAlignment = StringAlignment.Center };

            int W = _previewPanel.Width;
            int H = _previewPanel.Height;
            const int HDR = 18;

            g.Clear(cBg);

            if (!string.IsNullOrEmpty(_previewHostError))
            {
                using var errorFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                using var errorBrush = new SolidBrush(cWarn);
                g.DrawString(_previewHostError, errorFont, errorBrush,
                    new RectangleF(8, HDR + 8, Math.Max(0, W - 16), Math.Max(0, H - HDR - 16)));
                return;
            }

            int topH  = Math.Max(200, (int)(H * 0.56));
            int leftW = Math.Max(200, (int)(W * 0.57));

            using (var hb = new SolidBrush(cHdr))
            {
                g.FillRectangle(hb, 0,     0,    leftW,     HDR);
                g.FillRectangle(hb, leftW, 0,    W - leftW, HDR);
                g.FillRectangle(hb, 0,     topH, W,         HDR);
            }

            string secTitle = ColumnSchematicDisclosure;
            string infoTitle = isEn ? "ENGINEERING DATA" : "DU LIEU KY THUAT";
            string elvTitle  = isEn ? "COLUMN ELEVATION  -  Stirrup Zones A1 / A2 / A1"
                                    : "MAT DUNG COT  -  Vung dai A1 / A2 / A1";
            g.DrawString(secTitle,  fHdr, Brushes.White, 5f, 4f);
            g.DrawString(infoTitle, fHdr, Brushes.White, leftW + 5f, 4f);
            g.DrawString(elvTitle,  fHdr, Brushes.White, 5f, topH + 4f);

            using (var dp = new Pen(Color.FromArgb(198, 210, 216), 1f))
            {
                g.DrawLine(dp, 0,     topH, W,     topH);
                g.DrawLine(dp, leftW, HDR,  leftW, topH);
            }

            // I. CROSS-SECTION (left panel)
            {
                int ax = 5, ay = HDR + 3, aw = leftW - ax - 3, ah = topH - ay - 4;
                int dimHLeft = 28, dimVTop = 18;
                int mxW = Math.Max(60, aw - dimHLeft - 8);
                int mxH = Math.Max(40, ah - dimVTop  - 18);

                double ratio = (hMm > 0 && bMm > 0) ? bMm / hMm : 1.0;
                int sW, sH;
                if (ratio >= 1.0) { sW = mxW; sH = (int)(mxW / ratio); if (sH > mxH) { sH = mxH; sW = (int)(mxH * ratio); } }
                else              { sH = mxH; sW = (int)(mxH * ratio); if (sW > mxW) { sW = mxW; sH = (int)(mxW / ratio); } }
                sW = Math.Max(60, Math.Min(sW, mxW));
                sH = Math.Max(40, Math.Min(sH, mxH));

                int sX = ax + dimHLeft + (mxW - sW) / 2;
                int sY = ay + dimVTop  + (mxH - sH) / 2;

                using (var cb = new SolidBrush(cConc)) g.FillRectangle(cb, sX, sY, sW, sH);
                g.SetClip(new System.Drawing.Rectangle(sX, sY, sW, sH));
                using (var hp = new Pen(cHatch, 0.7f))
                    for (int d = -(sH + 2); d < sW + 2; d += 10)
                        g.DrawLine(hp, sX + d, sY, sX + d + sH, sY + sH);
                g.ResetClip();
                using (var op = new Pen(cOutline, 2f)) g.DrawRectangle(op, sX, sY, sW, sH);

                double pixelsPerMm = Math.Min(sW / Math.Max(bMm, 1e-6), sH / Math.Max(hMm, 1e-6));
                double tieCoverOffsetMm = cover + _previewStirrupDiameterMm / 2.0;
                int cv = Math.Max(1, (int)Math.Round(tieCoverOffsetMm * pixelsPerMm));
                int iX = sX + cv, iY = sY + cv, iW = sW - 2 * cv, iH = sH - 2 * cv;

                using (var tp = new Pen(cTie, 2f)) g.DrawRectangle(tp, iX, iY, iW, iH);
                // Local rebar safety default: show the two closed inner cells for MultiCellClosed.
                // Legacy diamond and advanced cross-link options remain opt-in below.
                if (nB >= 3 && nH >= 3)
                {
                    int cellInset = Math.Max(3, Math.Min(iW, iH) / 16);
                    int cellWidth = Math.Max(8, iW / 4);
                    int cellHeight = Math.Max(8, iH - cellInset * 2);
                    int leftCellX = iX + Math.Max(cellInset, iW / 10);
                    int rightCellX = iX + iW - Math.Max(cellInset, iW / 10) - cellWidth;
                    int cellY = iY + cellInset;

                    using var innerCellPen = new Pen(Color.OrangeRed, 1.5f);
                    g.DrawRectangle(innerCellPen, leftCellX, cellY, cellWidth, cellHeight);
                    g.DrawRectangle(innerCellPen, rightCellX, cellY, cellWidth, cellHeight);
                }

                if (diamond)
                {
                    using var dp2 = new Pen(cTie, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                    g.DrawPolygon(dp2, new PointF[]
                    {
                        new PointF(iX + iW / 2f, iY),
                        new PointF(iX + iW,       iY + iH / 2f),
                        new PointF(iX + iW / 2f,  iY + iH),
                        new PointF(iX,            iY + iH / 2f)
                    });
                }

                if (xcross)
                {
                    using var cp = new Pen(Color.FromArgb(180, 28, 28), 1.2f)
                        { DashStyle = System.Drawing.Drawing2D.DashStyle.DashDot };
                    for (int i = 1; i < nB - 1; i++)
                    {
                        float lx = iX + (float)i / Math.Max(nB - 1, 1) * iW;
                        g.DrawLine(cp, lx, iY, lx, iY + iH);
                    }
                    for (int j = 1; j < nH - 1; j++)
                    {
                        float ly = iY + (float)j / Math.Max(nH - 1, 1) * iH;
                        g.DrawLine(cp, iX, ly, iX + iW, ly);
                    }
                }

                double mainBarInsetMm = cover + _previewStirrupDiameterMm + _previewMainBarDiameterMm / 2.0;
                int barInset = Math.Max(1, (int)Math.Round(mainBarInsetMm * pixelsPerMm));
                int barLeft = sX + barInset, barRight = sX + sW - barInset;
                int barTop = sY + barInset, barBottom = sY + sH - barInset;
                int dr = Math.Max(2, (int)Math.Round(_previewMainBarDiameterMm * pixelsPerMm));
                using (var bf = new SolidBrush(cBar))
                using (var bw = new Pen(Color.White, 1.2f))
                {
                    void Dot(float x, float y)
                    {
                        g.FillEllipse(bf, x - dr / 2f, y - dr / 2f, dr, dr);
                        g.DrawEllipse(bw, x - dr / 2f, y - dr / 2f, dr, dr);
                    }
                    for (int i = 0; i < nB; i++)
                    {
                        float bx = barLeft + (float)i / Math.Max(nB - 1, 1) * (barRight - barLeft);
                        Dot(bx, barTop); Dot(bx, barBottom);
                    }
                    for (int j = 1; j < nH - 1; j++)
                    {
                        float by = barTop + (float)j / Math.Max(nH - 1, 1) * (barBottom - barTop);
                        Dot(barLeft, by); Dot(barRight, by);
                    }
                }

                using var dimPen = new Pen(cDim, 1f);
                using var dimBr  = new SolidBrush(cDim);

                int bdY = sY - 11;
                g.DrawLine(dimPen, sX, bdY, sX + sW, bdY);
                g.DrawLine(dimPen, sX, sY, sX, bdY);
                g.DrawLine(dimPen, sX + sW, sY, sX + sW, bdY);
                g.DrawLine(dimPen, sX,      bdY - 3, sX,      bdY + 3);
                g.DrawLine(dimPen, sX + sW, bdY - 3, sX + sW, bdY + 3);
                g.DrawString(string.Format("B = {0:0} mm", bMm), fBdy, dimBr,
                    new RectangleF(sX, bdY - 13, sW, 12), sfC);

                int hdX = sX - 11;
                g.DrawLine(dimPen, hdX, sY,      hdX, sY + sH);
                g.DrawLine(dimPen, hdX, sY,      sX, sY);
                g.DrawLine(dimPen, hdX, sY + sH, sX, sY + sH);
                g.DrawLine(dimPen, hdX - 3, sY,      hdX + 3, sY);
                g.DrawLine(dimPen, hdX - 3, sY + sH, hdX + 3, sY + sH);
                var savedTf = g.Transform;
                g.TranslateTransform(hdX - 11f, sY + sH / 2f);
                g.RotateTransform(-90f);
                g.DrawString(string.Format("H = {0:0} mm", hMm), fBdy, dimBr,
                    new RectangleF(-sH / 2f, -11, sH, 12), sfC);
                g.Transform = savedTf;

                using var cvp = new Pen(cDim, 0.7f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                g.DrawLine(cvp, sX + sW + 3, sY, sX + sW + 3, iY);
                g.DrawString(string.Format("c={0:0}", cover), fSml, dimBr, sX + sW + 5, sY + 2);

                string legText = isEn
                    ? string.Format("{0}ph{1}  ({2}B + {3}H)  Tie: ph{4}", tot, mD, nB, nH, tD)
                    : string.Format("{0}ph{1}  ({2}xB + {3}xH)  Dai: ph{4}", tot, mD, nB, nH, tD);
                legText = legText.Replace("ph", "\u03a6");
                using var sfLeg = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(legText, fSml, new SolidBrush(cOutline),
                    new RectangleF(ax, sY + sH + 5, aw, 15), sfLeg);
            }

            // II. ENGINEERING INFO PANEL (right)
            {
                int px = leftW + 4, py = HDR + 3;
                int pw2 = W - leftW - 7, ph2 = topH - py - 3;

                using (var ib = new SolidBrush(Color.FromArgb(250, 251, 252)))
                    g.FillRectangle(ib, px, py, pw2, ph2);
                using (var ib2 = new Pen(Color.FromArgb(210, 218, 224), 1f))
                    g.DrawRectangle(ib2, px, py, pw2, ph2);

                int tx = px + 7, ty = py + 7;

                string sm = mark.Length > 15 ? mark.Substring(0, 14) + "..." : mark;
                using (var ob = new SolidBrush(cOutline))
                using (var db = new SolidBrush(cDim))
                {
                    g.DrawString(isEn ? "Column" : "Cot", fBdy, db, tx, ty); ty += 8;
                    g.DrawString(sm, fHdr, ob, tx, ty); ty += 13;
                    g.DrawString(levelName, fBdy, db, tx, ty); ty += 16;
                }

                void Div()
                {
                    using var dpd = new Pen(Color.FromArgb(218, 225, 230), 1f);
                    g.DrawLine(dpd, tx, ty + 2, px + pw2 - 5, ty + 2);
                    ty += 7;
                }
                void Row(string lbl, string val, bool bold = false)
                {
                    using var db2 = new SolidBrush(cDim);
                    using var ob2 = new SolidBrush(cOutline);
                    g.DrawString(lbl, fBdy, db2, tx, ty);
                    g.DrawString(val, bold ? fLbl : fMon, ob2, tx, ty + 8);
                    ty += 19;
                }

                Div();
                Row(isEn ? "B x H" : "Tiet dien B x H",
                    string.Format("{0:0} x {1:0} mm", bMm, hMm), true);
                Row(isEn ? "Height" : "Chieu cao", string.Format("{0:0} mm", heightMm));
                Div();
                Row(isEn ? "Main bars" : "Thep chu",
                    string.Format("{0} \u03a6{1}", tot, mD), true);
                Row(isEn ? "  B-side" : "  Canh B", string.Format("{0} bars", nB));
                Row(isEn ? "  H-side" : "  Canh H", string.Format("{0} bars", nH));
                Div();
                Row(isEn ? "Stirrups" : "Thep dai", string.Format("\u03a6{0}", tD), true);
                Row(isEn ? "  Zone A1 (dense)" : "  Vung A1 (day)",
                    string.Format("@{0:0} mm", a1mm));
                Row(isEn ? "  Zone A2 (spare)" : "  Vung A2 (thua)",
                    string.Format("@{0:0} mm", a2mm));
                Row(isEn ? "Cover" : "Lop bao ve",
                    string.Format("c = {0:0} mm", cover));
                Div();

                var flags = new List<string>();
                if (diamond)  flags.Add(isEn ? "Diamond" : "Dai thoi");
                if (xcross)   flags.Add(isEn ? "CrossLink" : "Dai C");
                if (isFdn)    flags.Add(isEn ? "Foundation" : "Mong");
                if (topHook)  flags.Add(isEn ? "90deg hook" : "Moc 90deg");
                if (cranked)  flags.Add("1:6");
                string fs = flags.Count > 0 ? string.Join("  ", flags) : "-";
                using (var db3 = new SolidBrush(cDim))
                    g.DrawString(isEn ? "Options" : "Tuy chon", fBdy, db3, tx, ty);
                ty += 8;
                using (var ob3 = new SolidBrush(cOutline))
                    g.DrawString(fs, fSml, ob3, new RectangleF(tx, ty, pw2 - 10, 22), sfL);
                ty += 22;
                Div();

                bool ok = a1mm <= a2mm && nB >= 2 && nH >= 2 && cover >= 15;
                int stY = Math.Min(ty, py + ph2 - 20);
                using (var stb = new SolidBrush(ok
                    ? Color.FromArgb(200, 232, 244, 234)
                    : Color.FromArgb(200, 255, 235, 235)))
                    g.FillRectangle(stb, px + 4, stY, pw2 - 8, 18);
                string stMsg = ok
                    ? (isEn ? "OK  Configuration valid" : "OK  Cau hinh hop le")
                    : (a1mm > a2mm
                        ? (isEn ? "! A1 spacing > A2!"   : "! A1 > A2!")
                        : (isEn ? "! Check inputs"        : "! Kiem tra"));
                using (var stbr = new SolidBrush(ok ? cOk : cWarn))
                    g.DrawString(stMsg, fLbl, stbr, px + 8, stY + 4);
            }

            // III. COLUMN ELEVATION SCHEMATIC (bottom panel)
            {
                int botY = topH + HDR + 4;
                int eh   = H - topH - HDR - 10;
                if (eh < 40) return;

                int colW = Math.Min(36, Math.Max(20, (int)(W * 0.065)));
                int colH = eh - 16;
                int colX = W / 5 - colW / 2;
                int colY = botY + 4;

                using (var cf = new SolidBrush(cConc))  g.FillRectangle(cf, colX, colY, colW, colH);
                using (var co = new Pen(cOutline, 1.5f)) g.DrawRectangle(co, colX, colY, colW, colH);

                int a1TopPx = (int)(colH * 0.26);
                int a1BotPx = (int)(colH * 0.26);

                using (var sp = new Pen(cTie, 1.3f))
                {
                    for (int sy = colY + colH - 3; sy > colY + colH - a1BotPx; sy -= 5)
                        g.DrawLine(sp, colX - 2, sy, colX + colW + 2, sy);
                    for (int sy = colY + colH - a1BotPx - 4; sy > colY + a1TopPx + 2; sy -= 12)
                        g.DrawLine(sp, colX - 2, sy, colX + colW + 2, sy);
                    for (int sy = colY + a1TopPx; sy >= colY + 2; sy -= 5)
                        g.DrawLine(sp, colX - 2, sy, colX + colW + 2, sy);
                }

                int bL = colX + 7, bR = colX + colW - 7;
                using var rp   = new Pen(cBar, 2f);
                using var dimB = new SolidBrush(cDim);

                if (isFdn)
                {
                    int fl = 14;
                    g.DrawLine(rp, bL - fl, colY + colH + 10, bL, colY + colH + 10);
                    g.DrawLine(rp, bL, colY + colH + 10, bL, colY);
                    g.DrawLine(rp, bR + fl, colY + colH + 10, bR, colY + colH + 10);
                    g.DrawLine(rp, bR, colY + colH + 10, bR, colY);
                    g.DrawString(isEn ? "L-bend" : "Chan quy", fSml, dimB, colX - 10, colY + colH + 1);
                }
                else
                {
                    if (cranked)
                    {
                        int crY1 = colY + colH - a1BotPx - 4;
                        int crY2 = crY1 - 10;
                        g.DrawLine(rp, bL, colY + colH, bL, crY1);
                        g.DrawLine(rp, bL, crY1, bL - 4, crY2);
                        g.DrawLine(rp, bL - 4, crY2, bL - 4, colY);
                        g.DrawLine(rp, bR, colY + colH, bR, crY1);
                        g.DrawLine(rp, bR, crY1, bR + 4, crY2);
                        g.DrawLine(rp, bR + 4, crY2, bR + 4, colY);
                        g.DrawString("1:6", fSml, dimB, bR + 5, crY2);
                    }
                    else
                    {
                        g.DrawLine(rp, bL, colY + colH, bL, colY);
                        g.DrawLine(rp, bR, colY + colH, bR, colY);
                    }
                    using var dp4 = new Pen(cBar, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                    g.DrawLine(dp4, bL, colY + colH, bL, colY + colH + 12);
                    g.DrawLine(dp4, bR, colY + colH, bR, colY + colH + 12);
                }

                if (topHook)
                {
                    g.DrawLine(rp, bL, colY, bL + 10, colY);
                    g.DrawLine(rp, bR, colY, bR - 10, colY);
                    g.DrawString(isEn ? "90deg hook" : "Moc 90deg", fSml, dimB, colX - 8, colY - 12);
                }
                else
                {
                    using var dp5 = new Pen(cBar, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                    g.DrawLine(dp5, bL, colY, bL, colY - 10);
                    g.DrawLine(dp5, bR, colY, bR, colY - 10);
                }

                int bkX = colX + colW + 10;
                using var bkp = new Pen(cDim, 0.8f);
                void Bracket(int y1, int y2, string lbl)
                {
                    g.DrawLine(bkp, bkX, y1, bkX, y2);
                    g.DrawLine(bkp, bkX, y1, bkX + 3, y1);
                    g.DrawLine(bkp, bkX, y2, bkX + 3, y2);
                    g.DrawString(lbl, fSml, dimB, bkX + 5, (y1 + y2) / 2 - 7);
                }
                Bracket(colY,                  colY + a1TopPx,        string.Format("A1 @{0:0}", a1mm));
                Bracket(colY + a1TopPx,        colY + colH - a1BotPx, string.Format("A2 @{0:0}", a2mm));
                Bracket(colY + colH - a1BotPx, colY + colH,           string.Format("A1 @{0:0}", a1mm));

                using var lvp = new Pen(cDim, 0.8f) { DashStyle = System.Drawing.Drawing2D.DashStyle.DashDot };
                g.DrawLine(lvp, colX - 14, colY + colH, bkX + 60, colY + colH);
                g.DrawString(string.Format("v {0}", levelName), fSml, dimB, bkX + 62, colY + colH - 6);

                using var hap = new Pen(cDim, 0.8f);
                int hax = colX - 8;
                g.DrawLine(hap, hax, colY,        hax, colY + colH);
                g.DrawLine(hap, hax - 3, colY,        hax + 3, colY);
                g.DrawLine(hap, hax - 3, colY + colH, hax + 3, colY + colH);

                using var sfCen = new StringFormat { Alignment = StringAlignment.Center };
                int lblW = colX - 6;
                g.DrawString(mark,
                    fLbl, new SolidBrush(cOutline),
                    new RectangleF(0, botY + 4,  lblW, 14), sfCen);
                g.DrawString(string.Format("{0:0} mm", heightMm),
                    fSml, new SolidBrush(cDim),
                    new RectangleF(0, botY + 18, lblW, 12), sfCen);
            }
        }
        private void ApplyLanguage()
        {
            bool isEn = LanguageManager.IsEnglish;

            Text = isEn ? "Rectangular Column Reinforcement" : "Bố trí Thép Cột Vuông / Chữ Nhật";

            if (_tabMain != null) _tabMain.Text = isEn ? "Main Rebar & Preview" : "Thép Chủ & Xem Trước";
            if (_tabStirrup != null) _tabStirrup.Text = isEn ? "Stirrups" : "Thép Đai";
            if (_tabGenSettings != null) _tabGenSettings.Text = isEn ? "General Settings" : "Cài Đặt Chung";
            if (_tabViews != null) _tabViews.Text = isEn ? "Drawing & Views" : "Bản Vẽ & Khung Nhìn";

            // Tab 1 Main
            if (_grpMainSection != null) _grpMainSection.Text = isEn ? "Main Rebar Arrangement" : "Bố trí Thép Chủ Tiết Diện";
            if (_lblBarsB != null) _lblBarsB.Text = isEn ? "Main rebar side B (incl. corners):" : "Thép chủ cạnh B (kể cả góc):";
            if (_lblBarsH != null) _lblBarsH.Text = isEn ? "Main rebar side H (incl. corners):" : "Thép chủ cạnh H (kể cả góc):";
            if (_lblMainDia != null) _lblMainDia.Text = isEn ? "Main rebar diameter:" : "Đường kính thép chủ:";

            if (_grpCover != null) _grpCover.Text = isEn ? "Concrete Cover" : "Lớp Bê Tông Bảo Vệ";
            if (_chkCustomCover != null) _chkCustomCover.Text = isEn ? "Custom Cover (mm)" : "Nhập tay Lớp Bảo Vệ (mm)";
            if (_lblCustomCover != null) _lblCustomCover.Text = isEn ? "Custom Cover (mm):" : "Lớp bảo vệ tùy chỉnh (mm):";
            if (_btnProjectCover != null) _btnProjectCover.Text = isEn ? "Project Cover" : "Lớp Bảo Vệ Dự Án";

            if (_grpMainAnchor != null) _grpMainAnchor.Text = isEn ? "Anchorage & Lap Splice Detailing" : "Cấu tạo Neo & Nối Thép";
            if (_rdBaseStandardLevel != null) _rdBaseStandardLevel.Text = isEn ? "Typical / Floor Column (Continuous dowel)" : "Cột tầng sàn / điển hình (Thép chờ nối tầng)";
            if (_rdBaseFoundation != null) _rdBaseFoundation.Text = isEn ? "Base / Foundation Column (90° Footing L-bend)" : "Cột tầng móng (Nối chân quỳ 90° vào móng)";
            if (_chkCrankedSplice != null) _chkCrankedSplice.Text = isEn ? "1:6 Cranked offset splice at joint" : "Nhấn vắt nghiêng 1:6 tại vị trí nối";
            if (_chkTopAnchor != null) _chkTopAnchor.Text = isEn ? "90° Inward hook for roof column" : "Neo uốn móc 90° đỉnh mái";
            if (_chkStaggeredSplice != null) _chkStaggeredSplice.Text = isEn ? "50% Staggered lap splice (1.3 Ls)" : "Nối so le 50% (Cách 1.3 Ls)";

            // Tab 2 Stirrups
            if (_grpStirrupZone != null) _grpStirrupZone.Text = isEn ? "Stirrup Distribution A1 / A2 / A1 (Structural Standard)" : "Phân Vùng Đai A1 / A2 / A1 (Chuẩn Kết Cấu)";
            if (_lblStirrupDia != null) _lblStirrupDia.Text = isEn ? "Stirrup bar diameter:" : "Đường kính thép đai:";
            if (_lblStirrupA1 != null) _lblStirrupA1.Text = isEn ? "Dense A1 stirrup spacing (mm):" : "Khoảng cách đai dày A1 (mm):";
            if (_lblStirrupA2 != null) _lblStirrupA2.Text = isEn ? "Sparse A2 stirrup spacing (mm):" : "Khoảng cách đai thưa A2 (mm):";
            if (_lblZoneA1Len != null) _lblZoneA1Len.Text = isEn ? "Dense A1 zone length (mm):" : "Chiều dài vùng đai dày A1 (mm):";

            if (_grpInnerStirrup != null) _grpInnerStirrup.Text = isEn ? "Legacy / Advanced Tie Options" : "Tùy chọn đai Legacy / Nâng cao";
            if (_chkInnerDiamond != null) _chkInnerDiamond.Text = isEn ? "Legacy — diamond tie JP_T80" : "Legacy — đai lồng / đai thoi JP_T80";
            if (_chkCrossLinks != null) _chkCrossLinks.Text = isEn ? "Advanced — crosslinks / C-links JP_T68" : "Nâng cao — đai móc phụ / Crosslink JP_T68";

            // Tab 3 General Settings
            if (_grpHook != null) _grpHook.Text = (isEn ? "REBAR HOOK BENDING SECTION" : "CẤU TẠO UỐN MÓC THÉP") + (isEn ? " (not applied)" : " (chưa áp dụng)");
            if (_rdHookLengthFixed != null) _rdHookLengthFixed.Text = isEn ? "By fixed length L (mm):" : "Theo chiều dài cố định L (mm):";
            if (_rdHookLengthDia != null) _rdHookLengthDia.Text = isEn ? "By diameter (xD):" : "Theo đường kính thanh (xD):";

            if (_grpBendCut != null) _grpBendCut.Text = (isEn ? "REBAR BENDING OR CUTTING CONDITIONS" : "ĐIỀU KIỆN UỐN HOẶC CẮT THÉP") + (isEn ? " (not applied)" : " (chưa áp dụng)");
            if (_lblBendE != null) _lblBendE.Text = isEn ? "Bend rebar if offset e ≤ (mm):" : "Uốn thép nếu độ lệch e ≤ (mm):";
            if (_lblBendRatio != null) _lblBendRatio.Text = isEn ? "Bend slope ratio Hd/e ≥:" : "Tỷ lệ độ dốc uốn Hd/e ≥:";

            if (_grpTopRoof != null) _grpTopRoof.Text = (isEn ? "SET TOP ROOF REBAR" : "KẾT THÚC THÉP ĐỈNH MÁI") + (isEn ? " (not applied)" : " (chưa áp dụng)");
            if (_rdTopRoofHook != null) _rdTopRoofHook.Text = isEn ? "Bend hook for top floor rebar" : "Bẻ móc cho thép tầng đỉnh mái";
            if (_rdTopRoofContinue != null) _rdTopRoofContinue.Text = isEn ? "Continue straight for next level" : "Chờ thẳng cho tầng tiếp theo";

            if (_grpSplicePos != null) _grpSplicePos.Text = (isEn ? "REBAR SPLICE POSITION" : "VỊ TRÍ NỐI THÉP CỘT") + (isEn ? " (not applied)" : " (chưa áp dụng)");
            if (_lblSpliceDist != null) _lblSpliceDist.Text = isEn ? "Splice distance from column base L = (mm):" : "Khoảng cách nối từ chân cột L = (mm):";

            if (_grpAssignInfo != null) _grpAssignInfo.Text = (isEn ? "ASSIGN ADDITIONAL INFORMATION TO REBAR" : "GÁN THÔNG TIN BỔ SUNG CHO THÉP") + (isEn ? " (not applied)" : " (chưa áp dụng)");
            if (_chkAssignElevation != null) _chkAssignElevation.Text = isEn ? "Assign column elevation to rebar" : "Gán cao độ cột vào thông số thép";
            if (_chkAssignPartition != null) _chkAssignPartition.Text = isEn ? "Automatically assign Partition to rebar" : "Tự động gán Phân vùng (Partition) cho thép";

            if (_grpSlabBeam != null) _grpSlabBeam.Text = (isEn ? "OPTION AT SLAB BEAM POSITION" : "TÙY CHỌN TẠI VỊ TRÍ DẦM / SÀN") + (isEn ? " (not applied)" : " (chưa áp dụng)");
            if (_lblDefaultHd != null) _lblDefaultHd.Text = isEn ? "Default beam height Hd (mm):" : "Chiều cao dầm mặc định Hd (mm):";

            // Tab 4 Drawing
            if (_grpViews != null) _grpViews.Text = isEn ? "Drawing & View Options" : "Tự Động Tạo Khung Nhìn & Bản Vẽ";
            if (_chkAutoDrawing != null) _chkAutoDrawing.Text = isEn ? "Automatically generate 2D section drawing & BBS" : "Tự động tạo bản vẽ 2D (Mặt cắt tiết diện & Thống kê thép)";
            if (_chkAutoSection3D != null) _chkAutoSection3D.Text = isEn ? "Create 3D Inspection Views (Plan View + 3D View)" : "Tự động tạo Khung nhìn xem thép 3D (Mặt bằng + 3D)";

            // Right & Bottom Panels
            if (_lblColTitle != null) _lblColTitle.Text = isEn ? "Column List" : "Danh Sách Cột";
            if (_lblTemplate != null) _lblTemplate.Text = isEn ? "Configuration Template:" : "Mẫu Thiết Lập:";
            if (_btnSaveTemplate != null) _btnSaveTemplate.Text = isEn ? "Save..." : "Lưu mẫu...";
            if (_btnApplyTemplate != null) _btnApplyTemplate.Text = isEn ? "Apply" : "Áp dụng";
            if (_btnDeleteTemplate != null) _btnDeleteTemplate.Text = isEn ? "Delete" : "Xóa mẫu";
            if (_rdScopeSelected != null) _rdScopeSelected.Text = isEn ? $"Selected columns ({_preSelectedColumns.Count})" : $"Chỉ các cột đã chọn ({_preSelectedColumns.Count})";
            if (_rdScopeAll != null) _rdScopeAll.Text = isEn ? $"All model columns ({_availableColumns.Count})" : $"Tất cả cột ({_availableColumns.Count})";
            if (_btnSelectAll != null) _btnSelectAll.Text = isEn ? "Select All" : "Chọn Tất Cả";
            if (_btnDeselectAll != null) _btnDeselectAll.Text = isEn ? "Deselect All" : "Bỏ Chọn";

            if (_btnCreateRebar != null) _btnCreateRebar.Text = isEn ? "Create Rebar" : "Tạo Thép";
            if (_btnPreview3D != null) _btnPreview3D.Text = isEn ? "Solve preview" : "Giải xem trước";
            if (_btnClose != null) _btnClose.Text = isEn ? "Close" : "Đóng";

            if (_workflowTabs != null && _workflowTabs.TabPages.Count > 0)
                RebarConfigurationPage.ApplyLanguage(_workflowTabs.TabPages[_workflowTabs.TabPages.Count - 1]);

            UpdateSelectedCount();
            UpdatePreviewStateUi();
            UpdateWorkflowNavigation();
            _formGuard?.ApplyLanguage();
            _previewPanel?.Invalidate();
        }

        private void LoadTemplateList()
        {
            _cmbTemplate.Items.Clear();
            var names = RebarTemplateManager.ListColumnTemplates();
            _cmbTemplate.Items.AddRange(names.ToArray());
            if (names.Any()) _cmbTemplate.SelectedIndex = 0;
        }

        private void SaveTemplate()
        {
            string name = KhimTools.Core.KhimPrompt.ShowDialog(
                LanguageManager.IsEnglish ? "Enter template name:" : "Nhập tên mẫu thiết lập:",
                LanguageManager.IsEnglish ? "Save Template" : "Lưu Mẫu Thiết Lập",
                "New_Template");

            if (string.IsNullOrWhiteSpace(name)) return;

            var settings = new ColumnRebarSettings
            {
                Name = name.Trim(),
                DesignStandard = "Eurocode2",
                ConcreteGrade = "C30/37",
                SteelGrade = "B500B",
                MainBarType = _cmbMainDia.Text,
                StirrupBarType = _cmbStirrupDia.Text,
                BarsAlongB = (int)_numBarsB.Value,
                BarsAlongH = (int)_numBarsH.Value,
                StirrupSpacingA1 = (double)_numStirrupSpacingA1.Value,
                StirrupSpacingA2 = (double)_numStirrupSpacingA2.Value,
                ZoneA1Length = (double)_numZoneA1Length.Value,
                IsCustomCover = _chkCustomCover.Checked,
                CustomCover = (double)_numCustomCover.Value,
                LapLengthMultiplier = (double)_numLapMultiplier.Value,
                EnableCrankedSplice = _chkCrankedSplice.Checked,
                HasTopAnchor = _chkTopAnchor.Checked,
                IsFoundationColumn = _rdBaseFoundation.Checked,
                HasDowel = !_rdBaseFoundation.Checked,
                StaggeredSplice = _chkStaggeredSplice.Checked,
                TieLayout = GetSelectedTieLayoutType(),
                HasInnerDiamondStirrup = _chkInnerDiamond.Checked,
                HasCrossLinks = _chkCrossLinks.Checked
            };

            RebarTemplateManager.SaveColumnTemplate(settings);
            LoadTemplateList();
            _cmbTemplate.Text = settings.Name;
        }

        private void ApplyTemplate()
        {
            string name = _cmbTemplate.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            var settings = RebarTemplateManager.LoadColumnTemplate(name);
            if (settings == null) return;

            SetComboValue(_cmbMainDia, settings.MainBarType);
            SetComboValue(_cmbStirrupDia, settings.StirrupBarType);
            _numBarsB.Value = Math.Max(_numBarsB.Minimum, Math.Min(_numBarsB.Maximum, settings.BarsAlongB));
            _numBarsH.Value = Math.Max(_numBarsH.Minimum, Math.Min(_numBarsH.Maximum, settings.BarsAlongH));
            _numStirrupSpacingA1.Value = (decimal)settings.StirrupSpacingA1;
            _numStirrupSpacingA2.Value = (decimal)settings.StirrupSpacingA2;
            _numZoneA1Length.Value = (decimal)settings.ZoneA1Length;
            _chkCustomCover.Checked = settings.IsCustomCover;
            _numCustomCover.Value = (decimal)settings.CustomCover;
            _numLapMultiplier.Value = (decimal)settings.LapLengthMultiplier;
            _chkCrankedSplice.Checked = settings.EnableCrankedSplice;
            _chkTopAnchor.Checked = settings.HasTopAnchor;
            
            if (settings.IsFoundationColumn)
            {
                _rdBaseFoundation.Checked = true;
                _rdBaseStandardLevel.Checked = false;
            }
            else
            {
                _rdBaseFoundation.Checked = false;
                _rdBaseStandardLevel.Checked = true;
            }

            _chkStaggeredSplice.Checked = settings.StaggeredSplice;
            ColumnTieLayoutType layout = settings.TieLayout;
            // Old templates have no TieLayout field. Their old flags remain an
            // explicit legacy request; otherwise the new default is MultiCellClosed.
            if (settings.HasInnerDiamondStirrup) layout = ColumnTieLayoutType.DiamondLegacy;
            else if (settings.HasCrossLinks) layout = ColumnTieLayoutType.CrossTie;
            _chkInnerDiamond.Checked = layout == ColumnTieLayoutType.DiamondLegacy;
            _chkCrossLinks.Checked = layout == ColumnTieLayoutType.CrossTie;

            _previewPanel?.Invalidate();
        }

        private void DeleteTemplate()
        {
            string name = _cmbTemplate.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            RebarTemplateManager.DeleteColumnTemplate(name);
            LoadTemplateList();
        }

        private void SetComboValue(ComboBox combo, string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i].ToString().Equals(val, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private DesignCode GetSelectedDesignStandard() => DesignCode.Eurocode2;
        private ConcreteGrade GetSelectedConcreteGrade() => ConcreteGrade.C30_37;
        private SteelGrade GetSelectedSteelGrade() => SteelGrade.B500;

        private class ColumnListItem
        {
            public FamilyInstance Column { get; }
            private readonly string _label;
            public ColumnListItem(FamilyInstance col, string label) { Column = col; _label = label; }
            public override string ToString() => _label;
        }
    }
}

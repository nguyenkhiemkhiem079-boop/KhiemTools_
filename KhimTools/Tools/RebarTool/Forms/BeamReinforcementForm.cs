using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.RebarTool.Core;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Control = System.Windows.Forms.Control;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using FontStyle = System.Drawing.FontStyle;
using Button = System.Windows.Forms.Button;
using GroupBox = System.Windows.Forms.GroupBox;
using Label = System.Windows.Forms.Label;
using CheckBox = System.Windows.Forms.CheckBox;
using ComboBox = System.Windows.Forms.ComboBox;
using ListBox = System.Windows.Forms.ListBox;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>
    /// Form "Beam Reinforcement Layout" cao cấp cho Dầm (Structural Framing) v2.0.
    /// Tích hợp Live Preview GDI+ cho cả 3 Tab: Tiết diện dầm (Tab 1), Mặt cắt dọc thép tăng cường (Tab 2), Phân vùng đai (Tab 3).
    /// Hỗ trợ song ngữ Tiếng Việt 🇻🇳 / English 🇬🇧.
    /// </summary>
    public class BeamReinforcementForm : Form
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableBeams;
        private readonly List<FamilyInstance> _preSelectedBeams;

        // Selection & UI Controls
        private ListBox _beamListBox;
        private Label _lblSelectedCount;
        private Label _lblBeamTitle;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private ComboBox _cmbLanguage;

        // Configuration Templates
        private Label _lblTemplate;
        private ComboBox _cmbTemplate;
        private Button _btnSaveTemplate;
        private Button _btnApplyTemplate;
        private Button _btnDeleteTemplate;

        // Tabs
        private TabPage _tabMain;
        private TabPage _tabExtra;
        private TabPage _tabStirrup;
        private TabPage _tabViews;

        // GDI+ Preview Panels
        private Panel _previewPanelMain;
        private Panel _previewPanelExtra;
        private Panel _previewPanelStirrup;

        // Tab 1: Thép Chủ Chạy Suốt & Cover & Thép Sườn
        private GroupBox _grpMainSec;
        private Label _lblTopBars;
        private Label _lblBotBars;
        private Label _lblSideBars;

        private NumericUpDown _numTopQty;
        private ComboBox _cmbTopDia;
        private NumericUpDown _numBotQty;
        private ComboBox _cmbBotDia;

        private CheckBox _chkAutoSide;
        private NumericUpDown _numSideQty;
        private ComboBox _cmbSideDia;

        private GroupBox _grpCover;
        private Label _lblCustomCover;
        private CheckBox _chkCustomCover;
        private NumericUpDown _numCustomCover;
        private Button _btnProjectCover;

        // Tab 2: Thép Tăng Cường
        private GroupBox _grpExtra;
        private Label _lblTopLeftExtra;
        private Label _lblTopRightExtra;
        private Label _lblBotMidExtra;

        private NumericUpDown _numTopLeftExtra;
        private ComboBox _cmbTopLeftDia;

        private NumericUpDown _numTopRightExtra;
        private ComboBox _cmbTopRightDia;
        private NumericUpDown _numBotMidExtra;
        private ComboBox _cmbBotMidDia;

        private GroupBox _grpJointSettings;
        private Label _lblLdMultiplier;
        private Label _lblHookTailMultiplier;
        private NumericUpDown _numLdMultiplier;
        private NumericUpDown _numHookTailMultiplier;
        // Tab 3: Thép Đai
        private GroupBox _grpStirrup;
        private Label _lblStirrupDia;
        private Label _lblStirrupA1;
        private Label _lblStirrupA2;
        private Label _lblZoneA1Len;

        private ComboBox _cmbStirrupDia;
        private NumericUpDown _numStirrupSpacingA1;
        private NumericUpDown _numStirrupSpacingA2;
        private NumericUpDown _numZoneA1Length;

        // Tab 4: View & Bản vẽ
        private GroupBox _grpViews;
        private CheckBox _chkAutoDrawing;
        private CheckBox _chkAutoSection3D;

        private Button _btnCreateRebar;
        private Button _btnClose;

        // Design Standard Controls
        private ComboBox _cmbDesignStandard;
        private ComboBox _cmbConcreteGrade;
        private ComboBox _cmbSteelGrade;
        private Label _lblDesignWarning;
        private GroupBox _grpDesignStandard;

        public BeamReinforcementForm(Document doc, List<FamilyInstance> availableBeams, List<FamilyInstance> preSelectedBeams = null)
        {
            _doc = doc;
            _availableBeams = availableBeams ?? new List<FamilyInstance>();
            _preSelectedBeams = preSelectedBeams ?? new List<FamilyInstance>();
            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            PopulateBeamList();
            PopulateBarTypeCombos();
            LoadTemplateList();
            ApplyLanguage();
        }

        private void BuildUi()
        {
            Text = "📏 KHIM TOOLS — Bố trí Thép Dầm Kết Cấu";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Structural Beam Detailing",
                "Automated Beam Reinforcement Engine & Joint Anchorage Detailing",
                "v2.5 Pro");
            Controls.Add(header);

            // 1. Bottom Control Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.FromArgb(245, 245, 247) };
            var lblLang = new Label { Text = "🌐 Language:", AutoSize = true, Left = 15, Top = 18, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, Left = 95, Top = 14, Font = new Font("Segoe UI", 8.5F) };
            _cmbLanguage.Items.Add("🇻🇳 Tiếng Việt");
            _cmbLanguage.Items.Add("🇬🇧 English");
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;
            _cmbLanguage.SelectedIndexChanged += (s, e) =>
            {
                LanguageManager.CurrentLanguage = _cmbLanguage.SelectedIndex == 1 ? AppLanguage.English : AppLanguage.Vietnamese;
                ApplyLanguage();
            };

            _btnCreateRebar = new Button
            {
                Text = "⚡ Create Rebar",
                Width = 130,
                Height = 36,
                Top = 10,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnCreateRebar.FlatAppearance.BorderSize = 0;

            _btnClose = new Button
            {
                Text = "Close",
                Width = 90,
                Height = 36,
                Top = 10,
                BackColor = Color.FromArgb(225, 225, 230),
                FlatStyle = FlatStyle.Flat
            };
            _btnClose.FlatAppearance.BorderSize = 0;

            _btnCreateRebar.Click += BtnCreateRebar_Click;
            _btnClose.Click += (s, e) => Close();

            bottomPanel.Controls.Add(lblLang);
            bottomPanel.Controls.Add(_cmbLanguage);
            bottomPanel.Controls.Add(_btnCreateRebar);
            bottomPanel.Controls.Add(_btnClose);
            bottomPanel.Resize += (s, e) =>
            {
                _btnClose.Left = bottomPanel.Width - _btnClose.Width - 15;
                _btnCreateRebar.Left = _btnClose.Left - _btnCreateRebar.Width - 10;
            };
            Controls.Add(bottomPanel);

            // 2. Right Selection Panel
            var rightPanel = new Panel { Dock = DockStyle.Right, Width = 230, Padding = new Padding(10), BackColor = Color.FromArgb(250, 250, 252) };
            _lblBeamTitle = new Label { Text = "📋 Danh Sách Dầm", Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            _lblSelectedCount = new Label { Text = "Đã chọn: 0 dầm", Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };

            var selectButtonsPanel = new Panel { Dock = DockStyle.Bottom, Height = 32 };
            _btnSelectAll = new Button { Text = "Select All", Width = 95, Height = 26, Top = 3, Left = 0, FlatStyle = FlatStyle.System };
            _btnDeselectAll = new Button { Text = "Clear", Width = 70, Height = 26, Top = 3, Left = 102, FlatStyle = FlatStyle.System };

            _btnSelectAll.Click += (s, e) => SetAllBeamsSelected(true);
            _btnDeselectAll.Click += (s, e) => SetAllBeamsSelected(false);

            selectButtonsPanel.Controls.Add(_btnSelectAll);
            selectButtonsPanel.Controls.Add(_btnDeselectAll);

            _beamListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.MultiExtended
            };
            _beamListBox.SelectedIndexChanged += (s, e) => UpdateSelectedCount();

            rightPanel.Controls.Add(_beamListBox);
            rightPanel.Controls.Add(selectButtonsPanel);
            rightPanel.Controls.Add(_lblSelectedCount);
            rightPanel.Controls.Add(_lblBeamTitle);
            Controls.Add(rightPanel);

            // 2.5 Top Template Configuration Panel
            var templatePanel = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(240, 240, 243), Padding = new Padding(6) };
            _lblTemplate = new Label { Text = "📋 Mẫu Thiết Lập:", AutoSize = true, Left = 15, Top = 14, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _cmbTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Left = 140, Top = 10 };
            
            _btnSaveTemplate = new Button { Text = "Save As...", Width = 90, Height = 26, Left = 350, Top = 9, FlatStyle = FlatStyle.System };
            _btnApplyTemplate = new Button { Text = "Apply", Width = 75, Height = 26, Left = 445, Top = 9, FlatStyle = FlatStyle.System };
            _btnDeleteTemplate = new Button { Text = "Delete", Width = 75, Height = 26, Left = 525, Top = 9, FlatStyle = FlatStyle.System };

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
            var tabControl = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };

            // --- TAB 1: THÉP CHỦ & COVER ---
            _tabMain = new TabPage { Text = "📌 Thép Chủ & Cover", Padding = new Padding(10), BackColor = Color.White };
            var pnlMainLeft = new Panel { Dock = DockStyle.Left, Width = 350 };

            _grpMainSec = new GroupBox { Text = "Thép Chủ Chạy Suốt Dầm", Dock = DockStyle.Top, Height = 170, Padding = new Padding(8) };
            var layoutMainSec = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            _numTopQty = new NumericUpDown { Minimum = 2, Maximum = 10, Value = 2, Width = 55 };
            _numTopQty.ValueChanged += (s, e) => _previewPanelMain?.Invalidate();
            _cmbTopDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cmbTopDia.SelectedIndexChanged += (s, e) => _previewPanelMain?.Invalidate();

            _numBotQty = new NumericUpDown { Minimum = 2, Maximum = 10, Value = 2, Width = 55 };
            _numBotQty.ValueChanged += (s, e) => _previewPanelMain?.Invalidate();
            _cmbBotDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cmbBotDia.SelectedIndexChanged += (s, e) => _previewPanelMain?.Invalidate();

            _chkAutoSide = new CheckBox { Text = "Tự thêm thép sườn (khi H ≥ 700mm)", Checked = true, AutoSize = true };
            _chkAutoSide.CheckedChanged += (s, e) => _previewPanelMain?.Invalidate();
            _numSideQty = new NumericUpDown { Minimum = 0, Maximum = 8, Value = 2, Increment = 2, Width = 55 };
            _numSideQty.ValueChanged += (s, e) => _previewPanelMain?.Invalidate();
            _cmbSideDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cmbSideDia.SelectedIndexChanged += (s, e) => _previewPanelMain?.Invalidate();

            _lblTopBars = AddRow2Control(layoutMainSec, "Số thanh Lớp Trên:", _numTopQty, _cmbTopDia);
            _lblBotBars = AddRow2Control(layoutMainSec, "Số thanh Lớp Dưới:", _numBotQty, _cmbBotDia);
            _lblSideBars = AddRow2Control(layoutMainSec, "Thép sườn dầm (Skin):", _numSideQty, _cmbSideDia);
            layoutMainSec.Controls.Add(_chkAutoSide);
            _grpMainSec.Controls.Add(layoutMainSec);

            _grpCover = new GroupBox { Text = "🛡️ Lớp Bê Tông Bảo Vệ (Cover)", Dock = DockStyle.Top, Height = 95, Padding = new Padding(8) };
            var layoutCover = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _chkCustomCover = new CheckBox { Text = "Nhập tay Cover (bỏ chọn = tự động từ Revit)", Checked = false, AutoSize = true, Margin = new Padding(3, 4, 3, 4) };
            _numCustomCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 70, Enabled = false };
            _chkCustomCover.CheckedChanged += (s, e) => _numCustomCover.Enabled = _chkCustomCover.Checked;

            _btnProjectCover = new Button { Text = "⚙️ Cover Dự Án", Width = 95, Height = 25, FlatStyle = FlatStyle.System };
            _btnProjectCover.Click += (s, e) => new ProjectCoverSetupForm(_doc).ShowDialog();

            _lblCustomCover = AddRowToLayout(layoutCover, "Cover tùy chỉnh (mm):", _numCustomCover);
            layoutCover.Controls.Add(_chkCustomCover);
            layoutCover.Controls.Add(_btnProjectCover);
            _grpCover.Controls.Add(layoutCover);

            pnlMainLeft.Controls.Add(_grpCover);
            pnlMainLeft.Controls.Add(_grpMainSec);
            _tabMain.Controls.Add(pnlMainLeft);

            // Preview Panel GDI+ Tab 1 (Cross Section)
            _previewPanelMain = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(252, 252, 254) };
            _previewPanelMain.Paint += PreviewPanelMain_Paint;
            _tabMain.Controls.Add(_previewPanelMain);
            _previewPanelMain.BringToFront();

            tabControl.TabPages.Add(_tabMain);

            // --- TAB 2: THÉP TĂNG CƯỜNG ---
            _tabExtra = new TabPage { Text = "➕ Thép Tăng Cường", Padding = new Padding(10), BackColor = Color.White };
            var pnlExtraLeft = new Panel { Dock = DockStyle.Left, Width = 350 };

            _grpExtra = new GroupBox { Text = "Thép Gia Cường Gối & Bụng Dầm", Dock = DockStyle.Top, Height = 170, Padding = new Padding(10) };
            var layoutExtra = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutExtra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layoutExtra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            _numTopLeftExtra = new NumericUpDown { Minimum = 0, Maximum = 6, Value = 1, Width = 55 };
            _numTopLeftExtra.ValueChanged += (s, e) => _previewPanelExtra?.Invalidate();
            _cmbTopLeftDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cmbTopLeftDia.SelectedIndexChanged += (s, e) => _previewPanelExtra?.Invalidate();

            _numTopRightExtra = new NumericUpDown { Minimum = 0, Maximum = 6, Value = 1, Width = 55 };
            _numTopRightExtra.ValueChanged += (s, e) => _previewPanelExtra?.Invalidate();
            _cmbTopRightDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cmbTopRightDia.SelectedIndexChanged += (s, e) => _previewPanelExtra?.Invalidate();

            _numBotMidExtra = new NumericUpDown { Minimum = 0, Maximum = 6, Value = 1, Width = 55 };
            _numBotMidExtra.ValueChanged += (s, e) => _previewPanelExtra?.Invalidate();
            _cmbBotMidDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cmbBotMidDia.SelectedIndexChanged += (s, e) => _previewPanelExtra?.Invalidate();

            _lblTopLeftExtra = AddRow2Control(layoutExtra, "Gối Trái (lớp 2, cắt L/3):", _numTopLeftExtra, _cmbTopLeftDia);
            _lblTopRightExtra = AddRow2Control(layoutExtra, "Gối Phải (lớp 2, cắt L/3):", _numTopRightExtra, _cmbTopRightDia);
            _lblBotMidExtra = AddRow2Control(layoutExtra, "Bụng Dầm (cắt lùi L/6):", _numBotMidExtra, _cmbBotMidDia);
            _grpExtra.Controls.Add(layoutExtra);

            pnlExtraLeft.Controls.Add(_grpExtra);

            // Joint Settings GroupBox
            _grpJointSettings = new GroupBox { Text = "🔗 Liên kết Dầm Cột & Neo Thép (TCVN 5574)", Dock = DockStyle.Top, Height = 110, Padding = new Padding(10) };
            var layoutJoint = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutJoint.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layoutJoint.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _numLdMultiplier = new NumericUpDown { Minimum = 20, Maximum = 60, Value = 35, Width = 80 };
            _numHookTailMultiplier = new NumericUpDown { Minimum = 8, Maximum = 30, Value = 12, Width = 80 };

            _lblLdMultiplier = AddRowToLayout(layoutJoint, "Hệ số neo Ld (x d):", _numLdMultiplier);
            _lblHookTailMultiplier = AddRowToLayout(layoutJoint, "Đoạn bẻ móc uốn (x d):", _numHookTailMultiplier);
            _grpJointSettings.Controls.Add(layoutJoint);

            pnlExtraLeft.Controls.Add(_grpJointSettings);

            // ── Design Standard GroupBox ──────────────────────────────────────────
            _grpDesignStandard = new GroupBox
            {
                Text = "📐 Tiêu Chuẩn Thiết Kế (Anchorage Ld)",
                Dock = DockStyle.Top,
                Height = 148,
                Padding = new Padding(10, 14, 10, 6),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            var layoutDesignBeam = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            layoutDesignBeam.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutDesignBeam.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layoutDesignBeam.Font = new Font("Segoe UI", 8.5F);

            _cmbDesignStandard = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _cmbDesignStandard.Items.AddRange(new object[] { "TCVN 5574:2018", "Eurocode 2" });
            _cmbDesignStandard.SelectedIndex = 0;

            _cmbConcreteGrade = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _cmbConcreteGrade.Items.AddRange(new object[] { "Auto (35d)", "B15", "B20", "B22.5", "B25", "B30", "B35", "B40", "B45", "B50",
                "C20/25", "C25/30", "C28/35", "C30/37", "C32/40", "C35/45", "C40/50" });
            _cmbConcreteGrade.SelectedIndex = 0;

            _cmbSteelGrade = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _cmbSteelGrade.Items.AddRange(new object[] { "Auto (30d)", "CB240-T", "CB300-V", "CB400-V", "CB500-V", "B400", "B500" });
            _cmbSteelGrade.SelectedIndex = 0;

            _lblDesignWarning = new Label
            {
                Text = "",
                ForeColor = Color.OrangeRed,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft
            };

            EventHandler updateBeamDesignWarning = (s, ev) =>
            {
                bool tcvnMode = _cmbDesignStandard.SelectedIndex == 0;
                bool autoGrade = _cmbConcreteGrade.SelectedIndex == 0 || _cmbSteelGrade.SelectedIndex == 0;
                _lblDesignWarning.Text = autoGrade
                    ? "⚠ Chọn mác BT & thép để tính Ld chính xác"
                    : $"✔ Ld tính theo {(tcvnMode ? "TCVN 5574:2018" : "Eurocode 2")}";
                _lblDesignWarning.ForeColor = autoGrade ? Color.OrangeRed : Color.DarkGreen;
            };
            _cmbDesignStandard.SelectedIndexChanged += updateBeamDesignWarning;
            _cmbConcreteGrade.SelectedIndexChanged += updateBeamDesignWarning;
            _cmbSteelGrade.SelectedIndexChanged += updateBeamDesignWarning;

            AddRowToLayout(layoutDesignBeam, "Tiêu chuẩn:", _cmbDesignStandard);
            AddRowToLayout(layoutDesignBeam, "Mác bê tông:", _cmbConcreteGrade);
            AddRowToLayout(layoutDesignBeam, "Mác thép:", _cmbSteelGrade);
            _grpDesignStandard.Controls.Add(layoutDesignBeam);
            _grpDesignStandard.Controls.Add(_lblDesignWarning);
            pnlExtraLeft.Controls.Add(_grpDesignStandard);
            // ─────────────────────────────────────────────────────────────────────

            _tabExtra.Controls.Add(pnlExtraLeft);

            // Preview Panel GDI+ Tab 2 (Beam Elevation with Extra Bars)
            _previewPanelExtra = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(252, 252, 254) };
            _previewPanelExtra.Paint += PreviewPanelExtra_Paint;
            _tabExtra.Controls.Add(_previewPanelExtra);
            _previewPanelExtra.BringToFront();

            tabControl.TabPages.Add(_tabExtra);

            // --- TAB 3: THÉP ĐAI ---
            _tabStirrup = new TabPage { Text = "🌀 Thép Đai (Stirrups)", Padding = new Padding(10), BackColor = Color.White };
            var pnlStirrupLeft = new Panel { Dock = DockStyle.Left, Width = 350 };

            _grpStirrup = new GroupBox { Text = "Phân Vùng Đai A1 / A2 / A1 Dầm", Dock = DockStyle.Top, Height = 175, Padding = new Padding(10) };
            var layoutStirrup = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutStirrup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layoutStirrup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _cmbStirrupDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            _cmbStirrupDia.SelectedIndexChanged += (s, e) => _previewPanelStirrup?.Invalidate();

            _numStirrupSpacingA1 = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, Increment = 10, Width = 80 };
            _numStirrupSpacingA1.ValueChanged += (s, e) => _previewPanelStirrup?.Invalidate();

            _numStirrupSpacingA2 = new NumericUpDown { Minimum = 100, Maximum = 500, Value = 200, Increment = 10, Width = 80 };
            _numStirrupSpacingA2.ValueChanged += (s, e) => _previewPanelStirrup?.Invalidate();

            _numZoneA1Length = new NumericUpDown { Minimum = 0, Maximum = 3000, Value = 0, Increment = 100, Width = 80 };
            _numZoneA1Length.ValueChanged += (s, e) => _previewPanelStirrup?.Invalidate();

            _lblStirrupDia = AddRowToLayout(layoutStirrup, "Đường kính thép đai:", _cmbStirrupDia);
            _lblStirrupA1 = AddRowToLayout(layoutStirrup, "Khoảng cách đai gối A1 (mm):", _numStirrupSpacingA1);
            _lblStirrupA2 = AddRowToLayout(layoutStirrup, "Khoảng cách đai bụng A2 (mm):", _numStirrupSpacingA2);
            _lblZoneA1Len = AddRowToLayout(layoutStirrup, "Chiều dài vùng gối A1 (mm, 0 = L/4):", _numZoneA1Length);
            _grpStirrup.Controls.Add(layoutStirrup);

            pnlStirrupLeft.Controls.Add(_grpStirrup);
            _tabStirrup.Controls.Add(pnlStirrupLeft);

            // Preview Panel GDI+ Tab 3 (Stirrup Distribution Elevation)
            _previewPanelStirrup = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(252, 252, 254) };
            _previewPanelStirrup.Paint += PreviewPanelStirrup_Paint;
            _tabStirrup.Controls.Add(_previewPanelStirrup);
            _previewPanelStirrup.BringToFront();

            tabControl.TabPages.Add(_tabStirrup);

            // --- TAB 4: BẢN VẼ & VIEW 3D ---
            _tabViews = new TabPage { Text = "🖼️ Bản Vẽ & View 3D", Padding = new Padding(12), BackColor = Color.White };
            _grpViews = new GroupBox { Text = "Tự động Tạo View & Triển khai Bản vẽ Dầm", Dock = DockStyle.Top, Height = 130, Padding = new Padding(10) };
            var pnlViews = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkAutoDrawing = new CheckBox { Text = "Tự động tạo bản vẽ 2D (Mặt cắt tiết diện & Thống kê thép)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
            _chkAutoSection3D = new CheckBox { Text = "Tự động tạo View xem thép 3D (Plan View + 3D View)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
            pnlViews.Controls.Add(_chkAutoDrawing);
            pnlViews.Controls.Add(_chkAutoSection3D);
            _grpViews.Controls.Add(pnlViews);
            _tabViews.Controls.Add(_grpViews);
            tabControl.TabPages.Add(_tabViews);

            Controls.Add(tabControl);
            tabControl.BringToFront();
        }

        private static Label AddRowToLayout(TableLayoutPanel table, string labelText, Control inputControl)
        {
            var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 5, 3, 3) };
            table.Controls.Add(lbl);
            table.Controls.Add(inputControl);
            return lbl;
        }

        private static Label AddRow2Control(TableLayoutPanel table, string labelText, Control input1, Control input2)
        {
            var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 5, 3, 3) };
            var pnl = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
            pnl.Controls.Add(input1);
            pnl.Controls.Add(input2);
            table.Controls.Add(lbl);
            table.Controls.Add(pnl);
            return lbl;
        }

        private void SetAllBeamsSelected(bool selectAll)
        {
            for (int i = 0; i < _beamListBox.Items.Count; i++)
            {
                _beamListBox.SetSelected(i, selectAll);
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int count = _beamListBox.SelectedItems.Count;
            if (_preSelectedBeams.Any())
            {
                _lblSelectedCount.Text = $"🟢 Đã chọn sẵn: {count} dầm từ Revit";
                _lblSelectedCount.ForeColor = Color.DarkGreen;
            }
            else
            {
                _lblSelectedCount.Text = $"🔵 Đã chọn: {count} / {_beamListBox.Items.Count} dầm";
                _lblSelectedCount.ForeColor = Color.DarkBlue;
            }
        }

        private void PopulateBeamList()
        {
            _beamListBox.Items.Clear();
            var preSelectedIds = new HashSet<ElementId>(_preSelectedBeams.Select(b => b.Id));

            for (int i = 0; i < _availableBeams.Count; i++)
            {
                var bm = _availableBeams[i];
                ElementId lvlId = (bm.LevelId != ElementId.InvalidElementId)
                    ? bm.LevelId
                    : (bm.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);

                string levelName = (lvlId != ElementId.InvalidElementId ? _doc.GetElement(lvlId)?.Name : null)
                    ?? bm.LookupParameter("Reference Level")?.AsString()
                    ?? "Level N/A";

                string mark = bm.LookupParameter("Mark")?.AsString() ?? bm.Id.ToLongValue().ToString();
                var item = new BeamListItem(bm, $"{levelName} - {mark}");
                _beamListBox.Items.Add(item);

                if (preSelectedIds.Contains(bm.Id))
                {
                    _beamListBox.SetSelected(i, true);
                }
            }

            UpdateSelectedCount();
        }

        private void PopulateBarTypeCombos()
        {
            var names = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();

            _cmbTopDia.Items.AddRange(names);
            _cmbBotDia.Items.AddRange(names);
            _cmbSideDia.Items.AddRange(names);
            _cmbStirrupDia.Items.AddRange(names);
            _cmbTopLeftDia.Items.AddRange(names);
            _cmbTopRightDia.Items.AddRange(names);
            _cmbBotMidDia.Items.AddRange(names);

            if (names.Any())
            {
                _cmbTopDia.SelectedIndex = 0;
                _cmbBotDia.SelectedIndex = 0;
                _cmbSideDia.SelectedIndex = 0;
                _cmbStirrupDia.SelectedIndex = 0;
                _cmbTopLeftDia.SelectedIndex = 0;
                _cmbTopRightDia.SelectedIndex = 0;
                _cmbBotMidDia.SelectedIndex = 0;
            }
        }

        // ─── GDI+ PAINT HANDLERS ───────────────────────────────────────────

        private void PreviewPanelMain_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int panelW = _previewPanelMain.Width;
            int panelH = _previewPanelMain.Height;
            int w = 120, h = 180;
            int x0 = (panelW - w) / 2;
            int y0 = (panelH - h) / 2 - 15;

            using var penRect = new Pen(Color.FromArgb(80, 80, 80), 2);
            using var brushCover = new SolidBrush(Color.FromArgb(248, 248, 250));
            g.FillRectangle(brushCover, x0, y0, w, h);
            g.DrawRectangle(penRect, x0, y0, w, h);

            int margin = 10;
            using var penStirrup = new Pen(Color.FromArgb(0, 122, 255), 1.5f);
            g.DrawRectangle(penStirrup, x0 + margin, y0 + margin, w - 2 * margin, h - 2 * margin);

            int topQty = (int)_numTopQty.Value;
            int botQty = (int)_numBotQty.Value;
            int sideQty = (int)_numSideQty.Value;

            using var brushRed = new SolidBrush(Color.Firebrick);
            using var brushBlue = new SolidBrush(Color.FromArgb(0, 102, 204));

            // Top bars
            for (int i = 0; i < topQty; i++)
            {
                float bx = x0 + margin + 6 + i * (w - 2 * margin - 12f) / Math.Max(topQty - 1, 1);
                float by = y0 + margin + 6;
                g.FillEllipse(brushRed, bx - 4, by - 4, 8, 8);
            }

            // Bottom bars
            for (int i = 0; i < botQty; i++)
            {
                float bx = x0 + margin + 6 + i * (w - 2 * margin - 12f) / Math.Max(botQty - 1, 1);
                float by = y0 + h - margin - 6;
                g.FillEllipse(brushRed, bx - 4, by - 4, 8, 8);
            }

            // Side bars
            if (sideQty > 0)
            {
                int pairs = Math.Max(sideQty / 2, 1);
                for (int i = 1; i <= pairs; i++)
                {
                    float by = y0 + margin + 6 + i * (h - 2 * margin - 12f) / (pairs + 1);
                    float bxLeft = x0 + margin + 6;
                    float bxRight = x0 + w - margin - 6;

                    g.FillEllipse(brushBlue, bxLeft - 4, by - 4, 8, 8);
                    g.FillEllipse(brushBlue, bxRight - 4, by - 4, 8, 8);
                }
            }

            var font = new Font("Segoe UI", 7.5F);
            var fontBold = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            g.DrawString($"BxH = 300x500", font, Brushes.Black, 6, y0 + h + 10);
            g.DrawString($"Top: {topQty}Φ{_cmbTopDia.Text}", fontBold, Brushes.Firebrick, 6, y0 + h + 25);
            g.DrawString($"Bot: {botQty}Φ{_cmbBotDia.Text}", fontBold, Brushes.Firebrick, 6, y0 + h + 40);
            if (sideQty > 0)
            {
                g.DrawString($"Side: {sideQty}Φ{_cmbSideDia.Text}", fontBold, Brushes.Navy, 6, y0 + h + 55);
            }
        }

        private void PreviewPanelExtra_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int panelW = _previewPanelExtra.Width;
            int panelH = _previewPanelExtra.Height;

            int beamW = panelW - 60;
            int beamH = 50;
            int x0 = 30;
            int y0 = panelH / 2 - beamH / 2;

            // Support Columns (Gray blocks)
            using var brushCol = new SolidBrush(Color.FromArgb(210, 215, 220));
            g.FillRectangle(brushCol, x0 - 15, y0 - 30, 20, beamH + 60);
            g.FillRectangle(brushCol, x0 + beamW - 5, y0 - 30, 20, beamH + 60);
            g.DrawRectangle(Pens.Gray, x0 - 15, y0 - 30, 20, beamH + 60);
            g.DrawRectangle(Pens.Gray, x0 + beamW - 5, y0 - 30, 20, beamH + 60);

            // Beam outline
            using var penBeam = new Pen(Color.DimGray, 2);
            g.DrawRectangle(penBeam, x0, y0, beamW, beamH);

            // Continuous Main Top & Bottom Bars
            using var penMain = new Pen(Color.Firebrick, 2);
            g.DrawLine(penMain, x0 - 10, y0 + 6, x0 + beamW + 10, y0 + 6);
            g.DrawLine(penMain, x0 - 10, y0 + beamH - 6, x0 + beamW + 10, y0 + beamH - 6);

            int topLeftExtra = (int)_numTopLeftExtra.Value;
            int topRightExtra = (int)_numTopRightExtra.Value;
            int botMidExtra = (int)_numBotMidExtra.Value;

            using var penExtraTop = new Pen(Color.Crimson, 2.5f);
            using var penExtraBot = new Pen(Color.MediumBlue, 2.5f);
            var fontSmall = new Font("Segoe UI", 7.5F, FontStyle.Bold);

            // Top Left Extra (L/3)
            if (topLeftExtra > 0)
            {
                int lenL3 = beamW / 3;
                g.DrawLine(penExtraTop, x0 - 10, y0 + 14, x0 + lenL3, y0 + 14);
                g.DrawString($"Top Left: {topLeftExtra}Φ{_cmbTopLeftDia.Text} (L/3)", fontSmall, Brushes.Crimson, x0 + 5, y0 - 18);
            }

            // Top Right Extra (L/3)
            if (topRightExtra > 0)
            {
                int lenL3 = beamW / 3;
                g.DrawLine(penExtraTop, x0 + beamW - lenL3, y0 + 14, x0 + beamW + 10, y0 + 14);
                g.DrawString($"Top Right: {topRightExtra}Φ{_cmbTopRightDia.Text} (L/3)", fontSmall, Brushes.Crimson, x0 + beamW - lenL3, y0 - 18);
            }

            // Bottom Mid Extra (L/6 cut-off)
            if (botMidExtra > 0)
            {
                int cutL6 = beamW / 6;
                g.DrawLine(penExtraBot, x0 + cutL6, y0 + beamH - 14, x0 + beamW - cutL6, y0 + beamH - 14);
                g.DrawString($"Bot Mid: {botMidExtra}Φ{_cmbBotMidDia.Text} (L/6 cut)", fontSmall, Brushes.MediumBlue, x0 + beamW / 2 - 45, y0 + beamH + 5);
            }
        }

        private void PreviewPanelStirrup_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int panelW = _previewPanelStirrup.Width;
            int panelH = _previewPanelStirrup.Height;

            int beamW = panelW - 60;
            int beamH = 60;
            int x0 = 30;
            int y0 = panelH / 2 - beamH / 2;

            // Support Columns
            using var brushCol = new SolidBrush(Color.FromArgb(210, 215, 220));
            g.FillRectangle(brushCol, x0 - 15, y0 - 15, 20, beamH + 30);
            g.FillRectangle(brushCol, x0 + beamW - 5, y0 - 15, 20, beamH + 30);

            // Beam outline
            g.DrawRectangle(Pens.DimGray, x0, y0, beamW, beamH);

            int a1Len = beamW / 4;
            double s1 = (double)_numStirrupSpacingA1.Value;
            double s2 = (double)_numStirrupSpacingA2.Value;

            using var penStirrup = new Pen(Color.FromArgb(0, 122, 255), 1.5f);

            // Left A1 Dense Zone
            for (int x = x0 + 5; x <= x0 + a1Len; x += 10)
            {
                g.DrawLine(penStirrup, x, y0 + 3, x, y0 + beamH - 3);
            }

            // Mid A2 Sparse Zone
            for (int x = x0 + a1Len + 20; x <= x0 + beamW - a1Len - 10; x += 20)
            {
                g.DrawLine(penStirrup, x, y0 + 3, x, y0 + beamH - 3);
            }

            // Right A1 Dense Zone
            for (int x = x0 + beamW - a1Len; x <= x0 + beamW - 5; x += 10)
            {
                g.DrawLine(penStirrup, x, y0 + 3, x, y0 + beamH - 3);
            }

            var fontSmall = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            g.DrawString($"A1: a={s1}mm", fontSmall, Brushes.DarkBlue, x0 + 5, y0 + beamH + 5);
            g.DrawString($"A2: a={s2}mm", fontSmall, Brushes.DarkBlue, x0 + beamW / 2 - 25, y0 + beamH + 5);
            g.DrawString($"A1: a={s1}mm", fontSmall, Brushes.DarkBlue, x0 + beamW - a1Len, y0 + beamH + 5);
        }

        private void ApplyLanguage()
        {
            bool isEn = LanguageManager.IsEnglish;

            Text = isEn ? "📏 KHIM TOOLS — Beam Reinforcement Layout (v2.0)" : "📏 KHIM TOOLS — Bố trí Thép Dầm (v2.0)";

            if (_tabMain != null) _tabMain.Text = isEn ? "📌 Main Rebar & Cover" : "📌 Thép Chủ & Cover";
            if (_tabExtra != null) _tabExtra.Text = isEn ? "➕ Additional Rebar" : "➕ Thép Tăng Cường";
            if (_tabStirrup != null) _tabStirrup.Text = isEn ? "🌀 Stirrups" : "🌀 Thép Đai (Stirrups)";
            if (_tabViews != null) _tabViews.Text = isEn ? "🖼️ Drawing & Views" : "🖼️ Bản Vẽ & Views";

            // Tab 1
            if (_grpMainSec != null) _grpMainSec.Text = isEn ? "Continuous Main & Skin Rebar" : "Thép Chủ Chạy Suốt & Thép Sườn Dầm";
            if (_lblTopBars != null) _lblTopBars.Text = isEn ? "Top continuous bars:" : "Số thanh Lớp Trên:";
            if (_lblBotBars != null) _lblBotBars.Text = isEn ? "Bottom continuous bars:" : "Số thanh Lớp Dưới:";
            if (_lblSideBars != null) _lblSideBars.Text = isEn ? "Skin / Side bars (total):" : "Thép sườn dầm (Skin):";
            if (_chkAutoSide != null) _chkAutoSide.Text = isEn ? "Auto add side bars when H ≥ 700mm" : "Tự thêm thép sườn (khi H ≥ 700mm)";

            if (_grpCover != null) _grpCover.Text = isEn ? "Concrete Cover" : "Lớp Bê Tông Bảo Vệ (Cover)";
            if (_chkCustomCover != null) _chkCustomCover.Text = isEn ? "Custom Cover (mm)" : "Nhập tay Cover (mm)";
            if (_lblCustomCover != null) _lblCustomCover.Text = isEn ? "Custom Cover (mm):" : "Cover tùy chỉnh (mm):";
            if (_btnProjectCover != null) _btnProjectCover.Text = isEn ? "⚙️ Project Cover" : "⚙️ Cover Dự Án";

            // Tab 2
            if (_grpExtra != null) _grpExtra.Text = isEn ? "Top Support & Bottom Mid Extra Bars" : "Thép Gia Cường Gối & Bụng Dầm";
            if (_lblTopLeftExtra != null) _lblTopLeftExtra.Text = isEn ? "Top Left Extra (Layer 2, L/3):" : "Gối Trái (lớp 2, cắt L/3):";
            if (_lblTopRightExtra != null) _lblTopRightExtra.Text = isEn ? "Top Right Extra (Layer 2, L/3):" : "Gối Phải (lớp 2, cắt L/3):";
            if (_lblBotMidExtra != null) _lblBotMidExtra.Text = isEn ? "Bottom Mid Extra (Cutoff L/6):" : "Bụng Dầm (cắt lùi L/6):";

            if (_grpJointSettings != null) _grpJointSettings.Text = isEn ? "🔗 Joint Connections & Anchorage (TCVN 5574)" : "🔗 Liên kết Dầm Cột & Neo Thép (TCVN 5574)";
            if (_lblLdMultiplier != null) _lblLdMultiplier.Text = isEn ? "Ld multiplier (x dia):" : "Hệ số neo Ld (x d):";
            if (_lblHookTailMultiplier != null) _lblHookTailMultiplier.Text = isEn ? "Hook tail (x dia):" : "Đoạn bẻ móc uốn (x d):";

            // Tab 3
            if (_grpStirrup != null) _grpStirrup.Text = isEn ? "Beam Stirrup Zones A1 / A2 / A1" : "Phân Vùng Đai A1 / A2 / A1 Dầm";
            if (_lblStirrupDia != null) _lblStirrupDia.Text = isEn ? "Stirrup bar diameter:" : "Đường kính thép đai:";
            if (_lblStirrupA1 != null) _lblStirrupA1.Text = isEn ? "Support A1 stirrup spacing (mm):" : "Khoảng cách đai gối A1 (mm):";
            if (_lblStirrupA2 != null) _lblStirrupA2.Text = isEn ? "Mid-span A2 stirrup spacing (mm):" : "Khoảng cách đai bụng A2 (mm):";
            if (_lblZoneA1Len != null) _lblZoneA1Len.Text = isEn ? "Support A1 zone length (mm, 0=L/4):" : "Chiều dài vùng gối A1 (mm, 0 = L/4):";

            // Tab 4
            if (_grpViews != null) _grpViews.Text = isEn ? "Drawing & View Options" : "Tự Động Tạo View & Triển Khai Bản Vẽ Dầm";
            if (_chkAutoDrawing != null) _chkAutoDrawing.Text = isEn ? "Automatically generate section drawing sheets" : "Tự động tạo bản vẽ 2D (Mặt cắt tiết diện & Thống kê thép)";
            if (_chkAutoSection3D != null) _chkAutoSection3D.Text = isEn ? "Create 3D Rebar View for beams" : "Tự động tạo View xem thép 3D (Plan View + 3D View)";

            // Right & Bottom Panels
            if (_lblBeamTitle != null) _lblBeamTitle.Text = isEn ? "📋 Beam List" : "📋 Danh Sách Dầm";
            if (_lblTemplate != null) _lblTemplate.Text = isEn ? "📋 Configuration Template:" : "📋 Mẫu Thiết Lập:";
            if (_btnSaveTemplate != null) _btnSaveTemplate.Text = isEn ? "Save As..." : "Lưu mẫu...";
            if (_btnApplyTemplate != null) _btnApplyTemplate.Text = isEn ? "Apply" : "Áp dụng";
            if (_btnDeleteTemplate != null) _btnDeleteTemplate.Text = isEn ? "Delete" : "Xóa mẫu";
            if (_btnSelectAll != null) _btnSelectAll.Text = isEn ? "Select All" : "Chọn Tất Cả";
            if (_btnDeselectAll != null) _btnDeselectAll.Text = isEn ? "Clear" : "Bỏ Chọn";

            if (_btnCreateRebar != null) _btnCreateRebar.Text = isEn ? "⚡ Create Rebar" : "⚡ Tạo Thép";
            if (_btnClose != null) _btnClose.Text = isEn ? "Close" : "Đóng";

            UpdateSelectedCount();
            _previewPanelMain?.Invalidate();
            _previewPanelExtra?.Invalidate();
            _previewPanelStirrup?.Invalidate();
        }

        private void BtnCreateRebar_Click(object sender, EventArgs e)
        {
            var selectedItems = _beamListBox.SelectedItems.Cast<BeamListItem>().ToList();
            if (!selectedItems.Any())
            {
                string warnTitle = LanguageManager.IsEnglish ? "Selection Missing" : "Thiếu Thông Tin";
                string warnMsg = LanguageManager.IsEnglish ? "Please select at least 1 beam from the right list." : "Vui lòng chọn ít nhất 1 dầm trong danh sách bên phải.";
                KhimDialogHelper.ShowWarning(warnTitle, warnMsg);
                return;
            }

            RebarBarType topType = FindBarType(_cmbTopDia.Text);
            RebarBarType botType = FindBarType(_cmbBotDia.Text);
            RebarBarType stirrupType = FindBarType(_cmbStirrupDia.Text);
            RebarBarType sideType = FindBarType(_cmbSideDia.Text) ?? stirrupType;

            RebarBarType topLeftExtraType = FindBarType(_cmbTopLeftDia.Text) ?? topType;
            RebarBarType topRightExtraType = FindBarType(_cmbTopRightDia.Text) ?? topType;
            RebarBarType botMidExtraType = FindBarType(_cmbBotMidDia.Text) ?? botType;

            if (topType == null || botType == null || stirrupType == null)
            {
                string warnTitle = LanguageManager.IsEnglish ? "Selection Missing" : "Thiếu Thông Tin";
                string warnMsg = LanguageManager.IsEnglish ? "Please select bar diameters for top, bottom, and stirrups." : "Chưa chọn đủ đường kính thép.";
                KhimDialogHelper.ShowWarning(warnTitle, warnMsg);
                return;
            }

            double? customCoverFeet = _chkCustomCover.Checked
                ? UnitUtils.ConvertToInternalUnits((double)_numCustomCover.Value, UnitTypeId.Millimeters)
                : null;

            using var tx = new Transaction(_doc, "Create Beam Rebar");
            tx.Start();
            FailureHandlingOptions failOptions = tx.GetFailureHandlingOptions();
            failOptions.SetFailuresPreprocessor(new KhimTools.SlabJoin.Utilities.SwallowWarningsPreprocessor());
            tx.SetFailureHandlingOptions(failOptions);
            try
            {
                var generator = new BeamRebarGenerator(_doc);
                var report = new RebarGenerationReport();

                foreach (var item in selectedItems)
                {
                    var input = new BeamRebarInput
                    {
                        Beam = item.Beam,
                        MainTopBarType = topType,
                        MainBottomBarType = botType,
                        StirrupBarType = stirrupType,
                        SideBarType = sideType,
                        TopLeftExtraBarType = topLeftExtraType,
                        TopRightExtraBarType = topRightExtraType,
                        BottomMidExtraBarType = botMidExtraType,
                        TopContinuousQty = (int)_numTopQty.Value,
                        BottomContinuousQty = (int)_numBotQty.Value,
                        TopLeftExtraQty = (int)_numTopLeftExtra.Value,
                        TopRightExtraQty = (int)_numTopRightExtra.Value,
                        BottomMidExtraQty = (int)_numBotMidExtra.Value,
                        AutoSideBars = _chkAutoSide.Checked,
                        SideBarQty = (int)_numSideQty.Value,
                        StirrupSpacingA1 = UnitUtils.ConvertToInternalUnits((double)_numStirrupSpacingA1.Value, UnitTypeId.Millimeters),
                        StirrupSpacingA2 = UnitUtils.ConvertToInternalUnits((double)_numStirrupSpacingA2.Value, UnitTypeId.Millimeters),
                        ZoneA1Length = UnitUtils.ConvertToInternalUnits((double)_numZoneA1Length.Value, UnitTypeId.Millimeters),
                        CustomCoverFeet = customCoverFeet,
                        LdMultiplier = (double)_numLdMultiplier.Value,
                        HookTailMultiplier = (double)_numHookTailMultiplier.Value,
                        DesignStandard = GetSelectedDesignStandard(),
                        ConcreteGrade = GetSelectedConcreteGrade(),
                        SteelGrade = GetSelectedSteelGrade()
                    };

                    generator.Generate(input, report);
                }

                tx.Commit();
                KhimDialogHelper.ShowRebarGenerationReport(report, "Dầm (Beam)", selectedItems.Count);
            }
            catch (Exception ex)
            {
                tx.RollBack();
                string errTitle = LanguageManager.IsEnglish ? "Error Creating Beam Rebar" : "Lỗi Tạo Thép Dầm";
                KhimDialogHelper.ShowError(errTitle, ex.Message, ex.StackTrace);
            }
        }

        private RebarBarType FindBarType(string label) =>
            new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(t => t.Name.Equals(label, StringComparison.OrdinalIgnoreCase));

        private void LoadTemplateList()
        {
            _cmbTemplate.Items.Clear();
            var names = RebarTemplateManager.ListBeamTemplates();
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

             var settings = new BeamRebarSettings
            {
                Name = name.Trim(),
                DesignStandard = GetSelectedDesignStandard() == DesignCode.Eurocode2 ? "Eurocode2" : "TCVN5574_2018",
                ConcreteGrade = _cmbConcreteGrade.Text,
                SteelGrade = _cmbSteelGrade.Text,
                MainTopBarType = _cmbTopDia.Text,
                MainBottomBarType = _cmbBotDia.Text,
                StirrupBarType = _cmbStirrupDia.Text,
                SideBarType = _cmbSideDia.Text,
                TopContinuousQty = (int)_numTopQty.Value,
                BottomContinuousQty = (int)_numBotQty.Value,
                TopLeftExtraQty = (int)_numTopLeftExtra.Value,
                TopLeftExtraBarType = _cmbTopLeftDia.Text,
                TopRightExtraQty = (int)_numTopRightExtra.Value,
                TopRightExtraBarType = _cmbTopRightDia.Text,
                BottomMidExtraQty = (int)_numBotMidExtra.Value,
                BottomMidExtraBarType = _cmbBotMidDia.Text,
                AutoSideBars = _chkAutoSide.Checked,
                SideBarQty = (int)_numSideQty.Value,
                StirrupSpacingA1 = (double)_numStirrupSpacingA1.Value,
                StirrupSpacingA2 = (double)_numStirrupSpacingA2.Value,
                ZoneA1Length = (double)_numZoneA1Length.Value,
                IsCustomCover = _chkCustomCover.Checked,
                CustomCover = (double)_numCustomCover.Value,
                LdMultiplier = (double)_numLdMultiplier.Value,
                HookTailMultiplier = (double)_numHookTailMultiplier.Value
            };

            RebarTemplateManager.SaveBeamTemplate(settings);
            LoadTemplateList();
            _cmbTemplate.Text = settings.Name;
        }

        private void ApplyTemplate()
        {
            string name = _cmbTemplate.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            var settings = RebarTemplateManager.LoadBeamTemplate(name);
            if (settings == null) return;

            SetComboValue(_cmbDesignStandard, settings.DesignStandard?.Contains("Eurocode") == true ? "Eurocode 2" : "TCVN 5574:2018");
            SetComboValue(_cmbConcreteGrade, settings.ConcreteGrade ?? "Auto (35d)");
            SetComboValue(_cmbSteelGrade, settings.SteelGrade ?? "Auto (30d)");
            SetComboValue(_cmbTopDia, settings.MainTopBarType);
            SetComboValue(_cmbBotDia, settings.MainBottomBarType);
            SetComboValue(_cmbStirrupDia, settings.StirrupBarType);
            SetComboValue(_cmbSideDia, settings.SideBarType);

            SetComboValue(_cmbTopLeftDia, settings.TopLeftExtraBarType);
            SetComboValue(_cmbTopRightDia, settings.TopRightExtraBarType);
            SetComboValue(_cmbBotMidDia, settings.BottomMidExtraBarType);

            _numTopQty.Value = Math.Max(_numTopQty.Minimum, Math.Min(_numTopQty.Maximum, settings.TopContinuousQty));
            _numBotQty.Value = Math.Max(_numBotQty.Minimum, Math.Min(_numBotQty.Maximum, settings.BottomContinuousQty));

            _numTopLeftExtra.Value = Math.Max(_numTopLeftExtra.Minimum, Math.Min(_numTopLeftExtra.Maximum, settings.TopLeftExtraQty));
            _numTopRightExtra.Value = Math.Max(_numTopRightExtra.Minimum, Math.Min(_numTopRightExtra.Maximum, settings.TopRightExtraQty));
            _numBotMidExtra.Value = Math.Max(_numBotMidExtra.Minimum, Math.Min(_numBotMidExtra.Maximum, settings.BottomMidExtraQty));

            _chkAutoSide.Checked = settings.AutoSideBars;
            _numSideQty.Value = Math.Max(_numSideQty.Minimum, Math.Min(_numSideQty.Maximum, settings.SideBarQty));

            _numStirrupSpacingA1.Value = (decimal)settings.StirrupSpacingA1;
            _numStirrupSpacingA2.Value = (decimal)settings.StirrupSpacingA2;
            _numZoneA1Length.Value = (decimal)settings.ZoneA1Length;

            _chkCustomCover.Checked = settings.IsCustomCover;
            _numCustomCover.Value = (decimal)settings.CustomCover;

            _numLdMultiplier.Value = (decimal)settings.LdMultiplier;
            _numHookTailMultiplier.Value = (decimal)settings.HookTailMultiplier;

            _previewPanelMain?.Invalidate();
            _previewPanelExtra?.Invalidate();
            _previewPanelStirrup?.Invalidate();
        }

        private void DeleteTemplate()
        {
            string name = _cmbTemplate.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            RebarTemplateManager.DeleteBeamTemplate(name);
            LoadTemplateList();
        }

        private DesignCode GetSelectedDesignStandard()
        {
            if (_cmbDesignStandard.SelectedIndex == 1) return DesignCode.Eurocode2;
            return DesignCode.TCVN5574_2018;
        }

        private ConcreteGrade GetSelectedConcreteGrade()
        {
            string txt = _cmbConcreteGrade.Text;
            if (txt.StartsWith("Auto")) return ConcreteGrade.Auto;
            string clean = txt.Replace("/", "_").Replace(" ", "");
            if (Enum.TryParse(clean, out ConcreteGrade res)) return res;
            return ConcreteGrade.Auto;
        }

        private SteelGrade GetSelectedSteelGrade()
        {
            string txt = _cmbSteelGrade.Text;
            if (txt.StartsWith("Auto")) return SteelGrade.Auto;
            string clean = txt.Replace("-", "_").Replace(" ", "");
            if (Enum.TryParse(clean, out SteelGrade res)) return res;
            return SteelGrade.Auto;
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

        private class BeamListItem
        {
            public FamilyInstance Beam { get; }
            private readonly string _label;
            public BeamListItem(FamilyInstance bm, string label) { Beam = bm; _label = label; }
            public override string ToString() => _label;
        }
    }
}

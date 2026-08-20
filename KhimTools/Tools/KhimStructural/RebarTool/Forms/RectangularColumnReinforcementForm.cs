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
    public class RectangularColumnReinforcementForm : Form
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableColumns;
        private readonly List<FamilyInstance> _preSelectedColumns;

        // UI Controls
        private ListBox _columnListBox;
        private Label _lblSelectedCount;
        private Panel _previewPanel;

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
        private Button _btnClose;

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

        public RectangularColumnReinforcementForm(Document doc, List<FamilyInstance> availableColumns, List<FamilyInstance> preSelectedColumns = null)
        {
            _doc = doc;
            _availableColumns = availableColumns ?? new List<FamilyInstance>();
            _preSelectedColumns = preSelectedColumns ?? new List<FamilyInstance>();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            PopulateColumnList();
            PopulateBarTypeCombos();
            LoadTemplateList();
            ApplyLanguage();
        }

        private void BuildUi()
        {
            Text = "🏗️ KHIM TOOLS — Bố trí Thép Cột Vuông / Chữ Nhật";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

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

            // 2. Right Column Selection Panel
            var rightPanel = new Panel { Dock = DockStyle.Right, Width = 230, Padding = new Padding(10), BackColor = Color.FromArgb(250, 250, 252) };
            _lblColTitle = new Label { Text = "📋 Danh Sách Cột", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            var scopePanel = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(240, 243, 248), Padding = new Padding(4) };
            _rdScopeSelected = new RadioButton { Text = $"Chỉ các cột đã chọn ({_preSelectedColumns.Count})", Checked = _preSelectedColumns.Any(), AutoSize = true, Top = 4, Left = 4, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.DarkGreen };
            _rdScopeAll = new RadioButton { Text = $"Tất cả cột ({_availableColumns.Count})", Checked = !_preSelectedColumns.Any(), AutoSize = true, Top = 26, Left = 4, Font = new Font("Segoe UI", 8.5F) };

            _rdScopeSelected.CheckedChanged += (s, e) => PopulateColumnList();
            _rdScopeAll.CheckedChanged += (s, e) => PopulateColumnList();

            scopePanel.Controls.Add(_rdScopeSelected);
            scopePanel.Controls.Add(_rdScopeAll);

            _lblSelectedCount = new Label { Text = "Đã chọn: 0 cột", Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };

            var selectButtonsPanel = new Panel { Dock = DockStyle.Bottom, Height = 32 };
            _btnSelectAll = new Button { Text = "Select All", Width = 95, Height = 26, Top = 3, Left = 0, FlatStyle = FlatStyle.System };
            _btnDeselectAll = new Button { Text = "Clear", Width = 70, Height = 26, Top = 3, Left = 102, FlatStyle = FlatStyle.System };

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
                _previewPanel?.Invalidate();
            };

            rightPanel.Controls.Add(_columnListBox);
            rightPanel.Controls.Add(scopePanel);
            rightPanel.Controls.Add(selectButtonsPanel);
            rightPanel.Controls.Add(_lblSelectedCount);
            rightPanel.Controls.Add(_lblColTitle);
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

            // --- TAB 1: THÉP CHỦ & REVIEW ---
            _tabMain = new TabPage { Text = "📌 Thép Chủ & Review", Padding = new Padding(8), BackColor = Color.White };
            var pnlMainLeft = new Panel { Dock = DockStyle.Left, Width = 350 };

            _grpMainSection = new GroupBox { Text = "Bố trí Thép Chủ Tiết Diện", Dock = DockStyle.Top, Height = 135, Padding = new Padding(8) };
            var layoutMainSec = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _numBarsB = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 3, Width = 80 };
            _numBarsH = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 3, Width = 80 };
            _numBarsB.ValueChanged += (s, e) => _previewPanel?.Invalidate();
            _numBarsH.ValueChanged += (s, e) => _previewPanel?.Invalidate();

            _lblBarsB = AddRowToLayout(layoutMainSec, "Thép chủ cạnh B (kể cả góc):", _numBarsB);
            _lblBarsH = AddRowToLayout(layoutMainSec, "Thép chủ cạnh H (kể cả góc):", _numBarsH);
            _lblMainDia = AddRowToLayout(layoutMainSec, "Đường kính thép chủ:", _cmbMainDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 });
            _cmbMainDia.SelectedIndexChanged += (s, e) => _previewPanel?.Invalidate();
            _grpMainSection.Controls.Add(layoutMainSec);

            _grpCover = new GroupBox { Text = "🛡️ Cover Bê Tông", Dock = DockStyle.Top, Height = 95, Padding = new Padding(8) };
            var layoutCover = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _chkCustomCover = new CheckBox { Text = "Nhập tay Cover (mm)", Checked = false, AutoSize = true, Margin = new Padding(3, 4, 3, 4) };
            _numCustomCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 70, Enabled = false };
            _chkCustomCover.CheckedChanged += (s, e) => _numCustomCover.Enabled = _chkCustomCover.Checked;

            _btnProjectCover = new Button { Text = "⚙️ Cover Dự Án", Width = 105, Height = 25, FlatStyle = FlatStyle.System };
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
            _tabMain.Controls.Add(_previewPanel);
            _previewPanel.BringToFront();

            tabControl.TabPages.Add(_tabMain);

            // --- TAB 2: THÉP ĐAI ---
            _tabStirrup = new TabPage { Text = "🌀 Thép Đai (Stirrups)", Padding = new Padding(12), BackColor = Color.White };

            _grpStirrupZone = new GroupBox { Text = "Phân Vùng Đai A1 / A2 / A1 (Chuẩn Kết Cấu)", Dock = DockStyle.Top, Height = 175, Padding = new Padding(10) };
            var layoutStirrupZone = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutStirrupZone.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layoutStirrupZone.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _lblStirrupDia = AddRowToLayout(layoutStirrupZone, "Đường kính thép đai:", _cmbStirrupDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 });
            _cmbStirrupDia.SelectedIndexChanged += (s, e) => _previewPanel?.Invalidate();
            _lblStirrupA1 = AddRowToLayout(layoutStirrupZone, "Khoảng cách đai dầy A1 (mm):", _numStirrupSpacingA1 = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, Increment = 10, Width = 90 });
            _lblStirrupA2 = AddRowToLayout(layoutStirrupZone, "Khoảng cách đai thưa A2 (mm):", _numStirrupSpacingA2 = new NumericUpDown { Minimum = 100, Maximum = 500, Value = 200, Increment = 10, Width = 90 });
            _lblZoneA1Len = AddRowToLayout(layoutStirrupZone, "Chiều dài vùng dầy A1 (mm):", _numZoneA1Length = new NumericUpDown { Minimum = 300, Maximum = 2000, Value = 600, Increment = 50, Width = 90 });
            _grpStirrupZone.Controls.Add(layoutStirrupZone);

            _grpInnerStirrup = new GroupBox { Text = "Cấu tạo Đai Phụ & Móc Đai", Dock = DockStyle.Top, Height = 110, Padding = new Padding(10) };
            var pnlInnerStirrup = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkInnerDiamond = new CheckBox { Text = "Tạo đai lồng / đai thoi JP_T80 (khi ≥3 thanh/cạnh)", Checked = true, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
            _chkCrossLinks = new CheckBox { Text = "Tạo đai móc phụ / Crosslink JP_T68", Checked = true, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
            _chkInnerDiamond.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _chkCrossLinks.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            pnlInnerStirrup.Controls.Add(_chkInnerDiamond);
            pnlInnerStirrup.Controls.Add(_chkCrossLinks);
            _grpInnerStirrup.Controls.Add(pnlInnerStirrup);

            _tabStirrup.Controls.Add(_grpInnerStirrup);
            _tabStirrup.Controls.Add(_grpStirrupZone);
            tabControl.TabPages.Add(_tabStirrup);

            // --- TAB 3: GENERAL SETTINGS ---
            _tabGenSettings = new TabPage { Text = "⚙️ General Settings", Padding = new Padding(10), BackColor = Color.White };
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

            layoutGenSettings.Controls.Add(_grpHook, 0, 0); layoutGenSettings.Controls.Add(_grpBendCut, 1, 0);
            layoutGenSettings.Controls.Add(_grpTopRoof, 0, 1); layoutGenSettings.Controls.Add(_grpSplicePos, 1, 1);
            layoutGenSettings.Controls.Add(_grpAssignInfo, 0, 2); layoutGenSettings.Controls.Add(_grpSlabBeam, 1, 2);

            _tabGenSettings.Controls.Add(layoutGenSettings);
            tabControl.TabPages.Add(_tabGenSettings);

            // --- TAB 4: BẢN VẼ & VIEW 3D ---
            _tabViews = new TabPage { Text = "🖼️ Bản Vẽ & View 3D", Padding = new Padding(12), BackColor = Color.White };
            _grpViews = new GroupBox { Text = "Tự động Tạo View & Triển khai Bản vẽ", Dock = DockStyle.Top, Height = 130, Padding = new Padding(10) };
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
                _lblSelectedCount.Text = $"🟢 Đã chọn sẵn: {count} cột từ Revit";
                _lblSelectedCount.ForeColor = Color.DarkGreen;
            }
            else
            {
                _lblSelectedCount.Text = $"🔵 Đã chọn: {count} / {_columnListBox.Items.Count} cột";
                _lblSelectedCount.ForeColor = Color.DarkBlue;
            }
        }

        private void PopulateColumnList()
        {
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

            _cmbMainDia.Items.AddRange(names);
            _cmbStirrupDia.Items.AddRange(names);
            if (names.Any())
            {
                _cmbMainDia.SelectedIndex = 0;
                _cmbStirrupDia.SelectedIndex = 0;
            }
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

            using var tx = new Transaction(_doc, "Create Rectangular Column Rebar");
            tx.Start();
            FailureHandlingOptions failOptions = tx.GetFailureHandlingOptions();
            failOptions.SetFailuresPreprocessor(new KhimTools.SlabJoin.Utilities.SwallowWarningsPreprocessor());
            tx.SetFailureHandlingOptions(failOptions);
            try
            {
                // Nạp sẵn toàn bộ RebarShape chuẩn (JP_T00, JP_T11, JP_T21, JP_T51, JP_T80...) vào Document
                RebarShapeLibrary.PreloadCommonShapes(_doc);

                var generator = new RectangularColumnRebarGenerator(_doc);
                var drawingGen = new ColumnRebarDrawingGenerator(_doc);
                var sectionGen = new ColumnRebarSectionViewGenerator(_doc);
                var view3DGen = new ColumnRebar3DViewGenerator(_doc);

                List<FamilyInstance> rawColumns = selectedItems.Select(i => i.Column).ToList();
                List<List<FamilyInstance>> axisGroups = RebarLapSpliceHelper.GroupColumnsByAxis(rawColumns, _doc);

                var report = new RebarGenerationReport();

                foreach (var group in axisGroups)
                {
                    var inputs = group.Select(col => new RectangularColumnRebarInput
                    {
                        Column = col,
                        MainBarType = mainType,
                        StirrupBarType = stirrupType,
                        BarsAlongB = (int)_numBarsB.Value,
                        BarsAlongH = (int)_numBarsH.Value,
                        StirrupSpacingA1 = UnitUtils.ConvertToInternalUnits((double)_numStirrupSpacingA1.Value, UnitTypeId.Millimeters),
                        StirrupSpacingA2 = UnitUtils.ConvertToInternalUnits((double)_numStirrupSpacingA2.Value, UnitTypeId.Millimeters),
                        ZoneA1Length = UnitUtils.ConvertToInternalUnits((double)_numZoneA1Length.Value, UnitTypeId.Millimeters),
                        HasInnerDiamondStirrup = _chkInnerDiamond.Checked,
                        HasCrossLinks = _chkCrossLinks.Checked,
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

                    var createdRebars = generator.GenerateMultiStory(inputs, report);

                    foreach (var item in group)
                    {
                        if (_chkAutoDrawing.Checked)
                        {
                            try
                            {
                                var profile = RectangularColumnGeometryHelper.GetRectangularProfile(item);
                                double coverFeet = customCoverFeet ?? RebarCoverHelper.GetColumnCover(item, RebarFace.Exterior);

                                drawingGen.CreateOrUpdate(new ColumnRebarDrawingInput
                                {
                                    Shape = ColumnShapeType.Rectangular,
                                    ColumnMark = item.LookupParameter("Mark")?.AsString() ?? item.Id.ToLongValue().ToString(),
                                    ColumnWidthMm = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters),
                                    ColumnHeightMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters),
                                    BarsAlongB = (int)_numBarsB.Value,
                                    BarsAlongH = (int)_numBarsH.Value,
                                    MainBarLabel = _cmbMainDia.Text,
                                    StirrupLabel = _cmbStirrupDia.Text,
                                    StirrupSpacingMm = (double)_numStirrupSpacingA1.Value,
                                    CoverMm = UnitUtils.ConvertFromInternalUnits(coverFeet, UnitTypeId.Millimeters)
                                });
                            }
                            catch (Exception exDraw)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ColumnRebarDrawingGenerator] Error: {exDraw.Message}");
                            }
                        }

                        if (_chkAutoSection3D.Checked)
                        {
                            try
                            {
                                var itemRebars = HostedRebarQuery.GetHostedRebar(_doc, item);
                                sectionGen.CreateOrUpdate(item, itemRebars);
                                view3DGen.CreateOrUpdate(item, itemRebars);
                            }
                            catch (Exception exView)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ColumnRebarViewGenerator] Error: {exView.Message}");
                            }
                        }
                    }
                }
                tx.Commit();
                if (report.HasErrors)
                {
                    KhimDialogHelper.ShowRebarGenerationReport(report, "Cột Chữ Nhật (Column)", selectedItems.Count);
                }
                else
                {
                    KhimDialogHelper.ShowColumnRebarSuccess(selectedItems.Count, axisGroups.Count, _chkAutoDrawing.Checked, _chkAutoSection3D.Checked);
                }
            }
            catch (Exception ex)
            {
                tx.RollBack();
                string errTitle = LanguageManager.IsEnglish ? "Error Creating Rebar" : "Lỗi Tạo Thép Cột";
                KhimDialogHelper.ShowError(errTitle, ex.Message, ex.StackTrace);
            }
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

            // ══════════════════════════════════════════════════════════════════
            // 1. DỮ LIỆU CỘT & THIẾT LẬP THÉP
            // ══════════════════════════════════════════════════════════════════
            var selectedItem = _columnListBox.SelectedItem as ColumnListItem;
            FamilyInstance col = selectedItem?.Column ?? _preSelectedColumns.FirstOrDefault() ?? _availableColumns.FirstOrDefault();

            double heightMm = 3600;
            double bMm = 600;
            double hMm = 700;
            string mark = "<not set>";
            string levelName = isEn ? "Level 1" : "Tầng 1";

            if (col != null)
            {
                try
                {
                    var profile = RectangularColumnGeometryHelper.GetRectangularProfile(col);
                    heightMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.Height, UnitTypeId.Millimeters));
                    bMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters));
                    hMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters));
                    mark = col.LookupParameter("Mark")?.AsString() ?? "<not set>";
                    levelName = _doc.GetElement(col.LevelId)?.Name ?? (isEn ? "Level" : "Tầng");
                }
                catch { }
            }

            int barsB = (int)_numBarsB.Value;
            int barsH = (int)_numBarsH.Value;
            int totalBars = 2 * (barsB + barsH - 2);
            string diaStr = _cmbMainDia?.Text ?? "18";
            string stirrupDiaStr = _cmbStirrupDia?.Text ?? "8";
            double coverVal = _chkCustomCover.Checked ? (double)_numCustomCover.Value : 25;

            var fontTitle = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            var fontSmall = new Font("Segoe UI", 7.5F);
            var fontRed = new Font("Segoe UI", 7.5F, FontStyle.Bold);

            // ══════════════════════════════════════════════════════════════════
            // 2. PHẦN TRÊN: MẶT ĐỨNG CỐT THÉP CỘT (ELEVATION PREVIEW)
            // ══════════════════════════════════════════════════════════════════
            g.DrawString(isEn ? "1. COLUMN ELEVATION PREVIEW" : "1. MẶT ĐỨNG CỐT THÉP CỘT", fontTitle, Brushes.DarkRed, 10, 6);

            int colWidth = 45;
            int colHeight = 180;
            int cx = Math.Max(_previewPanel.Width / 2, 110);
            int cy = 115;
            int x0 = cx - colWidth / 2;
            int y0 = cy - colHeight / 2;

            // Thân bê tông mặt đứng
            using (var fillBrush = new SolidBrush(Color.FromArgb(40, 140, 70)))
                g.FillRectangle(fillBrush, x0, y0, colWidth, colHeight);
            using (var outlinePen = new Pen(Color.Black, 1.5f))
                g.DrawRectangle(outlinePen, x0, y0, colWidth, colHeight);

            // Các đường đai ngang màu đỏ (Dày A1 ở 2 đầu, thưa A2 ở giữa)
            using (var stirrupPen = new Pen(Color.Red, 1.2f))
            {
                for (int y = y0 + colHeight - 4; y >= y0 + colHeight - 38; y -= 5)
                    g.DrawLine(stirrupPen, x0 + 2, y, x0 + colWidth - 2, y);

                for (int y = y0 + colHeight - 46; y >= y0 + 46; y -= 10)
                    g.DrawLine(stirrupPen, x0 + 2, y, x0 + colWidth - 2, y);

                for (int y = y0 + 38; y >= y0 + 4; y -= 5)
                    g.DrawLine(stirrupPen, x0 + 2, y, x0 + colWidth - 2, y);
            }

            // Thép chủ đứng (Cranked 1:6 / Móc đỉnh / Chân quỳ)
            using (var rebarPen = new Pen(Color.Navy, 2))
            {
                bool isFoundation = _rdBaseFoundation != null && _rdBaseFoundation.Checked;
                bool isTopHook = _chkTopAnchor != null && _chkTopAnchor.Checked;
                bool isCranked = _chkCrankedSplice != null && _chkCrankedSplice.Checked;

                int[] barXs = new int[] { x0 + 6, x0 + colWidth / 2, x0 + colWidth - 6 };

                foreach (int bx in barXs)
                {
                    int bY = y0 + colHeight;
                    int tY = y0;

                    // Base anchor
                    if (isFoundation)
                    {
                        int footDir = (bx < cx) ? -12 : 12;
                        g.DrawLine(rebarPen, bx + footDir, bY + 12, bx, bY + 12);
                        g.DrawLine(rebarPen, bx, bY + 12, bx, bY);
                    }
                    else
                    {
                        g.DrawLine(rebarPen, bx, bY + 12, bx, bY);
                    }

                    // Body
                    if (isCranked && !isTopHook)
                    {
                        int crankY1 = y0 + 12;
                        int crankY2 = y0 - 4;
                        int crankX = (bx < cx) ? bx + 4 : (bx > cx ? bx - 4 : bx);

                        g.DrawLine(rebarPen, bx, bY, bx, crankY1);
                        g.DrawLine(rebarPen, bx, crankY1, crankX, crankY2);
                        g.DrawLine(rebarPen, crankX, crankY2, crankX, y0 - 20);
                    }
                    else
                    {
                        g.DrawLine(rebarPen, bx, bY, bx, tY);
                    }

                    // Top Hook
                    if (isTopHook)
                    {
                        int hookDir = (bx < cx) ? 10 : (bx > cx ? -10 : -5);
                        g.DrawLine(rebarPen, bx, tY, bx, tY + 2);
                        g.DrawLine(rebarPen, bx, tY + 2, bx + hookDir, tY + 2);
                    }
                }
            }

            // Đường tim cao độ tầng
            using (var levelPen = new Pen(Color.Gray, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.DashDot })
                g.DrawLine(levelPen, x0 - 15, y0 + colHeight, x0 + colWidth + 20, y0 + colHeight);
            g.DrawString($"▼ {levelName}", fontSmall, Brushes.Black, x0 + colWidth + 2, y0 + colHeight + 2);

            // Thông số mặt đứng
            int leftX = 6;
            int rightX = x0 + colWidth + 6;
            int textY = y0 + colHeight / 2 - 25;

            string heightLabel = isEn ? "Height" : "Chiều cao";
            string markLabel = isEn ? "Mark" : "Ký hiệu";
            string mainRebarLabel = isEn ? "Main Rebar" : "Thép chủ";
            string distLabel = isEn ? "Distribute" : "Phân bố";

            g.DrawString($"{heightLabel} = {heightMm} (mm)\nBxH = {bMm}x{hMm}\n{markLabel}: {mark}", fontSmall, Brushes.Black, leftX, textY);
            g.DrawString($"{mainRebarLabel}:\n  {totalBars}Φ{diaStr}\n{distLabel}:\n  A1, A2, A1", fontRed, Brushes.Red, rightX, textY);

            // ══════════════════════════════════════════════════════════════════
            // 3. ĐƯỜNG PHÂN CÁCH (DIVIDER)
            // ══════════════════════════════════════════════════════════════════
            int dividerY = y0 + colHeight + 25;
            using (var divPen = new Pen(Color.FromArgb(220, 220, 230), 1))
                g.DrawLine(divPen, 8, dividerY, _previewPanel.Width - 8, dividerY);

            // ══════════════════════════════════════════════════════════════════
            // 4. PHẦN DƯỚI: MẶT CẮT NGANG TIẾT DIỆN (CROSS SECTION BxH PREVIEW)
            // ══════════════════════════════════════════════════════════════════
            int secTitleY = dividerY + 6;
            g.DrawString(isEn ? "2. CROSS SECTION PREVIEW (B x H)" : "2. MẶT CẮT TIẾT DIỆN THÉP CỘT (B x H)", fontTitle, Brushes.DarkBlue, 10, secTitleY);

            // Tính kích thước vẽ tiết diện theo tỷ lệ B / H
            int secAreaY = secTitleY + 22;
            int maxBoxW = 140;
            int maxBoxH = 120;
            double ratio = (hMm > 0) ? (bMm / hMm) : 1.0;
            int secW = ratio >= 1.0 ? maxBoxW : (int)(maxBoxH * ratio);
            int secH = ratio >= 1.0 ? (int)(maxBoxW / ratio) : maxBoxH;
            secW = Math.Max(secW, 70);
            secH = Math.Max(secH, 70);

            int secX = (_previewPanel.Width - secW) / 2;
            int secY = secAreaY + (maxBoxH - secH) / 2;

            // 4.1 Bê tông cột (Nền xanh nhạt + viền đen)
            using (var secFill = new SolidBrush(Color.FromArgb(235, 245, 235)))
                g.FillRectangle(secFill, secX, secY, secW, secH);
            using (var secBorder = new Pen(Color.Black, 2))
                g.DrawRectangle(secBorder, secX, secY, secW, secH);

            // 4.2 Lớp bảo vệ bê tông (Cover offset)
            int coverPx = 10;
            int inX = secX + coverPx;
            int inY = secY + coverPx;
            int inW = secW - coverPx * 2;
            int inH = secH - coverPx * 2;

            // 4.3 Đai ngoài chữ nhật kín màu đỏ (Outer Hoop)
            using (var outerStirrupPen = new Pen(Color.Red, 2))
                g.DrawRectangle(outerStirrupPen, inX, inY, inW, inH);

            // 4.4 Đai thoi / Đai lồng (Diamond Hoop) nếu có từ 3 thanh/cạnh
            if (_chkInnerDiamond != null && _chkInnerDiamond.Checked && barsB >= 3 && barsH >= 3)
            {
                using var diamondPen = new Pen(Color.OrangeRed, 1.5f);
                Point[] diamondPts = new Point[]
                {
                    new Point(inX + inW / 2, inY),
                    new Point(inX + inW, inY + inH / 2),
                    new Point(inX + inW / 2, inY + inH),
                    new Point(inX, inY + inH / 2)
                };
                g.DrawPolygon(diamondPen, diamondPts);
            }

            // 4.5 Đai C / Crosslink nếu có
            if (_chkCrossLinks != null && _chkCrossLinks.Checked)
            {
                using var crossPen = new Pen(Color.Purple, 1.2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                if (barsB >= 3)
                {
                    for (int i = 1; i < barsB - 1; i++)
                    {
                        int lx = inX + (int)((double)i / (barsB - 1) * inW);
                        g.DrawLine(crossPen, lx, inY, lx, inY + inH);
                    }
                }
                if (barsH >= 3)
                {
                    for (int j = 1; j < barsH - 1; j++)
                    {
                        int ly = inY + (int)((double)j / (barsH - 1) * inH);
                        g.DrawLine(crossPen, inX, ly, inX + inW, ly);
                    }
                }
            }

            // 4.6 Các chấm tròn thép chủ (Main Rebar Dots)
            int dotR = 8;
            using var barFill = new SolidBrush(Color.Navy);
            using var barBorder = new Pen(Color.White, 1.2f);

            var barPts = new List<Point>();

            // Cạnh trên & dưới
            for (int i = 0; i < barsB; i++)
            {
                int x = inX + (int)((double)i / (barsB - 1) * inW);
                barPts.Add(new Point(x, inY));
                barPts.Add(new Point(x, inY + inH));
            }

            // Cạnh trái & phải
            for (int j = 1; j < barsH - 1; j++)
            {
                int y = inY + (int)((double)j / (barsH - 1) * inH);
                barPts.Add(new Point(inX, y));
                barPts.Add(new Point(inX + inW, y));
            }

            foreach (var pt in barPts)
            {
                g.FillEllipse(barFill, pt.X - dotR / 2, pt.Y - dotR / 2, dotR, dotR);
                g.DrawEllipse(barBorder, pt.X - dotR / 2, pt.Y - dotR / 2, dotR, dotR);
            }

            // 4.7 Kích thước & Chú thích tiết diện
            using (var dimPen = new Pen(Color.DarkSlateGray, 1))
            {
                // Dim B (Top)
                g.DrawLine(dimPen, secX, secY - 6, secX + secW, secY - 6);
                g.DrawLine(dimPen, secX, secY - 9, secX, secY - 3);
                g.DrawLine(dimPen, secX + secW, secY - 9, secX + secW, secY - 3);
                using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString($"B = {bMm}", fontSmall, Brushes.Black, secX + secW / 2, secY - 20, sfCenter);

                // Dim H (Right)
                g.DrawLine(dimPen, secX + secW + 6, secY, secX + secW + 6, secY + secH);
                g.DrawLine(dimPen, secX + secW + 3, secY, secX + secW + 9, secY);
                g.DrawLine(dimPen, secX + secW + 3, secY + secH, secX + secW + 9, secY + secH);
                g.DrawString($"H = {hMm}", fontSmall, Brushes.Black, secX + secW + 10, secY + secH / 2 - 6);
            }

            // Legend ghi chú dưới cùng
            int legendY = secY + secH + 8;
            string secLegend = isEn
                ? $"Section: {totalBars}Φ{diaStr} ({barsB}xB + {barsH}xH) | Stirrup: Φ{stirrupDiaStr} | Cover: {coverVal}mm"
                : $"Tiết diện: {totalBars}Φ{diaStr} ({barsB}xB + {barsH}xH) | Đai: Φ{stirrupDiaStr} | Lớp bảo vệ: {coverVal}mm";

            using (var sfLeg = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString(secLegend, fontSmall, Brushes.DarkBlue, _previewPanel.Width / 2, legendY, sfLeg);
            }
        }

        private void ApplyLanguage()
        {
            bool isEn = LanguageManager.IsEnglish;

            Text = isEn ? "🏗️ Rectangular Column Reinforcement" : "🏗️ Bố trí Thép Cột Vuông / Chữ Nhật";

            if (_tabMain != null) _tabMain.Text = isEn ? "📌 Main Rebar & Preview" : "📌 Thép Chủ & Xem Trước";
            if (_tabStirrup != null) _tabStirrup.Text = isEn ? "🌀 Stirrups" : "🌀 Thép Đai";
            if (_tabGenSettings != null) _tabGenSettings.Text = isEn ? "⚙️ General Settings" : "⚙️ Cài Đặt Chung";
            if (_tabViews != null) _tabViews.Text = isEn ? "🖼️ Drawing & Views" : "🖼️ Bản Vẽ & Khung Nhìn";

            // Tab 1 Main
            if (_grpMainSection != null) _grpMainSection.Text = isEn ? "Main Rebar Arrangement" : "Bố trí Thép Chủ Tiết Diện";
            if (_lblBarsB != null) _lblBarsB.Text = isEn ? "Main rebar side B (incl. corners):" : "Thép chủ cạnh B (kể cả góc):";
            if (_lblBarsH != null) _lblBarsH.Text = isEn ? "Main rebar side H (incl. corners):" : "Thép chủ cạnh H (kể cả góc):";
            if (_lblMainDia != null) _lblMainDia.Text = isEn ? "Main rebar diameter:" : "Đường kính thép chủ:";

            if (_grpCover != null) _grpCover.Text = isEn ? "Concrete Cover" : "Lớp Bê Tông Bảo Vệ";
            if (_chkCustomCover != null) _chkCustomCover.Text = isEn ? "Custom Cover (mm)" : "Nhập tay Lớp Bảo Vệ (mm)";
            if (_lblCustomCover != null) _lblCustomCover.Text = isEn ? "Custom Cover (mm):" : "Lớp bảo vệ tùy chỉnh (mm):";
            if (_btnProjectCover != null) _btnProjectCover.Text = isEn ? "⚙️ Project Cover" : "⚙️ Lớp Bảo Vệ Dự Án";

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

            if (_grpInnerStirrup != null) _grpInnerStirrup.Text = isEn ? "Inner Tie & Crosslink Options" : "Cấu tạo Đai Phụ & Móc Đai";
            if (_chkInnerDiamond != null) _chkInnerDiamond.Text = isEn ? "Create inner diamond stirrup (when ≥3 bars/side)" : "Tạo đai lồng / đai thoi (khi ≥3 thanh/cạnh)";
            if (_chkCrossLinks != null) _chkCrossLinks.Text = isEn ? "Create crosslinks / C-links" : "Tạo đai móc phụ / Đai C";

            // Tab 3 General Settings
            if (_grpHook != null) _grpHook.Text = isEn ? "REBAR HOOK BENDING SECTION" : "CẤU TẠO UỐN MÓC THÉP";
            if (_rdHookLengthFixed != null) _rdHookLengthFixed.Text = isEn ? "By fixed length L (mm):" : "Theo chiều dài cố định L (mm):";
            if (_rdHookLengthDia != null) _rdHookLengthDia.Text = isEn ? "By diameter (xD):" : "Theo đường kính thanh (xD):";

            if (_grpBendCut != null) _grpBendCut.Text = isEn ? "REBAR BENDING OR CUTTING CONDITIONS" : "ĐIỀU KIỆN UỐN HOẶC CẮT THÉP";
            if (_lblBendE != null) _lblBendE.Text = isEn ? "Bend rebar if offset e ≤ (mm):" : "Uốn thép nếu độ lệch e ≤ (mm):";
            if (_lblBendRatio != null) _lblBendRatio.Text = isEn ? "Bend slope ratio Hd/e ≥:" : "Tỷ lệ độ dốc uốn Hd/e ≥:";

            if (_grpTopRoof != null) _grpTopRoof.Text = isEn ? "SET TOP ROOF REBAR" : "KẾT THÚC THÉP ĐỈNH MÁI";
            if (_rdTopRoofHook != null) _rdTopRoofHook.Text = isEn ? "Bend hook for top floor rebar" : "Bẻ móc cho thép tầng đỉnh mái";
            if (_rdTopRoofContinue != null) _rdTopRoofContinue.Text = isEn ? "Continue straight for next level" : "Chờ thẳng cho tầng tiếp theo";

            if (_grpSplicePos != null) _grpSplicePos.Text = isEn ? "REBAR SPLICE POSITION" : "VỊ TRÍ NỐI THÉP CỘT";
            if (_lblSpliceDist != null) _lblSpliceDist.Text = isEn ? "Splice distance from column base L = (mm):" : "Khoảng cách nối từ chân cột L = (mm):";

            if (_grpAssignInfo != null) _grpAssignInfo.Text = isEn ? "ASSIGN ADDITIONAL INFORMATION TO REBAR" : "GÁN THÔNG TIN BỔ SUNG CHO THÉP";
            if (_chkAssignElevation != null) _chkAssignElevation.Text = isEn ? "Assign column elevation to rebar" : "Gán cao độ cột vào thông số thép";
            if (_chkAssignPartition != null) _chkAssignPartition.Text = isEn ? "Automatically assign Partition to rebar" : "Tự động gán Phân vùng (Partition) cho thép";

            if (_grpSlabBeam != null) _grpSlabBeam.Text = isEn ? "OPTION AT SLAB BEAM POSITION" : "TÙY CHỌN TẠI VỊ TRÍ DẦM / SÀN";
            if (_lblDefaultHd != null) _lblDefaultHd.Text = isEn ? "Default beam height Hd (mm):" : "Chiều cao dầm mặc định Hd (mm):";

            // Tab 4 Drawing
            if (_grpViews != null) _grpViews.Text = isEn ? "Drawing & View Options" : "Tự Động Tạo Khung Nhìn & Bản Vẽ";
            if (_chkAutoDrawing != null) _chkAutoDrawing.Text = isEn ? "Automatically generate 2D section drawing & BBS" : "Tự động tạo bản vẽ 2D (Mặt cắt tiết diện & Thống kê thép)";
            if (_chkAutoSection3D != null) _chkAutoSection3D.Text = isEn ? "Create 3D Inspection Views (Plan View + 3D View)" : "Tự động tạo Khung nhìn xem thép 3D (Mặt bằng + 3D)";

            // Right & Bottom Panels
            if (_lblColTitle != null) _lblColTitle.Text = isEn ? "📋 Column List" : "📋 Danh Sách Cột";
            if (_lblTemplate != null) _lblTemplate.Text = isEn ? "📋 Configuration Template:" : "📋 Mẫu Thiết Lập:";
            if (_btnSaveTemplate != null) _btnSaveTemplate.Text = isEn ? "Save..." : "Lưu mẫu...";
            if (_btnApplyTemplate != null) _btnApplyTemplate.Text = isEn ? "Apply" : "Áp dụng";
            if (_btnDeleteTemplate != null) _btnDeleteTemplate.Text = isEn ? "Delete" : "Xóa mẫu";
            if (_rdScopeSelected != null) _rdScopeSelected.Text = isEn ? $"Selected columns ({_preSelectedColumns.Count})" : $"Chỉ các cột đã chọn ({_preSelectedColumns.Count})";
            if (_rdScopeAll != null) _rdScopeAll.Text = isEn ? $"All model columns ({_availableColumns.Count})" : $"Tất cả cột ({_availableColumns.Count})";
            if (_btnSelectAll != null) _btnSelectAll.Text = isEn ? "Select All" : "Chọn Tất Cả";
            if (_btnDeselectAll != null) _btnDeselectAll.Text = isEn ? "Deselect All" : "Bỏ Chọn";

            if (_btnCreateRebar != null) _btnCreateRebar.Text = isEn ? "⚡ Create Rebar" : "⚡ Tạo Thép";
            if (_btnClose != null) _btnClose.Text = isEn ? "Close" : "Đóng";

            UpdateSelectedCount();
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
            _chkInnerDiamond.Checked = settings.HasInnerDiamondStirrup;
            _chkCrossLinks.Checked = settings.HasCrossLinks;

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

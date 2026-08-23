using KhimTools.Core.UI;
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
    /// Form "Multi-Column Rebar 2.0" cao cß║Ñp cho Cß╗Öt Vu├┤ng / Chß╗» Nhß║¡t.
    /// Tß╗▒ ─æß╗Öng giß╗» v├á highlight 100% danh s├ích cß╗Öt ─æ├ú chß╗ìn tr╞░ß╗¢c trong Revit viewport.
    /// </summary>
    public class RectangularColumnReinforcementForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableColumns;
        private readonly List<FamilyInstance> _preSelectedColumns;

        // UI Controls
        private ListBox _columnListBox;
        private Label _lblSelectedCount;
        private Panel _previewPanel;

        // Tab 1: Th├⌐p Chß╗º & Cover
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

        // Tab 2: Th├⌐p ─Éai
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

        // Tab 4: Bß║ún vß║╜ & View
        private CheckBox _chkAutoDrawing;
        private CheckBox _chkAutoSection3D;
        private Button _btnCreateRebar;
        private Label _lblSafetyStatus;
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
            Text = "KHIM TOOLS ΓÇö Bß╗æ tr├¡ Th├⌐p Cß╗Öt Vu├┤ng / Chß╗» Nhß║¡t";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 1. Bottom Control Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.FromArgb(245, 245, 247) };
            var lblLang = new Label { Text = "Language:", AutoSize = true, Left = 15, Top = 18, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, Left = 95, Top = 14, Font = new Font("Segoe UI", 8.5F) };
            _cmbLanguage.Items.Add("Tiß║┐ng Viß╗çt");
            _cmbLanguage.Items.Add("English");
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;
            _cmbLanguage.SelectedIndexChanged += (s, e) =>
            {
                LanguageManager.CurrentLanguage = _cmbLanguage.SelectedIndex == 1 ? AppLanguage.English : AppLanguage.Vietnamese;
                ApplyLanguage();
            };

            _btnCreateRebar = new Button
            {
                Text = "Create Rebar",
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

            _lblSafetyStatus = new Label
            {
                AutoSize = true,
                Left = 230,
                Top = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 125, 50),
                Visible = false
            };
            bottomPanel.Controls.Add(_lblSafetyStatus);
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
            _lblColTitle = new Label { Text = "Danh S├ích Cß╗Öt", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            var scopePanel = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(240, 243, 248), Padding = new Padding(4) };
            _rdScopeSelected = new RadioButton { Text = $"Chß╗ë c├íc cß╗Öt ─æ├ú chß╗ìn ({_preSelectedColumns.Count})", Checked = _preSelectedColumns.Any(), AutoSize = true, Top = 4, Left = 4, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.DarkGreen };
            _rdScopeAll = new RadioButton { Text = $"Tß║Ñt cß║ú cß╗Öt ({_availableColumns.Count})", Checked = !_preSelectedColumns.Any(), AutoSize = true, Top = 26, Left = 4, Font = new Font("Segoe UI", 8.5F) };

            _rdScopeSelected.CheckedChanged += (s, e) => PopulateColumnList();
            _rdScopeAll.CheckedChanged += (s, e) => PopulateColumnList();

            scopePanel.Controls.Add(_rdScopeSelected);
            scopePanel.Controls.Add(_rdScopeAll);

            _lblSelectedCount = new Label { Text = "─É├ú chß╗ìn: 0 cß╗Öt", Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };

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
            _lblTemplate = new Label { Text = "Mß║½u Thiß║┐t Lß║¡p:", AutoSize = true, Left = 15, Top = 14, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
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

            // 3. TabControl Trung t├óm
            var tabControl = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };

            // --- TAB 1: TH├ëP CHß╗ª & REVIEW ---
            _tabMain = new TabPage { Text = "Th├⌐p Chß╗º & Review", Padding = new Padding(8), BackColor = Color.White };
            var pnlMainLeft = new Panel { Dock = DockStyle.Left, Width = 350 };

            _grpMainSection = new GroupBox { Text = "Bß╗æ tr├¡ Th├⌐p Chß╗º Tiß║┐t Diß╗çn", Dock = DockStyle.Top, Height = 135, Padding = new Padding(8) };
            var layoutMainSec = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _numBarsB = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 3, Width = 80 };
            _numBarsH = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 3, Width = 80 };
            _numBarsB.ValueChanged += (s, e) => _previewPanel?.Invalidate();
            _numBarsH.ValueChanged += (s, e) => _previewPanel?.Invalidate();

            _lblBarsB = AddRowToLayout(layoutMainSec, "Th├⌐p chß╗º cß║ính B (kß╗â cß║ú g├│c):", _numBarsB);
            _lblBarsH = AddRowToLayout(layoutMainSec, "Th├⌐p chß╗º cß║ính H (kß╗â cß║ú g├│c):", _numBarsH);
            _lblMainDia = AddRowToLayout(layoutMainSec, "─É╞░ß╗¥ng k├¡nh th├⌐p chß╗º:", _cmbMainDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 });
            _cmbMainDia.SelectedIndexChanged += (s, e) => _previewPanel?.Invalidate();
            _grpMainSection.Controls.Add(layoutMainSec);

            _grpCover = new GroupBox { Text = "Cover B├¬ T├┤ng", Dock = DockStyle.Top, Height = 95, Padding = new Padding(8) };
            var layoutCover = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _chkCustomCover = new CheckBox { Text = "Nhß║¡p tay Cover (mm)", Checked = false, AutoSize = true, Margin = new Padding(3, 4, 3, 4) };
            _numCustomCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 70, Enabled = false };
            _chkCustomCover.CheckedChanged += (s, e) => _numCustomCover.Enabled = _chkCustomCover.Checked;

            _btnProjectCover = new Button { Text = "Cover Dß╗▒ ├ün", Width = 105, Height = 25, FlatStyle = FlatStyle.System };
            _btnProjectCover.Click += (s, e) => new ProjectCoverSetupForm(_doc).ShowDialog();

            _lblCustomCover = AddRowToLayout(layoutCover, "Cover t├╣y chß╗ënh (mm):", _numCustomCover);
            layoutCover.Controls.Add(_chkCustomCover);
            layoutCover.Controls.Add(_btnProjectCover);
            _grpCover.Controls.Add(layoutCover);

            _grpMainAnchor = new GroupBox { Text = "Cß║Ñu tß║ío Neo & Nß╗æi Th├⌐p", Dock = DockStyle.Top, Height = 175, Padding = new Padding(8) };
            var pnlAnchor = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _rdBaseFoundation = new RadioButton { Text = "Cß╗Öt tß║ºng m├│ng (Nß╗æi ch├ón quß╗│ 90┬░ v├áo m├│ng)", AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _rdBaseStandardLevel = new RadioButton { Text = "Cß╗Öt tß║ºng s├án / ─æiß╗ân h├¼nh (Th├⌐p chß╗¥ nß╗æi tß║ºng)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _chkCrankedSplice = new CheckBox { Text = "Nhß║Ñn vß║»t nghi├¬ng 1:6 vß╗ï tr├¡ nß╗æi (ß║ónh 1)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _chkTopAnchor = new CheckBox { Text = "Neo uß╗æn m├│c 90┬░ ─æß╗ënh m├íi (ß║ónh 2)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
            _chkStaggeredSplice = new CheckBox { Text = "Nß╗æi so le 50% (Staggered 1.3 Ls)", Checked = true, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };

            _rdBaseFoundation.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _rdBaseStandardLevel.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _chkCrankedSplice.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _chkTopAnchor.CheckedChanged += (s, e) => _previewPanel?.Invalidate();

            var pnlLapMult = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(3, 2, 3, 2) };
            pnlLapMult.Controls.Add(new Label { Text = "Ls = n ├ù d:", AutoSize = true, Margin = new Padding(0, 5, 5, 0) });
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

            // --- TAB 2: TH├ëP ─ÉAI ---
            _tabStirrup = new TabPage { Text = "Th├⌐p ─Éai (Stirrups)", Padding = new Padding(12), BackColor = Color.White };

            _grpStirrupZone = new GroupBox { Text = "Ph├ón V├╣ng ─Éai A1 / A2 / A1 (Chuß║⌐n Kß║┐t Cß║Ñu)", Dock = DockStyle.Top, Height = 175, Padding = new Padding(10) };
            var layoutStirrupZone = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutStirrupZone.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layoutStirrupZone.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _lblStirrupDia = AddRowToLayout(layoutStirrupZone, "─É╞░ß╗¥ng k├¡nh th├⌐p ─æai:", _cmbStirrupDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 });
            _cmbStirrupDia.SelectedIndexChanged += (s, e) => _previewPanel?.Invalidate();
            _lblStirrupA1 = AddRowToLayout(layoutStirrupZone, "Khoß║úng c├ích ─æai dß║ºy A1 (mm):", _numStirrupSpacingA1 = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, Increment = 10, Width = 90 });
            _lblStirrupA2 = AddRowToLayout(layoutStirrupZone, "Khoß║úng c├ích ─æai th╞░a A2 (mm):", _numStirrupSpacingA2 = new NumericUpDown { Minimum = 100, Maximum = 500, Value = 200, Increment = 10, Width = 90 });
            _lblZoneA1Len = AddRowToLayout(layoutStirrupZone, "Chiß╗üu d├ái v├╣ng dß║ºy A1 (mm):", _numZoneA1Length = new NumericUpDown { Minimum = 300, Maximum = 2000, Value = 600, Increment = 50, Width = 90 });
            _grpStirrupZone.Controls.Add(layoutStirrupZone);

            _grpInnerStirrup = new GroupBox { Text = "Cß║Ñu tß║ío ─Éai Phß╗Ñ & M├│c ─Éai", Dock = DockStyle.Top, Height = 110, Padding = new Padding(10) };
            var pnlInnerStirrup = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkInnerDiamond = new CheckBox { Text = "Tß║ío ─æai lß╗ông / ─æai thoi JP_T80 (khi ΓëÑ3 thanh/cß║ính)", Checked = true, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
            _chkCrossLinks = new CheckBox { Text = "Tß║ío ─æai m├│c phß╗Ñ / Crosslink JP_T68", Checked = true, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
            _chkInnerDiamond.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
            _chkCrossLinks.CheckedChanged += (s, e) => _previewPanel?.Invalidate();
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
            _lblBendE = new Label { Text = "Bend rebar if e Γëñ (mm):", AutoSize = true };
            _numBendConditionE = new NumericUpDown { Minimum = 10, Maximum = 300, Value = 75, Width = 70 };
            _lblBendRatio = new Label { Text = "Bend by ratio Hd/e ΓëÑ:", AutoSize = true };
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
            _rdSpliceTwoPos = new RadioButton { Text = "Splice rebar at two positions (Nß╗æi so le 50%)", Checked = true, AutoSize = true };
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

            // --- TAB 4: Bß║óN Vß║╝ & VIEW 3D ---
            _tabViews = new TabPage { Text = "Bß║ún Vß║╜ & View 3D", Padding = new Padding(12), BackColor = Color.White };
            _grpViews = new GroupBox { Text = "Tß╗▒ ─æß╗Öng Tß║ío View & Triß╗ân khai Bß║ún vß║╜", Dock = DockStyle.Top, Height = 130, Padding = new Padding(10) };
            var pnlViews = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkAutoDrawing = new CheckBox { Text = "Tß╗▒ ─æß╗Öng tß║ío bß║ún vß║╜ 2D (Mß║╖t cß║»t tiß║┐t diß╗çn & Thß╗æng k├¬ th├⌐p)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
            _chkAutoSection3D = new CheckBox { Text = "Tß╗▒ ─æß╗Öng tß║ío View xem th├⌐p 3D (Plan View + 3D View)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
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
                _lblSelectedCount.Text = $"≡ƒƒó ─É├ú chß╗ìn sß║╡n: {count} cß╗Öt tß╗½ Revit";
                _lblSelectedCount.ForeColor = Color.DarkGreen;
            }
            else
            {
                _lblSelectedCount.Text = $"≡ƒö╡ ─É├ú chß╗ìn: {count} / {_columnListBox.Items.Count} cß╗Öt";
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
                MessageBox.Show(this, "Vui l├▓ng chß╗ìn ├¡t nhß║Ñt 1 cß╗Öt trong danh s├ích b├¬n phß║úi.", "Thiß║┐u th├┤ng tin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RebarBarType mainType = FindBarType(_cmbMainDia.Text);
            RebarBarType stirrupType = FindBarType(_cmbStirrupDia.Text);
            if (mainType == null || stirrupType == null)
            {
                MessageBox.Show(this, "Ch╞░a chß╗ìn ─æß╗º ─æ╞░ß╗¥ng k├¡nh th├⌐p.", "Thiß║┐u th├┤ng tin",
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
                // Nß║íp sß║╡n to├án bß╗Ö RebarShape chuß║⌐n (JP_T00, JP_T11, JP_T21, JP_T51, JP_T80...) v├áo Document
                RebarShapeLibrary.PreloadCommonShapes(_doc);

                var generator = new RectangularColumnRebarGenerator(_doc);
                var drawingGen = new ColumnRebarDrawingGenerator(_doc);
                var sectionGen = new ColumnRebarSectionViewGenerator(_doc);
                var view3DGen = new ColumnRebar3DViewGenerator(_doc);

                List<FamilyInstance> rawColumns = selectedItems.Select(i => i.Column).ToList();
                List<List<FamilyInstance>> axisGroups = RebarLapSpliceHelper.GroupColumnsByAxis(rawColumns, _doc);

                var report = new RebarGenerationReport();
                var allCreatedRebars = new List<Rebar>();

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
                    if (createdRebars != null) allCreatedRebars.AddRange(createdRebars);

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

                try
                {
                    var sampleCol = rawColumns.FirstOrDefault();
                    if (sampleCol != null)
                    {
                        var profile = RectangularColumnGeometryHelper.GetRectangularProfile(sampleCol);
                        double bMm = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters);
                        double hMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters);
                        double acMm2 = bMm * hMm;

                        int barsB = (int)_numBarsB.Value;
                        int barsH = (int)_numBarsH.Value;
                        int totalBars = (barsB * 2) + Math.Max(0, (barsH - 2) * 2);

                        double diaMm = UnitUtils.ConvertFromInternalUnits(mainType.BarModelDiameter, UnitTypeId.Millimeters);
                        if (diaMm <= 0) diaMm = 20.0;
                        double asMm2 = totalBars * (Math.PI * Math.Pow(diaMm / 2.0, 2));

                        var standard = RebarDesignStandardFactory.Create(GetSelectedDesignStandard());
                        var safety = RebarSafetyValidator.EvaluateColumn(sampleCol, allCreatedRebars, asMm2, acMm2, standard);

                        if (_lblSafetyStatus != null)
                        {
                            _lblSafetyStatus.Text = safety.FullDisplayText;
                            _lblSafetyStatus.ForeColor = safety.StatusColor;
                            _lblSafetyStatus.Visible = true;
                        }
                    }
                }
                catch { }
                if (report.HasErrors)
                {
                    KhimDialogHelper.ShowRebarGenerationReport(report, "Cß╗Öt Chß╗» Nhß║¡t (Column)", selectedItems.Count);
                }
                else
                {
                    KhimDialogHelper.ShowColumnRebarSuccess(selectedItems.Count, axisGroups.Count, _chkAutoDrawing.Checked, _chkAutoSection3D.Checked);
                }
            }
            catch (Exception ex)
            {
                tx.RollBack();
                string errTitle = LanguageManager.IsEnglish ? "Error Creating Rebar" : "Lß╗ùi Tß║ío Th├⌐p Cß╗Öt";
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

            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            // 1. Dß╗« LIß╗åU Cß╗ÿT & THIß║╛T Lß║¼P TH├ëP
            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            var selectedItem = _columnListBox.SelectedItem as ColumnListItem;
            FamilyInstance col = selectedItem?.Column ?? _preSelectedColumns.FirstOrDefault() ?? _availableColumns.FirstOrDefault();

            double heightMm = 3600;
            double bMm = 600;
            double hMm = 700;
            string mark = "<not set>";
            string levelName = isEn ? "Level 1" : "Tß║ºng 1";

            if (col != null)
            {
                try
                {
                    var profile = RectangularColumnGeometryHelper.GetRectangularProfile(col);
                    heightMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.Height, UnitTypeId.Millimeters));
                    bMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters));
                    hMm = Math.Round(UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters));
                    mark = col.LookupParameter("Mark")?.AsString() ?? "<not set>";
                    levelName = _doc.GetElement(col.LevelId)?.Name ?? (isEn ? "Level" : "Tß║ºng");
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

            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            // 2. PHß║ªN TR├èN: Mß║╢T ─Éß╗¿NG Cß╗ÉT TH├ëP Cß╗ÿT (ELEVATION PREVIEW)
            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            g.DrawString(isEn ? "1. COLUMN ELEVATION PREVIEW" : "1. Mß║╢T ─Éß╗¿NG Cß╗ÉT TH├ëP Cß╗ÿT", fontTitle, Brushes.DarkRed, 10, 6);

            int colWidth = 45;
            int colHeight = 180;
            int cx = Math.Max(_previewPanel.Width / 2, 110);
            int cy = 115;
            int x0 = cx - colWidth / 2;
            int y0 = cy - colHeight / 2;

            // Th├ón b├¬ t├┤ng mß║╖t ─æß╗⌐ng
            using (var fillBrush = new SolidBrush(Color.FromArgb(40, 140, 70)))
                g.FillRectangle(fillBrush, x0, y0, colWidth, colHeight);
            using (var outlinePen = new Pen(Color.Black, 1.5f))
                g.DrawRectangle(outlinePen, x0, y0, colWidth, colHeight);

            // C├íc ─æ╞░ß╗¥ng ─æai ngang m├áu ─æß╗Å (D├áy A1 ß╗ƒ 2 ─æß║ºu, th╞░a A2 ß╗ƒ giß╗»a)
            using (var stirrupPen = new Pen(Color.Red, 1.2f))
            {
                for (int y = y0 + colHeight - 4; y >= y0 + colHeight - 38; y -= 5)
                    g.DrawLine(stirrupPen, x0 + 2, y, x0 + colWidth - 2, y);

                for (int y = y0 + colHeight - 46; y >= y0 + 46; y -= 10)
                    g.DrawLine(stirrupPen, x0 + 2, y, x0 + colWidth - 2, y);

                for (int y = y0 + 38; y >= y0 + 4; y -= 5)
                    g.DrawLine(stirrupPen, x0 + 2, y, x0 + colWidth - 2, y);
            }

            // Th├⌐p chß╗º ─æß╗⌐ng (Cranked 1:6 / M├│c ─æß╗ënh / Ch├ón quß╗│)
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

            // ─É╞░ß╗¥ng tim cao ─æß╗Ö tß║ºng
            using (var levelPen = new Pen(Color.Gray, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.DashDot })
                g.DrawLine(levelPen, x0 - 15, y0 + colHeight, x0 + colWidth + 20, y0 + colHeight);
            g.DrawString($"Γû╝ {levelName}", fontSmall, Brushes.Black, x0 + colWidth + 2, y0 + colHeight + 2);

            // Th├┤ng sß╗æ mß║╖t ─æß╗⌐ng
            int leftX = 6;
            int rightX = x0 + colWidth + 6;
            int textY = y0 + colHeight / 2 - 25;

            string heightLabel = isEn ? "Height" : "Chiß╗üu cao";
            string markLabel = isEn ? "Mark" : "K├╜ hiß╗çu";
            string mainRebarLabel = isEn ? "Main Rebar" : "Th├⌐p chß╗º";
            string distLabel = isEn ? "Distribute" : "Ph├ón bß╗æ";

            g.DrawString($"{heightLabel} = {heightMm} (mm)\nBxH = {bMm}x{hMm}\n{markLabel}: {mark}", fontSmall, Brushes.Black, leftX, textY);
            g.DrawString($"{mainRebarLabel}:\n  {totalBars}╬ª{diaStr}\n{distLabel}:\n  A1, A2, A1", fontRed, Brushes.Red, rightX, textY);

            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            // 3. ─É╞»ß╗£NG PH├éN C├üCH (DIVIDER)
            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            int dividerY = y0 + colHeight + 25;
            using (var divPen = new Pen(Color.FromArgb(220, 220, 230), 1))
                g.DrawLine(divPen, 8, dividerY, _previewPanel.Width - 8, dividerY);

            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            // 4. PHß║ªN D╞»ß╗ÜI: Mß║╢T Cß║«T NGANG TIß║╛T DIß╗åN (CROSS SECTION BxH PREVIEW)
            // ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
            int secTitleY = dividerY + 6;
            g.DrawString(isEn ? "2. CROSS SECTION PREVIEW (B x H)" : "2. Mß║╢T Cß║«T TIß║╛T DIß╗åN TH├ëP Cß╗ÿT (B x H)", fontTitle, Brushes.DarkBlue, 10, secTitleY);

            // T├¡nh k├¡ch th╞░ß╗¢c vß║╜ tiß║┐t diß╗çn theo tß╗╖ lß╗ç B / H
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

            // 4.1 B├¬ t├┤ng cß╗Öt (Nß╗ün xanh nhß║ít + viß╗ün ─æen)
            using (var secFill = new SolidBrush(Color.FromArgb(235, 245, 235)))
                g.FillRectangle(secFill, secX, secY, secW, secH);
            using (var secBorder = new Pen(Color.Black, 2))
                g.DrawRectangle(secBorder, secX, secY, secW, secH);

            // 4.2 Lß╗¢p bß║úo vß╗ç b├¬ t├┤ng (Cover offset)
            int coverPx = 10;
            int inX = secX + coverPx;
            int inY = secY + coverPx;
            int inW = secW - coverPx * 2;
            int inH = secH - coverPx * 2;

            // 4.3 ─Éai ngo├ái chß╗» nhß║¡t k├¡n m├áu ─æß╗Å (Outer Hoop)
            using (var outerStirrupPen = new Pen(Color.Red, 2))
                g.DrawRectangle(outerStirrupPen, inX, inY, inW, inH);

            // 4.4 ─Éai thoi / ─Éai lß╗ông (Diamond Hoop) nß║┐u c├│ tß╗½ 3 thanh/cß║ính
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

            // 4.5 ─Éai C / Crosslink nß║┐u c├│
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

            // 4.6 C├íc chß║Ñm tr├▓n th├⌐p chß╗º (Main Rebar Dots)
            int dotR = 8;
            using var barFill = new SolidBrush(Color.Navy);
            using var barBorder = new Pen(Color.White, 1.2f);

            var barPts = new List<Point>();

            // Cß║ính tr├¬n & d╞░ß╗¢i
            for (int i = 0; i < barsB; i++)
            {
                int x = inX + (int)((double)i / (barsB - 1) * inW);
                barPts.Add(new Point(x, inY));
                barPts.Add(new Point(x, inY + inH));
            }

            // Cß║ính tr├íi & phß║úi
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

            // 4.7 K├¡ch th╞░ß╗¢c & Ch├║ th├¡ch tiß║┐t diß╗çn
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

            // Legend ghi ch├║ d╞░ß╗¢i c├╣ng
            int legendY = secY + secH + 8;
            string secLegend = isEn
                ? $"Section: {totalBars}╬ª{diaStr} ({barsB}xB + {barsH}xH) | Stirrup: ╬ª{stirrupDiaStr} | Cover: {coverVal}mm"
                : $"Tiß║┐t diß╗çn: {totalBars}╬ª{diaStr} ({barsB}xB + {barsH}xH) | ─Éai: ╬ª{stirrupDiaStr} | Lß╗¢p bß║úo vß╗ç: {coverVal}mm";

            using (var sfLeg = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString(secLegend, fontSmall, Brushes.DarkBlue, _previewPanel.Width / 2, legendY, sfLeg);
            }
        }

        private void ApplyLanguage()
        {
            bool isEn = LanguageManager.IsEnglish;

            Text = isEn ? "Rectangular Column Reinforcement" : "Bß╗æ tr├¡ Th├⌐p Cß╗Öt Vu├┤ng / Chß╗» Nhß║¡t";

            if (_tabMain != null) _tabMain.Text = isEn ? "Main Rebar & Preview" : "Th├⌐p Chß╗º & Xem Tr╞░ß╗¢c";
            if (_tabStirrup != null) _tabStirrup.Text = isEn ? "Stirrups" : "Th├⌐p ─Éai";
            if (_tabGenSettings != null) _tabGenSettings.Text = isEn ? "General Settings" : "C├ái ─Éß║╖t Chung";
            if (_tabViews != null) _tabViews.Text = isEn ? "Drawing & Views" : "Bß║ún Vß║╜ & Khung Nh├¼n";

            // Tab 1 Main
            if (_grpMainSection != null) _grpMainSection.Text = isEn ? "Main Rebar Arrangement" : "Bß╗æ tr├¡ Th├⌐p Chß╗º Tiß║┐t Diß╗çn";
            if (_lblBarsB != null) _lblBarsB.Text = isEn ? "Main rebar side B (incl. corners):" : "Th├⌐p chß╗º cß║ính B (kß╗â cß║ú g├│c):";
            if (_lblBarsH != null) _lblBarsH.Text = isEn ? "Main rebar side H (incl. corners):" : "Th├⌐p chß╗º cß║ính H (kß╗â cß║ú g├│c):";
            if (_lblMainDia != null) _lblMainDia.Text = isEn ? "Main rebar diameter:" : "─É╞░ß╗¥ng k├¡nh th├⌐p chß╗º:";

            if (_grpCover != null) _grpCover.Text = isEn ? "Concrete Cover" : "Lß╗¢p B├¬ T├┤ng Bß║úo Vß╗ç";
            if (_chkCustomCover != null) _chkCustomCover.Text = isEn ? "Custom Cover (mm)" : "Nhß║¡p tay Lß╗¢p Bß║úo Vß╗ç (mm)";
            if (_lblCustomCover != null) _lblCustomCover.Text = isEn ? "Custom Cover (mm):" : "Lß╗¢p bß║úo vß╗ç t├╣y chß╗ënh (mm):";
            if (_btnProjectCover != null) _btnProjectCover.Text = isEn ? "Project Cover" : "Lß╗¢p Bß║úo Vß╗ç Dß╗▒ ├ün";

            if (_grpMainAnchor != null) _grpMainAnchor.Text = isEn ? "Anchorage & Lap Splice Detailing" : "Cß║Ñu tß║ío Neo & Nß╗æi Th├⌐p";
            if (_rdBaseStandardLevel != null) _rdBaseStandardLevel.Text = isEn ? "Typical / Floor Column (Continuous dowel)" : "Cß╗Öt tß║ºng s├án / ─æiß╗ân h├¼nh (Th├⌐p chß╗¥ nß╗æi tß║ºng)";
            if (_rdBaseFoundation != null) _rdBaseFoundation.Text = isEn ? "Base / Foundation Column (90┬░ Footing L-bend)" : "Cß╗Öt tß║ºng m├│ng (Nß╗æi ch├ón quß╗│ 90┬░ v├áo m├│ng)";
            if (_chkCrankedSplice != null) _chkCrankedSplice.Text = isEn ? "1:6 Cranked offset splice at joint" : "Nhß║Ñn vß║»t nghi├¬ng 1:6 tß║íi vß╗ï tr├¡ nß╗æi";
            if (_chkTopAnchor != null) _chkTopAnchor.Text = isEn ? "90┬░ Inward hook for roof column" : "Neo uß╗æn m├│c 90┬░ ─æß╗ënh m├íi";
            if (_chkStaggeredSplice != null) _chkStaggeredSplice.Text = isEn ? "50% Staggered lap splice (1.3 Ls)" : "Nß╗æi so le 50% (C├ích 1.3 Ls)";

            // Tab 2 Stirrups
            if (_grpStirrupZone != null) _grpStirrupZone.Text = isEn ? "Stirrup Distribution A1 / A2 / A1 (Structural Standard)" : "Ph├ón V├╣ng ─Éai A1 / A2 / A1 (Chuß║⌐n Kß║┐t Cß║Ñu)";
            if (_lblStirrupDia != null) _lblStirrupDia.Text = isEn ? "Stirrup bar diameter:" : "─É╞░ß╗¥ng k├¡nh th├⌐p ─æai:";
            if (_lblStirrupA1 != null) _lblStirrupA1.Text = isEn ? "Dense A1 stirrup spacing (mm):" : "Khoß║úng c├ích ─æai d├áy A1 (mm):";
            if (_lblStirrupA2 != null) _lblStirrupA2.Text = isEn ? "Sparse A2 stirrup spacing (mm):" : "Khoß║úng c├ích ─æai th╞░a A2 (mm):";
            if (_lblZoneA1Len != null) _lblZoneA1Len.Text = isEn ? "Dense A1 zone length (mm):" : "Chiß╗üu d├ái v├╣ng ─æai d├áy A1 (mm):";

            if (_grpInnerStirrup != null) _grpInnerStirrup.Text = isEn ? "Inner Tie & Crosslink Options" : "Cß║Ñu tß║ío ─Éai Phß╗Ñ & M├│c ─Éai";
            if (_chkInnerDiamond != null) _chkInnerDiamond.Text = isEn ? "Create inner diamond stirrup (when ΓëÑ3 bars/side)" : "Tß║ío ─æai lß╗ông / ─æai thoi (khi ΓëÑ3 thanh/cß║ính)";
            if (_chkCrossLinks != null) _chkCrossLinks.Text = isEn ? "Create crosslinks / C-links" : "Tß║ío ─æai m├│c phß╗Ñ / ─Éai C";

            // Tab 3 General Settings
            if (_grpHook != null) _grpHook.Text = isEn ? "REBAR HOOK BENDING SECTION" : "Cß║ñU Tß║áO Uß╗ÉN M├ôC TH├ëP";
            if (_rdHookLengthFixed != null) _rdHookLengthFixed.Text = isEn ? "By fixed length L (mm):" : "Theo chiß╗üu d├ái cß╗æ ─æß╗ïnh L (mm):";
            if (_rdHookLengthDia != null) _rdHookLengthDia.Text = isEn ? "By diameter (xD):" : "Theo ─æ╞░ß╗¥ng k├¡nh thanh (xD):";

            if (_grpBendCut != null) _grpBendCut.Text = isEn ? "REBAR BENDING OR CUTTING CONDITIONS" : "─ÉIß╗ÇU KIß╗åN Uß╗ÉN HOß║╢C Cß║«T TH├ëP";
            if (_lblBendE != null) _lblBendE.Text = isEn ? "Bend rebar if offset e Γëñ (mm):" : "Uß╗æn th├⌐p nß║┐u ─æß╗Ö lß╗çch e Γëñ (mm):";
            if (_lblBendRatio != null) _lblBendRatio.Text = isEn ? "Bend slope ratio Hd/e ΓëÑ:" : "Tß╗╖ lß╗ç ─æß╗Ö dß╗æc uß╗æn Hd/e ΓëÑ:";

            if (_grpTopRoof != null) _grpTopRoof.Text = isEn ? "SET TOP ROOF REBAR" : "Kß║╛T TH├ÜC TH├ëP ─Éß╗êNH M├üI";
            if (_rdTopRoofHook != null) _rdTopRoofHook.Text = isEn ? "Bend hook for top floor rebar" : "Bß║╗ m├│c cho th├⌐p tß║ºng ─æß╗ënh m├íi";
            if (_rdTopRoofContinue != null) _rdTopRoofContinue.Text = isEn ? "Continue straight for next level" : "Chß╗¥ thß║│ng cho tß║ºng tiß║┐p theo";

            if (_grpSplicePos != null) _grpSplicePos.Text = isEn ? "REBAR SPLICE POSITION" : "Vß╗è TR├ì Nß╗ÉI TH├ëP Cß╗ÿT";
            if (_lblSpliceDist != null) _lblSpliceDist.Text = isEn ? "Splice distance from column base L = (mm):" : "Khoß║úng c├ích nß╗æi tß╗½ ch├ón cß╗Öt L = (mm):";

            if (_grpAssignInfo != null) _grpAssignInfo.Text = isEn ? "ASSIGN ADDITIONAL INFORMATION TO REBAR" : "G├üN TH├öNG TIN Bß╗ö SUNG CHO TH├ëP";
            if (_chkAssignElevation != null) _chkAssignElevation.Text = isEn ? "Assign column elevation to rebar" : "G├ín cao ─æß╗Ö cß╗Öt v├áo th├┤ng sß╗æ th├⌐p";
            if (_chkAssignPartition != null) _chkAssignPartition.Text = isEn ? "Automatically assign Partition to rebar" : "Tß╗▒ ─æß╗Öng g├ín Ph├ón v├╣ng (Partition) cho th├⌐p";

            if (_grpSlabBeam != null) _grpSlabBeam.Text = isEn ? "OPTION AT SLAB BEAM POSITION" : "T├ÖY CHß╗îN Tß║áI Vß╗è TR├ì Dß║ªM / S├ÇN";
            if (_lblDefaultHd != null) _lblDefaultHd.Text = isEn ? "Default beam height Hd (mm):" : "Chiß╗üu cao dß║ºm mß║╖c ─æß╗ïnh Hd (mm):";

            // Tab 4 Drawing
            if (_grpViews != null) _grpViews.Text = isEn ? "Drawing & View Options" : "Tß╗▒ ─Éß╗Öng Tß║ío Khung Nh├¼n & Bß║ún Vß║╜";
            if (_chkAutoDrawing != null) _chkAutoDrawing.Text = isEn ? "Automatically generate 2D section drawing & BBS" : "Tß╗▒ ─æß╗Öng tß║ío bß║ún vß║╜ 2D (Mß║╖t cß║»t tiß║┐t diß╗çn & Thß╗æng k├¬ th├⌐p)";
            if (_chkAutoSection3D != null) _chkAutoSection3D.Text = isEn ? "Create 3D Inspection Views (Plan View + 3D View)" : "Tß╗▒ ─æß╗Öng tß║ío Khung nh├¼n xem th├⌐p 3D (Mß║╖t bß║▒ng + 3D)";

            // Right & Bottom Panels
            if (_lblColTitle != null) _lblColTitle.Text = isEn ? "Column List" : "Danh S├ích Cß╗Öt";
            if (_lblTemplate != null) _lblTemplate.Text = isEn ? "Configuration Template:" : "Mß║½u Thiß║┐t Lß║¡p:";
            if (_btnSaveTemplate != null) _btnSaveTemplate.Text = isEn ? "Save..." : "L╞░u mß║½u...";
            if (_btnApplyTemplate != null) _btnApplyTemplate.Text = isEn ? "Apply" : "├üp dß╗Ñng";
            if (_btnDeleteTemplate != null) _btnDeleteTemplate.Text = isEn ? "Delete" : "X├│a mß║½u";
            if (_rdScopeSelected != null) _rdScopeSelected.Text = isEn ? $"Selected columns ({_preSelectedColumns.Count})" : $"Chß╗ë c├íc cß╗Öt ─æ├ú chß╗ìn ({_preSelectedColumns.Count})";
            if (_rdScopeAll != null) _rdScopeAll.Text = isEn ? $"All model columns ({_availableColumns.Count})" : $"Tß║Ñt cß║ú cß╗Öt ({_availableColumns.Count})";
            if (_btnSelectAll != null) _btnSelectAll.Text = isEn ? "Select All" : "Chß╗ìn Tß║Ñt Cß║ú";
            if (_btnDeselectAll != null) _btnDeselectAll.Text = isEn ? "Deselect All" : "Bß╗Å Chß╗ìn";

            if (_btnCreateRebar != null) _btnCreateRebar.Text = isEn ? "Create Rebar" : "Tß║ío Th├⌐p";
            if (_btnClose != null) _btnClose.Text = isEn ? "Close" : "─É├│ng";

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
                LanguageManager.IsEnglish ? "Enter template name:" : "Nhß║¡p t├¬n mß║½u thiß║┐t lß║¡p:",
                LanguageManager.IsEnglish ? "Save Template" : "L╞░u Mß║½u Thiß║┐t Lß║¡p",
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

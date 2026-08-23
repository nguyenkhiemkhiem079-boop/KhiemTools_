using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Models;
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
    /// Form "Circular Column Reinforcement Layout" cao cß║Ñp cho Cß╗Öt Tr├▓n.
    /// Tß╗▒ ─æß╗Öng giß╗» v├á highlight 100% danh s├ích cß╗Öt ─æ├ú chß╗ìn tr╞░ß╗¢c trong Revit viewport.
    /// </summary>
    public class CircularColumnReinforcementForm : Form
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableColumns;
        private readonly List<FamilyInstance> _preSelectedColumns;

        // Controls
        private ListBox _columnListBox;
        private Label _lblSelectedCount;
        private Panel _previewPanel;

        // Controls Th├⌐p Chß╗º
        private NumericUpDown _numMainQty;
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

        // Controls Th├⌐p ─Éai
        private ComboBox _cmbStirrupDia;
        private NumericUpDown _numStirrupSpacing;

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

        // Configuration Templates
        private Label _lblTemplate;
        private ComboBox _cmbTemplate;
        private Button _btnSaveTemplate;
        private Button _btnApplyTemplate;
        private Button _btnDeleteTemplate;

        public CircularColumnReinforcementForm(Document doc, List<FamilyInstance> availableColumns, List<FamilyInstance> preSelectedColumns = null)
        {
            _doc = doc;
            _availableColumns = availableColumns ?? new List<FamilyInstance>();
            _preSelectedColumns = preSelectedColumns ?? new List<FamilyInstance>();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            PopulateColumnList();
            PopulateBarTypeCombos();
            LoadTemplateList();
        }

        private void BuildUi()
        {
            Text = "Γ¡ò KHIM TOOLS ΓÇö Bß╗æ tr├¡ Th├⌐p Cß╗Öt Tr├▓n";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS ΓÇö Circular Column Detailing",
                "Automated Round Column Reinforcement Engine",
                "v2.5 Pro");
            Controls.Add(header);

            // 1. Bottom Action Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.FromArgb(245, 245, 247) };
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

            _lblSafetyStatus = new Label
            {
                AutoSize = true,
                Left = 20,
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

            var rightPanel = new Panel { Dock = DockStyle.Right, Width = 230, Padding = new Padding(10), BackColor = Color.FromArgb(250, 250, 252) };
            var lblColTitle = new Label { Text = "Danh S├ích Cß╗Öt", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            var scopePanel = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(240, 243, 248), Padding = new Padding(4) };
            _rdScopeSelected = new RadioButton { Text = $"Chß╗ë c├íc cß╗Öt ─æ├ú chß╗ìn ({_preSelectedColumns.Count})", Checked = _preSelectedColumns.Any(), AutoSize = true, Top = 4, Left = 4, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.DarkGreen };
            _rdScopeAll = new RadioButton { Text = $"Tß║Ñt cß║ú cß╗Öt ({_availableColumns.Count})", Checked = !_preSelectedColumns.Any(), AutoSize = true, Top = 26, Left = 4, Font = new Font("Segoe UI", 8.5F) };

            _rdScopeSelected.CheckedChanged += (s, e) => PopulateColumnList();
            _rdScopeAll.CheckedChanged += (s, e) => PopulateColumnList();

            scopePanel.Controls.Add(_rdScopeSelected);
            scopePanel.Controls.Add(_rdScopeAll);

            _lblSelectedCount = new Label { Text = "─É├ú chß╗ìn: 0 cß╗Öt", Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };

            var selectButtonsPanel = new Panel { Dock = DockStyle.Bottom, Height = 32 };
            var btnSelectAll = new Button { Text = "Select All", Width = 95, Height = 26, Top = 3, Left = 0, FlatStyle = FlatStyle.System };
            var btnDeselectAll = new Button { Text = "Clear", Width = 70, Height = 26, Top = 3, Left = 102, FlatStyle = FlatStyle.System };

            btnSelectAll.Click += (s, e) => SetAllColumnsSelected(true);
            btnDeselectAll.Click += (s, e) => SetAllColumnsSelected(false);

            selectButtonsPanel.Controls.Add(btnSelectAll);
            selectButtonsPanel.Controls.Add(btnDeselectAll);

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
            rightPanel.Controls.Add(lblColTitle);
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

            var tabControl = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };

            var tabMain = new TabPage { Text = "Th├⌐p Chß╗º & Cover", Padding = new Padding(12), BackColor = Color.White };
            var pnlMainLeft = new Panel { Dock = DockStyle.Left, Width = 350 };

            var grpMainSection = new GroupBox { Text = "Bß╗æ tr├¡ Th├⌐p Chß╗º Tiß║┐t Diß╗çn Cß╗Öt Tr├▓n", Dock = DockStyle.Top, Height = 110, Padding = new Padding(10) };
            var layoutMainSec = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutMainSec.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _numMainQty = new NumericUpDown { Minimum = 4, Maximum = 40, Value = 8, Increment = 2, Width = 90 };
            _numMainQty.ValueChanged += (s, e) => _previewPanel?.Invalidate();

            AddRowToLayout(layoutMainSec, "Sß╗æ l╞░ß╗úng thanh chß╗º:", _numMainQty);
            AddRowToLayout(layoutMainSec, "─É╞░ß╗¥ng k├¡nh th├⌐p chß╗º:", _cmbMainDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 });
            grpMainSection.Controls.Add(layoutMainSec);

            var grpCover = new GroupBox { Text = "Lß╗¢p B├¬ T├┤ng Bß║úo Vß╗ç (Concrete Cover)", Dock = DockStyle.Top, Height = 100, Padding = new Padding(10) };
            var layoutCover = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutCover.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _chkCustomCover = new CheckBox { Text = "Nhß║¡p tay Cover (bß╗Å chß╗ìn = tß╗▒ ─æß╗Öng tß╗½ Revit)", Checked = false, AutoSize = true, Margin = new Padding(3, 4, 3, 4) };
            _numCustomCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 90, Enabled = false };
            _chkCustomCover.CheckedChanged += (s, e) => _numCustomCover.Enabled = _chkCustomCover.Checked;

            var btnProjectCover = new Button { Text = "Cover Dß╗▒ ├ün", Width = 105, Height = 25, FlatStyle = FlatStyle.System };
            btnProjectCover.Click += (s, e) => new ProjectCoverSetupForm(_doc).ShowDialog();

            AddRowToLayout(layoutCover, "Cover t├╣y chß╗ënh (mm):", _numCustomCover);
            layoutCover.Controls.Add(_chkCustomCover);
            layoutCover.Controls.Add(btnProjectCover);
            grpCover.Controls.Add(layoutCover);

            var grpMainAnchor = new GroupBox { Text = "Cß║Ñu tß║ío Neo & Nß╗æi Th├⌐p", Dock = DockStyle.Top, Height = 175, Padding = new Padding(8) };
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
            grpMainAnchor.Controls.Add(pnlAnchor);

            pnlMainLeft.Controls.Add(grpMainAnchor);
            pnlMainLeft.Controls.Add(grpCover);
            pnlMainLeft.Controls.Add(grpMainSection);
            tabMain.Controls.Add(pnlMainLeft);

            _previewPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(252, 252, 254) };
            _previewPanel.Paint += PreviewPanel_Paint;
            tabMain.Controls.Add(_previewPanel);
            _previewPanel.BringToFront();

            tabControl.TabPages.Add(tabMain);

            var tabStirrup = new TabPage { Text = "Th├⌐p ─Éai Tr├▓n (Stirrups)", Padding = new Padding(12), BackColor = Color.White };
            var grpStirrup = new GroupBox { Text = "Th├┤ng sß╗æ ─Éai V├▓ng / ─Éai Xoß║»n Cß╗Öt Tr├▓n", Dock = DockStyle.Top, Height = 130, Padding = new Padding(10) };
            var layoutStirrup = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layoutStirrup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layoutStirrup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            AddRowToLayout(layoutStirrup, "─É╞░ß╗¥ng k├¡nh th├⌐p ─æai:", _cmbStirrupDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 });
            AddRowToLayout(layoutStirrup, "Khoß║úng c├ích ─æai (mm):", _numStirrupSpacing = new NumericUpDown { Minimum = 50, Maximum = 400, Value = 150, Increment = 10, Width = 90 });
            grpStirrup.Controls.Add(layoutStirrup);
            tabStirrup.Controls.Add(grpStirrup);
            tabControl.TabPages.Add(tabStirrup);

            var tabGenSettings = new TabPage { Text = "General Settings", Padding = new Padding(10), BackColor = Color.White };
            var layoutGenSettings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            layoutGenSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layoutGenSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var grpHook = new GroupBox { Text = "REBAR HOOK BENDING SECTION", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlHook = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _rdHookLengthFixed = new RadioButton { Text = "By fixed length L (mm):", Checked = true, AutoSize = true };
            _numHookFixedMm = new NumericUpDown { Minimum = 50, Maximum = 500, Value = 150, Width = 70 };
            _rdHookLengthDia = new RadioButton { Text = "By diameter (xD):", AutoSize = true };
            _numHookDiaxD = new NumericUpDown { Minimum = 5, Maximum = 30, Value = 10, Width = 70 };
            pnlHook.Controls.Add(_rdHookLengthFixed); pnlHook.Controls.Add(_numHookFixedMm);
            pnlHook.Controls.Add(_rdHookLengthDia); pnlHook.Controls.Add(_numHookDiaxD);
            grpHook.Controls.Add(pnlHook);

            var grpBendCut = new GroupBox { Text = "REBAR BENDING OR CUTTING CONDITIONS", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlBendCut = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            pnlBendCut.Controls.Add(new Label { Text = "Bend rebar if e Γëñ (mm):", AutoSize = true });
            _numBendConditionE = new NumericUpDown { Minimum = 10, Maximum = 300, Value = 75, Width = 70 };
            pnlBendCut.Controls.Add(_numBendConditionE);
            pnlBendCut.Controls.Add(new Label { Text = "Bend by ratio Hd/e ΓëÑ:", AutoSize = true });
            _numBendRatioHd = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 6, Width = 70 };
            pnlBendCut.Controls.Add(_numBendRatioHd);
            grpBendCut.Controls.Add(pnlBendCut);

            var grpTopRoof = new GroupBox { Text = "SET TOP ROOF REBAR", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlTopRoof = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _rdTopRoofHook = new RadioButton { Text = "Bend hook for top floor rebar", Checked = true, AutoSize = true };
            _rdTopRoofContinue = new RadioButton { Text = "Continue waiting for top floor rebar", AutoSize = true };
            pnlTopRoof.Controls.Add(_rdTopRoofHook); pnlTopRoof.Controls.Add(_rdTopRoofContinue);
            grpTopRoof.Controls.Add(pnlTopRoof);

            var grpSplicePos = new GroupBox { Text = "REBAR SPLICE POSITION", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlSplicePos = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            pnlSplicePos.Controls.Add(new Label { Text = "Splice distance from column base L = (mm):", AutoSize = true });
            _numSpliceDistBase = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = 50, Width = 70 };
            pnlSplicePos.Controls.Add(_numSpliceDistBase);
            _rdSpliceTwoPos = new RadioButton { Text = "Splice rebar at two positions (Nß╗æi so le 50%)", Checked = true, AutoSize = true };
            pnlSplicePos.Controls.Add(_rdSpliceTwoPos);
            grpSplicePos.Controls.Add(pnlSplicePos);

            var grpAssignInfo = new GroupBox { Text = "ASSIGN ADDITIONAL INFORMATION TO REBAR", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlAssignInfo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkAssignElevation = new CheckBox { Text = "Assign column elevation to rebar", Checked = true, AutoSize = true };
            _chkAssignPartition = new CheckBox { Text = "Automatically assign Partition to rebar", Checked = true, AutoSize = true };
            pnlAssignInfo.Controls.Add(_chkAssignElevation); pnlAssignInfo.Controls.Add(_chkAssignPartition);
            grpAssignInfo.Controls.Add(pnlAssignInfo);

            var grpSlabBeam = new GroupBox { Text = "OPTION AT SLAB BEAM POSITION", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var pnlSlabBeam = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            pnlSlabBeam.Controls.Add(new Label { Text = "Default height Hd (mm):", AutoSize = true });
            _numDefaultBeamHd = new NumericUpDown { Minimum = 100, Maximum = 2000, Value = 500, Increment = 50, Width = 80 };
            pnlSlabBeam.Controls.Add(_numDefaultBeamHd);
            grpSlabBeam.Controls.Add(pnlSlabBeam);

            layoutGenSettings.Controls.Add(grpHook, 0, 0); layoutGenSettings.Controls.Add(grpBendCut, 1, 0);
            layoutGenSettings.Controls.Add(grpTopRoof, 0, 1); layoutGenSettings.Controls.Add(grpSplicePos, 1, 1);
            layoutGenSettings.Controls.Add(grpAssignInfo, 0, 2); layoutGenSettings.Controls.Add(grpSlabBeam, 1, 2);

            tabGenSettings.Controls.Add(layoutGenSettings);
            tabControl.TabPages.Add(tabGenSettings);

            var tabViews = new TabPage { Text = "Bß║ún Vß║╜ & View 3D", Padding = new Padding(12), BackColor = Color.White };
            var grpViews = new GroupBox { Text = "Tß╗▒ ─æß╗Öng Tß║ío View & Triß╗ân khai Bß║ún vß║╜", Dock = DockStyle.Top, Height = 130, Padding = new Padding(10) };
            var pnlViews = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkAutoDrawing = new CheckBox { Text = "Tß╗▒ ─æß╗Öng tß║ío bß║ún vß║╜ 2D (Mß║╖t cß║»t tiß║┐t diß╗çn & Thß╗æng k├¬ th├⌐p)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
            _chkAutoSection3D = new CheckBox { Text = "Tß╗▒ ─æß╗Öng tß║ío View xem th├⌐p 3D (Plan View + 3D View)", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
            pnlViews.Controls.Add(_chkAutoDrawing);
            pnlViews.Controls.Add(_chkAutoSection3D);
            grpViews.Controls.Add(pnlViews);

            tabViews.Controls.Add(grpViews);
            tabControl.TabPages.Add(tabViews);

            Controls.Add(tabControl);
            tabControl.BringToFront();
        }

        private void AddRowToLayout(TableLayoutPanel table, string labelText, Control inputControl)
        {
            var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 5, 3, 3) };
            table.Controls.Add(lbl);
            table.Controls.Add(inputControl);
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

        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int cx = _previewPanel.Width / 2;
            int cy = _previewPanel.Height / 2;
            int r = Math.Min(_previewPanel.Width, _previewPanel.Height) / 2 - 20;
            if (r <= 0) return;

            using var penColumn = new Pen(Color.Gray, 2);
            g.DrawEllipse(penColumn, cx - r, cy - r, 2 * r, 2 * r);

            int count = (int)_numMainQty.Value;
            if (count > 0)
            {
                using var brushBar = new SolidBrush(Color.DarkBlue);
                int barR = r - 15;
                for (int i = 0; i < count; i++)
                {
                    double angle = 2 * Math.PI * i / count;
                    int bx = cx + (int)(barR * Math.Cos(angle));
                    int by = cy + (int)(barR * Math.Sin(angle));
                    g.FillEllipse(brushBar, bx - 4, by - 4, 8, 8);
                }
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

            using var tx = new Transaction(_doc, "Create Circular Column Rebar");
            tx.Start();
            FailureHandlingOptions failOptions = tx.GetFailureHandlingOptions();
            failOptions.SetFailuresPreprocessor(new KhimTools.SlabJoin.Utilities.SwallowWarningsPreprocessor());
            tx.SetFailureHandlingOptions(failOptions);
            try
            {
                // Nß║íp sß║╡n to├án bß╗Ö RebarShape chuß║⌐n (JP_T00, JP_T11, JP_T75...) v├áo Document
                RebarShapeLibrary.PreloadCommonShapes(_doc);

                var generator = new CircularColumnRebarGenerator(_doc);
                var drawingGen = new ColumnRebarDrawingGenerator(_doc);
                var sectionGen = new ColumnRebarSectionViewGenerator(_doc);
                var view3DGen = new ColumnRebar3DViewGenerator(_doc);

                List<FamilyInstance> rawColumns = selectedItems.Select(i => i.Column).ToList();
                List<List<FamilyInstance>> axisGroups = RebarLapSpliceHelper.GroupColumnsByAxis(rawColumns, _doc);

                var report = new RebarGenerationReport();
                var allCreatedRebars = new List<Rebar>();

                foreach (var group in axisGroups)
                {
                    var inputs = group.Select(col => new CircularColumnRebarInput
                    {
                        Column = col,
                        MainBarType = mainType,
                        StirrupBarType = stirrupType,
                        MainBarQty = (int)_numMainQty.Value,
                        StirrupSpacing = UnitUtils.ConvertToInternalUnits((double)_numStirrupSpacing.Value, UnitTypeId.Millimeters),
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
                                var profile = CircularColumnGeometryHelper.GetCircularProfile(item);
                                double coverFeet = customCoverFeet ?? RebarCoverHelper.GetColumnCover(item, RebarFace.Exterior);

                                drawingGen.CreateOrUpdate(new ColumnRebarDrawingInput
                                {
                                    Shape = ColumnShapeType.Circular,
                                    ColumnMark = item.LookupParameter("Mark")?.AsString() ?? item.Id.ToLongValue().ToString(),
                                    ColumnDiameterMm = UnitUtils.ConvertFromInternalUnits(profile.Diameter, UnitTypeId.Millimeters),
                                    MainBarQty = (int)_numMainQty.Value,
                                    MainBarLabel = _cmbMainDia.Text,
                                    StirrupLabel = _cmbStirrupDia.Text,
                                    StirrupSpacingMm = (double)_numStirrupSpacing.Value,
                                    CoverMm = UnitUtils.ConvertFromInternalUnits(coverFeet, UnitTypeId.Millimeters)
                                });
                            }
                            catch (Exception exDraw)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CircularColumnDrawingGenerator] Error: {exDraw.Message}");
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
                                System.Diagnostics.Debug.WriteLine($"[CircularColumnViewGenerator] Error: {exView.Message}");
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
                        var profile = CircularColumnGeometryHelper.GetCircularProfile(sampleCol);
                        double dMm = UnitUtils.ConvertFromInternalUnits(profile.Diameter, UnitTypeId.Millimeters);
                        double acMm2 = Math.PI * Math.Pow(dMm / 2.0, 2);

                        int totalBars = (int)_numMainQty.Value;
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
                    KhimDialogHelper.ShowRebarGenerationReport(report, "Cß╗Öt Tr├▓n (Circular Column)", selectedItems.Count);
                }
                else
                {
                    KhimDialogHelper.ShowColumnRebarSuccess(selectedItems.Count, axisGroups.Count, _chkAutoDrawing.Checked, _chkAutoSection3D.Checked);
                }
            }
            catch (Exception ex)
            {
                tx.RollBack();
                string errTitle = LanguageManager.IsEnglish ? "Error Creating Rebar" : "Lß╗ùi Tß║ío Th├⌐p Cß╗Öt Tr├▓n";
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
                BarsAlongB = (int)_numMainQty.Value,
                BarsAlongH = 0,
                StirrupSpacingA1 = (double)_numStirrupSpacing.Value,
                StirrupSpacingA2 = (double)_numStirrupSpacing.Value,
                ZoneA1Length = 0,
                IsCustomCover = _chkCustomCover.Checked,
                CustomCover = (double)_numCustomCover.Value,
                LapLengthMultiplier = (double)_numLapMultiplier.Value,
                EnableCrankedSplice = _chkCrankedSplice.Checked,
                HasTopAnchor = _chkTopAnchor.Checked,
                IsFoundationColumn = _rdBaseFoundation.Checked,
                HasDowel = !_rdBaseFoundation.Checked,
                StaggeredSplice = _chkStaggeredSplice.Checked,
                HasInnerDiamondStirrup = false,
                HasCrossLinks = false
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
            _numMainQty.Value = Math.Max(_numMainQty.Minimum, Math.Min(_numMainQty.Maximum, settings.BarsAlongB));
            _numStirrupSpacing.Value = (decimal)settings.StirrupSpacingA1;
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

            _previewPanel?.Invalidate();
        }

        private void DeleteTemplate()
        {
            string name = _cmbTemplate.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            RebarTemplateManager.DeleteColumnTemplate(name);
            LoadTemplateList();
        }

        private DesignCode GetSelectedDesignStandard() => DesignCode.Eurocode2;
        private ConcreteGrade GetSelectedConcreteGrade() => ConcreteGrade.C30_37;
        private SteelGrade GetSelectedSteelGrade() => SteelGrade.B500;

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

        private class ColumnListItem
        {
            public FamilyInstance Column { get; }
            private readonly string _label;
            public ColumnListItem(FamilyInstance col, string label) { Column = col; _label = label; }
            public override string ToString() => _label;
        }
    }
}

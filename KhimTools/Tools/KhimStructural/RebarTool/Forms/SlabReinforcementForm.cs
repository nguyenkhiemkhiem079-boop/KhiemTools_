using KhimTools.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
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
        private Button _btnAutoMerge;
        private Button _btnMergeSelected;
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
        private Button _btnClose;

        public SlabReinforcementForm(Document doc, List<Floor> availableFloors, List<Floor> preSelectedFloors = null)
        {
            _doc = doc;
            _availableFloors = availableFloors ?? new List<Floor>();
            _preSelectedFloors = preSelectedFloors ?? new List<Floor>();
            _panelManager = new SlabPanelManager();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            PopulateBarCombos();

            // Khởi tạo danh sách panel từ sàn được chọn hoặc toàn bộ sàn
            var initialFloors = _preSelectedFloors.Any() ? _preSelectedFloors : _availableFloors;
            _panelManager.InitializeFromFloors(_doc, initialFloors);
            RefreshGridPanels();
            LoadTemplateList();
        }

        private void BuildUi()
        {
            Text = "KHIM TOOLS — Bố trí Thép Sàn theo Panel (Slab Rebar v3.0)";
            Width = 1080;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 0. Header Banner
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Multi-Panel Slab Detailing Engine",
                "Panel System, Bottom/Top Mesh, Support Hats, Distribution Bars & Spacers",
                "v3.0 Pro");
            Controls.Add(header);

            // 1. Bottom Control Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Color.FromArgb(245, 245, 247) };
            var lblLang = new Label { Text = "Language:", AutoSize = true, Left = 15, Top = 20, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, Left = 95, Top = 16 };
            _cmbLanguage.Items.Add("Tiếng Việt");
            _cmbLanguage.Items.Add("English");
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;

            _btnAssignData = new Button { Text = "💾 Gán Thông Số", Width = 135, Height = 36, Top = 11, Left = 620 };
            KhimUiStyle.ApplySecondaryButton(_btnAssignData);
            _btnAssignData.Click += BtnAssignData_Click;

            _btnCreateRebar = new Button { Text = "Tạo Thép Sàn", Width = 140, Height = 36, Top = 11, Left = 765 };
            KhimUiStyle.ApplyPrimaryButton(_btnCreateRebar, KhimUiStyle.CreateButtonBg);
            _btnCreateRebar.Click += BtnCreateRebar_Click;

            _btnClose = new Button { Text = "Đóng", Width = 90, Height = 36, Top = 11, Left = 915 };
            KhimUiStyle.ApplySecondaryButton(_btnClose);
            _btnClose.Click += (s, e) => Close();

            bottomPanel.Controls.Add(lblLang);
            bottomPanel.Controls.Add(_cmbLanguage);
            bottomPanel.Controls.Add(_btnAssignData);
            bottomPanel.Controls.Add(_btnCreateRebar);
            bottomPanel.Controls.Add(_btnClose);
            Controls.Add(bottomPanel);

            // 2. Main Content Split (Left: Tabs 580px, Right: Panel DataGridView)
            var pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 65, 15, 5) };

            // ── LEFT: TabControl (Thông số cốt thép)
            var tabControl = new TabControl { Left = 15, Top = 70, Width = 560, Height = 525, Font = new Font("Segoe UI", 9F) };

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

            pnlMain.Controls.Add(tabControl);

            // ── RIGHT: Panel List DataGridView (460px)
            var pnlRight = new Panel { Left = 585, Top = 70, Width = 465, Height = 525 };
            BuildPanelGridSection(pnlRight);
            pnlMain.Controls.Add(pnlRight);

            Controls.Add(pnlMain);
        }

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
            page.Controls.Add(grp);
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
            page.Controls.Add(grp);
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
            _btnSaveTemplate = new Button { Text = "💾 Lưu Mẫu", Left = 290, Top = 33, Width = 100, Height = 32 };
            KhimUiStyle.ApplySecondaryButton(_btnSaveTemplate);
            _btnLoadTemplate = new Button { Text = "📂 Nạp Mẫu", Left = 400, Top = 33, Width = 100, Height = 32 };
            KhimUiStyle.ApplySecondaryButton(_btnLoadTemplate);

            grpTpl.Controls.Add(_cmbTemplates);
            grpTpl.Controls.Add(_btnSaveTemplate);
            grpTpl.Controls.Add(_btnLoadTemplate);

            page.Controls.Add(grpCode);
            page.Controls.Add(grpTpl);
        }

        private void BuildPanelGridSection(Panel pnl)
        {
            var lblTitle = new Label { Text = "DANH SÁCH PANEL SÀN", Top = 5, Left = 5, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59) };

            var pnlToolBar = new Panel { Top = 30, Left = 0, Width = 465, Height = 32 };
            _btnAutoMerge = new Button { Text = "Auto Merge", Left = 0, Top = 0, Width = 110, Height = 30 };
            KhimUiStyle.ApplySecondaryButton(_btnAutoMerge);
            _btnAutoMerge.Click += (s, e) => { _panelManager.AutoMergeAdjacent(); RefreshGridPanels(); };

            _btnMergeSelected = new Button { Text = "Merge", Left = 115, Top = 0, Width = 90, Height = 30 };
            KhimUiStyle.ApplySecondaryButton(_btnMergeSelected);
            _btnMergeSelected.Click += BtnMergeSelected_Click;

            _btnDeletePanel = new Button { Text = "Xóa", Left = 210, Top = 0, Width = 80, Height = 30 };
            KhimUiStyle.ApplySecondaryButton(_btnDeletePanel);
            _btnDeletePanel.Click += BtnDeletePanel_Click;

            _btnPickEdge = new Button { Text = "🔍 Pick Edge", Left = 295, Top = 0, Width = 100, Height = 30 };
            KhimUiStyle.ApplySecondaryButton(_btnPickEdge);
            _btnPickEdge.Click += (s, e) => OpenEdgePickerForSelectedPanel();

            pnlToolBar.Controls.Add(_btnAutoMerge);
            pnlToolBar.Controls.Add(_btnMergeSelected);
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
        }

        private void RefreshGridPanels()
        {
            _gridPanels.Rows.Clear();
            int selectedCount = 0;

            foreach (var p in _panelManager.Panels)
            {
                int rowIdx = _gridPanels.Rows.Add();
                var row = _gridPanels.Rows[rowIdx];
                row.Cells["colCheck"].Value = p.IsSelected;
                row.Cells[1].Value = p.PanelId;
                row.Cells[2].Value = p.LevelName;
                row.Cells[3].Value = $"{p.WidthMm:N0} x {p.LengthMm:N0} mm";
                row.Cells[4].Value = $"{p.ThicknessMm:N0}";

                if (p.IsSelected) selectedCount++;
            }

            _lblPanelCount.Text = $"Đã chọn: {selectedCount} / {_panelManager.Panels.Count} panels";
        }

        private void SetAllSelection(bool select)
        {
            foreach (var p in _panelManager.Panels) p.IsSelected = select;
            RefreshGridPanels();
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
                }
            }
            else
            {
                KhimDialogHelper.ShowWarning("Vui lòng chọn 1 Panel trong bảng để cấu hình từng cạnh.");
            }
        }

        private void BtnAssignData_Click(object sender, EventArgs e)
        {
            int assigned = 0;
            for (int i = 0; i < _gridPanels.Rows.Count; i++)
            {
                bool isChecked = Convert.ToBoolean(_gridPanels.Rows[i].Cells["colCheck"].Value);
                if (isChecked && i < _panelManager.Panels.Count)
                {
                    var panel = _panelManager.Panels[i];
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

                    assigned++;
                }
            }

            KhimDialogHelper.ShowInfo($"Đã gán thành công thông số cấu hình cốt thép cho {assigned} panel.");
        }

        private void BtnCreateRebar_Click(object sender, EventArgs e)
        {
            // Đồng bộ trạng thái chọn từ DataGridView trước
            for (int i = 0; i < _gridPanels.Rows.Count; i++)
            {
                if (i < _panelManager.Panels.Count)
                {
                    _panelManager.Panels[i].IsSelected = Convert.ToBoolean(_gridPanels.Rows[i].Cells["colCheck"].Value);
                }
            }

            var selectedPanels = _panelManager.Panels.Where(p => p.IsSelected).ToList();
            if (!selectedPanels.Any())
            {
                KhimDialogHelper.ShowWarning("Vui lòng chọn ít nhất 1 Panel sàn để tạo thép.");
                return;
            }

            // Tự động gán settings hiện tại trước khi tạo
            BtnAssignData_Click(sender, e);

            var report = new RebarGenerationReport();
            var generator = new SlabRebarGenerator(_doc);

            using (var trans = new Transaction(_doc, "KHIM TOOLS — Tạo Thép Sàn Theo Panel"))
            {
                trans.Start();
                FailureHandlingOptions failOptions = trans.GetFailureHandlingOptions();
                failOptions.SetFailuresPreprocessor(new KhimTools.SlabJoin.Utilities.SwallowWarningsPreprocessor());
                trans.SetFailureHandlingOptions(failOptions);
                try
                {
                    // Lặp qua từng panel và sinh thép theo cấu hình riêng của panel đó
                    foreach (var panel in selectedPanels)
                    {
                        generator.GeneratePanel(panel, report);
                    }

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    KhimDialogHelper.ShowError($"Lỗi khi tạo thép sàn: {ex.Message}");
                    return;
                }
            }

            KhimDialogHelper.ShowRebarGenerationReport(report, "Tạo Thép Sàn (Slab Rebar Panel System)", selectedPanels.Count);
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
    }
}

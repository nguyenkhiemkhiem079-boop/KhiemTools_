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
    public class FoundationReinforcementForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableFoundations;
        private readonly FoundationRebarSettings _settings;

        // Form Controls
        private ComboBox _cmbLanguage;
        private Button _btnCreateRebar;
        private Button _btnPreviewRebar;
        private Button _btnSolve3D;
        private Button _btnClose;
        private ListBox _foundationListBox;
        private Panel _previewPanel;
        private Label _lblPreviewState;
        private TabControl _workflowTabs;
        private readonly List<Button> _roleNavigationButtons = new List<Button>();
        private readonly Dictionary<int, FoundationPreviewOutline> _previewOutlines = new Dictionary<int, FoundationPreviewOutline>();
        private readonly Dictionary<string, string> _previewFingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
        private RebarPreviewSnapshot _lastPreview;
        private readonly PreviewLifecycleSession<RebarPreviewSnapshot> _previewLifecycle = new PreviewLifecycleSession<RebarPreviewSnapshot>();

        // Tab 1 (Bottom Mesh)
        private ComboBox _cmbBotXDia;
        private NumericUpDown _numBotXSpacing;
        private CheckBox _chkBotXHook;
        private ComboBox _cmbBotYDia;
        private NumericUpDown _numBotYSpacing;
        private CheckBox _chkBotYHook;

        // Tab 2 (Top Mesh)
        private CheckBox _chkEnableTopMesh;
        private ComboBox _cmbTopXDia;
        private NumericUpDown _numTopXSpacing;
        private CheckBox _chkTopXHook;
        private ComboBox _cmbTopYDia;
        private NumericUpDown _numTopYSpacing;
        private CheckBox _chkTopYHook;

        // Tab 3 (Perimeter U-bars)
        private CheckBox _chkEnablePerimeterUStirrups;
        private ComboBox _cmbPerimeterStirrupDia;
        private NumericUpDown _numPerimeterStirrupSpacing;

        // Tab 4 (Column Dowels & Stirrups)
        private CheckBox _chkEnableDowels;
        private ComboBox _cmbDowelDia;
        private NumericUpDown _numDowelQtyX;
        private NumericUpDown _numDowelQtyY;
        private NumericUpDown _numDowelFootLeg;
        private NumericUpDown _numDowelExtension;
        private CheckBox _chkDowelInward;
        private CheckBox _chkStaggeredDowels;
        private CheckBox _chkEnableDowelStirrups;
        private NumericUpDown _numDowelStirrupQty;

        // Tab 5 (Design Code & Templates)
        private ComboBox _cmbDesignCode;
        private ComboBox _cmbConcreteGrade;
        private ComboBox _cmbSteelGrade;
        private NumericUpDown _numCoverMm;
        private ComboBox _cmbTemplates;
        private Button _btnSaveTemplate;
        private Button _btnLoadTemplate;
        private RebarFormGuard _formGuard;

        private sealed class FoundationPreviewOutline
        {
            public string HostUniqueId { get; set; }
            public string Label { get; set; }
            public double MinX { get; set; }
            public double MinY { get; set; }
            public double MaxX { get; set; }
            public double MaxY { get; set; }
            public double MinZ { get; set; }
            public double MaxZ { get; set; }
        }

        public FoundationReinforcementForm(Document doc, List<FamilyInstance> availableFoundations)
            : this(doc, availableFoundations, true)
        {
        }

        internal static FoundationReinforcementForm CreateLayoutPreview()
        {
            return new FoundationReinforcementForm(null, null, false);
        }

        private FoundationReinforcementForm(Document doc, List<FamilyInstance> availableFoundations, bool loadDocument)
        {
            _doc = doc;
            _availableFoundations = availableFoundations ?? new List<FamilyInstance>();
            _settings = new FoundationRebarSettings();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            ApplyLanguage();
            RebarLayout.EnableFullTypeNames(this);
            if (loadDocument) PopulateBarCombos();
            PopulateFoundationList();
            if (loadDocument) LoadTemplateList();
            _formGuard = RebarFormGuard.Attach(this, _btnCreateRebar,
                RebarFormGuard.RequireSelection(_foundationListBox, "Chọn ít nhất một móng."),
                RebarFormGuard.RequireCombo(_cmbBotXDia, "Chọn thép lớp dưới phương X."),
                RebarFormGuard.RequireCombo(_cmbBotYDia, "Chọn thép lớp dưới phương Y."),
                new RebarValidationRule(_cmbTopXDia,
                    () => !_chkEnableTopMesh.Checked || (_cmbTopXDia.SelectedIndex >= 0 && _cmbTopYDia.SelectedIndex >= 0),
                    "Chọn đủ loại thép lớp trên X/Y."),
                new RebarValidationRule(_cmbDowelDia,
                    () => !_chkEnableDowels.Checked || _cmbDowelDia.SelectedIndex >= 0,
                    "Chọn loại thép chờ cột."),
                new RebarValidationRule(_cmbPerimeterStirrupDia,
                    () => !_chkEnablePerimeterUStirrups.Checked || _cmbPerimeterStirrupDia.SelectedIndex >= 0,
                    "Chọn đường kính thép chữ U mép móng."),
                new RebarValidationRule(_btnCreateRebar,
                    () => _previewLifecycle.State == PreviewLifecycleState.Valid && _lastPreview != null,
                    "Giải và kiểm tra Preview cho cấu hình hiện tại trước khi tạo thép."));
            AttachPreviewInvalidationHandlers(this);
            UpdatePreviewStateUi();
        }

        private void BuildUi()
        {
            SetFormTitle("Rebar - Móng", "Lưới thép, thép chờ cột và cấu tạo biên");
            Width = 1040;
            Height = 740;
            MinimumSize = new Size(940, 680);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;

            // Bottom Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White };
            var workflow = new Label { Text = "1  Chọn móng     2  Cấu hình lưới     3  Kiểm tra thép chờ     4  Tạo thép", AutoSize = true, Left = 16, Top = 24, ForeColor = KhimUiStyle.TextSecondary, Font = new Font("Segoe UI Semibold", 9F) };
            var lblLang = new Label { Text = "Ngôn ngữ", AutoSize = true, Left = 470, Top = 24, ForeColor = KhimUiStyle.TextSecondary };
            _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 108, Left = 540, Top = 18 };
            _cmbLanguage.Items.Add("Tiếng Việt");
            _cmbLanguage.Items.Add("English");
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;
            _cmbLanguage.SelectedIndexChanged += (s, e) =>
            {
                LanguageManager.CurrentLanguage = _cmbLanguage.SelectedIndex == 1
                    ? AppLanguage.English : AppLanguage.Vietnamese;
                ApplyLanguage();
            };

            _btnCreateRebar = new Button { Text = "Tạo thép móng", Width = 148, Height = 38, Top = 13 };
            KhimUiStyle.ApplyPrimaryButton(_btnCreateRebar, KhimUiStyle.CreateButtonBg);

            _btnPreviewRebar = new Button { Text = LanguageManager.IsEnglish ? "Solve / Refresh" : "Giải / Cập nhật", Width = 130, Height = 38, Top = 13, AccessibleName = "Solve foundation reinforcement preview" };
            KhimUiStyle.ApplySecondaryButton(_btnPreviewRebar);
            _btnPreviewRebar.Click += BtnPreviewRebar_Click;
            _btnSolve3D = new Button { Text = LanguageManager.IsEnglish ? "View 3D" : "Xem 3D", Width = 90, Height = 38, Top = 13, Enabled = false, AccessibleName = "Open foundation solver preview" };
            KhimUiStyle.ApplySecondaryButton(_btnSolve3D);
            _btnSolve3D.Click += (s, e) =>
            {
                if (_lastPreview == null || _previewLifecycle.State != PreviewLifecycleState.Valid) return;
                using (var preview = new RebarSolverPreviewForm(_lastPreview)) preview.ShowDialog(this);
            };

            _btnClose = new Button { Text = "Đóng", Width = 88, Height = 38, Top = 13 };
            KhimUiStyle.ApplySecondaryButton(_btnClose);

            _btnCreateRebar.Click += BtnCreateRebar_Click;
            _btnClose.Click += (s, e) => Close();

            bottomPanel.Controls.Add(workflow);
            bottomPanel.Controls.Add(lblLang);
            bottomPanel.Controls.Add(_cmbLanguage);
            bottomPanel.Controls.Add(_btnCreateRebar);
            bottomPanel.Controls.Add(_btnClose);

            bottomPanel.Resize += (s, e) =>
            {
                _btnClose.Left = bottomPanel.Width - _btnClose.Width - 15;
                _btnCreateRebar.Left = _btnClose.Left - _btnCreateRebar.Width - 10;
            };
            var footer = RebarLayout.Footer(_cmbLanguage, _btnPreviewRebar, _btnSolve3D, _btnCreateRebar, _btnClose);
            bottomPanel.Dispose();
            Controls.Add(footer);

            // Right Panel (Selection List & Live Preview)
            var rightPanel = new Panel { Dock = DockStyle.Right, Width = 290, Padding = new Padding(14), BackColor = Color.White };
            var lblFdnTitle = new Label { Text = "CẤU KIỆN ÁP DỤNG", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI Semibold", 9F), ForeColor = KhimUiStyle.TextSecondary };

            _foundationListBox = new ListBox { Dock = DockStyle.Top, Height = 200, SelectionMode = SelectionMode.MultiExtended };

            _lblPreviewState = new Label { Text = LanguageManager.IsEnglish ? "PREVIEW · Not solved" : "XEM TRƯỚC · Chưa giải", Dock = DockStyle.Top, Height = 32, Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = KhimUiStyle.TextSecondary, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "Foundation preview state" };

            _previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            _previewPanel.Paint += PreviewPanel_Paint;

            rightPanel.Controls.Add(_previewPanel);
            rightPanel.Controls.Add(_lblPreviewState);
            rightPanel.Controls.Add(_foundationListBox);
            rightPanel.Controls.Add(lblFdnTitle);
            Controls.Add(rightPanel);

            // Center Tab Control
            var tabControl = _workflowTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Font = new Font("Segoe UI", 9F),
                Padding = new Point(12, 6),
                Appearance = TabAppearance.FlatButtons,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(0, 1),
                AccessibleName = "Foundation role-oriented settings"
            };

            // TAB 1: Lớp Thép Dưới (Bottom Mesh)
            var tabBot = new TabPage("Thép Lưới Dưới") { BackColor = KhimUiStyle.FormBg };
            var grpBotX = new GroupBox { Text = "Phương X (Dưới)", Left = 15, Top = 15, Width = 520, Height = 110 };
            KhimUiStyle.ApplyCardStyle(grpBotX);
            var lblBotXDia = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbBotXDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblBotXSpacing = new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true };
            _numBotXSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            _chkBotXHook = new CheckBox { Text = "Uốn bẻ móc đứng 90° lên đỉnh móng", Left = 15, Top = 70, AutoSize = true, Checked = true };

            grpBotX.Controls.Add(lblBotXDia);
            grpBotX.Controls.Add(_cmbBotXDia);
            grpBotX.Controls.Add(lblBotXSpacing);
            grpBotX.Controls.Add(_numBotXSpacing);
            grpBotX.Controls.Add(_chkBotXHook);

            var grpBotY = new GroupBox { Text = "Phương Y (Dưới)", Left = 15, Top = 140, Width = 520, Height = 110 };
            KhimUiStyle.ApplyCardStyle(grpBotY);
            var lblBotYDia = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbBotYDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblBotYSpacing = new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true };
            _numBotYSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            _chkBotYHook = new CheckBox { Text = "Uốn bẻ móc đứng 90° lên đỉnh móng", Left = 15, Top = 70, AutoSize = true, Checked = true };

            grpBotY.Controls.Add(lblBotYDia);
            grpBotY.Controls.Add(_cmbBotYDia);
            grpBotY.Controls.Add(lblBotYSpacing);
            grpBotY.Controls.Add(_numBotYSpacing);
            grpBotY.Controls.Add(_chkBotYHook);

            tabBot.Controls.Add(grpBotX);
            tabBot.Controls.Add(grpBotY);
            RebarLayout.Fields(grpBotX, RebarLayout.Field("Đường kính", _cmbBotXDia),
                RebarLayout.Field("Khoảng cách a (mm)", _numBotXSpacing), new Control[] { _chkBotXHook });
            RebarLayout.Fields(grpBotY, RebarLayout.Field("Đường kính", _cmbBotYDia),
                RebarLayout.Field("Khoảng cách a (mm)", _numBotYSpacing), new Control[] { _chkBotYHook });
            RebarLayout.Stack(tabBot, grpBotX, grpBotY);
            tabControl.TabPages.Add(tabBot);

            // TAB 2: Lớp Thép Trên (Top Mesh)
            var tabTop = new TabPage("Thép Lưới Trên") { BackColor = KhimUiStyle.FormBg };
            _chkEnableTopMesh = new CheckBox { Text = "Bật bố trí Thép Lớp Trên (Cho Đài Móng / Móng Sâu)", Left = 15, Top = 15, AutoSize = true, Checked = false };

            var grpTopX = new GroupBox { Text = "Phương X (Trên)", Left = 15, Top = 45, Width = 520, Height = 100 };
            KhimUiStyle.ApplyCardStyle(grpTopX);
            _cmbTopXDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _numTopXSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 200, Increment = 10 };
            _chkTopXHook = new CheckBox { Text = "Móc bẻ 90° xuống đáy móng", Left = 15, Top = 65, AutoSize = true, Checked = true };

            grpTopX.Controls.Add(new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true });
            grpTopX.Controls.Add(_cmbTopXDia);
            grpTopX.Controls.Add(new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true });
            grpTopX.Controls.Add(_numTopXSpacing);
            grpTopX.Controls.Add(_chkTopXHook);

            var grpTopY = new GroupBox { Text = "Phương Y (Trên)", Left = 15, Top = 155, Width = 520, Height = 100 };
            KhimUiStyle.ApplyCardStyle(grpTopY);
            _cmbTopYDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _numTopYSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 200, Increment = 10 };
            _chkTopYHook = new CheckBox { Text = "Móc bẻ 90° xuống đáy móng", Left = 15, Top = 65, AutoSize = true, Checked = true };

            grpTopY.Controls.Add(new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true });
            grpTopY.Controls.Add(_cmbTopYDia);
            grpTopY.Controls.Add(new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true });
            grpTopY.Controls.Add(_numTopYSpacing);
            grpTopY.Controls.Add(_chkTopYHook);

            tabTop.Controls.Add(_chkEnableTopMesh);
            tabTop.Controls.Add(grpTopX);
            tabTop.Controls.Add(grpTopY);
            RebarLayout.Fields(grpTopX, RebarLayout.Field("Đường kính", _cmbTopXDia),
                RebarLayout.Field("Khoảng cách a (mm)", _numTopXSpacing), new Control[] { _chkTopXHook });
            RebarLayout.Fields(grpTopY, RebarLayout.Field("Đường kính", _cmbTopYDia),
                RebarLayout.Field("Khoảng cách a (mm)", _numTopYSpacing), new Control[] { _chkTopYHook });
            RebarLayout.Stack(tabTop, _chkEnableTopMesh, grpTopX, grpTopY);
            tabControl.TabPages.Add(tabTop);

            // TAB 3: Perimeter edge U-bars (existing generator output, now explicit in the UI)
            var tabPerimeter = new TabPage("Thép chữ U mép móng") { BackColor = KhimUiStyle.FormBg };
            _chkEnablePerimeterUStirrups = new CheckBox
            {
                Text = "Tạo thép chữ U gia cường mép móng (4 cạnh)",
                AutoSize = true,
                Checked = _settings.EnablePerimeterUStirrups
            };
            _cmbPerimeterStirrupDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _numPerimeterStirrupSpacing = new NumericUpDown { Minimum = 50, Maximum = 500, Value = 200, Increment = 10 };
            var grpPerimeter = new GroupBox { Text = "Cấu hình thép U theo chu vi", Dock = DockStyle.Top, Padding = new Padding(10) };
            KhimUiStyle.ApplyCardStyle(grpPerimeter);
            RebarLayout.Fields(grpPerimeter,
                RebarLayout.Field("Đường kính", _cmbPerimeterStirrupDia),
                RebarLayout.Field("Khoảng cách (mm)", _numPerimeterStirrupSpacing),
                new Control[] { new Label { Text = "Chiều dài chân U do solver chọn: min(40% chiều dày móng, 366 mm).", AutoSize = true } });
            RebarLayout.Stack(tabPerimeter, _chkEnablePerimeterUStirrups, grpPerimeter);
            _chkEnablePerimeterUStirrups.CheckedChanged += (s, e) =>
            {
                grpPerimeter.Enabled = _chkEnablePerimeterUStirrups.Checked;
                _cmbPerimeterStirrupDia.Enabled = _chkEnablePerimeterUStirrups.Checked;
                _numPerimeterStirrupSpacing.Enabled = _chkEnablePerimeterUStirrups.Checked;
            };
            tabControl.TabPages.Add(tabPerimeter);

            // TAB 4: Thép Chờ Cột & Thép Đai (Column Dowels & Stirrups)
            var tabDowel = new TabPage("Thép Chờ & Thép Đai") { BackColor = KhimUiStyle.FormBg };
            _chkEnableDowels = new CheckBox { Text = "Bật tạo Thép Chờ Cột & Thép Đai Lồng (Column Dowels & Stirrups)", Left = 15, Top = 12, AutoSize = true, Checked = true };

            var grpDowel = new GroupBox { Text = "Thông Số Thép Chờ Cột & Đai Lồng Chân Cột", Left = 15, Top = 38, Width = 520, Height = 240 };
            KhimUiStyle.ApplyCardStyle(grpDowel);

            _cmbDowelDia = new ComboBox { Left = 130, Top = 25, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _numDowelQtyX = new NumericUpDown { Left = 380, Top = 25, Width = 100, Minimum = 2, Maximum = 10, Value = 2 };
            _numDowelQtyY = new NumericUpDown { Left = 380, Top = 65, Width = 100, Minimum = 2, Maximum = 10, Value = 2 };

            _numDowelFootLeg = new NumericUpDown { Left = 130, Top = 105, Width = 110, Minimum = 100, Maximum = 1000, Value = 300, Increment = 50 };
            _numDowelExtension = new NumericUpDown { Left = 380, Top = 105, Width = 100, Minimum = 200, Maximum = 2000, Value = 600, Increment = 50 };

            _chkDowelInward = new CheckBox { Text = "Bẻ chân quỳ úp vào trong lòng cột", Left = 15, Top = 145, AutoSize = true, Checked = false };
            _chkStaggeredDowels = new CheckBox { Text = "Bố trí nối so le 50% thép chờ (50% Staggered)", Left = 250, Top = 145, AutoSize = true, Checked = true };

            _chkEnableDowelStirrups = new CheckBox { Text = "Đặt Thép Đai lồng móng cố định chân cột", Left = 15, Top = 180, AutoSize = true, Checked = true };
            _numDowelStirrupQty = new NumericUpDown { Left = 380, Top = 175, Width = 100, Minimum = 2, Maximum = 10, Value = 3 };

            grpDowel.Controls.Add(new Label { Text = "Đường kính thép chờ:", Left = 15, Top = 28, AutoSize = true });
            grpDowel.Controls.Add(_cmbDowelDia);
            grpDowel.Controls.Add(new Label { Text = "Số thanh phương X:", Left = 250, Top = 28, AutoSize = true });
            grpDowel.Controls.Add(_numDowelQtyX);
            grpDowel.Controls.Add(new Label { Text = "Số thanh phương Y:", Left = 250, Top = 68, AutoSize = true });
            grpDowel.Controls.Add(_numDowelQtyY);

            grpDowel.Controls.Add(new Label { Text = "Chân quỳ 90° (mm):", Left = 15, Top = 108, AutoSize = true });
            grpDowel.Controls.Add(_numDowelFootLeg);
            grpDowel.Controls.Add(new Label { Text = "Đoạn chờ L0 (mm):", Left = 250, Top = 108, AutoSize = true });
            grpDowel.Controls.Add(_numDowelExtension);

            grpDowel.Controls.Add(_chkDowelInward);
            grpDowel.Controls.Add(_chkStaggeredDowels);

            grpDowel.Controls.Add(_chkEnableDowelStirrups);
            grpDowel.Controls.Add(new Label { Text = "Số đai lồng:", Left = 290, Top = 178, AutoSize = true });
            grpDowel.Controls.Add(_numDowelStirrupQty);

            tabDowel.Controls.Add(_chkEnableDowels);
            tabDowel.Controls.Add(grpDowel);
            RebarLayout.Fields(grpDowel,
                RebarLayout.Field("Đường kính thép chờ", _cmbDowelDia),
                RebarLayout.Field("Số thanh phương X", _numDowelQtyX),
                RebarLayout.Field("Số thanh phương Y", _numDowelQtyY),
                RebarLayout.Field("Chân quỳ 90° (mm)", _numDowelFootLeg),
                RebarLayout.Field("Đoạn chờ L0 (mm)", _numDowelExtension),
                new Control[] { _chkDowelInward }, new Control[] { _chkStaggeredDowels },
                new Control[] { _chkEnableDowelStirrups },
                RebarLayout.Field("Số đai lồng", _numDowelStirrupQty));
            RebarLayout.Stack(tabDowel, _chkEnableDowels, grpDowel);
            tabControl.TabPages.Add(tabDowel);

            // TAB 5: Tiêu Chuẩn & Template
            var tabDesign = new TabPage("Tiêu Chuẩn & Template") { BackColor = KhimUiStyle.FormBg };
            var grpCode = new GroupBox { Text = "Tiêu Chuẩn Thiết Kế & Cấp Độ Bền", Left = 15, Top = 15, Width = 520, Height = 140 };
            KhimUiStyle.ApplyCardStyle(grpCode);

            _cmbDesignCode = new ComboBox { Left = 130, Top = 30, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDesignCode.Items.Add("TCVN 5574:2018");
            _cmbDesignCode.Items.Add("Eurocode 2 / Eurocode 7");
            _cmbDesignCode.SelectedIndex = 0;

            _cmbConcreteGrade = new ComboBox { Left = 130, Top = 70, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbSteelGrade = new ComboBox { Left = 380, Top = 70, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _numCoverMm = new NumericUpDown { Left = 130, Top = 105, Width = 110, Minimum = 20, Maximum = 100, Value = 50, Increment = 5 };

            Action updateGrades = () =>
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

                _cmbConcreteGrade.SelectedIndex = 2;
                _cmbSteelGrade.SelectedIndex = Math.Min(2, _cmbSteelGrade.Items.Count - 1);
            };

            _cmbDesignCode.SelectedIndexChanged += (s, e) => updateGrades();
            updateGrades();

            grpCode.Controls.Add(new Label { Text = "Tiêu chuẩn:", Left = 15, Top = 35, AutoSize = true });
            grpCode.Controls.Add(_cmbDesignCode);
            grpCode.Controls.Add(new Label { Text = "Mác bê tông:", Left = 15, Top = 75, AutoSize = true });
            grpCode.Controls.Add(_cmbConcreteGrade);
            grpCode.Controls.Add(new Label { Text = "Mác thép:", Left = 310, Top = 75, AutoSize = true });
            grpCode.Controls.Add(_cmbSteelGrade);
            grpCode.Controls.Add(new Label { Text = "Cover (mm):", Left = 15, Top = 110, AutoSize = true });
            grpCode.Controls.Add(_numCoverMm);

            var grpTpl = new GroupBox { Text = "Quản Lý Template JSON", Left = 15, Top = 170, Width = 520, Height = 90 };
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

            tabDesign.Controls.Add(grpCode);
            tabDesign.Controls.Add(grpTpl);
            RebarLayout.Fields(grpCode, RebarLayout.Field("Tiêu chuẩn", _cmbDesignCode),
                RebarLayout.Field("Mác bê tông", _cmbConcreteGrade), RebarLayout.Field("Mác thép", _cmbSteelGrade),
                RebarLayout.Field("Cover (mm)", _numCoverMm));
            RebarLayout.Fields(grpTpl, RebarLayout.Field("Mẫu thiết lập", _cmbTemplates),
                new Control[] { _btnSaveTemplate, _btnLoadTemplate });
            RebarLayout.Stack(tabDesign, grpCode, grpTpl);
            tabControl.TabPages.Add(tabDesign);

            tabControl.TabPages.Add(RebarReferenceViews.CreatePage(RebarReferenceKind.Foundation));
            tabControl.TabPages.Add(RebarConfigurationPage.Create(this, _doc, RebarReferenceKind.Foundation,
                RebarConfigurationField.Number("Foundation.DowelFootMm", "Chân neo thép chờ (mm)", _numDowelFootLeg),
                RebarConfigurationField.Number("Foundation.DowelExtensionMm", "Chiều dài thép chờ (mm)", _numDowelExtension),
            RebarConfigurationField.Number("Foundation.CoverMm", "Lớp bảo vệ (mm)", _numCoverMm),
            RebarConfigurationField.Number("Foundation.BottomSpacingX", "Lưới đáy X (mm)", _numBotXSpacing),
            RebarConfigurationField.Number("Foundation.BottomSpacingY", "Lưới đáy Y (mm)", _numBotYSpacing),
            RebarConfigurationField.Number("Foundation.PerimeterUBarSpacingMm", "Thép U mép - khoảng cách (mm)", _numPerimeterStirrupSpacing),
            RebarConfigurationField.Flag("Foundation.Dowels", "Tạo thép chờ", _chkEnableDowels),
            RebarConfigurationField.Flag("Foundation.PerimeterUStirrups", "Tạo thép chữ U mép móng", _chkEnablePerimeterUStirrups),
                RebarConfigurationField.Flag("Foundation.Staggered", "Thép chờ so le", _chkStaggeredDowels),
                RebarConfigurationField.Flag("Foundation.Inward", "Chân neo hướng vào", _chkDowelInward),
                RebarConfigurationField.Flag("Foundation.BottomXHook", "Móc lưới dưới X", _chkBotXHook),
                RebarConfigurationField.Flag("Foundation.BottomYHook", "Móc lưới dưới Y", _chkBotYHook),
                RebarConfigurationField.Flag("Foundation.TopMesh", "Tạo lưới trên", _chkEnableTopMesh)));
            var workspace = new Panel { Dock = DockStyle.Fill, BackColor = KhimUiStyle.FormBg };
            var roleNavigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(6, 5, 6, 3),
                BackColor = Color.White
            };
            AddRoleNavigation(roleNavigation, "Lưới dưới", 0);
            AddRoleNavigation(roleNavigation, "Lưới trên", 1);
            AddRoleNavigation(roleNavigation, "Thép U mép", 2);
            AddRoleNavigation(roleNavigation, "Thép chờ & đai cổ", 3);
            AddRoleNavigation(roleNavigation, "Thiết lập", 4);
            AddRoleNavigation(roleNavigation, "Tham khảo", 5);
            AddRoleNavigation(roleNavigation, "Cấu hình", 6);
            tabControl.SelectedIndexChanged += (s, e) => UpdateRoleNavigation();
            workspace.Controls.Add(tabControl);
            workspace.Controls.Add(roleNavigation);
            Controls.Add(workspace);
            workspace.BringToFront();
            UpdateRoleNavigation();
            footer.SendToBack();
        }

        private void ApplyLanguage()
        {
            bool isEnglish = LanguageManager.IsEnglish;
            Text = isEnglish ? "Foundation Reinforcement" : "Rebar - Móng";
            AccessibleName = isEnglish ? "Foundation Reinforcement" : "Rebar - Móng - Lưới thép, thép chờ cột và cấu tạo biên";

            var vietnameseToEnglish = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["1  Chọn móng     2  Cấu hình lưới     3  Kiểm tra thép chờ     4  Tạo thép"] = "1  Select footing     2  Configure reinforcement     3  Review dowels     4  Create",
                ["Ngôn ngữ"] = "Language",
                ["Tạo thép móng"] = "Create Foundation Rebar",
                ["Giải / Cập nhật"] = "Solve / Refresh",
                ["Xem 3D"] = "View 3D",
                ["Đóng"] = "Close",
                ["CẤU KIỆN ÁP DỤNG"] = "SELECTED FOUNDATIONS",
                ["Thép Lưới Dưới"] = "Bottom Mat",
                ["Thép Lưới Trên"] = "Top Mat",
                ["Thép chữ U mép móng"] = "Perimeter U-bars",
                ["Thép Chờ & Thép Đai"] = "Column Dowels & Ties",
                ["Tiêu Chuẩn & Template"] = "Design & Templates",
                ["Cấu hình dự án"] = "Project Settings",
                ["Phương X (Dưới)"] = "X Direction (Bottom)",
                ["Phương Y (Dưới)"] = "Y Direction (Bottom)",
                ["Phương X (Trên)"] = "X Direction (Top)",
                ["Phương Y (Trên)"] = "Y Direction (Top)",
                ["Đường kính:"] = "Bar diameter:",
                ["Đường kính"] = "Bar diameter",
                ["Khoảng cách a (mm):"] = "Spacing (mm):",
                ["Khoảng cách (mm)"] = "Spacing (mm)",
                ["Uốn bẻ móc đứng 90° lên đỉnh móng"] = "Bend 90° hooks up to the footing top",
                ["Bật bố trí Thép Lớp Trên (Cho Đài Móng / Móng Sâu)"] = "Enable top mat (pile cap / deep footing)",
                ["Móc bẻ 90° xuống đáy móng"] = "Bend 90° hooks down to footing bottom",
                ["Tạo thép chữ U gia cường mép móng (4 cạnh)"] = "Create perimeter U-bars (all four edges)",
                ["Cấu hình thép U theo chu vi"] = "Perimeter U-bar settings",
                ["Chiều dài chân U do solver chọn: min(40% chiều dày móng, 366 mm)."] = "U-leg length is solver-controlled: min(40% footing depth, 366 mm).",
                ["Bật tạo Thép Chờ Cột & Thép Đai Lồng (Column Dowels & Stirrups)"] = "Enable column dowels and confining ties",
                ["Thông Số Thép Chờ Cột & Đai Lồng Chân Cột"] = "Column Dowels & Confining Ties",
                ["Đường kính thép chờ:"] = "Dowel bar diameter:",
                ["Số thanh phương X:"] = "Bars along X:",
                ["Số thanh phương Y:"] = "Bars along Y:",
                ["Chân quỳ 90° (mm):"] = "90° footing leg (mm):",
                ["Đoạn chờ L0 (mm):"] = "Dowel extension L0 (mm):",
                ["Bẻ chân quỳ úp vào trong lòng cột"] = "Bend dowel legs inward toward the column",
                ["Bố trí nối so le 50% thép chờ (50% Staggered)"] = "Stagger 50% of dowel extensions",
                ["Đặt Thép Đai lồng móng cố định chân cột"] = "Add confining ties around column dowels",
                ["Số đai lồng:"] = "Number of confining ties:",
                ["Tiêu Chuẩn Thiết Kế & Cấp Độ Bền"] = "Design Code & Material Grades",
                ["Tiêu chuẩn:"] = "Design code:",
                ["Mác bê tông:"] = "Concrete grade:",
                ["Mác thép:"] = "Reinforcement grade:",
                ["Cover (mm):"] = "Concrete cover (mm):",
                ["Quản Lý Template JSON"] = "JSON Template Management",
                ["Mẫu thiết lập"] = "Template",
                ["Lưu mẫu"] = "Save Template",
                ["Nạp mẫu"] = "Load Template",
                ["Chân neo thép chờ (mm)"] = "Dowel anchorage leg (mm)",
                ["Chiều dài thép chờ (mm)"] = "Dowel extension (mm)",
                ["Lớp bảo vệ (mm)"] = "Concrete cover (mm)",
                ["Lưới đáy X (mm)"] = "Bottom mat X spacing (mm)",
                ["Lưới đáy Y (mm)"] = "Bottom mat Y spacing (mm)",
                ["Thép U mép - khoảng cách (mm)"] = "Perimeter U-bar spacing (mm)",
                ["Tạo thép chờ"] = "Create column dowels",
                ["Tạo thép chữ U mép móng"] = "Create perimeter U-bars",
                ["Thép chờ so le"] = "Stagger dowels",
                ["Chân neo hướng vào"] = "Dowel legs point inward",
                ["Móc lưới dưới X"] = "Bottom mat X hooks",
                ["Móc lưới dưới Y"] = "Bottom mat Y hooks",
                ["Tạo lưới trên"] = "Create top mat",
                ["Lưới dưới"] = "Bottom mat",
                ["Lưới trên"] = "Top mat",
                ["Thép U mép"] = "Edge U-bars",
                ["Thép chờ & đai cổ"] = "Dowels & ties",
                ["Thiết lập"] = "Design",
                ["Tham khảo"] = "Reference",
                ["Cấu hình"] = "Settings"
            };

            var englishToVietnamese = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in vietnameseToEnglish)
                if (!englishToVietnamese.ContainsKey(entry.Value)) englishToVietnamese.Add(entry.Value, entry.Key);

            foreach (Control control in EnumerateDescendantControls(this))
            {
                string translated;
                if (isEnglish && vietnameseToEnglish.TryGetValue(control.Text, out translated))
                    control.Text = translated;
                else if (!isEnglish && englishToVietnamese.TryGetValue(control.Text, out translated))
                    control.Text = translated;
            }

            if (_workflowTabs != null && _workflowTabs.TabPages.Count > 5)
            {
                int selectedIndex = _workflowTabs.SelectedIndex;
                TabPage oldReferencePage = _workflowTabs.TabPages[5];
                _workflowTabs.TabPages.RemoveAt(5);
                oldReferencePage.Dispose();
                _workflowTabs.TabPages.Insert(5, RebarReferenceViews.CreatePage(RebarReferenceKind.Foundation));
                _workflowTabs.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, _workflowTabs.TabPages.Count - 1));
            }

            UpdateRoleNavigation();
            UpdatePreviewStateUi();
            _previewPanel?.Invalidate();
        }

        private static IEnumerable<Control> EnumerateDescendantControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateDescendantControls(child))
                    yield return descendant;
            }
        }

        private void AddRoleNavigation(FlowLayoutPanel host, string label, int pageIndex)
        {
            var button = new Button
            {
                Text = label,
                Tag = pageIndex,
                AutoSize = true,
                Height = 30,
                AccessibleName = "Show foundation " + label + " settings",
                Margin = new Padding(2, 0, 2, 0)
            };
            button.Click += (s, e) =>
            {
                if (_workflowTabs != null && pageIndex >= 0 && pageIndex < _workflowTabs.TabPages.Count)
                    _workflowTabs.SelectedIndex = pageIndex;
            };
            _roleNavigationButtons.Add(button);
            KhimUiStyle.ApplySecondaryButton(button);
            host.Controls.Add(button);
        }

        private void UpdateRoleNavigation()
        {
            for (int index = 0; index < _roleNavigationButtons.Count; index++)
            {
                Button button = _roleNavigationButtons[index];
                if (_workflowTabs != null && _workflowTabs.SelectedIndex == (int)button.Tag)
                    KhimUiStyle.ApplyPrimaryButton(button);
                else
                    KhimUiStyle.ApplySecondaryButton(button);
            }
        }

        private void PopulateBarCombos()
        {
            var barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .Select(b => b.Name)
                .OrderBy(n => n)
                .ToList();

            if (!barTypes.Any()) barTypes = new List<string> { "d10", "d12", "d14", "d16", "d18", "d20" };

            PopulateCombo(_cmbBotXDia, barTypes, "14");
            PopulateCombo(_cmbBotYDia, barTypes, "14");
            PopulateCombo(_cmbTopXDia, barTypes, "12");
            PopulateCombo(_cmbTopYDia, barTypes, "12");
            PopulateCombo(_cmbDowelDia, barTypes, "18");
            PopulateCombo(_cmbPerimeterStirrupDia, barTypes, "10");
        }

        private void PopulateCombo(ComboBox combo, List<string> items, string defaultDia)
        {
            combo.Items.Clear();
            foreach (var item in items) combo.Items.Add(item);
            int idx = items.FindIndex(i => i.Contains(defaultDia));
            combo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void PopulateFoundationList()
        {
            _foundationListBox.Items.Clear();
            _previewOutlines.Clear();
            for (int i = 0; i < _availableFoundations.Count; i++)
            {
                FamilyInstance fdn = _availableFoundations[i];
                _foundationListBox.Items.Add($"{fdn.Name} (ID: {fdn.Id.ToLongValue()})");
                BoundingBoxXYZ bounds = fdn.get_BoundingBox(null);
                if (bounds != null)
                    _previewOutlines[i] = MakePreviewOutline(fdn, bounds);
            }
            if (_foundationListBox.Items.Count > 0)
                _foundationListBox.SelectedIndex = 0;
        }

        private void LoadTemplateList()
        {
            _cmbTemplates.Items.Clear();
            foreach (string name in FoundationRebarSettings.GetSavedTemplateNames())
                _cmbTemplates.Items.Add(name);
            if (_cmbTemplates.Items.Count > 0)
                _cmbTemplates.SelectedIndex = 0;
        }

        private void SaveCurrentTemplate()
        {
            CaptureSettingsFromControls();
            string name = _cmbTemplates.Text;
            if (string.IsNullOrWhiteSpace(name)) name = _settings.TemplateName;

            if (!FoundationRebarSettings.SaveTemplate(_settings, name))
            {
                KhimDialogHelper.ShowError("Không thể lưu template móng.");
                return;
            }

            LoadTemplateList();
            _cmbTemplates.SelectedItem = name;
            KhimDialogHelper.ShowInfo("Đã lưu template: " + name);
        }

        private void LoadSelectedTemplate()
        {
            var loaded = FoundationRebarSettings.LoadTemplate(_cmbTemplates.Text);
            if (loaded == null)
            {
                KhimDialogHelper.ShowError("Không thể nạp template đã chọn.");
                return;
            }

            CopySettings(loaded, _settings);
            ApplySettingsToControls(_settings);
            _previewPanel.Invalidate();
            KhimDialogHelper.ShowInfo("Đã nạp template: " + loaded.TemplateName);
        }

        private void CaptureSettingsFromControls()
        {
            _settings.BotXDiaLabel = _cmbBotXDia.Text;
            _settings.BotXSpacingMm = (double)_numBotXSpacing.Value;
            _settings.BotXHookUp = _chkBotXHook.Checked;
            _settings.BotYDiaLabel = _cmbBotYDia.Text;
            _settings.BotYSpacingMm = (double)_numBotYSpacing.Value;
            _settings.BotYHookUp = _chkBotYHook.Checked;
            _settings.EnableTopMesh = _chkEnableTopMesh.Checked;
            _settings.TopXDiaLabel = _cmbTopXDia.Text;
            _settings.TopXSpacingMm = (double)_numTopXSpacing.Value;
            _settings.TopXHookDown = _chkTopXHook.Checked;
            _settings.TopYDiaLabel = _cmbTopYDia.Text;
            _settings.TopYSpacingMm = (double)_numTopYSpacing.Value;
            _settings.TopYHookDown = _chkTopYHook.Checked;
            _settings.EnablePerimeterUStirrups = _chkEnablePerimeterUStirrups.Checked;
            _settings.PerimeterStirrupDiaLabel = _cmbPerimeterStirrupDia.Text;
            _settings.PerimeterStirrupSpacingMm = (double)_numPerimeterStirrupSpacing.Value;
            _settings.EnableColumnDowels = _chkEnableDowels.Checked;
            _settings.DowelDiaLabel = _cmbDowelDia.Text;
            _settings.DowelQtyX = (int)_numDowelQtyX.Value;
            _settings.DowelQtyY = (int)_numDowelQtyY.Value;
            _settings.DowelFootLegMm = (double)_numDowelFootLeg.Value;
            _settings.DowelExtensionMm = (double)_numDowelExtension.Value;
            _settings.DowelLegInward = _chkDowelInward.Checked;
            _settings.StaggeredDowels = _chkStaggeredDowels.Checked;
            _settings.EnableDowelStirrups = _chkEnableDowelStirrups.Checked;
            _settings.DowelStirrupQty = (int)_numDowelStirrupQty.Value;
            _settings.DesignCode = _cmbDesignCode.Text;
            _settings.ConcreteGrade = _cmbConcreteGrade.Text;
            _settings.SteelGrade = _cmbSteelGrade.Text;
            _settings.CustomCoverMm = (double)_numCoverMm.Value;
        }

        private void ApplySettingsToControls(FoundationRebarSettings value)
        {
            _cmbBotXDia.Text = value.BotXDiaLabel;
            _numBotXSpacing.Value = Clamp(_numBotXSpacing, value.BotXSpacingMm);
            _chkBotXHook.Checked = value.BotXHookUp;
            _cmbBotYDia.Text = value.BotYDiaLabel;
            _numBotYSpacing.Value = Clamp(_numBotYSpacing, value.BotYSpacingMm);
            _chkBotYHook.Checked = value.BotYHookUp;
            _chkEnableTopMesh.Checked = value.EnableTopMesh;
            _cmbTopXDia.Text = value.TopXDiaLabel;
            _numTopXSpacing.Value = Clamp(_numTopXSpacing, value.TopXSpacingMm);
            _chkTopXHook.Checked = value.TopXHookDown;
            _cmbTopYDia.Text = value.TopYDiaLabel;
            _numTopYSpacing.Value = Clamp(_numTopYSpacing, value.TopYSpacingMm);
            _chkTopYHook.Checked = value.TopYHookDown;
            _chkEnablePerimeterUStirrups.Checked = value.EnablePerimeterUStirrups;
            _cmbPerimeterStirrupDia.Text = value.PerimeterStirrupDiaLabel;
            _numPerimeterStirrupSpacing.Value = Clamp(_numPerimeterStirrupSpacing, value.PerimeterStirrupSpacingMm);
            _chkEnableDowels.Checked = value.EnableColumnDowels;
            _cmbDowelDia.Text = value.DowelDiaLabel;
            _numDowelQtyX.Value = Clamp(_numDowelQtyX, value.DowelQtyX);
            _numDowelQtyY.Value = Clamp(_numDowelQtyY, value.DowelQtyY);
            _numDowelFootLeg.Value = Clamp(_numDowelFootLeg, value.DowelFootLegMm);
            _numDowelExtension.Value = Clamp(_numDowelExtension, value.DowelExtensionMm);
            _chkDowelInward.Checked = value.DowelLegInward;
            _chkStaggeredDowels.Checked = value.StaggeredDowels;
            _chkEnableDowelStirrups.Checked = value.EnableDowelStirrups;
            _numDowelStirrupQty.Value = Clamp(_numDowelStirrupQty, value.DowelStirrupQty);
            _cmbDesignCode.Text = value.DesignCode;
            _cmbConcreteGrade.Text = value.ConcreteGrade;
            _cmbSteelGrade.Text = value.SteelGrade;
            _numCoverMm.Value = Clamp(_numCoverMm, value.CustomCoverMm);
        }

        private static decimal Clamp(NumericUpDown control, double value)
        {
            decimal result = (decimal)value;
            return Math.Max(control.Minimum, Math.Min(control.Maximum, result));
        }

        private static void CopySettings(FoundationRebarSettings source, FoundationRebarSettings target)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            Newtonsoft.Json.JsonConvert.PopulateObject(json, target);
        }

        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = _previewPanel.Width;
            int h = _previewPanel.Height;
            g.Clear(Color.White);
            int index = _foundationListBox.SelectedIndices.Count == 0 ? -1 : _foundationListBox.SelectedIndices[0];
            FoundationPreviewOutline outline;
            if (index < 0 || !_previewOutlines.TryGetValue(index, out outline))
            {
                TextRenderer.DrawText(g, LanguageManager.IsEnglish ? "Select a foundation to inspect its bounding-box extents." : "Chọn móng để xem phạm vi hộp bao.", Font, _previewPanel.ClientRectangle,
                    Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            float minX = (float)outline.MinX, minY = (float)outline.MinY;
            float width = Math.Max(1e-5f, (float)(outline.MaxX - outline.MinX));
            float height = Math.Max(1e-5f, (float)(outline.MaxY - outline.MinY));
            float scale = Math.Min((w - 56f) / width, (h - 82f) / height);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) return;
            float ox = (w - width * scale) / 2f;
            float oy = (h - height * scale) / 2f;
            PointF Map(double x, double y) => new PointF(ox + (float)(x - minX) * scale, oy + (float)(outline.MaxY - y) * scale);
            PointF topLeft = Map(outline.MinX, outline.MaxY);
            var hostRect = new RectangleF(topLeft.X, topLeft.Y, width * scale, height * scale);

            string fingerprint;
            RebarPreviewComponent component = _previewFingerprints.TryGetValue(outline.HostUniqueId, out fingerprint)
                ? _lastPreview?.Find(fingerprint) : null;
            using (var hostPen = new Pen(Color.FromArgb(51, 65, 85), 2f))
            using (var barPen = new Pen(_previewLifecycle.State == PreviewLifecycleState.Valid ? Color.FromArgb(37, 99, 235) : Color.FromArgb(148, 163, 184), 1.4f))
            using (var textBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
            using (var noteBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            using (var font = new Font("Segoe UI", 8.5f))
            {
                g.DrawRectangle(hostPen, hostRect.X, hostRect.Y, hostRect.Width, hostRect.Height);
                if (component != null)
                {
                    foreach (RebarPreviewPath path in component.Paths)
                    {
                        PointF[] points = path.Points.Select(point => Map(point.X, point.Y)).ToArray();
                        if (points.Length < 2) continue;
                        float dx = points.Max(point => point.X) - points.Min(point => point.X);
                        float dy = points.Max(point => point.Y) - points.Min(point => point.Y);
                        if (dx < 0.5f && dy < 0.5f) g.FillEllipse(barPen.Brush, points[0].X - 2.5f, points[0].Y - 2.5f, 5f, 5f);
                        else g.DrawLines(barPen, points);
                    }
                    TextRenderer.DrawText(g, LanguageManager.IsEnglish ? "SOLVED CENTERLINES · " + component.BarCount + " bars" : "TIM THÉP ĐÃ GIẢI · " + component.BarCount + " thanh",
                        Font, new Rectangle(8, 6, w - 16, 24), textBrush.Color, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                }
                else TextRenderer.DrawText(g, LanguageManager.IsEnglish ? "Bounding-box outline only — solve to show production-generated reinforcement." : "Chỉ có hộp bao — hãy giải để xem thép từ bộ tạo sản xuất.",
                    Font, new Rectangle(8, 6, w - 16, 36), noteBrush.Color, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                g.DrawString(outline.Label + "  ·  " + (width * 304.8f).ToString("N0") + " × " + (height * 304.8f).ToString("N0") + " mm",
                    font, textBrush, 8, h - 24);
                g.DrawString(LanguageManager.IsEnglish ? "PLAN · host bounding-box axes X/Y" : "MẶT BẰNG · hộp bao trục mô hình X/Y", font, noteBrush, 8, h - 42);
                if (_previewLifecycle.State == PreviewLifecycleState.Stale)
                    TextRenderer.DrawText(g, LanguageManager.IsEnglish ? "STALE · showing the last accepted solve" : "CŨ · đang hiển thị kết quả đã giải trước đó",
                        Font, new Rectangle(8, 26, w - 16, 24), noteBrush.Color, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            }
        }

        private void AttachPreviewInvalidationHandlers(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (control == _cmbLanguage) continue;
                if (control is NumericUpDown numeric) numeric.ValueChanged += (s, e) => MarkPreviewStale();
                else if (control is ComboBox combo) combo.SelectedIndexChanged += (s, e) => MarkPreviewStale();
                else if (control is CheckBox check) check.CheckedChanged += (s, e) => MarkPreviewStale();
                else if (control is TextBox text) text.TextChanged += (s, e) => MarkPreviewStale();
                if (control.HasChildren) AttachPreviewInvalidationHandlers(control);
            }
            _foundationListBox.SelectedIndexChanged += (s, e) =>
            {
                MarkPreviewStale();
                _previewPanel?.Invalidate();
            };
        }

        private void MarkPreviewStale()
        {
            if (_previewLifecycle.State == PreviewLifecycleState.Valid) _previewLifecycle.MarkStale();
            UpdatePreviewStateUi();
            _previewPanel?.Invalidate();
        }

        private void UpdatePreviewStateUi()
        {
            if (_lblPreviewState == null) return;
            switch (_previewLifecycle.State)
            {
                case PreviewLifecycleState.Valid:
                    _lblPreviewState.Text = LanguageManager.IsEnglish ? "PREVIEW · Valid" : "XEM TRƯỚC · Hợp lệ";
                    _lblPreviewState.ForeColor = Color.FromArgb(21, 128, 61);
                    break;
                case PreviewLifecycleState.Stale:
                    _lblPreviewState.Text = LanguageManager.IsEnglish ? "PREVIEW · Inputs changed — solve again" : "XEM TRƯỚC · Thông số đổi — giải lại";
                    _lblPreviewState.ForeColor = Color.FromArgb(180, 83, 9);
                    break;
                case PreviewLifecycleState.Invalid:
                    _lblPreviewState.Text = LanguageManager.IsEnglish ? "PREVIEW · Solve failed" : "XEM TRƯỚC · Giải không thành công";
                    _lblPreviewState.ForeColor = Color.FromArgb(185, 28, 28);
                    break;
                default:
                    _lblPreviewState.Text = LanguageManager.IsEnglish ? "PREVIEW · Not solved" : "XEM TRƯỚC · Chưa giải";
                    _lblPreviewState.ForeColor = KhimUiStyle.TextSecondary;
                    break;
            }
            bool valid = _previewLifecycle.State == PreviewLifecycleState.Valid && _lastPreview != null;
            _btnCreateRebar.Enabled = valid;
            _btnSolve3D.Enabled = valid;
        }

        private FoundationProfile AnalyzeSelectedFoundation(int index)
        {
            if (index < 0 || index >= _availableFoundations.Count)
                throw new InvalidOperationException("Select a foundation before solving or creating reinforcement.");
            FoundationProfile profile = FoundationGeometryHelper.AnalyzeFoundation(_doc, _availableFoundations[index]);
            if (profile == null) throw new InvalidOperationException("Foundation geometry is unavailable for the selected host.");
            _previewOutlines[index] = MakePreviewOutline(profile.FoundationElement, profile.BoundingBox);
            return profile;
        }

        private FoundationPreviewOutline MakePreviewOutline(FamilyInstance foundation, BoundingBoxXYZ bounds) => new FoundationPreviewOutline
        {
            HostUniqueId = foundation.UniqueId,
            Label = foundation.Name,
            MinX = bounds.Min.X, MinY = bounds.Min.Y, MinZ = bounds.Min.Z,
            MaxX = bounds.Max.X, MaxY = bounds.Max.Y, MaxZ = bounds.Max.Z
        };

        private static IList<Rebar> GenerateFoundation(FoundationRebarGenerator generator, FoundationProfile profile,
            FoundationRebarSettings settings, IDictionary<string, string> roleByBarId)
        {
            var report = new RebarGenerationReport();
            List<Rebar> bars = generator.Generate(profile, settings, report, roleByBarId);
            if (report.HasErrors)
                throw new InvalidOperationException(report.Errors[0].ErrorReason);
            return bars;
        }

        private void BtnPreviewRebar_Click(object sender, EventArgs e)
        {
            try
            {
                int[] selectedIndices = _foundationListBox.SelectedIndices.Cast<int>().ToArray();
                if (selectedIndices.Length == 0) throw new InvalidOperationException("Select at least one foundation before solving its reinforcement.");
                CaptureSettingsFromControls();
                var generator = new FoundationRebarGenerator(_doc);
                FoundationProfile[] profiles = selectedIndices.Select(AnalyzeSelectedFoundation).ToArray();
                _previewFingerprints.Clear();
                RebarPreviewRequest[] requests = profiles.Select(profile =>
                {
                    string key = profile.FoundationElement.UniqueId;
                    string fingerprint = RebarPreviewService.Fingerprint(profile, _settings);
                    var roleByBarId = new Dictionary<string, string>(StringComparer.Ordinal);
                    _previewFingerprints[key] = fingerprint;
                    return new RebarPreviewRequest(fingerprint,
                        () => GenerateFoundation(generator, profile, _settings, roleByBarId),
                        () =>
                        {
                            CaptureSettingsFromControls();
                            FoundationProfile current = FoundationGeometryHelper.AnalyzeFoundation(_doc, profile.FoundationElement);
                            return RebarPreviewService.Fingerprint(current, _settings);
                        }, RebarPreviewService.Describe(profile, _settings),
                        bar =>
                        {
                            string role;
                            return bar != null && roleByBarId.TryGetValue(bar.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), out role)
                                ? role : string.Empty;
                        });
                }).ToArray();
                _previewLifecycle.BeginGeneration();
                UpdatePreviewStateUi();
                _lastPreview = RebarPreviewService.Capture(_doc, requests);
                _previewLifecycle.Complete(_lastPreview, _lastPreview.PlanFingerprint);
                UpdatePreviewStateUi();
                _previewPanel?.Invalidate();
            }
            catch (Exception ex)
            {
                _lastPreview = null;
                _previewFingerprints.Clear();
                _previewLifecycle.Invalidate();
                UpdatePreviewStateUi();
                _previewPanel?.Invalidate();
                KhimDialogHelper.ShowError("Unable to solve foundation reinforcement preview: " + ex.Message);
            }
        }

        private void BtnCreateRebar_Click(object sender, EventArgs e)
        {
            int[] selectedIndices = _foundationListBox.SelectedIndices.Cast<int>().ToArray();
            if (selectedIndices.Length == 0)
            {
                KhimDialogHelper.ShowError("Vui lòng chọn ít nhất 1 Móng để tạo thép.");
                return;
            }

            CaptureSettingsFromControls();
            var generator = new FoundationRebarGenerator(_doc);
            var report = new RebarGenerationReport();
            FoundationProfile[] profiles;
            RebarPreviewSnapshot acceptedPreview;
            string currentFingerprint;
            try
            {
                profiles = selectedIndices.Select(AnalyzeSelectedFoundation).ToArray();
                currentFingerprint = RebarPreviewService.FingerprintInputs(profiles.Select(profile => RebarPreviewService.Fingerprint(profile, _settings)));
                if (_lastPreview == null || !_previewLifecycle.TryGetValid(currentFingerprint, out acceptedPreview) ||
                    profiles.Any(profile => acceptedPreview.Find(RebarPreviewService.Fingerprint(profile, _settings)) == null))
                {
                    UpdatePreviewStateUi();
                    _previewPanel?.Invalidate();
                    KhimDialogHelper.ShowWarning("Solve or refresh the foundation Preview for the current hosts and settings before creating reinforcement.");
                    return;
                }
                foreach (FoundationProfile profile in profiles)
                    if (RebarPreviewService.HasExistingDuplicateBar(_doc, profile.FoundationElement, acceptedPreview,
                        RebarPreviewService.Fingerprint(profile, _settings)))
                    {
                        KhimDialogHelper.ShowWarning("Equivalent reinforcement already exists on foundation " + profile.FoundationElement.Id + ". Remove or edit existing bars before creating to avoid duplicates.");
                        return;
                    }
            }
            catch (Exception ex)
            {
                _previewLifecycle.Invalidate();
                UpdatePreviewStateUi();
                KhimDialogHelper.ShowError("Could not validate the accepted foundation preview: " + ex.Message);
                return;
            }

            try
            {
                TransactionBoundary.Execute(_doc, "Bố trí Thép Móng — KhimTools", () =>
                {
                    foreach (FoundationProfile profile in profiles)
                    {
                        var roleByBarId = new Dictionary<string, string>(StringComparer.Ordinal);
                        List<Rebar> generated = generator.Generate(profile, _settings, report, roleByBarId);
                        _doc.Regenerate();
                        if (report.HasErrors || !RebarPreviewService.Matches(acceptedPreview,
                            RebarPreviewService.Fingerprint(profile, _settings), generated))
                            throw new InvalidOperationException("Generated foundation centerlines differ from the accepted solver preview, or a generator error occurred; the entire foundation transaction was rolled back.");
                    }
                });
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Không thể tạo thép móng: " + ex.Message);
                return;
            }

            KhimDialogHelper.ShowRebarGenerationReport(report, "Móng (Foundation)", selectedIndices.Length);
            Close();
        }
    }
}

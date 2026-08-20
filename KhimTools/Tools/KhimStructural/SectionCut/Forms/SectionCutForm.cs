using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.SectionCutTool.Core;
using KhimTools.SectionCutTool.Models;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Control = System.Windows.Forms.Control;
using Button = System.Windows.Forms.Button;
using GroupBox = System.Windows.Forms.GroupBox;
using Label = System.Windows.Forms.Label;
using CheckBox = System.Windows.Forms.CheckBox;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using FontStyle = System.Drawing.FontStyle;
using View = Autodesk.Revit.DB.View;

namespace KhimTools.SectionCutTool.Forms
{
    /// <summary>
    /// Form giao diện thương mại "SectionCutTool" — Tự động cắt mặt cắt dọc & ngang cho Dầm, Cột, Vách, Sàn, Móng.
    /// Hỗ trợ chọn Section View Type, View Template riêng cho Dọc & Ngang, song ngữ Việt-Anh, Template JSON và Live Preview.
    /// </summary>
    public class SectionCutForm : Form
    {
        private readonly Document _doc;
        private readonly Autodesk.Revit.UI.UIDocument _uidoc;
        private readonly List<ElementCutItem> _allItems;
        private readonly SectionGenerator _generator;

        // UI Controls - Left Panel
        private DataGridView _gridElements;
        private ComboBox _cmbCategoryFilter;
        private Button _btnPickRevit;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Label _lblSelectedCount;

        // UI Controls - Right Panel (Tabs)
        private TabControl _tabControl;
        private TabPage _tabTypes;
        private TabPage _tabCrop;
        private TabPage _tabNaming;

        // Tab 1: Section View Type, Types & Positions & View Templates
        private ComboBox _cmbSectionViewType;
        private CheckBox _chkCreateLongitudinal;
        private NumericUpDown _numLongitudinalScale;
        private ComboBox _cmbLongitudinalTemplate;
        private CheckBox _chkCreateCrossSection;
        private NumericUpDown _numCrossSectionScale;
        private ComboBox _cmbCrossSectionTemplate;
        private RadioButton _rdModeKeyPositions;
        private RadioButton _rdModeRelative;
        private RadioButton _rdModeSpacing;
        private TextBox _txtRelativePositions;
        private NumericUpDown _numSpacing;

        // Tab 2: Crop & View Template Management
        private NumericUpDown _numCropOffsetLeft;
        private NumericUpDown _numCropOffsetRight;
        private NumericUpDown _numCropOffsetTop;
        private NumericUpDown _numCropOffsetBottom;
        private NumericUpDown _numFarClipOffset;
        private CheckBox _chkApplyViewTemplate;
        private ComboBox _cmbViewTemplateGeneral;
        private Button _btnRefreshTemplates;
        private CheckBox _chkFineDetail;
        private CheckBox _chkHideCrop;

        // Tab 3: Naming & Templates
        private TextBox _txtPatternLongitudinal;
        private TextBox _txtPatternCross;
        private ComboBox _cmbTemplate;
        private Button _btnSaveTemplate;
        private Button _btnApplyTemplate;
        private Button _btnDeleteTemplate;

        // Bottom Controls
        private Button _btnPreview;
        private Button _btnGenerate;
        private Button _btnClose;
        private ComboBox _cmbLanguage;

        public SectionCutForm(Document doc, List<Element> availableElements, List<Element> preSelectedElements = null)
            : this(doc, null, availableElements, preSelectedElements)
        {
        }

        public SectionCutForm(Document doc, Autodesk.Revit.UI.UIDocument uidoc, List<Element> availableElements, List<Element> preSelectedElements = null)
        {
            _doc = doc;
            _uidoc = uidoc;
            _generator = new SectionGenerator(doc);

            var preIds = new HashSet<long>((preSelectedElements ?? new List<Element>()).Select(e => e.Id.ToLongValue()));
            _allItems = (availableElements ?? new List<Element>())
                .Select(e => new ElementCutItem(e) { IsSelected = preIds.Contains(e.Id.ToLongValue()) })
                .OrderByDescending(e => e.IsSelected)
                .ThenBy(e => e.CategoryName)
                .ThenBy(e => e.Mark)
                .ToList();

            // Nếu không có phần tử nào được pick từ trước, chọn tất cả mặc định
            if (!preIds.Any() && _allItems.Any())
            {
                foreach (var item in _allItems) item.IsSelected = true;
            }

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            PopulateCategoryFilter();
            PopulateSectionViewTypes();
            PopulateViewTemplates();
            LoadTemplateList();
            ApplySettingsToUi(new SectionCutSettings());
            RefreshGrid();
            ApplyLanguage();
        }

        private void BuildUi()
        {
            Text = "📐 KHIM TOOLS — Cắt Mặt Cắt Kết Cấu Tự Động (Auto Section Cut)";
            Width = 980;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 1. Header Banner
            var header = KhimUiStyle.CreateHeaderBanner(
                "📐 TỰ ĐỘNG CẮT MẶT CẮT DỌC & NGANG CHO BẢN VẼ THÉP",
                "Auto Longitudinal & Cross-Section View Generator for Rebar Detailing",
                "v2.0");
            Controls.Add(header);

            // 2. Main Content Container
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            Controls.Add(pnlMain);
            pnlMain.BringToFront();

            // ── Left Side: Elements List (Width 430) ──────────────────────────
            var grpLeft = new GroupBox
            {
                Text = "Danh Sách Cấu Kiện Kết Cấu",
                Left = 12,
                Top = 10,
                Width = 430,
                Height = 560
            };
            KhimUiStyle.ApplyCardStyle(grpLeft);

            var lblFilter = new Label { Text = "Lọc:", Left = 12, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _cmbCategoryFilter = new ComboBox
            {
                Left = 48,
                Top = 22,
                Width = 195,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbCategoryFilter.SelectedIndexChanged += (s, e) => RefreshGrid();

            _btnPickRevit = new Button { Text = "👆 Pick Revit", Left = 248, Top = 20, Width = 88, Height = 27, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnSelectAll = new Button { Text = "Tất cả", Left = 340, Top = 20, Width = 38, Height = 27 };
            _btnDeselectAll = new Button { Text = "Bỏ", Left = 382, Top = 20, Width = 36, Height = 27 };

            KhimUiStyle.ApplyPrimaryButton(_btnPickRevit, Color.FromArgb(14, 116, 144));
            KhimUiStyle.ApplySecondaryButton(_btnSelectAll);
            KhimUiStyle.ApplySecondaryButton(_btnDeselectAll);

            _btnPickRevit.Click += BtnPickRevit_Click;
            _btnSelectAll.Click += (s, e) => SetAllSelection(true);
            _btnDeselectAll.Click += (s, e) => SetAllSelection(false);

            _gridElements = new DataGridView
            {
                Left = 12,
                Top = 55,
                Width = 406,
                Height = 465,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoGenerateColumns = false
            };

            var colCheck = new DataGridViewCheckBoxColumn { Name = "colCheck", HeaderText = "✓", Width = 35 };
            var colMark = new DataGridViewTextBoxColumn { Name = "colMark", HeaderText = "Mark", Width = 80 };
            var colType = new DataGridViewTextBoxColumn { Name = "colType", HeaderText = "Type", Width = 100 };
            var colCategory = new DataGridViewTextBoxColumn { Name = "colCat", HeaderText = "Loại", Width = 90 };
            var colLength = new DataGridViewTextBoxColumn { Name = "colLen", HeaderText = "Dài (mm)", Width = 80 };

            _gridElements.Columns.AddRange(colCheck, colMark, colType, colCategory, colLength);
            _gridElements.CellValueChanged += (s, e) => UpdateSelectedCount();
            _gridElements.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_gridElements.IsCurrentCellDirty) _gridElements.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _lblSelectedCount = new Label
            {
                Text = "Đã chọn: 0 / 0 cấu kiện",
                Left = 12,
                Top = 528,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.PrimaryButtonBg
            };

            grpLeft.Controls.Add(lblFilter);
            grpLeft.Controls.Add(_cmbCategoryFilter);
            grpLeft.Controls.Add(_btnPickRevit);
            grpLeft.Controls.Add(_btnSelectAll);
            grpLeft.Controls.Add(_btnDeselectAll);
            grpLeft.Controls.Add(_gridElements);
            grpLeft.Controls.Add(_lblSelectedCount);
            pnlMain.Controls.Add(grpLeft);

            // ── Right Side: Section Config Tabs (Width 500) ────────────────────
            _tabControl = new TabControl
            {
                Left = 452,
                Top = 10,
                Width = 500,
                Height = 560
            };

            _tabTypes = new TabPage { Text = "1. Loại Mặt Cắt & Template" };
            _tabCrop = new TabPage { Text = "2. Crop Box & Template Chi Tiết" };
            _tabNaming = new TabPage { Text = "3. Đặt Tên & Mẫu JSON" };

            BuildTabTypes(_tabTypes);
            BuildTabCrop(_tabCrop);
            BuildTabNaming(_tabNaming);

            _tabControl.TabPages.AddRange(new[] { _tabTypes, _tabCrop, _tabNaming });
            pnlMain.Controls.Add(_tabControl);

            // ── Bottom Action Panel ───────────────────────────────────────────
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(241, 245, 249)
            };
            Controls.Add(pnlBottom);

            var lblLang = new Label { Text = "Ngôn ngữ:", Left = 15, Top = 16, AutoSize = true };
            _cmbLanguage = new ComboBox
            {
                Left = 85,
                Top = 12,
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbLanguage.Items.AddRange(new object[] { "🇻🇳 Tiếng Việt", "🇬🇧 English" });
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;
            _cmbLanguage.SelectedIndexChanged += (s, e) =>
            {
                LanguageManager.CurrentLanguage = _cmbLanguage.SelectedIndex == 1 ? AppLanguage.English : AppLanguage.Vietnamese;
                ApplyLanguage();
            };

            _btnPreview = new Button
            {
                Text = "👁️ Xem trước số view",
                Left = 460,
                Top = 9,
                Width = 150,
                Height = 34
            };
            KhimUiStyle.ApplySecondaryButton(_btnPreview);
            _btnPreview.Click += BtnPreview_Click;

            _btnGenerate = new Button
            {
                Text = "⚡ TẠO MẶT CẮT HÀNG LOẠT",
                Left = 620,
                Top = 9,
                Width = 230,
                Height = 34,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            KhimUiStyle.ApplyPrimaryButton(_btnGenerate, KhimUiStyle.CreateButtonBg);
            _btnGenerate.Click += BtnGenerate_Click;

            _btnClose = new Button
            {
                Text = "Đóng",
                Left = 860,
                Top = 9,
                Width = 90,
                Height = 34
            };
            KhimUiStyle.ApplySecondaryButton(_btnClose);
            _btnClose.Click += (s, e) => Close();

            pnlBottom.Controls.Add(lblLang);
            pnlBottom.Controls.Add(_cmbLanguage);
            pnlBottom.Controls.Add(_btnPreview);
            pnlBottom.Controls.Add(_btnGenerate);
            pnlBottom.Controls.Add(_btnClose);
        }

        private void BuildTabTypes(TabPage page)
        {
            // Group 0: Loại Section Type trong Revit
            var grpType = new GroupBox
            {
                Text = "Loại Mặt Cắt Trong Revit (Section View Type)",
                Left = 10,
                Top = 5,
                Width = 470,
                Height = 60
            };
            KhimUiStyle.ApplyCardStyle(grpType);

            var lblSecType = new Label { Text = "Section Family Type:", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _cmbSectionViewType = new ComboBox { Left = 160, Top = 22, Width = 295, DropDownStyle = ComboBoxStyle.DropDownList };

            grpType.Controls.Add(lblSecType);
            grpType.Controls.Add(_cmbSectionViewType);
            page.Controls.Add(grpType);

            // Group 1: Mặt cắt dọc & Template riêng
            var grpLong = new GroupBox
            {
                Text = "Mặt Cắt Dọc (Longitudinal Section)",
                Left = 10,
                Top = 70,
                Width = 470,
                Height = 105
            };
            KhimUiStyle.ApplyCardStyle(grpLong);

            _chkCreateLongitudinal = new CheckBox
            {
                Text = "Tạo mặt cắt dọc theo trục tim cấu kiện",
                Left = 15,
                Top = 22,
                Width = 260,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var lblLongScale = new Label { Text = "Tỷ lệ (1:X):", Left = 290, Top = 23, AutoSize = true };
            _numLongitudinalScale = new NumericUpDown
            {
                Left = 365,
                Top = 20,
                Width = 85,
                Minimum = 1,
                Maximum = 500,
                Value = 50
            };

            var lblLongTpl = new Label { Text = "View Template Mặt Cắt Dọc:", Left = 15, Top = 60, AutoSize = true, ForeColor = KhimUiStyle.PrimaryButtonBg };
            _cmbLongitudinalTemplate = new ComboBox { Left = 180, Top = 57, Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };

            grpLong.Controls.Add(_chkCreateLongitudinal);
            grpLong.Controls.Add(lblLongScale);
            grpLong.Controls.Add(_numLongitudinalScale);
            grpLong.Controls.Add(lblLongTpl);
            grpLong.Controls.Add(_cmbLongitudinalTemplate);
            page.Controls.Add(grpLong);

            // Group 2: Mặt cắt ngang & Template riêng
            var grpCross = new GroupBox
            {
                Text = "Mặt Cắt Ngang (Cross Section)",
                Left = 10,
                Top = 180,
                Width = 470,
                Height = 340
            };
            KhimUiStyle.ApplyCardStyle(grpCross);

            _chkCreateCrossSection = new CheckBox
            {
                Text = "Tạo các mặt cắt ngang qua thân cấu kiện",
                Left = 15,
                Top = 22,
                Width = 260,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var lblCrossScale = new Label { Text = "Tỷ lệ (1:X):", Left = 290, Top = 23, AutoSize = true };
            _numCrossSectionScale = new NumericUpDown
            {
                Left = 365,
                Top = 20,
                Width = 85,
                Minimum = 1,
                Maximum = 500,
                Value = 20
            };

            var lblCrossTpl = new Label { Text = "View Template Mặt Cắt Ngang:", Left = 15, Top = 58, AutoSize = true, ForeColor = KhimUiStyle.PrimaryButtonBg };
            _cmbCrossSectionTemplate = new ComboBox { Left = 180, Top = 55, Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblMode = new Label { Text = "Phương pháp xác định vị trí cắt ngang:", Left = 15, Top = 95, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            _rdModeKeyPositions = new RadioButton
            {
                Text = "⭐ Vị trí đặc trưng tự động (Gối trái 15%, Giữa nhịp 50%, Gối phải 85%)",
                Left = 15,
                Top = 120,
                Width = 440,
                Checked = true
            };

            _rdModeRelative = new RadioButton
            {
                Text = "📐 Theo danh sách vị trí % (cách nhau bởi dấu phẩy):",
                Left = 15,
                Top = 150,
                Width = 440
            };

            _txtRelativePositions = new TextBox
            {
                Left = 35,
                Top = 175,
                Width = 415,
                Text = "15, 50, 85"
            };

            _rdModeSpacing = new RadioButton
            {
                Text = "📏 Cắt theo khoảng cách đều cố định (Spacing):",
                Left = 15,
                Top = 210,
                Width = 400
            };

            var lblStep = new Label { Text = "Bước cắt (mm):", Left = 35, Top = 240, AutoSize = true };
            _numSpacing = new NumericUpDown
            {
                Left = 135,
                Top = 237,
                Width = 100,
                Minimum = 200,
                Maximum = 10000,
                Value = 1000,
                Increment = 100
            };

            var lblTips = new Label
            {
                Text = "💡 Mẹo: Bạn có thể gán các View Template khác nhau cho Mặt cắt dọc (thể hiện trục/level) và Mặt cắt ngang (thể hiện chi tiết đai, hatch, cốt thép).",
                Left = 15,
                Top = 275,
                Width = 435,
                Height = 55,
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            grpCross.Controls.Add(_chkCreateCrossSection);
            grpCross.Controls.Add(lblCrossScale);
            grpCross.Controls.Add(_numCrossSectionScale);
            grpCross.Controls.Add(lblCrossTpl);
            grpCross.Controls.Add(_cmbCrossSectionTemplate);
            grpCross.Controls.Add(lblMode);
            grpCross.Controls.Add(_rdModeKeyPositions);
            grpCross.Controls.Add(_rdModeRelative);
            grpCross.Controls.Add(_txtRelativePositions);
            grpCross.Controls.Add(_rdModeSpacing);
            grpCross.Controls.Add(lblStep);
            grpCross.Controls.Add(_numSpacing);
            grpCross.Controls.Add(lblTips);
            page.Controls.Add(grpCross);
        }

        private void BuildTabCrop(TabPage page)
        {
            var grpCrop = new GroupBox
            {
                Text = "Bù Trừ Vùng Nhìn & Độ Sâu (Crop Box & Far Clip)",
                Left = 10,
                Top = 10,
                Width = 470,
                Height = 240
            };
            KhimUiStyle.ApplyCardStyle(grpCrop);

            var lblOffL = new Label { Text = "Lề Trái (mm):", Left = 20, Top = 35, AutoSize = true };
            _numCropOffsetLeft = new NumericUpDown { Left = 120, Top = 32, Width = 90, Minimum = 0, Maximum = 2000, Value = 200, Increment = 50 };

            var lblOffR = new Label { Text = "Lề Phải (mm):", Left = 240, Top = 35, AutoSize = true };
            _numCropOffsetRight = new NumericUpDown { Left = 340, Top = 32, Width = 90, Minimum = 0, Maximum = 2000, Value = 200, Increment = 50 };

            var lblOffT = new Label { Text = "Lề Trên (mm):", Left = 20, Top = 75, AutoSize = true };
            _numCropOffsetTop = new NumericUpDown { Left = 120, Top = 72, Width = 90, Minimum = 0, Maximum = 2000, Value = 200, Increment = 50 };

            var lblOffB = new Label { Text = "Lề Dưới (mm):", Left = 240, Top = 75, AutoSize = true };
            _numCropOffsetBottom = new NumericUpDown { Left = 340, Top = 72, Width = 90, Minimum = 0, Maximum = 2000, Value = 200, Increment = 50 };

            var lblFar = new Label { Text = "Độ sâu nhìn Far Clip (mm):", Left = 20, Top = 120, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _numFarClipOffset = new NumericUpDown { Left = 200, Top = 117, Width = 90, Minimum = 50, Maximum = 5000, Value = 150, Increment = 50 };

            var lblFarDesc = new Label
            {
                Text = "Khống chế độ sâu nhìn giúp mặt cắt không bị nhìn xuyên thấu qua các cấu kiện phía sau gây rối bản vẽ kết cấu.",
                Left = 20,
                Top = 155,
                Width = 430,
                Height = 65,
                ForeColor = KhimUiStyle.TextSecondary
            };

            grpCrop.Controls.Add(lblOffL);
            grpCrop.Controls.Add(_numCropOffsetLeft);
            grpCrop.Controls.Add(lblOffR);
            grpCrop.Controls.Add(_numCropOffsetRight);
            grpCrop.Controls.Add(lblOffT);
            grpCrop.Controls.Add(_numCropOffsetTop);
            grpCrop.Controls.Add(lblOffB);
            grpCrop.Controls.Add(_numCropOffsetBottom);
            grpCrop.Controls.Add(lblFar);
            grpCrop.Controls.Add(_numFarClipOffset);
            grpCrop.Controls.Add(lblFarDesc);
            page.Controls.Add(grpCrop);

            var grpTemplate = new GroupBox
            {
                Text = "Quản Lý View Template Toàn Diện",
                Left = 10,
                Top = 260,
                Width = 470,
                Height = 250
            };
            KhimUiStyle.ApplyCardStyle(grpTemplate);

            _chkApplyViewTemplate = new CheckBox
            {
                Text = "Bật tự động gán View Template cho các View vừa tạo",
                Left = 20,
                Top = 30,
                Width = 380,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var lblVft = new Label { Text = "View Template chung (khi không đặt riêng):", Left = 20, Top = 65, AutoSize = true };
            _cmbViewTemplateGeneral = new ComboBox { Left = 20, Top = 88, Width = 310, DropDownStyle = ComboBoxStyle.DropDownList };

            _btnRefreshTemplates = new Button { Text = "🔄 Làm mới", Left = 340, Top = 86, Width = 100, Height = 26 };
            KhimUiStyle.ApplySecondaryButton(_btnRefreshTemplates);
            _btnRefreshTemplates.Click += (s, e) =>
            {
                PopulateViewTemplates();
                KhimDialogHelper.ShowInfo("Đã làm mới danh sách View Template từ Revit!");
            };

            _chkFineDetail = new CheckBox
            {
                Text = "Tự động đặt Detail Level = Fine (để hiện rõ cốt thép 3D)",
                Left = 20,
                Top = 130,
                Width = 400,
                Checked = true
            };

            _chkHideCrop = new CheckBox
            {
                Text = "Ẩn đường viền Crop Box sau khi tạo xong view",
                Left = 20,
                Top = 165,
                Width = 400,
                Checked = false
            };

            grpTemplate.Controls.Add(_chkApplyViewTemplate);
            grpTemplate.Controls.Add(lblVft);
            grpTemplate.Controls.Add(_cmbViewTemplateGeneral);
            grpTemplate.Controls.Add(_btnRefreshTemplates);
            grpTemplate.Controls.Add(_chkFineDetail);
            grpTemplate.Controls.Add(_chkHideCrop);
            page.Controls.Add(grpTemplate);
        }

        private void BuildTabNaming(TabPage page)
        {
            var grpNaming = new GroupBox
            {
                Text = "Cấu Trúc Đặt Tên Tự Động (Naming Patterns)",
                Left = 10,
                Top = 10,
                Width = 470,
                Height = 240
            };
            KhimUiStyle.ApplyCardStyle(grpNaming);

            var lblDocPattern = new Label { Text = "Pattern Mặt Cắt Dọc:", Left = 20, Top = 30, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtPatternLongitudinal = new TextBox { Left = 20, Top = 52, Width = 425, Text = "MC-D-{Mark}" };

            var lblNgangPattern = new Label { Text = "Pattern Mặt Cắt Ngang:", Left = 20, Top = 90, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtPatternCross = new TextBox { Left = 20, Top = 112, Width = 425, Text = "MC-N-{Mark}-{Index}" };

            var lblTokens = new Label
            {
                Text = "Các Placeholder hợp lệ:\n" +
                       "• {Mark}: Ký hiệu cấu kiện\n" +
                       "• {Type}: Tên tiết diện Type\n" +
                       "• {Category}: Tên loại (Dầm, Cột, Vách...)\n" +
                       "• {Index}: Thứ tự 01, 02, 03...\n" +
                       "• {Level}: Tên tầng\n" +
                       "• {Pos}: Vị trí (Goi-Trai, Giua-Nhip...)",
                Left = 20,
                Top = 145,
                Width = 430,
                Height = 90,
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            grpNaming.Controls.Add(lblDocPattern);
            grpNaming.Controls.Add(_txtPatternLongitudinal);
            grpNaming.Controls.Add(lblNgangPattern);
            grpNaming.Controls.Add(_txtPatternCross);
            grpNaming.Controls.Add(lblTokens);
            page.Controls.Add(grpNaming);

            var grpTpl = new GroupBox
            {
                Text = "Quản Lý Mẫu Thiết Lập (Template JSON)",
                Left = 10,
                Top = 260,
                Width = 470,
                Height = 240
            };
            KhimUiStyle.ApplyCardStyle(grpTpl);

            var lblTplList = new Label { Text = "Danh sách mẫu đã lưu:", Left = 20, Top = 30, AutoSize = true };
            _cmbTemplate = new ComboBox { Left = 20, Top = 52, Width = 425, DropDownStyle = ComboBoxStyle.DropDownList };

            _btnSaveTemplate = new Button { Text = "💾 Lưu Mẫu Hiện Tại", Left = 20, Top = 95, Width = 150, Height = 32 };
            _btnApplyTemplate = new Button { Text = "📂 Áp Dụng Mẫu", Left = 180, Top = 95, Width = 140, Height = 32 };
            _btnDeleteTemplate = new Button { Text = "🗑️ Xóa", Left = 330, Top = 95, Width = 95, Height = 32 };

            KhimUiStyle.ApplySecondaryButton(_btnSaveTemplate);
            KhimUiStyle.ApplySecondaryButton(_btnApplyTemplate);
            KhimUiStyle.ApplySecondaryButton(_btnDeleteTemplate);

            _btnSaveTemplate.Click += (s, e) => SaveCurrentTemplate();
            _btnApplyTemplate.Click += (s, e) => ApplySelectedTemplate();
            _btnDeleteTemplate.Click += (s, e) => DeleteSelectedTemplate();

            grpTpl.Controls.Add(lblTplList);
            grpTpl.Controls.Add(_cmbTemplate);
            grpTpl.Controls.Add(_btnSaveTemplate);
            grpTpl.Controls.Add(_btnApplyTemplate);
            grpTpl.Controls.Add(_btnDeleteTemplate);
            page.Controls.Add(grpTpl);
        }

        private void PopulateCategoryFilter()
        {
            _cmbCategoryFilter.Items.Clear();
            _cmbCategoryFilter.Items.Add("⭐ Chỉ cấu kiện đã chọn (Selected)");
            _cmbCategoryFilter.Items.Add("Tất cả cấu kiện (All)");
            _cmbCategoryFilter.Items.Add("Dầm (Structural Framing)");
            _cmbCategoryFilter.Items.Add("Cột (Structural Columns)");
            _cmbCategoryFilter.Items.Add("Vách / Tường (Walls)");
            _cmbCategoryFilter.Items.Add("Sàn (Floors)");
            _cmbCategoryFilter.Items.Add("Móng (Structural Foundations)");
            _cmbCategoryFilter.SelectedIndex = _allItems.Any(i => i.IsSelected) ? 0 : 1;
        }

        private void PopulateSectionViewTypes()
        {
            _cmbSectionViewType.Items.Clear();
            var types = _generator.GetAvailableSectionViewTypes();
            foreach (var t in types) _cmbSectionViewType.Items.Add(t);
            if (_cmbSectionViewType.Items.Count > 0) _cmbSectionViewType.SelectedIndex = 0;
        }

        private void PopulateViewTemplates()
        {
            string curLong = _cmbLongitudinalTemplate?.SelectedItem?.ToString();
            string curCross = _cmbCrossSectionTemplate?.SelectedItem?.ToString();
            string curGen = _cmbViewTemplateGeneral?.SelectedItem?.ToString();

            var templates = _generator.GetAvailableViewTemplates();

            // 1. Longitudinal Template ComboBox
            _cmbLongitudinalTemplate.Items.Clear();
            _cmbLongitudinalTemplate.Items.Add("<Không áp dụng / None>");
            foreach (var t in templates) _cmbLongitudinalTemplate.Items.Add(t);
            if (!string.IsNullOrEmpty(curLong) && _cmbLongitudinalTemplate.Items.Contains(curLong))
                _cmbLongitudinalTemplate.SelectedItem = curLong;
            else
                _cmbLongitudinalTemplate.SelectedIndex = 0;

            // 2. Cross Section Template ComboBox
            _cmbCrossSectionTemplate.Items.Clear();
            _cmbCrossSectionTemplate.Items.Add("<Không áp dụng / None>");
            foreach (var t in templates) _cmbCrossSectionTemplate.Items.Add(t);
            if (!string.IsNullOrEmpty(curCross) && _cmbCrossSectionTemplate.Items.Contains(curCross))
                _cmbCrossSectionTemplate.SelectedItem = curCross;
            else
                _cmbCrossSectionTemplate.SelectedIndex = 0;

            // 3. General Template ComboBox
            _cmbViewTemplateGeneral.Items.Clear();
            _cmbViewTemplateGeneral.Items.Add("<Không áp dụng / None>");
            foreach (var t in templates) _cmbViewTemplateGeneral.Items.Add(t);
            if (!string.IsNullOrEmpty(curGen) && _cmbViewTemplateGeneral.Items.Contains(curGen))
                _cmbViewTemplateGeneral.SelectedItem = curGen;
            else
                _cmbViewTemplateGeneral.SelectedIndex = 0;
        }

        private void RefreshGrid()
        {
            _gridElements.Rows.Clear();
            string filter = _cmbCategoryFilter.SelectedItem?.ToString() ?? "";

            foreach (var item in _allItems)
            {
                if (filter.Contains("Selected") && !item.IsSelected) continue;
                if (filter.Contains("Framing") && !item.CategoryName.Contains("Framing") && !item.CategoryName.Contains("Dầm")) continue;
                if (filter.Contains("Columns") && !item.CategoryName.Contains("Column") && !item.CategoryName.Contains("Cột")) continue;
                if (filter.Contains("Walls") && !item.CategoryName.Contains("Wall") && !item.CategoryName.Contains("Tường")) continue;
                if (filter.Contains("Floors") && !item.CategoryName.Contains("Floor") && !item.CategoryName.Contains("Sàn")) continue;
                if (filter.Contains("Foundations") && !item.CategoryName.Contains("Foundation") && !item.CategoryName.Contains("Móng")) continue;

                int rowIdx = _gridElements.Rows.Add();
                var row = _gridElements.Rows[rowIdx];
                row.Tag = item;
                row.Cells["colCheck"].Value = item.IsSelected;
                row.Cells["colMark"].Value = item.Mark;
                row.Cells["colType"].Value = item.TypeName;
                row.Cells["colCat"].Value = item.CategoryName;
                row.Cells["colLen"].Value = item.LengthMm > 0 ? item.LengthMm.ToString("N0") : "-";
            }

            UpdateSelectedCount();
        }

        private void BtnPickRevit_Click(object sender, EventArgs e)
        {
            if (_uidoc == null) return;
            try
            {
                this.Hide();
                var selectionFilter = new KhimTools.SectionCutTool.Commands.StructuralElementSelectionFilter();
                string promptMsg = LanguageManager.IsEnglish
                    ? "Select structural elements to cut sections (Click Finish on Options Bar)..."
                    : "Chọn các cấu kiện cần cắt Section (Bấm Finish trên thanh Options Bar)...";

                var pickedRefs = _uidoc.Selection.PickObjects(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    selectionFilter,
                    promptMsg);

                if (pickedRefs != null && pickedRefs.Any())
                {
                    var pickedIds = new HashSet<long>(pickedRefs.Select(r => r.ElementId.ToLongValue()));
                    foreach (var item in _allItems)
                    {
                        if (pickedIds.Contains(item.Element.Id.ToLongValue()))
                        {
                            item.IsSelected = true;
                        }
                    }

                    // Thêm phần tử mới nếu chưa có trong _allItems
                    foreach (var r in pickedRefs)
                    {
                        long eid = r.ElementId.ToLongValue();
                        if (!_allItems.Any(i => i.Element.Id.ToLongValue() == eid))
                        {
                            Element el = _doc.GetElement(r);
                            if (el != null)
                            {
                                _allItems.Add(new ElementCutItem(el) { IsSelected = true });
                            }
                        }
                    }

                    // Sắp xếp lại đưa các phần tử được chọn lên đầu
                    var sorted = _allItems.OrderByDescending(i => i.IsSelected).ThenBy(i => i.CategoryName).ThenBy(i => i.Mark).ToList();
                    _allItems.Clear();
                    _allItems.AddRange(sorted);

                    _cmbCategoryFilter.SelectedIndex = 0; // Filter to selected
                    RefreshGrid();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User pressed ESC
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError($"Lỗi khi pick chọn: {ex.Message}");
            }
            finally
            {
                this.Show();
                this.BringToFront();
            }
        }

        private void SetAllSelection(bool select)
        {
            for (int i = 0; i < _gridElements.Rows.Count; i++)
            {
                _gridElements.Rows[i].Cells["colCheck"].Value = select;
                if (_gridElements.Rows[i].Tag is ElementCutItem item)
                {
                    item.IsSelected = select;
                }
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int count = 0;
            for (int i = 0; i < _gridElements.Rows.Count; i++)
            {
                bool isChecked = Convert.ToBoolean(_gridElements.Rows[i].Cells["colCheck"].Value);
                if (_gridElements.Rows[i].Tag is ElementCutItem item)
                {
                    item.IsSelected = isChecked;
                }
                if (isChecked) count++;
            }
            _lblSelectedCount.Text = $"Đã chọn: {count} / {_gridElements.Rows.Count} cấu kiện";
        }

        private SectionCutSettings CollectSettingsFromUi()
        {
            var s = new SectionCutSettings
            {
                SectionViewTypeName = _cmbSectionViewType.SelectedItem?.ToString() ?? "",
                CreateLongitudinal = _chkCreateLongitudinal.Checked,
                LongitudinalScale = (int)_numLongitudinalScale.Value,
                CreateCrossSection = _chkCreateCrossSection.Checked,
                CrossSectionScale = (int)_numCrossSectionScale.Value,

                CropOffsetLeftMm = (double)_numCropOffsetLeft.Value,
                CropOffsetRightMm = (double)_numCropOffsetRight.Value,
                CropOffsetTopMm = (double)_numCropOffsetTop.Value,
                CropOffsetBottomMm = (double)_numCropOffsetBottom.Value,
                FarClipOffsetMm = (double)_numFarClipOffset.Value,

                ApplyViewTemplate = _chkApplyViewTemplate.Checked,
                ViewTemplateName = _cmbViewTemplateGeneral.SelectedIndex > 0 ? _cmbViewTemplateGeneral.SelectedItem.ToString() : "",
                LongitudinalViewTemplateName = _cmbLongitudinalTemplate.SelectedIndex > 0 ? _cmbLongitudinalTemplate.SelectedItem.ToString() : "",
                CrossSectionViewTemplateName = _cmbCrossSectionTemplate.SelectedIndex > 0 ? _cmbCrossSectionTemplate.SelectedItem.ToString() : "",

                SetFineDetailLevel = _chkFineDetail.Checked,
                HideCropRegionAfterCreation = _chkHideCrop.Checked,

                LongitudinalNamingPattern = _txtPatternLongitudinal.Text.Trim(),
                CrossSectionNamingPattern = _txtPatternCross.Text.Trim()
            };

            if (_rdModeSpacing.Checked)
            {
                s.CrossSectionMode = CrossSectionCutMode.FixedSpacing;
                s.SpacingMm = (double)_numSpacing.Value;
            }
            else if (_rdModeRelative.Checked)
            {
                s.CrossSectionMode = CrossSectionCutMode.RelativePositions;
                s.RelativePositions = ParseRelativeRatios(_txtRelativePositions.Text);
            }
            else
            {
                s.CrossSectionMode = CrossSectionCutMode.KeyPositionsAuto;
                s.RelativePositions = new List<double> { 0.15, 0.50, 0.85 };
            }

            return s;
        }

        private void ApplySettingsToUi(SectionCutSettings s)
        {
            if (s == null) return;

            if (!string.IsNullOrEmpty(s.SectionViewTypeName))
            {
                int idx = _cmbSectionViewType.FindStringExact(s.SectionViewTypeName);
                if (idx >= 0) _cmbSectionViewType.SelectedIndex = idx;
            }

            _chkCreateLongitudinal.Checked = s.CreateLongitudinal;
            _numLongitudinalScale.Value = Math.Max(1, Math.Min(500, s.LongitudinalScale));
            _chkCreateCrossSection.Checked = s.CreateCrossSection;
            _numCrossSectionScale.Value = Math.Max(1, Math.Min(500, s.CrossSectionScale));

            if (s.CrossSectionMode == CrossSectionCutMode.FixedSpacing)
            {
                _rdModeSpacing.Checked = true;
                _numSpacing.Value = (decimal)Math.Max(200, Math.Min(10000, s.SpacingMm));
            }
            else if (s.CrossSectionMode == CrossSectionCutMode.RelativePositions && s.RelativePositions != null)
            {
                _rdModeRelative.Checked = true;
                _txtRelativePositions.Text = string.Join(", ", s.RelativePositions.Select(r => (r * 100).ToString("0")));
            }
            else
            {
                _rdModeKeyPositions.Checked = true;
            }

            _numCropOffsetLeft.Value = (decimal)s.CropOffsetLeftMm;
            _numCropOffsetRight.Value = (decimal)s.CropOffsetRightMm;
            _numCropOffsetTop.Value = (decimal)s.CropOffsetTopMm;
            _numCropOffsetBottom.Value = (decimal)s.CropOffsetBottomMm;
            _numFarClipOffset.Value = (decimal)s.FarClipOffsetMm;

            _txtPatternLongitudinal.Text = s.LongitudinalNamingPattern ?? "MC-D-{Mark}";
            _txtPatternCross.Text = s.CrossSectionNamingPattern ?? "MC-N-{Mark}-{Index}";

            _chkApplyViewTemplate.Checked = s.ApplyViewTemplate;
            _chkFineDetail.Checked = s.SetFineDetailLevel;
            _chkHideCrop.Checked = s.HideCropRegionAfterCreation;

            if (!string.IsNullOrEmpty(s.LongitudinalViewTemplateName))
            {
                int idx = _cmbLongitudinalTemplate.FindStringExact(s.LongitudinalViewTemplateName);
                if (idx >= 0) _cmbLongitudinalTemplate.SelectedIndex = idx;
            }

            if (!string.IsNullOrEmpty(s.CrossSectionViewTemplateName))
            {
                int idx = _cmbCrossSectionTemplate.FindStringExact(s.CrossSectionViewTemplateName);
                if (idx >= 0) _cmbCrossSectionTemplate.SelectedIndex = idx;
            }

            if (!string.IsNullOrEmpty(s.ViewTemplateName))
            {
                int idx = _cmbViewTemplateGeneral.FindStringExact(s.ViewTemplateName);
                if (idx >= 0) _cmbViewTemplateGeneral.SelectedIndex = idx;
            }
        }

        private List<double> ParseRelativeRatios(string text)
        {
            var list = new List<double>();
            if (string.IsNullOrWhiteSpace(text)) return new List<double> { 0.15, 0.50, 0.85 };

            var parts = text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (double.TryParse(p.Trim().Replace("%", ""), out double val))
                {
                    if (val > 1.0) val = val / 100.0;
                    if (val >= 0.0 && val <= 1.0 && !list.Contains(val)) list.Add(val);
                }
            }
            return list.Any() ? list : new List<double> { 0.15, 0.50, 0.85 };
        }

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            UpdateSelectedCount();
            var selected = _allItems.Where(i => i.IsSelected).ToList();
            if (!selected.Any())
            {
                KhimDialogHelper.ShowWarning(LanguageManager.IsEnglish
                    ? "Please select at least 1 element from the table."
                    : "Vui lòng chọn ít nhất 1 cấu kiện trong bảng.");
                return;
            }

            var settings = CollectSettingsFromUi();
            int longCount = 0;
            int crossCount = 0;

            foreach (var item in selected)
            {
                var placements = SectionGeometryHelper.CalculateSectionPlacements(item.Element, settings);
                longCount += placements.Count(p => p.IsLongitudinal);
                crossCount += placements.Count(p => !p.IsLongitudinal);
            }

            int total = longCount + crossCount;
            string msg = LanguageManager.IsEnglish
                ? $"Preview Estimate:\n\n• Selected Elements: {selected.Count}\n• Longitudinal Sections: {longCount}\n• Cross Sections: {crossCount}\n\n👉 Total Views to be created: {total}"
                : $"Dự kiến số lượng ViewSection sẽ tạo:\n\n• Số cấu kiện đã chọn: {selected.Count}\n• Mặt cắt dọc: {longCount} view\n• Mặt cắt ngang: {crossCount} view\n\n👉 Tổng cộng: {total} ViewSection";

            KhimDialogHelper.ShowInfo(msg);
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            UpdateSelectedCount();
            var selected = _allItems.Where(i => i.IsSelected).ToList();
            if (!selected.Any())
            {
                KhimDialogHelper.ShowWarning(LanguageManager.IsEnglish
                    ? "Please select at least 1 element to generate section views."
                    : "Vui lòng chọn ít nhất 1 cấu kiện để tạo mặt cắt.");
                return;
            }

            var settings = CollectSettingsFromUi();

            try
            {
                Cursor = Cursors.WaitCursor;
                _btnGenerate.Enabled = false;
                _btnGenerate.Text = LanguageManager.IsEnglish ? "Generating..." : "Đang xử lý...";

                var report = _generator.GenerateSections(selected, settings, (curr, total, label) =>
                {
                    _btnGenerate.Text = $"({curr}/{total}) {label}";
                    Application.DoEvents();
                });

                Cursor = Cursors.Default;
                _btnGenerate.Enabled = true;
                _btnGenerate.Text = LanguageManager.IsEnglish ? "⚡ CREATE BATCH SECTIONS" : "⚡ TẠO MẶT CẮT HÀNG LOẠT";

                var createdViews = report.Items.Where(x => x.Success && x.CreatedView != null).Select(x => x.CreatedView).ToList();
                if (_uidoc != null && createdViews.Any())
                {
                    try
                    {
                        // Highlight các View vừa tạo trong Project Browser
                        _uidoc.Selection.SetElementIds(createdViews.Select(v => v.Id).ToList());
                        // Tự động mở mặt cắt đầu tiên để người dùng kiểm tra ngay
                        _uidoc.ActiveView = createdViews.First();
                    }
                    catch { }
                }

                string viewNames = createdViews.Any()
                    ? string.Join("\n", createdViews.Take(6).Select(v => $"  ✔ {v.Name}")) + (createdViews.Count > 6 ? $"\n  ... và {createdViews.Count - 6} mặt cắt khác." : "")
                    : "";

                string summaryMsg = LanguageManager.IsEnglish
                    ? $"Section Generation Finished!\n\n• Successfully Created: {report.SuccessCount} ViewSections\n• Failures: {report.FailureCount}\n• Elements Processed: {selected.Count}"
                    : $"Hoàn tất quá trình tạo mặt cắt tự động!\n\n• Tạo thành công: {report.SuccessCount} ViewSection\n• Gặp lỗi: {report.FailureCount}\n• Số cấu kiện xử lý: {selected.Count}";

                if (!string.IsNullOrEmpty(viewNames))
                {
                    summaryMsg += LanguageManager.IsEnglish
                        ? $"\n\nCreated Views:\n{viewNames}\n\n(The first created section view has been opened in Revit!)"
                        : $"\n\nDanh sách mặt cắt đã tạo:\n{viewNames}\n\n(Đã tự động mở mặt cắt đầu tiên trên Revit để bạn kiểm tra!)";
                }

                if (report.FailureCount > 0 && report.Items.Any(x => !x.Success))
                {
                    var errs = string.Join("\n", report.Items.Where(x => !x.Success).Select(x => $"• {x.ViewName}: {x.ErrorMessage}").Take(5));
                    summaryMsg += LanguageManager.IsEnglish ? $"\n\nError details:\n{errs}" : $"\n\nChi tiết lỗi:\n{errs}";
                }

                if (report.FailureCount > 0 && report.SuccessCount == 0)
                {
                    KhimDialogHelper.ShowWarning(summaryMsg);
                }
                else
                {
                    KhimDialogHelper.ShowInfo(summaryMsg);
                }

                if (report.SuccessCount > 0)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                _btnGenerate.Enabled = true;
                _btnGenerate.Text = LanguageManager.IsEnglish ? "⚡ CREATE BATCH SECTIONS" : "⚡ TẠO MẶT CẮT HÀNG LOẠT";
                KhimDialogHelper.ShowError(
                    LanguageManager.IsEnglish ? "Error Creating Sections" : "Lỗi Tạo Mặt Cắt",
                    ex.Message,
                    ex.StackTrace);
            }
        }

        // ── Template JSON Management ───────────────────────────────────────────
        private void LoadTemplateList()
        {
            _cmbTemplate.Items.Clear();
            var list = SectionTemplateManager.ListTemplates();
            foreach (var t in list) _cmbTemplate.Items.Add(t);
            if (_cmbTemplate.Items.Count > 0) _cmbTemplate.SelectedIndex = 0;
        }

        private void SaveCurrentTemplate()
        {
            string name = KhimPrompt.ShowDialog(
                LanguageManager.IsEnglish ? "Enter template name:" : "Nhập tên mẫu cấu hình mặt cắt:",
                LanguageManager.IsEnglish ? "Save Template" : "Lưu Mẫu Thiết Lập",
                "Standard_Section");

            if (string.IsNullOrWhiteSpace(name)) return;

            var settings = CollectSettingsFromUi();
            settings.Name = name.Trim();
            SectionTemplateManager.SaveTemplate(settings);
            LoadTemplateList();
            _cmbTemplate.Text = settings.Name;

            KhimDialogHelper.ShowInfo(LanguageManager.IsEnglish ? "Template saved successfully!" : "Đã lưu mẫu thiết lập thành công!");
        }

        private void ApplySelectedTemplate()
        {
            string name = _cmbTemplate.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            var settings = SectionTemplateManager.LoadTemplate(name);
            if (settings != null)
            {
                ApplySettingsToUi(settings);
                KhimDialogHelper.ShowInfo(LanguageManager.IsEnglish ? "Template applied!" : "Đã áp dụng mẫu thiết lập!");
            }
        }

        private void DeleteSelectedTemplate()
        {
            string name = _cmbTemplate.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            SectionTemplateManager.DeleteTemplate(name);
            LoadTemplateList();
        }

        private void ApplyLanguage()
        {
            bool en = LanguageManager.IsEnglish;
            _tabTypes.Text = en ? "1. Section Types & Templates" : "1. Loại Mặt Cắt & Template";
            _tabCrop.Text = en ? "2. Crop Box & Templates Detail" : "2. Crop Box & Template Chi Tiết";
            _tabNaming.Text = en ? "3. Naming & Templates" : "3. Đặt Tên & Mẫu JSON";

            _btnSelectAll.Text = en ? "Select All" : "Tất cả";
            _btnDeselectAll.Text = en ? "Deselect" : "Bỏ chọn";
            _btnPreview.Text = en ? "👁️ Preview Count" : "👁️ Xem trước số view";
            _btnGenerate.Text = en ? "⚡ GENERATE SECTIONS" : "⚡ TẠO MẶT CẮT HÀNG LOẠT";
            _btnClose.Text = en ? "Close" : "Đóng";
        }
    }
}

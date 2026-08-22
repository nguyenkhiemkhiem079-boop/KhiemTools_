using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.SheetExport.Models;
using KhimTools.SheetExport.Services;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using ComboBox = System.Windows.Forms.ComboBox;
using CheckBox = System.Windows.Forms.CheckBox;
using RadioButton = System.Windows.Forms.RadioButton;
using GroupBox = System.Windows.Forms.GroupBox;
using Form = System.Windows.Forms.Form;

namespace KhimTools.SheetExport.Forms
{
    public class SheetExportForm : Form
    {
        private readonly Document _doc;
        private List<SheetExportItem> _allSheetItems = new List<SheetExportItem>();
        private List<SheetExportItem> _filteredSheetItems = new List<SheetExportItem>();
        private readonly ExportOptions _options = new ExportOptions();
        private bool _isLoading = false;

        // ── Navigation Sidebar ───────────────────────────────────────────────
        private Panel _sidebar;
        private Button _btnNavSelect;
        private Button _btnNavSettings;
        private Button _btnNavFilter;
        private Panel _contentContainer;

        // ── Main Views ───────────────────────────────────────────────────────
        private Panel _viewSelect;
        private Panel _viewSettings;
        private Panel _viewFilter;

        // ── View 1: Select UI Elements ───────────────────────────────────────
        private ComboBox _cmbSheetSet;
        private Button _btnUpdateSet;
        private Button _btnSaveSet;
        private TextBox _txtSheetSetName;
        private ComboBox _cmbDisciplineFilter;
        private TextBox _txtSearchSheet;
        private Button _btnRefreshList;
        private Button _btnSelectAll;
        private Button _btnClearAll;
        private Button _btnInvert;

        // File Settings & DataGrid
        private RadioButton _rbSeparateFiles;
        private RadioButton _rbCombineFiles;
        private CheckBox _chkUseNamingConvention;
        private TextBox _txtNaming1;
        private TextBox _txtNaming2;
        private TextBox _txtNaming3;
        private TextBox _txtFileCombineName;
        private CheckBox _chkExportPdf;
        private CheckBox _chkExportDwg;
        private CheckBox _chkCreateTransmittal;
        private DataGridView _gridSheets;

        // ── View 2: Settings UI Elements ─────────────────────────────────────
        private TabControl _tabSettings;
        // PDF Settings
        private RadioButton _rbPlacementCenter;
        private RadioButton _rbPlacementOffset;
        private RadioButton _rbMarginNoMargin;
        private RadioButton _rbMarginPrinterLimit;
        private RadioButton _rbMarginUserDefined;
        private NumericUpDown _numMarginX;
        private NumericUpDown _numMarginY;
        private RadioButton _rbZoomFitToPage;
        private RadioButton _rbZoomPercent;
        private NumericUpDown _numZoomPercent;
        private ComboBox _cmbPrinter;
        private RadioButton _rbVectorProcessing;
        private RadioButton _rbRasterProcessing;
        private ComboBox _cmbRasterQuality;
        private ComboBox _cmbColorDepth;
        private CheckBox _chkViewLinksInBlue;
        private CheckBox _chkHideRefPlanes;
        private CheckBox _chkHideUnreferencedTags;
        private CheckBox _chkHideScopeBoxes;
        private CheckBox _chkHideCropBoundaries;
        private CheckBox _chkReplaceHalftone;
        private CheckBox _chkMaskCoincidentLines;

        // DWG Settings
        private ComboBox _cmbDwgSetup;
        private ComboBox _cmbAutoCadVersion;
        private CheckBox _chkDwgMergedViews;

        // ── View 3: Filter UI Elements ───────────────────────────────────────
        private CheckBox _chkFilterModifiedOnly;

        // ── Bottom Bar UI Elements (Print Bar) ───────────────────────────────
        private RadioButton _rbSaveSameFolder;
        private RadioButton _rbSplitFoldersByFormat;
        private NumericUpDown _numTimeoutSeconds;
        private Button _btnOpenFolderSelection;
        private TextBox _txtOutputDirectory;
        private Button _btnBrowseFolder;
        private Label _lblTotalSummary;
        private Button _btnPrint;

        public SheetExportForm(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            KhimUiStyle.ApplyFormTheme(this);
            InitializeComponentsCustom();
            LoadDataFromRevit();
        }

        private void InitializeComponentsCustom()
        {
            Text = "📄 K-TOOLS — Sheet Batch Export & Print Manager";
            Width = 1360;
            Height = 820;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(1100, 680);
            BackColor = KhimUiStyle.FormBg;

            // 0. Top Banner Header
            var header = KhimUiStyle.CreateHeaderBanner(
                "K-TOOLS — Sheet Batch Export & Print Manager",
                "Bộ công cụ xuất in PDF & AutoCAD DWG hàng loạt, tự động nhận diện khổ giấy và quản lý bộ bản vẽ",
                "v2.5 Pro");
            Controls.Add(header);

            // 1. Sidebar Navigation (Left)
            BuildSidebar();

            // 2. Content Container (Center)
            _contentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Build Views
            BuildViewSelect();
            BuildViewSettings();
            BuildViewFilter();

            _contentContainer.Controls.Add(_viewSelect);
            _contentContainer.Controls.Add(_viewSettings);
            _contentContainer.Controls.Add(_viewFilter);

            // 3. Bottom Print Bar (Footer)
            var bottomBar = BuildBottomPrintBar();

            Controls.Add(_contentContainer);
            Controls.Add(_sidebar);
            Controls.Add(bottomBar);

            // Switch to default Select view
            SwitchView(0);
        }

        #region Sidebar Navigation
        private void BuildSidebar()
        {
            _sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 80,
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(6, 12, 6, 12)
            };

            _btnNavSelect = CreateSidebarButton("☑\nSelect", 0);
            _btnNavSettings = CreateSidebarButton("⚙\nSettings", 1);
            _btnNavFilter = CreateSidebarButton("🔍\nFilter", 2);

            _btnNavSelect.Top = 15;
            _btnNavSettings.Top = 90;
            _btnNavFilter.Top = 165;

            _sidebar.Controls.Add(_btnNavSelect);
            _sidebar.Controls.Add(_btnNavSettings);
            _sidebar.Controls.Add(_btnNavFilter);
        }

        private Button CreateSidebarButton(string text, int viewIndex)
        {
            var btn = new Button
            {
                Text = text,
                Width = 68,
                Height = 65,
                Left = 6,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => SwitchView(viewIndex);
            return btn;
        }

        private void SwitchView(int viewIndex)
        {
            _viewSelect.Visible = (viewIndex == 0);
            _viewSettings.Visible = (viewIndex == 1);
            _viewFilter.Visible = (viewIndex == 2);

            _btnNavSelect.BackColor = (viewIndex == 0) ? Color.White : Color.Transparent;
            _btnNavSettings.BackColor = (viewIndex == 1) ? Color.White : Color.Transparent;
            _btnNavFilter.BackColor = (viewIndex == 2) ? Color.White : Color.Transparent;

            _btnNavSelect.ForeColor = (viewIndex == 0) ? KhimUiStyle.PrimaryButtonBg : KhimUiStyle.TextSecondary;
            _btnNavSettings.ForeColor = (viewIndex == 1) ? KhimUiStyle.PrimaryButtonBg : KhimUiStyle.TextSecondary;
            _btnNavFilter.ForeColor = (viewIndex == 2) ? KhimUiStyle.PrimaryButtonBg : KhimUiStyle.TextSecondary;
        }
        #endregion

        #region View 1: Select Tab
        private void BuildViewSelect()
        {
            _viewSelect = new Panel { Dock = DockStyle.Fill, BackColor = KhimUiStyle.FormBg };

            // ── TOP CONFIG CARD PANEL ─────────────────────────────────────────
            var pnlTopConfig = new Panel
            {
                Dock = DockStyle.Top,
                Height = 160,
                Padding = new Padding(12, 10, 12, 6),
                BackColor = KhimUiStyle.CardBg
            };

            // Row 1: Separate / Combine Radios + ViewSheetSet dropdown
            var pnlRow1 = new Panel { Dock = DockStyle.Top, Height = 32 };
            _rbSeparateFiles = new RadioButton
            {
                Text = "Tạo từng file riêng biệt (Separate files)",
                Left = 6,
                Top = 4,
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _rbCombineFiles = new RadioButton
            {
                Text = "Gộp chung thành 1 file PDF duy nhất (Combine)",
                Left = 280,
                Top = 4,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var lblSheetSet = new Label
            {
                Text = "ViewSheetSet:",
                Left = 620,
                Top = 7,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextSecondary
            };
            _cmbSheetSet = new ComboBox
            {
                Left = 715,
                Top = 3,
                Width = 210,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };

            _txtSheetSetName = new TextBox { Text = "Set Mới", Left = 935, Top = 4, Width = 110, Font = new Font("Segoe UI", 9F) };
            _btnSaveSet = new Button
            {
                Text = "Lưu Set",
                Left = 1052,
                Top = 3,
                Width = 70,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };

            pnlRow1.Controls.AddRange(new System.Windows.Forms.Control[] { _rbSeparateFiles, _rbCombineFiles, lblSheetSet, _cmbSheetSet, _txtSheetSetName, _btnSaveSet });
            pnlTopConfig.Controls.Add(pnlRow1);

            // Row 2: Naming Convention Flow Panel (Chống chèn chữ 100%)
            var pnlRow2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(2, 4, 2, 2)
            };

            _chkUseNamingConvention = new CheckBox
            {
                Text = "Định dạng tên file:",
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(3, 5, 6, 3)
            };
            _txtNaming1 = new TextBox { Text = DateTime.Now.ToString("yyMMdd"), Width = 80, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 3, 4, 3) };
            var lblSep1 = new Label { Text = "—", AutoSize = true, Margin = new Padding(2, 6, 2, 3) };
            _txtNaming2 = new TextBox { Text = "PROJECT_NAME", Width = 150, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 3, 4, 3) };
            var lblSep2 = new Label { Text = "—", AutoSize = true, Margin = new Padding(2, 6, 2, 3) };
            _txtNaming3 = new TextBox { Text = "FOUNDATION SECTIONS", Width = 230, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 3, 4, 3) };

            _txtNaming1.TextChanged += (s, e) => UpdateCombineFileName();
            _txtNaming2.TextChanged += (s, e) => UpdateCombineFileName();
            _txtNaming3.TextChanged += (s, e) => UpdateCombineFileName();

            pnlRow2.Controls.AddRange(new System.Windows.Forms.Control[] { _chkUseNamingConvention, _txtNaming1, lblSep1, _txtNaming2, lblSep2, _txtNaming3 });
            pnlTopConfig.Controls.Add(pnlRow2);

            // Row 3: Combine File Name + Formats
            var pnlRow3 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(2, 2, 2, 2)
            };

            var lblCombine = new Label { Text = "Tên file PDF gộp:", AutoSize = true, Font = new Font("Segoe UI", 9F), Margin = new Padding(3, 6, 6, 3) };
            _txtFileCombineName = new TextBox { Text = "Combined_Project_Sheets.pdf", Width = 400, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 3, 10, 3) };

            _chkExportPdf = new CheckBox { Text = "Xuất PDF", AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkBlue, Margin = new Padding(6, 5, 10, 3) };
            _chkExportDwg = new CheckBox { Text = "Xuất DWG", AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkGreen, Margin = new Padding(6, 5, 10, 3) };
            _chkCreateTransmittal = new CheckBox { Text = "Tạo Bảng Kê Excel (Transmittal)", AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F), Margin = new Padding(6, 5, 6, 3) };

            _chkExportPdf.CheckedChanged += (s, e) => RefreshGridRows();
            _chkExportDwg.CheckedChanged += (s, e) => RefreshGridRows();

            pnlRow3.Controls.AddRange(new System.Windows.Forms.Control[] { lblCombine, _txtFileCombineName, _chkExportPdf, _chkExportDwg, _chkCreateTransmittal });
            pnlTopConfig.Controls.Add(pnlRow3);

            // Row 4: Search & Quick Selection Toolbar
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = KhimUiStyle.FormBg,
                Padding = new Padding(4, 6, 4, 4)
            };

            var lblFilter = new Label { Text = "Bộ môn:", Left = 6, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = KhimUiStyle.TextSecondary };
            _cmbDisciplineFilter = new ComboBox { Left = 62, Top = 7, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbDisciplineFilter.Items.AddRange(new object[] { "(Tất cả bộ môn)", "Structure (Kết cấu)", "Architecture (Kiến trúc)", "MEP (Cơ điện)" });
            _cmbDisciplineFilter.SelectedIndex = 0;
            _cmbDisciplineFilter.SelectedIndexChanged += (s, e) => ApplySearchAndFilter();

            _txtSearchSheet = new TextBox { Left = 210, Top = 7, Width = 260, Font = new Font("Segoe UI", 9F) };
            _txtSearchSheet.TextChanged += (s, e) => ApplySearchAndFilter();

            _btnSelectAll = new Button { Text = "Chọn hết", Left = 480, Top = 6, Width = 80, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };
            _btnClearAll = new Button { Text = "Bỏ chọn", Left = 566, Top = 6, Width = 80, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };
            _btnInvert = new Button { Text = "Đảo chọn", Left = 652, Top = 6, Width = 80, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };

            _btnSelectAll.Click += (s, e) => SetAllGridItems(true);
            _btnClearAll.Click += (s, e) => SetAllGridItems(false);
            _btnInvert.Click += (s, e) => InvertGridItems();

            _btnRefreshList = new Button { Text = "🔄 Nạp lại", Left = 740, Top = 6, Width = 80, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };
            _btnRefreshList.Click += (s, e) => LoadDataFromRevit();

            pnlToolbar.Controls.AddRange(new System.Windows.Forms.Control[] {
                lblFilter, _cmbDisciplineFilter, _txtSearchSheet, _btnSelectAll, _btnClearAll, _btnInvert, _btnRefreshList
            });
            pnlTopConfig.Controls.Add(pnlToolbar);

            _viewSelect.Controls.Add(pnlTopConfig);

            // ── CENTER UNIFIED DATAGRIDVIEW ──────────────────────────────────
            var pnlGridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 6, 12, 6),
                BackColor = KhimUiStyle.FormBg
            };

            _gridSheets = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            BuildGridColumns();

            // Lắng nghe sự kiện tick checkbox trực tiếp trên DataGridView
            _gridSheets.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0 && e.RowIndex < _filteredSheetItems.Count)
                {
                    bool val = Convert.ToBoolean(_gridSheets.Rows[e.RowIndex].Cells[0].Value);
                    _filteredSheetItems[e.RowIndex].IsSelected = val;
                    UpdateSummaryLabel();
                }
            };

            _gridSheets.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_gridSheets.IsCurrentCellDirty && _gridSheets.CurrentCellAddress.X == 0)
                {
                    _gridSheets.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            pnlGridContainer.Controls.Add(_gridSheets);
            _viewSelect.Controls.Add(pnlGridContainer);

            // Đảm bảo Grid nằm dưới TopConfig
            pnlGridContainer.BringToFront();
        }

        private void BuildGridColumns()
        {
            _gridSheets.Columns.Clear();

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "In/Xuất",
                Width = 65,
                FillWeight = 15,
                Resizable = DataGridViewTriState.False
            };
            var colIndex = new DataGridViewTextBoxColumn { HeaderText = "#", Width = 45, FillWeight = 10, ReadOnly = true };
            var colNum = new DataGridViewTextBoxColumn { HeaderText = "Số Hiệu (Number)", Width = 110, FillWeight = 25, ReadOnly = true };
            var colName = new DataGridViewTextBoxColumn { HeaderText = "Tên Bản Vẽ (Sheet Name)", Width = 280, FillWeight = 60, ReadOnly = true };
            var colRev = new DataGridViewTextBoxColumn { HeaderText = "Rev", Width = 55, FillWeight = 12, ReadOnly = true };
            var colRevDate = new DataGridViewTextBoxColumn { HeaderText = "Ngày Rev", Width = 85, FillWeight = 18, ReadOnly = true };
            var colSize = new DataGridViewTextBoxColumn { HeaderText = "Khổ Giấy", Width = 75, FillWeight = 15, ReadOnly = true };
            var colOrient = new DataGridViewTextBoxColumn { HeaderText = "Chiều In", Width = 85, FillWeight = 18, ReadOnly = true };
            var colFormat = new DataGridViewTextBoxColumn { HeaderText = "Định Dạng", Width = 85, FillWeight = 18, ReadOnly = true };
            var colStatus = new DataGridViewTextBoxColumn { HeaderText = "Trạng Thái", Width = 100, FillWeight = 20, ReadOnly = true };

            _gridSheets.Columns.AddRange(colCheck, colIndex, colNum, colName, colRev, colRevDate, colSize, colOrient, colFormat, colStatus);
        }
        #endregion

        #region View 2: Settings Tab (PDF & DWG)
        private void BuildViewSettings()
        {
            _viewSettings = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = KhimUiStyle.FormBg };

            _tabSettings = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

            // Subtab PDF
            var tabPdf = new TabPage { Text = "    PDF Setup    ", Padding = new Padding(12), BackColor = Color.White };
            BuildPdfSettingsTab(tabPdf);
            _tabSettings.TabPages.Add(tabPdf);

            // Subtab DWG
            var tabDwg = new TabPage { Text = "    DWG Setup    ", Padding = new Padding(12), BackColor = Color.White };
            BuildDwgSettingsTab(tabDwg);
            _tabSettings.TabPages.Add(tabDwg);

            _viewSettings.Controls.Add(_tabSettings);
        }

        private void BuildPdfSettingsTab(TabPage tab)
        {
            var pnlContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                AutoScroll = true
            };
            pnlContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            pnlContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            pnlContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            // 1. GroupBox: Paper Placement
            var grpPlacement = new GroupBox { Text = "Căn Lề & Vị Trí Giấy (Paper Placement)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            _rbPlacementCenter = new RadioButton { Text = "Chính giữa (Center)", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbPlacementOffset = new RadioButton { Text = "Khoảng cách từ góc (Offset from corner)", Left = 15, Top = 50, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };

            _rbMarginNoMargin = new RadioButton { Text = "Không chừa lề (No Margin)", Left = 35, Top = 75, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _rbMarginPrinterLimit = new RadioButton { Text = "Giới hạn máy in (Printer Limit)", Left = 35, Top = 100, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbMarginUserDefined = new RadioButton { Text = "Tự định nghĩa lề:", Left = 35, Top = 125, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            var lblX = new Label { Text = "X:", Left = 50, Top = 155, AutoSize = true };
            _numMarginX = new NumericUpDown { Left = 70, Top = 152, Width = 60, DecimalPlaces = 2, Value = 0 };
            var lblY = new Label { Text = "Y:", Left = 140, Top = 155, AutoSize = true };
            _numMarginY = new NumericUpDown { Left = 160, Top = 152, Width = 60, DecimalPlaces = 2, Value = 0 };

            grpPlacement.Controls.AddRange(new System.Windows.Forms.Control[] {
                _rbPlacementCenter, _rbPlacementOffset, _rbMarginNoMargin, _rbMarginPrinterLimit, _rbMarginUserDefined,
                lblX, _numMarginX, lblY, _numMarginY
            });

            // 2. GroupBox: Zoom
            var grpZoom = new GroupBox { Text = "Tỉ Lệ Thu Phóng (Zoom)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            _rbZoomFitToPage = new RadioButton { Text = "Vừa khổ giấy (Fit to page)", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbZoomPercent = new RadioButton { Text = "Phóng tỉ lệ chuẩn:", Left = 15, Top = 50, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _numZoomPercent = new NumericUpDown { Left = 150, Top = 48, Width = 60, Value = 100, Maximum = 500, Minimum = 10 };
            var lblPct = new Label { Text = "% (100% = Đúng tỉ lệ bản vẽ)", Left = 215, Top = 52, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };

            grpZoom.Controls.AddRange(new System.Windows.Forms.Control[] { _rbZoomFitToPage, _rbZoomPercent, _numZoomPercent, lblPct });

            // 3. GroupBox: Quality & Color
            var grpQuality = new GroupBox { Text = "Chất Lượng & Màu Sắc", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            _rbVectorProcessing = new RadioButton { Text = "Vector Processing (Độ nét cao)", Left = 15, Top = 25, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _rbRasterProcessing = new RadioButton { Text = "Raster Processing", Left = 15, Top = 50, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            var lblRasterQuality = new Label { Text = "Chất lượng Raster:", Left = 15, Top = 80, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _cmbRasterQuality = new ComboBox { Left = 135, Top = 76, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbRasterQuality.Items.AddRange(new object[] { "Low", "Medium", "High", "Presentation" });
            _cmbRasterQuality.SelectedIndex = 3;

            var lblColor = new Label { Text = "Chế độ màu sắc:", Left = 15, Top = 110, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _cmbColorDepth = new ComboBox { Left = 135, Top = 106, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbColorDepth.Items.AddRange(new object[] { "Color", "Grayscale", "Black Lines" });
            _cmbColorDepth.SelectedIndex = 2; // Black Lines mặc định cho kỹ thuật

            grpQuality.Controls.AddRange(new System.Windows.Forms.Control[] {
                _rbVectorProcessing, _rbRasterProcessing, lblRasterQuality, _cmbRasterQuality, lblColor, _cmbColorDepth
            });

            // 4. GroupBox: Options
            var grpOptions = new GroupBox { Text = "Ẩn Các Đối Tượng Phụ (Hide Annotations)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            _chkViewLinksInBlue = new CheckBox { Text = "Hiển thị liên kết màu xanh (View links in blue)", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _chkHideRefPlanes = new CheckBox { Text = "Ẩn mặt phẳng tham chiếu (Hide Ref/Work Planes)", Left = 15, Top = 50, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkHideUnreferencedTags = new CheckBox { Text = "Ẩn tag chưa tham chiếu (Hide Unreferenced Tags)", Left = 15, Top = 75, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkHideScopeBoxes = new CheckBox { Text = "Ẩn hộp giới hạn (Hide Scope Boxes)", Left = 15, Top = 100, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkHideCropBoundaries = new CheckBox { Text = "Ẩn khung crop (Hide Crop Boundaries)", Left = 15, Top = 125, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkReplaceHalftone = new CheckBox { Text = "Thay Halftone bằng nét mảnh (Thin Lines)", Left = 15, Top = 150, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkMaskCoincidentLines = new CheckBox { Text = "Mask nét trùng lặp (Mask Coincident Lines)", Left = 15, Top = 175, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            grpOptions.Controls.AddRange(new System.Windows.Forms.Control[] {
                _chkViewLinksInBlue, _chkHideRefPlanes, _chkHideUnreferencedTags, _chkHideScopeBoxes,
                _chkHideCropBoundaries, _chkReplaceHalftone, _chkMaskCoincidentLines
            });

            pnlContainer.Controls.Add(grpPlacement, 0, 0);
            pnlContainer.Controls.Add(grpZoom, 1, 0);
            pnlContainer.Controls.Add(grpQuality, 2, 0);
            pnlContainer.Controls.Add(grpOptions, 0, 1);
            pnlContainer.SetColumnSpan(grpOptions, 3);

            tab.Controls.Add(pnlContainer);
        }

        private void BuildDwgSettingsTab(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

            var lblSetup = new Label { Text = "Cấu hình xuất DWG (DWG Export Setup):", Left = 20, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _cmbDwgSetup = new ComboBox { Left = 20, Top = 50, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5F) };

            var lblVer = new Label { Text = "Phiên bản AutoCAD:", Left = 20, Top = 90, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _cmbAutoCadVersion = new ComboBox { Left = 20, Top = 115, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5F) };
            _cmbAutoCadVersion.Items.AddRange(new object[] { "AutoCAD 2018 format", "AutoCAD 2013 format", "AutoCAD 2010 format", "AutoCAD 2007 format" });
            _cmbAutoCadVersion.SelectedIndex = 0;

            _chkDwgMergedViews = new CheckBox
            {
                Text = "Xuất các khung nhìn trên Sheet thành 1 file DWG duy nhất (Merged Views)",
                Left = 20,
                Top = 160,
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            pnl.Controls.AddRange(new System.Windows.Forms.Control[] { lblSetup, _cmbDwgSetup, lblVer, _cmbAutoCadVersion, _chkDwgMergedViews });
            tab.Controls.Add(pnl);
        }
        #endregion

        #region View 3: Filter & Revision QA Tab
        private void BuildViewFilter()
        {
            _viewFilter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = KhimUiStyle.FormBg };

            var grpQa = new GroupBox
            {
                Text = "Lọc Theo Trạng Thái Bản Vẽ & Revision (Issue QA)",
                Dock = DockStyle.Top,
                Height = 150,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Padding(15)
            };

            _chkFilterModifiedOnly = new CheckBox
            {
                Text = "Chỉ hiển thị các bản vẽ MỚI hoặc ĐÃ SỬA ĐỔI (New / Modified Sheets only)",
                Left = 20,
                Top = 35,
                AutoSize = true,
                Checked = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkRed
            };
            _chkFilterModifiedOnly.CheckedChanged += (s, e) => ApplySearchAndFilter();

            var lblDesc = new Label
            {
                Text = "Hệ thống tự động so sánh số hiệu Revision của từng Sheet với lịch sử phát hành trước đó.\n" +
                       "Tính năng này giúp bạn chỉ xuất lại đúng những bản vẽ đã có thay đổi trong đợt phát hành mới.",
                Left = 20,
                Top = 70,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = KhimUiStyle.TextSecondary
            };

            grpQa.Controls.Add(_chkFilterModifiedOnly);
            grpQa.Controls.Add(lblDesc);

            _viewFilter.Controls.Add(grpQa);
        }
        #endregion

        #region Bottom Bar
        private Panel BuildBottomPrintBar()
        {
            var grpPrint = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = KhimUiStyle.CardBg,
                Padding = new Padding(15, 10, 15, 10)
            };

            // Folder row
            _btnOpenFolderSelection = new Button
            {
                Text = "📂 Mở Thư Mục",
                Left = 15,
                Top = 14,
                Width = 115,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnOpenFolderSelection.Click += (s, e) => OpenOutputDirectory();

            _txtOutputDirectory = new TextBox
            {
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KhimTools_Export"),
                Left = 138,
                Top = 15,
                Width = 520,
                Font = new Font("Segoe UI", 9F)
            };

            _btnBrowseFolder = new Button
            {
                Text = "...",
                Left = 665,
                Top = 14,
                Width = 40,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };
            _btnBrowseFolder.Click += BtnBrowseFolder_Click;

            _lblTotalSummary = new Label
            {
                Text = "0 / 0 Sheets selected",
                Left = 15,
                Top = 48,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextPrimary
            };

            _btnPrint = new Button
            {
                Text = "⚡ XUẤT IN BẢN VẼ",
                Width = 180,
                Height = 44,
                Left = 1130,
                Top = 14,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.CreateButtonBg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPrint.Click += BtnPrint_Click;

            grpPrint.Controls.AddRange(new System.Windows.Forms.Control[] {
                _btnOpenFolderSelection, _txtOutputDirectory, _btnBrowseFolder, _lblTotalSummary, _btnPrint
            });

            grpPrint.Resize += (s, e) =>
            {
                _btnPrint.Left = grpPrint.Width - _btnPrint.Width - 18;
                _txtOutputDirectory.Width = Math.Max(250, _btnPrint.Left - _txtOutputDirectory.Left - 60);
                _btnBrowseFolder.Left = _txtOutputDirectory.Right + 6;
            };

            return grpPrint;
        }
        #endregion

        #region Data Loading & Synchronization
        private void LoadDataFromRevit()
        {
            _isLoading = true;
            try
            {
                _allSheetItems = SheetCollectorService.GetAllSheets(_doc) ?? new List<SheetExportItem>();
                RevisionSnapshotService.CompareAndUpdateStatus(_doc, _allSheetItems);

                // Populate ViewSheetSets
                _cmbSheetSet.Items.Clear();
                _cmbSheetSet.Items.Add("<In-Session Scheme>");
                var sets = new FilteredElementCollector(_doc).OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>().ToList();
                foreach (var s in sets)
                {
                    _cmbSheetSet.Items.Add(s.Name);
                }
                _cmbSheetSet.SelectedIndex = 0;

                // Populate DWG Setups
                try
                {
                    var dwgSetups = BaseExportOptions.GetPredefinedSetupNames(_doc);
                    _cmbDwgSetup.Items.Clear();
                    _cmbDwgSetup.Items.Add("In-Session Setup (Mặc định)");
                    if (dwgSetups != null)
                    {
                        foreach (var s in dwgSetups) _cmbDwgSetup.Items.Add(s);
                    }
                    _cmbDwgSetup.SelectedIndex = 0;
                }
                catch { }

                // Populate Project Code for Naming
                string projName = _doc.ProjectInformation?.Name ?? _doc.Title ?? "PROJECT";
                if (!string.IsNullOrWhiteSpace(projName))
                {
                    _txtNaming2.Text = projName.Replace(" ", "_").ToUpper();
                }

                ApplySearchAndFilter();
                UpdateCombineFileName();
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Lỗi Khởi Tạo Dữ Liệu Sheet", ex.Message, ex.StackTrace);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ApplySearchAndFilter()
        {
            string query = _txtSearchSheet.Text.Trim();
            string discipline = _cmbDisciplineFilter.SelectedItem?.ToString() ?? "(All)";

            var list = _allSheetItems.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                list = list.Where(s => (s.SheetNumber != null && s.SheetNumber.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                       (s.SheetName != null && s.SheetName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (discipline.StartsWith("Structure") || discipline.Contains("Kết cấu"))
            {
                list = list.Where(s => s.SheetNumber != null && (s.SheetNumber.StartsWith("KC") || s.SheetNumber.StartsWith("ST") || s.SheetNumber.StartsWith("S-")));
            }
            else if (discipline.StartsWith("Architecture") || discipline.Contains("Kiến trúc"))
            {
                list = list.Where(s => s.SheetNumber != null && (s.SheetNumber.StartsWith("KT") || s.SheetNumber.StartsWith("AR") || s.SheetNumber.StartsWith("A-")));
            }

            if (_chkFilterModifiedOnly.Checked)
            {
                list = list.Where(s => s.IssueStatus == SheetIssueStatus.New || s.IssueStatus == SheetIssueStatus.Modified);
            }

            _filteredSheetItems = list.ToList();
            RefreshGridRows();
            UpdateSummaryLabel();
        }

        private void SetAllGridItems(bool state)
        {
            foreach (var item in _filteredSheetItems)
            {
                item.IsSelected = state;
            }
            RefreshGridRows();
            UpdateSummaryLabel();
        }

        private void InvertGridItems()
        {
            foreach (var item in _filteredSheetItems)
            {
                item.IsSelected = !item.IsSelected;
            }
            RefreshGridRows();
            UpdateSummaryLabel();
        }

        private void RefreshGridRows()
        {
            _gridSheets.Rows.Clear();

            int idx = 1;
            string fmt = (_chkExportPdf.Checked && _chkExportDwg.Checked) ? "PDF / DWG" : (_chkExportPdf.Checked ? "PDF" : "DWG");

            foreach (var item in _filteredSheetItems)
            {
                int r = _gridSheets.Rows.Add();
                _gridSheets.Rows[r].Cells[0].Value = item.IsSelected;
                _gridSheets.Rows[r].Cells[1].Value = idx++;
                _gridSheets.Rows[r].Cells[2].Value = item.SheetNumber;
                _gridSheets.Rows[r].Cells[3].Value = item.SheetName;
                _gridSheets.Rows[r].Cells[4].Value = item.CurrentRevisionNumber;
                _gridSheets.Rows[r].Cells[5].Value = item.CurrentRevisionDate;
                _gridSheets.Rows[r].Cells[6].Value = item.PaperSize;
                _gridSheets.Rows[r].Cells[7].Value = "Landscape";
                _gridSheets.Rows[r].Cells[8].Value = fmt;
                _gridSheets.Rows[r].Cells[9].Value = item.ExportStatusText ?? "Sẵn sàng";
            }
        }

        private void UpdateSummaryLabel()
        {
            int selectedCount = _allSheetItems.Count(s => s.IsSelected);
            int totalCount = _allSheetItems.Count;
            _lblTotalSummary.Text = $"✔ Đã chọn: {selectedCount} / {totalCount} bản vẽ sẵn sàng xuất.";
        }

        private void UpdateCombineFileName()
        {
            string f1 = _txtNaming1.Text.Trim();
            string f2 = _txtNaming2.Text.Trim();
            string f3 = _txtNaming3.Text.Trim();

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(f1)) parts.Add(f1);
            if (!string.IsNullOrEmpty(f2)) parts.Add(f2);
            if (!string.IsNullOrEmpty(f3)) parts.Add(f3);

            string combined = string.Join("_", parts);
            if (string.IsNullOrEmpty(combined)) combined = "Combined_Project_Sheets";
            _txtFileCombineName.Text = combined + ".pdf";
        }
        #endregion

        #region Actions & Export Execution
        private void BtnBrowseFolder_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = _txtOutputDirectory.Text;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _txtOutputDirectory.Text = dlg.SelectedPath;
            }
        }

        private void OpenOutputDirectory()
        {
            string outDir = _txtOutputDirectory.Text.Trim();
            if (Directory.Exists(outDir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", outDir) { UseShellExecute = true });
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(outDir);
                    Process.Start(new ProcessStartInfo("explorer.exe", outDir) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    KhimDialogHelper.ShowError("Lỗi Mở Thư Mục", ex.Message);
                }
            }
        }

        private void BtnPrint_Click(object sender, EventArgs e)
        {
            var selectedItems = _allSheetItems.Where(s => s.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng chọn ít nhất 1 bản vẽ trong danh sách trước khi in / xuất.");
                return;
            }

            if (!_chkExportPdf.Checked && !_chkExportDwg.Checked)
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng tích chọn ít nhất 1 định dạng xuất (PDF hoặc DWG).");
                return;
            }

            string outDir = _txtOutputDirectory.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng chọn thư mục lưu file.");
                return;
            }

            if (!Directory.Exists(outDir))
            {
                try { Directory.CreateDirectory(outDir); }
                catch (Exception ex)
                {
                    KhimDialogHelper.ShowError("Lỗi Tạo Thư Mục", ex.Message);
                    return;
                }
            }

            // Sync options
            _options.ExportPdf = _chkExportPdf.Checked;
            _options.ExportDwg = _chkExportDwg.Checked;
            _options.DwgExportSetupName = _cmbDwgSetup.Text.Trim();
            _options.OutputDirectory = outDir;
            _options.SplitFoldersByFormat = false;
            _options.CombinePdf = _rbCombineFiles.Checked;
            _options.CombinedPdfFileName = _txtFileCombineName.Text.Trim();

            _options.PaperPlacementCenter = _rbPlacementCenter?.Checked ?? false;
            _options.PaperPlacementOffset = _rbPlacementOffset?.Checked ?? true;
            _options.MarginNoMargin = _rbMarginNoMargin?.Checked ?? true;
            _options.MarginOffsetX = _numMarginX != null ? (double)_numMarginX.Value : 0;
            _options.MarginOffsetY = _numMarginY != null ? (double)_numMarginY.Value : 0;

            _options.ZoomFitToPage = _rbZoomFitToPage?.Checked ?? false;
            _options.ZoomPercentage = _numZoomPercent != null ? (int)_numZoomPercent.Value : 100;
            _options.VectorProcessing = _rbVectorProcessing?.Checked ?? true;
            _options.RasterQuality = _cmbRasterQuality?.SelectedItem?.ToString() ?? "Presentation";
            _options.ColorMode = _cmbColorDepth?.SelectedItem?.ToString() ?? "Color";

            _options.ViewLinksInBlue = _chkViewLinksInBlue?.Checked ?? false;
            _options.HideRefPlanes = _chkHideRefPlanes?.Checked ?? true;
            _options.HideUnreferencedViewTags = _chkHideUnreferencedTags?.Checked ?? true;
            _options.HideScopeBoxes = _chkHideScopeBoxes?.Checked ?? true;
            _options.HideCropBoundaries = _chkHideCropBoundaries?.Checked ?? true;
            _options.ReplaceHalftoneWithThinLines = _chkReplaceHalftone?.Checked ?? true;
            _options.MaskCoincidentLines = _chkMaskCoincidentLines?.Checked ?? false;

            // Compute exact filename for each selected sheet
            foreach (var item in selectedItems)
            {
                item.ComputedFileName = ComputeSheetFileName(item);
            }

            _btnPrint.Enabled = false;
            _btnPrint.Text = "Đang xuất...";
            Cursor = Cursors.WaitCursor;

            try
            {
                var queue = new ExportRetryQueue(_options.MaxRetryCount);
                var qaResults = queue.ProcessBatch(_doc, selectedItems, _options, msg =>
                {
                    _btnPrint.Text = msg;
                    RefreshGridRows();
                    Application.DoEvents();
                });

                // Generate Transmittal Excel only if requested
                if (_chkCreateTransmittal.Checked)
                {
                    TransmittalGeneratorService.GenerateExcelTransmittal(outDir, "Official Release", _txtNaming2.Text, selectedItems);
                }

                int successCount = qaResults.Count(r => r.Success);
                int failCount = qaResults.Count(r => !r.Success);

                Cursor = Cursors.Default;
                string successMsg = $"🎉 Đã xuất thành công {successCount} bản vẽ sang thư mục:\n{outDir}";
                if (failCount > 0)
                {
                    successMsg += $"\n⚠️ Có {failCount} bản vẽ gặp lỗi.";
                }

                KhimDialogHelper.ShowSuccess("Hoàn Tất Xuất Bản Vẽ", successMsg);
                OpenOutputDirectory();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                KhimDialogHelper.ShowError("Lỗi Trong Quá Trình Xuất", ex.Message, ex.StackTrace);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnPrint.Enabled = true;
                _btnPrint.Text = "⚡ XUẤT IN BẢN VẼ";
                RefreshGridRows();
            }
        }

        private string ComputeSheetFileName(SheetExportItem item)
        {
            string cleanNum = SanitizeFileName(item.SheetNumber);
            string cleanName = SanitizeFileName(item.SheetName);

            if (_chkUseNamingConvention.Checked)
            {
                string p1 = SanitizeFileName(_txtNaming1.Text.Trim());
                string p2 = SanitizeFileName(_txtNaming2.Text.Trim());
                string p3 = SanitizeFileName(_txtNaming3.Text.Trim());

                var parts = new List<string>();
                if (!string.IsNullOrEmpty(p1)) parts.Add(p1);
                if (!string.IsNullOrEmpty(p2)) parts.Add(p2);
                if (!string.IsNullOrEmpty(cleanNum)) parts.Add(cleanNum);
                if (!string.IsNullOrEmpty(cleanName)) parts.Add(cleanName);

                return string.Join(" - ", parts);
            }
            else
            {
                return $"{cleanNum} - {cleanName}";
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Sheet";
            var invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
        #endregion
    }
}

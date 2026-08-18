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

namespace KhimTools.SheetExport.Forms
{
    public class SheetExportForm : System.Windows.Forms.Form
    {
        private readonly Document _doc;
        private List<SheetExportItem> _allSheetItems = new List<SheetExportItem>();
        private List<SheetExportItem> _filteredSheetItems = new List<SheetExportItem>();
        private List<NamingTemplate> _namingTemplates = new List<NamingTemplate>();
        private NamingTemplate _selectedTemplate;

        // UI Controls - Left Panel (Sheet List & Filter)
        private TextBox _txtSearch;
        private DataGridView _gridSheets;
        private Label _lblSelectionCount;
        private Button _btnSelectAll;
        private Button _btnSelectNone;
        private Button _btnInvertSelection;
        private Button _btnSelectChangedOnly;

        // UI Controls - Center/Right Settings (Tabs)
        private TabControl _tabSettings;
        private ComboBox _cmbNamingTemplate;
        private TextBox _txtProjectCode;
        private Button _btnEditNamingTemplate;

        // Format & PDF / DWG Options
        private CheckBox _chkExportPdf;
        private CheckBox _chkExportDwg;
        private ComboBox _cmbDwgSetup;
        private ComboBox _cmbColorDepth;
        private ComboBox _cmbQualityDpi;
        private CheckBox _chkCombinePdf;
        private TextBox _txtCombinedFileName;
        private CheckBox _chkAddBookmarks;
        private CheckBox _chkWatermark;
        private ComboBox _cmbWatermarkPreset;
        private CheckBox _chkAutoCoverPage;

        // Issue & Reports
        private TextBox _txtIssueSetName;
        private CheckBox _chkGenerateTransmittal;
        private CheckBox _chkGenerateQaReport;
        private NumericUpDown _numMaxRetries;

        // Destination Folder
        private TextBox _txtOutputDir;
        private Button _btnBrowseOutputDir;
        private Button _btnOpenOutputDir;

        // Right Preview & Log Panels
        private PictureBox _picThumbnail;
        private Label _lblThumbnailTitle;
        private RichTextBox _rtbLogOutput;
        private ProgressBar _progressBar;

        // Action Buttons
        private Button _btnPreflightCheck;
        private Button _btnExportStart;
        private Button _btnClose;

        public SheetExportForm(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            KhimUiStyle.ApplyFormTheme(this);
            InitializeFormLayout();
            LoadDataFromRevit();
        }

        private void InitializeFormLayout()
        {
            Text = "📄 KHIM TOOLS — Xuất & In Bản Vẽ Hàng Loạt (Sheet Batch Exporter)";
            Width = 1360;
            Height = 840;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new System.Drawing.Size(1180, 700);

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Sheet Batch Export & Print Manager",
                "Bộ công cụ xuất in PDF/DWG hàng loạt, tự động nhận diện khổ giấy và quản lý phát hành bản vẽ",
                "v2.5 Pro");
            Controls.Add(header);

            // Main Splitter (Left: Sheet List, Right: Options + Preview + Log)
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Vertical,
                SplitterDistance = 640,
                Panel1MinSize = 480,
                Panel2MinSize = 520,
                Padding = new Padding(8)
            };

            // ── LEFT PANEL: Sheet List & Selection Controls ──────────────────
            var leftPanel = new System.Windows.Forms.Panel { Dock = DockStyle.Fill };
            var lblSheetListTitle = new Label
            {
                Text = "📋 Danh Sách Sheet Bản Vẽ Trong Dự Án",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };

            // Search bar
            var pnlSearchBar = new System.Windows.Forms.Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(2) };
            var lblSearch = new Label { Text = "🔍 Tìm kiếm:", Left = 2, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtSearch = new TextBox { Left = 85, Top = 5, Width = 340, Font = new Font("Segoe UI", 9F) };
            _txtSearch.TextChanged += (s, e) => ApplySearchFilter();

            var btnClearSearch = new Button { Text = "✕", Left = 430, Top = 4, Width = 30, Height = 25, FlatStyle = FlatStyle.System };
            btnClearSearch.Click += (s, e) => { _txtSearch.Clear(); };

            pnlSearchBar.Controls.Add(lblSearch);
            pnlSearchBar.Controls.Add(_txtSearch);
            pnlSearchBar.Controls.Add(btnClearSearch);

            // Quick Selection bar
            var pnlSelectionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            _btnSelectAll = new Button { Text = "Chọn Tất Cả", Width = 90, Height = 28, FlatStyle = FlatStyle.System };
            _btnSelectNone = new Button { Text = "Bỏ Chọn", Width = 75, Height = 28, FlatStyle = FlatStyle.System };
            _btnInvertSelection = new Button { Text = "Đảo Lựa Chọn", Width = 95, Height = 28, FlatStyle = FlatStyle.System };
            _btnSelectChangedOnly = new Button
            {
                Text = "⚡ Chỉ Sheet Đã Sửa / Mới",
                Width = 180,
                Height = 28,
                BackColor = Color.FromArgb(235, 245, 255),
                FlatStyle = FlatStyle.Flat
            };

            _btnSelectAll.Click += (s, e) => SetSelectionAll(true);
            _btnSelectNone.Click += (s, e) => SetSelectionAll(false);
            _btnInvertSelection.Click += (s, e) => InvertSelection();
            _btnSelectChangedOnly.Click += (s, e) => SelectChangedSheetsOnly();

            pnlSelectionBar.Controls.Add(_btnSelectAll);
            pnlSelectionBar.Controls.Add(_btnSelectNone);
            pnlSelectionBar.Controls.Add(_btnInvertSelection);
            pnlSelectionBar.Controls.Add(_btnSelectChangedOnly);

            _gridSheets = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };

            BuildGridColumns();

            _gridSheets.SelectionChanged += GridSheets_SelectionChanged;
            _gridSheets.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_gridSheets.IsCurrentCellDirty)
                {
                    _gridSheets.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _gridSheets.CellValueChanged += (s, e) =>
            {
                UpdateSelectionCount();
            };

            _lblSelectionCount = new Label
            {
                Text = "Đã chọn: 0 / 0 sheets",
                Dock = DockStyle.Bottom,
                Height = 24,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.DarkGreen
            };

            leftPanel.Controls.Add(_gridSheets);
            leftPanel.Controls.Add(pnlSelectionBar);
            leftPanel.Controls.Add(pnlSearchBar);
            leftPanel.Controls.Add(_lblSelectionCount);
            leftPanel.Controls.Add(lblSheetListTitle);
            mainSplit.Panel1.Controls.Add(leftPanel);

            // ── RIGHT PANEL: Tab Control + Preview + Log Output ─────────────
            var rightPanel = new System.Windows.Forms.Panel { Dock = DockStyle.Fill };

            _tabSettings = new TabControl { Dock = DockStyle.Top, Height = 350, Font = new Font("Segoe UI", 9F) };

            // TAB 1: Naming & File Formats
            var tabFormats = new TabPage { Text = "⚙️ Định Dạng & Khổ Giấy", Padding = new Padding(12) };
            BuildTabFormats(tabFormats);
            _tabSettings.TabPages.Add(tabFormats);

            // TAB 2: Issue & Transmittal
            var tabIssue = new TabPage { Text = "📋 Quản Lý Phát Hành (Issue)", Padding = new Padding(12) };
            BuildTabIssue(tabIssue);
            _tabSettings.TabPages.Add(tabIssue);

            // TAB 3: Advanced PDF & Watermark
            var tabAdvancedPdf = new TabPage { Text = "🎨 Nâng Cao & Đóng Dấu", Padding = new Padding(12) };
            BuildTabAdvancedPdf(tabAdvancedPdf);
            _tabSettings.TabPages.Add(tabAdvancedPdf);

            // Preview Thumbnail & Log Splitter
            var previewSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Horizontal,
                SplitterDistance = 160
            };

            var pnlPreviewBox = new GroupBox { Text = "🖼️ Xem Trước Sheet (Thumbnail)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblThumbnailTitle = new Label { Text = "Chưa chọn sheet", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };
            _picThumbnail = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(245, 246, 250) };
            pnlPreviewBox.Controls.Add(_picThumbnail);
            pnlPreviewBox.Controls.Add(_lblThumbnailTitle);
            previewSplit.Panel1.Controls.Add(pnlPreviewBox);

            var pnlLogBox = new GroupBox { Text = "📜 Tiến Trình Xuất File (Real-time Log)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _rtbLogOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 36),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 8.5F)
            };

            _progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 12, Visible = false };

            pnlLogBox.Controls.Add(_rtbLogOutput);
            pnlLogBox.Controls.Add(_progressBar);
            previewSplit.Panel2.Controls.Add(pnlLogBox);

            rightPanel.Controls.Add(previewSplit);
            rightPanel.Controls.Add(_tabSettings);
            mainSplit.Panel2.Controls.Add(rightPanel);

            // ── BOTTOM CONTAINER: Destination Folder & Action Bar ────────────
            var pnlBottomContainer = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 98,
                BackColor = Color.FromArgb(242, 244, 248),
                Padding = new Padding(12, 6, 12, 6)
            };

            // Row 1: Output Directory Picker
            var pnlOutputDirBar = new System.Windows.Forms.Panel { Dock = DockStyle.Top, Height = 38 };
            var lblDest = new Label
            {
                Text = "📁 Thư Mục Lưu File:",
                Left = 0,
                Top = 8,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KhimTools_Export");
            _txtOutputDir = new TextBox
            {
                Left = 150,
                Top = 5,
                Width = 720,
                Font = new Font("Segoe UI", 9.5F),
                Text = defaultDir
            };

            _btnBrowseOutputDir = new Button
            {
                Text = "📂 Chọn Thư Mục...",
                Left = 880,
                Top = 4,
                Width = 145,
                Height = 29,
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnBrowseOutputDir.Click += BtnBrowseOutputDir_Click;

            _btnOpenOutputDir = new Button
            {
                Text = "📁 Mở Folder",
                Left = 1035,
                Top = 4,
                Width = 105,
                Height = 29,
                FlatStyle = FlatStyle.System
            };
            _btnOpenOutputDir.Click += (s, e) => OpenOutputDirectory();

            pnlOutputDirBar.Controls.Add(lblDest);
            pnlOutputDirBar.Controls.Add(_txtOutputDir);
            pnlOutputDirBar.Controls.Add(_btnBrowseOutputDir);
            pnlOutputDirBar.Controls.Add(_btnOpenOutputDir);

            pnlOutputDirBar.Resize += (s, e) =>
            {
                int rightControlsWidth = _btnBrowseOutputDir.Width + _btnOpenOutputDir.Width + 25;
                _txtOutputDir.Width = Math.Max(200, pnlOutputDirBar.Width - _txtOutputDir.Left - rightControlsWidth);
                _btnBrowseOutputDir.Left = _txtOutputDir.Left + _txtOutputDir.Width + 8;
                _btnOpenOutputDir.Left = _btnBrowseOutputDir.Left + _btnBrowseOutputDir.Width + 8;
            };

            // Row 2: Action Buttons
            var bottomBar = new System.Windows.Forms.Panel { Dock = DockStyle.Bottom, Height = 44 };

            _btnPreflightCheck = new Button { Text = "🔍 Kiểm Tra Trước Khi Xuất", Left = 0, Top = 5, Width = 195, Height = 34, FlatStyle = FlatStyle.System };
            _btnPreflightCheck.Click += BtnPreflightCheck_Click;

            _btnExportStart = new Button
            {
                Text = "⚡ XUẤT BẢN VẼ (EXPORT NOW)",
                Left = 700,
                Top = 3,
                Width = 250,
                Height = 38,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            _btnExportStart.FlatAppearance.BorderSize = 0;
            _btnExportStart.Click += BtnExportStart_Click;

            _btnClose = new Button { Text = "Đóng", Left = 960, Top = 3, Width = 90, Height = 38, FlatStyle = FlatStyle.System };
            _btnClose.Click += (s, e) => Close();

            bottomBar.Controls.Add(_btnPreflightCheck);
            bottomBar.Controls.Add(_btnExportStart);
            bottomBar.Controls.Add(_btnClose);

            bottomBar.Resize += (s, e) =>
            {
                _btnClose.Left = bottomBar.Width - _btnClose.Width;
                _btnExportStart.Left = _btnClose.Left - _btnExportStart.Width - 10;
            };

            pnlBottomContainer.Controls.Add(pnlOutputDirBar);
            pnlBottomContainer.Controls.Add(bottomBar);

            Controls.Add(mainSplit);
            Controls.Add(pnlBottomContainer);
        }

        private void BuildGridColumns()
        {
            _gridSheets.Columns.Clear();

            var chkCol = new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsSelected",
                HeaderText = "In",
                Width = 45
            };
            _gridSheets.Columns.Add(chkCol);

            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusBadgeText", HeaderText = "Trạng Thái", Width = 90, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SheetNumber", HeaderText = "Số Sheet", Width = 95, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SheetName", HeaderText = "Tên Bản Vẽ", Width = 180, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CurrentRevisionNumber", HeaderText = "Rev", Width = 45, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaperSize", HeaderText = "Khổ Giấy", Width = 70, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ComputedFileName", HeaderText = "Tên File Xuất", Width = 190, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ExportStatusText", HeaderText = "Kết Quả", Width = 90, ReadOnly = true });
        }

        private void BuildTabFormats(TabPage tab)
        {
            var pnl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(8),
                AutoScroll = true
            };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Row 0: Export Formats (PDF / CAD DWG)
            pnl.Controls.Add(new Label { Text = "Định Dạng Cần Xuất:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, 0);
            var pnlFormats = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 3, 0, 8) };
            _chkExportPdf = new CheckBox { Text = "📄 Xuất File PDF (Revit Native)", Checked = true, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.DarkBlue, Margin = new Padding(0, 0, 15, 0) };
            _chkExportDwg = new CheckBox { Text = "📐 Xuất File CAD (AutoCAD DWG)", Checked = false, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.DarkGreen };
            pnlFormats.Controls.Add(_chkExportPdf);
            pnlFormats.Controls.Add(_chkExportDwg);
            pnl.Controls.Add(pnlFormats, 1, 0);

            // Row 1: CAD Export Setup
            pnl.Controls.Add(new Label { Text = "Cấu Hình Xuất CAD (DWG):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            var pnlDwgSetup = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 3, 0, 8) };
            _cmbDwgSetup = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Margin = new Padding(0, 0, 10, 0) };
            _cmbDwgSetup.Items.Add("In-Session Setup (Mặc định)");
            _cmbDwgSetup.SelectedIndex = 0;
            pnlDwgSetup.Controls.Add(_cmbDwgSetup);
            pnl.Controls.Add(pnlDwgSetup, 1, 1);

            // Row 2: Color Depth & Quality (PDF)
            pnl.Controls.Add(new Label { Text = "Màu Sắc & Độ Phân Giải:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            var pnlQuality = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 3, 0, 8) };
            _cmbColorDepth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Margin = new Padding(0, 0, 10, 0) };
            _cmbColorDepth.Items.AddRange(new object[] { "Màu (Color)", "Mức Xám (Grayscale)", "Đen Trắng (Black & White)" });
            _cmbColorDepth.SelectedIndex = 0;

            _cmbQualityDpi = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
            _cmbQualityDpi.Items.AddRange(new object[] { "300 DPI (Chuẩn In Ấn)", "600 DPI (Chất Lượng Cao)", "150 DPI (Bản Nháp)" });
            _cmbQualityDpi.SelectedIndex = 0;

            pnlQuality.Controls.Add(_cmbColorDepth);
            pnlQuality.Controls.Add(_cmbQualityDpi);
            pnl.Controls.Add(pnlQuality, 1, 2);

            // Row 3: Combine PDF
            pnl.Controls.Add(new Label { Text = "Gộp File PDF (Combine):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            var pnlCombine = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 3, 0, 8) };
            _chkCombinePdf = new CheckBox { Text = "Gộp các sheet vào 1 file:", Checked = false, AutoSize = true, Margin = new Padding(0, 3, 10, 0) };
            _txtCombinedFileName = new TextBox { Text = "Combined_Project_Sheets.pdf", Width = 230, Enabled = false };
            _chkCombinePdf.CheckedChanged += (s, e) => _txtCombinedFileName.Enabled = _chkCombinePdf.Checked;
            pnlCombine.Controls.Add(_chkCombinePdf);
            pnlCombine.Controls.Add(_txtCombinedFileName);
            pnl.Controls.Add(pnlCombine, 1, 3);

            // Row 4: Naming Template
            pnl.Controls.Add(new Label { Text = "Quy Tắc Đặt Tên File:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
            var pnlTemplateChoice = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 3, 0, 8) };
            _cmbNamingTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Margin = new Padding(0, 0, 10, 0) };
            _cmbNamingTemplate.SelectedIndexChanged += CmbNamingTemplate_SelectedIndexChanged;

            _btnEditNamingTemplate = new Button { Text = "⚙️ Sửa Template", Width = 110, Height = 26, FlatStyle = FlatStyle.System };
            _btnEditNamingTemplate.Click += BtnEditNamingTemplate_Click;

            pnlTemplateChoice.Controls.Add(_cmbNamingTemplate);
            pnlTemplateChoice.Controls.Add(_btnEditNamingTemplate);
            pnl.Controls.Add(pnlTemplateChoice, 1, 4);

            // Row 5: Project Code
            pnl.Controls.Add(new Label { Text = "Mã Dự Án (Project Code):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
            _txtProjectCode = new TextBox { Text = "PROJ", Width = 160, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 8) };
            _txtProjectCode.TextChanged += (s, e) => RecalculateFileNames();
            pnl.Controls.Add(_txtProjectCode, 1, 5);

            tab.Controls.Add(pnl);
        }

        private void BuildTabIssue(TabPage tab)
        {
            var pnl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(8),
                AutoScroll = true
            };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Issue Set Name
            pnl.Controls.Add(new Label { Text = "Tên Đợt Phát Hành:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _txtIssueSetName = new TextBox { Text = "IFC - Issued For Construction", Width = 300, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 8) };
            pnl.Controls.Add(_txtIssueSetName, 1, 0);

            // Transmittal Register
            pnl.Controls.Add(new Label { Text = "Bảng Kê Bản Vẽ (Excel):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _chkGenerateTransmittal = new CheckBox { Text = "Tự động tạo file Excel Bảng kê phát hành (Drawing Transmittal Register)", Checked = true, AutoSize = true, Margin = new Padding(0, 3, 0, 8) };
            pnl.Controls.Add(_chkGenerateTransmittal, 1, 1);

            // Technical QA Report
            pnl.Controls.Add(new Label { Text = "Báo Cáo Kỹ Thuật (QA):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            _chkGenerateQaReport = new CheckBox { Text = "Tự động tạo file Excel Nhật ký kiểm định QA (thời gian, dung lượng, lỗi)", Checked = false, AutoSize = true, Margin = new Padding(0, 3, 0, 8) };
            pnl.Controls.Add(_chkGenerateQaReport, 1, 2);

            // Max Retries
            pnl.Controls.Add(new Label { Text = "Tự Động Thử Lại (Retry):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            _numMaxRetries = new NumericUpDown { Minimum = 0, Maximum = 5, Value = 2, Width = 65, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 8) };
            pnl.Controls.Add(_numMaxRetries, 1, 3);

            tab.Controls.Add(pnl);
        }

        private void BuildTabAdvancedPdf(TabPage tab)
        {
            var pnl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(8),
                AutoScroll = true
            };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // PDF Bookmarks
            pnl.Controls.Add(new Label { Text = "Bookmarks Điều Hướng:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _chkAddBookmarks = new CheckBox { Text = "Tự động tạo Bookmark cây điều hướng theo Sheet No. & Tên Sheet (khi gộp PDF)", Checked = true, AutoSize = true, Margin = new Padding(0, 3, 0, 8) };
            pnl.Controls.Add(_chkAddBookmarks, 1, 0);

            // Cover Sheet
            pnl.Controls.Add(new Label { Text = "Trang Bìa Mục Lục:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _chkAutoCoverPage = new CheckBox { Text = "Tự động chèn Trang Bìa Mục Lục Bản Vẽ vào trang đầu tiên (khi gộp PDF)", Checked = false, AutoSize = true, Margin = new Padding(0, 3, 0, 8) };
            pnl.Controls.Add(_chkAutoCoverPage, 1, 1);

            // Watermark / Stamp
            pnl.Controls.Add(new Label { Text = "Đóng Dấu Watermark:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            var pnlWatermark = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 3, 0, 8) };
            _chkWatermark = new CheckBox { Text = "Đóng dấu chéo", Checked = false, AutoSize = true, Margin = new Padding(0, 3, 10, 0) };
            _cmbWatermarkPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 240, Enabled = false };
            _cmbWatermarkPreset.Items.AddRange(new object[] {
                "IFC - ISSUED FOR CONSTRUCTION",
                "S0 - PRELIMINARY DESIGN",
                "TENDER DOCUMENTS",
                "FOR APPROVAL",
                "AS-BUILT DRAWING",
                "CONFIDENTIAL"
            });
            _cmbWatermarkPreset.SelectedIndex = 0;
            _chkWatermark.CheckedChanged += (s, e) => _cmbWatermarkPreset.Enabled = _chkWatermark.Checked;

            pnlWatermark.Controls.Add(_chkWatermark);
            pnlWatermark.Controls.Add(_cmbWatermarkPreset);
            pnl.Controls.Add(pnlWatermark, 1, 2);

            tab.Controls.Add(pnl);
        }

        private void LoadDataFromRevit()
        {
            try
            {
                _allSheetItems = SheetCollectorService.GetAllSheets(_doc) ?? new List<SheetExportItem>();
                RevisionSnapshotService.CompareAndUpdateStatus(_doc, _allSheetItems);

                _namingTemplates = ExtensibleStorageService.LoadNamingTemplates(_doc) ?? NamingTemplate.GetBuiltInTemplates();
                _cmbNamingTemplate.Items.Clear();
                foreach (var t in _namingTemplates)
                {
                    _cmbNamingTemplate.Items.Add(t.Name);
                }
                if (_namingTemplates.Any()) _cmbNamingTemplate.SelectedIndex = 0;

                try
                {
                    var dwgSetups = BaseExportOptions.GetPredefinedSetupNames(_doc);
                    _cmbDwgSetup.Items.Clear();
                    _cmbDwgSetup.Items.Add("In-Session Setup (Mặc định)");
                    if (dwgSetups != null)
                    {
                        foreach (var s in dwgSetups)
                        {
                            _cmbDwgSetup.Items.Add(s);
                        }
                    }
                    _cmbDwgSetup.SelectedIndex = 0;
                }
                catch { }

                string projName = _doc.ProjectInformation?.Name ?? _doc.Title ?? "PROJ";
                if (!string.IsNullOrWhiteSpace(projName))
                {
                    _txtProjectCode.Text = projName.Replace(" ", "_");
                }

                _filteredSheetItems = new List<SheetExportItem>(_allSheetItems);
                _gridSheets.DataSource = _filteredSheetItems;
                RecalculateFileNames();
                UpdateSelectionCount();
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Lỗi Khởi Tạo Dữ Liệu Sheet", ex.Message, ex.StackTrace);
            }
        }

        private void ApplySearchFilter()
        {
            string query = _txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                _filteredSheetItems = new List<SheetExportItem>(_allSheetItems);
            }
            else
            {
                _filteredSheetItems = _allSheetItems
                    .Where(s => (s.SheetNumber != null && s.SheetNumber.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (s.SheetName != null && s.SheetName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
            }

            _gridSheets.DataSource = null;
            _gridSheets.DataSource = _filteredSheetItems;
            UpdateSelectionCount();
        }

        private void RecalculateFileNames()
        {
            if (_selectedTemplate == null || _allSheetItems == null) return;

            string prjCode = _txtProjectCode.Text.Trim();
            foreach (var item in _allSheetItems)
            {
                string computed = NamingTemplateManager.ComputeFileName(item, _selectedTemplate, prjCode);
                item.ComputedFileName = computed;
                item.IsRegexValid = NamingTemplateManager.ValidateFileNameRegex(computed, _selectedTemplate, out _);
            }
            _gridSheets.Refresh();
        }

        private void SetSelectionAll(bool select)
        {
            foreach (var item in _filteredSheetItems) item.IsSelected = select;
            _gridSheets.Refresh();
            UpdateSelectionCount();
        }

        private void InvertSelection()
        {
            foreach (var item in _filteredSheetItems) item.IsSelected = !item.IsSelected;
            _gridSheets.Refresh();
            UpdateSelectionCount();
        }

        private void SelectChangedSheetsOnly()
        {
            foreach (var item in _allSheetItems)
            {
                item.IsSelected = (item.IssueStatus == SheetIssueStatus.New || item.IssueStatus == SheetIssueStatus.Modified);
            }
            _gridSheets.Refresh();
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            int selectedCount = _allSheetItems.Count(s => s.IsSelected);
            _lblSelectionCount.Text = $"Đã chọn: {selectedCount} / {_allSheetItems.Count} sheets";
        }

        private void CmbNamingTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = _cmbNamingTemplate.SelectedIndex;
            if (idx >= 0 && idx < _namingTemplates.Count)
            {
                _selectedTemplate = _namingTemplates[idx];
                RecalculateFileNames();
            }
        }

        private void BtnEditNamingTemplate_Click(object sender, EventArgs e)
        {
            using var editForm = new NamingTemplateEditForm(_selectedTemplate);
            if (editForm.ShowDialog(this) == DialogResult.OK)
            {
                _selectedTemplate = editForm.Template;
                ExtensibleStorageService.SaveNamingTemplates(_doc, _namingTemplates);
                RecalculateFileNames();
            }
        }

        private void GridSheets_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridSheets.CurrentRow?.DataBoundItem is SheetExportItem item)
            {
                _lblThumbnailTitle.Text = $"Sheet: {item.SheetNumber} - {item.SheetName} ({item.PaperSize})";
                try
                {
                    _picThumbnail.Image?.Dispose();
                    _picThumbnail.Image = SheetPreviewService.GetSheetThumbnail(item.Sheet, _picThumbnail.Size);
                }
                catch { }
            }
        }

        private void BtnBrowseOutputDir_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = _txtOutputDir.Text;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _txtOutputDir.Text = dlg.SelectedPath;
            }
        }

        private void OpenOutputDirectory()
        {
            string outDir = _txtOutputDir.Text.Trim();
            if (Directory.Exists(outDir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", outDir) { UseShellExecute = true });
            }
            else
            {
                KhimDialogHelper.ShowInfo($"Thư mục chưa tồn tại:\n{outDir}");
            }
        }

        private void BtnPreflightCheck_Click(object sender, EventArgs e)
        {
            var selected = _allSheetItems.Where(s => s.IsSelected).ToList();
            if (!selected.Any())
            {
                KhimDialogHelper.ShowWarning("Pre-flight QA Check", "Vui lòng chọn ít nhất 1 sheet để kiểm tra.");
                return;
            }

            var warnings = PreflightCheckService.RunPreflightChecks(selected);
            if (!warnings.Any())
            {
                KhimDialogHelper.ShowSuccess("Pre-flight QA Check", "✔ Tất cả bản vẽ đã sẵn sàng. Không phát hiện bất thường về khổ giấy hay quy tắc đặt tên!");
            }
            else
            {
                string msg = string.Join("\n\n", warnings.Select(w => $"• {(w.IsCritical ? "🔴 CRITICAL" : "🟡 WARNING")}: {w.Title}\n  {w.Details}"));
                KhimDialogHelper.ShowWarning("Pre-flight QA Check Summary", msg);
            }
        }

        private void AppendLog(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendLog), text);
                return;
            }
            _rtbLogOutput.AppendText(text + "\n");
            _rtbLogOutput.SelectionStart = _rtbLogOutput.Text.Length;
            _rtbLogOutput.ScrollToCaret();
            Application.DoEvents();
        }

        private ColorDepthType GetSelectedColorDepth()
        {
            return _cmbColorDepth.SelectedIndex switch
            {
                1 => ColorDepthType.GrayScale,
                2 => ColorDepthType.BlackLine,
                _ => ColorDepthType.Color
            };
        }

        private void BtnExportStart_Click(object sender, EventArgs e)
        {
            var selectedItems = _allSheetItems.Where(s => s.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng tích chọn ít nhất 1 sheet bản vẽ trong danh sách để xuất.");
                return;
            }

            if (!_chkExportPdf.Checked && !_chkExportDwg.Checked)
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng chọn ít nhất 1 định dạng xuất (PDF hoặc AutoCAD DWG).");
                return;
            }

            string outDir = _txtOutputDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng chọn thư mục lưu file xuất.");
                return;
            }

            if (!Directory.Exists(outDir))
            {
                try { Directory.CreateDirectory(outDir); }
                catch (Exception exDir)
                {
                    KhimDialogHelper.ShowError("Không Thể Tạo Thư Mục", exDir.Message);
                    return;
                }
            }

            var options = new ExportOptions
            {
                ExportPdf = _chkExportPdf.Checked,
                ExportDwg = _chkExportDwg.Checked,
                DwgExportSetupName = _cmbDwgSetup.Text.Trim(),
                OutputDirectory = outDir,
                ProjectCode = _txtProjectCode.Text.Trim(),
                CombinePdf = _chkCombinePdf.Checked,
                CombinedPdfFileName = _txtCombinedFileName.Text.Trim(),
                AddBookmarks = _chkAddBookmarks.Checked,
                AutoCoverPage = _chkAutoCoverPage.Checked,
                ApplyWatermark = _chkWatermark.Checked,
                WatermarkText = _cmbWatermarkPreset.Text,
                IssueSetName = _txtIssueSetName.Text.Trim(),
                GenerateTransmittal = _chkGenerateTransmittal.Checked,
                GenerateQaReport = _chkGenerateQaReport.Checked,
                MaxRetryCount = (int)_numMaxRetries.Value
            };

            _rtbLogOutput.Clear();
            _progressBar.Visible = true;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = selectedItems.Count;
            _progressBar.Value = 0;

            AppendLog($"🚀 BẮT ĐẦU BATCH EXPORT [{DateTime.Now:HH:mm:ss}]");
            AppendLog($"📁 Thư mục xuất: {outDir}");
            AppendLog($"📊 Tổng số Sheet đã chọn: {selectedItems.Count}");
            AppendLog($"🎨 Chế độ màu: {_cmbColorDepth.Text}");

            _btnExportStart.Enabled = false;
            var colorDepth = GetSelectedColorDepth();

            try
            {
                if (options.ExportPdf && options.CombinePdf)
                {
                    // ── Chế độ Gộp 1 File PDF ────────────────────────────────
                    AppendLog("\n🧩 Đang xuất PDF Gộp tất cả sheet bằng Revit Native Exporter...");
                    string rawCombinedName = options.CombinedPdfFileName.Replace(".pdf", "").Trim();
                    if (string.IsNullOrEmpty(rawCombinedName)) rawCombinedName = "Combined_Project_Sheets";

                    var sheetsToCombine = selectedItems.Select(i => i.Sheet).ToList();
                    string combinedPath = PdfExportEngine.ExportCombinedSheets(_doc, sheetsToCombine, outDir, rawCombinedName, colorDepth);

                    if (options.AddBookmarks)
                    {
                        AppendLog("  🔖 Đang thêm Bookmarks cây điều hướng PDF...");
                        PdfPostProcessService.AddBookmarks(combinedPath, selectedItems);
                    }

                    if (options.AutoCoverPage)
                    {
                        AppendLog("  📑 Đang chèn Trang Bìa Mục Lục Bản Vẽ...");
                        PdfPostProcessService.InsertCoverSheet(combinedPath, options.IssueSetName, selectedItems);
                    }

                    if (options.ApplyWatermark && !string.IsNullOrWhiteSpace(options.WatermarkText))
                    {
                        AppendLog("  🌊 Đang đóng dấu Watermark cho PDF gộp...");
                        PdfPostProcessService.ApplyWatermark(combinedPath, options.WatermarkText);
                    }

                    foreach (var s in selectedItems)
                    {
                        s.ExportStatusText = "✔ Gộp thành công";
                        _progressBar.Value = Math.Min(_progressBar.Value + 1, _progressBar.Maximum);
                    }

                    AppendLog($"  ✓ Đã hoàn tất file PDF gộp: {Path.GetFileName(combinedPath)}");
                }
                else
                {
                    // ── Chế độ Xuất Từng File Riêng Biệt ────────────────────
                    var retryQueue = new ExportRetryQueue(options.MaxRetryCount);
                    int currentIdx = 0;

                    var qaEntries = retryQueue.ProcessBatch(_doc, selectedItems, options, msg =>
                    {
                        AppendLog(msg);
                        if (msg.StartsWith("  ✓ Hoàn tất"))
                        {
                            currentIdx++;
                            _progressBar.Value = Math.Min(currentIdx, _progressBar.Maximum);
                        }
                    });

                    // Generate QA Technical Log Excel nếu được chọn
                    if (options.GenerateQaReport)
                    {
                        AppendLog("\n📝 Đang sinh file Excel Báo cáo kỹ thuật QA Log...");
                        string qaPath = QaReportService.GenerateQaExcelReport(outDir, options.ProjectCode, qaEntries);
                        AppendLog($"  ✓ QA Log: {Path.GetFileName(qaPath)}");
                    }
                }

                // Generate Transmittal Register Excel
                if (options.GenerateTransmittal)
                {
                    AppendLog("\n📊 Đang sinh file Excel Bảng kê phát hành (Drawing Transmittal)...");
                    string transPath = TransmittalGeneratorService.GenerateExcelTransmittal(outDir, options.IssueSetName, options.ProjectCode, selectedItems);
                    AppendLog($"  ✓ Transmittal: {Path.GetFileName(transPath)}");
                }

                // Save Snapshot to ExtensibleStorage
                RevisionSnapshotService.CreateSnapshot(_doc, options.IssueSetName, selectedItems, options.ExportPdf ? "PDF" : "DWG");

                AppendLog("\n🎉 HOÀN TẤT TOÀN BỘ QUÁ TRÌNH BATCH EXPORT!");
                _progressBar.Value = _progressBar.Maximum;

                var result = MessageBox.Show(
                    $"Đã xuất thành công {selectedItems.Count} sheet bản vẽ vào thư mục:\n{outDir}\n\nBạn có muốn mở thư mục chứa file ngay bây giờ không?",
                    "Export Hoàn Tất",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    OpenOutputDirectory();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"\n❌ LỖI: {ex.Message}");
                KhimDialogHelper.ShowError("Lỗi Xuất File", ex.Message, ex.StackTrace);
            }
            finally
            {
                _btnExportStart.Enabled = true;
                _progressBar.Visible = false;
                _gridSheets.Refresh();
            }
        }
    }
}

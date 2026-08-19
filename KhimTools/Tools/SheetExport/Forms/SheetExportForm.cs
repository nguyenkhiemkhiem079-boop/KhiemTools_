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
    /// <summary>
    /// Giao diện Quản Lý Xuất & In Bản Vẽ Chuyên Nghiệp (Sheet Batch Exporter / Print Manager)
    /// chuẩn ProSheets / Xporter với thanh Sidebar (Select, Settings, Filter),
    /// tùy biến quy tắc đặt tên Naming Convention, cài đặt in Vector/Raster DPI, gộp/tách file PDF/DWG.
    /// </summary>
    public class SheetExportForm : System.Windows.Forms.Form
    {
        private readonly Document _doc;
        private List<SheetExportItem> _allSheetItems = new List<SheetExportItem>();
        private List<SheetExportItem> _filteredSheetItems = new List<SheetExportItem>();
        private readonly ExportOptions _options = new ExportOptions();

        // ── Navigation Sidebar ───────────────────────────────────────────────
        private System.Windows.Forms.Panel _sidebar;
        private Button _btnNavSelect;
        private Button _btnNavSettings;
        private Button _btnNavFilter;
        private System.Windows.Forms.Panel _contentContainer;

        // ── Main Views ───────────────────────────────────────────────────────
        private System.Windows.Forms.Panel _viewSelect;
        private System.Windows.Forms.Panel _viewSettings;
        private System.Windows.Forms.Panel _viewFilter;

        // ── View 1: Select UI Elements ───────────────────────────────────────
        // Left sub-panel
        private ComboBox _cmbSheetSet;
        private Button _btnUpdateSet;
        private Button _btnSaveSet;
        private TextBox _txtSheetSetName;
        private RadioButton _rbAllSheets;
        private RadioButton _rbAllViews;
        private ComboBox _cmbDisciplineFilter;
        private TextBox _txtSearchSheet;
        private Button _btnRefreshList;
        private CheckedListBox _chkListSheets;

        // Right sub-panel (File Settings & DataGrid)
        private RadioButton _rbSeparateFiles;
        private RadioButton _rbCombineFiles;
        private CheckBox _chkUseNamingConvention;
        private TextBox _txtNaming1;
        private TextBox _txtNaming2;
        private TextBox _txtNaming3;
        private TextBox _txtFileCombineName;
        private CheckBox _chkExportPdf;
        private CheckBox _chkExportDwg;
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
        private CheckBox _chkFilterStructure;
        private CheckBox _chkFilterArchitecture;
        private CheckBox _chkFilterMep;
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
            Text = "📄 KHIM TOOLS — Sheet Batch Export & Print Manager";
            Width = 1400;
            Height = 880;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new System.Drawing.Size(1200, 720);
            BackColor = Color.FromArgb(248, 249, 252);

            // 0. Top Banner Header
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Sheet Batch Export & Print Manager",
                "Bộ công cụ xuất in PDF & AutoCAD DWG hàng loạt, tự động nhận diện khổ giấy và quản lý bộ bản vẽ",
                "v2.5 Pro");
            Controls.Add(header);

            // 1. Sidebar Navigation (Left)
            BuildSidebar();

            // 2. Content Container (Center)
            _contentContainer = new System.Windows.Forms.Panel
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
            _sidebar = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Left,
                Width = 72,
                BackColor = Color.FromArgb(242, 244, 248),
                Padding = new Padding(4, 10, 4, 10)
            };

            _btnNavSelect = CreateSidebarButton("☑\nSelect", 0);
            _btnNavSettings = CreateSidebarButton("⚙\nSettings", 1);
            _btnNavFilter = CreateSidebarButton("🍸\nFilter", 2);

            _btnNavSelect.Top = 15;
            _btnNavSettings.Top = 85;
            _btnNavFilter.Top = 155;

            _sidebar.Controls.Add(_btnNavSelect);
            _sidebar.Controls.Add(_btnNavSettings);
            _sidebar.Controls.Add(_btnNavFilter);
        }

        private Button CreateSidebarButton(string text, int viewIndex)
        {
            var btn = new Button
            {
                Text = text,
                Width = 62,
                Height = 62,
                Left = 5,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 64, 75),
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

            _btnNavSelect.ForeColor = (viewIndex == 0) ? Color.FromArgb(0, 122, 255) : Color.FromArgb(60, 64, 75);
            _btnNavSettings.ForeColor = (viewIndex == 1) ? Color.FromArgb(0, 122, 255) : Color.FromArgb(60, 64, 75);
            _btnNavFilter.ForeColor = (viewIndex == 2) ? Color.FromArgb(0, 122, 255) : Color.FromArgb(60, 64, 75);
        }
        #endregion

        #region View 1: Select Tab
        private void BuildViewSelect()
        {
            _viewSelect = new System.Windows.Forms.Panel { Dock = DockStyle.Fill };

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Vertical,
                Padding = new Padding(6)
            };

            Shown += (s, e) =>
            {
                try
                {
                    if (split.Width > 850)
                    {
                        split.Panel1MinSize = 300;
                        split.Panel2MinSize = 450;
                        int dist = Math.Min(split.Width - 460, 380);
                        if (dist > 300) split.SplitterDistance = dist;
                    }
                }
                catch { }
            };

            // ── LEFT PANE: Sheet Sets & Sheet Checklist ──────────────────────
            var leftPane = split.Panel1;

            // GroupBox: Select by (ViewSheetSets)
            var grpSelectBy = new GroupBox
            {
                Text = "Select by",
                Dock = DockStyle.Top,
                Height = 100,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(8)
            };

            _cmbSheetSet = new ComboBox { Left = 10, Top = 22, Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _btnUpdateSet = new Button { Text = "Update Set", Left = 248, Top = 20, Width = 95, Height = 27, FlatStyle = FlatStyle.System };

            _btnSaveSet = new Button { Text = "Save ViewSheetSet", Left = 10, Top = 58, Width = 135, Height = 27, FlatStyle = FlatStyle.System, BackColor = Color.Black, ForeColor = Color.White };
            _txtSheetSetName = new TextBox { Text = "ViewSheetSet Name", Left = 152, Top = 60, Width = 190, Font = new Font("Segoe UI", 9F), ForeColor = Color.Gray };

            grpSelectBy.Controls.Add(_cmbSheetSet);
            grpSelectBy.Controls.Add(_btnUpdateSet);
            grpSelectBy.Controls.Add(_btnSaveSet);
            grpSelectBy.Controls.Add(_txtSheetSetName);

            // Filter Bar (All Sheets / All Views / Search)
            var pnlFilterBar = new System.Windows.Forms.Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(4) };

            _rbAllSheets = new RadioButton { Text = "All Sheets", Left = 10, Top = 8, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _rbAllViews = new RadioButton { Text = "All Views", Left = 110, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            _cmbDisciplineFilter = new ComboBox { Left = 10, Top = 35, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbDisciplineFilter.Items.AddRange(new object[] { "(All)", "Structure (KC)", "Architecture (KT)", "MEP" });
            _cmbDisciplineFilter.SelectedIndex = 0;
            _cmbDisciplineFilter.SelectedIndexChanged += (s, e) => ApplySearchAndFilter();

            _txtSearchSheet = new TextBox { Left = 138, Top = 35, Width = 165, Font = new Font("Segoe UI", 9F) };
            _txtSearchSheet.TextChanged += (s, e) => ApplySearchAndFilter();

            _btnRefreshList = new Button { Text = "🔄", Left = 308, Top = 34, Width = 35, Height = 26, FlatStyle = FlatStyle.System };
            _btnRefreshList.Click += (s, e) => LoadDataFromRevit();

            pnlFilterBar.Controls.Add(_rbAllSheets);
            pnlFilterBar.Controls.Add(_rbAllViews);
            pnlFilterBar.Controls.Add(_cmbDisciplineFilter);
            pnlFilterBar.Controls.Add(_txtSearchSheet);
            pnlFilterBar.Controls.Add(_btnRefreshList);

            // Sheet Checklist
            _chkListSheets = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F),
                IntegralHeight = false
            };
            _chkListSheets.ItemCheck += ChkListSheets_ItemCheck;

            // Quick select buttons below checklist
            var pnlQuickButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
            var btnSelectAll = new Button { Text = "Select All", Width = 80, Height = 26, FlatStyle = FlatStyle.System };
            var btnClearAll = new Button { Text = "Clear All", Width = 80, Height = 26, FlatStyle = FlatStyle.System };
            var btnInvert = new Button { Text = "Invert", Width = 80, Height = 26, FlatStyle = FlatStyle.System };
            btnSelectAll.Click += (s, e) => SetAllChecklistItems(true);
            btnClearAll.Click += (s, e) => SetAllChecklistItems(false);
            btnInvert.Click += (s, e) => InvertChecklistItems();
            pnlQuickButtons.Controls.Add(btnSelectAll);
            pnlQuickButtons.Controls.Add(btnClearAll);
            pnlQuickButtons.Controls.Add(btnInvert);

            leftPane.Controls.Add(_chkListSheets);
            leftPane.Controls.Add(pnlQuickButtons);
            leftPane.Controls.Add(pnlFilterBar);
            leftPane.Controls.Add(grpSelectBy);

            // ── RIGHT PANE: File Settings & DataGridView ─────────────────────
            var rightPane = split.Panel2;

            // GroupBox: File (Separate/Combine & Naming Convention & Formats)
            var grpFile = new GroupBox
            {
                Text = "File",
                Dock = DockStyle.Top,
                Height = 135,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(8)
            };

            _rbSeparateFiles = new RadioButton { Text = "Create separate files", Left = 15, Top = 20, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbCombineFiles = new RadioButton { Text = "Combine multiple views/sheets into a single file", Left = 220, Top = 20, AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F) };

            _chkUseNamingConvention = new CheckBox { Text = "Use naming convention:", Left = 15, Top = 48, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtNaming1 = new TextBox { Text = DateTime.Now.ToString("yyMMdd"), Left = 190, Top = 46, Width = 80, Font = new Font("Segoe UI", 9F) };
            var lblSep1 = new Label { Text = "—", Left = 275, Top = 48, Width = 20, AutoSize = true };
            _txtNaming2 = new TextBox { Text = "SPRINGVALE", Left = 295, Top = 46, Width = 110, Font = new Font("Segoe UI", 9F) };
            var lblSep2 = new Label { Text = "—", Left = 410, Top = 48, Width = 20, AutoSize = true };
            _txtNaming3 = new TextBox { Text = "FOUNDATION SECTIONS", Left = 430, Top = 46, Width = 220, Font = new Font("Segoe UI", 9F) };

            _txtNaming1.TextChanged += (s, e) => UpdateCombineFileName();
            _txtNaming2.TextChanged += (s, e) => UpdateCombineFileName();
            _txtNaming3.TextChanged += (s, e) => UpdateCombineFileName();

            var lblCombine = new Label { Text = "File Combine Name :", Left = 15, Top = 78, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtFileCombineName = new TextBox { Text = "Combined_Sheets.pdf", Left = 190, Top = 75, Width = 460, Font = new Font("Segoe UI", 9F) };

            _chkExportPdf = new CheckBox { Text = "PDF", Left = 15, Top = 106, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.DarkBlue };
            _chkExportDwg = new CheckBox { Text = "DWG", Left = 75, Top = 106, AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.DarkGreen };

            grpFile.Controls.Add(_rbSeparateFiles);
            grpFile.Controls.Add(_rbCombineFiles);
            grpFile.Controls.Add(_chkUseNamingConvention);
            grpFile.Controls.Add(_txtNaming1);
            grpFile.Controls.Add(lblSep1);
            grpFile.Controls.Add(_txtNaming2);
            grpFile.Controls.Add(lblSep2);
            grpFile.Controls.Add(_txtNaming3);
            grpFile.Controls.Add(lblCombine);
            grpFile.Controls.Add(_txtFileCombineName);
            grpFile.Controls.Add(_chkExportPdf);
            grpFile.Controls.Add(_chkExportDwg);

            // DataGridView: Main Sheet Table
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

            rightPane.Controls.Add(_gridSheets);
            rightPane.Controls.Add(grpFile);

            _viewSelect.Controls.Add(split);
        }

        private void BuildGridColumns()
        {
            _gridSheets.Columns.Clear();

            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Index", Width = 55, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SheetNumber", HeaderText = "Number/Id", Width = 110, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SheetName", HeaderText = "Sheet Name/View Name", Width = 230, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CurrentRevisionNumber", HeaderText = "Revision", Width = 65, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CurrentRevisionDate", HeaderText = "RevisionDate", Width = 95, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaperSize", HeaderText = "Size", Width = 70, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Format", Width = 75, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Orientation", Width = 90, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ExportStatusText", HeaderText = "Progress", Width = 110, ReadOnly = true });
        }
        #endregion

        #region View 2: Settings Tab (PDF & DWG)
        private void BuildViewSettings()
        {
            _viewSettings = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            _tabSettings = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

            // Subtab PDF
            var tabPdf = new TabPage { Text = "    PDF    ", Padding = new Padding(12), BackColor = Color.White };
            BuildPdfSettingsTab(tabPdf);
            _tabSettings.TabPages.Add(tabPdf);

            // Subtab DWG
            var tabDwg = new TabPage { Text = "    DWG    ", Padding = new Padding(12), BackColor = Color.White };
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
            var grpPlacement = new GroupBox { Text = "Paper Placement", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            _rbPlacementCenter = new RadioButton { Text = "Center", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbPlacementOffset = new RadioButton { Text = "Offset from corner", Left = 15, Top = 50, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };

            _rbMarginNoMargin = new RadioButton { Text = "No Margin", Left = 35, Top = 75, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _rbMarginPrinterLimit = new RadioButton { Text = "Printer limit", Left = 130, Top = 75, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbMarginUserDefined = new RadioButton { Text = "User Defined", Left = 225, Top = 75, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            var lblX = new Label { Text = "X (mm) -", Left = 15, Top = 110, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numMarginX = new NumericUpDown { Left = 75, Top = 108, Width = 65, Value = 0 };
            var lblY = new Label { Text = "Y (mm) -", Left = 155, Top = 110, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numMarginY = new NumericUpDown { Left = 215, Top = 108, Width = 65, Value = 0 };

            grpPlacement.Controls.Add(_rbPlacementCenter);
            grpPlacement.Controls.Add(_rbPlacementOffset);
            grpPlacement.Controls.Add(_rbMarginNoMargin);
            grpPlacement.Controls.Add(_rbMarginPrinterLimit);
            grpPlacement.Controls.Add(_rbMarginUserDefined);
            grpPlacement.Controls.Add(lblX);
            grpPlacement.Controls.Add(_numMarginX);
            grpPlacement.Controls.Add(lblY);
            grpPlacement.Controls.Add(_numMarginY);

            // 2. GroupBox: Zoom
            var grpZoom = new GroupBox { Text = "Zoom", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            _rbZoomFitToPage = new RadioButton { Text = "Fit to Page", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbZoomPercent = new RadioButton { Text = "Zoom", Left = 15, Top = 55, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _numZoomPercent = new NumericUpDown { Left = 80, Top = 53, Width = 60, Minimum = 10, Maximum = 500, Value = 100 };
            var lblPercent = new Label { Text = "% Size", Left = 150, Top = 55, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            grpZoom.Controls.Add(_rbZoomFitToPage);
            grpZoom.Controls.Add(_rbZoomPercent);
            grpZoom.Controls.Add(_numZoomPercent);
            grpZoom.Controls.Add(lblPercent);

            // 3. GroupBox: Printer
            var grpPrinter = new GroupBox { Text = "Printer", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            var lblPrinter = new Label { Text = "Printer", Left = 15, Top = 30, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbPrinter = new ComboBox { Left = 75, Top = 27, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbPrinter.Items.Add("Revit PDF Native (Vector DPI300)");
            _cmbPrinter.SelectedIndex = 0;
            grpPrinter.Controls.Add(lblPrinter);
            grpPrinter.Controls.Add(_cmbPrinter);

            // 4. GroupBox: Hidden Line Views
            var grpHiddenLines = new GroupBox { Text = "Hidden Line Views", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            var lblRemoveLines = new Label { Text = "Remove Lines Using", Left = 15, Top = 22, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbVectorProcessing = new RadioButton { Text = "Vector Processing", Left = 15, Top = 45, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _rbRasterProcessing = new RadioButton { Text = "Raster Processing", Left = 15, Top = 72, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            grpHiddenLines.Controls.Add(lblRemoveLines);
            grpHiddenLines.Controls.Add(_rbVectorProcessing);
            grpHiddenLines.Controls.Add(_rbRasterProcessing);

            // 5. GroupBox: Appearance
            var grpAppearance = new GroupBox { Text = "Appearance", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            var lblRasterQ = new Label { Text = "Raster Quality", Left = 15, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbRasterQuality = new ComboBox { Left = 120, Top = 25, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbRasterQuality.Items.AddRange(new object[] { "Presentation", "High", "Medium", "Low" });
            _cmbRasterQuality.SelectedIndex = 0;

            var lblColors = new Label { Text = "Colors", Left = 15, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbColorDepth = new ComboBox { Left = 120, Top = 59, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbColorDepth.Items.AddRange(new object[] { "Color", "Grayscale", "Black Lines" });
            _cmbColorDepth.SelectedIndex = 0;

            grpAppearance.Controls.Add(lblRasterQ);
            grpAppearance.Controls.Add(_cmbRasterQuality);
            grpAppearance.Controls.Add(lblColors);
            grpAppearance.Controls.Add(_cmbColorDepth);

            // 6. GroupBox: Options
            var grpOptions = new GroupBox { Text = "Options", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(10) };
            _chkViewLinksInBlue = new CheckBox { Text = "View links in blue (Color prints only)", Left = 15, Top = 25, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkHideRefPlanes = new CheckBox { Text = "Hide ref/work planes", Left = 15, Top = 50, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkHideUnreferencedTags = new CheckBox { Text = "Hide unreferenced view tags", Left = 15, Top = 75, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkHideScopeBoxes = new CheckBox { Text = "Hide scope boxes", Left = 15, Top = 100, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkHideCropBoundaries = new CheckBox { Text = "Hide crop boundaries", Left = 15, Top = 125, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkReplaceHalftone = new CheckBox { Text = "Replace halftone with thin lines", Left = 15, Top = 150, AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F) };
            _chkMaskCoincidentLines = new CheckBox { Text = "Region edges mask coincident lines", Left = 15, Top = 175, AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F) };

            grpOptions.Controls.Add(_chkViewLinksInBlue);
            grpOptions.Controls.Add(_chkHideRefPlanes);
            grpOptions.Controls.Add(_chkHideUnreferencedTags);
            grpOptions.Controls.Add(_chkHideScopeBoxes);
            grpOptions.Controls.Add(_chkHideCropBoundaries);
            grpOptions.Controls.Add(_chkReplaceHalftone);
            grpOptions.Controls.Add(_chkMaskCoincidentLines);

            // Add to TableLayoutPanel grid
            pnlContainer.Controls.Add(grpPlacement, 0, 0);
            pnlContainer.Controls.Add(grpZoom, 0, 1);
            pnlContainer.Controls.Add(grpPrinter, 0, 2);

            pnlContainer.Controls.Add(grpHiddenLines, 1, 0);
            pnlContainer.Controls.Add(grpAppearance, 1, 1);

            pnlContainer.Controls.Add(grpOptions, 2, 0);
            pnlContainer.SetRowSpan(grpOptions, 3);

            tab.Controls.Add(pnlContainer);
        }

        private void BuildDwgSettingsTab(TabPage tab)
        {
            var pnlDwg = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

            var grpDwgSetup = new GroupBox { Text = "AutoCAD DWG Export Options", Left = 15, Top = 15, Width = 550, Height = 220, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(12) };

            var lblSetup = new Label { Text = "DWG Export Setup (Layer Mapping):", Left = 15, Top = 30, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbDwgSetup = new ComboBox { Left = 15, Top = 55, Width = 380, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbDwgSetup.Items.Add("In-Session Setup (Mặc định)");
            _cmbDwgSetup.SelectedIndex = 0;

            var lblVersion = new Label { Text = "AutoCAD Version Format:", Left = 15, Top = 95, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAutoCadVersion = new ComboBox { Left = 15, Top = 120, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbAutoCadVersion.Items.AddRange(new object[] { "AutoCAD 2018 DWG (*.dwg)", "AutoCAD 2013 DWG (*.dwg)", "AutoCAD 2010 DWG (*.dwg)" });
            _cmbAutoCadVersion.SelectedIndex = 0;

            _chkDwgMergedViews = new CheckBox { Text = "Merge all views and links into single DWG file (No external Xrefs)", Left = 15, Top = 165, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };

            grpDwgSetup.Controls.Add(lblSetup);
            grpDwgSetup.Controls.Add(_cmbDwgSetup);
            grpDwgSetup.Controls.Add(lblVersion);
            grpDwgSetup.Controls.Add(_cmbAutoCadVersion);
            grpDwgSetup.Controls.Add(_chkDwgMergedViews);

            pnlDwg.Controls.Add(grpDwgSetup);
            tab.Controls.Add(pnlDwg);
        }
        #endregion

        #region View 3: Filter Tab
        private void BuildViewFilter()
        {
            _viewFilter = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

            var grpFilter = new GroupBox { Text = "Bộ Lọc Nhanh Bản Vẽ (Quick Sheet Filter)", Left = 20, Top = 20, Width = 600, Height = 250, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Padding = new Padding(15) };

            _chkFilterStructure = new CheckBox { Text = "Kết cấu (Structure - KC/ST)", Left = 20, Top = 35, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkFilterArchitecture = new CheckBox { Text = "Kiến trúc (Architecture - KT/AR)", Left = 20, Top = 70, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkFilterMep = new CheckBox { Text = "Cơ điện (MEP - ME/EL/PL)", Left = 20, Top = 105, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };
            _chkFilterModifiedOnly = new CheckBox { Text = "Chỉ lọc các sheet Có Thay Đổi / Mới (Modified/New Sheets)", Left = 20, Top = 150, AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkSlateBlue };

            _chkFilterStructure.CheckedChanged += (s, e) => ApplySearchAndFilter();
            _chkFilterArchitecture.CheckedChanged += (s, e) => ApplySearchAndFilter();
            _chkFilterMep.CheckedChanged += (s, e) => ApplySearchAndFilter();
            _chkFilterModifiedOnly.CheckedChanged += (s, e) => ApplySearchAndFilter();

            grpFilter.Controls.Add(_chkFilterStructure);
            grpFilter.Controls.Add(_chkFilterArchitecture);
            grpFilter.Controls.Add(_chkFilterMep);
            grpFilter.Controls.Add(_chkFilterModifiedOnly);

            _viewFilter.Controls.Add(grpFilter);
        }
        #endregion

        #region Bottom Print Bar
        private System.Windows.Forms.Control BuildBottomPrintBar()
        {
            var grpPrint = new GroupBox
            {
                Text = "Print",
                Dock = DockStyle.Bottom,
                Height = 115,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(10, 4, 10, 6),
                BackColor = Color.FromArgb(248, 249, 252)
            };

            // Line 1: Split options & Timeout
            _rbSaveSameFolder = new RadioButton { Text = "Save all files in the same folder location", Left = 15, Top = 18, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _rbSplitFoldersByFormat = new RadioButton { Text = "Save and split files by file format", Left = 360, Top = 18, AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F) };

            var lblTimeout = new Label { Text = "Print timeout (s) :", Left = 860, Top = 20, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numTimeoutSeconds = new NumericUpDown { Left = 970, Top = 17, Width = 60, Minimum = 10, Maximum = 600, Value = 120 };

            // Line 2: Folder selector + Summary + Print button
            _btnOpenFolderSelection = new Button
            {
                Text = "Open Folder Selection",
                Left = 15,
                Top = 45,
                Width = 160,
                Height = 30,
                BackColor = Color.Black,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnOpenFolderSelection.Click += (s, e) => OpenOutputDirectory();

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KhimTools_Export");
            _txtOutputDirectory = new TextBox { Text = defaultDir, Left = 185, Top = 47, Width = 660, Font = new Font("Segoe UI", 9F) };

            _btnBrowseFolder = new Button { Text = "..", Left = 852, Top = 46, Width = 35, Height = 26, FlatStyle = FlatStyle.System, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _btnBrowseFolder.Click += BtnBrowseFolder_Click;

            _lblTotalSummary = new Label
            {
                Text = "0 / 0 Sheets and 0 / 0 Views selected. Total: 0",
                Left = 185,
                Top = 82,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.DimGray
            };

            _btnPrint = new Button
            {
                Text = "Print",
                Left = 910,
                Top = 42,
                Width = 120,
                Height = 35,
                BackColor = Color.Black,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPrint.FlatAppearance.BorderSize = 0;
            _btnPrint.Click += BtnPrint_Click;

            grpPrint.Controls.Add(_rbSaveSameFolder);
            grpPrint.Controls.Add(_rbSplitFoldersByFormat);
            grpPrint.Controls.Add(lblTimeout);
            grpPrint.Controls.Add(_numTimeoutSeconds);
            grpPrint.Controls.Add(_btnOpenFolderSelection);
            grpPrint.Controls.Add(_txtOutputDirectory);
            grpPrint.Controls.Add(_btnBrowseFolder);
            grpPrint.Controls.Add(_lblTotalSummary);
            grpPrint.Controls.Add(_btnPrint);

            grpPrint.Resize += (s, e) =>
            {
                _btnPrint.Left = grpPrint.Width - _btnPrint.Width - 25;
                _numTimeoutSeconds.Left = _btnPrint.Left;
                lblTimeout.Left = _numTimeoutSeconds.Left - lblTimeout.Width - 10;
                _btnBrowseFolder.Left = _btnPrint.Left - _btnBrowseFolder.Width - 15;
                _txtOutputDirectory.Width = Math.Max(200, _btnBrowseFolder.Left - _txtOutputDirectory.Left - 8);
            };

            return grpPrint;
        }
        #endregion

        #region Data Loading & Synchronization
        private void LoadDataFromRevit()
        {
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

            if (discipline.StartsWith("Structure"))
            {
                list = list.Where(s => s.SheetNumber != null && (s.SheetNumber.StartsWith("KC") || s.SheetNumber.StartsWith("ST") || s.SheetNumber.StartsWith("S-")));
            }
            else if (discipline.StartsWith("Architecture"))
            {
                list = list.Where(s => s.SheetNumber != null && (s.SheetNumber.StartsWith("KT") || s.SheetNumber.StartsWith("AR") || s.SheetNumber.StartsWith("A-")));
            }

            if (_chkFilterModifiedOnly.Checked)
            {
                list = list.Where(s => s.IssueStatus == SheetIssueStatus.New || s.IssueStatus == SheetIssueStatus.Modified);
            }

            _filteredSheetItems = list.ToList();

            // Populate CheckedListBox
            _chkListSheets.Items.Clear();
            for (int i = 0; i < _filteredSheetItems.Count; i++)
            {
                var item = _filteredSheetItems[i];
                string display = $"{item.SheetNumber} - {item.SheetName}";
                _chkListSheets.Items.Add(display, item.IsSelected);
            }

            RefreshGridRows();
            UpdateSummaryLabel();
        }

        private void ChkListSheets_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.Index >= 0 && e.Index < _filteredSheetItems.Count)
            {
                _filteredSheetItems[e.Index].IsSelected = (e.NewValue == CheckState.Checked);
                BeginInvoke(new Action(() =>
                {
                    RefreshGridRows();
                    UpdateSummaryLabel();
                }));
            }
        }

        private void SetAllChecklistItems(bool state)
        {
            for (int i = 0; i < _chkListSheets.Items.Count; i++)
            {
                _chkListSheets.SetItemChecked(i, state);
                if (i < _filteredSheetItems.Count) _filteredSheetItems[i].IsSelected = state;
            }
            RefreshGridRows();
            UpdateSummaryLabel();
        }

        private void InvertChecklistItems()
        {
            for (int i = 0; i < _chkListSheets.Items.Count; i++)
            {
                bool nextState = !_chkListSheets.GetItemChecked(i);
                _chkListSheets.SetItemChecked(i, nextState);
                if (i < _filteredSheetItems.Count) _filteredSheetItems[i].IsSelected = nextState;
            }
            RefreshGridRows();
            UpdateSummaryLabel();
        }

        private void RefreshGridRows()
        {
            var selected = _allSheetItems.Where(s => s.IsSelected).ToList();
            _gridSheets.Rows.Clear();

            int idx = 1;
            string fmt = (_chkExportPdf.Checked && _chkExportDwg.Checked) ? "PDF / DWG" : (_chkExportPdf.Checked ? "PDF" : "DWG");

            foreach (var item in selected)
            {
                int r = _gridSheets.Rows.Add();
                _gridSheets.Rows[r].Cells[0].Value = idx++;
                _gridSheets.Rows[r].Cells[1].Value = item.SheetNumber;
                _gridSheets.Rows[r].Cells[2].Value = item.SheetName;
                _gridSheets.Rows[r].Cells[3].Value = item.CurrentRevisionNumber;
                _gridSheets.Rows[r].Cells[4].Value = item.CurrentRevisionDate;
                _gridSheets.Rows[r].Cells[5].Value = item.PaperSize;
                _gridSheets.Rows[r].Cells[6].Value = fmt;
                _gridSheets.Rows[r].Cells[7].Value = "Landscape";
                _gridSheets.Rows[r].Cells[8].Value = item.ExportStatusText ?? "Sẵn sàng";
            }
        }

        private void UpdateSummaryLabel()
        {
            int selectedSheets = _allSheetItems.Count(s => s.IsSelected);
            int totalSheets = _allSheetItems.Count;
            _lblTotalSummary.Text = $"{selectedSheets} / {totalSheets} Sheets and 0 / 0 Views selected. Total: {totalSheets}";
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
            _options.SplitFoldersByFormat = _rbSplitFoldersByFormat.Checked;
            _options.CombinePdf = _rbCombineFiles.Checked;
            _options.CombinedPdfFileName = _txtFileCombineName.Text.Trim();

            _options.PaperPlacementCenter = _rbPlacementCenter.Checked;
            _options.PaperPlacementOffset = _rbPlacementOffset.Checked;
            _options.MarginNoMargin = _rbMarginNoMargin.Checked;
            _options.MarginOffsetX = (double)_numMarginX.Value;
            _options.MarginOffsetY = (double)_numMarginY.Value;

            _options.ZoomFitToPage = _rbZoomFitToPage.Checked;
            _options.ZoomPercentage = (int)_numZoomPercent.Value;
            _options.VectorProcessing = _rbVectorProcessing.Checked;
            _options.RasterQuality = _cmbRasterQuality.SelectedItem?.ToString() ?? "Presentation";
            _options.ColorMode = _cmbColorDepth.SelectedItem?.ToString() ?? "Color";

            _options.ViewLinksInBlue = _chkViewLinksInBlue.Checked;
            _options.HideRefPlanes = _chkHideRefPlanes.Checked;
            _options.HideUnreferencedViewTags = _chkHideUnreferencedTags.Checked;
            _options.HideScopeBoxes = _chkHideScopeBoxes.Checked;
            _options.HideCropBoundaries = _chkHideCropBoundaries.Checked;
            _options.ReplaceHalftoneWithThinLines = _chkReplaceHalftone.Checked;
            _options.MaskCoincidentLines = _chkMaskCoincidentLines.Checked;

            _btnPrint.Enabled = false;
            _btnPrint.Text = "Exporting...";
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

                // Generate Transmittal Excel
                string transPath = TransmittalGeneratorService.GenerateExcelTransmittal(outDir, "Official Release", _txtNaming2.Text, selectedItems);

                Cursor = Cursors.Default;
                KhimDialogHelper.ShowSuccess(
                    "Hoàn Tất Xuất Bản Vẽ",
                    $"Đã xuất thành công {selectedItems.Count} bản vẽ sang thư mục:\n{outDir}\n\nĐã tạo bảng kê phát hành Transmittal Register!");

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
                _btnPrint.Text = "Print";
                RefreshGridRows();
            }
        }
        #endregion
    }
}

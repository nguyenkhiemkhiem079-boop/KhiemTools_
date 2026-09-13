using KhimTools.Core.UI;
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
using Control = System.Windows.Forms.Control;

namespace KhimTools.SheetExport.Forms
{
    public class SheetExportForm : KTBaseForm
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
        // ── Main Views ───────────────────────────────────────────────────────
        private Panel _viewSelect;
        private Panel _viewSettings;
        private Panel _viewFilter;

        // ── View 1: Select UI Elements ───────────────────────────────────────
        private Button _btnSaveSelection;
        private Button _btnLoadSelection;
        private Label _lblCurrentSelectionFile;
        private ComboBox _cmbDisciplineFilter;
        private ComboBox _cmbPaperFilter;
        private ComboBox _cmbIssueStatusFilter;
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
        private TextBox _txtNamingPattern;
        private Label _lblNamingPreview;
        private TextBox _txtIssueSetName;
        private DateTimePicker _dtpIssueDate;
        private ComboBox _cmbPdfExportQuality;
        private CheckBox _chkPreservePrevious;
        private TextBox _txtFileCombineName;
        private RadioButton _rbFormatPdf;
        private RadioButton _rbFormatDwg;
        private RadioButton _rbFormatBoth;
        private CheckBox _chkExportPdf;
        private CheckBox _chkExportDwg;
        private ProgressBar _progressBar;
        private Label _lblProgressStatus;
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
        private CheckBox _chkAutoDisableTempViewModes;

        // DWG Settings
        private ComboBox _cmbDwgSetup;
        private ComboBox _cmbAutoCadVersion;
        private CheckBox _chkDwgMergedViews;

        // ── View 3: Filter UI Elements ───────────────────────────────────────
        private CheckBox _chkFilterModifiedOnly;

        // ── Bottom Bar UI Elements (Print Bar) ───────────────────────────────
        private Button _btnOpenFolderSelection;
        private TextBox _txtOutputDirectory;
        private Button _btnBrowseFolder;
        private Label _lblTotalSummary;
        private Button _btnPrint;
        private Form _advancedSettingsForm;
        private readonly ToolTip _toolTips = new ToolTip();

        public SheetExportForm(Document doc)
            : this(doc ?? throw new ArgumentNullException(nameof(doc)), true)
        {
        }

        private SheetExportForm(Document doc, bool loadDocument)
        {
            _doc = doc;

            KhimUiStyle.ApplyFormTheme(this);
            InitializeComponentsCustom();
            if (loadDocument) LoadDataFromRevit();
        }

        // The layout fixture uses the actual controls without a Revit document or export side effects.
        internal static SheetExportForm CreateLayoutPreview()
        {
            var form = new SheetExportForm(null, false);
            form._btnPrint.Enabled = false;
            form._btnRefreshList.Enabled = false;
            form._btnSaveSelection.Enabled = false;
            form._btnLoadSelection.Enabled = false;
            return form;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _advancedSettingsForm?.Dispose();
                _toolTips.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponentsCustom()
        {
            Text = "Print & Export Sheets";
            Width = 1360;
            Height = 820;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(1120, 700);
            BackColor = KhimUiStyle.FormBg;
            Font = new Font("Segoe UI", 9F);

            BuildUnifiedWorkspace();
            EnsureAdvancedSettingsForm();
        }

        private void BuildUnifiedWorkspace()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = KhimUiStyle.FormBg,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

            root.Controls.Add(BuildWorkflowToolbar(), 0, 0);

            var workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = KhimUiStyle.FormBg,
                Margin = new Padding(0)
            };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            var sheetWorkspace = BuildSheetWorkspace();
            sheetWorkspace.Margin = new Padding(0, 0, 6, 0);
            var setupPanel = BuildExportSetupPanel();
            setupPanel.Margin = new Padding(6, 0, 0, 0);
            workspace.Controls.Add(sheetWorkspace, 0, 0);
            workspace.Controls.Add(setupPanel, 1, 0);
            root.Controls.Add(workspace, 0, 1);
            root.Controls.Add(BuildUnifiedFooter(), 0, 2);
            Controls.Add(root);
        }

        private Control BuildWorkflowToolbar()
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(10, 8, 10, 6),
                Margin = new Padding(0, 0, 0, 8)
            };

            bar.Controls.Add(new Label { Text = "Định dạng", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, 6, 8, 0) });
            _rbFormatPdf = CreateFormatOption("PDF", true);
            _rbFormatDwg = CreateFormatOption("DWG", false);
            _rbFormatBoth = CreateFormatOption("PDF + DWG", false);
            _rbFormatPdf.CheckedChanged += (s, e) => { if (_rbFormatPdf.Checked) RefreshGridRows(); };
            _rbFormatDwg.CheckedChanged += (s, e) => { if (_rbFormatDwg.Checked) RefreshGridRows(); };
            _rbFormatBoth.CheckedChanged += (s, e) => { if (_rbFormatBoth.Checked) RefreshGridRows(); };
            _rbFormatPdf.CheckedChanged += (s, e) => UpdateFormatAvailability();
            _rbFormatDwg.CheckedChanged += (s, e) => UpdateFormatAvailability();
            _rbFormatBoth.CheckedChanged += (s, e) => UpdateFormatAvailability();

            _btnSaveSelection = CreateSecondaryButton("Lưu bộ chọn", 96);
            _btnSaveSelection.Margin = new Padding(22, 0, 4, 0);
            _btnSaveSelection.Click += (s, e) => SaveSelectionDialog();
            _btnLoadSelection = CreateSecondaryButton("Mở bộ chọn", 96);
            _btnLoadSelection.Click += (s, e) => LoadSelectionDialog();
            _lblCurrentSelectionFile = new Label
            {
                Text = "Chưa dùng bộ chọn đã lưu",
                AutoSize = false,
                AutoEllipsis = true,
                Width = 280,
                Height = 24,
                ForeColor = KhimUiStyle.TextSecondary,
                Margin = new Padding(10, 6, 0, 0)
            };

            bar.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                _rbFormatPdf, _rbFormatDwg, _rbFormatBoth,
                _btnSaveSelection, _btnLoadSelection, _lblCurrentSelectionFile
            });
            return bar;
        }

        private Control BuildSheetWorkspace()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 4, 0, 6)
            };

            _txtSearchSheet = new TextBox { Width = 205, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 2, 8, 0) };
            _toolTips.SetToolTip(_txtSearchSheet, "Tìm theo số hiệu hoặc tên sheet");
            _txtSearchSheet.TextChanged += (s, e) => ApplySearchAndFilter();
            _cmbDisciplineFilter = new ComboBox { Width = 205, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 1, 8, 0) };
            _cmbDisciplineFilter.SelectedIndexChanged += (s, e) => { if (!_isLoading) ApplySearchAndFilter(); };
            _cmbPaperFilter = new ComboBox { Width = 105, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 1, 8, 0) };
            _cmbPaperFilter.SelectedIndexChanged += (s, e) => { if (!_isLoading) ApplySearchAndFilter(); };
            _cmbIssueStatusFilter = new ComboBox { Width = 125, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 1, 8, 0) };
            _cmbIssueStatusFilter.Items.AddRange(new object[] { "Mọi trạng thái", "Mới", "Đã sửa", "Không đổi" });
            _cmbIssueStatusFilter.SelectedIndex = 0;
            _cmbIssueStatusFilter.SelectedIndexChanged += (s, e) => { if (!_isLoading) ApplySearchAndFilter(); };
            _chkFilterModifiedOnly = new CheckBox { Text = "Chỉ sheet thay đổi", AutoSize = true, Margin = new Padding(2, 5, 10, 0) };
            _chkFilterModifiedOnly.CheckedChanged += (s, e) => ApplySearchAndFilter();
            _btnSelectAll = CreateSecondaryButton("Chọn tất cả", 88);
            _btnClearAll = CreateSecondaryButton("Bỏ chọn", 78);
            _btnInvert = CreateSecondaryButton("Đảo chọn", 78);
            _btnRefreshList = CreateSecondaryButton("Làm mới", 76);
            _btnSelectAll.Click += (s, e) => SetAllGridItems(true);
            _btnClearAll.Click += (s, e) => SetAllGridItems(false);
            _btnInvert.Click += (s, e) => InvertGridItems();
            _btnRefreshList.Click += (s, e) => LoadDataFromRevit();
            toolbar.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                _txtSearchSheet, _cmbDisciplineFilter, _cmbPaperFilter, _cmbIssueStatusFilter, _chkFilterModifiedOnly,
                _btnSelectAll, _btnClearAll, _btnInvert, _btnRefreshList
            });

            _gridSheets = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(226, 232, 240),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowTemplate = { Height = 30 }
            };
            _gridSheets.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _gridSheets.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _gridSheets.EnableHeadersVisualStyles = false;
            BuildGridColumns();
            _gridSheets.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0 && e.RowIndex < _filteredSheetItems.Count)
                {
                    _filteredSheetItems[e.RowIndex].IsSelected = Convert.ToBoolean(_gridSheets.Rows[e.RowIndex].Cells[0].Value);
                    UpdateSummaryLabel();
                    UpdateNamingPreview();
                }
            };
            _gridSheets.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_gridSheets.IsCurrentCellDirty && _gridSheets.CurrentCellAddress.X == 0)
                    _gridSheets.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            panel.Controls.Add(_gridSheets);
            panel.Controls.Add(toolbar);
            return panel;
        }

        private Control BuildExportSetupPanel()
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(18, 14, 18, 14) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

            flow.Controls.Add(CreateSectionLabel("Đầu ra"));
            _txtOutputDirectory = new TextBox { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KhimTools_Export"), Width = 326 };
            var folderRow = new FlowLayoutPanel { Height = 32, Width = 330, WrapContents = false, Margin = new Padding(0, 4, 0, 14) };
            _btnBrowseFolder = CreateSecondaryButton("Chọn thư mục", 108);
            _btnOpenFolderSelection = CreateSecondaryButton("Mở thư mục", 100);
            _btnBrowseFolder.Click += BtnBrowseFolder_Click;
            _btnOpenFolderSelection.Click += (s, e) => OpenOutputDirectory();
            folderRow.Controls.AddRange(new System.Windows.Forms.Control[] { _btnBrowseFolder, _btnOpenFolderSelection });
            flow.Controls.Add(_txtOutputDirectory);
            flow.Controls.Add(folderRow);

            flow.Controls.Add(CreateSectionLabel("Đợt phát hành"));
            _txtIssueSetName = new TextBox { Text = "Official Release", Width = 326, Margin = new Padding(0, 0, 0, 4) };
            _dtpIssueDate = new DateTimePicker { Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Margin = new Padding(0, 0, 0, 4) };
            var saveDatedSet = CreateSecondaryButton("Lưu set theo ngày", 140);
            saveDatedSet.Margin = new Padding(0, 0, 0, 14);
            saveDatedSet.Click += (s, e) => SaveDatedSelectionSet();
            flow.Controls.Add(_txtIssueSetName);
            flow.Controls.Add(_dtpIssueDate);
            flow.Controls.Add(saveDatedSet);

            flow.Controls.Add(CreateSectionLabel("Tên file"));
            _chkUseNamingConvention = new CheckBox { Text = "Dùng mẫu tên file", Checked = true, AutoSize = true, Margin = new Padding(0, 5, 0, 4) };
            _txtNamingPattern = new TextBox { Text = "{SheetNumber} - {SheetName}", Width = 326, Margin = new Padding(0, 0, 0, 4) };
            _txtNamingPattern.TextChanged += (s, e) => { UpdateNamingPreview(); UpdateCombineFileName(); };
            _chkUseNamingConvention.CheckedChanged += (s, e) => UpdateNamingPreview();
            _lblNamingPreview = new Label { Text = "Xem trước: -", Width = 326, Height = 38, ForeColor = KhimUiStyle.TextSecondary, Margin = new Padding(0, 0, 0, 12) };
            flow.Controls.Add(_chkUseNamingConvention);
            flow.Controls.Add(_txtNamingPattern);
            flow.Controls.Add(_lblNamingPreview);

            flow.Controls.Add(CreateSectionLabel("Chất lượng PDF"));
            _cmbPdfExportQuality = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 0, 4) };
            _cmbPdfExportQuality.Items.AddRange(new object[] { "Standard - 300 DPI", "High - 600 DPI", "Draft - 144 DPI" });
            _cmbPdfExportQuality.SelectedIndex = 0;
            flow.Controls.Add(_cmbPdfExportQuality);

            flow.Controls.Add(CreateSectionLabel("Tùy chọn bộ hồ sơ"));
            _rbSeparateFiles = new RadioButton { Text = "Từng file riêng", Checked = true, AutoSize = true, Margin = new Padding(0, 5, 0, 3) };
            _rbCombineFiles = new RadioButton { Text = "Gộp thành một file PDF", AutoSize = true, Margin = new Padding(0, 3, 0, 3) };
            _txtFileCombineName = new TextBox { Text = "Combined_Project_Sheets.pdf", Width = 326, Enabled = false, Margin = new Padding(0, 2, 0, 5) };
            _rbCombineFiles.CheckedChanged += (s, e) => _txtFileCombineName.Enabled = _rbCombineFiles.Checked;
            _chkCreateTransmittal = new CheckBox { Text = "Tạo bảng kê Excel", AutoSize = true, Margin = new Padding(0, 4, 0, 12) };
            _chkPreservePrevious = new CheckBox { Text = "Giữ lại bản xuất trước", Checked = true, AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
            flow.Controls.Add(_rbSeparateFiles);
            flow.Controls.Add(_rbCombineFiles);
            flow.Controls.Add(_txtFileCombineName);
            flow.Controls.Add(_chkPreservePrevious);
            flow.Controls.Add(_chkCreateTransmittal);

            var advanced = CreateSecondaryButton("Thiết lập PDF / DWG nâng cao", 230);
            advanced.Height = 34;
            advanced.Click += (s, e) => ShowAdvancedSettings();
            flow.Controls.Add(advanced);

            _txtNaming1 = new TextBox { Visible = false, Text = DateTime.Now.ToString("yyMMdd") };
            _txtNaming2 = new TextBox { Visible = false, Text = "PROJECT" };
            _txtNaming3 = new TextBox { Visible = false, Text = "ISSUE" };
            host.Controls.Add(flow);
            bool resizingSetup = false;
            host.Layout += (sender, args) =>
            {
                if (resizingSetup) return;
                resizingSetup = true;
                try
                {
                    int available = Math.Max(1, host.ClientSize.Width - host.Padding.Horizontal -
                        SystemInformation.VerticalScrollBarWidth);
                    foreach (Control child in flow.Controls)
                    {
                        if (child is TextBox || child is Label || child == folderRow)
                            child.Width = Math.Max(1, available - child.Margin.Horizontal);
                    }
                }
                finally { resizingSetup = false; }
            };
            return host;
        }

        private void UpdateFormatAvailability()
        {
            if (_rbCombineFiles == null || _txtFileCombineName == null) return;
            bool supportsCombinedPdf = !_rbFormatDwg.Checked;
            _rbCombineFiles.Enabled = supportsCombinedPdf;
            if (!supportsCombinedPdf) _rbSeparateFiles.Checked = true;
            _txtFileCombineName.Enabled = supportsCombinedPdf && _rbCombineFiles.Checked;
        }

        private Control BuildUnifiedFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 8, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _lblTotalSummary = new Label
            {
                Text = "0 / 0 sheet được chọn", Dock = DockStyle.Fill, AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _lblProgressStatus = new Label
            {
                Text = "Sẵn sàng", Dock = DockStyle.Fill, AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = KhimUiStyle.TextSecondary
            };
            _progressBar = new ProgressBar
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 14,
                Margin = new Padding(12, 0, 12, 0), Visible = false
            };
            _btnPrint = new Button
            {
                Text = "Xuất sheet đã chọn", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.CreateButtonBg, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand
            };
            _btnPrint.FlatAppearance.BorderSize = 0;
            _btnPrint.Click += BtnPrint_Click;
            footer.Controls.Add(_lblTotalSummary, 0, 0);
            footer.Controls.Add(_lblProgressStatus, 0, 1);
            footer.Controls.Add(_progressBar, 1, 0);
            footer.SetRowSpan(_progressBar, 2);
            footer.Controls.Add(_btnPrint, 2, 0);
            footer.SetRowSpan(_btnPrint, 2);
            return footer;
        }

        private RadioButton CreateFormatOption(string text, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                Appearance = Appearance.Button,
                AutoSize = true,
                Checked = isChecked,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(10, 3, 10, 3),
                Margin = new Padding(0, 0, 4, 0)
            };
        }

        private Button CreateSecondaryButton(string text, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            return button;
        }

        private Label CreateSectionLabel(string text)
        {
            return new Label { Text = text, Width = 326, Height = 24, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = KhimUiStyle.TextPrimary, Margin = new Padding(0, 0, 0, 2) };
        }

        private void EnsureAdvancedSettingsForm()
        {
            if (_advancedSettingsForm != null) return;
            _advancedSettingsForm = new Form
            {
                Text = "Thiết lập xuất nâng cao",
                Width = 980,
                Height = 620,
                MinimumSize = new Size(820, 520),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                BackColor = Color.White
            };
            _advancedSettingsForm.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; _advancedSettingsForm.Hide(); }
            };
            _tabSettings = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F) };
            var pdf = new TabPage { Text = "PDF", Padding = new Padding(12), BackColor = Color.White };
            var dwg = new TabPage { Text = "DWG", Padding = new Padding(12), BackColor = Color.White };
            BuildPdfSettingsTab(pdf);
            BuildDwgSettingsTab(dwg);
            _tabSettings.TabPages.Add(pdf);
            _tabSettings.TabPages.Add(dwg);
            _advancedSettingsForm.Controls.Add(_tabSettings);
        }

        private void ShowAdvancedSettings()
        {
            EnsureAdvancedSettingsForm();
            _advancedSettingsForm.ShowDialog(this);
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
            _btnNavFilter = CreateSidebarButton("Filter", 2);

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

            // Row 1: Separate / Combine Radios + Save/Load Selection JSON
            var pnlRow1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(2, 2, 2, 2)
            };

            _rbSeparateFiles = new RadioButton
            {
                Text = "Từng file riêng (Separate)",
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(3, 5, 8, 3)
            };
            _rbCombineFiles = new RadioButton
            {
                Text = "Gộp 1 file PDF (Combine)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(3, 5, 16, 3)
            };

            _btnSaveSelection = new Button
            {
                Text = "Lưu lựa chọn...",
                AutoSize = true,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(3, 2, 4, 3),
                Cursor = Cursors.Hand
            };
            _btnSaveSelection.Click += (s, e) => SaveSelectionDialog();

            _btnLoadSelection = new Button
            {
                Text = "Mở lựa chọn...",
                AutoSize = true,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(3, 2, 8, 3),
                Cursor = Cursors.Hand
            };
            _btnLoadSelection.Click += (s, e) => LoadSelectionDialog();

            _lblCurrentSelectionFile = new Label
            {
                Text = "File: (Mặc định toàn bộ)",
                AutoSize = true,
                ForeColor = KhimUiStyle.TextSecondary,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                Margin = new Padding(3, 6, 3, 3)
            };

            pnlRow1.Controls.AddRange(new System.Windows.Forms.Control[] {
                _rbSeparateFiles, _rbCombineFiles, _btnSaveSelection, _btnLoadSelection, _lblCurrentSelectionFile
            });
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

            // Row 3: Combine File Name + Format Picker (Radio Buttons & Checkboxes)
            var pnlRow3 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(2, 2, 2, 2)
            };

            var lblCombine = new Label { Text = "Tên file PDF gộp:", AutoSize = true, Font = new Font("Segoe UI", 9F), Margin = new Padding(3, 7, 6, 3) };
            _txtFileCombineName = new TextBox { Text = "Combined_Project_Sheets.pdf", Width = 320, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 4, 10, 3) };

            var lblFormatGroup = new Label { Text = "Định dạng:", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(6, 7, 4, 3) };

            _rbFormatPdf = new RadioButton { Text = "Xuất PDF", AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkBlue, Margin = new Padding(3, 6, 4, 3) };
            _rbFormatDwg = new RadioButton { Text = "Xuất DWG", AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkGreen, Margin = new Padding(3, 6, 4, 3) };
            _rbFormatBoth = new RadioButton { Text = "Cả hai (PDF + DWG)", AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.Purple, Margin = new Padding(3, 6, 8, 3) };

            _chkExportPdf = new CheckBox { Text = "PDF", AutoSize = true, Checked = true, Visible = false };
            _chkExportDwg = new CheckBox { Text = "DWG", AutoSize = true, Checked = false, Visible = false };

            _rbFormatPdf.CheckedChanged += (s, e) => { if (_rbFormatPdf.Checked) { _chkExportPdf.Checked = true; _chkExportDwg.Checked = false; } };
            _rbFormatDwg.CheckedChanged += (s, e) => { if (_rbFormatDwg.Checked) { _chkExportPdf.Checked = false; _chkExportDwg.Checked = true; } };
            _rbFormatBoth.CheckedChanged += (s, e) => { if (_rbFormatBoth.Checked) { _chkExportPdf.Checked = true; _chkExportDwg.Checked = true; } };

            _chkExportPdf.CheckedChanged += (s, e) => RefreshGridRows();
            _chkExportDwg.CheckedChanged += (s, e) => RefreshGridRows();

            _chkCreateTransmittal = new CheckBox { Text = "Tạo Bảng Kê Excel (Transmittal)", AutoSize = true, Checked = false, Font = new Font("Segoe UI", 9F), Margin = new Padding(6, 6, 6, 3) };

            pnlRow3.Controls.AddRange(new System.Windows.Forms.Control[] {
                lblCombine, _txtFileCombineName, lblFormatGroup, _rbFormatPdf, _rbFormatDwg, _rbFormatBoth,
                _chkExportPdf, _chkExportDwg, _chkCreateTransmittal
            });
            pnlTopConfig.Controls.Add(pnlRow3);

            // Row 4: Search & Quick Selection Toolbar
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = KhimUiStyle.FormBg,
                Padding = new Padding(4, 6, 4, 4)
            };

            var lblFilter = new Label { Text = "Lọc Series:", Left = 6, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = KhimUiStyle.TextSecondary };
            _cmbDisciplineFilter = new ComboBox { Left = 75, Top = 7, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbDisciplineFilter.SelectedIndexChanged += (s, e) => { if (!_isLoading) ApplySearchAndFilter(); };

            _txtSearchSheet = new TextBox { Left = 265, Top = 7, Width = 210, Font = new Font("Segoe UI", 9F) };
            _txtSearchSheet.TextChanged += (s, e) => ApplySearchAndFilter();

            _btnSelectAll = new Button { Text = "Chọn hết", Left = 485, Top = 6, Width = 75, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };
            _btnClearAll = new Button { Text = "Bỏ chọn", Left = 566, Top = 6, Width = 75, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };
            _btnInvert = new Button { Text = "Đảo chọn", Left = 647, Top = 6, Width = 75, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };

            _btnSelectAll.Click += (s, e) => SetAllGridItems(true);
            _btnClearAll.Click += (s, e) => SetAllGridItems(false);
            _btnInvert.Click += (s, e) => InvertGridItems();

            _btnRefreshList = new Button { Text = "Nạp lại", Left = 728, Top = 6, Width = 80, Height = 27, FlatStyle = FlatStyle.Flat, BackColor = KhimUiStyle.SecondaryButtonBg };
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
            _chkAutoDisableTempViewModes = new CheckBox
            {
                Text = "⚡ Tự động tắt Temporary View Properties khi in (tránh hộp thoại dừng batch)",
                Left = 15,
                Top = 200,
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };

            grpOptions.Controls.AddRange(new System.Windows.Forms.Control[] {
                _chkViewLinksInBlue, _chkHideRefPlanes, _chkHideUnreferencedTags, _chkHideScopeBoxes,
                _chkHideCropBoundaries, _chkReplaceHalftone, _chkMaskCoincidentLines, _chkAutoDisableTempViewModes
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
                Text = "Mở thư mục",
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

                        _progressBar = new ProgressBar
            {
                Left = 200,
                Top = 48,
                Height = 18,
                Width = 500,
                Visible = false,
                Style = ProgressBarStyle.Continuous
            };

            _lblProgressStatus = new Label
            {
                Left = 200,
                Top = 48,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray,
                Visible = false
            };

            grpPrint.Controls.AddRange(new System.Windows.Forms.Control[] {
                _btnOpenFolderSelection, _txtOutputDirectory, _btnBrowseFolder, _lblTotalSummary, _progressBar, _lblProgressStatus, _btnPrint
            });

            grpPrint.Resize += (s, e) =>
            {
                _btnPrint.Left = grpPrint.Width - _btnPrint.Width - 18;
                _txtOutputDirectory.Width = Math.Max(250, _btnPrint.Left - _txtOutputDirectory.Left - 60);
                _btnBrowseFolder.Left = _txtOutputDirectory.Right + 6;
                _progressBar.Width = Math.Max(200, _btnPrint.Left - _progressBar.Left - 20);
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

                // Populate Series & Discipline Filter Dropdown
                _cmbDisciplineFilter.Items.Clear();
                _cmbDisciplineFilter.Items.Add("(Tất cả bản vẽ)");
                _cmbDisciplineFilter.Items.Add("⭐ Chỉ hiện bản vẽ ĐANG CHỌN");
                _cmbDisciplineFilter.Items.Add("Structure (Kết cấu: S, KC, ST)");
                _cmbDisciplineFilter.Items.Add("Architecture (Kiến trúc: A, KT, AR)");
                _cmbDisciplineFilter.Items.Add("MEP (Cơ điện: M, E, P, MEP)");

                var seriesGroups = _allSheetItems
                    .Select(s => GetSheetSeries(s.SheetNumber))
                    .Where(s => !string.IsNullOrWhiteSpace(s) && s != "Khác (Other)")
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                if (seriesGroups.Any())
                {
                    foreach (var sGroup in seriesGroups)
                    {
                        _cmbDisciplineFilter.Items.Add($"Series: {sGroup}");
                    }
                }
                _cmbDisciplineFilter.SelectedIndex = 0;

                _cmbPaperFilter.Items.Clear();
                _cmbPaperFilter.Items.Add("Mọi khổ giấy");
                foreach (string paperSize in _allSheetItems.Select(s => s.PaperSize).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
                    _cmbPaperFilter.Items.Add(paperSize);
                _cmbPaperFilter.SelectedIndex = 0;

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

                // Tự động nạp file lựa chọn sheet gần nhất nếu có
                string lastPath = GetLastSelectionPath();
                if (!string.IsNullOrWhiteSpace(lastPath) && File.Exists(lastPath))
                {
                    LoadSelectionFromFile(lastPath, isAutoLoad: true);
                }
                else
                {
                    UpdateSelectionFileLabel("");
                    ApplySearchAndFilter();
                }

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
            string filterChoice = _cmbDisciplineFilter.SelectedItem?.ToString() ?? "(All)";

            var list = _allSheetItems.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                list = list.Where(s => ContainsIgnoreCase(s.SheetNumber, query) ||
                                       ContainsIgnoreCase(s.SheetName, query) ||
                                       ContainsIgnoreCase(s.CurrentRevisionNumber, query) ||
                                       ContainsIgnoreCase(s.CurrentRevisionDate, query) ||
                                       ContainsIgnoreCase(s.PaperSize, query) ||
                                       ContainsIgnoreCase(s.Orientation, query) ||
                                       ContainsIgnoreCase(s.StatusBadgeText, query) ||
                                       ContainsIgnoreCase(s.ComputedFileName, query) ||
                                       ContainsIgnoreCase(GetSheetSeries(s.SheetNumber), query));
            }

            if (filterChoice.StartsWith("⭐"))
            {
                list = list.Where(s => s.IsSelected);
            }
            else if (filterChoice.StartsWith("Structure") || filterChoice.Contains("Kết cấu"))
            {
                list = list.Where(s => s.SheetNumber != null && (s.SheetNumber.StartsWith("KC", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("ST", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("S-", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("S0") || s.SheetNumber.StartsWith("S1") || s.SheetNumber.StartsWith("S2") || s.SheetNumber.StartsWith("S3") || s.SheetNumber.StartsWith("S4") || s.SheetNumber.StartsWith("S5") || s.SheetNumber.StartsWith("S6") || s.SheetNumber.StartsWith("S7") || s.SheetNumber.StartsWith("S8") || s.SheetNumber.StartsWith("S9")));
            }
            else if (filterChoice.StartsWith("Architecture") || filterChoice.Contains("Kiến trúc"))
            {
                list = list.Where(s => s.SheetNumber != null && (s.SheetNumber.StartsWith("KT", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("AR", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("A-", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("A0") || s.SheetNumber.StartsWith("A1") || s.SheetNumber.StartsWith("A2")));
            }
            else if (filterChoice.StartsWith("MEP") || filterChoice.Contains("Cơ điện"))
            {
                list = list.Where(s => s.SheetNumber != null && (s.SheetNumber.StartsWith("MEP", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("M-", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("E-", StringComparison.OrdinalIgnoreCase) || s.SheetNumber.StartsWith("P-", StringComparison.OrdinalIgnoreCase)));
            }
            else if (filterChoice.StartsWith("Series:", StringComparison.OrdinalIgnoreCase))
            {
                string seriesName = filterChoice.Substring("Series:".Length).Trim();
                list = list.Where(s => GetSheetSeries(s.SheetNumber).Equals(seriesName, StringComparison.OrdinalIgnoreCase));
            }

            string paperFilter = _cmbPaperFilter?.SelectedItem?.ToString() ?? "Mọi khổ giấy";
            if (!paperFilter.StartsWith("Mọi", StringComparison.OrdinalIgnoreCase))
                list = list.Where(s => string.Equals(s.PaperSize, paperFilter, StringComparison.OrdinalIgnoreCase));

            string issueFilter = _cmbIssueStatusFilter?.SelectedItem?.ToString() ?? "Mọi trạng thái";
            if (issueFilter == "Mới") list = list.Where(s => s.IssueStatus == SheetIssueStatus.New);
            else if (issueFilter == "Đã sửa") list = list.Where(s => s.IssueStatus == SheetIssueStatus.Modified);
            else if (issueFilter == "Không đổi") list = list.Where(s => s.IssueStatus == SheetIssueStatus.Unchanged);

            if (_chkFilterModifiedOnly.Checked)
            {
                list = list.Where(s => s.IssueStatus == SheetIssueStatus.New || s.IssueStatus == SheetIssueStatus.Modified);
            }

            _filteredSheetItems = list.ToList();
            RefreshGridRows();
            UpdateSummaryLabel();
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetSheetSeries(string sheetNumber)
        {
            if (string.IsNullOrWhiteSpace(sheetNumber)) return "Khác (Other)";
            var match = System.Text.RegularExpressions.Regex.Match(sheetNumber.Trim(), @"^([A-Za-z]+[-_]?)(\d)");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                char firstDigit = match.Groups[2].Value[0];
                return $"{prefix}{firstDigit}000 Series";
            }
            return "Khác (Other)";
        }

        #region Sheet Selection JSON File Management
        private void SaveDatedSelectionSet()
        {
            var selected = _allSheetItems.Where(s => s.IsSelected).Select(s => s.SheetNumber).ToList();
            if (!selected.Any())
            {
                KhimDialogHelper.ShowWarning("Lưu set", "Hãy chọn ít nhất một sheet trước khi lưu set phát hành.");
                return;
            }

            try
            {
                string project = SanitizeFileName(_doc.ProjectInformation?.Name ?? _doc.Title ?? "Project");
                string setName = SanitizeFileName(_txtIssueSetName?.Text ?? "Issue");
                DateTime issueDate = _dtpIssueDate?.Value.Date ?? DateTime.Today;
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "KhimTools", "SheetSets", project);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"{issueDate:yyyy-MM-dd}_{setName}.json");
                var data = new SheetSelectionData
                {
                    ProjectTitle = _doc.Title ?? "",
                    SetName = _txtIssueSetName?.Text?.Trim() ?? "",
                    IssueDate = issueDate,
                    SavedAt = DateTime.Now,
                    SelectedSheetNumbers = selected
                };
                File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented), System.Text.Encoding.UTF8);
                SaveLastSelectionPath(path);
                UpdateSelectionFileLabel(path);
                KhimDialogHelper.ShowSuccess("Đã lưu set", $"Đã lưu {selected.Count} sheet vào set '{data.SetName}' ngày {issueDate:dd/MM/yyyy}.");
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Không thể lưu set", ex.Message);
            }
        }

        private void SaveSelectionDialog()
        {
            var selectedSheetNums = _allSheetItems.Where(s => s.IsSelected).Select(s => s.SheetNumber).ToList();
            if (!selectedSheetNums.Any())
            {
                KhimDialogHelper.ShowWarning("Lưu Lựa Chọn", "Hiện tại chưa có bản vẽ nào được chọn để lưu.");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Lưu Danh Sách Bản Vẽ Đang Chọn",
                Filter = "Sheet Selection File (*.json)|*.json",
                DefaultExt = "json",
                FileName = $"{SanitizeFileName(_doc.Title ?? "Project")}_Sheets_{DateTime.Now:yyyyMMdd}.json"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var data = new SheetSelectionData
                    {
                        ProjectTitle = _doc.Title ?? "",
                        SavedAt = DateTime.Now,
                        SelectedSheetNumbers = selectedSheetNums
                    };
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(sfd.FileName, json, System.Text.Encoding.UTF8);

                    SaveLastSelectionPath(sfd.FileName);
                    UpdateSelectionFileLabel(sfd.FileName);

                    KhimDialogHelper.ShowSuccess("Đã Lưu Lựa Chọn", $"Đã lưu danh sách {selectedSheetNums.Count} bản vẽ vào file:\n{sfd.FileName}");
                }
                catch (Exception ex)
                {
                    KhimDialogHelper.ShowError("Lỗi Lưu File", ex.Message);
                }
            }
        }

        private void LoadSelectionDialog()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Mở File Danh Sách Bản Vẽ Đã Lưu",
                Filter = "Sheet Selection File (*.json)|*.json",
                DefaultExt = "json"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                LoadSelectionFromFile(ofd.FileName, isAutoLoad: false);
            }
        }

        private void LoadSelectionFromFile(string filePath, bool isAutoLoad)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<SheetSelectionData>(json);
                if (data == null || data.SelectedSheetNumbers == null) return;

                if (_txtIssueSetName != null && !string.IsNullOrWhiteSpace(data.SetName))
                    _txtIssueSetName.Text = data.SetName;
                if (_dtpIssueDate != null && data.IssueDate > DateTime.MinValue)
                    _dtpIssueDate.Value = data.IssueDate;

                var targetSet = new HashSet<string>(data.SelectedSheetNumbers, StringComparer.OrdinalIgnoreCase);
                int matchedCount = 0;

                foreach (var item in _allSheetItems)
                {
                    if (targetSet.Contains(item.SheetNumber))
                    {
                        item.IsSelected = true;
                        matchedCount++;
                    }
                    else
                    {
                        item.IsSelected = false;
                    }
                }

                var projectSheetNums = new HashSet<string>(_allSheetItems.Select(s => s.SheetNumber), StringComparer.OrdinalIgnoreCase);
                var missingSheetNums = data.SelectedSheetNumbers.Where(num => !projectSheetNums.Contains(num)).ToList();
                int missingCount = missingSheetNums.Count;

                SaveLastSelectionPath(filePath);
                UpdateSelectionFileLabel(filePath);
                ApplySearchAndFilter();

                if (!isAutoLoad)
                {
                    string msg = $"✔ Đã nạp và chọn {matchedCount} bản vẽ từ file:\n{Path.GetFileName(filePath)}";
                    if (missingCount > 0)
                    {
                        msg += $"\n\n⚠️ Bỏ qua {missingCount} bản vẽ không còn tồn tại trong model:\n" + string.Join(", ", missingSheetNums.Take(5));
                        if (missingCount > 5) msg += $" và {missingCount - 5} bản vẽ khác...";
                    }
                    KhimDialogHelper.ShowInfo("Nạp Lựa Chọn Thành Công", msg);
                }
            }
            catch (Exception ex)
            {
                if (!isAutoLoad) KhimDialogHelper.ShowError("Lỗi Đọc File Lựa Chọn", ex.Message);
            }
        }

        private void UpdateSelectionFileLabel(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _lblCurrentSelectionFile.Text = "File: (Mặc định toàn bộ)";
                return;
            }
            string name = Path.GetFileName(filePath);
            _lblCurrentSelectionFile.Text = $"File: {name}";
        }

        private static string GetLocalConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "KhimTools");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "sheet_export_selection_settings.json");
        }

        private void SaveLastSelectionPath(string path)
        {
            try
            {
                string configFile = GetLocalConfigPath();
                var settings = new SheetExportLocalSettings { LastSelectionFilePath = path };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
                File.WriteAllText(configFile, json, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private string GetLastSelectionPath()
        {
            try
            {
                string configFile = GetLocalConfigPath();
                if (File.Exists(configFile))
                {
                    string json = File.ReadAllText(configFile, System.Text.Encoding.UTF8);
                    var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<SheetExportLocalSettings>(json);
                    return settings?.LastSelectionFilePath ?? "";
                }
            }
            catch { }
            return "";
        }
        #endregion

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
            string fmt = _rbFormatBoth.Checked ? "PDF / DWG" : (_rbFormatPdf.Checked ? "PDF" : "DWG");

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
            _lblTotalSummary.Text = $"{selectedCount} / {totalCount} sheet được chọn";
            if (_btnPrint != null)
                _btnPrint.Text = selectedCount > 0 ? $"Xuất {selectedCount} sheet" : "Xuất sheet đã chọn";
            UpdateNamingPreview();
        }

        private void UpdateCombineFileName()
        {
            if (_txtFileCombineName == null || _txtFileCombineName.Focused) return;
            string project = SanitizeFileName(_doc.ProjectInformation?.Name ?? _doc.Title ?? "Project");
            _txtFileCombineName.Text = $"{project}_Sheets_{DateTime.Now:yyyyMMdd}.pdf";
        }

        private void UpdateNamingPreview()
        {
            if (_lblNamingPreview == null || _txtNamingPattern == null) return;
            var sample = _allSheetItems.FirstOrDefault(s => s.IsSelected) ?? _allSheetItems.FirstOrDefault();
            _lblNamingPreview.Text = sample == null
                ? "Xem trước: chưa có sheet"
                : $"Xem trước: {ComputeSheetFileName(sample)}";
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
            _options.ExportPdf = _rbFormatPdf.Checked || _rbFormatBoth.Checked;
            _options.ExportDwg = _rbFormatDwg.Checked || _rbFormatBoth.Checked;
            _options.DwgExportSetupName = _cmbDwgSetup?.Text?.Trim() ?? "";
            _options.OutputDirectory = outDir;
            _options.SplitFoldersByFormat = false;
            _options.CombinePdf = _rbCombineFiles.Checked;
            _options.CombinedPdfFileName = _txtFileCombineName.Text.Trim();
            _options.IssueSetName = _txtIssueSetName?.Text?.Trim() ?? "Official Release";
            _options.IssueDate = _dtpIssueDate?.Value.Date ?? DateTime.Today;
            _options.PreservePreviousExports = _chkPreservePrevious?.Checked ?? true;
            _options.PdfExportDpi = GetSelectedPdfDpi();

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
            _options.AutoDisableTemporaryViewProperties = _chkAutoDisableTempViewModes?.Checked ?? true;

            // Compute exact filename for each selected sheet
            foreach (var item in selectedItems)
            {
                item.ComputedFileName = ComputeSheetFileName(item);
            }

            var preflightWarnings = PreflightCheckService.RunPreflightChecks(selectedItems, outDir, _options.CombinePdf);
            var criticalWarnings = preflightWarnings.Where(w => w.IsCritical).ToList();
            if (criticalWarnings.Any())
            {
                KhimDialogHelper.ShowError("Preflight không đạt", string.Join("\n\n", criticalWarnings.Select(w => w.Title + "\n" + w.Details)));
                return;
            }
            if (preflightWarnings.Any())
            {
                string warningText = string.Join("\n\n", preflightWarnings.Select(w => w.Title + "\n" + w.Details));
                var decision = MessageBox.Show(this, warningText + "\n\nBạn vẫn muốn tiếp tục xuất?", "Preflight có cảnh báo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (decision != DialogResult.Yes) return;
            }

            string archivedFolder;
            try
            {
                archivedFolder = ExportHistoryService.ArchiveExistingOutputs(selectedItems, _options);
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Không thể lưu bản xuất trước", ex.Message);
                return;
            }

            // Tự động kiểm tra và tắt Temporary View Properties trước khi in
            var disabledTempViews = new List<string>();
            if (_options.AutoDisableTemporaryViewProperties)
            {
                try
                {
                    using (var tx = new Transaction(_doc, "K-TOOLS: Tắt Temporary View Properties"))
                    {
                        tx.Start();
                        foreach (var item in selectedItems)
                        {
                            var sheet = item.Sheet;
                            if (sheet == null) continue;

                            if (sheet.IsTemporaryViewPropertiesModeEnabled())
                            {
                                sheet.DisableTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties);
                                disabledTempViews.Add($"Sheet [{sheet.SheetNumber}] {sheet.Name}");
                            }

                            var viewportIds = sheet.GetAllViewports();
                            foreach (var vpId in viewportIds)
                            {
                                var vp = _doc.GetElement(vpId) as Viewport;
                                if (vp == null) continue;
                                var childView = _doc.GetElement(vp.ViewId) as Autodesk.Revit.DB.View;
                                if (childView != null && childView.IsTemporaryViewPropertiesModeEnabled())
                                {
                                    childView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties);
                                    disabledTempViews.Add($"View '{childView.Name}' (trên Sheet {sheet.SheetNumber})");
                                }
                            }
                        }
                        tx.Commit();
                    }
                }
                catch
                {
                    // Tránh chặn tiến trình xuất nếu document đang ở trạng thái read-only
                }
            }

            _btnPrint.Enabled = false;
            _btnPrint.Text = "Đang xuất...";
            _lblProgressStatus.Text = "Đang chuẩn bị dữ liệu xuất";
            _progressBar.Visible = true;
            _progressBar.Style = ProgressBarStyle.Marquee;
            Cursor = Cursors.WaitCursor;

            try
            {
                var queue = new ExportRetryQueue(_options.MaxRetryCount);
                var qaResults = queue.ProcessBatch(_doc, selectedItems, _options, msg =>
                {
                    _lblProgressStatus.Text = msg;
                    RefreshGridRows();
                    Application.DoEvents();
                });

                // Generate Transmittal Excel only if requested
                if (_chkCreateTransmittal.Checked)
                {
                    TransmittalGeneratorService.GenerateExcelTransmittal(outDir, "Official Release", _txtNaming2.Text, selectedItems);
                }

                int successCount = selectedItems.Count(item =>
                {
                    var resultsForSheet = qaResults.Where(r => string.Equals(r.SheetNumber, item.SheetNumber, StringComparison.OrdinalIgnoreCase)).ToList();
                    return resultsForSheet.Any() && resultsForSheet.All(r => r.Success);
                });
                var lockedItems = selectedItems.Where(s => s.IsLocked).ToList();
                var otherFails = selectedItems.Where(s => s.IsFailed && !s.IsLocked).ToList();

                Cursor = Cursors.Default;
                var sbSummary = new System.Text.StringBuilder();
                sbSummary.AppendLine($"🎉 Xuất hoàn tất: {successCount} / {selectedItems.Count} bản vẽ sang thư mục:");
                sbSummary.AppendLine(outDir);
                if (!string.IsNullOrWhiteSpace(archivedFolder))
                    sbSummary.AppendLine($"Bản trước đã được lưu tại: {archivedFolder}");

                if (disabledTempViews.Any())
                {
                    sbSummary.AppendLine();
                    sbSummary.AppendLine($"ℹ️ Đã tự động tắt Temporary View Properties cho {disabledTempViews.Count} view để tránh hộp thoại gián đoạn:");
                    foreach (var dv in disabledTempViews.Take(5))
                    {
                        sbSummary.AppendLine($"  • {dv}");
                    }
                    if (disabledTempViews.Count > 5)
                    {
                        sbSummary.AppendLine($"  ... và {disabledTempViews.Count - 5} view khác.");
                    }
                }

                if (lockedItems.Any())
                {
                    sbSummary.AppendLine();
                    sbSummary.AppendLine($"⚠️ CẢNH BÁO: Có {lockedItems.Count} file PDF đang mở bởi ứng dụng khác (Đã bỏ qua để không dừng batch):");
                    foreach (var lk in lockedItems.Take(10))
                    {
                        sbSummary.AppendLine($"  • [{lk.SheetNumber}] {lk.SheetName} ➔ {Path.GetFileName(lk.ComputedFileName)}.pdf");
                    }
                    if (lockedItems.Count > 10)
                    {
                        sbSummary.AppendLine($"  ... và {lockedItems.Count - 10} file khác.");
                    }
                    sbSummary.AppendLine("👉 Vui lòng đóng các file PDF trên và xuất lại riêng các sheet đó.");
                }

                if (otherFails.Any())
                {
                    sbSummary.AppendLine();
                    sbSummary.AppendLine($"❌ Có {otherFails.Count} bản vẽ gặp lỗi khác:");
                    foreach (var f in otherFails.Take(5))
                    {
                        sbSummary.AppendLine($"  • [{f.SheetNumber}]: {f.ErrorMessage}");
                    }
                }

                if (lockedItems.Any() || otherFails.Any())
                {
                    KhimDialogHelper.ShowWarning("Tổng Kết Xuất Bản Vẽ (Có Cảnh Báo)", sbSummary.ToString());
                }
                else
                {
                    KhimDialogHelper.ShowSuccess("Hoàn Tất Xuất Bản Vẽ", sbSummary.ToString());
                }
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
                _progressBar.Visible = false;
                _progressBar.Style = ProgressBarStyle.Blocks;
                _lblProgressStatus.Text = "Sẵn sàng";
                RefreshGridRows();
                UpdateSummaryLabel();
            }
        }

        private string ComputeSheetFileName(SheetExportItem item)
        {
            string cleanNum = SanitizeFileName(item.SheetNumber);
            string cleanName = SanitizeFileName(item.SheetName);

            if (_chkUseNamingConvention.Checked && _txtNamingPattern != null)
            {
                string project = _doc.ProjectInformation?.Name ?? _doc.Title ?? "Project";
                string revision = item.CurrentRevisionNumber ?? "";
                string result = _txtNamingPattern.Text
                    .Replace("{SheetNumber}", cleanNum)
                    .Replace("{SheetName}", cleanName)
                    .Replace("{Project}", SanitizeFileName(project))
                    .Replace("{Revision}", SanitizeFileName(revision))
                    .Replace("{Date}", DateTime.Now.ToString("yyyyMMdd"));
                return SanitizeFileName(result);
            }
            else
            {
                return $"{cleanNum} - {cleanName}";
            }
        }

        private int GetSelectedPdfDpi()
        {
            string value = _cmbPdfExportQuality?.SelectedItem?.ToString() ?? "";
            if (value.Contains("600")) return 600;
            if (value.Contains("144")) return 144;
            return 300;
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

    public class SheetSelectionData
    {
        public string ProjectTitle { get; set; } = "";
        public string SetName { get; set; } = "";
        public DateTime IssueDate { get; set; } = DateTime.Today;
        public DateTime SavedAt { get; set; } = DateTime.Now;
        public List<string> SelectedSheetNumbers { get; set; } = new List<string>();
    }

    public class SheetExportLocalSettings
    {
        public string LastSelectionFilePath { get; set; } = "";
    }
}

using System;
using System.Collections.Generic;
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
        private List<SheetExportItem> _allSheetItems;
        private List<NamingTemplate> _namingTemplates;
        private NamingTemplate _selectedTemplate;

        // UI Controls - Left Panel (Sheet List)
        private DataGridView _gridSheets;
        private Label _lblSelectionCount;
        private Button _btnSelectAll;
        private Button _btnSelectNone;
        private Button _btnSelectChangedOnly;

        // UI Controls - Center/Right Settings (Tabs)
        private TabControl _tabSettings;
        private ComboBox _cmbNamingTemplate;
        private TextBox _txtProjectCode;
        private Button _btnEditNamingTemplate;

        // Format & PDF Options
        private CheckBox _chkExportPdf;
        private CheckBox _chkExportDwg;
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

        // Right Preview & Log Panels
        private PictureBox _picThumbnail;
        private Label _lblThumbnailTitle;
        private RichTextBox _rtbLogOutput;

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
            Text = "📄 KHIM TOOLS — Sheet Batch Export & Issue Manager";
            Width = 1150;
            Height = 750;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = true;
            MinimizeBox = true;

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Sheet Batch Export & Issue Manager",
                "Automated PDF/DWG Batch Printing, Transmittal Register & Revision Diffing Engine",
                "v2.5 Pro");
            Controls.Add(header);

            // Main Splitter (Left: Sheet List, Right: Options + Preview + Log)
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Vertical,
                SplitterDistance = 580,
                Padding = new Padding(8)
            };

            // ── LEFT PANEL: Sheet List & Selection Controls ──────────────────
            var leftPanel = new System.Windows.Forms.Panel { Dock = DockStyle.Fill };
            var lblSheetListTitle = new Label
            {
                Text = "📋 Danh Sách Sheet Bản Vẽ (Selection & Issue Status)",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };

            var pnlSelectionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 35,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            _btnSelectAll = new Button { Text = "Select All", Width = 85, Height = 28, FlatStyle = FlatStyle.System };
            _btnSelectNone = new Button { Text = "Clear All", Width = 80, Height = 28, FlatStyle = FlatStyle.System };
            _btnSelectChangedOnly = new Button
            {
                Text = "⚡ Chỉ chọn Sheet Thay Đổi (New/Mod)",
                Width = 230,
                Height = 28,
                BackColor = Color.FromArgb(235, 245, 255),
                FlatStyle = FlatStyle.Flat
            };

            _btnSelectAll.Click += (s, e) => SetSelectionAll(true);
            _btnSelectNone.Click += (s, e) => SetSelectionAll(false);
            _btnSelectChangedOnly.Click += (s, e) => SelectChangedSheetsOnly();

            pnlSelectionBar.Controls.Add(_btnSelectAll);
            pnlSelectionBar.Controls.Add(_btnSelectNone);
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
            leftPanel.Controls.Add(_lblSelectionCount);
            leftPanel.Controls.Add(lblSheetListTitle);
            mainSplit.Panel1.Controls.Add(leftPanel);

            // ── RIGHT PANEL: Tab Control + Preview + Log Output ─────────────
            var rightPanel = new System.Windows.Forms.Panel { Dock = DockStyle.Fill };

            _tabSettings = new TabControl { Dock = DockStyle.Top, Height = 340 };

            // TAB 1: Naming & File Formats
            var tabFormats = new TabPage { Text = "⚙️ Formats & Naming", Padding = new Padding(10) };
            BuildTabFormats(tabFormats);
            _tabSettings.TabPages.Add(tabFormats);

            // TAB 2: Issue & Transmittal
            var tabIssue = new TabPage { Text = "📋 Issue & Transmittal", Padding = new Padding(10) };
            BuildTabIssue(tabIssue);
            _tabSettings.TabPages.Add(tabIssue);

            // TAB 3: Advanced PDF & Watermark
            var tabAdvancedPdf = new TabPage { Text = "🎨 Advanced PDF", Padding = new Padding(10) };
            BuildTabAdvancedPdf(tabAdvancedPdf);
            _tabSettings.TabPages.Add(tabAdvancedPdf);

            // Preview Thumbnail & Log Splitter
            var previewSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Horizontal,
                SplitterDistance = 140
            };

            var pnlPreviewBox = new GroupBox { Text = "🖼️ Sheet Thumbnail Preview", Dock = DockStyle.Fill };
            _lblThumbnailTitle = new Label { Text = "Chưa chọn sheet", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };
            _picThumbnail = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(245, 246, 250) };
            pnlPreviewBox.Controls.Add(_picThumbnail);
            pnlPreviewBox.Controls.Add(_lblThumbnailTitle);
            previewSplit.Panel1.Controls.Add(pnlPreviewBox);

            var pnlLogBox = new GroupBox { Text = "📜 Process Real-time Log", Dock = DockStyle.Fill };
            _rtbLogOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 36),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 8.5F)
            };
            pnlLogBox.Controls.Add(_rtbLogOutput);
            previewSplit.Panel2.Controls.Add(pnlLogBox);

            rightPanel.Controls.Add(previewSplit);
            rightPanel.Controls.Add(_tabSettings);
            mainSplit.Panel2.Controls.Add(rightPanel);

            // ── BOTTOM ACTION BAR ───────────────────────────────────────────
            var bottomBar = new System.Windows.Forms.Panel { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.FromArgb(245, 245, 248) };

            _btnPreflightCheck = new Button { Text = "🔍 Pre-flight QA Check", Left = 15, Top = 12, Width = 160, Height = 32, FlatStyle = FlatStyle.System };
            _btnPreflightCheck.Click += BtnPreflightCheck_Click;

            _btnExportStart = new Button
            {
                Text = "⚡ EXPORT BATCH NOW",
                Left = 720,
                Top = 10,
                Width = 200,
                Height = 36,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            _btnExportStart.FlatAppearance.BorderSize = 0;
            _btnExportStart.Click += BtnExportStart_Click;

            _btnClose = new Button { Text = "Close", Left = 935, Top = 10, Width = 85, Height = 36, FlatStyle = FlatStyle.System };
            _btnClose.Click += (s, e) => Close();

            bottomBar.Controls.Add(_btnPreflightCheck);
            bottomBar.Controls.Add(_btnExportStart);
            bottomBar.Controls.Add(_btnClose);

            Controls.Add(mainSplit);
            Controls.Add(bottomBar);
        }

        private void BuildGridColumns()
        {
            _gridSheets.Columns.Clear();

            var chkCol = new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsSelected",
                HeaderText = "Export",
                Width = 50
            };
            _gridSheets.Columns.Add(chkCol);

            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusBadgeText", HeaderText = "Trạng Thái", Width = 95, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SheetNumber", HeaderText = "Sheet No.", Width = 90, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SheetName", HeaderText = "Tên Sheet", Width = 150, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CurrentRevisionNumber", HeaderText = "Rev", Width = 45, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaperSize", HeaderText = "Khổ Giấy", Width = 65, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ComputedFileName", HeaderText = "File Export Preview", Width = 180, ReadOnly = true });
            _gridSheets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ExportStatusText", HeaderText = "Kết Quả In", Width = 100, ReadOnly = true });
        }

        private void BuildTabFormats(TabPage tab)
        {
            var pnl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(5) };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Naming Template Select
            pnl.Controls.Add(new Label { Text = "Naming Template:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            var pnlTemplateChoice = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _cmbNamingTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
            _cmbNamingTemplate.SelectedIndexChanged += CmbNamingTemplate_SelectedIndexChanged;

            _btnEditNamingTemplate = new Button { Text = "⚙️ Sửa Template", Width = 110, Height = 26, FlatStyle = FlatStyle.System };
            _btnEditNamingTemplate.Click += BtnEditNamingTemplate_Click;

            pnlTemplateChoice.Controls.Add(_cmbNamingTemplate);
            pnlTemplateChoice.Controls.Add(_btnEditNamingTemplate);
            pnl.Controls.Add(pnlTemplateChoice, 1, 0);

            // Project Code
            pnl.Controls.Add(new Label { Text = "Mã Dự Án (Project):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _txtProjectCode = new TextBox { Text = "PROJ2026", Width = 150, Anchor = AnchorStyles.Left };
            _txtProjectCode.TextChanged += (s, e) => RecalculateFileNames();
            pnl.Controls.Add(_txtProjectCode, 1, 1);

            // Export Formats
            pnl.Controls.Add(new Label { Text = "Định Dạng Export:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            var pnlFormats = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _chkExportPdf = new CheckBox { Text = "📄 PDF Document", Checked = true, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _chkExportDwg = new CheckBox { Text = "📐 AutoCAD DWG", Checked = false, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            pnlFormats.Controls.Add(_chkExportPdf);
            pnlFormats.Controls.Add(_chkExportDwg);
            pnl.Controls.Add(pnlFormats, 1, 2);

            // Combine PDF
            pnl.Controls.Add(new Label { Text = "Gộp PDF (Combine):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            var pnlCombine = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _chkCombinePdf = new CheckBox { Text = "Gộp tất cả bản vẽ vào 1 file PDF duy nhất", Checked = false, AutoSize = true };
            _txtCombinedFileName = new TextBox { Text = "Combined_Project_Sheets.pdf", Width = 200, Enabled = false };
            _chkCombinePdf.CheckedChanged += (s, e) => _txtCombinedFileName.Enabled = _chkCombinePdf.Checked;
            pnlCombine.Controls.Add(_chkCombinePdf);
            pnlCombine.Controls.Add(_txtCombinedFileName);
            pnl.Controls.Add(pnlCombine, 1, 3);

            // Output Directory
            pnl.Controls.Add(new Label { Text = "Thư Mục Xuất File:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
            var pnlDir = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KhimTools_Export");
            _txtOutputDir = new TextBox { Text = defaultDir, Width = 260 };
            _btnBrowseOutputDir = new Button { Text = "Browse...", Width = 75, Height = 25, FlatStyle = FlatStyle.System };
            _btnBrowseOutputDir.Click += BtnBrowseOutputDir_Click;
            pnlDir.Controls.Add(_txtOutputDir);
            pnlDir.Controls.Add(_btnBrowseOutputDir);
            pnl.Controls.Add(pnlDir, 1, 4);

            tab.Controls.Add(pnl);
        }

        private void BuildTabIssue(TabPage tab)
        {
            var pnl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(5) };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Issue Set Name
            pnl.Controls.Add(new Label { Text = "Tên Đợt Phát Hành:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _txtIssueSetName = new TextBox { Text = "IFC - Issued For Construction 50%", Width = 280, Anchor = AnchorStyles.Left };
            pnl.Controls.Add(_txtIssueSetName, 1, 0);

            // Transmittal Register
            pnl.Controls.Add(new Label { Text = "Drawing Register:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _chkGenerateTransmittal = new CheckBox { Text = "Tự động tạo file Excel Bảng kê phát hành (Transmittal Register)", Checked = true, AutoSize = true };
            pnl.Controls.Add(_chkGenerateTransmittal, 1, 1);

            // Technical QA Report
            pnl.Controls.Add(new Label { Text = "QA Technical Log:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            _chkGenerateQaReport = new CheckBox { Text = "Tự động tạo file Excel Báo cáo kỹ thuật QA (thời gian, dung lượng, lỗi)", Checked = true, AutoSize = true };
            pnl.Controls.Add(_chkGenerateQaReport, 1, 2);

            // Max Retries
            pnl.Controls.Add(new Label { Text = "Auto Retry (Lần):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            _numMaxRetries = new NumericUpDown { Minimum = 0, Maximum = 5, Value = 2, Width = 65, Anchor = AnchorStyles.Left };
            pnl.Controls.Add(_numMaxRetries, 1, 3);

            tab.Controls.Add(pnl);
        }

        private void BuildTabAdvancedPdf(TabPage tab)
        {
            var pnl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(5) };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // PDF Bookmarks
            pnl.Controls.Add(new Label { Text = "PDF Outlines/Bookmarks:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _chkAddBookmarks = new CheckBox { Text = "Tự động tạo Bookmark cây điều hướng theo Sheet No. & Name", Checked = true, AutoSize = true };
            pnl.Controls.Add(_chkAddBookmarks, 1, 0);

            // Cover Sheet
            pnl.Controls.Add(new Label { Text = "Cover Page (Trang Bìa):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _chkAutoCoverPage = new CheckBox { Text = "Chèn Trang Bìa Mục Lục Bản Vẽ tự động ở trang đầu file PDF gộp", Checked = false, AutoSize = true };
            pnl.Controls.Add(_chkAutoCoverPage, 1, 1);

            // Watermark / Stamp
            pnl.Controls.Add(new Label { Text = "Watermark / Stamp:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            var pnlWatermark = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _chkWatermark = new CheckBox { Text = "Đóng dấu chéo Watermark", Checked = false, AutoSize = true };
            _cmbWatermarkPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 220, Enabled = false };
            _cmbWatermarkPreset.Items.AddRange(new object[] {
                "IFC - ISSUED FOR CONSTRUCTION",
                "S0 - PRELIMINARY DESIGN",
                "TENDER DOCUMENTS",
                "FOR APPROVAL",
                "AS-BUILT DRAWING"
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
            _allSheetItems = SheetCollectorService.GetAllSheets(_doc);
            RevisionSnapshotService.CompareAndUpdateStatus(_doc, _allSheetItems);

            _namingTemplates = ExtensibleStorageService.LoadNamingTemplates(_doc);
            _cmbNamingTemplate.Items.Clear();
            foreach (var t in _namingTemplates)
            {
                _cmbNamingTemplate.Items.Add(t.Name);
            }
            if (_namingTemplates.Any()) _cmbNamingTemplate.SelectedIndex = 0;

            _gridSheets.DataSource = _allSheetItems;
            RecalculateFileNames();
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
            foreach (var item in _allSheetItems) item.IsSelected = select;
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
        }

        private void BtnExportStart_Click(object sender, EventArgs e)
        {
            var selectedItems = _allSheetItems.Where(s => s.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng chọn ít nhất 1 sheet bản vẽ để xuất.");
                return;
            }

            string outDir = _txtOutputDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                KhimDialogHelper.ShowWarning("Thiếu Thông Tin", "Vui lòng chọn thư mục lưu file xuất.");
                return;
            }

            var options = new ExportOptions
            {
                ExportPdf = _chkExportPdf.Checked,
                ExportDwg = _chkExportDwg.Checked,
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
            AppendLog($"🚀 BẮT ĐẦU BATCH EXPORT [{DateTime.Now:HH:mm:ss}]");
            AppendLog($"📁 Target Directory: {outDir}");
            AppendLog($"📊 Sheets Selected: {selectedItems.Count}");

            _btnExportStart.Enabled = false;
            var retryQueue = new ExportRetryQueue(options.MaxRetryCount);

            try
            {
                var qaEntries = retryQueue.ProcessBatch(_doc, selectedItems, options, AppendLog);

                // Combine PDF Post-processing
                if (options.ExportPdf && options.CombinePdf)
                {
                    AppendLog("\n🧩 Đang gộp các file PDF thành 1 file duy nhất...");
                    string rawCombinedName = options.CombinedPdfFileName.Replace(".pdf", "");
                    var sheetsToCombine = selectedItems.Select(i => i.Sheet).ToList();
                    string combinedPath = PdfExportEngine.ExportCombinedSheets(_doc, sheetsToCombine, outDir, rawCombinedName);

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
                }

                // Generate Transmittal Register Excel
                if (options.GenerateTransmittal)
                {
                    AppendLog("\n📊 Đang sinh file Excel Bảng kê phát hành (Transmittal)...");
                    string transPath = TransmittalGeneratorService.GenerateExcelTransmittal(outDir, options.IssueSetName, options.ProjectCode, selectedItems);
                    AppendLog($"  ✓ Transmittal: {Path.GetFileName(transPath)}");
                }

                // Generate QA Technical Log Excel
                if (options.GenerateQaReport)
                {
                    AppendLog("\n📝 Đang sinh file Excel Báo cáo kỹ thuật QA Log...");
                    string qaPath = QaReportService.GenerateQaExcelReport(outDir, options.ProjectCode, qaEntries);
                    AppendLog($"  ✓ QA Log: {Path.GetFileName(qaPath)}");
                }

                // Save Snapshot to ExtensibleStorage
                RevisionSnapshotService.CreateSnapshot(_doc, options.IssueSetName, selectedItems, options.ExportPdf ? "PDF" : "DWG");

                AppendLog("\n🎉 HOÀN TẤT TOÀN BỘ QUÁ TRÌNH BATCH EXPORT!");
                KhimDialogHelper.ShowSuccess("Export Hoàn Tất", $"Đã xuất thành công {selectedItems.Count} sheet bản vẽ vào thư mục:\n{outDir}");
            }
            catch (Exception ex)
            {
                AppendLog($"\n❌ LỖI NGHIÊM TRỌNG: {ex.Message}");
                KhimDialogHelper.ShowError("Lỗi Export Batch", ex.Message, ex.StackTrace);
            }
            finally
            {
                _btnExportStart.Enabled = true;
                _gridSheets.Refresh();
            }
        }
    }
}

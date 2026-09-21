using KhimTools.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.SheetGen.Models;
using KhimTools.SheetGen.Services;
using ComboBox = System.Windows.Forms.ComboBox;
using Form = System.Windows.Forms.Form;
using Point = System.Drawing.Point;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TextBox = System.Windows.Forms.TextBox;
using View = Autodesk.Revit.DB.View;

namespace KhimTools.SheetGen.Forms
{
    public partial class SheetGenForm : KTBaseForm
    {
        private readonly Document _doc;
        private List<TitleBlockOption> _titleBlocks;
        private List<ViewOption> _views;

        // UI Components
        private TabControl _tabControl;
        private DataGridView _gridSeries;
        private DataGridView _gridPreview;
        private Label _lblStatus;
        private Button _btnGenerate;
        private bool _updatingStatus;

        public SheetGenForm(Document doc) : this(doc, true) { }

        internal static SheetGenForm CreateLayoutPreview() => new SheetGenForm(null, false);

        private SheetGenForm(Document doc, bool loadDocument)
        {
            _doc = doc;
            if (loadDocument) InitializeData();
            else { _titleBlocks = new List<TitleBlockOption>(); _views = new List<ViewOption>(); }
            InitializeComponent();
            ApplyLanguage();
            if (!loadDocument) _btnGenerate.Enabled = false;
        }

        private void InitializeData()
        {
            _titleBlocks = SheetGenService.GetAvailableTitleBlocks(_doc);
            _views = SheetGenService.GetAvailableViews(_doc);
        }

        private void InitializeComponent()
        {
            this.Text = "K-TOOLS - Multi-Series Sheet Generator (SheetGen)";
            this.Size = new Size(1150, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(950, 600);
            this.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);


            // 2. Tab Control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Point(16, 8)
            };

            // TAB 1: CẤU HÌNH SERIES
            var tabSeries = new TabPage("  ⚡ Cấu Hình Các Phân Hệ Series  ");
            tabSeries.BackColor = System.Drawing.Color.White;
            SetupSeriesTab(tabSeries);
            _tabControl.TabPages.Add(tabSeries);

            // TAB 2: PREVIEW BẢNG SHEET CHI TIẾT
            var tabPreview = new TabPage("  📋 Danh Sách Sheet Chi Tiết (Preview)  ");
            tabPreview.BackColor = System.Drawing.Color.White;
            SetupPreviewTab(tabPreview);
            _tabControl.TabPages.Add(tabPreview);
            SetupAutoViewSheetTab();

            this.Controls.Add(_tabControl);

            // 3. Bottom Action Panel
            var pnlBottom = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = System.Drawing.Color.FromArgb(241, 245, 249),
                Padding = new Padding(15, 10, 15, 10)
            };

            var btnImport = new Button
            {
                Text = "📥 Import CSV",
                Location = new Point(15, 15),
                Size = new Size(110, 32),
                BackColor = System.Drawing.Color.FromArgb(71, 85, 105),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnImport.Click += (s, e) => ImportCsv();

            var btnExport = new Button
            {
                Text = "📤 Xuất Mẫu CSV",
                Location = new Point(135, 15),
                Size = new Size(120, 32),
                BackColor = System.Drawing.Color.FromArgb(71, 85, 105),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnExport.Click += (s, e) => ExportCsv();

            _lblStatus = new System.Windows.Forms.Label
            {
                Text = "Tổng số: 0 Sheet được cấu hình",
                Location = new Point(275, 22),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(71, 85, 105)
            };

            _btnGenerate = new Button
            {
                Text = "🚀 TẠO TẤT CẢ SHEET VÀO REVIT",
                Dock = DockStyle.Right,
                Width = 240,
                BackColor = System.Drawing.Color.FromArgb(16, 185, 129), // Emerald 500
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnGenerate.Click += (s, e) => ExecuteCreateSheets();

            var btnClose = new Button
            {
                Text = "Đóng",
                Dock = DockStyle.Right,
                Width = 90,
                BackColor = System.Drawing.Color.FromArgb(203, 213, 225),
                ForeColor = System.Drawing.Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                btnImport, btnExport, _lblStatus, _btnGenerate, btnClose
            });
            this.Controls.Add(pnlBottom);

            pnlBottom.BringToFront();
            _tabControl.BringToFront();

            // Nạp sẵn preset mẫu Kết Cấu
            LoadStructuralPreset();
        }

        private void SetupSeriesTab(TabPage tab)
        {
            var pnlTop = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                Padding = new Padding(10, 8, 10, 8),
                BackColor = System.Drawing.Color.FromArgb(248, 250, 252)
            };

            var lblPreset = new System.Windows.Forms.Label
            {
                Text = "📌 Nạp Mẫu Series Nhanh:",
                Location = new Point(10, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            var btnPresetStruct = new Button
            {
                Text = "🏢 Kết Cấu (Structural)",
                Location = new Point(170, 8),
                Size = new Size(150, 28),
                BackColor = System.Drawing.Color.FromArgb(2, 132, 199),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnPresetStruct.Click += (s, e) => LoadStructuralPreset();

            var btnPresetArch = new Button
            {
                Text = "Kiến Trúc (Arch)",
                Location = new Point(328, 8),
                Size = new Size(140, 28),
                BackColor = System.Drawing.Color.FromArgb(13, 148, 136),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnPresetArch.Click += (s, e) => LoadArchitecturalPreset();

            var btnPresetMep = new Button
            {
                Text = "⚡ Cơ Điện (MEP)",
                Location = new Point(476, 8),
                Size = new Size(130, 28),
                BackColor = System.Drawing.Color.FromArgb(217, 119, 6),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnPresetMep.Click += (s, e) => LoadMepPreset();

            var btnAddSeriesRow = new Button
            {
                Text = "Thêm series",
                Location = new Point(620, 8),
                Size = new Size(110, 28),
                BackColor = System.Drawing.Color.FromArgb(100, 116, 139),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnAddSeriesRow.Click += (s, e) => AddSeriesRow();

            var btnGenerateFromSeries = new Button
            {
                Text = "⚡ SINH TẤT CẢ SHEET TỪ SERIES",
                Location = new Point(740, 6),
                Size = new Size(240, 32),
                BackColor = System.Drawing.Color.FromArgb(99, 102, 241), // Indigo 500
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnGenerateFromSeries.Click += (s, e) => GenerateSheetsFromSeriesAndSwitch();

            pnlTop.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                lblPreset, btnPresetStruct, btnPresetArch, btnPresetMep, btnAddSeriesRow, btnGenerateFromSeries
            });
            tab.Controls.Add(pnlTop);

            _gridSeries = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9F)
            };

            SetupSeriesGridColumns();
            tab.Controls.Add(_gridSeries);
            pnlTop.BringToFront();
            _gridSeries.BringToFront();
        }

        private void SetupSeriesGridColumns()
        {
            _gridSeries.Columns.Clear();

            _gridSeries.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "☑️", Width = 40 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên Phân Hệ Series", Width = 160 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tiền Tố (Prefix)", Width = 90 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bắt Đầu", Width = 70 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Số Lượng", Width = 75 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bước", Width = 55 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Quy Tắc Tên (Name Template: {n})", Width = 230 });

            var tbCol = new DataGridViewComboBoxColumn { HeaderText = "Khung Tên (TitleBlock)", Width = 180 };
            tbCol.Items.Add("<No title block - explicit blank sheet>"); tbCol.Items.Add("<Missing title block>"); foreach (var tb in _titleBlocks) tbCol.Items.Add(tb.Name);
            _gridSeries.Columns.Add(tbCol);

            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bộ Môn (Discipline)", Width = 110 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hậu Tố (Suffix)", Width = 80 });
            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Padding Số", Width = 75 });
        }

        private void SetupPreviewTab(TabPage tab)
        {
            var pnlTop = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(10, 6, 10, 6),
                BackColor = System.Drawing.Color.FromArgb(248, 250, 252)
            };

            var btnAddRow = new Button
            {
                Text = "Thêm 1 sheet rời",
                Location = new Point(10, 6),
                Size = new Size(130, 28),
                BackColor = System.Drawing.Color.FromArgb(100, 116, 139),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnAddRow.Click += (s, e) => AddSinglePreviewRow();

            var btnDeleteRow = new Button
            {
                Text = "Xóa sheet đang chọn",
                Location = new Point(150, 6),
                Size = new Size(160, 28),
                BackColor = System.Drawing.Color.FromArgb(239, 68, 68),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnDeleteRow.Click += (s, e) => DeleteSelectedPreviewRows();

            var btnClearAll = new Button
            {
                Text = "🧹 Xóa Toàn Bộ",
                Location = new Point(320, 6),
                Size = new Size(110, 28),
                BackColor = System.Drawing.Color.FromArgb(148, 163, 184),
                ForeColor = System.Drawing.Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F)
            };
            btnClearAll.Click += (s, e) => { _gridPreview.Rows.Clear(); UpdateStatus(); };

            pnlTop.Controls.AddRange(new System.Windows.Forms.Control[] { btnAddRow, btnDeleteRow, btnClearAll });
            tab.Controls.Add(pnlTop);

            _gridPreview = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 9F)
            };

            SetupPreviewGridColumns();
            _gridPreview.CellEndEdit += (sender, args) => { if (args.ColumnIndex < 8) UpdateStatus(); };
            tab.Controls.Add(_gridPreview);
            pnlTop.BringToFront();
            _gridPreview.BringToFront();
        }

        private void SetupPreviewGridColumns()
        {
            _gridPreview.Columns.Clear();
            _gridPreview.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "☑️", Width = 40, FillWeight = 5 });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Số Hiệu (Sheet Number)", FillWeight = 20 });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên Bản Vẽ (Sheet Name)", FillWeight = 35 });

            var tbCol = new DataGridViewComboBoxColumn { HeaderText = "Khung Tên (TitleBlock)", FillWeight = 25 };
            tbCol.Items.Add("<No title block - explicit blank sheet>"); tbCol.Items.Add("<Missing title block>"); foreach (var tb in _titleBlocks) tbCol.Items.Add(tb.Name);
            _gridPreview.Columns.Add(tbCol);

            var viewCol = new DataGridViewComboBoxColumn { HeaderText = "Gán Viewport (Optional)", FillWeight = 25 };
            viewCol.Items.Add("<Không gán View>");
            foreach (var v in _views) viewCol.Items.Add(v.ToString());
            _gridPreview.Columns.Add(viewCol);

            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bộ Môn", FillWeight = 12 });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Người vẽ", FillWeight = 10 });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kiểm tra", FillWeight = 10 });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", FillWeight = 16, ReadOnly = true });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status message", FillWeight = 35, ReadOnly = true });
        }

        private void LoadStructuralPreset()
        {
            _gridSeries.Rows.Clear();
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "<Missing title block>";

            _gridSeries.Rows.Add(true, "Móng & Cọc", "KC-", 1, 3, 1, "MẶT BẰNG & CHI TIẾT MÓNG {n}", defTb, "Structural");
            _gridSeries.Rows.Add(true, "Mặt Bằng Kết Cấu", "KC-", 101, 5, 1, "MẶT BẰNG KẾT CẤU TẦNG {n}", defTb, "Structural");
            _gridSeries.Rows.Add(true, "Chi Tiết Cột Vách", "KC-", 201, 3, 1, "CHI TIẾT CỘT VÁCH KHU {n}", defTb, "Structural");
            _gridSeries.Rows.Add(true, "Chi Tiết Dầm Sàn", "KC-", 301, 5, 1, "CHI TIẾT DẦM SÀN TẦNG {n}", defTb, "Structural");
            _gridSeries.Rows.Add(true, "Bảng Thống Kê", "KC-", 401, 2, 1, "BẢNG THỐNG KÊ VẬT LIỆU {n}", defTb, "Structural");

            GenerateSheetsFromSeries();
        }

        private void LoadArchitecturalPreset()
        {
            _gridSeries.Rows.Clear();
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "<Missing title block>";

            _gridSeries.Rows.Add(true, "Mặt Bằng Kiến Trúc", "KT-", 101, 5, 1, "MẶT BẰNG KIẾN TRÚC TẦNG {n}", defTb, "Architectural");
            _gridSeries.Rows.Add(true, "Mặt Đứng Tổng Thể", "KT-", 201, 4, 1, "MẶT ĐỨNG TRỤC {n}", defTb, "Architectural");
            _gridSeries.Rows.Add(true, "Mặt Cắt Công Trình", "KT-", 301, 3, 1, "MẶT CẮT {n}-{n}", defTb, "Architectural");
            _gridSeries.Rows.Add(true, "Chi Tiết Cửa & WC", "KT-", 401, 4, 1, "CHI TIẾT KIẾN TRÚC {n}", defTb, "Architectural");

            GenerateSheetsFromSeries();
        }

        private void LoadMepPreset()
        {
            _gridSeries.Rows.Clear();
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "<Missing title block>";

            _gridSeries.Rows.Add(true, "Hệ Thống Điện", "E-", 101, 5, 1, "MẶT BẰNG HỆ THỐNG ĐIỆN TẦNG {n}", defTb, "Electrical");
            _gridSeries.Rows.Add(true, "Cấp Thoát Nước", "P-", 101, 5, 1, "MẶT BẰNG CẤP THOÁT NƯỚC TẦNG {n}", defTb, "Plumbing");
            _gridSeries.Rows.Add(true, "Thông Gió & HVAC", "M-", 101, 5, 1, "MẶT BẰNG ĐIỀU HÒA THÔNG GIÓ TẦNG {n}", defTb, "HVAC");

            GenerateSheetsFromSeries();
        }

        private void AddSeriesRow()
        {
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "<Missing title block>";
            _gridSeries.Rows.Add(true, "Phân Hệ Mới", "KC-", 501, 3, 1, "BẢN VẼ CHI TIẾT {n}", defTb, "Structural");
        }

        private void GenerateSheetsFromSeries()
        {
            var seriesList = new List<SheetSeriesConfig>();
            foreach (DataGridViewRow row in _gridSeries.Rows)
            {
                if (row.IsNewRow) continue;
                bool isEnabled = Convert.ToBoolean(row.Cells[0].Value ?? false);
                if (!isEnabled) continue;

                seriesList.Add(new SheetSeriesConfig
                {
                    IsEnabled = true,
                    SeriesName = row.Cells[1].Value?.ToString() ?? "",
                    Prefix = row.Cells[2].Value?.ToString() ?? "KC-",
                    StartNumber = int.TryParse(row.Cells[3].Value?.ToString(), out int start) ? start : 1,
                    Count = int.TryParse(row.Cells[4].Value?.ToString(), out int cnt) ? cnt : 1,
                    Step = int.TryParse(row.Cells[5].Value?.ToString(), out int st) ? st : 1,
                    NamePattern = row.Cells[6].Value?.ToString() ?? "BẢN VẼ {n}",
                    TitleBlockName = ((row.Cells[7].Value?.ToString() ?? "").StartsWith("<No title block", StringComparison.OrdinalIgnoreCase) || (row.Cells[7].Value?.ToString() ?? "").StartsWith("<Missing title block", StringComparison.OrdinalIgnoreCase)) ? "" : row.Cells[7].Value?.ToString() ?? "",
                    Discipline = row.Cells[8].Value?.ToString() ?? "Structural",
                    Suffix = row.Cells.Count > 9 ? row.Cells[9].Value?.ToString() ?? "" : "",
                    NumberPadding = row.Cells.Count > 10 && int.TryParse(row.Cells[10].Value?.ToString(), out int padding) ? Math.Max(0, padding) : 2,
                    AllowBlankTitleBlock = (row.Cells[7].Value?.ToString() ?? "").StartsWith("<No title block", StringComparison.OrdinalIgnoreCase)
                });
            }

            var items = SheetGenService.GenerateFromSeries(seriesList, _titleBlocks);
            _gridPreview.Rows.Clear();

            foreach (var it in items)
            {
                _gridPreview.Rows.Add(true, it.SheetNumber, it.SheetName, string.IsNullOrWhiteSpace(it.TitleBlockName) ? (it.AllowBlankTitleBlock ? "<No title block - explicit blank sheet>" : "<Missing title block>") : it.TitleBlockName, "<Không gán View>", it.Discipline, "", "", SheetGenStatusCodes.ToDisplayCode(it.StatusCode), it.StatusMessage);
            }

            UpdateStatus();
        }

        private void GenerateSheetsFromSeriesAndSwitch()
        {
            GenerateSheetsFromSeries();
            _tabControl.SelectedIndex = 1; // Switch to Preview tab
        }

        private void AddSinglePreviewRow()
        {
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "<Missing title block>";
            _gridPreview.Rows.Add(true, "NEW-01", "TÊN BẢN VẼ MỚI", defTb, "<Không gán View>", "Structural", "", "", "Ready", "");
            UpdateStatus();
        }

        private void DeleteSelectedPreviewRows()
        {
            var selectedRows = _gridPreview.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (selectedRows.Any())
            {
                foreach (var r in selectedRows) _gridPreview.Rows.Remove(r);
            }
            else if (_gridPreview.CurrentRow != null)
            {
                _gridPreview.Rows.Remove(_gridPreview.CurrentRow);
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_updatingStatus) return;
            _updatingStatus = true;
            try
            {
                int total = _gridPreview.Rows.Count;
                int selected = _gridPreview.Rows.Cast<DataGridViewRow>().Count(r => Convert.ToBoolean(r.Cells[0].Value ?? false));
                _lblStatus.Text = LanguageManager.IsEnglish
                    ? $"Total: {total} Sheets generated ({selected} selected for creation)"
                    : $"Tổng cộng: {total} Sheet đã sinh ({selected} được chọn tạo vào Revit)";
                RefreshPreviewStatus();
            }
            finally { _updatingStatus = false; }
        }

        private void RefreshPreviewStatus()
        {
            if (_doc == null || _gridPreview == null || _gridPreview.Rows.Count == 0) return;
            var items = CollectItemsFromGrid();
            var checks = SheetGenPreflightService.Validate(_doc, items);
            bool canCreate = false;
            for (int i = 0; i < checks.Count && i < _gridPreview.Rows.Count; i++)
            {
                var row = _gridPreview.Rows[i];
                row.Cells[8].Value = SheetGenStatusCodes.ToDisplayCode(checks[i].Code);
                row.Cells[9].Value = checks[i].Message;
                if (items[i].IsSelected && checks[i].CanCreate) canCreate = true;
            }
            _btnGenerate.Enabled = canCreate;
        }

        private void ExportCsv()
        {
            using (var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = "KhimTools_MultiSeries_Sheets.csv",
                Title = "Xuất Danh Sách Sheet ra CSV"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var items = CollectItemsFromGrid();
                    if (SheetGenService.ExportToCsv(sfd.FileName, items))
                    {
                        TaskDialog.Show("Xuất Danh Mục", "Đã xuất file danh mục Sheet thành công!");
                    }
                }
            }
        }

        private void ImportCsv()
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                Title = "Import Danh Mục Sheet từ CSV"
            })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                var importedResult = SheetGenService.ImportFromCsvDetailed(ofd.FileName, _titleBlocks, _views);
                if (importedResult.Items.Any())
                {
                    _gridPreview.Rows.Clear();
                    foreach (var it in importedResult.Items)
                    {
                        var importedView = _views.FirstOrDefault(v => string.Equals(v.UniqueId, it.AssignedViewUniqueId, StringComparison.Ordinal) ||
                                                                       string.Equals(v.Name, it.AssignedViewName, StringComparison.OrdinalIgnoreCase));
                        string viewDisplay = importedView == null ? "<Không gán View>" : importedView.ToString();
                        _gridPreview.Rows.Add(it.IsSelected, it.SheetNumber, it.SheetName,
                            string.IsNullOrWhiteSpace(it.TitleBlockName) ? (it.AllowBlankTitleBlock ? "<No title block - explicit blank sheet>" : "<Missing title block>") : it.TitleBlockName,
                            viewDisplay,
                            it.Discipline, it.DrawnBy, it.CheckedBy, SheetGenStatusCodes.ToDisplayCode(it.StatusCode), it.StatusMessage);
                    }
                    UpdateStatus();
                    _tabControl.SelectedIndex = 1;
                }

                string detail = importedResult.Diagnostics.Count == 0 ? "" :
                    Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, importedResult.Diagnostics.Take(12).Select(d => d.ToString()));
                TaskDialog.Show("Import Sheet",
                    $"Đã nạp {importedResult.Items.Count} Sheet. Diagnostics: {importedResult.Diagnostics.Count}.{detail}");
            }
        }

        private List<SheetGenItem> CollectItemsFromGrid()
        {
            var list = new List<SheetGenItem>();
            foreach (DataGridViewRow row in _gridPreview.Rows)
            {
                if (row.IsNewRow) continue;
                string titleBlockCell = row.Cells[3].Value?.ToString() ?? "";
                var item = new SheetGenItem
                {
                    IsSelected = Convert.ToBoolean(row.Cells[0].Value ?? false),
                    SheetNumber = row.Cells[1].Value?.ToString()?.Trim() ?? "",
                    SheetName = row.Cells[2].Value?.ToString()?.Trim() ?? "",
                    TitleBlockName = (titleBlockCell.StartsWith("<No title block", StringComparison.OrdinalIgnoreCase) || titleBlockCell.StartsWith("<Missing title block", StringComparison.OrdinalIgnoreCase)) ? "" : titleBlockCell,
                    AllowBlankTitleBlock = titleBlockCell.StartsWith("<No title block", StringComparison.OrdinalIgnoreCase),
                    AssignedViewName = row.Cells[4].Value?.ToString() ?? "",
                    Discipline = row.Cells[5].Value?.ToString() ?? "",
                    DrawnBy = row.Cells[6].Value?.ToString()?.Trim() ?? "",
                    CheckedBy = row.Cells[7].Value?.ToString()?.Trim() ?? ""
                };

                var tb = _titleBlocks.FirstOrDefault(t => string.Equals(t.Name, item.TitleBlockName, StringComparison.OrdinalIgnoreCase));
                if (tb != null) { item.TitleBlockId = tb.Id; item.TitleBlockName = tb.Name; item.TitleBlockUniqueId = tb.UniqueId; }

                SheetGenStatusCode importedStatus;
                if (SheetGenStatusCodes.TryParse(row.Cells[8].Value?.ToString(), out importedStatus)) item.StatusCode = importedStatus;
                item.StatusMessage = row.Cells[9].Value?.ToString() ?? "";

                var v = _views.FirstOrDefault(vw => vw.ToString() == item.AssignedViewName ||
                                                    string.Equals(vw.Name, item.AssignedViewName, StringComparison.OrdinalIgnoreCase));
                if (v != null)
                {
                    item.AssignedViewId = v.Id;
                    item.AssignedViewUniqueId = v.UniqueId;
                    item.AssignedViewType = v.ViewType;
                    item.ContentKind = v.ContentKind;
                    item.AssignedViewName = v.Name;
                }
                list.Add(item);
            }
            return list;
        }

        private void ExecuteCreateSheets()
        {
            var items = CollectItemsFromGrid().Where(it => it.IsSelected).ToList();
            if (!items.Any())
            {
                TaskDialog.Show("Tạo Sheet", LanguageManager.IsEnglish ? "Please select at least 1 Sheet to create." : "Vui lòng tích chọn ít nhất 1 Sheet cần tạo.");
                return;
            }

            _btnGenerate.Enabled = false;
            _btnGenerate.Text = LanguageManager.IsEnglish ? "Creating..." : "Đang tạo Sheet...";
            try
            {
                var result = SheetGenService.CreateSheetsDetailed(_doc, items);
                string summary = LanguageManager.IsEnglish
                    ? "Requested: " + result.Requested + Environment.NewLine +
                      "Valid: " + result.Valid + Environment.NewLine +
                      "Created: " + result.Created + Environment.NewLine +
                      "Skipped: " + result.Skipped + Environment.NewLine +
                      "Failed: " + result.Failed
                    : "Yêu cầu: " + result.Requested + Environment.NewLine +
                      "Hợp lệ: " + result.Valid + Environment.NewLine +
                      "Đã tạo: " + result.Created + Environment.NewLine +
                      "Bỏ qua: " + result.Skipped + Environment.NewLine +
                      "Lỗi: " + result.Failed;
                var details = result.Results.Where(r => r.Status != SheetGenStatusCode.Created)
                    .Take(15).Select(r => $"{r.SheetNumber}: {r.Status} - {r.StatusText}").ToList();
                if (details.Any()) summary += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, details);
                TaskDialog.Show("Kết Quả Tạo Sheet", summary);

                if (result.Created > 0 && result.Failed == 0 && result.Skipped == 0)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            finally
            {
                _btnGenerate.Text = LanguageManager.IsEnglish ? "🚀 CREATE ALL SHEETS" : "🚀 TẠO TẤT CẢ SHEET VÀO REVIT";
                RefreshPreviewStatus();
            }
        }

        private void ApplyLanguage()
        {
            bool isEn = LanguageManager.IsEnglish;
            if (isEn)
            {
                this.Text = "K-TOOLS - Multi-Series Sheet Generator (SheetGen)";
                _btnGenerate.Text = "🚀 CREATE ALL SHEETS INTO REVIT";
            }
            UpdateStatus();
        }
    }
}

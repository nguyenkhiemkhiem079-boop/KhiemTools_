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
    public class SheetGenForm : Form
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

        public SheetGenForm(Document doc)
        {
            _doc = doc;
            InitializeData();
            InitializeComponent();
            ApplyLanguage();
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

            // 1. Header Panel
            var pnlHeader = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = System.Drawing.Color.FromArgb(15, 23, 42) // Slate 900
            };

            var lblTitle = new System.Windows.Forms.Label
            {
                Text = "⚡ TẠO SHEET THEO NHIỀU PHÂN HỆ SERIES (MULTI-SERIES SHEETGEN)",
                ForeColor = System.Drawing.Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(20, 10),
                AutoSize = true
            };
            var lblSub = new System.Windows.Forms.Label
            {
                Text = "Cấu hình hàng loạt Series theo từng bộ môn/hạng mục, tự động tăng tiến số hiệu & gán Khung tên chuẩn DiRoots",
                ForeColor = System.Drawing.Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F),
                Location = new Point(20, 36),
                AutoSize = true
            };
            pnlHeader.Controls.AddRange(new System.Windows.Forms.Control[] { lblTitle, lblSub });
            this.Controls.Add(pnlHeader);

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

            pnlHeader.BringToFront();
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
                Text = "🏛️ Kiến Trúc (Arch)",
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
                Text = "➕ Thêm Series",
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
            foreach (var tb in _titleBlocks) tbCol.Items.Add(tb.Name);
            _gridSeries.Columns.Add(tbCol);

            _gridSeries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bộ Môn (Discipline)", Width = 110 });
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
                Text = "➕ Thêm 1 Sheet Rời",
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
                Text = "🗑️ Xóa Sheet Đang Chọn",
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
            foreach (var tb in _titleBlocks) tbCol.Items.Add(tb.Name);
            _gridPreview.Columns.Add(tbCol);

            var viewCol = new DataGridViewComboBoxColumn { HeaderText = "Gán Viewport (Optional)", FillWeight = 25 };
            viewCol.Items.Add("<Không gán View>");
            foreach (var v in _views) viewCol.Items.Add(v.ToString());
            _gridPreview.Columns.Add(viewCol);

            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bộ Môn", FillWeight = 12 });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Người vẽ", FillWeight = 10 });
            _gridPreview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kiểm tra", FillWeight = 10 });
        }

        private void LoadStructuralPreset()
        {
            _gridSeries.Rows.Clear();
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "";

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
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "";

            _gridSeries.Rows.Add(true, "Mặt Bằng Kiến Trúc", "KT-", 101, 5, 1, "MẶT BẰNG KIẾN TRÚC TẦNG {n}", defTb, "Architectural");
            _gridSeries.Rows.Add(true, "Mặt Đứng Tổng Thể", "KT-", 201, 4, 1, "MẶT ĐỨNG TRỤC {n}", defTb, "Architectural");
            _gridSeries.Rows.Add(true, "Mặt Cắt Công Trình", "KT-", 301, 3, 1, "MẶT CẮT {n}-{n}", defTb, "Architectural");
            _gridSeries.Rows.Add(true, "Chi Tiết Cửa & WC", "KT-", 401, 4, 1, "CHI TIẾT KIẾN TRÚC {n}", defTb, "Architectural");

            GenerateSheetsFromSeries();
        }

        private void LoadMepPreset()
        {
            _gridSeries.Rows.Clear();
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "";

            _gridSeries.Rows.Add(true, "Hệ Thống Điện", "E-", 101, 5, 1, "MẶT BẰNG HỆ THỐNG ĐIỆN TẦNG {n}", defTb, "Electrical");
            _gridSeries.Rows.Add(true, "Cấp Thoát Nước", "P-", 101, 5, 1, "MẶT BẰNG CẤP THOÁT NƯỚC TẦNG {n}", defTb, "Plumbing");
            _gridSeries.Rows.Add(true, "Thông Gió & HVAC", "M-", 101, 5, 1, "MẶT BẰNG ĐIỀU HÒA THÔNG GIÓ TẦNG {n}", defTb, "HVAC");

            GenerateSheetsFromSeries();
        }

        private void AddSeriesRow()
        {
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "";
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
                    TitleBlockName = row.Cells[7].Value?.ToString() ?? "",
                    Discipline = row.Cells[8].Value?.ToString() ?? "Structural"
                });
            }

            var items = SheetGenService.GenerateFromSeries(seriesList, _titleBlocks);
            _gridPreview.Rows.Clear();

            foreach (var it in items)
            {
                _gridPreview.Rows.Add(true, it.SheetNumber, it.SheetName, it.TitleBlockName, "<Không gán View>", it.Discipline, "", "");
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
            string defTb = _titleBlocks.FirstOrDefault()?.Name ?? "";
            _gridPreview.Rows.Add(true, "NEW-01", "TÊN BẢN VẼ MỚI", defTb, "<Không gán View>", "Structural", "", "");
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
            int total = _gridPreview.Rows.Count;
            int selected = _gridPreview.Rows.Cast<DataGridViewRow>().Count(r => Convert.ToBoolean(r.Cells[0].Value));
            _lblStatus.Text = LanguageManager.IsEnglish
                ? $"Total: {total} Sheets generated ({selected} selected for creation)"
                : $"Tổng cộng: {total} Sheet đã sinh ({selected} được chọn tạo vào Revit)";
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
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    var imported = SheetGenService.ImportFromCsv(ofd.FileName, _titleBlocks);
                    if (imported.Any())
                    {
                        _gridPreview.Rows.Clear();
                        foreach (var it in imported)
                        {
                            _gridPreview.Rows.Add(true, it.SheetNumber, it.SheetName, it.TitleBlockName, "<Không gán View>", it.Discipline, it.DrawnBy, it.CheckedBy);
                        }
                        UpdateStatus();
                        _tabControl.SelectedIndex = 1;
                        TaskDialog.Show("Import Sheet", $"Đã nạp thành công {imported.Count} Sheet từ file CSV!");
                    }
                }
            }
        }

        private List<SheetGenItem> CollectItemsFromGrid()
        {
            var list = new List<SheetGenItem>();
            foreach (DataGridViewRow row in _gridPreview.Rows)
            {
                if (row.IsNewRow) continue;

                var item = new SheetGenItem
                {
                    IsSelected = Convert.ToBoolean(row.Cells[0].Value ?? false),
                    SheetNumber = row.Cells[1].Value?.ToString()?.Trim() ?? "",
                    SheetName = row.Cells[2].Value?.ToString()?.Trim() ?? "",
                    TitleBlockName = row.Cells[3].Value?.ToString() ?? "",
                    AssignedViewName = row.Cells[4].Value?.ToString() ?? "",
                    Discipline = row.Cells[5].Value?.ToString() ?? "",
                    DrawnBy = row.Cells[6].Value?.ToString()?.Trim() ?? "",
                    CheckedBy = row.Cells[7].Value?.ToString()?.Trim() ?? ""
                };

                var tb = _titleBlocks.FirstOrDefault(t => t.Name == item.TitleBlockName);
                if (tb != null) item.TitleBlockId = tb.Id;

                var v = _views.FirstOrDefault(vw => vw.ToString() == item.AssignedViewName);
                if (v != null) item.AssignedViewId = v.Id;

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
                var (created, errors) = SheetGenService.CreateSheets(_doc, items);

                string resultMsg = LanguageManager.IsEnglish
                    ? $"Successfully created {created} / {items.Count} Sheets into project!"
                    : $"Đã tạo thành công {created} / {items.Count} Sheet vào dự án!";

                if (errors.Any())
                {
                    resultMsg += "\n\n" + (LanguageManager.IsEnglish ? "Warnings/Errors:" : "Chi tiết lưu ý:") + "\n" + string.Join("\n", errors.Take(10));
                }

                TaskDialog.Show("Kết Quả Tạo Sheet", resultMsg);

                if (created > 0)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            finally
            {
                _btnGenerate.Enabled = true;
                _btnGenerate.Text = LanguageManager.IsEnglish ? "🚀 CREATE ALL SHEETS" : "🚀 TẠO TẤT CẢ SHEET VÀO REVIT";
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
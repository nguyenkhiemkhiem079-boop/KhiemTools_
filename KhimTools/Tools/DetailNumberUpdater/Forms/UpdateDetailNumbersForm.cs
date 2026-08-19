using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.DetailNumberUpdater.Services;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using View = Autodesk.Revit.DB.View;

namespace KhimTools.DetailNumberUpdater.Forms
{
    public class UpdateDetailNumbersForm : Form
    {
        private readonly Document _doc;
        private readonly ViewSheet _sheet;
        private List<DetailNumberPreviewItem> _previewItems = new List<DetailNumberPreviewItem>();

        private TextBox _txtPattern;
        private DataGridView _dgvViews;
        private Button _btnSelectAll;
        private Button _btnClearAll;
        private Button _btnApply;
        private Button _btnCancel;
        private Button _btnRefresh;

        public (int Success, int Failed, List<string> Errors) ExecutionResult { get; private set; }

        public UpdateDetailNumbersForm(Document doc, ViewSheet sheet)
        {
            _doc = doc;
            _sheet = sheet;

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            RefreshData();
        }

        private void BuildUi()
        {
            Text = "🔢 KHIM TOOLS — Update Detail Numbers";
            Width = 780;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(680, 480);
            BackColor = KhimUiStyle.FormBg;

            // 1. Header Banner
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Update Detail Numbers",
                $"Tự động trích xuất & cập nhật số hiệu chi tiết (Detail Number) cho Sheet [{_sheet?.SheetNumber}] {_sheet?.Name}",
                "v2.5 Pro");
            Controls.Add(header);

            // 2. Pattern Configuration Panel
            var pnlConfig = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(15, 10, 15, 5),
                BackColor = KhimUiStyle.CardBg
            };

            var lblPattern = new Label
            {
                Text = "⚙️ Quy tắc Regex trích xuất mã (Pattern):",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextPrimary,
                AutoSize = true,
                Left = 15,
                Top = 10
            };

            _txtPattern = new TextBox
            {
                Text = DetailNumberService.DefaultPattern,
                Left = 15,
                Top = 32,
                Width = 480,
                Font = new Font("Consolas", 9.5F)
            };
            _txtPattern.TextChanged += (s, e) => RefreshData();

            _btnRefresh = new Button
            {
                Text = "🔄 Xem trước lại",
                Left = 505,
                Top = 30,
                Width = 115,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };
            _btnRefresh.Click += (s, e) => RefreshData();

            var lblHint = new Label
            {
                Text = "Ví dụ mẫu khớp: CW42, W42, 1-CW25, BEAM-CW12 (tự động thêm đuôi .1, .2 nếu trùng lặp)",
                Font = new Font("Segoe UI", 8F),
                ForeColor = KhimUiStyle.TextSecondary,
                AutoSize = true,
                Left = 15,
                Top = 60
            };

            pnlConfig.Controls.AddRange(new System.Windows.Forms.Control[] { lblPattern, _txtPattern, _btnRefresh, lblHint });
            Controls.Add(pnlConfig);

            // 3. Action Toolbar above Grid
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(15, 6, 15, 6),
                BackColor = KhimUiStyle.FormBg
            };

            _btnSelectAll = new Button
            {
                Text = "Chọn hết",
                Left = 15,
                Top = 7,
                Width = 80,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };
            _btnSelectAll.Click += (s, e) => SetSelectAll(true);

            _btnClearAll = new Button
            {
                Text = "Bỏ chọn",
                Left = 102,
                Top = 7,
                Width = 80,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };
            _btnClearAll.Click += (s, e) => SetSelectAll(false);

            pnlToolbar.Controls.AddRange(new System.Windows.Forms.Control[] { _btnSelectAll, _btnClearAll });
            Controls.Add(pnlToolbar);

            // 4. Bottom Buttons Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = KhimUiStyle.CardBg
            };

            _btnApply = new Button
            {
                Text = "⚡ Cập Nhật Detail Number",
                Width = 190,
                Height = 36,
                Left = 555,
                Top = 12,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnApply.Click += BtnApply_Click;

            _btnCancel = new Button
            {
                Text = "Đóng",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 36,
                Left = 465,
                Top = 12,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg,
                Font = new Font("Segoe UI", 9F)
            };

            pnlBottom.Controls.AddRange(new System.Windows.Forms.Control[] { _btnCancel, _btnApply });
            pnlBottom.Resize += (s, e) =>
            {
                _btnApply.Left = pnlBottom.Width - _btnApply.Width - 18;
                _btnCancel.Left = _btnApply.Left - _btnCancel.Width - 10;
            };
            Controls.Add(pnlBottom);

            // 5. DataGridView for Preview
            var pnlGrid = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 5, 15, 5),
                BackColor = KhimUiStyle.FormBg
            };

            _dgvViews = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var colCheck = new DataGridViewCheckBoxColumn { HeaderText = "Cập nhật", Width = 75, FillWeight = 20 };
            var colName = new DataGridViewTextBoxColumn { HeaderText = "Tên Khung Nhìn (View Name)", ReadOnly = true, FillWeight = 90 };
            var colCurNum = new DataGridViewTextBoxColumn { HeaderText = "Detail No Hiện Tại", ReadOnly = true, FillWeight = 40 };
            var colNewNum = new DataGridViewTextBoxColumn { HeaderText = "Detail No Mới", ReadOnly = false, FillWeight = 40 };

            _dgvViews.Columns.AddRange(colCheck, colName, colCurNum, colNewNum);
            pnlGrid.Controls.Add(_dgvViews);
            Controls.Add(pnlGrid);
        }

        private void RefreshData()
        {
            _previewItems = DetailNumberService.GeneratePreview(_doc, _sheet, _txtPattern.Text);
            _dgvViews.Rows.Clear();

            foreach (var item in _previewItems)
            {
                int rowIndex = _dgvViews.Rows.Add(item.IsSelected, item.ViewName, item.CurrentDetailNumber, item.NewDetailNumber);
                var row = _dgvViews.Rows[rowIndex];

                if (item.IsMatched)
                {
                    row.Cells[3].Style.ForeColor = Color.DarkGreen;
                    row.Cells[3].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else
                {
                    row.Cells[3].Style.ForeColor = Color.Gray;
                }
            }
        }

        private void SetSelectAll(bool select)
        {
            foreach (DataGridViewRow row in _dgvViews.Rows)
            {
                row.Cells[0].Value = select;
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            // Cập nhật lại trạng thái chọn và Detail number mới từ Grid
            for (int i = 0; i < _dgvViews.Rows.Count; i++)
            {
                var row = _dgvViews.Rows[i];
                var item = _previewItems[i];
                item.IsSelected = Convert.ToBoolean(row.Cells[0].Value);
                item.NewDetailNumber = row.Cells[3].Value?.ToString() ?? "";
            }

            var selected = _previewItems.Where(i => i.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Viewport để cập nhật!",
                    "Chưa chọn View", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ExecutionResult = DetailNumberService.ApplyDetailNumbers(_doc, selected);

            string msg = $"🎉 Đã cập nhật Detail Number thành công cho {ExecutionResult.Success} Viewport!";
            if (ExecutionResult.Failed > 0)
            {
                msg += $"\n⚠️ Thất bại / Bỏ qua: {ExecutionResult.Failed}\n" + string.Join("\n", ExecutionResult.Errors.Take(5));
            }

            MessageBox.Show(msg, "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

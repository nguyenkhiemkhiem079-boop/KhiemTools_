using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using CheckBox = System.Windows.Forms.CheckBox;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using View = Autodesk.Revit.DB.View;

namespace KhimTools.ViewportAlign.Forms
{
    public class AlignViewportForm : Form
    {
        private readonly Document _doc;
        private readonly Viewport _sourceViewport;
        private readonly View _sourceView;
        private readonly ViewSheet _sourceSheet;
        private readonly List<ViewSheet> _allSheets;

        private CheckedListBox _clbSheets;
        private TextBox _txtSearch;
        private CheckBox _chkSkipLegends;
        private Button _btnSelectAll;
        private Button _btnClearAll;
        private Button _btnExecute;
        private Button _btnCancel;

        public List<ViewSheet> SelectedTargetSheets { get; private set; } = new List<ViewSheet>();
        public bool SkipLegends => _chkSkipLegends.Checked;

        public AlignViewportForm(Document doc, Viewport sourceViewport)
        {
            _doc = doc;
            _sourceViewport = sourceViewport;
            _sourceView = _doc.GetElement(_sourceViewport.ViewId) as View;
            _sourceSheet = _doc.GetElement(_sourceViewport.SheetId) as ViewSheet;

            _allSheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder && s.Id != _sourceSheet?.Id)
                .OrderBy(s => s.SheetNumber)
                .ToList();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            PopulateSheetList();
        }

        private void BuildUi()
        {
            Text = "📐 KHIM TOOLS — Align Viewport Across Sheets";
            Width = 620;
            Height = 650;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = KhimUiStyle.FormBg;

            // 1. Header Banner
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Viewport Alignment",
                "Đồng bộ vị trí khung nhìn (Viewport) chuẩn xác trên nhiều Sheet",
                "v2.5 Pro");
            Controls.Add(header);

            // 2. Info Box (Source Viewport details)
            var pnlInfo = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(15, 10, 15, 5),
                BackColor = KhimUiStyle.CardBg
            };

            var lblSourceInfo = new Label
            {
                Text = $"📌 Khung nhìn nguồn (Source View): {_sourceView?.Name ?? "Unknown"}\n" +
                       $"📄 Nằm tại Sheet: {_sourceSheet?.SheetNumber} - {_sourceSheet?.Name ?? "Unknown"}\n" +
                       $"🎯 Mục tiêu: Căn chỉnh vị trí các viewport trên các Sheet được chọn trùng khớp với Sheet nguồn.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = KhimUiStyle.TextPrimary,
                Dock = DockStyle.Fill
            };
            pnlInfo.Controls.Add(lblSourceInfo);
            Controls.Add(pnlInfo);

            // 3. Search & Filter Bar
            var pnlFilter = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                Padding = new Padding(15, 8, 15, 5),
                BackColor = KhimUiStyle.FormBg
            };

            var lblSearch = new Label
            {
                Text = "🔍 Tìm kiếm Sheet:",
                AutoSize = true,
                Left = 15,
                Top = 12,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextSecondary
            };
            _txtSearch = new TextBox
            {
                Left = 145,
                Top = 9,
                Width = 260,
                Font = new Font("Segoe UI", 9F)
            };
            _txtSearch.TextChanged += (s, e) => PopulateSheetList(_txtSearch.Text);

            _btnSelectAll = new Button
            {
                Text = "Chọn hết",
                Left = 415,
                Top = 8,
                Width = 80,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };
            _btnSelectAll.Click += (s, e) => SetCheckAll(true);

            _btnClearAll = new Button
            {
                Text = "Bỏ chọn",
                Left = 500,
                Top = 8,
                Width = 80,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };
            _btnClearAll.Click += (s, e) => SetCheckAll(false);

            pnlFilter.Controls.AddRange(new System.Windows.Forms.Control[] { lblSearch, _txtSearch, _btnSelectAll, _btnClearAll });
            Controls.Add(pnlFilter);

            // 4. Bottom Action Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = KhimUiStyle.CardBg
            };

            _chkSkipLegends = new CheckBox
            {
                Text = "Bỏ qua Ghi chú (Notes) & Bảng thống kê (Schedules/Legends)",
                Checked = true,
                AutoSize = true,
                Left = 15,
                Top = 20,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextPrimary
            };

            _btnExecute = new Button
            {
                Text = "⚡ Căn Chỉnh Vị Trí",
                DialogResult = DialogResult.OK,
                Width = 150,
                Height = 36,
                Left = 425,
                Top = 15,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnExecute.Click += BtnExecute_Click;

            _btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 36,
                Left = 335,
                Top = 15,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg,
                Font = new Font("Segoe UI", 9F)
            };

            pnlBottom.Controls.AddRange(new System.Windows.Forms.Control[] { _chkSkipLegends, _btnCancel, _btnExecute });
            Controls.Add(pnlBottom);

            // 5. Main Checklist of Sheets
            var pnlList = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 5, 15, 5),
                BackColor = KhimUiStyle.FormBg
            };

            _clbSheets = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlList.Controls.Add(_clbSheets);
            Controls.Add(pnlList);
        }

        private void PopulateSheetList(string searchFilter = "")
        {
            _clbSheets.Items.Clear();
            string keyword = (searchFilter ?? "").Trim().ToLowerInvariant();

            foreach (var sheet in _allSheets)
            {
                string display = $"[{sheet.SheetNumber}] {sheet.Name}";
                if (string.IsNullOrEmpty(keyword) || display.ToLowerInvariant().Contains(keyword))
                {
                    _clbSheets.Items.Add(new SheetItem(sheet, display), true);
                }
            }
        }

        private void SetCheckAll(bool check)
        {
            for (int i = 0; i < _clbSheets.Items.Count; i++)
            {
                _clbSheets.SetItemChecked(i, check);
            }
        }

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            SelectedTargetSheets = _clbSheets.CheckedItems
                .Cast<SheetItem>()
                .Select(item => item.Sheet)
                .ToList();

            if (!SelectedTargetSheets.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Sheet mục tiêu để căn chỉnh!",
                    "Chưa chọn Sheet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private class SheetItem
        {
            public ViewSheet Sheet { get; }
            public string Display { get; }

            public SheetItem(ViewSheet sheet, string display)
            {
                Sheet = sheet;
                Display = display;
            }

            public override string ToString() => Display;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.ViewportAlign.Services;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using CheckBox = System.Windows.Forms.CheckBox;
using RadioButton = System.Windows.Forms.RadioButton;
using GroupBox = System.Windows.Forms.GroupBox;
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
        private Button _btnSelectAll;
        private Button _btnClearAll;
        private Button _btnExecute;
        private Button _btnCancel;

        // Alignment Options Controls
        private CheckBox _chkAlignModelViews;
        private CheckBox _chkAlignDrafting;
        private CheckBox _chkAlignLegends;
        private CheckBox _chkAlignSchedules;
        private RadioButton _rdMatchSimilar;
        private RadioButton _rdMatchAll;
        private TextBox _txtKeywordFilter;

        public List<ViewSheet> SelectedTargetSheets { get; private set; } = new List<ViewSheet>();
        public ViewportAlignOptions AlignOptions { get; private set; } = new ViewportAlignOptions();

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
            bool isEn = LanguageManager.IsEnglish;
            Text = isEn ? "Align Viewports & Schedules Across Sheets" : "Căn Chỉnh Vị Trí Viewport & Bảng Thống Kê Giữa Các Sheet";
            Width = 720;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = KhimUiStyle.FormBg;

            // 1. Info Card (Source Viewport details)
            var pnlInfo = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(15, 10, 15, 5),
                BackColor = KhimUiStyle.CardBg
            };

            var lblSourceInfo = new Label
            {
                Text = isEn
                    ? $"Source View: {_sourceView?.Name ?? "Unknown"}\n" +
                      $"On Sheet: [{_sourceSheet?.SheetNumber}] {_sourceSheet?.Name ?? "Unknown"}\n" +
                      $"🎯 Purpose: Align target sheets' viewports and schedules to match the source location."
                    : $"Khung nhìn nguồn: {_sourceView?.Name ?? "Chưa rõ"}\n" +
                      $"Nằm tại Sheet: [{_sourceSheet?.SheetNumber}] {_sourceSheet?.Name ?? "Chưa rõ"}\n" +
                      $"🎯 Mục tiêu: Đồng bộ vị trí khung nhìn và bảng thống kê trên các Sheet đích khớp với mẫu.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = KhimUiStyle.TextPrimary,
                Dock = DockStyle.Fill
            };
            pnlInfo.Controls.Add(lblSourceInfo);
            Controls.Add(pnlInfo);

            // 2. Alignment Options GroupBox
            var grpOptions = new GroupBox
            {
                Text = isEn ? "Options: Elements to Align" : "Tùy Chọn Đối Tượng Căn Chỉnh",
                Dock = DockStyle.Top,
                Height = 120,
                Padding = new Padding(12, 6, 12, 6),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextPrimary
            };

            var flpOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            _chkAlignModelViews = new CheckBox { Text = isEn ? "Model Views (Plans/Sections)" : "Mặt bằng / Mặt cắt mô hình", Checked = true, AutoSize = true, Margin = new Padding(3, 4, 15, 4) };
            _chkAlignDrafting = new CheckBox { Text = isEn ? "Drafting Views" : "Bản vẽ chi tiết (Drafting)", Checked = true, AutoSize = true, Margin = new Padding(3, 4, 15, 4) };
            _chkAlignLegends = new CheckBox { Text = isEn ? "Legends / Notes" : "Ghi chú (Legends / Notes)", Checked = false, AutoSize = true, Margin = new Padding(3, 4, 15, 4) };
            _chkAlignSchedules = new CheckBox { Text = isEn ? "Schedules (Bảng thống kê)" : "Bảng thống kê (Schedules)", Checked = true, AutoSize = true, Margin = new Padding(3, 4, 15, 4) };

            var pnlKeyword = new Panel { Width = 660, Height = 28, Margin = new Padding(0, 4, 0, 0) };
            var lblKeyword = new Label { Text = isEn ? "Filter by view keyword (e.g. under, arc, over...):" : "Lọc theo từ khóa tên view (VD: under, arc, over...):", AutoSize = true, Left = 3, Top = 4 };
            _txtKeywordFilter = new TextBox { Left = 330, Top = 1, Width = 180, Font = new Font("Segoe UI", 8.5F) };
            pnlKeyword.Controls.Add(lblKeyword);
            pnlKeyword.Controls.Add(_txtKeywordFilter);

            flpOptions.Controls.Add(_chkAlignModelViews);
            flpOptions.Controls.Add(_chkAlignDrafting);
            flpOptions.Controls.Add(_chkAlignLegends);
            flpOptions.Controls.Add(_chkAlignSchedules);
            flpOptions.Controls.Add(pnlKeyword);

            grpOptions.Controls.Add(flpOptions);
            Controls.Add(grpOptions);

            // 3. Search & Filter Bar
            var pnlFilter = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(15, 6, 15, 4),
                BackColor = KhimUiStyle.FormBg
            };

            var lblSearch = new Label
            {
                Text = isEn ? "🔍 Search Sheets:" : "🔍 Tìm kiếm Sheet:",
                AutoSize = true,
                Left = 15,
                Top = 10,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextSecondary
            };
            _txtSearch = new TextBox
            {
                Left = 145,
                Top = 7,
                Width = 260,
                Font = new Font("Segoe UI", 9F)
            };
            _txtSearch.TextChanged += (s, e) => PopulateSheetList(_txtSearch.Text);

            _btnSelectAll = new Button
            {
                Text = isEn ? "Select All" : "Chọn hết",
                Left = 415,
                Top = 6,
                Width = 80,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg
            };
            _btnSelectAll.Click += (s, e) => SetCheckAll(true);

            _btnClearAll = new Button
            {
                Text = isEn ? "Deselect" : "Bỏ chọn",
                Left = 500,
                Top = 6,
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
                Height = 60,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = KhimUiStyle.CardBg
            };

            _btnExecute = new Button
            {
                Text = isEn ? "Align Elements" : "Căn Chỉnh Vị Trí",
                DialogResult = DialogResult.OK,
                Width = 160,
                Height = 36,
                Left = 520,
                Top = 12,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnExecute.Click += BtnExecute_Click;

            _btnCancel = new Button
            {
                Text = isEn ? "Cancel" : "Hủy",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 36,
                Left = 430,
                Top = 12,
                FlatStyle = FlatStyle.Flat,
                BackColor = KhimUiStyle.SecondaryButtonBg,
                Font = new Font("Segoe UI", 9F)
            };

            pnlBottom.Controls.AddRange(new System.Windows.Forms.Control[] { _btnCancel, _btnExecute });
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
                MessageBox.Show(
                    LanguageManager.IsEnglish ? "Please select at least one target sheet!" : "Vui lòng chọn ít nhất một Sheet mục tiêu để căn chỉnh!",
                    LanguageManager.IsEnglish ? "No Sheet Selected" : "Chưa chọn Sheet",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            AlignOptions = new ViewportAlignOptions
            {
                AlignModelViews = _chkAlignModelViews.Checked,
                AlignDraftingViews = _chkAlignDrafting.Checked,
                AlignLegends = _chkAlignLegends.Checked,
                AlignSchedules = _chkAlignSchedules.Checked,
                KeywordFilter = _txtKeywordFilter.Text.Trim()
            };

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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.UI;
using KhimTools.DetailNumberUpdater.Services;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinFormsControl = System.Windows.Forms.Control;
using DrawingColor = System.Drawing.Color;

namespace KhimTools.DetailNumberUpdater.Forms
{
    public class UpdateDetailNumbersForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly ElementId _sheetId;
        private List<DetailNumberPreviewItem> _previewItems = new List<DetailNumberPreviewItem>();
        private TextBox _txtPattern;
        private Label _lblRegex;
        private Label _lblSummary;
        private DataGridView _grid;
        private Button _btnApply;
        private bool _bindingRows;

        public DetailNumberBatchResult ExecutionResult { get; private set; }

        private ViewSheet CurrentSheet { get { return _sheetId == null ? null : _doc.GetElement(_sheetId) as ViewSheet; } }

        public UpdateDetailNumbersForm(Document doc, ViewSheet sheet)
        {
            _doc = doc;
            _sheetId = sheet == null ? null : sheet.Id;
            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            RefreshData();
        }

        private void BuildUi()
        {
            Text = "K-TOOLS — Detail Number 2.0";
            Width = 980;
            Height = 660;
            MinimumSize = new Size(820, 520);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            var config = new WinFormsPanel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(14), BackColor = KhimUiStyle.CardBg };
            ViewSheet sheet = CurrentSheet;
            var lblSheet = new Label { AutoSize = true, Left = 14, Top = 10, Text = sheet == null ? "Sheet: <missing>" : "Sheet: " + sheet.SheetNumber + " — " + sheet.Name, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = KhimUiStyle.TextPrimary };
            var lblPattern = new Label { Text = "Regex pattern", AutoSize = true, Left = 14, Top = 39 };
            _txtPattern = new TextBox { Text = DetailNumberService.DefaultPattern, Left = 14, Top = 61, Width = 690, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Consolas", 9F) };
            _txtPattern.TextChanged += (sender, args) => RefreshData();
            var btnReset = new Button { Text = "Reset", Left = 714, Top = 59, Width = 90, Height = 26, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnReset.Click += (sender, args) => { _txtPattern.Text = DetailNumberService.DefaultPattern; };
            var btnRefresh = new Button { Text = "Refresh", Left = 812, Top = 59, Width = 90, Height = 26, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnRefresh.Click += (sender, args) => RefreshData();
            _lblRegex = new Label { AutoSize = true, Left = 14, Top = 89, ForeColor = DrawingColor.DarkGreen };
            config.Controls.AddRange(new WinFormsControl[] { lblSheet, lblPattern, _txtPattern, btnReset, btnRefresh, _lblRegex });
            Controls.Add(config);

            var toolbar = new WinFormsPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(14, 8, 14, 4), BackColor = KhimUiStyle.FormBg };
            var btnAll = new Button { Text = "Select all", Left = 14, Top = 7, Width = 92, Height = 28 };
            btnAll.Click += (sender, args) => SetSelection(true);
            var btnNone = new Button { Text = "Clear all", Left = 112, Top = 7, Width = 92, Height = 28 };
            btnNone.Click += (sender, args) => SetSelection(false);
            _lblSummary = new Label { AutoSize = true, Left = 220, Top = 13, ForeColor = KhimUiStyle.TextSecondary };
            toolbar.Controls.AddRange(new WinFormsControl[] { btnAll, btnNone, _lblSummary });
            Controls.Add(toolbar);

            var gridPanel = new WinFormsPanel { Dock = DockStyle.Fill, Padding = new Padding(14, 4, 14, 4), BackColor = KhimUiStyle.FormBg };
            _grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = DrawingColor.White, BorderStyle = BorderStyle.FixedSingle };
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Apply", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", ReadOnly = true, FillWeight = 24 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "View", ReadOnly = true, FillWeight = 45 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Current", ReadOnly = true, FillWeight = 25 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Proposed", FillWeight = 25 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", ReadOnly = true, FillWeight = 55 });
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CellEndEdit += Grid_CellEndEdit;
            gridPanel.Controls.Add(_grid);
            Controls.Add(gridPanel);

            var bottom = new WinFormsPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(14, 10, 14, 10), BackColor = KhimUiStyle.CardBg };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 34, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnApply = new Button { Text = "Apply", Width = 120, Height = 34, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = KhimUiStyle.PrimaryButtonBg, ForeColor = DrawingColor.White };
            _btnApply.Click += BtnApply_Click;
            bottom.Controls.AddRange(new WinFormsControl[] { btnCancel, _btnApply });
            bottom.Resize += (sender, args) => { _btnApply.Left = bottom.Width - _btnApply.Width - 14; btnCancel.Left = _btnApply.Left - btnCancel.Width - 10; };
            Controls.Add(bottom);
            CancelButton = btnCancel;
        }

        private void RefreshData()
        {
            ViewSheet sheet = CurrentSheet;
            _previewItems = sheet == null ? new List<DetailNumberPreviewItem>() : DetailNumberService.GeneratePreview(_doc, sheet, _txtPattern.Text);
            _bindingRows = true;
            try
            {
                _grid.Rows.Clear();
                foreach (DetailNumberPreviewItem item in _previewItems)
                {
                    int row = _grid.Rows.Add(item.IsSelected, item.Status.ToString(), item.ViewName, item.CurrentNumber, item.ProposedNumber, item.Message);
                    StyleRow(_grid.Rows[row], item);
                }
            }
            finally { _bindingRows = false; }
            UpdateSummary();
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_bindingRows || e.RowIndex < 0 || e.RowIndex >= _previewItems.Count || e.ColumnIndex != 0) return;
            _previewItems[e.RowIndex].IsSelected = Convert.ToBoolean(_grid.Rows[e.RowIndex].Cells[0].Value);
            UpdateSummary();
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_bindingRows || e.RowIndex < 0 || e.RowIndex >= _previewItems.Count || e.ColumnIndex != 4) return;
            DetailNumberPreviewItem item = _previewItems[e.RowIndex];
            item.ProposedNumber = DetailNumberConflictResolver.Normalize(Convert.ToString(_grid.Rows[e.RowIndex].Cells[4].Value));
            item.IsManualOverride = true;
            item.IsSelected = true;
            UpdateSummary();
        }

        private void SetSelection(bool selected)
        {
            foreach (DetailNumberPreviewItem item in _previewItems) item.IsSelected = selected;
            _bindingRows = true;
            try { for (int i = 0; i < _grid.Rows.Count; i++) _grid.Rows[i].Cells[0].Value = selected; }
            finally { _bindingRows = false; }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            RegexValidationResult validation = DetailNumberService.ValidateRegex(_txtPattern.Text);
            _lblRegex.Text = validation.IsValid ? "Regex valid" : "INVALID_REGEX: " + validation.ErrorMessage;
            _lblRegex.ForeColor = validation.IsValid ? DrawingColor.DarkGreen : DrawingColor.DarkRed;
            ViewSheet sheet = CurrentSheet;
            DetailNumberPreflightReport report = sheet == null ? new DetailNumberPreflightReport { Regex = validation, Summary = new DetailNumberPreflightSummary(), Candidates = _previewItems.Cast<DetailNumberCandidate>().ToList() } : DetailNumberPreflightService.Preflight(_doc, sheet, _previewItems, _txtPattern.Text);
            _lblSummary.Text = string.Format("Requested {0} | Ready {1} | No change {2} | Skipped {3}", report.Summary.Requested, report.Summary.Ready, report.Summary.NoChange, report.Summary.Skipped);
            _btnApply.Enabled = report.CanApply;
            _bindingRows = true;
            try
            {
                for (int i = 0; i < _previewItems.Count && i < _grid.Rows.Count; i++)
                {
                    DetailNumberPreviewItem item = _previewItems[i];
                    _grid.Rows[i].Cells[1].Value = item.Status.ToString();
                    _grid.Rows[i].Cells[5].Value = item.Message;
                    StyleRow(_grid.Rows[i], item);
                }
            }
            finally { _bindingRows = false; }
        }

        private void StyleRow(DataGridViewRow row, DetailNumberPreviewItem item)
        {
            row.Cells[1].Style.ForeColor = item.CanExecute || item.Status == DetailNumberStatusCode.READY ? DrawingColor.DarkGreen : item.Status == DetailNumberStatusCode.NO_CHANGE ? DrawingColor.DarkBlue : DrawingColor.DarkRed;
            row.Cells[4].Style.ForeColor = item.CanExecute ? DrawingColor.DarkGreen : DrawingColor.DimGray;
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < _previewItems.Count && i < _grid.Rows.Count; i++)
            {
                DetailNumberPreviewItem item = _previewItems[i];
                item.IsSelected = Convert.ToBoolean(_grid.Rows[i].Cells[0].Value);
                string proposed = DetailNumberConflictResolver.Normalize(Convert.ToString(_grid.Rows[i].Cells[4].Value));
                if (!string.Equals(proposed, item.ProposedNumber, StringComparison.Ordinal)) item.IsManualOverride = true;
                item.ProposedNumber = proposed;
            }
            ViewSheet sheet = CurrentSheet;
            DetailNumberPreflightReport preflight = DetailNumberPreflightService.Preflight(_doc, sheet, _previewItems, _txtPattern.Text);
            if (!preflight.CanApply)
            {
                MessageBox.Show("No valid changes are ready to apply.", "K-TOOLS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateSummary();
                return;
            }
            ExecutionResult = DetailNumberService.Execute(_doc, sheet, _previewItems, _txtPattern.Text);
            MessageBox.Show(string.Format("Changed: {0}\nAlready correct: {1}\nSkipped: {2}\nFailed: {3}", ExecutionResult.Changed, ExecutionResult.AlreadyCorrect, ExecutionResult.Skipped, ExecutionResult.FailedCount), "K-TOOLS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

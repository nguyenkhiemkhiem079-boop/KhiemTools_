using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core.UI;
using KhimTools.Core;
using KhimTools.SheetCopy.Models;
using KhimTools.SheetCopy.Services;

namespace KhimTools.SheetCopy.Forms
{
    public sealed class SheetCopyForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly List<SheetCopyItem> _items;
        private readonly CheckedListBox _sources = new CheckedListBox();
        private readonly DataGridView _preview = new DataGridView();
        private readonly ComboBox _mode = new ComboBox();
        private readonly CheckBox _reuseLegends = new CheckBox();
        private readonly CheckBox _reuseSchedules = new CheckBox();
        private readonly Label _status = new Label();
        private readonly Button _create = new Button();

        public SheetCopyRequest Request { get; private set; }

        public SheetCopyForm(Document doc) : this(doc, true) { }
        private SheetCopyForm(Document doc, bool loadDocument)
        {
            _doc = doc;
            _items = loadDocument ? SheetCopyCollector.CollectSheets(doc, doc == null ? null : doc.ActiveView as ViewSheet) : new List<SheetCopyItem>();
            KhimUiStyle.ApplyFormTheme(this);
            BuildLayout();
            if (loadDocument) LoadRows();
        }

        internal static SheetCopyForm CreateLayoutPreview() => new SheetCopyForm(null, false);

        private void BuildLayout()
        {
            Text = "K-TOOLS - Sheet Copy 2.0";
            Width = 1180; Height = 720; MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9F);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            _sources.Dock = DockStyle.Fill; _sources.CheckOnClick = true; _sources.DisplayMember = "DisplayLabel";
            root.Controls.Add(_sources, 0, 0);
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(6), WrapContents = false };
            options.Controls.Add(new Label { Text = "Mode", AutoSize = true, Margin = new Padding(4, 8, 4, 4) });
            _mode.DropDownStyle = ComboBoxStyle.DropDownList; _mode.Width = 220;
            _mode.Items.AddRange(new object[] { "Sheet only", "Views", "Views + Detailing", "Dependent Views" }); _mode.SelectedIndex = 1; options.Controls.Add(_mode);
            _reuseLegends.Text = "Reuse legends"; _reuseLegends.Checked = true; _reuseLegends.AutoSize = true; options.Controls.Add(_reuseLegends);
            _reuseSchedules.Text = "Reuse schedules"; _reuseSchedules.Checked = true; _reuseSchedules.AutoSize = true; options.Controls.Add(_reuseSchedules);
            main.Controls.Add(options, 0, 0);
            ConfigurePreview(); main.Controls.Add(_preview, 0, 1); root.Controls.Add(main, 1, 0);
            _status.Dock = DockStyle.Fill; _status.Text = "Select source Sheets and edit target identity in Preview."; _status.AutoSize = true;
            root.Controls.Add(_status, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _create.Text = "Create Copies"; _create.Width = 130; _create.Click += CreateClicked; buttons.Controls.Add(_create);
            var refresh = new Button { Text = "Refresh", Width = 90 }; refresh.Click += (s, e) => { LoadRows(); }; buttons.Controls.Add(refresh);
            var close = new Button { Text = "Close", Width = 90, DialogResult = DialogResult.Cancel }; buttons.Controls.Add(close);
            root.Controls.Add(buttons, 1, 1); Controls.Add(root); AcceptButton = _create; CancelButton = close;
        }

        private void ConfigurePreview()
        {
            _preview.Dock = DockStyle.Fill; _preview.AllowUserToAddRows = false; _preview.AutoGenerateColumns = false; _preview.RowHeadersVisible = false;
            _preview.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "✓", DataPropertyName = "IsSelected", Width = 35 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source Sheet", DataPropertyName = "DisplayLabel", ReadOnly = true, Width = 220 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target Number", DataPropertyName = "TargetSheetNumber", Width = 130 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target Name", DataPropertyName = "TargetSheetName", Width = 220 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Views", DataPropertyName = "ViewportCount", ReadOnly = true, Width = 55 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Legends", DataPropertyName = "LegendCount", ReadOnly = true, Width = 60 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Schedules", DataPropertyName = "ScheduleCount", ReadOnly = true, Width = 70 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Annotations", DataPropertyName = "AnnotationCount", ReadOnly = true, Width = 78 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", ReadOnly = true, Width = 110 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", DataPropertyName = "Message", ReadOnly = true, Width = 240 });
        }

        private void LoadRows()
        {
            _sources.Items.Clear(); _preview.Rows.Clear();
            foreach (SheetCopyItem item in _items)
            {
                int index = _sources.Items.Add(item, item.IsSelected);
                if (item.IsSelected && (_doc == null || (_doc.ActiveView is ViewSheet active && active.Id == item.SourceSheetId))) _sources.SetItemChecked(index, true);
                int row = _preview.Rows.Add(item.IsSelected, item.DisplayLabel, item.TargetSheetNumber, item.TargetSheetName, item.ViewportCount, item.LegendCount, item.ScheduleCount, item.AnnotationCount);
                _preview.Rows[row].Tag = item;
            }
        }

        private void CreateClicked(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in _preview.Rows)
            {
                if (!(row.Tag is SheetCopyItem item)) continue;
                item.IsSelected = Convert.ToBoolean(row.Cells[0].Value ?? false);
                item.TargetSheetNumber = Convert.ToString(row.Cells[2].Value) ?? string.Empty;
                item.TargetSheetName = Convert.ToString(row.Cells[3].Value) ?? string.Empty;
            }
            foreach (int checkedIndex in _sources.CheckedIndices) if (checkedIndex >= 0 && checkedIndex < _items.Count) _items[checkedIndex].IsSelected = true;
            var options = new SheetCopyOptions { ViewPolicy = (ViewCopyPolicy)_mode.SelectedIndex, ReuseLegends = _reuseLegends.Checked, ReuseSchedules = _reuseSchedules.Checked };
            Request = new SheetCopyRequest { Document = _doc, Options = options };
            Request.Items.AddRange(_items.Where(i => i.IsSelected));
            if (Request.Items.Count == 0) { MessageBox.Show(this, "Select at least one source Sheet.", "Sheet Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

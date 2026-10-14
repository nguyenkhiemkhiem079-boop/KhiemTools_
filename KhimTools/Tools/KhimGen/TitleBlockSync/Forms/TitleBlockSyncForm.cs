using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.UI;
using KhimTools.ParameterTransfer.Models;
using KhimTools.TitleBlockSync.Models;
using KhimTools.TitleBlockSync.Services;

namespace KhimTools.TitleBlockSync.Forms
{
    public sealed class TitleBlockSyncForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly ComboBox _source = new ComboBox();
        private readonly CheckedListBox _targets = new CheckedListBox();
        private readonly CheckedListBox _parameters = new CheckedListBox();
        private readonly CheckBox _type = new CheckBox();
        private readonly CheckBox _instance = new CheckBox();
        private readonly CheckBox _sheet = new CheckBox();
        private readonly CheckBox _overwrite = new CheckBox();
        private readonly CheckBox _partial = new CheckBox();
        private readonly DataGridView _preview = new DataGridView();
        private readonly Label _status = new Label();
        private readonly List<TitleBlockInfo> _sources = new List<TitleBlockInfo>();
        private readonly List<ViewSheet> _sheets = new List<ViewSheet>();
        private TitleBlockInfo _active;
        public TitleBlockSyncRequest Request { get; private set; }

        public TitleBlockSyncForm(Document doc) : this(doc, true) { }
        private TitleBlockSyncForm(Document doc, bool load) { _doc = doc; KhimUiStyle.ApplyFormTheme(this); BuildLayout(); if (load) LoadDocument(); }
        internal static TitleBlockSyncForm CreateLayoutPreview() { return new TitleBlockSyncForm(null, false); }

        private void BuildLayout()
        {
            Text = "K-TOOLS - Title Block Sync 2.0"; Width = 1240; Height = 760; MinimumSize = new Size(980, 600); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9F);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 2, RowCount = 3 }; root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            var sourcePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(4) }; sourcePanel.Controls.Add(new Label { Text = "Source Sheet / Title Block", AutoSize = true, Margin = new Padding(4, 8, 4, 4) }); _source.DropDownStyle = ComboBoxStyle.DropDownList; _source.Width = 280; sourcePanel.Controls.Add(_source); root.Controls.Add(sourcePanel, 0, 0); root.SetColumnSpan(sourcePanel, 2);
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 }; left.RowStyles.Add(new RowStyle(SizeType.Percent, 58)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 42)); left.Controls.Add(Group("Targets (source excluded; placeholders excluded)", _targets), 0, 0); left.Controls.Add(Group("Parameters (safe writable defaults)", _parameters), 0, 1); root.Controls.Add(left, 0, 1);
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 }; right.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); var scopes = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(5) }; _type.Text = "Title Block Type"; _type.Checked = true; _instance.Text = "Instance Parameters"; _instance.Checked = true; _sheet.Text = "Sheet Parameters"; _sheet.Checked = true; _overwrite.Text = "Overwrite blank source"; _partial.Text = "Allow partial target"; scopes.Controls.AddRange(new System.Windows.Forms.Control[] { _type, _instance, _sheet, _overwrite, _partial }); right.Controls.Add(scopes, 0, 0); ConfigurePreview(); right.Controls.Add(_preview, 0, 1); _status.Text = "Select an explicit source and one or more targets, then Preview."; _status.Dock = DockStyle.Fill; right.Controls.Add(_status, 0, 2); root.Controls.Add(right, 1, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var sync = new Button { Text = "Sync", Width = 100 }; sync.Click += SyncClicked; var preview = new Button { Text = "Preview", Width = 100 }; preview.Click += PreviewClicked; var refresh = new Button { Text = "Refresh", Width = 100 }; refresh.Click += (s, e) => LoadDocument(); var close = new Button { Text = "Close", Width = 100, DialogResult = DialogResult.Cancel }; buttons.Controls.AddRange(new System.Windows.Forms.Control[] { sync, preview, refresh, close }); root.Controls.Add(buttons, 0, 2); root.SetColumnSpan(buttons, 2); Controls.Add(root); CancelButton = close;
        }
        private static GroupBox Group(string title, System.Windows.Forms.Control child) { var box = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(5) }; child.Dock = DockStyle.Fill; var checkedList = child as CheckedListBox; if (checkedList != null) checkedList.CheckOnClick = true; box.Controls.Add(child); return box; }
        private void ConfigurePreview() { _preview.Dock = DockStyle.Fill; _preview.AllowUserToAddRows = false; _preview.RowHeadersVisible = false; _preview.ColumnCount = 5; _preview.Columns[0].HeaderText = "Scope"; _preview.Columns[1].HeaderText = "Parameter"; _preview.Columns[2].HeaderText = "Old"; _preview.Columns[3].HeaderText = "New"; _preview.Columns[4].HeaderText = "Status"; }
        private void LoadDocument()
        {
            _source.Items.Clear(); _sources.Clear(); _targets.Items.Clear(); _sheets.Clear(); _parameters.Items.Clear(); _preview.Rows.Clear(); if (_doc == null) return;
            _source.SelectedIndex = -1; _active = TitleBlockCollector.FindActiveIfUnambiguous(_doc);
            foreach (ViewSheet sheet in TitleBlockCollector.CollectSheets(_doc))
            {
                _sheets.Add(sheet); var blocks = TitleBlockCollector.CollectTitleBlocks(_doc, sheet); if (blocks.Count == 1) { TitleBlockInfo info = TitleBlockCollector.Analyze(_doc, sheet, blocks[0]); _sources.Add(info); _source.Items.Add(sheet.SheetNumber + " - " + sheet.Name + " | " + info.FamilyName + ":" + info.TypeName); }
            }
            if (_active != null) foreach (int i in Enumerable.Range(0, _sources.Count)) if (_sources[i].Sheet.Id == _active.Sheet.Id) { _source.SelectedIndex = i; break; }
            ExcludeSelectedSourceFromTargets();
            foreach (ViewSheet sheet in _sheets)
            {
                if (_active != null && sheet.Id == _active.Sheet.Id) continue; var blocks = TitleBlockCollector.CollectTitleBlocks(_doc, sheet); if (blocks.Count != 1) continue; int i = _targets.Items.Add(sheet.SheetNumber + " - " + sheet.Name, true); _targets.Items[i] = new TitleBlockSyncTarget { SheetId = sheet.Id, SheetUniqueId = sheet.UniqueId, SheetNumber = sheet.SheetNumber, SheetName = sheet.Name, CurrentFamilyName = blocks[0].Symbol == null ? string.Empty : blocks[0].Symbol.FamilyName, CurrentTypeName = blocks[0].Symbol == null ? string.Empty : blocks[0].Symbol.Name };
            }
            if (_source.SelectedIndex >= 0) LoadParameters(_sources[_source.SelectedIndex]);
            _source.SelectedIndexChanged += (s, e) => { if (_source.SelectedIndex >= 0) { LoadParameters(_sources[_source.SelectedIndex]); ExcludeSelectedSourceFromTargets(); } };
        }
        private void ExcludeSelectedSourceFromTargets()
        {
            if (_source.SelectedIndex < 0 || _source.SelectedIndex >= _sources.Count) return;
            ElementId sourceId = _sources[_source.SelectedIndex].Sheet.Id;
            for (int i = 0; i < _targets.Items.Count; i++) if (_targets.Items[i] is TitleBlockSyncTarget target && target.SheetId == sourceId) _targets.SetItemChecked(i, false);
        }
        private void LoadParameters(TitleBlockInfo source)
        {
            _parameters.Items.Clear(); if (source == null) return; foreach (ParameterTransferPlan p in TitleBlockParameterService.SelectDefaults(source.TitleBlock, false)) _parameters.Items.Add(p, true); foreach (ParameterTransferPlan p in TitleBlockParameterService.SelectDefaults(source.Sheet, true)) _parameters.Items.Add(p, true);
        }
        private TitleBlockSyncRequest BuildRequest()
        {
            if (_source.SelectedIndex < 0 || _source.SelectedIndex >= _sources.Count) return null; TitleBlockInfo source = _sources[_source.SelectedIndex]; var request = new TitleBlockSyncRequest { Document = _doc, SourceSheetId = source.Sheet.Id, SourceTitleBlockId = source.TitleBlock.Id, Options = new TitleBlockSyncOptions { SyncTitleBlockType = _type.Checked, SyncInstanceParameters = _instance.Checked, SyncSheetParameters = _sheet.Checked, OverwriteBlankSource = _overwrite.Checked, AllowPartialTarget = _partial.Checked } };
            foreach (int i in _targets.CheckedIndices) if (_targets.Items[i] is TitleBlockSyncTarget target && target.SheetId != source.Sheet.Id) request.Targets.Add(target);
            foreach (object item in _parameters.CheckedItems) if (item is ParameterTransferPlan p) request.SelectedParameterKeys.Add(p.Key);
            return request;
        }
        private void PreviewClicked(object sender, EventArgs e) { Request = BuildRequest(); if (Request == null) { _status.Text = "Choose an explicit source title block; no sheet is auto-selected."; return; } TitleBlockSyncPlan plan = TitleBlockSyncPlanner.BuildPlan(_doc, Request); _preview.Rows.Clear(); foreach (ParameterSyncItem item in plan.Parameters.Where(p => p.Selected)) foreach (TitleBlockTargetPlan t in plan.Targets) { var items = item.Scope == TitleBlockSyncScope.SHEET ? t.SheetParameters : t.InstanceParameters; foreach (ParameterSyncTargetPlan x in items) _preview.Rows.Add(item.Scope, item.TransferPlan.ParameterName, x.OldValue == null ? "" : x.OldValue.DisplayValue, x.NewValue == null ? "" : x.NewValue.DisplayValue, x.Status); } _status.Text = plan.Status + ": " + plan.Targets.Count + " target(s), " + plan.Parameters.Count + " parameter(s)."; }
        private void SyncClicked(object sender, EventArgs e) { Request = BuildRequest(); if (Request == null || Request.Targets.Count == 0) { _status.Text = "Select an explicit source and at least one target."; return; } DialogResult = DialogResult.OK; Close(); }
    }
}

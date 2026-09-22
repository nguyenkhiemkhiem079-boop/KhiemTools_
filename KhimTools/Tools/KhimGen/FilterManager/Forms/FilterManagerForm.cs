using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.UI;
using KhimTools.FilterManager.Models;
using KhimTools.FilterManager.Services;
using RevitView = Autodesk.Revit.DB.View;
using WinControl = System.Windows.Forms.Control;

namespace KhimTools.FilterManager.Forms
{
    public sealed class FilterManagerForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly ElementId _activeViewId;
        private readonly ComboBox _source = new ComboBox();
        private readonly ComboBox _mode = new ComboBox();
        private readonly CheckedListBox _targets = new CheckedListBox();
        private readonly CheckedListBox _filters = new CheckedListBox();
        private readonly CheckBox _visibility = new CheckBox();
        private readonly CheckBox _enabled = new CheckBox();
        private readonly CheckBox _graphics = new CheckBox();
        private readonly CheckBox _order = new CheckBox();
        private readonly CheckBox _addMissing = new CheckBox();
        private readonly CheckBox _clearOverrides = new CheckBox();
        private readonly DataGridView _preview = new DataGridView();
        private readonly Label _status = new Label();
        private readonly List<RevitView> _views = new List<RevitView>();
        private readonly List<FilterDefinitionInfo> _definitions = new List<FilterDefinitionInfo>();
        public FilterCopyRequest Request { get; private set; }

        public FilterManagerForm(Document doc, ElementId activeViewId) : this(doc, activeViewId, true) { }
        private FilterManagerForm(Document doc, ElementId activeViewId, bool load)
        {
            _doc = doc; _activeViewId = activeViewId ?? ElementId.InvalidElementId; KhimUiStyle.ApplyFormTheme(this); BuildLayout(); if (load) LoadDocument();
        }
        internal static FilterManagerForm CreateLayoutPreview() { return new FilterManagerForm(null, ElementId.InvalidElementId, false); }

        private void BuildLayout()
        {
            Text = "K-TOOLS - Filter Manager 2.0"; Width = 1280; Height = 780; MinimumSize = new Size(980, 620); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9F);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 2, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            var sourcePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(4) }; sourcePanel.Controls.Add(new Label { Text = "Explicit source View / Template", AutoSize = true, Margin = new Padding(4, 8, 8, 4) }); _source.DropDownStyle = ComboBoxStyle.DropDownList; _source.Width = 500; sourcePanel.Controls.Add(_source); root.Controls.Add(sourcePanel, 0, 0); root.SetColumnSpan(sourcePanel, 2);
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 }; left.RowStyles.Add(new RowStyle(SizeType.Percent, 42)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 8)); left.Controls.Add(Group("Target Views / Templates (explicit selection)", _targets), 0, 0); left.Controls.Add(Group("Applied filter definitions", _filters), 0, 1); root.Controls.Add(left, 0, 1);
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 }; right.RowStyles.Add(new RowStyle(SizeType.Absolute, 105)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(5), WrapContents = true };
            _mode.DropDownStyle = ComboBoxStyle.DropDownList; _mode.Items.Add(FilterCopyMode.MERGE_SELECTED_FILTERS); _mode.Items.Add(FilterCopyMode.EXACT_SYNC); _mode.SelectedIndex = 0; _mode.Width = 170; options.Controls.Add(new Label { Text = "Mode", AutoSize = true, Margin = new Padding(4, 8, 3, 4) }); options.Controls.Add(_mode);
            _visibility.Text = "Copy Visibility"; _visibility.Checked = true; _enabled.Text = "Copy Enabled State"; _enabled.Checked = true; _graphics.Text = "Copy Graphic Overrides"; _graphics.Checked = true; _order.Text = "Copy Filter Order"; _order.Checked = true; _addMissing.Text = "Add Missing Filters"; _addMissing.Checked = true; _clearOverrides.Text = "Clear Overrides"; options.Controls.AddRange(new WinControl[] { _visibility, _enabled, _graphics, _order, _addMissing, _clearOverrides }); options.Controls.Add(new Label { Text = "Manage: Add Existing Filter | Remove from View | Toggle Visibility | Toggle Enabled | Copy/Paste Graphic Overrides | Reset selected state", AutoSize = true, Margin = new Padding(4, 8, 4, 4) }); right.Controls.Add(options, 0, 0); ConfigurePreview(); right.Controls.Add(_preview, 0, 1); _status.Text = "Choose an explicit source and targets, then Preview."; _status.Dock = DockStyle.Fill; right.Controls.Add(_status, 0, 2); root.Controls.Add(right, 1, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var apply = new Button { Text = "Apply", Width = 100 }; apply.Click += ApplyClicked; var preview = new Button { Text = "Preview", Width = 100 }; preview.Click += PreviewClicked; var refresh = new Button { Text = "Refresh", Width = 100 }; refresh.Click += (s, e) => LoadDocument(); var close = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel }; buttons.Controls.AddRange(new WinControl[] { apply, preview, refresh, close }); root.Controls.Add(buttons, 0, 2); root.SetColumnSpan(buttons, 2); Controls.Add(root); CancelButton = close;
        }

        private static GroupBox Group(string title, WinControl child) { var box = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(5) }; child.Dock = DockStyle.Fill; if (child is CheckedListBox list) list.CheckOnClick = true; box.Controls.Add(child); return box; }
        private void ConfigurePreview() { _preview.Dock = DockStyle.Fill; _preview.AllowUserToAddRows = false; _preview.RowHeadersVisible = false; _preview.ColumnCount = 7; _preview.Columns[0].HeaderText = "TARGET"; _preview.Columns[1].HeaderText = "FILTER"; _preview.Columns[2].HeaderText = "ACTION"; _preview.Columns[3].HeaderText = "CURRENT"; _preview.Columns[4].HeaderText = "PROPOSED"; _preview.Columns[5].HeaderText = "STATUS"; _preview.Columns[6].HeaderText = "Definition"; }
        private void LoadDocument()
        {
            _source.Items.Clear(); _targets.Items.Clear(); _filters.Items.Clear(); _views.Clear(); _definitions.Clear(); _preview.Rows.Clear(); Request = null; _source.SelectedIndex = -1; if (_doc == null) return;
            _views.AddRange(FilterCollectorService.CollectCompatibleViews(_doc)); foreach (RevitView view in _views) { int index = _source.Items.Add(new ViewChoice(view)); int targetIndex = _targets.Items.Add(new ViewChoice(view), false); if (view.Id == _activeViewId) _source.SelectedIndex = index; if (view.Id == _activeViewId) _targets.SetItemChecked(targetIndex, false); }
            _definitions.AddRange(FilterCollectorService.CollectFilterDefinitions(_doc));
            if (_source.SelectedIndex >= 0) LoadFilters();
            _source.SelectedIndexChanged += (s, e) => LoadFilters();
        }
        private void LoadFilters()
        {
            _filters.Items.Clear(); RevitView view = SelectedSource(); if (view == null) return; foreach (AppliedFilterState state in FilterStateService.Capture(view)) { FilterDefinitionInfo info = FilterCollectorService.Describe(_doc, state.FilterId); if (info != null) _filters.Items.Add(info, true); }
        }
        private RevitView SelectedSource() { return _source.SelectedIndex < 0 || _source.SelectedIndex >= _source.Items.Count ? null : (_source.Items[_source.SelectedIndex] as ViewChoice)?.View; }
        private FilterCopyRequest BuildRequest()
        {
            RevitView source = SelectedSource(); if (source == null) return null; var request = new FilterCopyRequest { Document = _doc, SourceViewId = source.Id, Options = new FilterSyncOptions { Mode = _mode.SelectedIndex == 1 ? FilterCopyMode.EXACT_SYNC : FilterCopyMode.MERGE_SELECTED_FILTERS, CopyVisibility = _visibility.Checked, CopyEnabled = _enabled.Checked, CopyGraphicOverrides = _graphics.Checked, CopyOrder = _order.Checked, AddMissingFilters = _addMissing.Checked, ClearOverrides = _clearOverrides.Checked } };
            foreach (int i in _targets.CheckedIndices) { ViewChoice choice = _targets.Items[i] as ViewChoice; if (choice != null && choice.View.Id != source.Id) request.TargetViewIds.Add(choice.View.Id); }
            foreach (object item in _filters.CheckedItems) if (item is FilterDefinitionInfo info) request.FilterIds.Add(info.Id);
            return request;
        }
        private void PreviewClicked(object sender, EventArgs e)
        {
            Request = BuildRequest(); if (Request == null) { _status.Text = "Select an explicit source; no View is auto-selected."; return; }
            FilterCopyPlan plan = FilterCopyPlanner.BuildPlan(_doc, Request); _preview.Rows.Clear();
            foreach (FilterTargetPlan target in plan.Targets)
            {
                foreach (AppliedFilterState filter in target.Filters) _preview.Rows.Add(target.TargetViewName, filter.Name, target.Actions.Count == 0 ? "NO_CHANGE" : target.Actions[0], "Current filter state", "Source state", target.Status, filter.DefinitionType);
                foreach (ElementId id in target.RemoveFilterIds) _preview.Rows.Add(target.TargetViewName, id, "REMOVE_FROM_TARGET", "Applied", "Not applied", "EXACT_SYNC_REMOVE_REQUIRED", "Existing");
            }
            _status.Text = plan.Status + ": " + plan.Targets.Count + " target(s), " + plan.SourceFilters.Count + " filter(s).";
        }
        private void ApplyClicked(object sender, EventArgs e)
        {
            Request = BuildRequest(); if (Request == null || Request.TargetViewIds.Count == 0 || Request.FilterIds.Count == 0) { _status.Text = "Choose an explicit source, at least one target and one applied filter."; return; } if (Request.Options.Mode == FilterCopyMode.EXACT_SYNC && MessageBox.Show("EXACT_SYNC will remove extra filters from target Views. Confirm Filters to Add / Update / Remove From Target.", "Confirm Exact Sync", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; DialogResult = DialogResult.OK; Close();
        }
        private sealed class ViewChoice
        {
            public RevitView View { get; private set; }
            public ViewChoice(RevitView view) { View = view; }
            public override string ToString() { return (View.IsTemplate ? "[Template] " : string.Empty) + View.Name + " (" + View.ViewType + ")"; }
        }
    }
}

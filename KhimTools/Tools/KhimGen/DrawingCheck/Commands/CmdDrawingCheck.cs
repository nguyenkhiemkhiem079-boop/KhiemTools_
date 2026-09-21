using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core.UI;
using Form = System.Windows.Forms.Form;
using Button = System.Windows.Forms.Button;
using ComboBox = System.Windows.Forms.ComboBox;
using Control = System.Windows.Forms.Control;
using Color = System.Drawing.Color;
using View = Autodesk.Revit.DB.View;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace KhimTools.DrawingCheck
{
    internal sealed class DrawingIssue
    {
        public string Sheet { get; set; }
        public string View { get; set; }
        public string Issue { get; set; }
        public string Element { get; set; }
        public string Id { get; set; }
        public string Detail { get; set; }
        public ElementId ViewId { get; set; }
        public ElementId ElementId { get; set; }
    }

    internal static class DrawingCheckService
    {
        internal static List<DrawingIssue> Scan(Document doc, View active, int scope)
        {
            var views = new Dictionary<ElementId, List<string>>();
            if (scope == 0 && !(active is ViewSheet)) views[active.Id] = new List<string>();
            else
            {
                var sheets = scope == 2
                    ? new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => !s.IsPlaceholder)
                    : new[] { active as ViewSheet }.Where(s => s != null);
                foreach (var sheet in sheets)
                    foreach (var id in sheet.GetAllPlacedViews())
                    {
                        if (!views.ContainsKey(id)) views[id] = new List<string>();
                        views[id].Add(sheet.SheetNumber);
                    }
            }
            var issues = new List<DrawingIssue>();
            foreach (var pair in views)
            {
                var view = doc.GetElement(pair.Key) as View;
                if (view == null || view.IsTemplate || !FilteredElementCollector.IsViewValidForElementIteration(doc, view.Id)) continue;
                var sheetName = string.Join(", ", pair.Value.Distinct());
                var tagged = new HashSet<ElementId>();
                var hostTags = new Dictionary<string, HashSet<IndependentTag>>();
                var tags = new FilteredElementCollector(doc, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>();
                foreach (var tag in tags)
                {
                    bool targetCategory = tag.Category != null &&
                        (tag.Category.Id.Equals(new ElementId(BuiltInCategory.OST_FloorTags)) ||
                         tag.Category.Id.Equals(new ElementId(BuiltInCategory.OST_WallTags)));
                    bool broken = tag.IsOrphaned;
                    bool unloaded = false;
                    bool hasTarget = targetCategory;
                    var hostKeys = new HashSet<string>();
                    var hiddenHosts = new HashSet<ElementId>();
                    foreach (var reference in tag.GetTaggedElementIds())
                    {
                        Element host;
                        if (reference.LinkInstanceId != ElementId.InvalidElementId)
                        {
                            var link = doc.GetElement(reference.LinkInstanceId) as RevitLinkInstance;
                            var linkedDoc = link?.GetLinkDocument();
                            if (link != null && linkedDoc == null) { unloaded = true; continue; }
                            host = linkedDoc?.GetElement(reference.LinkedElementId);
                        }
                        else
                        {
                            host = doc.GetElement(reference.HostElementId);
                            if (host is Floor || host is Wall) tagged.Add(host.Id);
                        }
                        if (host is Floor || host is Wall)
                        {
                            hasTarget = true;
                            hostKeys.Add(reference.LinkInstanceId + ":" + host.Id + ":" + tag.GetTypeId());
                            if (reference.LinkInstanceId == ElementId.InvalidElementId && host.IsHidden(view))
                                hiddenHosts.Add(host.Id);
                        }
                        if (host == null) broken = true;
                    }
                    if (hasTarget && unloaded)
                        issues.Add(Create(view, sheetName, tag, "Link chưa tải", "Không thể xác nhận host khi Revit link chưa tải; chưa kết luận mất host."));
                    else if (hasTarget && broken)
                        issues.Add(Create(view, sheetName, tag, "Tag mất host", "Tag sàn/tường mất liên kết. Kiểm tra và gán lại host hoặc tạo lại tag."));
                    else if (hasTarget)
                    {
                        if (HasMissingTagContent(tag.TagText))
                            issues.Add(Create(view, sheetName, tag, "Tag thiếu nội dung", "Host còn tồn tại nhưng tag trống hoặc chỉ hiện ?. Kiểm tra giá trị tham số và label của family tag."));
                        if (hiddenHosts.Count > 0)
                            issues.Add(Create(view, sheetName, tag, "Host bị ẩn", "Host bị Hide in View (theo phần tử). Host ID: " + string.Join(", ", hiddenHosts) + ". Không tự bỏ ẩn."));
                        foreach (var key in hostKeys)
                        {
                            if (!hostTags.ContainsKey(key)) hostTags[key] = new HashSet<IndependentTag>();
                            hostTags[key].Add(tag);
                        }
                    }
                }
                var reportedDuplicates = new HashSet<ElementId>();
                foreach (var group in hostTags.Values.Where(g => g.Count > 1))
                    foreach (var tag in group)
                        if (reportedDuplicates.Add(tag.Id))
                            issues.Add(Create(view, sheetName, tag, "Tag trùng (rà soát)", "Có nhiều tag cùng loại trên cùng host trong view. Tag ID: " + string.Join(", ", group.Select(t => t.Id)) + ". Có thể là chủ ý trình bày; không tự xóa."));
                foreach (var host in new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType()
                    .Where(e => e is Floor || e is Wall))
                {
                    if (!tagged.Contains(host.Id) && !host.IsHidden(view))
                        issues.Add(Create(view, sheetName, host, "Thiếu tag", "Chưa có tag tham chiếu phần tử trong view này."));
                }
            }
            return issues;
        }

        internal static bool HasMissingTagContent(string text)
        {
            return string.IsNullOrWhiteSpace(text) || text.All(c => c == '?' || char.IsWhiteSpace(c));
        }

        private static DrawingIssue Create(View view, string sheet, Element element, string issue, string detail)
        {
            return new DrawingIssue { Sheet = sheet, View = view.Name, Issue = issue,
                Element = element.Category?.Name + " · " + element.Name, Id = element.Id.ToString(),
                Detail = detail, ViewId = view.Id, ElementId = element.Id };
        }
    }

    internal sealed class DrawingCheckForm : KTBaseForm
    {
        private readonly UIDocument _uidoc;
        private readonly ComboBox _scope;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private readonly Button _locate;
        internal DrawingIssue SelectedIssue { get; private set; }

        internal DrawingCheckForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            Text = "Drawing Check — Kiểm tra tag sàn / tường";
            Size = new Size(1100, 650);
            MinimumSize = new Size(850, 500);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            _scope = new ComboBox { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
            _scope.Items.AddRange(new object[] { "View hiện tại", "Sheet hiện tại", "Tất cả sheet" });
            _scope.SelectedIndex = uidoc?.ActiveView is ViewSheet ? 1 : 0;
            var scan = new Button { Text = "Kiểm tra", Width = 100, Height = 30, Enabled = uidoc != null };
            scan.Click += (s, e) => RunScan();
            toolbar.Controls.AddRange(new Control[] { _scope, scan });
            _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White };
            foreach (var column in new[] { new[] { "Sheet", "Sheet" }, new[] { "View", "View" },
                new[] { "Issue", "Lỗi" }, new[] { "Element", "Phần tử" }, new[] { "Id", "Element ID" }, new[] { "Detail", "Chi tiết" } })
                _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = column[0], HeaderText = column[1] });
            _grid.Columns[5].FillWeight = 220;
            _grid.Columns[4].FillWeight = 60;
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Text = "Chọn phạm vi rồi bấm Kiểm tra. Chỉ kiểm tra, không thay đổi mô hình." };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            _locate = new Button { Text = "Mở view & chọn", Width = 150, Height = 32, Enabled = false };
            _locate.Click += (s, e) => Locate();
            _grid.SelectionChanged += (s, e) => _locate.Enabled = _grid.CurrentRow?.DataBoundItem is DrawingIssue;
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Locate(); };
            actions.Controls.Add(_locate);
            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(_grid, 0, 1);
            root.Controls.Add(_status, 0, 2);
            root.Controls.Add(actions, 0, 3);
            Controls.Add(root);
        }

        private void RunScan()
        {
            if (_scope.SelectedIndex == 0 && !(_uidoc.ActiveView is ViewSheet) &&
                !FilteredElementCollector.IsViewValidForElementIteration(_uidoc.Document, _uidoc.ActiveView.Id))
            { _status.Text = "Chọn view bản vẽ đồ họa để kiểm tra."; return; }
            if (_scope.SelectedIndex == 1 && !(_uidoc.ActiveView is ViewSheet))
            { _status.Text = "Mở một sheet để kiểm tra sheet hiện tại."; return; }
            if (_scope.SelectedIndex == 0 && _uidoc.ActiveView.IsTemplate)
            { _status.Text = "Chọn view bản vẽ, không chọn view template."; return; }
            try
            {
                UseWaitCursor = true;
                var issues = DrawingCheckService.Scan(_uidoc.Document, _uidoc.ActiveView, _scope.SelectedIndex);
                _grid.DataSource = issues;
                _status.Text = issues.Count == 0 ? "Không phát hiện vấn đề trong phạm vi kiểm tra. Kiểm tra lại phần tử sát biên crop."
                    : string.Join(" · ", issues.GroupBy(i => i.Issue).Select(g => g.Key + ": " + g.Count()));
            }
            catch (Exception ex) { _status.Text = "Không thể hoàn tất kiểm tra: " + ex.Message; }
            finally { UseWaitCursor = false; }
        }

        private void Locate()
        {
            SelectedIssue = _grid.CurrentRow?.DataBoundItem as DrawingIssue;
            if (SelectedIssue == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class CmdDrawingCheck : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                using (var form = new DrawingCheckForm(uidoc))
                {
                    if (form.ShowDialog() == DialogResult.OK && form.SelectedIssue != null)
                    {
                        var issue = form.SelectedIssue;
                        var view = uidoc.Document.GetElement(issue.ViewId) as View;
                        if (view == null || uidoc.Document.GetElement(issue.ElementId) == null)
                        { TaskDialog.Show("Drawing Check", "View hoặc phần tử không còn tồn tại. Hãy kiểm tra lại."); return Result.Cancelled; }
                        uidoc.ActiveView = view;
                        uidoc.Selection.SetElementIds(new[] { issue.ElementId });
                        uidoc.ShowElements(issue.ElementId);
                    }
                }
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; TaskDialog.Show("Drawing Check", ex.Message); return Result.Failed; }
        }
    }
}

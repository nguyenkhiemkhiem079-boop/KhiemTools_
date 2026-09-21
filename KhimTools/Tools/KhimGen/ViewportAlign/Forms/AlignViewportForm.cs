using KhimTools.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.ViewportAlign.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using ComboBox = System.Windows.Forms.ComboBox;
using CheckBox = System.Windows.Forms.CheckBox;
using RadioButton = System.Windows.Forms.RadioButton;
using GroupBox = System.Windows.Forms.GroupBox;
using TreeView = System.Windows.Forms.TreeView;
using TreeNode = System.Windows.Forms.TreeNode;
using View = Autodesk.Revit.DB.View;

namespace KhimTools.ViewportAlign.Forms
{
    public class AlignViewportForm : KTBaseForm
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private Viewport _sourceViewport;
        private ScheduleSheetInstance _sourceSchedule;
        private View _sourceView;
        private ViewSheet _sourceSheet;
        private List<ViewSheet> _allSheets;

        // UI Controls
        private TextBox _txtSearch;
        private Button _btnRefresh;
        private TreeView _treeSheets;
        private Label _lblTemplateName;
        private Button _btnSelectTemplate;
        private Button _btnSelectSchedule;
        private ComboBox _cmbOperation;
        private Label _lblPreview;

        // Arrange Options
        private RadioButton _rdViewsAndTitles;
        private RadioButton _rdViewsOnly;
        private RadioButton _rdTitlesOnly;

        // Auto Select Views Checkboxes
        private CheckBox _chkOnlyNotes;
        private CheckBox _chkOnlyKeyplan;
        private CheckBox _chkOnlyView;
        private CheckBox _chkOnlySchedule;

        private Button _btnOk;
        private Button _btnCancel;

        private bool _isUpdatingTree = false;

        public Viewport SourceViewport => _sourceViewport;
        public ScheduleSheetInstance SourceSchedule => _sourceSchedule;
        public AlignmentReference SourceReference
        {
            get
            {
                try
                {
                    if (_sourceViewport != null) return ViewportAlignService.CreateViewportReference(_doc, _sourceViewport);
                    if (_sourceSchedule != null) return ViewportAlignService.CreateScheduleReference(_doc, _sourceSchedule);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[ViewportAlign] source reference unavailable: " + ex.Message);
                }
                return null;
            }
        }
        public AlignmentOperation SelectedOperation
        {
            get
            {
                if (_cmbOperation?.SelectedItem is AlignmentOperation operation) return operation;
                return SelectedArrangeMode == ArrangeMode.ViewsOnly
                    ? AlignmentOperation.MATCH_CENTER
                    : SelectedArrangeMode == ArrangeMode.TitlesOnly
                        ? AlignmentOperation.MATCH_TITLE
                        : AlignmentOperation.MATCH_VIEW_AND_TITLE;
            }
        }
        public ArrangeMode SelectedArrangeMode
        {
            get
            {
                if (_rdViewsOnly.Checked) return ArrangeMode.ViewsOnly;
                if (_rdTitlesOnly.Checked) return ArrangeMode.TitlesOnly;
                return ArrangeMode.ViewsAndTitles;
            }
        }

        public List<TargetViewItem> SelectedTargetViews { get; private set; } = new List<TargetViewItem>();

        public AlignViewportForm(UIDocument uidoc, Viewport initialSourceVp = null)
        {
            _uidoc = uidoc;
            _doc = uidoc?.Document;
            _sourceViewport = initialSourceVp;

            if (_sourceViewport != null)
            {
                _sourceView = _doc.GetElement(_sourceViewport.ViewId) as View;
                _sourceSheet = _doc.GetElement(_sourceViewport.SheetId) as ViewSheet;
            }

            ReloadDocumentState();

            BuildUi();
            PopulateTree();
        }

        private void BuildUi()
        {
            bool isEn = LanguageManager.IsEnglish;
            Text = isEn ? "Arrange Views & Title" : "Căn Chỉnh Vị Trí Viewport & Tiêu Đề Bản Vẽ";
            Width = 1020;
            Height = 650;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(920, 600);
            MaximizeBox = true;
            MinimizeBox = false;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // ══════════════════════════════════════════════════════════════════
            // 1. BOTTOM ACTION BAR
            // ══════════════════════════════════════════════════════════════════
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.FromArgb(240, 242, 245),
                Padding = new Padding(15, 10, 15, 10)
            };

            _btnOk = new Button
            {
                Text = isEn ? "OK" : "Căn Chỉnh Vị Trí",
                Width = 140,
                Height = 35,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.Click += (s, e) => ExecuteAlignment();

            _btnCancel = new Button
            {
                Text = isEn ? "Cancel" : "Hủy",
                Width = 90,
                Height = 35,
                BackColor = Color.FromArgb(225, 228, 232),
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            bottomPanel.Controls.Add(_btnOk);
            bottomPanel.Controls.Add(_btnCancel);
            bottomPanel.Resize += (s, e) =>
            {
                _btnOk.Left = bottomPanel.Width - _btnOk.Width - 15;
                _btnCancel.Left = _btnOk.Left - _btnCancel.Width - 10;
                _btnOk.Top = 10;
                _btnCancel.Top = 10;
            };
            Controls.Add(bottomPanel);

            // ══════════════════════════════════════════════════════════════════
            // 2. RIGHT PANEL: SOURCE VIEWPORT & ARRANGE OPTIONS
            // ══════════════════════════════════════════════════════════════════
            var rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 390,
                AutoScroll = true,
                Padding = new Padding(10, 10, 15, 10),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            // 2.1 Group: Source Viewport
            var grpSource = new GroupBox
            {
                Text = isEn ? "Source Viewport" : "Viewport Mẫu (Source)",
                Dock = DockStyle.Top,
                Height = 175,
                Padding = new Padding(12, 10, 12, 10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            var lblSourceTitle = new Label
            {
                Text = isEn ? "Selected Template Viewport:" : "Viewport mẫu đã chọn:",
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _lblTemplateName = new Label
            {
                Text = GetSourceViewportDisplay(),
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0)
            };

            _btnSelectTemplate = new Button
            {
                Text = isEn ? "Select View Template" : "Chọn Viewport Mẫu Trên Sheet",
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = Color.FromArgb(2, 132, 199),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnSelectTemplate.FlatAppearance.BorderSize = 0;
            _btnSelectTemplate.Click += (s, e) => PickSourceViewportInteractively();

            _btnSelectSchedule = new Button
            {
                Text = isEn ? "Select Schedule Reference" : "Chọn Schedule Mẫu",
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(14, 116, 144),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            _btnSelectSchedule.FlatAppearance.BorderSize = 0;
            _btnSelectSchedule.Click += (s, e) => PickSourceScheduleInteractively();

            grpSource.Controls.Add(_btnSelectSchedule);
            grpSource.Controls.Add(_btnSelectTemplate);
            grpSource.Controls.Add(_lblTemplateName);
            grpSource.Controls.Add(lblSourceTitle);
            rightPanel.Controls.Add(grpSource);

            // 2.2 Group: Arrange Options
            var grpArrange = new GroupBox
            {
                Text = isEn ? "Arrange Options" : "Tùy Chọn Căn Chỉnh",
                Dock = DockStyle.Top,
                Height = 130,
                Padding = new Padding(12, 8, 12, 8),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Margin = new Padding(0, 10, 0, 0)
            };

            var flpArrange = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            _rdViewsAndTitles = new RadioButton
            {
                Text = isEn ? "Arrange Views & Titles" : "View và tiêu đề",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Margin = new Padding(3, 4, 3, 4)
            };

            _rdViewsOnly = new RadioButton
            {
                Text = isEn ? "Arrange Views Only" : "Chỉ vị trí view",
                AutoSize = true,
                Margin = new Padding(3, 4, 3, 4)
            };

            _rdTitlesOnly = new RadioButton
            {
                Text = isEn ? "Arrange Titles Only" : "Chỉ tiêu đề",
                AutoSize = true,
                Margin = new Padding(3, 4, 3, 4)
            };

            flpArrange.Controls.Add(_rdViewsAndTitles);
            flpArrange.Controls.Add(_rdViewsOnly);
            flpArrange.Controls.Add(_rdTitlesOnly);

            _cmbOperation = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 300,
                Height = 28,
                Margin = new Padding(3, 8, 3, 3)
            };
            foreach (AlignmentOperation operation in Enum.GetValues(typeof(AlignmentOperation)))
                _cmbOperation.Items.Add(operation);
            _cmbOperation.SelectedItem = AlignmentOperation.MATCH_VIEW_AND_TITLE;
            _rdViewsAndTitles.CheckedChanged += (s, e) => { if (_rdViewsAndTitles.Checked) _cmbOperation.SelectedItem = AlignmentOperation.MATCH_VIEW_AND_TITLE; };
            _rdViewsOnly.CheckedChanged += (s, e) => { if (_rdViewsOnly.Checked) _cmbOperation.SelectedItem = AlignmentOperation.MATCH_CENTER; };
            _rdTitlesOnly.CheckedChanged += (s, e) => { if (_rdTitlesOnly.Checked) _cmbOperation.SelectedItem = AlignmentOperation.MATCH_TITLE; };
            _cmbOperation.SelectedIndexChanged += (s, e) => UpdatePreviewSummary();
            flpArrange.Controls.Add(_cmbOperation);
            grpArrange.Controls.Add(flpArrange);
            rightPanel.Controls.Add(grpArrange);

            // 2.3 Group: Auto Select Views
            var grpAutoSelect = new GroupBox
            {
                Text = isEn ? "Auto Select Views" : "Chọn Nhanh Đối Tượng",
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(12, 8, 12, 8),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Margin = new Padding(0, 10, 0, 0)
            };

            var flpAutoSelect = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            _chkOnlyNotes = new CheckBox { Text = isEn ? "ONLY NOTE / LEGEND" : "Ghi chú / Legend", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            _chkOnlyKeyplan = new CheckBox { Text = isEn ? "ONLY KEYPLAN" : "Mặt bằng định vị (Keyplan)", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            _chkOnlyView = new CheckBox { Text = isEn ? "ONLY MODEL VIEW" : "Mặt bằng / mặt cắt mô hình", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            _chkOnlySchedule = new CheckBox { Text = isEn ? "ONLY SCHEDULES" : "Bảng thống kê (Schedules)", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };

            _chkOnlyNotes.CheckedChanged += (s, e) => ApplyAutoSelectFilters();
            _chkOnlyKeyplan.CheckedChanged += (s, e) => ApplyAutoSelectFilters();
            _chkOnlyView.CheckedChanged += (s, e) => ApplyAutoSelectFilters();
            _chkOnlySchedule.CheckedChanged += (s, e) => ApplyAutoSelectFilters();

            flpAutoSelect.Controls.Add(_chkOnlyNotes);
            flpAutoSelect.Controls.Add(_chkOnlyKeyplan);
            flpAutoSelect.Controls.Add(_chkOnlyView);
            flpAutoSelect.Controls.Add(_chkOnlySchedule);
            grpAutoSelect.Controls.Add(flpAutoSelect);
            rightPanel.Controls.Add(grpAutoSelect);

            _lblPreview = new Label
            {
                Dock = DockStyle.Top,
                Height = 42,
                AutoEllipsis = true,
                Padding = new Padding(4),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = isEn ? "Preview: no targets selected" : "Xem trước: chưa chọn đối tượng"
            };
            rightPanel.Controls.Add(_lblPreview);

            Controls.Add(rightPanel);

            // ══════════════════════════════════════════════════════════════════
            // 3. LEFT PANEL: SEARCH BAR & TREEVIEW OF SHEETS AND VIEWS
            // ══════════════════════════════════════════════════════════════════
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 10, 5, 10)
            };

            // 3.1 Search Bar Top
            var pnlSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(0, 0, 0, 6)
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 28,
                Font = new Font("Segoe UI", 9.5F)
            };
            _txtSearch.TextChanged += (s, e) => FilterTree();

            _btnRefresh = new Button
            {
                Text = isEn ? "Refresh" : "Làm mới",
                Dock = DockStyle.Right,
                Width = 85,
                Height = 28,
                FlatStyle = FlatStyle.System
            };
            _btnRefresh.Click += (s, e) =>
            {
                _txtSearch.Text = "";
                ReloadDocumentState();
                PopulateTree();
            };

            pnlSearch.Controls.Add(_txtSearch);
            pnlSearch.Controls.Add(_btnRefresh);
            leftPanel.Controls.Add(pnlSearch);

            // 3.2 TreeView with Checkboxes
            _treeSheets = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            _treeSheets.AfterCheck += TreeSheets_AfterCheck;
            leftPanel.Controls.Add(_treeSheets);

            Controls.Add(leftPanel);
        }

        private string GetSourceViewportDisplay()
        {
            if (_sourceSchedule != null)
            {
                string scheduleName = _doc?.GetElement(_sourceSchedule.ScheduleId) is ViewSchedule schedule
                    ? schedule.Name
                    : (LanguageManager.IsEnglish ? "Schedule" : "Bảng thống kê");
                return $" [Schedule] {scheduleName}";
            }
            if (_sourceViewport == null) return " <None>";
            string sheetNum = _sourceSheet?.SheetNumber ?? "";
            string viewName = _sourceView?.Name ?? "Viewport";
            return $" [{sheetNum}] {viewName}";
        }

        private void PickSourceViewportInteractively()
        {
            Hide();
            try
            {
                Reference pickedRef = _uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new ViewportSelectionFilter(),
                    LanguageManager.IsEnglish
                        ? "Select source Viewport on Sheet to use as alignment reference"
                        : "Chọn Viewport nguồn trên Sheet để lấy vị trí mẫu");

                if (pickedRef != null && _doc.GetElement(pickedRef) is Viewport vp)
                {
                    _sourceViewport = vp;
                    _sourceSchedule = null;
                    _sourceView = _doc.GetElement(_sourceViewport.ViewId) as View;
                    _sourceSheet = _doc.GetElement(_sourceViewport.SheetId) as ViewSheet;
                    _lblTemplateName.Text = GetSourceViewportDisplay();
                    UpdatePreviewSummary();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Khim Tools", ex.Message);
            }
            finally
            {
                Show();
                BringToFront();
            }
        }

        private void PickSourceScheduleInteractively()
        {
            Hide();
            try
            {
                Reference pickedRef = _uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new ScheduleSelectionFilter(),
                    LanguageManager.IsEnglish
                        ? "Select source Schedule on Sheet to use as alignment reference"
                        : "Chọn Schedule nguồn trên Sheet để làm tham chiếu");

                if (pickedRef != null && _doc.GetElement(pickedRef) is ScheduleSheetInstance schedule)
                {
                    _sourceSchedule = schedule;
                    _sourceViewport = null;
                    _sourceView = _doc.GetElement(schedule.ScheduleId) as View;
                    _sourceSheet = _doc.GetElement(schedule.OwnerViewId) as ViewSheet;
                    _lblTemplateName.Text = GetSourceViewportDisplay();
                    UpdatePreviewSummary();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Khim Tools", ex.Message);
            }
            finally
            {
                Show();
                BringToFront();
            }
        }

        private void ReloadDocumentState()
        {
            if (_allSheets == null) _allSheets = new List<ViewSheet>();
            _allSheets.Clear();
            if (_doc == null) return;
            _allSheets.AddRange(new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => s != null && !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber ?? "")
                .ToList());
        }

        private void UpdatePreviewSummary()
        {
            if (_lblPreview == null) return;
            int selected = _treeSheets?.Nodes.Cast<TreeNode>()
                .SelectMany(n => n.Nodes.Cast<TreeNode>())
                .Count(n => n.Checked) ?? 0;
            string sourceKind = _sourceSchedule != null ? "SCHEDULE" : _sourceViewport != null ? "VIEWPORT" : "NONE";
            int ready = 0;
            int blocked = 0;
            var source = SourceReference;
            if (source != null && _treeSheets != null)
            {
                var selectedItems = _treeSheets.Nodes.Cast<TreeNode>()
                    .SelectMany(n => n.Nodes.Cast<TreeNode>())
                    .Where(n => n.Checked && n.Tag is TargetViewItem)
                    .Select(n => (TargetViewItem)n.Tag)
                    .ToList();
                var rows = ViewportAlignmentPreflightService.Run(_doc, source, selectedItems, SelectedOperation);
                ready = rows.Count(r => r.CanExecute);
                blocked = rows.Count - ready;
            }
            _lblPreview.Text = LanguageManager.IsEnglish
                ? $"Preview — Source: {sourceKind}; Operation: {SelectedOperation}; Selected: {selected}; Ready: {ready}; Blocked: {blocked}"
                : $"Xem trước — Nguồn: {sourceKind}; Thao tác: {SelectedOperation}; Đã chọn: {selected}; Sẵn sàng: {ready}; Bị chặn: {blocked}";
        }

        private void PopulateTree()
        {
            _isUpdatingTree = true;
            _treeSheets.BeginUpdate();
            _treeSheets.Nodes.Clear();

            string search = (_txtSearch.Text ?? "").Trim().ToLowerInvariant();

            foreach (var sheet in _allSheets)
            {
                var viewsOnSheet = ViewportAlignmentCollector.Collect(_doc, sheet);
                if (!viewsOnSheet.Any()) continue;

                // Kiểm tra bộ lọc từ khóa
                bool sheetMatches = string.IsNullOrEmpty(search) ||
                                    (sheet.SheetNumber ?? "").ToLowerInvariant().Contains(search) ||
                                    (sheet.Name ?? "").ToLowerInvariant().Contains(search);

                var matchingViews = string.IsNullOrEmpty(search)
                    ? viewsOnSheet
                    : viewsOnSheet.Where(v => sheetMatches || (v.ViewName ?? "").ToLowerInvariant().Contains(search)).ToList();

                if (!matchingViews.Any()) continue;

                string sheetTitle = $"{sheet.SheetNumber ?? ""} - {sheet.Name ?? ""}";
                var sheetNode = new TreeNode(sheetTitle)
                {
                    Tag = sheet,
                    Checked = false
                };

                foreach (var viewItem in matchingViews)
                {
                    // Đánh dấu nếu là viewport mẫu
                    bool isSource = (_sourceViewport != null && viewItem.ViewportOrScheduleId == _sourceViewport.Id) ||
                                    (_sourceSchedule != null && viewItem.ViewportOrScheduleId == _sourceSchedule.Id);
                    string viewTitle = isSource ? $"{viewItem.ViewName} (Source Reference)" : viewItem.ViewName;

                    var viewNode = new TreeNode(viewTitle)
                    {
                        Tag = viewItem,
                        Checked = false,
                        ForeColor = isSource ? Color.Gray : Color.Black
                    };

                    sheetNode.Nodes.Add(viewNode);
                }

                _treeSheets.Nodes.Add(sheetNode);
                if (!string.IsNullOrEmpty(search)) sheetNode.Expand();
            }

            _treeSheets.EndUpdate();
            _isUpdatingTree = false;
            UpdatePreviewSummary();
        }

        private void FilterTree()
        {
            PopulateTree();
        }

        private void TreeSheets_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingTree) return;
            _isUpdatingTree = true;

            try
            {
                TreeNode node = e.Node;
                bool isChecked = node.Checked;

                // 1. Nếu tích vào Sheet cha -> Tự động tích/bỏ tích tất cả View con
                if (node.Parent == null)
                {
                    foreach (TreeNode child in node.Nodes)
                    {
                        child.Checked = isChecked;
                    }
                }
                // 2. Nếu tích vào View con -> Cập nhật trạng thái Sheet cha
                else
                {
                    TreeNode parent = node.Parent;
                    bool anyChecked = false;

                    foreach (TreeNode sibling in parent.Nodes)
                    {
                        if (sibling.Checked) anyChecked = true;
                    }

                    parent.Checked = anyChecked;
                }
            }
            finally
            {
                _isUpdatingTree = false;
                UpdatePreviewSummary();
            }
        }

        private void ApplyAutoSelectFilters()
        {
            bool filterNotes = _chkOnlyNotes.Checked;
            bool filterKeyplan = _chkOnlyKeyplan.Checked;
            bool filterView = _chkOnlyView.Checked;
            bool filterSchedule = _chkOnlySchedule.Checked;

            bool anyFilterActive = filterNotes || filterKeyplan || filterView || filterSchedule;

            _isUpdatingTree = true;
            _treeSheets.BeginUpdate();

            foreach (TreeNode sheetNode in _treeSheets.Nodes)
            {
                bool anyChildChecked = false;

                foreach (TreeNode viewNode in sheetNode.Nodes)
                {
                    if (viewNode.Tag is TargetViewItem item)
                    {
                        if (!anyFilterActive)
                        {
                            viewNode.Checked = false;
                            continue;
                        }

                        bool check = false;
                        string lowerName = (item.ViewName ?? "").ToLowerInvariant();

                        if (filterNotes && (item.ViewType == ViewType.Legend || lowerName.Contains("note") || lowerName.Contains("legend") || lowerName.Contains("ghi chú")))
                            check = true;
                        if (filterKeyplan && (lowerName.Contains("keyplan") || lowerName.Contains("định vị") || lowerName.Contains("so do")))
                            check = true;
                        if (filterView && !item.IsSchedule && item.ViewType != ViewType.Legend && !lowerName.Contains("keyplan"))
                            check = true;
                        if (filterSchedule && item.IsSchedule)
                            check = true;

                        viewNode.Checked = check;
                        if (check) anyChildChecked = true;
                    }
                }

                sheetNode.Checked = anyChildChecked;
                if (anyChildChecked) sheetNode.Expand();
            }

            _treeSheets.EndUpdate();
            _isUpdatingTree = false;
        }

        private void ExecuteAlignment()
        {
            if (SourceReference == null)
            {
                TaskDialog.Show("Khim Tools",
                    LanguageManager.IsEnglish
                        ? "Please select a source Viewport or Schedule reference first."
                        : "Vui lòng chọn Viewport hoặc Schedule nguồn trước khi căn chỉnh.");
                return;
            }

            SelectedTargetViews.Clear();

            foreach (TreeNode sheetNode in _treeSheets.Nodes)
            {
                foreach (TreeNode viewNode in sheetNode.Nodes)
                {
                    if (viewNode.Checked && viewNode.Tag is TargetViewItem item)
                    {
                        // Keep the reference in the selection so preflight can report SOURCE_REFERENCE.
                        SelectedTargetViews.Add(item);
                    }
                }
            }

            if (!SelectedTargetViews.Any())
            {
                TaskDialog.Show("Khim Tools",
                    LanguageManager.IsEnglish
                        ? "Please check at least one view to align."
                        : "Vui lòng tích chọn ít nhất một Khung nhìn (View) cần căn chỉnh.");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private class ViewportSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Viewport;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private class ScheduleSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is ScheduleSheetInstance;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}

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
using CheckBox = System.Windows.Forms.CheckBox;
using RadioButton = System.Windows.Forms.RadioButton;
using GroupBox = System.Windows.Forms.GroupBox;
using TreeView = System.Windows.Forms.TreeView;
using TreeNode = System.Windows.Forms.TreeNode;
using View = Autodesk.Revit.DB.View;

namespace KhimTools.ViewportAlign.Forms
{
    public class AlignViewportForm : Form
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private Viewport _sourceViewport;
        private View _sourceView;
        private ViewSheet _sourceSheet;
        private readonly List<ViewSheet> _allSheets;

        // UI Controls
        private TextBox _txtSearch;
        private Button _btnRefresh;
        private TreeView _treeSheets;
        private Label _lblTemplateName;
        private Button _btnSelectTemplate;

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

            _allSheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
                .ToList();

            BuildUi();
            PopulateTree();
        }

        private void BuildUi()
        {
            bool isEn = LanguageManager.IsEnglish;
            Text = isEn ? "Arrange Views & Title" : "Căn Chỉnh Vị Trí Viewport & Tiêu Đề Bản Vẽ";
            Width = 840;
            Height = 650;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
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
                Width = 330,
                Padding = new Padding(10, 10, 15, 10),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            // 2.1 Group: Source Viewport
            var grpSource = new GroupBox
            {
                Text = isEn ? "Source Viewport" : "Viewport Mẫu (Source)",
                Dock = DockStyle.Top,
                Height = 135,
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
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            _rdViewsAndTitles = new RadioButton
            {
                Text = isEn ? "Arrange Views & Titles" : "Căn chỉnh cả View & Tiêu đề (Titles)",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Margin = new Padding(3, 4, 3, 4)
            };

            _rdViewsOnly = new RadioButton
            {
                Text = isEn ? "Arrange Views Only" : "Chỉ căn chỉnh vị trí View",
                AutoSize = true,
                Margin = new Padding(3, 4, 3, 4)
            };

            _rdTitlesOnly = new RadioButton
            {
                Text = isEn ? "Arrange Titles Only" : "Chỉ căn chỉnh vị trí Tiêu đề (Titles)",
                AutoSize = true,
                Margin = new Padding(3, 4, 3, 4)
            };

            flpArrange.Controls.Add(_rdViewsAndTitles);
            flpArrange.Controls.Add(_rdViewsOnly);
            flpArrange.Controls.Add(_rdTitlesOnly);
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
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            _chkOnlyNotes = new CheckBox { Text = isEn ? "ONLY NOTE / LEGEND" : "CHỈ GHI CHÚ / LEGEND", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            _chkOnlyKeyplan = new CheckBox { Text = isEn ? "ONLY KEYPLAN" : "CHỈ MẶT BẰNG ĐỊNH VỊ (KEYPLAN)", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            _chkOnlyView = new CheckBox { Text = isEn ? "ONLY MODEL VIEW" : "CHỈ MẶT BẰNG / MẶT CẮT MÔ HÌNH", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            _chkOnlySchedule = new CheckBox { Text = isEn ? "ONLY SCHEDULES" : "CHỈ BẢNG THỐNG KÊ (SCHEDULES)", AutoSize = true, Margin = new Padding(3, 4, 3, 3) };

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
                    _sourceView = _doc.GetElement(_sourceViewport.ViewId) as View;
                    _sourceSheet = _doc.GetElement(_sourceViewport.SheetId) as ViewSheet;
                    _lblTemplateName.Text = GetSourceViewportDisplay();
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

        private void PopulateTree()
        {
            _isUpdatingTree = true;
            _treeSheets.BeginUpdate();
            _treeSheets.Nodes.Clear();

            string search = (_txtSearch.Text ?? "").Trim().ToLowerInvariant();

            foreach (var sheet in _allSheets)
            {
                var viewsOnSheet = ViewportAlignService.GetViewsOnSheet(_doc, sheet);
                if (!viewsOnSheet.Any()) continue;

                // Kiểm tra bộ lọc từ khóa
                bool sheetMatches = string.IsNullOrEmpty(search) ||
                                    sheet.SheetNumber.ToLowerInvariant().Contains(search) ||
                                    sheet.Name.ToLowerInvariant().Contains(search);

                var matchingViews = string.IsNullOrEmpty(search)
                    ? viewsOnSheet
                    : viewsOnSheet.Where(v => sheetMatches || v.ViewName.ToLowerInvariant().Contains(search)).ToList();

                if (!matchingViews.Any()) continue;

                string sheetTitle = $"{sheet.SheetNumber} - {sheet.Name}";
                var sheetNode = new TreeNode(sheetTitle)
                {
                    Tag = sheet,
                    Checked = false
                };

                foreach (var viewItem in matchingViews)
                {
                    // Đánh dấu nếu là viewport mẫu
                    bool isSource = (_sourceViewport != null && viewItem.ViewportOrScheduleId == _sourceViewport.Id);
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
                    bool allChecked = true;
                    bool anyChecked = false;

                    foreach (TreeNode sibling in parent.Nodes)
                    {
                        if (sibling.Checked) anyChecked = true;
                        else allChecked = false;
                    }

                    parent.Checked = anyChecked;
                }
            }
            finally
            {
                _isUpdatingTree = false;
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
            if (_sourceViewport == null)
            {
                TaskDialog.Show("Khim Tools",
                    LanguageManager.IsEnglish
                        ? "Please select a Source Viewport template first."
                        : "Vui lòng chọn một Viewport mẫu trước khi căn chỉnh.");
                return;
            }

            SelectedTargetViews.Clear();

            foreach (TreeNode sheetNode in _treeSheets.Nodes)
            {
                foreach (TreeNode viewNode in sheetNode.Nodes)
                {
                    if (viewNode.Checked && viewNode.Tag is TargetViewItem item)
                    {
                        // Không căn chỉnh lại chính Viewport mẫu
                        if (item.ViewportOrScheduleId != _sourceViewport.Id)
                        {
                            SelectedTargetViews.Add(item);
                        }
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
    }
}

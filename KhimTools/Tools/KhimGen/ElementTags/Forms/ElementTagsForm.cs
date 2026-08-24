using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.Core.UI;
using KhimTools.ElementTags.Models;
using KhimTools.ElementTags.Services;
using Color = System.Drawing.Color;
using Form = System.Windows.Forms.Form;
using Document = Autodesk.Revit.DB.Document;
using View = Autodesk.Revit.DB.View;
using View3D = Autodesk.Revit.DB.View3D;
using ElementId = Autodesk.Revit.DB.ElementId;
using FamilySymbol = Autodesk.Revit.DB.FamilySymbol;
using IndependentTag = Autodesk.Revit.DB.IndependentTag;
using BuiltInCategory = Autodesk.Revit.DB.BuiltInCategory;
using TextBox = System.Windows.Forms.TextBox;

namespace KhimTools.ElementTags.Forms
{
    public class ElementTagsForm : KTBaseForm
    {
        private UIDocument _uidoc;
        private Document _doc;
        private List<ElementTagsItem> _allItems;
        private HashSet<long> _ignoredTagIds = new HashSet<long>();

        // Left Panel configuration controls
        private DataGridView _grid;
        private CheckBox _chkAddLeader;
        private CheckBox _chkOnlyUntagged;
        private Button _btnTagAll;
        private Button _btnCheckHost;
        private Button _btnClashTag;
        private Button _btnCheck3d;
        private Button _btnReset;

        // Right Panel controls matching audit workflow but styled professionally
        private System.Windows.Forms.Panel _pnlRight;
        private TextBox _txtMaxErrorDistance;
        private Button _btnRunProximityAudit;
        
        private TabControl _tabResult;
        private TabPage _tabColumns;
        private TabPage _tabWalls;
        private TabPage _tabFloors;
        private TabPage _tabIgnored;

        private DataGridView _gridColumns;
        private DataGridView _gridWalls;
        private DataGridView _gridFloors;
        private DataGridView _gridIgnored;

        private Label _lblProximityAlert;
        private Button _btnResetColor;
        private Button _btnHighlightRed;
        private Button _btnZoomTo;
        private Button _btnPass;
        private Button _btnClose;

        // Save list of all tag and host IDs to quickly highlight or reset
        private List<ElementId> _lastAuditedTagIds = new List<ElementId>();
        private List<ElementId> _lastAuditedHostIds = new List<ElementId>();

        public ElementTagsForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;

            InitializeComponent();
            LoadData();
            
            // Run initial audit on load
            RunProximityAudit();
        }

        private void InitializeComponent()
        {
            // Set Form Properties
            this.Size = new Size(1100, 720);
            this.SetFormTitle("K-TOOLS - Check Tag Host", "Audit & Resolve Tag Host Proximity");
            KhimUiStyle.ApplyFormTheme(this);

            // Container Panel
            var pnlContainer = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 60, 16, 16)
            };
            this.Controls.Add(pnlContainer);

            // ── LEFT PANEL (WIDTH 400): AUTO TAGGING CONFIG ──
            var pnlLeft = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Left,
                Width = 380,
                Padding = new Padding(0, 0, 10, 0)
            };
            pnlContainer.Controls.Add(pnlLeft);

            // 1. DataGridView Configuration (Left)
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = KhimUiStyle.CardBorder,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 34 }
            };

            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colCheck",
                HeaderText = "✔",
                Width = 35,
                FlatStyle = FlatStyle.Flat
            };
            var colCategory = new DataGridViewTextBoxColumn
            {
                Name = "colCategory",
                HeaderText = "CATEGORY",
                Width = 110,
                ReadOnly = true
            };
            var colColor = new DataGridViewTextBoxColumn
            {
                Name = "colColor",
                HeaderText = "COLOR",
                Width = 55,
                ReadOnly = true
            };
            var colTagType = new DataGridViewComboBoxColumn
            {
                Name = "colTagType",
                HeaderText = "TAG TYPE",
                Width = 180,
                FlatStyle = FlatStyle.Flat
            };

            _grid.Columns.AddRange(colCheck, colCategory, colColor, colTagType);
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = KhimUiStyle.SecondaryButtonBg,
                ForeColor = KhimUiStyle.TextSecondary,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            _grid.ColumnHeadersHeight = 32;

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellClick += Grid_CellClick;
            pnlLeft.Controls.Add(_grid);

            // Bottom controls on Left Panel
            var pnlLeftBottom = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 220,
                Padding = new Padding(0, 10, 0, 0)
            };
            pnlLeft.Controls.Add(pnlLeftBottom);

            var pnlOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight
            };
            pnlLeftBottom.Controls.Add(pnlOptions);

            _chkAddLeader = new CheckBox
            {
                Text = "Add Leader",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 6, 20, 3)
            };
            _chkOnlyUntagged = new CheckBox
            {
                Text = "Only tag untagged elements",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = KhimUiStyle.TextSecondary,
                Margin = new Padding(0, 6, 3, 3)
            };
            pnlOptions.Controls.AddRange(new System.Windows.Forms.Control[] { _chkAddLeader, _chkOnlyUntagged });

            var pnlActions = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 0)
            };
            pnlLeftBottom.Controls.Add(pnlActions);

            _btnTagAll = new Button
            {
                Text = "TAG ALL SELECTED",
                Height = 40,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 8)
            };
            KhimUiStyle.ApplyPrimaryButton(_btnTagAll);
            _btnTagAll.Click += BtnTagAll_Click;
            pnlActions.Controls.Add(_btnTagAll);

            var pnlSubActions = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };
            pnlActions.Controls.Add(pnlSubActions);

            var pnlLeftButtons = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Left,
                Width = 280
            };
            pnlSubActions.Controls.Add(pnlLeftButtons);

            _btnCheckHost = new Button
            {
                Text = "Check tag host 2d",
                Location = new Point(0, 8),
                Size = new Size(135, 32)
            };
            KhimUiStyle.ApplySecondaryButton(_btnCheckHost);
            _btnCheckHost.Click += BtnCheckHost_Click;

            _btnClashTag = new Button
            {
                Text = "Clash tag",
                Location = new Point(145, 8),
                Size = new Size(135, 32)
            };
            KhimUiStyle.ApplySecondaryButton(_btnClashTag);
            _btnClashTag.Click += BtnClashTag_Click;

            _btnCheck3d = new Button
            {
                Text = "Check 3d",
                Location = new Point(0, 46),
                Size = new Size(280, 32)
            };
            KhimUiStyle.ApplySecondaryButton(_btnCheck3d);
            _btnCheck3d.Click += BtnCheck3d_Click;

            pnlLeftButtons.Controls.AddRange(new System.Windows.Forms.Control[] { _btnCheckHost, _btnClashTag, _btnCheck3d });

            _btnReset = new Button
            {
                Text = "RESET",
                Dock = DockStyle.Right,
                Width = 80,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(8, 8, 0, 0)
            };
            KhimUiStyle.ApplySecondaryButton(_btnReset);
            _btnReset.Click += BtnReset_Click;
            pnlSubActions.Controls.Add(_btnReset);

            _grid.BringToFront();

            // ── RIGHT PANEL (WIDTH FILL): AUDIT & ALIGNMENT RESULTS ──
            _pnlRight = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 0, 0, 0)
            };
            pnlContainer.Controls.Add(_pnlRight);

            // Header config line (Max Error Distance & Refresh button)
            var pnlRightHeader = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(0, 0, 0, 8)
            };
            _pnlRight.Controls.Add(pnlRightHeader);

            var lblMaxDistance = new Label
            {
                Text = "Khoảng cách lỗi tối đa (mm):",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 10),
                Width = 180,
                ForeColor = KhimUiStyle.TextPrimary
            };
            pnlRightHeader.Controls.Add(lblMaxDistance);

            _txtMaxErrorDistance = new TextBox
            {
                Text = "200",
                Location = new Point(185, 8),
                Width = 80,
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlRightHeader.Controls.Add(_txtMaxErrorDistance);

            _btnRunProximityAudit = new Button
            {
                Text = "Refresh",
                Location = new Point(275, 5),
                Size = new Size(80, 28)
            };
            KhimUiStyle.ApplySecondaryButton(_btnRunProximityAudit);
            _btnRunProximityAudit.Click += BtnRunProximityAudit_Click;
            pnlRightHeader.Controls.Add(_btnRunProximityAudit);

            // TabControl to hold Column, Wall, Floor and Ignored tabs
            _tabResult = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _pnlRight.Controls.Add(_tabResult);

            _tabColumns = new TabPage("Cột (0)");
            _tabWalls = new TabPage("Vách (0)");
            _tabFloors = new TabPage("Sàn (0)");
            _tabIgnored = new TabPage("Đã bỏ qua (0)");

            _tabResult.TabPages.AddRange(new TabPage[] { _tabColumns, _tabWalls, _tabFloors, _tabIgnored });

            // Initialize Grid helper for each Tab
            _gridColumns = CreateAuditGrid();
            _gridWalls = CreateAuditGrid();
            _gridFloors = CreateAuditGrid();
            _gridIgnored = CreateAuditGrid();

            _tabColumns.Controls.Add(_gridColumns);
            _tabWalls.Controls.Add(_gridWalls);
            _tabFloors.Controls.Add(_gridFloors);
            _tabIgnored.Controls.Add(_gridIgnored);

            // Footer of Right Panel (Status text & Action buttons)
            var pnlRightFooter = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 85,
                Padding = new Padding(0, 10, 0, 0)
            };
            _pnlRight.Controls.Add(pnlRightFooter);

            _lblProximityAlert = new Label
            {
                Text = "Chưa phát hiện lỗi sai vị trí.",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.ForestGreen,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlRightFooter.Controls.Add(_lblProximityAlert);

            var pnlFooterButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 5, 0, 0)
            };
            pnlRightFooter.Controls.Add(pnlFooterButtons);

            _btnZoomTo = new Button { Text = "Zoom To", Size = new Size(110, 32) };
            KhimUiStyle.ApplyPrimaryButton(_btnZoomTo);
            _btnZoomTo.Click += BtnZoomTo_Click;

            _btnPass = new Button { Text = "Pass", Size = new Size(90, 32), BackColor = Color.FromArgb(245, 158, 11) }; // Orange/Yellow theme
            _btnPass.ForeColor = Color.White;
            _btnPass.FlatStyle = FlatStyle.Flat;
            _btnPass.FlatAppearance.BorderSize = 0;
            _btnPass.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _btnPass.Click += BtnPass_Click;

            _btnClose = new Button { Text = "Đóng", Size = new Size(90, 32) };
            KhimUiStyle.ApplySecondaryButton(_btnClose);
            _btnClose.Click += BtnClose_Click;

            _btnHighlightRed = new Button { Text = "Highlight Đỏ", Size = new Size(110, 32) };
            KhimUiStyle.ApplySecondaryButton(_btnHighlightRed);
            _btnHighlightRed.BackColor = Color.FromArgb(239, 68, 68); // Red
            _btnHighlightRed.ForeColor = Color.White;
            _btnHighlightRed.Click += BtnHighlightRed_Click;

            _btnResetColor = new Button { Text = "Reset Màu", Size = new Size(95, 32) };
            KhimUiStyle.ApplySecondaryButton(_btnResetColor);
            _btnResetColor.Click += BtnResetColor_Click;

            // Add in reverse order due to FlowDirection.RightToLeft
            pnlFooterButtons.Controls.AddRange(new System.Windows.Forms.Control[] { _btnZoomTo, _btnPass, _btnClose, _btnHighlightRed, _btnResetColor });

            _tabResult.BringToFront();
        }

        private DataGridView CreateAuditGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = KhimUiStyle.CardBorder,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 32 }
            };

            var colTagId = new DataGridViewTextBoxColumn { Name = "colTagId", HeaderText = "Tag ID", Width = 90, ReadOnly = true };
            var colHostId = new DataGridViewTextBoxColumn { Name = "colHostId", HeaderText = "Host ID", Width = 90, ReadOnly = true };
            var colTagText = new DataGridViewTextBoxColumn { Name = "colTagText", HeaderText = "Tag Text", Width = 160, ReadOnly = true };
            var colIssue = new DataGridViewTextBoxColumn { Name = "colIssue", HeaderText = "Issue Description", Width = 280, ReadOnly = true };

            grid.Columns.AddRange(colTagId, colHostId, colTagText, colIssue);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = KhimUiStyle.SecondaryButtonBg,
                ForeColor = KhimUiStyle.TextSecondary,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            grid.ColumnHeadersHeight = 30;

            return grid;
        }

        private void LoadData()
        {
            try
            {
                _allItems = ElementTagsService.GetTaggableCategoriesInView(_doc, _doc.ActiveView);

                _grid.Rows.Clear();
                foreach (var item in _allItems)
                {
                    int r = _grid.Rows.Add();
                    _grid.Rows[r].Cells["colCheck"].Value = item.IsChecked;
                    _grid.Rows[r].Cells["colCategory"].Value = item.CategoryName;
                    _grid.Rows[r].Cells["colColor"].Value = "";

                    var cellCombo = _grid.Rows[r].Cells["colTagType"] as DataGridViewComboBoxCell;
                    if (cellCombo != null)
                    {
                        cellCombo.DataSource = item.AvailableTagSymbols;
                        cellCombo.DisplayMember = "Name";
                        cellCombo.ValueMember = "Self";
                        cellCombo.Value = item.SelectedTagSymbol;
                    }
                    _grid.Rows[r].Tag = item;
                }
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Lỗi Load Dữ Liệu", ex.Message);
            }
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["colColor"].Index)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                var item = _grid.Rows[e.RowIndex].Tag as ElementTagsItem;
                if (item != null)
                {
                    int size = 14;
                    int x = e.CellBounds.Left + (e.CellBounds.Width - size) / 2;
                    int y = e.CellBounds.Top + (e.CellBounds.Height - size) / 2;

                    using (var pen = new Pen(item.TagColor, 3))
                    {
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        e.Graphics.DrawRectangle(pen, x, y, size, size);
                    }
                }
                e.Handled = true;
            }
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["colColor"].Index)
            {
                var item = _grid.Rows[e.RowIndex].Tag as ElementTagsItem;
                if (item != null)
                {
                    using (var dlg = new ColorDialog())
                    {
                        dlg.Color = item.TagColor;
                        if (dlg.ShowDialog() == DialogResult.OK)
                        {
                            item.TagColor = dlg.Color;
                            _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);

                            // Apply Color Override instantly to the View
                            ElementTagsService.ApplyColorOverride(_doc, _doc.ActiveView, new List<ElementTagsItem> { item });
                        }
                    }
                }
            }
        }

        private void BtnTagAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var item = _grid.Rows[i].Tag as ElementTagsItem;
                if (item != null)
                {
                    item.IsChecked = Convert.ToBoolean(_grid.Rows[i].Cells["colCheck"].Value);
                    item.SelectedTagSymbol = _grid.Rows[i].Cells["colTagType"].Value as FamilySymbol;
                }
            }

            var activeSelection = _uidoc.Selection.GetElementIds().ToList();

            try
            {
                int created = ElementTagsService.TagElements(
                    _doc, _doc.ActiveView, _allItems,
                    _chkAddLeader.Checked, _chkOnlyUntagged.Checked,
                    activeSelection);

                string msg = LanguageManager.IsEnglish
                    ? $"Successfully created {created} tags."
                    : $"Đã gắn thành công {created} nhãn Tag.";

                KhimDialogHelper.ShowSuccess("Auto Tag", msg);
                LoadData();
                RunProximityAudit(); // Update audit after creating tags
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Lỗi Gán Tag", ex.Message);
            }
        }

        private void RunProximityAudit()
        {
            try
            {
                double maxDist = 200;
                double.TryParse(_txtMaxErrorDistance.Text, out maxDist);

                List<ElementTagsService.TagProximityError> columnErrors;
                List<ElementTagsService.TagProximityError> wallErrors;
                List<ElementTagsService.TagProximityError> floorErrors;

                ElementTagsService.AuditTagsProximity(_doc, _doc.ActiveView, maxDist, out columnErrors, out wallErrors, out floorErrors);

                _gridColumns.Rows.Clear();
                _gridWalls.Rows.Clear();
                _gridFloors.Rows.Clear();
                _gridIgnored.Rows.Clear();

                _lastAuditedTagIds.Clear();
                _lastAuditedHostIds.Clear();

                // 1. Columns
                foreach (var err in columnErrors)
                {
                    if (_ignoredTagIds.Contains(err.TagId.ToLongValue()))
                    {
                        AddRowToGrid(_gridIgnored, err);
                    }
                    else
                    {
                        AddRowToGrid(_gridColumns, err);
                        _lastAuditedTagIds.Add(err.TagId);
                        _lastAuditedHostIds.Add(err.HostId);
                    }
                }

                // 2. Walls
                foreach (var err in wallErrors)
                {
                    if (_ignoredTagIds.Contains(err.TagId.ToLongValue()))
                    {
                        AddRowToGrid(_gridIgnored, err);
                    }
                    else
                    {
                        AddRowToGrid(_gridWalls, err);
                        _lastAuditedTagIds.Add(err.TagId);
                        _lastAuditedHostIds.Add(err.HostId);
                    }
                }

                // 3. Floors
                foreach (var err in floorErrors)
                {
                    if (_ignoredTagIds.Contains(err.TagId.ToLongValue()))
                    {
                        AddRowToGrid(_gridIgnored, err);
                    }
                    else
                    {
                        AddRowToGrid(_gridFloors, err);
                        _lastAuditedTagIds.Add(err.TagId);
                        _lastAuditedHostIds.Add(err.HostId);
                    }
                }

                // Refresh Tab Header text with counts
                _tabColumns.Text = $"Cột ({_gridColumns.Rows.Count})";
                _tabWalls.Text = $"Vách ({_gridWalls.Rows.Count})";
                _tabFloors.Text = $"Sàn ({_gridFloors.Rows.Count})";
                _tabIgnored.Text = $"Đã bỏ qua ({_gridIgnored.Rows.Count})";

                int activeErrors = _gridColumns.Rows.Count + _gridWalls.Rows.Count + _gridFloors.Rows.Count;
                if (activeErrors > 0)
                {
                    _lblProximityAlert.Text = LanguageManager.IsEnglish
                        ? $"Found {activeErrors} misplaced tags. Highlighted in red in the active view."
                        : $"Đã phát hiện {activeErrors} nhãn Tag bị sai vị trí.";
                    _lblProximityAlert.ForeColor = Color.FromArgb(239, 68, 68); // Red
                }
                else
                {
                    _lblProximityAlert.Text = LanguageManager.IsEnglish
                        ? "Perfect! All tags are within the acceptable proximity of their hosts."
                        : "Tuyệt vời! Tất cả nhãn Tag đều nằm trong phạm vi khoảng cách cho phép.";
                    _lblProximityAlert.ForeColor = Color.ForestGreen;
                }
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Lỗi Rà Soát Khoảng Cách", ex.Message);
            }
        }

        private void AddRowToGrid(DataGridView grid, ElementTagsService.TagProximityError err)
        {
            int r = grid.Rows.Add();
            grid.Rows[r].Cells["colTagId"].Value = err.TagId.ToString();
            grid.Rows[r].Cells["colHostId"].Value = err.HostId.ToString();
            grid.Rows[r].Cells["colTagText"].Value = err.TagText;
            grid.Rows[r].Cells["colIssue"].Value = err.IssueDescription;
            grid.Rows[r].Tag = err;
        }

        private void BtnRunProximityAudit_Click(object sender, EventArgs e)
        {
            RunProximityAudit();
        }

        private void BtnCheckHost_Click(object sender, EventArgs e)
        {
            RunProximityAudit();
        }

        private void BtnClashTag_Click(object sender, EventArgs e)
        {
            try
            {
                int adjusted = ElementTagsService.ResolveClashingTags(_doc, _doc.ActiveView);
                string msg = LanguageManager.IsEnglish
                    ? $"Adjusted {adjusted} overlapping tags."
                    : $"Đã tự động dịch chuyển tránh chồng chéo {adjusted} nhãn Tag.";
                KhimDialogHelper.ShowSuccess("Clash Tag Resolve", msg);
                RunProximityAudit();
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Clash Tag Error", ex.Message);
            }
        }

        private void BtnCheck3d_Click(object sender, EventArgs e)
        {
            var view = _doc.ActiveView;
            if (view is View3D view3d)
            {
                if (view3d.IsLocked)
                {
                    KhimDialogHelper.ShowInfo("Check 3D View", 
                        LanguageManager.IsEnglish 
                            ? "3D View is locked. Tagging is fully supported." 
                            : "Khung nhìn 3D đã khóa. Sẵn sàng gắn tag.");
                }
                else
                {
                    KhimDialogHelper.ShowWarning("Check 3D View", 
                        LanguageManager.IsEnglish 
                            ? "Please LOCK the 3D View before running auto-tag." 
                            : "Vui lòng KHÓA khung nhìn 3D trước khi gán tag.");
                }
            }
            else
            {
                KhimDialogHelper.ShowInfo("Check 3D View", 
                    LanguageManager.IsEnglish 
                        ? "Active view is a 2D View. Auto-tag is fully supported." 
                        : "Khung nhìn hiện tại là 2D. Sẵn sàng gắn tag.");
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            _chkAddLeader.Checked = true;
            _chkOnlyUntagged.Checked = true;
            _ignoredTagIds.Clear();
            _txtMaxErrorDistance.Text = "200";
            LoadData();
            RunProximityAudit();
        }

        private void BtnResetColor_Click(object sender, EventArgs e)
        {
            try
            {
                var allIds = _lastAuditedTagIds.Concat(_lastAuditedHostIds).Distinct().ToList();
                if (allIds.Count > 0)
                {
                    ElementTagsService.ResetElementOverrides(_doc, _doc.ActiveView, allIds);
                }
                _lblProximityAlert.Text = LanguageManager.IsEnglish
                    ? "Reset graphic overrides successfully."
                    : "Đã khôi phục lại màu hiển thị bình thường.";
                _lblProximityAlert.ForeColor = Color.ForestGreen;
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Reset Overrides Error", ex.Message);
            }
        }

        private void BtnHighlightRed_Click(object sender, EventArgs e)
        {
            try
            {
                var allIds = _lastAuditedTagIds.Concat(_lastAuditedHostIds).Distinct().ToList();
                if (allIds.Count > 0)
                {
                    ElementTagsService.ApplyRedOverrideForHostAndTags(_doc, _doc.ActiveView, allIds);
                    _lblProximityAlert.Text = LanguageManager.IsEnglish
                        ? "Highlighted all misplaced tags & hosts in red."
                        : "Đã tô đỏ tất cả Tag và Cột/Tường/Sàn sai trong View hiện hành.";
                    _lblProximityAlert.ForeColor = Color.FromArgb(239, 68, 68);
                }
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Highlight Overrides Error", ex.Message);
            }
        }

        private void BtnZoomTo_Click(object sender, EventArgs e)
        {
            var activeGrid = GetActiveGrid();
            if (activeGrid == null || activeGrid.SelectedRows.Count == 0) return;

            var selectedRow = activeGrid.SelectedRows[0];
            var tagIdStr = selectedRow.Cells["colTagId"].Value?.ToString();
            if (string.IsNullOrEmpty(tagIdStr)) return;

            try
            {
                long val = long.Parse(tagIdStr);
#if NET48
                var elementId = new ElementId((int)val);
#else
                var elementId = new ElementId(val);
#endif
                _uidoc.ShowElements(elementId);
                _uidoc.Selection.SetElementIds(new List<ElementId> { elementId });
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Zoom Error", ex.Message);
            }
        }

        private void BtnPass_Click(object sender, EventArgs e)
        {
            var activeGrid = GetActiveGrid();
            if (activeGrid == null || activeGrid.SelectedRows.Count == 0 || activeGrid == _gridIgnored) return;

            var selectedRow = activeGrid.SelectedRows[0];
            var tagIdStr = selectedRow.Cells["colTagId"].Value?.ToString();
            if (string.IsNullOrEmpty(tagIdStr)) return;

            long tagIdVal = long.Parse(tagIdStr);
            _ignoredTagIds.Add(tagIdVal);
            
            RunProximityAudit();
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private DataGridView GetActiveGrid()
        {
            if (_tabResult.SelectedTab == _tabColumns) return _gridColumns;
            if (_tabResult.SelectedTab == _tabWalls) return _gridWalls;
            if (_tabResult.SelectedTab == _tabFloors) return _gridFloors;
            if (_tabResult.SelectedTab == _tabIgnored) return _gridIgnored;
            return null;
        }
    }
}

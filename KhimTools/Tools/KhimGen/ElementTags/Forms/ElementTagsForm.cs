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

namespace KhimTools.ElementTags.Forms
{
    public class ElementTagsForm : KTBaseForm
    {
        private UIDocument _uidoc;
        private Document _doc;
        private List<ElementTagsItem> _allItems;

        private DataGridView _grid;
        private CheckBox _chkAddLeader;
        private CheckBox _chkOnlyUntagged;
        private Button _btnTagAll;
        private Button _btnCheckHost;
        private Button _btnClashTag;
        private Button _btnCheck3d;
        private Button _btnReset;

        public ElementTagsForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;

            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            // Set Form Properties
            this.Size = new Size(520, 680);
            this.SetFormTitle("Elements Tags", "Auto Tag & Annotation Manager");
            KhimUiStyle.ApplyFormTheme(this);

            // Container Panel
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 60, 16, 16) // Top margin is for the header bar
            };
            this.Controls.Add(pnlContainer);

            // 1. DataGridView Configuration
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

            // Custom column configurations
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colCheck",
                HeaderText = "✔",
                Width = 40,
                FlatStyle = FlatStyle.Flat
            };
            var colCategory = new DataGridViewTextBoxColumn
            {
                Name = "colCategory",
                HeaderText = "CATEGORY",
                Width = 160,
                ReadOnly = true
            };
            var colColor = new DataGridViewTextBoxColumn
            {
                Name = "colColor",
                HeaderText = "COLOR",
                Width = 70,
                ReadOnly = true
            };
            var colTagType = new DataGridViewComboBoxColumn
            {
                Name = "colTagType",
                HeaderText = "TAG TYPE",
                Width = 210,
                FlatStyle = FlatStyle.Flat
            };

            _grid.Columns.AddRange(colCheck, colCategory, colColor, colTagType);

            // Apply Header Styling
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

            pnlContainer.Controls.Add(_grid);

            // 2. Bottom Control Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 200,
                Padding = new Padding(0, 10, 0, 0)
            };
            pnlContainer.Controls.Add(pnlBottom);

            // Checkbox Container
            var pnlOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight
            };
            pnlBottom.Controls.Add(pnlOptions);

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

            // Actions Layout (Matching mockup)
            var pnlActions = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 0)
            };
            pnlBottom.Controls.Add(pnlActions);

            // Large Blue tag button
            _btnTagAll = new Button
            {
                Text = "TAG ALL SELECTED",
                Height = 44,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 8)
            };
            KhimUiStyle.ApplyPrimaryButton(_btnTagAll);
            _btnTagAll.Click += BtnTagAll_Click;
            pnlActions.Controls.Add(_btnTagAll);

            // Sub Action Area
            var pnlSubActions = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };
            pnlActions.Controls.Add(pnlSubActions);

            // Left Block (Holds Check Host, Clash Tag, Check 3D)
            var pnlLeftButtons = new Panel
            {
                Dock = DockStyle.Left,
                Width = 380
            };
            pnlSubActions.Controls.Add(pnlLeftButtons);

            _btnCheckHost = new Button
            {
                Text = "Check tag host 2d",
                Location = new Point(0, 8),
                Size = new Size(185, 34)
            };
            KhimUiStyle.ApplySecondaryButton(_btnCheckHost);
            _btnCheckHost.Click += BtnCheckHost_Click;

            _btnClashTag = new Button
            {
                Text = "Clash tag",
                Location = new Point(195, 8),
                Size = new Size(185, 34)
            };
            KhimUiStyle.ApplySecondaryButton(_btnClashTag);
            _btnClashTag.Click += BtnClashTag_Click;

            _btnCheck3d = new Button
            {
                Text = "Check 3d",
                Location = new Point(0, 48),
                Size = new Size(380, 34)
            };
            KhimUiStyle.ApplySecondaryButton(_btnCheck3d);
            _btnCheck3d.Click += BtnCheck3d_Click;

            pnlLeftButtons.Controls.AddRange(new System.Windows.Forms.Control[] { _btnCheckHost, _btnClashTag, _btnCheck3d });

            // Right Block (Vertical Reset Button)
            _btnReset = new Button
            {
                Text = "RESET",
                Dock = DockStyle.Right,
                Width = 90,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(8, 8, 0, 0)
            };
            KhimUiStyle.ApplySecondaryButton(_btnReset);
            _btnReset.Click += BtnReset_Click;
            pnlSubActions.Controls.Add(_btnReset);

            // Make sure DGV stands in the center
            _grid.BringToFront();
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

                    // Populate ComboBox for Tag Type
                    var cellCombo = _grid.Rows[r].Cells["colTagType"] as DataGridViewComboBoxCell;
                    if (cellCombo != null)
                    {
                        cellCombo.DataSource = item.AvailableTagSymbols;
                        cellCombo.DisplayMember = "Name";
                        cellCombo.ValueMember = "Self"; // We can bind the FamilySymbol object itself
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
                    // Draw a premium hollow square with custom color matching mockup
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
            // Sync values from grid to models
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
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Lỗi Gán Tag", ex.Message);
            }
        }

        private void BtnCheckHost_Click(object sender, EventArgs e)
        {
            try
            {
                List<ElementId> orphanIds;
                List<ElementId> invisibleHostIds;
                List<ElementId> tooFarIds;

                ElementTagsService.CheckTagsStatus(_doc, _doc.ActiveView, out orphanIds, out invisibleHostIds, out tooFarIds);

                int totalErrors = orphanIds.Count + invisibleHostIds.Count + tooFarIds.Count;

                if (totalErrors > 0)
                {
                    // Select all problematic tags in Revit view so the user can easily find them
                    var allProblematicIds = orphanIds.Concat(invisibleHostIds).Concat(tooFarIds).Distinct().ToList();
                    _uidoc.Selection.SetElementIds(allProblematicIds);

                    string report = LanguageManager.IsEnglish
                        ? $"[TAG QUALITY AUDIT REPORT]\n" +
                          $"- Orphan tags (no host): {orphanIds.Count}\n" +
                          $"- Tags with invisible host: {invisibleHostIds.Count}\n" +
                          $"- Tags placed too far from host: {tooFarIds.Count}\n\n" +
                          $"All {allProblematicIds.Count} problematic tags have been SELECTED in the active view."
                        : $"[BÁO CÁO RÀ SOÁT CHẤT LƯỢNG TAG]\n" +
                          $"• Tag mồ côi (mất Host): {orphanIds.Count} vị trí\n" +
                          $"• Tag có Host bị ẩn trong view: {invisibleHostIds.Count} vị trí\n" +
                          $"• Tag nằm quá xa cấu kiện (sai vị trí): {tooFarIds.Count} vị trí\n\n" +
                          $"➔ Đã CHỌN (SELECT) tất cả {allProblematicIds.Count} Tag lỗi trong View để bạn rà soát và chỉnh sửa.";

                    KhimDialogHelper.ShowWarning("Check Tag Host & Position", report);
                }
                else
                {
                    string msg = LanguageManager.IsEnglish
                        ? "Perfect! All tags in current view have valid hosts and are correctly positioned."
                        : "Tuyệt vời! Tất cả các Tag trong View hiện hành đều gắn đúng Host, Host đang hiển thị và nằm đúng vị trí thể hiện.";
                    KhimDialogHelper.ShowInfo("Check Tag Host & Position", msg);
                }
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Check Tag Error", ex.Message);
            }
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
                            ? "Please LOCK the 3D View before running auto-tag (padlock icon at the bottom of the View window)." 
                            : "Vui lòng KHÓA khung nhìn 3D (bấm biểu tượng ổ khóa ở thanh trạng thái dưới cùng màn hình) trước khi gán tag.");
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
            LoadData();
        }
    }
}

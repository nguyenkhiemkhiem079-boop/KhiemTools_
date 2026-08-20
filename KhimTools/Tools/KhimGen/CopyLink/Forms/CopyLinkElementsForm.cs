using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.CopyLink.Models;
using KhimTools.CopyLink.Services;
using KhimTools.Core;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using CheckBox = System.Windows.Forms.CheckBox;
using ComboBox = System.Windows.Forms.ComboBox;
using ComboBoxStyle = System.Windows.Forms.ComboBoxStyle;
using CheckedListBox = System.Windows.Forms.CheckedListBox;

namespace KhimTools.CopyLink.Forms
{
    public class CopyLinkElementsForm : Form
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private List<LinkInstanceInfo> _allLinkInstances = new List<LinkInstanceInfo>();
        private List<LinkCategoryItem> _currentCategories = new List<LinkCategoryItem>();

        // UI Controls
        private ComboBox _cmbRevitLinks;
        private Button _btnPickLink;
        private TextBox _txtSearchCategory;
        private Button _btnSelectAll;
        private Button _btnClearAll;
        private CheckedListBox _clbCategories;
        private Label _lblSummary;
        private Button _btnCopy;
        private Button _btnClose;

        public LinkInstanceInfo SelectedLinkInstance => _cmbRevitLinks.SelectedItem as LinkInstanceInfo;
        public List<LinkCategoryItem> SelectedCategories
        {
            get
            {
                var list = new List<LinkCategoryItem>();
                for (int i = 0; i < _clbCategories.Items.Count; i++)
                {
                    if (_clbCategories.GetItemChecked(i) && _clbCategories.Items[i] is LinkCategoryItem item)
                    {
                        list.Add(item);
                    }
                }
                return list;
            }
        }

        public CopyLinkElementsForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc?.Document;

            BuildUi();
            LoadLinkInstances();
        }

        private void BuildUi()
        {
            bool isEn = LanguageManager.IsEnglish;
            Text = isEn ? "Khim Tools - Copy Elements from Revit Link" : "Khim Tools - Sao Chép Element từ Revit Link";
            Width = 460;
            Height = 620;
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

            _btnCopy = new Button
            {
                Text = isEn ? "Copy Elements" : "Tiến hành Copy",
                Width = 140,
                Height = 35,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnCopy.FlatAppearance.BorderSize = 0;
            _btnCopy.Click += (s, e) => ExecuteCopy();

            _btnClose = new Button
            {
                Text = isEn ? "Close" : "Đóng",
                Width = 90,
                Height = 35,
                BackColor = Color.FromArgb(225, 228, 232),
                FlatStyle = FlatStyle.Flat
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            bottomPanel.Controls.Add(_btnCopy);
            bottomPanel.Controls.Add(_btnClose);
            bottomPanel.Resize += (s, e) =>
            {
                _btnCopy.Left = bottomPanel.Width - _btnCopy.Width - 15;
                _btnClose.Left = _btnCopy.Left - _btnClose.Width - 10;
                _btnCopy.Top = 10;
                _btnClose.Top = 10;
            };
            Controls.Add(bottomPanel);

            // ══════════════════════════════════════════════════════════════════
            // 2. MAIN CONTAINER
            // ══════════════════════════════════════════════════════════════════
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 12, 15, 10)
            };

            // 2.1 Revit Link Selection
            var lblRevitLink = new Label
            {
                Text = "Revit Link:",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            mainPanel.Controls.Add(lblRevitLink);

            var pnlLinkRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(0, 0, 0, 4)
            };

            _cmbRevitLinks = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _cmbRevitLinks.SelectedIndexChanged += (s, e) => OnLinkSelectedChanged();

            _btnPickLink = new Button
            {
                Text = isEn ? "Pick on Screen" : "Pick Link",
                Dock = DockStyle.Right,
                Width = 95,
                FlatStyle = FlatStyle.System
            };
            _btnPickLink.Click += (s, e) => PickLinkOnScreen();

            pnlLinkRow.Controls.Add(_cmbRevitLinks);
            pnlLinkRow.Controls.Add(_btnPickLink);
            mainPanel.Controls.Add(pnlLinkRow);

            // 2.2 Category Header & Search
            var lblCategory = new Label
            {
                Text = "Category:",
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(0, 8, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            mainPanel.Controls.Add(lblCategory);

            var pnlSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(0, 0, 0, 4)
            };

            _txtSearchCategory = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F)
            };
            _txtSearchCategory.TextChanged += (s, e) => FilterCategories();

            var pnlSelectButtons = new Panel
            {
                Dock = DockStyle.Right,
                Width = 145
            };

            _btnSelectAll = new Button
            {
                Text = isEn ? "All" : "Chọn hết",
                Width = 68,
                Height = 26,
                Left = 4,
                Top = 0,
                FlatStyle = FlatStyle.System
            };
            _btnSelectAll.Click += (s, e) => SetAllChecked(true);

            _btnClearAll = new Button
            {
                Text = isEn ? "Clear" : "Bỏ chọn",
                Width = 68,
                Height = 26,
                Left = 76,
                Top = 0,
                FlatStyle = FlatStyle.System
            };
            _btnClearAll.Click += (s, e) => SetAllChecked(false);

            pnlSelectButtons.Controls.Add(_btnSelectAll);
            pnlSelectButtons.Controls.Add(_btnClearAll);

            pnlSearch.Controls.Add(_txtSearchCategory);
            pnlSearch.Controls.Add(pnlSelectButtons);
            mainPanel.Controls.Add(pnlSearch);

            // 2.3 Category CheckedListBox
            _clbCategories = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            _clbCategories.ItemCheck += (s, e) => BeginInvoke((MethodInvoker)UpdateSummary);
            mainPanel.Controls.Add(_clbCategories);

            // 2.4 Summary Label Bottom
            _lblSummary = new Label
            {
                Text = "Đã chọn: 0 category (0 elements)",
                Dock = DockStyle.Bottom,
                Height = 24,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };
            mainPanel.Controls.Add(_lblSummary);

            Controls.Add(mainPanel);
        }

        private void LoadLinkInstances()
        {
            _allLinkInstances = LinkElementCopyService.GetLinkInstances(_doc);
            _cmbRevitLinks.Items.Clear();

            foreach (var link in _allLinkInstances)
            {
                _cmbRevitLinks.Items.Add(link);
            }

            if (_cmbRevitLinks.Items.Count > 0)
            {
                _cmbRevitLinks.SelectedIndex = 0;
            }
            else
            {
                _clbCategories.Items.Clear();
                _lblSummary.Text = "Không tìm thấy file Revit Link nào trong dự án!";
            }
        }

        private void OnLinkSelectedChanged()
        {
            if (SelectedLinkInstance == null || SelectedLinkInstance.LinkDocument == null)
            {
                _currentCategories.Clear();
                _clbCategories.Items.Clear();
                UpdateSummary();
                return;
            }

            _currentCategories = LinkElementCopyService.GetCategoriesWithElements(SelectedLinkInstance.LinkDocument);
            FilterCategories();
        }

        private void FilterCategories()
        {
            _clbCategories.Items.Clear();
            string filter = (_txtSearchCategory.Text ?? "").Trim().ToLowerInvariant();

            var filtered = string.IsNullOrEmpty(filter)
                ? _currentCategories
                : _currentCategories.Where(c => c.CategoryName.ToLowerInvariant().Contains(filter)).ToList();

            foreach (var cat in filtered)
            {
                _clbCategories.Items.Add(cat, false);
            }

            UpdateSummary();
        }

        private void SetAllChecked(bool isChecked)
        {
            for (int i = 0; i < _clbCategories.Items.Count; i++)
            {
                _clbCategories.SetItemChecked(i, isChecked);
            }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var selected = SelectedCategories;
            int totalElems = selected.Sum(c => c.ElementCount);
            _lblSummary.Text = $"Đã chọn: {selected.Count} Category ({totalElems} đối tượng)";
            _btnCopy.Enabled = totalElems > 0;
        }

        private void PickLinkOnScreen()
        {
            Hide();
            try
            {
                Reference pickedRef = _uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new LinkSelectionFilter(),
                    "Chọn một Revit Link trên màn hình:");

                if (pickedRef != null && _doc.GetElement(pickedRef) is RevitLinkInstance linkInst)
                {
                    var match = _allLinkInstances.FirstOrDefault(l => l.InstanceId == linkInst.Id);
                    if (match != null)
                    {
                        _cmbRevitLinks.SelectedItem = match;
                    }
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

        private void ExecuteCopy()
        {
            var linkInfo = SelectedLinkInstance;
            if (linkInfo == null || linkInfo.LinkDocument == null)
            {
                TaskDialog.Show("Khim Tools", "Vui lòng chọn một file Revit Link hợp lệ.");
                return;
            }

            var categories = SelectedCategories;
            if (!categories.Any())
            {
                TaskDialog.Show("Khim Tools", "Vui lòng tích chọn ít nhất một Category cần sao chép.");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private class LinkSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is RevitLinkInstance;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.GridLevel.Models;
using KhimTools.GridLevel.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using CheckBox = System.Windows.Forms.CheckBox;
using GroupBox = System.Windows.Forms.GroupBox;
using NumericUpDown = System.Windows.Forms.NumericUpDown;
using TabControl = System.Windows.Forms.TabControl;
using TabPage = System.Windows.Forms.TabPage;
using DataGridView = System.Windows.Forms.DataGridView;
using DataGridViewTextBoxColumn = System.Windows.Forms.DataGridViewTextBoxColumn;
using DataGridViewCheckBoxColumn = System.Windows.Forms.DataGridViewCheckBoxColumn;

namespace KhimTools.GridLevel.Forms
{
    public class AutoGridPlanForm : Form
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        // Tab 1: Grid Controls
        private CheckBox _chkCreateGrids;
        private TextBox _txtXStartName;
        private TextBox _txtXSpacings;
        private NumericUpDown _numXExtension;
        private CheckBox _chkXBubble0;
        private CheckBox _chkXBubble1;

        private TextBox _txtYStartName;
        private TextBox _txtYSpacings;
        private NumericUpDown _numYExtension;
        private CheckBox _chkYBubble0;
        private CheckBox _chkYBubble1;

        private NumericUpDown _numOriginX;
        private NumericUpDown _numOriginY;
        private NumericUpDown _numRotation;
        private Button _btnPickPoint;
        private CheckBox _chkCreateDimensions;

        // Tab 2: Level Controls
        private CheckBox _chkCreateLevels;
        private DataGridView _dgvLevels;
        private Button _btnAddLevel;
        private Button _btnAddTypicalFloors;
        private Button _btnDeleteLevel;

        // Bottom Buttons
        private Button _btnExecute;
        private Button _btnCancel;

        public GridSettings GridsResult { get; private set; } = new GridSettings();
        public List<LevelItem> LevelsResult { get; private set; } = new List<LevelItem>();
        public bool ShouldCreateGrids => _chkCreateGrids.Checked;
        public bool ShouldCreateLevels => _chkCreateLevels.Checked;

        public AutoGridPlanForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc?.Document;

            BuildUi();
            PopulateDefaultLevels();
        }

        private void BuildUi()
        {
            bool isEn = LanguageManager.IsEnglish;
            Text = isEn ? "Auto Grid & Floor Plan Generator" : "Tạo Hệ Lưới Trục & Mặt Bằng Tự Động";
            Width = 780;
            Height = 660;
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

            _btnExecute = new Button
            {
                Text = isEn ? "Create Grids & Plans" : "Tạo Lưới Trục & Mặt Bằng",
                Width = 190,
                Height = 35,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnExecute.FlatAppearance.BorderSize = 0;
            _btnExecute.Click += (s, e) => OnExecuteClick();

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

            bottomPanel.Controls.Add(_btnExecute);
            bottomPanel.Controls.Add(_btnCancel);
            bottomPanel.Resize += (s, e) =>
            {
                _btnExecute.Left = bottomPanel.Width - _btnExecute.Width - 15;
                _btnCancel.Left = _btnExecute.Left - _btnCancel.Width - 10;
                _btnExecute.Top = 10;
                _btnCancel.Top = 10;
            };
            Controls.Add(bottomPanel);

            // ══════════════════════════════════════════════════════════════════
            // 2. MAIN TAB CONTROL
            // ══════════════════════════════════════════════════════════════════
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new System.Drawing.Point(16, 8),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            // ── TAB 1: HỆ LƯỚI TRỤC (GRIDS) ──
            var tabGrids = new TabPage
            {
                Text = isEn ? "1. Grids Setup" : "1. Thiết Lập Lưới Trục (Grids)",
                BackColor = Color.White,
                Padding = new Padding(15)
            };
            BuildGridsTab(tabGrids);
            tabControl.TabPages.Add(tabGrids);

            // ── TAB 2: CAO ĐỘ TẦNG & MẶT BẰNG (LEVELS & PLANS) ──
            var tabLevels = new TabPage
            {
                Text = isEn ? "2. Levels & Floor Plans" : "2. Cao Độ Tầng & Mặt Bằng",
                BackColor = Color.White,
                Padding = new Padding(15)
            };
            BuildLevelsTab(tabLevels);
            tabControl.TabPages.Add(tabLevels);

            Controls.Add(tabControl);
        }

        private void BuildGridsTab(TabPage page)
        {
            bool isEn = LanguageManager.IsEnglish;

            _chkCreateGrids = new CheckBox
            {
                Text = isEn ? "Generate Grids in Project" : "Tạo Hệ Lưới Trục Trong Dự Án",
                Checked = true,
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            page.Controls.Add(_chkCreateGrids);

            var pnlContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 5, 0, 0) };

            // Group 1: Trục Phương Dọc (Trục X)
            var grpX = new GroupBox
            {
                Text = isEn ? "Vertical Grids (X-Axis)" : "Trục Phương Dọc (Trục X: 1, 2, 3...)",
                Dock = DockStyle.Top,
                Height = 135,
                Padding = new Padding(12, 10, 12, 10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            var lblXStart = new Label { Text = isEn ? "Start Name:" : "Tên trục đầu:", Left = 15, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtXStartName = new TextBox { Text = "1", Left = 120, Top = 24, Width = 60, Font = new Font("Segoe UI", 9F) };

            var lblXSpacings = new Label { Text = isEn ? "Spacings (mm):" : "Khoảng cách (mm):", Left = 200, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtXSpacings = new TextBox { Text = "6000, 7200, 6000, 7200, 6000", Left = 320, Top = 24, Width = 380, Font = new Font("Segoe UI", 9F) };

            var lblXExt = new Label { Text = isEn ? "Extension (mm):" : "Khoảng nhô đầu trục:", Left = 15, Top = 64, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numXExtension = new NumericUpDown { Minimum = 500, Maximum = 10000, Value = 2000, Increment = 500, Left = 150, Top = 60, Width = 80 };

            _chkXBubble0 = new CheckBox { Text = isEn ? "Bubble Bottom (End 0)" : "Hiện Bubble Đầu Dưới (End 0)", Checked = true, Left = 260, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _chkXBubble1 = new CheckBox { Text = isEn ? "Bubble Top (End 1)" : "Hiện Bubble Đầu Trên (End 1)", Checked = false, Left = 480, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };

            grpX.Controls.Add(lblXStart);
            grpX.Controls.Add(_txtXStartName);
            grpX.Controls.Add(lblXSpacings);
            grpX.Controls.Add(_txtXSpacings);
            grpX.Controls.Add(lblXExt);
            grpX.Controls.Add(_numXExtension);
            grpX.Controls.Add(_chkXBubble0);
            grpX.Controls.Add(_chkXBubble1);
            pnlContent.Controls.Add(grpX);

            // Group 2: Trục Phương Ngang (Trục Y)
            var grpY = new GroupBox
            {
                Text = isEn ? "Horizontal Grids (Y-Axis)" : "Trục Phương Ngang (Trục Y: A, B, C...)",
                Dock = DockStyle.Top,
                Height = 135,
                Padding = new Padding(12, 10, 12, 10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Margin = new Padding(0, 10, 0, 0)
            };

            var lblYStart = new Label { Text = isEn ? "Start Name:" : "Tên trục đầu:", Left = 15, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtYStartName = new TextBox { Text = "A", Left = 120, Top = 24, Width = 60, Font = new Font("Segoe UI", 9F) };

            var lblYSpacings = new Label { Text = isEn ? "Spacings (mm):" : "Khoảng cách (mm):", Left = 200, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _txtYSpacings = new TextBox { Text = "5000, 4500, 5000", Left = 320, Top = 24, Width = 380, Font = new Font("Segoe UI", 9F) };

            var lblYExt = new Label { Text = isEn ? "Extension (mm):" : "Khoảng nhô đầu trục:", Left = 15, Top = 64, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numYExtension = new NumericUpDown { Minimum = 500, Maximum = 10000, Value = 2000, Increment = 500, Left = 150, Top = 60, Width = 80 };

            _chkYBubble0 = new CheckBox { Text = isEn ? "Bubble Left (End 0)" : "Hiện Bubble Bên Trái (End 0)", Checked = true, Left = 260, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _chkYBubble1 = new CheckBox { Text = isEn ? "Bubble Right (End 1)" : "Hiện Bubble Bên Phải (End 1)", Checked = false, Left = 480, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };

            grpY.Controls.Add(lblYStart);
            grpY.Controls.Add(_txtYStartName);
            grpY.Controls.Add(lblYSpacings);
            grpY.Controls.Add(_txtYSpacings);
            grpY.Controls.Add(lblYExt);
            grpY.Controls.Add(_numYExtension);
            grpY.Controls.Add(_chkYBubble0);
            grpY.Controls.Add(_chkYBubble1);
            pnlContent.Controls.Add(grpY);

            // Group 3: Tọa Độ & Kích Thước
            var grpPos = new GroupBox
            {
                Text = isEn ? "Origin & Annotations" : "Gốc Tọa Độ & Kích Thước (Dimensions)",
                Dock = DockStyle.Top,
                Height = 110,
                Padding = new Padding(12, 10, 12, 10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Margin = new Padding(0, 10, 0, 0)
            };

            var lblOrigX = new Label { Text = "X0 (mm):", Left = 15, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numOriginX = new NumericUpDown { Minimum = -999999999, Maximum = 999999999, Value = 0, Increment = 1000, Left = 75, Top = 25, Width = 95 };

            var lblOrigY = new Label { Text = "Y0 (mm):", Left = 175, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numOriginY = new NumericUpDown { Minimum = -999999999, Maximum = 999999999, Value = 0, Increment = 1000, Left = 235, Top = 25, Width = 95 };

            var lblRot = new Label { Text = isEn ? "Rotation (°):" : "Góc xoay (°):", Left = 335, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            _numRotation = new NumericUpDown { Minimum = -360, Maximum = 360, Value = 0, Increment = 15, Left = 425, Top = 25, Width = 65 };

            _btnPickPoint = new Button
            {
                Text = isEn ? "Pick Point on View" : "Pick Điểm Gốc",
                Left = 505,
                Top = 22,
                Width = 150,
                Height = 28,
                FlatStyle = FlatStyle.System
            };
            _btnPickPoint.Click += (s, e) => PickOriginPoint();

            _chkCreateDimensions = new CheckBox
            {
                Text = isEn ? "Automatically place continuous dimensions along grids" : "Tự động đánh đường kích thước (Dimensions) liên hoàn",
                Checked = true,
                Left = 15,
                Top = 68,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.DarkGreen
            };

            grpPos.Controls.Add(lblOrigX);
            grpPos.Controls.Add(_numOriginX);
            grpPos.Controls.Add(lblOrigY);
            grpPos.Controls.Add(_numOriginY);
            grpPos.Controls.Add(lblRot);
            grpPos.Controls.Add(_numRotation);
            grpPos.Controls.Add(_btnPickPoint);
            grpPos.Controls.Add(_chkCreateDimensions);
            pnlContent.Controls.Add(grpPos);

            page.Controls.Add(pnlContent);
        }

        private void BuildLevelsTab(TabPage page)
        {
            bool isEn = LanguageManager.IsEnglish;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 42 };
            _chkCreateLevels = new CheckBox
            {
                Text = isEn ? "Generate Levels & Plans in Project" : "Tạo Cao Độ Tầng & Mặt Bằng Trong Dự Án",
                Checked = true,
                Left = 0,
                Top = 6,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };

            var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 360, FlowDirection = FlowDirection.RightToLeft };
            _btnDeleteLevel = new Button { Text = isEn ? "Delete" : "Xóa Tầng", Width = 80, Height = 28, FlatStyle = FlatStyle.System, Margin = new Padding(3, 3, 3, 3) };
            _btnAddTypicalFloors = new Button { Text = isEn ? "+ Typical (Nx)" : "+ Nhân Bản Tầng", Width = 115, Height = 28, FlatStyle = FlatStyle.System, Margin = new Padding(3, 3, 3, 3) };
            _btnAddLevel = new Button { Text = isEn ? "+ Add Level" : "+ Thêm Tầng", Width = 95, Height = 28, FlatStyle = FlatStyle.System, Margin = new Padding(3, 3, 3, 3) };

            _btnAddLevel.Click += (s, e) => AddSingleLevel();
            _btnAddTypicalFloors.Click += (s, e) => AddTypicalFloors();
            _btnDeleteLevel.Click += (s, e) => DeleteSelectedLevel();

            pnlButtons.Controls.Add(_btnDeleteLevel);
            pnlButtons.Controls.Add(_btnAddTypicalFloors);
            pnlButtons.Controls.Add(_btnAddLevel);

            pnlTop.Controls.Add(_chkCreateLevels);
            pnlTop.Controls.Add(pnlButtons);
            page.Controls.Add(pnlTop);

            // DataGridView for Levels
            _dgvLevels = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9F),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };

            _dgvLevels.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 248);
            _dgvLevels.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            _dgvLevels.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgvLevels.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvLevels.RowTemplate.Height = 28;

            _dgvLevels.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = isEn ? "Level Name" : "Tên Tầng", Width = 170 });
            _dgvLevels.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = isEn ? "Elevation (mm)" : "Cao Độ (mm)", Width = 120 });
            _dgvLevels.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = isEn ? "Height (mm)" : "Chiều Cao (mm)", Width = 110 });
            _dgvLevels.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = isEn ? "Struct Plan" : "MB Kết Cấu", Width = 110 });
            _dgvLevels.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = isEn ? "Floor Plan" : "MB Kiến Trúc", Width = 110 });
            _dgvLevels.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = isEn ? "Ceiling Plan" : "MB Trần (RCP)", Width = 110 });

            _dgvLevels.CellValueChanged += DgvLevels_CellValueChanged;
            page.Controls.Add(_dgvLevels);
        }

        private void PopulateDefaultLevels()
        {
            _dgvLevels.Rows.Clear();
            _dgvLevels.Rows.Add("Tầng Hầm B1", -3300, 3300, true, true, false);
            _dgvLevels.Rows.Add("Tầng 1", 0, 4200, true, true, false);
            _dgvLevels.Rows.Add("Tầng 2", 4200, 3600, true, true, false);
            _dgvLevels.Rows.Add("Tầng 3", 7800, 3600, true, true, false);
            _dgvLevels.Rows.Add("Tầng Mái", 11400, 3600, true, true, false);
        }

        private void DgvLevels_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Khi người dùng đổi Chiều cao tầng (cột 2) -> Tự động tính toán lại cao độ các tầng trên
            if (e.ColumnIndex == 2)
            {
                RecalculateElevations();
            }
        }

        private void RecalculateElevations()
        {
            if (_dgvLevels.Rows.Count <= 1) return;

            double currentElev = 0;
            if (double.TryParse(_dgvLevels.Rows[0].Cells[1].Value?.ToString(), out double startElev))
            {
                currentElev = startElev;
            }

            for (int i = 0; i < _dgvLevels.Rows.Count; i++)
            {
                _dgvLevels.Rows[i].Cells[1].Value = currentElev;
                if (double.TryParse(_dgvLevels.Rows[i].Cells[2].Value?.ToString(), out double h))
                {
                    currentElev += h;
                }
            }
        }

        private void AddSingleLevel()
        {
            int nextIdx = _dgvLevels.Rows.Count + 1;
            double lastElev = 0;
            double defaultH = 3600;

            if (_dgvLevels.Rows.Count > 0)
            {
                var lastRow = _dgvLevels.Rows[_dgvLevels.Rows.Count - 1];
                if (double.TryParse(lastRow.Cells[1].Value?.ToString(), out double e) &&
                    double.TryParse(lastRow.Cells[2].Value?.ToString(), out double h))
                {
                    lastElev = e + h;
                    defaultH = h;
                }
            }

            _dgvLevels.Rows.Add($"Tầng {nextIdx}", lastElev, defaultH, true, true, false);
        }

        private void AddTypicalFloors()
        {
            string input = KhimPrompt.ShowDialog("Nhập số lượng tầng điển hình cần thêm (VD: 5):", "Thêm Tầng Điển Hình", "5");
            if (int.TryParse(input, out int count) && count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    AddSingleLevel();
                }
            }
        }

        private void DeleteSelectedLevel()
        {
            if (_dgvLevels.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in _dgvLevels.SelectedRows)
                {
                    _dgvLevels.Rows.Remove(row);
                }
                RecalculateElevations();
            }
        }

        private void PickOriginPoint()
        {
            Hide();
            try
            {
                // Cho phép bắt điểm đa dạng (Endpoints, Intersections, Midpoints, Centers, Nearest)
                ObjectSnapTypes snapTypes = ObjectSnapTypes.Endpoints |
                                            ObjectSnapTypes.Intersections |
                                            ObjectSnapTypes.Midpoints |
                                            ObjectSnapTypes.Centers |
                                            ObjectSnapTypes.Nearest;

                XYZ pt = _uidoc.Selection.PickPoint(snapTypes, "Pick điểm gốc tọa độ chèn hệ trục (X0, Y0):");
                if (pt != null)
                {
                    double xMm = UnitUtils.ConvertFromInternalUnits(pt.X, UnitTypeId.Millimeters);
                    double yMm = UnitUtils.ConvertFromInternalUnits(pt.Y, UnitTypeId.Millimeters);

                    decimal decX = (decimal)Math.Round(xMm, 1);
                    decimal decY = (decimal)Math.Round(yMm, 1);

                    if (decX < _numOriginX.Minimum) _numOriginX.Minimum = decX - 10000000m;
                    if (decX > _numOriginX.Maximum) _numOriginX.Maximum = decX + 10000000m;
                    _numOriginX.Value = decX;

                    if (decY < _numOriginY.Minimum) _numOriginY.Minimum = decY - 10000000m;
                    if (decY > _numOriginY.Maximum) _numOriginY.Maximum = decY + 10000000m;
                    _numOriginY.Value = decY;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Khim Tools — Pick Point",
                    "Không thể pick điểm trên khung nhìn hiện tại. Vui lòng mở Mặt Bằng (Floor Plan / Structural Plan) để pick điểm gốc chèn hệ trục.\n\nChi tiết: " + ex.Message);
            }
            finally
            {
                Show();
                BringToFront();
            }
        }

        private void OnExecuteClick()
        {
            if (!_chkCreateGrids.Checked && !_chkCreateLevels.Checked)
            {
                TaskDialog.Show("Khim Tools", "Vui lòng tích chọn ít nhất 1 mục: Tạo Lưới Trục hoặc Tạo Cao Độ Tầng.");
                return;
            }

            // 1. Thu thập dữ liệu Grids
            GridsResult = new GridSettings
            {
                CreateGrids = _chkCreateGrids.Checked,
                XStartName = _txtXStartName.Text.Trim(),
                XSpacingsString = _txtXSpacings.Text.Trim(),
                XExtensionMm = (double)_numXExtension.Value,
                XShowBubbleEnd0 = _chkXBubble0.Checked,
                XShowBubbleEnd1 = _chkXBubble1.Checked,

                YStartName = _txtYStartName.Text.Trim(),
                YSpacingsString = _txtYSpacings.Text.Trim(),
                YExtensionMm = (double)_numYExtension.Value,
                YShowBubbleEnd0 = _chkYBubble0.Checked,
                YShowBubbleEnd1 = _chkYBubble1.Checked,

                Origin = new XYZ(
                    UnitUtils.ConvertToInternalUnits((double)_numOriginX.Value, UnitTypeId.Millimeters),
                    UnitUtils.ConvertToInternalUnits((double)_numOriginY.Value, UnitTypeId.Millimeters),
                    0),
                RotationDegrees = (double)_numRotation.Value,
                CreateDimensions = _chkCreateDimensions.Checked
            };

            // 2. Thu thập dữ liệu Levels
            LevelsResult.Clear();
            foreach (DataGridViewRow row in _dgvLevels.Rows)
            {
                string name = row.Cells[0].Value?.ToString() ?? "";
                double.TryParse(row.Cells[1].Value?.ToString(), out double elev);
                double.TryParse(row.Cells[2].Value?.ToString(), out double height);
                bool structPlan = (bool)(row.Cells[3].Value ?? true);
                bool floorPlan = (bool)(row.Cells[4].Value ?? true);
                bool ceilingPlan = (bool)(row.Cells[5].Value ?? false);

                LevelsResult.Add(new LevelItem
                {
                    LevelName = name,
                    ElevationMm = elev,
                    StoryHeightMm = height,
                    CreateStructuralPlan = structPlan,
                    CreateFloorPlan = floorPlan,
                    CreateCeilingPlan = ceilingPlan
                });
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

using KhimTools.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Core;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Control = System.Windows.Forms.Control;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using FontStyle = System.Drawing.FontStyle;
using Button = System.Windows.Forms.Button;
using GroupBox = System.Windows.Forms.GroupBox;
using Label = System.Windows.Forms.Label;
using CheckBox = System.Windows.Forms.CheckBox;

using KhimTools.Core;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Forms
{
    public class ProjectCoverSetupForm : KTBaseForm
    {
        private readonly Document _doc;

        private CheckBox _chkColumns;
        private NumericUpDown _numColumnCover;

        private CheckBox _chkBeams;
        private NumericUpDown _numBeamCover;

        private CheckBox _chkSlabs;
        private NumericUpDown _numSlabCover;

        private CheckBox _chkFoundations;
        private NumericUpDown _numFoundationCover;

        private Button _btnApply;
        private Button _btnClose;

        public ProjectCoverSetupForm(Document doc)
        {
            _doc = doc;
            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            LoadCurrentProjectCovers();
        }

        private void BuildUi()
        {
            Text = "KHIM TOOLS — Project Concrete Cover Setup";
            Width = 600;
            Height = 490;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Project Cover Setup",
                "Synchronize Concrete Cover Settings across All Project Structural Categories",
                "v2.5 Pro");
            Controls.Add(header);
            MinimizeBox = false;

            // Header Banner
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(240, 244, 250), Padding = new Padding(12) };
            var lblTitle = new Label
            {
                Text = "Cấu Hình Lớp Bê Tông Bảo Vệ (Concrete Cover) Toàn Dự Án",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 40, 80),
                Dock = DockStyle.Top,
                Height = 22
            };
            var lblSub = new Label
            {
                Text = "Chọn tham số Cover cần gán đồng bộ cho từng loại cấu kiện trong mô hình Revit:",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.DimGray,
                Dock = DockStyle.Top,
                Height = 20
            };
            headerPanel.Controls.Add(lblSub);
            headerPanel.Controls.Add(lblTitle);
            Controls.Add(headerPanel);

            // Bottom Action Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.FromArgb(245, 245, 247) };
            _btnApply = new Button
            {
                Text = "Áp Dụng Cho Dự Án",
                Width = 160,
                Height = 36,
                Top = 10,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _btnApply.FlatAppearance.BorderSize = 0;

            _btnClose = new Button
            {
                Text = "Đóng",
                Width = 90,
                Height = 36,
                Top = 10,
                BackColor = Color.FromArgb(225, 225, 230),
                FlatStyle = FlatStyle.Flat
            };
            _btnClose.FlatAppearance.BorderSize = 0;

            _btnApply.Click += BtnApply_Click;
            _btnClose.Click += (s, e) => Close();

            bottomPanel.Controls.Add(_btnApply);
            bottomPanel.Controls.Add(_btnClose);
            bottomPanel.Resize += (s, e) =>
            {
                _btnClose.Left = bottomPanel.Width - _btnClose.Width - 15;
                _btnApply.Left = _btnClose.Left - _btnApply.Width - 10;
            };
            Controls.Add(bottomPanel);

            // Center Form Controls
            var grpCategory = new GroupBox { Text = "Bảng Cài Đặt Cover Theo Loại Cấu Kiện", Dock = DockStyle.Fill, Padding = new Padding(12) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            // Headers
            table.Controls.Add(new Label { Text = "Loại Cấu Kiện", Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            table.Controls.Add(new Label { Text = "Giá Trị Cover (mm)", Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            table.Controls.Add(new Label { Text = "Cập Nhật", Font = new Font("Segoe UI", 9F, FontStyle.Bold) });

            // 1. Cột
            _chkColumns = new CheckBox { Text = "Thực hiện", Checked = true, AutoSize = true };
            _numColumnCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 90 };
            AddRow(table, "⏹ Cột (Structural Columns)", _numColumnCover, _chkColumns);

            // 2. Dầm
            _chkBeams = new CheckBox { Text = "Thực hiện", Checked = true, AutoSize = true };
            _numBeamCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 90 };
            AddRow(table, "📏 Dầm (Structural Framing)", _numBeamCover, _chkBeams);

            // 3. Sàn
            _chkSlabs = new CheckBox { Text = "Thực hiện", Checked = true, AutoSize = true };
            _numSlabCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 15, Increment = 5, Width = 90 };
            AddRow(table, "🔲 Sàn (Structural Floors)", _numSlabCover, _chkSlabs);

            // 4. Móng
            _chkFoundations = new CheckBox { Text = "Thực hiện", Checked = true, AutoSize = true };
            _numFoundationCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 50, Increment = 5, Width = 90 };
            AddRow(table, "Móng (Structural Foundations)", _numFoundationCover, _chkFoundations);

            grpCategory.Controls.Add(table);
            Controls.Add(grpCategory);
            grpCategory.BringToFront();
        }

        private void AddRow(TableLayoutPanel table, string catLabel, Control numInput, Control chkControl)
        {
            table.Controls.Add(new Label { Text = catLabel, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) });
            table.Controls.Add(numInput);
            table.Controls.Add(chkControl);
        }

        private void LoadCurrentProjectCovers()
        {
            try
            {
                // Thử tìm cover mẫu của 1 cột trong model để set value ban đầu
                var col = new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().FirstOrDefault();
                if (col != null)
                {
                    double feet = RebarCoverHelper.GetColumnCover(col);
                    double mm = RebarCoverHelper.ToMm(feet);
                    if (mm >= 10 && mm <= 100) _numColumnCover.Value = (decimal)Math.Round(mm);
                }
            }
            catch { }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            using var tx = new Transaction(_doc, "Setup Project Concrete Cover");
            tx.Start();
            try
            {
                int totalApplied = 0;
                string resultSummary = "";

                if (_chkColumns.Checked)
                {
                    var ct = RebarCoverHelper.GetOrCreateCoverType(_doc, (double)_numColumnCover.Value);
                    int n = RebarCoverHelper.ApplyCoverToCategory(_doc, BuiltInCategory.OST_StructuralColumns, ct);
                    totalApplied += n;
                    resultSummary += $"• Cột (Structural Columns): {n} đối tượng -> Cover {(double)_numColumnCover.Value}mm\n";
                }

                if (_chkBeams.Checked)
                {
                    var ct = RebarCoverHelper.GetOrCreateCoverType(_doc, (double)_numBeamCover.Value);
                    int n = RebarCoverHelper.ApplyCoverToCategory(_doc, BuiltInCategory.OST_StructuralFraming, ct);
                    totalApplied += n;
                    resultSummary += $"• Dầm (Structural Framing): {n} đối tượng -> Cover {(double)_numBeamCover.Value}mm\n";
                }

                if (_chkSlabs.Checked)
                {
                    var ct = RebarCoverHelper.GetOrCreateCoverType(_doc, (double)_numSlabCover.Value);
                    int n = RebarCoverHelper.ApplyCoverToCategory(_doc, BuiltInCategory.OST_Floors, ct);
                    totalApplied += n;
                    resultSummary += $"• Sàn (Structural Floors): {n} đối tượng -> Cover {(double)_numSlabCover.Value}mm\n";
                }

                if (_chkFoundations.Checked)
                {
                    var ct = RebarCoverHelper.GetOrCreateCoverType(_doc, (double)_numFoundationCover.Value);
                    int n = RebarCoverHelper.ApplyCoverToCategory(_doc, BuiltInCategory.OST_StructuralFoundation, ct);
                    totalApplied += n;
                    resultSummary += $"• Móng (Structural Foundations): {n} đối tượng -> Cover {(double)_numFoundationCover.Value}mm\n";
                }

                tx.Commit();

                MessageBox.Show(this, $"Đã cập nhật Lớp bê tông bảo vệ thành công cho {totalApplied} đối tượng trong dự án:\n\n" + resultSummary,
                    "Hoàn thành Cấu hình Cover", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                tx.RollBack();
                MessageBox.Show(this, "Lỗi khi cập nhật Cover cho dự án: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

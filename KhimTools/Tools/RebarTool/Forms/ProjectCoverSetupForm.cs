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
    public class ProjectCoverSetupForm : Form
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
            Text = "KHIM TOOLS ΓÇö Project Concrete Cover Setup";
            Width = 600;
            Height = 490;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS ΓÇö Project Cover Setup",
                "Synchronize Concrete Cover Settings across All Project Structural Categories",
                "v2.5 Pro");
            Controls.Add(header);
            MinimizeBox = false;

            // Header Banner
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(240, 244, 250), Padding = new Padding(12) };
            var lblTitle = new Label
            {
                Text = "Cß║Ñu H├¼nh Lß╗¢p B├¬ T├┤ng Bß║úo Vß╗ç (Concrete Cover) To├án Dß╗▒ ├ün",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 40, 80),
                Dock = DockStyle.Top,
                Height = 22
            };
            var lblSub = new Label
            {
                Text = "Chß╗ìn tham sß╗æ Cover cß║ºn g├ín ─æß╗ông bß╗Ö cho tß╗½ng loß║íi cß║Ñu kiß╗çn trong m├┤ h├¼nh Revit:",
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
                Text = "├üp Dß╗Ñng Cho Dß╗▒ ├ün",
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
                Text = "─É├│ng",
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
            var grpCategory = new GroupBox { Text = "Bß║úng C├ái ─Éß║╖t Cover Theo Loß║íi Cß║Ñu Kiß╗çn", Dock = DockStyle.Fill, Padding = new Padding(12) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            // Headers
            table.Controls.Add(new Label { Text = "Loß║íi Cß║Ñu Kiß╗çn", Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            table.Controls.Add(new Label { Text = "Gi├í Trß╗ï Cover (mm)", Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            table.Controls.Add(new Label { Text = "Cß║¡p Nhß║¡t", Font = new Font("Segoe UI", 9F, FontStyle.Bold) });

            // 1. Cß╗Öt
            _chkColumns = new CheckBox { Text = "Thß╗▒c hiß╗çn", Checked = true, AutoSize = true };
            _numColumnCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 90 };
            AddRow(table, "ΓÅ╣ Cß╗Öt (Structural Columns)", _numColumnCover, _chkColumns);

            // 2. Dß║ºm
            _chkBeams = new CheckBox { Text = "Thß╗▒c hiß╗çn", Checked = true, AutoSize = true };
            _numBeamCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 25, Increment = 5, Width = 90 };
            AddRow(table, "≡ƒôÅ Dß║ºm (Structural Framing)", _numBeamCover, _chkBeams);

            // 3. S├án
            _chkSlabs = new CheckBox { Text = "Thß╗▒c hiß╗çn", Checked = true, AutoSize = true };
            _numSlabCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 15, Increment = 5, Width = 90 };
            AddRow(table, "≡ƒö▓ S├án (Structural Floors)", _numSlabCover, _chkSlabs);

            // 4. M├│ng
            _chkFoundations = new CheckBox { Text = "Thß╗▒c hiß╗çn", Checked = true, AutoSize = true };
            _numFoundationCover = new NumericUpDown { Minimum = 10, Maximum = 100, Value = 50, Increment = 5, Width = 90 };
            AddRow(table, "M├│ng (Structural Foundations)", _numFoundationCover, _chkFoundations);

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
                // Thß╗¡ t├¼m cover mß║½u cß╗ºa 1 cß╗Öt trong model ─æß╗â set value ban ─æß║ºu
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
                    resultSummary += $"ΓÇó Cß╗Öt (Structural Columns): {n} ─æß╗æi t╞░ß╗úng -> Cover {(double)_numColumnCover.Value}mm\n";
                }

                if (_chkBeams.Checked)
                {
                    var ct = RebarCoverHelper.GetOrCreateCoverType(_doc, (double)_numBeamCover.Value);
                    int n = RebarCoverHelper.ApplyCoverToCategory(_doc, BuiltInCategory.OST_StructuralFraming, ct);
                    totalApplied += n;
                    resultSummary += $"ΓÇó Dß║ºm (Structural Framing): {n} ─æß╗æi t╞░ß╗úng -> Cover {(double)_numBeamCover.Value}mm\n";
                }

                if (_chkSlabs.Checked)
                {
                    var ct = RebarCoverHelper.GetOrCreateCoverType(_doc, (double)_numSlabCover.Value);
                    int n = RebarCoverHelper.ApplyCoverToCategory(_doc, BuiltInCategory.OST_Floors, ct);
                    totalApplied += n;
                    resultSummary += $"ΓÇó S├án (Structural Floors): {n} ─æß╗æi t╞░ß╗úng -> Cover {(double)_numSlabCover.Value}mm\n";
                }

                if (_chkFoundations.Checked)
                {
                    var ct = RebarCoverHelper.GetOrCreateCoverType(_doc, (double)_numFoundationCover.Value);
                    int n = RebarCoverHelper.ApplyCoverToCategory(_doc, BuiltInCategory.OST_StructuralFoundation, ct);
                    totalApplied += n;
                    resultSummary += $"ΓÇó M├│ng (Structural Foundations): {n} ─æß╗æi t╞░ß╗úng -> Cover {(double)_numFoundationCover.Value}mm\n";
                }

                tx.Commit();

                MessageBox.Show(this, $"─É├ú cß║¡p nhß║¡t Lß╗¢p b├¬ t├┤ng bß║úo vß╗ç th├ánh c├┤ng cho {totalApplied} ─æß╗æi t╞░ß╗úng trong dß╗▒ ├ín:\n\n" + resultSummary,
                    "Ho├án th├ánh Cß║Ñu h├¼nh Cover", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                tx.RollBack();
                MessageBox.Show(this, "Lß╗ùi khi cß║¡p nhß║¡t Cover cho dß╗▒ ├ín: " + ex.Message, "Lß╗ùi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

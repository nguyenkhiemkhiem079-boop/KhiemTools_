using System;
using System.Drawing;
using System.Windows.Forms;

namespace KhimTools.Core
{
    /// <summary>
    /// Bộ Design System & Styling Manager chuẩn thương mại cho toàn bộ giao diện KhimTools.
    /// Neutral work surfaces with blue actions and no injected brand banners.
    /// </summary>
    public static class KhimUiStyle
    {
        // ── Brand Palette ──────────────────────────────────────────────────
        public static readonly Color HeaderBg = Color.FromArgb(32, 33, 36);       // #202124 Neutral charcoal
        public static readonly Color HeaderAccent = Color.FromArgb(22, 119, 210);
        public static readonly Color FormBg = Color.FromArgb(245, 247, 250);
        public static readonly Color CardBg = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(215, 219, 224);  // #D7DBE0
        public static readonly Color TextPrimary = Color.FromArgb(32, 38, 46);    // #20262E
        public static readonly Color TextSecondary = Color.FromArgb(95, 99, 104);// #5F6368
        public static readonly Color PrimaryButtonBg = Color.FromArgb(22, 119, 210);
        public static readonly Color PrimaryButtonHover = Color.FromArgb(18, 98, 176);
        public static readonly Color CreateButtonBg = PrimaryButtonBg;
        public static readonly Color SecondaryButtonBg = Color.FromArgb(241, 245, 249);
        public static readonly Color SecondaryButtonHover = Color.FromArgb(226, 232, 240);
        public static readonly Color InputBorder = Color.FromArgb(203, 213, 225);
        public static readonly Color SelectionBg = Color.FromArgb(219, 234, 254);
        public static readonly Color SelectionText = Color.FromArgb(30, 64, 175);

        // ── Form Theme ─────────────────────────────────────────────────────
        public static void ApplyFormTheme(Form form)
        {
            if (form == null) return;
            form.BackColor = FormBg;
            form.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.KeyPreview = true;
            form.MinimumSize = new Size(Math.Min(420, Math.Max(1, form.Width)), Math.Min(320, Math.Max(1, form.Height)));
            form.Padding = form.Padding == Padding.Empty ? new Padding(1) : form.Padding;

            StyleControlTree(form);
            if (!(form is UI.KTBaseForm))
            {
                form.Shown -= OnFormShown;
                form.Shown += OnFormShown;
            }
        }

        private static void OnFormShown(object sender, EventArgs e) => StyleControlTree(sender as Control);

        private static void OnControlAdded(object sender, ControlEventArgs e)
        {
            StyleControlTree(e.Control);
        }

        public static void StyleControlTree(Control root)
        {
            if (root == null) return;

            StyleControl(root);
            root.ControlAdded -= OnControlAdded;
            root.ControlAdded += OnControlAdded;

            foreach (Control child in root.Controls)
            {
                StyleControlTree(child);
            }
        }

        private static void StyleControl(Control control)
        {
            if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = Color.White;
                textBox.ForeColor = TextPrimary;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = Color.White;
                comboBox.ForeColor = TextPrimary;
                comboBox.IntegralHeight = false;
                comboBox.DropDownHeight = 240;
            }
            else if (control is NumericUpDown numeric)
            {
                numeric.BorderStyle = BorderStyle.FixedSingle;
                numeric.BackColor = Color.White;
                numeric.ForeColor = TextPrimary;
                numeric.TextAlign = HorizontalAlignment.Right;
            }
            else if (control is DataGridView grid)
            {
                StyleDataGrid(grid);
            }
            else if (control is CheckedListBox checkedList)
            {
                checkedList.BackColor = CardBg;
                checkedList.ForeColor = TextPrimary;
                checkedList.BorderStyle = BorderStyle.FixedSingle;
                checkedList.CheckOnClick = true;
                checkedList.IntegralHeight = false;
            }
            else if (control is ListBox list)
            {
                list.BackColor = CardBg;
                list.ForeColor = TextPrimary;
                list.BorderStyle = BorderStyle.FixedSingle;
                list.IntegralHeight = false;
            }
            else if (control is TabControl tabs)
            {
                tabs.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular);
                tabs.Padding = new Point(14, 5);
            }
            else if (control is GroupBox group)
            {
                ApplyCardStyle(group);
            }
            else if (control is TabPage tabPage)
            {
                tabPage.BackColor = FormBg;
                tabPage.ForeColor = TextPrimary;
                tabPage.Padding = new Padding(10);
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.ForeColor = TextPrimary;
                checkBox.AutoEllipsis = true;
            }
            else if (control is RadioButton radioButton)
            {
                radioButton.ForeColor = TextPrimary;
                radioButton.AutoEllipsis = true;
            }
            else if (control is Button button && button.FlatStyle != FlatStyle.Flat)
            {
                ApplySecondaryButton(button);
            }
            else if (control is Label label && label.ForeColor == SystemColors.ControlText)
            {
                label.ForeColor = TextPrimary;
            }
        }

        public static void StyleDataGrid(DataGridView grid)
        {
            if (grid == null) return;
            grid.BackgroundColor = CardBg;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = CardBorder;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.ColumnHeadersHeight = Math.Max(34, grid.ColumnHeadersHeight);
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = Math.Max(30, grid.RowTemplate.Height);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.MultiSelect = true;
            grid.DefaultCellStyle.BackColor = CardBg;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = SelectionBg;
            grid.DefaultCellStyle.SelectionForeColor = SelectionText;
            grid.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
        }

        // ── Card Style for GroupBoxes ───────────────────────────────────────
        public static void ApplyCardStyle(GroupBox grp, Color? titleColor = null)
        {
            if (grp == null) return;
            grp.BackColor = CardBg;
            grp.ForeColor = titleColor ?? TextPrimary;
            grp.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grp.Padding = new Padding(12, 10, 12, 12);
        }

        // ── Primary Action Button Styling ────────────────────────────────────
        public static void ApplyPrimaryButton(Button btn, Color? customBg = null)
        {
            if (btn == null) return;
            Color bg = customBg ?? PrimaryButtonBg;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.UseVisualStyleBackColor = false;
            btn.FlatAppearance.MouseOverBackColor = PrimaryButtonHover;
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(15, 82, 148);
        }

        // ── Secondary Action Button Styling ──────────────────────────────────
        public static void ApplySecondaryButton(Button btn)
        {
            if (btn == null) return;
            btn.BackColor = SecondaryButtonBg;
            btn.ForeColor = TextPrimary;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = CardBorder;
            btn.FlatAppearance.BorderSize = 1;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btn.Cursor = Cursors.Hand;
            btn.UseVisualStyleBackColor = false;
            btn.FlatAppearance.MouseOverBackColor = SecondaryButtonHover;
            btn.FlatAppearance.MouseDownBackColor = SelectionBg;
        }
    }
}

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KhimTools.RebarTool.Forms
{
    internal static class RebarLayout
    {
        internal static Control[] Field(string caption, Control input)
        {
            input.AccessibleName = caption;
            return new Control[] { new Label { Text = caption, AutoSize = true }, input };
        }

        internal static void Fields(GroupBox group, params Control[][] rows)
        {
            var retained = rows.SelectMany(row => row).ToArray();
            foreach (var control in retained) control.Parent?.Controls.Remove(control);
            foreach (var old in group.Controls.Cast<Control>().ToArray()) old.Dispose();
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            foreach (var row in rows)
            {
                int index = table.RowCount++;
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                for (int col = 0; col < row.Length; col++)
                {
                    var control = row[col];
                    control.Dock = DockStyle.Fill;
                    control.Margin = new Padding(4, 6, 12, 6);
                    control.Font = SystemFonts.MessageBoxFont;
                    if (control is Label label) { label.AutoSize = true; label.UseMnemonic = false; label.TextAlign = ContentAlignment.MiddleLeft; }
                    if (control is CheckBox check) { check.AutoSize = true; check.UseMnemonic = false; check.AutoEllipsis = false; }
                    table.Controls.Add(control, col, index);
                }
                if (row.Length == 1) table.SetColumnSpan(row[0], 2);
            }
            group.AutoSize = true;
            group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            group.Controls.Add(table);
        }

        internal static void Stack(Control host, params Control[] items)
        {
            foreach (var item in items) item.Parent?.Controls.Remove(item);
            foreach (var old in host.Controls.Cast<Control>().ToArray()) old.Dispose();
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var item in items)
            {
                item.Dock = DockStyle.Fill;
                item.Margin = new Padding(4, 4, 4, 10);
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                table.Controls.Add(item, 0, table.RowCount++);
            }
            if (host is ScrollableControl scroll) scroll.AutoScroll = true;
            host.Controls.Add(table);
        }

        internal static Panel Footer(ComboBox language, params Button[] buttons)
        {
            var panel = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(12, 8, 12, 8) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            if (language != null)
            {
                language.Dock = DockStyle.None;
                language.Anchor = AnchorStyles.Left;
                language.Width = 120;
                language.AccessibleName = "Language";
                layout.Controls.Add(language, 0, 0);
            }
            var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right };
            foreach (var button in buttons)
            {
                button.AutoSize = true;
                button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                button.MinimumSize = new Size(button.Width, 38);
                button.Padding = new Padding(10, 4, 10, 4);
                button.Margin = new Padding(8, 0, 0, 0);
                actions.Controls.Add(button);
            }
            layout.Controls.Add(actions, 1, 0);
            panel.Controls.Add(layout);
            return panel;
        }

        internal static void PresetBar(Panel panel, Label label, ComboBox combo, params Button[] buttons)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 + buttons.Length };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            label.Anchor = AnchorStyles.Left;
            layout.Controls.Add(label, 0, 0);
            combo.Dock = DockStyle.Fill;
            layout.Controls.Add(combo, 1, 0);
            for (int index = 0; index < buttons.Length; index++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                buttons[index].AutoSize = true;
                buttons[index].Anchor = AnchorStyles.Left;
                layout.Controls.Add(buttons[index], index + 2, 0);
            }
            panel.Controls.Add(layout);
        }

        internal static Panel ScrollPreview(Panel canvas, Size minimumSize)
        {
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            canvas.Dock = DockStyle.None;
            canvas.MinimumSize = minimumSize;
            canvas.Size = minimumSize;
            viewport.Controls.Add(canvas);
            viewport.Layout += (sender, args) =>
            {
                var size = new Size(Math.Max(canvas.MinimumSize.Width, viewport.ClientSize.Width),
                    Math.Max(canvas.MinimumSize.Height, viewport.ClientSize.Height));
                if (canvas.Size != size) canvas.Size = size;
            };
            return viewport;
        }

        internal static void ColumnEditor(TabPage page, Panel settings, Panel preview)
        {
            settings.Parent = null;
            preview.Parent = null;
            settings.Dock = DockStyle.Fill;
            settings.AutoScroll = false;
            settings.AutoSize = true;
            settings.MinimumSize = new Size(400, 0);
            preview.MinimumSize = new Size(420, 480);
            preview.Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            page.AutoScroll = true;
            page.Controls.Add(layout);
            bool? wide = null;
            EventHandler arrange = (sender, args) =>
            {
                bool next = page.ClientSize.Width - page.Padding.Horizontal >=
                    settings.MinimumSize.Width + preview.MinimumSize.Width + 16;
                if (wide == next) return;
                wide = next;
                layout.SuspendLayout();
                layout.Controls.Clear();
                layout.ColumnStyles.Clear();
                layout.RowStyles.Clear();
                layout.ColumnCount = next ? 2 : 1;
                layout.RowCount = next ? 1 : 2;
                if (next) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, settings.MinimumSize.Width));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                if (!next) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(settings, 0, 0);
                layout.Controls.Add(preview, next ? 1 : 0, next ? 0 : 1);
                layout.ResumeLayout(true);
            };
            page.SizeChanged += arrange;
            arrange(null, EventArgs.Empty);
        }

        internal static void EnableFullTypeNames(Form form)
        {
            var tooltip = new ToolTip { ShowAlways = true, AutoPopDelay = 10000 };
            form.Disposed += (sender, args) => tooltip.Dispose();
            ConfigureTypeNames(form, tooltip);
        }

        private static void ConfigureTypeNames(Control root, ToolTip tooltip)
        {
            if (root is ComboBox combo)
            {
                combo.DropDown += (sender, args) =>
                {
                    int width = combo.Width;
                    foreach (var item in combo.Items)
                        width = Math.Max(width, TextRenderer.MeasureText(combo.GetItemText(item), combo.Font).Width + 32);
                    combo.DropDownWidth = Math.Min(width, Screen.FromControl(combo).WorkingArea.Width);
                };
                combo.SelectedIndexChanged += (sender, args) => tooltip.SetToolTip(combo, combo.Text);
                tooltip.SetToolTip(combo, combo.Text);
            }
            if (root is ListBox list) list.HorizontalScrollbar = true;
            foreach (Control child in root.Controls) ConfigureTypeNames(child, tooltip);
        }

        // Column forms share vertical option groups. Disable flow-to-a-hidden-column
        // and let the containing scroll viewport handle height instead.
        internal static void FitColumnGroups(Control root)
        {
            foreach (Control child in root.Controls.Cast<Control>().ToArray()) FitColumnGroups(child);
            if (root is FlowLayoutPanel flow && flow.FlowDirection == FlowDirection.TopDown)
            {
                flow.WrapContents = false;
                flow.AutoSize = true;
                flow.Dock = DockStyle.Top;
                EventHandler fit = (sender, args) =>
                {
                    foreach (Control item in flow.Controls)
                    {
                        if (item is Label || item is ButtonBase)
                            item.MaximumSize = new Size(Math.Max(80, flow.ClientSize.Width - item.Margin.Horizontal - 4), 0);
                        if (item is NumericUpDown numeric)
                            item.MinimumSize = new Size(0, numeric.PreferredHeight);
                    }
                };
                flow.SizeChanged += fit;
                fit(null, EventArgs.Empty);
            }
            if (root is TableLayoutPanel table && table.ColumnCount == 2 && table.Controls.OfType<Label>().Any())
            {
                table.ColumnStyles.Clear();
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
                table.AutoSize = true;
                table.Dock = DockStyle.Top;
                table.Padding = new Padding(0, 0, 0, 4);
                table.RowStyles.Clear();
                foreach (Control item in table.Controls)
                {
                    item.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                    if (item is Button button)
                    {
                        button.AutoSize = true;
                        button.MinimumSize = new Size(0, 32);
                    }
                }
            }
            if (root is GroupBox group && group.Controls.Count == 1 &&
                (group.Controls[0] is FlowLayoutPanel || group.Controls[0] is TableLayoutPanel))
            {
                group.AutoSize = true;
                group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                group.MinimumSize = new Size(0, group.Height);
            }
            if (root is TabControl tabs) tabs.Multiline = true;
            if (root is ListBox list) list.HorizontalScrollbar = true;
        }
    }
}

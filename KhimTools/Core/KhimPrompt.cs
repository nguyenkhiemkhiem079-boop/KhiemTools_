using System;
using System.Drawing;
using System.Windows.Forms;
using KhimTools.Core.UI;

namespace KhimTools.Core
{
    public static class KhimPrompt
    {
        public static string ShowDialog(string text, string caption, string defaultValue = "")
        {
            using (var prompt = new KTBaseForm())
            {
                prompt.Width = 430;
                prompt.Height = 210;
                prompt.MinimumSize = new Size(360, 200);
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.SetFormTitle(caption, text);

                var body = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(16, 14, 16, 14),
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = KhimUiStyle.FormBg
                };
                body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                body.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

                var textLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = text,
                    AutoSize = true,
                    ForeColor = KhimUiStyle.TextSecondary,
                    Margin = new Padding(0, 0, 0, 8)
                };
                var textBox = new TextBox { Dock = DockStyle.Top, Text = defaultValue, Margin = new Padding(0) };

                var actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Padding = new Padding(0, 6, 0, 0)
                };
                var confirmation = new Button { Text = "OK", Width = 96, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Hủy", Width = 96, DialogResult = DialogResult.Cancel };
                KhimUiStyle.ApplyPrimaryButton(confirmation);
                KhimUiStyle.ApplySecondaryButton(cancel);
                actions.Controls.Add(confirmation);
                actions.Controls.Add(cancel);

                body.Controls.Add(textLabel, 0, 0);
                body.Controls.Add(textBox, 0, 1);
                body.Controls.Add(actions, 0, 2);
                prompt.Controls.Add(body);
                body.BringToFront();

                prompt.AcceptButton = confirmation;
                prompt.CancelButton = cancel;

                return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
            }
        }
    }
}

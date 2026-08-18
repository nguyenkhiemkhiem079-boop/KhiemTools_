using System;
using System.Windows.Forms;

namespace KhimTools.Core
{
    public static class KhimPrompt
    {
        public static string ShowDialog(string text, string caption, string defaultValue = "")
        {
            using (var prompt = new Form())
            {
                prompt.Width = 380;
                prompt.Height = 150;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = caption;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                var textLabel = new Label { Left = 16, Top = 16, Text = text, Width = 330 };
                var textBox = new TextBox { Left = 16, Top = 40, Width = 330, Text = defaultValue };
                
                var confirmation = new Button { Text = "OK", Left = 160, Width = 85, Height = 28, Top = 72, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.System };
                var cancel = new Button { Text = "Cancel", Left = 255, Width = 85, Height = 28, Top = 72, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.System };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(cancel);

                prompt.AcceptButton = confirmation;
                prompt.CancelButton = cancel;

                return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
            }
        }
    }
}

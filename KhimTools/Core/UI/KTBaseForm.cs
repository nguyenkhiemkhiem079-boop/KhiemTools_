using System;
using System.Drawing;
using System.Windows.Forms;

namespace KhimTools.Core.UI
{
    /// <summary>
    /// Common WinForms shell. It deliberately does not inject branded content so each
    /// tool owns one header only and compact dialogs retain standard Windows chrome.
    /// </summary>
    public class KTBaseForm : Form
    {
        public KTBaseForm()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = KhimUiStyle.FormBg;
            KeyPreview = true;
            ShowIcon = false;
            MaximizeBox = false;
            MinimizeBox = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);

            Shown += (s, e) => KhimUiStyle.StyleControlTree(this);
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && !ContainsFocusedDropDown())
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
        }

        public void SetFormTitle(string title, string subtitle = "")
        {
            Text = title;
            AccessibleName = string.IsNullOrWhiteSpace(subtitle) ? title : title + " - " + subtitle;
        }

        protected override void OnShown(EventArgs e)
        {
            KhimUiStyle.StyleControlTree(this);
            base.OnShown(e);
        }

        private bool ContainsFocusedDropDown()
        {
            foreach (Control control in Controls)
            {
                if (HasFocusedDropDown(control)) return true;
            }
            return false;
        }

        private static bool HasFocusedDropDown(Control root)
        {
            if (root is ComboBox combo && combo.DroppedDown) return true;
            foreach (Control child in root.Controls)
            {
                if (HasFocusedDropDown(child)) return true;
            }
            return false;
        }
    }
}

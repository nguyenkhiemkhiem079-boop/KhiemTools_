using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KhimTools.Core.UI
{
    /// <summary>
    /// Base Form chuẩn cho tất cả giao diện K-TOOLS:
    /// - Không có Title Bar mặc định thô kệch của Windows (FormBorderStyle = None).
    /// - Tích hợp Header bar hiện đại, nút Đóng (X), kéo di chuyển form mượt mà.
    /// - Viền 1px tinh tế chống chìm vào nền Revit.
    /// - Tự động bo góc nhẹ 6px hiện đại.
    /// </summary>
    public class KTBaseForm : Form
    {
        // Win32 API để kéo di chuyển form bằng chuột
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        // Giao diện
        protected Panel HeaderPanel;
        protected Label LblHeaderTitle;
        protected Label LblHeaderSubtitle;
        protected Button BtnCloseHeader;
        protected Color BorderColor = ColorTranslator.FromHtml("#CBD5E1");
        protected Color HeaderColor = ColorTranslator.FromHtml("#202124");

        public KTBaseForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = KhimUiStyle.FormBg;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            InitializeBaseHeader();
            Shown += (s, e) => KhimUiStyle.StyleControlTree(this);
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && !ContainsFocusInDropDown())
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
        }

        private bool ContainsFocusInDropDown()
        {
            foreach (Control control in Controls)
            {
                if (control is ComboBox combo && combo.DroppedDown) return true;
            }
            return false;
        }

        private void InitializeBaseHeader()
        {
            HeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = HeaderColor,
                Padding = new Padding(12, 0, 8, 0)
            };
            HeaderPanel.MouseDown += Header_MouseDown;

            // Nút Đóng (X)
            BtnCloseHeader = new Button
            {
                Text = "×",
                AccessibleName = "Đóng cửa sổ",
                Size = new Size(40, 40),
                Location = new Point(Width - 48, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = ColorTranslator.FromHtml("#94A3B8"),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            BtnCloseHeader.FlatAppearance.BorderSize = 0;
            BtnCloseHeader.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#EF4444");
            BtnCloseHeader.MouseEnter += (s, e) => { BtnCloseHeader.ForeColor = Color.White; };
            BtnCloseHeader.MouseLeave += (s, e) => { BtnCloseHeader.ForeColor = ColorTranslator.FromHtml("#94A3B8"); };
            BtnCloseHeader.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // Tiêu đề
            LblHeaderTitle = new Label
            {
                Text = "K-TOOLS",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 8),
                Cursor = Cursors.Default
            };
            LblHeaderTitle.MouseDown += Header_MouseDown;

            // Phụ đề
            LblHeaderSubtitle = new Label
            {
                Text = "Revit Automation Suite",
                ForeColor = ColorTranslator.FromHtml("#94A3B8"),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(16, 29),
                Cursor = Cursors.Default
            };
            LblHeaderSubtitle.MouseDown += Header_MouseDown;

            HeaderPanel.Controls.Add(BtnCloseHeader);
            HeaderPanel.Controls.Add(LblHeaderTitle);
            HeaderPanel.Controls.Add(LblHeaderSubtitle);

            Controls.Add(HeaderPanel);
        }

        protected override void OnShown(EventArgs e)
        {
            FormBorderStyle = FormBorderStyle.None;
            HeaderPanel.BringToFront();
            KhimUiStyle.StyleControlTree(this);
            base.OnShown(e);
        }

        public void SetFormTitle(string title, string subtitle = "")
        {
            if (LblHeaderTitle != null)
            {
                LblHeaderTitle.Text = title;
            }
            if (LblHeaderSubtitle != null)
            {
                if (!string.IsNullOrEmpty(subtitle))
                {
                    LblHeaderSubtitle.Text = subtitle;
                    LblHeaderSubtitle.Visible = true;
                    LblHeaderTitle.Location = new Point(16, 8);
                }
                else
                {
                    LblHeaderSubtitle.Visible = false;
                    LblHeaderTitle.Location = new Point(16, 16);
                }
            }
            this.Text = title;
        }

        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Vẽ viền 1px bo ngoài form
            using (var pen = new Pen(BorderColor, 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (BtnCloseHeader != null)
            {
                BtnCloseHeader.Location = new Point(Width - 48, 6);
            }
        }
    }
}

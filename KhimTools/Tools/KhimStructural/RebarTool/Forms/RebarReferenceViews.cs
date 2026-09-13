using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KhimTools.RebarTool.Forms
{
    internal enum RebarReferenceKind { Beam, Slab, Foundation, RectangularColumn, CircularColumn }

    internal static class RebarReferenceViews
    {
        internal static TabPage CreatePage(RebarReferenceKind kind)
        {
            var page = new TabPage("Tham khảo 2D") { BackColor = Color.White };
            page.Controls.Add(Create(kind));
            return page;
        }

        internal static Control Create(RebarReferenceKind kind)
        {
            var tabs = new TabControl { Dock = DockStyle.Fill, Multiline = true };
            string[] names = { "Mặt bằng", "Cắt dọc", "Cắt ngang", "Neo / Nối" };
            for (int i = 0; i < names.Length; i++)
            {
                var page = new TabPage(names[i]) { BackColor = Color.White };
                var diagram = new ReferenceCanvas(kind, i) { AccessibleName = names[i], MinimumSize = new Size(540, 360) };
                page.Controls.Add(RebarLayout.ScrollPreview(diagram, diagram.MinimumSize));
                tabs.TabPages.Add(page);
            }
            return tabs;
        }

        private sealed class ReferenceCanvas : Panel
        {
            private readonly RebarReferenceKind _kind;
            private readonly int _view;
            internal ReferenceCanvas(RebarReferenceKind kind, int view)
            {
                _kind = kind;
                _view = view;
                DoubleBuffered = true;
                ResizeRedraw = true;
                BackColor = Color.White;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Fixed drawing coordinates preserve proportions and keep annotations separate.
                float scale = Math.Min(ClientSize.Width / 540f, ClientSize.Height / 360f);
                g.TranslateTransform((ClientSize.Width - 540 * scale) / 2, (ClientSize.Height - 360 * scale) / 2);
                g.ScaleTransform(scale, scale);
                using (var concrete = new Pen(Color.FromArgb(104, 115, 125), 1.5f))
                using (var bar = new Pen(Color.FromArgb(0, 122, 194), 3))
                using (var secondary = new Pen(Color.FromArgb(0, 150, 120), 2))
                using (var dimension = new Pen(Color.FromArgb(104, 115, 125), 1))
                using (var font = new Font("Segoe UI", 10))
                {
                    Text(g, font, "Sơ đồ cấu tạo - không theo tỷ lệ", 24, 14);
                    if (_view == 3) { Anchorage(g, bar, secondary, dimension, font); return; }
                    bool column = _kind == RebarReferenceKind.RectangularColumn || _kind == RebarReferenceKind.CircularColumn;
                    bool mesh = _kind == RebarReferenceKind.Slab || _kind == RebarReferenceKind.Foundation;
                    var box = column && _view == 1 ? new Rectangle(190, 55, 160, 220)
                        : new Rectangle(90, 75, 360, 180);
                    bool circle = _kind == RebarReferenceKind.CircularColumn && _view != 1;
                    if (circle) box = new Rectangle(175, 65, 190, 190);
                    if (circle) g.DrawEllipse(concrete, box); else g.DrawRectangle(concrete, box);
                    if (circle)
                    {
                        g.DrawEllipse(secondary, box.X + 20, box.Y + 20, box.Width - 40, box.Height - 40);
                        for (int i = 0; i < 8; i++)
                        {
                            double angle = i * Math.PI / 4;
                            Dot(g, (float)(270 + 68 * Math.Cos(angle)), (float)(160 + 68 * Math.Sin(angle)));
                        }
                    }
                    else if (mesh && _view == 0)
                    {
                        for (int x = 110; x <= 430; x += 32) g.DrawLine(bar, x, 95, x, 235);
                        for (int y = 95; y <= 235; y += 28) g.DrawLine(secondary, 110, y, 430, y);
                        Text(g, font, "X: dX / aX", 90, 325);
                        Text(g, font, "Y: dY / aY", 290, 325);
                    }
                    else if (_view == 2 || (column && _view == 0))
                    {
                        g.DrawRectangle(secondary, box.X + 20, box.Y + 20, box.Width - 40, box.Height - 40);
                        foreach (int x in new[] { box.Left + 28, box.Right - 28 })
                            foreach (int y in new[] { box.Top + 28, box.Bottom - 28 }) Dot(g, x, y);
                        if (mesh)
                            for (int x = box.Left + 70; x < box.Right - 40; x += 45)
                            { Dot(g, x, box.Top + 28); Dot(g, x, box.Bottom - 28); }
                        Text(g, font, mesh ? "Lớp trên / lớp dưới" : "Thép chủ / thép đai", 90, 325);
                    }
                    else if (column)
                    {
                        g.DrawLine(bar, 215, 65, 215, 265);
                        g.DrawLine(bar, 325, 65, 325, 265);
                        for (int y = 80; y < 260; y += 22) g.DrawLine(secondary, 205, y, 335, y);
                        Text(g, font, "Đai: dđ / ađ", 90, 310);
                    }
                    else
                    {
                        g.DrawLines(bar, new[] { new Point(110, 120), new Point(110, 100), new Point(430, 100), new Point(430, 120) });
                        g.DrawLines(bar, new[] { new Point(110, 210), new Point(110, 230), new Point(430, 230), new Point(430, 210) });
                        if (_kind == RebarReferenceKind.Beam)
                            for (int x = 125; x <= 415; x += 25) g.DrawLine(secondary, x, 95, x, 235);
                        Text(g, font, "Thép trên / thép dưới", 90, 325);
                    }
                    Dimension(g, dimension, font, box.Left, box.Right, box.Bottom + 22,
                        circle ? "D" : column ? "b" : _view == 0 ? "Lx" : "b");
                    Text(g, font, circle ? "D" : column ? (_view == 1 ? "H" : "h") : _view == 0 ? "Ly" : "h",
                        box.Right + 12, box.Top + 70);
                    Text(g, font, "c", box.Left + 5, box.Top + 2);
                }
            }

            private static void Anchorage(Graphics g, Pen bar, Pen secondary, Pen dimension, Font font)
            {
                Text(g, font, "Neo đầu thanh", 70, 52);
                g.DrawLines(bar, new[] { new Point(70, 110), new Point(380, 110), new Point(380, 155) });
                Dimension(g, dimension, font, 260, 380, 70, "lneo = kneo × d");
                Text(g, font, "r uốn", 392, 118);
                Text(g, font, "Nối chồng", 70, 182);
                g.DrawLine(bar, 70, 235, 340, 235);
                g.DrawLine(secondary, 220, 248, 470, 248);
                Dimension(g, dimension, font, 220, 340, 281, "lnối = knối × d");
                Text(g, font, "d: đường kính thanh; k: hệ số do người dùng quy định", 24, 324);
            }

            private static void Dot(Graphics g, float x, float y)
            {
                using (var brush = new SolidBrush(Color.FromArgb(0, 122, 194))) g.FillEllipse(brush, x - 4, y - 4, 8, 8);
            }

            private static void Dimension(Graphics g, Pen pen, Font font, int x1, int x2, int y, string label)
            {
                g.DrawLine(pen, x1, y, x2, y);
                g.DrawLine(pen, x1, y - 5, x1, y + 5);
                g.DrawLine(pen, x2, y - 5, x2, y + 5);
                var size = g.MeasureString(label, font);
                Text(g, font, label, (x1 + x2 - size.Width) / 2, y + 7);
            }

            private new static void Text(Graphics g, Font font, string text, float x, float y)
            {
                using (var brush = new SolidBrush(Color.FromArgb(38, 50, 56))) g.DrawString(text, font, brush, x, y);
            }
        }
    }
}

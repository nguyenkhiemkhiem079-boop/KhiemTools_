using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace IconGen
{
    class Program
    {
        static void Main(string[] args)
        {
            string outDir = @"c:\Users\khiem.nguyen\Documents\KhimTools_v2\KhimTools\Resources";
            Directory.CreateDirectory(outDir);

            // 1. Workspace
            DrawIcon(outDir, "icon_workspace", Color.FromArgb(14, 165, 233), (g, sz) =>
            {
                int m = sz >= 32 ? 6 : 3;
                int half = (sz - m * 2 - 2) / 2;
                using var b = new SolidBrush(Color.White);
                g.FillRectangle(b, m, m, half, half);
                g.FillRectangle(b, m + half + 2, m, half, half);
                g.FillRectangle(b, m, m + half + 2, half, half);
                g.FillRectangle(b, m + half + 2, m + half + 2, half, half);
            });

            // 2. Check Update
            DrawIcon(outDir, "icon_update", Color.FromArgb(16, 185, 129), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 3 : 2);
                g.DrawArc(pen, pad, pad, sz - pad * 2, sz - pad * 2, 45, 270);
                using var b = new SolidBrush(Color.White);
                PointF[] pts = { new PointF(sz - pad, sz / 2 - (sz >= 32 ? 4 : 2)), new PointF(sz - pad + (sz >= 32 ? 4 : 2), sz / 2 + (sz >= 32 ? 2 : 1)), new PointF(sz - pad - (sz >= 32 ? 4 : 2), sz / 2 + (sz >= 32 ? 2 : 1)) };
                g.FillPolygon(b, pts);
            });

            // 3. Join Elements
            DrawIcon(outDir, "icon_join", Color.FromArgb(99, 102, 241), (g, sz) =>
            {
                int pad = sz >= 32 ? 6 : 3;
                int len = sz >= 32 ? 14 : 7;
                using var b1 = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
                using var b2 = new SolidBrush(Color.White);
                g.FillRectangle(b1, pad, pad, len, len);
                g.FillRectangle(b2, sz - pad - len, sz - pad - len, len, len);
                using var pen = new Pen(Color.White, sz >= 32 ? 2.5f : 1.5f);
                g.DrawLine(pen, pad + len / 2, pad + len / 2, sz - pad - len / 2, sz - pad - len / 2);
            });

            // 4. Unjoin Elements
            DrawIcon(outDir, "icon_unjoin", Color.FromArgb(239, 68, 68), (g, sz) =>
            {
                int pad = sz >= 32 ? 6 : 3;
                int len = sz >= 32 ? 12 : 6;
                using var b1 = new SolidBrush(Color.White);
                g.FillRectangle(b1, pad, pad, len, len);
                g.FillRectangle(b1, sz - pad - len, sz - pad - len, len, len);
                using var pen = new Pen(Color.Yellow, sz >= 32 ? 2.5f : 1.5f);
                g.DrawLine(pen, pad + len, pad, sz - pad, sz - pad - len);
            });

            // 5. Align Viewport
            DrawIcon(outDir, "icon_align", Color.FromArgb(6, 182, 212), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var penDash = new Pen(Color.FromArgb(180, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dash };
                g.DrawLine(penDash, sz / 2, pad, sz / 2, sz - pad);
                using var b = new SolidBrush(Color.White);
                int rectW = sz >= 32 ? 9 : 4;
                int rectH = sz >= 32 ? 16 : 8;
                g.FillRectangle(b, sz / 2 - rectW - 1, sz / 2 - rectH / 2, rectW, rectH);
                g.FillRectangle(b, sz / 2 + 2, sz / 2 - rectH / 2, rectW, rectH);
            });

            // 6. Update Detail No
            DrawIcon(outDir, "icon_detail", Color.FromArgb(245, 158, 11), (g, sz) =>
            {
                int pad = sz >= 32 ? 6 : 3;
                using var b = new SolidBrush(Color.White);
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawEllipse(pen, pad, pad, sz - pad * 2, sz - pad * 2);
                using var font = new Font("Arial", sz >= 32 ? 13f : 7f, FontStyle.Bold);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("#", font, b, new RectangleF(0, 0, sz, sz), sf);
            });

            // 7. Sheet Exporter
            DrawIcon(outDir, "icon_export", Color.FromArgb(16, 185, 129), (g, sz) =>
            {
                int w = sz >= 32 ? 18 : 9;
                int h = sz >= 32 ? 22 : 11;
                int x = (sz - w) / 2;
                int y = (sz - h) / 2;
                using var b = new SolidBrush(Color.White);
                g.FillRectangle(b, x, y, w, h);
                using var pen = new Pen(Color.FromArgb(16, 185, 129), sz >= 32 ? 2 : 1);
                g.DrawLine(pen, x + 3, y + 4, x + w - 3, y + 4);
                g.DrawLine(pen, x + 3, y + 8, x + w - 3, y + 8);
                if (sz >= 32) g.DrawLine(pen, x + 3, y + 12, x + w - 5, y + 12);
            });

            // 8. Column Rebar
            DrawIcon(outDir, "rebar_col", Color.FromArgb(225, 29, 72), (g, sz) =>
            {
                int pad = sz >= 32 ? 6 : 3;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawRectangle(pen, pad, pad, sz - pad * 2, sz - pad * 2);
                using var b = new SolidBrush(Color.White);
                int r = sz >= 32 ? 3 : 2;
                g.FillEllipse(b, pad + 1, pad + 1, r, r);
                g.FillEllipse(b, sz - pad - r - 1, pad + 1, r, r);
                g.FillEllipse(b, pad + 1, sz - pad - r - 1, r, r);
                g.FillEllipse(b, sz - pad - r - 1, sz - pad - r - 1, r, r);
            });

            // 9. Beam Rebar
            DrawIcon(outDir, "rebar_beam", Color.FromArgb(234, 88, 12), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                int h = sz >= 32 ? 14 : 7;
                int y = (sz - h) / 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawRectangle(pen, pad, y, sz - pad * 2, h);
                g.DrawLine(pen, pad, y + h / 2, sz - pad, y + h / 2);
            });

            // 10. Slab Rebar
            DrawIcon(outDir, "rebar_slab", Color.FromArgb(13, 148, 136), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 1.5f : 1f);
                g.DrawRectangle(pen, pad, pad, sz - pad * 2, sz - pad * 2);
                g.DrawLine(pen, pad, sz / 2, sz - pad, sz / 2);
                g.DrawLine(pen, sz / 2, pad, sz / 2, sz - pad);
            });

            // 11. Foundation Rebar
            DrawIcon(outDir, "rebar_fdn", Color.FromArgb(101, 163, 13), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                int trapW = sz >= 32 ? 20 : 10;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawRectangle(pen, pad, sz - pad - (sz >= 32 ? 8 : 4), sz - pad * 2, sz >= 32 ? 8 : 4);
                g.DrawLine(pen, sz / 2, pad, sz / 2, sz - pad - (sz >= 32 ? 8 : 4));
            });

            // 12. Cover Setup
            DrawIcon(outDir, "rebar_cover", Color.FromArgb(100, 116, 139), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawEllipse(pen, pad, pad, sz - pad * 2, sz - pad * 2);
                using var b = new SolidBrush(Color.White);
                g.FillEllipse(b, sz / 2 - (sz >= 32 ? 4 : 2), sz / 2 - (sz >= 32 ? 4 : 2), sz >= 32 ? 8 : 4, sz >= 32 ? 8 : 4);
            });

            // 13. Section Cut
            DrawIcon(outDir, "icon_section", Color.FromArgb(124, 58, 237), (g, sz) =>
            {
                int pad = sz >= 32 ? 4 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawLine(pen, pad, pad, sz - pad, sz - pad);
                using var b = new SolidBrush(Color.White);
                PointF[] arrow1 = { new PointF(pad, pad), new PointF(pad + (sz >= 32 ? 7 : 3), pad), new PointF(pad, pad + (sz >= 32 ? 7 : 3)) };
                g.FillPolygon(b, arrow1);
            });

            // 14. Column Drawing
            DrawIcon(outDir, "rebar_draw", Color.FromArgb(8, 145, 178), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawRectangle(pen, pad, pad, sz - pad * 2, sz - pad * 2);
                g.DrawLine(pen, pad + 3, sz - pad - 4, sz - pad - 3, pad + 4);
            });

            // 15. Room 3D
            DrawIcon(outDir, "icon_room", Color.FromArgb(147, 51, 234), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawRectangle(pen, pad + (sz >= 32 ? 4 : 2), pad, sz - pad * 2 - (sz >= 32 ? 8 : 4), sz - pad * 2);
                using var b = new SolidBrush(Color.White);
                g.FillEllipse(b, sz / 2 + (sz >= 32 ? 2 : 1), sz / 2, sz >= 32 ? 3 : 2, sz >= 32 ? 3 : 2);
            });

            // 16. Room Finishes
            DrawIcon(outDir, "icon_finish", Color.FromArgb(219, 39, 119), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawRectangle(pen, pad, pad, sz - pad * 2, sz - pad * 2);
                using var b = new SolidBrush(Color.White);
                g.FillRectangle(b, pad + 3, pad + 3, sz - pad * 2 - 6, sz - pad * 2 - 6);
            });

            // 17. MEP Openings
            DrawIcon(outDir, "icon_mep", Color.FromArgb(234, 88, 12), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawRectangle(pen, pad, pad, sz - pad * 2, sz - pad * 2);
                using var b = new SolidBrush(Color.Yellow);
                g.FillEllipse(b, sz / 2 - (sz >= 32 ? 5 : 2), sz / 2 - (sz >= 32 ? 5 : 2), sz >= 32 ? 10 : 4, sz >= 32 ? 10 : 4);
            });

            // 18. MEP Elevation Tags
            DrawIcon(outDir, "icon_tag", Color.FromArgb(22, 163, 74), (g, sz) =>
            {
                int pad = sz >= 32 ? 5 : 2;
                using var pen = new Pen(Color.White, sz >= 32 ? 2 : 1.5f);
                g.DrawLine(pen, pad, sz / 2, sz - pad, sz / 2);
                using var font = new Font("Arial", sz >= 32 ? 8f : 5f, FontStyle.Bold);
                using var b = new SolidBrush(Color.White);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("TAG", font, b, new RectangleF(0, 0, sz, sz / 2), sf);
            });

            Console.WriteLine("All 18 icons generated successfully in 32x32, 16x16!");
        }

        static void DrawIcon(string outDir, string name, Color bgColor, Action<Graphics, int> drawAction)
        {
            CreateImage(Path.Combine(outDir, name + "_32.png"), 32, bgColor, drawAction);
            CreateImage(Path.Combine(outDir, name + "_16.png"), 16, bgColor, drawAction);
            CreateImage(Path.Combine(outDir, name + ".png"), 32, bgColor, drawAction);
        }

        static void CreateImage(string filePath, int size, Color bgColor, Action<Graphics, int> drawAction)
        {
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int radius = size >= 32 ? 6 : 3;
            using (var path = GetRoundedRect(new Rectangle(0, 0, size, size), radius))
            using (var brush = new LinearGradientBrush(new Point(0, 0), new Point(size, size), bgColor, ControlPaint.Dark(bgColor, 0.2f)))
            {
                g.FillPath(brush, path);
            }

            using (var path = GetRoundedRect(new Rectangle(0, 0, size - 1, size - 1), radius))
            using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
            {
                g.DrawPath(pen, path);
            }

            drawAction(g, size);
            bmp.Save(filePath, ImageFormat.Png);
        }

        static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>Isometric review of detached centerlines returned by Revit's Rebar shape solver.</summary>
    internal sealed class RebarSolverPreviewForm : Form
    {
        private readonly RebarPreviewSnapshot _snapshot;
        private readonly Panel _canvas;

        public RebarSolverPreviewForm(RebarPreviewSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException("snapshot");
            Text = "K-TOOLS — Rebar 3D Solver Preview";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 460);
            Size = new Size(860, 650);
            BackColor = Color.White;

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 8, 0),
                Text = string.Format("Solved bars: {0}    Centerline paths: {1}    Rollback-only; model unchanged",
                    snapshot.Components.Sum(c => c.BarCount), snapshot.Components.Sum(c => c.Paths.Count))
            };
            _canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 250, 252) };
            _canvas.Paint += PaintPreview;
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
            footer.Controls.Add(close);
            Controls.Add(_canvas);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = close;
        }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            var paths = _snapshot.Components.SelectMany(c => c.Paths).ToArray();
            if (paths.Length == 0) return;
            PointF[][] projected = paths.Select(path => path.Points.Select(Project).ToArray()).ToArray();
            float minX = projected.SelectMany(p => p).Min(p => p.X);
            float maxX = projected.SelectMany(p => p).Max(p => p.X);
            float minY = projected.SelectMany(p => p).Min(p => p.Y);
            float maxY = projected.SelectMany(p => p).Max(p => p.Y);
            float width = Math.Max(1f, maxX - minX);
            float height = Math.Max(1f, maxY - minY);
            float scale = Math.Min((_canvas.ClientSize.Width - 64f) / width, (_canvas.ClientSize.Height - 72f) / height);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) return;
            float offsetX = (_canvas.ClientSize.Width - width * scale) / 2f;
            float offsetY = (_canvas.ClientSize.Height - height * scale) / 2f;
            PointF Map(PointF p) => new PointF(offsetX + (p.X - minX) * scale, offsetY + (maxY - p.Y) * scale);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(21, 101, 192), 2f))
            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(Color.FromArgb(70, 82, 95)))
            {
                e.Graphics.DrawString("ISOMETRIC SOLVED CENTERLINES", font, brush, 12, 10);
                foreach (PointF[] path in projected)
                {
                    if (path.Length < 2) continue;
                    e.Graphics.DrawLines(pen, path.Select(Map).ToArray());
                }
            }
        }

        private static PointF Project(RebarPreviewPoint point) =>
            new PointF((float)((point.X - point.Y) * 0.8660254), (float)(point.Z - (point.X + point.Y) * 0.25));
    }
}

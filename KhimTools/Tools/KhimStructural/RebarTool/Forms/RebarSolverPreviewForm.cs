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
            var tabs = new TabControl { Dock = DockStyle.Fill };
            AddProjectionTab(tabs, "3D isometric", "iso");
            AddProjectionTab(tabs, "Plan (XY)", "plan");
            AddProjectionTab(tabs, "Elevation (XZ)", "elevation");
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var accept = new Button { Text = "Use this preview", AutoSize = true, DialogResult = DialogResult.OK };
            footer.Controls.Add(cancel);
            footer.Controls.Add(accept);
            Controls.Add(tabs);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = accept;
            CancelButton = cancel;
        }

        private void AddProjectionTab(TabControl tabs, string title, string projection)
        {
            var canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 250, 252), Tag = projection };
            canvas.Paint += PaintPreview;
            tabs.TabPages.Add(new TabPage(title) { Controls = { canvas } });
        }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            var paths = _snapshot.Components.SelectMany(c => c.Paths).ToArray();
            if (paths.Length == 0) return;
            Panel canvas = (Panel)sender;
            string projection = (string)canvas.Tag;
            PointF[][] projected = paths.Select(path => path.Points.Select(point => Project(point, projection)).ToArray()).ToArray();
            float minX = projected.SelectMany(p => p).Min(p => p.X);
            float maxX = projected.SelectMany(p => p).Max(p => p.X);
            float minY = projected.SelectMany(p => p).Min(p => p.Y);
            float maxY = projected.SelectMany(p => p).Max(p => p.Y);
            float width = Math.Max(1f, maxX - minX);
            float height = Math.Max(1f, maxY - minY);
            float scale = Math.Min((canvas.ClientSize.Width - 64f) / width, (canvas.ClientSize.Height - 72f) / height);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) return;
            float offsetX = (canvas.ClientSize.Width - width * scale) / 2f;
            float offsetY = (canvas.ClientSize.Height - height * scale) / 2f;
            PointF Map(PointF p) => new PointF(offsetX + (p.X - minX) * scale, offsetY + (maxY - p.Y) * scale);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(21, 101, 192), 2f))
            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(Color.FromArgb(70, 82, 95)))
            {
                string heading = projection == "plan" ? "PLAN (XY) — SOLVED REBAR CENTERLINES" :
                    projection == "elevation" ? "ELEVATION (XZ) — SOLVED REBAR CENTERLINES" :
                    "ISOMETRIC — SOLVED REBAR CENTERLINES";
                e.Graphics.DrawString(heading, font, brush, 12, 10);
                foreach (PointF[] path in projected)
                {
                    if (path.Length < 2) continue;
                    e.Graphics.DrawLines(pen, path.Select(Map).ToArray());
                }
            }
        }

        private static PointF Project(RebarPreviewPoint point, string projection)
        {
            if (projection == "plan") return new PointF((float)point.X, (float)point.Y);
            if (projection == "elevation") return new PointF((float)point.X, (float)point.Z);
            return new PointF((float)((point.X - point.Y) * 0.8660254), (float)(point.Z - (point.X + point.Y) * 0.25));
        }
    }
}

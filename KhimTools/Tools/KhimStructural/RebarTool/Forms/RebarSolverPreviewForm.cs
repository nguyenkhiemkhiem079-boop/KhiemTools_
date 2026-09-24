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
        private float _zoom = 1f;
        private PointF _pan = PointF.Empty;
        private Point _dragStart;
        private PointF _dragPanStart;
        private bool _dragging;
        private readonly System.Collections.Generic.List<Panel> _canvases = new System.Collections.Generic.List<Panel>();
        private TabControl _tabs;
        private ComboBox _componentSelector;
        private ComboBox _roleSelector;
        private Label _summary;

        private sealed class PreviewRoleOption
        {
            public string Key { get; private set; }
            public string Label { get; private set; }
            public PreviewRoleOption(string key, string label) { Key = key; Label = label; }
            public override string ToString() => Label;
        }

        public RebarSolverPreviewForm(RebarPreviewSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException("snapshot");
            Text = "K-TOOLS — Rebar 3D Solver Preview";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 460);
            Size = new Size(860, 650);
            BackColor = Color.White;

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(12, 3, 8, 3),
                ColumnCount = 2,
                RowCount = 1
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _summary = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _componentSelector = new ComboBox
            {
                Width = 300,
                Dock = DockStyle.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "Filter solver preview by host"
            };
            _componentSelector.Items.Add("All hosts (" + snapshot.Components.Count + ")");
            for (int index = 0; index < snapshot.Components.Count; index++)
            {
                RebarPreviewComponent component = snapshot.Components[index];
                string host = string.IsNullOrWhiteSpace(component.HostId) ? "Component " + (index + 1) : "Host " + component.HostId;
                _componentSelector.Items.Add(host + " · " + component.BarCount + " bars");
            }
            _componentSelector.SelectedIndex = 0;
            _componentSelector.SelectedIndexChanged += (sender, args) =>
            {
                UpdateSummary();
                InvalidateCanvases();
            };
            _roleSelector = new ComboBox
            {
                Width = 190,
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "Filter solver preview by reinforcement role"
            };
            _roleSelector.Items.Add(new PreviewRoleOption(null, "All roles / Tất cả"));
            string[] availableRoles = snapshot.Components.SelectMany(component => component.Paths)
                .Select(path => path.Role).Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.Ordinal).OrderBy(role => role, StringComparer.Ordinal).ToArray();
            foreach (string role in availableRoles)
                _roleSelector.Items.Add(new PreviewRoleOption(role, RoleLabel(role)));
            _roleSelector.SelectedIndex = 0;
            _roleSelector.SelectedIndexChanged += (sender, args) =>
            {
                UpdateSummary();
                InvalidateCanvases();
            };
            header.Controls.Add(_summary, 0, 0);
            header.Controls.Add(_componentSelector, 1, 0);
            UpdateSummary();
            _tabs = new TabControl { Dock = DockStyle.Fill };
            AddProjectionTab(_tabs, "ISO", "iso");
            AddProjectionTab(_tabs, "TOP", "top");
            AddProjectionTab(_tabs, "FRONT", "front");
            AddProjectionTab(_tabs, "RIGHT", "right");

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                WrapContents = false,
                Padding = new Padding(8, 4, 8, 2),
                BackColor = Color.FromArgb(241, 245, 249)
            };
            AddToolButton(toolbar, "FIT", () => Fit());
            AddToolButton(toolbar, "−", () => ChangeZoom(1f / 1.2f));
            AddToolButton(toolbar, "+", () => ChangeZoom(1.2f));
            if (availableRoles.Length > 0)
            {
                toolbar.Controls.Add(new Label { Text = "Role:", AutoSize = true, Padding = new Padding(8, 7, 0, 0), ForeColor = Color.FromArgb(71, 85, 105) });
                toolbar.Controls.Add(_roleSelector);
            }
            toolbar.Controls.Add(new Label { Text = "Mouse wheel: zoom    Drag: pan", AutoSize = true, Padding = new Padding(10, 7, 0, 0), ForeColor = Color.FromArgb(71, 85, 105) });
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var accept = new Button { Text = "Use this preview", AutoSize = true, DialogResult = DialogResult.OK };
            footer.Controls.Add(cancel);
            footer.Controls.Add(accept);
            Controls.Add(_tabs);
            Controls.Add(footer);
            Controls.Add(toolbar);
            Controls.Add(header);
            AcceptButton = accept;
            CancelButton = cancel;
        }

        private void AddProjectionTab(TabControl tabs, string title, string projection)
        {
            var canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 250, 252), Tag = projection };
            canvas.Paint += PaintPreview;
            canvas.MouseWheel += Canvas_MouseWheel;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.Resize += (sender, args) => canvas.Invalidate();
            canvas.TabStop = true;
            canvas.Cursor = Cursors.SizeAll;
            _canvases.Add(canvas);
            tabs.TabPages.Add(new TabPage(title) { Controls = { canvas } });
        }

        private static void AddToolButton(FlowLayoutPanel toolbar, string label, Action click)
        {
            var button = new Button { Text = label, Width = 52, Height = 28, Margin = new Padding(2, 0, 2, 0), FlatStyle = FlatStyle.System };
            button.Click += (sender, args) => click();
            toolbar.Controls.Add(button);
        }

        private void Fit()
        {
            _zoom = 1f;
            _pan = PointF.Empty;
            InvalidateCanvases();
        }

        private void ChangeZoom(float factor)
        {
            _zoom = Math.Max(0.25f, Math.Min(8f, _zoom * factor));
            InvalidateCanvases();
        }

        private void InvalidateCanvases()
        {
            foreach (Panel canvas in _canvases) canvas.Invalidate();
        }

        private RebarPreviewComponent[] GetVisibleComponents()
        {
            int selected = _componentSelector == null ? 0 : _componentSelector.SelectedIndex;
            return selected <= 0
                ? _snapshot.Components.ToArray()
                : _snapshot.Components.Skip(selected - 1).Take(1).ToArray();
        }

        private void UpdateSummary()
        {
            if (_summary == null) return;
            RebarPreviewComponent[] components = GetVisibleComponents();
            string role = (_roleSelector?.SelectedItem as PreviewRoleOption)?.Key;
            int pathCount = components.Sum(component => component.Paths.Count(path => role == null || path.Role == role));
            string scope = _componentSelector == null || _componentSelector.SelectedIndex <= 0
                ? "all hosts"
                : _componentSelector.SelectedItem.ToString();
            _summary.Text = string.Format("{0}    Solved bars: {1}    Paths in role: {2}    Rollback-only; model unchanged",
                scope, components.Sum(component => component.BarCount), pathCount);
        }

        private static string RoleLabel(string role)
        {
            switch (role)
            {
                case "longitudinal": return "Longitudinal / Thép dọc";
                case "outer-tie": return "Outer tie / Đai ngoài";
                case "inner-tie-left": return "Inner tie left / Đai trong trái";
                case "inner-tie-right": return "Inner tie right / Đai trong phải";
                case "diamond-tie": return "Diamond tie / Đai thoi";
                case "cross-tie": return "Cross-tie / Đai phụ";
                case "tie": return "Ties / Đai";
                case "bottom-x": return "Bottom X / Lưới đáy X";
                case "bottom-y": return "Bottom Y / Lưới đáy Y";
                case "top-x": return "Top X / Lưới trên X";
                case "top-y": return "Top Y / Lưới trên Y";
                case "support-x": return "Support X / Mũ gối X";
                case "support-y": return "Support Y / Mũ gối Y";
                case "opening": return "Opening trim / Gia cường lỗ mở";
                case "spacer": return "Spacer / Con kê";
                default: return role;
            }
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            Panel canvas = (Panel)sender;
            float previous = _zoom;
            _zoom = Math.Max(0.25f, Math.Min(8f, _zoom * (e.Delta > 0 ? 1.15f : 1f / 1.15f)));
            float ratio = _zoom / previous;
            float anchorX = e.X - canvas.ClientSize.Width / 2f;
            float anchorY = e.Y - canvas.ClientSize.Height / 2f;
            _pan = new PointF(anchorX * (1f - ratio) + _pan.X * ratio, anchorY * (1f - ratio) + _pan.Y * ratio);
            InvalidateCanvases();
            canvas.Focus();
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Middle) return;
            _dragging = true;
            _dragStart = e.Location;
            _dragPanStart = _pan;
            ((Panel)sender).Capture = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            _pan = new PointF(_dragPanStart.X + e.X - _dragStart.X, _dragPanStart.Y + e.Y - _dragStart.Y);
            InvalidateCanvases();
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
            ((Panel)sender).Capture = false;
        }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            var paths = GetVisibleComponents().SelectMany(c => c.Paths).ToArray();
            if (paths.Length == 0) return;
            Panel canvas = (Panel)sender;
            string projection = (string)canvas.Tag;
            string selectedRole = (_roleSelector?.SelectedItem as PreviewRoleOption)?.Key;
            var projected = paths.Select(path => new
            {
                Role = path.Role,
                Points = path.Points.Select(point => Project(point, projection)).ToArray()
            }).ToArray();
            float minX = projected.SelectMany(path => path.Points).Min(point => point.X);
            float maxX = projected.SelectMany(path => path.Points).Max(point => point.X);
            float minY = projected.SelectMany(path => path.Points).Min(point => point.Y);
            float maxY = projected.SelectMany(path => path.Points).Max(point => point.Y);
            float width = Math.Max(1f, maxX - minX);
            float height = Math.Max(1f, maxY - minY);
            float scale = Math.Min((canvas.ClientSize.Width - 48f) / width, (canvas.ClientSize.Height - 60f) / height) * 0.82f * _zoom;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) return;
            float offsetX = (canvas.ClientSize.Width - width * scale) / 2f + _pan.X;
            float offsetY = (canvas.ClientSize.Height - height * scale) / 2f + _pan.Y;
            PointF Map(PointF p) => new PointF(offsetX + (p.X - minX) * scale, offsetY + (maxY - p.Y) * scale);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(21, 101, 192), 2f))
            using (var subduedPen = new Pen(Color.FromArgb(180, 190, 198), 1f))
            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(Color.FromArgb(70, 82, 95)))
            {
                string heading = projection == "top" ? "TOP (XY) — SOLVED REBAR CENTERLINES" :
                    projection == "front" ? "FRONT (XZ) — SOLVED REBAR CENTERLINES" :
                    projection == "right" ? "RIGHT (YZ) — SOLVED REBAR CENTERLINES" :
                    "ISOMETRIC — SOLVED REBAR CENTERLINES";
                e.Graphics.DrawString(heading, font, brush, 12, 10);
                foreach (var rolePath in projected)
                {
                    Pen pathPen = selectedRole == null || rolePath.Role == selectedRole ? pen : subduedPen;
                    PointF[] path = rolePath.Points;
                    if (path.Length < 2) continue;
                    e.Graphics.DrawLines(pathPen, path.Select(Map).ToArray());
                }
            }
        }

        private static PointF Project(RebarPreviewPoint point, string projection)
        {
            if (projection == "top") return new PointF((float)point.X, (float)point.Y);
            if (projection == "front") return new PointF((float)point.X, (float)point.Z);
            if (projection == "right") return new PointF((float)point.Y, (float)point.Z);
            return new PointF((float)((point.X - point.Y) * 0.8660254), (float)(point.Z - (point.X + point.Y) * 0.25));
        }
    }
}

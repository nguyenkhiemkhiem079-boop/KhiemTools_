using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.RebarTool.Models;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using FontStyle = System.Drawing.FontStyle;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace KhimTools.RebarTool.Forms
{
    public class SlabEdgePickerForm : System.Windows.Forms.Form
    {
        private readonly SlabPanel _panel;
        private System.Windows.Forms.Panel _canvas;
        private DataGridView _gridEdges;
        private Label _lblInfo;
        private Button _btnApply;
        private Button _btnClose;
        private int _selectedEdgeIndex = -1;

        public SlabEdgePickerForm(SlabPanel panel)
        {
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));

            KhimUiStyle.ApplyFormTheme(this);
            InitializeLayout();
            PopulateEdgeGrid();
        }

        private void InitializeLayout()
        {
            Text = $"🔍 Pick & Skip Edge — Panel {_panel.PanelId} ({_panel.WidthMm:N0} x {_panel.LengthMm:N0} mm)";
            Width = 820;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 0. Header
            var header = KhimUiStyle.CreateHeaderBanner(
                $"Cấu Hình Từng Cạnh — Panel {_panel.PanelId}",
                "Click chọn cạnh để Bật/Tắt Thép Mũ Gối (Skip Top Hat) hoặc Thép Đáy (Skip Bottom)",
                "2D Interactive");
            Controls.Add(header);

            // 1. Bottom
            var bottom = new System.Windows.Forms.Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(245, 245, 247) };
            _btnApply = new Button { Text = "✓ Áp Dụng", Width = 110, Height = 32, Top = 9, Left = 570 };
            KhimUiStyle.ApplyPrimaryButton(_btnApply, KhimUiStyle.CreateButtonBg);
            _btnApply.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            _btnClose = new Button { Text = "Đóng", Width = 90, Height = 32, Top = 9, Left = 690 };
            KhimUiStyle.ApplySecondaryButton(_btnClose);
            _btnClose.Click += (s, e) => Close();

            bottom.Controls.Add(_btnApply);
            bottom.Controls.Add(_btnClose);
            Controls.Add(bottom);

            // 2. Center Split (Left: 2D Canvas, Right: Edge Properties Table)
            _canvas = new System.Windows.Forms.Panel
            {
                Left = 15,
                Top = 65,
                Width = 420,
                Height = 430,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _canvas.Paint += Canvas_Paint;
            _canvas.MouseClick += Canvas_MouseClick;
            Controls.Add(_canvas);

            var pnlRight = new System.Windows.Forms.Panel { Left = 445, Top = 65, Width = 350, Height = 430 };
            _lblInfo = new Label
            {
                Text = "Danh sách 4 cạnh của Panel:",
                Top = 5,
                Left = 5,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _gridEdges = new DataGridView
            {
                Top = 30,
                Left = 0,
                Width = 350,
                Height = 390,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };

            var colIdx = new DataGridViewTextBoxColumn { HeaderText = "Cạnh", Width = 55, ReadOnly = true };
            var colType = new DataGridViewComboBoxColumn { HeaderText = "Loại Tựa", Width = 120 };
            colType.Items.AddRange("Beam Support", "Slab Adjacent", "Free Edge");

            var colSkipTop = new DataGridViewCheckBoxColumn { HeaderText = "Skip Mũ", Width = 80 };
            var colSkipBot = new DataGridViewCheckBoxColumn { HeaderText = "Skip Đáy", Width = 80 };

            _gridEdges.Columns.AddRange(colIdx, colType, colSkipTop, colSkipBot);
            _gridEdges.CellValueChanged += (s, e) => { ReadGridData(); _canvas.Invalidate(); };
            _gridEdges.SelectionChanged += (s, e) =>
            {
                if (_gridEdges.SelectedRows.Count > 0)
                {
                    _selectedEdgeIndex = _gridEdges.SelectedRows[0].Index;
                    _canvas.Invalidate();
                }
            };

            pnlRight.Controls.Add(_lblInfo);
            pnlRight.Controls.Add(_gridEdges);
            Controls.Add(pnlRight);
        }

        private void PopulateEdgeGrid()
        {
            _gridEdges.Rows.Clear();
            if (_panel.Edges == null || !_panel.Edges.Any())
            {
                // Mặc định tạo 4 cạnh chuẩn chữ nhật nếu chưa có
                _panel.Edges = new List<SlabPanelEdge>
                {
                    new SlabPanelEdge { EdgeIndex = 0, EdgeType = SlabPanelEdgeType.BeamSupport },
                    new SlabPanelEdge { EdgeIndex = 1, EdgeType = SlabPanelEdgeType.BeamSupport },
                    new SlabPanelEdge { EdgeIndex = 2, EdgeType = SlabPanelEdgeType.BeamSupport },
                    new SlabPanelEdge { EdgeIndex = 3, EdgeType = SlabPanelEdgeType.BeamSupport }
                };
            }

            for (int i = 0; i < _panel.Edges.Count; i++)
            {
                var edge = _panel.Edges[i];
                int r = _gridEdges.Rows.Add();
                _gridEdges.Rows[r].Cells[0].Value = $"Cạnh {i + 1}";
                _gridEdges.Rows[r].Cells[1].Value = edge.EdgeType == SlabPanelEdgeType.BeamSupport ? "Beam Support" : (edge.EdgeType == SlabPanelEdgeType.SlabAdjacent ? "Slab Adjacent" : "Free Edge");
                _gridEdges.Rows[r].Cells[2].Value = edge.SkipTopHat;
                _gridEdges.Rows[r].Cells[3].Value = edge.SkipBottomMesh;
            }
        }

        private void ReadGridData()
        {
            for (int i = 0; i < _gridEdges.Rows.Count && i < _panel.Edges.Count; i++)
            {
                var edge = _panel.Edges[i];
                string typeStr = _gridEdges.Rows[i].Cells[1].Value?.ToString() ?? "Beam Support";
                edge.EdgeType = typeStr == "Beam Support" ? SlabPanelEdgeType.BeamSupport : (typeStr == "Slab Adjacent" ? SlabPanelEdgeType.SlabAdjacent : SlabPanelEdgeType.FreeEdge);
                edge.SkipTopHat = Convert.ToBoolean(_gridEdges.Rows[i].Cells[2].Value);
                edge.SkipBottomMesh = Convert.ToBoolean(_gridEdges.Rows[i].Cells[3].Value);
            }
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            int w = _canvas.Width;
            int h = _canvas.Height;

            // Vẽ khung Panel tỷ lệ
            Rectangle rect = new Rectangle(50, 50, w - 100, h - 100);

            using (Brush b = new SolidBrush(Color.FromArgb(240, 249, 255)))
            {
                g.FillRectangle(b, rect);
            }

            // Vẽ 4 cạnh
            Point[] pts = new Point[]
            {
                new Point(rect.Left, rect.Top),       // P0 (Top-Left)
                new Point(rect.Right, rect.Top),      // P1 (Top-Right)
                new Point(rect.Right, rect.Bottom),   // P2 (Bottom-Right)
                new Point(rect.Left, rect.Bottom)     // P3 (Bottom-Left)
            };

            for (int i = 0; i < 4; i++)
            {
                Point pStart = pts[i];
                Point pEnd = pts[(i + 1) % 4];

                bool isSkipTop = (i < _panel.Edges.Count) && _panel.Edges[i].SkipTopHat;
                bool isSelected = (_selectedEdgeIndex == i);

                Color edgeColor = isSelected ? Color.DarkOrange : (isSkipTop ? Color.Crimson : Color.DodgerBlue);
                int penWidth = isSelected ? 5 : 3;

                using (Pen p = new Pen(edgeColor, penWidth))
                {
                    if (isSkipTop) p.DashStyle = DashStyle.Dash;
                    g.DrawLine(p, pStart, pEnd);
                }

                // Nhãn cạnh (Cạnh 1, Cạnh 2...)
                int midX = (pStart.X + pEnd.X) / 2;
                int midY = (pStart.Y + pEnd.Y) / 2;
                string label = $"Cạnh {i + 1}" + (isSkipTop ? " (Skip Mũ)" : "");
                using (Font font = new Font("Segoe UI", 8.5F, isSelected ? FontStyle.Bold : FontStyle.Regular))
                using (Brush brush = new SolidBrush(edgeColor))
                {
                    g.DrawString(label, font, brush, midX - 25, midY - 10);
                }
            }

            // Ghi chú tâm Panel
            using (Font f = new Font("Segoe UI", 10F, FontStyle.Bold))
            using (Brush br = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                string panelTitle = $"Panel {_panel.PanelId}\n{_panel.WidthMm:N0} x {_panel.LengthMm:N0} mm";
                g.DrawString(panelTitle, f, br, rect.Left + (rect.Width / 2) - 45, rect.Top + (rect.Height / 2) - 20);
            }
        }

        private void Canvas_MouseClick(object sender, MouseEventArgs e)
        {
            Rectangle rect = new Rectangle(50, 50, _canvas.Width - 100, _canvas.Height - 100);

            // Kiểm tra xem click gần cạnh nào
            if (Math.Abs(e.Y - rect.Top) < 20 && e.X >= rect.Left && e.X <= rect.Right) _selectedEdgeIndex = 0; // Top
            else if (Math.Abs(e.X - rect.Right) < 20 && e.Y >= rect.Top && e.Y <= rect.Bottom) _selectedEdgeIndex = 1; // Right
            else if (Math.Abs(e.Y - rect.Bottom) < 20 && e.X >= rect.Left && e.X <= rect.Right) _selectedEdgeIndex = 2; // Bottom
            else if (Math.Abs(e.X - rect.Left) < 20 && e.Y >= rect.Top && e.Y <= rect.Bottom) _selectedEdgeIndex = 3; // Left

            if (_selectedEdgeIndex >= 0 && _selectedEdgeIndex < _gridEdges.Rows.Count)
            {
                // Toggle SkipTop khi click trực tiếp
                bool currentSkip = Convert.ToBoolean(_gridEdges.Rows[_selectedEdgeIndex].Cells[2].Value);
                _gridEdges.Rows[_selectedEdgeIndex].Cells[2].Value = !currentSkip;
                _gridEdges.Rows[_selectedEdgeIndex].Selected = true;
                ReadGridData();
                _canvas.Invalidate();
            }
        }
    }
}

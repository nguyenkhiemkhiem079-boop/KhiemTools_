using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KhimTools.Core;

namespace KhimTools.RebarTool.Forms
{
    internal enum RebarReferenceKind { Beam, Slab, Foundation, RectangularColumn, CircularColumn }

    internal static class RebarReferenceViews
    {
        internal static TabPage CreatePage(RebarReferenceKind kind)
        {
            bool isEn = LanguageManager.IsEnglish;
            string pageTitle = isEn ? "2D Reference" : "Tham khảo 2D";
            var page = new TabPage(pageTitle) { BackColor = Color.White };
            page.Controls.Add(Create(kind));
            return page;
        }

        internal static Control Create(RebarReferenceKind kind)
        {
            bool isEn = LanguageManager.IsEnglish;
            var tabs = new TabControl { Dock = DockStyle.Fill, Multiline = true };

            string[] names = kind == RebarReferenceKind.RectangularColumn
                ? (isEn
                    ? new[] { "Cross Section", "Elevation", "Stirrup Layout", "Anchorage & Lap" }
                    : new[] { "Tiết diện", "Mặt đứng", "Cấu tạo đai", "Neo / Nối" })
                : (isEn
                    ? new[] { "Plan", "Longitudinal", "Cross Section", "Anchorage" }
                    : new[] { "Mặt bằng", "Cắt dọc", "Cắt ngang", "Neo / Nối" });

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

                float scale = Math.Min(ClientSize.Width / 540f, ClientSize.Height / 360f);
                g.TranslateTransform((ClientSize.Width - 540 * scale) / 2, (ClientSize.Height - 360 * scale) / 2);
                g.ScaleTransform(scale, scale);

                if (_kind == RebarReferenceKind.RectangularColumn)
                {
                    DrawRectangularColumnReference(g, _view);
                }
                else
                {
                    DrawGenericReference(g, _kind, _view);
                }
            }

            // ================================================================
            // RECTANGULAR COLUMN ENGINEERING REFERENCE DRAWINGS (540 x 360)
            // ================================================================
            private static void DrawRectangularColumnReference(Graphics g, int view)
            {
                bool isEn = LanguageManager.IsEnglish;

                // K-TOOLS Engineering Color Palette
                var cBg      = Color.FromArgb(247, 248, 250);
                var cCard    = Color.White;
                var cBorder  = Color.FromArgb(207, 216, 220);
                var cOutline = Color.FromArgb(38,  50,  56);
                var cBar     = Color.FromArgb(21,  101, 192);
                var cTie     = Color.FromArgb(211, 47,  47);
                var cTieDia  = Color.FromArgb(230, 81,  0);
                var cTieCr   = Color.FromArgb(123, 31,  162);
                var cDim     = Color.FromArgb(84,  110, 122);
                var cConc    = Color.FromArgb(236, 239, 241);
                var cHatch   = Color.FromArgb(218, 224, 227);
                var cHdr     = Color.FromArgb(38,  50,  56);
                var cOk      = Color.FromArgb(27,  94,  32);

                g.Clear(cBg);

                using var fHdr = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
                using var fSub = new Font("Segoe UI", 6.5f);
                using var fSec = new Font("Segoe UI Semibold", 7f, FontStyle.Bold);
                using var fBdy = new Font("Segoe UI", 6.5f);
                using var fSml = new Font("Segoe UI", 6f);
                using var fDim = new Font("Consolas", 6.5f);
                using var brOutline = new SolidBrush(cOutline);
                using var brDim = new SolidBrush(cDim);
                using var brHdrText = new SolidBrush(Color.White);
                using var brSubText = new SolidBrush(Color.FromArgb(207, 216, 220));
                using var penOutline = new Pen(cOutline, 1.5f);
                using var penBorder = new Pen(cBorder, 1f);
                using var penDim = new Pen(cDim, 0.8f);

                switch (view)
                {
                    case 0: // Tiết diện / Cross Section (4x4 arrangement)
                        DrawSectionTab(g, isEn, cConc, cHatch, cOutline, cBar, cTie, cTieDia, cTieCr, cDim, cHdr, cBorder, cCard,
                            fHdr, fSub, fSec, fBdy, fSml, fDim, brOutline, brDim, brHdrText, brSubText, penOutline, penBorder, penDim);
                        break;

                    case 1: // Mặt đứng / Elevation (A1/A2/A1 zones)
                        DrawElevationTab(g, isEn, cConc, cOutline, cBar, cTie, cDim, cHdr, cBorder, cCard,
                            fHdr, fSub, fSec, fBdy, fSml, fDim, brOutline, brDim, brHdrText, brSubText, penOutline, penBorder, penDim);
                        break;

                    case 2: // Cấu tạo đai / Stirrup Layouts (3 configurations)
                        DrawStirrupTypesTab(g, isEn, cConc, cOutline, cBar, cTie, cTieDia, cTieCr, cDim, cHdr, cBorder, cCard,
                            fHdr, fSub, fSec, fBdy, fSml, fDim, brOutline, brDim, brHdrText, brSubText, penOutline, penBorder, penDim);
                        break;

                    case 3: // Neo / Nối / Anchorage & Lap Splice
                        DrawAnchorageTab(g, isEn, cConc, cHatch, cOutline, cBar, cTie, cDim, cHdr, cBorder, cCard,
                            fHdr, fSub, fSec, fBdy, fSml, fDim, brOutline, brDim, brHdrText, brSubText, penOutline, penBorder, penDim);
                        break;
                }
            }

            private static void DrawHeaderBanner(Graphics g, string title, string sub, Color cHdr,
                Font fHdr, Font fSub, Brush brHdrText, Brush brSubText)
            {
                using var br = new SolidBrush(cHdr);
                g.FillRectangle(br, 10, 8, 520, 28);
                g.DrawString(title, fHdr, brHdrText, 16, 11);
                g.DrawString(sub, fSub, brSubText, 16, 23);
            }

            // ----------------------------------------------------------------
            // TAB 0: TIẾT DIỆN (CROSS SECTION)
            // ----------------------------------------------------------------
            private static void DrawSectionTab(Graphics g, bool isEn,
                Color cConc, Color cHatch, Color cOutline, Color cBar, Color cTie, Color cTieDia, Color cTieCr, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "RECTANGULAR COLUMN SECTION - REBAR ARRANGEMENT" : "TIẾT DIỆN CỘT ĐIỂN HÌNH - BỐ TRÍ THÉP B × H";
                string sub   = isEn ? "Outer stirrup, diamond tie & cross-ties (4x4 layout)" : "Đai ngoài, đai kim cương và đai móc C (sơ đồ 4×4 thanh)";
                DrawHeaderBanner(g, title, sub, cHdr, fHdr, fSub, brHdrText, brSubText);

                // Concrete Section (left side)
                int secX = 42, secY = 56, secW = 206, secH = 206;
                var secRect = new Rectangle(secX, secY, secW, secH);

                using (var concBr = new SolidBrush(cConc)) g.FillRectangle(concBr, secRect);

                // Hatch texture
                var oldClip = g.Clip;
                g.SetClip(secRect);
                using (var hp = new Pen(cHatch, 1f))
                {
                    for (int x = -secH; x < secW + secH; x += 16)
                        g.DrawLine(hp, secX + x, secY, secX + x + secH, secY + secH);
                }
                g.Clip = oldClip;

                g.DrawRectangle(penOutline, secRect);

                // Dimension B on top
                g.DrawLine(penDim, secX, secY - 10, secX + secW, secY - 10);
                g.DrawLine(penDim, secX, secY - 14, secX, secY - 6);
                g.DrawLine(penDim, secX + secW, secY - 14, secX + secW, secY - 6);
                using var sfCen = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(isEn ? "b (width)" : "b (cạnh ngang)", fDim, brDim, secX + secW / 2, secY - 18, sfCen);

                // Dimension H on left
                g.DrawLine(penDim, secX - 10, secY, secX - 10, secY + secH);
                g.DrawLine(penDim, secX - 14, secY, secX - 6, secY);
                g.DrawLine(penDim, secX - 14, secY + secH, secX - 6, secY + secH);
                var state = g.Save();
                g.TranslateTransform(secX - 18, secY + secH / 2);
                g.RotateTransform(-90);
                g.DrawString(isEn ? "h (height)" : "h (cạnh dọc)", fDim, brDim, 0, 0, sfCen);
                g.Restore(state);

                // Outer hoop
                int tieInset = 12;
                var tieRect = new Rectangle(secX + tieInset, secY + tieInset, secW - 2 * tieInset, secH - 2 * tieInset);
                using var tiePen = new Pen(cTie, 1.8f);
                g.DrawRectangle(tiePen, tieRect);
                // 135 deg hook detail at top-left
                g.DrawLine(tiePen, tieRect.Left, tieRect.Top + 14, tieRect.Left + 14, tieRect.Top);
                g.DrawLine(tiePen, tieRect.Left + 14, tieRect.Top, tieRect.Left + 8, tieRect.Top + 14);

                // Diamond tie (midpoints)
                Point pT = new Point(tieRect.Left + tieRect.Width / 2, tieRect.Top);
                Point pR = new Point(tieRect.Right, tieRect.Top + tieRect.Height / 2);
                Point pB = new Point(tieRect.Left + tieRect.Width / 2, tieRect.Bottom);
                Point pL = new Point(tieRect.Left, tieRect.Top + tieRect.Height / 2);
                using var diaPen = new Pen(cTieDia, 1.5f);
                g.DrawPolygon(diaPen, new[] { pT, pR, pB, pL });

                // Cross ties (C-hooks)
                using var crossPen = new Pen(cTieCr, 1.3f) { DashStyle = DashStyle.Dash };
                // Horizontal cross tie through 2nd row
                int yRow2 = tieRect.Top + (int)(tieRect.Height * 0.33);
                g.DrawLine(crossPen, tieRect.Left, yRow2, tieRect.Right, yRow2);
                // Vertical cross tie through 3rd column
                int xCol3 = tieRect.Left + (int)(tieRect.Width * 0.67);
                g.DrawLine(crossPen, xCol3, tieRect.Top, xCol3, tieRect.Bottom);

                // Main Rebars (4x4 = 12 bars along perimeter)
                int[] colX = { tieRect.Left + 8, tieRect.Left + (int)(tieRect.Width * 0.33), tieRect.Left + (int)(tieRect.Width * 0.67), tieRect.Right - 8 };
                int[] rowY = { tieRect.Top + 8,  tieRect.Top + (int)(tieRect.Height * 0.33), tieRect.Top + (int)(tieRect.Height * 0.67), tieRect.Bottom - 8 };

                using var barBr = new SolidBrush(cBar);
                using var barPen = new Pen(cOutline, 0.8f);
                for (int c = 0; c < 4; c++)
                {
                    for (int r = 0; r < 4; r++)
                    {
                        if (c > 0 && c < 3 && r > 0 && r < 3) continue; // hollow core
                        float bx = colX[c], by = rowY[r];
                        g.FillEllipse(barBr, bx - 4.5f, by - 4.5f, 9f, 9f);
                        g.DrawEllipse(barPen, bx - 4.5f, by - 4.5f, 9f, 9f);
                    }
                }

                // Bottom notes card under section
                using (var cardBr = new SolidBrush(cCard)) g.FillRectangle(cardBr, 26, 276, 238, 72);
                g.DrawRectangle(penBorder, 26, 276, 238, 72);
                g.DrawString(isEn ? "SECTION FORMULAS & RULES" : "CÔNG THỨC & QUY CÁCH TIẾT DIỆN", fSec, brOutline, 32, 280);
                string secTxt = isEn
                    ? "• Total bars: n = 2(nb + nh - 2)\n• Clear spacing: a >= max(d, 25mm)\n• Seismic 135deg hooks: extension >= 10d"
                    : "• Tổng số thanh: n = 2(nb + nh - 2)\n• Khoảng hở tịnh tiến: a >= max(d, 25mm)\n• Móc uốn kháng chấn: 135°, neo >= 10d";
                g.DrawString(secTxt, fBdy, brOutline, 32, 294);

                // Right side: Specification Card
                using (var cardBr = new SolidBrush(cCard)) g.FillRectangle(cardBr, 276, 46, 254, 302);
                g.DrawRectangle(penBorder, 276, 46, 254, 302);

                using (var hdrBg = new SolidBrush(Color.FromArgb(236, 239, 241)))
                    g.FillRectangle(hdrBg, 276, 46, 254, 22);
                g.DrawString(isEn ? "REBAR DETAILING SPECIFICATIONS" : "QUY CÁCH CẤU TẠO CỐT THÉP", fSec, brOutline, 284, 51);

                // Legend items
                int legY = 74;
                void AddLegend(Brush b, Pen p, string label, bool isCircle)
                {
                    if (isCircle)
                    {
                        g.FillEllipse(b, 286, legY + 2, 8, 8);
                        g.DrawEllipse(penOutline, 286, legY + 2, 8, 8);
                    }
                    else
                    {
                        g.DrawLine(p, 284, legY + 6, 298, legY + 6);
                    }
                    g.DrawString(label, fBdy, brOutline, 304, legY);
                    legY += 19;
                }

                AddLegend(barBr, null, isEn ? "Main bars: dia 16 - 32 mm" : "Thép chủ: d = 16 - 32 mm", true);
                AddLegend(null, tiePen, isEn ? "Outer tie: 135deg hook, >= 10d" : "Đai ngoài: móc 135°, neo >= 10d", false);
                AddLegend(null, diaPen, isEn ? "Diamond tie: holds side bars" : "Đai kim cương: giữ thanh biên", false);
                AddLegend(null, crossPen, isEn ? "Cross-ties: C-hook / U-tie" : "Đai móc C / đai chữ U (cross-tie)", false);
                using (var cPen = new Pen(cDim, 1f))
                    AddLegend(null, cPen, isEn ? "Cover c: >= 25 mm (interior)" : "Lớp bảo vệ c: >= 25 mm (cột nhà)", false);

                g.DrawLine(penBorder, 284, legY + 2, 522, legY + 2);
                legY += 8;

                g.DrawString(isEn ? "STANDARD REQUIREMENTS:" : "YÊU CẦU TIÊU CHUẨN (TCVN / ACI):", fSec, brOutline, 284, legY);
                legY += 16;
                string stdTxt = isEn
                    ? "1. Reinforcement ratio:\n   rho = 1.0% - 3.0% (max 4.0% at lap)\n2. Side bar restraint:\n   Every corner & bar spaced > 150mm\n   must be braced by tie bend.\n3. Hook staggering:\n   Hooks must alternate at successive\n   tie levels along the column height."
                    : "1. Hàm lượng cốt thép hợp lý:\n   mu = 1.0% - 3.0% (tối đa 4.0% tại nối)\n2. Cố định thanh dọc:\n   Thanh góc và thanh cách nhau > 150 mm\n   phải được giữ bởi góc uốn của đai.\n3. Bố trí so le móc đai:\n   Móc uốn phải đặt so le góc qua từng\n   lớp đai dọc theo chiều cao cột.";
                g.DrawString(stdTxt, fSml, brOutline, 284, legY);
            }

            // ----------------------------------------------------------------
            // TAB 1: MẶT ĐỨNG (ELEVATION A1/A2/A1 ZONES)
            // ----------------------------------------------------------------
            private static void DrawElevationTab(Graphics g, bool isEn,
                Color cConc, Color cOutline, Color cBar, Color cTie, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "COLUMN ELEVATION - STIRRUP ZONES A1 / A2 / A1" : "SƠ ĐỒ BỐ TRÍ CỐT THÉP MẶT ĐỨNG CỘT - VÙNG ĐAI A1 / A2 / A1";
                string sub   = isEn ? "Dense zone A1, intermediate A2 & cranked lap splice" : "Phân vùng đai dày A1, đai thưa A2 và vùng uốn cổ chai nối chồng";
                DrawHeaderBanner(g, title, sub, cHdr, fHdr, fSub, brHdrText, brSubText);

                // Column body
                int colX = 90, colY = 48, colW = 90, colH = 246;
                var colRect = new Rectangle(colX, colY, colW, colH);
                using (var concBr = new SolidBrush(cConc)) g.FillRectangle(concBr, colRect);
                g.DrawRectangle(penOutline, colRect);

                // Level floor lines
                using var lvp = new Pen(cDim, 1f) { DashStyle = DashStyle.DashDot };
                g.DrawLine(lvp, 30, colY, 210, colY);
                g.DrawLine(lvp, 30, colY + colH, 210, colY + colH);
                g.DrawString(isEn ? "Upper Floor (Level n+1)" : "Sàn tầng trên (Level n+1)", fSml, brDim, 28, colY - 12);
                g.DrawString(isEn ? "Lower Floor (Level n)" : "Sàn tầng dưới (Level n)", fSml, brDim, 28, colY + colH + 2);

                // Zone heights: top A1 = 60px, bottom A1 = 60px, middle A2 = 126px
                int a1H = 60;
                int a2H = colH - 2 * a1H;
                int a1TopY = colY;
                int a2TopY = colY + a1H;
                int a1BotY = colY + colH - a1H;

                // Stirrup lines
                using var tiePenA1 = new Pen(cTie, 1.4f);
                using var tiePenA2 = new Pen(Color.FromArgb(239, 83, 80), 1.1f);

                // Top A1
                for (int y = a1TopY + 6; y < a1TopY + a1H; y += 8)
                    g.DrawLine(tiePenA1, colX + 4, y, colX + colW - 4, y);

                // Middle A2
                for (int y = a2TopY + 10; y < a2TopY + a2H; y += 18)
                    g.DrawLine(tiePenA2, colX + 4, y, colX + colW - 4, y);

                // Bottom A1
                for (int y = a1BotY + 6; y < a1BotY + a1H; y += 8)
                    g.DrawLine(tiePenA1, colX + 4, y, colX + colW - 4, y);

                // Main vertical bars
                using var barPen = new Pen(cBar, 2.2f);
                int bL = colX + 16, bR = colX + colW - 16;
                // Left bar
                g.DrawLine(barPen, bL, colY - 10, bL, colY + colH + 16);
                // Right bar
                g.DrawLine(barPen, bR, colY - 10, bR, colY + colH + 16);
                // Middle bar with cranked splice schematic
                int bM = colX + colW / 2;
                g.DrawLine(barPen, bM, colY - 6, bM, colY + 160);
                g.DrawLine(barPen, bM, colY + 160, bM - 5, colY + 175);
                g.DrawLine(barPen, bM - 5, colY + 175, bM - 5, colY + colH + 16);
                // Lap bar
                using var lapPen = new Pen(Color.FromArgb(30, 136, 229), 2f) { DashStyle = DashStyle.Dash };
                g.DrawLine(lapPen, bM, colY + 140, bM, colY + 210);

                // Top 90 deg anchorage
                g.DrawLine(barPen, bL, colY - 10, bL + 12, colY - 10);
                g.DrawLine(barPen, bR, colY - 10, bR - 12, colY - 10);

                // Zone brackets on right
                int bkX = colX + colW + 10;
                void DrawBracket(int y1, int y2, string titleTxt, string descTxt)
                {
                    g.DrawLine(penDim, bkX, y1, bkX, y2);
                    g.DrawLine(penDim, bkX, y1, bkX + 4, y1);
                    g.DrawLine(penDim, bkX, y2, bkX + 4, y2);
                    g.DrawString(titleTxt, fSec, brOutline, bkX + 7, (y1 + y2) / 2 - 8);
                    g.DrawString(descTxt, fSml, brDim, bkX + 7, (y1 + y2) / 2 + 3);
                }

                DrawBracket(a1TopY, a1TopY + a1H, isEn ? "Zone A1 (dense)" : "Vùng A1 (đai dày)", "a1 <= min(h/4, 100)");
                DrawBracket(a2TopY, a2TopY + a2H, isEn ? "Zone A2 (middle)" : "Vùng A2 (đai thưa)", "a2 <= min(h/2, 200)");
                DrawBracket(a1BotY, a1BotY + a1H, isEn ? "Zone A1 (dense)" : "Vùng A1 (đai dày)", "H_cr >= max(H/6, 450)");

                // Right side: Design Guide Card
                using (var cardBr = new SolidBrush(cCard)) g.FillRectangle(cardBr, 276, 46, 254, 302);
                g.DrawRectangle(penBorder, 276, 46, 254, 302);

                using (var hdrBg = new SolidBrush(Color.FromArgb(236, 239, 241)))
                    g.FillRectangle(hdrBg, 276, 46, 254, 22);
                g.DrawString(isEn ? "SEISMIC DETAILING CRITERIA" : "TIÊU CHUẨN ĐAI KHÁNG CHẤN", fSec, brOutline, 284, 51);

                string gTxt = isEn
                    ? "1. Critical Zone Length (H_cr):\n   H_cr >= max(H_clear / 6, h_col, 450mm)\n   High bending & shear plastic hinge.\n\n2. Zone A1 Spacing (Dense):\n   a1 <= min(h/4, 6~8 d_bar, 100~150mm)\n   Prevents bar buckling under axial loads.\n\n3. Zone A2 Spacing (Middle):\n   a2 <= min(h/2, 12~15 d_bar, 200~300mm)\n   Common practice: a2 = 2 * a1.\n\n4. Splice Zone Recommendation:\n   • Splice within middle 1/2 of column (A2).\n   • NEVER splice inside plastic hinge A1.\n   • Lap length L_lap >= 40d with dense ties."
                    : "1. Chiều dài vùng tới hạn A1 (H_cr):\n   H_cr >= max(H thông thủy / 6, h_cột, 450 mm)\n   Vị trí khớp dẻo chịu mô men và lực cắt lớn.\n\n2. Bước đai vùng A1 (đai dày):\n   a1 <= min(h/4, 6~8 d_thép, 100~150 mm)\n   Chống phình cốt thép chủ khi chịu nén.\n\n3. Bước đai vùng A2 (thân cột):\n   a2 <= min(h/2, 12~15 d_thép, 200~300 mm)\n   Thông thường quy định a2 = 2 * a1.\n\n4. Vị trí nối cốt thép:\n   • Nối ở 1/2 giữa chiều cao cột (vùng A2).\n   • Tránh nối trong vùng khớp dẻo A1.\n   • Chiều dài L_nối >= 40d, bố trí đai dày.";
                g.DrawString(gTxt, fSml, brOutline, 284, 76);
            }

            // ----------------------------------------------------------------
            // TAB 2: CẤU TẠO ĐAI (STIRRUP LAYOUT TYPES)
            // ----------------------------------------------------------------
            private static void DrawStirrupTypesTab(Graphics g, bool isEn,
                Color cConc, Color cOutline, Color cBar, Color cTie, Color cTieDia, Color cTieCr, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "COLUMN TIE / STIRRUP CONFIGURATION TYPES" : "CÁC DẠNG CẤU TẠO THÉP ĐAI CỘT CHỮ NHẬT";
                string sub   = isEn ? "Single outer hoop, diamond tie & cross-ties by section size" : "Lựa chọn đai đơn, đai kim cương và đai móc C theo kích thước và số thanh";
                DrawHeaderBanner(g, title, sub, cHdr, fHdr, fSub, brHdrText, brSubText);

                int cardW = 162, cardH = 208, cardY = 46;
                using var barBr = new SolidBrush(cBar);
                using var concBr = new SolidBrush(cConc);
                using var tiePen = new Pen(cTie, 1.8f);

                // --- CARD 1: Đai đơn ---
                int c1X = 14;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, c1X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, c1X, cardY, cardW, cardH);
                g.DrawString(isEn ? "1. SINGLE OUTER HOOP" : "1. ĐAI ĐƠN (CHU VI)", fSec, brOutline, c1X + 10, cardY + 8);

                int b1X = c1X + 31, b1Y = cardY + 28, b1W = 100, b1H = 100;
                g.FillRectangle(concBr, b1X, b1Y, b1W, b1H);
                g.DrawRectangle(penOutline, b1X, b1Y, b1W, b1H);
                g.DrawRectangle(tiePen, b1X + 8, b1Y + 8, b1W - 16, b1H - 16);
                // 4 corners + 2 intermediate = 6 bars
                float[] b1Xs = { b1X + 14, b1X + b1W / 2, b1X + b1W - 14 };
                float[] b1Ys = { b1Y + 14, b1Y + b1H - 14 };
                foreach (var y in b1Ys) foreach (var x in b1Xs) { g.FillEllipse(barBr, x - 3.5f, y - 3.5f, 7f, 7f); g.DrawEllipse(penOutline, x - 3.5f, y - 3.5f, 7f, 7f); }

                string desc1 = isEn
                    ? "• Size: B, H <= 350mm\n• Bars per side: <= 2-3\n• Simple small columns\n• No inner ties needed"
                    : "• Tiết diện: B, H <= 350 mm\n• Số thanh mỗi cạnh <= 2-3\n• Cột nhà dân / nhịp nhỏ\n• Không cần đai phụ trong";
                g.DrawString(desc1, fSml, brOutline, c1X + 8, cardY + 138);

                // --- CARD 2: Đai + Kim cương ---
                int c2X = 188;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, c2X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, c2X, cardY, cardW, cardH);
                g.DrawString(isEn ? "2. HOOP + DIAMOND TIE" : "2. ĐAI + KIM CƯƠNG", fSec, brOutline, c2X + 10, cardY + 8);

                int b2X = c2X + 31, b2Y = cardY + 28, b2W = 100, b2H = 100;
                g.FillRectangle(concBr, b2X, b2Y, b2W, b2H);
                g.DrawRectangle(penOutline, b2X, b2Y, b2W, b2H);
                var t2Rect = new Rectangle(b2X + 8, b2Y + 8, b2W - 16, b2H - 16);
                g.DrawRectangle(tiePen, t2Rect);
                using (var diaPen = new Pen(cTieDia, 1.5f))
                {
                    Point p1 = new Point(t2Rect.Left + t2Rect.Width / 2, t2Rect.Top);
                    Point p2 = new Point(t2Rect.Right, t2Rect.Top + t2Rect.Height / 2);
                    Point p3 = new Point(t2Rect.Left + t2Rect.Width / 2, t2Rect.Bottom);
                    Point p4 = new Point(t2Rect.Left, t2Rect.Top + t2Rect.Height / 2);
                    g.DrawPolygon(diaPen, new[] { p1, p2, p3, p4 });
                }
                // 3x3 layout = 8 perimeter bars
                float[] b2Xs = { t2Rect.Left + 6, t2Rect.Left + t2Rect.Width / 2, t2Rect.Right - 6 };
                float[] b2Ys = { t2Rect.Top + 6,  t2Rect.Top + t2Rect.Height / 2, t2Rect.Bottom - 6 };
                for (int xi = 0; xi < 3; xi++)
                {
                    for (int yi = 0; yi < 3; yi++)
                    {
                        if (xi == 1 && yi == 1) continue;
                        float x = b2Xs[xi], y = b2Ys[yi];
                        g.FillEllipse(barBr, x - 3.5f, y - 3.5f, 7f, 7f);
                        g.DrawEllipse(penOutline, x - 3.5f, y - 3.5f, 7f, 7f);
                    }
                }

                string desc2 = isEn
                    ? "• Size: B, H >= 400mm\n• Bars per side: >= 3\n• Braces perimeter side bars\n• High torsional stiffness"
                    : "• Tiết diện: B, H >= 400 mm\n• Số thanh mỗi cạnh >= 3\n• Giữ ổn định các thanh biên\n• Tăng khả năng kháng xoắn";
                g.DrawString(desc2, fSml, brOutline, c2X + 8, cardY + 138);

                // --- CARD 3: Đai + Cross-links ---
                int c3X = 362;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, c3X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, c3X, cardY, cardW, cardH);
                g.DrawString(isEn ? "3. HOOP + CROSS-TIES" : "3. ĐAI + ĐAI MÓC C", fSec, brOutline, c3X + 10, cardY + 8);

                int b3X = c3X + 31, b3Y = cardY + 28, b3W = 100, b3H = 100;
                g.FillRectangle(concBr, b3X, b3Y, b3W, b3H);
                g.DrawRectangle(penOutline, b3X, b3Y, b3W, b3H);
                var t3Rect = new Rectangle(b3X + 8, b3Y + 8, b3W - 16, b3H - 16);
                g.DrawRectangle(tiePen, t3Rect);
                using (var crPen = new Pen(cTieCr, 1.4f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(crPen, t3Rect.Left, t3Rect.Top + t3Rect.Height / 2, t3Rect.Right, t3Rect.Top + t3Rect.Height / 2);
                    g.DrawLine(crPen, t3Rect.Left + t3Rect.Width / 2, t3Rect.Top, t3Rect.Left + t3Rect.Width / 2, t3Rect.Bottom);
                }
                for (int xi = 0; xi < 3; xi++)
                {
                    for (int yi = 0; yi < 3; yi++)
                    {
                        if (xi == 1 && yi == 1) continue;
                        float x = b2Xs[xi] - c2X + c3X, y = b2Ys[yi];
                        g.FillEllipse(barBr, x - 3.5f, y - 3.5f, 7f, 7f);
                        g.DrawEllipse(penOutline, x - 3.5f, y - 3.5f, 7f, 7f);
                    }
                }

                string desc3 = isEn
                    ? "• Rectangular / wall columns\n• Ties intermediate bars\n• Easy site installation\n• Standard 135deg / 90deg"
                    : "• Cột dẹt / tiết diện lớn\n• Khống chế thanh thép giữa\n• Thuận tiện thi công\n• Móc chuẩn 135 / 90 độ";
                g.DrawString(desc3, fSml, brOutline, c3X + 8, cardY + 138);

                // --- Bottom Guideline Banner ---
                using (var bBr = new SolidBrush(Color.FromArgb(236, 239, 241))) g.FillRectangle(bBr, 14, 262, 510, 86);
                g.DrawRectangle(penBorder, 14, 262, 510, 86);
                g.DrawString(isEn ? "MANDATORY DETAILING PRINCIPLES (TCVN 5574:2018 / ACI 318):" : "NGUYÊN TẮC BẮT BUỘC KHI CẤU TẠO ĐAI (TCVN 5574:2018 / ACI 318):", fSec, brOutline, 22, 268);
                string bTxt = isEn
                    ? "• When clear spacing between adjacent longitudinal bars s > 150mm: An inner tie (diamond or cross-tie) is REQUIRED.\n• Seismic stirrup hooks must be bent at 135 degrees with an extension of at least 10d (>= 75mm).\n• Stirrup hook locations must alternate diagonally and vertically from one tie level to the next."
                    : "• Khi khoảng cách giữa các thanh thép dọc kề nhau s > 150 mm: BẮT BUỘC bố trí đai phụ (kim cương hoặc đai móc C).\n• Móc đai chịu chấn uốn 135 độ với đoạn thẳng neo dài >= 10d (hoặc >= 75 mm); không dùng móc 90 độ cho đai ngoài.\n• Các góc móc đai phải được bố trí so le theo đường chéo và xoay góc qua các lớp đai liên tiếp dọc thân cột.";
                g.DrawString(bTxt, fSml, brOutline, 22, 284);
            }

            // ----------------------------------------------------------------
            // TAB 3: NEO / NỐI (ANCHORAGE & LAP SPLICE)
            // ----------------------------------------------------------------
            private static void DrawAnchorageTab(Graphics g, bool isEn,
                Color cConc, Color cHatch, Color cOutline, Color cBar, Color cTie, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "COLUMN REBAR ANCHORAGE & LAP SPLICE DETAILS" : "CHI TIẾT NEO CHÂN CỘT, UỐN CỔ CHAI & NỐI CHỒNG";
                string sub   = isEn ? "Footing L-bend anchorage, 1:6 cranked splice & staggered lap rules" : "Neo móng bẻ L, uốn cổ chai thay đổi tiết diện và quy cách nối chồng so le";
                DrawHeaderBanner(g, title, sub, cHdr, fHdr, fSub, brHdrText, brSubText);

                int cardW = 162, cardH = 208, cardY = 46;
                using var barPen = new Pen(cBar, 2f);
                using var barPen2 = new Pen(Color.FromArgb(30, 136, 229), 1.8f);
                using var tiePen = new Pen(cTie, 1.2f);
                using var concBr = new SolidBrush(cConc);

                // --- PANEL A: Neo chân móng ---
                int p1X = 14;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, p1X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, p1X, cardY, cardW, cardH);
                g.DrawString(isEn ? "A. FOOTING ANCHORAGE" : "A. NEO CHÂN MÓNG (L-BEND)", fSec, brOutline, p1X + 8, cardY + 8);

                // Footing & column concrete
                int ftY = cardY + 105;
                g.FillRectangle(concBr, p1X + 16, ftY, cardW - 32, 45);
                g.FillRectangle(concBr, p1X + 46, cardY + 40, 70, ftY - (cardY + 40));
                g.DrawRectangle(penOutline, p1X + 46, cardY + 40, 70, ftY - (cardY + 40));
                g.DrawRectangle(penOutline, p1X + 16, ftY, cardW - 32, 45);

                // Level line
                using (var lvp = new Pen(cDim, 1f) { DashStyle = DashStyle.DashDot })
                    g.DrawLine(lvp, p1X + 10, ftY, p1X + cardW - 10, ftY);
                g.DrawString(isEn ? "Top of Footing" : "Mặt móng", fSml, brDim, p1X + 18, ftY - 11);

                // Column bars with L-bend
                int b1L = p1X + 60, b1R = p1X + 102;
                g.DrawLine(barPen, b1L, cardY + 45, b1L, ftY + 32);
                g.DrawLine(barPen, b1L, ftY + 32, b1L + 28, ftY + 32); // L-bend inward
                g.DrawLine(barPen, b1R, cardY + 45, b1R, ftY + 32);
                g.DrawLine(barPen, b1R, ftY + 32, b1R - 28, ftY + 32); // L-bend inward

                string aTxt = isEn
                    ? "• L-bend hook >= 200mm\n• L_anc >= 30d ~ 35d\n• Rests on bottom mat\n• Hooks face column core"
                    : "• Chân bẻ L >= 200 mm\n• L_neo >= 30d ~ 35d\n• Đặt trên lưới thép móng\n• Móc L hướng vào trong";
                g.DrawString(aTxt, fSml, brOutline, p1X + 8, cardY + 155);

                // --- PANEL B: Uốn cổ chai 1:6 ---
                int p2X = 188;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, p2X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, p2X, cardY, cardW, cardH);
                g.DrawString(isEn ? "B. CRANKED SPLICE (1:6)" : "B. UỐN CỔ CHAI (1:6)", fSec, brOutline, p2X + 8, cardY + 8);

                // Upper narrower column & lower wider column
                int colMidY = cardY + 85;
                g.FillRectangle(concBr, p2X + 46, cardY + 35, 70, colMidY - (cardY + 35));
                g.FillRectangle(concBr, p2X + 38, colMidY, 86, 60);
                g.DrawRectangle(penOutline, p2X + 46, cardY + 35, 70, colMidY - (cardY + 35));
                g.DrawRectangle(penOutline, p2X + 38, colMidY, 86, 60);

                // Cranked bar
                int b2L = p2X + 50, b2R = p2X + 112;
                // Lower bar cranked inward
                g.DrawLine(barPen, b2L, cardY + 140, b2L, colMidY + 15);
                g.DrawLine(barPen, b2L, colMidY + 15, b2L + 8, colMidY - 20);
                g.DrawLine(barPen, b2L + 8, colMidY - 20, b2L + 8, cardY + 40);
                // Upper continuing bar
                g.DrawLine(barPen2, b2L + 12, cardY + 95, b2L + 12, cardY + 40);

                // Extra ties at crank
                for (int ty = colMidY - 22; ty <= colMidY + 18; ty += 8)
                    g.DrawLine(tiePen, p2X + 40, ty, p2X + 122, ty);

                g.DrawString("Slope <= 1:6", fDim, brDim, p2X + 66, colMidY - 6);

                string bTxt = isEn
                    ? "• Max slope: 1 in 6\n• Extra ties at bends\n• If offset > 1:6, use\n  separate dowel bars"
                    : "• Độ dốc vát <= 1:6\n• Bố trí đai dày chỗ uốn\n• Nếu lệch > 1:6: phải\n  dùng thép chờ riêng";
                g.DrawString(bTxt, fSml, brOutline, p2X + 8, cardY + 155);

                // --- PANEL C: Nối chồng so le ---
                int p3X = 362;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, p3X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, p3X, cardY, cardW, cardH);
                g.DrawString(isEn ? "C. STAGGERED SPLICE" : "C. NỐI CHỒNG SO LE", fSec, brOutline, p3X + 8, cardY + 8);

                // Concrete strip
                g.FillRectangle(concBr, p3X + 42, cardY + 35, 78, 110);
                g.DrawRectangle(penOutline, p3X + 42, cardY + 35, 78, 110);

                // Pair 1 splice (higher)
                int l1X = p3X + 56;
                g.DrawLine(barPen, l1X, cardY + 140, l1X, cardY + 70);
                g.DrawLine(barPen2, l1X + 4, cardY + 105, l1X + 4, cardY + 40);
                // Dimension L_lap
                g.DrawLine(penDim, l1X - 6, cardY + 70, l1X - 6, cardY + 105);
                g.DrawString("L_lap", fDim, brDim, l1X - 22, cardY + 82);

                // Pair 2 splice (lower - staggered)
                int l2X = p3X + 96;
                g.DrawLine(barPen, l2X, cardY + 140, l2X, cardY + 105);
                g.DrawLine(barPen2, l2X + 4, cardY + 135, l2X + 4, cardY + 40);

                // Ties along splice
                for (int ty = cardY + 45; ty <= cardY + 138; ty += 12)
                    g.DrawLine(tiePen, p3X + 44, ty, p3X + 118, ty);

                string cTxt = isEn
                    ? "• Lap length: L_lap >= 40d\n• Stagger offset >= 1.3 L_lap\n• Max 50% spliced at section\n• Dense ties across lap zone"
                    : "• Chiều dài L_nối >= 40d\n• Khoảng cách so le >= 1.3 L_nối\n• Tối đa 50% nối tại 1 mặt cắt\n• Bố trí đai dày suốt đoạn nối";
                g.DrawString(cTxt, fSml, brOutline, p3X + 8, cardY + 155);

                // --- Bottom Summary Banner ---
                using (var bBr = new SolidBrush(Color.FromArgb(236, 239, 241))) g.FillRectangle(bBr, 14, 262, 510, 86);
                g.DrawRectangle(penBorder, 14, 262, 510, 86);
                g.DrawString(isEn ? "ENGINEERING GUIDELINES FOR SPLICES & ANCHORAGES:" : "CHỈ DẪN KỸ THUẬT QUAN TRỌNG VỀ NEO VÀ NỐI CỐT THÉP:", fSec, brOutline, 22, 268);
                string bBanner = isEn
                    ? "• Lap lengths depend on concrete grade (B25, B30) and rebar grade (CB300, CB400, Grade 60).\n• Increase lap length by 1.3x if 100% of bars are spliced at the same cross section.\n• Lap splices are strictly prohibited in seismic plastic hinge zones (Zone A1 at column ends)."
                    : "• Chiều dài neo và nối phụ thuộc cấp độ bền bê tông (B20, B25, B30) và nhóm thép (CB300-V, CB400-V, CB500-V).\n• Tăng chiều dài nối chồng lên 1.3 lần nếu nối 100% cốt thép tại cùng một vị trí mặt cắt ngang.\n• Tuyệt đối không nối cốt thép trong vùng dẻo (vùng A1 đai dày đầu cột và chân cột sát dầm/sàn).";
                g.DrawString(bBanner, fSml, brOutline, 22, 284);
            }

            // ================================================================
            // GENERIC REFERENCE DRAWINGS (BEAM, SLAB, FOUNDATION, CIRCULAR)
            // ================================================================
            private static void DrawGenericReference(Graphics g, RebarReferenceKind kind, int view)
            {
                using var concrete = new Pen(Color.FromArgb(104, 115, 125), 1.5f);
                using var bar = new Pen(Color.FromArgb(0, 122, 194), 3);
                using var secondary = new Pen(Color.FromArgb(0, 150, 120), 2);
                using var dimension = new Pen(Color.FromArgb(104, 115, 125), 1);
                using var font = new Font("Segoe UI", 10);

                Text(g, font, "Sơ đồ cấu tạo - không theo tỷ lệ", 24, 14);
                if (view == 3) { Anchorage(g, bar, secondary, dimension, font); return; }
                bool column = kind == RebarReferenceKind.RectangularColumn || kind == RebarReferenceKind.CircularColumn;
                bool mesh = kind == RebarReferenceKind.Slab || kind == RebarReferenceKind.Foundation;
                var box = column && view == 1 ? new Rectangle(190, 55, 160, 220)
                    : new Rectangle(90, 75, 360, 180);
                bool circle = kind == RebarReferenceKind.CircularColumn && view != 1;
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
                else if (mesh && view == 0)
                {
                    for (int x = 110; x <= 430; x += 32) g.DrawLine(bar, x, 95, x, 235);
                    for (int y = 95; y <= 235; y += 28) g.DrawLine(secondary, 110, y, 430, y);
                    Text(g, font, "X: dX / aX", 90, 325);
                    Text(g, font, "Y: dY / aY", 290, 325);
                }
                else if (view == 2 || (column && view == 0))
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
                    Text(g, font, "Đai: d_đ / a_đ", 90, 310);
                }
                else
                {
                    g.DrawLines(bar, new[] { new Point(110, 120), new Point(110, 100), new Point(430, 100), new Point(430, 120) });
                    g.DrawLines(bar, new[] { new Point(110, 210), new Point(110, 230), new Point(430, 230), new Point(430, 210) });
                    if (kind == RebarReferenceKind.Beam)
                        for (int x = 125; x <= 415; x += 25) g.DrawLine(secondary, x, 95, x, 235);
                    Text(g, font, "Thép trên / thép dưới", 90, 325);
                }
                Dimension(g, dimension, font, box.Left, box.Right, box.Bottom + 22,
                    circle ? "D" : column ? "b" : view == 0 ? "Lx" : "b");
                Text(g, font, circle ? "D" : column ? (view == 1 ? "H" : "h") : view == 0 ? "Ly" : "h",
                    box.Right + 12, box.Top + 70);
                Text(g, font, "c", box.Left + 5, box.Top + 2);
            }

            private static void Anchorage(Graphics g, Pen bar, Pen secondary, Pen dimension, Font font)
            {
                Text(g, font, "Neo đầu thanh", 70, 52);
                g.DrawLines(bar, new[] { new Point(70, 110), new Point(380, 110), new Point(380, 155) });
                Dimension(g, dimension, font, 260, 380, 70, "l_neo = k_neo × d");
                Text(g, font, "r uốn", 392, 118);
                Text(g, font, "Nối chồng", 70, 182);
                g.DrawLine(bar, 70, 235, 340, 235);
                g.DrawLine(secondary, 220, 248, 470, 248);
                Dimension(g, dimension, font, 220, 340, 281, "l_nối = k_nối × d");
                Text(g, font, "d: đường kính thanh; k: hệ số do người dùng quy định", 24, 324);
            }

            private static void Dot(Graphics g, float x, float y)
            {
                using var brush = new SolidBrush(Color.FromArgb(0, 122, 194));
                g.FillEllipse(brush, x - 4, y - 4, 8, 8);
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
                using var brush = new SolidBrush(Color.FromArgb(38, 50, 56));
                g.DrawString(text, font, brush, x, y);
            }
        }
    }
}

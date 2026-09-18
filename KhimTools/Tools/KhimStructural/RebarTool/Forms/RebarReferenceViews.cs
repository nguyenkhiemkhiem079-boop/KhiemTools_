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
            string pageTitle = isEn ? "2D Reference" : "Tham kháº£o 2D";
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
                    : new[] { "Tiáº¿t diá»‡n", "Máº·t Ä‘á»©ng", "Cáº¥u táº¡o Ä‘ai", "Neo / Ná»‘i" })
                : (isEn
                    ? new[] { "Plan", "Longitudinal", "Cross Section", "Anchorage" }
                    : new[] { "Máº·t báº±ng", "Cáº¯t dá»c", "Cáº¯t ngang", "Neo / Ná»‘i" });

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
                    case 0: // Tiáº¿t diá»‡n / Cross Section (4x4 arrangement)
                        DrawSectionTab(g, isEn, cConc, cHatch, cOutline, cBar, cTie, cTieDia, cTieCr, cDim, cHdr, cBorder, cCard,
                            fHdr, fSub, fSec, fBdy, fSml, fDim, brOutline, brDim, brHdrText, brSubText, penOutline, penBorder, penDim);
                        break;

                    case 1: // Máº·t Ä‘á»©ng / Elevation (A1/A2/A1 zones)
                        DrawElevationTab(g, isEn, cConc, cOutline, cBar, cTie, cDim, cHdr, cBorder, cCard,
                            fHdr, fSub, fSec, fBdy, fSml, fDim, brOutline, brDim, brHdrText, brSubText, penOutline, penBorder, penDim);
                        break;

                    case 2: // Cáº¥u táº¡o Ä‘ai / Stirrup Layouts (3 configurations)
                        DrawStirrupTypesTab(g, isEn, cConc, cOutline, cBar, cTie, cTieDia, cTieCr, cDim, cHdr, cBorder, cCard,
                            fHdr, fSub, fSec, fBdy, fSml, fDim, brOutline, brDim, brHdrText, brSubText, penOutline, penBorder, penDim);
                        break;

                    case 3: // Neo / Ná»‘i / Anchorage & Lap Splice
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
            // TAB 0: TIáº¾T DIá»†N (CROSS SECTION)
            // ----------------------------------------------------------------
            private static void DrawSectionTab(Graphics g, bool isEn,
                Color cConc, Color cHatch, Color cOutline, Color cBar, Color cTie, Color cTieDia, Color cTieCr, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "RECTANGULAR COLUMN SECTION - REBAR ARRANGEMENT" : "TIáº¾T DIá»†N Cá»˜T ÄIá»‚N HÃŒNH - Bá» TRÃ THÃ‰P B Ã— H";
                string sub   = isEn ? "Outer stirrup, diamond tie & cross-ties (4x4 layout)" : "Äai ngoÃ i, Ä‘ai kim cÆ°Æ¡ng & Ä‘ai mÃ³c C (SÆ¡ Ä‘á»“ 4Ã—4 thanh)";
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
                g.DrawString(isEn ? "b (width)" : "b (cáº¡nh ngang)", fDim, brDim, secX + secW / 2, secY - 18, sfCen);

                // Dimension H on left
                g.DrawLine(penDim, secX - 10, secY, secX - 10, secY + secH);
                g.DrawLine(penDim, secX - 14, secY, secX - 6, secY);
                g.DrawLine(penDim, secX - 14, secY + secH, secX - 6, secY + secH);
                var state = g.Save();
                g.TranslateTransform(secX - 18, secY + secH / 2);
                g.RotateTransform(-90);
                g.DrawString(isEn ? "h (height)" : "h (cáº¡nh dá»c)", fDim, brDim, 0, 0, sfCen);
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
                g.DrawString(isEn ? "SECTION FORMULAS & RULES" : "CÃ”NG THá»¨C & QUY CÃCH TIáº¾T DIá»†N", fSec, brOutline, 32, 280);
                string secTxt = isEn
                    ? "â€¢ Total bars: n = 2(nb + nh - 2)\nâ€¢ Clear spacing: a >= max(d, 25mm)\nâ€¢ Seismic 135deg hooks: extension >= 10d"
                    : "â€¢ Tá»•ng sá»‘ thanh: n = 2(nb + nh - 2)\nâ€¢ Khoáº£ng há»Ÿ tá»‹nh tiáº¿n: a >= max(d, 25mm)\nâ€¢ MÃ³c uá»‘n khÃ¡ng cháº¥n: 135 Ä‘á»™, neo >= 10d";
                g.DrawString(secTxt, fBdy, brOutline, 32, 294);

                // Right side: Specification Card
                using (var cardBr = new SolidBrush(cCard)) g.FillRectangle(cardBr, 276, 46, 254, 302);
                g.DrawRectangle(penBorder, 276, 46, 254, 302);

                using (var hdrBg = new SolidBrush(Color.FromArgb(236, 239, 241)))
                    g.FillRectangle(hdrBg, 276, 46, 254, 22);
                g.DrawString(isEn ? "REBAR DETAILING SPECIFICATIONS" : "QUY CÃCH Cáº¤U Táº O Cá»T THÃ‰P", fSec, brOutline, 284, 51);

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

                AddLegend(barBr, null, isEn ? "Main bars: dia 16 - 32 mm" : "ThÃ©p chá»§: d = 16 - 32 mm", true);
                AddLegend(null, tiePen, isEn ? "Outer tie: 135deg hook, >= 10d" : "Äai ngoÃ i: mÃ³c 135 Ä‘á»™, neo >= 10d", false);
                AddLegend(null, diaPen, isEn ? "Diamond tie: holds side bars" : "Äai kim cÆ°Æ¡ng: giá»¯ thanh biÃªn", false);
                AddLegend(null, crossPen, isEn ? "Cross-ties: C-hook / U-tie" : "Äai mÃ³c C / Ä‘ai chá»¯ U (Cross-tie)", false);
                using (var cPen = new Pen(cDim, 1f))
                    AddLegend(null, cPen, isEn ? "Cover c: >= 25 mm (interior)" : "Lá»›p báº£o vá»‡ c: >= 25mm (cá»™t nhÃ )", false);

                g.DrawLine(penBorder, 284, legY + 2, 522, legY + 2);
                legY += 8;

                g.DrawString(isEn ? "STANDARD REQUIREMENTS:" : "YÃŠU Cáº¦U TIÃŠU CHUáº¨N (TCVN / ACI):", fSec, brOutline, 284, legY);
                legY += 16;
                string stdTxt = isEn
                    ? "1. Reinforcement ratio:\n   rho = 1.0% - 3.0% (max 4.0% at lap)\n2. Side bar restraint:\n   Every corner & bar spaced > 150mm\n   must be braced by tie bend.\n3. Hook staggering:\n   Hooks must alternate at successive\n   tie levels along the column height."
                    : "1. HÃ m lÆ°á»£ng cá»‘t thÃ©p há»£p lÃ½:\n   mu = 1.0% - 3.0% (tá»‘i Ä‘a 4.0% táº¡i ná»‘i)\n2. Cá»‘ Ä‘á»‹nh thanh dá»c:\n   Thanh gÃ³c & thanh cÃ¡ch nhau > 150mm\n   pháº£i Ä‘Æ°á»£c giá»¯ bá»Ÿi gÃ³c uá»‘n cá»§a Ä‘ai.\n3. Bá»‘ trÃ­ so le mÃ³c Ä‘ai:\n   MÃ³c uá»‘n pháº£i Ä‘áº·t so le gÃ³c qua tá»«ng\n   lá»›p Ä‘ai dá»c theo chiá»u cao cá»™t.";
                g.DrawString(stdTxt, fSml, brOutline, 284, legY);
            }

            // ----------------------------------------------------------------
            // TAB 1: Máº¶T Äá»¨NG (ELEVATION A1/A2/A1 ZONES)
            // ----------------------------------------------------------------
            private static void DrawElevationTab(Graphics g, bool isEn,
                Color cConc, Color cOutline, Color cBar, Color cTie, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "COLUMN ELEVATION - STIRRUP ZONES A1 / A2 / A1" : "SÆ  Äá»’ Bá» TRÃ Cá»T THÃ‰P Máº¶T Äá»¨NG Cá»˜T - VÃ™NG ÄAI A1 / A2 / A1";
                string sub   = isEn ? "Dense zone A1, intermediate A2 & cranked lap splice" : "PhÃ¢n vÃ¹ng Ä‘ai dÃ y A1, Ä‘ai thÆ°a A2 & vÃ¹ng uá»‘n cá»• chai ná»‘i chá»“ng";
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
                g.DrawString(isEn ? "Upper Floor (Level n+1)" : "SÃ n táº§ng trÃªn (Level n+1)", fSml, brDim, 28, colY - 12);
                g.DrawString(isEn ? "Lower Floor (Level n)" : "SÃ n táº§ng dÆ°á»›i (Level n)", fSml, brDim, 28, colY + colH + 2);

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

                DrawBracket(a1TopY, a1TopY + a1H, isEn ? "Zone A1 (dense)" : "VÃ¹ng A1 (Ä‘ai dÃ y)", "a1 <= min(h/4, 100)");
                DrawBracket(a2TopY, a2TopY + a2H, isEn ? "Zone A2 (middle)" : "VÃ¹ng A2 (Ä‘ai thÆ°a)", "a2 <= min(h/2, 200)");
                DrawBracket(a1BotY, a1BotY + a1H, isEn ? "Zone A1 (dense)" : "VÃ¹ng A1 (Ä‘ai dÃ y)", "H_cr >= max(H/6, 450)");

                // Right side: Design Guide Card
                using (var cardBr = new SolidBrush(cCard)) g.FillRectangle(cardBr, 276, 46, 254, 302);
                g.DrawRectangle(penBorder, 276, 46, 254, 302);

                using (var hdrBg = new SolidBrush(Color.FromArgb(236, 239, 241)))
                    g.FillRectangle(hdrBg, 276, 46, 254, 22);
                g.DrawString(isEn ? "SEISMIC DETAILING CRITERIA" : "TIÃŠU CHUáº¨N ÄAI KHÃNG CHáº¤N", fSec, brOutline, 284, 51);

                string gTxt = isEn
                    ? "1. Critical Zone Length (H_cr):\n   H_cr >= max(H_clear / 6, h_col, 450mm)\n   High bending & shear plastic hinge.\n\n2. Zone A1 Spacing (Dense):\n   a1 <= min(h/4, 6~8 d_bar, 100~150mm)\n   Prevents bar buckling under axial loads.\n\n3. Zone A2 Spacing (Middle):\n   a2 <= min(h/2, 12~15 d_bar, 200~300mm)\n   Common practice: a2 = 2 * a1.\n\n4. Splice Zone Recommendation:\n   â€¢ Splice within middle 1/2 of column (A2).\n   â€¢ NEVER splice inside plastic hinge A1.\n   â€¢ Lap length L_lap >= 40d with dense ties."
                    : "1. Chiá»u dÃ i vÃ¹ng tá»›i háº¡n A1 (H_cr):\n   H_cr >= max(H_thÃ´ng thá»§y / 6, h_cá»™t, 450mm)\n   Vá»‹ trÃ­ khá»›p dáº»o chá»‹u mÃ´ men vÃ  lá»±c cáº¯t lá»›n.\n\n2. BÆ°á»›c Ä‘ai vÃ¹ng A1 (Ä‘ai dÃ y):\n   a1 <= min(h/4, 6~8 d_thÃ©p, 100~150mm)\n   Chá»‘ng phÃ¬nh cá»‘t thÃ©p chá»§ khi chá»‹u nÃ©n.\n\n3. BÆ°á»›c Ä‘ai vÃ¹ng A2 (thÃ¢n cá»™t):\n   a2 <= min(h/2, 12~15 d_thÃ©p, 200~300mm)\n   ThÃ´ng thÆ°á»ng quy Ä‘á»‹nh a2 = 2 * a1.\n\n4. Vá»‹ trÃ­ ná»‘i cá»‘t thÃ©p:\n   â€¢ Ná»‘i á»Ÿ 1/2 giá»¯a chiá»u cao cá»™t (vÃ¹ng A2).\n   â€¢ TrÃ¡nh ná»‘i trong vÃ¹ng khá»›p dáº»o A1.\n   â€¢ Chiá»u dÃ i L_ná»‘i >= 40d, bá»‘ trÃ­ Ä‘ai dÃ y.";
                g.DrawString(gTxt, fSml, brOutline, 284, 76);
            }

            // ----------------------------------------------------------------
            // TAB 2: Cáº¤U Táº O ÄAI (STIRRUP LAYOUT TYPES)
            // ----------------------------------------------------------------
            private static void DrawStirrupTypesTab(Graphics g, bool isEn,
                Color cConc, Color cOutline, Color cBar, Color cTie, Color cTieDia, Color cTieCr, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "COLUMN TIE / STIRRUP CONFIGURATION TYPES" : "CÃC Dáº NG Cáº¤U Táº O THÃ‰P ÄAI Cá»˜T CHá»® NHáº¬T";
                string sub   = isEn ? "Single outer hoop, diamond tie & cross-ties by section size" : "Lá»±a chá»n Ä‘ai Ä‘Æ¡n, Ä‘ai kim cÆ°Æ¡ng & Ä‘ai mÃ³c C theo kÃ­ch thÆ°á»›c vÃ  sá»‘ thanh";
                DrawHeaderBanner(g, title, sub, cHdr, fHdr, fSub, brHdrText, brSubText);

                int cardW = 162, cardH = 208, cardY = 46;
                using var barBr = new SolidBrush(cBar);
                using var concBr = new SolidBrush(cConc);
                using var tiePen = new Pen(cTie, 1.8f);

                // --- CARD 1: Äai Ä‘Æ¡n ---
                int c1X = 14;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, c1X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, c1X, cardY, cardW, cardH);
                g.DrawString(isEn ? "1. SINGLE OUTER HOOP" : "1. ÄAI ÄÆ N (CHU VI)", fSec, brOutline, c1X + 10, cardY + 8);

                int b1X = c1X + 31, b1Y = cardY + 28, b1W = 100, b1H = 100;
                g.FillRectangle(concBr, b1X, b1Y, b1W, b1H);
                g.DrawRectangle(penOutline, b1X, b1Y, b1W, b1H);
                g.DrawRectangle(tiePen, b1X + 8, b1Y + 8, b1W - 16, b1H - 16);
                // 4 corners + 2 intermediate = 6 bars
                float[] b1Xs = { b1X + 14, b1X + b1W / 2, b1X + b1W - 14 };
                float[] b1Ys = { b1Y + 14, b1Y + b1H - 14 };
                foreach (var y in b1Ys) foreach (var x in b1Xs) { g.FillEllipse(barBr, x - 3.5f, y - 3.5f, 7f, 7f); g.DrawEllipse(penOutline, x - 3.5f, y - 3.5f, 7f, 7f); }

                string desc1 = isEn
                    ? "â€¢ Size: B, H <= 350mm\nâ€¢ Bars per side: <= 2-3\nâ€¢ Simple small columns\nâ€¢ No inner ties needed"
                    : "â€¢ Tiáº¿t diá»‡n: B, H <= 350mm\nâ€¢ Sá»‘ thanh má»—i cáº¡nh <= 2-3\nâ€¢ Cá»™t nhÃ  dÃ¢n / nhá»‹p nhá»\nâ€¢ KhÃ´ng cáº§n Ä‘ai phá»¥ trong";
                g.DrawString(desc1, fSml, brOutline, c1X + 8, cardY + 138);

                // --- CARD 2: Äai + Kim cÆ°Æ¡ng ---
                int c2X = 188;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, c2X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, c2X, cardY, cardW, cardH);
                g.DrawString(isEn ? "2. HOOP + DIAMOND TIE" : "2. ÄAI + KIM CÆ¯Æ NG", fSec, brOutline, c2X + 10, cardY + 8);

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
                    ? "â€¢ Size: B, H >= 400mm\nâ€¢ Bars per side: >= 3\nâ€¢ Braces perimeter side bars\nâ€¢ High torsional stiffness"
                    : "â€¢ Tiáº¿t diá»‡n: B, H >= 400mm\nâ€¢ Sá»‘ thanh má»—i cáº¡nh >= 3\nâ€¢ Giá»¯ á»•n Ä‘á»‹nh cÃ¡c thanh biÃªn\nâ€¢ TÄƒng kháº£ nÄƒng khÃ¡ng xoáº¯n";
                g.DrawString(desc2, fSml, brOutline, c2X + 8, cardY + 138);

                // --- CARD 3: Äai + Cross-links ---
                int c3X = 362;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, c3X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, c3X, cardY, cardW, cardH);
                g.DrawString(isEn ? "3. HOOP + CROSS-TIES" : "3. ÄAI + ÄAI MÃ“C C", fSec, brOutline, c3X + 10, cardY + 8);

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
                    ? "â€¢ Rectangular / wall columns\nâ€¢ Ties intermediate bars\nâ€¢ Easy site installation\nâ€¢ Standard 135deg / 90deg"
                    : "â€¢ Cá»™t dáº¹t / tiáº¿t diá»‡n lá»›n\nâ€¢ Khá»‘ng cháº¿ thanh thÃ©p giá»¯a\nâ€¢ Ráº¥t thuáº­n tiá»‡n thi cÃ´ng\nâ€¢ MÃ³c chuáº©n 135 / 90 Ä‘á»™";
                g.DrawString(desc3, fSml, brOutline, c3X + 8, cardY + 138);

                // --- Bottom Guideline Banner ---
                using (var bBr = new SolidBrush(Color.FromArgb(236, 239, 241))) g.FillRectangle(bBr, 14, 262, 510, 86);
                g.DrawRectangle(penBorder, 14, 262, 510, 86);
                g.DrawString(isEn ? "MANDATORY DETAILING PRINCIPLES (TCVN 5574:2018 / ACI 318):" : "NGUYÃŠN Táº®C Báº®T BUá»˜C KHI Cáº¤U Táº O ÄAI (TCVN 5574:2018 / ACI 318):", fSec, brOutline, 22, 268);
                string bTxt = isEn
                    ? "â€¢ When clear spacing between adjacent longitudinal bars s > 150mm: An inner tie (diamond or cross-tie) is REQUIRED.\nâ€¢ Seismic stirrup hooks must be bent at 135 degrees with an extension of at least 10d (>= 75mm).\nâ€¢ Stirrup hook locations must alternate diagonally and vertically from one tie level to the next."
                    : "â€¢ Khi khoáº£ng cÃ¡ch giá»¯a cÃ¡c thanh thÃ©p dá»c ká» nhau s > 150mm: Báº®T BUá»˜C bá»‘ trÃ­ Ä‘ai phá»¥ (kim cÆ°Æ¡ng hoáº·c Ä‘ai mÃ³c C).\nâ€¢ MÃ³c Ä‘ai chá»‹u cháº¥n uá»‘n 135 Ä‘á»™ vá»›i Ä‘oáº¡n tháº³ng neo dÃ i >= 10d (hoáº·c >= 75mm); tuyá»‡t Ä‘á»‘i khÃ´ng dÃ¹ng mÃ³c 90 Ä‘á»™ cho Ä‘ai ngoÃ i.\nâ€¢ CÃ¡c gÃ³c mÃ³c Ä‘ai pháº£i Ä‘Æ°á»£c bá»‘ trÃ­ so le theo Ä‘Æ°á»ng chÃ©o vÃ  xoay gÃ³c qua cÃ¡c lá»›p Ä‘ai liÃªn tiáº¿p dá»c thÃ¢n cá»™t.";
                g.DrawString(bTxt, fSml, brOutline, 22, 284);
            }

            // ----------------------------------------------------------------
            // TAB 3: NEO / Ná»I (ANCHORAGE & LAP SPLICE)
            // ----------------------------------------------------------------
            private static void DrawAnchorageTab(Graphics g, bool isEn,
                Color cConc, Color cHatch, Color cOutline, Color cBar, Color cTie, Color cDim, Color cHdr, Color cBorder, Color cCard,
                Font fHdr, Font fSub, Font fSec, Font fBdy, Font fSml, Font fDim,
                Brush brOutline, Brush brDim, Brush brHdrText, Brush brSubText,
                Pen penOutline, Pen penBorder, Pen penDim)
            {
                string title = isEn ? "COLUMN REBAR ANCHORAGE & LAP SPLICE DETAILS" : "CHI TIáº¾T NEO CHÃ‚N Cá»˜T, Uá»N Cá»” CHAI & Ná»I CHá»’NG";
                string sub   = isEn ? "Footing L-bend anchorage, 1:6 cranked splice & staggered lap rules" : "Neo mÃ³ng báº» L, uá»‘n cá»• chai thay Ä‘á»•i tiáº¿t diá»‡n & quy cÃ¡ch ná»‘i chá»“ng so le";
                DrawHeaderBanner(g, title, sub, cHdr, fHdr, fSub, brHdrText, brSubText);

                int cardW = 162, cardH = 208, cardY = 46;
                using var barPen = new Pen(cBar, 2f);
                using var barPen2 = new Pen(Color.FromArgb(30, 136, 229), 1.8f);
                using var tiePen = new Pen(cTie, 1.2f);
                using var concBr = new SolidBrush(cConc);

                // --- PANEL A: Neo chÃ¢n mÃ³ng ---
                int p1X = 14;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, p1X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, p1X, cardY, cardW, cardH);
                g.DrawString(isEn ? "A. FOOTING ANCHORAGE" : "A. NEO CHÃ‚N MÃ“NG (L-BEND)", fSec, brOutline, p1X + 8, cardY + 8);

                // Footing & column concrete
                int ftY = cardY + 105;
                g.FillRectangle(concBr, p1X + 16, ftY, cardW - 32, 45);
                g.FillRectangle(concBr, p1X + 46, cardY + 40, 70, ftY - (cardY + 40));
                g.DrawRectangle(penOutline, p1X + 46, cardY + 40, 70, ftY - (cardY + 40));
                g.DrawRectangle(penOutline, p1X + 16, ftY, cardW - 32, 45);

                // Level line
                using (var lvp = new Pen(cDim, 1f) { DashStyle = DashStyle.DashDot })
                    g.DrawLine(lvp, p1X + 10, ftY, p1X + cardW - 10, ftY);
                g.DrawString(isEn ? "Top of Footing" : "Máº·t mÃ³ng", fSml, brDim, p1X + 18, ftY - 11);

                // Column bars with L-bend
                int b1L = p1X + 60, b1R = p1X + 102;
                g.DrawLine(barPen, b1L, cardY + 45, b1L, ftY + 32);
                g.DrawLine(barPen, b1L, ftY + 32, b1L + 28, ftY + 32); // L-bend inward
                g.DrawLine(barPen, b1R, cardY + 45, b1R, ftY + 32);
                g.DrawLine(barPen, b1R, ftY + 32, b1R - 28, ftY + 32); // L-bend inward

                string aTxt = isEn
                    ? "â€¢ L-bend hook >= 200mm\nâ€¢ L_anc >= 30d ~ 35d\nâ€¢ Rests on bottom mat\nâ€¢ Hooks face column core"
                    : "â€¢ ChÃ¢n báº» L >= 200mm\nâ€¢ L_neo >= 30d ~ 35d\nâ€¢ Äáº·t trÃªn lÆ°á»›i thÃ©p mÃ³ng\nâ€¢ MÃ³c L hÆ°á»›ng vÃ o trong";
                g.DrawString(aTxt, fSml, brOutline, p1X + 8, cardY + 155);

                // --- PANEL B: Uá»‘n cá»• chai 1:6 ---
                int p2X = 188;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, p2X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, p2X, cardY, cardW, cardH);
                g.DrawString(isEn ? "B. CRANKED SPLICE (1:6)" : "B. Uá»N Cá»” CHAI (1:6)", fSec, brOutline, p2X + 8, cardY + 8);

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
                    ? "â€¢ Max slope: 1 in 6\nâ€¢ Extra ties at bends\nâ€¢ If offset > 1:6, use\n  separate dowel bars"
                    : "â€¢ Äá»™ dá»‘c vÃ¡t <= 1:6\nâ€¢ Bá»‘ trÃ­ Ä‘ai dÃ y chá»— uá»‘n\nâ€¢ Náº¿u lá»‡ch > 1:6: pháº£i\n  dÃ¹ng thÃ©p chá» riÃªng";
                g.DrawString(bTxt, fSml, brOutline, p2X + 8, cardY + 155);

                // --- PANEL C: Ná»‘i chá»“ng so le ---
                int p3X = 362;
                using (var cb = new SolidBrush(cCard)) g.FillRectangle(cb, p3X, cardY, cardW, cardH);
                g.DrawRectangle(penBorder, p3X, cardY, cardW, cardH);
                g.DrawString(isEn ? "C. STAGGERED SPLICE" : "C. Ná»I CHá»’NG SO LE", fSec, brOutline, p3X + 8, cardY + 8);

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
                    ? "â€¢ Lap length: L_lap >= 40d\nâ€¢ Stagger offset >= 1.3 L_lap\nâ€¢ Max 50% spliced at section\nâ€¢ Dense ties across lap zone"
                    : "â€¢ Chiá»u dÃ i L_ná»‘i >= 40d\nâ€¢ Khoáº£ng cÃ¡ch so le >= 1.3 L_ná»‘i\nâ€¢ Tá»‘i Ä‘a 50% ná»‘i táº¡i 1 máº·t cáº¯t\nâ€¢ Bá»‘ trÃ­ Ä‘ai dÃ y suá»‘t Ä‘oáº¡n ná»‘i";
                g.DrawString(cTxt, fSml, brOutline, p3X + 8, cardY + 155);

                // --- Bottom Summary Banner ---
                using (var bBr = new SolidBrush(Color.FromArgb(236, 239, 241))) g.FillRectangle(bBr, 14, 262, 510, 86);
                g.DrawRectangle(penBorder, 14, 262, 510, 86);
                g.DrawString(isEn ? "ENGINEERING GUIDELINES FOR SPLICES & ANCHORAGES:" : "CHá»ˆ DáºªN Ká»¸ THUáº¬T QUAN TRá»ŒNG Vá»€ NEO VÃ€ Ná»I Cá»T THÃ‰P:", fSec, brOutline, 22, 268);
                string bBanner = isEn
                    ? "â€¢ Lap lengths depend on concrete grade (B25, B30) and rebar grade (CB300, CB400, Grade 60).\nâ€¢ Increase lap length by 1.3x if 100% of bars are spliced at the same cross section.\nâ€¢ Lap splices are strictly prohibited in seismic plastic hinge zones (Zone A1 at column ends)."
                    : "â€¢ Chiá»u dÃ i neo vÃ  ná»‘i phá»¥ thuá»™c cáº¥p Ä‘á»™ bá»n bÃª tÃ´ng (B20, B25, B30) vÃ  nhÃ³m thÃ©p (CB300-V, CB400-V, CB500-V).\nâ€¢ TÄƒng chiá»u dÃ i ná»‘i chá»“ng lÃªn 1.3 láº§n náº¿u ná»‘i 100% cá»‘t thÃ©p táº¡i cÃ¹ng má»™t vá»‹ trÃ­ máº·t cáº¯t ngang.\nâ€¢ Tuyá»‡t Ä‘á»‘i khÃ´ng ná»‘i cá»‘t thÃ©p trong vÃ¹ng dáº»o (vÃ¹ng A1 Ä‘ai dÃ y Ä‘áº§u cá»™t vÃ  chÃ¢n cá»™t sÃ¡t dáº§m/sÃ n).";
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

                Text(g, font, "SÆ¡ Ä‘á»“ cáº¥u táº¡o - khÃ´ng theo tá»· lá»‡", 24, 14);
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
                    Text(g, font, mesh ? "Lá»›p trÃªn / lá»›p dÆ°á»›i" : "ThÃ©p chá»§ / thÃ©p Ä‘ai", 90, 325);
                }
                else if (column)
                {
                    g.DrawLine(bar, 215, 65, 215, 265);
                    g.DrawLine(bar, 325, 65, 325, 265);
                    for (int y = 80; y < 260; y += 22) g.DrawLine(secondary, 205, y, 335, y);
                    Text(g, font, "Äai: dÄ‘ / aÄ‘", 90, 310);
                }
                else
                {
                    g.DrawLines(bar, new[] { new Point(110, 120), new Point(110, 100), new Point(430, 100), new Point(430, 120) });
                    g.DrawLines(bar, new[] { new Point(110, 210), new Point(110, 230), new Point(430, 230), new Point(430, 210) });
                    if (kind == RebarReferenceKind.Beam)
                        for (int x = 125; x <= 415; x += 25) g.DrawLine(secondary, x, 95, x, 235);
                    Text(g, font, "ThÃ©p trÃªn / thÃ©p dÆ°á»›i", 90, 325);
                }
                Dimension(g, dimension, font, box.Left, box.Right, box.Bottom + 22,
                    circle ? "D" : column ? "b" : view == 0 ? "Lx" : "b");
                Text(g, font, circle ? "D" : column ? (view == 1 ? "H" : "h") : view == 0 ? "Ly" : "h",
                    box.Right + 12, box.Top + 70);
                Text(g, font, "c", box.Left + 5, box.Top + 2);
            }

            private static void Anchorage(Graphics g, Pen bar, Pen secondary, Pen dimension, Font font)
            {
                Text(g, font, "Neo Ä‘áº§u thanh", 70, 52);
                g.DrawLines(bar, new[] { new Point(70, 110), new Point(380, 110), new Point(380, 155) });
                Dimension(g, dimension, font, 260, 380, 70, "lneo = kneo Ã— d");
                Text(g, font, "r uá»‘n", 392, 118);
                Text(g, font, "Ná»‘i chá»“ng", 70, 182);
                g.DrawLine(bar, 70, 235, 340, 235);
                g.DrawLine(secondary, 220, 248, 470, 248);
                Dimension(g, dimension, font, 220, 340, 281, "lná»‘i = kná»‘i Ã— d");
                Text(g, font, "d: Ä‘Æ°á»ng kÃ­nh thanh; k: há»‡ sá»‘ do ngÆ°á»i dÃ¹ng quy Ä‘á»‹nh", 24, 324);
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
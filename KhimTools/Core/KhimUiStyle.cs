using System;
using System.Drawing;
using System.Windows.Forms;

namespace KhimTools.Core
{
    /// <summary>
    /// Bộ Design System & Styling Manager chuẩn thương mại cho toàn bộ giao diện KhimTools.
    /// Mang đến giao diện sang trọng (Modern Slate/Navy Dark Banner, White Cards, Flat Accent Buttons).
    /// </summary>
    public static class KhimUiStyle
    {
        // ── Brand Palette ──────────────────────────────────────────────────
        public static readonly Color HeaderBg = Color.FromArgb(15, 23, 42);       // #0F172A Dark Slate/Navy
        public static readonly Color HeaderAccent = Color.FromArgb(2, 132, 199);   // #0284C7 Sky Blue
        public static readonly Color FormBg = Color.FromArgb(248, 250, 252);      // #F8FAFC Slate 50
        public static readonly Color CardBg = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(226, 232, 240);  // #E2E8F0
        public static readonly Color TextPrimary = Color.FromArgb(30, 41, 59);    // #1E293B
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);// #64748B
        public static readonly Color PrimaryButtonBg = Color.FromArgb(0, 114, 198); // #0072C6 Revit Blue
        public static readonly Color PrimaryButtonHover = Color.FromArgb(2, 132, 199);
        public static readonly Color CreateButtonBg = Color.FromArgb(16, 185, 129); // #10B981 Emerald Green
        public static readonly Color SecondaryButtonBg = Color.FromArgb(241, 245, 249);
        public static readonly Color SecondaryButtonHover = Color.FromArgb(226, 232, 240);

        // ── Form Theme ─────────────────────────────────────────────────────
        public static void ApplyFormTheme(Form form)
        {
            if (form == null) return;
            form.BackColor = FormBg;
            form.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static string GetDefaultVersionTag()
        {
            var ver = typeof(KhimUiStyle).Assembly.GetName().Version;
            return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build} Pro" : "v2.7.1 Pro";
        }

        // ── Header Banner Generator ─────────────────────────────────────────
        public static Panel CreateHeaderBanner(string title, string subtitle, string versionTag = null)
        {
            if (string.IsNullOrEmpty(versionTag))
            {
                versionTag = GetDefaultVersionTag();
            }

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = HeaderBg,
                Padding = new Padding(15, 0, 15, 0)
            };

            // Accent Bottom Line
            var accentLine = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                BackColor = HeaderAccent
            };
            headerPanel.Controls.Add(accentLine);

            // Title Label
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Left = 15,
                Top = 8
            };

            // Subtitle Label
            var lblSubtitle = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184), // #94A3B8
                AutoSize = true,
                Left = 15,
                Top = 30
            };

            // Version Pill Badge
            var lblBadge = new Label
            {
                Text = versionTag,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // #38BDF8
                BackColor = Color.FromArgb(30, 41, 59),  // #1E293B
                AutoSize = true,
                Padding = new Padding(6, 3, 6, 3),
                Top = 15
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(lblBadge);

            headerPanel.Resize += (s, e) =>
            {
                lblBadge.Left = headerPanel.Width - lblBadge.Width - 18;
            };

            return headerPanel;
        }

        // ── Card Style for GroupBoxes ───────────────────────────────────────
        public static void ApplyCardStyle(GroupBox grp, Color? titleColor = null)
        {
            if (grp == null) return;
            grp.BackColor = CardBg;
            grp.ForeColor = titleColor ?? TextPrimary;
            grp.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grp.Padding = new Padding(10);
        }

        // ── Primary Action Button Styling ────────────────────────────────────
        public static void ApplyPrimaryButton(Button btn, Color? customBg = null)
        {
            if (btn == null) return;
            Color bg = customBg ?? PrimaryButtonBg;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;

            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(
                Math.Min(255, bg.R + 20),
                Math.Min(255, bg.G + 20),
                Math.Min(255, bg.B + 20));

            btn.MouseLeave += (s, e) => btn.BackColor = bg;
        }

        // ── Secondary Action Button Styling ──────────────────────────────────
        public static void ApplySecondaryButton(Button btn)
        {
            if (btn == null) return;
            btn.BackColor = SecondaryButtonBg;
            btn.ForeColor = TextPrimary;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = CardBorder;
            btn.FlatAppearance.BorderSize = 1;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btn.Cursor = Cursors.Hand;

            btn.MouseEnter += (s, e) => btn.BackColor = SecondaryButtonHover;
            btn.MouseLeave += (s, e) => btn.BackColor = SecondaryButtonBg;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using KhimTools.Core;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Forms
{
    public class NamingTemplateEditForm : Form
    {
        private TextBox _txtName;
        private TextBox _txtExpression;
        private TextBox _txtRegex;
        private Label _lblPreview;
        private Label _lblRegexStatus;
        private Button _btnSave;
        private Button _btnCancel;

        public NamingTemplate Template { get; private set; }

        public NamingTemplateEditForm(NamingTemplate template = null)
        {
            Template = template != null
                ? new NamingTemplate { Name = template.Name, Expression = template.Expression, RegexPattern = template.RegexPattern, IsDefault = template.IsDefault }
                : new NamingTemplate { Name = "Template Mới", Expression = "{ProjectCode}_{SheetNumber}_{SheetName}", RegexPattern = @"^[A-Za-z0-9_\-\.\s]+$" };

            KhimUiStyle.ApplyFormTheme(this);
            InitializeUi();
            UpdateLivePreview();
        }

        private void InitializeUi()
        {
            Text = "✏️ KHIM TOOLS — Cấu Hình Naming Template";
            Width = 540;
            Height = 490;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "KHIM TOOLS — Naming Template Editor",
                "Configure File Naming Token Expressions & Regex Validation Patterns",
                "v2.5 Pro");
            Controls.Add(header);

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15),
                ColumnCount = 2,
                RowCount = 6
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // 1. Template Name
            mainPanel.Controls.Add(new Label { Text = "Tên Template:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _txtName = new TextBox { Text = Template.Name, Width = 320, Anchor = AnchorStyles.Left };
            mainPanel.Controls.Add(_txtName, 1, 0);

            // 2. Pattern Expression
            mainPanel.Controls.Add(new Label { Text = "Pattern Expression:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _txtExpression = new TextBox { Text = Template.Expression, Width = 320, Anchor = AnchorStyles.Left };
            _txtExpression.TextChanged += (s, e) => UpdateLivePreview();
            mainPanel.Controls.Add(_txtExpression, 1, 1);

            // Tokens Help
            var lblTokens = new Label
            {
                Text = "Token khả dụng: {ProjectCode}, {SheetNumber}, {SheetName}, {Revision}, {RevisionDate}, {Date}, {PaperSize}, {Orientation}",
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 7.8F, FontStyle.Italic),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 10)
            };
            mainPanel.Controls.Add(lblTokens, 1, 2);

            // 3. Regex Pattern
            mainPanel.Controls.Add(new Label { Text = "Pattern Regex Validation:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            _txtRegex = new TextBox { Text = Template.RegexPattern, Width = 320, Anchor = AnchorStyles.Left };
            _txtRegex.TextChanged += (s, e) => UpdateLivePreview();
            mainPanel.Controls.Add(_txtRegex, 1, 3);

            // 4. Live Preview Box
            var pnlPreview = new GroupBox { Text = "Preview Tên File", Width = 460, Height = 80, Margin = new Padding(0, 10, 0, 10) };
            _lblPreview = new Label { Text = "", AutoSize = false, Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkBlue };
            _lblRegexStatus = new Label { Text = "", AutoSize = false, Dock = DockStyle.Bottom, Height = 25, Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };
            pnlPreview.Controls.Add(_lblPreview);
            pnlPreview.Controls.Add(_lblRegexStatus);

            mainPanel.Controls.Add(pnlPreview, 0, 4);
            mainPanel.SetColumnSpan(pnlPreview, 2);

            // Action Buttons
            var pnlBtn = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(5) };
            _btnCancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Width = 85, Height = 30 };
            _btnSave = new Button { Text = "Lưu Template", DialogResult = DialogResult.OK, Width = 110, Height = 30, BackColor = Color.FromArgb(0, 122, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            _btnSave.Click += BtnSave_Click;

            pnlBtn.Controls.Add(_btnCancel);
            pnlBtn.Controls.Add(_btnSave);

            Controls.Add(mainPanel);
            Controls.Add(pnlBtn);
        }

        private void UpdateLivePreview()
        {
            var dummyItem = new SheetExportItem
            {
                SheetNumber = "A-101",
                SheetName = "MẶT BẰNG TẦNG 1",
                CurrentRevisionNumber = "A",
                PaperSize = "A1",
                Orientation = "Landscape"
            };

            var tempObj = new NamingTemplate { Expression = _txtExpression.Text, RegexPattern = _txtRegex.Text };
            string computed = Services.NamingTemplateManager.ComputeFileName(dummyItem, tempObj, "PROJ2026");

            _lblPreview.Text = "📄 File preview: " + computed;

            bool isRegexOk = Services.NamingTemplateManager.ValidateFileNameRegex(computed, tempObj, out string err);
            if (isRegexOk)
            {
                _lblRegexStatus.Text = "✔ Tên file hợp lệ với pattern Regex";
                _lblRegexStatus.ForeColor = Color.DarkGreen;
            }
            else
            {
                _lblRegexStatus.Text = "⚠ " + err;
                _lblRegexStatus.ForeColor = Color.Crimson;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show(this, "Vui lòng nhập tên Template.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Template.Name = _txtName.Text.Trim();
            Template.Expression = _txtExpression.Text.Trim();
            Template.RegexPattern = _txtRegex.Text.Trim();
        }
    }
}

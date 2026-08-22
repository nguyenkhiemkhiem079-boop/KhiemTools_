using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Models;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using ComboBox = System.Windows.Forms.ComboBox;
using CheckBox = System.Windows.Forms.CheckBox;
using GroupBox = System.Windows.Forms.GroupBox;
using NumericUpDown = System.Windows.Forms.NumericUpDown;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace KhimTools.RebarTool.Forms
{
    public class FoundationReinforcementForm : Form
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableFoundations;
        private readonly FoundationRebarSettings _settings;

        // Form Controls
        private ComboBox _cmbLanguage;
        private Button _btnCreateRebar;
        private Button _btnClose;
        private ListBox _foundationListBox;
        private Panel _previewPanel;

        // Tab 1 (Bottom Mesh)
        private ComboBox _cmbBotXDia;
        private NumericUpDown _numBotXSpacing;
        private CheckBox _chkBotXHook;
        private ComboBox _cmbBotYDia;
        private NumericUpDown _numBotYSpacing;
        private CheckBox _chkBotYHook;

        // Tab 2 (Top Mesh)
        private CheckBox _chkEnableTopMesh;
        private ComboBox _cmbTopXDia;
        private NumericUpDown _numTopXSpacing;
        private CheckBox _chkTopXHook;
        private ComboBox _cmbTopYDia;
        private NumericUpDown _numTopYSpacing;
        private CheckBox _chkTopYHook;

        // Tab 3 (Column Dowels & Stirrups)
        private CheckBox _chkEnableDowels;
        private ComboBox _cmbDowelDia;
        private NumericUpDown _numDowelQtyX;
        private NumericUpDown _numDowelQtyY;
        private NumericUpDown _numDowelFootLeg;
        private NumericUpDown _numDowelExtension;
        private CheckBox _chkDowelInward;
        private CheckBox _chkStaggeredDowels;
        private CheckBox _chkEnableDowelStirrups;
        private NumericUpDown _numDowelStirrupQty;

        // Tab 4 (Design Code & Templates)
        private ComboBox _cmbDesignCode;
        private ComboBox _cmbConcreteGrade;
        private ComboBox _cmbSteelGrade;
        private NumericUpDown _numCoverMm;
        private ComboBox _cmbTemplates;
        private Button _btnSaveTemplate;
        private Button _btnLoadTemplate;

        public FoundationReinforcementForm(Document doc, List<FamilyInstance> availableFoundations)
        {
            _doc = doc;
            _availableFoundations = availableFoundations ?? new List<FamilyInstance>();
            _settings = new FoundationRebarSettings();

            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            PopulateBarCombos();
            PopulateFoundationList();
            LoadTemplateList();
        }

        private void BuildUi()
        {
            Text = "🧱 K-TOOLS — Bố trí Thép Móng (Foundation Rebar v2.5)";
            Width = 920;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Header Banner
            var header = KhimUiStyle.CreateHeaderBanner(
                "K-TOOLS — Foundation Reinforcement Engine",
                "Automated Bottom/Top Meshes, Column Starter Bars & Edge Ties (TCVN 5574 & Eurocode 2/7)",
                "v2.5 Pro");
            Controls.Add(header);

            // Bottom Panel
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.FromArgb(245, 245, 247) };
            var lblLang = new Label { Text = "🌐 Language:", AutoSize = true, Left = 15, Top = 18, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, Left = 95, Top = 14 };
            _cmbLanguage.Items.Add("🇻🇳 Tiếng Việt");
            _cmbLanguage.Items.Add("🇬🇧 English");
            _cmbLanguage.SelectedIndex = LanguageManager.IsEnglish ? 1 : 0;

            _btnCreateRebar = new Button { Text = "⚡ Create Rebar", Width = 135, Height = 36, Top = 10 };
            KhimUiStyle.ApplyPrimaryButton(_btnCreateRebar, KhimUiStyle.CreateButtonBg);

            _btnClose = new Button { Text = "Close", Width = 90, Height = 36, Top = 10 };
            KhimUiStyle.ApplySecondaryButton(_btnClose);

            _btnCreateRebar.Click += BtnCreateRebar_Click;
            _btnClose.Click += (s, e) => Close();

            bottomPanel.Controls.Add(lblLang);
            bottomPanel.Controls.Add(_cmbLanguage);
            bottomPanel.Controls.Add(_btnCreateRebar);
            bottomPanel.Controls.Add(_btnClose);

            bottomPanel.Resize += (s, e) =>
            {
                _btnClose.Left = bottomPanel.Width - _btnClose.Width - 15;
                _btnCreateRebar.Left = _btnClose.Left - _btnCreateRebar.Width - 10;
            };
            Controls.Add(bottomPanel);

            // Right Panel (Selection List & Live Preview)
            var rightPanel = new Panel { Dock = DockStyle.Right, Width = 260, Padding = new Padding(10), BackColor = Color.FromArgb(250, 250, 252) };
            var lblFdnTitle = new Label { Text = "📋 Danh Sách Móng", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            _foundationListBox = new ListBox { Dock = DockStyle.Top, Height = 200, SelectionMode = SelectionMode.MultiExtended };

            var lblPreviewTitle = new Label { Text = "👁️ Live 2D Footing Preview", Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkBlue };

            _previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            _previewPanel.Paint += PreviewPanel_Paint;

            rightPanel.Controls.Add(_previewPanel);
            rightPanel.Controls.Add(lblPreviewTitle);
            rightPanel.Controls.Add(_foundationListBox);
            rightPanel.Controls.Add(lblFdnTitle);
            Controls.Add(rightPanel);

            // Center Tab Control
            var tabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Point(12, 6) };

            // TAB 1: Lớp Thép Dưới (Bottom Mesh)
            var tabBot = new TabPage("1️⃣ Thép Lưới Dưới") { BackColor = KhimUiStyle.FormBg };
            var grpBotX = new GroupBox { Text = "Phương X (Dưới)", Left = 15, Top = 15, Width = 520, Height = 110 };
            KhimUiStyle.ApplyCardStyle(grpBotX);
            var lblBotXDia = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbBotXDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblBotXSpacing = new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true };
            _numBotXSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            _chkBotXHook = new CheckBox { Text = "Uốn bẻ móc đứng 90° lên đỉnh móng", Left = 15, Top = 70, AutoSize = true, Checked = true };

            grpBotX.Controls.Add(lblBotXDia);
            grpBotX.Controls.Add(_cmbBotXDia);
            grpBotX.Controls.Add(lblBotXSpacing);
            grpBotX.Controls.Add(_numBotXSpacing);
            grpBotX.Controls.Add(_chkBotXHook);

            var grpBotY = new GroupBox { Text = "Phương Y (Dưới)", Left = 15, Top = 140, Width = 520, Height = 110 };
            KhimUiStyle.ApplyCardStyle(grpBotY);
            var lblBotYDia = new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true };
            _cmbBotYDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblBotYSpacing = new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true };
            _numBotYSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 150, Increment = 10 };
            _chkBotYHook = new CheckBox { Text = "Uốn bẻ móc đứng 90° lên đỉnh móng", Left = 15, Top = 70, AutoSize = true, Checked = true };

            grpBotY.Controls.Add(lblBotYDia);
            grpBotY.Controls.Add(_cmbBotYDia);
            grpBotY.Controls.Add(lblBotYSpacing);
            grpBotY.Controls.Add(_numBotYSpacing);
            grpBotY.Controls.Add(_chkBotYHook);

            tabBot.Controls.Add(grpBotX);
            tabBot.Controls.Add(grpBotY);
            tabControl.TabPages.Add(tabBot);

            // TAB 2: Lớp Thép Trên (Top Mesh)
            var tabTop = new TabPage("2️⃣ Thép Lưới Trên") { BackColor = KhimUiStyle.FormBg };
            _chkEnableTopMesh = new CheckBox { Text = "Bật bố trí Thép Lớp Trên (Cho Đài Móng / Móng Sâu)", Left = 15, Top = 15, AutoSize = true, Checked = false };

            var grpTopX = new GroupBox { Text = "Phương X (Trên)", Left = 15, Top = 45, Width = 520, Height = 100 };
            KhimUiStyle.ApplyCardStyle(grpTopX);
            _cmbTopXDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _numTopXSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 200, Increment = 10 };
            _chkTopXHook = new CheckBox { Text = "Móc bẻ 90° xuống đáy móng", Left = 15, Top = 65, AutoSize = true, Checked = true };

            grpTopX.Controls.Add(new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true });
            grpTopX.Controls.Add(_cmbTopXDia);
            grpTopX.Controls.Add(new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true });
            grpTopX.Controls.Add(_numTopXSpacing);
            grpTopX.Controls.Add(_chkTopXHook);

            var grpTopY = new GroupBox { Text = "Phương Y (Trên)", Left = 15, Top = 155, Width = 520, Height = 100 };
            KhimUiStyle.ApplyCardStyle(grpTopY);
            _cmbTopYDia = new ComboBox { Left = 110, Top = 30, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _numTopYSpacing = new NumericUpDown { Left = 380, Top = 30, Width = 110, Minimum = 50, Maximum = 500, Value = 200, Increment = 10 };
            _chkTopYHook = new CheckBox { Text = "Móc bẻ 90° xuống đáy móng", Left = 15, Top = 65, AutoSize = true, Checked = true };

            grpTopY.Controls.Add(new Label { Text = "Đường kính:", Left = 15, Top = 35, AutoSize = true });
            grpTopY.Controls.Add(_cmbTopYDia);
            grpTopY.Controls.Add(new Label { Text = "Khoảng cách a (mm):", Left = 240, Top = 35, AutoSize = true });
            grpTopY.Controls.Add(_numTopYSpacing);
            grpTopY.Controls.Add(_chkTopYHook);

            tabTop.Controls.Add(_chkEnableTopMesh);
            tabTop.Controls.Add(grpTopX);
            tabTop.Controls.Add(grpTopY);
            tabControl.TabPages.Add(tabTop);

            // TAB 3: Thép Chờ Cột & Thép Đai (Column Dowels & Stirrups)
            var tabDowel = new TabPage("3️⃣ Thép Chờ & Thép Đai") { BackColor = KhimUiStyle.FormBg };
            _chkEnableDowels = new CheckBox { Text = "Bật tạo Thép Chờ Cột & Thép Đai Lồng (Column Dowels & Stirrups)", Left = 15, Top = 12, AutoSize = true, Checked = true };

            var grpDowel = new GroupBox { Text = "Thông Số Thép Chờ Cột & Đai Lồng Chân Cột", Left = 15, Top = 38, Width = 520, Height = 240 };
            KhimUiStyle.ApplyCardStyle(grpDowel);

            _cmbDowelDia = new ComboBox { Left = 130, Top = 25, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _numDowelQtyX = new NumericUpDown { Left = 380, Top = 25, Width = 100, Minimum = 2, Maximum = 10, Value = 2 };
            _numDowelQtyY = new NumericUpDown { Left = 380, Top = 65, Width = 100, Minimum = 2, Maximum = 10, Value = 2 };

            _numDowelFootLeg = new NumericUpDown { Left = 130, Top = 105, Width = 110, Minimum = 100, Maximum = 1000, Value = 300, Increment = 50 };
            _numDowelExtension = new NumericUpDown { Left = 380, Top = 105, Width = 100, Minimum = 200, Maximum = 2000, Value = 600, Increment = 50 };

            _chkDowelInward = new CheckBox { Text = "Bẻ chân quỳ úp vào trong lòng cột", Left = 15, Top = 145, AutoSize = true, Checked = false };
            _chkStaggeredDowels = new CheckBox { Text = "Bố trí nối so le 50% thép chờ (50% Staggered)", Left = 250, Top = 145, AutoSize = true, Checked = true };

            _chkEnableDowelStirrups = new CheckBox { Text = "Đặt Thép Đai lồng móng cố định chân cột", Left = 15, Top = 180, AutoSize = true, Checked = true };
            _numDowelStirrupQty = new NumericUpDown { Left = 380, Top = 175, Width = 100, Minimum = 1, Maximum = 10, Value = 3 };

            grpDowel.Controls.Add(new Label { Text = "Đường kính thép chờ:", Left = 15, Top = 28, AutoSize = true });
            grpDowel.Controls.Add(_cmbDowelDia);
            grpDowel.Controls.Add(new Label { Text = "Số thanh phương X:", Left = 250, Top = 28, AutoSize = true });
            grpDowel.Controls.Add(_numDowelQtyX);
            grpDowel.Controls.Add(new Label { Text = "Số thanh phương Y:", Left = 250, Top = 68, AutoSize = true });
            grpDowel.Controls.Add(_numDowelQtyY);

            grpDowel.Controls.Add(new Label { Text = "Chân quỳ 90° (mm):", Left = 15, Top = 108, AutoSize = true });
            grpDowel.Controls.Add(_numDowelFootLeg);
            grpDowel.Controls.Add(new Label { Text = "Đoạn chờ L0 (mm):", Left = 250, Top = 108, AutoSize = true });
            grpDowel.Controls.Add(_numDowelExtension);

            grpDowel.Controls.Add(_chkDowelInward);
            grpDowel.Controls.Add(_chkStaggeredDowels);

            grpDowel.Controls.Add(_chkEnableDowelStirrups);
            grpDowel.Controls.Add(new Label { Text = "Số đai lồng:", Left = 290, Top = 178, AutoSize = true });
            grpDowel.Controls.Add(_numDowelStirrupQty);

            tabDowel.Controls.Add(_chkEnableDowels);
            tabDowel.Controls.Add(grpDowel);
            tabControl.TabPages.Add(tabDowel);

            // TAB 4: Tiêu Chuẩn & Template
            var tabDesign = new TabPage("4️⃣ Tiêu Chuẩn & Template") { BackColor = KhimUiStyle.FormBg };
            var grpCode = new GroupBox { Text = "Tiêu Chuẩn Thiết Kế & Cấp Độ Bền", Left = 15, Top = 15, Width = 520, Height = 140 };
            KhimUiStyle.ApplyCardStyle(grpCode);

            _cmbDesignCode = new ComboBox { Left = 130, Top = 30, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDesignCode.Items.Add("TCVN 5574:2018");
            _cmbDesignCode.Items.Add("Eurocode 2 / Eurocode 7");
            _cmbDesignCode.SelectedIndex = 0;

            _cmbConcreteGrade = new ComboBox { Left = 130, Top = 70, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbSteelGrade = new ComboBox { Left = 380, Top = 70, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _numCoverMm = new NumericUpDown { Left = 130, Top = 105, Width = 110, Minimum = 20, Maximum = 100, Value = 50, Increment = 5 };

            Action updateGrades = () =>
            {
                bool isEurocode = _cmbDesignCode.SelectedIndex == 1;
                _cmbConcreteGrade.Items.Clear();
                _cmbSteelGrade.Items.Clear();

                if (isEurocode)
                {
                    _cmbConcreteGrade.Items.AddRange(new object[] { "C20/25", "C25/30", "C30/37", "C35/45", "C40/50" });
                    _cmbSteelGrade.Items.AddRange(new object[] { "B400", "B500" });
                }
                else
                {
                    _cmbConcreteGrade.Items.AddRange(new object[] { "B15", "B20", "B25", "B30", "B35", "B40", "B45", "B50" });
                    _cmbSteelGrade.Items.AddRange(new object[] { "CB240-T", "CB300-V", "CB400-V", "CB500-V" });
                }

                _cmbConcreteGrade.SelectedIndex = 2;
                _cmbSteelGrade.SelectedIndex = Math.Min(2, _cmbSteelGrade.Items.Count - 1);
            };

            _cmbDesignCode.SelectedIndexChanged += (s, e) => updateGrades();
            updateGrades();

            grpCode.Controls.Add(new Label { Text = "Tiêu chuẩn:", Left = 15, Top = 35, AutoSize = true });
            grpCode.Controls.Add(_cmbDesignCode);
            grpCode.Controls.Add(new Label { Text = "Mác bê tông:", Left = 15, Top = 75, AutoSize = true });
            grpCode.Controls.Add(_cmbConcreteGrade);
            grpCode.Controls.Add(new Label { Text = "Mác thép:", Left = 310, Top = 75, AutoSize = true });
            grpCode.Controls.Add(_cmbSteelGrade);
            grpCode.Controls.Add(new Label { Text = "Cover (mm):", Left = 15, Top = 110, AutoSize = true });
            grpCode.Controls.Add(_numCoverMm);

            var grpTpl = new GroupBox { Text = "Quản Lý Template JSON", Left = 15, Top = 170, Width = 520, Height = 90 };
            KhimUiStyle.ApplyCardStyle(grpTpl);
            _cmbTemplates = new ComboBox { Left = 15, Top = 35, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _btnSaveTemplate = new Button { Text = "💾 Save Tpl", Left = 290, Top = 33, Width = 100, Height = 32 };
            KhimUiStyle.ApplySecondaryButton(_btnSaveTemplate);
            _btnLoadTemplate = new Button { Text = "📂 Load Tpl", Left = 400, Top = 33, Width = 100, Height = 32 };
            KhimUiStyle.ApplySecondaryButton(_btnLoadTemplate);

            grpTpl.Controls.Add(_cmbTemplates);
            grpTpl.Controls.Add(_btnSaveTemplate);
            grpTpl.Controls.Add(_btnLoadTemplate);

            tabDesign.Controls.Add(grpCode);
            tabDesign.Controls.Add(grpTpl);
            tabControl.TabPages.Add(tabDesign);

            Controls.Add(tabControl);
        }

        private void PopulateBarCombos()
        {
            var barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .Select(b => b.Name)
                .OrderBy(n => n)
                .ToList();

            if (!barTypes.Any()) barTypes = new List<string> { "d10", "d12", "d14", "d16", "d18", "d20" };

            PopulateCombo(_cmbBotXDia, barTypes, "14");
            PopulateCombo(_cmbBotYDia, barTypes, "14");
            PopulateCombo(_cmbTopXDia, barTypes, "12");
            PopulateCombo(_cmbTopYDia, barTypes, "12");
            PopulateCombo(_cmbDowelDia, barTypes, "18");
        }

        private void PopulateCombo(ComboBox combo, List<string> items, string defaultDia)
        {
            combo.Items.Clear();
            foreach (var item in items) combo.Items.Add(item);
            int idx = items.FindIndex(i => i.Contains(defaultDia));
            combo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void PopulateFoundationList()
        {
            _foundationListBox.Items.Clear();
            foreach (var fdn in _availableFoundations)
            {
                _foundationListBox.Items.Add($"{fdn.Name} (ID: {fdn.Id.ToLongValue()})");
            }
            if (_foundationListBox.Items.Count > 0)
                _foundationListBox.SelectedIndex = 0;
        }

        private void LoadTemplateList()
        {
            _cmbTemplates.Items.Clear();
            foreach (string name in FoundationRebarSettings.GetSavedTemplateNames())
                _cmbTemplates.Items.Add(name);
            if (_cmbTemplates.Items.Count > 0)
                _cmbTemplates.SelectedIndex = 0;
        }

        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = _previewPanel.Width;
            int h = _previewPanel.Height;

            g.Clear(Color.White);

            Rectangle rectFdn = new Rectangle(20, h / 2 - 40, w - 40, 70);
            using (Pen pFdn = new Pen(Color.DimGray, 2))
                g.DrawRectangle(pFdn, rectFdn);

            // Draw Bottom Mesh
            using (Pen pRed = new Pen(Color.Red, 3))
            {
                g.DrawLine(pRed, rectFdn.Left + 8, rectFdn.Bottom - 8, rectFdn.Right - 8, rectFdn.Bottom - 8);
                g.DrawLine(pRed, rectFdn.Left + 8, rectFdn.Bottom - 8, rectFdn.Left + 8, rectFdn.Top + 8);
                g.DrawLine(pRed, rectFdn.Right - 8, rectFdn.Bottom - 8, rectFdn.Right - 8, rectFdn.Top + 8);
            }

            // Draw Column Dowels
            using (Pen pBlue = new Pen(Color.Blue, 3))
            {
                g.DrawLine(pBlue, w / 2 - 25, rectFdn.Bottom - 12, w / 2 - 10, rectFdn.Bottom - 12);
                g.DrawLine(pBlue, w / 2 - 10, rectFdn.Bottom - 12, w / 2 - 10, rectFdn.Top - 30);

                g.DrawLine(pBlue, w / 2 + 25, rectFdn.Bottom - 12, w / 2 + 10, rectFdn.Bottom - 12);
                g.DrawLine(pBlue, w / 2 + 10, rectFdn.Bottom - 12, w / 2 + 10, rectFdn.Top - 30);
            }
        }

        private void BtnCreateRebar_Click(object sender, EventArgs e)
        {
            var selectedIndices = _foundationListBox.SelectedIndices;
            if (selectedIndices.Count == 0)
            {
                KhimDialogHelper.ShowError("Vui lòng chọn ít nhất 1 Móng để tạo thép.");
                return;
            }

            _settings.BotXDiaLabel = _cmbBotXDia.Text;
            _settings.BotXSpacingMm = (double)_numBotXSpacing.Value;
            _settings.BotXHookUp = _chkBotXHook.Checked;

            _settings.BotYDiaLabel = _cmbBotYDia.Text;
            _settings.BotYSpacingMm = (double)_numBotYSpacing.Value;
            _settings.BotYHookUp = _chkBotYHook.Checked;

            _settings.EnableTopMesh = _chkEnableTopMesh.Checked;
            _settings.TopXDiaLabel = _cmbTopXDia.Text;
            _settings.TopXSpacingMm = (double)_numTopXSpacing.Value;

            _settings.EnableColumnDowels = _chkEnableDowels.Checked;
            _settings.DowelDiaLabel = _cmbDowelDia.Text;
            _settings.DowelQtyX = (int)_numDowelQtyX.Value;
            _settings.DowelQtyY = (int)_numDowelQtyY.Value;
            _settings.DowelFootLegMm = (double)_numDowelFootLeg.Value;
            _settings.DowelExtensionMm = (double)_numDowelExtension.Value;
            _settings.DowelLegInward = _chkDowelInward.Checked;
            _settings.StaggeredDowels = _chkStaggeredDowels.Checked;

            _settings.EnableDowelStirrups = _chkEnableDowelStirrups.Checked;
            _settings.DowelStirrupQty = (int)_numDowelStirrupQty.Value;

            _settings.DesignCode = _cmbDesignCode.Text;
            _settings.ConcreteGrade = _cmbConcreteGrade.Text;
            _settings.SteelGrade = _cmbSteelGrade.Text;
            _settings.CustomCoverMm = (double)_numCoverMm.Value;

            var generator = new FoundationRebarGenerator(_doc);
            var report = new RebarGenerationReport();

            using (var tx = new Transaction(_doc, "Bố trí Thép Móng — KhimTools"))
            {
                tx.Start();
                foreach (int idx in selectedIndices)
                {
                    FamilyInstance fdn = _availableFoundations[idx];
                    FoundationProfile profile = FoundationGeometryHelper.AnalyzeFoundation(_doc, fdn);
                    generator.Generate(profile, _settings, report);
                }
                tx.Commit();
            }

            KhimDialogHelper.ShowRebarGenerationReport(report, "Móng (Foundation)", selectedIndices.Count);
            Close();
        }
    }
}

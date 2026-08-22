using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.SlabJoin.Models;
using KhimTools.SlabJoin.Services;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using ComboBox = System.Windows.Forms.ComboBox;
using Control = System.Windows.Forms.Control;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using FontStyle = System.Drawing.FontStyle;

using KhimTools.Core;
using KhimTools.SlabJoin.Models;
using KhimTools.SlabJoin.Services;

namespace KhimTools.SlabJoin.Forms
{
    public class JoinElementsForm : Form
    {
        private readonly Document _doc;
        private readonly ICollection<ElementId> _selectedIds;

        private Button _btnJoin;
        private Button _btnUnjoin;
        private Button _btnSwitch;

        private RadioButton _rdCurrentView;
        private RadioButton _rdAllModel;
        private RadioButton _rdSelection;

        private FlowLayoutPanel _rulesPanel;
        private Button _btnAddRule;

        private RichTextBox _terminal;
        private Button _btnClearTerminal;

        private TextBox _txtTemplateName;
        private ComboBox _cmbTemplates;
        private Button _btnSaveTemplate;
        private Button _btnLoadTemplate;
        private Button _btnDeleteTemplate;

        public JoinElementsForm(Document doc, ICollection<ElementId> selectedIds)
        {
            _doc = doc;
            _selectedIds = selectedIds ?? new List<ElementId>();
            KhimUiStyle.ApplyFormTheme(this);
            BuildUi();
            RefreshTemplateList();
            AddDefaultRule();
            LogMessage("Plugin initialized.");
        }

        private void BuildUi()
        {
            Text = "🔗 K-TOOLS — Geometry Join & Order Manager";
            Width = 920;
            Height = 630;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(800, 520);
            BackColor = KhimUiStyle.FormBg;

            // 0. TOP HEADER BANNER
            var header = KhimUiStyle.CreateHeaderBanner(
                "K-TOOLS — Geometry Join Manager",
                "Cross-Category Join, Unjoin & Join Order Switch Engine",
                "v2.5 Pro");
            Controls.Add(header);

            // ══════ TOP: Action Buttons ══════
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12, 8, 12, 8), BackColor = KhimUiStyle.CardBg };

            _btnJoin = MakeActionButton("+ Join Elements", Color.FromArgb(240, 248, 255), Color.FromArgb(0, 114, 198), 0);
            _btnUnjoin = MakeActionButton("× Unjoin Elements", Color.FromArgb(255, 240, 240), Color.FromArgb(200, 50, 50), 150);
            _btnSwitch = MakeActionButton("‖ Switch Order", Color.FromArgb(240, 255, 245), Color.FromArgb(16, 185, 129), 300);

            _btnJoin.Click += (s, e) => RunAction("JOIN");
            _btnUnjoin.Click += (s, e) => RunAction("UNJOIN");
            _btnSwitch.Click += (s, e) => RunAction("SWITCH");

            topPanel.Controls.AddRange(new Control[] { _btnJoin, _btnUnjoin, _btnSwitch });
            Controls.Add(topPanel);

            // ══════ SCOPE BAR ══════
            var scopePanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 6, 12, 6), BackColor = Color.FromArgb(250, 250, 252) };

            var lblScope = new Label { Text = "SCOPE:", AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.DimGray, Top = 10, Left = 12 };
            _rdCurrentView = new RadioButton { Text = "Current View", Checked = true, AutoSize = true, Top = 8, Left = 75, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(30, 40, 60), FlatStyle = FlatStyle.Standard };
            _rdAllModel = new RadioButton { Text = "All Model", AutoSize = true, Top = 8, Left = 210 };
            _rdSelection = new RadioButton { Text = "Selection", AutoSize = true, Top = 8, Left = 320 };

            var lblWarn = new Label { Text = "⚠ 'All Model' may be slow on large projects", AutoSize = true, Top = 10, Left = 430, ForeColor = Color.FromArgb(180, 140, 40), Font = new Font("Segoe UI", 8F, FontStyle.Italic) };

            scopePanel.Controls.AddRange(new Control[] { lblScope, _rdCurrentView, _rdAllModel, _rdSelection, lblWarn });
            Controls.Add(scopePanel);

            // ══════ MAIN SPLIT: Left (Rules + Templates) | Right (Terminal) ══════
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 490,
                Panel1MinSize = 350,
                Panel2MinSize = 250,
                BackColor = Color.FromArgb(230, 230, 235)
            };

            // ──── LEFT: Category Rules + Templates ────
            var leftPanel = splitContainer.Panel1;
            leftPanel.BackColor = Color.White;
            leftPanel.Padding = new Padding(12);

            // Rules Header
            var rulesHeaderPanel = new Panel { Dock = DockStyle.Top, Height = 35 };
            var lblRulesTitle = new Label { Text = "BATCH PROCESSING — CATEGORY MATCHING", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 30), AutoSize = true, Top = 8, Left = 0 };
            _btnAddRule = new Button { Text = "+ Add Rule", Width = 80, Height = 28, Top = 3, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(30, 30, 30), Font = new Font("Segoe UI", 8.5F) };
            _btnAddRule.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _btnAddRule.Click += (s, e) => AddRule();
            rulesHeaderPanel.Controls.Add(lblRulesTitle);
            rulesHeaderPanel.Controls.Add(_btnAddRule);
            rulesHeaderPanel.Resize += (s, e) => _btnAddRule.Left = rulesHeaderPanel.Width - _btnAddRule.Width - 5;

            // Rules List (scrollable)
            _rulesPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 5, 0, 5)
            };

            // Template Settings
            var templateGroup = new GroupBox { Text = "TEMPLATE SETTINGS", Dock = DockStyle.Bottom, Height = 90, Padding = new Padding(8), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(60, 60, 60) };
            var templateLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            templateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            templateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            templateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            _txtTemplateName = new TextBox { Text = "My Template", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F) };
            _btnSaveTemplate = new Button { Text = "💾 Save Template", Dock = DockStyle.Fill, Height = 28, BackColor = Color.FromArgb(30, 40, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _btnSaveTemplate.FlatAppearance.BorderSize = 0;
            _btnSaveTemplate.Click += BtnSaveTemplate_Click;

            _cmbTemplates = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _btnLoadTemplate = new Button { Text = "↓ Load", Dock = DockStyle.Fill, Height = 28, FlatStyle = FlatStyle.Flat };
            _btnLoadTemplate.FlatAppearance.BorderColor = Color.Gray;
            _btnLoadTemplate.Click += BtnLoadTemplate_Click;
            _btnDeleteTemplate = new Button { Text = "× Delete", Dock = DockStyle.Fill, Height = 28, FlatStyle = FlatStyle.Flat, ForeColor = Color.Red };
            _btnDeleteTemplate.FlatAppearance.BorderColor = Color.FromArgb(220, 100, 100);
            _btnDeleteTemplate.Click += BtnDeleteTemplate_Click;

            templateLayout.Controls.Add(_txtTemplateName); templateLayout.Controls.Add(_btnSaveTemplate); templateLayout.Controls.Add(new Label());
            templateLayout.Controls.Add(_cmbTemplates); templateLayout.Controls.Add(_btnLoadTemplate); templateLayout.Controls.Add(_btnDeleteTemplate);
            templateGroup.Controls.Add(templateLayout);

            leftPanel.Controls.Add(_rulesPanel);
            leftPanel.Controls.Add(rulesHeaderPanel);
            leftPanel.Controls.Add(templateGroup);

            // ──── RIGHT: Terminal Output ────
            var rightPanel = splitContainer.Panel2;
            rightPanel.BackColor = Color.FromArgb(20, 30, 50);
            rightPanel.Padding = new Padding(8);

            var terminalHeader = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(20, 30, 50) };
            var lblTerminal = new Label { Text = "TERMINAL OUTPUT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 200, 220), Top = 5, Left = 0, AutoSize = true };
            _btnClearTerminal = new Button { Text = "Clear", FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(150, 170, 190), BackColor = Color.FromArgb(20, 30, 50), Width = 45, Height = 22, Top = 3, Font = new Font("Segoe UI", 8F) };
            _btnClearTerminal.FlatAppearance.BorderSize = 0;
            _btnClearTerminal.Click += (s, e) => _terminal.Clear();
            terminalHeader.Controls.Add(lblTerminal);
            terminalHeader.Controls.Add(_btnClearTerminal);
            terminalHeader.Resize += (s, e) => _btnClearTerminal.Left = terminalHeader.Width - _btnClearTerminal.Width - 5;

            _terminal = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 30, 50),
                ForeColor = Color.FromArgb(180, 200, 220),
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.None,
                WordWrap = true
            };

            rightPanel.Controls.Add(_terminal);
            rightPanel.Controls.Add(terminalHeader);

            Controls.Add(splitContainer);
            splitContainer.BringToFront();
        }

        // ─── ACTION BUTTON FACTORY ──────────────────────────────────────

        private Button MakeActionButton(string text, Color bgColor, Color textColor, int left)
        {
            var btn = new Button
            {
                Text = text,
                Width = 125,
                Height = 34,
                Top = 8,
                Left = left + 12,
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 215);
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        // ─── CATEGORY RULE ROW ──────────────────────────────────────────

        private void AddDefaultRule()
        {
            AddRule(BuiltInCategory.OST_Floors, BuiltInCategory.OST_Floors);
        }

        private void AddRule(BuiltInCategory catA = BuiltInCategory.OST_Floors, BuiltInCategory catB = BuiltInCategory.OST_Floors)
        {
            var row = new Panel { Width = _rulesPanel.Width - 30, Height = 38 };

            var cmbA = MakeCategoryCombo(catA);
            cmbA.Left = 0; cmbA.Top = 5; cmbA.Width = 170;

            var lblArrow = new Label { Text = "↕", AutoSize = true, Top = 8, Left = 178, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.Gray };

            var cmbB = MakeCategoryCombo(catB);
            cmbB.Left = 198; cmbB.Top = 5; cmbB.Width = 170;

            var btnRemove = new Button
            {
                Text = "×",
                Width = 30,
                Height = 28,
                Top = 5,
                Left = 378,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Red,
                BackColor = Color.FromArgb(255, 240, 240),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
            btnRemove.FlatAppearance.BorderColor = Color.FromArgb(230, 180, 180);
            btnRemove.Click += (s, e) => { _rulesPanel.Controls.Remove(row); row.Dispose(); };

            row.Controls.AddRange(new Control[] { cmbA, lblArrow, cmbB, btnRemove });
            _rulesPanel.Controls.Add(row);
        }

        private ComboBox MakeCategoryCombo(BuiltInCategory defaultCat)
        {
            var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            foreach (var cat in CategoryMatchRule.SupportedCategories)
            {
                cmb.Items.Add(new CatItem(cat));
            }
            // Select default
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (((CatItem)cmb.Items[i]).Category == defaultCat)
                {
                    cmb.SelectedIndex = i;
                    break;
                }
            }
            return cmb;
        }

        // ─── COLLECT RULES FROM UI ──────────────────────────────────────

        private List<CategoryMatchRule> CollectRules()
        {
            var rules = new List<CategoryMatchRule>();
            foreach (Control ctrl in _rulesPanel.Controls)
            {
                if (ctrl is Panel row)
                {
                    var combos = row.Controls.OfType<ComboBox>().ToList();
                    if (combos.Count >= 2)
                    {
                        var catA = (combos[0].SelectedItem as CatItem)?.Category ?? BuiltInCategory.OST_Floors;
                        var catB = (combos[1].SelectedItem as CatItem)?.Category ?? BuiltInCategory.OST_Floors;
                        rules.Add(new CategoryMatchRule { CategoryA = catA, CategoryB = catB });
                    }
                }
            }
            return rules;
        }

        private ScopeMode GetScope()
        {
            if (_rdAllModel.Checked) return ScopeMode.AllModel;
            if (_rdSelection.Checked) return ScopeMode.Selection;
            return ScopeMode.CurrentView;
        }

        // ─── RUN ACTION ─────────────────────────────────────────────────

        private void RunAction(string action)
        {
            var rules = CollectRules();
            if (!rules.Any())
            {
                LogMessage("⚠ No rules defined. Add at least one category rule.");
                return;
            }

            var scope = GetScope();
            LogMessage($"═══ {action} started (Scope: {scope}) ═══");

            var sw = Stopwatch.StartNew();
            var service = new ElementJoinService();

            List<JoinPairResult> results;
            switch (action)
            {
                case "JOIN":
                    results = service.JoinByRules(_doc, rules, scope, _selectedIds, msg => LogMessage(msg));
                    break;
                case "UNJOIN":
                    results = service.UnjoinByRules(_doc, rules, scope, _selectedIds, msg => LogMessage(msg));
                    break;
                case "SWITCH":
                    results = service.SwitchByRules(_doc, rules, scope, _selectedIds, msg => LogMessage(msg));
                    break;
                default:
                    return;
            }

            sw.Stop();
            int ok = results.Count(r => r.Success);
            int skip = results.Count(r => !r.Success && !r.IsError);
            int fail = results.Count(r => r.IsError);
            LogMessage($"═══ Done in {sw.Elapsed.TotalSeconds:F2}s — Success: {ok}, Skipped: {skip}, Failed: {fail} ═══");
        }

        // ─── TERMINAL LOG ───────────────────────────────────────────────

        private void LogMessage(string msg)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _terminal.AppendText($"{timestamp}  {msg}\n");
            _terminal.ScrollToCaret();
        }

        // ─── TEMPLATE ACTIONS ───────────────────────────────────────────

        private void BtnSaveTemplate_Click(object sender, EventArgs e)
        {
            string name = _txtTemplateName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                LogMessage("⚠ Template name is empty.");
                return;
            }

            var template = new JoinTemplate
            {
                Name = name,
                DefaultScope = GetScope(),
                Rules = JoinTemplateManager.ToTemplateRules(CollectRules())
            };
            JoinTemplateManager.Save(template);
            RefreshTemplateList();
            LogMessage($"💾 Saved template: {name}");
        }

        private void BtnLoadTemplate_Click(object sender, EventArgs e)
        {
            string name = _cmbTemplates.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(name)) return;

            var template = JoinTemplateManager.Load(name);
            if (template == null)
            {
                LogMessage($"⚠ Template '{name}' not found.");
                return;
            }

            // Clear existing rules
            _rulesPanel.Controls.Clear();

            var rules = JoinTemplateManager.FromTemplateRules(template.Rules);
            foreach (var rule in rules)
            {
                AddRule(rule.CategoryA, rule.CategoryB);
            }

            // Set scope
            switch (template.DefaultScope)
            {
                case ScopeMode.AllModel: _rdAllModel.Checked = true; break;
                case ScopeMode.Selection: _rdSelection.Checked = true; break;
                default: _rdCurrentView.Checked = true; break;
            }

            LogMessage($"↓ Loaded template: {name} ({rules.Count} rules)");
        }

        private void BtnDeleteTemplate_Click(object sender, EventArgs e)
        {
            string name = _cmbTemplates.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(name)) return;

            JoinTemplateManager.Delete(name);
            RefreshTemplateList();
            LogMessage($"× Deleted template: {name}");
        }

        private void RefreshTemplateList()
        {
            _cmbTemplates.Items.Clear();
            foreach (var name in JoinTemplateManager.ListTemplateNames())
                _cmbTemplates.Items.Add(name);
            if (_cmbTemplates.Items.Count > 0)
                _cmbTemplates.SelectedIndex = 0;
        }

        // ─── HELPER CLASS ───────────────────────────────────────────────

        private class CatItem
        {
            public BuiltInCategory Category { get; }
            public CatItem(BuiltInCategory cat) => Category = cat;
            public override string ToString() => CategoryMatchRule.CategoryDisplayName(Category);
        }
    }
}

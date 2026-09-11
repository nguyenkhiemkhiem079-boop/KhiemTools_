using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace KhimTools.RebarTool.Forms
{
    internal sealed class RebarValidationRule
    {
        private readonly Func<Control> _errorControl;
        public Control Control => _errorControl();
        public Func<bool> IsValid { get; }
        public string Message { get; }

        public RebarValidationRule(Control control, Func<bool> isValid, string message)
            : this(() => control, isValid, message)
        {
        }

        public RebarValidationRule(Func<Control> errorControl, Func<bool> isValid, string message)
        {
            _errorControl = errorControl ?? (() => null);
            IsValid = isValid;
            Message = message;
        }
    }

    internal sealed class RebarFormGuard : IDisposable
    {
        private readonly Button _primaryButton;
        private readonly List<RebarValidationRule> _rules;
        private readonly ErrorProvider _errors;
        private readonly ToolStripStatusLabel _status;

        private RebarFormGuard(Form form, Button primaryButton, IEnumerable<RebarValidationRule> rules)
        {
            _primaryButton = primaryButton;
            _rules = rules.Where(rule => rule != null).ToList();
            _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink, ContainerControl = form };

            var strip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                SizingGrip = false,
                BackColor = Color.White,
                Padding = new Padding(10, 0, 8, 0)
            };
            _status = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5F)
            };
            strip.Items.Add(_status);
            strip.Items.Add(new ToolStripStatusLabel("VALIDATION | mm")
            {
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            });
            var validateButton = new ToolStripButton("Kiểm tra dữ liệu")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Kiểm tra định dạng và các trường đầu vào bắt buộc"
            };
            validateButton.Click += (sender, args) => ShowValidationSummary();
            strip.Items.Add(validateButton);
            form.Controls.Add(strip);
            strip.BringToFront();

            WireControlTree(form);
            form.Shown += OnInputChanged;
            ValidateNow();
        }

        public static RebarFormGuard Attach(Form form, Button primaryButton, params RebarValidationRule[] rules)
        {
            return new RebarFormGuard(form, primaryButton, rules ?? Array.Empty<RebarValidationRule>());
        }

        public static RebarValidationRule RequireSelection(ListBox list, string message)
        {
            return new RebarValidationRule(list, () => list != null && list.SelectedItems.Count > 0, message);
        }

        public static RebarValidationRule RequireCombo(ComboBox combo, string message)
        {
            return new RebarValidationRule(combo, () => combo != null && combo.SelectedIndex >= 0, message);
        }

        public static RebarValidationRule RequireNumericTextBoxes(Control root, string message)
        {
            TextBox invalid = null;
            return new RebarValidationRule(
                () => invalid ?? root,
                () =>
                {
                    invalid = EnumerateControls(root).OfType<TextBox>()
                        .FirstOrDefault(box => box.Visible && box.Enabled && !TryParseNonNegative(box.Text));
                    return invalid == null;
                },
                message);
        }

        public bool ValidateNow()
        {
            _errors.Clear();
            var failures = new List<RebarValidationRule>();
            foreach (var rule in _rules)
            {
                bool valid;
                try { valid = rule.IsValid(); }
                catch { valid = false; }
                if (!valid)
                {
                    failures.Add(rule);
                    if (rule.Control != null) _errors.SetError(rule.Control, rule.Message);
                }
            }

            bool canCreate = failures.Count == 0;
            _primaryButton.Enabled = canCreate;
            _status.ForeColor = canCreate ? Color.FromArgb(22, 101, 52) : Color.FromArgb(153, 27, 27);
            _status.Text = canCreate
                ? "Dữ liệu đầu vào hợp lệ."
                : "Cần kiểm tra: " + failures[0].Message;
            return canCreate;
        }

        private void WireControlTree(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (control is NumericUpDown numeric) numeric.ValueChanged += OnInputChanged;
                else if (control is ComboBox combo) combo.SelectedIndexChanged += OnInputChanged;
                else if (control is CheckBox check) check.CheckedChanged += OnInputChanged;
                else if (control is RadioButton radio) radio.CheckedChanged += OnInputChanged;
                else if (control is TextBox text) text.TextChanged += OnInputChanged;
                else if (control is ListBox list) list.SelectedIndexChanged += OnInputChanged;
                WireControlTree(control);
            }
        }

        private void OnInputChanged(object sender, EventArgs e)
        {
            ValidateNow();
        }

        private void ShowValidationSummary()
        {
            bool valid = ValidateNow();
            MessageBox.Show(
                _errors.ContainerControl,
                valid ? "Tất cả trường bắt buộc đã hợp lệ." : _status.Text,
                "K-TOOLS - Kiểm tra dữ liệu Rebar",
                MessageBoxButtons.OK,
                valid ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateControls(child)) yield return descendant;
            }
        }

        private static bool TryParseNonNegative(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string normalized = value.Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                && parsed >= 0 && !double.IsNaN(parsed) && !double.IsInfinity(parsed);
        }

        public void Dispose()
        {
            _errors.Dispose();
        }
    }
}

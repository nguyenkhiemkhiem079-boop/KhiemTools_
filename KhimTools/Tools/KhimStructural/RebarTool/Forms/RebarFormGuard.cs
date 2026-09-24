using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using KhimTools.Core;

namespace KhimTools.RebarTool.Forms
{
    internal sealed class RebarValidationRule
    {
        private readonly Func<Control> _errorControl;
        private readonly string _message;
        public Control Control => _errorControl();
        public Func<bool> IsValid { get; }
        public string Message => RebarFormGuard.LocalizeMessage(_message);

        public RebarValidationRule(Control control, Func<bool> isValid, string message)
            : this(() => control, isValid, message)
        {
        }

        public RebarValidationRule(Func<Control> errorControl, Func<bool> isValid, string message)
        {
            _errorControl = errorControl ?? (() => null);
            IsValid = isValid;
            _message = message ?? string.Empty;
        }
    }

    internal sealed class RebarFormGuard : IDisposable
    {
        private readonly Button _primaryButton;
        private readonly List<RebarValidationRule> _rules;
        private readonly ErrorProvider _errors;
        private readonly ToolStripStatusLabel _status;
        private readonly ToolStripStatusLabel _validationMode;
        private readonly ToolStripButton _validateButton;

        private static readonly Dictionary<string, string> EnglishMessages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Chọn ít nhất một panel sàn."] = "Select at least one slab panel.",
            ["Bật ít nhất một lớp hoặc nhóm thép cần tạo."] = "Enable at least one reinforcement layer or group.",
            ["Chọn đủ thép đáy phương X/Y."] = "Select bar types for both bottom X and Y layers.",
            ["Chọn đủ thép trên phương X/Y."] = "Select bar types for both top X and Y layers.",
            ["Chọn đủ thép mũ phương X/Y."] = "Select bar types for both support X and Y reinforcement.",
            ["Cập nhật Preview cho thông số hiện tại trước khi tạo thép."] = "Refresh the preview for the current inputs before creating rebar.",
            ["Chọn ít nhất một cột."] = "Select at least one column.",
            ["Chọn ít nhất một cột tròn."] = "Select at least one circular column.",
            ["Chọn ít nhất một móng."] = "Select at least one foundation.",
            ["Chọn loại thép chủ."] = "Select a main bar type.",
            ["Chọn loại thép đai."] = "Select a tie bar type.",
            ["Chọn thép lớp dưới phương X."] = "Select a bottom-layer X bar type.",
            ["Chọn thép lớp dưới phương Y."] = "Select a bottom-layer Y bar type.",
            ["Chọn đủ loại thép lớp trên X/Y."] = "Select bar types for both top-layer directions.",
            ["Chọn loại thép chờ cột."] = "Select a column dowel bar type.",
            ["Chọn đường kính thép chữ U mép móng."] = "Select a perimeter U-bar diameter.",
            ["Khoảng cách đai vùng A1 phải nhỏ hơn hoặc bằng A2."] = "A1 tie spacing must be less than or equal to A2.",
            ["Bố trí đai đa ô cần ít nhất 5 thanh chủ theo cạnh B."] = "The multi-cell tie layout requires at least five main bars along side B.",
            ["Giải Preview cho cột và thông số hiện tại trước khi tạo thép."] = "Solve the preview for the current column inputs before creating rebar.",
            ["Giải và kiểm tra Preview cho cấu hình hiện tại trước khi tạo thép."] = "Solve and verify the preview for the current settings before creating rebar.",
            ["Chọn một dầm hợp lệ."] = "Select a valid beam.",
            ["Chọn loại thép chủ phía trên."] = "Select a top main bar type.",
            ["Chọn loại thép chủ phía dưới."] = "Select a bottom main bar type.",
            ["Các thông số chiều dài và khoảng cách phải là số không âm."] = "Length and spacing inputs must be non-negative numbers.",
            ["Solve or refresh the beam preview for the current inputs before Create."] = "Solve or refresh the beam preview for the current inputs before Create."
        };
        private static readonly Dictionary<string, string> VietnameseMessages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Solve or refresh the beam preview for the current inputs before Create."] = "Giải hoặc cập nhật preview dầm theo thông số hiện tại trước khi tạo thép."
        };

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
            _validationMode = new ToolStripStatusLabel("VALIDATION | mm")
            {
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            strip.Items.Add(_validationMode);
            _validateButton = new ToolStripButton("Kiểm tra dữ liệu")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Kiểm tra định dạng và các trường đầu vào bắt buộc"
            };
            _validateButton.Click += (sender, args) => ShowValidationSummary();
            strip.Items.Add(_validateButton);
            form.Controls.Add(strip);
            strip.SendToBack();

            WireControlTree(form);
            form.Shown += OnInputChanged;
            form.Disposed += (sender, args) => Dispose();
            ApplyLanguage();
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
                        .FirstOrDefault(box => box.Visible && box.Enabled && !box.ReadOnly && !TryParseNonNegative(box.Text));
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
                ? (LanguageManager.IsEnglish ? "Inputs are valid." : "Dữ liệu đầu vào hợp lệ.")
                : (LanguageManager.IsEnglish ? "Check: " : "Cần kiểm tra: ") + failures[0].Message;
            return canCreate;
        }

        public void ApplyLanguage()
        {
            bool isEnglish = LanguageManager.IsEnglish;
            _validateButton.Text = isEnglish ? "Validate Inputs" : "Kiểm tra dữ liệu";
            _validateButton.ToolTipText = isEnglish
                ? "Validate required fields and input formats"
                : "Kiểm tra định dạng và các trường đầu vào bắt buộc";
            _validationMode.Text = isEnglish ? "VALIDATION | mm" : "KIỂM TRA | mm";
            ValidateNow();
        }

        internal static string LocalizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;
            string translated;
            if (LanguageManager.IsEnglish)
                return EnglishMessages.TryGetValue(message, out translated) ? translated : message;
            return VietnameseMessages.TryGetValue(message, out translated) ? translated : message;
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
                valid
                    ? (LanguageManager.IsEnglish ? "All required inputs are valid." : "Tất cả trường bắt buộc đã hợp lệ.")
                    : _status.Text,
                LanguageManager.IsEnglish ? "K-TOOLS - Rebar Input Validation" : "K-TOOLS - Kiểm tra dữ liệu Rebar",
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

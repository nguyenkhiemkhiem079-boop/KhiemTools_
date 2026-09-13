using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using KhimTools.RebarTool.Core;
using Newtonsoft.Json;
using Document = Autodesk.Revit.DB.Document;

namespace KhimTools.RebarTool.Forms
{
    internal sealed class RebarConfigurationField
    {
        internal string Key;
        internal string Caption;
        internal Control Control;
        internal decimal Minimum;
        internal decimal Maximum;
        internal decimal Read() => Control is CheckBox check ? (check.Checked ? 1 : 0)
            : Control is NumericUpDown number ? number.Value : decimal.Parse(Control.Text, CultureInfo.CurrentCulture);
        internal void Write(decimal value)
        {
            if (value < Minimum || value > Maximum ||
                (Control is CheckBox || Control is NumericUpDown integral && integral.DecimalPlaces == 0) && value != decimal.Truncate(value))
                throw new InvalidDataException("Ngoài phạm vi: " + Caption);
            if (Control is CheckBox check) check.Checked = value == 1;
            else if (Control is NumericUpDown number) number.Value = value;
            else Control.Text = value.ToString(CultureInfo.CurrentCulture);
        }
        internal static RebarConfigurationField Number(string key, string caption, NumericUpDown control)
            => new RebarConfigurationField { Key = key, Caption = caption, Control = control, Minimum = control.Minimum, Maximum = control.Maximum };
        internal static RebarConfigurationField Length(string key, string caption, TextBox control)
            => new RebarConfigurationField { Key = key, Caption = caption, Control = control, Minimum = 1, Maximum = 100000 };
        internal static RebarConfigurationField Flag(string key, string caption, CheckBox control)
            => new RebarConfigurationField { Key = key, Caption = caption, Control = control, Minimum = 0, Maximum = 1 };
    }

    internal static class RebarConfigurationPage
    {
        internal static TabPage Create(Form owner, Document document, RebarReferenceKind kind, params RebarConfigurationField[] fields)
        {
            var page = new TabPage("Cấu hình dự án") { Name = "RebarConfigurationPage", BackColor = Color.White };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.Controls.Add(root);
            var scope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            scope.Items.AddRange(new object[] { "Mặc định dự án", "Riêng loại cấu kiện" });
            scope.SelectedIndex = 1;
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            toolbar.Controls.Add(scope);
            var revision = new Label { AutoSize = true, Margin = new Padding(8) };
            toolbar.Controls.Add(revision);
            root.Controls.Add(toolbar, 0, 0);
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(split, 0, 1);
            var grid = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Ghi đè", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Thông số", ReadOnly = true, FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Giá trị", FillWeight = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nguồn", ReadOnly = true, FillWeight = 45 });
            split.Controls.Add(grid, 0, 0);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            root.Controls.Add(footer, 0, 2);
            string member = kind.ToString();
            RebarConfigurationStore store = document == null || string.IsNullOrEmpty(document.PathName) ? null : new RebarConfigurationStore(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KhimTools", "RebarConfiguration"),
                document.ProjectInformation.UniqueId + "|" + document.PathName);
            RebarConfiguration config = new RebarConfiguration();
            var fallback = fields.ToDictionary(f => f.Key, f => f.Read());
            Action refresh = () =>
            {
                grid.Rows.Clear();
                config.Members.TryGetValue(member, out var local);
                var layer = scope.SelectedIndex == 0 ? config.Project : local;
                foreach (var field in fields)
                {
                    bool overridden = layer != null && layer.ContainsKey(field.Key);
                    decimal value = overridden ? layer[field.Key] : scope.SelectedIndex == 0
                        ? fallback[field.Key] : config.Project.TryGetValue(field.Key, out var projectValue) ? projectValue : fallback[field.Key];
                    int row = grid.Rows.Add(overridden, field.Caption, value.ToString(CultureInfo.CurrentCulture), overridden ? "Ghi đè" : config.Project.ContainsKey(field.Key) && scope.SelectedIndex == 1 ? "Dự án" : "Form");
                    if (field.Control is CheckBox)
                        grid.Rows[row].Cells[2] = new DataGridViewCheckBoxCell { Value = value == 1 };
                }
                revision.Text = "Revision " + config.Revision;
            };
            Action<RebarConfiguration> validate = candidate =>
            {
                foreach (var field in fields)
                {
                    decimal value = candidate.Resolve(member, field.Key, fallback[field.Key]);
                    if (value < field.Minimum || value > field.Maximum ||
                        (field.Control is CheckBox || field.Control is NumericUpDown number && number.DecimalPlaces == 0) && value != decimal.Truncate(value))
                        throw new InvalidDataException("Ngoài phạm vi: " + field.Caption);
                }
            };
            Action apply = () =>
            {
                validate(config);
                foreach (var field in fields) field.Write(config.Resolve(member, field.Key, fallback[field.Key]));
            };
            Action load = () => { config = store?.Load() ?? new RebarConfiguration(); validate(config); refresh(); };
            Func<int, RebarConfiguration> capture = scopeIndex =>
            {
                if (!grid.EndEdit()) throw new InvalidDataException("Hoàn tất nhập giá trị trước khi lưu.");
                var next = RebarConfigurationStore.Parse(JsonConvert.SerializeObject(config));
                if (!next.Members.ContainsKey(member)) next.Members[member] = new Dictionary<string, decimal>();
                var layer = scopeIndex == 0 ? next.Project : next.Members[member];
                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (!Convert.ToBoolean(grid.Rows[i].Cells[0].Value)) { layer.Remove(field.Key); continue; }
                    decimal value;
                    bool valid;
                    if (field.Control is CheckBox) { value = Convert.ToBoolean(grid.Rows[i].Cells[2].Value) ? 1 : 0; valid = true; }
                    else valid = decimal.TryParse(Convert.ToString(grid.Rows[i].Cells[2].Value), NumberStyles.Number, CultureInfo.CurrentCulture, out value);
                    if (!valid || value < field.Minimum || value > field.Maximum ||
                        field.Control is NumericUpDown number && number.DecimalPlaces == 0 && value != decimal.Truncate(value))
                        throw new InvalidDataException("Giá trị không hợp lệ: " + field.Caption);
                    layer[field.Key] = value;
                }
                validate(next);
                return next;
            };
            Action<string, Action> button = (caption, action) =>
            {
                var command = new Button { Text = caption, AutoSize = true, MinimumSize = new Size(80, 32) };
                command.Click += (s, e) => { try { action(); } catch (Exception ex) { MessageBox.Show(page.FindForm(), ex.Message, "Cấu hình Rebar", MessageBoxButtons.OK, MessageBoxIcon.Warning); } };
                footer.Controls.Add(command);
            };
            button("Nạp lại", load);
            button("Lưu & áp dụng", () =>
            {
                if (store == null) throw new InvalidOperationException("Lưu dự án Revit trước khi lưu cấu hình.");
                var next = capture(scope.SelectedIndex);
                store.Save(next);
                config = next;
                apply(); refresh();
            });
            button("Nhập JSON", () =>
            {
                using (var dialog = new OpenFileDialog { Filter = "Rebar configuration|*.json", CheckFileExists = true })
                    if (dialog.ShowDialog(page.FindForm()) == DialogResult.OK)
                    {
                        var next = RebarConfigurationStore.Parse(File.ReadAllText(dialog.FileName));
                        validate(next);
                        next.ProjectKey = config.ProjectKey;
                        next.Revision = config.Revision;
                        config = next; refresh();
                    }
            });
            button("Xuất JSON", () =>
            {
                config = capture(scope.SelectedIndex);
                using (var dialog = new SaveFileDialog { Filter = "Rebar configuration|*.json", FileName = "RebarConfiguration.json" })
                    if (dialog.ShowDialog(page.FindForm()) == DialogResult.OK)
                        File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(config, Formatting.Indented));
            });
            int displayedScope = scope.SelectedIndex;
            bool restoringScope = false;
            scope.SelectedIndexChanged += (s, e) =>
            {
                if (restoringScope) return;
                try { config = capture(displayedScope); displayedScope = scope.SelectedIndex; refresh(); }
                catch (Exception ex)
                {
                    restoringScope = true;
                    scope.SelectedIndex = displayedScope;
                    restoringScope = false;
                    MessageBox.Show(owner, ex.Message, "Cấu hình Rebar");
                }
            };
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            // Load after all generation fields exist. Invalid files remain untouched.
            owner.Shown += (sender, args) =>
            {
                try
                {
                    foreach (var field in fields) fallback[field.Key] = field.Read();
                    load(); apply();
                }
                catch (Exception ex) { revision.Text = "Không nạp được cấu hình"; MessageBox.Show(owner, ex.Message, "Cấu hình Rebar"); }
            };
            refresh();
            return page;
        }
    }
}

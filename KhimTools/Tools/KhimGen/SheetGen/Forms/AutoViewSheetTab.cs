using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.SheetGen.Services;
using View = Autodesk.Revit.DB.View;
using ComboBox = System.Windows.Forms.ComboBox;
using Control = System.Windows.Forms.Control;
using Color = System.Drawing.Color;

namespace KhimTools.SheetGen.Forms
{
    public partial class SheetGenForm
    {
        private List<AutoViewRow> _autoRows = new List<AutoViewRow>();
        private void SetupAutoViewSheetTab()
        {
            var tab = new TabPage("Auto View → Sheet") { BackColor = Color.White };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var mode = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            mode.Items.AddRange(new object[] { "Đưa view đã có vào sheet", "Tạo view theo tầng → sheet" }); mode.SelectedIndex = 0;
            var types = _doc == null ? new List<ViewFamilyType>() : new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .Where(t => t.ViewFamily == ViewFamily.FloorPlan || t.ViewFamily == ViewFamily.StructuralPlan || t.ViewFamily == ViewFamily.CeilingPlan).OrderBy(t => t.Name).ToList();
            var templates = _doc == null ? new List<View>() : new FilteredElementCollector(_doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).OrderBy(v => v.Name).ToList();
            var typeChoice = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = types, DisplayMember = "Name", Enabled = false };
            var templateChoice = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            templateChoice.Items.Add("Không dùng template");
            foreach (var template in templates) templateChoice.Items.Add(template.Name);
            templateChoice.SelectedIndex = 0;
            var suffix = new TextBox { Width = 110, Text = " - PLAN", Enabled = false };
            var margin = new NumericUpDown { Width = 65, Minimum = 0, Maximum = 200, Value = 30 };
            var preview = new Button { Text = "Lập bảng xem trước", Width = 150, Height = 30, Enabled = _doc != null };
            toolbar.Controls.AddRange(new Control[] { mode, typeChoice, templateChoice,
                new Label { Text = "Hậu tố", AutoSize = true, Margin = new Padding(4,6,4,0) }, suffix,
                new Label { Text = "Lề sheet (mm)", AutoSize = true, Margin = new Padding(4,6,4,0) }, margin, preview });
            var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "Chọn", FillWeight = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ViewName", HeaderText = "Tên view", FillWeight = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ViewType", HeaderText = "Loại view", ReadOnly = true });
            var sheets = _doc == null ? new List<ViewSheet>() : new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => !s.IsPlaceholder).OrderBy(s => s.SheetNumber).ToList();
            var sheetNumbers = new List<string> { "" }; sheetNumbers.AddRange(sheets.Select(s => s.SheetNumber));
            grid.Columns.Add(new DataGridViewComboBoxColumn { DataPropertyName = "SheetNumber", HeaderText = "Sheet đích", DataSource = sheetNumbers });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Trạng thái", ReadOnly = true, FillWeight = 220 });
            grid.CurrentCellDirtyStateChanged += (s,e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var all = new Button { Text = "Chọn tất cả", Width = 110, Height = 32 };
            var clear = new Button { Text = "Bỏ chọn", Width = 100, Height = 32 };
            var run = new Button { Text = "Đưa view vào sheet", Width = 170, Height = 32, Enabled = false };
            var status = new Label { AutoSize = true, Text = "Lập bảng xem trước rồi chọn dòng cần chạy. Lề áp dụng bốn phía; điều chỉnh để tránh khung tên.", MaximumSize = new Size(650,0), Margin = new Padding(8,6,0,0) };
            footer.Controls.AddRange(new Control[] { all, clear, run, status });
            mode.SelectedIndexChanged += (s,e) => {
                typeChoice.Enabled = templateChoice.Enabled = suffix.Enabled = mode.SelectedIndex == 1;
                _autoRows.Clear(); grid.DataSource = null; run.Enabled = false;
            };
            Action invalidate = () => { _autoRows.Clear(); grid.DataSource = null; run.Enabled = false; status.Text = "Cấu hình đã đổi — lập lại bảng xem trước."; };
            typeChoice.SelectedIndexChanged += (s,e) => invalidate();
            templateChoice.SelectedIndexChanged += (s,e) => invalidate();
            suffix.TextChanged += (s,e) => invalidate();
            preview.Click += (s,e) => {
                if (mode.SelectedIndex == 0) _autoRows = AutoViewSheetService.Preview(_doc);
                else
                {
                    if (!(typeChoice.SelectedItem is ViewFamilyType selectedType)) { status.Text = "Chọn loại view."; return; }
                    var templateId = templateChoice.SelectedIndex > 0 ? templates[templateChoice.SelectedIndex - 1].Id : ElementId.InvalidElementId;
                    _autoRows = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).Select(level => {
                        var matches = sheets.Where(sheet => sheet.Name.Equals(level.Name, StringComparison.OrdinalIgnoreCase) || AutoViewSheetService.MatchesNumber(sheet.Name, level.Name)).ToList();
                        return new AutoViewRow { CreateNew = true, LevelId = level.Id, ViewTypeId = selectedType.Id, TemplateId = templateId,
                            ViewId = ElementId.InvalidElementId, ViewName = level.Name + suffix.Text, ViewType = selectedType.Name,
                            SheetNumber = matches.Count == 1 ? matches[0].SheetNumber : "", Status = "Chọn tầng và sheet đích để tạo & đặt view" };
                    }).ToList();
                }
                grid.DataSource = _autoRows; grid.Columns[1].ReadOnly = mode.SelectedIndex == 0;
                run.Enabled = _autoRows.Count > 0;
                status.Text = $"{_autoRows.Count} dòng. Các dòng chưa được chọn; rà soát sheet đích trước khi chạy.";
            };
            all.Click += (s,e) => { foreach (var row in _autoRows) row.Selected = true; grid.Refresh(); };
            clear.Click += (s,e) => { foreach (var row in _autoRows) row.Selected = false; grid.Refresh(); };
            run.Click += (s,e) => {
                grid.EndEdit();
                try { UseWaitCursor = true; int count = AutoViewSheetService.Place(_doc, _autoRows, (double)margin.Value);
                    grid.Refresh(); status.Text = $"Đã đặt {count} view. Xem trạng thái từng dòng; view lỗi không được tạo/đặt."; }
                catch (Exception ex) { status.Text = "Không hoàn tất: " + ex.Message; }
                finally { UseWaitCursor = false; }
            };
            root.Controls.Add(toolbar,0,0); root.Controls.Add(grid,0,1); root.Controls.Add(footer,0,2);
            tab.Controls.Add(root); _tabControl.TabPages.Add(tab);
            _tabControl.SelectedIndexChanged += (s,e) => { if (_btnGenerate != null) _btnGenerate.Visible = _tabControl.SelectedTab != tab; };
        }
    }
}

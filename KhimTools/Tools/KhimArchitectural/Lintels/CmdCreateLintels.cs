using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ComboBox = System.Windows.Forms.ComboBox;
using Control = System.Windows.Forms.Control;
using Form = System.Windows.Forms.Form;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace KhimTools.Architectural.Lintels
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCreateLintels : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                List<FamilyInstance> openings = GetOpenings(uidoc);
                if (openings.Count == 0) return Result.Cancelled;

                List<FamilySymbol> symbols = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .WhereElementIsElementType()
                    .Cast<FamilySymbol>()
                    .OrderBy(x => x.FamilyName).ThenBy(x => x.Name)
                    .ToList();

                if (symbols.Count == 0)
                {
                    TaskDialog.Show("K-TOOLS — Lanh tô", "Dự án chưa có Structural Framing type để tạo lanh tô.");
                    return Result.Cancelled;
                }

                using var form = new LintelSettingsForm(symbols, openings.Count);
                if (form.ShowDialog() != DialogResult.OK) return Result.Cancelled;

                var result = LintelService.Create(doc, openings, form.SelectedSymbol,
                    form.EndExtensionMm, form.VerticalOffsetMm, form.SkipExisting);

                TaskDialog.Show("K-TOOLS — Lanh tô",
                    $"Đã tạo {result.Created} lanh tô cho {openings.Count} cửa/cửa sổ.\n" +
                    $"Bỏ qua đã có: {result.SkippedExisting}\nKhông tạo được: {result.Failed}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("K-TOOLS — Lanh tô", ex.Message);
                return Result.Failed;
            }
        }

        private static List<FamilyInstance> GetOpenings(UIDocument uidoc)
        {
            Document doc = uidoc.Document;
            var selected = uidoc.Selection.GetElementIds()
                .Select(doc.GetElement)
                .OfType<FamilyInstance>()
                .Where(IsDoorOrWindow)
                .ToList();
            if (selected.Count > 0) return selected;

            IList<Reference> picked = uidoc.Selection.PickObjects(ObjectType.Element,
                new DoorWindowFilter(), "Chọn cửa đi/cửa sổ cần tạo lanh tô, sau đó bấm Finish");
            return picked.Select(x => doc.GetElement(x)).OfType<FamilyInstance>().Distinct().ToList();
        }

        private static bool IsDoorOrWindow(FamilyInstance item)
        {
            ElementId categoryId = item.Category?.Id;
            return categoryId == new ElementId(BuiltInCategory.OST_Doors) ||
                   categoryId == new ElementId(BuiltInCategory.OST_Windows);
        }

        private sealed class DoorWindowFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is FamilyInstance fi && IsDoorOrWindow(fi);
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }

    internal sealed class LintelSettingsForm : Form
    {
        private readonly ComboBox _types;
        private readonly NumericUpDown _extension;
        private readonly NumericUpDown _offset;
        private readonly CheckBox _skipExisting;

        internal FamilySymbol SelectedSymbol => ((SymbolItem)_types.SelectedItem).Symbol;
        internal double EndExtensionMm => (double)_extension.Value;
        internal double VerticalOffsetMm => (double)_offset.Value;
        internal bool SkipExisting => _skipExisting.Checked;

        internal LintelSettingsForm(IList<FamilySymbol> symbols, int openingCount)
        {
            Text = "K-TOOLS — Tạo lanh tô";
            Width = 510;
            Height = 300;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new System.Drawing.Font("Segoe UI", 9F);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = $"Đã chọn {openingCount} cửa/cửa sổ", AutoSize = true, Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold) }, 0, 0);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 0), 2);

            _types = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (FamilySymbol symbol in symbols) _types.Items.Add(new SymbolItem(symbol));
            int preferred = -1;
            for (int i = 0; i < symbols.Count; i++)
            {
                if (Contains(symbols[i].Name, "lintel") || Contains(symbols[i].Name, "lanh"))
                {
                    preferred = i;
                    break;
                }
            }
            _types.SelectedIndex = preferred >= 0 ? preferred : 0;
            AddRow(layout, 1, "Loại Structural Framing", _types);

            _extension = CreateNumber(200, 0, 2000);
            AddRow(layout, 2, "Vươn mỗi đầu (mm)", _extension);
            _offset = CreateNumber(0, -1000, 3000);
            AddRow(layout, 3, "Bù cao độ (mm)", _offset);
            _skipExisting = new CheckBox { Text = "Bỏ qua cửa đã có lanh tô do K-TOOLS tạo", Checked = true, AutoSize = true };
            layout.Controls.Add(_skipExisting, 1, 4);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            buttons.Controls.Add(new Button { Text = "Tạo lanh tô", DialogResult = DialogResult.OK, Width = 110, Height = 30 });
            buttons.Controls.Add(new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Width = 80, Height = 30 });
            layout.Controls.Add(buttons, 0, 5);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            AcceptButton = (Button)buttons.Controls[0];
            CancelButton = (Button)buttons.Controls[1];
        }

        private static void AddRow(TableLayoutPanel panel, int row, string text, Control control)
        {
            panel.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        private static NumericUpDown CreateNumber(decimal value, decimal min, decimal max) =>
            new NumericUpDown { Value = value, Minimum = min, Maximum = max, DecimalPlaces = 0, Increment = 10, Dock = DockStyle.Left, Width = 130 };

        private static bool Contains(string value, string text) =>
            value?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

        private sealed class SymbolItem
        {
            internal FamilySymbol Symbol { get; }
            internal SymbolItem(FamilySymbol symbol) { Symbol = symbol; }
            public override string ToString() => $"{Symbol.FamilyName} : {Symbol.Name}";
        }
    }
}

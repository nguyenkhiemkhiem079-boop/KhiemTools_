using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using KhimTools.Core.UI;
using WinFormsControl = System.Windows.Forms.Control;

namespace KhimTools.DetailNumberUpdater.Forms
{
    public class SheetSelectionForm : KTBaseForm
    {
        private readonly List<ViewSheet> _sheets;
        private readonly TextBox _txtSearch;
        private readonly ListBox _lstSheets;

        public ViewSheet SelectedSheet { get; private set; }

        public SheetSelectionForm(Document doc)
        {
            _sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(sheet => !sheet.IsPlaceholder)
                .OrderBy(sheet => sheet.SheetNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(sheet => sheet.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Text = "K-TOOLS — Select Sheet";
            Width = 560;
            Height = 440;
            MinimumSize = new Size(460, 340);

            var lbl = new Label { Text = "Select Sheet / Chọn Sheet", AutoSize = true, Left = 16, Top = 16 };
            _txtSearch = new TextBox { Left = 16, Top = 44, Width = 510, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _txtSearch.TextChanged += (sender, args) => RefreshList();
            _lstSheets = new ListBox { Left = 16, Top = 78, Width = 510, Height = 260, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            _lstSheets.DoubleClick += (sender, args) => AcceptSelection();

            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.None, Width = 90, Height = 30, Left = 336, Top = 350, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            btnOk.Click += (sender, args) => AcceptSelection();
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 30, Left = 436, Top = 350, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            Controls.AddRange(new WinFormsControl[] { lbl, _txtSearch, _lstSheets, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            RefreshList();
        }

        private void RefreshList()
        {
            string query = (_txtSearch.Text ?? string.Empty).Trim();
            _lstSheets.BeginUpdate();
            try
            {
                _lstSheets.Items.Clear();
                foreach (ViewSheet sheet in _sheets.Where(item => string.IsNullOrEmpty(query) ||
                    (item.SheetNumber ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (item.Name ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    _lstSheets.Items.Add(new SheetListItem(sheet));
            }
            finally
            {
                _lstSheets.EndUpdate();
            }
        }

        private void AcceptSelection()
        {
            var item = _lstSheets.SelectedItem as SheetListItem;
            if (item == null)
            {
                MessageBox.Show("Select a sheet first.", "K-TOOLS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SelectedSheet = item.Sheet;
            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class SheetListItem
        {
            public ViewSheet Sheet { get; private set; }
            public SheetListItem(ViewSheet sheet) { Sheet = sheet; }
            public override string ToString() { return (Sheet.SheetNumber ?? string.Empty) + " — " + (Sheet.Name ?? string.Empty); }
        }
    }
}

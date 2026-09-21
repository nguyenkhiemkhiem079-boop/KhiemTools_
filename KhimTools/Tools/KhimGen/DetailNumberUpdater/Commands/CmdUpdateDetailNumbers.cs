using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.DetailNumberUpdater.Forms;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.DetailNumberUpdater.Commands
{
    /// <summary>
    /// Command: Cập nhật Detail Number cho các Viewport trên Sheet từ tên View.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdUpdateDetailNumbers : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("K-TOOLS", "Không tìm thấy tài liệu Revit đang mở.");
                return Result.Cancelled;
            }

            try
            {
                ViewSheet sheet = doc.ActiveView as ViewSheet;
                if (sheet == null)
                {
                    using (var selector = new SheetSelectionForm(doc))
                    {
                        if (selector.ShowDialog() != DialogResult.OK || selector.SelectedSheet == null)
                            return Result.Cancelled;
                        sheet = selector.SelectedSheet;
                    }
                }

                if (sheet == null)
                {
                    TaskDialog.Show("K-TOOLS — Update Detail Numbers",
                        "Vui lòng mở một Sheet view để chạy công cụ cập nhật Detail Number.");
                    return Result.Cancelled;
                }

                var form = new UpdateDetailNumbersForm(doc, sheet);
                form.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("K-TOOLS — Lỗi Update Detail Numbers", $"Lỗi không mong đợi:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}

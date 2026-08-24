using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.ElementTags.Forms;

namespace KhimTools.ElementTags.Commands
{
    /// <summary>
    /// Command: Khởi chạy giao diện gán Tag và quản lý Tag hàng loạt (Elements Tags).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdElementTags : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("KhimTools — Elements Tags", "Không tìm thấy tài liệu Revit đang mở.");
                return Result.Cancelled;
            }

            try
            {
                using (var form = new ElementTagsForm(uidoc))
                {
                    form.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("KhimTools — Elements Tags Error", $"Lỗi không mong đợi:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.SlabJoin.Forms;

namespace KhimTools.SlabJoin.Commands
{
    /// <summary>
    /// Lệnh "Join Elements" — mở form Join Elements chuyên nghiệp
    /// hỗ trợ join/unjoin/switch giữa bất kỳ loại cấu kiện nào.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdJoinElements : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;
                if (doc == null) return Result.Cancelled;

                var selectedIds = uidoc.Selection.GetElementIds();
                var form = new JoinElementsForm(doc, selectedIds);
                form.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Join Elements Error",
                    $"[{ex.GetType().Name}] {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}

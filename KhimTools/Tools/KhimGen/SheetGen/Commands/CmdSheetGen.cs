using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.SheetGen.Forms;

namespace KhimTools.SheetGen.Commands
{
    /// <summary>
    /// Command: Mở giao diện Tạo Sheet Tự Động (Auto Sheet Generator / SheetGen).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdSheetGen : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            using (var form = new SheetGenForm(uidoc.Document))
            {
                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }
}
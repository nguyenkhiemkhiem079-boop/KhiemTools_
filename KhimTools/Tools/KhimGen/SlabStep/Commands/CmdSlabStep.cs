using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.SlabStep.Forms;

namespace KhimTools.SlabStep.Commands
{
    /// <summary>
    /// Command: Mở giao diện Tạo Giật Cấp Sàn Tự Động (Slab Step Generator).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdSlabStep : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            using (var form = new SlabStepForm(uidoc))
            {
                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }
}

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.QuickDraft.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickSlab : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData?.Application;
            var uidoc = uiapp?.ActiveUIDocument;
            var doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                var cmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.StructuralFloor);
                if (cmdId != null && uiapp.CanPostCommand(cmdId))
                {
                    uiapp.PostCommand(cmdId);
                    return Result.Succeeded;
                }

                cmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.ArchitecturalFloor);
                if (cmdId != null && uiapp.CanPostCommand(cmdId))
                {
                    uiapp.PostCommand(cmdId);
                    return Result.Succeeded;
                }

                TaskDialog.Show("KhimTools Quick Slab", "Slab placement command is ready. Please use standard Revit Floor tool.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("KhimTools Quick Slab", $"Error launching slab tool:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}

using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.QuickDraft.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickWall : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData?.Application;
            var uidoc = uiapp?.ActiveUIDocument;
            var doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                // Attempt to post native Structural Wall or Wall command
                var cmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.StructuralWall);
                if (cmdId != null && uiapp.CanPostCommand(cmdId))
                {
                    uiapp.PostCommand(cmdId);
                    return Result.Succeeded;
                }

                cmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.ArchitecturalWall);
                if (cmdId != null && uiapp.CanPostCommand(cmdId))
                {
                    uiapp.PostCommand(cmdId);
                    return Result.Succeeded;
                }

                TaskDialog.Show("KhimTools Quick Wall", "Wall placement command is ready. Please use standard Revit Wall tool.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("KhimTools Quick Wall", $"Error launching wall tool:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}

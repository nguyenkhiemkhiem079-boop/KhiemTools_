using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.FamilyManager.Models;
using KhimTools.QuickDraft.Services;

namespace KhimTools.QuickDraft.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdQuickFoundation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData?.Application?.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var settings = FamilyManagerSettings.Load();
            string preferred = settings.PreferredFamilies.TryGetValue("Foundation", out var p) ? p : "Móng cọc 1 tim";

            return QuickDraftService.PlaceLoadableElement(uidoc, BuiltInCategory.OST_StructuralFoundation, preferred, "Structural Foundation");
        }
    }
}

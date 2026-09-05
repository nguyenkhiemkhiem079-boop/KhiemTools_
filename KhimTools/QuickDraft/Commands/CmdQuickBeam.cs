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
    public class CmdQuickBeam : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData?.Application?.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var settings = FamilyManagerSettings.Load();
            string preferred = settings.PreferredFamilies.TryGetValue("Beam", out var p) ? p : "M_Concrete-Rectangular-Beam";

            return QuickDraftService.PlaceLoadableElement(uidoc, BuiltInCategory.OST_StructuralFraming, preferred, "Structural Framing / Beam");
        }
    }
}

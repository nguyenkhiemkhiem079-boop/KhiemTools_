using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.MEP.Tags
{
    /// <summary>
    /// Command: Tự động gán nhãn cao độ đáy (BOP/Invert Elevation) cho ống gió và ống cấp thoát nước.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdMepElevationTags : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            TaskDialog.Show("KhimMEP — Elevation Tags",
                LanguageManager.IsEnglish
                    ? "Ready to auto-tag invert & bottom of pipe/duct elevations."
                    : "Sẵn sàng tự động gắn Tag cao độ đáy (BOP) và cao độ tim cho ống MEP.");

            return Result.Succeeded;
        }
    }
}

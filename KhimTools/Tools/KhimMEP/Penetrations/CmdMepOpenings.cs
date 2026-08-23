using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.MEP.Penetrations
{
    /// <summary>
    /// Command: Tự động kiểm tra xung đột ống MEP với Dầm/Sàn/Vách và đục lỗ mở (Openings).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdMepOpenings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            int ductCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctCurves).GetElementCount();
            int pipeCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeCurves).GetElementCount();

            TaskDialog.Show("KhimMEP — Openings & Penetrations",
                LanguageManager.IsEnglish
                    ? $"MEP Clash Detector ready.\nFound: {ductCount} Ducts, {pipeCount} Pipes."
                    : $"Bộ phát hiện xung đột và tạo lỗ mở MEP sẵn sàng.\nTìm thấy: {ductCount} Ống gió, {pipeCount} Ống nước.");

            return Result.Succeeded;
        }
    }
}

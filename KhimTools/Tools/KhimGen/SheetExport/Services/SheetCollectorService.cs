using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class SheetCollectorService
    {
        public static List<SheetExportItem> GetAllSheets(Document doc)
        {
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate && !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
                .ToList();

            var list = new List<SheetExportItem>();

            // Collect once across the document. View-scoped collectors can force
            // Revit to prepare view geometry, which is expensive on first open.
            var titleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .ToElements()
                .GroupBy(t => t.OwnerViewId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var sheet in sheets)
            {
                titleBlocks.TryGetValue(sheet.Id, out var titleBlock);
                Revision revision = null;
                string revisionSequence = "0";
                try
                {
                    var revisionIds = sheet.GetAllRevisionIds();
                    revisionSequence = revisionIds?.Count.ToString() ?? "0";
                    if (revisionIds != null && revisionIds.Count > 0)
                        revision = doc.GetElement(revisionIds.Last()) as Revision;
                }
                catch { }

                var item = new SheetExportItem
                {
                    Sheet = sheet,
                    SheetUniqueId = sheet.UniqueId,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    CurrentRevisionNumber = revision?.RevisionNumber
                        ?? sheet.LookupParameter("Current Revision")?.AsString()
                        ?? sheet.LookupParameter("Sheet Revision")?.AsString() ?? "",
                    CurrentRevisionDate = revision?.RevisionDate
                        ?? sheet.LookupParameter("Current Revision Date")?.AsString() ?? "",
                    RevisionSequence = revisionSequence,
                    PaperSize = DetectPaperSize(titleBlock),
                    Orientation = DetectOrientation(titleBlock)
                };

                list.Add(item);
            }

            return list;
        }

        private static string DetectPaperSize(Element titleBlock)
        {
            try
            {
                if (titleBlock != null)
                {
                    var wParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                    var hParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT);

                    if (wParam != null && hParam != null)
                    {
                        double wMm = UnitUtils.ConvertFromInternalUnits(wParam.AsDouble(), UnitTypeId.Millimeters);
                        double hMm = UnitUtils.ConvertFromInternalUnits(hParam.AsDouble(), UnitTypeId.Millimeters);

                        double maxDim = Math.Max(wMm, hMm);
                        double minDim = Math.Min(wMm, hMm);

                        if (Math.Abs(maxDim - 1189) < 50 && Math.Abs(minDim - 841) < 50) return "A0";
                        if (Math.Abs(maxDim - 841) < 50 && Math.Abs(minDim - 594) < 50) return "A1";
                        if (Math.Abs(maxDim - 594) < 50 && Math.Abs(minDim - 420) < 50) return "A2";
                        if (Math.Abs(maxDim - 420) < 50 && Math.Abs(minDim - 297) < 50) return "A3";
                        if (Math.Abs(maxDim - 297) < 50 && Math.Abs(minDim - 210) < 50) return "A4";

                        return $"{Math.Round(maxDim)}x{Math.Round(minDim)}mm";
                    }
                }
            }
            catch { }
            return "A1";
        }

        private static string DetectOrientation(Element titleBlock)
        {
            try
            {
                if (titleBlock != null)
                {
                    var wParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                    var hParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT);

                    if (wParam != null && hParam != null)
                    {
                        return wParam.AsDouble() >= hParam.AsDouble() ? "Landscape" : "Portrait";
                    }
                }
            }
            catch { }
            return "Landscape";
        }
    }
}

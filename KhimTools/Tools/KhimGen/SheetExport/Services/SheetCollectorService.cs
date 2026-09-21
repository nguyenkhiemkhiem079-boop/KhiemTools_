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

            // A collector scoped to one sheet is expensive because Revit has to build and
            // evaluate a new element query every time. Collect title blocks once, then use
            // OwnerViewId to look up the block belonging to each sheet.
            var titleBlocksBySheetId = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(e => e.OwnerViewId != null && e.OwnerViewId != ElementId.InvalidElementId)
                .GroupBy(e => e.OwnerViewId)
                .ToDictionary(g => g.Key, g => g.First());

            var list = new List<SheetExportItem>(sheets.Count);

            foreach (var sheet in sheets)
            {
                titleBlocksBySheetId.TryGetValue(sheet.Id, out var titleBlock);
                GetPaperInfo(titleBlock, out string paperSize, out string orientation);
                GetCurrentRevisionInfo(sheet, doc, out string revisionNumber,
                    out string revisionDate, out string revisionSequence);

                var item = new SheetExportItem
                {
                    Sheet = sheet,
                    SheetUniqueId = sheet.UniqueId,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    CurrentRevisionNumber = revisionNumber,
                    CurrentRevisionDate = revisionDate,
                    RevisionSequence = revisionSequence,
                    PaperSize = paperSize,
                    Orientation = orientation
                };

                list.Add(item);
            }

            return list;
        }

        private static void GetCurrentRevisionInfo(ViewSheet sheet, Document doc,
            out string revisionNumber, out string revisionDate, out string revisionSequence)
        {
            revisionNumber = "";
            revisionDate = "";
            revisionSequence = "0";
            try
            {
                var revIds = sheet.GetAllRevisionIds();
                revisionSequence = revIds?.Count.ToString() ?? "0";
                if (revIds != null && revIds.Any())
                {
                    var lastRevId = revIds.Last();
                    if (doc.GetElement(lastRevId) is Revision rev)
                    {
                        revisionNumber = rev.RevisionNumber ?? "";
                        revisionDate = rev.RevisionDate ?? "";
                        return;
                    }
                }
                revisionNumber = sheet.LookupParameter("Current Revision")?.AsString()
                    ?? sheet.LookupParameter("Sheet Revision")?.AsString() ?? "";
                revisionDate = sheet.LookupParameter("Current Revision Date")?.AsString() ?? "";
            }
            catch
            {
                revisionNumber = sheet.LookupParameter("Current Revision")?.AsString() ?? "";
            }
        }

        private static void GetPaperInfo(Element titleBlock, out string paperSize, out string orientation)
        {
            paperSize = "A1";
            orientation = "Landscape";
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
                        orientation = wParam.AsDouble() >= hParam.AsDouble() ? "Landscape" : "Portrait";

                        if (Math.Abs(maxDim - 1189) < 50 && Math.Abs(minDim - 841) < 50) paperSize = "A0";
                        else if (Math.Abs(maxDim - 841) < 50 && Math.Abs(minDim - 594) < 50) paperSize = "A1";
                        else if (Math.Abs(maxDim - 594) < 50 && Math.Abs(minDim - 420) < 50) paperSize = "A2";
                        else if (Math.Abs(maxDim - 420) < 50 && Math.Abs(minDim - 297) < 50) paperSize = "A3";
                        else if (Math.Abs(maxDim - 297) < 50 && Math.Abs(minDim - 210) < 50) paperSize = "A4";
                        else paperSize = $"{Math.Round(maxDim)}x{Math.Round(minDim)}mm";
                    }
                }
            }
            catch { }
        }
    }
}

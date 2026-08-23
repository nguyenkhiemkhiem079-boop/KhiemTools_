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

            foreach (var sheet in sheets)
            {
                var item = new SheetExportItem
                {
                    Sheet = sheet,
                    SheetUniqueId = sheet.UniqueId,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    CurrentRevisionNumber = GetCurrentRevisionNumber(sheet, doc),
                    CurrentRevisionDate = GetCurrentRevisionDate(sheet, doc),
                    RevisionSequence = GetCurrentRevisionSequence(sheet, doc),
                    PaperSize = DetectPaperSize(sheet, doc),
                    Orientation = DetectOrientation(sheet, doc)
                };

                list.Add(item);
            }

            return list;
        }

        private static string GetCurrentRevisionNumber(ViewSheet sheet, Document doc)
        {
            try
            {
                var revIds = sheet.GetAllRevisionIds();
                if (revIds != null && revIds.Any())
                {
                    var lastRevId = revIds.Last();
                    if (doc.GetElement(lastRevId) is Revision rev)
                    {
                        return rev.RevisionNumber;
                    }
                }
                string currentRev = sheet.LookupParameter("Current Revision")?.AsString() ?? sheet.LookupParameter("Sheet Revision")?.AsString();
                return currentRev ?? "";
            }
            catch
            {
                return sheet.LookupParameter("Current Revision")?.AsString() ?? "";
            }
        }

        private static string GetCurrentRevisionDate(ViewSheet sheet, Document doc)
        {
            try
            {
                var revIds = sheet.GetAllRevisionIds();
                if (revIds != null && revIds.Any())
                {
                    var lastRevId = revIds.Last();
                    if (doc.GetElement(lastRevId) is Revision rev)
                    {
                        return rev.RevisionDate;
                    }
                }
                return sheet.LookupParameter("Current Revision Date")?.AsString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string GetCurrentRevisionSequence(ViewSheet sheet, Document doc)
        {
            try
            {
                var revIds = sheet.GetAllRevisionIds();
                return revIds?.Count.ToString() ?? "0";
            }
            catch
            {
                return "0";
            }
        }

        private static string DetectPaperSize(ViewSheet sheet, Document doc)
        {
            try
            {
                var titleBlock = new FilteredElementCollector(doc, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault();

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

        private static string DetectOrientation(ViewSheet sheet, Document doc)
        {
            try
            {
                var titleBlock = new FilteredElementCollector(doc, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault();

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

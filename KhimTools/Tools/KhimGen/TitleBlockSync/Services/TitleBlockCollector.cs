using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.TitleBlockSync.Models;

namespace KhimTools.TitleBlockSync.Services
{
    public sealed class TitleBlockInfo
    {
        public ViewSheet Sheet { get; set; }
        public FamilyInstance TitleBlock { get; set; }
        public FamilySymbol Symbol { get; set; }
        public string SheetNumber { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public XYZ Position { get; set; }
        public double Rotation { get; set; }
        public string RevisionFingerprint { get; set; } = string.Empty;
        public bool IsPlaceholder { get; set; }
    }

    public static class TitleBlockCollector
    {
        public static IList<ViewSheet> CollectSheets(Document doc)
        {
            var result = new List<ViewSheet>();
            if (doc == null) return result;
            foreach (ViewSheet sheet in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
                if (sheet != null && !sheet.IsPlaceholder) result.Add(sheet);
            return result.OrderBy(s => s.SheetNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Id.IntegerValue).ToList();
        }

        public static IList<FamilyInstance> CollectTitleBlocks(Document doc, ViewSheet sheet)
        {
            var result = new List<FamilyInstance>();
            if (doc == null || sheet == null) return result;
            foreach (FamilyInstance instance in new FilteredElementCollector(doc, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().Cast<FamilyInstance>())
                if (instance != null) result.Add(instance);
            return result.OrderBy(x => x.Id.IntegerValue).ToList();
        }

        public static TitleBlockInfo Analyze(Document doc, ViewSheet sheet, FamilyInstance block)
        {
            if (sheet == null || block == null) return null;
            FamilySymbol symbol = block.Symbol;
            var location = block.Location as LocationPoint;
            return new TitleBlockInfo
            {
                Sheet = sheet, TitleBlock = block, Symbol = symbol,
                SheetNumber = sheet.SheetNumber ?? string.Empty, SheetName = sheet.Name ?? string.Empty,
                FamilyName = symbol == null ? string.Empty : (symbol.FamilyName ?? string.Empty),
                TypeName = symbol == null ? string.Empty : (symbol.Name ?? string.Empty),
                Position = location == null ? null : location.Point,
                Rotation = location == null ? 0.0 : location.Rotation,
                RevisionFingerprint = RevisionFingerprint(sheet), IsPlaceholder = sheet.IsPlaceholder
            };
        }

        public static TitleBlockInfo FindExplicit(Document doc, ElementId sheetId, ElementId titleBlockId, out TitleBlockSyncStatusCode status)
        {
            status = TitleBlockSyncStatusCode.READY;
            ViewSheet sheet = doc == null ? null : doc.GetElement(sheetId) as ViewSheet;
            if (sheet == null || sheet.IsPlaceholder) { status = TitleBlockSyncStatusCode.SOURCE_SHEET_MISSING; return null; }
            var blocks = CollectTitleBlocks(doc, sheet);
            if (blocks.Count == 0) { status = TitleBlockSyncStatusCode.SOURCE_TITLEBLOCK_MISSING; return null; }
            FamilyInstance selected = null;
            if (titleBlockId != null && titleBlockId != ElementId.InvalidElementId)
                foreach (FamilyInstance candidate in blocks) if (candidate.Id == titleBlockId) { selected = candidate; break; }
            if (selected == null)
            {
                if (blocks.Count > 1) { status = TitleBlockSyncStatusCode.SOURCE_MULTIPLE_TITLEBLOCKS; return null; }
                selected = blocks[0];
            }
            return Analyze(doc, sheet, selected);
        }

        public static TitleBlockInfo FindActiveIfUnambiguous(Document doc)
        {
            ViewSheet active = doc == null ? null : doc.ActiveView as ViewSheet;
            if (active == null || active.IsPlaceholder) return null;
            var blocks = CollectTitleBlocks(doc, active);
            return blocks.Count == 1 ? Analyze(doc, active, blocks[0]) : null;
        }

        public static string RevisionFingerprint(ViewSheet sheet)
        {
            if (sheet == null) return string.Empty;
            var ids = new List<string>();
            foreach (ElementId id in sheet.GetAllRevisionIds()) ids.Add(id.IntegerValue.ToString());
            ids.Sort(StringComparer.Ordinal);
            return string.Join(",", ids);
        }

        public static string Fingerprint(TitleBlockInfo source, IEnumerable<TitleBlockInfo> targets, IEnumerable<KhimTools.ParameterTransfer.Models.ParameterTransferPlan> parameters)
        {
            var parts = new List<string>();
            if (source != null)
            {
                parts.Add(source.Sheet.UniqueId); parts.Add(source.SheetNumber); parts.Add(source.SheetName);
                parts.Add(source.TitleBlock.UniqueId); parts.Add(source.TitleBlock.GetTypeId().IntegerValue.ToString()); parts.Add(source.RevisionFingerprint);
            }
            if (targets != null)
                foreach (TitleBlockInfo target in targets.OrderBy(x => x.Sheet.UniqueId, StringComparer.Ordinal))
                    parts.Add(target.Sheet.UniqueId + ":" + target.TitleBlock.UniqueId + ":" + target.TitleBlock.GetTypeId().IntegerValue + ":" + target.RevisionFingerprint);
            if (parameters != null)
                foreach (KhimTools.ParameterTransfer.Models.ParameterTransferPlan p in parameters.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
                    parts.Add(p.Key + "=" + (p.SourceValue == null ? string.Empty : p.SourceValue.DisplayValue));
            return string.Join("|", parts);
        }

        public static bool IsProtectedSheetParameter(Parameter parameter)
        {
            if (parameter == null) return true;
            string name = parameter.Definition == null ? string.Empty : parameter.Definition.Name ?? string.Empty;
            if (name.IndexOf("sheet number", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("sheet name", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("revision", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            try { BuiltInParameter bip = (BuiltInParameter)parameter.Id.IntegerValue; return bip == BuiltInParameter.SHEET_NUMBER || bip == BuiltInParameter.SHEET_NAME; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[TitleBlockSync] sheet protection probe: " + ex.Message); return false; }
        }

        public static bool IsProtectedTitleBlockParameter(Parameter parameter)
        {
            if (parameter == null || parameter.IsReadOnly) return true;
            string name = parameter.Definition == null ? string.Empty : parameter.Definition.Name ?? string.Empty;
            string lower = name.ToLowerInvariant();
            return lower.Contains("family") || lower.Contains("type") || lower.Contains("width") || lower.Contains("height") || lower.Contains("rotation") || lower.Contains("location") || lower.Contains("revision") || lower.Contains("sheet number") || lower.Contains("sheet name") || lower.Contains("project information");
        }

        public static bool IsGlobalProjectParameter(Parameter parameter)
        {
            if (parameter == null || parameter.Definition == null) return false;
            string name = parameter.Definition.Name ?? string.Empty;
            return name.IndexOf("project information", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("project address", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

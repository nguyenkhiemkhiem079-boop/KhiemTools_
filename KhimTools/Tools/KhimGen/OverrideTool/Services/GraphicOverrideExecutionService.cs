using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core.Revit;

namespace KhimTools.OverrideTool.Services
{
    /// <summary>Owns model transactions for per-view graphic override commands.</summary>
    public static class GraphicOverrideExecutionService
    {
        public static int Reset(Document doc, View view, IList<ElementId> elementIds)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
            int updated = 0;
            using (var transaction = new Transaction(doc, "KhimTools: Reset Graphic Override"))
            {
                const string operation = "OverrideTool.Reset";
                TransactionBoundary.Start(transaction, operation);
                try
                {
                    var emptySettings = new OverrideGraphicSettings();
                    foreach (ElementId id in elementIds)
                    {
                        if (id == null || doc.GetElement(id) == null) continue;
                        view.SetElementOverrides(id, emptySettings);
                        updated++;
                    }
                    TransactionBoundary.Commit(transaction, operation);
                }
                catch
                {
                    TransactionBoundary.RollBack(transaction, operation);
                    throw;
                }
            }
            return updated;
        }

        public static int Apply(Document doc, View view, IList<ElementId> elementIds, Color color,
            bool surface, bool cut, bool lines, bool background, int lineWeight, int transparency, bool halftone)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
            if (color == null) throw new ArgumentNullException(nameof(color));

            FillPatternElement solidPattern = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>().FirstOrDefault(pattern => pattern.GetFillPattern().IsSolidFill);
            int updated = 0;
            using (var transaction = new Transaction(doc, "KhimTools: Graphic Overdrive"))
            {
                const string operation = "OverrideTool.Apply";
                TransactionBoundary.Start(transaction, operation);
                try
                {
                    foreach (ElementId id in elementIds)
                    {
                        if (id == null || doc.GetElement(id) == null) continue;
                        OverrideGraphicSettings settings = view.GetElementOverrides(id);
                        if (surface)
                        {
                            if (solidPattern != null) settings = settings.SetSurfaceForegroundPatternId(solidPattern.Id);
                            settings = settings.SetSurfaceForegroundPatternColor(color).SetSurfaceForegroundPatternVisible(true);
                            if (background) settings = settings.SetSurfaceBackgroundPatternColor(color);
                        }
                        if (cut)
                        {
                            if (solidPattern != null) settings = settings.SetCutForegroundPatternId(solidPattern.Id);
                            settings = settings.SetCutForegroundPatternColor(color).SetCutForegroundPatternVisible(true);
                            if (background) settings = settings.SetCutBackgroundPatternColor(color);
                        }
                        if (lines) settings = settings.SetProjectionLineColor(color).SetCutLineColor(color);
                        if (lineWeight > 0) settings = settings.SetProjectionLineWeight(lineWeight).SetCutLineWeight(lineWeight);
                        settings = settings.SetSurfaceTransparency(Math.Max(0, Math.Min(100, transparency))).SetHalftone(halftone);
                        view.SetElementOverrides(id, settings);
                        updated++;
                    }
                    TransactionBoundary.Commit(transaction, operation);
                }
                catch
                {
                    TransactionBoundary.RollBack(transaction, operation);
                    throw;
                }
            }
            return updated;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class QuickDimensionService
    {
        // Explicit strategies: Center, NEAR_FACE, FAR_FACE; AUTO remains AMBIGUOUS_REFERENCE_STRATEGY until user chooses.
        public static DimensionPlan BuildPlan(Document doc, View view, IList<ElementId> selectedIds, string referenceStrategy, DimensionOptions options)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.QUICK, Options = options ?? new DimensionOptions { ReferenceStrategy = referenceStrategy } };
            context.Options.ReferenceStrategy = referenceStrategy;
            if (string.IsNullOrWhiteSpace(referenceStrategy) || string.Equals(referenceStrategy, "AUTO", System.StringComparison.OrdinalIgnoreCase) || string.Equals(referenceStrategy, "EXPLICIT", System.StringComparison.OrdinalIgnoreCase)) { var ambiguous = DimensionPlanBuilder.CreateBasePlan(context); ambiguous.Status = DimensionStatus.AMBIGUOUS_REFERENCE_STRATEGY; ambiguous.Errors.Add("AMBIGUOUS_REFERENCE_STRATEGY: choose CENTER, NEAR_FACE, or FAR_FACE."); return ambiguous; }
            IList<Element> elements = (selectedIds == null ? Enumerable.Empty<Element>() : selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null)).ToList();
            DimensionReferenceRole role = string.Equals(referenceStrategy, "CENTER", System.StringComparison.OrdinalIgnoreCase) ? DimensionReferenceRole.CENTER : DimensionReferenceRole.GENERIC_FACE;
            foreach (Element element in elements)
            {
                IList<DimensionReferenceInfo> candidates = DimensionReferenceService.FromElements(doc, view, new[] { element }, role, context.Options.Axis);
                if (candidates.Count == 0) continue;
                DimensionReferenceInfo chosen = string.Equals(referenceStrategy, "FAR_FACE", System.StringComparison.OrdinalIgnoreCase) ? candidates.OrderByDescending(x => x.ProjectedPosition).First() : candidates.OrderBy(x => x.ProjectedPosition).First();
                context.References.Add(chosen);
            }
            return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}

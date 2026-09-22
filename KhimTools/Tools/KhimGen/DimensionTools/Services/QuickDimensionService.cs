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
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.QUICK, Options = options ?? new DimensionOptions { ReferenceStrategy = referenceStrategy } }; context.Options.ReferenceStrategy = referenceStrategy; if (string.IsNullOrWhiteSpace(referenceStrategy) || string.Equals(referenceStrategy, "AUTO", System.StringComparison.OrdinalIgnoreCase)) { var plan = new DimensionPlan { Context = context, ViewId = view == null ? ElementId.InvalidElementId : view.Id, Operation = DimensionOperation.QUICK, Status = DimensionStatus.AMBIGUOUS_REFERENCE_STRATEGY }; plan.Errors.Add("AMBIGUOUS_REFERENCE_STRATEGY: choose Center, Near Face, or Far Face."); return plan; } IEnumerable<Element> elements = selectedIds == null ? Enumerable.Empty<Element>() : selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null); context.References.AddRange(DimensionReferenceService.FromElements(doc, view, elements, DimensionReferenceRole.GENERIC_FACE, context.Options.Axis)); return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}

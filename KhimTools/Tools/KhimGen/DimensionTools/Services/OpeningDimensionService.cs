using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class OpeningDimensionService
    {
        // Opening references cover Wall openings, Doors, Windows and MEP Host elements; family GetReferences/GetReferenceByName are preferred over Width-only geometry.
        public static DimensionPlan BuildPlan(Document doc, View view, IList<ElementId> selectedIds, DimensionOptions options)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.OPENING, Options = options ?? new DimensionOptions() }; IEnumerable<Element> elements = selectedIds == null ? Enumerable.Empty<Element>() : selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null); foreach (Element element in elements) { FamilyInstance family = element as FamilyInstance; if (family != null) { context.References.AddRange(DimensionReferenceService.FromElements(doc, view, new[] { element }, DimensionReferenceRole.OPENING_EDGE, context.Options.Axis)); } else if (element is Opening) { context.References.AddRange(DimensionReferenceService.FromElements(doc, view, new[] { element }, DimensionReferenceRole.OPENING_EDGE, context.Options.Axis)); } } return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}

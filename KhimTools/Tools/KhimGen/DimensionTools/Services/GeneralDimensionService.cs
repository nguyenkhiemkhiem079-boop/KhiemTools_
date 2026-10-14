using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class GeneralDimensionService
    {
        // Axis choices: HORIZONTAL_IN_VIEW, VERTICAL_IN_VIEW or Pick Dimension Line; all references are validated before NewDimension.
        public static DimensionPlan BuildPlan(Document doc, View view, IList<DimensionReferenceInfo> references, Line dimensionLine, DimensionOptions options) { var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.GENERAL, DimensionLine = dimensionLine, Options = options ?? new DimensionOptions() }; if (references != null) foreach (DimensionReferenceInfo reference in references) context.References.Add(reference); return DimensionPlanBuilder.Build(context); }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}

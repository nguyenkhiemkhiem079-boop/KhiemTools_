using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;
namespace KhimTools.ModifyObjects.Core
{
    /// <summary>Detached plan values. Revit Document and XYZ instances stay in the planner/executor boundary.</summary>
    public sealed class ModifyObjectPlanContext
    {
        public string DocumentIdentityKey { get; set; }
        public ModifyObjectOperation Operation { get; set; }
        public IList<ElementId> ElementIds { get; } = new List<ElementId>();
        public ElementId PrimaryElementId { get; set; }
        public ElementId SecondaryElementId { get; set; }
        public ModifyObjectPointSnapshot SplitPoint { get; set; }
        public ModifyObjectPointSnapshot OpeningStart { get; set; }
        public ModifyObjectPointSnapshot OpeningEnd { get; set; }
        public ModifyObjectPointSnapshot MoveVector { get; set; }
        public int ArrayCount { get; set; }
        public ModifyObjectPointSnapshot ArrayVector { get; set; }
        public ElementId TargetLevelId { get; set; }
        public double TargetBaseOffset { get; set; }
        public bool IncludeOriginal { get; set; }
        public bool PreviewOnly { get; set; }
        public ModifyObjectConflictPolicy ConflictPolicy { get; set; }
        public static ModifyObjectPlanContext From(ModifyObjectContext source)
        {
            if (source == null) return null;
            var result = new ModifyObjectPlanContext { DocumentIdentityKey = DocumentIdentity.From(source.Document).StableKey, Operation = source.Operation, PrimaryElementId = source.PrimaryElementId, SecondaryElementId = source.SecondaryElementId, SplitPoint = ModifyObjectPointSnapshot.From(source.SplitPoint), OpeningStart = ModifyObjectPointSnapshot.From(source.OpeningStart), OpeningEnd = ModifyObjectPointSnapshot.From(source.OpeningEnd), MoveVector = ModifyObjectPointSnapshot.From(source.MoveVector), ArrayCount = source.ArrayCount, ArrayVector = ModifyObjectPointSnapshot.From(source.ArrayVector), TargetLevelId = source.TargetLevelId, TargetBaseOffset = source.TargetBaseOffset, IncludeOriginal = source.IncludeOriginal, PreviewOnly = source.PreviewOnly, ConflictPolicy = source.ConflictPolicy };
            foreach (ElementId id in source.ElementIds) result.ElementIds.Add(id);
            return result;
        }
    }
}

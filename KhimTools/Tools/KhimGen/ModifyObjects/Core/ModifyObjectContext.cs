using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.ModifyObjects.Core
{
    public sealed class ModifyObjectContext
    {
        public Document Document { get; set; }
        public ModifyObjectOperation Operation { get; set; }
        public IList<ElementId> ElementIds { get; private set; }
        public ElementId PrimaryElementId { get; set; }
        public ElementId SecondaryElementId { get; set; }
        public XYZ SplitPoint { get; set; }
        public XYZ OpeningStart { get; set; }
        public XYZ OpeningEnd { get; set; }
        public XYZ MoveVector { get; set; }
        public int ArrayCount { get; set; }
        public XYZ ArrayVector { get; set; }
        public ElementId TargetLevelId { get; set; }
        public double TargetBaseOffset { get; set; }
        public bool IncludeOriginal { get; set; }
        public bool PreviewOnly { get; set; }
        public ModifyObjectConflictPolicy ConflictPolicy { get; set; }
        public ModifyObjectContext() { ElementIds = new List<ElementId>(); ArrayCount = 1; IncludeOriginal = true; ConflictPolicy = ModifyObjectConflictPolicy.BLOCK; }
    }
    public enum ModifyObjectConflictPolicy { KEEP_LOWER, KEEP_UPPER, MANUAL, BLOCK }
}

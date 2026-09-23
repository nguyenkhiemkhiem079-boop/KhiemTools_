using System;
using Autodesk.Revit.DB;

namespace KhimTools.ModifyObjects.Core
{
    /// <summary>Immutable-enough source identity captured during ANALYZE and checked before EXECUTE.</summary>
    public sealed class ModifyObjectSourceSnapshot
    {
        public ElementId ElementId { get; set; }
        public string UniqueId { get; set; }
        public ElementId CategoryId { get; set; }
        public ElementId TypeId { get; set; }
        public ElementId LevelId { get; set; }
        public double BaseOffset { get; set; }
        public double TopOffset { get; set; }
        public string GeometryFingerprint { get; set; }
        public string ParameterFingerprint { get; set; }
        public ElementId GroupId { get; set; }
        public ElementId DesignOptionId { get; set; }
        public bool Pinned { get; set; }
        public string HostRelation { get; set; }
        public DateTime CapturedAt { get; set; }
    }
}

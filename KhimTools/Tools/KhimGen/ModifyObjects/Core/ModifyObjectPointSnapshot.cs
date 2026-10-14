using Autodesk.Revit.DB;
namespace KhimTools.ModifyObjects.Core
{
    public sealed class ModifyObjectPointSnapshot
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public static ModifyObjectPointSnapshot From(XYZ point) { return point == null ? null : new ModifyObjectPointSnapshot { X = point.X, Y = point.Y, Z = point.Z }; }
        public XYZ ToXyz() { return new XYZ(X, Y, Z); }
    }
}

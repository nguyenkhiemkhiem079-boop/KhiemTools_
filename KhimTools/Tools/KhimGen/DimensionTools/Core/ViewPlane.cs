using Autodesk.Revit.DB;

namespace KhimTools.DimensionTools.Core
{
    public sealed class ViewPlane
    {
        public XYZ Origin { get; private set; }
        public XYZ RightDirection { get; private set; }
        public XYZ UpDirection { get; private set; }
        public XYZ ViewDirection { get; private set; }
        private ViewPlane() { }
        internal static ViewPlane Create(XYZ origin, XYZ right, XYZ up, XYZ viewDirection)
        {
            if (origin == null || right == null || up == null || viewDirection == null || right.IsZeroLength() || up.IsZeroLength() || viewDirection.IsZeroLength()) return null;
            return new ViewPlane { Origin = origin, RightDirection = right.Normalize(), UpDirection = up.Normalize(), ViewDirection = viewDirection.Normalize() };
        }
        public static ViewPlane FromView(View view)
        {
            return view == null ? null : Create(view.Origin, view.RightDirection, view.UpDirection, view.ViewDirection);
        }
        public XYZ ToView(XYZ world) { XYZ delta = world - Origin; return new XYZ(delta.DotProduct(RightDirection), delta.DotProduct(UpDirection), delta.DotProduct(ViewDirection)); }
        public XYZ FromView(XYZ local) { return Origin + RightDirection * local.X + UpDirection * local.Y + ViewDirection * local.Z; }
        public XYZ Project(XYZ world) { XYZ local = ToView(world); return FromView(new XYZ(local.X, local.Y, 0)); }
        public double ProjectAlong(XYZ world, XYZ worldAxis) { return world == null || worldAxis == null || worldAxis.IsZeroLength() ? 0 : (world - Origin).DotProduct(worldAxis.Normalize()); }
        public static bool IsSupportedView(View view) { if (view == null || view.IsTemplate) return false; if (view is ViewSheet) return false; return view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan || view.ViewType == ViewType.Section || view.ViewType == ViewType.Elevation || view.ViewType == ViewType.Detail || view.ViewType == ViewType.DraftingView; }
    }
}

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
        public static ViewPlane FromView(View view)
        {
            return view == null ? null : new ViewPlane { Origin = view.Origin, RightDirection = view.RightDirection.Normalize(), UpDirection = view.UpDirection.Normalize(), ViewDirection = view.ViewDirection.Normalize() };
        }
        public XYZ ToView(XYZ world) { XYZ delta = world - Origin; return new XYZ(delta.DotProduct(RightDirection), delta.DotProduct(UpDirection), delta.DotProduct(ViewDirection)); }
        public XYZ FromView(XYZ local) { return Origin + RightDirection * local.X + UpDirection * local.Y + ViewDirection * local.Z; }
        public XYZ Project(XYZ world) { XYZ local = ToView(world); return FromView(new XYZ(local.X, local.Y, 0)); }
        public double ProjectAlong(XYZ world, XYZ axis) { XYZ local = ToView(world); return local.DotProduct(axis); }
        public static bool IsSupportedView(View view) { if (view == null || view.IsTemplate) return false; if (view is ViewSheet) return false; return view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan || view.ViewType == ViewType.Section || view.ViewType == ViewType.Elevation || view.ViewType == ViewType.Detail || view.ViewType == ViewType.DraftingView; }
    }
}

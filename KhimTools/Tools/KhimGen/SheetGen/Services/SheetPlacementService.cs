using Autodesk.Revit.DB;
using KhimTools.SheetGen.Models;

namespace KhimTools.SheetGen.Services
{
    public enum SheetPlacementAnchor
    {
        DefaultCenter,
        Center,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>Computes a safe placement point from the sheet outline and margin; it never relies on a project-specific hardcoded point.</summary>
    public static class SheetPlacementService
    {
        public static XYZ GetPlacementPoint(ViewSheet sheet, SheetPlacementAnchor anchor = SheetPlacementAnchor.DefaultCenter, double marginMillimeters = 25.0)
        {
            if (sheet == null) return XYZ.Zero;
            BoundingBoxUV outline = sheet.Outline;
            if (outline == null) return XYZ.Zero;

            double margin = UnitUtils.ConvertToInternalUnits(marginMillimeters, UnitTypeId.Millimeters);
            double minU = outline.Min.U + margin;
            double maxU = outline.Max.U - margin;
            double minV = outline.Min.V + margin;
            double maxV = outline.Max.V - margin;
            if (minU > maxU) { minU = outline.Min.U; maxU = outline.Max.U; }
            if (minV > maxV) { minV = outline.Min.V; maxV = outline.Max.V; }

            double u = (minU + maxU) / 2.0;
            double v = (minV + maxV) / 2.0;
            switch (anchor)
            {
                case SheetPlacementAnchor.TopLeft: u = minU; v = maxV; break;
                case SheetPlacementAnchor.TopRight: u = maxU; v = maxV; break;
                case SheetPlacementAnchor.BottomLeft: u = minU; v = minV; break;
                case SheetPlacementAnchor.BottomRight: u = maxU; v = minV; break;
            }
            return new XYZ(u, v, 0.0);
        }
    }
}

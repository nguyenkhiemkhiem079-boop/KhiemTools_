using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Explicit creation paths for rectangular column ties. Production outer and
    /// inner ties are created from a compatible loaded StirrupTie RebarShape.
    /// </summary>
    public static class RectangularStirrupHelper
    {
        public static Rebar CreateHoop(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double halfB, double halfH, double rotationRad, XYZ normal)
        {
            XYZ localX;
            XYZ localY;
            GetColumnAxes(hostColumn as FamilyInstance, rotationRad, out localX, out localY);
            XYZ lowerLeft = RectangularColumnGeometryHelper.TransformLocalToWorld(
                hostColumn as FamilyInstance, -halfB, -halfH, 0, center, rotationRad);
            return CreateClosedTieWithResolvedHooks(doc, hostColumn, barType, lowerLeft,
                localX, localY, halfB * 2.0, halfH * 2.0, "OuterTie");
        }

        /// <summary>
        /// Creates one internal closed rectangular cell. The two X boundaries are
        /// supplied by the longitudinal-bar layout, never by a hardcoded half ratio.
        /// </summary>
        public static Rebar CreateInnerClosedTie(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double leftX, double rightX, double halfH, double rotationRad, XYZ normal,
            string role = "InnerTie")
        {
            if (rightX <= leftX + 0.01) return null;
            XYZ localX;
            XYZ localY;
            GetColumnAxes(hostColumn as FamilyInstance, rotationRad, out localX, out localY);
            XYZ lowerLeft = RectangularColumnGeometryHelper.TransformLocalToWorld(
                hostColumn as FamilyInstance, leftX, -halfH, 0, center, rotationRad);
            return CreateClosedTieWithResolvedHooks(doc, hostColumn, barType, lowerLeft,
                localX, localY, rightX - leftX, halfH * 2.0, role);
        }

        /// <summary>Legacy diamond option, still shape-driven and never a four-line loop.</summary>
        public static Rebar CreateDiamondHoop(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double halfB, double halfH, double rotationRad, XYZ normal)
        {
            XYZ localX;
            XYZ localY;
            GetColumnAxes(hostColumn as FamilyInstance, rotationRad, out localX, out localY);
            XYZ lowerLeft = RectangularColumnGeometryHelper.TransformLocalToWorld(
                hostColumn as FamilyInstance, -halfB, -halfH, 0, center, rotationRad);
            return CreatePlanarLegacyWithResolvedHooks(doc, RebarShapeConfig.DiamondStirrup,
                hostColumn, barType, lowerLeft, localX, localY, halfB * 2.0, halfH * 2.0,
                "DiamondLegacy");
        }

        /// <summary>
        /// Creates an open cross tie with explicit StirrupTie hooks. It is only used
        /// by the opt-in legacy CrossTie layout.
        /// </summary>
        public static Rebar CreateCrossLink(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double lx1, double ly1, double lx2, double ly2, double rotationRad, XYZ normal)
        {
            FamilyInstance column = hostColumn as FamilyInstance;
            XYZ c1 = RectangularColumnGeometryHelper.TransformLocalToWorld(column, lx1, ly1, 0, center, rotationRad);
            XYZ c2 = RectangularColumnGeometryHelper.TransformLocalToWorld(column, lx2, ly2, 0, center, rotationRad);
            if (c1.DistanceTo(c2) < 0.01) return null;

            RebarHookType hook = RebarHookHelper.ResolveExactHookType(doc, 135, RebarStyle.StirrupTie)
                ?? RebarHookHelper.ResolveExactHookType(doc, 90, RebarStyle.StirrupTie);
            if (hook == null) return null;
            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, hook, hook, hostColumn,
                normal ?? XYZ.BasisZ, new List<Curve> { Line.CreateBound(c1, c2) },
                RebarHookOrientation.Left, RebarHookOrientation.Right, false);
        }

        private static Rebar CreateClosedTieWithResolvedHooks(Document doc, Element host, RebarBarType barType,
            XYZ lowerLeft, XYZ localX, XYZ localY, double width, double height, string role)
        {
            // Prefer the project detailing standard's 135° hook, then an exact 90°
            // hook when that is the only compatible type loaded. Neither path removes
            // hooks or silently changes the rebar style.
            foreach (double angle in new[] { 135.0, 90.0 })
            {
                RebarHookType hook = RebarHookHelper.ResolveExactHookType(doc, angle, RebarStyle.StirrupTie);
                if (hook == null) continue;
                Rebar rebar = RebarShapeCreationHelper.CreateRectangularTieFromShape(
                    doc, host, barType, lowerLeft, localX, localY, width, height,
                    hook, hook, RebarHookOrientation.Left, RebarHookOrientation.Right, role);
                if (rebar != null) return rebar;
            }
            return null;
        }

        private static Rebar CreatePlanarLegacyWithResolvedHooks(Document doc, string shapeName,
            Element host, RebarBarType barType, XYZ lowerLeft, XYZ localX, XYZ localY,
            double width, double height, string role)
        {
            foreach (double angle in new[] { 135.0, 90.0 })
            {
                RebarHookType hook = RebarHookHelper.ResolveExactHookType(doc, angle, RebarStyle.StirrupTie);
                if (hook == null) continue;
                Rebar rebar = RebarShapeCreationHelper.CreatePlanarTieFromShape(
                    doc, shapeName, host, barType, lowerLeft, localX, localY, width, height,
                    hook, hook, RebarHookOrientation.Left, RebarHookOrientation.Right, role);
                if (rebar != null) return rebar;
            }
            return null;
        }

        private static void GetColumnAxes(FamilyInstance column, double rotationRad, out XYZ localX, out XYZ localY)
        {
            localX = null;
            localY = null;
            try
            {
                Transform transform = column?.GetTransform();
                localX = transform?.BasisX.Normalize();
                localY = transform?.BasisY.Normalize();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[K-TOOLS][Rebar] Could not read column transform: " + ex.Message);
            }

            if (localX == null || localY == null || Math.Abs(localX.DotProduct(localY)) > 1e-5)
            {
                localX = new XYZ(Math.Cos(rotationRad), Math.Sin(rotationRad), 0).Normalize();
                localY = new XYZ(-Math.Sin(rotationRad), Math.Cos(rotationRad), 0).Normalize();
            }
        }
    }
}

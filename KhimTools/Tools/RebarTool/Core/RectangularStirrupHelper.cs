using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Tß║ío ─æai chß╗» nhß║¡t (Outer Hoop), ─æai thoi (Diamond Hoop) v├á m├│c ─æai phß╗Ñ/crosslink
    /// chuß║⌐n 100% nß║▒m gß╗ìn b├¬n trong cß╗Öt vu├┤ng/chß╗» nhß║¡t (xoay/lß║¡t theo g├│c cß╗Öt).
    /// </summary>
    public static class RectangularStirrupHelper
    {
        /// <summary>Tß║ío ─æai ngo├ái vu├┤ng/chß╗» nhß║¡t k├¡n chuß║⌐n.</summary>
        public static Rebar CreateHoop(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double halfB, double halfH, double rotationRad, XYZ normal)
        {
            var famCol = hostColumn as FamilyInstance;

            XYZ p1 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, halfB, halfH, 0, center, rotationRad);
            XYZ p2 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, -halfB, halfH, 0, center, rotationRad);
            XYZ p3 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, -halfB, -halfH, 0, center, rotationRad);
            XYZ p4 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, halfB, -halfH, 0, center, rotationRad);

            var loop = new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };

            XYZ calcNormal = normal ?? XYZ.BasisZ;

            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, null, null, hostColumn,
                calcNormal, loop, RebarHookOrientation.Right, RebarHookOrientation.Right);
        }

        /// <summary>Tß║ío ─æai thoi / ─æai lß╗ông nß╗æi c├íc thanh giß╗»a cß╗ºa 4 cß║ính.</summary>
        public static Rebar CreateDiamondHoop(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double halfB, double halfH, double rotationRad, XYZ normal)
        {
            var famCol = hostColumn as FamilyInstance;

            XYZ d1 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, 0, halfH, 0, center, rotationRad);
            XYZ d2 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, halfB, 0, 0, center, rotationRad);
            XYZ d3 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, 0, -halfH, 0, center, rotationRad);
            XYZ d4 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, -halfB, 0, 0, center, rotationRad);

            var loop = new List<Curve>
            {
                Line.CreateBound(d1, d2),
                Line.CreateBound(d2, d3),
                Line.CreateBound(d3, d4),
                Line.CreateBound(d4, d1)
            };

            XYZ calcNormal = normal ?? XYZ.BasisZ;

            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, null, null, hostColumn,
                calcNormal, loop, RebarHookOrientation.Right, RebarHookOrientation.Right);
        }

        /// <summary>
        /// Tß║ío Th├⌐p C-link / Crosstie nß╗æi 2 thanh th├⌐p chß╗º ─æß╗æi diß╗çn.
        /// </summary>
        public static Rebar CreateCrossLink(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double lx1, double ly1, double lx2, double ly2, double rotationRad, XYZ normal)
        {
            var famCol = hostColumn as FamilyInstance;

            XYZ c1 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, lx1, ly1, 0, center, rotationRad);
            XYZ c2 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, lx2, ly2, 0, center, rotationRad);

            if (c1.DistanceTo(c2) < 0.01) return null;

            var loop = new List<Curve> { Line.CreateBound(c1, c2) };
            RebarHookType hook135 = RebarHookHelper.GetHookType(doc, 135, RebarStyle.StirrupTie);
            RebarHookType hook90 = RebarHookHelper.GetHookType(doc, 90, RebarStyle.StirrupTie) ?? hook135;

            XYZ calcNormal = normal ?? XYZ.BasisZ;
            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, hook135, hook90, hostColumn,
                calcNormal, loop, RebarHookOrientation.Left, RebarHookOrientation.Right);
        }
    }
}

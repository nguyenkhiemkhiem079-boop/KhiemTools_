using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Tạo đai chữ nhật (Outer Hoop), đai thoi (Diamond Hoop) và móc đai phụ/crosslink
    /// chuẩn 100% nằm gọn bên trong cột vuông/chữ nhật (xoay/lật theo góc cột).
    /// </summary>
    public static class RectangularStirrupHelper
    {
        /// <summary>Tạo đai ngoài vuông/chữ nhật kín.</summary>
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

            Rebar rebar = null;
            try
            {
                rebar = Rebar.CreateFromCurves(
                    doc, RebarStyle.StirrupTie, barType, null, null, hostColumn,
                    normal, loop, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RectangularStirrupHelper] CreateHoop failed: {ex.Message}");
            }

            return rebar;
        }

        /// <summary>Tạo đai thoi / đai lồng nối các thanh giữa của 4 cạnh.</summary>
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

            Rebar rebar = null;
            try
            {
                rebar = Rebar.CreateFromCurves(
                    doc, RebarStyle.StirrupTie, barType, null, null, hostColumn,
                    normal, loop, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RectangularStirrupHelper] CreateDiamondHoop failed: {ex.Message}");
            }

            return rebar;
        }

        /// <summary>
        /// Tạo Thép C-link / Crosstie nối 2 thanh thép chủ đối diện với 2 đầu uốn móc 180° (Hook 180).
        /// </summary>
        public static Rebar CreateCrossLink(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double lx1, double ly1, double lx2, double ly2, double rotationRad, XYZ normal)
        {
            var famCol = hostColumn as FamilyInstance;

            XYZ c1 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, lx1, ly1, 0, center, rotationRad);
            XYZ c2 = RectangularColumnGeometryHelper.TransformLocalToWorld(famCol, lx2, ly2, 0, center, rotationRad);

            if (c1.DistanceTo(c2) < 0.01) return null;

            var loop = new List<Curve> { Line.CreateBound(c1, c2) };
            RebarHookType hook180 = RebarHookHelper.GetHookType(doc, 180, RebarStyle.StirrupTie);

            Rebar rebar = null;
            try
            {
                rebar = Rebar.CreateFromCurves(
                    doc, RebarStyle.StirrupTie, barType, hook180, hook180, hostColumn,
                    normal, loop, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            }
            catch
            {
                try
                {
                    rebar = Rebar.CreateFromCurves(
                        doc, RebarStyle.StirrupTie, barType, null, null, hostColumn,
                        normal, loop, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RectangularStirrupHelper] CreateCrossLink failed: {ex.Message}");
                }
            }

            return rebar;
        }
    }
}

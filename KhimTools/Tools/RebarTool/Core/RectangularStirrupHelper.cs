using System;
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

            XYZ v1 = (p2 - p1).Normalize();
            XYZ v2 = (p3 - p2).Normalize();

            // Mở hở 1 khoảng nhỏ (~6mm) để Revit không tính là Closed Loop hoàn toàn,
            // từ đó cho phép gắn Hook 135° và khớp đúng shape JP_T51.
            XYZ p1_gap = p1 + v1 * 0.02; 
            
            var loop = new List<Curve>
            {
                Line.CreateBound(p1_gap, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };

            XYZ calcNormal = v1.CrossProduct(v2).Normalize();
            if (calcNormal.GetLength() < 0.01) calcNormal = normal ?? XYZ.BasisZ;

            RebarHookType hook135 = RebarHookHelper.GetHookType(doc, 135, RebarStyle.StirrupTie);

            Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, hook135, hook135, hostColumn,
                calcNormal, loop, RebarHookOrientation.Left, RebarHookOrientation.Left);

            if (bar != null)
            {
                RebarShapeLibrary.ApplyShapeParameters(bar, new Dictionary<string, double>
                {
                    { "A", 2 * halfH },
                    { "B", 2 * halfB },
                    { "VNDC_L1", 2 * halfH },
                    { "VNDC_L2", 2 * halfB }
                });
            }

            return bar;
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

            XYZ v1 = (d2 - d1).Normalize();
            XYZ v2 = (d3 - d2).Normalize();
            
            XYZ d1_gap = d1 + v1 * 0.02; // ~6mm gap

            var loop = new List<Curve>
            {
                Line.CreateBound(d1_gap, d2),
                Line.CreateBound(d2, d3),
                Line.CreateBound(d3, d4),
                Line.CreateBound(d4, d1)
            };

            XYZ calcNormal = v1.CrossProduct(v2).Normalize();
            if (calcNormal.GetLength() < 0.01) calcNormal = normal ?? XYZ.BasisZ;

            RebarHookType hook135 = RebarHookHelper.GetHookType(doc, 135, RebarStyle.StirrupTie);

            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, hook135, hook135, hostColumn,
                calcNormal, loop, RebarHookOrientation.Left, RebarHookOrientation.Left);
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

            XYZ calcNormal = normal ?? XYZ.BasisZ;
            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, hook180, hook180, hostColumn,
                calcNormal, loop, RebarHookOrientation.Left, RebarHookOrientation.Right);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public class SupportedColumnInfo
    {
        public FamilyInstance Column { get; set; }
        public bool IsDetected { get; set; }
        public double SizeXFeet { get; set; }
        public double SizeYFeet { get; set; }
        public XYZ Center { get; set; }
        public double RotationRad { get; set; }
        public bool IsCircular { get; set; }
        public double DiameterFeet { get; set; }
    }

    /// <summary>
    /// Profile phân tích hình học cho đối tượng Móng (Structural Foundation / Footing / Pile Cap).
    /// </summary>
    public class FoundationProfile
    {
        public FamilyInstance FoundationElement { get; set; }
        public BoundingBoxXYZ BoundingBox { get; set; }
        public XYZ Center { get; set; }
        public double LengthFeet { get; set; }
        public double WidthFeet { get; set; }
        public double ThicknessFeet { get; set; }

        public double CoverBottomFeet { get; set; } = 50.0 / 304.8;
        public double CoverTopFeet { get; set; } = 50.0 / 304.8;
        public double CoverSideFeet { get; set; } = 50.0 / 304.8;

        public SupportedColumnInfo SupportedColumn { get; set; }
    }

    /// <summary>
    /// Helper trích xuất số liệu hình học 3D của móng đơn, móng băng, đài móng
    /// và tự động nhận diện cột kết cấu đặt trên móng để tính toán thép chờ chuẩn xác 100%.
    /// </summary>
    public static class FoundationGeometryHelper
    {
        public static FoundationProfile AnalyzeFoundation(Document doc, FamilyInstance foundation)
        {
            if (doc == null || foundation == null) return null;

            var profile = new FoundationProfile
            {
                FoundationElement = foundation
            };

            BoundingBoxXYZ bb = foundation.get_BoundingBox(null);
            if (bb == null) return null;

            profile.BoundingBox = bb;
            profile.LengthFeet = Math.Abs(bb.Max.X - bb.Min.X);
            profile.WidthFeet = Math.Abs(bb.Max.Y - bb.Min.Y);
            profile.ThicknessFeet = Math.Abs(bb.Max.Z - bb.Min.Z);
            profile.Center = (bb.Min + bb.Max) / 2.0;

            // Lấy Lớp bảo vệ (Cover)
            double botCover = RebarCoverHelper.GetColumnCover(foundation, RebarFace.Bottom);
            double topCover = RebarCoverHelper.GetColumnCover(foundation, RebarFace.Top);
            double sideCover = RebarCoverHelper.GetColumnCover(foundation, RebarFace.Exterior);

            if (botCover > 0) profile.CoverBottomFeet = botCover;
            if (topCover > 0) profile.CoverTopFeet = topCover;
            if (sideCover > 0) profile.CoverSideFeet = sideCover;

            // Tự động nhận diện cột kết cấu đặt trên mặt móng
            profile.SupportedColumn = FindSupportedColumn(doc, foundation, bb);

            return profile;
        }

        public static SupportedColumnInfo FindSupportedColumn(Document doc, FamilyInstance foundation, BoundingBoxXYZ bbFdn)
        {
            var info = new SupportedColumnInfo { IsDetected = false };
            if (doc == null || foundation == null || bbFdn == null) return info;

            try
            {
                // Tìm các phần tử Structural Column nằm ngay trên đỉnh móng (Z trong khoảng [Max.Z - 0.5ft, Max.Z + 2.0ft])
                XYZ minSearch = new XYZ(bbFdn.Min.X - 0.5, bbFdn.Min.Y - 0.5, bbFdn.Max.Z - 0.5);
                XYZ maxSearch = new XYZ(bbFdn.Max.X + 0.5, bbFdn.Max.Y + 0.5, bbFdn.Max.Z + 2.5);
                var outline = new Outline(minSearch, maxSearch);
                var filter = new BoundingBoxIntersectsFilter(outline);

                var candidates = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .WherePasses(filter)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                if (!candidates.Any()) return info;

                // Chọn cột gần tâm móng nhất
                XYZ fdnCenter = (bbFdn.Min + bbFdn.Max) / 2.0;
                FamilyInstance col = candidates
                    .OrderBy(c =>
                    {
                        var bb = c.get_BoundingBox(null);
                        if (bb == null) return double.MaxValue;
                        XYZ mid = (bb.Min + bb.Max) / 2.0;
                        return (mid.X - fdnCenter.X) * (mid.X - fdnCenter.X) + (mid.Y - fdnCenter.Y) * (mid.Y - fdnCenter.Y);
                    })
                    .FirstOrDefault();

                if (col == null) return info;

                info.Column = col;
                info.IsDetected = true;

                // Kiểm tra loại cột tròn hay chữ nhật
                bool isCirc = Commands.CmdColumnRebar.IsCircular(col);
                info.IsCircular = isCirc;

                if (isCirc)
                {
                    var circProfile = CircularColumnGeometryHelper.GetCircularProfile(col);
                    info.DiameterFeet = circProfile.Diameter;
                    info.SizeXFeet = circProfile.Diameter;
                    info.SizeYFeet = circProfile.Diameter;
                    info.Center = circProfile.BaseCenter;
                    info.RotationRad = 0;
                }
                else
                {
                    var rectProfile = RectangularColumnGeometryHelper.GetRectangularProfile(col);
                    info.SizeXFeet = rectProfile.B;
                    info.SizeYFeet = rectProfile.H;
                    info.Center = rectProfile.BaseCenter;
                    info.RotationRad = rectProfile.RotationRad;
                }
            }
            catch { }

            return info;
        }

        public static XYZ TransformLocalToWorld(XYZ origin, double rotationRad, double lx, double ly, double z)
        {
            double cos = Math.Cos(rotationRad);
            double sin = Math.Sin(rotationRad);
            double wx = origin.X + lx * cos - ly * sin;
            double wy = origin.Y + lx * sin + ly * cos;
            return new XYZ(wx, wy, z);
        }
    }
}


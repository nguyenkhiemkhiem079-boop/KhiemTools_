using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
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
    }

    /// <summary>
    /// Helper trích xuất số liệu hình học 3D của móng đơn, móng băng, đài móng.
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

            return profile;
        }
    }
}

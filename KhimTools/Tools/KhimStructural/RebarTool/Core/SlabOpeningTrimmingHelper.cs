using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public class SlabOpeningTrimmingResult
    {
        public bool HasOpening { get; set; }
        public BoundingBoxXYZ OpeningBox { get; set; }
        public int TrimmingBarCount { get; set; }
        public List<Curve> TrimmingCurves { get; set; } = new List<Curve>();
        public List<Curve> DiagonalCurves { get; set; } = new List<Curve>();
        public double RequiredAnchorageLengthMm { get; set; }
        public string DiagnosticMessage { get; set; } = "";
    }

    /// <summary>
    /// Bộ gia cường lỗ mở bản sàn (Section 23 Slab Opening Reinforcement / Sheet KC-01):
    /// Tạo thép bo mép lỗ mở (Trimming U-Bars / Straight Trimmers) và thép gia cường góc chéo (Diagonal Crack Control Bars)
    /// với chiều dài neo chuẩn vào bê tông sàn xung quanh.
    /// </summary>
    public static class SlabOpeningTrimmingHelper
    {
        /// <summary>
        /// Tính toán các đường cong thép gia cường xung quanh lỗ mở hình chữ nhật trên sàn
        /// </summary>
        public static SlabOpeningTrimmingResult CalculateTrimmingBars(
            BoundingBoxXYZ openingBox,
            double slabThicknessMm,
            double coverMm,
            double barDiaMm,
            double anchorageLengthMm = 600.0)
        {
            var res = new SlabOpeningTrimmingResult
            {
                OpeningBox = openingBox,
                RequiredAnchorageLengthMm = anchorageLengthMm
            };

            if (openingBox == null) return res;

            res.HasOpening = true;
            double anchFt = UnitUtils.ConvertToInternalUnits(anchorageLengthMm, UnitTypeId.Millimeters);
            double coverFt = UnitUtils.ConvertToInternalUnits(coverMm, UnitTypeId.Millimeters);

            double xMin = openingBox.Min.X;
            double xMax = openingBox.Max.X;
            double yMin = openingBox.Min.Y;
            double yMax = openingBox.Max.Y;
            double zMid = (openingBox.Min.Z + openingBox.Max.Z) * 0.5;

            // 1. Thép bo dọc theo 4 cạnh (Trimming Edge Bars): Vươn qua mỗi góc một đoạn neo Ld
            // Cạnh dưới (Y = yMin - cover)
            var b1 = Line.CreateBound(new XYZ(xMin - anchFt, yMin - coverFt, zMid), new XYZ(xMax + anchFt, yMin - coverFt, zMid));
            // Cạnh trên (Y = yMax + cover)
            var b2 = Line.CreateBound(new XYZ(xMin - anchFt, yMax + coverFt, zMid), new XYZ(xMax + anchFt, yMax + coverFt, zMid));
            // Cạnh trái (X = xMin - cover)
            var b3 = Line.CreateBound(new XYZ(xMin - coverFt, yMin - anchFt, zMid), new XYZ(xMin - coverFt, yMax + anchFt, zMid));
            // Cạnh phải (X = xMax + cover)
            var b4 = Line.CreateBound(new XYZ(xMax + coverFt, yMin - anchFt, zMid), new XYZ(xMax + coverFt, yMax + anchFt, zMid));

            res.TrimmingCurves.Add(b1);
            res.TrimmingCurves.Add(b2);
            res.TrimmingCurves.Add(b3);
            res.TrimmingCurves.Add(b4);

            // 2. Thép chéo chống nứt 4 góc (Diagonal 45° Bars @ 2 cây mỗi góc)
            double diagHalfFt = anchFt * 0.707;
            // Góc Tây Nam (xMin, yMin)
            res.DiagonalCurves.Add(Line.CreateBound(new XYZ(xMin - diagHalfFt, yMin + diagHalfFt, zMid), new XYZ(xMin + diagHalfFt, yMin - diagHalfFt, zMid)));
            // Góc Tây Bắc (xMin, yMax)
            res.DiagonalCurves.Add(Line.CreateBound(new XYZ(xMin - diagHalfFt, yMax - diagHalfFt, zMid), new XYZ(xMin + diagHalfFt, yMax + diagHalfFt, zMid)));
            // Góc Đông Nam (xMax, yMin)
            res.DiagonalCurves.Add(Line.CreateBound(new XYZ(xMax - diagHalfFt, yMin - diagHalfFt, zMid), new XYZ(xMax + diagHalfFt, yMin + diagHalfFt, zMid)));
            // Góc Đông Bắc (xMax, yMax)
            res.DiagonalCurves.Add(Line.CreateBound(new XYZ(xMax - diagHalfFt, yMax + diagHalfFt, zMid), new XYZ(xMax + diagHalfFt, yMax - diagHalfFt, zMid)));

            res.TrimmingBarCount = res.TrimmingCurves.Count + res.DiagonalCurves.Count;
            res.DiagnosticMessage = $"Opening {res.TrimmingCurves.Count} edge trimmers + {res.DiagonalCurves.Count} diagonal crack bars generated with {anchorageLengthMm:F0}mm anchorage.";
            return res;
        }
    }
}

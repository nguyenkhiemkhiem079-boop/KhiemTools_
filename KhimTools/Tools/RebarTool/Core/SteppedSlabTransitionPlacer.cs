using System;
using System.Collections.Generic;

namespace KhimTools.RebarTool.Core
{
    public enum SteppedTransitionType
    {
        LowStep,   // Bậc thấp: uốn bẻ cong thanh chính liên tục (cranked bar)
        HighStep   // Bậc cao: xử lý như vách/dầm mini (neo Ld riêng từng vùng + thép đai/U-bar)
    }

    /// <summary>
    /// DTO chứa thông số đầu vào cho bài toán chuyển tiếp sàn giật cấp (thuần số liệu hình học mm).
    /// </summary>
    public class SteppedSlabInput
    {
        public double Slab1ThicknessMm { get; set; } = 150.0;
        public double Slab2ThicknessMm { get; set; } = 120.0;
        public double Slab1TopElevationMm { get; set; } = 0.0;
        public double Slab2TopElevationMm { get; set; } = -30.0;

        public double CoverTop1Mm { get; set; } = 15.0;
        public double CoverTop2Mm { get; set; } = 15.0;
        public double CoverBot1Mm { get; set; } = 15.0;
        public double CoverBot2Mm { get; set; } = 15.0;

        public double BarDiameterMm { get; set; } = 10.0;
        public double CrankRatio { get; set; } = 6.0; // Tỷ lệ góc uốn (VD 6:1)
        public double MaxHorizontalTransitionLimitMm { get; set; } = 600.0;
    }

    /// <summary>
    /// Point2D đại diện cho tọa độ chuyển tiếp (X = vị trí ngang, Z = cao độ đứng).
    /// </summary>
    public struct Point2D
    {
        public double X { get; set; }
        public double Z { get; set; }

        public Point2D(double x, double z)
        {
            X = x;
            Z = z;
        }

        public override string ToString() => $"({X:F1}, {Z:F1})";
    }

    /// <summary>
    /// Ket qua phân tích và tính toán thông số uốn bẻ giật cấp sàn.
    /// </summary>
    public class SteppedSlabAnalysisResult
    {
        public SteppedTransitionType TransitionType { get; set; }

        public double DeltaHTop { get; set; }
        public double DeltaHBot { get; set; }

        public double MandrelDiameter { get; set; }
        public double BendRadius { get; set; }
        public double MinStraightLengthAfterBend { get; set; }

        public double HorizontalTransitionLengthTop { get; set; }
        public double HorizontalTransitionLengthBot { get; set; }

        public bool IsTopLayerCranked { get; set; }
        public bool IsBottomLayerCranked { get; set; }

        public List<Point2D> TopLayerCrankPoints { get; set; } = new List<Point2D>();
        public List<Point2D> BottomLayerCrankPoints { get; set; } = new List<Point2D>();

        public string WarningMessage { get; set; }
    }

    /// <summary>
    /// Placer xử lý bố trí thép tại vị trí sàn giật cấp cao độ (SteppedSlabTransitionPlacer).
    /// Nhận IRebarDesignStandard qua Constructor Injection (Strategy Pattern).
    /// </summary>
    public class SteppedSlabTransitionPlacer
    {
        private readonly IRebarDesignStandard _designStandard;

        public SteppedSlabTransitionPlacer(IRebarDesignStandard designStandard)
        {
            _designStandard = designStandard ?? throw new ArgumentNullException(nameof(designStandard));
        }

        /// <summary>
        /// Phân tích và tính toán đường uốn bẻ chuyển tiếp giật cấp sàn.
        /// </summary>
        public SteppedSlabAnalysisResult AnalyzeTransition(SteppedSlabInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var result = new SteppedSlabAnalysisResult();

            // 1. Gọi tiêu chuẩn thiết kế lấy đường kính uốn & đoạn thẳng sau uốn
            // Nếu dùng TCVN chưa verified, phương thức này sẽ throw NotSupportedException theo đúng yêu cầu prompt
            result.MandrelDiameter = _designStandard.GetMinMandrelDiameter(input.BarDiameterMm);
            result.BendRadius = result.MandrelDiameter / 2.0;
            result.MinStraightLengthAfterBend = _designStandard.GetMinStraightLengthAfterBend(input.BarDiameterMm, "tension");

            // 2. Tính Delta H cho mặt trên (Top) và mặt dưới (Bottom/Soffit)
            double top1 = input.Slab1TopElevationMm;
            double top2 = input.Slab2TopElevationMm;
            result.DeltaHTop = Math.Abs(top1 - top2);

            double bot1 = top1 - input.Slab1ThicknessMm;
            double bot2 = top2 - input.Slab2ThicknessMm;
            result.DeltaHBot = Math.Abs(bot1 - bot2);

            // 3. Xác định lớp thép nào cần bẻ (nếu DeltaH > 0.1mm)
            result.IsTopLayerCranked = result.DeltaHTop > 0.1;
            result.IsBottomLayerCranked = result.DeltaHBot > 0.1;

            // 4. Tính toán khoảng uốn ngang L cho từng lớp:
            // L = max(DeltaH * CrankRatio, MinStraightLengthAfterBend)
            if (result.IsTopLayerCranked)
            {
                double lRatio = result.DeltaHTop * input.CrankRatio;
                result.HorizontalTransitionLengthTop = Math.Max(lRatio, result.MinStraightLengthAfterBend);
            }

            if (result.IsBottomLayerCranked)
            {
                double lRatio = result.DeltaHBot * input.CrankRatio;
                result.HorizontalTransitionLengthBot = Math.Max(lRatio, result.MinStraightLengthAfterBend);
            }

            double maxL = Math.Max(result.HorizontalTransitionLengthTop, result.HorizontalTransitionLengthBot);

            // 5. Phân loại Bậc Thấp (LowStep) hay Bậc Cao (HighStep)
            // Ngưỡng phân loại tính động theo tiêu chuẩn:
            // Nếu khoảng chuyển tiếp ngang L nằm trong giới hạn MaxHorizontalTransitionLimitMm -> Bậc thấp (LowStep).
            // Ngược lại -> Bậc cao (HighStep).
            if (maxL <= input.MaxHorizontalTransitionLimitMm)
            {
                result.TransitionType = SteppedTransitionType.LowStep;

                // Tính toán tọa độ các điểm bẻ Crank (Crank Points) cho lớp trên & lớp dưới
                CalculateCrankPoints(input, result);
            }
            else
            {
                result.TransitionType = SteppedTransitionType.HighStep;
                result.WarningMessage = $"Độ giật cấp DeltaH quá lớn (DeltaHTop={result.DeltaHTop:F1}mm). " +
                                       $"Chiều dài uốn ngang L={maxL:F1}mm vượt quá giới hạn {input.MaxHorizontalTransitionLimitMm:F1}mm. " +
                                       $"Chuyển sang cấu tạo Bậc Cao (neo Ld độc lập & thép đai/U-bar).";
            }

            return result;
        }

        private void CalculateCrankPoints(SteppedSlabInput input, SteppedSlabAnalysisResult result)
        {
            // Tọa độ X = 0 là đường giật cấp (Stepped edge)
            // Giả định Slab 1 bên trái (X < 0) và Slab 2 bên phải (X > 0)

            double barRadius = input.BarDiameterMm / 2.0;

            // ── Lớp Thép Trên (Top Layer) ──────────────────────────────────────
            if (result.IsTopLayerCranked)
            {
                double zTop1 = input.Slab1TopElevationMm - input.CoverTop1Mm - barRadius;
                double zTop2 = input.Slab2TopElevationMm - input.CoverTop2Mm - barRadius;

                double halfL = result.HorizontalTransitionLengthTop / 2.0;

                // 4 điểm uốn crank:
                // Point 1: Thép chạy thẳng từ Sàn 1
                // Point 2: Bắt đầu bẻ uốn tại vị trí -halfL
                // Point 3: Kết thúc uốn uốn tại vị trí +halfL ở cao độ Sàn 2
                // Point 4: Thép chạy tiếp sang Sàn 2
                result.TopLayerCrankPoints.Add(new Point2D(-halfL - 100.0, zTop1));
                result.TopLayerCrankPoints.Add(new Point2D(-halfL, zTop1));
                result.TopLayerCrankPoints.Add(new Point2D(halfL, zTop2));
                result.TopLayerCrankPoints.Add(new Point2D(halfL + 100.0, zTop2));
            }
            else
            {
                // Mặt trên phẳng: Lớp trên chạy thẳng không bẻ
                double zTop = input.Slab1TopElevationMm - input.CoverTop1Mm - barRadius;

                result.TopLayerCrankPoints.Add(new Point2D(-200.0, zTop));
                result.TopLayerCrankPoints.Add(new Point2D(200.0, zTop));
            }

            // ── Lớp Thép Dưới (Bottom Layer) ──────────────────────────────────
            if (result.IsBottomLayerCranked)
            {
                double bot1Elev = input.Slab1TopElevationMm - input.Slab1ThicknessMm;
                double bot2Elev = input.Slab2TopElevationMm - input.Slab2ThicknessMm;

                double zBot1 = bot1Elev + input.CoverBot1Mm + barRadius;
                double zBot2 = bot2Elev + input.CoverBot2Mm + barRadius;

                double halfL = result.HorizontalTransitionLengthBot / 2.0;

                result.BottomLayerCrankPoints.Add(new Point2D(-halfL - 100.0, zBot1));
                result.BottomLayerCrankPoints.Add(new Point2D(-halfL, zBot1));
                result.BottomLayerCrankPoints.Add(new Point2D(halfL, zBot2));
                result.BottomLayerCrankPoints.Add(new Point2D(halfL + 100.0, zBot2));
            }
            else
            {
                // Soffit chung (đáy phẳng): Lớp dưới chạy thẳng không bẻ
                double bot1Elev = input.Slab1TopElevationMm - input.Slab1ThicknessMm;
                double zBot = bot1Elev + input.CoverBot1Mm + barRadius;

                result.BottomLayerCrankPoints.Add(new Point2D(-200.0, zBot));
                result.BottomLayerCrankPoints.Add(new Point2D(200.0, zBot));
            }
        }
    }
}

using System;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Strategy Interface quy định số liệu uốn bẻ, neo và nối chồng cốt thép theo tiêu chuẩn thiết kế.
    /// </summary>
    public interface IRebarDesignStandard
    {
        DesignCode StandardCode { get; }

        /// <summary>
        /// Đường kính trục uốn tối thiểu (Mandrel diameter) tính bằng mm.
        /// </summary>
        double GetMinMandrelDiameter(double barDiameterMm, string steelGrade = null);

        /// <summary>
        /// Đoạn thẳng tối thiểu sau điểm uốn tính bằng mm.
        /// </summary>
        double GetMinStraightLengthAfterBend(double barDiameterMm, string zone = "tension");

        /// <summary>
        /// Chiều dài neo Ld tính bằng mm.
        /// </summary>
        double GetAnchorageLength(
            double barDiameterMm,
            ConcreteGrade concrete,
            SteelGrade steel,
            AnchorageType type);

        /// <summary>
        /// Chiều dài nối chồng (Lap splice length) tính bằng mm.
        /// </summary>
        double GetLapSpliceLength(
            double barDiameterMm,
            ConcreteGrade concrete,
            SteelGrade steel,
            AnchorageType type,
            double percentLappedFactor = 1.5);

        /// <summary>
        /// Kiểm tra hàm lượng thép cột (As / Ac).
        /// </summary>
        (bool isValid, double ratioPercent, string warningMessage) ValidateColumnSteelRatio(double totalAsMm2, double sectionAreaMm2);

        /// <summary>
        /// Kiểm tra hàm lượng thép dầm (As / (b * d)).
        /// </summary>
        (bool isValid, double topRatioPercent, double botRatioPercent, string warningMessage) ValidateBeamSteelRatio(double topAsMm2, double botAsMm2, double bMm, double dMm);
    }

    /// <summary>
    /// Tiêu chuẩn Eurocode 2 (EN 1992-1-1:2004, Điều 8.3, Bảng 8.1N) - 🟢 ĐÃ XÁC MINH
    /// </summary>
    public class EurocodeRebarStandard : IRebarDesignStandard
    {
        public DesignCode StandardCode => DesignCode.Eurocode2;

        public double GetMinMandrelDiameter(double barDiameterMm, string steelGrade = null)
        {
            // EN 1992-1-1:2004 Clause 8.3, Table 8.1N:
            // Phi <= 16mm -> 4 * Phi
            // Phi > 16mm  -> 7 * Phi
            return barDiameterMm <= 16.0 ? (4.0 * barDiameterMm) : (7.0 * barDiameterMm);
        }

        public double GetMinStraightLengthAfterBend(double barDiameterMm, string zone = "tension")
        {
            // EN 1992-1-1:2004 Clause 8.3 -> >= 5 * Phi
            return 5.0 * barDiameterMm;
        }

        public double GetAnchorageLength(double barDiameterMm, ConcreteGrade concrete, SteelGrade steel, AnchorageType type)
        {
            return RebarAnchorageCalculator.CalculateAnchorageLength(barDiameterMm, concrete, steel, type, DesignCode.Eurocode2);
        }

        public double GetLapSpliceLength(double barDiameterMm, ConcreteGrade concrete, SteelGrade steel, AnchorageType type, double percentLappedFactor = 1.5)
        {
            return RebarAnchorageCalculator.CalculateLapLength(barDiameterMm, concrete, steel, type, DesignCode.Eurocode2, 30, percentLappedFactor);
        }

        public (bool isValid, double ratioPercent, string warningMessage) ValidateColumnSteelRatio(double totalAsMm2, double sectionAreaMm2)
        {
            if (sectionAreaMm2 <= 0) return (false, 0, "Diện tích tiết diện cột không hợp lệ.");
            double ratio = (totalAsMm2 / sectionAreaMm2) * 100.0;
            // EN 1992-1-1 Clause 9.5.2: rho_min = 0.2%, rho_max = 4.0%
            if (ratio < 0.2)
                return (false, ratio, $"Hàm lượng thép cột μ = {ratio:F2}% < μ_min (0.2%) theo Eurocode 2 (EN 1992-1-1 Clause 9.5.2).");
            if (ratio > 4.0)
                return (false, ratio, $"Hàm lượng thép cột μ = {ratio:F2}% > μ_max (4.0%) theo Eurocode 2 (EN 1992-1-1 Clause 9.5.2).");

            return (true, ratio, null);
        }

        public (bool isValid, double topRatioPercent, double botRatioPercent, string warningMessage) ValidateBeamSteelRatio(double topAsMm2, double botAsMm2, double bMm, double dMm)
        {
            double bd = bMm * dMm;
            if (bd <= 0) return (false, 0, 0, "Kích thước tiết diện dầm không hợp lệ.");
            double topRatio = (topAsMm2 / bd) * 100.0;
            double botRatio = (botAsMm2 / bd) * 100.0;
            // EN 1992-1-1 Clause 9.2.1.1: rho_min = 0.13%, rho_max = 4.0%
            string msg = null;
            bool valid = true;
            if (topRatio < 0.13 || botRatio < 0.13)
            {
                valid = false;
                msg = $"Hàm lượng thép dầm (Top: {topRatio:F2}%, Bot: {botRatio:F2}%) nhỏ hơn μ_min (0.13%) theo Eurocode 2.";
            }
            else if (topRatio > 4.0 || botRatio > 4.0)
            {
                valid = false;
                msg = $"Hàm lượng thép dầm (Top: {topRatio:F2}%, Bot: {botRatio:F2}%) vượt quá μ_max (4.0%) theo Eurocode 2.";
            }
            return (valid, topRatio, botRatio, msg);
        }
    }

    /// <summary>
    /// Tiêu chuẩn TCVN 5574:2018 - 🟢 ĐÃ XÁC MINH (TCVN 5574:2018 Điều 10.3.7 & 10.3.5.7)
    /// </summary>
    public class TcvnRebarStandard : IRebarDesignStandard
    {
        public DesignCode StandardCode => DesignCode.TCVN5574_2018;

        public double GetMinMandrelDiameter(double barDiameterMm, string steelGrade = null)
        {
            // TCVN 5574:2018 Điều 10.3.7:
            // Đường kính gối uốn tối thiểu dbend:
            // - Thép trơn (CB240-T, ...): 2.5 * ds (nếu ds < 20mm), 4.0 * ds (nếu ds >= 20mm)
            // - Thép có gân (CB300-V, CB400-V, CB500-V, ...): 5.0 * ds (nếu ds < 20mm), 8.0 * ds (nếu ds >= 20mm)
            bool isSmooth = !string.IsNullOrEmpty(steelGrade) &&
                            (steelGrade.IndexOf("240", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             steelGrade.IndexOf("smooth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             steelGrade.IndexOf("plain", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isSmooth)
            {
                return barDiameterMm < 20.0 ? (2.5 * barDiameterMm) : (4.0 * barDiameterMm);
            }
            else
            {
                return barDiameterMm < 20.0 ? (5.0 * barDiameterMm) : (8.0 * barDiameterMm);
            }
        }

        public double GetMinStraightLengthAfterBend(double barDiameterMm, string zone = "tension")
        {
            // TCVN 5574:2018 Điều 10.3.5.7 -> >= 5 * ds
            return 5.0 * barDiameterMm;
        }

        public double GetAnchorageLength(double barDiameterMm, ConcreteGrade concrete, SteelGrade steel, AnchorageType type)
        {
            return RebarAnchorageCalculator.CalculateAnchorageLength(barDiameterMm, concrete, steel, type, DesignCode.TCVN5574_2018);
        }

        public double GetLapSpliceLength(double barDiameterMm, ConcreteGrade concrete, SteelGrade steel, AnchorageType type, double percentLappedFactor = 1.5)
        {
            return RebarAnchorageCalculator.CalculateLapLength(barDiameterMm, concrete, steel, type, DesignCode.TCVN5574_2018, 35, percentLappedFactor);
        }

        public (bool isValid, double ratioPercent, string warningMessage) ValidateColumnSteelRatio(double totalAsMm2, double sectionAreaMm2)
        {
            if (sectionAreaMm2 <= 0) return (false, 0, "Diện tích tiết diện cột không hợp lệ.");
            double ratio = (totalAsMm2 / sectionAreaMm2) * 100.0;
            // TCVN 5574:2018 Điều 10.3.2: mu_min = 0.4%, mu_max = 5.0%
            if (ratio < 0.4)
                return (false, ratio, $"Hàm lượng thép cột μ = {ratio:F2}% < μ_min (0.4%) theo TCVN 5574:2018 (Điều 10.3.2).");
            if (ratio > 5.0)
                return (false, ratio, $"Hàm lượng thép cột μ = {ratio:F2}% > μ_max (5.0%) theo TCVN 5574:2018 (Điều 10.3.2).");

            return (true, ratio, null);
        }

        public (bool isValid, double topRatioPercent, double botRatioPercent, string warningMessage) ValidateBeamSteelRatio(double topAsMm2, double botAsMm2, double bMm, double dMm)
        {
            double bd = bMm * dMm;
            if (bd <= 0) return (false, 0, 0, "Kích thước tiết diện dầm không hợp lệ.");
            double topRatio = (topAsMm2 / bd) * 100.0;
            double botRatio = (botAsMm2 / bd) * 100.0;
            // TCVN 5574:2018 Điều 10.3.2: mu_min = 0.1%, mu_max = 4.0%
            string msg = null;
            bool valid = true;
            if (topRatio < 0.1 || botRatio < 0.1)
            {
                valid = false;
                msg = $"Hàm lượng thép dầm (Top: {topRatio:F2}%, Bot: {botRatio:F2}%) nhỏ hơn μ_min (0.1%) theo TCVN 5574:2018 (Điều 10.3.2).";
            }
            else if (topRatio > 4.0 || botRatio > 4.0)
            {
                valid = false;
                msg = $"Hàm lượng thép dầm (Top: {topRatio:F2}%, Bot: {botRatio:F2}%) vượt quá μ_max (4.0%) theo TCVN 5574:2018 (Điều 10.3.2).";
            }
            return (valid, topRatio, botRatio, msg);
        }
    }

    /// <summary>
    /// Factory khởi tạo IRebarDesignStandard
    /// </summary>
    public static class RebarDesignStandardFactory
    {
        public static IRebarDesignStandard Create(DesignCode code)
        {
            switch (code)
            {
                case DesignCode.Eurocode2:
                    return new EurocodeRebarStandard();
                case DesignCode.TCVN5574_2018:
                    return new TcvnRebarStandard();
                default:
                    return new EurocodeRebarStandard();
            }
        }

        public static IRebarDesignStandard Create(string codeName)
        {
            if (string.IsNullOrWhiteSpace(codeName)) return new EurocodeRebarStandard();
            if (codeName.IndexOf("Eurocode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                codeName.IndexOf("EC2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new EurocodeRebarStandard();
            }
            if (codeName.IndexOf("TCVN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new TcvnRebarStandard();
            }

            return new EurocodeRebarStandard();
        }
    }
}

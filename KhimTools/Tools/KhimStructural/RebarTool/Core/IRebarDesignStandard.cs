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
        /// Khoảng hở tối thiểu giữa các thanh cốt thép song song tính bằng mm.
        /// </summary>
        double GetMinClearSpacing(double barDiameterMm, double maxAggregateSizeMm = 20.0, bool isTopBar = false);

        /// <summary>
        /// Chiều dài đoạn neo đuôi móc (Hook tail length) tính bằng mm.
        /// </summary>
        double GetHookTailLength(double barDiameterMm, double bendAngleDeg = 135.0, bool isStirrup = true, bool isSeismicDesign = false);

        /// <summary>
        /// Lớp bê tông bảo vệ tối thiểu theo loại cấu kiện và cấp độ bền/môi trường (mm).
        /// </summary>
        double GetMinConcreteCoverMm(string memberType, string exposureClass = "XC1");

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
    /// Tiêu chuẩn Eurocode 2 (EN 1992-1-1:2004, Điều 8.3, Bảng 8.1N)
    /// </summary>
    public class EurocodeRebarStandard : IRebarDesignStandard
    {
        public DesignCode StandardCode => DesignCode.Eurocode2;

        public double GetMinMandrelDiameter(double barDiameterMm, string steelGrade = null)
        {
            // EN 1992-1-1:2004 Table 8.1N:
            // Uốn đai / cốt dọc:
            // barDiameterMm <= 16mm -> 4 * ds
            // barDiameterMm > 16mm -> 7 * ds
            return barDiameterMm <= 16.0 ? (4.0 * barDiameterMm) : (7.0 * barDiameterMm);
        }

        public double GetMinStraightLengthAfterBend(double barDiameterMm, string zone = "tension")
        {
            // EN 1992-1-1 Figure 8.1: standard hook / bend -> >= 5 * ds
            return 5.0 * barDiameterMm;
        }

        public double GetMinClearSpacing(double barDiameterMm, double maxAggregateSizeMm = 20.0, bool isTopBar = false)
        {
            // EN 1992-1-1 Clause 8.2: s_clear >= max(k1 * ds, dg + k2, 20mm). k1 = 1.0, k2 = 5mm.
            return Math.Max(barDiameterMm, Math.Max(maxAggregateSizeMm + 5.0, 20.0));
        }

        public double GetHookTailLength(double barDiameterMm, double bendAngleDeg = 135.0, bool isStirrup = true, bool isSeismicDesign = false)
        {
            // EN 1992-1-1 Figure 8.1:
            // Đai uốn 135° hoặc 150°: >= max(5 * ds, 50mm) với uốn chuẩn, hoặc 10 * ds cho động đất
            if (isStirrup)
            {
                return isSeismicDesign 
                    ? Math.Max(10.0 * barDiameterMm, 70.0)
                    : Math.Max(5.0 * barDiameterMm, 50.0);
            }
            // Thép chủ móc 90° / 135°: >= 5 * ds (hoặc 12 * ds cho neo đầy đủ)
            return Math.Max(bendAngleDeg >= 135.0 ? 5.0 * barDiameterMm : 10.0 * barDiameterMm, 100.0);
        }

        public double GetMinConcreteCoverMm(string memberType, string exposureClass = "XC1")
        {
            // EN 1992-1-1 Clause 4.4.1: c_nom = c_min + Delta_c_dev (Delta_c_dev ~ 10mm)
            string m = (memberType ?? "").ToLowerInvariant();
            if (m.Contains("column") || m.Contains("cot")) return 30.0;
            if (m.Contains("beam") || m.Contains("dam")) return 30.0;
            if (m.Contains("slab") || m.Contains("san")) return 20.0;
            if (m.Contains("foundation") || m.Contains("mong")) return 50.0;
            return 25.0;
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
    /// Tiêu chuẩn TCVN 5574:2018 (TCVN 5574:2018 Điều 10.3.7 & 10.3.5.7)
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

        public double GetMinClearSpacing(double barDiameterMm, double maxAggregateSizeMm = 20.0, bool isTopBar = false)
        {
            // TCVN 5574:2018 Điều 10.3.5.2 & 10.3.5.3:
            // - Cốt thép dưới: s_clear >= max(ds, 25mm, d_agg)
            // - Cốt thép trên: s_clear >= max(ds, 30mm, d_agg)
            double baseMin = isTopBar ? 30.0 : 25.0;
            return Math.Max(barDiameterMm, Math.Max(baseMin, maxAggregateSizeMm));
        }

        public double GetHookTailLength(double barDiameterMm, double bendAngleDeg = 135.0, bool isStirrup = true, bool isSeismicDesign = false)
        {
            // TCVN 5574:2018 Điều 10.3.5.7 & Mục móc uốn đai:
            // - Đai kháng chấn 135°: >= max(10 * ds, 75mm)
            // - Đai thông thường 135°: >= max(5 * ds, 50mm)
            // - Móc neo 90° cốt dọc: >= max(12 * ds, 150mm)
            if (isStirrup)
            {
                return isSeismicDesign
                    ? Math.Max(10.0 * barDiameterMm, 75.0)
                    : Math.Max(5.0 * barDiameterMm, 50.0);
            }
            return Math.Max(bendAngleDeg >= 135.0 ? 6.0 * barDiameterMm : 12.0 * barDiameterMm, 150.0);
        }

        public double GetMinConcreteCoverMm(string memberType, string exposureClass = "XC1")
        {
            // TCVN 5574:2018 Bảng 18 (Lớp bê tông bảo vệ tối thiểu):
            // - Bản sàn, tường: 20mm (trong nhà khô ráo), 25mm (ngoài trời)
            // - Dầm, sườn: 25mm (trong nhà), 30mm (ngoài trời)
            // - Cột: 25mm hoặc >= d_s (trong nhà), 30mm (ngoài trời)
            // - Móng toàn khối: 40mm (có bê tông lót), 70mm (không bê tông lót)
            string m = (memberType ?? "").ToLowerInvariant();
            if (m.Contains("column") || m.Contains("cot")) return 30.0;
            if (m.Contains("beam") || m.Contains("dam")) return 30.0;
            if (m.Contains("slab") || m.Contains("san")) return 25.0;
            if (m.Contains("foundation") || m.Contains("mong")) return 50.0;
            return 25.0;
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
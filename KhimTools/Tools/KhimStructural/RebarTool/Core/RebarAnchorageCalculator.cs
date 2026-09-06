using System;

namespace KhimTools.RebarTool.Core
{
    public enum ConcreteGrade
    {
        Auto,
        B15, B20, B25, B30, B35, B40, B45, B50,
        C20_25, C25_30, C30_37, C35_45, C40_50
    }

    public enum SteelGrade
    {
        Auto,
        CB240_T, CB300_V, CB400_V, CB500_V,
        B400, B500
    }

    public enum AnchorageType
    {
        TensionStraight,
        TensionHooked,
        Compression
    }

    public enum DesignCode
    {
        TCVN5574_2018,
        Eurocode2
    }

    public class AnchorageCalculationResult
    {
        public double RequiredAnchorageLengthMm { get; set; }
        public double ActualAnchorageLengthMm { get; set; }
        public bool IsValid { get; set; } = true;
        public string Status { get; set; } = "PASS";
        public string Reason { get; set; } = "";
    }

    public class LapSpliceCalculationResult
    {
        public double RequiredLapLengthMm { get; set; }
        public double ActualLapLengthMm { get; set; }
        public string SpliceZone { get; set; } = "Central Column / Midspan Zone";
        public string TransverseDetailingRequirement { get; set; } = "Enclosed Stirrups / Confinement Ties";
        public bool IsValid { get; set; } = true;
        public string Status { get; set; } = "PASS";
        public string Reason { get; set; } = "";
    }

    public static class RebarAnchorageCalculator
    {
        public static AnchorageCalculationResult CalculateAnchorageDetailed(
            double barDiameterMm,
            ConcreteGrade concrete,
            SteelGrade steel,
            AnchorageType type,
            DesignCode code,
            double availableLengthMm = 0)
        {
            double reqLen = CalculateAnchorageLength(barDiameterMm, concrete, steel, type, code);
            var res = new AnchorageCalculationResult
            {
                RequiredAnchorageLengthMm = reqLen,
                ActualAnchorageLengthMm = (availableLengthMm > 0) ? availableLengthMm : reqLen
            };

            if (availableLengthMm > 0 && availableLengthMm < reqLen)
            {
                res.IsValid = false;
                res.Status = "ENGINEERING_VALIDATION_FAILED";
                res.Reason = $"Available anchorage ({availableLengthMm:F0}mm) < Required anchorage ({reqLen:F0}mm).";
            }
            else
            {
                res.Status = "PASS";
                res.Reason = $"Anchorage satisfied: {res.ActualAnchorageLengthMm:F0}mm >= {reqLen:F0}mm under {code}.";
            }

            return res;
        }

        public static LapSpliceCalculationResult CalculateLapDetailed(
            double barDiameterMm,
            ConcreteGrade concrete,
            SteelGrade steel,
            AnchorageType type,
            DesignCode code,
            double availableLapMm = 0,
            double percentLappedFactor = 1.5)
        {
            double reqLap = CalculateLapLength(barDiameterMm, concrete, steel, type, code, 30, percentLappedFactor);
            var res = new LapSpliceCalculationResult
            {
                RequiredLapLengthMm = reqLap,
                ActualLapLengthMm = (availableLapMm > 0) ? availableLapMm : reqLap
            };

            if (availableLapMm > 0 && availableLapMm < reqLap)
            {
                res.IsValid = false;
                res.Status = "ENGINEERING_VALIDATION_FAILED";
                res.Reason = $"Available lap length ({availableLapMm:F0}mm) < Required lap ({reqLap:F0}mm).";
            }
            else
            {
                res.Status = "PASS";
                res.Reason = $"Lap splice satisfied: {res.ActualLapLengthMm:F0}mm >= {reqLap:F0}mm under {code}.";
            }

            return res;
        }
        public static double CalculateAnchorageLength(
            double barDiameterMm,
            ConcreteGrade concrete,
            SteelGrade steel,
            AnchorageType type,
            DesignCode code,
            double fallbackMultiplier = 35)
        {
            if (concrete == ConcreteGrade.Auto || steel == SteelGrade.Auto)
            {
                System.Diagnostics.Debug.WriteLine($"[RebarAnchorageCalculator] Warning: ConcreteGrade ({concrete}) or SteelGrade ({steel}) is Auto. Using empirical fallback anchorage {fallbackMultiplier}d = {barDiameterMm * fallbackMultiplier:F1}mm.");
                return barDiameterMm * fallbackMultiplier;
            }

            if (code == DesignCode.TCVN5574_2018)
            {
                return CalculateTCVN(barDiameterMm, concrete, steel, type);
            }
            else
            {
                return CalculateEurocode(barDiameterMm, concrete, steel, type);
            }
        }

        public static double CalculateLapLength(
            double barDiameterMm,
            ConcreteGrade concrete,
            SteelGrade steel,
            AnchorageType type,
            DesignCode code,
            double fallbackMultiplier = 30,
            double percentLappedFactor = 1.5)
        {
            if (concrete == ConcreteGrade.Auto || steel == SteelGrade.Auto)
            {
                System.Diagnostics.Debug.WriteLine($"[RebarAnchorageCalculator] Warning: ConcreteGrade ({concrete}) or SteelGrade ({steel}) is Auto. Using empirical fallback lap {fallbackMultiplier}d = {barDiameterMm * fallbackMultiplier:F1}mm.");
                return barDiameterMm * fallbackMultiplier;
            }

            double ld = CalculateAnchorageLength(barDiameterMm, concrete, steel, type, code);

            if (code == DesignCode.TCVN5574_2018)
            {
                // TCVN 5574:2018 Clause 8.3.2.3:
                // lap length = alpha * l_an
                // alpha depends on percent lapped: typically 1.2 to 2.0. We use percentLappedFactor.
                double lan = ld; // assuming ld is calculated tension straight
                double ll = percentLappedFactor * lan;
                return Math.Max(ll, Math.Max(barDiameterMm * 20, 250.0));
            }
            else
            {
                // Eurocode 2 Clause 8.7.3:
                // l_0 = alpha_6 * l_bd
                // alpha_6 depends on percent lapped: 1.0 to 1.5. We use percentLappedFactor.
                double l0 = percentLappedFactor * ld;
                return Math.Max(l0, Math.Max(barDiameterMm * 15, 200.0));
            }
        }

        private static double CalculateTCVN(double d, ConcreteGrade concrete, SteelGrade steel, AnchorageType type)
        {
            // TCVN 5574:2018 Table 13 - design tensile strength of concrete R_bt (MPa)
            double r_bt = 0.9; // B20 fallback
            switch (concrete)
            {
                case ConcreteGrade.B15: r_bt = 0.75; break;
                case ConcreteGrade.B20: r_bt = 0.90; break;
                case ConcreteGrade.B25: r_bt = 1.05; break;
                case ConcreteGrade.B30: r_bt = 1.15; break;
                case ConcreteGrade.B35: r_bt = 1.25; break;
                case ConcreteGrade.B40: r_bt = 1.35; break;
                case ConcreteGrade.B45: r_bt = 1.42; break;
                case ConcreteGrade.B50: r_bt = 1.50; break;
            }

            // TCVN 5574:2018 - design strength of steel R_s (MPa)
            double r_s = 350; // CB400-V fallback
            bool isDeformed = true;
            switch (steel)
            {
                case SteelGrade.CB240_T: r_s = 210; isDeformed = false; break;
                case SteelGrade.CB300_V: r_s = 260; isDeformed = true; break;
                case SteelGrade.CB400_V: r_s = 350; isDeformed = true; break;
                case SteelGrade.CB500_V: r_s = 435; isDeformed = true; break;
            }

            // Bond strength f_bd = eta_1 * eta_2 * r_bt
            double eta_1 = isDeformed ? 2.5 : 1.5;
            double eta_2 = (d <= 32) ? 1.0 : 0.9;
            double f_bd = eta_1 * eta_2 * r_bt;

            // Basic anchorage length l_an = d * r_s / (4 * f_bd)
            double l_an = d * r_s / (4.0 * f_bd);

            // Design anchorage length l_d = alpha * l_an
            double alpha = 1.0;
            if (type == AnchorageType.TensionStraight)
            {
                alpha = 1.0;
            }
            else if (type == AnchorageType.TensionHooked)
            {
                alpha = 0.7; // TCVN 5574 tension hooked
            }
            else if (type == AnchorageType.Compression)
            {
                alpha = 0.75; // TCVN 5574 compression
            }

            double l_d = alpha * l_an;
            return Math.Max(l_d, Math.Max(d * 15, 200.0));
        }

        private static double CalculateEurocode(double d, ConcreteGrade concrete, SteelGrade steel, AnchorageType type)
        {
            // EN 1992-1-1 Table 3.1 & formulas
            // f_ctd = alpha_ct * f_ctk_0.05 / gamma_c
            // f_ctk_0.05 = 0.7 * f_ctm
            // f_ctm = 0.3 * f_ck^(2/3)
            double f_ck = 25; // C25/30 fallback
            switch (concrete)
            {
                case ConcreteGrade.B15: f_ck = 15; break;
                case ConcreteGrade.B20: f_ck = 20; break;
                case ConcreteGrade.B25: f_ck = 25; break;
                case ConcreteGrade.B30: f_ck = 30; break;
                case ConcreteGrade.B35: f_ck = 35; break;
                case ConcreteGrade.B40: f_ck = 40; break;
                case ConcreteGrade.B45: f_ck = 45; break;
                case ConcreteGrade.B50: f_ck = 50; break;
                case ConcreteGrade.C20_25: f_ck = 20; break;
                case ConcreteGrade.C25_30: f_ck = 25; break;
                case ConcreteGrade.C30_37: f_ck = 30; break;
                case ConcreteGrade.C35_45: f_ck = 35; break;
                case ConcreteGrade.C40_50: f_ck = 40; break;
            }

            double f_ctm = 0.3 * Math.Pow(f_ck, 2.0 / 3.0);
            double f_ctk = 0.7 * f_ctm;
            double f_ctd = 1.0 * f_ctk / 1.5;

            // design strength of steel f_yd = f_yk / 1.15
            double f_yk = 500; // B500 fallback
            switch (steel)
            {
                case SteelGrade.CB240_T: f_yk = 240; break;
                case SteelGrade.CB300_V: f_yk = 300; break;
                case SteelGrade.CB400_V: f_yk = 400; break;
                case SteelGrade.CB500_V: f_yk = 500; break;
                case SteelGrade.B400: f_yk = 400; break;
                case SteelGrade.B500: f_yk = 500; break;
            }
            double f_yd = f_yk / 1.15;

            // Design bond strength f_bd = 2.25 * eta_1 * eta_2 * f_ctd
            double eta_1 = 1.0; // good bond condition assumed
            double eta_2 = (d <= 32) ? 1.0 : (132.0 - d) / 100.0;
            double f_bd = 2.25 * eta_1 * eta_2 * f_ctd;

            // Basic anchorage length l_b_rqd = d / 4 * f_yd / f_bd
            double l_b_rqd = (d / 4.0) * (f_yd / f_bd);

            // Design anchorage length l_bd = alpha_1 * l_b_rqd
            double alpha_1 = 1.0;
            if (type == AnchorageType.TensionHooked)
            {
                alpha_1 = 0.7; // hooked tension
            }

            double l_bd = alpha_1 * l_b_rqd;

            // Minimum anchorage length
            double l_b_min = 100.0;
            if (type == AnchorageType.Compression)
            {
                l_b_min = Math.Max(0.6 * l_b_rqd, Math.Max(d * 10, 100.0));
            }
            else
            {
                l_b_min = Math.Max(0.3 * l_b_rqd, Math.Max(d * 10, 100.0));
            }

            return Math.Max(l_bd, l_b_min);
        }
    }
}

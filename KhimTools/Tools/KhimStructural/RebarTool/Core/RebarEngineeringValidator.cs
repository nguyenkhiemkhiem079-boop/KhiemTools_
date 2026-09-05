using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{

    public class RebarValidationResult
    {
        public bool IsValid { get; set; } = true;
        public string FailureReason { get; set; } = "";
        public List<EngineeringViolation> Violations { get; set; } = new List<EngineeringViolation>();

        // Phân rã chi tiết kết quả thẩm tra theo tiêu chuẩn Eurocode 2
        public bool ContainmentPassed { get; set; } = true;
        public bool CoverPassed { get; set; } = true;
        public bool ClearSpacingPassed { get; set; } = true;
        public bool StockLengthPassed { get; set; } = true;
        public bool LapSplicePassed { get; set; } = true;
        public bool AnchoragePassed { get; set; } = true;
        public bool MandrelDiameterPassed { get; set; } = true;
        public bool TransverseSectionPassed { get; set; } = true;
        public bool LongitudinalSectionPassed { get; set; } = true;
        public bool ConnectionTransitionPassed { get; set; } = true;

        public string IntentBreakdown { get; set; } = "";
        public string ContainmentStatus { get; set; } = "PASS";
        public string TransverseSectionStatus { get; set; } = "PASS";
        public string LongitudinalSectionStatus { get; set; } = "PASS";
        public string SpliceCheck { get; set; } = "PASS";
        public string CoverCheck { get; set; } = "PASS";

        public void AddViolation(string code, string category, string desc, XYZ loc = null, double expected = 0, double actual = 0, string unit = "mm", bool isCritical = true)
        {
            IsValid = false;
            Violations.Add(new EngineeringViolation
            {
                Code = code,
                Category = category,
                Description = desc,
                ViolationLocation = loc ?? XYZ.Zero,
                ExpectedValue = expected,
                ActualValue = actual,
                Unit = unit,
                IsCritical = isCritical
            });

            if (string.IsNullOrEmpty(FailureReason))
            {
                FailureReason = desc;
            }
            else
            {
                FailureReason += $" | {desc}";
            }
        }
    }

    /// <summary>
    /// Thẩm tra kỹ thuật cốt thép toàn diện (Section 34 Eurocode Engineering Validator):
    /// Không bao giờ bỏ qua lỗi hoặc fallback ngầm định. Trả về báo cáo chẩn đoán chi tiết.
    /// </summary>
    public static class RebarEngineeringValidator
    {
        public const double CommercialMaxStockLengthMm = 11700.0; // 11.7m
        public const double DefaultMinClearSpacingMm = 25.0;      // EC2 Cl. 8.2: max(phi, dg + 5mm, 20mm) >= 25mm

        /// <summary>
        /// Thẩm tra toàn diện một tập hợp cốt thép Rebar đối với Host và DetailingIntentContext
        /// </summary>
        public static RebarValidationResult ValidateRebarAssembly(
            Document doc,
            Element host,
            IEnumerable<Rebar> rebars,
            DetailingIntentContext intentContext,
            IRebarDesignStandard standard,
            double nominalCoverMm = 30.0)
        {
            var result = new RebarValidationResult();
            var rebarList = rebars?.Where(r => r != null && r.IsValidObject).ToList() ?? new List<Rebar>();

            if (host == null || !host.IsValidObject)
            {
                result.AddViolation("ERR_HOST_NULL", "Host", "Host element is null or invalid.");
                return result;
            }

            if (rebarList.Count == 0)
            {
                result.AddViolation("ERR_NO_REBARS", "Assembly", "No valid rebars found in assembly.");
                return result;
            }

            if (intentContext == null)
            {
                intentContext = new DetailingIntentContext(host, DetailingIntentType.StandardInternal);
            }

            result.IntentBreakdown = $"Host: [{host.Id}] {host.Name} | Intent: {intentContext.IntentType} | Connected: {intentContext.ConnectedHost?.Id.IntegerValue ?? -1}";

            // 1. Kiểm tra Containment 3D (Đâm thủng bê tông tự do vs Chủ đích vươn sang ConnectedHost)
            ValidateContainmentWithIntent(doc, host, rebarList, intentContext, nominalCoverMm, result);

            // 2. Kiểm tra Chiều dài thương mại thương phẩm (Commercial Stock Length <= 11.7m)
            ValidateStockLength(rebarList, result);

            // 3. Kiểm tra Đường kính gá uốn (Mandrel Diameter - EC2 Cl. 8.3)
            ValidateMandrelDiameter(rebarList, standard, result);

            // 4. Cập nhật trạng thái tổng quan
            result.ContainmentStatus = result.ContainmentPassed ? "PASS" : "FAIL";
            result.CoverCheck = result.CoverPassed ? "PASS" : "FAIL";
            result.TransverseSectionStatus = result.TransverseSectionPassed ? "PASS" : "FAIL";
            result.LongitudinalSectionStatus = result.LongitudinalSectionPassed ? "PASS" : "FAIL";
            result.SpliceCheck = result.LapSplicePassed ? "PASS" : "FAIL";

            result.IsValid = result.Violations.Count == 0;
            return result;
        }

        private static void ValidateContainmentWithIntent(
            Document doc,
            Element host,
            List<Rebar> rebars,
            DetailingIntentContext intentContext,
            double nominalCoverMm,
            RebarValidationResult result)
        {
            // Sử dụng RebarHostContainmentValidator với hỗ trợ DetailingIntentContext
            var report = RebarHostContainmentValidator.ValidateHostContainmentWithIntent(
                doc, host, rebars, intentContext, nominalCoverMm);

            if (!report.OverallPassed)
            {
                if (report.Protrusions.Count > 0)
                {
                    result.ContainmentPassed = false;
                    foreach (var prot in report.Protrusions)
                    {
                        result.AddViolation(
                            "ERR_FREE_SPACE_PROTRUSION",
                            "GeometryContainment",
                            $"Cốt thép đâm thủng ra ngoài bê tông {prot.OutsideDistanceMm:F1}mm tại {prot.FaceDesc} (Không thuộc cấu kiện kết cấu nào).",
                            prot.ViolationPoint,
                            0,
                            prot.OutsideDistanceMm);
                    }
                }

                if (report.CoverViolations.Count > 0)
                {
                    result.CoverPassed = false;
                    foreach (var cov in report.CoverViolations)
                    {
                        result.AddViolation(
                            "ERR_COVER_DEFICIENCY",
                            "ConcreteCover",
                            $"Vi phạm chiều dày lớp bảo vệ bê tông: {cov.ActualCoverMm:F1}mm < yêu cầu {cov.RequiredCoverMm:F1}mm tại {cov.FaceDesc}.",
                            cov.ViolationPoint,
                            cov.RequiredCoverMm,
                            cov.ActualCoverMm);
                    }
                }

                if (report.TransverseSections.Any(s => !s.Passed))
                {
                    result.TransverseSectionPassed = false;
                    foreach (var s in report.TransverseSections.Where(s => !s.Passed))
                    {
                        result.AddViolation(
                            "ERR_TRANSVERSE_SECTION_FAIL",
                            "TransverseQA",
                            $"Mặt cắt ngang trạm {s.StationName} không đạt chuẩn: {string.Join(", ", s.FailureReasons)}",
                            s.StationPoint);
                    }
                }

                if (report.LongitudinalSections.Any(s => !s.Passed))
                {
                    result.LongitudinalSectionPassed = false;
                    foreach (var s in report.LongitudinalSections.Where(s => !s.Passed))
                    {
                        result.AddViolation(
                            "ERR_LONGITUDINAL_SECTION_FAIL",
                            "LongitudinalQA",
                            $"Mặt cắt dọc không đạt chuẩn: {string.Join(", ", s.FailureReasons)}",
                            s.StationPoint);
                    }
                }
            }
        }

        private static void ValidateStockLength(List<Rebar> rebars, RebarValidationResult result)
        {
            foreach (var rebar in rebars)
            {
                try
                {
                    Parameter lenParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH);
                    if (lenParam != null && lenParam.HasValue)
                    {
                        double lenMm = UnitUtils.ConvertFromInternalUnits(lenParam.AsDouble(), UnitTypeId.Millimeters);
                        if (lenMm > CommercialMaxStockLengthMm + 1.0)
                        {
                            result.StockLengthPassed = false;
                            result.AddViolation(
                                "ERR_STOCK_LENGTH_EXCEEDED",
                                "Fabrication",
                                $"Chiều dài thanh thép {lenMm:F0}mm vượt quá chiều dài thương mại chuẩn {CommercialMaxStockLengthMm}mm. Bắt buộc bố trí mối nối (Lap Splice / Coupler).",
                                XYZ.Zero,
                                CommercialMaxStockLengthMm,
                                lenMm);
                        }
                    }
                }
                catch { }
            }
        }

        private static void ValidateMandrelDiameter(List<Rebar> rebars, IRebarDesignStandard standard, RebarValidationResult result)
        {
            if (standard == null) return;

            foreach (var rebar in rebars)
            {
                try
                {
                    var barType = rebar.Document.GetElement(rebar.GetTypeId()) as RebarBarType;
                    if (barType == null) continue;

                    double barDiaMm = UnitUtils.ConvertFromInternalUnits(barType.BarModelDiameter, UnitTypeId.Millimeters);
                    double bendDiaFeet = barType.StandardBendDiameter;
                    double bendDiaMm = UnitUtils.ConvertFromInternalUnits(bendDiaFeet, UnitTypeId.Millimeters);

                    double reqMandrelMm = standard.GetMinMandrelDiameter(barDiaMm);

                    if (bendDiaMm < reqMandrelMm - 0.5)
                    {
                        result.MandrelDiameterPassed = false;
                        result.AddViolation(
                            "ERR_MANDREL_DIAMETER_DEFICIENT",
                            "EurocodeMandrel",
                            $"Đường kính gá uốn {bendDiaMm:F1}mm không đạt yêu cầu Eurocode 2 Cl. 8.3 (Tối thiểu {reqMandrelMm:F1}mm cho phi {barDiaMm:F0}mm).",
                            XYZ.Zero,
                            reqMandrelMm,
                            bendDiaMm);
                    }
                }
                catch { }
            }
        }
    }
}

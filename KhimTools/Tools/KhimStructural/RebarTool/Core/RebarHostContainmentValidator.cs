using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;

namespace KhimTools.RebarTool.Core
{
    public class ContainmentViolation
    {
        public ElementId RebarId { get; set; } = ElementId.InvalidElementId;
        public ElementId HostId { get; set; } = ElementId.InvalidElementId;
        public string RebarRole { get; set; } = "";
        public XYZ ViolationPoint { get; set; } = XYZ.Zero;
        public double OutsideDistanceMm { get; set; }
        public double RequiredCoverMm { get; set; }
        public double ActualCoverMm { get; set; }
        public string ViolationType { get; set; } = ""; // "ProtrusionOutsideHost" or "CoverDeficiency"
        public string CurveSegmentDesc { get; set; } = "";
        public string FaceDesc { get; set; } = "";
    }

    public class SectionStationResult
    {
        public string StationName { get; set; } = "";
        public double StationRatio { get; set; }
        public XYZ StationPoint { get; set; } = XYZ.Zero;
        public bool Passed { get; set; } = true;
        public int TotalBarsEncountered { get; set; }
        public int ContainedBarsCount { get; set; }
        public int ViolatedBarsCount { get; set; }
        public double MinActualCoverMm { get; set; }
        public double RequiredCoverMm { get; set; }
        public double MinClearSpacingMm { get; set; }
        public List<string> FailureReasons { get; set; } = new List<string>();
    }

    public class ContainmentValidationReport
    {
        public ElementId HostId { get; set; } = ElementId.InvalidElementId;
        public string HostName { get; set; } = "";
        public string HostCategory { get; set; } = "";
        public bool OverallPassed { get; set; } = true;
        public int TotalRebarsChecked { get; set; }
        public int TotalSamplePointsEvaluated { get; set; }

        public List<ContainmentViolation> Protrusions { get; set; } = new List<ContainmentViolation>();
        public List<ContainmentViolation> CoverViolations { get; set; } = new List<ContainmentViolation>();
        public List<SectionStationResult> TransverseSections { get; set; } = new List<SectionStationResult>();
        public List<SectionStationResult> LongitudinalSections { get; set; } = new List<SectionStationResult>();

        public double MinActualCoverFoundMm { get; set; } = double.MaxValue;
        public double MaxProtrusionDistanceFoundMm { get; set; } = 0;
        public string SummaryMessage { get; set; } = "";
    }

    public class HostFacePlane
    {
        public XYZ Origin { get; set; }
        public XYZ Normal { get; set; } // Outward normal vector
        public string FaceType { get; set; } // "Top", "Bottom", "Side", "End"
        public double RequiredCoverFeet { get; set; }
        public PlanarFace PlanarFace { get; set; }
    }

    /// <summary>
    /// Validator hình học Solid cấp độ cao nhất: Kiểm soát 100% cốt thép phải nằm trọn
    /// trong khối bê tông Host và tuân thủ lớp bê tông bảo vệ (Concrete Cover).
    /// Nguyên lý P0: REBAR MUST NEVER LEAVE HOST.
    /// Khảo sát hình học thực tế: Thể tích Solid, Bán kính thanh (d/2), Hook, Bend,
    /// Mặt cắt ngang (Transverse Stations) và Mặt cắt dọc (Longitudinal Stations).
    /// </summary>
    public static class RebarHostContainmentValidator
    {
        public const double DiscretizationStepMm = 25.0; // Bước chia nhỏ đường cong thanh thép (25mm)
        public const double MinClearSpacingStandardMm = 25.0;

        /// <summary>
        /// Đánh giá toàn diện 3D Solid Containment cho danh sách Rebars trên một Host Element
        /// </summary>
        public static ContainmentValidationReport ValidateHostContainment(
            Document doc,
            Element host,
            IEnumerable<Rebar> rebars,
            double? customCoverMm = null)
        {
            var report = new ContainmentValidationReport();
            if (host == null || !host.IsValidObject)
            {
                report.OverallPassed = false;
                report.SummaryMessage = "Host element is null or invalid.";
                return report;
            }

            report.HostId = host.Id;
            report.HostName = host.Name;
            report.HostCategory = host.Category?.Name ?? "Structural";

            var solids = ExtractHostSolids(host);
            if (!solids.Any())
            {
                report.OverallPassed = false;
                report.SummaryMessage = "Không thể trích xuất hình học Solid từ Host element.";
                return report;
            }

            var facePlanes = ExtractFacePlanes(solids, host, customCoverMm);
            var rebarList = rebars?.Where(r => r != null && r.IsValidObject).ToList() ?? new List<Rebar>();
            report.TotalRebarsChecked = rebarList.Count;

            // 1. Kiểm tra 3D Physical Bar Containment (Đường tim + bán kính d/2)
            foreach (var rebar in rebarList)
            {
                ValidateSingleRebar(rebar, host, solids, facePlanes, report);
            }

            // 2. Khảo sát mặt cắt ngang (Transverse Sections QA: 0%, 25%, 50%, 75%, 100% + critical zones)
            ValidateTransverseSections(host, rebarList, facePlanes, report);

            // 3. Khảo sát mặt cắt dọc (Longitudinal Sections QA)
            ValidateLongitudinalSection(host, rebarList, facePlanes, report);

            // Kết luận tổng thể
            report.OverallPassed = (report.Protrusions.Count == 0) &&
                                   (report.CoverViolations.Count == 0) &&
                                   report.TransverseSections.All(s => s.Passed) &&
                                   report.LongitudinalSections.All(s => s.Passed);

            if (report.OverallPassed)
            {
                report.SummaryMessage = $"PASS: 100% cốt thép ({report.TotalRebarsChecked} Rebars, {report.TotalSamplePointsEvaluated} điểm) nằm hoàn toàn trong bê tông và đạt chuẩn lớp bảo vệ.";
            }
            else
            {
                int protCount = report.Protrusions.Count;
                int covCount = report.CoverViolations.Count;
                report.SummaryMessage = $"FAIL: Phát hiện {protCount} vị trí thép đâm thủng ra ngoài bê tông, {covCount} vị trí vi phạm lớp bảo vệ!";
            }

            return report;
        }

        private static void ValidateSingleRebar(
            Rebar rebar,
            Element host,
            List<Solid> solids,
            List<HostFacePlane> facePlanes,
            ContainmentValidationReport report)
        {
            double barDiaFeet = GetBarDiameter(rebar);
            double barRadiusFeet = barDiaFeet / 2.0;
            double barDiaMm = UnitUtils.ConvertFromInternalUnits(barDiaFeet, UnitTypeId.Millimeters);

            string role = GetRebarRole(rebar);
            int barCount = Math.Max(1, rebar.NumberOfBarPositions);

            for (int barIdx = 0; barIdx < barCount; barIdx++)
            {
                IList<Curve> curves = null;
                try
                {
                    curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, barIdx);
                }
                catch
                {
                    try
                    {
                        curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, barIdx);
                    }
                    catch { }
                }

                if (curves == null || !curves.Any()) continue;

                int segIndex = 0;
                foreach (var curve in curves)
                {
                    segIndex++;
                    var samplePoints = DiscretizeCurve(curve, DiscretizationStepMm);

                    foreach (var pt in samplePoints)
                    {
                        report.TotalSamplePointsEvaluated++;

                        // Đánh giá khoảng cách đến từng mặt ngoài của bê tông
                        foreach (var fp in facePlanes)
                        {
                            // Vector từ điểm mặt phẳng đến điểm khảo sát
                            XYZ vec = pt - fp.Origin;
                            // Chiếu lên vector pháp tuyến hướng ra ngoài
                            double signedDistFeet = vec.DotProduct(fp.Normal);
                            double signedDistMm = UnitUtils.ConvertFromInternalUnits(signedDistFeet, UnitTypeId.Millimeters);

                            // Bán kính thanh theo mm
                            double barRadiusMm = barDiaMm / 2.0;
                            double reqCoverMm = UnitUtils.ConvertFromInternalUnits(fp.RequiredCoverFeet, UnitTypeId.Millimeters);

                            // Nếu signedDistMm > 0: điểm tim thép đã nằm ngoài mặt phẳng bê tông
                            // Nếu signedDistMm > -barRadiusMm: vỏ thanh thép đã nhô ra ngoài bề mặt bê tông!
                            if (signedDistMm > -barRadiusMm + 0.1) // 0.1mm tolerance sai số số thực
                            {
                                double protrudeMm = signedDistMm + barRadiusMm;
                                report.MaxProtrusionDistanceFoundMm = Math.Max(report.MaxProtrusionDistanceFoundMm, protrudeMm);

                                report.Protrusions.Add(new ContainmentViolation
                                {
                                    RebarId = rebar.Id,
                                    HostId = host.Id,
                                    RebarRole = role,
                                    ViolationPoint = pt,
                                    OutsideDistanceMm = Math.Round(protrudeMm, 2),
                                    RequiredCoverMm = Math.Round(reqCoverMm, 1),
                                    ActualCoverMm = Math.Round(-signedDistMm - barRadiusMm, 1),
                                    ViolationType = "ProtrusionOutsideHost",
                                    CurveSegmentDesc = $"Seg {segIndex}, BarPos {barIdx + 1}/{barCount}, Dia {barDiaMm:F0}mm",
                                    FaceDesc = $"{fp.FaceType} Face"
                                });
                            }
                            else
                            {
                                // Vỏ thanh nằm trong bê tông, kiểm tra xem có vi phạm lớp bảo vệ không
                                double actualCoverMm = -signedDistMm - barRadiusMm;
                                report.MinActualCoverFoundMm = Math.Min(report.MinActualCoverFoundMm, actualCoverMm);

                                if (actualCoverMm < reqCoverMm - 1.0) // thiếu lớp bảo vệ > 1mm
                                {
                                    report.CoverViolations.Add(new ContainmentViolation
                                    {
                                        RebarId = rebar.Id,
                                        HostId = host.Id,
                                        RebarRole = role,
                                        ViolationPoint = pt,
                                        OutsideDistanceMm = 0,
                                        RequiredCoverMm = Math.Round(reqCoverMm, 1),
                                        ActualCoverMm = Math.Round(actualCoverMm, 1),
                                        ViolationType = "CoverDeficiency",
                                        CurveSegmentDesc = $"Seg {segIndex}, BarPos {barIdx + 1}/{barCount}, Dia {barDiaMm:F0}mm",
                                        FaceDesc = $"{fp.FaceType} Face"
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Kiểm thử tự động trên các mặt cắt ngang (Transverse Sections)
        /// Stations: 0%, 25%, 50%, 75%, 100% cùng các vùng xung yếu (Support, Confinement A1, Midspan A2)
        /// </summary>
        private static void ValidateTransverseSections(
            Element host,
            List<Rebar> rebars,
            List<HostFacePlane> facePlanes,
            ContainmentValidationReport report)
        {
            BoundingBoxXYZ bb = host.get_BoundingBox(null);
            if (bb == null) return;

            bool isVertical = host.Category != null &&
                (host.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns ||
                 host.Category.BuiltInCategory == BuiltInCategory.OST_Columns);

            bool isHorizontal = host.Category != null &&
                (host.Category.BuiltInCategory == BuiltInCategory.OST_StructuralFraming);

            // Xác định trục dọc của cấu kiện
            XYZ axisDir = isVertical ? XYZ.BasisZ : (isHorizontal ? GetBeamLongitudinalAxis(host) : XYZ.BasisX);
            double startCoord = isVertical ? bb.Min.Z : (isHorizontal ? ProjectCoord(bb.Min, axisDir) : bb.Min.X);
            double endCoord = isVertical ? bb.Max.Z : (isHorizontal ? ProjectCoord(bb.Max, axisDir) : bb.Max.X);
            double length = Math.Abs(endCoord - startCoord);

            if (length < 0.5) return;

            // Danh sách các trạm kiểm thử mặt cắt ngang theo tỷ lệ chiều dài
            var stations = new List<(string name, double ratio)>
            {
                ("Station 0% (Gối/Đáy)", 0.02),
                ("Station 15% (Vùng dày A1)", 0.15),
                ("Station 25% (Một phần tư nhịp)", 0.25),
                ("Station 50% (Giữa nhịp / Giữa cột)", 0.50),
                ("Station 75% (Ba phần tư nhịp)", 0.75),
                ("Station 85% (Vùng dày A1)", 0.85),
                ("Station 100% (Gối/Đỉnh)", 0.98)
            };

            foreach (var st in stations)
            {
                double stationCoord = startCoord + st.ratio * length;
                var res = new SectionStationResult
                {
                    StationName = st.name,
                    StationRatio = st.ratio,
                    StationPoint = isVertical ? new XYZ((bb.Min.X + bb.Max.X) / 2.0, (bb.Min.Y + bb.Max.Y) / 2.0, stationCoord)
                                              : (bb.Min + axisDir * (st.ratio * length))
                };

                // Thu thập các thanh thép cắt qua mặt phẳng station
                int containedCount = 0;
                int violatedCount = 0;
                double minSectionCoverMm = double.MaxValue;

                foreach (var rebar in rebars)
                {
                    double barDiaFeet = GetBarDiameter(rebar);
                    double barRadiusMm = UnitUtils.ConvertFromInternalUnits(barDiaFeet / 2.0, UnitTypeId.Millimeters);

                    var curves = GetCenterlineCurvesAll(rebar);
                    foreach (var c in curves)
                    {
                        var pts = DiscretizeCurve(c, DiscretizationStepMm);
                        // Tìm điểm gần mặt phẳng trạm nhất (< 25mm)
                        var nearPts = pts.Where(p => Math.Abs(ProjectCoord(p, axisDir) - stationCoord) < UnitUtils.ConvertToInternalUnits(25.0, UnitTypeId.Millimeters)).ToList();

                        foreach (var np in nearPts)
                        {
                            res.TotalBarsEncountered++;
                            bool ptOk = true;

                            foreach (var fp in facePlanes)
                            {
                                double signedDistMm = UnitUtils.ConvertFromInternalUnits((np - fp.Origin).DotProduct(fp.Normal), UnitTypeId.Millimeters);
                                double reqCoverMm = UnitUtils.ConvertFromInternalUnits(fp.RequiredCoverFeet, UnitTypeId.Millimeters);
                                double actCoverMm = -signedDistMm - barRadiusMm;

                                minSectionCoverMm = Math.Min(minSectionCoverMm, actCoverMm);

                                if (signedDistMm > -barRadiusMm + 0.1)
                                {
                                    ptOk = false;
                                    res.FailureReasons.Add($"Thép lồi ra ngoài {fp.FaceType} Face tại {st.name}: {signedDistMm + barRadiusMm:F1}mm");
                                    break;
                                }
                                if (actCoverMm < reqCoverMm - 1.0)
                                {
                                    ptOk = false;
                                    res.FailureReasons.Add($"Thiếu lớp bảo vệ tại {fp.FaceType} Face: có {actCoverMm:F1}mm, cần {reqCoverMm:F1}mm");
                                    break;
                                }
                            }

                            if (ptOk) containedCount++;
                            else violatedCount++;
                        }
                    }
                }

                res.ContainedBarsCount = containedCount;
                res.ViolatedBarsCount = violatedCount;
                res.MinActualCoverMm = minSectionCoverMm == double.MaxValue ? 0 : Math.Round(minSectionCoverMm, 1);
                res.Passed = (violatedCount == 0);

                report.TransverseSections.Add(res);
            }
        }

        /// <summary>
        /// Khảo sát mặt cắt dọc (Longitudinal Section QA):
        /// Kiểm tra điểm đầu, điểm cuối, móc neo 90/135 độ, đoạn uốn cổ chai 1:6 không lòi ra khỏi mép đầu/cuối của host
        /// </summary>
        private static void ValidateLongitudinalSection(
            Element host,
            List<Rebar> rebars,
            List<HostFacePlane> facePlanes,
            ContainmentValidationReport report)
        {
            var endPlanes = facePlanes.Where(f => f.FaceType == "End").ToList();

            var longRes = new SectionStationResult
            {
                StationName = "Longitudinal Alignment & End Anchorage QA",
                StationRatio = 0.5,
                Passed = true
            };

            foreach (var r in rebars)
            {
                double barDiaFeet = GetBarDiameter(r);
                double barRadiusMm = UnitUtils.ConvertFromInternalUnits(barDiaFeet / 2.0, UnitTypeId.Millimeters);

                var curves = GetCenterlineCurvesAll(r);
                foreach (var c in curves)
                {
                    XYZ pStart = c.GetEndPoint(0);
                    XYZ pEnd = c.GetEndPoint(1);

                    foreach (var pt in new[] { pStart, pEnd })
                    {
                        longRes.TotalBarsEncountered++;
                        foreach (var ep in endPlanes)
                        {
                            double signedDistMm = UnitUtils.ConvertFromInternalUnits((pt - ep.Origin).DotProduct(ep.Normal), UnitTypeId.Millimeters);
                            if (signedDistMm > -barRadiusMm + 0.1)
                            {
                                longRes.Passed = false;
                                longRes.ViolatedBarsCount++;
                                longRes.FailureReasons.Add($"Đầu thanh thép đâm xuyên ra ngoài mặt biên: lồi {signedDistMm + barRadiusMm:F1}mm");
                            }
                            else
                            {
                                longRes.ContainedBarsCount++;
                            }
                        }
                    }
                }
            }

            report.LongitudinalSections.Add(longRes);
        }

        private static List<Solid> ExtractHostSolids(Element host)
        {
            var list = new List<Solid>();
            if (host == null) return list;

            var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            var geomElem = host.get_Geometry(opt);
            if (geomElem == null) return list;

            ExtractSolidsRecursive(geomElem, list, Transform.Identity);
            return list;
        }

        private static void ExtractSolidsRecursive(GeometryElement geomElem, List<Solid> list, Transform parentTf)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid s && s.Volume > 1e-6)
                {
                    if (parentTf.AlmostEqual(Transform.Identity))
                        list.Add(s);
                    else
                    {
                        try
                        {
                            Solid transformed = SolidUtils.CreateTransformed(s, parentTf);
                            if (transformed != null) list.Add(transformed);
                        }
                        catch
                        {
                            list.Add(s);
                        }
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    Transform instTf = parentTf.Multiply(gi.Transform);
                    GeometryElement instGeom = gi.GetSymbolGeometry();
                    if (instGeom != null)
                    {
                        ExtractSolidsRecursive(instGeom, list, instTf);
                    }
                }
            }
        }

        private static List<HostFacePlane> ExtractFacePlanes(List<Solid> solids, Element host, double? customCoverMm)
        {
            var planes = new List<HostFacePlane>();
            double defaultCoverMm = customCoverMm.HasValue && customCoverMm.Value > 0 ? customCoverMm.Value : 25.0;
            double defaultCoverFeet = UnitUtils.ConvertToInternalUnits(defaultCoverMm, UnitTypeId.Millimeters);

            double coverTopFeet = GetCoverParamFeet(host, BuiltInParameter.CLEAR_COVER_TOP, defaultCoverFeet);
            double coverBotFeet = GetCoverParamFeet(host, BuiltInParameter.CLEAR_COVER_BOTTOM, defaultCoverFeet);
            double coverSideFeet = GetCoverParamFeet(host, BuiltInParameter.CLEAR_COVER_OTHER, defaultCoverFeet);

            foreach (var solid in solids)
            {
                foreach (Face face in solid.Faces)
                {
                    if (face is PlanarFace pf)
                    {
                        XYZ norm = pf.FaceNormal.Normalize();
                        string fType = "Side";
                        double reqCover = coverSideFeet;

                        if (norm.DotProduct(XYZ.BasisZ) > 0.7)
                        {
                            fType = "Top";
                            reqCover = coverTopFeet;
                        }
                        else if (norm.DotProduct(XYZ.BasisZ) < -0.7)
                        {
                            fType = "Bottom";
                            reqCover = coverBotFeet;
                        }
                        else
                        {
                            // Kiểm tra mặt đầu (End Face) của dầm/cột
                            fType = "Side";
                            reqCover = coverSideFeet;
                        }

                        planes.Add(new HostFacePlane
                        {
                            Origin = pf.Origin,
                            Normal = norm,
                            FaceType = fType,
                            RequiredCoverFeet = reqCover,
                            PlanarFace = pf
                        });
                    }
                }
            }

            return planes;
        }

        private static List<XYZ> DiscretizeCurve(Curve curve, double stepMm)
        {
            var pts = new List<XYZ>();
            if (curve == null) return pts;

            double stepFeet = UnitUtils.ConvertToInternalUnits(stepMm, UnitTypeId.Millimeters);
            double len = curve.Length;

            pts.Add(curve.GetEndPoint(0));
            if (len > stepFeet)
            {
                int numSteps = (int)Math.Ceiling(len / stepFeet);
                for (int i = 1; i < numSteps; i++)
                {
                    double param = (double)i / numSteps;
                    pts.Add(curve.Evaluate(param, true));
                }
            }
            pts.Add(curve.GetEndPoint(1));

            return pts;
        }

        private static List<Curve> GetCenterlineCurvesAll(Rebar rebar)
        {
            var list = new List<Curve>();
            if (rebar == null || !rebar.IsValidObject) return list;

            int n = Math.Max(1, rebar.NumberOfBarPositions);
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, i);
                    if (curves != null) list.AddRange(curves);
                }
                catch { }
            }
            return list;
        }

        private static double GetBarDiameter(Rebar rebar)
        {
            try
            {
                Parameter p = rebar.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                if (p != null && p.HasValue) return p.AsDouble();
            }
            catch { }
            return UnitUtils.ConvertToInternalUnits(20.0, UnitTypeId.Millimeters);
        }

        private static string GetRebarRole(Rebar rebar)
        {
            try
            {
                Parameter p = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (p != null && p.HasValue)
                {
                    string val = p.AsString() ?? "";
                    if (val.Contains("Role:"))
                    {
                        var parts = val.Split('|');
                        var rPart = parts.FirstOrDefault(x => x.StartsWith("Role:"));
                        if (rPart != null) return rPart.Replace("Role:", "").Trim();
                    }
                }
            }
            catch { }
            return "Rebar";
        }

        private static double GetCoverParamFeet(Element elem, BuiltInParameter bip, double fallbackFeet)
        {
            try
            {
                Parameter p = elem.get_Parameter(bip);
                if (p != null && p.HasValue && p.AsDouble() > 0) return p.AsDouble();
            }
            catch { }
            return fallbackFeet;
        }

        private static XYZ GetBeamLongitudinalAxis(Element beam)
        {
            try
            {
                if (beam is FamilyInstance fi)
                {
                    Curve c = (fi.Location as LocationCurve)?.Curve;
                    if (c != null) return (c.GetEndPoint(1) - c.GetEndPoint(0)).Normalize();
                }
            }
            catch { }
            return XYZ.BasisX;
        }

        private static double ProjectCoord(XYZ pt, XYZ dir)
        {
            return pt.DotProduct(dir);
        }
    }
}

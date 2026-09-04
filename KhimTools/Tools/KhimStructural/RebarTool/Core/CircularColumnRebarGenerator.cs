using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public class CircularColumnRebarInput
    {
        public FamilyInstance Column { get; set; }
        public RebarBarType MainBarType { get; set; }
        public RebarBarType StirrupBarType { get; set; }
        public int MainBarQty { get; set; } = 8;
        public double StirrupSpacing { get; set; } = ToFeet(150); // mặc định @150 (fallback)
        public double ZoneA1Length { get; set; } = ToFeet(600);
        public double StirrupSpacingA1 { get; set; } = ToFeet(100);
        public double StirrupSpacingA2 { get; set; } = ToFeet(200);

        public bool HasDowel { get; set; } = true;
        public bool HasTopAnchor { get; set; } = true;

        public bool IsFoundationColumn { get; set; } = false;
        public bool IsTopRoofColumn { get; set; } = false;
        public bool EnableCrankedSplice { get; set; } = true;
        public double FootingAnchorMultiplier { get; set; } = 30;
        public double TopRoofHookLengthMultiplier { get; set; } = 12;

        public DesignCode DesignStandard { get; set; } = DesignCode.TCVN5574_2018;
        public ConcreteGrade ConcreteGrade { get; set; } = ConcreteGrade.Auto;
        public SteelGrade SteelGrade { get; set; } = SteelGrade.Auto;

        /// <summary>Lớp bê tông bảo vệ tùy chỉnh (feet). Nếu null sẽ lấy tự động từ Revit Host Cover.</summary>
        public double? CustomCoverFeet { get; set; }

        /// <summary>Hệ số nối chồng: Ls = multiplier × d (30d nén, 40d kéo).</summary>
        public double LapLengthMultiplier { get; set; } = 30;

        /// <summary>Nối so le 50%.</summary>
        public bool StaggeredSplice { get; set; } = true;

        /// <summary>Cột tầng trên kế tiếp (nếu có).</summary>
        public FamilyInstance AdjacentColumnAbove { get; set; }

        /// <summary>Cột tầng dưới kế tiếp (nếu có).</summary>
        public FamilyInstance AdjacentColumnBelow { get; set; }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }

    /// <summary>
    /// Sinh thép chủ + đai cho 1 cột tròn.
    /// </summary>
    public class CircularColumnRebarGenerator
    {
        private readonly Document _doc;

        public CircularColumnRebarGenerator(Document doc)
        {
            _doc = doc;
        }

        public List<Rebar> Generate(CircularColumnRebarInput input, RebarGenerationReport report = null)
        {
            var created = new List<Rebar>();

            var profile = CircularColumnGeometryHelper.GetCircularProfile(input.Column);
            double cover = (input.CustomCoverFeet.HasValue && input.CustomCoverFeet.Value > 0)
                ? input.CustomCoverFeet.Value
                : RebarCoverHelper.GetColumnCover(input.Column, RebarFace.Exterior);

            double stirrupDia = input.StirrupBarType.BarModelDiameter;
            double mainDia = input.MainBarType.BarModelDiameter;

            double outerRadius = profile.Diameter / 2.0;
            double mainBarRadius = outerRadius - cover - stirrupDia - mainDia / 2.0;
            double stirrupRadius = outerRadius - cover - stirrupDia / 2.0;

            if (mainBarRadius <= 0)
                throw new InvalidOperationException(
                    "Đường kính cột quá nhỏ so với cover + đường kính thép đã chọn. Kiểm tra lại D cột hoặc cỡ thép.");

            // Đã loại bỏ kiểm tra cảnh báo hàm lượng thép an toàn kết cấu theo yêu cầu
            created.AddRange(CreateMainBars(input, profile, mainBarRadius, report));
            created.AddRange(CreateStirrups(input, profile, stirrupRadius, report));

            // P0 Geometry-First Validation
            var containmentReport = RebarHostContainmentValidator.ValidateHostContainment(
                _doc, input.Column, created, input.CustomCoverFeet.HasValue ? UnitUtils.ConvertFromInternalUnits(input.CustomCoverFeet.Value, UnitTypeId.Millimeters) : (double?)null);
            if (!containmentReport.OverallPassed)
            {
                if (containmentReport.Protrusions.Any())
                {
                    var firstProt = containmentReport.Protrusions.First();
                    report?.AddError(input.Column, "Physical Bar Protrusion",
                        new InvalidOperationException($"P0 CRITICAL: Cốt thép cột tròn lòi ra ngoài bê tông {firstProt.OutsideDistanceMm:F1}mm tại {firstProt.FaceDesc}."));
                }
                if (containmentReport.CoverViolations.Any())
                {
                    report?.AddWarning($"Vi phạm lớp bảo vệ cột tròn: {containmentReport.CoverViolations.Count} vị trí. Min cover = {containmentReport.MinActualCoverFoundMm:F1}mm");
                }
            }

            RebarLifecycleManager.TagRebars(created, input.Column, "Column", "CircularColumn");
            report?.AddSuccess(created.Count);
            return created;
        }

        private List<Rebar> CreateMainBars(CircularColumnRebarInput input,
            CircularColumnGeometryHelper.ColumnProfile profile, double radius, RebarGenerationReport report = null)
        {
            var bars = new List<Rebar>();

            double percentFactor = input.StaggeredSplice
                ? (input.DesignStandard == DesignCode.TCVN5574_2018 ? 1.2 : 1.0)
                : 1.5;

            double mainDia = input.MainBarType.BarModelDiameter;
            double lapLength = RebarLapSpliceHelper.CalculateLapLength(
                mainDia,
                input.LapLengthMultiplier,
                input.ConcreteGrade,
                input.SteelGrade,
                AnchorageType.Compression,
                input.DesignStandard,
                percentFactor);

            // Tự động phát hiện cột trên / dưới nếu chưa được gán thủ công
            if (input.AdjacentColumnAbove == null && !input.IsTopRoofColumn)
            {
                var (_, autoAbove) = ColumnContinuityEngine.FindAdjacentColumns(_doc, input.Column);
                input.AdjacentColumnAbove = autoAbove;
            }
            if (input.AdjacentColumnBelow == null && !input.IsFoundationColumn)
            {
                var (autoBelow, _) = ColumnContinuityEngine.FindAdjacentColumns(_doc, input.Column);
                input.AdjacentColumnBelow = autoBelow;
            }

            var continuity = ColumnContinuityEngine.AnalyzeTransition(input.Column, input.AdjacentColumnAbove, mainDia, lapLength);
            bool isTopRoof = input.IsTopRoofColumn || (continuity.TransitionType == ColumnTransitionType.TopRoofTerminated);
            bool isLargeReduction = continuity.TransitionType == ColumnTransitionType.LargeReductionDoweled;

            double baseZBottom = (input.AdjacentColumnBelow != null)
                ? profile.BaseCenter.Z
                : profile.BaseCenter.Z - (input.IsFoundationColumn ? lapLength : (input.HasDowel ? ToFeet(400) : 0));

            double baseZTop = (!isTopRoof && !isLargeReduction)
                ? profile.TopCenter.Z + lapLength
                : profile.TopCenter.Z - ToFeet(25);

            for (int i = 0; i < input.MainBarQty; i++)
            {
                double angle = 2 * Math.PI * i / input.MainBarQty;
                double cosA = Math.Cos(angle);
                double sinA = Math.Sin(angle);

                double x = profile.BaseCenter.X + radius * cosA;
                double y = profile.BaseCenter.Y + radius * sinA;

                double zTop = baseZTop;
                if (!isTopRoof && !isLargeReduction && input.StaggeredSplice && (i % 2 == 1))
                {
                    zTop += continuity.StaggerOffsetFeet;
                }

                var curves = new List<Curve>();

                // 1. BASE FOOTING ANCHOR
                if (input.IsFoundationColumn && input.AdjacentColumnBelow == null)
                {
                    double footLegLen = RebarAnchorageCalculator.CalculateAnchorageLength(
                        UnitUtils.ConvertFromInternalUnits(mainDia, UnitTypeId.Millimeters),
                        input.ConcreteGrade,
                        input.SteelGrade,
                        AnchorageType.TensionStraight,
                        input.DesignStandard,
                        input.FootingAnchorMultiplier);
                    footLegLen = UnitUtils.ConvertToInternalUnits(footLegLen, UnitTypeId.Millimeters);

                    XYZ footStart = new XYZ(x - cosA * footLegLen, y - sinA * footLegLen, baseZBottom);
                    XYZ footCorner = new XYZ(x, y, baseZBottom);
                    curves.Add(Line.CreateBound(footStart, footCorner));
                }

                // 2. CRANKED 1:6 SPLICE AT JOINT (KHI CÓ CỘT TẦNG TRÊN VÀ ĐỘ THU TIẾT DIỆN <= 75mm)
                if (!isTopRoof && !isLargeReduction && input.EnableCrankedSplice)
                {
                    double inwardStep = continuity.MaxEdgeOffsetFeet > 0 ? (continuity.MaxEdgeOffsetFeet + mainDia) : mainDia;
                    double crankHeight = Math.Max(mainDia * 6.0, inwardStep * 6.0);
                    double crankZStart = profile.TopCenter.Z - Math.Min(crankHeight, ToFeet(500));
                    double crankZEnd = crankZStart + crankHeight;

                    double crankRadius = Math.Max(0.1, radius - inwardStep);
                    double crankX = profile.BaseCenter.X + crankRadius * cosA;
                    double crankY = profile.BaseCenter.Y + crankRadius * sinA;

                    XYZ pt1 = new XYZ(x, y, baseZBottom);
                    XYZ ptCrank1 = new XYZ(x, y, crankZStart);
                    XYZ ptCrank2 = new XYZ(crankX, crankY, crankZEnd);
                    XYZ ptTop = new XYZ(crankX, crankY, zTop);

                    curves.Add(Line.CreateBound(pt1, ptCrank1));
                    curves.Add(Line.CreateBound(ptCrank1, ptCrank2));
                    curves.Add(Line.CreateBound(ptCrank2, ptTop));
                }
                else
                {
                    XYZ pt1 = new XYZ(x, y, baseZBottom);
                    XYZ pt2 = new XYZ(x, y, zTop);
                    curves.Add(Line.CreateBound(pt1, pt2));
                }

                // 3. TOP HOOK 90° (KHI LÀ CỘT MÁI HOẶC ĐỘ THU TIẾT DIỆN > 75mm)
                if ((isTopRoof && input.HasTopAnchor) || isLargeReduction)
                {
                    double hookLen = RebarAnchorageCalculator.CalculateAnchorageLength(
                        UnitUtils.ConvertFromInternalUnits(mainDia, UnitTypeId.Millimeters),
                        input.ConcreteGrade,
                        input.SteelGrade,
                        AnchorageType.TensionHooked,
                        input.DesignStandard,
                        input.TopRoofHookLengthMultiplier);
                    hookLen = UnitUtils.ConvertToInternalUnits(hookLen, UnitTypeId.Millimeters);

                    XYZ hookStart = new XYZ(x, y, zTop);
                    XYZ hookEnd = new XYZ(x - cosA * hookLen, y - sinA * hookLen, zTop);
                    curves.Add(Line.CreateBound(hookStart, hookEnd));
                }

                if (curves.Any())
                {
                    XYZ norm = new XYZ(-sinA, cosA, 0);
                    if (curves.Count >= 2)
                    {
                        XYZ v1 = (curves[0].GetEndPoint(1) - curves[0].GetEndPoint(0)).Normalize();
                        XYZ v2 = (curves[1].GetEndPoint(1) - curves[1].GetEndPoint(0)).Normalize();
                        XYZ cross = v1.CrossProduct(v2);
                        if (cross.GetLength() > 0.01) norm = cross.Normalize();
                    }

                    Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                        _doc, RebarStyle.Standard, input.MainBarType, null, null, input.Column,
                        norm, curves, RebarHookOrientation.Left, RebarHookOrientation.Right);

                    if (bar != null)
                    {
                        bars.Add(bar);
                    }
                    else
                    {
                        XYZ pt1 = new XYZ(x, y, baseZBottom);
                        XYZ pt2 = new XYZ(x, y, zTop);
                        Rebar fallbackBar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Column, input.MainBarType, pt1, pt2);
                        if (fallbackBar != null)
                        {
                            bars.Add(fallbackBar);
                        }
                        else
                        {
                            report?.AddError(input.Column, "Thép chủ cột tròn (Main Rebar)", new InvalidOperationException("Không thể khởi tạo thanh thép chủ."));
                        }
                    }
                }
            }

            return bars;
        }

        public List<Rebar> GenerateMultiStory(List<CircularColumnRebarInput> inputs, RebarGenerationReport report = null)
        {
            var created = new List<Rebar>();
            if (inputs == null || !inputs.Any()) return created;

            for (int i = 0; i < inputs.Count; i++)
            {
                if (i > 0) inputs[i].AdjacentColumnBelow = inputs[i - 1].Column;
                if (i < inputs.Count - 1) inputs[i].AdjacentColumnAbove = inputs[i + 1].Column;

                created.AddRange(Generate(inputs[i], report));
            }

            return created;
        }

        private List<Rebar> CreateStirrups(CircularColumnRebarInput input,
            CircularColumnGeometryHelper.ColumnProfile profile, double radius, RebarGenerationReport report = null)
        {
            var hoops = new List<Rebar>();

            // Gap 2 & 7a: Phân vùng đai A1/A2/A1 + Vùng nút dầm-cột (Joint Core)
            double maxBeamDepthFeet = FindMaxIntersectingBeamDepth(input.Column, profile.TopCenter.Z);
            double zBeamBot = profile.TopCenter.Z - maxBeamDepthFeet;

            double clearHeight = Math.Max(zBeamBot - profile.BaseCenter.Z, 0);
            double l1 = Math.Max(input.ZoneA1Length, Math.Max(clearHeight / 6.0, profile.Diameter));
            double s1 = input.StirrupSpacingA1 > 0 ? input.StirrupSpacingA1 : (input.StirrupSpacing > 0 ? input.StirrupSpacing : ToFeet(100));
            double s2 = input.StirrupSpacingA2 > 0 ? input.StirrupSpacingA2 : ToFeet(200);

            List<double> zList = CalculateMultiZoneZCoordinates(profile.BaseCenter.Z, zBeamBot, profile.TopCenter.Z, l1, s1, s2);

            foreach (double z in zList)
            {
                XYZ center = new XYZ(profile.BaseCenter.X, profile.BaseCenter.Y, z);

                try
                {
                    Rebar hoop = CircularStirrupHelper.CreateHoop(
                        _doc, input.Column, input.StirrupBarType, center, radius, XYZ.BasisZ);

                    if (hoop != null) hoops.Add(hoop);
                }
                catch (Exception ex)
                {
                    report?.AddError(input.Column, "Đai cột tròn (Circular Hoop)", ex);
                }
            }

            return hoops;
        }

        private static List<double> CalculateMultiZoneZCoordinates(double zBase, double zBeamBot, double zTop, double l1, double s1, double s2)
        {
            var zList = new List<double>();
            double clearHeight = zBeamBot - zBase;
            if (clearHeight <= 0 || s1 <= 0) return zList;

            double zEndA1Bottom = Math.Min(zBase + l1, zBeamBot);
            double zStartA1Top = Math.Max(zBeamBot - l1, zBase);

            for (double z = zBase; z <= zEndA1Bottom + 0.001; z += s1) zList.Add(z);

            double lastZ = zList.LastOrDefault();
            if (lastZ <= 0) lastZ = zBase;

            for (double z = lastZ + s2; z < zStartA1Top - 0.001; z += s2) zList.Add(z);

            for (double z = zStartA1Top; z <= zBeamBot + 0.001; z += s1)
            {
                if (!zList.Any(existingZ => Math.Abs(existingZ - z) < 0.01)) zList.Add(z);
            }

            if (zTop > zBeamBot + 0.001)
            {
                for (double z = zBeamBot + s1; z <= zTop + 0.001; z += s1)
                {
                    if (!zList.Any(existingZ => Math.Abs(existingZ - z) < 0.01)) zList.Add(z);
                }
            }

            zList.Sort();
            return zList;
        }

        private double FindMaxIntersectingBeamDepth(FamilyInstance column, double topZ)
        {
            try
            {
                BoundingBoxXYZ colBb = column.get_BoundingBox(null);
                if (colBb == null) return 0;

                var beams = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .ToList();

                double maxDepth = 0;
                foreach (var bm in beams)
                {
                    BoundingBoxXYZ bmBb = bm.get_BoundingBox(null);
                    if (bmBb == null) continue;

                    if (bmBb.Max.Z >= topZ - ToFeet(1500) && bmBb.Min.Z <= topZ + ToFeet(300))
                    {
                        if (bmBb.Min.X <= colBb.Max.X + ToFeet(300) && bmBb.Max.X >= colBb.Min.X - ToFeet(300) &&
                            bmBb.Min.Y <= colBb.Max.Y + ToFeet(300) && bmBb.Max.Y >= colBb.Min.Y - ToFeet(300))
                        {
                            double depth = bmBb.Max.Z - bmBb.Min.Z;
                            if (depth > maxDepth) maxDepth = depth;
                        }
                    }
                }
                return maxDepth;
            }
            catch { return 0; }
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}

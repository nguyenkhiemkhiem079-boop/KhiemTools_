using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public class RectangularColumnRebarInput
    {
        public FamilyInstance Column { get; set; }
        public RebarBarType MainBarType { get; set; }
        public RebarBarType StirrupBarType { get; set; }

        /// <summary>Số thép chủ dọc theo cạnh B (tính cả 2 thanh góc), tối thiểu 2.</summary>
        public int BarsAlongB { get; set; } = 3;

        /// <summary>Số thép chủ dọc theo cạnh H (tính cả 2 thanh góc), tối thiểu 2.</summary>
        public int BarsAlongH { get; set; } = 3;

        /// <summary>Chiều dài vùng đai dầy A1 ở đỉnh và chân cột (feet).</summary>
        public double ZoneA1Length { get; set; } = ToFeet(600);

        /// <summary>Khoảng cách đai vùng dầy A1 (feet) - mặc định 100mm.</summary>
        public double StirrupSpacingA1 { get; set; } = ToFeet(100);

        /// <summary>Khoảng cách đai vùng thưa A2 (feet) - mặc định 200mm.</summary>
        public double StirrupSpacingA2 { get; set; } = ToFeet(200);

        /// <summary>Bật/Tắt đai thoi / đai lồng JP_T80 khi có từ 3 thanh chủ / cạnh.</summary>
        public bool HasInnerDiamondStirrup { get; set; } = true;

        /// <summary>Bật/Tắt đai móc phụ / crosslink JP_T68.</summary>
        public bool HasCrossLinks { get; set; } = true;

        public bool HasDowel { get; set; } = true;
        public bool HasTopAnchor { get; set; } = true;

        /// <summary>Cột tầng móng (bẻ chân vịt 90° neo vào đài móng/móng băng).</summary>
        public bool IsFoundationColumn { get; set; } = false;

        /// <summary>Cột tầng mái / đỉnh (bẻ móc 90° vào lòng cột/sàn mái - Ảnh 2).</summary>
        public bool IsTopRoofColumn { get; set; } = false;

        /// <summary>Bẻ bóp cổ chai 1:6 tại vị trí nối tầng (Ảnh 1).</summary>
        public bool EnableCrankedSplice { get; set; } = true;

        /// <summary>Chiều dài chân vịt neo móng (feet) - mặc định 30d.</summary>
        public double FootingAnchorMultiplier { get; set; } = 30;

        /// <summary>Chiều dài móc uốn đỉnh mái (feet) - mặc định 12d.</summary>
        public double TopRoofHookLengthMultiplier { get; set; } = 12;

        /// <summary>Lớp bê tông bảo vệ tùy chỉnh (feet). Nếu null sẽ lấy tự động từ Revit Host Cover.</summary>
        public double? CustomCoverFeet { get; set; }

        public DesignCode DesignStandard { get; set; } = DesignCode.TCVN5574_2018;
        public ConcreteGrade ConcreteGrade { get; set; } = ConcreteGrade.Auto;
        public SteelGrade SteelGrade { get; set; } = SteelGrade.Auto;

        /// <summary>Hệ số nối chồng fallback khi để Auto grade: Ls = multiplier × d (35d TCVN, 30d Eurocode).</summary>
        public double LapLengthMultiplier { get; set; } = 35;

        /// <summary>Nối so le 50% — nửa số thanh nối ở 1 cao độ, nửa còn lại cách 1.3×Ls.</summary>
        public bool StaggeredSplice { get; set; } = true;

        /// <summary>Cột tầng trên kế tiếp (nếu có) — dùng để tính vùng nối chồng tại đỉnh.</summary>
        public FamilyInstance AdjacentColumnAbove { get; set; }

        /// <summary>Cột tầng dưới kế tiếp (nếu có) — dùng để tính vùng nối chồng tại chân.</summary>
        public FamilyInstance AdjacentColumnBelow { get; set; }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }

    /// <summary>
    /// Sinh thép chủ + thép đai chữ nhật (JP_T51), đai thoi (JP_T80), móc đai (JP_T68)
    /// phân bố theo vùng A1 / A2 / A1 chuẩn kết cấu 100%.
    /// </summary>
    public class RectangularColumnRebarGenerator
    {
        private readonly Document _doc;
        public RectangularColumnRebarGenerator(Document doc) => _doc = doc;

        public List<Rebar> Generate(RectangularColumnRebarInput input, RebarGenerationReport report = null)
        {
            var created = new List<Rebar>();

            var profile = RectangularColumnGeometryHelper.GetRectangularProfile(input.Column);
            double cover = (input.CustomCoverFeet.HasValue && input.CustomCoverFeet.Value > 0)
                ? input.CustomCoverFeet.Value
                : RebarCoverHelper.GetColumnCover(input.Column, RebarFace.Exterior);

            double stirrupDia = input.StirrupBarType.BarModelDiameter;
            double mainDia = input.MainBarType.BarModelDiameter;

            // Bán kính/khoảng cách từ tâm cột đến tâm thép chủ & đai
            double halfB_main = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double halfH_main = profile.H / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double halfB_stirrup = profile.B / 2.0 - cover - stirrupDia / 2.0;
            double halfH_stirrup = profile.H / 2.0 - cover - stirrupDia / 2.0;

            if (halfB_main <= 0 || halfH_main <= 0)
                throw new InvalidOperationException(
                    "Tiết diện cột quá nhỏ so với lớp bảo vệ (cover) + đường kính thép đã chọn.");

            var mainPoints = BuildPerimeterPoints(halfB_main, halfH_main, input.BarsAlongB, input.BarsAlongH);

            // Đã loại bỏ kiểm tra cảnh báo hàm lượng thép an toàn kết cấu theo yêu cầu
            created.AddRange(CreateMainBars(input, profile, mainPoints, report));
            created.AddRange(CreateStirrups(input, profile, halfB_stirrup, halfH_stirrup));

            // P0 Geometry-First Validation với DetailingIntentContext (phân biệt nối cột tầng trên vs đâm thủng tự do)
            var intentType = input.AdjacentColumnAbove != null ? DetailingIntentType.ColumnContinuation : DetailingIntentType.TopTermination;
            var intentContext = new DetailingIntentContext(input.Column, input.AdjacentColumnAbove, intentType);
            var containmentReport = RebarHostContainmentValidator.ValidateHostContainmentWithIntent(
                _doc, input.Column, created, intentContext, input.CustomCoverFeet.HasValue ? UnitUtils.ConvertFromInternalUnits(input.CustomCoverFeet.Value, UnitTypeId.Millimeters) : (double?)null);
            if (!containmentReport.OverallPassed)
            {
                if (containmentReport.Protrusions.Any())
                {
                    var firstProt = containmentReport.Protrusions.First();
                    report?.AddError(input.Column, "Physical Bar Protrusion",
                        new InvalidOperationException($"P0 CRITICAL: Cốt thép cột lòi ra ngoài bê tông {firstProt.OutsideDistanceMm:F1}mm tại {firstProt.FaceDesc}."));
                }
                if (containmentReport.CoverViolations.Any())
                {
                    report?.AddWarning($"Vi phạm lớp bảo vệ cột: {containmentReport.CoverViolations.Count} vị trí. Min cover = {containmentReport.MinActualCoverFoundMm:F1}mm");
                }
            }

            RebarLifecycleManager.TagRebars(created, input.Column, "Column", "RectangularColumn");
            report?.AddSuccess(created.Count);
            return created;
        }

        private List<(double x, double y)> BuildPerimeterPoints(double halfB, double halfH, int barsB, int barsH)
        {
            barsB = Math.Max(barsB, 2);
            barsH = Math.Max(barsH, 2);
            var pts = new List<(double, double)>();

            // Cạnh trên (Y = +halfH)
            for (int i = 0; i < barsB; i++)
            {
                double t = (double)i / (barsB - 1);
                double x = -halfB + t * 2 * halfB;
                pts.Add((x, halfH));
            }

            // Cạnh dưới (Y = -halfH)
            for (int i = 0; i < barsB; i++)
            {
                double t = (double)i / (barsB - 1);
                double x = -halfB + t * 2 * halfB;
                pts.Add((x, -halfH));
            }

            // Cạnh trái & phải (bỏ các góc đã tạo)
            for (int i = 1; i < barsH - 1; i++)
            {
                double t = (double)i / (barsH - 1);
                double y = -halfH + t * 2 * halfH;
                pts.Add((-halfB, y));
                pts.Add((halfB, y));
            }

            return pts;
        }

        private List<Rebar> CreateMainBars(RectangularColumnRebarInput input,
            RectangularColumnGeometryHelper.ColumnProfile profile, List<(double x, double y)> localPoints, RebarGenerationReport report = null)
        {
            var bars = new List<Rebar>();

            double percentFactor = input.StaggeredSplice
                ? (input.DesignStandard == DesignCode.TCVN5574_2018 ? 1.2 : 1.0)
                : 1.5;

            double mainDia = input.MainBarType.BarModelDiameter;
            double fallbackMult = (input.LapLengthMultiplier > 0)
                ? input.LapLengthMultiplier
                : (input.DesignStandard == DesignCode.TCVN5574_2018 ? 35.0 : 30.0);

            double lapLength = RebarLapSpliceHelper.CalculateLapLength(
                mainDia,
                fallbackMult,
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
                : profile.TopCenter.Z - ToFeet(25); // sát mặt dưới nắp bê tông bảo vệ top

            for (int i = 0; i < localPoints.Count; i++)
            {
                var (lx, ly) = localPoints[i];
                double zTop = baseZTop;

                // Nối so le 50% ở đỉnh cột nếu có cột trên
                if (!isTopRoof && !isLargeReduction && input.StaggeredSplice && (i % 2 == 1))
                {
                    zTop += continuity.StaggerOffsetFeet;
                }

                var curves = new List<Curve>();
                double rot = profile.RotationRad;
                XYZ center = profile.BaseCenter;

                // Xác định mặt phẳng uốn bẻ 2D chuẩn (X-Z hoặc Y-Z) để đảm bảo đồng phẳng 100%
                bool bendAlongY = Math.Abs(ly) >= Math.Abs(lx);
                double dirX = lx > 0 ? -1 : 1;
                double dirY = ly > 0 ? -1 : 1;

                // --- 1. BASE FOOTING ANCHOR (NẾU LÀ CỘT MÓNG - Chân quỳ L bẻ vào tâm) ---
                double footLegLen = RebarAnchorageCalculator.CalculateAnchorageLength(
                    UnitUtils.ConvertFromInternalUnits(mainDia, UnitTypeId.Millimeters),
                    input.ConcreteGrade,
                    input.SteelGrade,
                    AnchorageType.TensionStraight,
                    input.DesignStandard,
                    input.FootingAnchorMultiplier);
                footLegLen = UnitUtils.ConvertToInternalUnits(footLegLen, UnitTypeId.Millimeters);

                if (input.IsFoundationColumn && input.AdjacentColumnBelow == null)
                {
                    double footLx = bendAlongY ? lx : lx + dirX * footLegLen;
                    double footLy = bendAlongY ? ly + dirY * footLegLen : ly;

                    XYZ footStart = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, footLx, footLy, baseZBottom - profile.BaseCenter.Z, center, rot);
                    XYZ footCorner = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, lx, ly, baseZBottom - profile.BaseCenter.Z, center, rot);
                    curves.Add(Line.CreateBound(footStart, footCorner));
                }

                // --- 2. CRANKED 1:6 SPLICE AT JOINT (KHI CÓ CỘT TẦNG TRÊN VÀ GIẢM TIẾT DIỆN <= 75mm) ---
                if (!isTopRoof && !isLargeReduction && input.EnableCrankedSplice)
                {
                    // Inward step tính theo độ giảm tiết diện thực tế + đường kính thanh thép
                    double inwardStep = continuity.MaxEdgeOffsetFeet > 0 ? (continuity.MaxEdgeOffsetFeet + mainDia) : mainDia;
                    double crankHeight = Math.Max(mainDia * 6.0, inwardStep * 6.0); // Tuân thủ nghiêm ngặt độ dốc 1:6 tối đa
                    double crankZStart = profile.TopCenter.Z - Math.Min(crankHeight, ToFeet(500));
                    double crankZEnd = crankZStart + crankHeight;

                    // Bẻ bóp vào trong tâm theo 1 phương duy nhất (đảm bảo đồng phẳng)
                    double crankLx = bendAlongY ? lx : (lx > 0 ? lx - inwardStep : lx + inwardStep);
                    double crankLy = bendAlongY ? (ly > 0 ? ly - inwardStep : ly + inwardStep) : ly;

                    XYZ pt1 = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, lx, ly, baseZBottom - profile.BaseCenter.Z, center, rot);
                    XYZ ptCrank1 = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, lx, ly, crankZStart - profile.BaseCenter.Z, center, rot);
                    XYZ ptCrank2 = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, crankLx, crankLy, crankZEnd - profile.BaseCenter.Z, center, rot);
                    XYZ ptTop = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, crankLx, crankLy, zTop - profile.BaseCenter.Z, center, rot);

                    curves.Add(Line.CreateBound(pt1, ptCrank1));
                    curves.Add(Line.CreateBound(ptCrank1, ptCrank2));
                    curves.Add(Line.CreateBound(ptCrank2, ptTop));
                }
                else
                {
                    // Thép dọc thẳng
                    XYZ pt1 = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, lx, ly, baseZBottom - profile.BaseCenter.Z, center, rot);
                    XYZ pt2 = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, lx, ly, zTop - profile.BaseCenter.Z, center, rot);
                    curves.Add(Line.CreateBound(pt1, pt2));
                }

                // --- 3. TOP HOOK 90° (KHI LÀ CỘT MÁI HOẶC ĐỘ THU TIẾT DIỆN > 75mm) ---
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

                    double hookEndLx = bendAlongY ? lx : lx + dirX * hookLen;
                    double hookEndLy = bendAlongY ? ly + dirY * hookLen : ly;

                    XYZ hookStart = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, lx, ly, zTop - profile.BaseCenter.Z, center, rot);
                    XYZ hookEnd = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, hookEndLx, hookEndLy, zTop - profile.BaseCenter.Z, center, rot);
                    curves.Add(Line.CreateBound(hookStart, hookEnd));
                }

                if (curves.Any())
                {
                    // Normal vector cho mặt phẳng thanh thép 2D
                    XYZ localNorm = bendAlongY ? new XYZ(1, 0, 0) : new XYZ(0, 1, 0);
                    XYZ worldNorm = RectangularColumnGeometryHelper.TransformLocalToWorld(input.Column, localNorm.X, localNorm.Y, 0, XYZ.Zero, rot).Normalize();
                    if (worldNorm.GetLength() < 0.01) worldNorm = XYZ.BasisX;

                    Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                        _doc, RebarStyle.Standard, input.MainBarType, null, null, input.Column,
                        worldNorm, curves, RebarHookOrientation.Left, RebarHookOrientation.Right);

                    if (bar != null)
                    {
                        // Gán tham số hình học phân đoạn Shape 11 / Shape 00 / VNDC_L1
                        var shapeParams = new Dictionary<string, double>
                        {
                            { "A", zTop - baseZBottom },
                            { "VNDC_L1", zTop - baseZBottom }
                        };
                        RebarShapeLibrary.ApplyShapeParameters(bar, shapeParams);

                        bars.Add(bar);
                    }
                    else
                    {
                        string errMsg = $"Không thể khởi tạo thanh thép uốn cổ chai/móc neo tại vị trí (lx={lx:F2}, ly={ly:F2}): Revit shape solver không giải được hình học. Tuyệt đối không thay thế bằng thanh thẳng sai thiết kế (NEED DESIGN INPUT).";
                        report?.AddError(input.Column, "Thép chủ cột chữ nhật (Main Rebar)", new InvalidOperationException(errMsg));
                    }
                }
            }

            return bars;
        }

        public List<Rebar> GenerateMultiStory(List<RectangularColumnRebarInput> inputs, RebarGenerationReport report = null)
        {
            var created = new List<Rebar>();
            if (inputs == null || !inputs.Any()) return created;

            for (int i = 0; i < inputs.Count; i++)
            {
                if (i > 0) inputs[i].AdjacentColumnBelow = inputs[i - 1].Column;
                if (i < inputs.Count - 1) inputs[i].AdjacentColumnAbove = inputs[i + 1].Column;
                else inputs[i].IsTopRoofColumn = true;

                created.AddRange(Generate(inputs[i], report));
            }

            return created;
        }

        private List<Rebar> CreateStirrups(RectangularColumnRebarInput input,
            RectangularColumnGeometryHelper.ColumnProfile profile, double halfB, double halfH, RebarGenerationReport report = null)
        {
            var hoops = new List<Rebar>();

            // Gap 2: Tìm dầm giao vào cột để xác định vùng nút dầm-cột (Joint Core)
            double maxBeamDepthFeet = FindMaxIntersectingBeamDepth(input.Column, profile.TopCenter.Z);
            double zBeamBot = profile.TopCenter.Z - maxBeamDepthFeet;

            double colHeight = profile.Height;
            double clearHeight = Math.Max(zBeamBot - profile.BaseCenter.Z, 0);

            double l1 = Math.Max(input.ZoneA1Length, Math.Max(clearHeight / 6.0, Math.Max(profile.B, profile.H)));
            double s1 = input.StirrupSpacingA1 > 0 ? input.StirrupSpacingA1 : ToFeet(100);
            double s2 = input.StirrupSpacingA2 > 0 ? input.StirrupSpacingA2 : ToFeet(200);

            // Tính danh sách cao độ Z bao gồm 3 vùng thông thủy (A1/A2/A1) + Vùng nút dầm-cột (Joint Core) với đai dày s1
            List<double> zList = CalculateMultiZoneZCoordinates(profile.BaseCenter.Z, zBeamBot, profile.TopCenter.Z, l1, s1, s2);

            int barsB = Math.Max(input.BarsAlongB, 2);
            int barsH = Math.Max(input.BarsAlongH, 2);

            foreach (double z in zList)
            {
                XYZ center = new XYZ(profile.BaseCenter.X, profile.BaseCenter.Y, z);

                // 1. Đai ngoài chữ nhật kín JP_T51 (Closed Tie với 2x 135° Hook)
                try
                {
                    Rebar outerHoop = RectangularStirrupHelper.CreateHoop(
                        _doc, input.Column, input.StirrupBarType, center, halfB, halfH, profile.RotationRad, XYZ.BasisZ);
                    if (outerHoop != null) hoops.Add(outerHoop);
                }
                catch (Exception ex)
                {
                    report?.AddError(input.Column, "Đai cột chữ nhật (Outer Hoop)", ex);
                }

                // 2. Đai thoi / đai lồng JP_T80 (khi cạnh B >= 3 và H >= 3)
                if (input.HasInnerDiamondStirrup && barsB >= 3 && barsH >= 3)
                {
                    try
                    {
                        Rebar diamondHoop = RectangularStirrupHelper.CreateDiamondHoop(
                            _doc, input.Column, input.StirrupBarType, center, halfB, halfH, profile.RotationRad, XYZ.BasisZ);
                        if (diamondHoop != null) hoops.Add(diamondHoop);
                    }
                    catch (Exception ex)
                    {
                        report?.AddError(input.Column, "Đai thoi cột (Diamond Hoop)", ex);
                    }
                }

                // 3. Thép 02 (C-link / Crosslink với 2x 180° Hook 180) cho các vị trí thép chủ giữa
                if (input.HasCrossLinks)
                {
                    // Các vị trí theo chiều B (nối Y = -halfH đến Y = +halfH)
                    for (int i = 1; i < barsB - 1; i++)
                    {
                        try
                        {
                            double t = (double)i / (barsB - 1);
                            double lx = -halfB + t * 2 * halfB;
                            Rebar linkH = RectangularStirrupHelper.CreateCrossLink(
                                _doc, input.Column, input.StirrupBarType, center, lx, -halfH, lx, halfH, profile.RotationRad, XYZ.BasisZ);
                            if (linkH != null) hoops.Add(linkH);
                        }
                        catch (Exception ex)
                        {
                            report?.AddError(input.Column, "Móc đai ngang B (Crosslink B)", ex);
                        }
                    }

                    // Các vị trí theo chiều H (nối X = -halfB đến X = +halfB)
                    for (int j = 1; j < barsH - 1; j++)
                    {
                        try
                        {
                            double t = (double)j / (barsH - 1);
                            double ly = -halfH + t * 2 * halfH;
                            Rebar linkB = RectangularStirrupHelper.CreateCrossLink(
                                _doc, input.Column, input.StirrupBarType, center, -halfB, ly, halfB, ly, profile.RotationRad, XYZ.BasisZ);
                            if (linkB != null) hoops.Add(linkB);
                        }
                        catch (Exception ex)
                        {
                            report?.AddError(input.Column, "Móc đai đứng H (Crosslink H)", ex);
                        }
                    }
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

            // Vùng 1: Chân cột A1 (dầy s1)
            for (double z = zBase; z <= zEndA1Bottom + 0.001; z += s1)
            {
                zList.Add(z);
            }

            // Vùng 2: Thân cột A2 (thưa s2)
            double lastZ = zList.LastOrDefault();
            if (lastZ <= 0) lastZ = zBase;

            for (double z = lastZ + s2; z < zStartA1Top - 0.001; z += s2)
            {
                zList.Add(z);
            }

            // Vùng 3: Đỉnh cột thông thủy A1 (dầy s1)
            for (double z = zStartA1Top; z <= zBeamBot + 0.001; z += s1)
            {
                if (!zList.Any(existingZ => Math.Abs(existingZ - z) < 0.01))
                {
                    zList.Add(z);
                }
            }

            // Vùng 4: Vùng nút dầm-cột Joint Core (từ zBeamBot -> zTop) với bước đai s1
            if (zTop > zBeamBot + 0.001)
            {
                for (double z = zBeamBot + s1; z <= zTop + 0.001; z += s1)
                {
                    if (!zList.Any(existingZ => Math.Abs(existingZ - z) < 0.01))
                    {
                        zList.Add(z);
                    }
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

                    // Kiểm tra dầm nằm gần vùng đỉnh cột Z
                    if (bmBb.Max.Z >= topZ - ToFeet(1500) && bmBb.Min.Z <= topZ + ToFeet(300))
                    {
                        // Kiểm tra va chạm mặt bằng XY
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
            catch
            {
                return 0;
            }
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
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
        public int BarsAlongB { get; set; } = 7;

        /// <summary>Số thép chủ dọc theo cạnh H (tính cả 2 thanh góc), tối thiểu 2.</summary>
        public int BarsAlongH { get; set; } = 3;

        /// <summary>Chiều dài vùng đai dầy A1 ở đỉnh và chân cột (feet).</summary>
        public double ZoneA1Length { get; set; } = ToFeet(600);

        /// <summary>Khoảng cách đai vùng dầy A1 (feet) - mặc định 100mm.</summary>
        public double StirrupSpacingA1 { get; set; } = ToFeet(100);

        /// <summary>Khoảng cách đai vùng thưa A2 (feet) - mặc định 200mm.</summary>
        public double StirrupSpacingA2 { get; set; } = ToFeet(200);

        /// <summary>Transverse reinforcement topology; MultiCellClosed is the normal detail.</summary>
        public ColumnTieLayoutType TieLayout { get; set; } = ColumnTieLayoutType.MultiCellClosed;

        /// <summary>Legacy diamond option; never enabled by the normal workflow.</summary>
        public bool HasInnerDiamondStirrup { get; set; } = false;

        /// <summary>Opt-in cross-tie option for legacy/advanced detailing.</summary>
        public bool HasCrossLinks { get; set; } = false;

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

        /// <summary>Hệ số nối chồng: Ls = multiplier × d (30d nén, 40d kéo, 45d đặc biệt).</summary>
        public double LapLengthMultiplier { get; set; } = 30;

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

        public List<Rebar> Generate(RectangularColumnRebarInput input, RebarGenerationReport report = null,
            IDictionary<string, string> roleByBarId = null)
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
            List<Rebar> mainBars = CreateMainBars(input, profile, mainPoints, report);
            created.AddRange(mainBars);
            RecordRoles(mainBars, "longitudinal", roleByBarId);
            created.AddRange(CreateStirrups(input, profile, halfB_stirrup, halfH_stirrup, report, roleByBarId));

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

            double mainDia = input.MainBarType.BarModelDiameter;
            double lapLength = RebarLapSpliceHelper.CalculateLapLength(
                mainDia,
                input.LapLengthMultiplier,
                input.ConcreteGrade,
                input.SteelGrade,
                AnchorageType.Compression,
                input.DesignStandard);

            double baseZBottom = (input.AdjacentColumnBelow != null)
                ? profile.BaseCenter.Z
                : profile.BaseCenter.Z - (input.IsFoundationColumn ? lapLength : (input.HasDowel ? ToFeet(400) : 0));

            bool isTopRoof = input.AdjacentColumnAbove == null || input.IsTopRoofColumn;
            double baseZTop = (!isTopRoof)
                ? profile.TopCenter.Z + lapLength
                : profile.TopCenter.Z - ToFeet(25); // sát mặt dưới nắp bê tông bảo vệ top

            for (int i = 0; i < localPoints.Count; i++)
            {
                var (lx, ly) = localPoints[i];
                double zTop = baseZTop;

                // Nối so le 50% ở đỉnh cột nếu có cột trên
                if (!isTopRoof && input.StaggeredSplice && (i % 2 == 1))
                {
                    zTop += 1.3 * lapLength;
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

                // --- 2. CRANKED 1:6 SPLICE AT JOINT (NẾU CÓ CỘT TẦNG TRÊN) ---
                if (!isTopRoof && input.EnableCrankedSplice)
                {
                    double crankZStart = profile.TopCenter.Z - ToFeet(100);
                    double crankHeight = mainDia * 6; // Độ dốc 1:6 chuẩn kỹ thuật
                    double crankZEnd = crankZStart + crankHeight;

                    // Bẻ bóp vào trong tâm theo 1 phương duy nhất (đảm bảo đồng phẳng)
                    double inwardStep = mainDia;
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

                // --- 3. TOP ROOF HOOK 90° (NẾU LÀ CỘT MÁI / KẾT THÚC) ---
                if (isTopRoof && input.HasTopAnchor)
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
                    if (bar == null)
                    {
                        string reason = "Không thể khởi tạo thanh thép chủ: " +
                            (RebarShapeCreationHelper.LastFailureReason ?? "Revit rejected the candidate.");
                        report?.AddError(input.Column, "Main Bar", new InvalidOperationException(reason));
                        throw new InvalidOperationException(reason);
                    }

                    // Assign only shape parameters explicitly declared by the solved
                    // shape, then regenerate and validate the resulting candidate again.
                    var shapeParams = new Dictionary<string, double>
                    {
                        { "A", zTop - baseZBottom },
                        { "VNDC_L1", zTop - baseZBottom }
                    };
                    RebarShapeLibrary.ApplyShapeParameters(bar, shapeParams);
                    string validationFailure;
                    if (!RebarShapeCreationHelper.TryValidateCreatedRebar(
                        _doc, bar, input.Column, RebarStyle.Standard, false,
                        "Main Bar", out validationFailure))
                    {
                        report?.AddError(input.Column, "Main Bar", new InvalidOperationException(validationFailure));
                        throw new InvalidOperationException(validationFailure);
                    }
                    bars.Add(bar);
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
                inputs[i].AdjacentColumnBelow = i > 0 &&
                    RebarLapSpliceHelper.AreConsecutiveColumns(inputs[i - 1].Column, inputs[i].Column)
                    ? inputs[i - 1].Column : null;
                inputs[i].AdjacentColumnAbove = i < inputs.Count - 1 &&
                    RebarLapSpliceHelper.AreConsecutiveColumns(inputs[i].Column, inputs[i + 1].Column)
                    ? inputs[i + 1].Column : null;
                inputs[i].IsTopRoofColumn = inputs[i].AdjacentColumnAbove == null;

                created.AddRange(Generate(inputs[i], report));
            }

            return created;
        }

        private List<Rebar> CreateStirrups(RectangularColumnRebarInput input,
            RectangularColumnGeometryHelper.ColumnProfile profile, double halfB, double halfH,
            RebarGenerationReport report = null, IDictionary<string, string> roleByBarId = null)
        {
            double maxBeamDepthFeet = FindMaxIntersectingBeamDepth(input.Column, profile.TopCenter.Z);
            double zBeamBot = Math.Max(profile.BaseCenter.Z,
                Math.Min(profile.TopCenter.Z, profile.TopCenter.Z - maxBeamDepthFeet));
            double clearHeight = zBeamBot - profile.BaseCenter.Z;
            if (clearHeight <= 0.01)
            {
                var error = new InvalidOperationException("Column has no clear height for transverse reinforcement.");
                report?.AddError(input.Column, "Distribution", error);
                throw error;
            }

            double l1 = input.ZoneA1Length > 0
                ? input.ZoneA1Length
                : Math.Max(clearHeight / 6.0, Math.Max(profile.B, profile.H));
            l1 = Math.Min(l1, clearHeight / 2.0);
            double s1 = input.StirrupSpacingA1 > 0 ? input.StirrupSpacingA1 : ToFeet(100);
            double s2 = input.StirrupSpacingA2 > 0 ? input.StirrupSpacingA2 : ToFeet(200);
            List<ColumnTieZone> zones = BuildTieZones(
                profile.BaseCenter.Z, zBeamBot, profile.TopCenter.Z, l1, s1, s2);
            if (zones.Count == 0)
            {
                var error = new InvalidOperationException("No valid A1/A2/A1 tie zones were calculated.");
                report?.AddError(input.Column, "Distribution", error);
                throw error;
            }

            var hoops = new List<Rebar>();
            foreach (ColumnTieZone zone in zones)
            {
                using (var stationTransaction = new SubTransaction(_doc))
                {
                    stationTransaction.Start();
                    try
                    {
                        XYZ center = new XYZ(profile.BaseCenter.X, profile.BaseCenter.Y, zone.StartZ);
                        List<Rebar> stationBars = CreateTieLoopsAtStation(input, profile, halfB, halfH, center, report);
                        if (stationBars.Count == 0)
                            throw new InvalidOperationException("No valid tie loop was created.");
                        foreach (Rebar tie in stationBars)
                        {
                            ApplyTieLayout(tie, zone);
                            RecordRole(tie, GetTieRole(tie, stationBars, input), roleByBarId);
                            string failure;
                            if (!RebarShapeCreationHelper.TryValidateCreatedRebar(
                                _doc, tie, input.Column, RebarStyle.StirrupTie, true,
                                zone.ZoneType + " " + GetTieRole(tie, stationBars, input), out failure))
                                throw new InvalidOperationException(failure);
                        }
                        TransactionStatus status = stationTransaction.Commit();
                        if (status != TransactionStatus.Committed)
                            throw new InvalidOperationException("Revit rolled back the " + zone.ZoneType + " tie set.");
                        hoops.AddRange(stationBars);
                    }
                    catch (Exception ex)
                    {
                        report?.AddError(input.Column, zone.ZoneType + " Tie Set", ex);
                        RebarShapeCreationHelper.RollbackCandidateOrThrow(
                            stationTransaction, zone.ZoneType + " tie station", ex);
                        throw;
                    }
                }
            }
            return hoops;
        }

        /// <summary>
        /// QA/debug path: creates exactly one outer, left-inner and right-inner tie
        /// at one elevation and commits them as one atomic station.
        /// </summary>
        public List<Rebar> CreateSingleTieStation(RectangularColumnRebarInput input, double z,
            RebarGenerationReport report = null)
        {
            if (input == null || input.Column == null) throw new ArgumentNullException("input");
            RectangularColumnGeometryHelper.ColumnProfile profile =
                RectangularColumnGeometryHelper.GetRectangularProfile(input.Column);
            double cover = input.CustomCoverFeet ?? RebarCoverHelper.GetColumnCover(input.Column, RebarFace.Exterior);
            double halfB = profile.B / 2.0 - cover - input.StirrupBarType.BarModelDiameter / 2.0;
            double halfH = profile.H / 2.0 - cover - input.StirrupBarType.BarModelDiameter / 2.0;
            if (halfB <= 0 || halfH <= 0) throw new InvalidOperationException("Column section is too small for the selected tie bar.");

            using (var stationTransaction = new SubTransaction(_doc))
            {
                stationTransaction.Start();
                try
                {
                    var bars = CreateTieLoopsAtStation(input, profile, halfB, halfH,
                        new XYZ(profile.BaseCenter.X, profile.BaseCenter.Y, z), report);
                    int expected = input.TieLayout == ColumnTieLayoutType.MultiCellClosed ? 3 : 1;
                    if (bars.Count != expected)
                        throw new InvalidOperationException("Expected " + expected + " tie loops but created " + bars.Count + ".");
                    _doc.Regenerate();
                    TransactionStatus status = stationTransaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit rolled back the single-station tie test.");
                    report?.AddSuccess(bars.Count);
                    return bars;
                }
                catch (Exception ex)
                {
                    report?.AddError(input.Column, "Single Tie Station", ex);
                    RebarShapeCreationHelper.RollbackCandidateOrThrow(
                        stationTransaction, "single tie station", ex);
                    return new List<Rebar>();
                }
            }
        }

        private List<Rebar> CreateTieLoopsAtStation(RectangularColumnRebarInput input,
            RectangularColumnGeometryHelper.ColumnProfile profile, double halfB, double halfH,
            XYZ center, RebarGenerationReport report)
        {
            var ties = new List<Rebar>();
            Rebar outer = RectangularStirrupHelper.CreateHoop(
                _doc, input.Column, input.StirrupBarType, center, halfB, halfH,
                profile.RotationRad, XYZ.BasisZ);
            if (outer == null)
                throw new InvalidOperationException("OuterTie: " + (RebarShapeCreationHelper.LastFailureReason ?? "creation failed."));
            ties.Add(outer);

            if (input.TieLayout == ColumnTieLayoutType.MultiCellClosed)
            {
                if (input.BarsAlongB < 5)
                    throw new InvalidOperationException("MultiCellClosed requires at least five longitudinal bars along B.");
                double cover = input.CustomCoverFeet ?? RebarCoverHelper.GetColumnCover(input.Column, RebarFace.Exterior);
                double mainHalfB = profile.B / 2.0 - cover - input.StirrupBarType.BarModelDiameter - input.MainBarType.BarModelDiameter / 2.0;
                var xLines = BuildLongitudinalXLines(mainHalfB, Math.Max(input.BarsAlongB, 2));
                int leftBoundary = Math.Max(1, (input.BarsAlongB - 1) / 3);
                int rightBoundary = Math.Min(input.BarsAlongB - 2, (input.BarsAlongB - 1) - leftBoundary);
                if (rightBoundary <= leftBoundary)
                    throw new InvalidOperationException("Longitudinal bar layout cannot define two inner cells.");

                Rebar left = RectangularStirrupHelper.CreateInnerClosedTie(
                    _doc, input.Column, input.StirrupBarType, center,
                    -halfB, xLines[leftBoundary], halfH, profile.RotationRad, XYZ.BasisZ, "InnerTieLeft");
                if (left == null)
                    throw new InvalidOperationException("InnerTieLeft: " + (RebarShapeCreationHelper.LastFailureReason ?? "creation failed."));
                ties.Add(left);

                Rebar right = RectangularStirrupHelper.CreateInnerClosedTie(
                    _doc, input.Column, input.StirrupBarType, center,
                    xLines[rightBoundary], halfB, halfH, profile.RotationRad, XYZ.BasisZ, "InnerTieRight");
                if (right == null)
                    throw new InvalidOperationException("InnerTieRight: " + (RebarShapeCreationHelper.LastFailureReason ?? "creation failed."));
                ties.Add(right);
            }
            else if (input.TieLayout == ColumnTieLayoutType.DiamondLegacy || input.HasInnerDiamondStirrup)
            {
                Rebar diamond = RectangularStirrupHelper.CreateDiamondHoop(
                    _doc, input.Column, input.StirrupBarType, center, halfB, halfH,
                    profile.RotationRad, XYZ.BasisZ);
                if (diamond == null)
                    throw new InvalidOperationException("DiamondLegacy: " + (RebarShapeCreationHelper.LastFailureReason ?? "creation failed."));
                ties.Add(diamond);
            }
            else if (input.TieLayout == ColumnTieLayoutType.CrossTie || input.HasCrossLinks)
            {
                int barsB = Math.Max(input.BarsAlongB, 2);
                for (int i = 1; i < barsB - 1; i++)
                {
                    double x = -halfB + (2.0 * halfB * i / (barsB - 1));
                    Rebar link = RectangularStirrupHelper.CreateCrossLink(
                        _doc, input.Column, input.StirrupBarType, center, x, -halfH, x, halfH,
                        profile.RotationRad, XYZ.BasisZ);
                    if (link == null) throw new InvalidOperationException("CrossTie: " + (RebarShapeCreationHelper.LastFailureReason ?? "creation failed."));
                    ties.Add(link);
                }
            }
            return ties;
        }

        private void ApplyTieLayout(Rebar tie, ColumnTieZone zone)
        {
            RebarShapeDrivenAccessor accessor = tie.GetShapeDrivenAccessor();
            if (accessor == null || !accessor.IsValidObject)
                throw new InvalidOperationException("Tie has no valid shape-driven accessor.");
            if (zone.StationCount <= 1)
                accessor.SetLayoutAsSingle();
            else
                accessor.SetLayoutAsNumberWithSpacing(zone.StationCount, zone.Spacing, true, true, true);
            _doc.Regenerate();
        }

        private static string GetTieRole(Rebar tie, IList<Rebar> stationBars, RectangularColumnRebarInput input)
        {
            int index = stationBars.IndexOf(tie);
            if (index == 0) return "outer-tie";
            if (input.TieLayout == ColumnTieLayoutType.MultiCellClosed) return index == 1 ? "inner-tie-left" : "inner-tie-right";
            if (input.TieLayout == ColumnTieLayoutType.DiamondLegacy || input.HasInnerDiamondStirrup) return "diamond-tie";
            return "cross-tie";
        }

        private static void RecordRoles(IEnumerable<Rebar> bars, string role, IDictionary<string, string> roleByBarId)
        {
            if (roleByBarId == null) return;
            foreach (Rebar bar in bars ?? Enumerable.Empty<Rebar>()) RecordRole(bar, role, roleByBarId);
        }

        private static void RecordRole(Rebar bar, string role, IDictionary<string, string> roleByBarId)
        {
            if (bar != null && roleByBarId != null)
                roleByBarId[bar.Id.Value.ToString(CultureInfo.InvariantCulture)] = role;
        }

        private static List<double> BuildLongitudinalXLines(double halfB, int barsB)
        {
            var lines = new List<double>();
            for (int i = 0; i < barsB; i++)
                lines.Add(-halfB + (2.0 * halfB * i / (barsB - 1)));
            return lines;
        }

        private static List<ColumnTieZone> BuildTieZones(double zBase, double zBeamBot, double zTop,
            double l1, double s1, double s2)
        {
            var zones = new List<ColumnTieZone>();
            double clearEnd = Math.Max(zBase, Math.Min(zBeamBot, zTop));
            if (clearEnd - zBase <= 0.01 || s1 <= 0 || s2 <= 0) return zones;
            var occupied = new List<double>();
            AddZone(zones, occupied, zBase, Math.Min(zBase + l1, clearEnd), s1, ColumnTieZoneType.BottomA1);
            AddZone(zones, occupied, Math.Min(zBase + l1, clearEnd), Math.Max(zBase + l1, clearEnd - l1), s2, ColumnTieZoneType.MiddleA2);
            AddZone(zones, occupied, Math.Max(zBase, clearEnd - l1), clearEnd, s1, ColumnTieZoneType.TopA1);
            if (zTop > clearEnd + 0.01)
                AddZone(zones, occupied, clearEnd, zTop, s1, ColumnTieZoneType.JointCore);
            return zones;
        }

        private static void AddZone(ICollection<ColumnTieZone> zones, ICollection<double> occupied,
            double start, double end, double spacing, ColumnTieZoneType type)
        {
            if (end < start + 0.01 || spacing <= 0) return;
            while (occupied.Any(z => Math.Abs(z - start) < 0.001)) start += spacing;
            if (start > end + 0.001) return;
            int count = (int)Math.Floor((end - start) / spacing + 1e-9) + 1;
            double actualEnd = start + (count - 1) * spacing;
            zones.Add(new ColumnTieZone
            {
                StartZ = start,
                EndZ = actualEnd,
                Spacing = spacing,
                ZoneType = type,
                StationCount = count
            });
            for (int i = 0; i < count; i++) occupied.Add(start + i * spacing);
        }

        private double FindMaxIntersectingBeamDepth(FamilyInstance column, double topZ)
        {
            try
            {
                BoundingBoxXYZ colBb = column.get_BoundingBox(null);
                if (colBb == null)
                    throw new InvalidOperationException("Rectangular-column bounds are unavailable while resolving the beam-column joint zone.");

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
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not resolve intersecting beam depth for the rectangular-column joint zone; tie generation was aborted.", ex);
            }
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}

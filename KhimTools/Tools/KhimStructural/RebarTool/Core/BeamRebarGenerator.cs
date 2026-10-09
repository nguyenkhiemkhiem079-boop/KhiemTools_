using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Workflow;

namespace KhimTools.RebarTool.Core
{
    public enum BeamEndCondition
    {
        Column,
        BeamIntersection,
        Cantilever,
        Unsupported
    }

    public class BeamRebarInput
    {
        public FamilyInstance Beam { get; set; }
        public RebarBarType MainTopBarType { get; set; }
        public RebarBarType MainBottomBarType { get; set; }
        public RebarBarType StirrupBarType { get; set; }
        public RebarBarType SideBarType { get; set; }

        public int TopContinuousQty { get; set; } = 2;
        public int BottomContinuousQty { get; set; } = 2;

        public int TopLeftExtraQty { get; set; } = 0;
        public RebarBarType TopLeftExtraBarType { get; set; }
        public int TopRightExtraQty { get; set; } = 0;
        public RebarBarType TopRightExtraBarType { get; set; }
        public int BottomMidExtraQty { get; set; } = 0;
        public RebarBarType BottomMidExtraBarType { get; set; }

        public bool AutoSideBars { get; set; } = false;
        public int SideBarQty { get; set; } = 0;
        /// <summary>
        /// TCVN 5574:2018 Điều 10.3.5.4: Ngưỡng chiều cao dầm tự động bật thép sườn (mặc định 700 mm).
        /// </summary>

        public int HangerStirrupQty { get; set; } = 3;
        public double HangerStirrupSpacingMm { get; set; } = 50.0;

        public double StirrupSpacingA1 { get; set; } = ToFeet(100);
        public double StirrupSpacingA2 { get; set; } = ToFeet(200);
        public double ZoneA1Length { get; set; } = 0; // If 0, defaults to L/4

        public double? CustomCoverFeet { get; set; }

        public DesignCode DesignStandard { get; set; } = DesignCode.TCVN5574_2018;
        public ConcreteGrade ConcreteGrade { get; set; } = ConcreteGrade.Auto;
        public SteelGrade SteelGrade { get; set; } = SteelGrade.Auto;

        public double LdMultiplier { get; set; } = 35;
        public double HookTailMultiplier { get; set; } = 12;

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }

    /// <summary>
    /// Sinh thép chủ (Top/Bottom/Side), thép tăng cường (Gối/Bụng) và thép đai A1/A2/A1 cho Dầm.
    /// </summary>
    public class BeamRebarGenerator
    {
        private readonly Document _doc;
        public BeamRebarGenerator(Document doc) => _doc = doc;

        private void RequireBarType(RebarBarType barType, string role)
        {
            if (barType == null || !barType.IsValidObject)
                throw new InvalidOperationException("Select a loaded RebarBarType for " + role + "; no bar type is substituted automatically.");
            if (barType.Document != _doc)
                throw new InvalidOperationException("The selected RebarBarType for " + role + " belongs to a different Revit document.");
            RequireFinitePositive(barType.BarModelDiameter, "Selected Beam bar model diameter for " + role);
        }

        private static void RequireFinitePositive(double value, string label)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new InvalidOperationException(label + " must be a finite positive value.");
        }

        private static bool TryGetMaximumAssignedCover(Element host, out double coverFeet)
        {
            RebarFace[] faces = { RebarFace.Top, RebarFace.Bottom, RebarFace.Exterior, RebarFace.Interior, RebarFace.Other };
            double[] assigned = faces.Select(face =>
            {
                double value;
                return RebarCoverHelper.TryGetFaceCover(host, face, out value) ? (double?)value : null;
            }).Where(value => value.HasValue && value.Value > 0).Select(value => value.Value).ToArray();
            if (assigned.Length == 0)
            {
                coverFeet = 0;
                return false;
            }
            coverFeet = assigned.Max();
            return true;
        }

        private static Rebar RequireCreated(Rebar bar, string role)
        {
            if (bar != null && bar.IsValidObject) return bar;
            string reason = RebarShapeCreationHelper.LastFailureReason;
            throw new InvalidOperationException("Revit did not create the required Beam " + role + "." +
                (string.IsNullOrWhiteSpace(reason) ? string.Empty : " " + reason));
        }

        public List<Rebar> Generate(BeamRebarInput input, RebarGenerationReport report = null,
            IDictionary<string, string> roleByBarId = null)
        {
            if (input?.Beam == null) throw new ArgumentException("A valid Structural Framing beam is required.", nameof(input));
            if (_doc == null || input.Beam.Document != _doc || !input.Beam.IsValidObject)
                throw new InvalidOperationException("The selected Beam must be valid and belong to the generator's active document.");
            if (input.Beam.Category == null || input.Beam.Category.BuiltInCategory != BuiltInCategory.OST_StructuralFraming)
                throw new InvalidOperationException("Beam reinforcement supports Structural Framing hosts only.");
            if (input.TopContinuousQty < 2 || input.BottomContinuousQty < 2)
                throw new InvalidOperationException("Beam reinforcement requires at least two continuous top bars and two continuous bottom bars.");
            if (input.TopLeftExtraQty < 0 || input.TopRightExtraQty < 0 || input.BottomMidExtraQty < 0)
                throw new InvalidOperationException("Beam additional-bar quantities must be non-negative.");
            if (input.TopLeftExtraQty > 0 || input.TopRightExtraQty > 0 || input.BottomMidExtraQty > 0)
                throw new InvalidOperationException("Beam extra-bar generation is disabled until the UI exposes validated curtailment and support-anchorage positions. The legacy L/3 and 15%-85% locations are not treated as engineering inputs.");
            if (input.SideBarQty < 0 || input.SideBarQty % 2 != 0)
                throw new InvalidOperationException("Beam side-bar quantity must be a non-negative even number because side bars are placed in symmetric pairs.");
            if (input.SideBarQty > 0)
                throw new InvalidOperationException("Beam side-bar generation is disabled until vertical layer spacing, support anchorage and preview settings are exposed and validated.");
            if (input.AutoSideBars)
                throw new InvalidOperationException("Automatic Beam side-bar generation is disabled until its detailing rules are exposed as validated production inputs.");
            if (input.HangerStirrupQty < 0 || (input.HangerStirrupQty > 0 &&
                (double.IsNaN(input.HangerStirrupSpacingMm) || double.IsInfinity(input.HangerStirrupSpacingMm) || input.HangerStirrupSpacingMm <= 0)))
                throw new InvalidOperationException("Beam hanger-stirrup quantity must be non-negative, and enabled hanger stirrups require a finite positive spacing.");
            RequireFinitePositive(input.StirrupSpacingA1, "Beam end-zone stirrup spacing");
            RequireFinitePositive(input.StirrupSpacingA2, "Beam middle-zone stirrup spacing");
            if (double.IsNaN(input.ZoneA1Length) || double.IsInfinity(input.ZoneA1Length) || input.ZoneA1Length < 0)
                throw new InvalidOperationException("Beam end-zone length must be finite and non-negative (zero selects the documented quarter-span default).");
            RequireFinitePositive(input.LdMultiplier, "Beam anchorage multiplier");
            RequireFinitePositive(input.HookTailMultiplier, "Beam hook-tail multiplier");
            if (input.CustomCoverFeet.HasValue) RequireFinitePositive(input.CustomCoverFeet.Value, "Beam concrete cover");

            RequireBarType(input.MainTopBarType, "continuous top bars");
            RequireBarType(input.MainBottomBarType, "continuous bottom bars");
            RequireBarType(input.StirrupBarType, "stirrups");

            var profile = BeamGeometryHelper.GetBeamProfile(input.Beam);

            var created = new List<Rebar>();
            var supportElements = new HashSet<Element>();

            double cover;
            if (input.CustomCoverFeet.HasValue) cover = input.CustomCoverFeet.Value;
            else if (!TryGetMaximumAssignedCover(input.Beam, out cover))
                throw new InvalidOperationException("No Revit concrete-cover assignment is available for this Beam. Assign a cover in Structural Settings or set an explicit supported cover before previewing.");

            double stirrupDia = input.StirrupBarType.BarModelDiameter;
            double topMainDia = input.MainTopBarType.BarModelDiameter;
            double botMainDia = input.MainBottomBarType.BarModelDiameter;

            double sectionTolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            List<BeamLongitudinalLayout.SectionBar> topLayout = BeamLongitudinalLayout.CreateSymmetricRow(
                profile.B, profile.H, cover, stirrupDia, topMainDia, input.TopContinuousQty, top: true, sectionTolerance);
            List<BeamLongitudinalLayout.SectionBar> bottomLayout = BeamLongitudinalLayout.CreateSymmetricRow(
                profile.B, profile.H, cover, stirrupDia, botMainDia, input.BottomContinuousQty, top: false, sectionTolerance);
            BeamLongitudinalLayout.RequireNoOverlap(topLayout.Concat(bottomLayout), sectionTolerance);

            double halfB = profile.B / 2.0 - cover - stirrupDia / 2.0;
            double halfH = profile.H / 2.0 - cover - stirrupDia / 2.0;

            if (halfB <= 0 || halfH <= 0)
                throw new InvalidOperationException("Tiết diện dầm quá nhỏ so với lớp bảo vệ đã chọn.");

            // Đã loại bỏ kiểm tra cảnh báo hàm lượng thép an toàn kết cấu theo yêu cầu.
            // 1. Thép chủ trên chạy suốt.
            List<Rebar> topContinuous = CreateTopContinuousBars(input, profile, topLayout, supportElements);
            created.AddRange(topContinuous);
            RecordRoles(topContinuous, "top-continuous", roleByBarId);

            // 2. Thép chủ dưới chạy suốt.
            List<Rebar> bottomContinuous = CreateBottomContinuousBars(input, profile, bottomLayout, supportElements);
            created.AddRange(bottomContinuous);
            RecordRoles(bottomContinuous, "bottom-continuous", roleByBarId);

            // 3. Thép tăng cường gối trái và gối phải (Top Extra).
            if (input.TopLeftExtraQty > 0)
            {
                List<Rebar> bars = CreateTopLeftExtraBars(input, profile, cover, stirrupDia, topMainDia);
                created.AddRange(bars);
                RecordRoles(bars, "top-left-extra", roleByBarId);
            }
            if (input.TopRightExtraQty > 0)
            {
                List<Rebar> bars = CreateTopRightExtraBars(input, profile, cover, stirrupDia, topMainDia);
                created.AddRange(bars);
                RecordRoles(bars, "top-right-extra", roleByBarId);
            }

            // 4. Thép tăng cường bụng (Bottom Mid Extra).
            if (input.BottomMidExtraQty > 0)
            {
                List<Rebar> bars = CreateBottomMidExtraBars(input, profile, cover, stirrupDia, botMainDia);
                created.AddRange(bars);
                RecordRoles(bars, "bottom-mid-extra", roleByBarId);
            }

            // 5. Thép đai phân vùng A1 / A2 / A1.
            List<double> normalStirrupStations;
            List<Rebar> stirrups = CreateBeamStirrups(input, profile, halfB, halfH, stirrupDia, out normalStirrupStations);
            created.AddRange(stirrups);
            RecordRoles(stirrups, "stirrup", roleByBarId);

            // 6. Thép đai treo chống giật tại vị trí dầm phụ giao dầm chính (Gap 7b).
            List<Rebar> hangerStirrups = CreateHangerStirrups(input, profile, halfB, halfH, stirrupDia, normalStirrupStations);
            created.AddRange(hangerStirrups);
            RecordRoles(hangerStirrups, "hanger-stirrup", roleByBarId);

            ValidateActualCenterlineContainment(input.Beam, supportElements, created);
            report?.AddSuccess(created.Count);
            return created;
        }

        private static void ValidateActualCenterlineContainment(Element beam,
            IEnumerable<Element> supports, IEnumerable<Rebar> rebars)
        {
            var hostSolids = new Dictionary<string, List<Solid>>(StringComparer.Ordinal);
            foreach (Element host in (supports ?? Enumerable.Empty<Element>()).Concat(new[] { beam })
                .Where(element => element != null && element.IsValidObject)
                .GroupBy(element => element.UniqueId, StringComparer.Ordinal).Select(group => group.First()))
            {
                var solids = new List<Solid>();
                CollectSolids(host.get_Geometry(new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine }), solids);
                hostSolids[host.UniqueId] = solids.Where(solid => solid != null && solid.Faces.Size > 0 && solid.Volume > 1e-9).ToList();
            }

            double toleranceFeet = UnitUtils.ConvertToInternalUnits(1.0, UnitTypeId.Millimeters);
            foreach (Rebar rebar in rebars ?? Enumerable.Empty<Rebar>())
            {
                if (rebar == null || !rebar.IsValidObject || rebar.GetHostId() != beam.Id)
                    throw new InvalidOperationException("Beam reinforcement contains an invalid Rebar or a Rebar hosted by another element.");
                int positionCount = Math.Max(1, rebar.NumberOfBarPositions);
                for (int position = 0; position < positionCount; position++)
                {
                    if (!rebar.DoesBarExistAtPosition(position)) continue;
                    IList<Curve> centerlines = rebar.GetCenterlineCurves(false, true, false,
                        MultiplanarOption.IncludeAllMultiplanarCurves, position);
                    if (centerlines == null || centerlines.Count == 0)
                        throw new InvalidOperationException("Beam Rebar centerline could not be inspected after Revit creation.");
                    foreach (Curve centerline in centerlines)
                    {
                        if (!(centerline is Line) && !(centerline is Arc))
                            throw new InvalidOperationException("Beam reinforcement currently validates straight and circular-arc centerline segments only.");
                        if (!IsCurveCoveredByHosts(centerline, hostSolids.Values.SelectMany(solids => solids), toleranceFeet))
                            throw new InvalidOperationException("Beam Rebar centerline leaves the verified Beam/support concrete solids; the generated set was rejected.");
                    }
                }
            }
        }

        private static bool IsCurveCoveredByHosts(Curve curve, IEnumerable<Solid> solids, double toleranceFeet)
        {
            double curveLength = curve.Length;
            if (double.IsNaN(curveLength) || double.IsInfinity(curveLength) || curveLength <= toleranceFeet) return false;
            double startParameter = curve.GetEndParameter(0);
            double endParameter = curve.GetEndParameter(1);
            double parameterSpan = endParameter - startParameter;
            if (Math.Abs(parameterSpan) <= 1e-12) return false;

            var intervals = new List<Tuple<double, double>>();
            foreach (Solid solid in solids ?? Enumerable.Empty<Solid>())
            {
                using (SolidCurveIntersection intersection = solid.IntersectWithCurve(curve, new SolidCurveIntersectionOptions()))
                {
                    for (int index = 0; index < intersection.SegmentCount; index++)
                    {
                        Curve segment = intersection.GetCurveSegment(index);
                        if (segment == null) continue;
                        IntersectionResult first = curve.Project(segment.GetEndPoint(0));
                        IntersectionResult second = curve.Project(segment.GetEndPoint(1));
                        if (first == null || second == null) continue;
                        double a = (first.Parameter - startParameter) / parameterSpan;
                        double b = (second.Parameter - startParameter) / parameterSpan;
                        intervals.Add(Tuple.Create(Math.Max(0, Math.Min(a, b)), Math.Min(1, Math.Max(a, b))));
                    }
                }
            }

            double parameterTolerance = Math.Min(0.01, toleranceFeet / curveLength);
            double coveredThrough = 0;
            foreach (Tuple<double, double> interval in intervals.OrderBy(item => item.Item1).ThenBy(item => item.Item2))
            {
                if (interval.Item2 < coveredThrough - parameterTolerance) continue;
                if (interval.Item1 > coveredThrough + parameterTolerance) return false;
                coveredThrough = Math.Max(coveredThrough, interval.Item2);
                if (coveredThrough >= 1 - parameterTolerance) return true;
            }
            return coveredThrough >= 1 - parameterTolerance;
        }

        private static void CollectSolids(GeometryElement geometry, IList<Solid> solids)
        {
            if (geometry == null) return;
            foreach (GeometryObject item in geometry)
            {
                Solid solid = item as Solid;
                if (solid != null) solids.Add(solid);
                else
                {
                    GeometryInstance instance = item as GeometryInstance;
                    if (instance != null) CollectSolids(instance.GetInstanceGeometry(), solids);
                }
            }
        }

        private static void RecordRoles(IEnumerable<Rebar> bars, string role, IDictionary<string, string> roleByBarId)
        {
            if (roleByBarId == null) return;
            foreach (Rebar bar in bars ?? Enumerable.Empty<Rebar>())
                if (bar != null) roleByBarId[bar.Id.Value.ToString(CultureInfo.InvariantCulture)] = role;
        }

        // ===== TOP CONTINUOUS =====

        private List<Rebar> CreateTopContinuousBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, IList<BeamLongitudinalLayout.SectionBar> layout,
            ICollection<Element> supportElements)
        {
            var bars = new List<Rebar>();
            foreach (BeamLongitudinalLayout.SectionBar position in layout ?? Array.Empty<BeamLongitudinalLayout.SectionBar>())
            {
                double x = position.X;
                double yTop = position.Y;
                XYZ startPoint = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, 0);
                XYZ endPoint = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, profile.Length);
                EndAnchorage startAnch = CalculateEndAnchorage(input, startPoint, profile.Direction, position.Diameter, true, supportElements);
                EndAnchorage endAnch = CalculateEndAnchorage(input, endPoint, profile.Direction, position.Diameter, false, supportElements);

                List<Curve> curves = BuildMainBarCurves(profile, x, yTop, startAnch, endAnch, true);

                Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                    _doc,
                    RebarStyle.Standard,
                    input.MainTopBarType,
                    null,
                    null,
                    input.Beam,
                    profile.RightVector, // Normal of the vertical bending plane
                    curves,
                    RebarHookOrientation.Right,
                    RebarHookOrientation.Right,
                    allowLegacyFallback: false);

                bars.Add(RequireCreated(bar, "continuous top bar"));
            }

            return bars;
        }

        // ===== BOTTOM CONTINUOUS =====

        private List<Rebar> CreateBottomContinuousBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, IList<BeamLongitudinalLayout.SectionBar> layout,
            ICollection<Element> supportElements)
        {
            var bars = new List<Rebar>();
            foreach (BeamLongitudinalLayout.SectionBar position in layout ?? Array.Empty<BeamLongitudinalLayout.SectionBar>())
            {
                double x = position.X;
                double yBot = position.Y;
                XYZ startPoint = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, 0);
                XYZ endPoint = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, profile.Length);
                EndAnchorage startAnch = CalculateEndAnchorage(input, startPoint, profile.Direction, position.Diameter, true, supportElements);
                EndAnchorage endAnch = CalculateEndAnchorage(input, endPoint, profile.Direction, position.Diameter, false, supportElements);

                List<Curve> curves = BuildMainBarCurves(profile, x, yBot, startAnch, endAnch, false);

                Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                    _doc,
                    RebarStyle.Standard,
                    input.MainBottomBarType,
                    null,
                    null,
                    input.Beam,
                    profile.RightVector,
                    curves,
                    RebarHookOrientation.Right,
                    RebarHookOrientation.Right,
                    allowLegacyFallback: false);

                bars.Add(RequireCreated(bar, "continuous bottom bar"));
            }

            return bars;
        }

        // ===== TOP EXTRA (LEFT & RIGHT) =====

        private List<Rebar> CreateTopLeftExtraBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = input.TopLeftExtraQty;
            double yTop = profile.H / 2.0 - cover - stirrupDia - mainDia / 2.0 - mainDia; // lớp 2

            double zStart = -ToFeet(300);
            double zEnd = profile.Length / 3.0; // cắt ở L/3

            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double stepX = (qty > 1) ? (2 * halfB * 0.6) / (qty - 1) : 0;
            double startX = (qty > 1) ? -halfB * 0.6 : 0;

            RebarBarType barType = input.TopLeftExtraBarType;

            for (int i = 0; i < qty; i++)
            {
                double x = startX + i * stepX;
                XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zStart);
                XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zEnd);

                Rebar bar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, barType, start, end);
                bars.Add(RequireCreated(bar, "left top extra bar"));
            }

            return bars;
        }

        private List<Rebar> CreateTopRightExtraBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = input.TopRightExtraQty;
            double yTop = profile.H / 2.0 - cover - stirrupDia - mainDia / 2.0 - mainDia;

            double zStart = profile.Length * 2.0 / 3.0; // bắt đầu từ 2L/3
            double zEnd = profile.Length + ToFeet(300);

            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double stepX = (qty > 1) ? (2 * halfB * 0.6) / (qty - 1) : 0;
            double startX = (qty > 1) ? -halfB * 0.6 : 0;

            RebarBarType barType = input.TopRightExtraBarType;

            for (int i = 0; i < qty; i++)
            {
                double x = startX + i * stepX;
                XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zStart);
                XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zEnd);

                Rebar bar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, barType, start, end);
                bars.Add(RequireCreated(bar, "right top extra bar"));
            }

            return bars;
        }

        // ===== BOTTOM MID EXTRA =====

        private List<Rebar> CreateBottomMidExtraBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = input.BottomMidExtraQty;
            double yBot = -profile.H / 2.0 + cover + stirrupDia + mainDia / 2.0 + mainDia;

            double zStart = profile.Length * 0.15; // cắt cách gối L/6
            double zEnd = profile.Length * 0.85;

            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double stepX = (qty > 1) ? (2 * halfB * 0.6) / (qty - 1) : 0;
            double startX = (qty > 1) ? -halfB * 0.6 : 0;

            RebarBarType barType = input.BottomMidExtraBarType;

            for (int i = 0; i < qty; i++)
            {
                double x = startX + i * stepX;
                XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, zStart);
                XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, zEnd);

                Rebar bar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, barType, start, end);
                bars.Add(RequireCreated(bar, "bottom extra bar"));
            }

            return bars;
        }

        // ===== STIRRUPS =====

        private List<Rebar> CreateBeamStirrups(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double halfB, double halfH,
            double stirrupDia, out List<double> stations)
        {
            var hoops = new List<Rebar>();
            double tolerance = ToFeet(1.0);
            stations = BeamStirrupLayout.CreateZoneStations(profile.Length, input.ZoneA1Length,
                input.StirrupSpacingA1, input.StirrupSpacingA2, ToFeet(50), tolerance);
            stations = BeamStirrupLayout.MergeAndValidate(stations, tolerance, stirrupDia);
            foreach (double z in stations)
            {
                Rebar hoop = CreateStirrupAtZ(input, profile, halfB, halfH, z);
                hoops.Add(RequireCreated(hoop, "stirrup at station " + z.ToString("R", CultureInfo.InvariantCulture)));
            }

            return hoops;
        }

        private List<Rebar> CreateHangerStirrups(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double halfB, double halfH,
            double stirrupDia, IList<double> normalStations)
        {
            var hoops = new List<Rebar>();

            // 2. Generate hanger stirrups (thép treo) at secondary-beam intersections (Gap 7b).
            var interPts = FindIntersectingSecondaryBeams(input.Beam);
            int qty = input.HangerStirrupQty;
            if (qty == 0) return hoops;
            double spacingFeet = ToFeet(input.HangerStirrupSpacingMm);

            double tolerance = ToFeet(1.0);
            List<double> centers = interPts
                .Select(point => (point - profile.StartPoint).DotProduct(profile.Direction))
                .Where(station => station > 0 && station < profile.Length)
                .ToList();
            List<double> candidates = BeamStirrupLayout.CreateHangerStations(centers, qty,
                spacingFeet, ToFeet(50), profile.Length - ToFeet(50));
            var normal = normalStations ?? Array.Empty<double>();
            List<double> combined = BeamStirrupLayout.MergeAndValidate(
                normal.Concat(candidates), tolerance, stirrupDia);
            List<double> uniqueHangers = combined.Where(station =>
                !normal.Any(normalStation => Math.Abs(normalStation - station) <= tolerance)).ToList();
            foreach (double hz in uniqueHangers)
            {
                Rebar hoop = CreateStirrupAtZ(input, profile, halfB, halfH, hz);
                hoops.Add(RequireCreated(hoop, "hanger stirrup at station " + hz.ToString("R", CultureInfo.InvariantCulture)));
            }

            return hoops;
        }

        private Rebar CreateStirrupAtZ(BeamRebarInput input, BeamGeometryHelper.BeamProfile profile, double halfB, double halfH, double z)
        {
            XYZ p1 = BeamGeometryHelper.TransformLocalToWorld(profile, halfB, halfH, z);
            XYZ p2 = BeamGeometryHelper.TransformLocalToWorld(profile, -halfB, halfH, z);
            XYZ p3 = BeamGeometryHelper.TransformLocalToWorld(profile, -halfB, -halfH, z);
            XYZ p4 = BeamGeometryHelper.TransformLocalToWorld(profile, halfB, -halfH, z);

            var loop = new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };

            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                _doc, RebarStyle.StirrupTie, input.StirrupBarType, null, null, input.Beam,
                profile.Direction, loop, RebarHookOrientation.Right, RebarHookOrientation.Right,
                allowLegacyFallback: false);
        }

        private sealed class SupportIntersection
        {
            public FamilyInstance Element { get; set; }
            public double MinAlong { get; set; }
            public double MaxAlong { get; set; }
        }

        private SupportIntersection FindSupportingColumn(XYZ point, XYZ direction)
        {
            var intersections = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Select(candidate => TryGetSupportIntersection(candidate, point, direction))
                .Where(intersection => intersection != null)
                .ToList();
            if (intersections.Count > 1)
                throw new InvalidOperationException("More than one structural column intersects the Beam end axis; support anchorage is ambiguous.");
            return intersections.SingleOrDefault();
        }

        private SupportIntersection FindSupportingBeam(XYZ point, XYZ direction, FamilyInstance currentBeam)
        {
            var intersections = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(candidate => candidate.Id != currentBeam.Id && candidate.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Beam)
                .Select(candidate => TryGetSupportIntersection(candidate, point, direction))
                .Where(intersection => intersection != null)
                .ToList();
            if (intersections.Count > 1)
                throw new InvalidOperationException("More than one structural beam intersects the Beam end axis; support anchorage is ambiguous.");
            return intersections.SingleOrDefault();
        }

        private SupportIntersection TryGetSupportIntersection(FamilyInstance candidate, XYZ point, XYZ direction)
        {
            if (candidate == null || !candidate.IsValidObject || direction == null) return null;
            const double searchLengthFeet = 100.0;
            double tolerance = UnitUtils.ConvertToInternalUnits(1.0, UnitTypeId.Millimeters);
            XYZ start = point - direction * searchLengthFeet;
            XYZ end = point + direction * searchLengthFeet;
            BoundingBoxXYZ bounds = candidate.get_BoundingBox(null);
            if (bounds != null &&
                (Math.Max(start.X, end.X) + tolerance < bounds.Min.X || Math.Min(start.X, end.X) - tolerance > bounds.Max.X ||
                 Math.Max(start.Y, end.Y) + tolerance < bounds.Min.Y || Math.Min(start.Y, end.Y) - tolerance > bounds.Max.Y ||
                 Math.Max(start.Z, end.Z) + tolerance < bounds.Min.Z || Math.Min(start.Z, end.Z) - tolerance > bounds.Max.Z))
                return null;
            Line axis = Line.CreateBound(start, end);
            var intervals = new List<Tuple<double, double>>();
            CollectIntersectedIntervals(candidate.get_Geometry(new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine
            }), axis, point, direction, intervals);
            if (intervals.Count == 0) return null;

            var merged = new List<Tuple<double, double>>();
            foreach (Tuple<double, double> interval in intervals.OrderBy(item => item.Item1).ThenBy(item => item.Item2))
            {
                if (merged.Count == 0 || interval.Item1 > merged[merged.Count - 1].Item2 + tolerance)
                    merged.Add(interval);
                else
                {
                    Tuple<double, double> prior = merged[merged.Count - 1];
                    merged[merged.Count - 1] = Tuple.Create(prior.Item1, Math.Max(prior.Item2, interval.Item2));
                }
            }

            Tuple<double, double> containingPoint = merged.SingleOrDefault(interval =>
                interval.Item1 <= tolerance && interval.Item2 >= -tolerance);
            if (containingPoint == null) return null;
            return new SupportIntersection
            {
                Element = candidate,
                MinAlong = containingPoint.Item1,
                MaxAlong = containingPoint.Item2
            };
        }

        private static void CollectIntersectedIntervals(GeometryElement geometry, Curve axis, XYZ origin,
            XYZ direction, IList<Tuple<double, double>> intervals)
        {
            if (geometry == null) return;
            foreach (GeometryObject item in geometry)
            {
                Solid solid = item as Solid;
                if (solid != null)
                {
                    if (solid.Faces.Size == 0 || solid.Volume <= 1e-9) continue;
                    using (SolidCurveIntersection intersection = solid.IntersectWithCurve(axis, new SolidCurveIntersectionOptions()))
                    {
                        for (int index = 0; index < intersection.SegmentCount; index++)
                        {
                            Curve segment = intersection.GetCurveSegment(index);
                            if (segment == null) continue;
                            double first = (segment.GetEndPoint(0) - origin).DotProduct(direction);
                            double second = (segment.GetEndPoint(1) - origin).DotProduct(direction);
                            intervals.Add(Tuple.Create(Math.Min(first, second), Math.Max(first, second)));
                        }
                    }
                }
                else
                {
                    GeometryInstance instance = item as GeometryInstance;
                    if (instance != null)
                        CollectIntersectedIntervals(instance.GetInstanceGeometry(), axis, origin, direction, intervals);
                }
            }
        }

        private class EndAnchorage
        {
            public double Extension { get; set; }
            public bool NeedsHook { get; set; } = false;
            public double HookLength { get; set; } = 0;
        }

        private EndAnchorage CalculateEndAnchorage(BeamRebarInput input, XYZ beamEndPt, XYZ direction,
            double barDia, bool atStart, ICollection<Element> supportElements)
        {
            var anchorage = new EndAnchorage();
            SupportIntersection column = FindSupportingColumn(beamEndPt, direction);
            SupportIntersection supportingBeam = column == null
                ? FindSupportingBeam(beamEndPt, direction, input.Beam)
                : null;
            if (column == null && supportingBeam == null)
                throw new InvalidOperationException("Beam end support could not be resolved from intersecting structural solid geometry. Cantilever, wall/slab support and unconnected ends require an explicitly supported detailing workflow.");

            SupportIntersection support = column ?? supportingBeam;
            supportElements?.Add(support.Element);
            double embedment = atStart ? -support.MinAlong : support.MaxAlong;
            if (double.IsNaN(embedment) || double.IsInfinity(embedment) || embedment <= 0)
                throw new InvalidOperationException("The detected support has no positive physical embedment length along the Beam end axis.");
            if (!TryGetMaximumAssignedCover(support.Element, out double supportCover))
                throw new InvalidOperationException("The detected support has no assigned Revit reinforcement cover; support anchorage cannot use an invented cover value.");

            double availLength = embedment - supportCover;
            if (availLength <= 0)
                throw new InvalidOperationException("The detected support is shallower than its assigned reinforcement cover and provides no available anchorage length.");
            double reqLd = RebarAnchorageCalculator.CalculateAnchorageLength(
                UnitUtils.ConvertFromInternalUnits(barDia, UnitTypeId.Millimeters),
                input.ConcreteGrade,
                input.SteelGrade,
                AnchorageType.TensionStraight,
                input.DesignStandard,
                input.LdMultiplier);
            reqLd = UnitUtils.ConvertToInternalUnits(reqLd, UnitTypeId.Millimeters);

            double reqHookLd = RebarAnchorageCalculator.CalculateAnchorageLength(
                UnitUtils.ConvertFromInternalUnits(barDia, UnitTypeId.Millimeters),
                input.ConcreteGrade,
                input.SteelGrade,
                AnchorageType.TensionHooked,
                input.DesignStandard,
                input.LdMultiplier);
            reqHookLd = UnitUtils.ConvertToInternalUnits(reqHookLd, UnitTypeId.Millimeters);

            if (availLength >= reqLd)
            {
                anchorage.Extension = reqLd;
                anchorage.NeedsHook = false;
            }
            else
            {
                anchorage.Extension = availLength;
                anchorage.NeedsHook = true;
                anchorage.HookLength = Math.Max(reqHookLd - availLength, barDia * input.HookTailMultiplier);
            }

            return anchorage;
        }

        private List<Curve> BuildMainBarCurves(BeamGeometryHelper.BeamProfile profile, double x, double y,
            EndAnchorage startAnch, EndAnchorage endAnch, bool isTop)
        {
            var curves = new List<Curve>();

            double zStart = -Math.Max(startAnch.Extension, 0);
            double zEnd = profile.Length + Math.Max(endAnch.Extension, 0);

            XYZ pStart = BeamGeometryHelper.TransformLocalToWorld(profile, x, y, zStart);
            XYZ pEnd = BeamGeometryHelper.TransformLocalToWorld(profile, x, y, zEnd);

            XYZ startCorner = pStart;
            XYZ endCorner = pEnd;

            // Start Hook (Left support)
            if (startAnch.NeedsHook && startAnch.HookLength > 0.01)
            {
                XYZ hookDir = isTop ? -profile.UpVector : profile.UpVector;
                XYZ hookStart = startCorner + startAnch.HookLength * hookDir;
                if (hookStart.DistanceTo(startCorner) > 0.01)
                {
                    curves.Add(Line.CreateBound(hookStart, startCorner));
                }
            }

            // Main Straight Segment
            curves.Add(Line.CreateBound(startCorner, endCorner));

            // End Hook (Right support)
            if (endAnch.NeedsHook && endAnch.HookLength > 0.01)
            {
                XYZ hookDir = isTop ? -profile.UpVector : profile.UpVector;
                XYZ hookEnd = endCorner + endAnch.HookLength * hookDir;
                if (endCorner.DistanceTo(hookEnd) > 0.01)
                {
                    curves.Add(Line.CreateBound(endCorner, hookEnd));
                }
            }

            return curves;
        }

        private List<XYZ> FindIntersectingSecondaryBeams(FamilyInstance primaryBeam,
            ICollection<FamilyInstance> intersectingSecondaryBeams = null)
        {
            var intersectionPoints = new List<XYZ>();
            var beams = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            LocationCurve primLoc = primaryBeam.Location as LocationCurve;
            if (primLoc == null || primLoc.Curve == null) return intersectionPoints;

            Curve primCurve = primLoc.Curve;

            foreach (var bm in beams)
            {
                if (bm.Id == primaryBeam.Id) continue;

                LocationCurve secLoc = bm.Location as LocationCurve;
                if (secLoc == null || secLoc.Curve == null) continue;

                Curve secCurve = secLoc.Curve;

                IntersectionResultArray results;
                SetComparisonResult intersect = primCurve.Intersect(secCurve, out results);

                if (intersect == SetComparisonResult.Overlap && results != null)
                {
                    bool foundIntersection = false;
                    foreach (IntersectionResult r in results)
                    {
                        intersectionPoints.Add(r.XYZPoint);
                        foundIntersection = true;
                    }
                    if (foundIntersection) intersectingSecondaryBeams?.Add(bm);
                }
            }
            return intersectionPoints;
        }

        /// <summary>
        /// Captures every structural column and beam candidate because an offset
        /// longitudinal bar can intersect support geometry that the Beam centerline
        /// does not. This deliberately favors conservative staleness over a missed
        /// support change; solver-time support resolution still uses exact solids.
        /// </summary>
        internal string GetReinforcementContextFingerprint(FamilyInstance beam)
        {
            if (beam == null) throw new ArgumentNullException(nameof(beam));
            BoundingBoxXYZ beamBounds = beam.get_BoundingBox(null);
            if (beamBounds == null)
                throw new InvalidOperationException("Beam bounds are unavailable while fingerprinting support geometry; preview cannot be trusted.");

            double padding = UnitUtils.ConvertToInternalUnits(1.0, UnitTypeId.Millimeters);
            var searchOutline = new Outline(
                beamBounds.Min - new XYZ(padding, padding, padding),
                beamBounds.Max + new XYZ(padding, padding, padding));
            IEnumerable<Element> candidates = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .Concat(new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(candidate => candidate.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Beam));
            string[] candidateStates = candidates
                .Where(candidate => candidate.Id == beam.Id || HasUnknownOrIntersectingBounds(candidate, searchOutline))
                .OrderBy(candidate => candidate.UniqueId, StringComparer.Ordinal)
                .Select(GetSupportCandidateState)
                .ToArray();
            string[] coverTypeStates = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarCoverType)).Cast<RebarCoverType>()
                .OrderBy(candidate => candidate.UniqueId, StringComparer.Ordinal)
                .Select(candidate => string.Join(":", candidate.UniqueId, candidate.VersionGuid.ToString("D"),
                    candidate.CoverDistance.ToString("R", CultureInfo.InvariantCulture)))
                .ToArray();

            return WorkflowFingerprint.Compute(candidateStates.Concat(coverTypeStates));
        }

        private static bool HasUnknownOrIntersectingBounds(Element candidate, Outline outline)
        {
            BoundingBoxXYZ bounds = candidate.get_BoundingBox(null);
            return bounds == null ||
                (bounds.Min.X <= outline.MaximumPoint.X && bounds.Max.X >= outline.MinimumPoint.X &&
                 bounds.Min.Y <= outline.MaximumPoint.Y && bounds.Max.Y >= outline.MinimumPoint.Y &&
                 bounds.Min.Z <= outline.MaximumPoint.Z && bounds.Max.Z >= outline.MinimumPoint.Z);
        }

        private string GetSupportCandidateState(Element candidate)
        {
            var values = new List<string>
            {
                candidate.UniqueId,
                candidate.VersionGuid.ToString("D"),
                FormatElementId(candidate.GetTypeId()),
                FormatBounds(candidate.get_BoundingBox(null)),
                FormatLocation(candidate.Location),
                FormatSolidGeometry(candidate)
            };
            foreach (RebarFace face in new[] { RebarFace.Top, RebarFace.Bottom, RebarFace.Exterior, RebarFace.Interior, RebarFace.Other })
            {
                bool hasCover = RebarCoverHelper.TryGetFaceCover(candidate, face, out double coverFeet);
                values.Add(face + ":" + hasCover + ":" + coverFeet.ToString("R", CultureInfo.InvariantCulture));
            }

            Element type = _doc.GetElement(candidate.GetTypeId());
            if (type != null)
            {
                values.Add(type.UniqueId);
                values.Add(type.VersionGuid.ToString("D"));
            }
            return WorkflowFingerprint.Compute(values);
        }

        private static string FormatElementId(ElementId id) => id == null ? string.Empty : id.Value.ToString(CultureInfo.InvariantCulture);

        private static string FormatBounds(BoundingBoxXYZ bounds)
        {
            return bounds == null ? "no-bounds" : FormatPoint(bounds.Min) + ":" + FormatPoint(bounds.Max);
        }

        private static string FormatLocation(Location location)
        {
            LocationCurve curveLocation = location as LocationCurve;
            if (curveLocation?.Curve != null)
                return "curve:" + string.Join(";", curveLocation.Curve.Tessellate().Select(FormatPoint));
            LocationPoint pointLocation = location as LocationPoint;
            if (pointLocation != null)
                return "point:" + FormatPoint(pointLocation.Point) + ":" + pointLocation.Rotation.ToString("R", CultureInfo.InvariantCulture);
            return "no-location";
        }

        private static string FormatSolidGeometry(Element element)
        {
            var solids = new List<Solid>();
            CollectSolids(element.get_Geometry(new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine }), solids);
            string[] solidStates = solids.Where(solid => solid != null && solid.Faces.Size > 0 && solid.Volume > 1e-9)
                .Select(solid =>
                {
                    string[] edges = solid.Edges.Cast<Edge>().Select(edge =>
                    {
                        string[] points = edge.AsCurve().Tessellate().Select(FormatPoint).ToArray();
                        string forward = string.Join(";", points);
                        string reverse = string.Join(";", points.Reverse());
                        return string.CompareOrdinal(forward, reverse) <= 0 ? forward : reverse;
                    }).OrderBy(signature => signature, StringComparer.Ordinal).ToArray();
                    string[] faces = solid.Faces.Cast<Face>().Select(face =>
                    {
                        PlanarFace planar = face as PlanarFace;
                        string plane = planar == null ? string.Empty : FormatPoint(planar.Origin) + ":" + FormatPoint(planar.FaceNormal);
                        return face.GetType().FullName + ":" + face.Area.ToString("R", CultureInfo.InvariantCulture) + ":" + plane;
                    }).OrderBy(signature => signature, StringComparer.Ordinal).ToArray();
                    return solid.Volume.ToString("R", CultureInfo.InvariantCulture) + ":" +
                        string.Join("|", faces) + ":" + string.Join("|", edges);
                }).OrderBy(signature => signature, StringComparer.Ordinal).ToArray();
            return string.Join("#", solidStates);
        }

        private static string FormatPoint(XYZ point) =>
            point.X.ToString("R", CultureInfo.InvariantCulture) + "," +
            point.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
            point.Z.ToString("R", CultureInfo.InvariantCulture);

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}

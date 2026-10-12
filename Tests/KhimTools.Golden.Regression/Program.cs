using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KhimTools.Domain.Models.Architectural;
using KhimTools.Domain.Models.Mep;
using KhimTools.Domain.Models.Qs;
using KhimTools.RebarTool.Core;

static class Program
{
    private static int _checks;

    private sealed class SemanticBar
    {
        public string Type { get; set; }
        public string Host { get; set; }
        public string Orientation { get; set; }
        public int Count { get; set; }
        public double DiameterMm { get; set; }
        public double SpacingMm { get; set; }
        public double CoverMm { get; set; }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Golden regression failed: " + label);
        _checks++;
    }

    private static bool Near(double expected, double actual, double tolerance) =>
        double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance;

    private static void Main()
    {
        RunDomainGoldens();
        RunRebarCalculationGoldens();
        RunRebarPreviewDuplicateCases();
        RunSlabSectionGeometryCases();
        RunBeamStirrupLayoutCases();
        RunBeamManualPlanCases();
        RunComparatorContract();
        RunEdgeCases();
        RunSyntheticStress();
        Console.WriteLine($"GOLDEN_REGRESSION_ACCEPTANCE=PASS ({_checks} assertions)");
        Console.WriteLine("REBAR_DOMAIN_GOLDEN_ACCEPTANCE=PASS (production anchorage/lap calculator)");
        Console.WriteLine("EDGE_CASE_ACCEPTANCE=PASS (QS/MEP/Architectural domain calculations)");
        Console.WriteLine("REBAR_DOMAIN_EDGE_ACCEPTANCE=PASS (anchorage/lap calculator validation)");
        Console.WriteLine("REBAR_GEOMETRY_EDGE_CASE_STATUS=HOST_REQUIRED / NOT_EXECUTED");
        Console.WriteLine("PERFORMANCE_ACCEPTANCE=PASS (pure planning/calculation only)");
        Console.WriteLine("STRESS_ACCEPTANCE=PASS (100, 1,000, 10,000 synthetic records)");
        Console.WriteLine("REBAR_HOST_GEOMETRY_GOLDEN=HOST_REQUIRED / NOT_EXECUTED");
    }

    private static void RunRebarPreviewDuplicateCases()
    {
        string[] expected = { "host-A:type-16:line-1", "host-A:type-16:line-2", "host-A:type-16:line-2" };
        Check(RebarFingerprintMultiset.Contains(expected,
            new[] { expected[2], expected[0], expected[1] }), "Exact slab plan matches as an order-independent multiset");
        Check(!RebarFingerprintMultiset.Contains(expected,
            new[] { expected[0], "host-A:unrelated-line" }), "A shared planned set plus unrelated rebar is not a complete duplicate");
        Check(!RebarFingerprintMultiset.Contains(expected,
            new[] { expected[0], expected[1], "host-A:type-16:line-3" }), "Duplicate matching preserves planned multiplicity");
        Check(!RebarFingerprintMultiset.Contains(expected,
            new[] { "host-B:type-16:line-1", "host-B:type-16:line-2", "host-B:type-16:line-2" }),
            "Identical geometry hosted by another slab is not a duplicate");
        Check(RebarFingerprintMultiset.Contains(expected,
            expected.Concat(new[] { "host-A:unrelated-existing-reinforcement" })),
            "A complete planned duplicate remains detectable alongside unrelated reinforcement");
        Check(!RebarFingerprintMultiset.Contains(Array.Empty<string>(), expected), "An empty preview cannot be classified as a duplicate");
    }

    private static void RunSlabSectionGeometryCases()
    {
        var xCoplanar = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-2, 0, 1), new SlabSectionPoint(2, 0, 1)
        }, SlabSectionAxis.SectionX, 0, "bottom-x");
        Check(xCoplanar.Count == 1, "Section X coplanar X bar returns one primitive");
        Check(xCoplanar[0].Kind == SlabSectionPrimitiveKind.CoplanarSegment, "Section X coplanar X bar remains a line");
        Check(Near(-2, xCoplanar[0].StartAlong, 1e-9) && Near(2, xCoplanar[0].EndAlong, 1e-9), "Section X line uses actual projected endpoints");
        Check(xCoplanar[0].Role == "bottom-x", "Section X line preserves reinforcement role");

        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-2, 0.1, 1), new SlabSectionPoint(2, 0.1, 1)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Section X parallel offset X bar is hidden");

        var yCrossesX = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(1, -2, 2), new SlabSectionPoint(1, 2, 4)
        }, SlabSectionAxis.SectionX, 0, "top-y");
        Check(yCrossesX.Count == 1 && yCrossesX[0].Kind == SlabSectionPrimitiveKind.IntersectionMarker,
            "Y bar crossing Section X produces a symbolic marker");
        Check(Near(1, yCrossesX[0].StartAlong, 1e-9) && Near(3, yCrossesX[0].StartElevation, 1e-9),
            "Section X marker is interpolated at exact cut coordinates");
        Check(yCrossesX[0].Role == "top-y", "Section X marker preserves reinforcement role");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(1, 0.5, 2), new SlabSectionPoint(1, 2, 4)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Y bar wholly outside Section X is hidden");

        var yCoplanar = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, -2, 1), new SlabSectionPoint(0, 2, 1)
        }, SlabSectionAxis.SectionY, 0, "bottom-y");
        Check(yCoplanar.Count == 1 && yCoplanar[0].Kind == SlabSectionPrimitiveKind.CoplanarSegment,
            "Section Y coplanar Y bar remains a line");
        Check(Near(-2, yCoplanar[0].StartAlong, 1e-9) && Near(2, yCoplanar[0].EndAlong, 1e-9),
            "Section Y line uses actual projected endpoints");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0.1, -2, 1), new SlabSectionPoint(0.1, 2, 1)
        }, SlabSectionAxis.SectionY, 0).Count == 0, "Section Y parallel offset Y bar is hidden");

        var xCrossesY = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-2, 1, 2), new SlabSectionPoint(2, 1, 4)
        }, SlabSectionAxis.SectionY, 0, "support-x");
        Check(xCrossesY.Count == 1 && xCrossesY[0].Kind == SlabSectionPrimitiveKind.IntersectionMarker,
            "X bar crossing Section Y produces a symbolic marker");
        Check(Near(1, xCrossesY[0].StartAlong, 1e-9) && Near(3, xCrossesY[0].StartElevation, 1e-9),
            "Section Y marker is interpolated at exact cut coordinates");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0.5, 1, 2), new SlabSectionPoint(2, 1, 4)
        }, SlabSectionAxis.SectionY, 0).Count == 0, "X bar wholly outside Section Y is hidden");

        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, SlabSectionGeometry.PlaneToleranceFeet * 0.5, 0),
            new SlabSectionPoint(1, SlabSectionGeometry.PlaneToleranceFeet * 0.5, 0)
        }, SlabSectionAxis.SectionX, 0).Single().Kind == SlabSectionPrimitiveKind.CoplanarSegment,
            "Section plane tolerance accepts numerical coplanarity");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, SlabSectionGeometry.PlaneToleranceFeet * 2, 0),
            new SlabSectionPoint(1, SlabSectionGeometry.PlaneToleranceFeet * 2, 0)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Section plane tolerance rejects nearby off-plane bars");

        var twiceCrossing = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-1, -1, 0), new SlabSectionPoint(1, 1, 2),
            new SlabSectionPoint(-1, 1, 4), new SlabSectionPoint(1, -1, 6)
        }, SlabSectionAxis.SectionX, 0, "opening");
        Check(twiceCrossing.Count == 2, "Separate cut crossings remain separate primitives");
        Check(twiceCrossing.All(primitive => primitive.Kind == SlabSectionPrimitiveKind.IntersectionMarker),
            "Multiple intersections never become an artificial connecting segment");
        Check(Near(1, twiceCrossing[0].StartElevation, 1e-9) && Near(5, twiceCrossing[1].StartElevation, 1e-9),
            "Multiple markers retain their independent solved elevations");

        SlabSectionPoint[] outer =
        {
            new SlabSectionPoint(-5, -5, 1), new SlabSectionPoint(5, -5, 1),
            new SlabSectionPoint(5, 5, 1), new SlabSectionPoint(-5, 5, 1)
        };
        IReadOnlyList<SlabSectionPoint>[] openings =
        {
            new[]
            {
                new SlabSectionPoint(-1, -1, 1), new SlabSectionPoint(1, -1, 1),
                new SlabSectionPoint(1, 1, 1), new SlabSectionPoint(-1, 1, 1)
            }
        };
        var openingCut = SlabSectionGeometry.GetConcreteIntervals(outer, openings, SlabSectionAxis.SectionX, 0);
        Check(openingCut.Count == 2, "Opening intersecting section splits concrete into two intervals");
        Check(Near(-5, openingCut[0].Start, 1e-9) && Near(-1, openingCut[0].End, 1e-9), "Opening cut preserves left concrete interval");
        Check(Near(1, openingCut[1].Start, 1e-9) && Near(5, openingCut[1].End, 1e-9), "Opening cut preserves right concrete interval");

        SlabSectionPoint[] concaveDisconnected =
        {
            new SlabSectionPoint(-5, -5, 1), new SlabSectionPoint(-3, -5, 1),
            new SlabSectionPoint(-3, 3, 1), new SlabSectionPoint(3, 3, 1),
            new SlabSectionPoint(3, -5, 1), new SlabSectionPoint(5, -5, 1),
            new SlabSectionPoint(5, 5, 1), new SlabSectionPoint(-5, 5, 1)
        };
        var disconnected = SlabSectionGeometry.GetConcreteIntervals(concaveDisconnected, null, SlabSectionAxis.SectionX, 0);
        Check(disconnected.Count == 2, "Concave supported cut returns disconnected concrete intervals");
        Check(Near(-5, disconnected[0].Start, 1e-9) && Near(-3, disconnected[0].End, 1e-9), "Disconnected left interval is exact");
        Check(Near(3, disconnected[1].Start, 1e-9) && Near(5, disconnected[1].End, 1e-9), "Disconnected right interval is exact without a false connector");

        Check(SlabSectionGeometry.IntersectPath(null, SlabSectionAxis.SectionX, 0).Count == 0, "Empty solved path is safe");
        Check(SlabSectionGeometry.IntersectPath(new[] { new SlabSectionPoint(0, 0, 0) }, SlabSectionAxis.SectionX, 0).Count == 0,
            "Single-point solved path is safe");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(double.NaN, 0, 0), new SlabSectionPoint(1, 0, 1)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Invalid solved coordinates fail closed");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, 0, 0), new SlabSectionPoint(1, 0, 1)
        }, SlabSectionAxis.SectionX, double.NaN).Count == 0, "Invalid cut coordinate fails closed");
        Check(SlabSectionGeometry.GetConcreteIntervals(new[]
        {
            new SlabSectionPoint(0, 0, 0), new SlabSectionPoint(double.PositiveInfinity, 0, 0),
            new SlabSectionPoint(0, 1, 0)
        }, null, SlabSectionAxis.SectionX, 0).Count == 0, "Invalid concrete boundary fails closed");
    }

    private static void RunBeamStirrupLayoutCases()
    {
        List<double> stations = BeamStirrupLayout.CreateZoneStations(
            totalLength: 1000, zoneA1Length: 250, spacingA1: 100, spacingA2: 200,
            endClearance: 50, duplicateTolerance: 1);
        Check(stations.SequenceEqual(new[] { 50d, 150d, 250d, 450d, 650d, 750d, 850d, 950d }),
            "Beam A1/A2/A1 station plan is deterministic and preserves the requested zone spacings");
        Check(stations.Zip(stations.Skip(1), (left, right) => right > left).All(increasing => increasing),
            "Beam stirrup stations are strictly ordered without duplicate zone-boundary bars");

        List<double> hanger = BeamStirrupLayout.CreateHangerStations(new[] { 250d },
            quantity: 1, spacing: 50, startClearance: 50, endClearance: 950);
        List<double> merged = BeamStirrupLayout.MergeAndValidate(stations.Concat(hanger),
            duplicateTolerance: 1, minimumSeparation: 10);
        Check(merged.Count == stations.Count && merged.Contains(250),
            "A hanger station coincident with a regular stirrup is represented exactly once");

        bool overlapRejected = false;
        try
        {
            BeamStirrupLayout.MergeAndValidate(new[] { 100d, 105d }, duplicateTolerance: 1, minimumSeparation: 10);
        }
        catch (InvalidOperationException) { overlapRejected = true; }
        Check(overlapRejected, "Overlapping distinct Beam stirrup centerlines are rejected");

        bool invalidSpacingRejected = false;
        try
        {
            BeamStirrupLayout.CreateZoneStations(1000, 250, 0, 200, 50, 1);
        }
        catch (ArgumentOutOfRangeException) { invalidSpacingRejected = true; }
        Check(invalidSpacingRejected, "Non-positive Beam stirrup spacing fails closed instead of selecting a fallback");

        List<double> distinctHangers = BeamStirrupLayout.CreateHangerStations(new[] { 250d, 500d },
            quantity: 3, spacing: 50, startClearance: 50, endClearance: 950);
        Check(distinctHangers.SequenceEqual(new[] { 200d, 250d, 300d, 450d, 500d, 550d }),
            "Hanger stations are symmetric about actual intersecting-beam stations");
        Check(BeamStirrupLayout.MergeAndValidate(stations.Concat(distinctHangers), 1, 10).SequenceEqual(
            BeamStirrupLayout.MergeAndValidate(distinctHangers.Concat(stations), 1, 10)),
            "Beam normal/hanger station merging is independent of discovery order");

        List<BeamLongitudinalLayout.SectionBar> top = BeamLongitudinalLayout.CreateSymmetricRow(
            width: 300, height: 600, cover: 25, stirrupDiameter: 10, barDiameter: 16,
            quantity: 2, top: true);
        List<BeamLongitudinalLayout.SectionBar> bottom = BeamLongitudinalLayout.CreateSymmetricRow(
            width: 300, height: 600, cover: 25, stirrupDiameter: 10, barDiameter: 16,
            quantity: 2, top: false);
        Check(top.Count == 2 && Math.Abs(top[0].X + top[1].X) < 1e-9 &&
              Math.Abs(top[0].Y - 257) < 1e-9 && Math.Abs(bottom[0].Y + 257) < 1e-9,
            "Beam top/bottom longitudinal rows use exact cover, tie and bar-radius offsets with symmetric placement");
        BeamLongitudinalLayout.RequireNoOverlap(top.Concat(bottom));
        Check(true, "Separated Beam top and bottom rows pass section-plane non-overlap validation");

        bool crowdedRowRejected = false;
        try
        {
            BeamLongitudinalLayout.CreateSymmetricRow(width: 300, height: 600, cover: 25,
                stirrupDiameter: 10, barDiameter: 25, quantity: 10, top: true);
        }
        catch (InvalidOperationException) { crowdedRowRejected = true; }
        Check(crowdedRowRejected, "Beam longitudinal row rejects a quantity/diameter combination with overlapping bars");

        bool overlappingLayersRejected = false;
        try
        {
            BeamLongitudinalLayout.RequireNoOverlap(new[]
            {
                new BeamLongitudinalLayout.SectionBar(0, 0, 20),
                new BeamLongitudinalLayout.SectionBar(0, 10, 20)
            });
        }
        catch (InvalidOperationException) { overlappingLayersRejected = true; }
        Check(overlappingLayersRejected, "Beam section layout rejects overlapping longitudinal layer centerlines");
    }

    private static void RunBeamManualPlanCases()
    {
        Check(Math.Abs(BeamManualSegmentDistanceV1.SquaredDistance(
            new BeamManualPoint3V1(0, 0, 0), new BeamManualPoint3V1(2, 0, 0),
            new BeamManualPoint3V1(1, -1, 0), new BeamManualPoint3V1(1, 1, 0))) < 1e-12,
            "Manual Beam finite-segment distance detects a true perpendicular crossing");
        Check(Math.Abs(BeamManualSegmentDistanceV1.SquaredDistance(
            new BeamManualPoint3V1(0, 0, 0), new BeamManualPoint3V1(2, 0, 0),
            new BeamManualPoint3V1(0, 3, 0), new BeamManualPoint3V1(2, 3, 0)) - 9) < 1e-12,
            "Manual Beam finite-segment distance measures parallel bar clearance");
        Check(Math.Abs(BeamManualSegmentDistanceV1.SquaredDistance(
            new BeamManualPoint3V1(0, 0, 0), new BeamManualPoint3V1(1, 0, 0),
            new BeamManualPoint3V1(2, 0, 0), new BeamManualPoint3V1(3, 0, 0)) - 1) < 1e-12,
            "Manual Beam finite-segment distance respects separated collinear endpoints");

        BeamManualDetailingPlanV1 plan = CreateManualPlan();
        BeamManualValidationResultV1 valid = BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: true);
        Check(valid.IsValid, "Explicit synthetic manual Beam plan passes geometry-input validation without design defaults");
        BeamManualDetailingPlanV1 skewFrame = CreateManualPlan();
        skewFrame.Frame.RightX = 1; skewFrame.Frame.RightY = 0; skewFrame.Frame.RightZ = 0;
        Check(!BeamManualPlanValidatorV1.Validate(skewFrame, true).IsValid, "Manual plan rejects non-orthogonal local frame axes");
        BeamManualDetailingPlanV1 reflectedFrame = CreateManualPlan();
        reflectedFrame.Frame.RightX *= -1; reflectedFrame.Frame.RightY *= -1; reflectedFrame.Frame.RightZ *= -1;
        Check(!BeamManualPlanValidatorV1.Validate(reflectedFrame, true).IsValid, "Manual plan rejects reflected local frames with reversed handedness");

        IReadOnlyList<BeamManualResolvedBarV1> bars = BeamManualLayoutBuilderV1.Build(plan);
        Check(bars.Count == 2 && bars[0].Centerline.Count == 2 && bars[0].Centerline[0].StationMm == 120 &&
              bars[0].Centerline[1].StationMm == 5900, "Canonical manual builder retains absolute stations and each physical bar position");
        Check(bars[0].Role == BeamManualBarRole.TopContinuous && bars[1].Role == BeamManualBarRole.BottomMidExtra &&
              bars[0].DiameterMm == 16 && bars[1].DiameterMm == 12, "Manual builder preserves per-group role and exact resolved model diameter");
        BeamManualDetailingPlanV1 disabledGroup = CreateManualPlan();
        disabledGroup.BarGroups[1].Enabled = false;
        Check(BeamManualLayoutBuilderV1.Build(disabledGroup).Count == 1, "Manual builder omits explicitly disabled groups rather than generating them");

        string originalFingerprint = BeamManualLayoutBuilderV1.Fingerprint(plan);
        plan.BarGroups[0].Bars[0].StartStationMm += 1;
        Check(originalFingerprint != BeamManualLayoutBuilderV1.Fingerprint(plan), "Manual plan fingerprint includes explicit station changes");
        plan.BarGroups[0].Bars[0].StartStationMm -= 1;
        BeamManualDetailingPlanV1 changedType = CreateManualPlan();
        changedType.BarGroups[0].BarType.UniqueId = "SYNTHETIC_TEST_OTHER_TYPE";
        Check(originalFingerprint != BeamManualLayoutBuilderV1.Fingerprint(changedType), "Manual plan fingerprint includes exact RebarBarType identity");

        BeamManualDetailingPlanV1 overlap = CreateManualPlan();
        overlap.BarGroups[1].Bars[0].XMm = overlap.BarGroups[0].Bars[0].XMm;
        overlap.BarGroups[1].Bars[0].YMm = overlap.BarGroups[0].Bars[0].YMm;
        Check(!BeamManualPlanValidatorV1.Validate(overlap, true).IsValid, "Manual plan rejects overlapping physical bar envelopes");

        BeamManualDetailingPlanV1 outside = CreateManualPlan();
        outside.BarGroups[0].Bars[0].EndStationMm = outside.Frame.LengthMm + 1;
        Check(!BeamManualPlanValidatorV1.Validate(outside, true).IsValid, "Manual plan rejects stations outside verified Beam concrete");

        BeamManualDetailingPlanV1 asymmetricCover = CreateManualPlan();
        asymmetricCover.BarGroups[0].Bars[0].XMm = 0;
        asymmetricCover.BarGroups[0].Bars[0].YMm = 0;
        asymmetricCover.Covers.Single(cover => cover.Face == BeamManualCoverFace.Bottom).ClearCoverMm = 300;
        Check(!BeamManualPlanValidatorV1.Validate(asymmetricCover, true).IsValid,
            "Manual plan independently checks bottom-face cover even for a bar above section center");

        BeamManualDetailingPlanV1 endFace = CreateManualPlan();
        endFace.BarGroups[0].Bars[0].StartStationMm = 0;
        Check(!BeamManualPlanValidatorV1.Validate(endFace, true).IsValid, "Manual plan keeps the physical bar diameter envelope inside Beam end faces");

        BeamManualDetailingPlanV1 missing = CreateManualPlan();
        missing.BarGroups[0].BarType = null;
        Check(!BeamManualPlanValidatorV1.Validate(missing, true).IsValid, "Manual plan blocks missing exact RebarBarType identity");

        BeamManualDetailingPlanV1 explicitHook = CreateManualPlan();
        explicitHook.BarGroups[0].Bars[0].StartDetail.Kind = BeamManualEndDetailKind.ExplicitLoadedRevitHook;
        explicitHook.BarGroups[0].Bars[0].StartDetail.HookType = new BeamManualElementIdentityV1
        {
            DocumentKey = explicitHook.DocumentKey, UniqueId = "SYNTHETIC_TEST_HOOK_ID", Name = "SYNTHETIC TEST HOOK",
            State = BeamManualReferenceState.Resolved
        };
        explicitHook.BarGroups[0].Bars[0].StartDetail.HookOrientation = BeamManualHookOrientation.Left;
        Check(BeamManualPlanValidatorV1.Validate(explicitHook, true).IsValid,
            "Manual plan accepts a user-selected exact hook identity and orientation for adapter compatibility checking");

        BeamManualDetailingPlanV1 explicitReturn = CreateManualPlan();
        explicitReturn.BarGroups[0].Bars[0].StartDetail = new BeamManualEndDetailV1
        {
            Kind = BeamManualEndDetailKind.ExplicitCurveReturn,
            ReturnPoints = new List<BeamManualPointV1>
            {
                new BeamManualPointV1 { StationMm = 200, XMm = -80, YMm = 160 },
                new BeamManualPointV1 { StationMm = 120, XMm = -80, YMm = 210 }
            }
        };
        IReadOnlyList<BeamManualResolvedBarV1> returnBars = BeamManualLayoutBuilderV1.Build(explicitReturn);
        Check(returnBars[0].Centerline.Count == 3 && returnBars[0].Centerline[0].StationMm == 200 &&
              returnBars[0].Centerline[1].StationMm == 120 && returnBars[0].Centerline[2].StationMm == 5900,
            "Manual builder preserves explicit start-return vertices, anchor and full longitudinal path order");

        BeamManualDetailingPlanV1 missingStations = CreateManualPlan();
        missingStations.StirrupGroups.Add(new BeamManualStirrupGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_STIRRUP", Enabled = true,
            BarType = SyntheticType(missingStations.DocumentKey, "SYNTHETIC_STIRRUP_TYPE", 10),
            RebarShape = new BeamManualElementIdentityV1 { DocumentKey = missingStations.DocumentKey, UniqueId = "SYNTHETIC_SHAPE", Name = "SYNTHETIC TEST SHAPE", State = BeamManualReferenceState.Resolved },
            StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            SectionPath = new List<BeamManualPointV1> { new BeamManualPointV1 { XMm = -100, YMm = -200 }, new BeamManualPointV1 { XMm = 100, YMm = -200 }, new BeamManualPointV1 { XMm = 100, YMm = 200 }, new BeamManualPointV1 { XMm = -100, YMm = 200 } },
            CoincidentStationPolicy = "SYNTHETIC TEST POLICY"
        });
        Check(!BeamManualPlanValidatorV1.Validate(missingStations, true).IsValid, "Manual stirrup group requires explicit stations and cannot infer legacy zone defaults");

        BeamManualDetailingPlanV1 unresolved = CreateManualPlan();
        unresolved.BarGroups[0].BarType.State = BeamManualReferenceState.Unresolved;
        Check(!BeamManualPlanValidatorV1.Validate(unresolved, true).IsValid &&
              BeamManualPlanValidatorV1.Validate(unresolved, false).IsValid,
              "Foreign/unresolved type reference remains storable but blocks solve until exact user rebind");

        string temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "beam-manual-v1-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BeamManualDetailingPlanStoreV1(temporary, unresolved.DocumentKey, unresolved.HostUniqueId);
            BeamManualDetailingPlanV1 saved = store.Save(unresolved, expectedRevision: 0);
            BeamManualDetailingPlanV1 loaded = store.Load();
            Check(saved.Revision == 1 && loaded.Revision == 1 && loaded.BarGroups[0].BarType.State == BeamManualReferenceState.Unresolved &&
                  loaded.BarGroups[1].Bars[0].XMm == unresolved.BarGroups[1].Bars[0].XMm,
                  "Versioned manual-plan store round-trips geometry, roles, identities and unresolved state");
            Check(ThrowsIOException(() => store.Save(unresolved, expectedRevision: 0)), "Manual-plan store rejects stale editor revisions");
            BeamManualDetailingPlanV1 wrongHost = CreateManualPlan();
            wrongHost.HostUniqueId = "other-synthetic-host";
            Check(ThrowsInvalidData(() => store.Save(wrongHost, expectedRevision: 0)), "Manual-plan store is bound to exact document and Beam identity");
        }
        finally
        {
            if (System.IO.Directory.Exists(temporary)) System.IO.Directory.Delete(temporary, recursive: true);
        }
    }

    private static BeamManualDetailingPlanV1 CreateManualPlan()
    {
        var plan = new BeamManualDetailingPlanV1
        {
            DocumentKey = "SYNTHETIC_TEST_FIXTURE_ONLY_DOCUMENT",
            HostUniqueId = "SYNTHETIC_TEST_FIXTURE_ONLY_BEAM",
            Frame = new BeamManualFrameV1
            {
                WidthMm = 300, HeightMm = 600, LengthMm = 6000,
                DirectionX = 1, DirectionY = 0, DirectionZ = 0,
                RightX = 0, RightY = -1, RightZ = 0,
                UpX = 0, UpY = 0, UpZ = 1,
                HostGeometrySignature = "SYNTHETIC_TEST_FIXTURE_ONLY_RECTANGULAR_BEAM"
            },
            Covers = new List<BeamManualCoverV1>
            {
                SyntheticCover(BeamManualCoverFace.Top, 40), SyntheticCover(BeamManualCoverFace.Bottom, 45),
                SyntheticCover(BeamManualCoverFace.Left, 35), SyntheticCover(BeamManualCoverFace.Right, 50),
                SyntheticCover(BeamManualCoverFace.StirrupOutside, 30)
            },
            SupportEnds = new List<BeamManualSupportEndV1>
            {
                new BeamManualSupportEndV1 { End = BeamManualEnd.Start, Mode = BeamManualSupportMode.UserDefinedFreeEndWithinHost, IntendedContinuation = "SYNTHETIC TEST ONLY: finite geometric termination" },
                new BeamManualSupportEndV1 { End = BeamManualEnd.Finish, Mode = BeamManualSupportMode.UserDefinedFreeEndWithinHost, IntendedContinuation = "SYNTHETIC TEST ONLY: finite geometric termination" }
            }
        };
        plan.BarGroups.Add(new BeamManualBarGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_TOP", Role = BeamManualBarRole.TopContinuous, Enabled = true,
            BarType = SyntheticType(plan.DocumentKey, "SYNTHETIC_TYPE_TOP", 16),
            Bars = new List<BeamManualBarV1> { SyntheticBar(-80, 210, 120, 5900, "SYNTHETIC_LAYER_TOP_1") }
        });
        plan.BarGroups.Add(new BeamManualBarGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_EXTRA", Role = BeamManualBarRole.BottomMidExtra, Enabled = true,
            BarType = SyntheticType(plan.DocumentKey, "SYNTHETIC_TYPE_EXTRA", 12),
            Bars = new List<BeamManualBarV1> { SyntheticBar(65, -200, 1100, 5100, "SYNTHETIC_LAYER_BOTTOM_1") }
        });
        return plan;
    }

    private static BeamManualCoverV1 SyntheticCover(BeamManualCoverFace face, double value) => new BeamManualCoverV1
    {
        Face = face, Source = BeamManualCoverSource.ExplicitUserInput, ClearCoverMm = value
    };

    private static BeamManualElementReferenceV1 SyntheticType(string documentKey, string id, double diameter) => new BeamManualElementReferenceV1
    {
        DocumentKey = documentKey, UniqueId = id, Name = "SYNTHETIC TEST TYPE " + id,
        DiameterMm = diameter, State = BeamManualReferenceState.Resolved
    };

    private static BeamManualBarV1 SyntheticBar(double x, double y, double start, double end, string layer) => new BeamManualBarV1
    {
        XMm = x, YMm = y, StartStationMm = start, EndStationMm = end, LayerId = layer,
        StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
        FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation }
    };

    private static bool ThrowsIOException(Action action)
    {
        try { action(); return false; }
        catch (System.IO.IOException) { return true; }
    }

    private static bool ThrowsInvalidData(Action action)
    {
        try { action(); return false; }
        catch (System.IO.InvalidDataException) { return true; }
    }

    private static void RunRebarCalculationGoldens()
    {
        double tcvnAnchorage = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);
        double tcvnLap = RebarAnchorageCalculator.CalculateLapLength(
            18, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);
        double eurocodeAnchorage = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionStraight, DesignCode.Eurocode2);
        double eurocodeHooked = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionHooked, DesignCode.Eurocode2);

        Check(Near(600, tcvnAnchorage, 0.000001), "production Rebar TCVN B25/CB400V d18 anchorage golden");
        Check(Near(900, tcvnLap, 0.000001), "production Rebar TCVN B25/CB400V d18 lap golden");
        Check(Near(726.4640343386, eurocodeAnchorage, 0.000001), "production Rebar Eurocode C25/30 B500 d18 anchorage golden");
        Check(Near(508.52482403702, eurocodeHooked, 0.000001), "production Rebar Eurocode C25/30 B500 d18 hooked anchorage golden");
        Check(Near(630, RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.Auto, SteelGrade.Auto, AnchorageType.TensionStraight, DesignCode.Eurocode2), 0.000001),
            "production Rebar automatic-grade fallback golden");
    }

    private static void RunDomainGoldens()
    {
        // QS quantity calculations. Units are carried by the fixture label; the shared math is unit-agnostic.
        Check(QsQuantityMath.TryCalculatePayQuantity(12.345, 5, 2, out double concrete) && Near(12.96, concrete, 0.000001), "QS concrete volume with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(10.5, 2, 1, out double finishArea) && Near(10.7, finishArea, 0.000001), "QS finish area with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(0, 0, 2, out double count) && Near(0, count, 0), "QS zero count");
        Check(QsQuantityMath.TryCalculatePayQuantity(54.32, 0, 3, out double length) && Near(54.32, length, 0), "QS lineal length");
        Check(QsQuantityMath.TryCalculatePayQuantity(2.5, 10, 2, out double material) && Near(2.75, material, 0.000001), "QS material quantity with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(1.005, 0, 2, out double midpoint) && Near(1.01, midpoint, 0.000001), "QS deterministic midpoint rounding");

        var pipe = MepMeasurementCalculator.RoundOpening(100, 25);
        Check(Near(150, pipe.WidthMm, 0) && Near(150, pipe.HeightMm, 0), "MEP circular service clearance");
        var duct = MepMeasurementCalculator.RectangularOpening(600, 300, 25);
        Check(Near(650, duct.WidthMm, 0) && Near(350, duct.HeightMm, 0), "MEP rectangular service clearance");
        var elevations = MepMeasurementCalculator.VerticalRange(5000, 600);
        Check(Near(4700, elevations.BottomMm, 0) && Near(5300, elevations.TopMm, 0), "MEP bottom/top elevation range");

        var horizontal = LintelLayout.Compute(1000, 2000, 3000, 900, 200, 1, 0);
        Check(Near(350, horizontal.StartXmm, 0) && Near(1650, horizontal.EndXmm, 0), "Architectural lintel horizontal semantic geometry");
        Check(Near(2000, horizontal.StartYmm, 0) && Near(3000, horizontal.StartZmm, 0), "Architectural lintel elevation");
        var diagonal = LintelLayout.Compute(0, 0, 0, 1000, 0, 3, 4);
        Check(Near(-300, diagonal.StartXmm, 0) && Near(-400, diagonal.StartYmm, 0) &&
              Near(300, diagonal.EndXmm, 0) && Near(400, diagonal.EndYmm, 0), "Architectural normalized diagonal orientation");
    }

    private static void RunComparatorContract()
    {
        Check(10.0 == 10.0, "exact comparator");
        Check(Near(10.0, 10.0005, 0.001), "tolerance comparator");
        Check(new[] { "A", "B", "A" }.OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(new[] { "A", "A", "B" }.OrderBy(x => x, StringComparer.Ordinal)), "unordered collection comparator preserves multiplicity");
        Check(123.5 >= 120 && 123.5 <= 125, "range comparator");

        var expected = new SemanticBar { Type = "T16", Host = "COLUMN", Orientation = "VERTICAL", Count = 8, DiameterMm = 16, SpacingMm = 0, CoverMm = 40 };
        var actual = new SemanticBar { Type = "T16", Host = "COLUMN", Orientation = "VERTICAL", Count = 8, DiameterMm = 16.0002, SpacingMm = 0, CoverMm = 40.0001 };
        Check(SemanticallyEquivalent(expected, actual, 0.001), "semantic bar comparator accepts numeric tolerance and ignores volatile ids");
        actual.Host = "BEAM";
        Check(!SemanticallyEquivalent(expected, actual, 0.001), "semantic bar comparator rejects wrong host relationship");
    }

    private static bool SemanticallyEquivalent(SemanticBar a, SemanticBar b, double tolerance) =>
        a != null && b != null && a.Type == b.Type && a.Host == b.Host && a.Orientation == b.Orientation &&
        a.Count == b.Count && Near(a.DiameterMm, b.DiameterMm, tolerance) &&
        Near(a.SpacingMm, b.SpacingMm, tolerance) && Near(a.CoverMm, b.CoverMm, tolerance);

    private static void RunEdgeCases()
    {
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(0, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects zero diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(-16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects negative diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(double.NaN, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects NaN diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(double.MaxValue, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects overflowing extreme diameter result");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018, double.NaN)), "Rebar rejects non-finite fallback multiplier");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, (DesignCode)999)), "Rebar rejects unknown design code");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateLapLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018, percentLappedFactor: 0)), "Rebar rejects zero lap factor");
        Check(!QsQuantityMath.TryCalculatePayQuantity(double.NaN, 0, 2, out _), "QS rejects NaN input");
        Check(!QsQuantityMath.TryCalculatePayQuantity(-1, 0, 2, out _), "QS rejects negative quantity");
        Check(!QsQuantityMath.TryCalculatePayQuantity(1, -1, 2, out _), "QS rejects negative waste");
        Check(!QsQuantityMath.TryCalculatePayQuantity(double.MaxValue, double.MaxValue, 2, out _), "QS rejects overflow");
        Check(!QsQuantityMath.TryParseQuantity("1,2,3.5", false, out _), "QS rejects malformed unit quantity");
        Check(!QsQuantityMath.TryParseQuantity("NaN", false, out _), "QS rejects non-finite parsed quantity");
        Check(ThrowsArgument(() => MepMeasurementCalculator.RoundOpening(0, 25)), "MEP rejects zero diameter");
        Check(ThrowsArgument(() => MepMeasurementCalculator.RectangularOpening(double.PositiveInfinity, 300, 25)), "MEP rejects extreme non-finite size");
        Check(ThrowsArgument(() => MepMeasurementCalculator.VerticalRange(1000, 0)), "MEP rejects zero section height");
        Check(ThrowsArgument(() => LintelLayout.Compute(0, 0, 0, 0, 0, 1, 0)), "Architectural rejects zero lintel width");
        Check(ThrowsArgument(() => LintelLayout.Compute(0, 0, 0, 1, -1, 1, 0)), "Architectural rejects negative extension");
        Check(ThrowsArgument(() => LintelLayout.Compute(double.NaN, 0, 0, 1, 0, 1, 0)), "Architectural rejects non-finite input");
    }

    private static bool ThrowsOutOfRange(Action action)
    {
        try { action(); return false; }
        catch (ArgumentOutOfRangeException) { return true; }
    }

    private static bool ThrowsArgument(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }

    private static void RunSyntheticStress()
    {
        foreach (int recordCount in new[] { 100, 1000, 10000 })
        {
            var timer = Stopwatch.StartNew();
            double qsTotal = 0;
            double mepTotal = 0;
            double geometryChecksum = 0;
            for (int i = 1; i <= recordCount; i++)
            {
                if (!QsQuantityMath.TryCalculatePayQuantity(i / 10.0, 5, 2, out double pay))
                    throw new InvalidOperationException("QS stress input unexpectedly rejected.");
                qsTotal += pay;
                var measure = MepMeasurementCalculator.RectangularOpening(100 + i, 50 + (i % 300), 25);
                mepTotal += measure.WidthMm + measure.HeightMm;
                var layout = LintelLayout.Compute(i, i / 2.0, 3000, 1000, 100, 1, 0);
                geometryChecksum += layout.StartXmm + layout.EndXmm;
            }
            timer.Stop();
            Check(recordCount > 0 && qsTotal > 0 && mepTotal > 0 && geometryChecksum > 0, $"synthetic stress outputs are finite/non-empty for {recordCount} records");
            Check(timer.Elapsed < TimeSpan.FromSeconds(15), $"synthetic stress completes within 15 seconds for {recordCount} records");
            Console.WriteLine($"STRESS_RECORDS={recordCount}; ELAPSED_MS={timer.ElapsedMilliseconds}; QS_SUM={qsTotal:F2}; MEP_SUM={mepTotal:F2}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Workflow;
using KhimTools.RebarTool.Models;

namespace KhimTools.RebarTool.Core
{
    /// <summary>One production generator invocation included in a detached preview capture.</summary>
    public sealed class RebarPreviewRequest
    {
        public string InputFingerprint { get; private set; }
        public Func<IList<Rebar>> Generate { get; private set; }
        public Func<string> CurrentInputFingerprint { get; private set; }
        public IDictionary<string, string> Semantics { get; private set; }
        public Func<Rebar, string> RoleSelector { get; private set; }

        public RebarPreviewRequest(string inputFingerprint, Func<IList<Rebar>> generate,
            Func<string> currentInputFingerprint = null, IDictionary<string, string> semantics = null,
            Func<Rebar, string> roleSelector = null)
        {
            if (string.IsNullOrWhiteSpace(inputFingerprint)) throw new ArgumentException("A stable input fingerprint is required.", "inputFingerprint");
            InputFingerprint = inputFingerprint;
            Generate = generate ?? throw new ArgumentNullException("generate");
            CurrentInputFingerprint = currentInputFingerprint;
            Semantics = semantics == null ? new Dictionary<string, string>() : new Dictionary<string, string>(semantics, StringComparer.Ordinal);
            RoleSelector = roleSelector;
        }
    }

    /// <summary>Revit-free coordinates copied from Revit's solved centerline curves.</summary>
    public sealed class RebarPreviewPoint
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }
        public RebarPreviewPoint(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    public sealed class RebarPreviewPath
    {
        public IReadOnlyList<RebarPreviewPoint> Points { get; private set; }
        public string Role { get; private set; }
        public RebarPreviewPath(IEnumerable<RebarPreviewPoint> points, string role = null)
        {
            Points = (points ?? Enumerable.Empty<RebarPreviewPoint>()).ToArray();
            Role = role ?? string.Empty;
        }
    }

    public sealed class RebarPreviewComponent
    {
        public string InputFingerprint { get; private set; }
        public int BarCount { get; private set; }
        public string GeometryFingerprint { get; private set; }
        public IReadOnlyList<RebarPreviewPath> Paths { get; private set; }
        public IReadOnlyList<string> BarFingerprints { get; private set; }
        public string HostId { get; private set; }
        public string HostGeometryDescriptor { get; private set; }
        public IReadOnlyDictionary<string, string> Semantics { get; private set; }
        public string PlanFingerprint { get; private set; }

        internal RebarPreviewComponent(RebarPreviewRequest request, IList<Rebar> bars)
        {
            InputFingerprint = request.InputFingerprint;
            Semantics = new Dictionary<string, string>(request.Semantics, StringComparer.Ordinal);
            HostId = Semantics.ContainsKey("HostId") ? Semantics["HostId"] : string.Empty;
            HostGeometryDescriptor = Semantics.ContainsKey("HostGeometry") ? Semantics["HostGeometry"] : string.Empty;
            BarCount = bars.Count;
            var descriptors = bars.Select(bar =>
            {
                string role = request.RoleSelector == null ? string.Empty : request.RoleSelector(bar);
                IReadOnlyList<RebarPreviewPath> paths = RebarPreviewService.ReadPaths(new List<Rebar> { bar }, role);
                return new { Paths = paths, Fingerprint = RebarPreviewService.FingerprintBar(bar, paths) };
            }).ToArray();
            Paths = descriptors.SelectMany(descriptor => descriptor.Paths).ToArray();
            BarFingerprints = descriptors.Select(descriptor => descriptor.Fingerprint).ToArray();
            GeometryFingerprint = RebarPreviewService.FingerprintBars(BarFingerprints);
            PlanFingerprint = WorkflowFingerprint.Compute(InputFingerprint, GeometryFingerprint);
        }
    }

    public sealed class RebarPreviewSnapshot
    {
        public IReadOnlyList<RebarPreviewComponent> Components { get; private set; }
        public string PlanFingerprint { get; private set; }
        internal RebarPreviewSnapshot(IList<RebarPreviewComponent> components, string planFingerprint)
        {
            Components = components.ToArray();
            PlanFingerprint = planFingerprint;
        }

        public RebarPreviewComponent Find(string inputFingerprint) =>
            Components.FirstOrDefault(c => WorkflowFingerprint.Matches(c.InputFingerprint, inputFingerprint));
    }

    /// <summary>
    /// Runs the real Rebar generators and Revit shape solver in a rollback-only transaction,
    /// copies solved centerlines into detached data, then proves every supplied input fingerprint is unchanged.
    /// The exact detached result is compared with the real transaction before it may commit.
    /// </summary>
    public static class RebarPreviewService
    {
        public static RebarPreviewSnapshot Capture(Document document, IEnumerable<RebarPreviewRequest> requests)
        {
            if (document == null) throw new ArgumentNullException("document");
            RebarPreviewRequest[] requestArray = (requests ?? Enumerable.Empty<RebarPreviewRequest>()).ToArray();
            if (requestArray.Length == 0) throw new ArgumentException("At least one Rebar preview request is required.", "requests");

            var components = new List<RebarPreviewComponent>();
            using (var transaction = new Transaction(document, "K-TOOLS Rebar Preview (rollback only)"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("Could not start the rollback-only Rebar preview transaction.");
                try
                {
                    var failureCapture = new RebarGenerationFailurePreprocessor();
                    FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(failureCapture);
                    transaction.SetFailureHandlingOptions(failureOptions);
                    foreach (RebarPreviewRequest request in requestArray)
                    {
                        IList<Rebar> bars = request.Generate();
                        document.Regenerate();
                        if (bars == null || bars.Count == 0)
                            throw new InvalidOperationException("The Rebar generator returned no solved bars for preview.");
                        components.Add(new RebarPreviewComponent(request, bars));
                    }
                }
                finally
                {
                    if (transaction.GetStatus() == TransactionStatus.Started &&
                        transaction.RollBack() != TransactionStatus.RolledBack)
                        throw new InvalidOperationException("The Rebar preview transaction did not roll back.");
                }
                if (transaction.GetStatus() != TransactionStatus.RolledBack)
                    throw new InvalidOperationException("The Rebar preview ended without a rollback.");
            }

            foreach (RebarPreviewRequest request in requestArray)
            {
                if (request.CurrentInputFingerprint != null &&
                    !WorkflowFingerprint.Matches(request.InputFingerprint, request.CurrentInputFingerprint()))
                    throw new InvalidOperationException("A Rebar preview input changed while its detached plan was being captured.");
            }
            return new RebarPreviewSnapshot(components,
                FingerprintInputs(requestArray.Select(request => request.InputFingerprint)));
        }

        public static string FingerprintInputs(IEnumerable<string> inputFingerprints) =>
            WorkflowFingerprint.Compute((inputFingerprints ?? Enumerable.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal));

        public static bool Matches(RebarPreviewSnapshot snapshot, string inputFingerprint, IList<Rebar> generated)
        {
            if (snapshot == null || generated == null || generated.Count == 0) return false;
            RebarPreviewComponent expected = snapshot.Find(inputFingerprint);
            if (expected == null || expected.BarCount != generated.Count) return false;
            string[] actualBars = generated.Select(FingerprintBar).ToArray();
            return WorkflowFingerprint.Matches(expected.GeometryFingerprint, FingerprintBars(actualBars));
        }

        public static string Fingerprint(RectangularColumnRebarInput input)
        {
            if (input == null || input.Column == null || input.MainBarType == null || input.StirrupBarType == null)
                throw new ArgumentException("Column, main bar type and tie bar type are required for preview.", "input");
            return WorkflowFingerprint.Compute(new[]
            {
                input.Column.UniqueId, input.Column.VersionGuid.ToString("D"),
                input.MainBarType.UniqueId, input.MainBarType.VersionGuid.ToString("D"),
                input.StirrupBarType.UniqueId, input.StirrupBarType.VersionGuid.ToString("D"),
                input.BarsAlongB.ToString(CultureInfo.InvariantCulture), input.BarsAlongH.ToString(CultureInfo.InvariantCulture),
                input.ZoneA1Length.ToString("R", CultureInfo.InvariantCulture),
                input.StirrupSpacingA1.ToString("R", CultureInfo.InvariantCulture), input.StirrupSpacingA2.ToString("R", CultureInfo.InvariantCulture),
                input.TieLayout.ToString(), input.HasInnerDiamondStirrup.ToString(), input.HasCrossLinks.ToString(),
                input.HasDowel.ToString(), input.HasTopAnchor.ToString(), input.IsFoundationColumn.ToString(), input.IsTopRoofColumn.ToString(),
                input.EnableCrankedSplice.ToString(), input.FootingAnchorMultiplier.ToString("R", CultureInfo.InvariantCulture),
                input.TopRoofHookLengthMultiplier.ToString("R", CultureInfo.InvariantCulture),
                input.CustomCoverFeet.HasValue ? input.CustomCoverFeet.Value.ToString("R", CultureInfo.InvariantCulture) : "auto-cover",
                input.DesignStandard.ToString(), input.ConcreteGrade.ToString(), input.SteelGrade.ToString(),
                input.LapLengthMultiplier.ToString("R", CultureInfo.InvariantCulture), input.StaggeredSplice.ToString(),
                input.AdjacentColumnAbove?.UniqueId ?? "",
                input.AdjacentColumnAbove == null ? "" : input.AdjacentColumnAbove.VersionGuid.ToString("D"),
                input.AdjacentColumnBelow?.UniqueId ?? "",
                input.AdjacentColumnBelow == null ? "" : input.AdjacentColumnBelow.VersionGuid.ToString("D")
            });
        }

        public static string Fingerprint(CircularColumnRebarInput input)
        {
            if (input == null || input.Column == null || input.MainBarType == null || input.StirrupBarType == null)
                throw new ArgumentException("Circular column, main bar type and tie bar type are required for preview.", "input");
            return WorkflowFingerprint.Compute(new[]
            {
                input.Column.UniqueId, input.Column.VersionGuid.ToString("D"),
                TypeFingerprint(input.MainBarType), TypeFingerprint(input.StirrupBarType),
                input.MainBarQty.ToString(CultureInfo.InvariantCulture),
                input.StirrupSpacing.ToString("R", CultureInfo.InvariantCulture),
                input.ZoneA1Length.ToString("R", CultureInfo.InvariantCulture),
                input.StirrupSpacingA1.ToString("R", CultureInfo.InvariantCulture),
                input.StirrupSpacingA2.ToString("R", CultureInfo.InvariantCulture),
                input.HasDowel.ToString(), input.HasTopAnchor.ToString(), input.IsFoundationColumn.ToString(),
                input.IsTopRoofColumn.ToString(), input.EnableCrankedSplice.ToString(),
                input.FootingAnchorMultiplier.ToString("R", CultureInfo.InvariantCulture),
                input.TopRoofHookLengthMultiplier.ToString("R", CultureInfo.InvariantCulture),
                input.CustomCoverFeet.HasValue ? input.CustomCoverFeet.Value.ToString("R", CultureInfo.InvariantCulture) : "auto-cover",
                input.LapLengthMultiplier.ToString("R", CultureInfo.InvariantCulture), input.StaggeredSplice.ToString(),
                input.DesignStandard.ToString(), input.ConcreteGrade.ToString(), input.SteelGrade.ToString(),
                input.AdjacentColumnAbove == null ? "" : input.AdjacentColumnAbove.UniqueId,
                input.AdjacentColumnAbove == null ? "" : input.AdjacentColumnAbove.VersionGuid.ToString("D"),
                input.AdjacentColumnBelow == null ? "" : input.AdjacentColumnBelow.UniqueId,
                input.AdjacentColumnBelow == null ? "" : input.AdjacentColumnBelow.VersionGuid.ToString("D")
            });
        }

        public static IDictionary<string, string> Describe(CircularColumnRebarInput input)
        {
            CircularColumnGeometryHelper.ColumnProfile profile = CircularColumnGeometryHelper.GetCircularProfile(input.Column);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HostId"] = input.Column.UniqueId,
                ["HostGeometry"] = "D=" + profile.Diameter.ToString("R", CultureInfo.InvariantCulture) + " @ " + profile.BaseCenter,
                ["BarTypes"] = TypeFingerprint(input.MainBarType) + " | " + TypeFingerprint(input.StirrupBarType),
                ["Diameters"] = input.MainBarType.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture) + " | " + input.StirrupBarType.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture),
                ["Spacing"] = input.StirrupSpacingA1.ToString("R", CultureInfo.InvariantCulture) + " | " + input.StirrupSpacingA2.ToString("R", CultureInfo.InvariantCulture),
                ["Cover"] = input.CustomCoverFeet.HasValue ? input.CustomCoverFeet.Value.ToString("R", CultureInfo.InvariantCulture) : "host cover",
                ["Hooks"] = input.HasTopAnchor + ":" + input.HasDowel,
                ["BarCount"] = input.MainBarQty.ToString(CultureInfo.InvariantCulture),
                ["Zones"] = input.ZoneA1Length.ToString("R", CultureInfo.InvariantCulture),
                ["Orientation"] = input.Column.HandOrientation.ToString() + " | " + input.Column.FacingOrientation.ToString()
            };
        }

        public static IDictionary<string, string> Describe(RectangularColumnRebarInput input)
        {
            RectangularColumnGeometryHelper.ColumnProfile profile = RectangularColumnGeometryHelper.GetRectangularProfile(input.Column);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HostId"] = input.Column.UniqueId,
                ["HostGeometry"] = profile.B.ToString("R", CultureInfo.InvariantCulture) + "x" + profile.H.ToString("R", CultureInfo.InvariantCulture) + " @ " + profile.BaseCenter,
                ["BarTypes"] = TypeFingerprint(input.MainBarType) + " | " + TypeFingerprint(input.StirrupBarType),
                ["Diameters"] = input.MainBarType.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture) + " | " + input.StirrupBarType.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture),
                ["Spacing"] = input.StirrupSpacingA1.ToString("R", CultureInfo.InvariantCulture) + " | " + input.StirrupSpacingA2.ToString("R", CultureInfo.InvariantCulture),
                ["Cover"] = input.CustomCoverFeet.HasValue ? input.CustomCoverFeet.Value.ToString("R", CultureInfo.InvariantCulture) : "host cover",
                ["Hooks"] = input.HasTopAnchor + ":" + input.HasDowel,
                ["Layout"] = input.TieLayout.ToString(),
                ["BarCount"] = input.BarsAlongB + "x" + input.BarsAlongH,
                ["Zones"] = input.ZoneA1Length.ToString("R", CultureInfo.InvariantCulture),
                ["Orientation"] = input.Column.HandOrientation.ToString() + " | " + input.Column.FacingOrientation.ToString()
            };
        }

        public static string Fingerprint(BeamRebarInput input)
        {
            if (input == null || input.Beam == null) throw new ArgumentException("A beam host is required for preview.", "input");
            BeamGeometryHelper.BeamProfile profile = BeamGeometryHelper.GetBeamProfile(input.Beam);
            if (profile == null) throw new InvalidOperationException("Beam geometry is unavailable for preview.");
            string reinforcementContext = new BeamRebarGenerator(input.Beam.Document)
                .GetReinforcementContextFingerprint(input.Beam);
            return WorkflowFingerprint.Compute(new[]
            {
                input.Beam.UniqueId, input.Beam.VersionGuid.ToString("D"),
                reinforcementContext,
                TypeFingerprint(input.MainTopBarType), TypeFingerprint(input.MainBottomBarType),
                TypeFingerprint(input.TopLeftExtraBarType), TypeFingerprint(input.TopRightExtraBarType),
                TypeFingerprint(input.BottomMidExtraBarType), TypeFingerprint(input.StirrupBarType), TypeFingerprint(input.SideBarType),
                input.TopContinuousQty.ToString(CultureInfo.InvariantCulture), input.BottomContinuousQty.ToString(CultureInfo.InvariantCulture),
                input.TopLeftExtraQty.ToString(CultureInfo.InvariantCulture), input.TopRightExtraQty.ToString(CultureInfo.InvariantCulture),
                input.BottomMidExtraQty.ToString(CultureInfo.InvariantCulture), input.AutoSideBars.ToString(),
                input.SideBarQty.ToString(CultureInfo.InvariantCulture), input.SideBarThresholdMm.ToString("R", CultureInfo.InvariantCulture),
                input.HangerStirrupQty.ToString(CultureInfo.InvariantCulture), input.HangerStirrupSpacingMm.ToString("R", CultureInfo.InvariantCulture),
                input.StirrupSpacingA1.ToString("R", CultureInfo.InvariantCulture), input.StirrupSpacingA2.ToString("R", CultureInfo.InvariantCulture),
                input.ZoneA1Length.ToString("R", CultureInfo.InvariantCulture),
                input.CustomCoverFeet.HasValue ? input.CustomCoverFeet.Value.ToString("R", CultureInfo.InvariantCulture) : "auto-cover",
                input.DesignStandard.ToString(), input.ConcreteGrade.ToString(), input.SteelGrade.ToString(),
                input.LdMultiplier.ToString("R", CultureInfo.InvariantCulture), input.HookTailMultiplier.ToString("R", CultureInfo.InvariantCulture),
                profile.StartPoint.X.ToString("R", CultureInfo.InvariantCulture), profile.StartPoint.Y.ToString("R", CultureInfo.InvariantCulture), profile.StartPoint.Z.ToString("R", CultureInfo.InvariantCulture),
                profile.Direction.X.ToString("R", CultureInfo.InvariantCulture), profile.Direction.Y.ToString("R", CultureInfo.InvariantCulture), profile.Direction.Z.ToString("R", CultureInfo.InvariantCulture),
                profile.RightVector.X.ToString("R", CultureInfo.InvariantCulture), profile.RightVector.Y.ToString("R", CultureInfo.InvariantCulture), profile.RightVector.Z.ToString("R", CultureInfo.InvariantCulture),
                profile.UpVector.X.ToString("R", CultureInfo.InvariantCulture), profile.UpVector.Y.ToString("R", CultureInfo.InvariantCulture), profile.UpVector.Z.ToString("R", CultureInfo.InvariantCulture),
                profile.B.ToString("R", CultureInfo.InvariantCulture), profile.H.ToString("R", CultureInfo.InvariantCulture), profile.Length.ToString("R", CultureInfo.InvariantCulture)
            });
        }

        public static IDictionary<string, string> Describe(BeamRebarInput input)
        {
            BeamGeometryHelper.BeamProfile profile = BeamGeometryHelper.GetBeamProfile(input.Beam);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HostId"] = input.Beam.UniqueId,
                ["HostGeometry"] = profile.B.ToString("R", CultureInfo.InvariantCulture) + "x" + profile.H.ToString("R", CultureInfo.InvariantCulture) + " L=" + profile.Length.ToString("R", CultureInfo.InvariantCulture),
                ["BarTypes"] = string.Join(" | ", new[] { input.MainTopBarType, input.MainBottomBarType, input.TopLeftExtraBarType, input.TopRightExtraBarType, input.BottomMidExtraBarType, input.StirrupBarType, input.SideBarType }.Select(TypeFingerprint)),
                ["Diameters"] = string.Join(" | ", new[] { input.MainTopBarType, input.MainBottomBarType, input.TopLeftExtraBarType, input.TopRightExtraBarType, input.BottomMidExtraBarType, input.StirrupBarType, input.SideBarType }.Select(type => type == null ? "default" : type.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture))),
                ["Spacing"] = input.StirrupSpacingA1.ToString("R", CultureInfo.InvariantCulture) + " | " + input.StirrupSpacingA2.ToString("R", CultureInfo.InvariantCulture),
                ["Cover"] = input.CustomCoverFeet.HasValue ? input.CustomCoverFeet.Value.ToString("R", CultureInfo.InvariantCulture) : "host cover",
                ["Hooks"] = input.HookTailMultiplier.ToString("R", CultureInfo.InvariantCulture) + " | " + input.LdMultiplier.ToString("R", CultureInfo.InvariantCulture),
                ["BarCount"] = input.TopContinuousQty + "/" + input.BottomContinuousQty + "/" + input.TopLeftExtraQty + "/" + input.TopRightExtraQty + "/" + input.BottomMidExtraQty + "/" + input.SideBarQty,
                ["Zones"] = input.ZoneA1Length.ToString("R", CultureInfo.InvariantCulture),
                ["Orientation"] = profile.Direction + " | " + profile.RightVector + " | " + profile.UpVector
            };
        }

        public static string Fingerprint(SlabPanel panel, IList<RebarBarType> barTypes)
        {
            if (panel == null || panel.HostFloor == null || panel.Config == null)
                throw new ArgumentException("A slab panel, host and configuration are required for preview.", "panel");
            if (barTypes == null) throw new ArgumentNullException("barTypes");
            SlabPanelRebarConfig config = panel.Config;
            return WorkflowFingerprint.Compute(new[]
            {
                panel.HostFloor.UniqueId, panel.HostFloor.VersionGuid.ToString("D"), panel.PanelId,
                panel.WidthMm.ToString("R", CultureInfo.InvariantCulture), panel.LengthMm.ToString("R", CultureInfo.InvariantCulture),
                panel.ThicknessFeet.ToString("R", CultureInfo.InvariantCulture), panel.CoverTopFeet.ToString("R", CultureInfo.InvariantCulture),
                panel.CoverBottomFeet.ToString("R", CultureInfo.InvariantCulture), CurveLoopFingerprint(panel.Boundary),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.BottomLayer.DiaXLabel)),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.BottomLayer.DiaYLabel)),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.TopLayer.DiaXLabel)),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.TopLayer.DiaYLabel)),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.HatReinforce.DiaXLabel)),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.HatReinforce.DiaYLabel)),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.TopDistribution.DiaLabel)),
                TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, config.Spacer.DiaLabel)),
                string.Join("|", (panel.Openings ?? new List<CurveLoop>()).Select(CurveLoopFingerprint).OrderBy(value => value, StringComparer.Ordinal)),
                string.Join("|", (panel.Edges ?? new List<SlabPanelEdge>()).OrderBy(edge => edge.EdgeIndex).Select(edge =>
                    edge.EdgeIndex + ":" + edge.EdgeType + ":" + (edge.SupportingBeamId == null ? "" : edge.SupportingBeamId.ToString()) + ":" + edge.SkipTopHat + ":" + edge.SkipBottomMesh)),
                LayerFingerprint(config.BottomLayer), LayerFingerprint(config.TopLayer),
                config.HatReinforce.Enabled.ToString(), config.HatReinforce.DiaXLabel, config.HatReinforce.DiaYLabel,
                config.HatReinforce.SpacingXMm.ToString("R", CultureInfo.InvariantCulture), config.HatReinforce.SpacingYMm.ToString("R", CultureInfo.InvariantCulture),
                config.HatReinforce.IsFullSpan.ToString(), config.HatReinforce.HatFactor, config.HatReinforce.HookDownEdge.ToString(), config.HatReinforce.HookDownLenMm.ToString("R", CultureInfo.InvariantCulture),
                config.TopDistribution.Enabled.ToString(), config.TopDistribution.DiaLabel, config.TopDistribution.SpacingMm.ToString("R", CultureInfo.InvariantCulture),
                config.Spacer.Enabled.ToString(), config.Spacer.DiaLabel, config.Spacer.StepXMm.ToString("R", CultureInfo.InvariantCulture), config.Spacer.StepYMm.ToString("R", CultureInfo.InvariantCulture), config.Spacer.HookLenMm.ToString("R", CultureInfo.InvariantCulture),
                config.Anchors.BeamAnchorAMm.ToString("R", CultureInfo.InvariantCulture), config.Anchors.SlabAnchorBMm.ToString("R", CultureInfo.InvariantCulture),
                config.Tolerances.RoundingMm.ToString("R", CultureInfo.InvariantCulture), config.Tolerances.MinSpanMm.ToString("R", CultureInfo.InvariantCulture)
            });
        }

        public static IDictionary<string, string> Describe(SlabPanel panel, IList<RebarBarType> barTypes)
        {
            SlabPanelRebarConfig config = panel.Config;
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HostId"] = panel.HostFloor.UniqueId,
                ["HostGeometry"] = panel.ThicknessFeet.ToString("R", CultureInfo.InvariantCulture) + "; " + CurveLoopFingerprint(panel.Boundary),
                ["BarTypes"] = string.Join(" | ", new[] { config.BottomLayer.DiaXLabel, config.BottomLayer.DiaYLabel, config.TopLayer.DiaXLabel, config.TopLayer.DiaYLabel, config.HatReinforce.DiaXLabel, config.HatReinforce.DiaYLabel }.Select(label => TypeFingerprint(SlabRebarGenerator.FindBarType(barTypes, label)))),
                ["Diameters"] = string.Join(" | ", new[] { config.BottomLayer.DiaXLabel, config.BottomLayer.DiaYLabel, config.TopLayer.DiaXLabel, config.TopLayer.DiaYLabel, config.HatReinforce.DiaXLabel, config.HatReinforce.DiaYLabel }.Select(label => SlabRebarGenerator.FindBarType(barTypes, label)?.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture) ?? "default")),
                ["Spacing"] = config.BottomLayer.SpacingXMm + "/" + config.BottomLayer.SpacingYMm + " | " + config.TopLayer.SpacingXMm + "/" + config.TopLayer.SpacingYMm,
                ["Cover"] = panel.CoverTopFeet + "/" + panel.CoverBottomFeet,
                ["Hooks"] = config.HatReinforce.HookDownEdge + ":" + config.HatReinforce.HookDownLenMm,
                ["Layout"] = config.BottomLayer.InvertLayer + ":" + config.TopLayer.InvertLayer,
                ["Zones"] = config.HatReinforce.HatFactor + ":" + config.HatReinforce.IsFullSpan,
                ["BarCount"] = "Generated by production SlabRebarGenerator",
                ["Orientation"] = "World X/Y along boundary basis"
            };
        }

        public static string Fingerprint(FoundationProfile profile, FoundationRebarSettings settings)
        {
            if (profile?.FoundationElement == null || profile.BoundingBox == null || settings == null)
                throw new ArgumentException("A foundation host, analyzed bounding box and settings are required for preview.");

            BoundingBoxXYZ bounds = profile.BoundingBox;
            List<RebarBarType> types = new FilteredElementCollector(profile.FoundationElement.Document)
                .OfClass(typeof(RebarBarType)).Cast<RebarBarType>().ToList();
            string[] labels =
            {
                settings.BotXDiaLabel, settings.BotYDiaLabel, settings.TopXDiaLabel, settings.TopYDiaLabel,
                settings.SideTieDiaLabel, settings.DowelDiaLabel, settings.DowelStirrupDiaLabel,
                settings.PerimeterStirrupDiaLabel
            };
            return WorkflowFingerprint.Compute(new[]
            {
                profile.FoundationElement.UniqueId,
                profile.FoundationElement.VersionGuid.ToString("D"),
                BoundsFingerprint(bounds),
                Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.None),
                string.Join("|", labels.Select(label => FoundationRebarGenerator.FindBarType(types, label)).Select(TypeFingerprint))
            });
        }

        public static IDictionary<string, string> Describe(FoundationProfile profile, FoundationRebarSettings settings)
        {
            if (profile?.FoundationElement == null || profile.BoundingBox == null || settings == null)
                throw new ArgumentException("A foundation host, analyzed bounding box and settings are required for preview.");
            BoundingBoxXYZ bounds = profile.BoundingBox;
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HostId"] = profile.FoundationElement.UniqueId,
                ["HostGeometry"] = BoundsFingerprint(bounds),
                ["BarTypes"] = string.Join(" | ", new[] { settings.BotXDiaLabel, settings.BotYDiaLabel, settings.TopXDiaLabel, settings.TopYDiaLabel,
                    settings.DowelDiaLabel, settings.DowelStirrupDiaLabel, settings.PerimeterStirrupDiaLabel }),
                ["Spacing"] = settings.BotXSpacingMm.ToString("R", CultureInfo.InvariantCulture) + "/" + settings.BotYSpacingMm.ToString("R", CultureInfo.InvariantCulture) +
                    "; perimeter=" + settings.PerimeterStirrupSpacingMm.ToString("R", CultureInfo.InvariantCulture),
                ["Cover"] = settings.CustomCoverMm.ToString("R", CultureInfo.InvariantCulture),
                ["Hooks"] = settings.BotXHookUp + "/" + settings.BotYHookUp + "; dowel inward=" + settings.DowelLegInward,
                ["Layout"] = "FoundationRebarGenerator production output",
                ["Zones"] = "Dowels=" + settings.EnableColumnDowels + "; top mesh=" + settings.EnableTopMesh,
                ["BarCount"] = "Generated by production FoundationRebarGenerator",
                ["Orientation"] = "Foundation bounding-box model axes"
            };
        }

        private static string BoundsFingerprint(BoundingBoxXYZ bounds) => string.Join(";", new[]
        {
            bounds.Min.X.ToString("R", CultureInfo.InvariantCulture), bounds.Min.Y.ToString("R", CultureInfo.InvariantCulture), bounds.Min.Z.ToString("R", CultureInfo.InvariantCulture),
            bounds.Max.X.ToString("R", CultureInfo.InvariantCulture), bounds.Max.Y.ToString("R", CultureInfo.InvariantCulture), bounds.Max.Z.ToString("R", CultureInfo.InvariantCulture)
        });

        private static string TypeFingerprint(RebarBarType type) => type == null ? "<default>" :
            type.UniqueId + ":" + type.VersionGuid.ToString("D") + ":" + type.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture);

        public static bool HasExistingDuplicateBar(Document document, Element host, RebarPreviewSnapshot snapshot, string inputFingerprint)
        {
            RebarPreviewComponent expected = snapshot?.Find(inputFingerprint);
            if (document == null || host == null || expected == null) return false;
            HashSet<string> expectedBars = new HashSet<string>(expected.BarFingerprints, StringComparer.OrdinalIgnoreCase);
            return new FilteredElementCollector(document).OfClass(typeof(Rebar)).Cast<Rebar>()
                .Where(bar => bar.GetHostId() == host.Id)
                .Select(FingerprintBar)
                .Any(expectedBars.Contains);
        }

        internal static string FingerprintBar(Rebar bar)
        {
            IReadOnlyList<RebarPreviewPath> paths = ReadPaths(new List<Rebar> { bar });
            return FingerprintBar(bar, paths);
        }

        internal static string FingerprintBar(Rebar bar, IReadOnlyList<RebarPreviewPath> paths)
        {
            RebarBarType type = bar.Document.GetElement(bar.GetTypeId()) as RebarBarType;
            return WorkflowFingerprint.Compute(new[]
            {
                TypeFingerprint(type), Fingerprint(paths)
            });
        }

        internal static string FingerprintBars(IEnumerable<string> fingerprints) =>
            WorkflowFingerprint.Compute((fingerprints ?? Enumerable.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal));

        private static string LayerFingerprint(SlabLayerSettings layer) => string.Join(":", new[]
        {
            layer.Enabled.ToString(), layer.InvertLayer.ToString(), layer.DiaXLabel, layer.SpacingXMm.ToString("R", CultureInfo.InvariantCulture),
            layer.DiaYLabel, layer.SpacingYMm.ToString("R", CultureInfo.InvariantCulture), layer.ExtraParam
        });

        private static string CurveLoopFingerprint(CurveLoop loop)
        {
            if (loop == null) return string.Empty;
            return WorkflowFingerprint.Compute(loop.Select(curve => string.Join(";", curve.Tessellate().Select(point =>
                point.X.ToString("R", CultureInfo.InvariantCulture) + "," + point.Y.ToString("R", CultureInfo.InvariantCulture) + "," + point.Z.ToString("R", CultureInfo.InvariantCulture)))));
        }

        internal static IReadOnlyList<RebarPreviewPath> ReadPaths(IList<Rebar> bars, string role = null)
        {
            var paths = new List<RebarPreviewPath>();
            foreach (Rebar bar in bars)
            {
                IList<Curve> curves = bar.GetCenterlineCurves(false, false, false,
                    MultiplanarOption.IncludeAllMultiplanarCurves, 0);
                foreach (Curve curve in curves)
                {
                    IList<XYZ> points = curve.Tessellate();
                    if (points.Count < 2) continue;
                    paths.Add(new RebarPreviewPath(points.Select(p => new RebarPreviewPoint(p.X, p.Y, p.Z)), role));
                }
            }
            if (paths.Count == 0) throw new InvalidOperationException("Revit returned no solved Rebar centerline paths.");
            return paths;
        }

        internal static string Fingerprint(IEnumerable<RebarPreviewPath> paths)
        {
            string[] tokens = (paths ?? Enumerable.Empty<RebarPreviewPath>())
                .Select(path =>
                {
                    string[] points = path.Points.Select(p =>
                    p.X.ToString("R", CultureInfo.InvariantCulture) + "," +
                    p.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
                    p.Z.ToString("R", CultureInfo.InvariantCulture)).ToArray();
                    string forward = string.Join(";", points);
                    Array.Reverse(points);
                    string reverse = string.Join(";", points);
                    return string.CompareOrdinal(forward, reverse) <= 0 ? forward : reverse;
                })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return WorkflowFingerprint.Compute(tokens);
        }

    }
}

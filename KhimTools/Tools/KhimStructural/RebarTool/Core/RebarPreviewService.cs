using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Workflow;

namespace KhimTools.RebarTool.Core
{
    /// <summary>One production generator invocation included in a detached preview capture.</summary>
    public sealed class RebarPreviewRequest
    {
        public string InputFingerprint { get; private set; }
        public Func<IList<Rebar>> Generate { get; private set; }

        public RebarPreviewRequest(string inputFingerprint, Func<IList<Rebar>> generate)
        {
            if (string.IsNullOrWhiteSpace(inputFingerprint)) throw new ArgumentException("A stable input fingerprint is required.", "inputFingerprint");
            InputFingerprint = inputFingerprint;
            Generate = generate ?? throw new ArgumentNullException("generate");
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
        public RebarPreviewPath(IEnumerable<RebarPreviewPoint> points) { Points = (points ?? Enumerable.Empty<RebarPreviewPoint>()).ToArray(); }
    }

    public sealed class RebarPreviewComponent
    {
        public string InputFingerprint { get; private set; }
        public int BarCount { get; private set; }
        public string GeometryFingerprint { get; private set; }
        public IReadOnlyList<RebarPreviewPath> Paths { get; private set; }

        internal RebarPreviewComponent(string inputFingerprint, IList<Rebar> bars)
        {
            InputFingerprint = inputFingerprint;
            BarCount = bars.Count;
            Paths = RebarPreviewService.ReadPaths(bars);
            GeometryFingerprint = RebarPreviewService.Fingerprint(Paths);
        }
    }

    public sealed class RebarPreviewSnapshot
    {
        public IReadOnlyList<RebarPreviewComponent> Components { get; private set; }
        public string DocumentFingerprint { get; private set; }
        internal RebarPreviewSnapshot(IList<RebarPreviewComponent> components, string documentFingerprint)
        {
            Components = components.ToArray();
            DocumentFingerprint = documentFingerprint;
        }

        public RebarPreviewComponent Find(string inputFingerprint) =>
            Components.FirstOrDefault(c => WorkflowFingerprint.Matches(c.InputFingerprint, inputFingerprint));
    }

    /// <summary>
    /// Runs the real Rebar generators and Revit shape solver in a rollback-only transaction,
    /// copies solved centerlines into detached data, then proves the model fingerprint is unchanged.
    /// The exact detached result is compared with the real transaction before it may commit.
    /// </summary>
    public static class RebarPreviewService
    {
        public static RebarPreviewSnapshot Capture(Document document, IEnumerable<RebarPreviewRequest> requests)
        {
            if (document == null) throw new ArgumentNullException("document");
            RebarPreviewRequest[] requestArray = (requests ?? Enumerable.Empty<RebarPreviewRequest>()).ToArray();
            if (requestArray.Length == 0) throw new ArgumentException("At least one Rebar preview request is required.", "requests");

            string before = DocumentFingerprint(document);
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
                        components.Add(new RebarPreviewComponent(request.InputFingerprint, bars));
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

            string after = DocumentFingerprint(document);
            if (!WorkflowFingerprint.Matches(before, after))
                throw new InvalidOperationException("The Rebar preview changed persistent document state; preview output was discarded.");
            return new RebarPreviewSnapshot(components, after);
        }

        public static bool Matches(RebarPreviewSnapshot snapshot, string inputFingerprint, IList<Rebar> generated)
        {
            if (snapshot == null || generated == null || generated.Count == 0) return false;
            RebarPreviewComponent expected = snapshot.Find(inputFingerprint);
            if (expected == null || expected.BarCount != generated.Count) return false;
            return WorkflowFingerprint.Matches(expected.GeometryFingerprint, Fingerprint(ReadPaths(generated)));
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

        internal static IReadOnlyList<RebarPreviewPath> ReadPaths(IList<Rebar> bars)
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
                    paths.Add(new RebarPreviewPath(points.Select(p => new RebarPreviewPoint(p.X, p.Y, p.Z))));
                }
            }
            if (paths.Count == 0) throw new InvalidOperationException("Revit returned no solved Rebar centerline paths.");
            return paths;
        }

        internal static string Fingerprint(IEnumerable<RebarPreviewPath> paths)
        {
            string[] tokens = (paths ?? Enumerable.Empty<RebarPreviewPath>())
                .Select(path => string.Join(";", path.Points.Select(p =>
                    p.X.ToString("R", CultureInfo.InvariantCulture) + "," +
                    p.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
                    p.Z.ToString("R", CultureInfo.InvariantCulture))))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return WorkflowFingerprint.Compute(tokens);
        }

        private static string DocumentFingerprint(Document document)
        {
            var tokens = new List<string>();
            foreach (Element element in new FilteredElementCollector(document))
                tokens.Add(element.UniqueId + ":" + element.VersionGuid.ToString("D"));
            tokens.Sort(StringComparer.Ordinal);
            return WorkflowFingerprint.Compute(tokens);
        }
    }
}

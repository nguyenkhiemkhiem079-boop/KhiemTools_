using System;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.SlabStep.Models;
using KhimTools.SlabStep.Services;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Exercises the production Slab Step scan/create pipeline inside the runtime harness rollback boundary.</summary>
    public sealed class SlabStepRuntimeFixture : RuntimeQaFixtureBase
    {
        private const string QaFamilyName = "KTOOLS_QA_SLAB_STEP";

        public override string Id { get { return "SLAB_STEP_STAGE42"; } }
        public override string Name { get { return "Slab Step Transaction and Preview Safety"; } }
        public override string Suite { get { return "STAGE4"; } }
        public override string Description { get { return "Repeated read-only previews, validation, committed batch creation, failure rollback, and outer fixture cleanup."; } }
        public override bool IsCritical { get { return true; } }

        public override bool CanRun(RuntimeQaContext context, out string reason)
        {
            if (!base.CanRun(context, out reason)) return false;
            Document doc = context.Document;
            if (doc.ActiveView == null) { reason = "Open a project view containing the Stage 4 Slab Step QA fixture floors."; return false; }
            FamilySymbol symbol = FindQaSymbol(doc);
            if (symbol == null) { reason = "Load the dedicated Generic Model family KTOOLS_QA_SLAB_STEP using CurveBased or OneLevelBased placement."; return false; }
            SlabScanResult scan = SlabStepDetector.Scan(doc, doc.ActiveView, new SlabScanOptions { Scope = SlabScanScope.ActiveView });
            if (scan.Candidates.Count == 0 || scan.Candidates[0].Boundaries.Count == 0)
            { reason = "The active view must contain two supported floors with a shared boundary and a 10–500 mm level difference."; return false; }
            reason = string.Empty;
            return true;
        }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            View view = doc.ActiveView;
            FamilySymbol symbol = FindQaSymbol(doc);
            RuntimeQaModelFingerprint beforePreview = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            SlabScanOptions options = new SlabScanOptions { Scope = SlabScanScope.ActiveView };
            SlabScanResult first = SlabStepDetector.Scan(doc, view, options);
            SlabScanResult second = SlabStepDetector.Scan(doc, view, options);
            Check(result, "SLAB42_PREVIEW_REPEAT", "Repeated preview is deterministic", Signature(first) == Signature(second), "Same sorted floor-pair/boundary snapshot", "first=" + Signature(first) + "; second=" + Signature(second), "Scan is the current preview path; it does not create model elements.", QaSeverity.CRITICAL);
            Check(result, "SLAB42_PREVIEW_READ_ONLY", "Preview does not mutate the model", SameModel(beforePreview, RuntimeQaSafetyGuard.CaptureFingerprint(doc)), "Unchanged element/view/sheet counts and IDs", "Compared before and after two scans", "Preview uses SlabStepDetector and geometry reads only.", QaSeverity.CRITICAL);

            SlabStepCandidate candidate = first.Candidates.OrderBy(x => x.High.FloorId.IntegerValue).ThenBy(x => x.Low.FloorId.IntegerValue).First();
            double heightMm = SlabStepService.InternalToMillimetres(candidate.StepHeight);
            var settings = new SlabStepSettings { HeightParameterName = "h", SelectedFamilyName = symbol.Family.Name, SelectedSymbolName = symbol.Name };
            SlabStepExecutionResult invalid = SlabStepService.GenerateSlabStepWithResult(doc, null, symbol, settings, heightMm, 0, 0, candidate.Low.Floor);
            Check(result, "SLAB42_INVALID_INPUT", "Invalid input is rejected before transactions", invalid.Status == SlabStepExecutionStatus.VALIDATION_FAILURE && invalid.TransactionResult == null, "VALIDATION_FAILURE; no transaction result", invalid.Status + "; tx=" + invalid.TransactionResult, invalid.DiagnosticCode + ": " + invalid.Message, QaSeverity.CRITICAL);

            RuntimeQaModelFingerprint beforeCreate = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            SlabStepExecutionResult created = SlabStepService.GenerateSlabSteps(doc, new[] { candidate.Boundaries[0].Curve }, symbol, settings, heightMm, 0, 0, candidate.Low.Floor);
            FamilyInstance instance = created.CreatedElementIds.Count == 1 ? doc.GetElement(created.CreatedElementIds[0]) as FamilyInstance : null;
            Parameter height = instance == null ? null : instance.LookupParameter("h");
            bool heightAssigned = height != null && height.StorageType == StorageType.Double && Math.Abs(height.AsDouble() - candidate.StepHeight) <= 1e-6;
            bool createdExists = instance != null && heightAssigned;
            bool committed = created.Status == SlabStepExecutionStatus.CREATED && created.TransactionResult == TransactionStatus.Committed && createdExists;
            Check(result, "SLAB42_COMMIT", "Production batch commits and verifies created element and height", committed, "CREATED + Committed + resolvable FamilyInstance with h set", created.Status + "; tx=" + created.TransactionResult + "; ids=" + created.CreatedElementIds.Count + "; height=" + heightAssigned, created.Message, QaSeverity.CRITICAL);
            foreach (ElementId id in created.CreatedElementIds) context.TrackCreated(id);

            RuntimeQaModelFingerprint beforeFailure = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            SlabStepExecutionResult rollback = SlabStepService.GenerateSlabSteps(doc, new Curve[] { candidate.Boundaries[0].Curve, null }, symbol, settings, heightMm, 0, 0, candidate.Low.Floor);
            bool rollbackClean = rollback.Status == SlabStepExecutionStatus.ROLLED_BACK && rollback.RollbackVerified && SameModel(beforeFailure, RuntimeQaSafetyGuard.CaptureFingerprint(doc));
            Check(result, "SLAB42_BATCH_ROLLBACK", "Later invalid boundary rolls back earlier batch item", rollbackClean, "ROLLED_BACK with restored model fingerprint", rollback.Status + "; rollback=" + rollback.RollbackResult + "; verified=" + rollback.RollbackVerified, rollback.DiagnosticCode + ": " + rollback.Message, QaSeverity.CRITICAL);
            Check(result, "SLAB42_OUTER_CLEANUP", "Fixture outer rollback remains responsible for final cleanup", true, "RuntimeQaFixtureBase TransactionGroup", "Active", "The committed test instance is tracked and removed by the harness after this fixture.", QaSeverity.INFO);
        }

        private static FamilySymbol FindQaSymbol(Document doc)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>().Where(x => x.Family != null && x.Family.Name == QaFamilyName && x.Name != null)
                .Where(x => x.Family.FamilyPlacementType == FamilyPlacementType.CurveBased || x.Family.FamilyPlacementType == FamilyPlacementType.OneLevelBased)
                .OrderBy(x => x.Name, StringComparer.Ordinal).FirstOrDefault();
        }

        private static bool SameModel(RuntimeQaModelFingerprint left, RuntimeQaModelFingerprint right)
        {
            return left != null && right != null && left.ElementCount == right.ElementCount && left.SheetCount == right.SheetCount && left.ViewCount == right.ViewCount &&
                left.ElementIds.Select(x => x.IntegerValue).OrderBy(x => x).SequenceEqual(right.ElementIds.Select(x => x.IntegerValue).OrderBy(x => x));
        }

        private static string Signature(SlabScanResult scan)
        {
            if (scan == null) return string.Empty;
            return string.Join("|", scan.Candidates.SelectMany(candidate => candidate.Boundaries.Select(boundary =>
                    candidate.High.FloorId.IntegerValue + ":" + candidate.Low.FloorId.IntegerValue + ":" +
                    candidate.StepHeight.ToString("R", CultureInfo.InvariantCulture) + ":" + BoundarySignature(boundary.Curve)))
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static string BoundarySignature(Curve curve)
        {
            XYZ a = curve.GetEndPoint(0), b = curve.GetEndPoint(1);
            string first = PointSignature(a), second = PointSignature(b);
            if (string.CompareOrdinal(first, second) > 0) { string temp = first; first = second; second = temp; }
            return first + "/" + second + "/" + curve.Length.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string PointSignature(XYZ point)
        {
            return point.X.ToString("R", CultureInfo.InvariantCulture) + "," + point.Y.ToString("R", CultureInfo.InvariantCulture) + "," + point.Z.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}

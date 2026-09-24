using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Workflow;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Models;
using KhimTools.RebarTool.Commands;

namespace KhimTools.RuntimeQa.Fixtures
{
    public abstract class BeamPreviewRuntimeFixtureBase : RuntimeQaFixtureBase
    {
        protected abstract bool IsRotated(BeamGeometryHelper.BeamProfile profile);
        protected abstract string ScenarioLabel { get; }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            FamilyInstance beam = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .FirstOrDefault(candidate =>
                {
                    BeamGeometryHelper.BeamProfile profile = BeamGeometryHelper.GetBeamProfile(candidate);
                    return profile != null && IsRotated(profile);
                });
            RebarBarType barType = RuntimeQaFixtureHelpers.FindBarType(doc, 16);
            if (beam == null || barType == null)
            {
                Block(result, Id + "_RES", ScenarioLabel + " beam resources", "Compatible beam host and N16 RebarBarType",
                    "BLOCKED: load the required " + ScenarioLabel + " beam fixture and N16 type.", QaSeverity.CRITICAL);
                return;
            }

            var input = new BeamRebarInput
            {
                Beam = beam,
                MainTopBarType = barType,
                MainBottomBarType = barType,
                TopLeftExtraBarType = barType,
                TopRightExtraBarType = barType,
                BottomMidExtraBarType = barType,
                StirrupBarType = barType,
                SideBarType = barType,
                TopContinuousQty = 2,
                BottomContinuousQty = 2,
                TopLeftExtraQty = 1,
                TopRightExtraQty = 1,
                BottomMidExtraQty = 1,
                SideBarQty = 0,
                AutoSideBars = false
            };
            var generator = new BeamRebarGenerator(doc);
            string fingerprint = RebarPreviewService.Fingerprint(input);
            var request = new RebarPreviewRequest(fingerprint, () => generator.Generate(input),
                () => RebarPreviewService.Fingerprint(input), RebarPreviewService.Describe(input));
            int barsBefore = CountHostedBars(doc, beam);
            RebarPreviewSnapshot first = RebarPreviewService.Capture(doc, new[] { request });
            RebarPreviewSnapshot refreshed = RebarPreviewService.Capture(doc, new[] { request });
            RebarPreviewComponent expected = first.Find(fingerprint);
            RebarPreviewComponent repeated = refreshed.Find(fingerprint);
            Check(result, Id + "_REFRESH", "Repeated refresh is deterministic",
                expected != null && repeated != null && expected.BarCount == repeated.BarCount &&
                WorkflowFingerprint.Matches(expected.GeometryFingerprint, repeated.GeometryFingerprint),
                "Identical plan and centerlines", repeated == null ? "No preview" : repeated.BarCount.ToString(),
                "Refresh uses the same production BeamRebarGenerator and canonical input.", QaSeverity.CRITICAL);
            double originalSpacing = input.StirrupSpacingA1;
            input.StirrupSpacingA1 = originalSpacing + 0.125;
            string changedFingerprint = RebarPreviewService.Fingerprint(input);
            bool staleRejected = refreshed.Find(changedFingerprint) == null;
            RebarPreviewSnapshot afterRefresh = RebarPreviewService.Capture(doc, new[]
            {
                new RebarPreviewRequest(changedFingerprint, () => generator.Generate(input),
                    () => RebarPreviewService.Fingerprint(input), RebarPreviewService.Describe(input))
            });
            bool refreshedPlanAvailable = afterRefresh.Find(changedFingerprint) != null;
            input.StirrupSpacingA1 = originalSpacing;
            Check(result, Id + "_STALE", "Changed beam input invalidates preview", staleRejected,
                "Changed spacing requires refresh", staleRejected ? "Stale" : "Still executable",
                "Input fingerprints cover spacing, geometry, type and generation settings.", QaSeverity.CRITICAL);
            Check(result, Id + "_REFRESH_CHANGED", "Refreshing changed beam input creates a new plan",
                refreshedPlanAvailable, changedFingerprint, afterRefresh.PlanFingerprint,
                "The changed request is re-solved after the old snapshot rejects it.", QaSeverity.CRITICAL);
            Check(result, Id + "_CANCEL", "Preview capture leaves no hosted bars", CountHostedBars(doc, beam) == barsBefore,
                barsBefore.ToString(), CountHostedBars(doc, beam).ToString(),
                "Closing/canceling the detached review cannot persist rollback-only generated bars.", QaSeverity.CRITICAL);

            bool matched = false;
            bool duplicateDetected = false;
            TransactionStatus rolledBack = TransactionStatus.Uninitialized;
            using (var tx = new Transaction(doc, "K-TOOLS QA " + ScenarioLabel + " beam preview parity"))
            {
                if (tx.Start() != TransactionStatus.Started)
                {
                    Block(result, Id + "_TX", "Beam parity transaction", "A started transaction",
                        "HOST_VERIFICATION_REQUIRED: transaction could not start (" + tx.GetStatus() + ").", QaSeverity.CRITICAL);
                    return;
                }
                try
                {
                    List<Rebar> bars = generator.Generate(input);
                    doc.Regenerate();
                    matched = RebarPreviewService.Matches(refreshed, fingerprint, bars);
                    duplicateDetected = RebarPreviewService.HasExistingDuplicateBar(doc, beam, refreshed, fingerprint);
                    foreach (Rebar bar in bars ?? new List<Rebar>()) context.TrackCreated(bar.Id);
                }
                finally
                {
                    if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                    rolledBack = tx.GetStatus();
                }
            }
            Check(result, Id + "_PARITY", "Beam preview/execution parity", matched,
                "Bar type and centerline parity", matched ? "Matched" : "Mismatch",
                "Production plan is regenerated in a disposable transaction and always rolled back.", QaSeverity.CRITICAL);
            Check(result, Id + "_DUPLICATE", "Equivalent generated beam bars are detected", duplicateDetected,
                "At least one planned type/centerline signature is found on the host", duplicateDetected ? "Detected" : "Not detected",
                "Duplicate protection is tested while production-generated bars exist in the disposable parity transaction.", QaSeverity.CRITICAL);
            Check(result, Id + "_ROLLBACK", "Beam parity rollback", rolledBack == TransactionStatus.RolledBack,
                TransactionStatus.RolledBack.ToString(), rolledBack.ToString(),
                "RuntimeQaFixtureBase also verifies the enclosing TransactionGroup rollback.", QaSeverity.CRITICAL);
        }

        private static int CountHostedBars(Document document, Element host) =>
            new FilteredElementCollector(document).OfClass(typeof(Rebar)).Cast<Rebar>().Count(bar => bar.GetHostId() == host.Id);
    }

    public sealed class RectangularBeamPreviewRuntimeFixture : BeamPreviewRuntimeFixtureBase
    {
        public override string Id { get { return "BR-PREVIEW-RECT"; } }
        public override string Name { get { return "Rectangular beam solver preview"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Exercise preview refresh, stale-input rejection, model-preserving cancel and production parity for an axis-aligned rectangular beam."; } }
        public override bool IsCritical { get { return true; } }
        protected override bool IsRotated(BeamGeometryHelper.BeamProfile profile) =>
            Math.Abs(profile.Direction.X) > 0.999 || Math.Abs(profile.Direction.Y) > 0.999;
        protected override string ScenarioLabel => "axis-aligned rectangular";
    }

    public sealed class RotatedBeamPreviewRuntimeFixture : BeamPreviewRuntimeFixtureBase
    {
        public override string Id { get { return "BR-PREVIEW-ROTATED"; } }
        public override string Name { get { return "Rotated beam solver preview"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Exercise local-axis aware preview refresh, stale-input rejection, cancel and production parity for a horizontally rotated beam."; } }
        public override bool IsCritical { get { return true; } }
        protected override bool IsRotated(BeamGeometryHelper.BeamProfile profile) =>
            Math.Abs(profile.Direction.X) > 0.1 && Math.Abs(profile.Direction.Y) > 0.1;
        protected override string ScenarioLabel => "rotated";
    }

    public abstract class SlabPreviewRuntimeFixtureBase : RuntimeQaFixtureBase
    {
        protected abstract bool RequiresOpening { get; }
        protected abstract string ScenarioLabel { get; }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            Floor floor = new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>()
                .FirstOrDefault(candidate =>
                {
                    try
                    {
                        SlabProfile profile = SlabGeometryHelper.AnalyzeSlab(doc, candidate);
                        bool hasOpening = profile != null && profile.InnerOpenings != null && profile.InnerOpenings.Count > 0;
                        return profile != null && hasOpening == RequiresOpening;
                    }
                    catch { return false; }
                });
            if (floor == null)
            {
                Block(result, Id + "_RES", ScenarioLabel + " slab resources", RequiresOpening ? "A slab with at least one supported opening" : "A rectangular slab without openings",
                    "BLOCKED: load the required " + ScenarioLabel + " slab fixture.", QaSeverity.CRITICAL);
                return;
            }
            var manager = new SlabPanelManager();
            manager.InitializeFromFloors(doc, new List<Floor> { floor });
            SlabPanel panel = manager.Panels.FirstOrDefault();
            if (panel == null)
            {
                Block(result, Id + "_PANEL", "Slab panel analysis", "A production-analyzed slab panel",
                    "BLOCKED: the selected floor did not produce a supported slab panel.", QaSeverity.CRITICAL);
                return;
            }
            var generator = new SlabRebarGenerator(doc);
            string fingerprint = generator.GetPanelInputFingerprint(panel);
            var report = new RebarGenerationReport();
            var request = new RebarPreviewRequest(fingerprint, () => generator.GeneratePanel(panel, report),
                () => generator.GetPanelInputFingerprint(panel), RebarPreviewService.Describe(panel, generator.BarTypes));
            int barsBefore = CountHostedBars(doc, floor);
            RebarPreviewSnapshot first = RebarPreviewService.Capture(doc, new[] { request });
            RebarPreviewSnapshot refreshed = RebarPreviewService.Capture(doc, new[] { request });
            RebarPreviewComponent expected = first.Find(fingerprint);
            RebarPreviewComponent repeated = refreshed.Find(fingerprint);
            Check(result, Id + "_REFRESH", "Repeated refresh is deterministic",
                expected != null && repeated != null && expected.BarCount == repeated.BarCount &&
                WorkflowFingerprint.Matches(expected.GeometryFingerprint, repeated.GeometryFingerprint),
                "Identical panel plan and centerlines", repeated == null ? "No preview" : repeated.BarCount.ToString(),
                "Refresh uses the production SlabRebarGenerator with the same panel configuration.", QaSeverity.CRITICAL);
            panel.Config.BottomLayer.SpacingXMm += 10;
            string changedFingerprint = generator.GetPanelInputFingerprint(panel);
            bool staleRejected = refreshed.Find(changedFingerprint) == null;
            RebarPreviewSnapshot afterRefresh = RebarPreviewService.Capture(doc, new[]
            {
                new RebarPreviewRequest(changedFingerprint, () => generator.GeneratePanel(panel, new RebarGenerationReport()),
                    () => generator.GetPanelInputFingerprint(panel), RebarPreviewService.Describe(panel, generator.BarTypes))
            });
            bool refreshedPlanAvailable = afterRefresh.Find(changedFingerprint) != null;
            panel.Config.BottomLayer.SpacingXMm -= 10;
            Check(result, Id + "_STALE", "Changed slab spacing invalidates preview", staleRejected,
                "Changed spacing requires refresh", staleRejected ? "Stale" : "Still executable",
                "Fingerprint includes X/Y settings, type identities, boundary, openings and cover.", QaSeverity.CRITICAL);
            Check(result, Id + "_REFRESH_CHANGED", "Refreshing changed slab input creates a new plan",
                refreshedPlanAvailable, changedFingerprint, afterRefresh.PlanFingerprint,
                "The changed request is re-solved after the old snapshot rejects it.", QaSeverity.CRITICAL);
            Check(result, Id + "_CANCEL", "Preview capture leaves no hosted bars", CountHostedBars(doc, floor) == barsBefore,
                barsBefore.ToString(), CountHostedBars(doc, floor).ToString(),
                "Cancel/close after rollback-only capture cannot persist preview bars.", QaSeverity.CRITICAL);

            bool matched = false;
            bool duplicateDetected = false;
            TransactionStatus rolledBack = TransactionStatus.Uninitialized;
            using (var tx = new Transaction(doc, "K-TOOLS QA " + ScenarioLabel + " slab preview parity"))
            {
                if (tx.Start() != TransactionStatus.Started)
                {
                    Block(result, Id + "_TX", "Slab parity transaction", "A started transaction",
                        "HOST_VERIFICATION_REQUIRED: transaction could not start (" + tx.GetStatus() + ").", QaSeverity.CRITICAL);
                    return;
                }
                try
                {
                    List<Rebar> bars = generator.GeneratePanel(panel, new RebarGenerationReport());
                    doc.Regenerate();
                    matched = RebarPreviewService.Matches(refreshed, fingerprint, bars);
                    duplicateDetected = RebarPreviewService.HasExistingDuplicateBar(doc, floor, refreshed, fingerprint);
                    foreach (Rebar bar in bars ?? new List<Rebar>()) context.TrackCreated(bar.Id);
                }
                finally
                {
                    if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                    rolledBack = tx.GetStatus();
                }
            }
            Check(result, Id + "_PARITY", "Slab preview/execution parity", matched,
                "Bar type and centerline parity", matched ? "Matched" : "Mismatch",
                "Production layout is regenerated in a disposable transaction and always rolled back.", QaSeverity.CRITICAL);
            Check(result, Id + "_DUPLICATE", "Equivalent generated slab bars are detected", duplicateDetected,
                "At least one planned type/centerline signature is found on the host", duplicateDetected ? "Detected" : "Not detected",
                "Duplicate protection is tested while production-generated bars exist in the disposable parity transaction.", QaSeverity.CRITICAL);
            Check(result, Id + "_ROLLBACK", "Slab parity rollback", rolledBack == TransactionStatus.RolledBack,
                TransactionStatus.RolledBack.ToString(), rolledBack.ToString(),
                "RuntimeQaFixtureBase verifies the enclosing TransactionGroup rollback.", QaSeverity.CRITICAL);
        }

        private static int CountHostedBars(Document document, Element host) =>
            new FilteredElementCollector(document).OfClass(typeof(Rebar)).Cast<Rebar>().Count(bar => bar.GetHostId() == host.Id);
    }

    public sealed class RectangularSlabPreviewRuntimeFixture : SlabPreviewRuntimeFixtureBase
    {
        public override string Id { get { return "SR-PREVIEW-RECT"; } }
        public override string Name { get { return "Rectangular slab solver preview"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Exercise refresh, cancellation, stale-input protection and production parity for a supported slab without openings."; } }
        public override bool IsCritical { get { return true; } }
        protected override bool RequiresOpening => false;
        protected override string ScenarioLabel => "rectangular";
    }

    public sealed class SlabOpeningPreviewRuntimeFixture : SlabPreviewRuntimeFixtureBase
    {
        public override string Id { get { return "SR-PREVIEW-OPENING"; } }
        public override string Name { get { return "Slab opening solver preview"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Exercise production opening-trim geometry, refresh, cancellation, stale-input protection and preview parity."; } }
        public override bool IsCritical { get { return true; } }
        protected override bool RequiresOpening => true;
        protected override string ScenarioLabel => "opening";
    }

    public sealed class RectangularColumnPreviewRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "RC-PREVIEW"; } }
        public override string Name { get { return "Rectangular Column rollback-only solver preview"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Capture Revit-solved centerlines, prove rollback leaves the model unchanged, then compare preview geometry with the production generator before rollback."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            FamilyInstance column = context.UiDocument.Selection.GetElementIds()
                .Select(id => doc.GetElement(id) as FamilyInstance)
                .FirstOrDefault(c => c != null && !CmdColumnRebar.IsCircular(c));
            if (column == null)
                column = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault(c => !CmdColumnRebar.IsCircular(c));
            RebarBarType main = RuntimeQaFixtureHelpers.FindBarType(doc, 16);
            RebarBarType tie = RuntimeQaFixtureHelpers.FindBarType(doc, 10);
            if (column == null || main == null || tie == null)
            {
                Block(result, "RC-PREVIEW_RES", "Preview resources", "Rectangular column and compatible N16/N10 types",
                    "BLOCKED: select or load a rectangular column and compatible RebarBarTypes.", QaSeverity.CRITICAL);
                return;
            }

            var input = new RectangularColumnRebarInput
            {
                Column = column,
                MainBarType = main,
                StirrupBarType = tie,
                BarsAlongB = 7,
                BarsAlongH = 3,
                TieLayout = ColumnTieLayoutType.MultiCellClosed
            };
            var generator = new RectangularColumnRebarGenerator(doc);
            string fingerprint = RebarPreviewService.Fingerprint(input);
            RebarPreviewSnapshot snapshot = RebarPreviewService.Capture(doc, new[]
            {
                new RebarPreviewRequest(fingerprint, () =>
                {
                    RebarShapeLibrary.PreloadCommonShapes(doc);
                    var previewReport = new RebarGenerationReport();
                    List<Rebar> bars = generator.Generate(input, previewReport);
                    if (previewReport.HasErrors) throw new InvalidOperationException(previewReport.Errors[0].ErrorReason);
                    return bars;
                }, () => RebarPreviewService.Fingerprint(input), RebarPreviewService.Describe(input))
            });
            RebarPreviewSnapshot repeatedSnapshot = RebarPreviewService.Capture(doc, new[]
            {
                new RebarPreviewRequest(fingerprint, () =>
                {
                    RebarShapeLibrary.PreloadCommonShapes(doc);
                    var previewReport = new RebarGenerationReport();
                    List<Rebar> bars = generator.Generate(input, previewReport);
                    if (previewReport.HasErrors) throw new InvalidOperationException(previewReport.Errors[0].ErrorReason);
                    return bars;
                }, () => RebarPreviewService.Fingerprint(input), RebarPreviewService.Describe(input))
            });
            RebarPreviewComponent component = snapshot.Find(fingerprint);
            RebarPreviewComponent repeatedComponent = repeatedSnapshot.Find(fingerprint);
            Check(result, "RC-PREVIEW01", "Detached solver paths and deterministic refresh", component != null && component.BarCount > 0 && component.Paths.Count > 0 && repeatedComponent != null && component.BarCount == repeatedComponent.BarCount && WorkflowFingerprint.Matches(component.GeometryFingerprint, repeatedComponent.GeometryFingerprint),
                "Revit-solved bar paths", component == null ? "missing" : component.Paths.Count + " paths",
                "Capture returned detached curve coordinates after rollback-only generation.", QaSeverity.CRITICAL);

            int originalBars = input.BarsAlongB;
            input.BarsAlongB = originalBars + 1;
            string changedFingerprint = RebarPreviewService.Fingerprint(input);
            bool staleRejected = snapshot.Find(changedFingerprint) == null;
            RebarPreviewSnapshot changedPlan = RebarPreviewService.Capture(doc, new[]
            {
                new RebarPreviewRequest(changedFingerprint, () =>
                {
                    RebarShapeLibrary.PreloadCommonShapes(doc);
                    var refreshedReport = new RebarGenerationReport();
                    List<Rebar> bars = generator.Generate(input, refreshedReport);
                    if (refreshedReport.HasErrors) throw new InvalidOperationException(refreshedReport.Errors[0].ErrorReason);
                    return bars;
                }, () => RebarPreviewService.Fingerprint(input), RebarPreviewService.Describe(input))
            });
            Check(result, "RC-PREVIEW02", "Stale rejection and refreshed plan", staleRejected && changedPlan.Find(changedFingerprint) != null,
                "Changed inputs rejected", "Changed input not found in preview snapshot",
                "Input fingerprints include host/type identity and generation settings.", QaSeverity.CRITICAL);
            input.BarsAlongB = originalBars;

            bool matched = false;
            bool duplicateDetected = false;
            TransactionStatus finalStatus;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA Rebar preview parity"))
            {
                if (tx.Start() != TransactionStatus.Started)
                {
                    Block(result, "RC-PREVIEW_TX", "Preview parity transaction", "A started transaction",
                        "BLOCKED: Revit returned transaction status " + tx.GetStatus() + ".", QaSeverity.CRITICAL);
                    return;
                }
                try
                {
                    RebarShapeLibrary.PreloadCommonShapes(doc);
                    var executionReport = new RebarGenerationReport();
                    List<Rebar> generated = generator.Generate(input, executionReport);
                    doc.Regenerate();
                    matched = !executionReport.HasErrors && RebarPreviewService.Matches(snapshot, fingerprint, generated);
                    duplicateDetected = RebarPreviewService.HasExistingDuplicateBar(doc, column, snapshot, fingerprint);
                }
                finally
                {
                    if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                }
                finalStatus = tx.GetStatus();
            }
            Check(result, "RC-PREVIEW03", "Preview/execution centerline parity", matched,
                "Same solved centerlines", matched ? "Matched" : "Mismatch",
                "Production output is compared with the reviewed detached solver result before any commit.", QaSeverity.CRITICAL);
            Check(result, "RC-PREVIEW_DUPLICATE", "Equivalent generated column bars are detected", duplicateDetected,
                "At least one planned type/centerline signature is found on the host", duplicateDetected ? "Detected" : "Not detected",
                "Duplicate protection is tested while production-generated bars exist in the disposable parity transaction.", QaSeverity.CRITICAL);
            Check(result, "RC-PREVIEW04", "Execution parity rollback", finalStatus == TransactionStatus.RolledBack,
                TransactionStatus.RolledBack.ToString(), finalStatus.ToString(),
                "The QA fixture never leaves generated reinforcement in the user's model.", QaSeverity.CRITICAL);
        }
    }

    public sealed class RebarCoreRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "RC"; } }
        public override string Name { get { return "Rebar core straight bar"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Create a straight Rebar with the same production helper used by CmdRebarFixtureQa."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();
            FloorType floorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().FirstOrDefault(x => !x.IsFoundationSlab);
            RebarBarType barType = RuntimeQaFixtureHelpers.FindBarType(doc, 12);
            if (level == null || floorType == null || barType == null)
            {
                Block(result, "RC_RES", "Rebar core prerequisites", "Level, FloorType and RebarBarType", "BLOCKED: load a structural test project with Level, FloorType and RebarBarType.", QaSeverity.CRITICAL);
                return;
            }
            Floor floor = null;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA Rebar host"))
            {
                tx.Start();
                double size = UnitUtils.ConvertToInternalUnits(4000, UnitTypeId.Millimeters);
                double ox = UnitUtils.ConvertToInternalUnits(50000, UnitTypeId.Millimeters);
                var loop = new CurveLoop();
                XYZ p0 = new XYZ(ox, 0, level.Elevation);
                XYZ p1 = new XYZ(ox + size, 0, level.Elevation);
                XYZ p2 = new XYZ(ox + size, size, level.Elevation);
                XYZ p3 = new XYZ(ox, size, level.Elevation);
                loop.Append(Line.CreateBound(p0, p1)); loop.Append(Line.CreateBound(p1, p2)); loop.Append(Line.CreateBound(p2, p3)); loop.Append(Line.CreateBound(p3, p0));
                floor = Floor.Create(doc, new List<CurveLoop> { loop }, floorType.Id, level.Id);
                tx.Commit();
            }
            context.TrackCreated(floor == null ? ElementId.InvalidElementId : floor.Id);
            if (floor == null) { Check(result, "RC_HOST", "Temporary floor host", false, "A temporary Floor", "null", "Revit did not create the temporary host.", QaSeverity.CRITICAL); return; }
            BoundingBoxXYZ box = floor.get_BoundingBox(null);
            Rebar rebar = null;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA Rebar creation"))
            {
                tx.Start();
                RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx);
                double z = box.Min.Z + UnitUtils.ConvertToInternalUnits(25, UnitTypeId.Millimeters) + barType.BarModelDiameter / 2.0;
                rebar = RebarShapeCreationHelper.TryCreateStraightBar(doc, floor, barType,
                    new XYZ(box.Min.X + UnitUtils.ConvertToInternalUnits(100, UnitTypeId.Millimeters), box.Min.Y + UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters), z),
                    new XYZ(box.Max.X - UnitUtils.ConvertToInternalUnits(100, UnitTypeId.Millimeters), box.Min.Y + UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters), z));
                doc.Regenerate();
                tx.Commit();
                RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "RC_FAILURES", failureCapture);
            }
            context.TrackCreated(rebar == null ? ElementId.InvalidElementId : rebar.Id);
            string failure = null;
            bool valid = rebar != null && RebarShapeCreationHelper.TryValidateCreatedRebar(doc, rebar, floor, RebarStyle.Standard, true, "Runtime QA straight bar", out failure);
            Check(result, "RC01", "Production straight Rebar", valid, "Valid regenerated Rebar", valid ? "Valid" : failure, valid ? "CmdRebarFixtureQa production helper validated the bar." : failure, QaSeverity.CRITICAL);
            Check(result, "RC02", "Host and shape", valid && rebar.GetHostId() == floor.Id && rebar.GetShapeId() != ElementId.InvalidElementId,
                "Host and solved shape", valid ? rebar.GetShapeId().ToString() : "invalid", "Host, shape and containment were checked after regeneration.", QaSeverity.CRITICAL);
        }
    }

    public sealed class RectangularColumnRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "RC-STATION"; } }
        public override string Name { get { return "Rectangular Column C1 station tie"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Reuse the production rectangular-column station fixture: outer + two inner ties at base + 500 mm."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            FamilyInstance column = context.UiDocument.Selection.GetElementIds().Select(id => doc.GetElement(id) as FamilyInstance)
                .FirstOrDefault(c => c != null && !CmdColumnRebar.IsCircular(c));
            if (column == null) column = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault(c => !CmdColumnRebar.IsCircular(c));
            if (column == null) { Block(result, "RC01_RES", "Rectangular column host", "A rectangular structural column marked C1", "BLOCKED: select or create the C1 rectangular column fixture.", QaSeverity.CRITICAL); return; }
            RectangularColumnGeometryHelper.ColumnProfile profile;
            try { profile = RectangularColumnGeometryHelper.GetRectangularProfile(column); }
            catch (Exception ex) { Block(result, "RC01_RES", "Rectangular column profile", "A readable rectangular profile", "BLOCKED: " + ex.Message, QaSeverity.CRITICAL); return; }
            double b = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters);
            double h = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters);
            bool c1 = string.Equals(column.LookupParameter("Mark")?.AsString(), "C1", StringComparison.OrdinalIgnoreCase) || (Math.Abs(b - 1000) <= 2 && Math.Abs(h - 350) <= 2);
            if (!c1) { Block(result, "RC01_RES", "C1 geometry", "1000 x 350 mm C1 column", "BLOCKED: selected column is not C1 (1000 x 350 mm).", QaSeverity.CRITICAL); return; }
            RebarBarType main = RuntimeQaFixtureHelpers.FindBarType(doc, 32);
            RebarBarType tie = RuntimeQaFixtureHelpers.FindBarType(doc, 12);
            if (main == null || tie == null) { Block(result, "RC02_RES", "C1 bar types", "N32 main and N12 tie RebarBarTypes", "BLOCKED: load matching N32 and N12 RebarBarTypes.", QaSeverity.CRITICAL); return; }
            var input = new RectangularColumnRebarInput { Column = column, MainBarType = main, StirrupBarType = tie, BarsAlongB = 7, BarsAlongH = 3, TieLayout = ColumnTieLayoutType.MultiCellClosed, HasInnerDiamondStirrup = false, HasCrossLinks = false };
            var report = new RebarGenerationReport();
            List<Rebar> ties;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA C1 station"))
            {
                tx.Start();
                RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx);
                ties = new RectangularColumnRebarGenerator(doc).CreateSingleTieStation(input, profile.BaseCenter.Z + UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters), report);
                doc.Regenerate();
                bool count = ties != null && ties.Count == 3;
                Check(result, "RC03", "C1 three-tie topology", count, "3 ties: outer + two inner", ties == null ? "0" : ties.Count.ToString(), "Production generator was executed at base + 500 mm.", QaSeverity.CRITICAL);
                foreach (Rebar tieBar in ties ?? new List<Rebar>()) context.TrackCreated(tieBar.Id);
                tx.Commit();
                RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "RC-STATION_FAILURES", failureCapture);
            }
            RuntimeQaFixtureHelpers.AddRebarResult(result, "RC04", ties, report, column);
        }
    }

    public sealed class RectangularColumnFullCageRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "RC-FULL"; } }
        public override string Name { get { return "Rectangular Column full cage"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Run the production full rectangular-column cage generator separately from the station tie fixture."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            FamilyInstance column = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault(c => !CmdColumnRebar.IsCircular(c));
            RebarBarType main = RuntimeQaFixtureHelpers.FindBarType(doc, 32); RebarBarType tie = RuntimeQaFixtureHelpers.FindBarType(doc, 12);
            if (column == null || main == null || tie == null) { Block(result, "RC05_RES", "Full cage resources", "Rectangular column plus N32/N12 types", "BLOCKED: full cage prerequisites are missing.", QaSeverity.CRITICAL); return; }
            var input = new RectangularColumnRebarInput { Column = column, MainBarType = main, StirrupBarType = tie, BarsAlongB = 7, BarsAlongH = 3, TieLayout = ColumnTieLayoutType.MultiCellClosed };
            var report = new RebarGenerationReport(); List<Rebar> cage;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA full C1 cage"))
            {
                tx.Start(); RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx); cage = new RectangularColumnRebarGenerator(doc).Generate(input, report); doc.Regenerate(); foreach (Rebar bar in cage ?? new List<Rebar>()) context.TrackCreated(bar.Id); tx.Commit(); RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "RC-FULL_FAILURES", failureCapture);
            }
            RuntimeQaFixtureHelpers.AddRebarResult(result, "RC06", cage, report, column);
            Check(result, "RC07", "Full cage solver", !RuntimeQaFixtureHelpers.HasSolverError(report), "No Can't solve Rebar Shape", report == null ? "none" : report.FailureCount.ToString(), "Full cage failures are surfaced as QA failures.", QaSeverity.CRITICAL);
        }
    }

    public sealed class CircularColumnRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "CC"; } }
        public override string Name { get { return "Circular Column Rebar"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Run the production circular-column generator on a circular structural column."; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document; FamilyInstance column = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault(CmdColumnRebar.IsCircular);
            RebarBarType main = RuntimeQaFixtureHelpers.FindBarType(doc, 16); RebarBarType tie = RuntimeQaFixtureHelpers.FindBarType(doc, 10);
            if (column == null || main == null || tie == null) { Block(result, "CC_RES", "Circular column resources", "Circular column and compatible bar types", "BLOCKED: no suitable circular column fixture is available."); return; }
            var report = new RebarGenerationReport(); List<Rebar> bars;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA circular column"))
            {
                tx.Start(); RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx); bars = new CircularColumnRebarGenerator(doc).Generate(new CircularColumnRebarInput { Column = column, MainBarType = main, StirrupBarType = tie, MainBarQty = 8 }, report); doc.Regenerate(); foreach (Rebar bar in bars ?? new List<Rebar>()) context.TrackCreated(bar.Id); tx.Commit(); RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "CC_FAILURES", failureCapture);
            }
            RuntimeQaFixtureHelpers.AddRebarResult(result, "CC01", bars, report, column);
        }
    }

    public sealed class BeamRebarRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "BR"; } }
        public override string Name { get { return "Beam Rebar"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Run the production BeamRebarGenerator on a structural framing host."; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document; FamilyInstance beam = RuntimeQaFixtureHelpers.FirstInstance(doc, BuiltInCategory.OST_StructuralFraming); RebarBarType bar = RuntimeQaFixtureHelpers.FindBarType(doc, 16);
            if (beam == null || bar == null) { Block(result, "BR_RES", "Beam resources", "Structural framing host and RebarBarType", "BLOCKED: no suitable beam fixture is available."); return; }
            var report = new RebarGenerationReport(); List<Rebar> bars;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA beam"))
            {
                tx.Start(); RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx); bars = new BeamRebarGenerator(doc).Generate(new BeamRebarInput { Beam = beam, MainTopBarType = bar, MainBottomBarType = bar, StirrupBarType = bar, SideBarType = bar, TopContinuousQty = 2, BottomContinuousQty = 2 }, report); doc.Regenerate(); foreach (Rebar rb in bars ?? new List<Rebar>()) context.TrackCreated(rb.Id); tx.Commit(); RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "BR_FAILURES", failureCapture);
            }
            RuntimeQaFixtureHelpers.AddRebarResult(result, "BR01", bars, report, beam);
        }
    }

    public sealed class SlabRebarRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "SR"; } }
        public override string Name { get { return "Slab Rebar"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Run the production SlabRebarGenerator using an analyzed Floor profile."; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document; Floor floor = RuntimeQaFixtureHelpers.FirstFloor(doc); RebarBarType bar = RuntimeQaFixtureHelpers.FindBarType(doc, 10);
            if (floor == null || bar == null) { Block(result, "SR_RES", "Slab resources", "Floor and RebarBarType", "BLOCKED: no suitable slab fixture is available."); return; }
            SlabProfile profile; try { profile = SlabGeometryHelper.AnalyzeSlab(doc, floor); } catch (Exception ex) { Block(result, "SR_RES", "Slab profile", "Analyzable Floor profile", "BLOCKED: " + ex.Message); return; }
            if (profile == null) { Block(result, "SR_RES", "Slab profile", "Analyzable Floor profile", "BLOCKED: slab geometry is unavailable."); return; }
            var report = new RebarGenerationReport(); List<Rebar> bars;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA slab"))
            {
                tx.Start(); RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx); bars = new SlabRebarGenerator(doc).Generate(profile, new SlabRebarSettings(), report); doc.Regenerate(); foreach (Rebar rb in bars ?? new List<Rebar>()) context.TrackCreated(rb.Id); tx.Commit(); RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "SR_FAILURES", failureCapture);
            }
            RuntimeQaFixtureHelpers.AddRebarResult(result, "SR01", bars, report, floor);
        }
    }

    public sealed class FoundationRebarRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "FR"; } }
        public override string Name { get { return "Foundation Rebar"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Compare rollback-only FoundationRebarGenerator preview centerlines with transactional production output on a structural foundation host."; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document; FamilyInstance foundation = RuntimeQaFixtureHelpers.FirstFoundation(doc); RebarBarType bar = RuntimeQaFixtureHelpers.FindBarType(doc, 14);
            if (foundation == null || bar == null) { Block(result, "FR_RES", "Foundation resources", "Structural foundation host and RebarBarType", "BLOCKED: no suitable foundation fixture is available."); return; }
            FoundationProfile profile; try { profile = FoundationGeometryHelper.AnalyzeFoundation(doc, foundation); } catch (Exception ex) { Block(result, "FR_RES", "Foundation profile", "Analyzable foundation profile", "BLOCKED: " + ex.Message); return; }
            if (profile == null) { Block(result, "FR_RES", "Foundation profile", "Analyzable foundation profile", "BLOCKED: foundation geometry is unavailable."); return; }
            var settings = new FoundationRebarSettings();
            var generator = new FoundationRebarGenerator(doc);
            string inputFingerprint = RebarPreviewService.Fingerprint(profile, settings);
            RebarPreviewSnapshot preview;
            int barsBefore = CountHostedBars(doc, foundation);
            try
            {
                preview = RebarPreviewService.Capture(doc, new[]
                {
                    new RebarPreviewRequest(inputFingerprint,
                        () =>
                        {
                            var previewReport = new RebarGenerationReport();
                            List<Rebar> planned = generator.Generate(profile, settings, previewReport);
                            if (previewReport.HasErrors) throw new InvalidOperationException(previewReport.Errors[0].ErrorReason);
                            return planned;
                        },
                        () => RebarPreviewService.Fingerprint(FoundationGeometryHelper.AnalyzeFoundation(doc, foundation), settings),
                        RebarPreviewService.Describe(profile, settings))
                });
                RebarPreviewComponent component = preview.Find(inputFingerprint);
                Check(result, "FR_PREVIEW", "Rollback-only solver preview", component != null && component.BarCount > 0 && component.Paths.Count > 0,
                    "Foundation production solver paths", component == null ? "No component" : component.BarCount + " bars / " + component.Paths.Count + " paths",
                    "The rollback-only capture uses the production FoundationRebarGenerator.", QaSeverity.CRITICAL);
                Check(result, "FR_CANCEL", "Preview capture leaves no hosted bars", CountHostedBars(doc, foundation) == barsBefore,
                    barsBefore.ToString(), CountHostedBars(doc, foundation).ToString(),
                    "Foundation preview solver transaction is rolled back without persistent bars.", QaSeverity.CRITICAL);
            }
            catch (Exception ex)
            {
                Check(result, "FR_PREVIEW", "Rollback-only solver preview", false,
                    "Successful non-mutating foundation preview", "Exception: " + ex.Message,
                    "Rollback-only capture failed.", QaSeverity.CRITICAL);
                return;
            }

            var report = new RebarGenerationReport(); List<Rebar> bars;
            bool matched;
            bool duplicateDetected;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA foundation"))
            {
                tx.Start(); RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx); bars = generator.Generate(profile, settings, report); doc.Regenerate();
                matched = RebarPreviewService.Matches(preview, inputFingerprint, bars);
                duplicateDetected = RebarPreviewService.HasExistingDuplicateBar(doc, foundation, preview, inputFingerprint);
                foreach (Rebar rb in bars ?? new List<Rebar>()) context.TrackCreated(rb.Id);
                tx.Commit(); RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "FR_FAILURES", failureCapture);
            }
            Check(result, "FR_PARITY", "Preview/create centerline parity", matched,
                "Bar type and centerline parity", matched ? "Matched" : "Mismatch",
                "Foundation production output is regenerated in a disposable transaction and compared with the solver snapshot.", QaSeverity.CRITICAL);
            Check(result, "FR_DUPLICATE", "Equivalent generated foundation bars are detected", duplicateDetected,
                "At least one planned type/centerline signature is found on the host", duplicateDetected ? "Detected" : "Not detected",
                "Duplicate protection is checked while preview-matched bars exist in the disposable parity transaction.", QaSeverity.CRITICAL);
            RuntimeQaFixtureHelpers.AddRebarResult(result, "FR01", bars, report, foundation);
        }

        private static int CountHostedBars(Document document, Element host) =>
            new FilteredElementCollector(document).OfClass(typeof(Rebar)).Cast<Rebar>().Count(bar => bar.GetHostId() == host.Id);
    }

    public sealed class CircularColumnPreviewRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "CC-PREVIEW"; } }
        public override string Name { get { return "Circular Column rollback-only solver preview"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Verify circular-column preview refresh, non-destructive rollback, stale-input fingerprinting, duplicate detection and production centerline parity."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            FamilyInstance column = context.UiDocument.Selection.GetElementIds()
                .Select(id => doc.GetElement(id) as FamilyInstance)
                .FirstOrDefault(candidate => candidate != null && CmdColumnRebar.IsCircular(candidate));
            if (column == null)
                column = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault(CmdColumnRebar.IsCircular);
            RebarBarType main = RuntimeQaFixtureHelpers.FindBarType(doc, 16);
            RebarBarType tie = RuntimeQaFixtureHelpers.FindBarType(doc, 10);
            if (column == null || main == null || tie == null)
            {
                Block(result, "CC-PREVIEW_RES", "Preview resources", "Circular column and compatible N16/N10 types",
                    "BLOCKED: select or load a circular column and compatible RebarBarTypes.", QaSeverity.CRITICAL);
                return;
            }

            var input = new CircularColumnRebarInput
            {
                Column = column, MainBarType = main, StirrupBarType = tie, MainBarQty = 8,
                HasDowel = true, HasTopAnchor = true, StirrupSpacing = UnitUtils.ConvertToInternalUnits(150, UnitTypeId.Millimeters)
            };
            var generator = new CircularColumnRebarGenerator(doc);
            string fingerprint = RebarPreviewService.Fingerprint(input);
            int barsBefore = CountHostedBars(doc, column);
            RebarPreviewRequest CreateRequest() => new RebarPreviewRequest(fingerprint, () =>
            {
                RebarShapeLibrary.PreloadCommonShapes(doc);
                var report = new RebarGenerationReport();
                List<Rebar> bars = generator.Generate(input, report);
                if (report.HasErrors) throw new InvalidOperationException(report.Errors[0].ErrorReason);
                return bars;
            }, () => RebarPreviewService.Fingerprint(input), RebarPreviewService.Describe(input));

            RebarPreviewSnapshot preview;
            try
            {
                preview = RebarPreviewService.Capture(doc, new[] { CreateRequest() });
                RebarPreviewSnapshot refreshed = RebarPreviewService.Capture(doc, new[] { CreateRequest() });
                RebarPreviewComponent component = preview.Find(fingerprint);
                RebarPreviewComponent refreshedComponent = refreshed.Find(fingerprint);
                Check(result, "CC-PREVIEW_REFRESH", "Detached circular-column solver paths refresh deterministically",
                    component != null && component.BarCount > 0 && component.Paths.Count > 0 && refreshedComponent != null &&
                    WorkflowFingerprint.Matches(component.GeometryFingerprint, refreshedComponent.GeometryFingerprint),
                    "Same detached centerlines", component == null ? "No preview" : component.BarCount + " bars / " + component.Paths.Count + " paths",
                    "The preview invokes CircularColumnRebarGenerator under rollback-only capture.", QaSeverity.CRITICAL);
                Check(result, "CC-PREVIEW_CANCEL", "Capture/cancel leaves no hosted bars", CountHostedBars(doc, column) == barsBefore,
                    barsBefore.ToString(), CountHostedBars(doc, column).ToString(),
                    "The capture transaction rolls back and does not persist generated bars.", QaSeverity.CRITICAL);
            }
            catch (Exception ex)
            {
                Check(result, "CC-PREVIEW_CAPTURE", "Circular-column rollback-only solver capture", false,
                    "Successful detached production preview", ex.Message, "Capture failed.", QaSeverity.CRITICAL);
                return;
            }

            int originalQty = input.MainBarQty;
            input.MainBarQty++;
            bool staleRejected = preview.Find(RebarPreviewService.Fingerprint(input)) == null;
            input.MainBarQty = originalQty;
            Check(result, "CC-PREVIEW_STALE", "Changed circular-column inputs reject the prior plan", staleRejected,
                "Changed fingerprint absent from old snapshot", staleRejected ? "Rejected" : "Matched old plan",
                "The input fingerprint includes host, bar types, type versions and all generator settings.", QaSeverity.CRITICAL);

            bool matched = false;
            bool duplicate = false;
            TransactionStatus status;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA circular column parity"))
            {
                if (tx.Start() != TransactionStatus.Started)
                {
                    Block(result, "CC-PREVIEW_TX", "Parity transaction", "Started transaction", "BLOCKED: Revit could not start the transaction.", QaSeverity.CRITICAL);
                    return;
                }
                try
                {
                    RebarShapeLibrary.PreloadCommonShapes(doc);
                    var report = new RebarGenerationReport();
                    List<Rebar> generated = generator.Generate(input, report);
                    doc.Regenerate();
                    matched = !report.HasErrors && RebarPreviewService.Matches(preview, fingerprint, generated);
                    duplicate = RebarPreviewService.HasExistingDuplicateBar(doc, column, preview, fingerprint);
                }
                finally { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); }
                status = tx.GetStatus();
            }
            Check(result, "CC-PREVIEW_PARITY", "Preview/create centerline parity", matched,
                "Production centerlines match detached preview", matched ? "Matched" : "Mismatch",
                "The fixture regenerates inside a disposable transaction and rolls it back.", QaSeverity.CRITICAL);
            Check(result, "CC-PREVIEW_DUPLICATE", "Equivalent circular-column bars are detected", duplicate,
                "Existing planned bars detected", duplicate ? "Detected" : "Not detected",
                "Duplicate detection runs while matching bars exist in the parity transaction.", QaSeverity.CRITICAL);
            Check(result, "CC-PREVIEW_ROLLBACK", "Parity transaction rolls back", status == TransactionStatus.RolledBack,
                TransactionStatus.RolledBack.ToString(), status.ToString(),
                "Fixture does not persist test reinforcement.", QaSeverity.CRITICAL);
            Check(result, "CC-PREVIEW_CLEAN", "Host bar count is unchanged", CountHostedBars(doc, column) == barsBefore,
                barsBefore.ToString(), CountHostedBars(doc, column).ToString(),
                "No duplicate or temporary bars remain after fixture completion.", QaSeverity.CRITICAL);
        }

        private static int CountHostedBars(Document document, Element host) =>
            new FilteredElementCollector(document).OfClass(typeof(Rebar)).Cast<Rebar>().Count(bar => bar.GetHostId() == host.Id);
    }
}

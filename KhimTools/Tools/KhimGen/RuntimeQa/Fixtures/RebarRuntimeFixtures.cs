using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Models;
using KhimTools.RebarTool.Commands;

namespace KhimTools.RuntimeQa.Fixtures
{
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
                })
            });
            RebarPreviewComponent component = snapshot.Find(fingerprint);
            Check(result, "RC-PREVIEW01", "Detached solver paths", component != null && component.BarCount > 0 && component.Paths.Count > 0,
                "Revit-solved bar paths", component == null ? "missing" : component.Paths.Count + " paths",
                "Capture returned detached curve coordinates after rollback-only generation.", QaSeverity.CRITICAL);

            int originalBars = input.BarsAlongB;
            input.BarsAlongB = originalBars + 1;
            Check(result, "RC-PREVIEW02", "Stale input rejection", snapshot.Find(RebarPreviewService.Fingerprint(input)) == null,
                "Changed inputs rejected", "Changed input not found in preview snapshot",
                "Input fingerprints include host/type identity and generation settings.", QaSeverity.CRITICAL);
            input.BarsAlongB = originalBars;

            bool matched = false;
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
        public override string Description { get { return "Run the production FoundationRebarGenerator on a simple structural foundation host."; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document; FamilyInstance foundation = RuntimeQaFixtureHelpers.FirstFoundation(doc); RebarBarType bar = RuntimeQaFixtureHelpers.FindBarType(doc, 14);
            if (foundation == null || bar == null) { Block(result, "FR_RES", "Foundation resources", "Structural foundation host and RebarBarType", "BLOCKED: no suitable foundation fixture is available."); return; }
            FoundationProfile profile; try { profile = FoundationGeometryHelper.AnalyzeFoundation(doc, foundation); } catch (Exception ex) { Block(result, "FR_RES", "Foundation profile", "Analyzable foundation profile", "BLOCKED: " + ex.Message); return; }
            if (profile == null) { Block(result, "FR_RES", "Foundation profile", "Analyzable foundation profile", "BLOCKED: foundation geometry is unavailable."); return; }
            var report = new RebarGenerationReport(); List<Rebar> bars;
            using (var tx = new Transaction(doc, "K-TOOLS Runtime QA foundation"))
            {
                tx.Start(); RebarGenerationFailurePreprocessor failureCapture = RuntimeQaFixtureHelpers.AttachFailureCapture(tx); bars = new FoundationRebarGenerator(doc).Generate(profile, new FoundationRebarSettings(), report); doc.Regenerate(); foreach (Rebar rb in bars ?? new List<Rebar>()) context.TrackCreated(rb.Id); tx.Commit(); RuntimeQaFixtureHelpers.AddFailureCaptureCheck(result, "FR_FAILURES", failureCapture);
            }
            RuntimeQaFixtureHelpers.AddRebarResult(result, "FR01", bars, report, foundation);
        }
    }
}

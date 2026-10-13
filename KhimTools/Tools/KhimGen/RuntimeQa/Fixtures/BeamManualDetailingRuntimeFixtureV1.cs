using KhimTools.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Workflow;
using KhimTools.RebarTool.Core;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Rollback-only host validation for the explicitly supported straight Manual v1 bar path.</summary>
    public sealed class BeamManualDetailingRuntimeFixtureV1 : RuntimeQaFixtureBase
    {
        public override string Id => "BR-MANUAL-V1-ROLLBACK";
        public override string Name => "Beam Manual v1 straight bars / preview parity / rollback";
        public override string Suite => "REBAR";
        public override string Description => "QA-only synthetic explicit bars; validates host-bound Manual v1 solve, exact RebarBarType, partial/full duplicate selectivity, preview parity and rollback. Requires a confirmed disposable detached project.";
        public override bool IsCritical => true;

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            FamilyInstance beam = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault(candidate =>
                {
                    try { BeamGeometryHelper.GetBeamProfile(candidate); return true; }
                    catch { return false; }
                });
            if (beam == null)
            {
                Block(result, Id + "_BEAM", "Supported Beam resource", "One physically verified straight rectangular structural Beam",
                    "BLOCKED: load a compatible Beam into the confirmed disposable QA model.", QaSeverity.CRITICAL);
                return;
            }
            RebarBarType type = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                .Where(candidate => candidate != null && candidate.IsValidObject && candidate.BarModelDiameter > 0)
                .OrderBy(candidate => candidate.BarModelDiameter).ThenBy(candidate => candidate.UniqueId, StringComparer.Ordinal).FirstOrDefault();
            if (type == null)
            {
                Block(result, Id + "_TYPE", "Exact loaded RebarBarType", "One valid loaded RebarBarType",
                    "BLOCKED: no usable RebarBarType exists; no type is substituted.", QaSeverity.CRITICAL);
                return;
            }

            BeamManualRebarGeneratorV1 generator = new BeamManualRebarGeneratorV1(doc);
            BeamGeometryHelper.BeamProfile profile = BeamGeometryHelper.GetBeamProfile(beam);
            double diameterMm = UnitUtils.ConvertFromInternalUnits(type.BarModelDiameter, UnitTypeId.Millimeters);
            double widthMm = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters);
            double heightMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters);
            double lengthMm = UnitUtils.ConvertFromInternalUnits(profile.Length, UnitTypeId.Millimeters);
            if (widthMm / 4.0 <= diameterMm / 2.0 || heightMm / 2.0 <= diameterMm / 2.0 || lengthMm <= diameterMm)
            {
                Block(result, Id + "_SECTION", "Synthetic QA positions fit the Beam prism", "Two distinct centerlines with exact bar envelope inside the measured Beam prism",
                    "BLOCKED: the selected Beam is too small for safe synthetic QA geometry with this exact loaded type.", QaSeverity.CRITICAL);
                return;
            }
            BeamManualDetailingPlanV1 plan = generator.CreateBlankPlan(beam);
            profileLengthFeet = profile.Length;
            endRadiusMm = diameterMm / 2.0;
            plan.Covers = new List<BeamManualCoverV1>
            {
                Cover(BeamManualCoverFace.Top), Cover(BeamManualCoverFace.Bottom), Cover(BeamManualCoverFace.Left),
                Cover(BeamManualCoverFace.Right), Cover(BeamManualCoverFace.StirrupOutside)
            };
            plan.SupportEnds = new List<BeamManualSupportEndV1>
            {
                FreeEnd(BeamManualEnd.Start), FreeEnd(BeamManualEnd.Finish)
            };
            plan.BarGroups = new List<BeamManualBarGroupV1>
            {
                Group("QA_ONLY_A", "QA_ONLY_LAYER_A", -widthMm / 4.0),
                Group("QA_ONLY_B", "QA_ONLY_LAYER_B", widthMm / 4.0)
            };
            foreach (BeamManualBarGroupV1 group in plan.BarGroups)
                group.BarType = new BeamManualElementReferenceV1
                {
                    DocumentKey = plan.DocumentKey, UniqueId = type.UniqueId, Name = type.Name, DiameterMm = diameterMm,
                    State = BeamManualReferenceState.Resolved
                };
            string fingerprint = generator.Fingerprint(plan, beam);
            var roles = new Dictionary<string, string>(StringComparer.Ordinal);
            var request = new RebarPreviewRequest(fingerprint, () => generator.Generate(plan, beam, roles),
                () => generator.Fingerprint(plan, beam),
                new Dictionary<string, string> { { "HostId", beam.UniqueId }, { "HostGeometry", plan.Frame.HostGeometrySignature } },
                bar => roles.TryGetValue(bar.Id.ToLongValue().ToString(CultureInfo.InvariantCulture), out string role) ? role : string.Empty);
            RuntimeQaModelFingerprint beforePreview = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            RebarPreviewSnapshot preview = RebarPreviewService.Capture(doc, new[] { request });
            string previewRollbackMessage;
            bool previewClean = RuntimeQaSafetyGuard.VerifyRollback(doc, beforePreview, Array.Empty<ElementId>(), out previewRollbackMessage);
            RebarPreviewComponent expected = preview.Find(fingerprint);
            Check(result, Id + "_PREVIEW", "Manual v1 preview is detached and contains two explicit bars",
                previewClean && expected != null && expected.BarCount == 2,
                "Two QA-only bars in the detached snapshot and unchanged model fingerprint",
                expected == null ? "No preview component" : expected.BarCount.ToString(CultureInfo.InvariantCulture) + " bars; " + (previewClean ? "rollback clean" : previewRollbackMessage),
                "Synthetic geometry is computed from the measured Beam frame and exists only in this rollback-only QA fixture.", QaSeverity.CRITICAL);
            if (!previewClean || expected == null || expected.BarCount != 2) return;

            bool partialRejected = false;
            bool fullDetected = false;
            bool parity = false;
            TransactionStatus rollback = TransactionStatus.Uninitialized;
            var createdIds = new List<ElementId>();
            using (var tx = new Transaction(doc, "K-TOOLS QA Manual Beam v1 (rollback)"))
            {
                if (tx.Start() != TransactionStatus.Started)
                {
                    Block(result, Id + "_TX", "Manual Beam test transaction", "Started Revit transaction",
                        "BLOCKED: the host transaction could not start.", QaSeverity.CRITICAL);
                    return;
                }
                try
                {
                    using (var partial = new SubTransaction(doc))
                    {
                        if (partial.Start() != TransactionStatus.Started) throw new InvalidOperationException("Partial-plan QA subtransaction could not start.");
                        plan.BarGroups[1].Enabled = false;
                        IList<Rebar> onlyA = generator.Generate(plan, beam);
                        doc.Regenerate();
                        foreach (Rebar bar in onlyA) { createdIds.Add(bar.Id); context.TrackCreated(bar.Id); }
                        partialRejected = !RebarPreviewService.HasExistingDuplicateBar(doc, beam, preview, fingerprint);
                        if (partial.RollBack() != TransactionStatus.RolledBack) throw new InvalidOperationException("Partial-plan QA rollback was not confirmed.");
                        plan.BarGroups[1].Enabled = true;
                    }
                    IList<Rebar> actual = generator.Generate(plan, beam);
                    doc.Regenerate();
                    foreach (Rebar bar in actual) { createdIds.Add(bar.Id); context.TrackCreated(bar.Id); }
                    fullDetected = RebarPreviewService.HasExistingDuplicateBar(doc, beam, preview, fingerprint);
                    parity = RebarPreviewService.Matches(preview, fingerprint, actual.ToList()) && actual.All(bar => bar.GetTypeId() == type.Id && bar.GetHostId() == beam.Id && bar.Quantity == 1);
                }
                finally
                {
                    if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                    rollback = tx.GetStatus();
                }
            }
            Check(result, Id + "_PARTIAL_DUPLICATE", "Partial planned set is not treated as a complete duplicate", partialRejected,
                "One of two planned bars does not match the complete accepted set", partialRejected ? "Correctly not duplicate" : "Incorrectly treated as duplicate", "Duplicate test uses the accepted complete-plan multiset.", QaSeverity.CRITICAL);
            Check(result, Id + "_FULL_DUPLICATE", "Complete planned set is recognized as duplicate", fullDetected,
                "Both accepted bars on the same host", fullDetected ? "Detected" : "Not detected", "Exact type and solved centerline fingerprints are compared.", QaSeverity.CRITICAL);
            Check(result, Id + "_PARITY", "Created Manual v1 bars match the accepted solver snapshot and exact type/host", parity,
                "Two exact-type, single-quantity bars with preview-parity centerlines", parity ? "Matched" : "Mismatch", "Actual generation is enclosed in a transaction that is always rolled back.", QaSeverity.CRITICAL);
            string rollbackMessage = "Revit transaction status was " + rollback + ".";
            bool modelRestored = rollback == TransactionStatus.RolledBack && RuntimeQaSafetyGuard.VerifyRollback(doc, beforePreview, createdIds, out rollbackMessage);
            Check(result, Id + "_ROLLBACK", "Manual v1 test leaves model identities and observable state unchanged", modelRestored,
                "Exact pre-preview model fingerprint", modelRestored ? "Restored" : rollbackMessage,
                "Fixture base additionally rolls back its outer TransactionGroup.", QaSeverity.CRITICAL);
        }

        private BeamManualBarGroupV1 Group(string groupId, string layer, double x) => new BeamManualBarGroupV1
        {
            GroupId = groupId, Role = BeamManualBarRole.UserDefined, UserLabel = "QA synthetic explicit line", Enabled = true,
            Bars = new List<BeamManualBarV1>
            {
                new BeamManualBarV1 { XMm = x, YMm = 0, StartStationMm = endRadiusMm, EndStationMm = UnitUtils.ConvertFromInternalUnits(profileLengthFeet, UnitTypeId.Millimeters) - endRadiusMm, LayerId = layer,
                    StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
                    FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation } }
            }
        };

        private double profileLengthFeet;
        private double endRadiusMm;

        private static BeamManualCoverV1 Cover(BeamManualCoverFace face) => new BeamManualCoverV1
        {
            Face = face, Source = BeamManualCoverSource.ExplicitUserInput, ClearCoverMm = 0
        };

        private static BeamManualSupportEndV1 FreeEnd(BeamManualEnd end) => new BeamManualSupportEndV1
        {
            End = end, Mode = BeamManualSupportMode.UserDefinedFreeEndWithinHost,
            IntendedContinuation = "QA ONLY: explicit synthetic line terminates at measured Beam boundary; development not verified."
        };
    }
}

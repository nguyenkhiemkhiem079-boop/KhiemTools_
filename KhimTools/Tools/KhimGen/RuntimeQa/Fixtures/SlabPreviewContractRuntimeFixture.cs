using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Models;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>
    /// Host-only acceptance for stale slab previews, host-bound parity, complete-plan
    /// duplicate detection and selective rejection of partial/similar reinforcement.
    /// All generated bars are inside subtransactions and are rolled back.
    /// </summary>
    public sealed class SlabPreviewContractRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "SR-PREVIEW-CONTRACT"; } }
        public override string Name { get { return "Slab stale preview and duplicate selectivity"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Exercise the slab stale-input matrix, host identity, full quantity/type/shape/geometry parity and selective complete-plan duplicate detection; all model mutations roll back."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            List<Floor> floors = FindSupportedFloors(doc);
            if (floors.Count < 2)
            {
                Block(result, Id + "_RES", "Two slab host resources", "Two distinct structural rectangular Floors without openings and at least 2000 mm in each plan direction",
                    "BLOCKED: the QA model does not contain two suitable distinct slab hosts.", QaSeverity.CRITICAL);
                return;
            }

            var manager = new SlabPanelManager();
            manager.InitializeFromFloors(doc, floors);
            List<SlabPanel> panels = manager.Panels.Where(panel => panel != null && panel.HostFloor != null)
                .OrderBy(panel => panel.HostFloor.UniqueId, StringComparer.Ordinal).Take(2).ToList();
            if (panels.Count != 2 || panels[0].HostFloor.UniqueId == panels[1].HostFloor.UniqueId)
            {
                Block(result, Id + "_PANELS", "Distinct analyzed slab panels", "Two production-analyzed panels with different host identities",
                    "BLOCKED: production geometry analysis did not return two distinct supported panels.", QaSeverity.CRITICAL);
                return;
            }

            var generator = new SlabRebarGenerator(doc);
            List<RebarBarType> types = (generator.BarTypes ?? new List<RebarBarType>())
                .Where(type => type != null && type.BarModelDiameter > 0)
                .OrderBy(type => type.BarModelDiameter).ThenBy(type => type.UniqueId, StringComparer.Ordinal).ToList();
            RebarBarType primaryType = ResolveExplicitType(types, null);
            RebarBarType alternateType = ResolveExplicitType(types, primaryType == null ? null : primaryType.UniqueId);
            if (primaryType == null || alternateType == null)
            {
                Block(result, Id + "_BAR_TYPES", "Explicit slab bar types", "Two distinct loaded RebarBarTypes with exact name resolution",
                    "BLOCKED: two explicitly resolvable bar types are required to exercise type/diameter invalidation; no type substitution is allowed.", QaSeverity.CRITICAL);
                return;
            }

            SlabPanel panelA = panels[0];
            SlabPanel panelB = panels[1];
            ConfigureMinimalMesh(panelA, primaryType);
            ConfigureMinimalMesh(panelB, primaryType);
            panelA.Config.BottomLayer.SpacingXMm = 137;
            panelA.Config.BottomLayer.SpacingYMm = 163;
            panelB.Config.BottomLayer.SpacingXMm = 137;
            panelB.Config.BottomLayer.SpacingYMm = 163;

            string fingerprintA = generator.GetPanelInputFingerprint(panelA);
            string fingerprintB = generator.GetPanelInputFingerprint(panelB);
            var baseline = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            var previewIds = new List<ElementId>();
            var reportA = new RebarGenerationReport();
            var reportB = new RebarGenerationReport();
            var rolesA = new Dictionary<string, string>(StringComparer.Ordinal);
            var rolesB = new Dictionary<string, string>(StringComparer.Ordinal);
            RebarPreviewSnapshot preview = null;
            Exception previewException = null;
            try
            {
                preview = RebarPreviewService.Capture(doc, new[]
                {
                    CreateRequest(panelA, generator, fingerprintA, reportA, rolesA, previewIds),
                    CreateRequest(panelB, generator, fingerprintB, reportB, rolesB, previewIds)
                });
            }
            catch (Exception ex) { previewException = ex; }

            string previewRollbackMessage;
            bool previewClean = RuntimeQaSafetyGuard.VerifyRollback(doc, baseline, previewIds, out previewRollbackMessage);
            RebarPreviewComponent expectedA = preview == null ? null : preview.Find(fingerprintA);
            RebarPreviewComponent expectedB = preview == null ? null : preview.Find(fingerprintB);
            bool previewValid = previewException == null && previewClean && !reportA.HasErrors && !reportB.HasErrors &&
                expectedA != null && expectedB != null && expectedA.BarCount > 1 && expectedB.BarCount > 1;
            if (!previewValid)
            {
                Block(result, Id + "_PREVIEW", "Production slab previews", "Two non-empty production-generated plans with more than one Rebar element and an exact rollback-only capture",
                    "BLOCKED: preview/resources were unavailable or preview cleanup could not be certified. " +
                    (previewException == null ? previewRollbackMessage : previewException.GetType().Name + ": " + previewException.Message), QaSeverity.CRITICAL);
                return;
            }
            Check(result, Id + "_PREVIEW_CLEAN", "Preview capture is detached and leaves no persistent elements", previewClean,
                "Exact model fingerprint restored and every preview-created identity absent", previewClean ? "Restored" : previewRollbackMessage,
                "The production RebarPreviewService rollback capture is used; no preview element is committed.", QaSeverity.CRITICAL);
            if (RebarPreviewService.HasExistingDuplicateBar(doc, panelA.HostFloor, preview, fingerprintA) ||
                RebarPreviewService.HasExistingDuplicateBar(doc, panelB.HostFloor, preview, fingerprintB))
            {
                Block(result, Id + "_CLEAN_PLAN", "Uncontaminated test plans", "Neither selected host already contains this exact accepted bar multiset",
                    "BLOCKED: the QA model already contains one of the deliberately selected test plans; choose a clean disposable model.", QaSeverity.CRITICAL);
                return;
            }

            Check(result, Id + "_PLAN_SET", "Changing the selected panel set invalidates the aggregate accepted plan",
                preview.PlanFingerprint != RebarPreviewService.FingerprintInputs(new[] { fingerprintA }),
                "The two-panel plan fingerprint differs from the one-panel plan fingerprint", preview.PlanFingerprint,
                "Selection is represented by the exact set of panel input fingerprints.", QaSeverity.CRITICAL);

            SlabPanelRebarConfig originalConfig = panelA.Config.Clone();
            double originalCoverTop = panelA.CoverTopFeet;
            double originalCoverBottom = panelA.CoverBottomFeet;
            List<CurveLoop> originalOpenings = panelA.Openings.ToList();
            Check(result, Id + "_STALE_TYPE", "Bar type/diameter edits stale the accepted preview",
                ProbeStale(panelA, generator, preview, fingerprintA, () => panelA.Config.BottomLayer.DiaXLabel = alternateType.Name,
                    originalConfig, originalCoverTop, originalCoverBottom, originalOpenings),
                "Changed resolved RebarBarType identity is absent from the old snapshot", "Bar type probe completed",
                "No silent type substitution is allowed; a changed resolved type requires a fresh solve.", QaSeverity.CRITICAL);
            Check(result, Id + "_STALE_SPACING", "Spacing edits stale the accepted preview",
                ProbeStale(panelA, generator, preview, fingerprintA, () => panelA.Config.BottomLayer.SpacingXMm += 10,
                    originalConfig, originalCoverTop, originalCoverBottom, originalOpenings),
                "Changed X spacing is absent from the old snapshot", "Spacing probe completed",
                "The test changes one panel input at a time and restores it immediately.", QaSeverity.CRITICAL);
            Check(result, Id + "_STALE_TOP_COVER", "Top-cover edits stale the accepted preview",
                ProbeStale(panelA, generator, preview, fingerprintA, () => panelA.CoverTopFeet += 0.01,
                    originalConfig, originalCoverTop, originalCoverBottom, originalOpenings),
                "Changed top cover is absent from the old snapshot", "Top cover probe completed", "Cover is part of the slab input fingerprint.", QaSeverity.CRITICAL);
            Check(result, Id + "_STALE_BOTTOM_COVER", "Bottom-cover edits stale the accepted preview",
                ProbeStale(panelA, generator, preview, fingerprintA, () => panelA.CoverBottomFeet += 0.01,
                    originalConfig, originalCoverTop, originalCoverBottom, originalOpenings),
                "Changed bottom cover is absent from the old snapshot", "Bottom cover probe completed", "Cover is part of the slab input fingerprint.", QaSeverity.CRITICAL);
            Check(result, Id + "_STALE_LAYER", "Layer inversion edits stale the accepted preview",
                ProbeStale(panelA, generator, preview, fingerprintA, () => panelA.Config.BottomLayer.InvertLayer = true,
                    originalConfig, originalCoverTop, originalCoverBottom, originalOpenings),
                "Changed layer order is absent from the old snapshot", "Layer inversion probe completed", "Layer position changes require a fresh solve.", QaSeverity.CRITICAL);
            Check(result, Id + "_STALE_SUPPORT", "Support reinforcement edits stale the accepted preview",
                ProbeStale(panelA, generator, preview, fingerprintA, () =>
                {
                    panelA.Config.HatReinforce.Enabled = true;
                    panelA.Config.HatReinforce.DiaXLabel = primaryType.Name;
                    panelA.Config.HatReinforce.DiaYLabel = primaryType.Name;
                    panelA.Config.HatReinforce.SpacingXMm += 10;
                }, originalConfig, originalCoverTop, originalCoverBottom, originalOpenings),
                "Changed support enable/type/spacing is absent from the old snapshot", "Support probe completed", "Support remains configuration-specific and preview-gated.", QaSeverity.CRITICAL);
            Check(result, Id + "_STALE_SPACER", "Spacer reinforcement edits stale the accepted preview",
                ProbeStale(panelA, generator, preview, fingerprintA, () =>
                {
                    panelA.Config.Spacer.Enabled = true;
                    panelA.Config.Spacer.DiaLabel = primaryType.Name;
                    panelA.Config.Spacer.HookLenMm += 10;
                }, originalConfig, originalCoverTop, originalCoverBottom, originalOpenings),
                "Changed spacer enable/type/geometry is absent from the old snapshot", "Spacer probe completed", "Spacer settings are part of the accepted plan fingerprint.", QaSeverity.CRITICAL);

            bool freshSolveAccepted = false;
            bool freshSolveClean = false;
            string freshSolveDetail = string.Empty;
            var freshSolveIds = new List<ElementId>();
            try
            {
                panelA.Config.BottomLayer.SpacingXMm += 10;
                string freshFingerprint = generator.GetPanelInputFingerprint(panelA);
                var freshReport = new RebarGenerationReport();
                var freshRoles = new Dictionary<string, string>(StringComparer.Ordinal);
                RebarPreviewSnapshot freshPreview = RebarPreviewService.Capture(doc, new[]
                {
                    CreateRequest(panelA, generator, freshFingerprint, freshReport, freshRoles, freshSolveIds)
                });
                RebarPreviewComponent freshComponent = freshPreview == null ? null : freshPreview.Find(freshFingerprint);
                freshSolveAccepted = freshFingerprint != fingerprintA && !freshReport.HasErrors &&
                    freshComponent != null && freshComponent.BarCount > 1 &&
                    freshPreview.PlanFingerprint == RebarPreviewService.FingerprintInputs(new[] { freshFingerprint });
                if (!freshSolveAccepted)
                    freshSolveDetail = "The changed spacing did not produce a complete fresh production plan.";
            }
            catch (Exception ex)
            {
                freshSolveDetail = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                panelA.Config = originalConfig.Clone();
                string freshRollbackMessage;
                freshSolveClean = RuntimeQaSafetyGuard.VerifyRollback(doc, baseline, freshSolveIds, out freshRollbackMessage);
                if (!freshSolveClean)
                    freshSolveDetail = string.IsNullOrWhiteSpace(freshSolveDetail) ? freshRollbackMessage : freshSolveDetail + " " + freshRollbackMessage;
            }
            Check(result, Id + "_FRESH_SOLVE", "Changed slab inputs can be freshly solved after the prior plan is invalidated",
                freshSolveAccepted && freshSolveClean,
                "Changed spacing has a new accepted production snapshot and its rollback-only capture restores the exact model baseline",
                freshSolveAccepted + "; rollback clean=" + freshSolveClean + (string.IsNullOrWhiteSpace(freshSolveDetail) ? string.Empty : "; " + freshSolveDetail),
                "A fresh solve is explicitly exercised; the prior snapshot remains stale, and no Revit element persists.", QaSeverity.CRITICAL);

            SlabPanelRebarConfig originalConfigB = panelB.Config.Clone();
            try
            {
                panelB.Config.BottomLayer.SpacingXMm += 20;
                panelA.Config = panelB.Config.Clone();
                string assignedFingerprint = generator.GetPanelInputFingerprint(panelA);
                Check(result, Id + "_STALE_PANEL_ASSIGNMENT", "Per-panel settings reassignment stales the prior panel plan",
                    assignedFingerprint != fingerprintA && preview.Find(assignedFingerprint) == null,
                    "Reassigned per-panel settings are absent from the old snapshot", "Assignment probe completed",
                    "The form assigns per-panel settings before resolving the current input fingerprint.", QaSeverity.CRITICAL);
            }
            finally
            {
                panelA.Config = originalConfig.Clone();
                panelB.Config = originalConfigB;
            }

            bool transactionRolledBack = false;
            bool previewParityA = false;
            bool previewParityB = false;
            bool wrongHostRejected = false;
            bool unrelatedHostNotDuplicate = false;
            bool exactPlanDetected = false;
            bool partialPlanRejected = false;
            bool quantityParity = false;
            string generationFailure = string.Empty;
            var creationIds = new List<ElementId>();
            using (var tx = new Transaction(doc, "K-TOOLS QA slab preview contract"))
            {
                if (tx.Start() != TransactionStatus.Started)
                {
                    Block(result, Id + "_TX", "Slab contract transaction", "A started Revit transaction", "BLOCKED: the host transaction could not start.", QaSeverity.CRITICAL);
                    return;
                }
                try
                {
                    List<Rebar> barsB = GenerateAndTrack(doc, generator, panelB, context, creationIds, out RebarGenerationReport actualReportB);
                    doc.Regenerate();
                    previewParityB = !actualReportB.HasErrors && RebarPreviewService.Matches(preview, fingerprintB, barsB);
                    wrongHostRejected = !RebarPreviewService.Matches(preview, fingerprintA, barsB);
                    unrelatedHostNotDuplicate = !RebarPreviewService.HasExistingDuplicateBar(doc, panelA.HostFloor, preview, fingerprintA);
                    bool secondHostBound = !RebarPreviewService.HasExistingDuplicateBar(doc, panelB.HostFloor, preview, fingerprintA);
                    bool duplicateBDetected = RebarPreviewService.HasExistingDuplicateBar(doc, panelB.HostFloor, preview, fingerprintB);
                    unrelatedHostNotDuplicate = unrelatedHostNotDuplicate && secondHostBound && duplicateBDetected;

                    List<Rebar> barsA = GenerateAndTrack(doc, generator, panelA, context, creationIds, out RebarGenerationReport actualReportA);
                    doc.Regenerate();
                    previewParityA = !actualReportA.HasErrors && RebarPreviewService.Matches(preview, fingerprintA, barsA);
                    RebarPreviewComponent componentA = preview.Find(fingerprintA);
                    quantityParity = componentA != null && componentA.BarCount == barsA.Sum(bar => bar.Quantity) &&
                        componentA.RebarSetCount == barsA.Count && componentA.BarFingerprints.Count == barsA.Count;
                    exactPlanDetected = RebarPreviewService.HasExistingDuplicateBar(doc, panelA.HostFloor, preview, fingerprintA);

                    if (barsA.Count > 1)
                    {
                        doc.Delete(barsA.Skip(1).Select(bar => bar.Id).ToList());
                        doc.Regenerate();
                        partialPlanRejected = !RebarPreviewService.HasExistingDuplicateBar(doc, panelA.HostFloor, preview, fingerprintA);
                    }
                    else generationFailure = "Production plan has too few Rebar elements for partial-selectivity verification.";
                }
                catch (Exception ex) { generationFailure = ex.GetType().Name + ": " + ex.Message; }
                finally
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        transactionRolledBack = tx.RollBack() == TransactionStatus.RolledBack;
                }
            }

            string rollbackMessage = string.Empty;
            bool modelRestored = transactionRolledBack && RuntimeQaSafetyGuard.VerifyRollback(doc, baseline,
                previewIds.Concat(creationIds), out rollbackMessage);
            if (!transactionRolledBack) rollbackMessage = "The parity/duplicate transaction rollback status was not confirmed.";
            if (!string.IsNullOrEmpty(generationFailure)) rollbackMessage += " " + generationFailure;
            Check(result, Id + "_HOST_IDENTITY", "Preview parity rejects bars generated on a different slab host", wrongHostRejected,
                "Panel B output cannot satisfy Panel A host identity", wrongHostRejected ? "Wrong host rejected" : "Wrong host accepted",
                "The production parity fingerprint includes the host UniqueId.", QaSeverity.CRITICAL);
            Check(result, Id + "_PARITY", "Created bar type, shape, quantity, layout and centerlines match each host preview",
                previewParityA && previewParityB && quantityParity,
                "Both host plans match exact production fingerprints and quantity/set counts",
                "A=" + previewParityA + "; B=" + previewParityB + "; quantities=" + quantityParity,
                "RebarPreviewService.Matches compares host/type/shape/hooks/layout/quantity and all solved centerline paths.", QaSeverity.CRITICAL);
            Check(result, Id + "_DUPLICATE_SELECTIVITY", "Only a complete exact plan on the same host is treated as duplicate",
                exactPlanDetected && partialPlanRejected && unrelatedHostNotDuplicate,
                "Exact same-host plan detected; partial plan and equivalent plan on another host not detected",
                "Exact=" + exactPlanDetected + "; partial rejected=" + partialPlanRejected + "; other host rejected=" + unrelatedHostNotDuplicate,
                "A complete multiset is required; a shared layer or another host must not trigger duplicate rejection.", QaSeverity.CRITICAL);
            Check(result, Id + "_ROLLBACK", "Preview/cancel and generation checks leave the disposable model unchanged",
                modelRestored, "Exact pre-fixture element identities and observable states restored",
                modelRestored ? "Restored" : rollbackMessage,
                "All temporary generation runs are rolled back; existing unrelated model and Rebar state must remain unchanged.", QaSeverity.CRITICAL);
        }

        private static List<Rebar> GenerateAndTrack(Document doc, SlabRebarGenerator generator,
            SlabPanel panel, RuntimeQaContext context, IList<ElementId> temporaryIds, out RebarGenerationReport report)
        {
            report = new RebarGenerationReport();
            List<Rebar> bars = generator.GeneratePanel(panel, report);
            foreach (Rebar bar in bars ?? new List<Rebar>())
            {
                if (bar == null) continue;
                if (!temporaryIds.Contains(bar.Id)) temporaryIds.Add(bar.Id);
                context.TrackCreated(bar.Id);
            }
            return bars ?? new List<Rebar>();
        }

        private static bool ProbeStale(SlabPanel panel, SlabRebarGenerator generator, RebarPreviewSnapshot snapshot,
            string originalFingerprint, Action mutate, SlabPanelRebarConfig originalConfig,
            double originalCoverTop, double originalCoverBottom, IList<CurveLoop> originalOpenings)
        {
            try
            {
                mutate();
                string changedFingerprint = generator.GetPanelInputFingerprint(panel);
                return !string.Equals(originalFingerprint, changedFingerprint, StringComparison.Ordinal) &&
                    snapshot.Find(changedFingerprint) == null;
            }
            catch { return false; }
            finally
            {
                panel.Config = originalConfig.Clone();
                panel.CoverTopFeet = originalCoverTop;
                panel.CoverBottomFeet = originalCoverBottom;
                panel.Openings = originalOpenings.ToList();
            }
        }

        private static RebarPreviewRequest CreateRequest(SlabPanel panel, SlabRebarGenerator generator,
            string fingerprint, RebarGenerationReport report, IDictionary<string, string> roles, IList<ElementId> temporaryIds)
        {
            return new RebarPreviewRequest(fingerprint,
                () =>
                {
                    List<Rebar> bars = generator.GeneratePanel(panel, report, roles);
                    foreach (Rebar bar in bars ?? new List<Rebar>())
                        if (bar != null && !temporaryIds.Contains(bar.Id)) temporaryIds.Add(bar.Id);
                    return bars;
                },
                () => generator.GetPanelInputFingerprint(panel),
                RebarPreviewService.Describe(panel, generator.BarTypes),
                bar =>
                {
                    string role;
                    return bar != null && roles.TryGetValue(bar.Id.Value.ToString(CultureInfo.InvariantCulture), out role) ? role : string.Empty;
                });
        }

        private static List<Floor> FindSupportedFloors(Document doc)
        {
            var result = new List<Floor>();
            foreach (Floor floor in new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>()
                .OrderBy(candidate => candidate.UniqueId, StringComparer.Ordinal))
            {
                try
                {
                    SlabProfile profile = SlabGeometryHelper.AnalyzeSlab(doc, floor);
                    if (profile != null && profile.InnerOpenings.Count == 0 && profile.WidthMm >= 2000 &&
                        profile.LengthMm >= 2000 && profile.ThicknessFeet > 0)
                        result.Add(floor);
                }
                catch { }
            }
            return result;
        }

        private static RebarBarType ResolveExplicitType(IList<RebarBarType> types, string excludedUniqueId)
        {
            foreach (RebarBarType type in types ?? new List<RebarBarType>())
            {
                if (string.Equals(type.UniqueId, excludedUniqueId, StringComparison.Ordinal)) continue;
                RebarBarType resolved = SlabRebarGenerator.FindBarType(types, type.Name);
                if (resolved != null && string.Equals(resolved.UniqueId, type.UniqueId, StringComparison.Ordinal)) return type;
            }
            return null;
        }

        private static void ConfigureMinimalMesh(SlabPanel panel, RebarBarType type)
        {
            panel.Config.BottomLayer.Enabled = true;
            panel.Config.BottomLayer.InvertLayer = false;
            panel.Config.BottomLayer.DiaXLabel = type.Name;
            panel.Config.BottomLayer.DiaYLabel = type.Name;
            panel.Config.BottomLayer.SpacingXMm = 137;
            panel.Config.BottomLayer.SpacingYMm = 163;
            panel.Config.TopLayer.Enabled = false;
            panel.Config.HatReinforce.Enabled = false;
            panel.Config.TopDistribution.Enabled = false;
            panel.Config.Spacer.Enabled = false;
        }
    }
}

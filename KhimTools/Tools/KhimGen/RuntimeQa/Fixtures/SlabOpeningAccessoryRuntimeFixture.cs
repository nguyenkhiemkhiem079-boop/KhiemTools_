using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Revit;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Models;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>
    /// Host-only acceptance for the implemented slab opening, mesh, support and spacer
    /// generator paths. Preview/create parity and an injected transaction failure are
    /// checked without committing any model changes.
    /// </summary>
    public sealed class SlabOpeningAccessoryRuntimeFixture : RuntimeQaFixtureBase
    {
        private static readonly string[] RequiredRoles =
        {
            "bottom-x", "bottom-y", "top-x", "top-y", "support-x", "support-y", "spacer", "opening"
        };

        public override string Id { get { return "SR-OPENING-ACCESSORY"; } }
        public override string Name { get { return "Slab opening, spacer and support parity"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Solve and roll back a supported opening slab with bottom/top meshes, support bars, spacers and automatic opening trimmers; verify role/type/shape/quantity/centerline parity and transaction-failure cleanup."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            SlabPanel panel = FindOpeningPanel(doc);
            if (panel == null)
            {
                Block(result, Id + "_HOST", "Supported opening slab", "A structural horizontal rectangular slab with an axis-aligned rectangular opening and four supported outer edges",
                    "BLOCKED: this disposable QA model does not contain a suitable opening slab.", QaSeverity.CRITICAL);
                return;
            }

            var generator = new SlabRebarGenerator(doc);
            RebarBarType barType = ResolveExplicitType(generator.BarTypes);
            if (barType == null)
            {
                Block(result, Id + "_BAR_TYPE", "Explicit slab bar type", "A loaded RebarBarType that resolves back to the same type identity",
                    "BLOCKED: no explicitly resolvable loaded RebarBarType is available; no substitution will be attempted.", QaSeverity.CRITICAL);
                return;
            }
            ConfigureAllSupportedRoles(panel, barType);

            string fingerprint = generator.GetPanelInputFingerprint(panel);
            RuntimeQaModelFingerprint baseline = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            var previewIds = new List<ElementId>();
            var previewRoles = new Dictionary<string, string>(StringComparer.Ordinal);
            var previewReport = new RebarGenerationReport();
            RebarPreviewSnapshot preview = null;
            Exception previewException = null;
            try
            {
                preview = RebarPreviewService.Capture(doc, new[]
                {
                    CreateRequest(panel, generator, fingerprint, previewReport, previewRoles, previewIds)
                });
            }
            catch (Exception ex) { previewException = ex; }

            string previewRollbackMessage;
            bool previewClean = RuntimeQaSafetyGuard.VerifyRollback(doc, baseline, previewIds, out previewRollbackMessage);
            RebarPreviewComponent component = preview == null ? null : preview.Find(fingerprint);
            if (previewException != null || !previewClean || previewReport.HasErrors || component == null || component.BarCount == 0)
            {
                string detail = previewException == null ? previewRollbackMessage : previewException.GetType().Name + ": " + previewException.Message;
                Block(result, Id + "_PREVIEW", "Combined opening/accessory solver preview", "A non-empty, error-free production plan with automatic opening trims and every requested role",
                    "BLOCKED: required opening, support, spacer or RebarShape resources are unavailable or preview cleanup is uncertain. " + detail, QaSeverity.CRITICAL);
                return;
            }

            Check(result, Id + "_PREVIEW_CANCEL", "Preview cancel/close leaves no persistent reinforcement", previewClean,
                "Exact pre-preview model fingerprint and temporary identity cleanup", previewClean ? "Restored" : previewRollbackMessage,
                "Registration or static compilation alone is not treated as a host pass.", QaSeverity.CRITICAL);

            string changedOpeningFingerprint = null;
            List<CurveLoop> originalOpenings = panel.Openings.ToList();
            try
            {
                panel.Openings = new List<CurveLoop>();
                changedOpeningFingerprint = generator.GetPanelInputFingerprint(panel);
            }
            catch { }
            finally { panel.Openings = originalOpenings; }
            Check(result, Id + "_STALE_OPENING", "Opening boundary changes invalidate the accepted slab preview",
                !string.IsNullOrWhiteSpace(changedOpeningFingerprint) && changedOpeningFingerprint != fingerprint &&
                preview.Find(changedOpeningFingerprint) == null,
                "A plan fingerprint without the opening does not match the accepted opening plan",
                string.IsNullOrWhiteSpace(changedOpeningFingerprint) ? "Opening probe unavailable" : "Changed boundary rejected by old snapshot",
                "The live host remains untouched; only the detached panel opening input is varied.", QaSeverity.CRITICAL);

            HashSet<string> previewRoleSet = new HashSet<string>(component.Paths.Select(path => path.Role).Where(role => !string.IsNullOrEmpty(role)), StringComparer.Ordinal);
            bool previewRolesComplete = RequiredRoles.All(previewRoleSet.Contains);
            Check(result, Id + "_PREVIEW_ROLES", "Preview exposes bottom/top mesh, support, spacer and opening roles",
                previewRolesComplete, string.Join(", ", RequiredRoles), string.Join(", ", previewRoleSet.OrderBy(role => role, StringComparer.Ordinal)),
                "Role identity is taken from the production generator callback, not inferred from coordinates.", QaSeverity.CRITICAL);

            bool createdParity = false;
            bool createdRolesComplete = false;
            bool quantityParity = false;
            bool parityTransactionRolledBack = false;
            var createdIds = new List<ElementId>();
            using (var tx = new Transaction(doc, "K-TOOLS QA slab opening/accessory parity"))
            {
                if (tx.Start() != TransactionStatus.Started)
                {
                    Block(result, Id + "_TX", "Opening/accessory parity transaction", "A started Revit transaction",
                        "BLOCKED: the Revit host refused to start the parity transaction.", QaSeverity.CRITICAL);
                    return;
                }
                try
                {
                    var createRoles = new Dictionary<string, string>(StringComparer.Ordinal);
                    var createReport = new RebarGenerationReport();
                    List<Rebar> generated = generator.GeneratePanel(panel, createReport, createRoles);
                    Track(generated, createdIds, context);
                    doc.Regenerate();
                    createdParity = !createReport.HasErrors && RebarPreviewService.Matches(preview, fingerprint, generated);
                    HashSet<string> createdRoleSet = new HashSet<string>(createRoles.Values, StringComparer.Ordinal);
                    createdRolesComplete = RequiredRoles.All(createdRoleSet.Contains) && previewRoleSet.SetEquals(createdRoleSet);
                    quantityParity = component.BarCount == generated.Sum(bar => bar.Quantity) &&
                        component.RebarSetCount == generated.Count && component.BarFingerprints.Count == generated.Count;
                }
                finally
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        parityTransactionRolledBack = tx.RollBack() == TransactionStatus.RolledBack;
                }
            }

            Check(result, Id + "_CREATE_PARITY", "Production creation matches the complete preview for roles, type, shape, quantity and centerlines",
                createdParity && createdRolesComplete && quantityParity,
                "Exact generated Rebar multiset and all requested role identities equal the solved preview",
                "Parity=" + createdParity + "; roles=" + createdRolesComplete + "; quantity=" + quantityParity,
                "RebarPreviewService.Matches compares host/type/shape/hooks/layout/quantity and every solved centerline.", QaSeverity.CRITICAL);
            Check(result, Id + "_PARITY_ROLLBACK", "Parity inspection transaction rolls back",
                parityTransactionRolledBack, TransactionStatus.RolledBack.ToString(),
                parityTransactionRolledBack ? TransactionStatus.RolledBack.ToString() : "Rollback unconfirmed",
                "Generated bars remain temporary and are never committed by this QA fixture.", QaSeverity.CRITICAL);

            bool controlledFailureObserved = false;
            string failureDetails = string.Empty;
            var failedRunIds = new List<ElementId>();
            try
            {
                TransactionBoundary.Execute(doc, "K-TOOLS QA slab accessory injected failure", () =>
                {
                    var failedRoles = new Dictionary<string, string>(StringComparer.Ordinal);
                    var failedReport = new RebarGenerationReport();
                    List<Rebar> failedBars = generator.GeneratePanel(panel, failedReport, failedRoles);
                    Track(failedBars, failedRunIds, context);
                    if (failedReport.HasErrors || failedBars == null || failedBars.Count == 0)
                        throw new InvalidOperationException("The full opening/accessory plan did not complete before the QA failure point.");
                    doc.Regenerate();
                    throw new SlabAccessoryInjectedFailureException();
                });
            }
            catch (SlabAccessoryInjectedFailureException) { controlledFailureObserved = true; }
            catch (Exception ex) { failureDetails = ex.GetType().Name + ": " + ex.Message; }

            string rollbackMessage = string.Empty;
            bool wholeModelRestored = controlledFailureObserved && RuntimeQaSafetyGuard.VerifyRollback(doc, baseline,
                previewIds.Concat(createdIds).Concat(failedRunIds), out rollbackMessage);
            if (!controlledFailureObserved) rollbackMessage = "Expected transaction-boundary failure was not observed. " + failureDetails;
            Check(result, Id + "_FAILURE_ROLLBACK", "Injected generation failure aborts the whole transaction and preserves unrelated model state",
                wholeModelRestored, "TransactionBoundary rolled back every new bar and restored all preexisting element identities/states",
                wholeModelRestored ? "Exact baseline restored" : rollbackMessage,
                "The failure exists only in this Runtime QA fixture and is thrown after the full test plan is created.", QaSeverity.CRITICAL);
        }

        private static SlabPanel FindOpeningPanel(Document doc)
        {
            foreach (Floor floor in new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>()
                .OrderBy(candidate => candidate.UniqueId, StringComparer.Ordinal))
            {
                try
                {
                    SlabProfile profile = SlabGeometryHelper.AnalyzeSlab(doc, floor);
                    if (profile == null || profile.InnerOpenings == null || profile.InnerOpenings.Count == 0 ||
                        profile.WidthMm < 2000 || profile.LengthMm < 2000 || profile.ThicknessFeet <= 0)
                        continue;
                    var manager = new SlabPanelManager();
                    manager.InitializeFromFloors(doc, new List<Floor> { floor });
                    SlabPanel panel = manager.Panels.FirstOrDefault(candidate => candidate.HostFloor != null &&
                        candidate.Edges != null && candidate.Edges.Count == 4 && candidate.Openings.Count > 0);
                    if (panel != null) return panel;
                }
                catch { }
            }
            return null;
        }

        private static RebarBarType ResolveExplicitType(IList<RebarBarType> types)
        {
            foreach (RebarBarType type in (types ?? new List<RebarBarType>()).Where(candidate => candidate != null && candidate.BarModelDiameter > 0)
                .OrderBy(candidate => candidate.BarModelDiameter).ThenBy(candidate => candidate.UniqueId, StringComparer.Ordinal))
            {
                RebarBarType resolved = SlabRebarGenerator.FindBarType(types, type.Name);
                if (resolved != null && string.Equals(resolved.UniqueId, type.UniqueId, StringComparison.Ordinal)) return type;
            }
            return null;
        }

        private static void ConfigureAllSupportedRoles(SlabPanel panel, RebarBarType barType)
        {
            panel.Config.BottomLayer.Enabled = true;
            panel.Config.BottomLayer.InvertLayer = false;
            panel.Config.BottomLayer.DiaXLabel = barType.Name;
            panel.Config.BottomLayer.DiaYLabel = barType.Name;
            panel.Config.BottomLayer.SpacingXMm = 200;
            panel.Config.BottomLayer.SpacingYMm = 200;
            panel.Config.TopLayer.Enabled = true;
            panel.Config.TopLayer.InvertLayer = false;
            panel.Config.TopLayer.DiaXLabel = barType.Name;
            panel.Config.TopLayer.DiaYLabel = barType.Name;
            panel.Config.TopLayer.SpacingXMm = 200;
            panel.Config.TopLayer.SpacingYMm = 200;
            panel.Config.HatReinforce.Enabled = true;
            panel.Config.HatReinforce.DiaXLabel = barType.Name;
            panel.Config.HatReinforce.DiaYLabel = barType.Name;
            panel.Config.HatReinforce.SpacingXMm = 200;
            panel.Config.HatReinforce.SpacingYMm = 200;
            panel.Config.HatReinforce.HatFactor = "L/4";
            panel.Config.HatReinforce.IsFullSpan = false;
            panel.Config.HatReinforce.HookDownEdge = false;
            panel.Config.TopDistribution.Enabled = false;
            panel.Config.Spacer.Enabled = true;
            panel.Config.Spacer.DiaLabel = barType.Name;
            panel.Config.Spacer.StepXMm = 800;
            panel.Config.Spacer.StepYMm = 800;
            panel.Config.Spacer.HookLenMm = 100;
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

        private static void Track(IEnumerable<Rebar> bars, IList<ElementId> ids, RuntimeQaContext context)
        {
            foreach (Rebar bar in bars ?? Enumerable.Empty<Rebar>())
            {
                if (bar == null) continue;
                if (!ids.Contains(bar.Id)) ids.Add(bar.Id);
                context.TrackCreated(bar.Id);
            }
        }

        private sealed class SlabAccessoryInjectedFailureException : Exception { }
    }
}

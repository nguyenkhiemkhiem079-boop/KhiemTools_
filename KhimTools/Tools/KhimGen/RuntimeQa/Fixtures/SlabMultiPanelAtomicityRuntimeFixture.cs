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
    /// Exercises the production slab generator inside one production transaction boundary,
    /// then injects a test-only failure while Panel B is recording its first generated layer.
    /// The fixture is rollback-only and must run only in a confirmed disposable QA copy.
    /// </summary>
    public sealed class SlabMultiPanelAtomicityRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "SR-BATCH-ATOMIC"; } }
        public override string Name { get { return "Slab multi-panel creation atomicity"; } }
        public override string Suite { get { return "REBAR"; } }
        public override string Description { get { return "Generate Panel A and part of Panel B through the production slab generator in one transaction, inject a QA-only failure, and verify exact whole-batch rollback."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            List<Floor> candidateFloors = FindSupportedRectangularStructuralFloors(doc);
            if (candidateFloors.Count < 2)
            {
                Block(result, Id + "_RES", "Two supported slab hosts", "Two distinct structural Floors with supported geometry and no openings",
                    "BLOCKED: this QA model does not contain two suitable distinct structural Floors.", QaSeverity.CRITICAL);
                return;
            }

            var panelManager = new SlabPanelManager();
            panelManager.InitializeFromFloors(doc, candidateFloors.Take(2).ToList());
            List<SlabPanel> panels = panelManager.Panels
                .Where(panel => panel != null && panel.HostFloor != null)
                .OrderBy(panel => panel.HostFloor.UniqueId, StringComparer.Ordinal)
                .ToList();
            if (panels.Count != 2 || string.Equals(panels[0].HostFloor.UniqueId, panels[1].HostFloor.UniqueId, StringComparison.Ordinal))
            {
                Block(result, Id + "_PANELS", "Distinct production slab panels", "Exactly two distinct analyzed Floors",
                    "BLOCKED: production slab analysis did not provide two distinct panels.", QaSeverity.CRITICAL);
                return;
            }

            var generator = new SlabRebarGenerator(doc);
            RebarBarType barType = ResolveExplicitBarType(generator.BarTypes);
            if (barType == null)
            {
                Block(result, Id + "_BAR_TYPE", "Explicit slab bar type", "A loaded RebarBarType that resolves back to the identical type",
                    "BLOCKED: no unambiguous loaded RebarBarType is available; the fixture will not substitute one.", QaSeverity.CRITICAL);
                return;
            }

            SlabPanel panelA = panels[0];
            SlabPanel panelB = panels[1];
            ConfigurePanel(panelA, barType);
            ConfigurePanel(panelB, barType);
            Check(result, Id + "_TWO_PANELS", "Two distinct supported structural panels are prepared",
                panelA.HostFloor.Id != panelB.HostFloor.Id && panelA.Openings.Count == 0 && panelB.Openings.Count == 0,
                "Two analyzed structural Floors with separate host identities", panelA.HostFloor.UniqueId + " | " + panelB.HostFloor.UniqueId,
                "Both panels use production SlabGeometryHelper analysis and the same explicitly resolved loaded bar type.", QaSeverity.CRITICAL);

            string fingerprintA;
            string fingerprintB;
            try
            {
                fingerprintA = generator.GetPanelInputFingerprint(panelA);
                fingerprintB = generator.GetPanelInputFingerprint(panelB);
            }
            catch (Exception ex)
            {
                Check(result, Id + "_INPUT_FINGERPRINT", "Both slab input fingerprints are captured", false,
                    "Two stable production slab fingerprints", ex.GetType().Name + ": " + ex.Message,
                    "A missing or unreadable model/input fingerprint fails the fixture closed.", QaSeverity.CRITICAL);
                return;
            }

            RuntimeQaModelFingerprint baseline;
            Dictionary<string, ExistingRebarState> existingRebars;
            try
            {
                baseline = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
                existingRebars = CaptureExistingRebarStates(doc, baseline);
            }
            catch (Exception ex)
            {
                Check(result, Id + "_BASELINE", "Complete model and existing Rebar baseline is captured", false,
                    "Exact element IDs, element-state fingerprints, Rebar hosts and centerline fingerprints",
                    ex.GetType().Name + ": " + ex.Message,
                    "The fixture cannot certify rollback without a complete observable baseline.", QaSeverity.CRITICAL);
                return;
            }

            var previewTemporaryIds = new List<ElementId>();
            var previewReportA = new RebarGenerationReport();
            var previewReportB = new RebarGenerationReport();
            var previewRolesA = new Dictionary<string, string>(StringComparer.Ordinal);
            var previewRolesB = new Dictionary<string, string>(StringComparer.Ordinal);
            RebarPreviewRequest requestA = CreatePreviewRequest(panelA, generator, fingerprintA,
                previewReportA, previewRolesA, previewTemporaryIds);
            RebarPreviewRequest requestB = CreatePreviewRequest(panelB, generator, fingerprintB,
                previewReportB, previewRolesB, previewTemporaryIds);

            RebarPreviewSnapshot preview = null;
            Exception previewException = null;
            try { preview = RebarPreviewService.Capture(doc, new[] { requestA, requestB }); }
            catch (Exception ex) { previewException = ex; }

            string previewRollbackMessage;
            bool previewRollback = RuntimeQaSafetyGuard.VerifyRollback(doc, baseline, previewTemporaryIds, out previewRollbackMessage);
            Check(result, Id + "_PREVIEW_CLEANUP", "Production preview leaves no temporary model changes", previewRollback,
                "Exact baseline fingerprint restored and preview-created IDs absent",
                previewRollback ? "Restored" : previewRollbackMessage,
                "The preview uses the production generator and the Runtime QA safety fingerprint.", QaSeverity.CRITICAL);

            RebarPreviewComponent previewA = preview == null ? null : preview.Find(fingerprintA);
            RebarPreviewComponent previewB = preview == null ? null : preview.Find(fingerprintB);
            bool previewValid = previewException == null && previewRollback &&
                !previewReportA.HasErrors && !previewReportB.HasErrors &&
                previewA != null && previewB != null && previewA.BarCount > 0 && previewB.BarCount > 0;
            string previewDetails = previewException != null
                ? previewException.GetType().Name + ": " + previewException.Message
                : "Panel A=" + (previewA == null ? "missing" : previewA.BarCount.ToString(CultureInfo.InvariantCulture)) +
                    "; Panel B=" + (previewB == null ? "missing" : previewB.BarCount.ToString(CultureInfo.InvariantCulture)) +
                    "; generation errors=" + (previewReportA.FailureCount + previewReportB.FailureCount).ToString(CultureInfo.InvariantCulture);
            Check(result, Id + "_PREVIEW", "Both panels have valid production-generator previews", previewValid,
                "Two non-empty, error-free production preview components with stable input fingerprints",
                previewDetails,
                "Preview must succeed for both panels before the creation-transaction test begins.", QaSeverity.CRITICAL);
            if (!previewValid) return;

            int panelBBottomXPaths = previewB.Paths.Count(path => string.Equals(path.Role, "bottom-x", StringComparison.Ordinal));
            if (panelBBottomXPaths < 1)
            {
                Block(result, Id + "_INJECTION_POINT", "Panel B QA-only injection point", "At least one solved Panel B bottom-X centerline",
                    "BLOCKED: the production preview did not expose a usable first-layer injection point.", QaSeverity.CRITICAL);
                return;
            }

            var createdTemporaryIds = new List<ElementId>();
            var panelAReport = new RebarGenerationReport();
            var panelBReport = new RebarGenerationReport();
            var panelARoles = new Dictionary<string, string>(StringComparer.Ordinal);
            var failureInjector = new SlabBatchFailureRoleMap(doc, panelB.HostFloor.UniqueId,
                baseline.ElementIds, createdTemporaryIds, panelBBottomXPaths);
            bool panelASucceeded = false;
            bool panelBStarted = false;
            bool expectedFailureObserved = false;
            string transactionException = string.Empty;

            try
            {
                TransactionBoundary.Execute(doc, "K-TOOLS QA slab multi-panel atomicity", () =>
                {
                    List<Rebar> barsA = generator.GeneratePanel(panelA, panelAReport, panelARoles);
                    TrackTemporaryRebars(barsA, createdTemporaryIds, context);
                    doc.Regenerate();
                    panelASucceeded = !panelAReport.HasErrors && barsA != null && barsA.Count > 0 &&
                        RebarPreviewService.Matches(preview, fingerprintA, barsA);
                    if (!panelASucceeded)
                        throw new InvalidOperationException("Panel A generation did not complete with production preview parity.");

                    panelBStarted = true;
                    // The fixture-only IDictionary throws from the generator's role-recording callback
                    // after Panel B's first generated layer exists, before GeneratePanel can complete.
                    generator.GeneratePanel(panelB, panelBReport, failureInjector);
                    throw new InvalidOperationException("The QA-only Panel B failure injection point was not reached.");
                });
            }
            catch (SlabBatchFailureInjectionException)
            {
                expectedFailureObserved = true;
            }
            catch (Exception ex)
            {
                transactionException = ex.GetType().Name + ": " + ex.Message;
            }

            foreach (ElementId id in failureInjector.CreatedElementIds)
                TrackTemporaryId(id, createdTemporaryIds, context);

            string rollbackMessage;
            bool wholeModelRollback = RuntimeQaSafetyGuard.VerifyRollback(doc, baseline,
                previewTemporaryIds.Concat(createdTemporaryIds), out rollbackMessage);

            RuntimeQaModelFingerprint afterRollback = null;
            Dictionary<string, ExistingRebarState> rebarStatesAfter = null;
            string postSnapshotError = string.Empty;
            try
            {
                afterRollback = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
                rebarStatesAfter = CaptureExistingRebarStates(doc, afterRollback);
            }
            catch (Exception ex)
            {
                postSnapshotError = ex.GetType().Name + ": " + ex.Message;
            }

            bool modelFingerprintRestored = afterRollback != null && ModelFingerprintsMatch(baseline, afterRollback);
            bool rebarPreserved = existingRebars != null && rebarStatesAfter != null &&
                RebarStatesMatch(existingRebars, rebarStatesAfter) &&
                RebarHostCountsMatch(existingRebars, rebarStatesAfter);
            bool injectedBeforePanelBCompleted = expectedFailureObserved && panelBStarted &&
                failureInjector.Injected && failureInjector.CreatedElementIds.Count > 0 && !panelBReport.HasErrors;

            Check(result, Id + "_PANEL_A", "Panel A succeeds before Panel B is injected to fail", panelASucceeded,
                "Non-empty Panel A production creation matching the accepted preview",
                panelASucceeded ? "Succeeded and matched" : (transactionException.Length == 0 ? "Did not complete" : transactionException),
                "Panel A must pass generation and preview parity within the shared transaction before Panel B starts.", QaSeverity.CRITICAL);
            Check(result, Id + "_PANEL_B_INJECT", "Panel B fails mid-generation through a QA-only injection", injectedBeforePanelBCompleted,
                "Controlled failure after Panel B creates its first layer and before its generator returns",
                injectedBeforePanelBCompleted ? "Injected; captured temporary Rebar IDs=" + failureInjector.CreatedElementIds.Count.ToString(CultureInfo.InvariantCulture)
                    : (transactionException.Length == 0 ? "Expected injection was not observed" : transactionException),
                "The throwing role map exists only in this Runtime QA fixture; production generator code is not fault-injected.", QaSeverity.CRITICAL);
            Check(result, Id + "_BATCH_ROLLBACK", "The entire multi-panel transaction rolls back", wholeModelRollback && expectedFailureObserved,
                "TransactionBoundary confirms rollback and the complete model returns to its exact pre-test fingerprint",
                wholeModelRollback && expectedFailureObserved ? "Whole transaction rolled back" : rollbackMessage,
                "Any rollback/start uncertainty or surviving Panel A change fails this check.", QaSeverity.CRITICAL);
            Check(result, Id + "_POST_FINGERPRINT", "Post-rollback element identities and observable state match baseline",
                modelFingerprintRestored && wholeModelRollback,
                "Identical element-ID set and per-element state fingerprints",
                modelFingerprintRestored ? "Exact match" : (postSnapshotError.Length == 0 ? "Mismatch" : postSnapshotError),
                "Element counts alone are not used as proof of rollback.", QaSeverity.CRITICAL);
            Check(result, Id + "_EXISTING_REBAR", "Existing Rebar identities, host assignments, counts and centerlines are preserved",
                rebarPreserved,
                "Identical Rebar UniqueIds, host UniqueIds, quantities, type/shape/hooks/layout and centerline fingerprints",
                rebarPreserved ? "Exact Rebar multiset preserved" : (postSnapshotError.Length == 0 ? "Rebar identity/state mismatch" : postSnapshotError),
                "The comparison covers existing reinforcement across the whole document, including both test hosts.", QaSeverity.CRITICAL);
        }

        private static List<Floor> FindSupportedRectangularStructuralFloors(Document doc)
        {
            var floors = new List<Floor>();
            foreach (Floor floor in new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>()
                .OrderBy(candidate => candidate.UniqueId, StringComparer.Ordinal))
            {
                try
                {
                    SlabProfile profile = SlabGeometryHelper.AnalyzeSlab(doc, floor);
                    if (profile == null || profile.InnerOpenings == null || profile.InnerOpenings.Count != 0 ||
                        profile.WidthMm < 600 || profile.LengthMm < 600 || profile.ThicknessFeet <= 0)
                        continue;
                    floors.Add(floor);
                }
                catch
                {
                    // Unsupported individual floors are excluded; the fixture blocks unless two remain.
                }
            }
            return floors;
        }

        private static RebarBarType ResolveExplicitBarType(IList<RebarBarType> barTypes)
        {
            if (barTypes == null) return null;
            foreach (RebarBarType type in barTypes.Where(candidate => candidate != null && candidate.BarModelDiameter > 0)
                .OrderBy(candidate => candidate.BarModelDiameter).ThenBy(candidate => candidate.UniqueId, StringComparer.Ordinal))
            {
                RebarBarType resolved = SlabRebarGenerator.FindBarType(barTypes, type.Name);
                if (resolved != null && string.Equals(resolved.UniqueId, type.UniqueId, StringComparison.Ordinal)) return type;
            }
            return null;
        }

        private static void ConfigurePanel(SlabPanel panel, RebarBarType barType)
        {
            panel.Config.BottomLayer.Enabled = true;
            panel.Config.BottomLayer.InvertLayer = false;
            panel.Config.BottomLayer.DiaXLabel = barType.Name;
            panel.Config.BottomLayer.DiaYLabel = barType.Name;
            panel.Config.BottomLayer.SpacingXMm = 200;
            panel.Config.BottomLayer.SpacingYMm = 200;
            panel.Config.TopLayer.Enabled = false;
            panel.Config.HatReinforce.Enabled = false;
            panel.Config.TopDistribution.Enabled = false;
            panel.Config.Spacer.Enabled = false;
        }

        private static RebarPreviewRequest CreatePreviewRequest(SlabPanel panel, SlabRebarGenerator generator,
            string inputFingerprint, RebarGenerationReport report, IDictionary<string, string> roles,
            IList<ElementId> temporaryIds)
        {
            return new RebarPreviewRequest(inputFingerprint,
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

        private static void TrackTemporaryRebars(IEnumerable<Rebar> bars, IList<ElementId> temporaryIds, RuntimeQaContext context)
        {
            foreach (Rebar bar in bars ?? Enumerable.Empty<Rebar>())
                if (bar != null) TrackTemporaryId(bar.Id, temporaryIds, context);
        }

        private static void TrackTemporaryId(ElementId id, IList<ElementId> temporaryIds, RuntimeQaContext context)
        {
            if (id == null || id == ElementId.InvalidElementId) return;
            if (!temporaryIds.Contains(id)) temporaryIds.Add(id);
            context.TrackCreated(id);
        }

        private static Dictionary<string, ExistingRebarState> CaptureExistingRebarStates(Document doc,
            RuntimeQaModelFingerprint fingerprint)
        {
            if (doc == null || fingerprint == null || fingerprint.ElementIds == null || fingerprint.ElementStates == null)
                throw new InvalidOperationException("An exact model fingerprint is required before Rebar state capture.");

            var states = new Dictionary<string, ExistingRebarState>(StringComparer.Ordinal);
            foreach (Rebar bar in new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>())
            {
                Element host = doc.GetElement(bar.GetHostId());
                string elementState;
                if (host == null || string.IsNullOrWhiteSpace(bar.UniqueId) ||
                    !fingerprint.ElementStates.TryGetValue(bar.Id, out elementState))
                    throw new InvalidOperationException("An existing Rebar identity, host, or element-state fingerprint could not be resolved.");

                IReadOnlyList<RebarPreviewPath> paths = RebarPreviewService.ReadPaths(new List<Rebar> { bar });
                string centerlineFingerprint = RebarPreviewService.FingerprintBar(bar, paths);
                states.Add(bar.UniqueId, new ExistingRebarState
                {
                    HostUniqueId = host.UniqueId,
                    Quantity = bar.Quantity,
                    ElementStateFingerprint = elementState,
                    RebarFingerprint = centerlineFingerprint
                });
            }
            return states;
        }

        private static bool ModelFingerprintsMatch(RuntimeQaModelFingerprint before, RuntimeQaModelFingerprint after)
        {
            return before != null && after != null && before.ElementCount == after.ElementCount &&
                before.SheetCount == after.SheetCount && before.ViewCount == after.ViewCount &&
                before.ElementIds != null && after.ElementIds != null && before.ElementIds.SetEquals(after.ElementIds) &&
                before.ElementStates != null && after.ElementStates != null && before.ElementStates.Count == after.ElementStates.Count &&
                before.ElementStates.All(pair => after.ElementStates.TryGetValue(pair.Key, out string state) &&
                    string.Equals(pair.Value, state, StringComparison.Ordinal));
        }

        private static bool RebarStatesMatch(IDictionary<string, ExistingRebarState> before,
            IDictionary<string, ExistingRebarState> after)
        {
            return before != null && after != null && before.Count == after.Count && before.All(pair =>
                after.TryGetValue(pair.Key, out ExistingRebarState state) &&
                string.Equals(pair.Value.HostUniqueId, state.HostUniqueId, StringComparison.Ordinal) &&
                pair.Value.Quantity == state.Quantity &&
                string.Equals(pair.Value.ElementStateFingerprint, state.ElementStateFingerprint, StringComparison.Ordinal) &&
                string.Equals(pair.Value.RebarFingerprint, state.RebarFingerprint, StringComparison.Ordinal));
        }

        private static bool RebarHostCountsMatch(IDictionary<string, ExistingRebarState> before,
            IDictionary<string, ExistingRebarState> after)
        {
            Dictionary<string, int> beforeCounts = before.Values.GroupBy(state => state.HostUniqueId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            Dictionary<string, int> afterCounts = after.Values.GroupBy(state => state.HostUniqueId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            return beforeCounts.Count == afterCounts.Count && beforeCounts.All(pair =>
                afterCounts.TryGetValue(pair.Key, out int count) && count == pair.Value);
        }

        private sealed class ExistingRebarState
        {
            public string HostUniqueId { get; set; }
            public int Quantity { get; set; }
            public string ElementStateFingerprint { get; set; }
            public string RebarFingerprint { get; set; }
        }

        private sealed class SlabBatchFailureRoleMap : IDictionary<string, string>
        {
            private readonly Document _document;
            private readonly string _hostUniqueId;
            private readonly ISet<ElementId> _baselineIds;
            private readonly IList<ElementId> _temporaryIds;
            private readonly int _failAfterAssignments;
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

            public bool Injected { get; private set; }
            public IList<ElementId> CreatedElementIds { get; } = new List<ElementId>();

            public SlabBatchFailureRoleMap(Document document, string hostUniqueId, ISet<ElementId> baselineIds,
                IList<ElementId> temporaryIds, int failAfterAssignments)
            {
                _document = document ?? throw new ArgumentNullException(nameof(document));
                _hostUniqueId = hostUniqueId ?? throw new ArgumentNullException(nameof(hostUniqueId));
                _baselineIds = baselineIds ?? throw new ArgumentNullException(nameof(baselineIds));
                _temporaryIds = temporaryIds ?? throw new ArgumentNullException(nameof(temporaryIds));
                if (failAfterAssignments < 1) throw new ArgumentOutOfRangeException(nameof(failAfterAssignments));
                _failAfterAssignments = failAfterAssignments;
            }

            public string this[string key]
            {
                get { return _values[key]; }
                set
                {
                    long numericId;
                    if (!long.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericId))
                        throw new InvalidOperationException("The QA failure injector received an invalid generated Rebar identity.");
                    _values[key] = value;
                    if (Injected) return;
                    if (numericId <= 0) throw new InvalidOperationException("The QA failure injector received an invalid ElementId.");
                    if (_values.Count < _failAfterAssignments) return;

                    CapturePanelBTemporaryRebars();
                    if (CreatedElementIds.Count == 0)
                        throw new InvalidOperationException("The QA failure injector could not capture temporary Panel B Rebar identities.");
                    Injected = true;
                    throw new SlabBatchFailureInjectionException();
                }
            }

            private void CapturePanelBTemporaryRebars()
            {
                foreach (Rebar bar in new FilteredElementCollector(_document).OfClass(typeof(Rebar)).Cast<Rebar>())
                {
                    if (_baselineIds.Contains(bar.Id)) continue;
                    Element host = _document.GetElement(bar.GetHostId());
                    if (host == null || !string.Equals(host.UniqueId, _hostUniqueId, StringComparison.Ordinal)) continue;
                    if (!CreatedElementIds.Contains(bar.Id)) CreatedElementIds.Add(bar.Id);
                    if (!_temporaryIds.Contains(bar.Id)) _temporaryIds.Add(bar.Id);
                }
            }

            public ICollection<string> Keys { get { return _values.Keys; } }
            public ICollection<string> Values { get { return _values.Values; } }
            public int Count { get { return _values.Count; } }
            public bool IsReadOnly { get { return false; } }
            public void Add(string key, string value) { _values.Add(key, value); }
            public bool ContainsKey(string key) { return _values.ContainsKey(key); }
            public bool Remove(string key) { return _values.Remove(key); }
            public bool TryGetValue(string key, out string value) { return _values.TryGetValue(key, out value); }
            public void Add(KeyValuePair<string, string> item) { ((ICollection<KeyValuePair<string, string>>)_values).Add(item); }
            public void Clear() { _values.Clear(); }
            public bool Contains(KeyValuePair<string, string> item) { return ((ICollection<KeyValuePair<string, string>>)_values).Contains(item); }
            public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) { ((ICollection<KeyValuePair<string, string>>)_values).CopyTo(array, arrayIndex); }
            public bool Remove(KeyValuePair<string, string> item) { return ((ICollection<KeyValuePair<string, string>>)_values).Remove(item); }
            public IEnumerator<KeyValuePair<string, string>> GetEnumerator() { return _values.GetEnumerator(); }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }

        private sealed class SlabBatchFailureInjectionException : Exception
        {
            public SlabBatchFailureInjectionException()
                : base("CONTROLLED_QA_FAILURE: injected after Panel B created its first bottom-X layer and before panel generation completed.") { }
        }
    }
}

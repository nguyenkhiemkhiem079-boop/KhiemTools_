using System;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.QuantityTakeoff.Models;
using KhimTools.QuantityTakeoff.Services;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Exercises production K-QS active-view, selection and read-only full-document workflows.</summary>
    public sealed class KQsProductionRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "KQS_SCOPE_AND_READONLY_QA"; } }
        public override string Name { get { return "K-QS Scope and Read-only Production Scan"; } }
        public override string Suite { get { return "QS"; } }
        public override string Description { get { return "Checks production active-view and explicit-selection scan scopes plus read-only whole-document quantity collection."; } }
        public override bool IsCritical { get { return true; } }

        public override bool CanRun(RuntimeQaContext context, out string reason)
        {
            if (!base.CanRun(context, out reason)) return false;
            if (context.Document.ActiveView == null || context.Document.ActiveView.IsTemplate)
            {
                reason = "An active non-template view is required.";
                return false;
            }
            if (!context.UiDocument.Selection.GetElementIds().Any(id => context.Document.GetElement(id)?.Category != null))
            {
                reason = "Select at least one categorized model element to verify explicit selection scope.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            var selected = context.UiDocument.Selection.GetElementIds()
                .Where(id => doc.GetElement(id)?.Category != null).Distinct().ToList();
            var before = RuntimeQaSafetyGuard.CaptureFingerprint(doc);

            var selectedScan = QsModelScanner.Scan(doc, doc.ActiveView, QsScanScope.Selection, selected);
            var selectedLongIds = new System.Collections.Generic.HashSet<long>(selected.Select(x => x.ToLongValue()));
            bool selectionContained = selectedScan.Elements.All(x => selectedLongIds.Contains(x.ElementId.ToLongValue()));
            Check(result, "KQS_SELECTION_SCOPE", "Selection scan returns only explicit selected IDs",
                selectionContained, "Every returned ID belongs to the supplied selection",
                "selected=" + selected.Count + ";returned=" + selectedScan.Elements.Count,
                "Selection scope never falls back to a whole-document collector.", QaSeverity.CRITICAL);

            var viewScan = QsModelScanner.Scan(doc, doc.ActiveView, QsScanScope.ActiveView);
            bool viewScoped = viewScan.Scope == QsScanScope.ActiveView;
            Check(result, "KQS_VIEW_SCOPE", "Active-view scan keeps the requested view scope",
                viewScoped, "ActiveView", viewScan.Scope.ToString(), "A valid active view is required.", QaSeverity.CRITICAL);

            var quantities = QtoCollectorService.Collect(doc);
            bool validLines = quantities.Lines.All(x => !double.IsNaN(x.RawQuantity) && !double.IsInfinity(x.RawQuantity) && x.RawQuantity > 0 &&
                x.ElementIds.Count == x.ElementUniqueIds.Count && x.ElementUniqueIds.Distinct(StringComparer.Ordinal).Count() == x.ElementUniqueIds.Count);
            Check(result, "KQS_QUANTITY_LINES", "Full-document QTO lines have finite positive quantities and non-duplicated element traces",
                validLines, "Finite positive grouped quantities with unique source trace IDs",
                "lines=" + quantities.Lines.Count,
                "The baseline collector intentionally uses entire host-document scope; linked documents are not included.", QaSeverity.CRITICAL);

            var after = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            bool unchanged = before.ElementCount == after.ElementCount && before.ViewCount == after.ViewCount &&
                before.ElementIds.SetEquals(after.ElementIds);
            Check(result, "KQS_READONLY", "Production scans do not modify the model",
                unchanged, "Document element and view fingerprint unchanged",
                "elements=" + before.ElementCount + "->" + after.ElementCount + ";views=" + before.ViewCount + "->" + after.ViewCount,
                "Scanner and quantity collection are read-only.", QaSeverity.CRITICAL);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Core
{
    public static class RuntimeQaSafetyGuard
    {
        public static bool CanRun(RuntimeQaContext context, out string reason)
        {
            if (context == null) { reason = "No runtime QA context."; return false; }
            if (!context.IsDisposableQaCopyConfirmed)
            {
                reason = "BLOCKED_UNSAFE_DOCUMENT: confirm that this is a disposable detached QA copy before running fixtures.";
                return false;
            }
            return CanRun(context.Document, out reason);
        }

        public static bool CanRun(Document doc, out string reason)
        {
            reason = string.Empty;
            if (doc == null) { reason = "No active Revit project document."; return false; }
            if (doc.IsFamilyDocument) { reason = "Runtime QA requires a project document, not a family document."; return false; }
            if (doc.IsReadOnly) { reason = "BLOCKED_UNSAFE_DOCUMENT: the active document is read-only."; return false; }
            if (doc.IsWorkshared) { reason = "BLOCKED_UNSAFE_DOCUMENT: use a detached/local QA project instead of a workshared model."; return false; }
            return true;
        }

        public static RuntimeQaModelFingerprint CaptureFingerprint(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            var fingerprint = new RuntimeQaModelFingerprint();
            IList<Element> elements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();
            fingerprint.ElementCount = elements.Count;
            foreach (Element element in elements)
                if (element != null) fingerprint.ElementIds.Add(element.Id);
            fingerprint.SheetCount = elements.Count(e => e is ViewSheet);
            fingerprint.ViewCount = elements.Count(e => e is View && !(e is ViewSheet));
            if (fingerprint.ElementIds.Count != fingerprint.ElementCount)
                throw new InvalidOperationException("Could not capture an exact element-ID snapshot for runtime QA safety verification.");
            return fingerprint;
        }

        public static bool VerifyRollback(Document doc, RuntimeQaModelFingerprint before, IEnumerable<ElementId> createdIds, out string message)
        {
            message = "Rollback verified.";
            if (doc == null || before == null) { message = "No baseline fingerprint was available."; return false; }
            var leftovers = new List<ElementId>();
            foreach (ElementId id in createdIds ?? Enumerable.Empty<ElementId>())
            {
                if (id == null || id == ElementId.InvalidElementId) continue;
                try
                {
                    if (doc.GetElement(id) != null) leftovers.Add(id);
                }
                catch (Exception ex)
                {
                    message = "Could not verify whether temporary element " + id + " remains: " + ex.GetType().Name + ".";
                    return false;
                }
            }
            RuntimeQaModelFingerprint after;
            try { after = CaptureFingerprint(doc); }
            catch (Exception ex)
            {
                message = "Could not capture the post-rollback model snapshot: " + ex.GetType().Name + ".";
                return false;
            }
            if (leftovers.Count > 0) { message = "Temporary element IDs still exist: " + string.Join(", ", leftovers); return false; }
            if (after.ElementCount != before.ElementCount || after.SheetCount != before.SheetCount || after.ViewCount != before.ViewCount ||
                before.ElementIds == null || after.ElementIds == null || !before.ElementIds.SetEquals(after.ElementIds))
            {
                int changedIds = before.ElementIds == null || after.ElementIds == null ? -1 :
                    before.ElementIds.Except(after.ElementIds).Count() + after.ElementIds.Except(before.ElementIds).Count();
                message = string.Format("Model fingerprint changed after rollback (elements {0}->{1}, sheets {2}->{3}, views {4}->{5}, differing element IDs {6}).",
                    before.ElementCount, after.ElementCount, before.SheetCount, after.SheetCount, before.ViewCount, after.ViewCount, changedIds);
                return false;
            }
            return true;
        }

        public static void AddRollbackCheck(QaFixtureResult result, bool verified, string message)
        {
            result.RollbackVerified = verified;
            result.Checks.Add(new QaCheckResult
            {
                CheckId = "MODEL_ROLLBACK",
                Name = "Temporary model data removed",
                Status = verified ? QaStatus.PASS : QaStatus.FAIL,
                Severity = verified ? QaSeverity.INFO : QaSeverity.CRITICAL,
                Expected = "No fixture element IDs remain and model fingerprint is restored",
                Actual = verified ? "Restored" : "Changed",
                Message = message
            });
        }
    }
}

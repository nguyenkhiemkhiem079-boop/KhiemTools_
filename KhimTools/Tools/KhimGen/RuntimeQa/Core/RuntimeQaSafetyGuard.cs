using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Core
{
    public static class RuntimeQaSafetyGuard
    {
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
            var fingerprint = new RuntimeQaModelFingerprint();
            if (doc == null) return fingerprint;
            try
            {
                IList<Element> elements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();
                fingerprint.ElementCount = elements.Count;
                foreach (Element element in elements) if (element != null) fingerprint.ElementIds.Add(element.Id);
                fingerprint.SheetCount = elements.Count(e => e is ViewSheet);
                fingerprint.ViewCount = elements.Count(e => e is View && !(e is ViewSheet));
            }
            catch { }
            return fingerprint;
        }

        public static bool VerifyRollback(Document doc, RuntimeQaModelFingerprint before, IEnumerable<ElementId> createdIds, out string message)
        {
            message = "Rollback verified.";
            if (doc == null || before == null) { message = "No baseline fingerprint was available."; return false; }
            var leftovers = new List<ElementId>();
            foreach (ElementId id in createdIds ?? Enumerable.Empty<ElementId>())
            {
                try { if (id != null && id != ElementId.InvalidElementId && doc.GetElement(id) != null) leftovers.Add(id); } catch { }
            }
            RuntimeQaModelFingerprint after = CaptureFingerprint(doc);
            if (leftovers.Count > 0) { message = "Temporary element IDs still exist: " + string.Join(", ", leftovers); return false; }
            if (after.ElementCount != before.ElementCount || after.SheetCount != before.SheetCount || after.ViewCount != before.ViewCount)
            {
                message = string.Format("Model fingerprint changed after rollback (elements {0}->{1}, sheets {2}->{3}, views {4}->{5}).",
                    before.ElementCount, after.ElementCount, before.SheetCount, after.SheetCount, before.ViewCount, after.ViewCount);
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

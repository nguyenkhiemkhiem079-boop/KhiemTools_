using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            IList<Element> elements = new FilteredElementCollector(doc).ToElements();
            fingerprint.ElementCount = elements.Count;
            foreach (Element element in elements)
            {
                if (element == null) continue;
                fingerprint.ElementIds.Add(element.Id);
                fingerprint.ElementStates.Add(element.Id, CaptureElementState(element));
            }
            fingerprint.SheetCount = elements.Count(e => e is ViewSheet);
            fingerprint.ViewCount = elements.Count(e => e is View && !(e is ViewSheet));
            if (fingerprint.ElementIds.Count != fingerprint.ElementCount || fingerprint.ElementStates.Count != fingerprint.ElementCount)
                throw new InvalidOperationException("Could not capture exact element IDs and content fingerprints for runtime QA safety verification.");
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
            if (leftovers.Count > 0) { message = "Temporary element IDs still exist: " + FormatElementIds(leftovers); return false; }
            if (after.ElementCount != before.ElementCount || after.SheetCount != before.SheetCount || after.ViewCount != before.ViewCount ||
                before.ElementIds == null || after.ElementIds == null || !before.ElementIds.SetEquals(after.ElementIds) ||
                !StatesMatch(before.ElementStates, after.ElementStates))
            {
                IList<ElementId> addedIds = before.ElementIds == null || after.ElementIds == null
                    ? null : after.ElementIds.Except(before.ElementIds).ToList();
                IList<ElementId> deletedIds = before.ElementIds == null || after.ElementIds == null
                    ? null : before.ElementIds.Except(after.ElementIds).ToList();
                IList<ElementId> modifiedIds = before.ElementStates == null || after.ElementStates == null
                    ? null : GetChangedStateIds(before.ElementStates, after.ElementStates);
                message = string.Format("Model fingerprint changed after rollback (elements {0}->{1}, sheets {2}->{3}, views {4}->{5}; added: {6}; deleted: {7}; modified/version-changed: {8}).",
                    before.ElementCount, after.ElementCount, before.SheetCount, after.SheetCount, before.ViewCount, after.ViewCount,
                    FormatElementIds(addedIds), FormatElementIds(deletedIds), FormatElementIds(modifiedIds));
                return false;
            }
            return true;
        }

        private static bool StatesMatch(IDictionary<ElementId, string> before, IDictionary<ElementId, string> after)
        {
            return before != null && after != null && before.Count == after.Count &&
                before.All(pair => after.TryGetValue(pair.Key, out string state) && string.Equals(state, pair.Value, StringComparison.Ordinal));
        }

        private static IList<ElementId> GetChangedStateIds(IDictionary<ElementId, string> before, IDictionary<ElementId, string> after)
        {
            if (before == null || after == null) return new List<ElementId>();
            return before.Where(pair => after.TryGetValue(pair.Key, out string state) && !string.Equals(state, pair.Value, StringComparison.Ordinal))
                .Select(pair => pair.Key).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToList();
        }

        private static string CaptureElementState(Element element)
        {
            var parts = new List<string>
            {
                "type=" + (element.GetTypeId() == null ? "<null>" : element.GetTypeId().ToString()),
                "category=" + (element.Category == null || element.Category.Id == null ? "<null>" : element.Category.Id.ToString()),
                "ownerView=" + (element.OwnerViewId == null ? "<null>" : element.OwnerViewId.ToString()),
                "pinned=" + element.Pinned.ToString(CultureInfo.InvariantCulture)
            };

            var parameters = new List<string>();
            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter == null || parameter.Definition == null)
                    throw new InvalidOperationException("An element parameter or its definition could not be fingerprinted.");
                string key = parameter.Definition.Name ?? "<unnamed>";
                if (parameter.IsShared) key += "|" + parameter.GUID.ToString("D");
                string value;
                if (!parameter.HasValue) value = "<unset>";
                else
                {
                    switch (parameter.StorageType)
                    {
                        case StorageType.Double: value = parameter.AsDouble().ToString("R", CultureInfo.InvariantCulture); break;
                        case StorageType.Integer: value = parameter.AsInteger().ToString(CultureInfo.InvariantCulture); break;
                        case StorageType.String: value = parameter.AsString() ?? "<null>"; break;
                        case StorageType.ElementId:
                            ElementId referencedId = parameter.AsElementId();
                            value = referencedId == null ? "<null>" : referencedId.ToString();
                            break;
                        default: value = "<none>"; break;
                    }
                }
                parameters.Add("parameter=" + key + "|" + parameter.StorageType + "|" + value);
            }
            parts.AddRange(parameters.OrderBy(value => value, StringComparer.Ordinal));

            LocationPoint point = element.Location as LocationPoint;
            if (point != null)
            {
                parts.Add("location-point=" + FormatPoint(point.Point) + "|" + point.Rotation.ToString("R", CultureInfo.InvariantCulture));
            }
            else
            {
                LocationCurve locationCurve = element.Location as LocationCurve;
                if (locationCurve != null && locationCurve.Curve != null)
                    parts.Add("location-curve=" + string.Join(";", locationCurve.Curve.Tessellate().Select(FormatPoint)));
            }

            BoundingBoxXYZ bounds = element.get_BoundingBox(null);
            parts.Add(bounds == null ? "bounds=<null>" : "bounds=" + FormatPoint(bounds.Min) + "|" + FormatPoint(bounds.Max));

            var builder = new StringBuilder();
            foreach (string part in parts)
                builder.Append(part.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(part);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static string FormatPoint(XYZ point)
        {
            return point.X.ToString("R", CultureInfo.InvariantCulture) + "," +
                point.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
                point.Z.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string FormatElementIds(IEnumerable<ElementId> ids)
        {
            if (ids == null) return "count unavailable; IDs unavailable";
            IList<ElementId> values = ids.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToList();
            string listed = string.Join(", ", values.Take(20).Select(id => id.ToString()));
            if (values.Count > 20) listed += string.Format(" (+{0} more)", values.Count - 20);
            return string.Format("count {0}; IDs: {1}", values.Count, string.IsNullOrEmpty(listed) ? "none" : listed);
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

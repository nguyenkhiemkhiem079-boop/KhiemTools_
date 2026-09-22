using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    /// <summary>Captures and restores only temporary view state changed by K-TOOLS.</summary>
    public sealed class TemporaryViewStateScope : IDisposable
    {
        private readonly Document _doc;
        private readonly List<Tuple<View, TemporaryViewStateSnapshot>> _states;
        private bool _disposed;
        public List<ExportPreflightItem> Diagnostics { get; } = new List<ExportPreflightItem>();

        private TemporaryViewStateScope(Document doc, List<Tuple<View, TemporaryViewStateSnapshot>> states)
        {
            _doc = doc; _states = states;
        }

        public static TemporaryViewStateScope Capture(Document doc, IEnumerable<ViewSheet> sheets)
        {
            var states = new List<Tuple<View, TemporaryViewStateSnapshot>>();
            var seen = new HashSet<long>();
            foreach (ViewSheet sheet in sheets ?? Enumerable.Empty<ViewSheet>())
            {
                AddView(doc, sheet, states, seen);
                ICollection<ElementId> viewportIds = sheet == null ? null : sheet.GetAllViewports();
                foreach (ElementId viewportId in viewportIds ?? new List<ElementId>())
                {
                    Viewport viewport = doc.GetElement(viewportId) as Viewport;
                    AddView(doc, viewport == null ? null : doc.GetElement(viewport.ViewId) as View, states, seen);
                }
            }
            return new TemporaryViewStateScope(doc, states);
        }

        public bool DisableAll()
        {
            using (var transaction = new Transaction(_doc, "K-TOOLS: Disable temporary view properties"))
            {
                transaction.Start();
                try
                {
                    foreach (Tuple<View, TemporaryViewStateSnapshot> pair in _states)
                    {
                        if (!pair.Item2.WasTemporaryViewPropertiesEnabled) continue;
                        try
                        {
                            pair.Item1.DisableTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties);
                            pair.Item2.WasChangedByKTools = true;
                        }
                        catch (Exception ex)
                        {
                            Diagnostics.Add(new ExportPreflightItem { Code = ExportPreflightCode.TEMP_VIEW_MODE_BLOCKED, Severity = ExportPreflightSeverity.BLOCKED,
                                SheetId = pair.Item2.ViewId, Message = "TEMP_VIEW_MODE_BLOCKED: " + ex.Message, CanExecute = false });
                            Debug.WriteLine("[K-TOOLS][SheetExport] temporary view disable failed: " + ex);
                        }
                    }
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[K-TOOLS][SheetExport] temporary view disable transaction failed: " + ex);
                    transaction.RollBack();
                    throw;
                }
            }
            return Diagnostics.Count == 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_doc == null || !_states.Any(item => item.Item2.WasChangedByKTools)) return;
            using (var transaction = new Transaction(_doc, "K-TOOLS: Restore temporary view properties"))
            {
                transaction.Start();
                try
                {
                    foreach (Tuple<View, TemporaryViewStateSnapshot> pair in _states.Where(item => item.Item2.WasChangedByKTools))
                    {
                        try
                        {
                            if (pair.Item1.IsTemporaryViewPropertiesModeEnabled()) continue;
                            if (pair.Item2.TemporaryViewPropertiesId != null && pair.Item2.TemporaryViewPropertiesId != ElementId.InvalidElementId)
                                pair.Item1.EnableTemporaryViewPropertiesMode(pair.Item2.TemporaryViewPropertiesId);
                        }
                        catch (Exception ex) { Debug.WriteLine("[K-TOOLS][SheetExport] temporary view restore failed: " + ex); }
                    }
                    transaction.Commit();
                }
                catch (Exception ex) { Debug.WriteLine("[K-TOOLS][SheetExport] temporary view restore transaction failed: " + ex); transaction.RollBack(); }
            }
        }

        private static void AddView(Document doc, View view, List<Tuple<View, TemporaryViewStateSnapshot>> states, HashSet<long> seen)
        {
            // IntegerValue is available on the legacy Revit API as well as the
            // modern target; the warning on newer targets is compatibility-only.
            if (view == null || view.Id == null || !seen.Add(view.Id.IntegerValue)) return;
            bool enabled = false;
            ElementId propertiesId = ElementId.InvalidElementId;
            try { enabled = view.IsTemporaryViewPropertiesModeEnabled(); }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][SheetExport] temporary view state read failed: " + ex); }
            try { propertiesId = view.GetTemporaryViewPropertiesId(); }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][SheetExport] temporary view source read failed: " + ex); }
            states.Add(Tuple.Create(view, new TemporaryViewStateSnapshot { ViewId = view.Id, WasTemporaryViewPropertiesEnabled = enabled, TemporaryViewPropertiesId = propertiesId }));
        }
    }
}

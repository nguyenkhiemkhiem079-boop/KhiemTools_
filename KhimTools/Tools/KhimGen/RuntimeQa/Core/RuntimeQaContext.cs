using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.RuntimeQa.Core
{
    /// <summary>Runtime QA context. Revit API work is intentionally synchronous on the command thread.</summary>
    public sealed class RuntimeQaContext
    {
        public UIDocument UiDocument { get; private set; }
        public Document Document { get { return UiDocument == null ? null : UiDocument.Document; } }
        public string RunId { get; private set; }
        public string OutputDirectory { get; private set; }
        public bool PreserveArtifacts { get; set; }
        public bool StopOnCriticalFailure { get; set; }
        public bool IsDisposableQaCopyConfirmed { get; private set; }
        public Func<bool> IsCancellationRequested { get; set; }
        public ElementId OriginalActiveViewId { get; private set; }
        public IList<ElementId> OriginalSelection { get; private set; } = new List<ElementId>();
        public RuntimeQaModelFingerprint InitialFingerprint { get; private set; }
        internal readonly List<ElementId> CreatedElementIds = new List<ElementId>();

        public RuntimeQaContext(UIDocument uidoc, string runId = null, string outputDirectory = null)
        {
            UiDocument = uidoc;
            RunId = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString("N") : runId;
            OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KhimTools", "RuntimeQA", RunId)
                : outputDirectory;
            CaptureUiState();
            InitialFingerprint = RuntimeQaSafetyGuard.CaptureFingerprint(Document);
        }

        public void TrackCreated(ElementId id)
        {
            if (id != null && id != ElementId.InvalidElementId && !CreatedElementIds.Contains(id)) CreatedElementIds.Add(id);
        }

        public void ConfirmDisposableQaCopy(bool confirmed)
        {
            IsDisposableQaCopyConfirmed = confirmed;
        }

        internal void RestoreUiState()
        {
            if (UiDocument == null || Document == null) return;
            try { UiDocument.Selection.SetElementIds(OriginalSelection.ToList()); } catch { }
            if (OriginalActiveViewId != null && OriginalActiveViewId != ElementId.InvalidElementId)
            {
                try
                {
                    View view = Document.GetElement(OriginalActiveViewId) as View;
                    if (view != null && !view.IsTemplate) UiDocument.ActiveView = view;
                }
                catch { }
            }
        }

        private void CaptureUiState()
        {
            if (UiDocument == null) return;
            try { OriginalSelection = UiDocument.Selection.GetElementIds().ToList(); } catch { }
            try { OriginalActiveViewId = UiDocument.ActiveView == null ? ElementId.InvalidElementId : UiDocument.ActiveView.Id; } catch { OriginalActiveViewId = ElementId.InvalidElementId; }
        }
    }

    public sealed class RuntimeQaModelFingerprint
    {
        public int ElementCount { get; set; }
        public int SheetCount { get; set; }
        public int ViewCount { get; set; }
        public HashSet<ElementId> ElementIds { get; set; } = new HashSet<ElementId>();
        public Dictionary<ElementId, string> ElementStates { get; set; } = new Dictionary<ElementId, string>();
    }
}

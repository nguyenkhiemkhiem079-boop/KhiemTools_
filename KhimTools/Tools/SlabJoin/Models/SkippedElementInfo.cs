using Autodesk.Revit.DB;

namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// Describes a single element that was excluded from processing, and why.
    /// Used purely for reporting/logging purposes.
    /// </summary>
    public sealed class SkippedElementInfo
    {
        /// <summary>
        /// The ElementId of the skipped element.
        /// </summary>
        public ElementId ElementId { get; }

        /// <summary>
        /// Human-readable reason the element was skipped
        /// (e.g. "Non-structural floor", "Element is a type", "In a group", etc.).
        /// </summary>
        public string Reason { get; }

        public SkippedElementInfo(ElementId elementId, string reason)
        {
            ElementId = elementId;
            Reason = reason;
        }
    }
}

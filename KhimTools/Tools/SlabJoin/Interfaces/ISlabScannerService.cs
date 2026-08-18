using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.SlabJoin.Models;

namespace KhimTools.SlabJoin.Interfaces
{
    /// <summary>
    /// Responsible for discovering all eligible structural Floor elements in the
    /// active (host) document, applying every business-rule filter
    /// (structural only, no types, no linked models, no groups, no demolished
    /// elements, primary design option only).
    /// </summary>
    public interface ISlabScannerService
    {
        /// <summary>
        /// Quét toàn bộ model, trả về danh sách Floor đủ điều kiện join/unjoin.
        /// </summary>
        IList<Floor> GetEligibleStructuralFloors(Document doc, out List<SkippedElementInfo> skippedElements);

        /// <summary>
        /// Quét theo scope (Active View hoặc Entire Model).
        /// activeViewOnly = true → chỉ sàn trong doc.ActiveView.
        /// </summary>
        IList<Floor> GetFloors(Document doc, bool activeViewOnly, out List<SkippedElementInfo> skippedElements);
    }
}

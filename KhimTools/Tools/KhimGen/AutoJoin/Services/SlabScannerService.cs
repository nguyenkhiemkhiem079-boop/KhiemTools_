using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.SlabJoin.Interfaces;
using KhimTools.SlabJoin.Models;
using KhimTools.SlabJoin.Utilities;

namespace KhimTools.SlabJoin.Services
{
    /// <summary>
    /// Default implementation of <see cref="ISlabScannerService"/>.
    /// Lấy TẤT CẢ sàn (OST_Floors) — cả structural lẫn architectural —
    /// vì JoinGeometry hoạt động trên mọi loại Floor trong Revit.
    /// (Tham chiếu: Python pyRevit script đồng nghiệp sử dụng OST_Floors không filter Structural)
    ///
    /// Scope:
    ///   activeViewOnly = true  → chỉ lấy sàn trong Active View (khớp chế độ "Active View")
    ///   activeViewOnly = false → toàn bộ model (chế độ "Entire Model")
    /// </summary>
    public sealed class SlabScannerService : ISlabScannerService
    {
        public IList<Floor> GetEligibleStructuralFloors(Document doc, out List<SkippedElementInfo> skippedElements)
            => GetFloors(doc, activeViewOnly: false, out skippedElements);

        public IList<Floor> GetFloors(Document doc, bool activeViewOnly, out List<SkippedElementInfo> skippedElements)
        {
            skippedElements = new List<SkippedElementInfo>();
            var eligibleFloors = new List<Floor>();

            // Scope theo view hoặc toàn model — giống Python code
            FilteredElementCollector collector;
            if (activeViewOnly && doc.ActiveView != null)
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
            else
                collector = new FilteredElementCollector(doc);

            collector = collector
                .OfCategory(BuiltInCategory.OST_Floors)
                .OfClass(typeof(Floor))
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (!(element is Floor floor)) continue;

                // Bỏ qua phần tử đã bị phá dỡ
                if (ElementFilterUtils.IsDemolished(floor))
                {
                    skippedElements.Add(new SkippedElementInfo(floor.Id, "Demolished in current phase"));
                    continue;
                }

                // Bỏ qua phần tử thuộc Group (JoinGeometry không hoạt động với grouped elements)
                if (ElementFilterUtils.IsInGroup(floor))
                {
                    skippedElements.Add(new SkippedElementInfo(floor.Id, "Member of a group"));
                    continue;
                }

                // Bỏ qua Design Option không phải primary
                if (ElementFilterUtils.IsInNonPrimaryDesignOption(floor))
                {
                    skippedElements.Add(new SkippedElementInfo(floor.Id, "In a non-primary design option"));
                    continue;
                }

                eligibleFloors.Add(floor);
            }

            return eligibleFloors;
        }
    }
}

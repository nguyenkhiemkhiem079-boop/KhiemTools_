using System;
using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Services
{
    public static class ScheduleSegmentPlacementService
    {
        public static ScheduleSheetInstance PlaceSegment(Document doc, ElementId sheetId, ElementId scheduleId, XYZ point, int segmentIndex)
        {
            if (segmentIndex < 0) throw new ArgumentOutOfRangeException(nameof(segmentIndex));
            return ScheduleSplitApiAdapter.CreateSegmentInstance(doc, sheetId, scheduleId, point, segmentIndex);
        }
    }
}

// Synthetic API contracts only. No Autodesk Revit model or host execution.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Autodesk.Revit.DB
{
    public enum BuiltInCategory { OST_Walls = -2000011 }
    public sealed class ElementId
    {
#if NEW_API
        public long Value { get; }
        public ElementId(long value) { Value = value; }
#else
        public int IntegerValue { get; }
        public ElementId(int value) { IntegerValue = value; }
#endif
    }
    public sealed class Category { public ElementId Id { get; set; } }
    public sealed class Document { }
    public sealed class XYZ { }
    public sealed class ScheduleDefinition { public bool IsKeySchedule { get; set; } }
    public sealed class ViewSchedule
    {
        public bool IsTitleblockRevisionSchedule { get; set; }
        public bool IsInternalKeynoteSchedule { get; set; }
        public ScheduleDefinition Definition { get; } = new ScheduleDefinition();
#if NEW_API
        public List<double> Heights { get; private set; } = new List<double> { 10 };
        public bool ThrowOnSet { get; set; }
        public bool IsSplit() => Heights.Count > 1;
        public int GetSegmentCount() => Heights.Count;
        public double GetSegmentHeight(int index) => Heights[index];
        public void Split(IList<double> heights) { Heights = heights.ToList(); }
        public void SetSegmentHeight(int index, double height)
        {
            if (ThrowOnSet) throw new InvalidOperationException("synthetic adapter failure");
            Heights[index] = height;
        }
        public void DeleteSegment(int index) { Heights.RemoveAt(index); }
        public void MergeSegments(int moved, int target) { Heights[target] += Heights[moved]; Heights.RemoveAt(moved); }
        public IList<ElementId> GetScheduleInstances(int index) => new[] { new ElementId(123) };
        public ScheduleHeightsOnSheet GetScheduleHeightsOnSheet() => new ScheduleHeightsOnSheet();
#endif
    }
#if NEW_API
    public sealed class ScheduleHeightsOnSheet : IDisposable
    {
        public bool IsValidObject => true;
        public double TitleHeight => 1;
        public double ColumnHeaderHeight => 2;
        public IList<double> GetBodyRowHeights() => new[] { 3d, 4d };
        public void Dispose() { }
    }
#endif
    public sealed class ScheduleSheetInstance
    {
        public XYZ Point { get; set; }
#if NEW_API
        public int SegmentIndex { get; set; }
        public static ScheduleSheetInstance Create(Document document, ElementId sheet, ElementId schedule, XYZ point, int index) =>
            new ScheduleSheetInstance { Point = point, SegmentIndex = index };
#endif
    }
}

using System;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.ScheduleSplit.Services;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }
    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }
    private static void Main()
    {
        Check(((ElementId)null).ToLongValue() == -1, "Null identity sentinel");
        Check(new ElementId(123).ToLongValue() == 123 && new ElementId(123).ToCheckedIntValue() == 123, "Exact ordinary numeric identity");
        var category = new Category { Id = new ElementId(-2000011) };
        Check(category.IsCategory(BuiltInCategory.OST_Walls) && category.GetBuiltInCategoryCompat() == BuiltInCategory.OST_Walls, "Negative built-in category identity");
        Check(!((Category)null).IsCategory(BuiltInCategory.OST_Walls), "Null category does not match");
        var schedule = new ViewSchedule();
#if NEW_API
        Check(new ElementId(1L << 40).ToLongValue() == 1L << 40, "64-bit IDs are preserved");
        Check(Throws<OverflowException>(() => new ElementId(1L << 40).ToCheckedIntValue()) &&
              Throws<OverflowException>(() => new Category { Id = new ElementId(1L << 40) }.GetBuiltInCategoryCompat()),
              "Legacy integer storage and category identity reject overflow instead of truncation");
        Check(ScheduleSplitApiAdapter.IsAvailable && ScheduleSplitApiAdapter.CanSplit(schedule, out _), "New schedule capability available");
        ScheduleSplitApiAdapter.Split(schedule, new[] { 20d, 30d });
        Check(ScheduleSplitApiAdapter.GetSegmentCount(schedule) == 2, "Split calls actual discovered method");
        Check(ScheduleSplitApiAdapter.GetSegmentHeights(schedule).SequenceEqual(new[] { 20d, 30d }), "Exact segment heights through adapter");
        Check(ScheduleSplitApiAdapter.GetTotalHeight(schedule) == 10, "Height API returned data is consumed");
        var instance = ScheduleSplitApiAdapter.CreateSegmentInstance(new Document(), new ElementId(1), new ElementId(2), new XYZ(), 2);
        int originalIndex = ScheduleSplitApiAdapter.GetSegmentIndex(instance);
        ScheduleSplitApiAdapter.SetSegmentIndex(instance, 1);
        Check(originalIndex == 2 && ScheduleSplitApiAdapter.GetSegmentIndex(instance) == 1, "Exact placement overload and segment reassignment adapter");
        schedule.ThrowOnSet = true;
        Check(Throws<InvalidOperationException>(() => ScheduleSplitApiAdapter.SetSegmentHeight(schedule, 0, 10)), "Original API failure propagates without reflection wrapping");
#else
        Check(!ScheduleSplitApiAdapter.IsAvailable, "Missing schedule segment API detected");
        Check(!ScheduleSplitApiAdapter.CanSplit(schedule, out string reason) && reason.Contains("does not expose"), "Missing capability is an explicit preflight blocker");
        Check(Throws<InvalidOperationException>(() => ScheduleSplitApiAdapter.Split(schedule, new[] { 20d, 30d })), "Unavailable split never mutates a model");
        Check(Throws<NotSupportedException>(() => ScheduleSplitApiAdapter.CreateSegmentInstance(new Document(), new ElementId(1), new ElementId(2), new XYZ(), 0)), "No arbitrary placement fallback on old API");
#endif
        Console.WriteLine("REVIT_COMPAT_UNIT=PASS (" + _checks + " checks); synthetic API only, no live Revit execution.");
    }
}

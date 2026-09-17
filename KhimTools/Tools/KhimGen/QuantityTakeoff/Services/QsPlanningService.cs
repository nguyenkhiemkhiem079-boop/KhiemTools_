using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsPlanningService
    {
        public static List<QsSCurvePoint> BuildSCurve(IEnumerable<QsPerformancePoint> points) => (points ?? Enumerable.Empty<QsPerformancePoint>()).OrderBy(x => x.DataDate).Select(x => new QsSCurvePoint { Date = x.DataDate, PlannedValue = x.PV, EarnedValue = x.EV, ActualCost = x.AC ?? 0, PlannedPercent = x.BAC == 0 ? 0 : (double)(x.PV / x.BAC * 100), EarnedPercent = x.BAC == 0 ? 0 : (double)(x.EV / x.BAC * 100) }).ToList();
        public static decimal PlannedValue(IEnumerable<QsTaskQuantity> quantities, IEnumerable<QsScheduleTask> tasks, DateTime dataDate)
        { var taskMap = (tasks ?? Enumerable.Empty<QsScheduleTask>()).ToDictionary(x => x.TaskId); decimal total = 0; foreach (var q in quantities ?? Enumerable.Empty<QsTaskQuantity>()) if (taskMap.TryGetValue(q.TaskId, out var t) && t.StartDate < dataDate && t.FinishDate >= t.StartDate) { double ratio = Math.Max(0, Math.Min(1, (dataDate - t.StartDate).TotalDays / Math.Max(1, (t.FinishDate - t.StartDate).TotalDays))); total += q.PlannedCost * (decimal)ratio; } return total; }
        public static string DelayStatus(QsScheduleTask task, DateTime dataDate) { if (task.PercentComplete >= 100) return "COMPLETE"; if (task.FinishDate < dataDate) return "DELAYED"; if (task.StartDate <= dataDate) return "AT_RISK"; return "ON_TRACK"; }
        public static List<QsLookaheadItem> Lookahead(IEnumerable<QsScheduleTask> tasks, DateTime dataDate, int weeks = 4) => (tasks ?? Enumerable.Empty<QsScheduleTask>()).Where(x => x.FinishDate >= dataDate && x.StartDate <= dataDate.AddDays(7 * weeks) && x.PercentComplete < 100).Select(x => new QsLookaheadItem { TaskId = x.TaskId, TaskName = x.TaskName, Start = x.StartDate, Finish = x.FinishDate, Progress = x.PercentComplete, Status = Enum.TryParse<QsTaskStatus>(DelayStatus(x, dataDate), out var s) ? s : QsTaskStatus.IN_PROGRESS }).OrderBy(x => x.Start).ToList();
        public static double LinearPlannedQuantity(double total, DateTime start, DateTime finish, DateTime date) { if (finish <= start) return date >= finish ? total : 0; return total * Math.Max(0, Math.Min(1, (date - start).TotalDays / (finish - start).TotalDays)); }
    }
}

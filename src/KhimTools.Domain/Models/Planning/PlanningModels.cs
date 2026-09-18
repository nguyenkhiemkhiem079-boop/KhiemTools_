using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Planning
{
    public enum QsTaskStatus
    {
        NOT_STARTED,
        IN_PROGRESS,
        COMPLETE,
        ON_HOLD,
        DELAYED,
        CANCELLED
    }

    public sealed class QsWbsItem
    {
        public string WbsId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string WbsCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ParentWbsId { get; set; } = string.Empty;
        public int Level { get; set; }
        public string WorkPackage { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class QsScheduleTask
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ExternalTaskId { get; set; } = string.Empty;
        public string WbsId { get; set; } = string.Empty;
        public string TaskCode { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime FinishDate { get; set; }
        public DateTime? BaselineStart { get; set; }
        public DateTime? BaselineFinish { get; set; }
        public DateTime? CurrentStart { get; set; }
        public DateTime? CurrentFinish { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualFinish { get; set; }
        public double PercentComplete { get; set; }
        public QsTaskStatus TaskStatus { get; set; }
        public string WorkPackage { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public long Version { get; set; } = 1;
    }

    public sealed class QsScheduleBaseline
    {
        public string BaselineId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Revision { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "DRAFT";
        public List<QsScheduleTask> TaskSnapshot { get; set; } = new List<QsScheduleTask>();
    }

    public sealed class QsScheduleMapping
    {
        public string MappingId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string MappingType { get; set; } = "BOQ";
        public List<string> ElementUniqueIds { get; set; } = new List<string>();
        public List<string> BoqCodes { get; set; } = new List<string>();
        public List<string> Levels { get; set; } = new List<string>();
        public List<string> WorkPackages { get; set; } = new List<string>();
        public string Status { get; set; } = "ACTIVE";
    }

    public sealed class QsTaskQuantity
    {
        public string TaskId { get; set; } = string.Empty;
        public string MeasurementCode { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public double PlannedQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal PlannedCost { get; set; }
        public List<string> SourceQuantityIds { get; set; } = new List<string>();
    }

    public sealed class QsSCurvePoint
    {
        public DateTime Date { get; set; }
        public decimal PlannedValue { get; set; }
        public decimal EarnedValue { get; set; }
        public decimal ActualCost { get; set; }
        public double PlannedPercent { get; set; }
        public double EarnedPercent { get; set; }
    }

    public sealed class QsPerformancePoint
    {
        public string PeriodId { get; set; } = string.Empty;
        public DateTime DataDate { get; set; }
        public decimal PV { get; set; }
        public decimal EV { get; set; }
        public decimal? AC { get; set; }
        public decimal SV => EV - PV;
        public decimal? CV => AC.HasValue ? EV - AC.Value : (decimal?)null;
        public double? SPI => PV == 0 ? (double?)null : (double)(EV / PV);
        public double? CPI => !AC.HasValue || AC == 0 ? (double?)null : (double)(EV / AC.Value);
        public decimal BAC { get; set; }
    }

    public sealed class QsLookaheadItem
    {
        public string TaskId { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime Finish { get; set; }
        public double Progress { get; set; }
        public double RemainingQuantity { get; set; }
        public double? ProductionRate { get; set; }
        public DateTime? ForecastFinish { get; set; }
        public QsTaskStatus Status { get; set; }
    }
}

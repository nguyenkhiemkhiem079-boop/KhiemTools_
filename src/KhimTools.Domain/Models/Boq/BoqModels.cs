using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Boq
{
    public enum QsClassificationStatus
    {
        Classified,
        Unclassified,
        Ambiguous,
        Ignored
    }

    public sealed class QsBoqItem
    {
        public string BoqCode { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WorkPackage { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string ParentCode { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public long Version { get; set; } = 1;
    }

    public sealed class QsBoqRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Category { get; set; } = string.Empty;
        public string FamilyPattern { get; set; } = string.Empty;
        public string TypePattern { get; set; } = string.Empty;
        public string MaterialPattern { get; set; } = string.Empty;
        public string MeasurementCode { get; set; } = string.Empty;
        public string TargetBoqCode { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    public sealed class QsClassificationResult
    {
        public string BoqCode { get; set; } = string.Empty;
        public QsClassificationStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class QsQuantitySnapshot
    {
        public string SchemaVersion { get; set; } = "KQS_Snapshot_v1";
        public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProjectTitle { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string ScanScope { get; set; } = "ActiveView";
        public List<QsSnapshotElement> Elements { get; set; } = new List<QsSnapshotElement>();
        public List<QsSnapshotQuantity> Quantities { get; set; } = new List<QsSnapshotQuantity>();
    }

    public sealed class QsSnapshotElement
    {
        public string UniqueId { get; set; } = string.Empty;
        public long ElementIdAtSnapshot { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
    }

    public sealed class QsSnapshotQuantity
    {
        public string ElementUniqueId { get; set; } = string.Empty;
        public string MeasurementCode { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
    }

    public sealed class QsCompareRow
    {
        public string Status { get; set; } = string.Empty; // ADDED, REMOVED, UNCHANGED, MODIFIED
        public string ChangeReasons { get; set; } = string.Empty;
        public string UniqueId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string OldType { get; set; } = string.Empty;
        public string NewType { get; set; } = string.Empty;
        public string Measurement { get; set; } = string.Empty;
        public double BaselineQuantity { get; set; }
        public double CurrentQuantity { get; set; }
        public double Delta { get; set; }
        public double? DeltaPercent { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}

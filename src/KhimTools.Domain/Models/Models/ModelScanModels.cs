using System;
using System.Collections.Generic;
using KhimTools.Domain.Models.Elements;

namespace KhimTools.Domain.Models.Models
{
    public sealed class KqsModelIdentity
    {
        public string ModelId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string SourceType { get; set; } = "REVIT";
        public string VersionReference { get; set; } = string.Empty;
        public DateTime? LastScan { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum KqsScanSessionStatus
    {
        Open,
        Batching,
        Validating,
        Committed,
        Failed,
        Cancelled
    }

    public sealed class KqsScanSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string ModelVersionReference { get; set; } = string.Empty;
        public KqsScanSessionStatus Status { get; set; } = KqsScanSessionStatus.Open;
        public int TotalBatchesExpected { get; set; }
        public int BatchesReceived { get; set; }
        public int TotalElements { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
    }

    public sealed class KqsScanBatch
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString("N");
        public string SessionId { get; set; } = string.Empty;
        public int BatchIndex { get; set; }
        public int ElementCount => Elements.Count;
        public List<QsElementData> Elements { get; set; } = new List<QsElementData>();
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class KqsModelScanRecord
    {
        public string ScanId { get; set; } = Guid.NewGuid().ToString("N");
        public string SessionId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string VersionReference { get; set; } = string.Empty;
        public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
        public int TotalElements { get; set; }
        public bool IsCurrentActive { get; set; } = true;
        public List<QsElementData> Elements { get; set; } = new List<QsElementData>();
    }
}

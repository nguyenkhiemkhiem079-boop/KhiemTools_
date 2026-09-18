using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Changes
{
    public sealed class QsChangeEvent
    {
        public string ChangeEventId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ChangeNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ChangeType { get; set; } = "MODEL_REVISION";
        public string SourceType { get; set; } = "MANUAL";
        public string SourceReference { get; set; } = string.Empty;
        public DateTime RaisedDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "IDENTIFIED"; // IDENTIFIED, ASSESSED, APPROVED, REJECTED, VOID
        public string Priority { get; set; } = "MEDIUM";
        public decimal PotentialCostImpact { get; set; }
        public int PotentialTimeImpactDays { get; set; }
        public string Currency { get; set; } = "VND";
        public List<string> AffectedBoqCodes { get; set; } = new List<string>();
        public List<string> AffectedTaskIds { get; set; } = new List<string>();
        public List<string> AffectedElementUniqueIds { get; set; } = new List<string>();
        public long Version { get; set; } = 1;
    }

    public sealed class QsChangeImpactAssessment
    {
        public string AssessmentId { get; set; } = Guid.NewGuid().ToString("N");
        public string ChangeEventId { get; set; } = string.Empty;
        public int Revision { get; set; }
        public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;
        public decimal DirectCostImpact { get; set; }
        public decimal IndirectCostImpact { get; set; }
        public decimal TotalCostImpact { get; set; }
        public int TimeImpactDays { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = "PRELIMINARY";
        public string Assumptions { get; set; } = string.Empty;
    }

    public sealed class QsChangeQuantityImpact
    {
        public string ChangeEventId { get; set; } = string.Empty;
        public string MeasurementCode { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public double BaselineQuantity { get; set; }
        public double CurrentQuantity { get; set; }
        public double QuantityDelta => CurrentQuantity - BaselineQuantity;
        public string Unit { get; set; } = string.Empty;
        public List<string> RelatedUniqueIds { get; set; } = new List<string>();
    }

    public sealed class QsChangeCostImpact
    {
        public string ChangeEventId { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public double QuantityDelta { get; set; }
        public decimal Rate { get; set; }
        public decimal DirectCost { get; set; }
        public decimal IndirectCost { get; set; }
        public decimal TotalCost { get; set; }
        public string RateSource { get; set; } = "CONTRACT_RATE";
        public string Currency { get; set; } = "VND";
    }

    public sealed class QsChangeScheduleImpact
    {
        public string ChangeEventId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public DateTime? BaselineFinish { get; set; }
        public DateTime? CurrentFinish { get; set; }
        public int DelayDays { get; set; }
        public string ImpactType { get; set; } = "OTHER";
        public string Status { get; set; } = "POTENTIAL";
    }

    public sealed class QsExtensionOfTime
    {
        public string EotId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string EotNumber { get; set; } = string.Empty;
        public string ChangeEventId { get; set; } = string.Empty;
        public int ClaimedDays { get; set; }
        public int AssessedDays { get; set; }
        public int ApprovedDays { get; set; }
        public string Status { get; set; } = "DRAFT"; // DRAFT, SUBMITTED, APPROVED, REJECTED
        public string Cause { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public long Version { get; set; } = 1;
    }

    public sealed class QsClaim
    {
        public string ClaimId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ClaimNumber { get; set; } = string.Empty;
        public string ChangeEventId { get; set; } = string.Empty;
        public string ClaimType { get; set; } = "VARIATION";
        public decimal ClaimedAmount { get; set; }
        public decimal AssessedAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public int ClaimedDays { get; set; }
        public int ApprovedDays { get; set; }
        public string Status { get; set; } = "DRAFT"; // DRAFT, SUBMITTED, NEGOTIATING, SETTLED, REJECTED
        public long Version { get; set; } = 1;
    }

    public sealed class QsRfi
    {
        public string RfiId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string RfiNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime RaisedDate { get; set; } = DateTime.UtcNow;
        public DateTime? RequiredResponseDate { get; set; }
        public DateTime? RespondedDate { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public string Status { get; set; } = "OPEN"; // OPEN, ANSWERED, CLOSED
        public string RelatedChangeEventId { get; set; } = string.Empty;
    }

    public sealed class QsRisk
    {
        public string RiskId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string RiskNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Probability { get; set; }
        public int Impact { get; set; }
        public int RiskScore => Probability * Impact;
        public string Owner { get; set; } = string.Empty;
        public decimal CostExposure { get; set; }
        public int TimeExposureDays { get; set; }
        public string Status { get; set; } = "OPEN"; // OPEN, MITIGATED, OCCURRED, CLOSED
        public string ResponseStrategy { get; set; } = "MITIGATE";
        public long Version { get; set; } = 1;
    }

    public sealed class QsIssueRecord
    {
        public string IssueId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string IssueNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = "OPEN";
        public string RelatedRiskId { get; set; } = string.Empty;
        public string RelatedChangeEventId { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
    }
}

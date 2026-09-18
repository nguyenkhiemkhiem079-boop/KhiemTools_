using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Contracts
{
    public enum QsProgressStatus
    {
        NOT_STARTED,
        IN_PROGRESS,
        COMPLETE,
        ON_HOLD,
        NOT_APPLICABLE
    }

    public enum QsProgressMethod
    {
        ELEMENT_COMPLETE,
        ELEMENT_PERCENT,
        MEASURED_QUANTITY,
        MANUAL_QUANTITY,
        MODEL_STATUS
    }

    public sealed class QsContract
    {
        public string ContractId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ContractNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Currency { get; set; } = "VND";
        public decimal OriginalContractValue { get; set; }
        public decimal CurrentContractValue { get; set; }
        public decimal RetentionPercent { get; set; }
        public decimal RetentionLimitPercent { get; set; }
        public decimal AdvancePaymentAmount { get; set; }
        public decimal TaxPercent { get; set; }
        public string Status { get; set; } = "DRAFT"; // DRAFT, ACTIVE, COMPLETED, TERMINATED
        public List<QsContractBoqItem> BoqItems { get; set; } = new List<QsContractBoqItem>();
        public long Version { get; set; } = 1;
    }

    public sealed class QsContractBoqItem
    {
        public string ContractBoqItemId { get; set; } = Guid.NewGuid().ToString("N");
        public string ContractId { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WorkPackage { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double ContractQuantity { get; set; }
        public decimal UnitRate { get; set; }
        public decimal ContractAmount { get; set; }
        public double ApprovedVariationQuantity { get; set; }
        public decimal ApprovedVariationAmount { get; set; }
        public double CurrentContractQuantity => ContractQuantity + ApprovedVariationQuantity;
        public string Currency { get; set; } = "VND";
    }

    public sealed class QsProgressPeriod
    {
        public string PeriodId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ContractId { get; set; } = string.Empty;
        public int PeriodNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime ValuationDate { get; set; }
        public string Status { get; set; } = "OPEN"; // OPEN, SUBMITTED, CERTIFIED, CLOSED
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public long Version { get; set; } = 1;
    }

    public sealed class QsProgressMeasurement
    {
        public string MeasurementId { get; set; } = Guid.NewGuid().ToString("N");
        public string PeriodId { get; set; } = string.Empty;
        public string ContractBoqItemId { get; set; } = string.Empty;
        public string ElementUniqueId { get; set; } = string.Empty;
        public long CurrentElementId { get; set; }
        public double BaseQuantity { get; set; }
        public double MeasuredQuantity { get; set; }
        public double PreviousCumulativeQuantity { get; set; }
        public double CurrentPeriodQuantity { get; set; }
        public double CumulativeQuantity { get; set; }
        public double PercentComplete { get; set; }
        public QsProgressMethod MeasurementMethod { get; set; }
        public QsProgressStatus Status { get; set; }
        public string BoqCode { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class QsPaymentCertificate
    {
        public string CertificateId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ContractId { get; set; } = string.Empty;
        public string PeriodId { get; set; } = string.Empty;
        public string CertificateNumber { get; set; } = string.Empty;
        public decimal PreviousCertifiedAmount { get; set; }
        public decimal GrossCurrentPeriodAmount { get; set; }
        public decimal GrossCumulativeAmount { get; set; }
        public decimal RetentionThisPeriod { get; set; }
        public decimal AdvanceRecoveryThisPeriod { get; set; }
        public decimal OtherDeductions { get; set; }
        public decimal Tax { get; set; }
        public decimal NetCurrentPayment { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = "DRAFT"; // DRAFT, CERTIFIED, PAID, VOID
        public List<QsPaymentBoqLine> Lines { get; set; } = new List<QsPaymentBoqLine>();
        public long Version { get; set; } = 1;
    }

    public sealed class QsPaymentBoqLine
    {
        public string ContractBoqItemId { get; set; } = string.Empty;
        public double PreviousQuantity { get; set; }
        public double CurrentQuantity { get; set; }
        public double CumulativeQuantity { get; set; }
        public decimal PreviousValue { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal CumulativeValue { get; set; }
    }

    public sealed class QsVariationOrder
    {
        public string VariationId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string ContractId { get; set; } = string.Empty;
        public string VariationNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = "DRAFT"; // DRAFT, SUBMITTED, APPROVED, REJECTED
        public decimal SubmittedValue { get; set; }
        public decimal ApprovedValue { get; set; }
        public List<QsVariationItem> Items { get; set; } = new List<QsVariationItem>();
        public long Version { get; set; } = 1;
    }

    public sealed class QsVariationItem
    {
        public string VariationId { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public double QuantityDelta { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public decimal Amount => (decimal)QuantityDelta * Rate;
        public string Source { get; set; } = string.Empty;
        public List<string> RelatedUniqueIds { get; set; } = new List<string>();
    }
}

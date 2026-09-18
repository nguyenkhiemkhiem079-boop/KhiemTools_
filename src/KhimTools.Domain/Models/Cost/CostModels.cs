using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Cost
{
    public enum QsPricingStatus
    {
        PRICED,
        UNPRICED,
        UNIT_MISMATCH,
        RATE_MISSING,
        AMBIGUOUS_RATE,
        INVALID_QUANTITY,
        IGNORED
    }

    public enum QsAdjustmentType
    {
        Waste,
        Contingency,
        Escalation,
        Discount,
        Markup,
        Custom
    }

    public enum QsAdjustmentMethod
    {
        Percent,
        FixedAmount
    }

    public sealed class QsUnitRate
    {
        public string RateId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal BaseRate { get; set; }
        public string Currency { get; set; } = "VND";
        public string RateSource { get; set; } = string.Empty;
        public DateTime? EffectiveDate { get; set; }
        public string Region { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public decimal? LaborCost { get; set; }
        public decimal? MaterialCost { get; set; }
        public decimal? EquipmentCost { get; set; }
        public decimal? SubcontractCost { get; set; }
        public decimal? OtherCost { get; set; }
        public long Version { get; set; } = 1;
    }

    public sealed class QsCostAdjustment
    {
        public string AdjustmentId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public QsAdjustmentType Type { get; set; }
        public QsAdjustmentMethod Method { get; set; } = QsAdjustmentMethod.Percent;
        public decimal Value { get; set; }
        public int Priority { get; set; }
        public string AppliesTo { get; set; } = "Project";
        public bool Enabled { get; set; } = true;
    }

    public sealed class QsCostItem
    {
        public string BoqCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string QuantityUnit { get; set; } = string.Empty;
        public string RateId { get; set; } = string.Empty;
        public decimal UnitRate { get; set; }
        public string RateUnit { get; set; } = string.Empty;
        public decimal NormalizedQuantity { get; set; }
        public string Currency { get; set; } = "VND";
        public decimal DirectCost { get; set; }
        public decimal WasteCost { get; set; }
        public decimal AdjustmentCost { get; set; }
        public decimal FinalCost { get; set; }
        public string Level { get; set; } = string.Empty;
        public string WorkPackage { get; set; } = string.Empty;
        public QsPricingStatus PricingStatus { get; set; }
        public List<string> SourceQuantityIds { get; } = new List<string>();
    }

    public sealed class QsEstimate
    {
        public string EstimateId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Revision { get; set; } = "R0";
        public string Project { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Currency { get; set; } = "VND";
        public string QuantitySnapshotId { get; set; } = string.Empty;
        public string Status { get; set; } = "DRAFT"; // DRAFT, SUBMITTED, APPROVED, ARCHIVED
        public List<QsCostItem> Items { get; set; } = new List<QsCostItem>();
        public decimal DirectCost { get; set; }
        public decimal Adjustments { get; set; }
        public decimal FinalCost { get; set; }
        public long Version { get; set; } = 1;
    }

    public sealed class QsCostDelta
    {
        public string BoqCode { get; set; } = string.Empty;
        public decimal QuantityEffect { get; set; }
        public decimal RateEffect { get; set; }
        public decimal InteractionEffect { get; set; }
        public decimal TotalDelta => QuantityEffect + RateEffect + InteractionEffect;
    }
}

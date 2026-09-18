using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Procurement
{
    public sealed class QsProcurementPackage
    {
        public string PackageId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string PackageCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProcurementType { get; set; } = "MATERIAL"; // MATERIAL, SUBCONTRACT, EQUIPMENT
        public string WorkPackage { get; set; } = string.Empty;
        public string WbsId { get; set; } = string.Empty;
        public DateTime? RequiredOnSiteDate { get; set; }
        public DateTime? RFQPlannedDate { get; set; }
        public DateTime? AwardPlannedDate { get; set; }
        public DateTime? DeliveryPlannedDate { get; set; }
        public string Status { get; set; } = "DRAFT"; // DRAFT, RFQ_ISSUED, EVALUATING, AWARDED, DELIVERED, CLOSED
        public string Currency { get; set; } = "VND";
        public decimal BudgetAmount { get; set; }
        public List<string> BoqCodes { get; set; } = new List<string>();
        public long Version { get; set; } = 1;
    }

    public sealed class QsMaterialRequirement
    {
        public string RequirementId { get; set; } = Guid.NewGuid().ToString("N");
        public string PackageId { get; set; } = string.Empty;
        public string MaterialCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double RequiredQuantity { get; set; }
        public double WastePercent { get; set; }
        public double OrderedQuantity { get; set; }
        public double DeliveredQuantity { get; set; }
        public double AcceptedQuantity { get; set; }
        public double RejectedQuantity { get; set; }
        public double RemainingQuantity => Math.Max(0, RequiredQuantity * (1 + WastePercent / 100) - AcceptedQuantity);
        public DateTime? RequiredDate { get; set; }
        public string BoqCode { get; set; } = string.Empty;
        public string MeasurementCode { get; set; } = string.Empty;
        public string SourceType { get; set; } = "MODEL_QUANTITY";
        public List<string> SourceQuantityIds { get; set; } = new List<string>();
    }

    public sealed class QsVendor
    {
        public string VendorId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string VendorType { get; set; } = "SUPPLIER";
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = "ACTIVE";
    }

    public sealed class QsRfq
    {
        public string RfqId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string RfqNumber { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = "DRAFT";
        public List<string> VendorIds { get; set; } = new List<string>();
        public long Version { get; set; } = 1;
    }

    public sealed class QsVendorQuotation
    {
        public string QuotationId { get; set; } = Guid.NewGuid().ToString("N");
        public string RfqId { get; set; } = string.Empty;
        public string VendorId { get; set; } = string.Empty;
        public string QuotationNumber { get; set; } = string.Empty;
        public decimal TotalQuotedAmount { get; set; }
        public decimal FinalQuotedAmount { get; set; }
        public int DeliveryDays { get; set; }
        public string Currency { get; set; } = "VND";
        public string ComplianceStatus { get; set; } = "NOT_REVIEWED";
        public bool IsAwarded { get; set; }
        public List<QsQuotationLine> Lines { get; set; } = new List<QsQuotationLine>();
    }

    public sealed class QsQuotationLine
    {
        public string MaterialCode { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitRate { get; set; }
        public decimal Amount => (decimal)Quantity * UnitRate;
    }

    public sealed class QsPurchaseOrder
    {
        public string PoId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string PoNumber { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string VendorId { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public string Currency { get; set; } = "VND";
        public decimal OriginalAmount { get; set; }
        public decimal VariationAmount { get; set; }
        public decimal CurrentAmount => OriginalAmount + VariationAmount;
        public string Status { get; set; } = "DRAFT"; // DRAFT, ISSUED, PARTIALLY_DELIVERED, FULLY_DELIVERED, CANCELLED
        public List<QsPurchaseOrderLine> Lines { get; set; } = new List<QsPurchaseOrderLine>();
        public long Version { get; set; } = 1;
    }

    public sealed class QsPurchaseOrderLine
    {
        public string PoLineId { get; set; } = Guid.NewGuid().ToString("N");
        public string MaterialCode { get; set; } = string.Empty;
        public string BoqCode { get; set; } = string.Empty;
        public double OrderedQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitRate { get; set; }
        public decimal Amount => (decimal)OrderedQuantity * UnitRate;
    }

    public sealed class QsDelivery
    {
        public string DeliveryId { get; set; } = Guid.NewGuid().ToString("N");
        public string PoId { get; set; } = string.Empty;
        public DateTime DeliveryDate { get; set; }
        public string Status { get; set; } = "EXPECTED"; // EXPECTED, DELIVERED, ACCEPTED, REJECTED
        public List<QsDeliveryLine> Lines { get; set; } = new List<QsDeliveryLine>();
    }

    public sealed class QsDeliveryLine
    {
        public string PoLineId { get; set; } = string.Empty;
        public double DeliveredQuantity { get; set; }
        public double AcceptedQuantity { get; set; }
        public double RejectedQuantity { get; set; }
        public string InspectionStatus { get; set; } = "NOT_REVIEWED";
    }
}

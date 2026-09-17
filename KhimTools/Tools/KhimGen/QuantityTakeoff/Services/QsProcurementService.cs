using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsProcurementService
    {
        public static double ProcurementQuantity(double modelQuantity, double wastePercent) => modelQuantity * (1 + wastePercent / 100.0);
        public static DateTime LatestOrderDate(DateTime requiredOnSite, int leadTimeDays, int approvalDays = 0, int siteBufferDays = 0) => requiredOnSite.AddDays(-(leadTimeDays + approvalDays + siteBufferDays));
        public static decimal CommittedCost(IEnumerable<QsPurchaseOrder> orders) => (orders ?? Enumerable.Empty<QsPurchaseOrder>()).Where(x => x.Status != "CANCELLED").Sum(x => x.CurrentAmount);
        public static double Shortage(QsMaterialRequirement requirement) => Math.Max(0, requirement.RequiredQuantity * (1 + requirement.WastePercent / 100) - requirement.AcceptedQuantity);
        public static List<string> Validate(QsMaterialRequirement requirement) { var issues = new List<string>(); if (requirement.DeliveredQuantity > requirement.OrderedQuantity + 1e-9) issues.Add("PROC009"); if (requirement.AcceptedQuantity + requirement.RejectedQuantity > requirement.DeliveredQuantity + 1e-9) issues.Add("PROC010"); if (Shortage(requirement) > 1e-9) issues.Add("PROC012"); return issues; }
        public static decimal BudgetVariance(decimal budget, decimal committed) => budget - committed;
    }
}

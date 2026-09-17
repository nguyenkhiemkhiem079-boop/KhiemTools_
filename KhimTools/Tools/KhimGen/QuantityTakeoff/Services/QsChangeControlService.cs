using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsChangeControlService
    {
        public static QsChangeCostImpact CostImpact(QsChangeEvent change, QsChangeQuantityImpact quantity, decimal contractRate, decimal indirect = 0) => new QsChangeCostImpact { ChangeEventId = change?.ChangeEventId ?? "", BoqCode = quantity?.BoqCode ?? "", QuantityDelta = quantity?.QuantityDelta ?? 0, Rate = contractRate, DirectCost = (decimal)(quantity?.QuantityDelta ?? 0) * contractRate, IndirectCost = indirect, TotalCost = (decimal)(quantity?.QuantityDelta ?? 0) * contractRate + indirect };
        public static bool CanUpdateContract(string status) => string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);
        public static decimal ExpectedMonetaryValue(int probabilityPercent, decimal costImpact) => costImpact * probabilityPercent / 100m;
        public static List<string> Validate(QsChangeEvent change, QsChangeImpactAssessment assessment) { var issues = new List<string>(); if (change == null || string.IsNullOrWhiteSpace(change.SourceReference)) issues.Add("CHG001"); if (assessment == null) issues.Add("CHG002"); return issues; }
    }
}

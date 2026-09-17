using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsEarnedQuantityService
    {
        public static void Calculate(QsProgressMeasurement m, double previousApproved, double currentCumulative)
        { m.PreviousCumulativeQuantity = previousApproved; m.CumulativeQuantity = currentCumulative; m.CurrentPeriodQuantity = currentCumulative - previousApproved; m.PercentComplete = m.BaseQuantity <= 0 ? 0 : currentCumulative / m.BaseQuantity * 100.0; }
        public static List<string> Validate(QsProgressMeasurement m, double contractQuantity) { var issues = new List<string>(); if (m.CumulativeQuantity > contractQuantity + 1e-9) issues.Add("OVER_CONTRACT_QUANTITY"); if (m.CurrentPeriodQuantity < -1e-9) issues.Add("PROGRESS_REVERSAL"); if (m.PercentComplete > 100.000001) issues.Add("PROGRESS_GT_100"); if (m.PercentComplete < -1e-9) issues.Add("PROGRESS_LT_0"); return issues; }
        public static decimal EarnedValue(double quantity, decimal contractRate) => (decimal)quantity * contractRate;
    }
    public static class QsPaymentService
    {
        public static QsPaymentCertificate Calculate(QsContract contract, QsProgressPeriod period, IEnumerable<QsPaymentBoqLine> lines, decimal advanceOutstanding, decimal otherDeductions = 0)
        { var cert = new QsPaymentCertificate { ContractId = contract.ContractId, PeriodId = period.PeriodId, Currency = contract.Currency, Lines = lines?.ToList() ?? new List<QsPaymentBoqLine>() }; cert.PreviousCertifiedAmount = cert.Lines.Sum(x => x.PreviousValue); cert.GrossCurrentPeriodAmount = cert.Lines.Sum(x => x.CurrentValue); cert.GrossCumulativeAmount = cert.Lines.Sum(x => x.CumulativeValue); cert.RetentionThisPeriod = Math.Min(cert.GrossCurrentPeriodAmount * contract.RetentionPercent / 100m, Math.Max(0, contract.CurrentContractValue * contract.RetentionLimitPercent / 100m - cert.PreviousCertifiedAmount * contract.RetentionPercent / 100m)); cert.AdvanceRecoveryThisPeriod = Math.Min(advanceOutstanding, cert.GrossCurrentPeriodAmount); cert.OtherDeductions = otherDeductions; cert.Tax = (cert.GrossCurrentPeriodAmount - cert.RetentionThisPeriod - cert.AdvanceRecoveryThisPeriod - cert.OtherDeductions) * contract.TaxPercent / 100m; cert.NetCurrentPayment = cert.GrossCurrentPeriodAmount - cert.RetentionThisPeriod - cert.AdvanceRecoveryThisPeriod - cert.OtherDeductions + cert.Tax; return cert; }
    }
}

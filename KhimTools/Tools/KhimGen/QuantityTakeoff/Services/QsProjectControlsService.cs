using System;
using System.Collections.Generic;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsProjectControlsService
    {
        public static decimal ForecastFinalCost(QsCostForecast f) { if (f == null) return 0; f.CurrentBudget = f.Budget + f.ApprovedChanges; f.ForecastFinalCost = f.CommittedCost + f.UncommittedForecast + f.PendingChangeExposure + f.RiskAllowance; return f.ForecastFinalCost; }
        public static string Trend(decimal current, decimal? previous) => !previous.HasValue ? "N_A" : current > previous.Value ? "IMPROVING" : current < previous.Value ? "DETERIORATING" : "STABLE";
        public static QsProjectHealth Health(string cost, string schedule, string procurement, string commercial, string risk, string data) { var h = new QsProjectHealth { CostHealth = cost, ScheduleHealth = schedule, ProcurementHealth = procurement, CommercialHealth = commercial, RiskHealth = risk, DataHealth = data }; if (schedule == "CRITICAL") { h.OverallState = "AT_RISK"; h.Reason = "Schedule is CRITICAL"; } else if (risk == "CRITICAL" || cost == "CRITICAL") { h.OverallState = "AT_RISK"; h.Reason = "Critical risk or cost condition"; } else h.OverallState = "GOOD"; return h; }
        public static List<string> ClosePeriod(QsReportingPeriod period, IEnumerable<string> unresolvedCriticalIssues, string closedBy) { var issues = new List<string>(unresolvedCriticalIssues ?? Array.Empty<string>()); if (issues.Count > 0) return issues; period.Status = "CLOSED"; period.ClosedAt = DateTime.UtcNow; period.ClosedBy = closedBy ?? ""; return issues; }
    }
}

using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Projects
{
    public static class KqsPermissions
    {
        public const string ProjectRead = "PROJECT_READ";
        public const string ModelScan = "MODEL_SCAN";
        public const string BoqEdit = "BOQ_EDIT";
        public const string RateEdit = "RATE_EDIT";
        public const string EstimateApprove = "ESTIMATE_APPROVE";
        public const string ContractEdit = "CONTRACT_EDIT";
        public const string ProgressApprove = "PROGRESS_APPROVE";
        public const string IpcCertify = "IPC_CERTIFY";
        public const string ChangeApprove = "CHANGE_APPROVE";
        public const string RiskEdit = "RISK_EDIT";
        public const string ProcurementEdit = "PROCUREMENT_EDIT";
        public const string ScheduleEdit = "SCHEDULE_EDIT";
        public const string PeriodClose = "PERIOD_CLOSE";
        public const string ProjectAdmin = "PROJECT_ADMIN";

        public static readonly IReadOnlyList<string> All = new[]
        {
            ProjectRead, ModelScan, BoqEdit, RateEdit, EstimateApprove,
            ContractEdit, ProgressApprove, IpcCertify, ChangeApprove,
            RiskEdit, ProcurementEdit, ScheduleEdit, PeriodClose, ProjectAdmin
        };
    }

    public static class KqsRoles
    {
        public const string Admin = "ADMIN";
        public const string ProjectManager = "PROJECT_MANAGER";
        public const string QuantitySurveyor = "QUANTITY_SURVEYOR";
        public const string CommercialLead = "COMMERCIAL_LEAD";
        public const string SiteEngineer = "SITE_ENGINEER";
        public const string Viewer = "VIEWER";
    }

    public sealed class KqsProject
    {
        public string ProjectId { get; set; } = Guid.NewGuid().ToString("N");
        public string OrganizationId { get; set; } = string.Empty;
        public string ProjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Currency { get; set; } = "VND";
        public long Version { get; set; } = 1;
        public bool IsArchived { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class KqsProjectMember
    {
        public string MembershipId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string RoleId { get; set; } = KqsRoles.Viewer;
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, INACTIVE
        public List<string> GrantedPermissions { get; set; } = new List<string>();
        public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class KqsUser
    {
        public string UserId { get; set; } = Guid.NewGuid().ToString("N");
        public string OrganizationId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GlobalRole { get; set; } = KqsRoles.Viewer;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.TitleBlockSync.Models
{
    public sealed class TitleBlockSyncPlan : IWorkflowPlan
    {
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string PlanVersion { get; set; } = "3.3";
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public string SourceSheetUniqueId { get; set; } = string.Empty;
        public string SourceRevisionFingerprint { get; set; } = string.Empty;
        public ElementId SourceTitleBlockId { get; set; } = ElementId.InvalidElementId;
        public ElementId SourceTitleBlockTypeId { get; set; } = ElementId.InvalidElementId;
        public TitleBlockSyncOptions Options { get; set; } = new TitleBlockSyncOptions();
        public List<ParameterSyncItem> Parameters { get; } = new List<ParameterSyncItem>();
        public List<TitleBlockTargetPlan> Targets { get; } = new List<TitleBlockTargetPlan>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> BlockedConditions { get; } = new List<string>();
        public string Fingerprint { get; set; } = string.Empty;
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public bool IsStale { get; set; }
        public TitleBlockSyncStatusCode Status { get; set; } = TitleBlockSyncStatusCode.READY;
        public string Message { get; set; } = string.Empty;
    }
    public sealed class TitleBlockTargetPlan
    {
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public string TargetSheetUniqueId { get; set; } = string.Empty;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public ElementId TargetTitleBlockId { get; set; } = ElementId.InvalidElementId;
        public ElementId OldTypeId { get; set; } = ElementId.InvalidElementId;
        public ElementId NewTypeId { get; set; } = ElementId.InvalidElementId;
        public bool TypeChange { get; set; }
        public List<ParameterSyncTargetPlan> InstanceParameters { get; } = new List<ParameterSyncTargetPlan>();
        public List<ParameterSyncTargetPlan> SheetParameters { get; } = new List<ParameterSyncTargetPlan>();
        public TitleBlockSyncStatusCode Status { get; set; } = TitleBlockSyncStatusCode.READY;
        public string Message { get; set; } = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.TitleBlockSync.Models
{
    public sealed class ParameterSyncResult
    {
        public ParameterKey ParameterKey { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public TitleBlockSyncScope Scope { get; set; }
        public ParameterValueSnapshot OldValue { get; set; }
        public ParameterValueSnapshot NewValue { get; set; }
        public TitleBlockSyncStatusCode Status { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    public sealed class TitleBlockSyncExecutionResult
    {
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public ElementId OldTypeId { get; set; } = ElementId.InvalidElementId;
        public ElementId NewTypeId { get; set; } = ElementId.InvalidElementId;
        public bool TypeChanged { get; set; }
        public TitleBlockSyncStatusCode Status { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ParameterSyncResult> ParameterResults { get; } = new List<ParameterSyncResult>();
        public List<string> Messages { get; } = new List<string>();
    }
    public sealed class TitleBlockSyncBatchResult
    {
        public int Requested { get; set; }
        public int Synced { get; set; }
        public int NoChange { get; set; }
        public int Partial { get; set; }
        public int Blocked { get; set; }
        public int Failed { get; set; }
        public int TypeChanges { get; set; }
        public List<TitleBlockSyncExecutionResult> Results { get; } = new List<TitleBlockSyncExecutionResult>();
    }
}

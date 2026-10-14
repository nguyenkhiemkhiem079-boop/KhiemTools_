using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.TitleBlockSync.Models
{
    public enum TitleBlockSyncStatusCode
    {
        READY, NO_CHANGE, SOURCE_SHEET_MISSING, SOURCE_TITLEBLOCK_MISSING, SOURCE_MULTIPLE_TITLEBLOCKS,
        TARGET_SHEET_MISSING, TARGET_TITLEBLOCK_MISSING, TARGET_MULTIPLE_TITLEBLOCKS, SOURCE_TYPE_MISSING,
        TARGET_TYPE_CHANGE_UNSUPPORTED, PARAMETER_MISSING, PARAMETER_READ_ONLY, PARAMETER_TYPE_MISMATCH,
        PARAMETER_DATA_TYPE_MISMATCH, PARAMETER_PROTECTED, REVISION_MANAGED_PARAMETER, GLOBAL_PROJECT_PARAMETER,
        UNSAFE_ELEMENT_REFERENCE, BLANK_SOURCE_SKIPPED, STALE_SYNC_PLAN, SYNCED, PARTIAL, SKIPPED, FAILED, POST_VERIFY_FAILED
    }
    public enum TitleBlockSyncScope { TITLE_BLOCK_TYPE, TITLE_BLOCK_INSTANCE, SHEET }
    public enum TitleBlockSyncSeverity { INFO, WARNING, ERROR }
    public sealed class TitleBlockSyncPreflightResult
    {
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public ElementId TargetTitleBlockId { get; set; } = ElementId.InvalidElementId;
        public TitleBlockSyncScope Scope { get; set; }
        public ParameterKey ParameterKey { get; set; }
        public TitleBlockSyncStatusCode Status { get; set; }
        public TitleBlockSyncSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool CanExecute { get; set; }
    }
}

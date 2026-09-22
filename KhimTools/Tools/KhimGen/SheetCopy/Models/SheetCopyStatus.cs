using System;

namespace KhimTools.SheetCopy.Models
{
    public enum SheetCopyStatusCode
    {
        READY, NO_CHANGE, SOURCE_SHEET_MISSING, EMPTY_TARGET_NUMBER, INVALID_TARGET_NUMBER,
        DUPLICATE_IN_BATCH, DUPLICATE_IN_PROJECT, TITLE_BLOCK_MISSING, MULTIPLE_TITLE_BLOCKS,
        VIEW_MISSING, VIEW_DUPLICATION_UNSUPPORTED, VIEW_NAME_CONFLICT, VIEW_PLACEMENT_UNSUPPORTED,
        LEGEND_PLACEMENT_UNSUPPORTED, SCHEDULE_MISSING, SEGMENTED_SCHEDULE_DEFERRED,
        SYSTEM_REVISION_SCHEDULE, PARAMETER_READ_ONLY, PARAMETER_TYPE_MISMATCH,
        REVISION_CONTENT_SKIPPED, UNSUPPORTED_SHEET_CONTENT, STALE_COPY_PLAN, CREATED,
        PARTIAL, SKIPPED, FAILED, POST_VERIFY_FAILED, COPIED, SKIPPED_PROTECTED, MISSING_TARGET_PARAMETER,
        TYPE_MISMATCH, READ_ONLY
    }

    public enum SheetCopyContentKind
    {
        NORMAL_VIEWPORT, LEGEND_VIEWPORT, SCHEDULE, TITLE_BLOCK, SHEET_ANNOTATION,
        REVISION_RELATED, UNSUPPORTED
    }

    public enum ViewCopyPolicy
    {
        SHEET_ONLY,
        DUPLICATE_VIEWS,
        DUPLICATE_VIEWS_WITH_DETAILING,
        DUPLICATE_AS_DEPENDENT
    }

    public enum SheetCopyAction
    {
        CREATE, CHANGE_TYPE, MOVE_ALIGN, LEADER_UPDATE, REUSE, COPY, SKIP
    }

    public enum SheetCopySeverity { INFO, WARNING, ERROR }

    public sealed class SheetCopyDiagnostic
    {
        public SheetCopyStatusCode StatusCode { get; set; }
        public SheetCopySeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool CanExecute { get; set; }
    }
}

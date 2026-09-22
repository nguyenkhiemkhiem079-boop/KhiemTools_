using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.SheetExport.Models
{
    public enum ExportItemStatus
    {
        READY,
        SUCCESS,
        NO_CHANGE,
        LOCKED,
        FAILED_EXPORT,
        OUTPUT_MISSING,
        OUTPUT_EMPTY,
        POST_PROCESS_FAILED,
        PUBLISH_FAILED,
        SKIPPED,
        PARTIAL,
        CANCELLED,
        TEMP_VIEW_MODE_BLOCKED,
        AUXILIARY_REPORT_FAILED
    }

    public enum ExportPreflightSeverity
    {
        INFO,
        WARNING,
        BLOCKED
    }

    public enum ExportPreflightCode
    {
        NO_SELECTION,
        INVALID_OUTPUT_DIRECTORY,
        OUTPUT_NOT_WRITABLE,
        INVALID_FILE_NAME,
        DUPLICATE_OUTPUT,
        PATH_TOO_LONG,
        LOCKED_OUTPUT,
        LOCKED_EXISTING_PDF,
        LOCKED_EXISTING_DWG,
        LOCKED_COMBINED_PDF,
        UNKNOWN_PAPER_SIZE,
        MULTIPLE_TITLE_BLOCKS,
        INVALID_NAMING_REGEX,
        UNKNOWN_NAMING_TOKEN,
        MISSING_DWG_SETUP,
        INVALID_DWG_VERSION,
        DWG_SETUP_READY,
        DWG_SETUP_MISSING,
        DWG_SETUP_INVALID,
        DWG_SETUP_FALLBACK,
        TEMP_VIEW_MODE_BLOCKED,
        COMBINE_MIXED_PAPER_SIZE,
        COMBINE_MIXED_ORIENTATION,
        MISSING_SHEET,
        READY
    }

    public sealed class ExportPreflightItem
    {
        public ExportPreflightCode Code { get; set; }
        public ExportPreflightSeverity Severity { get; set; }
        public string SheetUniqueId { get; set; } = "";
        public ElementId SheetId { get; set; }
        public ExportFormat Format { get; set; }
        public string Path { get; set; } = "";
        public string Message { get; set; } = "";
        public bool CanExecute { get; set; }
    }

    public sealed class ExportJobOptions : ExportOptions
    {
        public static ExportJobOptions Normalize(ExportOptions source)
        {
            source = source ?? new ExportOptions();
            return new ExportJobOptions
            {
                ExportPdf = source.ExportPdf,
                ExportDwg = source.ExportDwg,
                DwgExportSetupName = source.DwgExportSetupName,
                DwgMergedViews = source.DwgMergedViews,
                DwgTargetVersion = source.DwgTargetVersion,
                OutputDirectory = source.OutputDirectory == null ? "" : source.OutputDirectory.Trim(),
                ProjectCode = string.IsNullOrWhiteSpace(source.ProjectCode) ? "PROJ" : source.ProjectCode.Trim(),
                SelectedNamingTemplateName = source.SelectedNamingTemplateName,
                NamingExpression = source.NamingExpression,
                NamingRegexPattern = source.NamingRegexPattern,
                SplitFoldersByFormat = source.SplitFoldersByFormat,
                PrintTimeoutSeconds = source.PrintTimeoutSeconds,
                IssueSetName = string.IsNullOrWhiteSpace(source.IssueSetName) ? "General Issue" : source.IssueSetName.Trim(),
                IssueDate = source.IssueDate == default(DateTime) ? DateTime.Today : source.IssueDate.Date,
                PreservePreviousExports = source.PreservePreviousExports,
                PreviousExportsFolderName = source.PreviousExportsFolderName,
                CombinePdf = source.CombinePdf,
                CombinedPdfFileName = source.CombinedPdfFileName,
                UseNamingConvention = source.UseNamingConvention,
                NamingField1 = source.NamingField1,
                NamingField2 = source.NamingField2,
                NamingField3 = source.NamingField3,
                AddBookmarks = source.AddBookmarks,
                AutoCoverPage = source.AutoCoverPage,
                ApplyWatermark = source.ApplyWatermark,
                WatermarkText = source.WatermarkText,
                PaperPlacementCenter = source.PaperPlacementCenter,
                PaperPlacementOffset = source.PaperPlacementOffset,
                MarginNoMargin = source.MarginNoMargin,
                MarginOffsetX = source.MarginOffsetX,
                MarginOffsetY = source.MarginOffsetY,
                ZoomFitToPage = source.ZoomFitToPage,
                ZoomPercentage = source.ZoomPercentage,
                VectorProcessing = source.VectorProcessing,
                PdfExportDpi = source.PdfExportDpi,
                RasterQuality = source.RasterQuality,
                ColorMode = source.ColorMode,
                ViewLinksInBlue = source.ViewLinksInBlue,
                HideRefPlanes = source.HideRefPlanes,
                HideUnreferencedViewTags = source.HideUnreferencedViewTags,
                HideScopeBoxes = source.HideScopeBoxes,
                HideCropBoundaries = source.HideCropBoundaries,
                ReplaceHalftoneWithThinLines = source.ReplaceHalftoneWithThinLines,
                MaskCoincidentLines = source.MaskCoincidentLines,
                GenerateTransmittal = source.GenerateTransmittal,
                GenerateQaReport = source.GenerateQaReport,
                MaxRetryCount = Math.Max(0, source.MaxRetryCount),
                WarnPageSizeMismatch = source.WarnPageSizeMismatch,
                AutoDisableTemporaryViewProperties = source.AutoDisableTemporaryViewProperties,
                PreserveStagingOnFailure = source.PreserveStagingOnFailure,
                MaxPathLength = source.MaxPathLength <= 0 ? 240 : source.MaxPathLength
            };
        }
    }

    public sealed class ExportItemResult
    {
        public string SheetId { get; set; } = "";
        public string SheetUniqueId { get; set; } = "";
        public string SheetNumber { get; set; } = "";
        public string RevisionNumber { get; set; } = "";
        public string RevisionDate { get; set; } = "";
        public ExportFormat Format { get; set; }
        public string StagedPath { get; set; } = "";
        public string FinalPath { get; set; } = "";
        public ExportItemStatus Status { get; set; }
        public int RetryCount { get; set; }
        public double DurationSeconds { get; set; }
        public long FileSizeBytes { get; set; }
        public string Message { get; set; } = "";
        public bool Success { get { return Status == ExportItemStatus.SUCCESS; } }
    }

    /// <summary>UI-neutral progress payload emitted at safe export boundaries.</summary>
    public sealed class ExportProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string SheetNumber { get; set; } = "";
        public ExportFormat Format { get; set; }
        public string Stage { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public sealed class ExportBatchResult
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString("N");
        public int RequestedSheets { get; set; }
        public int RequestedOutputs { get; set; }
        public int SuccessfulOutputs { get; set; }
        public int PartialSheets { get; set; }
        public int LockedOutputs { get; set; }
        public int FailedOutputs { get; set; }
        public int SkippedOutputs { get; set; }
        public bool Cancelled { get; set; }
        public bool AuxiliaryReportFailed { get; set; }
        public string AuxiliaryReportMessage { get; set; } = "";
        public List<ExportPreflightItem> Preflight { get; } = new List<ExportPreflightItem>();
        public List<ExportItemResult> Results { get; } = new List<ExportItemResult>();
        public List<ExportItemResult> SuccessfulResults { get { return Results.FindAll(item => item.Success); } }
    }

    public sealed class TemporaryViewStateSnapshot
    {
        public ElementId ViewId { get; set; }
        public bool WasTemporaryViewPropertiesEnabled { get; set; }
        public ElementId TemporaryViewPropertiesId { get; set; }
        public bool WasChangedByKTools { get; set; }
    }

    public sealed class SheetIssueFingerprint
    {
        public string SheetUniqueId { get; set; } = "";
        public string SheetNumber { get; set; } = "";
        public string SheetName { get; set; } = "";
        public string RevisionNumber { get; set; } = "";
        public string RevisionDate { get; set; } = "";

        public string Value
        {
            get { return string.Join("|", SheetUniqueId, SheetNumber, SheetName, RevisionNumber, RevisionDate); }
        }
    }
}

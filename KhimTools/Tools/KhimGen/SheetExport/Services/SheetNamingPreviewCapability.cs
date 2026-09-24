using System;
using KhimTools.Core.Automation;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    /// <summary>Typed internal automation adapter over the same NamingPlanService used by the Sheet Export UI/workflow.</summary>
    public sealed class SheetNamingPreviewCapability : IAutomationCapabilityHandler
    {
        public const string Id = "sheet-export.naming-preview";
        public AutomationCapabilityMetadata Metadata { get; } = new AutomationCapabilityMetadata(
            Id, "Sheet Export", "Preview output filename", "Expands and validates a sheet export name without changing the document or filesystem.",
            true, false, false, false, false, true, true, AutomationOperationClass.PreviewOnly, nameof(SheetNamingPreviewRequest), nameof(AutomationResult));

        public AutomationResult Execute(AutomationRequest request)
        {
            var input = request as SheetNamingPreviewRequest;
            if (input == null) return Rejected("A SheetNamingPreviewRequest is required.");
            if (input.UnitSystem != AutomationUnitSystem.NotApplicable) return Rejected("UnitSystem must be explicitly NotApplicable for filename preview.");
            if (string.IsNullOrWhiteSpace(input.SheetNumber)) return Rejected("SheetNumber is required.");
            if (input.SheetName == null) return Rejected("SheetName is required; use an empty string when the sheet has no name.");
            if (ExceedsLimit(input.SheetNumber) || ExceedsLimit(input.SheetName) || ExceedsLimit(input.Revision) ||
                ExceedsLimit(input.RevisionDate) || ExceedsLimit(input.PaperSize) || ExceedsLimit(input.Orientation) ||
                ExceedsLimit(input.ProjectCode) || ExceedsLimit(input.Expression) || ExceedsLimit(input.RegexPattern))
                return Rejected("Text inputs are limited to 256 characters.");

            var item = new SheetExportItem
            {
                SheetNumber = input.SheetNumber,
                SheetName = input.SheetName,
                CurrentRevisionNumber = input.Revision ?? "",
                CurrentRevisionDate = input.RevisionDate ?? "",
                PaperSize = input.PaperSize ?? "Unknown",
                Orientation = input.Orientation ?? "Unknown"
            };
            var options = new ExportOptions { ProjectCode = input.ProjectCode ?? "", IssueDate = input.IssueDate ?? DateTime.Today };
            var template = new NamingTemplate { Expression = input.Expression, RegexPattern = input.RegexPattern ?? "" };
            try
            {
                string output = NamingPlanService.Expand(item, template, ExportJobOptions.Normalize(options));
                var result = new AutomationResult
                {
                    Status = AutomationStatus.Succeeded, Summary = "Filename preview is valid.", Requested = 1,
                    Affected = 0, Skipped = 0, Failed = 0, Postcondition = !string.IsNullOrWhiteSpace(output), OutputName = output
                };
                if (!result.Postcondition)
                {
                    result.Status = AutomationStatus.Failed;
                    result.Failed = 1;
                    result.Summary = "Filename preview did not produce a valid output name.";
                }
                return result;
            }
            catch (NamingPlanException ex) { return Rejected(ex.Message); }
        }

        private static AutomationResult Rejected(string diagnostic)
        {
            var result = new AutomationResult { Status = AutomationStatus.Rejected, Summary = diagnostic, Requested = 1, Failed = 1, Postcondition = false };
            result.Diagnostics.Add(diagnostic);
            return result;
        }

        private static bool ExceedsLimit(string value) { return value != null && value.Length > 256; }
    }
}

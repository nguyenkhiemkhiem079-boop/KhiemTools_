using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace KhimTools.SheetGen.Models
{
    public enum SheetContentKind { None, NormalView, Legend, Schedule, Unsupported }
    public enum SheetValidationSeverity { None, Warning, Error }
    public enum SheetGenStatusCode
    {
        Ready, NoView, InvalidTitleBlockType, EmptySheetNumber, InvalidSheetNumber, InvalidSheetName, DuplicateInBatch, DuplicateInProject,
        EmptySheetName, MissingTitleBlock, TitleBlockNotFound, TitleBlockAmbiguous, ViewNotFound,
        ViewAmbiguous, ViewAlreadyPlaced, UnsupportedView, InvalidView, ParameterNotFound,
        ParameterReadOnly, Created, Skipped, Failed, ImportMalformed, Warning
    }

    public static class SheetGenStatusCodes
    {
        public static string ToDisplayCode(SheetGenStatusCode code)
        {
            return Regex.Replace(code.ToString(), "(?<!^)([A-Z])", "_$1").ToUpperInvariant();
        }

        public static bool TryParse(string text, out SheetGenStatusCode code)
        {
            if (Enum.TryParse(text, true, out code)) return true;
            foreach (SheetGenStatusCode candidate in Enum.GetValues(typeof(SheetGenStatusCode)))
            {
                if (string.Equals(ToDisplayCode(candidate), text ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    code = candidate;
                    return true;
                }
            }
            code = SheetGenStatusCode.Ready;
            return false;
        }
    }

    public sealed class SheetGenValidationResult
    {
        public int RowIndex { get; set; }
        public string SheetNumber { get; set; } = "";
        public SheetValidationSeverity Severity { get; set; }
        public SheetGenStatusCode Code { get; set; }
        public string Message { get; set; } = "";
        public bool CanCreate { get; set; }
        public string SuggestedAction { get; set; } = "";
        public override string ToString() => string.IsNullOrWhiteSpace(Message) ? Code.ToString() : $"{Code}: {Message}";
    }

    public sealed class SheetCreationResult
    {
        public int RowIndex { get; set; }
        public string SheetNumber { get; set; } = "";
        public string SheetName { get; set; } = "";
        public SheetGenStatusCode Status { get; set; }
        public SheetValidationSeverity Severity { get; set; }
        public ElementId CreatedSheetId { get; set; } = ElementId.InvalidElementId;
        public ElementId PlacedContentId { get; set; } = ElementId.InvalidElementId;
        public ElementId ContentInstanceId { get; set; } = ElementId.InvalidElementId;
        public TimeSpan Duration { get; set; }
        public List<string> Messages { get; } = new List<string>();
        public string StatusText => Messages.Count == 0 ? Status.ToString() : string.Join(" ", Messages);
    }

    public sealed class SheetBatchCreationResult
    {
        public int Requested { get; set; }
        public int Valid { get; set; }
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<SheetCreationResult> Results { get; } = new List<SheetCreationResult>();
        public IEnumerable<SheetCreationResult> Errors => Results.FindAll(r => r.Status == SheetGenStatusCode.Failed);
    }

    public sealed class SheetGenImportDiagnostic
    {
        public int RowIndex { get; set; }
        public SheetValidationSeverity Severity { get; set; }
        public SheetGenStatusCode Code { get; set; }
        public string Message { get; set; } = "";
        public override string ToString() => $"Row {RowIndex}: {Code} - {Message}";
    }

    public sealed class SheetGenImportResult
    {
        public List<SheetGenItem> Items { get; } = new List<SheetGenItem>();
        public List<SheetGenImportDiagnostic> Diagnostics { get; } = new List<SheetGenImportDiagnostic>();
        public bool HasErrors => Diagnostics.Exists(d => d.Severity == SheetValidationSeverity.Error);
    }

    public sealed class SheetGenParameterMapping
    {
        public string DisciplineParameterName { get; set; } = "Discipline";
        public bool WriteMissingParameterWarnings { get; set; } = true;
    }

    public class SheetGenItem
    {
        public bool IsSelected { get; set; } = true;
        public string SheetNumber { get; set; } = "";
        public string SheetName { get; set; } = "";
        public ElementId TitleBlockId { get; set; } = ElementId.InvalidElementId;
        public string TitleBlockName { get; set; } = "";
        public string TitleBlockUniqueId { get; set; } = "";
        public bool AllowBlankTitleBlock { get; set; }
        public ElementId AssignedViewId { get; set; } = ElementId.InvalidElementId;
        public string AssignedViewName { get; set; } = "";
        public string AssignedViewUniqueId { get; set; } = "";
        public string AssignedViewType { get; set; } = "";
        public SheetContentKind ContentKind { get; set; } = SheetContentKind.None;
        public string Discipline { get; set; } = "";
        public string DrawnBy { get; set; } = "";
        public string CheckedBy { get; set; } = "";
        public SheetGenStatusCode StatusCode { get; set; } = SheetGenStatusCode.Ready;
        public string StatusMessage { get; set; } = "";
        public ElementId PlacedContentId { get; set; } = ElementId.InvalidElementId;
        public ElementId ContentInstanceId { get; set; } = ElementId.InvalidElementId;
    }

    public enum DuplicateSheetMode { EmptySheet, DuplicateWithViews, DuplicateWithViewsAndDetails, DuplicateAsDependent }
}

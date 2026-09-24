using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.QuantityTakeoff.Models
{
    public sealed class QtoLine
    {
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public int CategoryId { get; set; }
        public string Material { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string TypeUniqueId { get; set; } = "";
        public string Level { get; set; } = "";
        public string LevelUniqueId { get; set; } = "";
        public string Unit { get; set; } = "";
        public double RawQuantity { get; set; }
        public double WastePercent { get; set; }
        public double PayQuantity { get; set; }
        public string Source { get; set; } = "";
        public string Confidence { get; set; } = "Native";
        public string RuleId { get; set; } = "";
        public string FormulaTrace { get; set; } = "";
        public int RoundingDigits { get; set; } = 3;
        public bool IsIncluded { get; set; } = true;
        public List<ElementId> ElementIds { get; } = new List<ElementId>();
        public List<string> ElementUniqueIds { get; } = new List<string>();
        public int ElementCount => ElementIds.Count;
    }

    public sealed class QtoFinding
    {
        public string Severity { get; set; } = "Warning";
        public string Check { get; set; } = "";
        public string Message { get; set; } = "";
        public int Count { get; set; }
        public List<ElementId> ElementIds { get; } = new List<ElementId>();
    }

    public sealed class QtoResult
    {
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
        public string DocumentTitle { get; set; } = "";
        public string DocumentKey { get; set; } = "";
        public int ScannedElementCount { get; set; }
        public int EligibleElementCount { get; set; }
        public int ExcludedElementCount { get; set; }
        public int MeasuredGroupCount { get; set; }
        public TimeSpan Duration { get; set; }
        public List<QtoLine> Lines { get; } = new List<QtoLine>();
        public List<QtoFinding> Findings { get; } = new List<QtoFinding>();
    }

    public sealed class QtoMeasurementRule
    {
        public bool Enabled { get; set; } = true;
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public double WastePercent { get; set; }
        public int RoundingDigits { get; set; } = 3;
    }

    public sealed class QtoRuleProfile
    {
        public int SchemaVersion { get; set; }
        public string ProfileId { get; set; } = "K-QS-DEFAULT";
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "K-QS Default Rules";
        public List<QtoMeasurementRule> Rules { get; set; } = new List<QtoMeasurementRule>();
    }

    public sealed class QtoSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public string SnapshotId { get; set; } = "";
        public string DocumentTitle { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string ProfileId { get; set; } = "";
        public int ProfileVersion { get; set; }
        public List<QtoSnapshotLine> Lines { get; set; } = new List<QtoSnapshotLine>();
    }

    public sealed class QtoSnapshotLine
    {
        public string Key { get; set; } = "";
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public int CategoryId { get; set; }
        public string Material { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string TypeUniqueId { get; set; } = "";
        public string Level { get; set; } = "";
        public string LevelUniqueId { get; set; } = "";
        public string Unit { get; set; } = "";
        public double Quantity { get; set; }
        public List<string> ElementUniqueIds { get; set; } = new List<string>();
    }

    public sealed class QtoVarianceLine
    {
        public string Key { get; set; } = "";
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public string Unit { get; set; } = "";
        public double PreviousQuantity { get; set; }
        public double CurrentQuantity { get; set; }
        public double Difference { get; set; }
        public double? DifferencePercent { get; set; }
        public string Status { get; set; } = "";
    }

    public sealed class CubicostQtoLine
    {
        public int SourceRow { get; set; }
        public string CubicostCode { get; set; } = "";
        public string RevitCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public string Unit { get; set; } = "";
        public double Quantity { get; set; }
    }

    public sealed class CubicostImportResult
    {
        public string FilePath { get; set; } = "";
        public string SheetName { get; set; } = "";
        public DateTime ImportedAt { get; set; } = DateTime.Now;
        public List<CubicostQtoLine> Lines { get; set; } = new List<CubicostQtoLine>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class QtoSourceComparison
    {
        public string RevitCode { get; set; } = "";
        public string Unit { get; set; } = "";
        public double RevitQuantity { get; set; }
        public double CubicostQuantity { get; set; }
        public double Difference { get; set; }
        public double? DifferencePercent { get; set; }
        public string Status { get; set; } = "";
    }
}

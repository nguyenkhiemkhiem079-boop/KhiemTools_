using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.QuantityTakeoff.Models
{
    public enum QsScanScope { ActiveView, Selection, EntireModel }
    public enum QsIssueSeverity { Error, Warning, Info }
    public sealed class QsElementData
    {
        public ElementId ElementId { get; set; }
        public string UniqueId { get; set; } = "";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string TypeName { get; set; } = "";
        public ElementId TypeId { get; set; }
        public ElementId LevelId { get; set; }
        public string LevelName { get; set; } = "";
        public string Mark { get; set; } = "";
        public string TypeMark { get; set; } = "";
        public double LengthInternal { get; set; }
        public double AreaInternal { get; set; }
        public double VolumeInternal { get; set; }
        public int Count { get; set; } = 1;
        public bool HasGeometry { get; set; }
        public bool IsValidForQs { get; set; } = true;
        public string SourceDocumentTitle { get; set; } = "";
        public List<string> MaterialNames { get; } = new List<string>();
    }
    public sealed class QsIssue
    {
        public QsIssueSeverity Severity { get; set; }
        public string RuleId { get; set; } = "";
        public ElementId ElementId { get; set; }
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";
        public string SuggestedAction { get; set; } = "";
    }
    public sealed class QsScanResult
    {
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public QsScanScope Scope { get; set; }
        public List<QsElementData> Elements { get; } = new List<QsElementData>();
        public List<QsIssue> Issues { get; } = new List<QsIssue>();
        public double ReadinessScore { get; set; }
        public bool TakeoffReady => !Issues.Exists(x => x.Severity == QsIssueSeverity.Error);
    }
}

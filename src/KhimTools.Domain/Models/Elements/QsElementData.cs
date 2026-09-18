using System;
using System.Collections.Generic;

namespace KhimTools.Domain.Models.Elements
{
    public enum QsScanScope
    {
        ActiveView,
        Selection,
        EntireModel
    }

    public enum QsIssueSeverity
    {
        Error,
        Warning,
        Info
    }

    public sealed class QsElementData
    {
        public long ElementId { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public long TypeId { get; set; }
        public long LevelId { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public string Mark { get; set; } = string.Empty;
        public string TypeMark { get; set; } = string.Empty;
        public double LengthInternal { get; set; }
        public double AreaInternal { get; set; }
        public double VolumeInternal { get; set; }
        public int Count { get; set; } = 1;
        public bool HasGeometry { get; set; }
        public bool IsValidForQs { get; set; } = true;
        public string SourceDocumentTitle { get; set; } = string.Empty;
        public List<string> MaterialNames { get; } = new List<string>();
        public Dictionary<string, string> CustomParameters { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class QsIssue
    {
        public QsIssueSeverity Severity { get; set; }
        public string RuleId { get; set; } = string.Empty;
        public long ElementId { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }

    public sealed class QsScanResult
    {
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public QsScanScope Scope { get; set; }
        public List<QsElementData> Elements { get; set; } = new List<QsElementData>();
        public List<QsIssue> Issues { get; set; } = new List<QsIssue>();
        public double ReadinessScore { get; set; }
        public bool TakeoffReady => !Issues.Exists(x => x.Severity == QsIssueSeverity.Error);
    }
}

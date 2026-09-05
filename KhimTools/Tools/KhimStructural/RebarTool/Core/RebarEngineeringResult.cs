using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public enum ViolationSeverity
    {
        INFO,
        WARNING,
        ERROR,
        CRITICAL
    }

    /// <summary>
    /// Mô hình vi phạm kỹ thuật cốt thép chi tiết (Section 47 Violation Model).
    /// </summary>
    public class EngineeringViolation
    {
        public ViolationSeverity Severity { get; set; } = ViolationSeverity.ERROR;
        public string Code { get; set; } = "";
        public string Category { get; set; } = "";
        public string ElementContext { get; set; } = "";
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
        public ElementId HostId { get; set; } = ElementId.InvalidElementId;
        public ElementId ConnectedHostId { get; set; } = ElementId.InvalidElementId;
        public ElementId RebarId { get; set; } = ElementId.InvalidElementId;
        public DetailingIntentType DetailingIntent { get; set; } = DetailingIntentType.StandardInternal;
        
        public double ExpectedValue { get; set; }
        public double ActualValue { get; set; }
        public string Unit { get; set; } = "mm";
        public XYZ Location { get; set; } = XYZ.Zero;
        public XYZ ViolationLocation { get => Location; set => Location = value; }
        public string Message { get; set; } = "";
        public string Description { get => Message; set => Message = value; }
        public string RecommendedAction { get; set; } = "";
        public bool IsCritical { get; set; } = true;
    }

    /// <summary>
    /// Kết quả thẩm tra kỹ thuật cốt thép toàn diện (Section 46 Engineering Result Model).
    /// </summary>
    public class RebarEngineeringResult
    {
        public bool IsValid { get; set; } = true;
        public bool GeometryValid { get; set; } = true;
        public bool HostValid { get; set; } = true;
        public bool ConnectedHostValid { get; set; } = true;
        public bool CoverValid { get; set; } = true;
        public bool SpacingValid { get; set; } = true;
        public bool AnchorageValid { get; set; } = true;
        public bool LapValid { get; set; } = true;
        public bool HookValid { get; set; } = true;
        public bool CrankValid { get; set; } = true;
        public bool ConnectionValid { get; set; } = true;
        public bool SectionValid { get; set; } = true;
        public bool ScheduleValid { get; set; } = true;
        public bool ShapeValid { get; set; } = true;

        public List<EngineeringViolation> Violations { get; set; } = new List<EngineeringViolation>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Diagnostics { get; set; } = new List<string>();
        public string FailureReason { get; set; } = "";

        public void AddViolation(
            string code,
            string category,
            string message,
            ViolationSeverity severity = ViolationSeverity.ERROR,
            XYZ location = null,
            double expected = 0,
            double actual = 0,
            string unit = "mm",
            string recommendedAction = "",
            ElementId elementId = null,
            ElementId hostId = null,
            ElementId connectedHostId = null,
            DetailingIntentType intent = DetailingIntentType.StandardInternal)
        {
            if (severity == ViolationSeverity.CRITICAL || severity == ViolationSeverity.ERROR)
            {
                IsValid = false;
            }

            var violation = new EngineeringViolation
            {
                Code = code,
                Category = category,
                Message = message,
                Severity = severity,
                Location = location ?? XYZ.Zero,
                ExpectedValue = expected,
                ActualValue = actual,
                Unit = unit,
                RecommendedAction = recommendedAction,
                ElementId = elementId ?? ElementId.InvalidElementId,
                HostId = hostId ?? ElementId.InvalidElementId,
                ConnectedHostId = connectedHostId ?? ElementId.InvalidElementId,
                DetailingIntent = intent
            };

            Violations.Add(violation);

            if (severity == ViolationSeverity.WARNING)
            {
                Warnings.Add($"[{category}] {message}");
            }
            else
            {
                if (string.IsNullOrEmpty(FailureReason))
                    FailureReason = message;
                else
                    FailureReason += $" | {message}";
            }

            Diagnostics.Add($"[{severity}][{code}] {message} (Expected: {expected}{unit}, Actual: {actual}{unit})");
        }
    }
}

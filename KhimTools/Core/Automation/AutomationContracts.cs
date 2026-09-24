using System;
using System.Collections.Generic;

namespace KhimTools.Core.Automation
{
    public enum AutomationUnitSystem
    {
        Unspecified = 0,
        NotApplicable = 1,
        Millimeters = 2,
        Meters = 3,
        Feet = 4,
        Inches = 5
    }

    public enum AutomationStatus { Succeeded, Rejected, Failed }
    public enum AutomationOperationClass { ReadOnly, PreviewOnly, Mutating }

    public sealed class AutomationCapabilityMetadata
    {
        public string CapabilityId { get; private set; }
        public string Module { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool ReadOnly { get; private set; }
        public bool MutatesDocument { get; private set; }
        public bool RequiresRevitHost { get; private set; }
        public bool RequiresSelection { get; private set; }
        public bool RequiresActiveView { get; private set; }
        public bool SupportsPreview { get; private set; }
        public bool SupportsDryRun { get; private set; }
        public AutomationOperationClass OperationClass { get; private set; }
        public string InputContract { get; private set; }
        public string OutputContract { get; private set; }

        public AutomationCapabilityMetadata(string capabilityId, string module, string name, string description,
            bool readOnly, bool mutatesDocument, bool requiresRevitHost, bool requiresSelection,
            bool requiresActiveView, bool supportsPreview, bool supportsDryRun, AutomationOperationClass operationClass, string inputContract, string outputContract)
        {
            if (capabilityId == null) throw new ArgumentNullException("capabilityId");
            if (module == null) throw new ArgumentNullException("module");
            if (name == null) throw new ArgumentNullException("name");
            if (description == null) throw new ArgumentNullException("description");
            CapabilityId = capabilityId;
            Module = module;
            Name = name;
            Description = description;
            ReadOnly = readOnly;
            MutatesDocument = mutatesDocument;
            RequiresRevitHost = requiresRevitHost;
            RequiresSelection = requiresSelection;
            RequiresActiveView = requiresActiveView;
            SupportsPreview = supportsPreview;
            SupportsDryRun = supportsDryRun;
            OperationClass = operationClass;
            if (inputContract == null) throw new ArgumentNullException("inputContract");
            if (outputContract == null) throw new ArgumentNullException("outputContract");
            InputContract = inputContract;
            OutputContract = outputContract;
        }
    }

    public abstract class AutomationRequest
    {
        public string CapabilityId { get; set; }
        public AutomationUnitSystem UnitSystem { get; set; }
        public bool DryRun { get; set; }
        public bool ExplicitExecute { get; set; }
    }

    public sealed class SheetNamingPreviewRequest : AutomationRequest
    {
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string Revision { get; set; }
        public string RevisionDate { get; set; }
        public string PaperSize { get; set; }
        public string Orientation { get; set; }
        public string ProjectCode { get; set; }
        public DateTime? IssueDate { get; set; }
        public string Expression { get; set; }
        public string RegexPattern { get; set; }
    }

    public sealed class AutomationResult
    {
        public AutomationStatus Status { get; set; }
        public string Summary { get; set; }
        public int Requested { get; set; }
        public int Affected { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public IList<string> Diagnostics { get; private set; }
        public bool Postcondition { get; set; }
        public string OutputName { get; set; }

        public AutomationResult() { Diagnostics = new List<string>(); }
    }

    public interface IAutomationCapabilityHandler
    {
        AutomationCapabilityMetadata Metadata { get; }
        AutomationResult Execute(AutomationRequest request);
    }
}

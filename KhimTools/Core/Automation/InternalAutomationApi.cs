using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.Core.Automation
{
    /// <summary>In-process, allow-listed automation dispatcher. It has no dynamic loading or command execution surface.</summary>
    public sealed class InternalAutomationApi
    {
        private readonly IDictionary<string, IAutomationCapabilityHandler> _handlers;
        private readonly bool _revitHostAvailable;

        public InternalAutomationApi(bool revitHostAvailable = false)
#if AUTOMATION_CONTRACT_TESTS
            : this(new IAutomationCapabilityHandler[0], revitHostAvailable) { }
#else
            : this(new IAutomationCapabilityHandler[] { new KhimTools.SheetExport.Services.SheetNamingPreviewCapability() }, revitHostAvailable) { }
#endif

        internal InternalAutomationApi(IEnumerable<IAutomationCapabilityHandler> handlers, bool revitHostAvailable)
        {
            if (handlers == null) throw new ArgumentNullException("handlers");
            var registered = handlers.ToList();
            if (registered.Any(handler => handler == null || handler.Metadata == null))
                throw new ArgumentException("Capability handlers and metadata must be present.", "handlers");
            if (registered.GroupBy(handler => handler.Metadata.CapabilityId, StringComparer.Ordinal).Any(group => group.Count() != 1))
                throw new ArgumentException("Capability identifiers must be unique.", "handlers");
            _handlers = registered.ToDictionary(handler => handler.Metadata.CapabilityId, StringComparer.Ordinal);
            _revitHostAvailable = revitHostAvailable;
        }

        public IReadOnlyList<AutomationCapabilityMetadata> Capabilities
        {
            get { return _handlers.Values.Select(handler => handler.Metadata).OrderBy(metadata => metadata.CapabilityId, StringComparer.Ordinal).ToList().AsReadOnly(); }
        }

        public AutomationResult Invoke(AutomationRequest request)
        {
            if (request == null) return Reject("Request is required.");
            if (string.IsNullOrWhiteSpace(request.CapabilityId)) return Reject("CapabilityId is required.");
            if (request.UnitSystem == AutomationUnitSystem.Unspecified) return Reject("UnitSystem must be explicitly specified; use NotApplicable when the capability has no dimensional inputs.");
            IAutomationCapabilityHandler handler;
            if (!_handlers.TryGetValue(request.CapabilityId, out handler)) return Reject("Unknown capability: " + request.CapabilityId);
            if (handler.Metadata.RequiresRevitHost && !_revitHostAvailable) return Reject("This capability requires an active Revit host.");
            if (request.DryRun && !handler.Metadata.SupportsDryRun) return Reject("DryRun is not supported by this capability.");
            try
            {
                var result = handler.Execute(request);
                if (result == null) return Failure("Capability returned no result.");
                if (result.Status == AutomationStatus.Succeeded && !result.Postcondition)
                {
                    result.Status = AutomationStatus.Failed;
                    result.Failed = Math.Max(1, result.Failed);
                    result.Summary = "Capability postcondition failed.";
                    result.Diagnostics.Add("POSTCONDITION_FAILED");
                }
                return result;
            }
            catch (Exception ex)
            {
                var result = new AutomationResult { Status = AutomationStatus.Failed, Summary = "Capability execution failed.", Requested = 1, Failed = 1, Postcondition = false };
                result.Diagnostics.Add(ex.Message);
                return result;
            }
        }

        private static AutomationResult Reject(string message)
        {
            var result = new AutomationResult { Status = AutomationStatus.Rejected, Summary = message, Failed = 1, Postcondition = false };
            result.Diagnostics.Add(message);
            return result;
        }

        private static AutomationResult Failure(string message)
        {
            var result = new AutomationResult { Status = AutomationStatus.Failed, Summary = message, Failed = 1, Postcondition = false };
            result.Diagnostics.Add(message);
            return result;
        }
    }
}
